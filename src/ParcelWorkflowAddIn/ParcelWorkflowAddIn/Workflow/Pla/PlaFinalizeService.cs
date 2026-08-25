using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Workflow.Output;

namespace ParcelWorkflowAddIn.Workflow.Pla;

internal interface IPlaGeneratedOutputAttachmentUploader
{
    Task<PlaGeneratedOutputAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string pdfPath,
        string sourceType,
        CancellationToken cancellationToken = default);
}

internal sealed class PlaGeneratedOutputAttachmentUploader : IPlaGeneratedOutputAttachmentUploader
{
    public const string ContentType = "application/pdf";

    private readonly Func<InnolaSession?> getSession;
    private readonly IInnolaTransactionDetailService detailService;

    public PlaGeneratedOutputAttachmentUploader(
        Func<InnolaSession?> getSession,
        IInnolaTransactionDetailService detailService)
    {
        this.getSession = getSession;
        this.detailService = detailService;
    }

    public async Task<PlaGeneratedOutputAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string pdfPath,
        string sourceType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            return PlaGeneratedOutputAttachmentResult.Failed("PLA generated output PDF is missing.", "pla_output_missing");
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return PlaGeneratedOutputAttachmentResult.Failed("PLA output could not be attached because the Innola session is not available.", "session_unavailable");
        }

        try
        {
            var content = await File.ReadAllBytesAsync(pdfPath, cancellationToken).ConfigureAwait(false);
            var upload = await detailService.UploadAttachmentAsync(
                session,
                transaction,
                Path.GetFileName(pdfPath),
                ContentType,
                content,
                sourceType,
                cancellationToken).ConfigureAwait(false);

            return upload.Success
                ? PlaGeneratedOutputAttachmentResult.Succeeded(sourceType, pdfPath)
                : PlaGeneratedOutputAttachmentResult.Failed(
                    upload.ErrorMessage ?? "PLA generated output could not be attached to the transaction.",
                    upload.ErrorCategory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return PlaGeneratedOutputAttachmentResult.Failed("PLA generated output could not be attached. Try again.", exception.GetType().Name);
        }
    }
}

internal sealed class PlaFinalizeService
{
    public const string EvidenceFileName = "pla_finalize_upload.json";
    public const string UploadedStatus = "uploaded";
    public const string FailedStatus = "failed";
    public const string PendingStatus = "pending";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPlaGeneratedOutputAttachmentUploader uploader;
    private readonly Func<InnolaTransactionSettings> settingsProvider;
    private readonly OutputSummaryPersistenceService outputSummaryPersistenceService;
    private readonly PlaVisualComparisonService visualComparisonService;
    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaFinalizeService(
        IPlaGeneratedOutputAttachmentUploader uploader,
        Func<InnolaTransactionSettings>? settingsProvider = null,
        OutputSummaryPersistenceService? outputSummaryPersistenceService = null,
        PlaVisualComparisonService? visualComparisonService = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.uploader = uploader;
        this.settingsProvider = settingsProvider ?? InnolaTransactionSettings.Load;
        this.outputSummaryPersistenceService = outputSummaryPersistenceService ?? new OutputSummaryPersistenceService();
        this.visualComparisonService = visualComparisonService ?? new PlaVisualComparisonService(getUtcNow);
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public PlaFinalizeReadinessResult CheckReadiness(CaseFolderLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!File.Exists(layout.ManifestPath))
        {
            return PlaFinalizeReadinessResult.Blocked("manifest_missing", "Finalize is blocked because the case manifest is missing.");
        }

        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        if (!IsPlaWorkflow(manifest))
        {
            return PlaFinalizeReadinessResult.Ready("not_pla");
        }

        var selection = PlaPlanEvidenceSelectionService.LoadSelection(layout);
        if (selection is null
            || !PlaPlanEvidenceSelectionService.TryResolveCaseRelativePath(layout, selection.GeneratedPlanEvidenceRelativePath, out var selectionEvidencePath)
            || !File.Exists(selectionEvidencePath))
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_plan_evidence_missing",
                "Finalize is blocked until saved PLA selected-plan evidence is available.");
        }

        var visualComparison = visualComparisonService.Load(layout);
        if (visualComparison is null || string.IsNullOrWhiteSpace(visualComparison.ReviewerDecision))
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_visual_review_missing",
                "Finalize is blocked until PLA visual comparison review is accepted or flagged.");
        }

        if (string.Equals(visualComparison.ReviewerDecision, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_visual_review_rejected",
                "Finalize is blocked because the PLA visual comparison is rejected.");
        }

        var outputSummary = outputSummaryPersistenceService.Load(layout);
        if (outputSummary is null)
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_output_summary_missing",
                "Finalize is blocked until PLA output documents are generated.");
        }

        var outputDocuments = ResolveGeneratedOutputDocuments(layout, outputSummary);
        if (outputDocuments.Count == 0)
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_output_documents_missing",
                "Finalize is blocked until at least one generated PLA output PDF exists.");
        }

        var sourceTypes = PlaOutputDocumentSourceTypeResolver.Resolve(settingsProvider(), outputDocuments.Count);
        if (!sourceTypes.Success)
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_output_source_type_unavailable",
                sourceTypes.Diagnostic ?? "Finalize is blocked because PLA output source types are not configured.");
        }

        return PlaFinalizeReadinessResult.Ready("ready");
    }

    public async Task<PlaFinalizeResult> UploadGeneratedOutputsAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(transaction);

        var readiness = CheckReadiness(layout);
        if (!readiness.IsReady)
        {
            WriteEvidence(layout, transaction, Array.Empty<PlaFinalizeUploadItem>(), FailedStatus, readiness.Message, readiness.Reason, operatorId);
            return PlaFinalizeResult.Failed(readiness.Message, readiness.Reason);
        }

        var outputSummary = outputSummaryPersistenceService.Load(layout)!;
        var outputDocuments = ResolveGeneratedOutputDocuments(layout, outputSummary);
        var sourceTypes = PlaOutputDocumentSourceTypeResolver.Resolve(settingsProvider(), outputDocuments.Count);
        if (!sourceTypes.Success)
        {
            WriteEvidence(layout, transaction, Array.Empty<PlaFinalizeUploadItem>(), FailedStatus, sourceTypes.Diagnostic, "pla_output_source_type_unavailable", operatorId);
            return PlaFinalizeResult.Failed(sourceTypes.Diagnostic ?? "PLA output source types are not configured.", "pla_output_source_type_unavailable");
        }

        var items = outputDocuments
            .Select((path, index) => new PlaFinalizeUploadItem(
                Path.GetFileName(path),
                ToCaseRelativePath(layout, path),
                sourceTypes.SourceTypes[index],
                PendingStatus,
                null,
                null,
                File.Exists(path) ? new FileInfo(path).Length : null))
            .ToList();
        WriteEvidence(layout, transaction, items, PendingStatus, "PLA output upload started.", null, operatorId);

        for (var index = 0; index < outputDocuments.Count; index++)
        {
            var upload = await uploader.UploadAsync(
                transaction,
                outputDocuments[index],
                sourceTypes.SourceTypes[index],
                cancellationToken).ConfigureAwait(false);

            items[index] = items[index] with
            {
                UploadStatus = upload.Success ? UploadedStatus : FailedStatus,
                ErrorCategory = upload.ErrorCategory,
                Message = upload.Message
            };

            if (!upload.Success)
            {
                var message = SanitizeUploadDiagnostic(upload.Message);
                WriteEvidence(layout, transaction, items, FailedStatus, message, upload.ErrorCategory, operatorId);
                return PlaFinalizeResult.Failed(message, upload.ErrorCategory);
            }
        }

        WriteEvidence(layout, transaction, items, UploadedStatus, "PLA generated output documents attached to the transaction.", null, operatorId);
        return PlaFinalizeResult.Succeeded(sourceTypes.SourceTypes, outputDocuments);
    }

    public PlaFinalizeEvidenceDocument? LoadEvidence(CaseFolderLayout layout)
    {
        var path = GetEvidencePath(layout);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlaFinalizeEvidenceDocument>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static string GetEvidencePath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, EvidenceFileName);
    }

    public static bool IsPlaWorkflow(ManifestDocument manifest)
    {
        return string.Equals(manifest.Payload.WorkflowProfile, SourceInputProfile.PlaPlanAnnexation, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.Payload.DetectedProfile?.ProfileCode, SourceInputProfile.PlaPlanAnnexation, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.Payload.TransactionTypeProfile?.WorkflowProfile, SourceInputProfile.PlaPlanAnnexation, StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> ResolveGeneratedOutputDocuments(
        CaseFolderLayout layout,
        OutputSummaryDocument outputSummary)
    {
        var candidates = outputSummary.Payload.ArtifactPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ResolveCasePath(layout, path))
            .Where(path => path is not null)
            .Cast<string>()
            .Where(path => File.Exists(path)
                && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)
                && IsInsideCase(layout, path)
                && !IsKnownNonPlaOutput(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates;
    }

    private void WriteEvidence(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        IReadOnlyList<PlaFinalizeUploadItem> uploadItems,
        string uploadStatus,
        string? message,
        string? errorCategory,
        string? operatorId)
    {
        try
        {
            Directory.CreateDirectory(layout.WorkingDirectory);
            var document = new PlaFinalizeEvidenceDocument(
                "1.0.0",
                transaction.TransactionId,
                transaction.TransactionNumber,
                transaction.TaskId,
                operatorId,
                getUtcNow().UtcDateTime.ToString("O"),
                uploadStatus,
                uploadItems,
                errorCategory,
                string.IsNullOrWhiteSpace(message) ? null : SanitizeUploadDiagnostic(message));
            File.WriteAllText(GetEvidencePath(layout), JsonSerializer.Serialize(document, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine($"PLA finalize evidence write failed: {exception.GetType().Name}.");
        }
    }

    private static string? ResolveCasePath(CaseFolderLayout layout, string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(layout.RootDirectory, path.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsInsideCase(CaseFolderLayout layout, string path)
    {
        var root = Path.GetFullPath(layout.RootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownNonPlaOutput(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Contains("compute_examination_report", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("compare", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToCaseRelativePath(CaseFolderLayout layout, string path)
    {
        return Path.GetRelativePath(layout.RootDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string SanitizeUploadDiagnostic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PLA output upload failed. Try again.";
        }

        return value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            ? "PLA output upload failed. Sensitive diagnostic was redacted."
            : value;
    }
}

internal sealed record PlaGeneratedOutputAttachmentResult(
    bool Success,
    string Message,
    string? SourceType,
    string? PdfPath,
    string? ErrorCategory = null)
{
    public static PlaGeneratedOutputAttachmentResult Succeeded(string sourceType, string pdfPath)
    {
        return new PlaGeneratedOutputAttachmentResult(true, "PLA generated output attached to the transaction.", sourceType, pdfPath);
    }

    public static PlaGeneratedOutputAttachmentResult Failed(string message, string? errorCategory = null)
    {
        return new PlaGeneratedOutputAttachmentResult(false, message, null, null, errorCategory);
    }
}

internal sealed record PlaFinalizeResult(
    bool Success,
    string Message,
    IReadOnlyList<string> SourceTypes,
    IReadOnlyList<string> OutputDocumentPaths,
    string? ErrorCategory = null)
{
    public static PlaFinalizeResult Succeeded(IReadOnlyList<string> sourceTypes, IReadOnlyList<string> outputDocumentPaths)
    {
        return new PlaFinalizeResult(true, "PLA generated output documents attached to the transaction.", sourceTypes, outputDocumentPaths);
    }

    public static PlaFinalizeResult Failed(string message, string? errorCategory = null)
    {
        return new PlaFinalizeResult(false, message, Array.Empty<string>(), Array.Empty<string>(), errorCategory);
    }
}

internal sealed record PlaFinalizeReadinessResult(
    bool IsReady,
    string Reason,
    string Message)
{
    public static PlaFinalizeReadinessResult Ready(string reason)
    {
        return new PlaFinalizeReadinessResult(true, reason, "PLA Finalize is ready.");
    }

    public static PlaFinalizeReadinessResult Blocked(string reason, string message)
    {
        return new PlaFinalizeReadinessResult(false, reason, message);
    }
}

internal sealed record PlaFinalizeEvidenceDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string? TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string? TaskId,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("written_at_utc")] string WrittenAtUtc,
    [property: JsonPropertyName("upload_status")] string UploadStatus,
    [property: JsonPropertyName("upload_items")] IReadOnlyList<PlaFinalizeUploadItem> UploadItems,
    [property: JsonPropertyName("error_category")] string? ErrorCategory,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record PlaFinalizeUploadItem(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("relative_path")] string RelativePath,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("upload_status")] string UploadStatus,
    [property: JsonPropertyName("error_category")] string? ErrorCategory,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("file_size")] long? FileSize);
