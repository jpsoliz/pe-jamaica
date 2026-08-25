using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;

namespace ParcelWorkflowAddIn.Workflow.Pla;

internal sealed class PlaPlanEvidenceSelectionService
{
    public const string WorkingDirectoryName = "pla_plan_annexation";
    public const string SelectionArtifactFileName = "pla_plan_evidence_selection.json";
    public const string PdfEvidenceFileName = "pla_selected_plan.pdf";
    public const string PngEvidenceFileName = "pla_selected_plan.png";
    public const string SourceType = "st_plan_annexation_pdf";
    public const string SelectionTypeFullPage = "full_page";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPlaPlanEvidenceRenderer renderer;
    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaPlanEvidenceSelectionService()
        : this(new PythonPdfiumPlaPlanEvidenceRenderer(), () => DateTimeOffset.UtcNow)
    {
    }

    public PlaPlanEvidenceSelectionService(IPlaPlanEvidenceRenderer renderer, Func<DateTimeOffset>? getUtcNow = null)
    {
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<PlaPlanEvidenceSourceOption> BuildSourceOptions(IEnumerable<SourceFileCopyResult>? sourceFiles)
    {
        if (sourceFiles is null)
        {
            return Array.Empty<PlaPlanEvidenceSourceOption>();
        }

        return sourceFiles
            .Where(IsSelectablePlaPdf)
            .OrderBy(source => source.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(source => new PlaPlanEvidenceSourceOption(
                source.FileName,
                source.CopiedPath!,
                SourceRole.PlanAnnexationPdf,
                string.IsNullOrWhiteSpace(source.SourceType) ? SourceType : source.SourceType!,
                source))
            .ToArray();
    }

    public static PlaPlanEvidenceSelectionDocument? LoadSelection(CaseFolderLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var path = GetSelectionArtifactPath(layout);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<PlaPlanEvidenceSelectionDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null
                || !TryResolveCaseRelativePath(layout, document.SourceRelativePath, out _)
                || !TryResolveCaseRelativePath(layout, document.GeneratedPlanEvidenceRelativePath, out _))
            {
                return null;
            }

            return document.WithCaseRoot(layout.RootDirectory);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<PlaPlanEvidenceSelectionSaveResult> SaveSelectionAsync(
        CaseFolderLayout layout,
        string transactionNumber,
        PlaPlanEvidenceSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(request);

        if (request.SelectedPageNumber < 1)
        {
            return PlaPlanEvidenceSelectionSaveResult.Failed("Selected page number must be 1 or greater.");
        }

        if (!IsSelectablePlaPdf(request.SourceFile))
        {
            return PlaPlanEvidenceSelectionSaveResult.Failed("Select a copied PLA plan annexation PDF source before saving the plan evidence.");
        }

        try
        {
            if (!IsPathInside(layout.RootDirectory, request.SourceFile.CopiedPath!))
            {
                return PlaPlanEvidenceSelectionSaveResult.Failed("Selected PLA plan PDF must be copied inside the active Case Folder.");
            }

            Directory.CreateDirectory(GetWorkingDirectory(layout));

            var renderRequest = new PlaPlanEvidenceRenderRequest(
                request.SourceFile.CopiedPath!,
                request.SelectedPageNumber,
                SelectionTypeFullPage);
            var renderResult = await renderer.RenderAsync(renderRequest, cancellationToken).ConfigureAwait(false);
            if (!renderResult.Success)
            {
                return PlaPlanEvidenceSelectionSaveResult.Failed(renderResult.Message ?? "PLA plan evidence could not be generated.");
            }

            if (!TryNormalizeEvidenceFormat(renderResult.Format, out var evidenceFormat))
            {
                return PlaPlanEvidenceSelectionSaveResult.Failed("PLA plan evidence renderer returned an unsupported artifact format.");
            }

            if (renderResult.Content.Length == 0)
            {
                return PlaPlanEvidenceSelectionSaveResult.Failed("PLA plan evidence renderer returned an empty artifact.");
            }

            var evidenceFileName = string.Equals(evidenceFormat, "png", StringComparison.OrdinalIgnoreCase)
                ? PngEvidenceFileName
                : PdfEvidenceFileName;
            var evidencePath = Path.Combine(GetWorkingDirectory(layout), evidenceFileName);
            await File.WriteAllBytesAsync(evidencePath, renderResult.Content, cancellationToken).ConfigureAwait(false);
            DeleteIfExists(string.Equals(evidenceFormat, "png", StringComparison.OrdinalIgnoreCase)
                ? GetPdfEvidencePath(layout)
                : GetPngEvidencePath(layout));

            var now = getUtcNow();
            var existing = LoadSelection(layout);
            var document = new PlaPlanEvidenceSelectionDocument
            {
                SchemaVersion = "1.0.0",
                TransactionNumber = transactionNumber,
                SourceType = string.IsNullOrWhiteSpace(request.SourceFile.SourceType) ? SourceType : request.SourceFile.SourceType!,
                SourceRelativePath = ToCaseRelativePath(layout, request.SourceFile.CopiedPath!),
                SelectedPageNumber = request.SelectedPageNumber,
                SelectionType = SelectionTypeFullPage,
                SelectionRegion = null,
                PageWidthPoints = renderResult.PageWidthPoints,
                PageHeightPoints = renderResult.PageHeightPoints,
                GeneratedPlanEvidenceRelativePath = ToCaseRelativePath(layout, evidencePath),
                GeneratedPlanEvidenceFormat = evidenceFormat,
                FallbackReason = renderResult.FallbackReason,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                UpdatedAtUtc = now
            }.WithCaseRoot(layout.RootDirectory);

            await File.WriteAllTextAsync(GetSelectionArtifactPath(layout), JsonSerializer.Serialize(document, JsonOptions), cancellationToken).ConfigureAwait(false);

            return PlaPlanEvidenceSelectionSaveResult.Saved(document);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            return PlaPlanEvidenceSelectionSaveResult.Failed($"PLA plan evidence could not be saved: {exception.Message}");
        }
    }

    public static string GetWorkingDirectory(CaseFolderLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return Path.Combine(layout.WorkingDirectory, WorkingDirectoryName);
    }

    public static string GetSelectionArtifactPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), SelectionArtifactFileName);
    }

    public static string GetPdfEvidencePath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), PdfEvidenceFileName);
    }

    public static string GetPngEvidencePath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), PngEvidenceFileName);
    }

    public static bool TryResolveCaseRelativePath(CaseFolderLayout layout, string? relativePath, out string path)
    {
        path = string.Empty;
        if (layout is null || string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            return false;
        }

        try
        {
            var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var resolvedPath = Path.GetFullPath(Path.Combine(layout.RootDirectory, normalizedRelativePath));
            if (!IsPathInside(layout.RootDirectory, resolvedPath) && !string.Equals(
                    Path.GetFullPath(layout.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = resolvedPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSelectablePlaPdf(SourceFileCopyResult source)
    {
        return source.Copied
            && !string.IsNullOrWhiteSpace(source.CopiedPath)
            && File.Exists(source.CopiedPath)
            && string.Equals(Path.GetExtension(source.CopiedPath), ".pdf", StringComparison.OrdinalIgnoreCase)
            && (SourceRole.Matches(source.SourceRole, SourceRole.PlanAnnexationPdf)
                || string.Equals(source.SourceType, SourceType, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToCaseRelativePath(CaseFolderLayout layout, string path)
    {
        if (!IsPathInside(layout.RootDirectory, path))
        {
            throw new InvalidOperationException("Artifact path must stay inside the active Case Folder.");
        }

        var relativePath = Path.GetRelativePath(layout.RootDirectory, path);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool TryNormalizeEvidenceFormat(string? format, out string normalizedFormat)
    {
        normalizedFormat = string.Empty;
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            normalizedFormat = "pdf";
            return true;
        }

        if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
        {
            normalizedFormat = "png";
            return true;
        }

        return false;
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal sealed record PlaPlanEvidenceSelectionRequest(
    SourceFileCopyResult SourceFile,
    int SelectedPageNumber);

internal sealed record PlaPlanEvidenceSourceOption(
    string FileName,
    string Path,
    string SourceRole,
    string SourceType,
    SourceFileCopyResult SourceFile);

internal sealed class PlaPlanEvidenceSelectionViewModel : INotifyPropertyChanged
{
    private readonly CaseFolderLayout layout;
    private readonly string transactionNumber;
    private readonly PlaPlanEvidenceSelectionService service;
    private readonly Action<PlaPlanEvidenceSelectionSaveResult>? selectionSaved;
    private readonly RelayCommand saveSelectionCommand;
    private PlaPlanEvidenceSourceOption? selectedSource;
    private int selectedPageNumber = 1;
    private PlaPlanEvidenceSelectionDocument? selection;
    private string artifactStatusText = "No PLA plan evidence selection saved.";
    private bool isSaving;

    public PlaPlanEvidenceSelectionViewModel(
        CaseFolderLayout layout,
        string transactionNumber,
        IEnumerable<SourceFileCopyResult> sourceFiles,
        PlaPlanEvidenceSelectionService? service = null,
        Action<PlaPlanEvidenceSelectionSaveResult>? selectionSaved = null)
    {
        this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        this.transactionNumber = transactionNumber;
        this.service = service ?? new PlaPlanEvidenceSelectionService();
        this.selectionSaved = selectionSaved;
        SourceOptions = PlaPlanEvidenceSelectionService.BuildSourceOptions(sourceFiles);
        saveSelectionCommand = new RelayCommand(async () => await SaveSelectionAsync().ConfigureAwait(true), () => CanSaveSelection);

        RestoreSelection(PlaPlanEvidenceSelectionService.LoadSelection(layout));
        if (selection is null)
        {
            selectedSource ??= SourceOptions.FirstOrDefault();
        }

        RefreshStatusText();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<PlaPlanEvidenceSourceOption> SourceOptions { get; }

    public PlaPlanEvidenceSourceOption? SelectedSource
    {
        get => selectedSource;
        set
        {
            if (!Equals(selectedSource, value))
            {
                selectedSource = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(CanSaveSelection));
                saveSelectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int SelectedPageNumber
    {
        get => selectedPageNumber;
        set
        {
            if (selectedPageNumber != value)
            {
                selectedPageNumber = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(CanSaveSelection));
                saveSelectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public PlaPlanEvidenceSelectionDocument? Selection
    {
        get => selection;
        private set
        {
            if (!Equals(selection, value))
            {
                selection = value;
                NotifyPropertyChanged();
                RefreshStatusText();
            }
        }
    }

    public string ArtifactStatusText
    {
        get => artifactStatusText;
        private set
        {
            if (!string.Equals(artifactStatusText, value, StringComparison.Ordinal))
            {
                artifactStatusText = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => isSaving;
        private set
        {
            if (isSaving != value)
            {
                isSaving = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(CanSaveSelection));
                saveSelectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanSaveSelection => !IsSaving && SelectedSource is not null && SelectedPageNumber > 0;

    public ICommand SaveSelectionCommand => saveSelectionCommand;

    public async Task<PlaPlanEvidenceSelectionSaveResult> SaveSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSource is null)
        {
            ArtifactStatusText = "Select a PLA plan PDF before saving.";
            return PlaPlanEvidenceSelectionSaveResult.Failed(ArtifactStatusText);
        }

        IsSaving = true;
        try
        {
            var result = await service.SaveSelectionAsync(
                layout,
                transactionNumber,
                new PlaPlanEvidenceSelectionRequest(SelectedSource.SourceFile, SelectedPageNumber),
                cancellationToken).ConfigureAwait(true);

            Selection = result.Selection;
            ArtifactStatusText = result.Success
                ? $"Generated PLA plan evidence saved as {result.Selection!.GeneratedPlanEvidenceFormat.ToUpperInvariant()}."
                : result.Message;
            if (result.Success)
            {
                selectionSaved?.Invoke(result);
            }

            return result;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void RestoreSelection(PlaPlanEvidenceSelectionDocument? existingSelection)
    {
        if (existingSelection is null)
        {
            return;
        }

        selection = existingSelection;
        selectedPageNumber = existingSelection.SelectedPageNumber;
        selectedSource = SourceOptions.FirstOrDefault(option =>
            string.Equals(Normalize(option.Path), Normalize(existingSelection.SourcePath), StringComparison.OrdinalIgnoreCase));

        if (selectedSource is null)
        {
            var fileNameMatches = SourceOptions
                .Where(option => string.Equals(option.FileName, Path.GetFileName(existingSelection.SourceRelativePath), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            selectedSource = fileNameMatches.Length == 1 ? fileNameMatches[0] : null;
        }
    }

    private void RefreshStatusText()
    {
        ArtifactStatusText = Selection is null
            ? "No PLA plan evidence selection saved."
            : $"Generated PLA plan evidence saved as {Selection.GeneratedPlanEvidenceFormat.ToUpperInvariant()}.";
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path);
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

internal sealed record PlaPlanEvidenceRenderRequest(
    string SourcePath,
    int SelectedPageNumber,
    string SelectionType);

internal sealed class PlaPlanEvidenceRenderResult
{
    private PlaPlanEvidenceRenderResult(
        bool success,
        byte[] content,
        string format,
        int pageWidthPoints,
        int pageHeightPoints,
        string? fallbackReason,
        string? message)
    {
        Success = success;
        Content = content;
        Format = format;
        PageWidthPoints = pageWidthPoints;
        PageHeightPoints = pageHeightPoints;
        FallbackReason = fallbackReason;
        Message = message;
    }

    public bool Success { get; }

    public byte[] Content { get; }

    public string Format { get; }

    public int PageWidthPoints { get; }

    public int PageHeightPoints { get; }

    public string? FallbackReason { get; }

    public string? Message { get; }

    public static PlaPlanEvidenceRenderResult Pdf(byte[] content, int pageWidthPoints, int pageHeightPoints, string? fallbackReason = null)
    {
        return new PlaPlanEvidenceRenderResult(true, content, "pdf", pageWidthPoints, pageHeightPoints, fallbackReason, null);
    }

    public static PlaPlanEvidenceRenderResult Png(byte[] content, int pageWidthPoints, int pageHeightPoints, string fallbackReason)
    {
        return new PlaPlanEvidenceRenderResult(true, content, "png", pageWidthPoints, pageHeightPoints, fallbackReason, null);
    }

    public static PlaPlanEvidenceRenderResult Failed(string message)
    {
        return new PlaPlanEvidenceRenderResult(false, Array.Empty<byte>(), string.Empty, 0, 0, null, message);
    }
}

internal interface IPlaPlanEvidenceRenderer
{
    Task<PlaPlanEvidenceRenderResult> RenderAsync(PlaPlanEvidenceRenderRequest request, CancellationToken cancellationToken);
}

internal sealed class PythonPdfiumPlaPlanEvidenceRenderer : IPlaPlanEvidenceRenderer
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(2);
    private readonly IProcessRunner processRunner;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;

    public PythonPdfiumPlaPlanEvidenceRenderer()
        : this(new ProcessRunner(), () => WorkflowExecutionSettings.Load())
    {
    }

    public PythonPdfiumPlaPlanEvidenceRenderer(IProcessRunner processRunner, Func<WorkflowExecutionSettings> getExecutionSettings)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.getExecutionSettings = getExecutionSettings ?? throw new ArgumentNullException(nameof(getExecutionSettings));
    }

    public async Task<PlaPlanEvidenceRenderResult> RenderAsync(PlaPlanEvidenceRenderRequest request, CancellationToken cancellationToken)
    {
        var settings = getExecutionSettings();
        if (string.IsNullOrWhiteSpace(settings.PythonExecutable) || !File.Exists(settings.PythonExecutable))
        {
            return PlaPlanEvidenceRenderResult.Failed("Configured ArcGIS Python executable is not available for PLA plan page rendering.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"innola-pla-render-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(tempDirectory, "render_pla_plan_page.py");
        var outputPath = Path.Combine(tempDirectory, "selected_page.png");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            await File.WriteAllTextAsync(scriptPath, BuildRenderScript(), cancellationToken).ConfigureAwait(false);

            var arguments = string.Join(
                " ",
                Quote(scriptPath),
                Quote(request.SourcePath),
                request.SelectedPageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Quote(outputPath));
            var result = await processRunner.RunAsync(
                settings.PythonExecutable,
                arguments,
                RenderTimeout,
                environmentVariables: null,
                cancellationToken).ConfigureAwait(false);

            if (result.TimedOut)
            {
                return PlaPlanEvidenceRenderResult.Failed("PLA plan page rendering timed out.");
            }

            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                return PlaPlanEvidenceRenderResult.Failed($"PLA plan page could not be rendered. {detail.Trim()}");
            }

            var content = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
            if (!TryReadRenderDimensions(result.StandardOutput, out var width, out var height))
            {
                width = 0;
                height = 0;
            }

            return PlaPlanEvidenceRenderResult.Png(
                content,
                width,
                height,
                "Selected PLA plan page was rendered as PNG because PDF page extraction is not available in this build.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or JsonException
            or NotSupportedException
            or ArgumentException)
        {
            return PlaPlanEvidenceRenderResult.Failed($"PLA plan page could not be rendered: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private static string BuildRenderScript()
    {
        return """
import json
import sys

import pypdfium2 as pdfium

source_path = sys.argv[1]
page_number = int(sys.argv[2])
output_path = sys.argv[3]
page_index = page_number - 1

document = pdfium.PdfDocument(source_path)
if page_index < 0 or page_index >= len(document):
    raise ValueError(f"Selected page {page_number} is outside the source PDF page range.")

page = document[page_index]
width, height = page.get_size()
bitmap = page.render(scale=2).to_pil()
bitmap.save(output_path)
print(json.dumps({"width": int(round(width)), "height": int(round(height))}))
""";
    }

    private static bool TryReadRenderDimensions(string output, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        using var document = JsonDocument.Parse(output);
        if (!document.RootElement.TryGetProperty("width", out var widthElement)
            || !document.RootElement.TryGetProperty("height", out var heightElement)
            || !widthElement.TryGetInt32(out width)
            || !heightElement.TryGetInt32(out height))
        {
            return false;
        }

        return true;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}

internal sealed class PlaPlanEvidenceSelectionSaveResult
{
    private PlaPlanEvidenceSelectionSaveResult(bool success, PlaPlanEvidenceSelectionDocument? selection, string message)
    {
        Success = success;
        Selection = selection;
        Message = message;
    }

    public bool Success { get; }

    public PlaPlanEvidenceSelectionDocument? Selection { get; }

    public string Message { get; }

    public static PlaPlanEvidenceSelectionSaveResult Saved(PlaPlanEvidenceSelectionDocument selection)
    {
        return new PlaPlanEvidenceSelectionSaveResult(true, selection, "PLA plan evidence selection saved.");
    }

    public static PlaPlanEvidenceSelectionSaveResult Failed(string message)
    {
        return new PlaPlanEvidenceSelectionSaveResult(false, null, message);
    }
}

internal sealed class PlaPlanEvidenceSelectionDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1.0.0";

    [JsonPropertyName("transaction_number")]
    public string TransactionNumber { get; init; } = string.Empty;

    [JsonPropertyName("source_type")]
    public string SourceType { get; init; } = PlaPlanEvidenceSelectionService.SourceType;

    [JsonPropertyName("source_relative_path")]
    public string SourceRelativePath { get; init; } = string.Empty;

    [JsonPropertyName("selected_page_number")]
    public int SelectedPageNumber { get; init; }

    [JsonPropertyName("selection_type")]
    public string SelectionType { get; init; } = PlaPlanEvidenceSelectionService.SelectionTypeFullPage;

    [JsonPropertyName("selection_region")]
    public PlaPlanEvidenceSelectionRegion? SelectionRegion { get; init; }

    [JsonPropertyName("page_width_points")]
    public int PageWidthPoints { get; init; }

    [JsonPropertyName("page_height_points")]
    public int PageHeightPoints { get; init; }

    [JsonPropertyName("generated_plan_evidence_path")]
    public string GeneratedPlanEvidenceRelativePath { get; init; } = string.Empty;

    [JsonPropertyName("generated_plan_evidence_format")]
    public string GeneratedPlanEvidenceFormat { get; init; } = string.Empty;

    [JsonPropertyName("fallback_reason")]
    public string? FallbackReason { get; init; }

    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("updated_at_utc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    [JsonIgnore]
    public string? CaseRootDirectory { get; init; }

    [JsonIgnore]
    public string SourcePath => ResolveCasePath(SourceRelativePath);

    [JsonIgnore]
    public string GeneratedPlanEvidencePath => ResolveCasePath(GeneratedPlanEvidenceRelativePath);

    public PlaPlanEvidenceSelectionDocument WithCaseRoot(string caseRootDirectory)
    {
        return new PlaPlanEvidenceSelectionDocument
        {
            SchemaVersion = SchemaVersion,
            TransactionNumber = TransactionNumber,
            SourceType = SourceType,
            SourceRelativePath = SourceRelativePath,
            SelectedPageNumber = SelectedPageNumber,
            SelectionType = SelectionType,
            SelectionRegion = SelectionRegion,
            PageWidthPoints = PageWidthPoints,
            PageHeightPoints = PageHeightPoints,
            GeneratedPlanEvidenceRelativePath = GeneratedPlanEvidenceRelativePath,
            GeneratedPlanEvidenceFormat = GeneratedPlanEvidenceFormat,
            FallbackReason = FallbackReason,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
            CaseRootDirectory = caseRootDirectory
        };
    }

    private string ResolveCasePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(CaseRootDirectory))
        {
            return relativePath;
        }

        return Path.Combine(CaseRootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

internal sealed class PlaPlanEvidenceSelectionRegion
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("width")]
    public double Width { get; init; }

    [JsonPropertyName("height")]
    public double Height { get; init; }
}
