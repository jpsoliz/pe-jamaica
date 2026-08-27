using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Enterprise.PortalAuth;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;
using ParcelWorkflowAddIn.Workflow.Maps;

namespace ParcelWorkflowAddIn.Workflow.Pla;

internal static class PlaBWorkflowConstants
{
    public const string WorkflowProfile = SourceInputProfile.PlaBPlanAnnexationFromPe;
    public const string WorkingDirectoryName = "pla_b";
    public const string SurveyDiagramSourceType = "st_survey_diagram";
    public const string SurveyDiagramSourceRole = SourceRole.SurveyDiagramPdf;
    public const string SurveyDiagramPngOutputSourceType = "st_survey_diagram_png";
    public const string PlanAnnexImageSourceType = "st_plan_annex_image";
    public const string SelectionPngFileName = "survey_diagram_selection.png";
    public const string SelectionMetadataFileName = "survey_diagram_selection.json";
    public const string FinalizeEvidenceFileName = "pla_b_finalize_upload.json";
    public const string PackageDirectoryName = "pe_package";
}

internal static class PlaBPeNumberNormalizer
{
    public static PlaBPeNumberNormalizationResult Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PlaBPeNumberNormalizationResult.Failed(
                "pe_number_missing",
                "PLA_B requires Innola field PeNumber.");
        }

        var text = value.Trim();
        if (text.StartsWith("PE-", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..].Trim();
        }

        return text.Length > 0 && text.All(char.IsDigit)
            ? PlaBPeNumberNormalizationResult.Succeeded(text)
            : PlaBPeNumberNormalizationResult.Failed(
                "pe_number_invalid",
                "PLA_B PeNumber must be numeric after removing the PE- prefix.");
    }
}

internal static class PlaBTestEmulationContext
{
    private static PlaBTestEmulationValues? current;

    public static void Set(string currentTransactionNumber, string peNumber)
    {
        var normalized = PlaBPeNumberNormalizer.Normalize(peNumber);
        current = new PlaBTestEmulationValues(
            currentTransactionNumber.Trim(),
            normalized.PeNumber ?? peNumber.Trim());
    }

    public static PlaBTestEmulationValues? GetForTransaction(string? transactionNumber)
    {
        if (current is null || string.IsNullOrWhiteSpace(transactionNumber))
        {
            return null;
        }

        return TransactionNumbersMatch(current.CurrentTransactionNumber, transactionNumber)
            ? current
            : null;
    }

    private static bool TransactionNumbersMatch(string left, string right)
    {
        return NormalizeTransactionNumber(left).Equals(NormalizeTransactionNumber(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTransactionNumber(string value)
    {
        var text = value.Trim();
        return text.StartsWith("TR", StringComparison.OrdinalIgnoreCase)
            ? text[2..].Trim()
            : text;
    }
}

internal sealed record PlaBTestEmulationValues(
    string CurrentTransactionNumber,
    string PeNumber);

public sealed record PlaBTestInputPreparationResult(
    bool Success,
    string Message)
{
    public static PlaBTestInputPreparationResult Succeeded(string message) => new(true, message);

    public static PlaBTestInputPreparationResult Failed(string message) => new(false, message);
}

internal sealed record PlaBPeNumberNormalizationResult(
    bool Success,
    string? PeNumber,
    string? ErrorCode,
    string? Message)
{
    public static PlaBPeNumberNormalizationResult Succeeded(string peNumber)
    {
        return new PlaBPeNumberNormalizationResult(true, peNumber, null, null);
    }

    public static PlaBPeNumberNormalizationResult Failed(string errorCode, string message)
    {
        return new PlaBPeNumberNormalizationResult(false, null, errorCode, message);
    }
}

internal static class PlaBWorkflowInputResolver
{
    public static PlaBWorkflowInputResult Resolve(InnolaTransactionDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var peValue = ReadCustomField(detail, "PeNumber");
        var normalized = PlaBPeNumberNormalizer.Normalize(peValue);
        if (!normalized.Success)
        {
            return PlaBWorkflowInputResult.Failed(normalized.ErrorCode!, normalized.Message!);
        }

        return PlaBWorkflowInputResult.Succeeded(normalized.PeNumber!, null);
    }

    private static string? ReadCustomField(InnolaTransactionDetail detail, string key)
    {
        if (detail.CustomFields is not null
            && detail.CustomFields.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }

    private static bool IsSurveyDiagramPdf(InnolaAttachmentMetadata attachment)
    {
        return string.Equals(attachment.SourceType, PlaBWorkflowConstants.SurveyDiagramSourceType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(attachment.Extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}

internal interface IPlaBRelatedPeTransactionFinder
{
    Task<PlaBRelatedPeTransactionResult> FindAsync(
        InnolaSession session,
        string peNumber,
        CancellationToken cancellationToken = default);
}

internal sealed class PlaBRelatedPeTransactionFinder : IPlaBRelatedPeTransactionFinder
{
    private readonly IInnolaTransactionService transactionService;
    private readonly Func<string> processStepProvider;

    public PlaBRelatedPeTransactionFinder(
        IInnolaTransactionService transactionService,
        Func<string>? processStepProvider = null)
    {
        this.transactionService = transactionService;
        this.processStepProvider = processStepProvider ?? new Func<string>(() => ShellState.TransactionProcessStep);
    }

    public async Task<PlaBRelatedPeTransactionResult> FindAsync(
        InnolaSession session,
        string peNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = PlaBPeNumberNormalizer.Normalize(peNumber);
        if (!normalized.Success)
        {
            return PlaBRelatedPeTransactionResult.Failed(normalized.ErrorCode!, normalized.Message!);
        }

        if (string.IsNullOrWhiteSpace(session.ServerUrl) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return PlaBRelatedPeTransactionResult.Failed("session_unavailable", "Related PE transaction lookup requires an active Innola session.");
        }

        var result = await transactionService.GetAvailableTransactionsAsync(
            new InnolaTransactionQuery(
                session.ServerUrl,
                session.AccessToken,
                session.Username,
                session.User.Groups,
                processStepProvider(),
                null,
                normalized.PeNumber,
                null,
                null),
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return PlaBRelatedPeTransactionResult.Failed(result.ErrorCategory ?? "pe_lookup_failed", result.ErrorMessage ?? "Related PE transaction lookup failed.");
        }

        var matches = result.Rows
            .Where(row => string.Equals(row.TransactionNumber, normalized.PeNumber, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            1 => PlaBRelatedPeTransactionResult.Succeeded(matches[0]),
            0 => PlaBRelatedPeTransactionResult.Failed("pe_transaction_missing", $"No PE transaction was found for {normalized.PeNumber}."),
            _ => PlaBRelatedPeTransactionResult.Failed("pe_transaction_multiple", $"Multiple PE transactions were found for {normalized.PeNumber}.")
        };
    }
}

internal sealed record PlaBRelatedPeTransactionResult(
    bool Success,
    InnolaTransactionRow? Transaction,
    string? ErrorCode,
    string? Message)
{
    public static PlaBRelatedPeTransactionResult Succeeded(InnolaTransactionRow transaction)
    {
        return new PlaBRelatedPeTransactionResult(true, transaction, null, null);
    }

    public static PlaBRelatedPeTransactionResult Failed(string errorCode, string message)
    {
        return new PlaBRelatedPeTransactionResult(false, null, errorCode, message);
    }
}

internal interface IPlaBPePackageDownloader
{
    Task<PlaBPePackageDownloadResult> DownloadAsync(
        InnolaSession session,
        InnolaTransactionDetail detail,
        CaseFolderLayout layout,
        CancellationToken cancellationToken = default);
}

internal sealed class PlaBPePackageDownloader : IPlaBPePackageDownloader
{
    private readonly IInnolaTransactionDetailService detailService;

    public PlaBPePackageDownloader(IInnolaTransactionDetailService detailService)
    {
        this.detailService = detailService;
    }

    public async Task<PlaBPePackageDownloadResult> DownloadAsync(
        InnolaSession session,
        InnolaTransactionDetail detail,
        CaseFolderLayout layout,
        CancellationToken cancellationToken = default)
    {
        var attachment = detail.Attachments.FirstOrDefault(IsSurveyPlanPackage);
        if (attachment is null)
        {
            return PlaBPePackageDownloadResult.Failed("package_missing", "Related PE transaction does not contain a survey_plan zip/GDB package.");
        }

        var content = await detailService.GetAttachmentContentAsync(session, detail, attachment, cancellationToken).ConfigureAwait(false);
        if (!content.Success)
        {
            return PlaBPePackageDownloadResult.Failed(content.ErrorCode ?? "package_download_failed", content.ErrorMessage ?? "Related PE package could not be downloaded.");
        }

        var packageDirectory = Path.Combine(layout.WorkingDirectory, PlaBWorkflowConstants.WorkingDirectoryName, PlaBWorkflowConstants.PackageDirectoryName);
        Directory.CreateDirectory(packageDirectory);
        var packagePath = Path.Combine(packageDirectory, SanitizeFileName(attachment.FileName));
        await File.WriteAllBytesAsync(packagePath, content.Content, cancellationToken).ConfigureAwait(false);
        return PlaBPePackageDownloadResult.Succeeded(packagePath, attachment);
    }

    private static bool IsSurveyPlanPackage(InnolaAttachmentMetadata attachment)
    {
        var text = $"{attachment.FileName} {attachment.Category} {attachment.SourceType} {attachment.SourceRole}";
        return string.Equals(attachment.Extension, ".zip", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("survey_plan", StringComparison.OrdinalIgnoreCase)
                || text.Contains("survey plan", StringComparison.OrdinalIgnoreCase)
                || SourceRole.Matches(attachment.SourceRole, SourceRole.WorkflowResumePackage)
                || string.Equals(attachment.SourceType, "st_survey_zip", StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeFileName(string fileName)
    {
        var safe = string.Concat(fileName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return string.IsNullOrWhiteSpace(safe) ? "survey_plan.zip" : safe;
    }
}

internal sealed record PlaBPePackageDownloadResult(
    bool Success,
    string? PackagePath,
    InnolaAttachmentMetadata? Attachment,
    string? ErrorCode,
    string? Message)
{
    public static PlaBPePackageDownloadResult Succeeded(string packagePath, InnolaAttachmentMetadata attachment)
    {
        return new PlaBPePackageDownloadResult(true, packagePath, attachment, null, null);
    }

    public static PlaBPePackageDownloadResult Failed(string errorCode, string message)
    {
        return new PlaBPePackageDownloadResult(false, null, null, errorCode, message);
    }
}

internal static class PlaBEnterpriseWorkingLayerLookupPlanner
{
    public static PlaBEnterpriseWorkingLayerLookupPlan Build(InnolaTransactionSettings settings, string peNumber)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = PlaBPeNumberNormalizer.Normalize(peNumber);
        if (!normalized.Success)
        {
            return PlaBEnterpriseWorkingLayerLookupPlan.Failed(normalized.ErrorCode!, normalized.Message!);
        }

        var review = settings.EnterpriseWorkingReview;
        var scopeField = string.IsNullOrWhiteSpace(review.TransactionScopeField)
            ? EnterpriseWorkingReviewSettings.Default.TransactionScopeField
            : review.TransactionScopeField.Trim();
        if (string.IsNullOrWhiteSpace(scopeField))
        {
            return PlaBEnterpriseWorkingLayerLookupPlan.Failed(
                "enterprise_scope_field_missing",
                "Enterprise working-layer lookup requires a transaction scope field.");
        }

        var layers = new[]
            {
                review.Layers.Polygons,
                review.Layers.Lines,
                review.Layers.Points
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
        if (layers.Length == 0)
        {
            return PlaBEnterpriseWorkingLayerLookupPlan.Failed(
                "enterprise_working_layers_missing",
                "Enterprise working-layer lookup requires at least one configured geometry layer target.");
        }

        return PlaBEnterpriseWorkingLayerLookupPlan.Succeeded(scopeField, normalized.PeNumber!, layers);
    }
}

internal sealed record PlaBEnterpriseWorkingLayerLookupPlan(
    bool Success,
    string? ScopeField,
    string? ScopeValue,
    IReadOnlyList<string> LayerTargets,
    string? ErrorCode,
    string? Message)
{
    public static PlaBEnterpriseWorkingLayerLookupPlan Succeeded(
        string scopeField,
        string scopeValue,
        IReadOnlyList<string> layerTargets)
    {
        return new PlaBEnterpriseWorkingLayerLookupPlan(true, scopeField, scopeValue, layerTargets, null, null);
    }

    public static PlaBEnterpriseWorkingLayerLookupPlan Failed(string errorCode, string message)
    {
        return new PlaBEnterpriseWorkingLayerLookupPlan(false, null, null, Array.Empty<string>(), errorCode, message);
    }
}

internal sealed record PlaBWorkflowInputResult(
    bool Success,
    string? PeNumber,
    InnolaAttachmentMetadata? SurveyDiagramAttachment,
    string? ErrorCode,
    string? Message)
{
    public static PlaBWorkflowInputResult Succeeded(string peNumber, InnolaAttachmentMetadata? surveyDiagramAttachment)
    {
        return new PlaBWorkflowInputResult(true, peNumber, surveyDiagramAttachment, null, null);
    }

    public static PlaBWorkflowInputResult Failed(string errorCode, string message)
    {
        return new PlaBWorkflowInputResult(false, null, null, errorCode, message);
    }
}

internal static class PlaBPackageService
{
    public static PlaBPackagePreparationResult ExtractAndResolveOutputGdb(
        CaseFolderLayout layout,
        string packageZipPath,
        string peNumber)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (string.IsNullOrWhiteSpace(packageZipPath) || !File.Exists(packageZipPath))
        {
            return PlaBPackagePreparationResult.Failed("package_missing", "Related PE survey_plan package is missing.");
        }

        var normalized = PlaBPeNumberNormalizer.Normalize(peNumber);
        if (!normalized.Success)
        {
            return PlaBPackagePreparationResult.Failed(normalized.ErrorCode!, normalized.Message!);
        }

        var packageRoot = Path.Combine(layout.WorkingDirectory, PlaBWorkflowConstants.WorkingDirectoryName, PlaBWorkflowConstants.PackageDirectoryName);
        try
        {
            Directory.CreateDirectory(packageRoot);
            ExtractZipInsideDirectory(packageZipPath, packageRoot);
        }
        catch (InvalidDataException)
        {
            return PlaBPackagePreparationResult.Failed("package_corrupt", "Related PE survey_plan package could not be unzipped.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return PlaBPackagePreparationResult.Failed("package_extract_failed", $"Related PE survey_plan package could not be prepared: {exception.Message}");
        }

        var expectedName = $"{normalized.PeNumber}_parcel_output.gdb";
        var gdb = Directory.EnumerateDirectories(packageRoot, "*.gdb", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(path => IsInside(layout.RootDirectory, path))
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase));

        return gdb is null
            ? PlaBPackagePreparationResult.Failed(
                "matching_gdb_missing",
                $"Related PE package does not contain expected geodatabase {expectedName}.")
            : PlaBPackagePreparationResult.Succeeded(gdb, packageRoot);
    }

    private static void ExtractZipInsideDirectory(string packageZipPath, string packageRoot)
    {
        using var archive = ZipFile.OpenRead(packageZipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
            {
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(packageRoot, entry.FullName));
            if (!IsInside(packageRoot, destinationPath) && !IsSamePath(packageRoot, destinationPath))
            {
                throw new IOException("Related PE package contains an unsafe archive path.");
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInside(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record PlaBPackagePreparationResult(
    bool Success,
    string? GdbPath,
    string? ExtractedPackageDirectory,
    string? ErrorCode,
    string? Message)
{
    public static PlaBPackagePreparationResult Succeeded(string gdbPath, string extractedPackageDirectory)
    {
        return new PlaBPackagePreparationResult(true, gdbPath, extractedPackageDirectory, null, null);
    }

    public static PlaBPackagePreparationResult Failed(string errorCode, string message)
    {
        return new PlaBPackagePreparationResult(false, null, null, errorCode, message);
    }
}

internal static class PlaBMapReviewPlanner
{
    public static PlaBMapReviewPlan Build(string? currentTransactionNumber, string? peNumber, string? peOutputGdbPath)
    {
        if (string.IsNullOrWhiteSpace(currentTransactionNumber))
        {
            return PlaBMapReviewPlan.Failed("current_transaction_missing", "Current PLA transaction number is required.");
        }

        var normalized = PlaBPeNumberNormalizer.Normalize(peNumber);
        if (!normalized.Success)
        {
            return PlaBMapReviewPlan.Failed(normalized.ErrorCode!, normalized.Message!);
        }

        if (string.IsNullOrWhiteSpace(peOutputGdbPath))
        {
            return PlaBMapReviewPlan.Failed("pe_gdb_missing", "Related PE output geodatabase is required.");
        }

        var currentGroup = $"PLA {currentTransactionNumber.Trim()} - Current Transaction";
        var peGroup = $"PE {normalized.PeNumber} - Approved PE Output";
        return PlaBMapReviewPlan.Succeeded(
            currentGroup,
            peGroup,
            new[]
            {
                new PlaBMapReviewGroupPlan(currentGroup, "enterprise_working_layer", Array.Empty<string>()),
                new PlaBMapReviewGroupPlan(peGroup, "pe_output_gdb", new[] { peOutputGdbPath.Trim() })
            });
    }
}

internal sealed record PlaBMapReviewPlan(
    bool Success,
    string? CurrentTransactionGroupName,
    string? PeTransactionGroupName,
    IReadOnlyList<PlaBMapReviewGroupPlan> Groups,
    string? ErrorCode,
    string? Message)
{
    public static PlaBMapReviewPlan Succeeded(
        string currentTransactionGroupName,
        string peTransactionGroupName,
        IReadOnlyList<PlaBMapReviewGroupPlan> groups)
    {
        return new PlaBMapReviewPlan(true, currentTransactionGroupName, peTransactionGroupName, groups, null, null);
    }

    public static PlaBMapReviewPlan Failed(string errorCode, string message)
    {
        return new PlaBMapReviewPlan(false, null, null, Array.Empty<PlaBMapReviewGroupPlan>(), errorCode, message);
    }
}

internal sealed record PlaBMapReviewGroupPlan(
    string GroupName,
    string SourceKind,
    IReadOnlyList<string> LayerPaths);

internal sealed class ArcGisPlaBMapRecoveryLoader
{
    private readonly IPortalAuthProvider portalAuthProvider;
    private readonly IWorkingMapPreparationService workingMapPreparationService;

    public ArcGisPlaBMapRecoveryLoader()
        : this(CompositePortalAuthProvider.CreateDefault(), new ArcGisWorkingMapPreparationService())
    {
    }

    public ArcGisPlaBMapRecoveryLoader(
        IPortalAuthProvider portalAuthProvider,
        IWorkingMapPreparationService workingMapPreparationService)
    {
        this.portalAuthProvider = portalAuthProvider;
        this.workingMapPreparationService = workingMapPreparationService;
    }

    public async Task<PlaBMapRecoveryLoadResult> LoadAsync(
        InnolaTransactionSettings settings,
        InnolaTransactionDetail peTransactionDetail,
        PlaBEnterpriseWorkingLayerLookupPlan enterprisePlan,
        PlaBMapReviewPlan mapPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(peTransactionDetail);
        ArgumentNullException.ThrowIfNull(enterprisePlan);
        ArgumentNullException.ThrowIfNull(mapPlan);

        if (!enterprisePlan.Success)
        {
            return PlaBMapRecoveryLoadResult.Failed(enterprisePlan.Message ?? "PLA_B working_review plan is invalid.");
        }

        if (!mapPlan.Success)
        {
            return PlaBMapRecoveryLoadResult.Failed(mapPlan.Message ?? "PLA_B map plan is invalid.");
        }

        var mapPreparation = await workingMapPreparationService
            .PrepareWorkingMapAsync(settings.WorkingMap, peTransactionDetail, cancellationToken)
            .ConfigureAwait(false);
        if (!mapPreparation.Success)
        {
            return PlaBMapRecoveryLoadResult.Failed(mapPreparation.Message);
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return PlaBMapRecoveryLoadResult.Failed("No active ArcGIS Pro map is available after working map preparation.");
        }

        var auth = await TryAuthenticateAsync(settings, enterprisePlan, cancellationToken).ConfigureAwait(false);
        if (!auth.Success)
        {
            return PlaBMapRecoveryLoadResult.Failed(auth.ErrorMessage ?? "ArcGIS Portal authentication failed for PLA_B working_review layers.");
        }

        var loadedLayerPaths = new List<string>();
        var zoomLayers = new List<Layer>();
        try
        {
            await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentGroup = EnsureGroupLayer(mapView.Map, mapPlan.CurrentTransactionGroupName!);
                var peGroup = EnsureGroupLayer(mapView.Map, mapPlan.PeTransactionGroupName!);
                ClearGroupLayer(mapView.Map, currentGroup);
                ClearGroupLayer(mapView.Map, peGroup);

                foreach (var layerTarget in enterprisePlan.LayerTargets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var layer = LayerFactory.Instance.CreateLayer(new Uri(layerTarget), currentGroup);
                    if (layer is FeatureLayer featureLayer)
                    {
                        featureLayer.SetDefinitionQuery(BuildDefinitionQuery(enterprisePlan.ScopeField!, enterprisePlan.ScopeValue!));
                        featureLayer.SetEditable(false);
                    }

                    loadedLayerPaths.Add(layerTarget);
                    zoomLayers.Add(layer);
                }

                foreach (var layerPath in EnumerateGeodatabaseLayerPaths(mapPlan.Groups
                             .Where(group => string.Equals(group.SourceKind, "pe_output_gdb", StringComparison.OrdinalIgnoreCase))
                             .SelectMany(group => group.LayerPaths)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var layer = LayerFactory.Instance.CreateLayer(new Uri(layerPath), peGroup);
                    if (IsMGeoLayerPath(layerPath))
                    {
                        layer.SetTransparency(70);
                    }

                    if (layer is FeatureLayer featureLayer)
                    {
                        featureLayer.SetEditable(false);
                    }

                    loadedLayerPaths.Add(layerPath);
                    zoomLayers.Add(layer);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or UriFormatException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            return PlaBMapRecoveryLoadResult.Failed($"PLA_B recovery layers could not be loaded into the map: {exception.Message}");
        }

        if (loadedLayerPaths.Count == 0)
        {
            return PlaBMapRecoveryLoadResult.Failed("PLA_B recovery did not find any map-loadable layers.");
        }

        try
        {
            await mapView.ZoomToAsync(zoomLayers).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return PlaBMapRecoveryLoadResult.Succeeded(
                $"PLA_B recovery layers were loaded, but automatic zoom failed: {exception.Message}",
                loadedLayerPaths);
        }

        return PlaBMapRecoveryLoadResult.Succeeded("PLA_B recovery layers were loaded into the active map.", loadedLayerPaths);
    }

    private async Task<PortalAuthResult> TryAuthenticateAsync(
        InnolaTransactionSettings settings,
        PlaBEnterpriseWorkingLayerLookupPlan enterprisePlan,
        CancellationToken cancellationToken)
    {
        var portalUrl = CompareWorkingGeometryService.ResolvePortalUrl(settings.EnterpriseWorkingReview.ServiceRoot);
        if (string.IsNullOrWhiteSpace(portalUrl))
        {
            return PortalAuthResult.Succeeded("arcgis-pro-session", "not_required");
        }

        return await portalAuthProvider.GetTokenAsync(
            new PortalAuthRequest(portalUrl, enterprisePlan.LayerTargets.FirstOrDefault(), "pla_b_recovery_load"),
            cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> EnumerateGeodatabaseLayerPaths(IEnumerable<string> geodatabasePaths)
    {
        foreach (var geodatabasePath in geodatabasePaths)
        {
            if (!Directory.Exists(geodatabasePath))
            {
                continue;
            }

            using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(geodatabasePath)));
            foreach (var definition in geodatabase.GetDefinitions<FeatureClassDefinition>())
            {
                var name = definition.GetName();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return Path.Combine(geodatabasePath, name);
                }
            }

            foreach (var definition in geodatabase.GetDefinitions<RasterDatasetDefinition>())
            {
                var name = definition.GetName();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return Path.Combine(geodatabasePath, name);
                }
            }

            foreach (var datasetDefinition in geodatabase.GetDefinitions<FeatureDatasetDefinition>())
            {
                var datasetName = datasetDefinition.GetName();
                if (string.IsNullOrWhiteSpace(datasetName))
                {
                    continue;
                }

                using var featureDataset = geodatabase.OpenDataset<FeatureDataset>(datasetName);
                foreach (var definition in featureDataset.GetDefinitions<FeatureClassDefinition>())
                {
                    var name = definition.GetName();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        yield return Path.Combine(geodatabasePath, datasetName, name);
                    }
                }
            }
        }
    }

    private static GroupLayer EnsureGroupLayer(Map map, string groupLayerName)
    {
        var existingGroup = map.Layers.OfType<GroupLayer>()
            .FirstOrDefault(layer => string.Equals(layer.Name, groupLayerName, StringComparison.OrdinalIgnoreCase));
        return existingGroup ?? LayerFactory.Instance.CreateGroupLayer(map, 0, groupLayerName);
    }

    private static void ClearGroupLayer(Map map, GroupLayer groupLayer)
    {
        foreach (var layer in FlattenLayers(groupLayer.Layers).ToArray())
        {
            map.RemoveLayer(layer);
        }
    }

    private static IEnumerable<Layer> FlattenLayers(IEnumerable<Layer> layers)
    {
        foreach (var layer in layers)
        {
            yield return layer;
            if (layer is CompositeLayer compositeLayer)
            {
                foreach (var childLayer in FlattenLayers(compositeLayer.Layers))
                {
                    yield return childLayer;
                }
            }
        }
    }

    private static string BuildDefinitionQuery(string fieldName, string value)
    {
        return $"{fieldName} = '{value.Replace("'", "''")}'";
    }

    private static bool IsMGeoLayerPath(string layerPath)
    {
        var name = Path.GetFileName(layerPath);
        return name.Equals("m-geo", StringComparison.OrdinalIgnoreCase)
            || name.Equals("m_geo", StringComparison.OrdinalIgnoreCase)
            || name.Equals("mgeo", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("mgeo_overlay_", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record PlaBMapRecoveryLoadResult(
    bool Success,
    string Message,
    IReadOnlyList<string> LoadedLayerPaths)
{
    public static PlaBMapRecoveryLoadResult Succeeded(string message, IReadOnlyList<string> loadedLayerPaths)
    {
        return new PlaBMapRecoveryLoadResult(true, message, loadedLayerPaths);
    }

    public static PlaBMapRecoveryLoadResult Failed(string message)
    {
        return new PlaBMapRecoveryLoadResult(false, message, Array.Empty<string>());
    }
}

public sealed class PlaBCurrentTransactionSourceDownloadService
{
    private static readonly HashSet<string> ViewableSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".dwg",
        ".txt",
        ".csv",
        ".tif",
        ".tiff",
        ".png",
        ".jpg",
        ".jpeg"
    };

    private readonly IInnolaTransactionDetailService detailService;
    private readonly CaseFolderStore caseFolderStore;
    private readonly AttachmentSourceFileWriter attachmentWriter;
    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaBCurrentTransactionSourceDownloadService(
        IInnolaTransactionDetailService detailService,
        CaseFolderStore? caseFolderStore = null,
        AttachmentSourceFileWriter? attachmentWriter = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.detailService = detailService;
        this.caseFolderStore = caseFolderStore ?? new CaseFolderStore();
        this.attachmentWriter = attachmentWriter ?? new AttachmentSourceFileWriter();
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PlaBCurrentTransactionSourceDownloadResult> DownloadAsync(
        InnolaSession session,
        SelectedInnolaTransaction selected,
        string outputRoot,
        string username,
        CancellationToken cancellationToken = default)
    {
        var detailResult = await detailService.GetTransactionDetailAsync(session, selected, cancellationToken).ConfigureAwait(false);
        if (!detailResult.Success || detailResult.Detail is null)
        {
            var reason = detailResult.ErrorMessage ?? "Current transaction details could not be loaded.";
            var category = string.IsNullOrWhiteSpace(detailResult.ErrorCode) ? string.Empty : $" ({detailResult.ErrorCode})";
            return PlaBCurrentTransactionSourceDownloadResult.Failed($"PLA_B could not load current transaction {selected.TransactionNumber}: {reason}{category}");
        }

        var detail = detailResult.Detail;
        var normalizedTransactionNumber = InnolaTransactionNumbers.NormalizeWorkflowKey(detail.TransactionNumber);
        if (!normalizedTransactionNumber.Equals(detail.TransactionNumber, StringComparison.Ordinal))
        {
            detail = detail with { TransactionNumber = normalizedTransactionNumber };
        }

        var layoutResult = PrepareCaseFolder(outputRoot, detail.TransactionNumber, username);
        if (!layoutResult.Success || layoutResult.Layout is null || layoutResult.Manifest is null)
        {
            return PlaBCurrentTransactionSourceDownloadResult.Failed(layoutResult.Message);
        }

        var sourceAttachments = detail.Attachments
            .Where(attachment => !InnolaResumePackageConventions.IsSystemPackageAttachment(attachment, detail.TransactionNumber))
            .ToArray();
        if (sourceAttachments.Length == 0 && layoutResult.Manifest.Payload.SourceFiles.Count == 0)
        {
            return PlaBCurrentTransactionSourceDownloadResult.Failed($"Current transaction {detail.TransactionNumber} has no source attachments to download.");
        }

        var sourceFiles = DeduplicateSourceFiles(layoutResult.Manifest.Payload.SourceFiles).ToList();
        var provenance = DeduplicateAttachmentProvenance(layoutResult.Manifest.Payload.AttachmentProvenance ?? Array.Empty<ManifestAttachmentProvenance>()).ToList();
        var warnings = new List<string>();
        var loadedAt = getUtcNow().UtcDateTime.ToString("O");
        foreach (var attachment in sourceAttachments)
        {
            if (!ViewableSourceExtensions.Contains(NormalizeExtension(attachment)))
            {
                warnings.Add($"{DescribeAttachment(attachment)} skipped because the file type is not supported by the document viewer.");
                continue;
            }

            if (provenance.Any(existing => existing.AttachmentId.Equals(attachment.AttachmentId, StringComparison.OrdinalIgnoreCase)
                && File.Exists(existing.CopiedPath)))
            {
                continue;
            }

            var content = await detailService.GetAttachmentContentAsync(session, detail, attachment, cancellationToken).ConfigureAwait(false);
            if (!content.Success)
            {
                warnings.Add($"{DescribeAttachment(attachment)} could not be downloaded: {DescribeFailure(content.ErrorMessage, content.ErrorCode)}");
                continue;
            }

            var serviceReference = $"innola-attachment:{attachment.AttachmentId}";
            var written = attachmentWriter.Write(
                layoutResult.Layout,
                serviceReference,
                attachment.FileName,
                content.Content,
                attachment.SourceRole,
                attachment.SourceType);
            if (!written.Success || written.ManifestSourceFile is null)
            {
                warnings.Add($"{DescribeAttachment(attachment)} could not be copied to the Case Folder: {DescribeFailure(written.ErrorMessage, null)}");
                continue;
            }

            sourceFiles.Add(written.ManifestSourceFile);
            provenance.Add(new ManifestAttachmentProvenance(
                attachment.AttachmentId,
                attachment.FileName,
                NormalizeExtension(attachment),
                attachment.MimeType,
                attachment.SourceRole,
                attachment.Category,
                attachment.Size,
                attachment.Checksum,
                serviceReference,
                written.ManifestSourceFile.CopiedPath,
                written.CopiedAt ?? loadedAt,
                attachment.SourceType));
        }

        var updatedManifest = layoutResult.Manifest with
        {
            Payload = layoutResult.Manifest.Payload with
            {
                SourceFiles = DeduplicateSourceFiles(sourceFiles),
                AttachmentProvenance = DeduplicateAttachmentProvenance(provenance),
                WorkflowProfile = PlaBWorkflowConstants.WorkflowProfile,
                InnolaTransaction = new ManifestInnolaTransaction(
                    detail.TransactionId,
                    detail.TransactionNumber,
                    detail.TaskId,
                    detail.TaskName,
                    detail.ProcessStep,
                    detail.CaseType,
                    detail.ProfileHint,
                    username,
                    detail.AssignedUser,
                    detail.AssignedGroup,
                    detail.OwnerUser,
                    detail.ClaimStatus,
                    loadedAt)
            }
        };
        ManifestSerializer.Write(layoutResult.Layout.ManifestPath, updatedManifest);

        var sourceFileCount = Directory.Exists(layoutResult.Layout.SourceDirectory)
            ? Directory.EnumerateFiles(layoutResult.Layout.SourceDirectory).Count()
            : 0;
        return sourceFileCount > 0
            ? PlaBCurrentTransactionSourceDownloadResult.Succeeded(layoutResult.Layout, detail, loadedAt, sourceFileCount, warnings)
            : PlaBCurrentTransactionSourceDownloadResult.Failed(
                BuildNoSourceFilesMessage(detail.TransactionNumber, layoutResult.Layout.SourceDirectory, warnings),
                warnings);
    }

    private PlaBCaseFolderPreparationResult PrepareCaseFolder(string outputRoot, string transactionNumber, string username)
    {
        try
        {
            var layout = CaseFolderLayout.For(outputRoot, transactionNumber);
            if (Directory.Exists(layout.RootDirectory))
            {
                var reopen = caseFolderStore.ReopenCaseFolder(layout.RootDirectory);
                return reopen.Success && reopen.Layout is not null && reopen.Manifest is not null
                    ? PlaBCaseFolderPreparationResult.Prepared(reopen.Layout, reopen.Manifest)
                    : PlaBCaseFolderPreparationResult.Failed($"Existing Case Folder could not be reopened: {string.Join("; ", reopen.RecoverabilityIssues.Select(issue => issue.Message))}");
            }

            var created = caseFolderStore.CreateCase(outputRoot, transactionNumber, username);
            if (!created.Success || created.Layout is null)
            {
                return PlaBCaseFolderPreparationResult.Failed(created.ErrorMessage ?? "Case Folder could not be created.");
            }

            return PlaBCaseFolderPreparationResult.Prepared(created.Layout, ManifestSerializer.Read(created.Layout.ManifestPath));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException)
        {
            return PlaBCaseFolderPreparationResult.Failed($"Case Folder could not be prepared: {exception.Message}");
        }
    }

    private static IReadOnlyList<ManifestSourceFile> DeduplicateSourceFiles(IReadOnlyList<ManifestSourceFile> sourceFiles)
    {
        return sourceFiles
            .GroupBy(source => $"{source.CopiedPath}|{SourceRole.Normalize(source.SourceRole) ?? string.Empty}|{source.SourceType ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(source => TryParseCopiedAt(source.CopiedAt)).ThenByDescending(source => source.FileSize).First())
            .OrderBy(source => SourceRole.Normalize(source.SourceRole) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.CopiedPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ManifestAttachmentProvenance> DeduplicateAttachmentProvenance(IReadOnlyList<ManifestAttachmentProvenance> provenance)
    {
        return provenance
            .GroupBy(item => $"{item.AttachmentId}|{item.CopiedPath}|{SourceRole.Normalize(item.SourceRole) ?? string.Empty}|{item.SourceType ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => TryParseCopiedAt(item.CopiedAt)).ThenByDescending(item => item.FileSize ?? 0L).First())
            .OrderBy(item => SourceRole.Normalize(item.SourceRole) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.CopiedPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateTimeOffset TryParseCopiedAt(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string NormalizeExtension(InnolaAttachmentMetadata attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.Extension))
        {
            return attachment.Extension.StartsWith(".", StringComparison.Ordinal)
                ? attachment.Extension.ToLowerInvariant()
                : $".{attachment.Extension.ToLowerInvariant()}";
        }

        return Path.GetExtension(attachment.FileName).ToLowerInvariant();
    }

    private static string DescribeAttachment(InnolaAttachmentMetadata attachment)
    {
        var name = string.IsNullOrWhiteSpace(attachment.FileName) ? "(unnamed attachment)" : attachment.FileName;
        var sourceType = string.IsNullOrWhiteSpace(attachment.SourceType) ? string.Empty : $" [{attachment.SourceType}]";
        return $"{name}{sourceType}";
    }

    private static string DescribeFailure(string? message, string? errorCode)
    {
        var reason = string.IsNullOrWhiteSpace(message) ? "unknown error" : message.Trim();
        return string.IsNullOrWhiteSpace(errorCode) ? reason : $"{reason} ({errorCode})";
    }

    private static string BuildNoSourceFilesMessage(
        string transactionNumber,
        string sourceDirectory,
        IReadOnlyList<string> warnings)
    {
        var message = $"Current transaction {transactionNumber} loaded, but no files were downloaded to {sourceDirectory}.";
        return warnings.Count == 0
            ? message
            : $"{message}\nSkipped attachments: {warnings.Count}. First issue: {warnings[0]}";
    }

    private sealed record PlaBCaseFolderPreparationResult(
        bool Success,
        CaseFolderLayout? Layout,
        ManifestDocument? Manifest,
        string Message)
    {
        public static PlaBCaseFolderPreparationResult Prepared(CaseFolderLayout layout, ManifestDocument manifest)
        {
            return new PlaBCaseFolderPreparationResult(true, layout, manifest, string.Empty);
        }

        public static PlaBCaseFolderPreparationResult Failed(string message)
        {
            return new PlaBCaseFolderPreparationResult(false, null, null, message);
        }
    }
}

public sealed record PlaBCurrentTransactionSourceDownloadResult(
    bool Success,
    string Message,
    CaseFolderLayout? Layout,
    InnolaTransactionDetail? Detail,
    string? LoadedAt,
    int SourceFileCount,
    IReadOnlyList<string> Warnings)
{
    public static PlaBCurrentTransactionSourceDownloadResult Succeeded(
        CaseFolderLayout layout,
        InnolaTransactionDetail detail,
        string loadedAt,
        int sourceFileCount,
        IReadOnlyList<string>? warnings = null)
    {
        return new PlaBCurrentTransactionSourceDownloadResult(true, string.Empty, layout, detail, loadedAt, sourceFileCount, warnings ?? Array.Empty<string>());
    }

    public static PlaBCurrentTransactionSourceDownloadResult Failed(string message, IReadOnlyList<string>? warnings = null)
    {
        return new PlaBCurrentTransactionSourceDownloadResult(false, message, null, null, null, 0, warnings ?? Array.Empty<string>());
    }
}

internal sealed class PlaBSurveyDiagramSelectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPlaBSurveyDiagramSelectionRenderer renderer;
    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaBSurveyDiagramSelectionService()
        : this(new PythonPdfiumPlaBSurveyDiagramSelectionRenderer(), () => DateTimeOffset.UtcNow)
    {
    }

    public PlaBSurveyDiagramSelectionService(IPlaBSurveyDiagramSelectionRenderer renderer, Func<DateTimeOffset>? getUtcNow = null)
    {
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PlaBSurveyDiagramSelectionSaveResult> SaveSelectionAsync(
        CaseFolderLayout layout,
        string currentTransactionNumber,
        string peNumber,
        PlaBSurveyDiagramSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(request);

        var normalized = PlaBPeNumberNormalizer.Normalize(peNumber);
        if (!normalized.Success)
        {
            return PlaBSurveyDiagramSelectionSaveResult.Failed(normalized.ErrorCode!, normalized.Message!);
        }

        if (!IsSurveyDiagramSource(request.SourceFile))
        {
            return PlaBSurveyDiagramSelectionSaveResult.Failed(
                "survey_diagram_missing",
                "Select a copied st_survey_diagram PDF before saving PLA_B survey diagram evidence.");
        }

        if (request.SelectedPageNumber < 1)
        {
            return PlaBSurveyDiagramSelectionSaveResult.Failed("page_invalid", "Selected page number must be 1 or greater.");
        }

        if (!IsPathInside(layout.RootDirectory, request.SourceFile.CopiedPath!))
        {
            return PlaBSurveyDiagramSelectionSaveResult.Failed("source_outside_case", "Selected survey diagram must be inside the active Case Folder.");
        }

        var render = await renderer.RenderAsync(
            new PlaBSurveyDiagramSelectionRenderRequest(
                request.SourceFile.CopiedPath!,
                request.SelectedPageNumber,
                request.SelectionRegion),
            cancellationToken).ConfigureAwait(false);
        if (!render.Success || render.Content.Length == 0)
        {
            return PlaBSurveyDiagramSelectionSaveResult.Failed(render.ErrorCode ?? "render_failed", render.Message ?? "Survey diagram selection could not be rendered.");
        }

        var workingDirectory = GetWorkingDirectory(layout);
        Directory.CreateDirectory(workingDirectory);
        var pngPath = GetPngPath(layout);
        await File.WriteAllBytesAsync(pngPath, render.Content, cancellationToken).ConfigureAwait(false);

        var now = getUtcNow();
        var existing = LoadSelection(layout);
        var document = new PlaBSurveyDiagramSelectionDocument(
            "1.0.0",
            currentTransactionNumber,
            normalized.PeNumber!,
            string.IsNullOrWhiteSpace(request.SourceFile.SourceType) ? PlaBWorkflowConstants.SurveyDiagramSourceType : request.SourceFile.SourceType!,
            ToCaseRelativePath(layout, request.SourceFile.CopiedPath!),
            request.SelectedPageNumber,
            request.SelectionRegion,
            ToCaseRelativePath(layout, pngPath),
            render.PageWidthPoints,
            render.PageHeightPoints,
            existing?.CreatedAtUtc ?? now,
            now);

        await File.WriteAllTextAsync(GetMetadataPath(layout), JsonSerializer.Serialize(document, JsonOptions), cancellationToken).ConfigureAwait(false);
        return PlaBSurveyDiagramSelectionSaveResult.Saved(document);
    }

    public static PlaBSurveyDiagramSelectionDocument? LoadSelection(CaseFolderLayout layout)
    {
        var path = GetMetadataPath(layout);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<PlaBSurveyDiagramSelectionDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null
                || string.IsNullOrWhiteSpace(document.PngRelativePath)
                || !TryResolveCaseRelativePath(layout, document.PngRelativePath, out var pngPath)
                || !File.Exists(pngPath))
            {
                return null;
            }

            return document;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static string GetWorkingDirectory(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, PlaBWorkflowConstants.WorkingDirectoryName);
    }

    public static string GetPngPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), PlaBWorkflowConstants.SelectionPngFileName);
    }

    public static string GetMetadataPath(CaseFolderLayout layout)
    {
        return Path.Combine(GetWorkingDirectory(layout), PlaBWorkflowConstants.SelectionMetadataFileName);
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
            var resolved = Path.GetFullPath(Path.Combine(layout.RootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathInside(layout.RootDirectory, resolved))
            {
                return false;
            }

            path = resolved;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSurveyDiagramSource(SourceFileCopyResult source)
    {
        return source.Copied
            && !string.IsNullOrWhiteSpace(source.CopiedPath)
            && File.Exists(source.CopiedPath)
            && string.Equals(Path.GetExtension(source.CopiedPath), ".pdf", StringComparison.OrdinalIgnoreCase)
            && (SourceRole.Matches(source.SourceRole, PlaBWorkflowConstants.SurveyDiagramSourceRole)
                || string.Equals(source.SourceType, PlaBWorkflowConstants.SurveyDiagramSourceType, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToCaseRelativePath(CaseFolderLayout layout, string path)
    {
        return Path.GetRelativePath(layout.RootDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record PlaBSurveyDiagramSelectionRequest(
    SourceFileCopyResult SourceFile,
    int SelectedPageNumber,
    PlaBPdfSelectionRegion SelectionRegion);

internal sealed record PlaBPdfSelectionRegion(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height);

internal sealed record PlaBSurveyDiagramSelectionDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("pe_number")] string PeNumber,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("source_relative_path")] string SourceRelativePath,
    [property: JsonPropertyName("selected_page_number")] int SelectedPageNumber,
    [property: JsonPropertyName("selection_region")] PlaBPdfSelectionRegion SelectionRegion,
    [property: JsonPropertyName("png_path")] string PngRelativePath,
    [property: JsonPropertyName("page_width_points")] int PageWidthPoints,
    [property: JsonPropertyName("page_height_points")] int PageHeightPoints,
    [property: JsonPropertyName("created_at_utc")] DateTimeOffset CreatedAtUtc,
    [property: JsonPropertyName("updated_at_utc")] DateTimeOffset UpdatedAtUtc);

internal sealed record PlaBSurveyDiagramSelectionSaveResult(
    bool Success,
    PlaBSurveyDiagramSelectionDocument? Selection,
    string? ErrorCode,
    string Message)
{
    public static PlaBSurveyDiagramSelectionSaveResult Saved(PlaBSurveyDiagramSelectionDocument selection)
    {
        return new PlaBSurveyDiagramSelectionSaveResult(true, selection, null, "PLA_B survey diagram selection saved.");
    }

    public static PlaBSurveyDiagramSelectionSaveResult Failed(string errorCode, string message)
    {
        return new PlaBSurveyDiagramSelectionSaveResult(false, null, errorCode, message);
    }
}

internal interface IPlaBSurveyDiagramSelectionRenderer
{
    Task<PlaBSurveyDiagramSelectionRenderResult> RenderAsync(
        PlaBSurveyDiagramSelectionRenderRequest request,
        CancellationToken cancellationToken);
}

internal sealed record PlaBSurveyDiagramSelectionRenderRequest(
    string SourcePdfPath,
    int SelectedPageNumber,
    PlaBPdfSelectionRegion SelectionRegion);

internal sealed record PlaBSurveyDiagramSelectionRenderResult(
    bool Success,
    byte[] Content,
    int PageWidthPoints,
    int PageHeightPoints,
    string? ErrorCode,
    string? Message)
{
    public static PlaBSurveyDiagramSelectionRenderResult Png(byte[] content, int pageWidthPoints, int pageHeightPoints)
    {
        return new PlaBSurveyDiagramSelectionRenderResult(true, content, pageWidthPoints, pageHeightPoints, null, null);
    }

    public static PlaBSurveyDiagramSelectionRenderResult Failed(string errorCode, string message)
    {
        return new PlaBSurveyDiagramSelectionRenderResult(false, Array.Empty<byte>(), 0, 0, errorCode, message);
    }
}

internal interface IPlaBGeneratedEvidenceUploader
{
    Task<PlaGeneratedOutputAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string artifactPath,
        string sourceType,
        string contentType,
        CancellationToken cancellationToken = default);
}

internal sealed class PlaBGeneratedEvidenceUploader : IPlaBGeneratedEvidenceUploader
{
    private readonly Func<InnolaSession?> getSession;
    private readonly IInnolaTransactionDetailService detailService;

    public PlaBGeneratedEvidenceUploader(
        Func<InnolaSession?> getSession,
        IInnolaTransactionDetailService detailService)
    {
        this.getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        this.detailService = detailService ?? throw new ArgumentNullException(nameof(detailService));
    }

    public async Task<PlaGeneratedOutputAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string artifactPath,
        string sourceType,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) || !File.Exists(artifactPath))
        {
            return PlaGeneratedOutputAttachmentResult.Failed("PLA_B generated evidence is missing.", "pla_b_evidence_missing");
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return PlaGeneratedOutputAttachmentResult.Failed("PLA_B evidence could not be attached because the Innola session is not available.", "session_unavailable");
        }

        try
        {
            var content = await File.ReadAllBytesAsync(artifactPath, cancellationToken).ConfigureAwait(false);
            var upload = await detailService.UploadAttachmentAsync(
                session,
                transaction,
                Path.GetFileName(artifactPath),
                contentType,
                content,
                sourceType,
                cancellationToken).ConfigureAwait(false);

            return upload.Success
                ? PlaGeneratedOutputAttachmentResult.Succeeded(sourceType, artifactPath)
                : PlaGeneratedOutputAttachmentResult.Failed(
                    upload.ErrorMessage ?? "PLA_B generated evidence could not be attached to the transaction.",
                    upload.ErrorCategory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return PlaGeneratedOutputAttachmentResult.Failed("PLA_B generated evidence could not be attached. Try again.", exception.GetType().Name);
        }
    }
}

internal sealed class PlaBFinalizeService
{
    public const string PngContentType = "image/png";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPlaBGeneratedEvidenceUploader uploader;
    private readonly Func<InnolaTransactionSettings> settingsProvider;
    private readonly Func<DateTimeOffset> getUtcNow;

    public PlaBFinalizeService(
        IPlaBGeneratedEvidenceUploader uploader,
        Func<InnolaTransactionSettings>? settingsProvider = null,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        this.settingsProvider = settingsProvider ?? InnolaTransactionSettings.Load;
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
        if (!IsPlaBWorkflow(manifest))
        {
            return PlaFinalizeReadinessResult.Ready("not_pla_b");
        }

        var selection = PlaBSurveyDiagramSelectionService.LoadSelection(layout);
        if (selection is null
            || !PlaBSurveyDiagramSelectionService.TryResolveCaseRelativePath(layout, selection.PngRelativePath, out var pngPath)
            || !File.Exists(pngPath))
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_b_survey_diagram_selection_missing",
                "Finalize is blocked until saved PLA_B survey diagram PNG evidence is available.");
        }

        if (!IsConfiguredPngOutputSource(settingsProvider()))
        {
            return PlaFinalizeReadinessResult.Blocked(
                "pla_b_output_source_type_unavailable",
                "Finalize is blocked because PLA_B survey diagram PNG output source type is not configured.");
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
            WriteEvidence(layout, transaction, Array.Empty<PlaFinalizeUploadItem>(), PlaFinalizeService.FailedStatus, readiness.Message, readiness.Reason, operatorId);
            return PlaFinalizeResult.Failed(readiness.Message, readiness.Reason);
        }

        var selection = PlaBSurveyDiagramSelectionService.LoadSelection(layout)!;
        PlaBSurveyDiagramSelectionService.TryResolveCaseRelativePath(layout, selection.PngRelativePath, out var pngPath);
        var item = new PlaFinalizeUploadItem(
            Path.GetFileName(pngPath),
            ToCaseRelativePath(layout, pngPath),
            PlaBWorkflowConstants.SurveyDiagramPngOutputSourceType,
            PlaFinalizeService.PendingStatus,
            null,
            null,
            new FileInfo(pngPath).Length);
        WriteEvidence(layout, transaction, new[] { item }, PlaFinalizeService.PendingStatus, "PLA_B evidence upload started.", null, operatorId);

        var upload = await uploader.UploadAsync(
            transaction,
            pngPath,
            PlaBWorkflowConstants.SurveyDiagramPngOutputSourceType,
            PngContentType,
            cancellationToken).ConfigureAwait(false);
        var completedItem = item with
        {
            UploadStatus = upload.Success ? PlaFinalizeService.UploadedStatus : PlaFinalizeService.FailedStatus,
            ErrorCategory = upload.ErrorCategory,
            Message = upload.Message
        };

        if (!upload.Success)
        {
            var message = SanitizeUploadDiagnostic(upload.Message);
            WriteEvidence(layout, transaction, new[] { completedItem }, PlaFinalizeService.FailedStatus, message, upload.ErrorCategory, operatorId);
            return PlaFinalizeResult.Failed(message, upload.ErrorCategory);
        }

        WriteEvidence(layout, transaction, new[] { completedItem }, PlaFinalizeService.UploadedStatus, "PLA_B generated evidence attached to the transaction.", null, operatorId);
        return PlaFinalizeResult.Succeeded(new[] { PlaBWorkflowConstants.SurveyDiagramPngOutputSourceType }, new[] { pngPath });
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
        return Path.Combine(PlaBSurveyDiagramSelectionService.GetWorkingDirectory(layout), PlaBWorkflowConstants.FinalizeEvidenceFileName);
    }

    public static bool IsPlaBWorkflow(ManifestDocument manifest)
    {
        return string.Equals(manifest.Payload.WorkflowProfile, PlaBWorkflowConstants.WorkflowProfile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.Payload.DetectedProfile?.ProfileCode, PlaBWorkflowConstants.WorkflowProfile, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.Payload.TransactionTypeProfile?.WorkflowProfile, PlaBWorkflowConstants.WorkflowProfile, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfiguredPngOutputSource(InnolaTransactionSettings settings)
    {
        var definition = settings.ComputeAttachmentSourceTypes.FirstOrDefault(source =>
            string.Equals(source.SourceType, PlaBWorkflowConstants.SurveyDiagramPngOutputSourceType, StringComparison.OrdinalIgnoreCase));
        return definition is not null
            && definition.InternalOnly
            && !definition.Required
            && definition.SupportsExtension(".png");
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
            Directory.CreateDirectory(PlaBSurveyDiagramSelectionService.GetWorkingDirectory(layout));
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
            System.Diagnostics.Debug.WriteLine($"PLA_B finalize evidence write failed: {exception.GetType().Name}.");
        }
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
            return "PLA_B evidence upload failed. Try again.";
        }

        return value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            ? "PLA_B evidence upload failed. Sensitive diagnostic was redacted."
            : value;
    }
}

internal sealed class PythonPdfiumPlaBSurveyDiagramSelectionRenderer : IPlaBSurveyDiagramSelectionRenderer
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(2);
    private readonly IProcessRunner processRunner;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;

    public PythonPdfiumPlaBSurveyDiagramSelectionRenderer()
        : this(new ProcessRunner(), () => WorkflowExecutionSettings.Load())
    {
    }

    public PythonPdfiumPlaBSurveyDiagramSelectionRenderer(IProcessRunner processRunner, Func<WorkflowExecutionSettings> getExecutionSettings)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.getExecutionSettings = getExecutionSettings ?? throw new ArgumentNullException(nameof(getExecutionSettings));
    }

    public async Task<PlaBSurveyDiagramSelectionRenderResult> RenderAsync(
        PlaBSurveyDiagramSelectionRenderRequest request,
        CancellationToken cancellationToken)
    {
        var settings = getExecutionSettings();
        if (string.IsNullOrWhiteSpace(settings.PythonExecutable) || !File.Exists(settings.PythonExecutable))
        {
            return PlaBSurveyDiagramSelectionRenderResult.Failed(
                "python_unavailable",
                "Configured ArcGIS Python executable is not available for PLA_B survey diagram rendering.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"innola-pla-b-render-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(tempDirectory, "render_pla_b_survey_diagram_selection.py");
        var outputPath = Path.Combine(tempDirectory, "survey_diagram_selection.png");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            await File.WriteAllTextAsync(scriptPath, BuildRenderScript(), cancellationToken).ConfigureAwait(false);

            var arguments = string.Join(
                " ",
                Quote(scriptPath),
                Quote(request.SourcePdfPath),
                request.SelectedPageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.SelectionRegion.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.SelectionRegion.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.SelectionRegion.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.SelectionRegion.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Quote(outputPath));
            var result = await processRunner.RunAsync(
                settings.PythonExecutable,
                arguments,
                RenderTimeout,
                environmentVariables: null,
                cancellationToken).ConfigureAwait(false);

            if (result.TimedOut)
            {
                return PlaBSurveyDiagramSelectionRenderResult.Failed("render_timeout", "PLA_B survey diagram rendering timed out.");
            }

            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                return PlaBSurveyDiagramSelectionRenderResult.Failed(
                    "render_failed",
                    $"PLA_B survey diagram selection could not be rendered. {detail.Trim()}");
            }

            var content = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
            return TryReadRenderDimensions(result.StandardOutput, out var width, out var height)
                ? PlaBSurveyDiagramSelectionRenderResult.Png(content, width, height)
                : PlaBSurveyDiagramSelectionRenderResult.Png(content, 0, 0);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or JsonException
            or NotSupportedException
            or ArgumentException)
        {
            return PlaBSurveyDiagramSelectionRenderResult.Failed(
                "render_failed",
                $"PLA_B survey diagram selection could not be rendered: {exception.Message}");
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
x = float(sys.argv[3])
y = float(sys.argv[4])
width = float(sys.argv[5])
height = float(sys.argv[6])
output_path = sys.argv[7]
page_index = page_number - 1

if width <= 0 or height <= 0:
    raise ValueError("Selection region width and height must be greater than zero.")

document = pdfium.PdfDocument(source_path)
if page_index < 0 or page_index >= len(document):
    raise ValueError(f"Selected page {page_number} is outside the source PDF page range.")

page = document[page_index]
page_width, page_height = page.get_size()
if x < 0 or y < 0 or x + width > page_width or y + height > page_height:
    raise ValueError("Selection region is outside the selected PDF page.")

scale = 2
bitmap = page.render(scale=scale).to_pil()
crop_box = (
    int(round(x * scale)),
    int(round(y * scale)),
    int(round((x + width) * scale)),
    int(round((y + height) * scale)),
)
bitmap.crop(crop_box).save(output_path)
print(json.dumps({"width": int(round(page_width)), "height": int(round(page_height))}))
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

public sealed class PlaBTestEmulationInputViewModel : INotifyPropertyChanged
{
    private string currentTransactionNumber = string.Empty;
    private string peNumber = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentTransactionNumber
    {
        get => currentTransactionNumber;
        set
        {
            if (!string.Equals(currentTransactionNumber, value, StringComparison.Ordinal))
            {
                currentTransactionNumber = value;
                NotifyPropertyChanged();
                NotifyComputedProperties();
            }
        }
    }

    public string PeNumber
    {
        get => peNumber;
        set
        {
            if (!string.Equals(peNumber, value, StringComparison.Ordinal))
            {
                peNumber = value;
                NotifyPropertyChanged();
                NotifyComputedProperties();
            }
        }
    }

    public string? NormalizedPeNumber => PlaBPeNumberNormalizer.Normalize(PeNumber).PeNumber;

    public bool HasCurrentTransactionNumber => !string.IsNullOrWhiteSpace(CurrentTransactionNumber);

    public bool CanPrepare =>
        HasCurrentTransactionNumber
        && PlaBPeNumberNormalizer.Normalize(PeNumber).Success;

    public string StatusText => CanPrepare
        ? $"PLA_B test values ready for PE {NormalizedPeNumber}."
        : "Enter a current transaction number and PE number to emulate PLA_B preparation.";

    private void NotifyComputedProperties()
    {
        NotifyPropertyChanged(nameof(NormalizedPeNumber));
        NotifyPropertyChanged(nameof(HasCurrentTransactionNumber));
        NotifyPropertyChanged(nameof(CanPrepare));
        NotifyPropertyChanged(nameof(StatusText));
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
