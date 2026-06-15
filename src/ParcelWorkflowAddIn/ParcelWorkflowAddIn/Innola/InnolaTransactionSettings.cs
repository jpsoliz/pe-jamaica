using System.Text.Json;
using System.IO;
using System.Linq;

namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionSettings(
    string ServerUrl,
    string Mode,
    string ProcessStep,
    string CaseFolderOutputRoot,
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

    public static InnolaTransactionSettings Default { get; } = new(
        InnolaSettings.DefaultServerUrl,
        "mock",
        "parcel_workflow",
        DefaultCaseFolderOutputRoot(),
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
                SupportedTransactionTypesWarning = "Supported transaction types are using safe defaults because WorkflowSettings.json was not found.",
                ComputeWorkflowStagesWarning = "Compute workflow stages are using safe defaults because WorkflowSettings.json was not found."
            };
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
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

    private sealed record SupportedTransactionTypesResolution(
        IReadOnlyList<string> Values,
        string? Warning);

    private sealed record NamedStringListResolution(
        IReadOnlyList<string> Values,
        string? Warning);
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
