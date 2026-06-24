using System.Text.Json;
using System.IO;
using System.Linq;

namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionSettings(
    string ServerUrl,
    string Mode,
    string ProcessStep,
    string CaseFolderOutputRoot,
    string ReviewWorkspaceMode,
    string? ReviewWorkspaceModeWarning,
    string PdfViewerMode,
    EnterpriseWorkingReviewSettings EnterpriseWorkingReview,
    EnterpriseParcelFabricReviewSettings EnterpriseParcelFabricReview,
    IReadOnlyList<string> SupportedTransactionTypes,
    string? SupportedTransactionTypesWarning,
    IReadOnlyList<string> ComputeWorkflowStages,
    string? ComputeWorkflowStagesWarning,
    string AttachmentUploadRoute,
    string AttachmentUploadBindingMode,
    string AttachmentUploadMode,
    string ResumeAttachmentSourceType,
    string CompletedAttachmentSourceType,
    string ResumeAttachmentRegisteredType,
    string CompletedAttachmentRegisteredType,
    string? AttachmentRegisteredSpatialUnitId,
    InnolaClientCertificateSettings ClientCertificate)
{
    public static IReadOnlyList<string> SafeDefaultSupportedTransactionTypes { get; } = new[]
    {
        "Plan Examination",
        "Cadastral Plan Examination"
    };

    public static IReadOnlyList<string> SafeDefaultComputeWorkflowStages { get; } = new[]
    {
        "Compute Survey Plan",
        "Assign Computation Task",
        "Computation Check"
    };

    public const string ReviewWorkspaceModeNormal = "normal";
    public const string ReviewWorkspaceModeParcelFabricLocal = "parcel_fabric_local";
    public const string ReviewWorkspaceModeEnterpriseWorkingLayers = "enterprise_working_layers";
    public const string ReviewWorkspaceModeEnterpriseParcelFabric = "enterprise_parcel_fabric";
    public const string ReviewWorkspaceModeParcelFabricLegacy = "parcel_fabric";
    public const string PdfViewerModeEmbeddedBrowser = "embedded_browser";
    public const string PdfViewerModeExternalOnly = "external_only";

    public static InnolaTransactionSettings Default { get; } = new(
        InnolaSettings.DefaultServerUrl,
        "mock",
        "parcel_workflow",
        DefaultCaseFolderOutputRoot(),
        ReviewWorkspaceModeNormal,
        null,
        PdfViewerModeEmbeddedBrowser,
        EnterpriseWorkingReviewSettings.Default,
        EnterpriseParcelFabricReviewSettings.Default,
        SafeDefaultSupportedTransactionTypes,
        null,
        SafeDefaultComputeWorkflowStages,
        null,
        "source/sources/attach",
        "query_only",
        "attach_then_register_source",
        InnolaResumePackageConventions.ResumeSourceType,
        InnolaResumePackageConventions.CompletedSourceType,
        "st_surveyplan",
        "st_surveyplan",
        null,
        InnolaClientCertificateSettings.Default);

    public static InnolaTransactionSettings Load()
    {
        return Load(ResolveSettingsPath());
    }

    internal static InnolaTransactionSettings Load(string settingsPath)
    {

        if (!File.Exists(settingsPath))
        {
            return Default with
            {
                ReviewWorkspaceModeWarning = "Review workspace mode is using the safe default because WorkflowSettings.json was not found.",
                SupportedTransactionTypesWarning = "Supported transaction types are using safe defaults because WorkflowSettings.json was not found.",
                ComputeWorkflowStagesWarning = "Compute workflow stages are using safe defaults because WorkflowSettings.json was not found."
            };
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            var reviewWorkspaceMode = ResolveReviewWorkspaceMode(root);
            var pdfViewerMode = ResolvePdfViewerMode(root);
            var enterpriseWorkingReview = EnterpriseWorkingReviewSettings.FromJson(root, reviewWorkspaceMode.Value);
            var enterpriseParcelFabricReview = EnterpriseParcelFabricReviewSettings.FromJson(root, reviewWorkspaceMode.Value);
            var supportedTypes = ResolveSupportedTransactionTypes(root);
            var computeWorkflowStages = ResolveComputeWorkflowStages(root);
            var serverUrl = ReadString(root, "innola_server_url") ?? Default.ServerUrl;
            var mode = ReadString(root, "innola_transaction_mode") ?? Default.Mode;
            var processStep = ReadString(root, "innola_process_step") ?? Default.ProcessStep;
            var outputRoot = ReadString(root, "case_folder_output_root");
            var attachmentUploadRoute = ReadString(root, "innola_attachment_upload_route") ?? Default.AttachmentUploadRoute;
            var attachmentUploadBindingMode = ReadString(root, "innola_attachment_upload_binding_mode") ?? Default.AttachmentUploadBindingMode;
            var attachmentUploadMode = ReadString(root, "innola_attachment_upload_mode") ?? Default.AttachmentUploadMode;
            var resumeAttachmentSourceType = ReadString(root, "innola_resume_attachment_source_type") ?? Default.ResumeAttachmentSourceType;
            var completedAttachmentSourceType = ReadString(root, "innola_completed_attachment_source_type") ?? Default.CompletedAttachmentSourceType;
            var resumeAttachmentRegisteredType = ReadString(root, "innola_resume_attachment_registered_type") ?? Default.ResumeAttachmentRegisteredType;
            var completedAttachmentRegisteredType = ReadString(root, "innola_completed_attachment_registered_type") ?? Default.CompletedAttachmentRegisteredType;
            var attachmentRegisteredSpatialUnitId = ReadString(root, "innola_attachment_registered_spatial_unit_id");
            var certificate = InnolaClientCertificateSettings.FromJson(root);
            return new InnolaTransactionSettings(
                InnolaHttp.NormalizeServerUrl(serverUrl),
                mode,
                processStep,
                string.IsNullOrWhiteSpace(outputRoot) ? Default.CaseFolderOutputRoot : ExpandPath(outputRoot),
                reviewWorkspaceMode.Value,
                reviewWorkspaceMode.Warning,
                pdfViewerMode,
                enterpriseWorkingReview,
                enterpriseParcelFabricReview,
                supportedTypes.Values,
                supportedTypes.Warning,
                computeWorkflowStages.Values,
                computeWorkflowStages.Warning,
                string.IsNullOrWhiteSpace(attachmentUploadRoute) ? Default.AttachmentUploadRoute : attachmentUploadRoute,
                string.IsNullOrWhiteSpace(attachmentUploadBindingMode) ? Default.AttachmentUploadBindingMode : attachmentUploadBindingMode,
                string.IsNullOrWhiteSpace(attachmentUploadMode) ? Default.AttachmentUploadMode : attachmentUploadMode,
                string.IsNullOrWhiteSpace(resumeAttachmentSourceType) ? Default.ResumeAttachmentSourceType : resumeAttachmentSourceType,
                string.IsNullOrWhiteSpace(completedAttachmentSourceType) ? Default.CompletedAttachmentSourceType : completedAttachmentSourceType,
                string.IsNullOrWhiteSpace(resumeAttachmentRegisteredType) ? Default.ResumeAttachmentRegisteredType : resumeAttachmentRegisteredType,
                string.IsNullOrWhiteSpace(completedAttachmentRegisteredType) ? Default.CompletedAttachmentRegisteredType : completedAttachmentRegisteredType,
                string.IsNullOrWhiteSpace(attachmentRegisteredSpatialUnitId) ? Default.AttachmentRegisteredSpatialUnitId : attachmentRegisteredSpatialUnitId,
                certificate);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or UriFormatException)
        {
            return Default with
            {
                ReviewWorkspaceModeWarning = "Review workspace mode is using the safe default because WorkflowSettings.json could not be parsed.",
                SupportedTransactionTypesWarning = "Supported transaction types are using safe defaults because WorkflowSettings.json could not be parsed.",
                ComputeWorkflowStagesWarning = "Compute workflow stages are using safe defaults because WorkflowSettings.json could not be parsed."
            };
        }
    }

    public static string SettingsPath => Path.Combine(GetAssemblyDirectory(), "Settings", "WorkflowSettings.json");

    public static string ResolveActiveSettingsPath()
    {
        return ResolveSettingsPath();
    }

    private static string ResolveSettingsPath()
    {
        foreach (var candidate in GetSettingsPathCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return SettingsPath;
    }

    private static IEnumerable<string> GetSettingsPathCandidates()
    {
        var assemblyDirectory = GetAssemblyDirectory();
        yield return Path.Combine(assemblyDirectory, "Settings", "WorkflowSettings.json");
        yield return Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "Settings", "WorkflowSettings.json"));
        yield return Path.Combine(AppContext.BaseDirectory, "Settings", "WorkflowSettings.json");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Settings", "WorkflowSettings.json"));
    }

    private static string GetAssemblyDirectory()
    {
        var assemblyPath = typeof(InnolaTransactionSettings).Assembly.Location;
        var directory = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetDirectoryName(assemblyPath);
        return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static SupportedTransactionTypesResolution ResolveSupportedTransactionTypes(JsonElement root)
    {
        if (!root.TryGetProperty("supported_transaction_types", out var value))
        {
            return new SupportedTransactionTypesResolution(
                SafeDefaultSupportedTransactionTypes,
                "Supported transaction types are using safe defaults because supported_transaction_types is missing.");
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return new SupportedTransactionTypesResolution(
                SafeDefaultSupportedTransactionTypes,
                "Supported transaction types are using safe defaults because supported_transaction_types is not a valid list.");
        }

        var supportedTypes = new List<string>();
        var ignoredEntries = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                ignoredEntries++;
                continue;
            }

            var transactionType = item.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(transactionType))
            {
                ignoredEntries++;
                continue;
            }

            if (!supportedTypes.Any(existing => existing.Equals(transactionType, StringComparison.OrdinalIgnoreCase)))
            {
                supportedTypes.Add(transactionType);
            }
        }

        if (supportedTypes.Count == 0)
        {
            return new SupportedTransactionTypesResolution(
                SafeDefaultSupportedTransactionTypes,
                "Supported transaction types are using safe defaults because supported_transaction_types is empty or invalid.");
        }

        return ignoredEntries > 0
            ? new SupportedTransactionTypesResolution(
                supportedTypes,
                "Some supported transaction type entries were invalid and were ignored.")
            : new SupportedTransactionTypesResolution(supportedTypes, null);
    }

    private static NamedStringListResolution ResolveComputeWorkflowStages(JsonElement root)
    {
        return ResolveNamedStringList(
            root,
            "compute_workflow_stages",
            SafeDefaultComputeWorkflowStages,
            "Compute workflow stages");
    }

    private static NamedStringListResolution ResolveNamedStringList(
        JsonElement root,
        string propertyName,
        IReadOnlyList<string> safeDefaults,
        string displayName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return new NamedStringListResolution(
                safeDefaults,
                $"{displayName} are using safe defaults because {propertyName} is missing.");
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return new NamedStringListResolution(
                safeDefaults,
                $"{displayName} are using safe defaults because {propertyName} is not a valid list.");
        }

        var resolvedValues = new List<string>();
        var ignoredEntries = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                ignoredEntries++;
                continue;
            }

            var resolvedValue = item.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                ignoredEntries++;
                continue;
            }

            if (!resolvedValues.Any(existing => existing.Equals(resolvedValue, StringComparison.OrdinalIgnoreCase)))
            {
                resolvedValues.Add(resolvedValue);
            }
        }

        if (resolvedValues.Count == 0)
        {
            return new NamedStringListResolution(
                safeDefaults,
                $"{displayName} are using safe defaults because {propertyName} is empty or invalid.");
        }

        return ignoredEntries > 0
            ? new NamedStringListResolution(
                resolvedValues,
                $"Some {displayName.ToLowerInvariant()} entries were invalid and were ignored.")
            : new NamedStringListResolution(resolvedValues, null);
    }

    private static NamedStringResolution ResolveReviewWorkspaceMode(JsonElement root)
    {
        var configuredValue = ReadString(root, "review_workspace_mode");
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return new NamedStringResolution(
                ReviewWorkspaceModeNormal,
                "Review workspace mode is using the safe default because review_workspace_mode is missing.");
        }

        var normalized = configuredValue.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            ReviewWorkspaceModeNormal => new NamedStringResolution(ReviewWorkspaceModeNormal, null),
            ReviewWorkspaceModeParcelFabricLocal => new NamedStringResolution(ReviewWorkspaceModeParcelFabricLocal, null),
            ReviewWorkspaceModeEnterpriseWorkingLayers => new NamedStringResolution(ReviewWorkspaceModeEnterpriseWorkingLayers, null),
            ReviewWorkspaceModeEnterpriseParcelFabric => new NamedStringResolution(ReviewWorkspaceModeEnterpriseParcelFabric, null),
            ReviewWorkspaceModeParcelFabricLegacy => new NamedStringResolution(ReviewWorkspaceModeParcelFabricLocal, null),
            "parcelfabric" => new NamedStringResolution(ReviewWorkspaceModeParcelFabricLocal, null),
            "parcel-fabric" => new NamedStringResolution(ReviewWorkspaceModeParcelFabricLocal, null),
            _ => new NamedStringResolution(
                ReviewWorkspaceModeNormal,
                $"Review workspace mode is using the safe default because '{configuredValue}' is not a supported value.")
        };
    }

    private static string ResolvePdfViewerMode(JsonElement root)
    {
        var configuredValue = ReadString(root, "pdf_viewer_mode");
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return PdfViewerModeEmbeddedBrowser;
        }

        return configuredValue.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant() switch
        {
            PdfViewerModeExternalOnly => PdfViewerModeExternalOnly,
            "external" => PdfViewerModeExternalOnly,
            _ => PdfViewerModeEmbeddedBrowser
        };
    }

    private static string DefaultCaseFolderOutputRoot()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documents)
            ? Path.Combine(AppContext.BaseDirectory, "CaseFolders")
            : Path.Combine(documents, "SidwellCo", "ParcelWorkflowCases");
    }

    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }

    internal static string FormatSupportedTransactionTypesDisplay(IReadOnlyList<string> supportedTransactionTypes)
    {
        return supportedTransactionTypes.Count == 0
            ? "No supported transaction types configured."
            : string.Join(Environment.NewLine, supportedTransactionTypes.Select(type => $"• {type}"));
    }

    internal static string FormatNamedListDisplay(IReadOnlyList<string> values, string emptyMessage)
    {
        return values.Count == 0
            ? emptyMessage
            : string.Join(Environment.NewLine, values.Select(value => $"• {value}"));
    }

    internal static string FormatReviewWorkspaceMode(string? value)
    {
        return value switch
        {
            ReviewWorkspaceModeParcelFabricLocal => "Local Parcel Fabric",
            ReviewWorkspaceModeEnterpriseWorkingLayers => "Enterprise Working Layers",
            ReviewWorkspaceModeEnterpriseParcelFabric => "Enterprise Parcel Fabric",
            ReviewWorkspaceModeNormal => "Normal",
            _ => "Normal"
        };
    }

    internal static string FormatReviewWorkspaceModeDescription(string? value)
    {
        return value switch
        {
            ReviewWorkspaceModeParcelFabricLocal => "Local transaction geodatabase using Parcel Fabric for richer parcel editing tools.",
            ReviewWorkspaceModeEnterpriseWorkingLayers => "Shared ArcGIS Enterprise working layers for distributed review and cross-session collaboration.",
            ReviewWorkspaceModeEnterpriseParcelFabric => "Shared ArcGIS Enterprise Parcel Fabric for collaborative parcel-aware review after validated points are ready.",
            ReviewWorkspaceModeNormal => "Local transaction geodatabase using standard point, line, and polygon feature classes.",
            _ => "Local transaction geodatabase using standard point, line, and polygon feature classes."
        };
    }

    internal static string FormatPdfViewerMode(string? value)
    {
        return value switch
        {
            PdfViewerModeExternalOnly => "External PDF Viewer",
            _ => "Embedded Rendered Viewer"
        };
    }

    internal static string FormatEnterpriseLayerTargets(EnterpriseWorkingReviewSettings settings)
    {
        static string Line(string label, string? value) => $"{label}: {(string.IsNullOrWhiteSpace(value) ? "Not configured" : value)}";

        return string.Join(
            Environment.NewLine,
            new[]
            {
                Line("Points", settings.Layers.Points),
                Line("Lines", settings.Layers.Lines),
                Line("Polygons", settings.Layers.Polygons),
                Line("Issues", settings.Layers.Issues),
                Line("Case index", settings.Layers.CaseIndex)
            });
    }

    private sealed record SupportedTransactionTypesResolution(
        IReadOnlyList<string> Values,
        string? Warning);

    private sealed record NamedStringListResolution(
        IReadOnlyList<string> Values,
        string? Warning);

    private sealed record NamedStringResolution(
        string Value,
        string? Warning);
}

public sealed record EnterpriseParcelFabricReviewSettings(
    bool Enabled,
    string? ServiceRoot,
    string? FabricLayerUrl,
    string? ParcelLayerUrl,
    string? RecordsLayerUrl,
    string ParcelTypeName,
    string RecordNamePattern,
    string TransactionScopeField,
    string TransactionIdField,
    string ReviewStateField,
    string PublishTiming,
    string BuildBehavior,
    bool LoadOverlays,
    string OverlaySource,
    bool AllowReplaceTransactionScope,
    bool RequireActiveMap,
    string? Warning)
{
    public const string PublishTimingOnOutputs = "on_outputs";
    public const string PublishTimingOnFinalReview = "on_final_review";
    public const string BuildBehaviorBuildAfterCopy = "build_after_copy";
    public const string BuildBehaviorCopyOnly = "copy_only";
    public const string OverlaySourceLocalCaseOutputs = "local_case_outputs";
    public const string OverlaySourceNone = "none";

    public static EnterpriseParcelFabricReviewSettings Default { get; } = new(
        false,
        null,
        null,
        null,
        null,
        "compute_review",
        "sidwell-record-{transaction_number}",
        "transaction_number",
        "transaction_id",
        "review_state",
        PublishTimingOnOutputs,
        BuildBehaviorBuildAfterCopy,
        true,
        OverlaySourceLocalCaseOutputs,
        true,
        true,
        null);

    public static EnterpriseParcelFabricReviewSettings FromJson(JsonElement root, string reviewWorkspaceMode)
    {
        if (!root.TryGetProperty("enterprise_parcel_fabric_review", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return reviewWorkspaceMode == InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric
                ? Default with
                {
                    Warning = "Enterprise Parcel Fabric mode is selected, but enterprise_parcel_fabric_review configuration is missing. Local modes remain available."
                }
                : Default;
        }

        var enabled = ReadBool(value, "enabled") ?? Default.Enabled;
        var fabricLayerUrl = ReadString(value, "fabric_layer_url");
        var parcelTypeName = ReadString(value, "parcel_type_name") ?? Default.ParcelTypeName;
        var recordNamePattern = ReadString(value, "record_name_pattern") ?? Default.RecordNamePattern;
        var transactionScopeField = ReadString(value, "transaction_scope_field") ?? Default.TransactionScopeField;

        var warnings = new List<string>();
        var relevant = reviewWorkspaceMode == InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric || enabled;
        if (relevant)
        {
            if (!enabled)
            {
                warnings.Add("Enterprise Parcel Fabric mode is selected, but enterprise parcel fabric review is disabled.");
            }

            if (string.IsNullOrWhiteSpace(fabricLayerUrl))
            {
                warnings.Add("fabric_layer_url is missing for Enterprise Parcel Fabric review.");
            }

            if (string.IsNullOrWhiteSpace(parcelTypeName))
            {
                warnings.Add("parcel_type_name is missing for Enterprise Parcel Fabric review.");
            }

            if (string.IsNullOrWhiteSpace(recordNamePattern))
            {
                warnings.Add("record_name_pattern is missing for Enterprise Parcel Fabric review.");
            }

            if (string.IsNullOrWhiteSpace(ReadString(value, "records_layer_url")))
            {
                warnings.Add("records_layer_url is missing for Enterprise Parcel Fabric review.");
            }

            if (string.IsNullOrWhiteSpace(transactionScopeField))
            {
                warnings.Add("transaction_scope_field is missing for Enterprise Parcel Fabric review.");
            }
        }

        return new EnterpriseParcelFabricReviewSettings(
            enabled,
            ReadString(value, "service_root"),
            fabricLayerUrl,
            ReadString(value, "parcel_layer_url"),
            ReadString(value, "records_layer_url"),
            parcelTypeName,
            recordNamePattern,
            transactionScopeField,
            ReadString(value, "transaction_id_field") ?? Default.TransactionIdField,
            ReadString(value, "review_state_field") ?? Default.ReviewStateField,
            NormalizePublishTiming(ReadString(value, "publish_timing")),
            NormalizeBuildBehavior(ReadString(value, "build_behavior")),
            ReadBool(value, "load_overlays") ?? Default.LoadOverlays,
            NormalizeOverlaySource(ReadString(value, "overlay_source")),
            ReadBool(value, "allow_replace_transaction_scope") ?? Default.AllowReplaceTransactionScope,
            ReadBool(value, "require_active_map") ?? Default.RequireActiveMap,
            warnings.Count == 0 ? null : string.Join(" ", warnings));
    }

    private static string NormalizePublishTiming(string? value)
    {
        var normalized = value?.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            PublishTimingOnFinalReview => PublishTimingOnFinalReview,
            _ => PublishTimingOnOutputs
        };
    }

    private static string NormalizeBuildBehavior(string? value)
    {
        var normalized = value?.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            BuildBehaviorCopyOnly => BuildBehaviorCopyOnly,
            _ => BuildBehaviorBuildAfterCopy
        };
    }

    private static string NormalizeOverlaySource(string? value)
    {
        var normalized = value?.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            OverlaySourceNone => OverlaySourceNone,
            _ => OverlaySourceLocalCaseOutputs
        };
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}

public sealed record EnterpriseWorkingReviewSettings(
    bool Enabled,
    string? ServiceRoot,
    string WorkspaceName,
    string PublishBehavior,
    string PublishTiming,
    string RestoreBehavior,
    bool AllowCrossMachineRestore,
    string TransactionScopeField,
    EnterpriseWorkingLayerTargets Layers,
    string? Warning)
{
    public const string PublishBehaviorReplaceTransactionScope = "replace_transaction_scope";
    public const string PublishBehaviorAppendOnly = "append_only";
    public const string PublishTimingOnComplete = "on_complete";
    public const string PublishTimingOnOutputs = "on_outputs";
    public const string RestoreBehaviorPreferLocalThenEnterprise = "prefer_local_then_enterprise";
    public const string RestoreBehaviorPreferEnterpriseThenLocal = "prefer_enterprise_then_local";
    public const string RestoreBehaviorLocalOnly = "local_only";

    public bool HasRequiredTargets =>
        !string.IsNullOrWhiteSpace(Layers.Points) &&
        !string.IsNullOrWhiteSpace(Layers.Lines) &&
        !string.IsNullOrWhiteSpace(Layers.Polygons) &&
        !string.IsNullOrWhiteSpace(TransactionScopeField);

    public static EnterpriseWorkingReviewSettings Default { get; } = new(
        false,
        null,
        "sidwell_working_review",
        PublishBehaviorReplaceTransactionScope,
        PublishTimingOnComplete,
        RestoreBehaviorPreferLocalThenEnterprise,
        true,
        "transaction_number",
        EnterpriseWorkingLayerTargets.Default,
        null);

    public static EnterpriseWorkingReviewSettings FromJson(JsonElement root, string reviewWorkspaceMode)
    {
        if (!root.TryGetProperty("enterprise_working_review", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return reviewWorkspaceMode == InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers
                ? Default with
                {
                    Warning = "Enterprise working layers mode is selected, but enterprise_working_review configuration is missing. Local modes remain available."
                }
                : Default;
        }

        var enabled = ReadBool(value, "enabled") ?? Default.Enabled;
        var serviceRoot = ReadString(value, "service_root");
        var workspaceName = ReadString(value, "workspace_name") ?? Default.WorkspaceName;
        var publishBehavior = NormalizePublishBehavior(ReadString(value, "publish_behavior"));
        var publishTiming = NormalizePublishTiming(ReadString(value, "publish_timing"));
        var restoreBehavior = NormalizeRestoreBehavior(ReadString(value, "restore_behavior"));
        var allowCrossMachineRestore = ReadBool(value, "allow_cross_machine_restore") ?? Default.AllowCrossMachineRestore;
        var transactionScopeField = ReadString(value, "transaction_scope_field") ?? Default.TransactionScopeField;
        var layers = EnterpriseWorkingLayerTargets.FromJson(value);

        var warning = BuildWarning(reviewWorkspaceMode, enabled, transactionScopeField, layers);
        return new EnterpriseWorkingReviewSettings(
            enabled,
            serviceRoot,
            workspaceName,
            publishBehavior,
            publishTiming,
            restoreBehavior,
            allowCrossMachineRestore,
            transactionScopeField,
            layers,
            warning);
    }

    private static string NormalizePublishBehavior(string? value)
    {
        var normalized = value?.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            PublishBehaviorAppendOnly => PublishBehaviorAppendOnly,
            _ => PublishBehaviorReplaceTransactionScope
        };
    }

    private static string NormalizeRestoreBehavior(string? value)
    {
        var normalized = value?.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            RestoreBehaviorPreferEnterpriseThenLocal => RestoreBehaviorPreferEnterpriseThenLocal,
            RestoreBehaviorLocalOnly => RestoreBehaviorLocalOnly,
            _ => RestoreBehaviorPreferLocalThenEnterprise
        };
    }

    private static string NormalizePublishTiming(string? value)
    {
        var normalized = value?.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            PublishTimingOnOutputs => PublishTimingOnOutputs,
            _ => PublishTimingOnComplete
        };
    }

    private static string? BuildWarning(
        string reviewWorkspaceMode,
        bool enabled,
        string transactionScopeField,
        EnterpriseWorkingLayerTargets layers)
    {
        var warnings = new List<string>();
        var enterpriseRelevant = reviewWorkspaceMode == InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers || enabled;
        if (enterpriseRelevant)
        {
            if (!enabled)
            {
                warnings.Add("Enterprise working layers mode is selected, but enterprise working review is disabled.");
            }

            if (string.IsNullOrWhiteSpace(layers.Points) || string.IsNullOrWhiteSpace(layers.Lines) || string.IsNullOrWhiteSpace(layers.Polygons))
            {
                warnings.Add("Required enterprise working geometry layer targets (points, lines, polygons) are not fully configured.");
            }

            if (string.IsNullOrWhiteSpace(transactionScopeField))
            {
                warnings.Add("transaction_scope_field is missing for enterprise working layer scoping.");
            }

            if (string.IsNullOrWhiteSpace(layers.CaseIndex))
            {
                warnings.Add("case_index layer is not configured. Resume and restore checks may be less efficient.");
            }

            if (string.IsNullOrWhiteSpace(layers.Issues))
            {
                warnings.Add("issues layer is not configured. Review issue publishing will remain local-only.");
            }
        }

        return warnings.Count == 0 ? null : string.Join(" ", warnings);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}

public sealed record EnterpriseWorkingLayerTargets(
    string? Points,
    string? Lines,
    string? Polygons,
    string? Issues,
    string? CaseIndex)
{
    public static EnterpriseWorkingLayerTargets Default { get; } = new(null, null, null, null, null);

    public static EnterpriseWorkingLayerTargets FromJson(JsonElement root)
    {
        if (!root.TryGetProperty("layers", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return Default;
        }

        return new EnterpriseWorkingLayerTargets(
            ReadString(value, "points"),
            ReadString(value, "lines"),
            ReadString(value, "polygons"),
            ReadString(value, "issues"),
            ReadString(value, "case_index"));
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

public sealed record InnolaClientCertificateSettings(
    bool Enabled,
    string StoreLocation,
    string StoreName,
    string? SubjectName,
    string? Thumbprint,
    bool AllowInvalidServerCertificate,
    bool CheckCertificateRevocationList)
{
    public static InnolaClientCertificateSettings Default { get; } = new(
        true,
        "CurrentUser",
        "My",
        "Jamaica eTitles Project Team",
        null,
        false,
        false);

    public static InnolaClientCertificateSettings FromJson(JsonElement root)
    {
        var enabled = ReadBool(root, "innola_client_certificate_enabled") ?? Default.Enabled;
        var storeLocation = ReadString(root, "innola_client_certificate_store_location") ?? Default.StoreLocation;
        var storeName = ReadString(root, "innola_client_certificate_store_name") ?? Default.StoreName;
        var subject = ReadString(root, "innola_client_certificate_subject") ?? Default.SubjectName;
        var thumbprint = ReadString(root, "innola_client_certificate_thumbprint") ?? Default.Thumbprint;
        var allowInvalidServerCertificate = ReadBool(root, "innola_allow_invalid_server_certificate") ?? Default.AllowInvalidServerCertificate;
        var checkCertificateRevocationList = ReadBool(root, "innola_check_certificate_revocation_list") ?? Default.CheckCertificateRevocationList;
        return new InnolaClientCertificateSettings(
            enabled,
            storeLocation,
            storeName,
            subject,
            thumbprint,
            allowInvalidServerCertificate,
            checkCertificateRevocationList);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}
