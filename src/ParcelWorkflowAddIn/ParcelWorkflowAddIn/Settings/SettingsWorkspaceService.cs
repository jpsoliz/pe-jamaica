using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;

namespace ParcelWorkflowAddIn.Settings;

public sealed class SettingsWorkspaceService
{
    public const string GsiPasswordModeEnvironmentVariable = "environment_variable";
    public const string GsiPasswordModeDirect = "direct";
    public const string OpenAiExtractionProfileCustom = "custom";
    public const string OpenAiExtractionProfileBalanced = "balanced";
    public const string OpenAiExtractionProfileHighAccuracy = "high_accuracy";

    private readonly PreflightRuleCatalogLoader ruleCatalogLoader;

    public SettingsWorkspaceService()
        : this(new PreflightRuleCatalogLoader())
    {
    }

    public SettingsWorkspaceService(PreflightRuleCatalogLoader ruleCatalogLoader)
    {
        this.ruleCatalogLoader = ruleCatalogLoader;
    }

    public SettingsWorkspaceDocument Load(string? settingsPath = null)
    {
        settingsPath ??= InnolaTransactionSettings.ResolveActiveSettingsPath();
        var transactionSettings = InnolaTransactionSettings.Load(settingsPath);
        var executionSettings = Workflow.Execution.WorkflowExecutionSettings.Load(settingsPath);
        var environmentSettings = LoadProcessingEnvironmentSettings(settingsPath);
        var settingsRoot = LoadSettingsRoot(settingsPath);
        var ruleCatalog = ruleCatalogLoader.Load();

        return new SettingsWorkspaceDocument
        {
            SettingsPath = settingsPath,
            PreflightRulesPath = ruleCatalog.SourcePath,
            SettingsWarning = BuildSettingsWarning(transactionSettings, environmentSettings),
            PreflightRulesWarning = ruleCatalog.LoadWarning,
            ArcGisProSdkLane = ReadString(settingsRoot, "arcgis_pro_sdk_lane") ?? environmentSettings.ArcGisProSdkLane,
            TargetFramework = ReadString(settingsRoot, "target_framework") ?? environmentSettings.TargetFramework,
            CaseFolderOutputRoot = ReadString(settingsRoot, "case_folder_output_root") ?? transactionSettings.CaseFolderOutputRoot,
            ArcGisPythonExecutable = ReadString(settingsRoot, "arcgis_python_executable") ?? environmentSettings.PythonExecutable,
            OutputAdapterScriptPath = ReadString(settingsRoot, "output_adapter_script_path") ?? executionSettings.OutputAdapterScriptPath,
            ValidationAdapterScriptPath = ReadString(settingsRoot, "validation_adapter_script_path") ?? executionSettings.ValidationAdapterScriptPath,
            ValidationRulesPath = ReadString(settingsRoot, "validation_rules_path") ?? executionSettings.ValidationRulesPath ?? string.Empty,
            OutputTemplateProjectPath = ReadString(settingsRoot, "output_template_project_path") ?? executionSettings.OutputTemplateProjectPath ?? string.Empty,
            OutputTemplateGdbPath = ReadString(settingsRoot, "output_template_gdb_path") ?? executionSettings.OutputTemplateGdbPath ?? string.Empty,
            OutputAdapterTimeoutSeconds = ReadPositiveInt(settingsRoot, "output_adapter_timeout_seconds") ?? executionSettings.OutputAdapterTimeoutSeconds,
            OcrEngine = ReadString(settingsRoot, "ocr_engine") ?? "local",
            OpenAiEnabled = ReadBool(settingsRoot, "openai_enabled") ?? false,
            OpenAiExtractionProfile = NormalizeOpenAiExtractionProfile(ReadString(settingsRoot, "openai_extraction_profile")),
            OpenAiModel = ReadString(settingsRoot, "openai_model") ?? string.Empty,
            OpenAiApiKeyEnvironmentVariable = ReadString(settingsRoot, "openai_api_key_environment_variable") ?? "OPENAI_API_KEY",
            InnolaServerUrl = transactionSettings.ServerUrl,
            InnolaTransactionMode = transactionSettings.Mode,
            InnolaProcessStep = transactionSettings.ProcessStep,
            SupportedTransactionTypes = transactionSettings.SupportedTransactionTypes.ToList(),
            ComputeWorkflowStages = transactionSettings.ComputeWorkflowStages.ToList(),
            InnolaAttachmentUploadRoute = transactionSettings.AttachmentUploadRoute,
            InnolaAttachmentUploadBindingMode = transactionSettings.AttachmentUploadBindingMode,
            InnolaAttachmentUploadMode = transactionSettings.AttachmentUploadMode,
            InnolaResumeAttachmentSourceType = transactionSettings.ResumeAttachmentSourceType,
            InnolaCompletedAttachmentSourceType = transactionSettings.CompletedAttachmentSourceType,
            InnolaResumeAttachmentRegisteredType = transactionSettings.ResumeAttachmentRegisteredType,
            InnolaCompletedAttachmentRegisteredType = transactionSettings.CompletedAttachmentRegisteredType,
            InnolaAttachmentRegisteredSpatialUnitId = transactionSettings.AttachmentRegisteredSpatialUnitId ?? string.Empty,
            InnolaClientCertificateEnabled = transactionSettings.ClientCertificate.Enabled,
            InnolaClientCertificateStoreLocation = transactionSettings.ClientCertificate.StoreLocation,
            InnolaClientCertificateStoreName = transactionSettings.ClientCertificate.StoreName,
            InnolaClientCertificateSubject = transactionSettings.ClientCertificate.SubjectName ?? string.Empty,
            InnolaClientCertificateThumbprint = transactionSettings.ClientCertificate.Thumbprint ?? string.Empty,
            InnolaAllowInvalidServerCertificate = transactionSettings.ClientCertificate.AllowInvalidServerCertificate,
            InnolaCheckCertificateRevocationList = transactionSettings.ClientCertificate.CheckCertificateRevocationList,
            ReviewWorkspaceMode = transactionSettings.ReviewWorkspaceMode,
            PdfViewerMode = transactionSettings.PdfViewerMode,
            EnterpriseWorkingEnabled = transactionSettings.EnterpriseWorkingReview.Enabled,
            EnterpriseWorkingServiceRoot = transactionSettings.EnterpriseWorkingReview.ServiceRoot ?? string.Empty,
            EnterpriseWorkingWorkspaceName = transactionSettings.EnterpriseWorkingReview.WorkspaceName,
            EnterpriseWorkingPublishBehavior = transactionSettings.EnterpriseWorkingReview.PublishBehavior,
            EnterpriseWorkingPublishTiming = transactionSettings.EnterpriseWorkingReview.PublishTiming,
            EnterpriseWorkingRestoreBehavior = transactionSettings.EnterpriseWorkingReview.RestoreBehavior,
            EnterpriseWorkingAllowCrossMachineRestore = transactionSettings.EnterpriseWorkingReview.AllowCrossMachineRestore,
            EnterpriseWorkingTransactionScopeField = transactionSettings.EnterpriseWorkingReview.TransactionScopeField,
            EnterpriseWorkingPointsLayer = transactionSettings.EnterpriseWorkingReview.Layers.Points ?? string.Empty,
            EnterpriseWorkingLinesLayer = transactionSettings.EnterpriseWorkingReview.Layers.Lines ?? string.Empty,
            EnterpriseWorkingPolygonsLayer = transactionSettings.EnterpriseWorkingReview.Layers.Polygons ?? string.Empty,
            EnterpriseWorkingIssuesLayer = transactionSettings.EnterpriseWorkingReview.Layers.Issues ?? string.Empty,
            EnterpriseWorkingCaseIndexLayer = transactionSettings.EnterpriseWorkingReview.Layers.CaseIndex ?? string.Empty,
            GsiServerUrl = ReadString(settingsRoot, "gsi_server_url") ?? string.Empty,
            GsiUsername = ReadString(settingsRoot, "gsi_username") ?? string.Empty,
            GsiPasswordMode = NormalizeGsiPasswordMode(ReadString(settingsRoot, "gsi_password_mode")),
            GsiPasswordEnvironmentVariable = ReadString(settingsRoot, "gsi_password_env_var") ?? "GSI_PASSWORD",
            GsiPassword = ReadString(settingsRoot, "gsi_password") ?? string.Empty,
            PreflightRules = ruleCatalog.Rules.Select(EditablePreflightRule.FromDefinition).ToList()
        };
    }

    public IReadOnlyList<SettingsWorkspaceValidationMessage> Validate(SettingsWorkspaceDocument document)
    {
        var messages = new List<SettingsWorkspaceValidationMessage>();

        if (!string.IsNullOrWhiteSpace(document.InnolaServerUrl) && !Uri.TryCreate(document.InnolaServerUrl, UriKind.Absolute, out _))
        {
            messages.Add(new("Innola Integration", "Server URL", "Innola server URL must be a valid absolute URL."));
        }

        if (!string.IsNullOrWhiteSpace(document.GsiServerUrl) && !Uri.TryCreate(document.GsiServerUrl, UriKind.Absolute, out _))
        {
            messages.Add(new("Spatial Workspace", "ArcGIS Enterprise Server", "ArcGIS Enterprise server must be a valid absolute URL."));
        }

        if (document.OutputAdapterTimeoutSeconds <= 0)
        {
            messages.Add(new("General", "Output Timeout", "Output adapter timeout must be a positive number of seconds."));
        }

        if (!document.SupportedTransactionTypes.Any())
        {
            messages.Add(new("Innola Integration", "Supported Transaction Types", "At least one supported transaction type is required."));
        }

        if (!document.ComputeWorkflowStages.Any())
        {
            messages.Add(new("Innola Integration", "Compute Workflow Stages", "At least one compute workflow stage is required."));
        }

        if (document.OpenAiEnabled && string.IsNullOrWhiteSpace(document.OpenAiModel))
        {
            messages.Add(new("AI Toolset", "OpenAI Model", "OpenAI model is required when OpenAI is enabled."));
        }

        if (document.OpenAiEnabled && string.IsNullOrWhiteSpace(document.OpenAiApiKeyEnvironmentVariable))
        {
            messages.Add(new("AI Toolset", "API Key Source", "An OpenAI API key environment variable is required when OpenAI is enabled."));
        }

        if (!IsSupportedOpenAiExtractionProfile(document.OpenAiExtractionProfile))
        {
            messages.Add(new("AI Toolset", "Extraction Profile", $"OpenAI extraction profile '{document.OpenAiExtractionProfile}' is not supported."));
        }

        if (string.Equals(document.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, StringComparison.OrdinalIgnoreCase))
        {
            if (!document.EnterpriseWorkingEnabled)
            {
                messages.Add(new("Spatial Workspace", "Enterprise Working Review", "Enterprise working layers mode requires Enterprise working review to be enabled."));
            }

            if (string.IsNullOrWhiteSpace(document.EnterpriseWorkingPointsLayer)
                || string.IsNullOrWhiteSpace(document.EnterpriseWorkingLinesLayer)
                || string.IsNullOrWhiteSpace(document.EnterpriseWorkingPolygonsLayer)
                || string.IsNullOrWhiteSpace(document.EnterpriseWorkingTransactionScopeField))
            {
                messages.Add(new("Spatial Workspace", "Enterprise Targets", "Enterprise working layers mode requires points, lines, polygons, and transaction scope field values."));
            }
        }

        if (string.Equals(document.GsiPasswordMode, GsiPasswordModeEnvironmentVariable, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(document.GsiPasswordEnvironmentVariable))
            {
                messages.Add(new("Spatial Workspace", "ArcGIS Enterprise Password Source", "ArcGIS Enterprise password environment variable is required when password mode is environment variable."));
            }
        }
        else if (string.Equals(document.GsiPasswordMode, GsiPasswordModeDirect, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(document.GsiPassword))
            {
                messages.Add(new("Spatial Workspace", "ArcGIS Enterprise Password", "A masked direct password value is required when password mode is direct."));
            }
        }

        foreach (var rule in document.PreflightRules)
        {
            var normalizedSeverity = PreflightRuleDefinition.NormalizeSeverity(rule.Severity, string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedSeverity))
            {
                messages.Add(new("Preflight Rules", rule.DisplayName, $"Rule severity '{rule.Severity}' is not supported."));
            }
        }

        return messages;
    }

    public void Save(SettingsWorkspaceDocument document)
    {
        var validationMessages = Validate(document);
        if (validationMessages.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationMessages.Select(message => $"{message.TabName} - {message.FieldName}: {message.Message}")));
        }

        var root = LoadSettingsRoot(document.SettingsPath) ?? new JsonObject();

        SetString(root, "arcgis_pro_sdk_lane", document.ArcGisProSdkLane);
        SetString(root, "target_framework", document.TargetFramework);
        SetString(root, "case_folder_output_root", document.CaseFolderOutputRoot);
        SetString(root, "arcgis_python_executable", document.ArcGisPythonExecutable);
        SetString(root, "output_adapter_script_path", document.OutputAdapterScriptPath);
        SetString(root, "validation_adapter_script_path", document.ValidationAdapterScriptPath);
        SetString(root, "validation_rules_path", document.ValidationRulesPath);
        SetString(root, "output_template_project_path", document.OutputTemplateProjectPath);
        SetString(root, "output_template_gdb_path", document.OutputTemplateGdbPath);
        root["output_adapter_timeout_seconds"] = document.OutputAdapterTimeoutSeconds;
        SetString(root, "ocr_engine", document.OcrEngine);
        root["openai_enabled"] = document.OpenAiEnabled;
        SetString(root, "openai_extraction_profile", NormalizeOpenAiExtractionProfile(document.OpenAiExtractionProfile));
        SetString(root, "openai_model", document.OpenAiModel);
        SetString(root, "openai_api_key_environment_variable", document.OpenAiApiKeyEnvironmentVariable);
        SetString(root, "innola_server_url", document.InnolaServerUrl);
        SetString(root, "innola_transaction_mode", document.InnolaTransactionMode);
        SetString(root, "innola_process_step", document.InnolaProcessStep);
        root["supported_transaction_types"] = CreateStringArray(document.SupportedTransactionTypes);
        root["compute_workflow_stages"] = CreateStringArray(document.ComputeWorkflowStages);
        SetString(root, "innola_attachment_upload_route", document.InnolaAttachmentUploadRoute);
        SetString(root, "innola_attachment_upload_binding_mode", document.InnolaAttachmentUploadBindingMode);
        SetString(root, "innola_attachment_upload_mode", document.InnolaAttachmentUploadMode);
        SetString(root, "innola_resume_attachment_source_type", document.InnolaResumeAttachmentSourceType);
        SetString(root, "innola_completed_attachment_source_type", document.InnolaCompletedAttachmentSourceType);
        SetString(root, "innola_resume_attachment_registered_type", document.InnolaResumeAttachmentRegisteredType);
        SetString(root, "innola_completed_attachment_registered_type", document.InnolaCompletedAttachmentRegisteredType);
        SetString(root, "innola_attachment_registered_spatial_unit_id", document.InnolaAttachmentRegisteredSpatialUnitId);
        root["innola_client_certificate_enabled"] = document.InnolaClientCertificateEnabled;
        SetString(root, "innola_client_certificate_store_location", document.InnolaClientCertificateStoreLocation);
        SetString(root, "innola_client_certificate_store_name", document.InnolaClientCertificateStoreName);
        SetString(root, "innola_client_certificate_subject", document.InnolaClientCertificateSubject);
        SetString(root, "innola_client_certificate_thumbprint", document.InnolaClientCertificateThumbprint);
        root["innola_allow_invalid_server_certificate"] = document.InnolaAllowInvalidServerCertificate;
        root["innola_check_certificate_revocation_list"] = document.InnolaCheckCertificateRevocationList;
        SetString(root, "review_workspace_mode", document.ReviewWorkspaceMode);
        SetString(root, "pdf_viewer_mode", NormalizePdfViewerMode(document.PdfViewerMode));
        root["enterprise_working_review"] = CreateEnterpriseWorkingReviewNode(document);
        SetString(root, "gsi_server_url", document.GsiServerUrl);
        SetString(root, "gsi_username", document.GsiUsername);
        SetString(root, "gsi_password_mode", NormalizeGsiPasswordMode(document.GsiPasswordMode));
        SetString(root, "gsi_password_env_var", document.GsiPasswordEnvironmentVariable);
        SetString(root, "gsi_password", string.Equals(document.GsiPasswordMode, GsiPasswordModeDirect, StringComparison.OrdinalIgnoreCase) ? document.GsiPassword : string.Empty);

        Directory.CreateDirectory(Path.GetDirectoryName(document.SettingsPath) ?? AppContext.BaseDirectory);
        File.WriteAllText(document.SettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        SavePreflightRules(document.PreflightRulesPath, document.PreflightRules);
    }

    private static ProcessingEnvironmentSettings LoadProcessingEnvironmentSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return ProcessingEnvironmentSettings.Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            return new ProcessingEnvironmentSettings(
                ReadString(root, "arcgis_pro_sdk_lane") ?? ProcessingEnvironmentSettings.Default.ArcGisProSdkLane,
                ReadString(root, "target_framework") ?? ProcessingEnvironmentSettings.Default.TargetFramework,
                Environment.ExpandEnvironmentVariables(ReadString(root, "arcgis_python_executable") ?? ProcessingEnvironmentSettings.Default.PythonExecutable),
                ReadStringArray(root, "required_python_packages").Count == 0 ? ProcessingEnvironmentSettings.Default.RequiredPackages : ReadStringArray(root, "required_python_packages"),
                ReadStringArray(root, "optional_python_packages"),
                ReadBool(root, "arcpy_required") ?? ProcessingEnvironmentSettings.Default.ArcPyRequired,
                ReadBool(root, "unknown_arcgis_version_is_warning") ?? ProcessingEnvironmentSettings.Default.UnknownArcGisVersionIsWarning);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
        {
            return ProcessingEnvironmentSettings.Default;
        }
    }

    private static JsonObject? LoadSettingsRoot(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static void SavePreflightRules(string rulesPath, IReadOnlyList<EditablePreflightRule> rules)
    {
        JsonObject root;
        if (File.Exists(rulesPath))
        {
            root = JsonNode.Parse(File.ReadAllText(rulesPath)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root["schema_version"] = "1.0.0";
        var existingRules = root["rules"] as JsonArray ?? new JsonArray();
        var byRuleId = rules.ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);
        var updatedRules = new JsonArray();

        foreach (var existingNode in existingRules)
        {
            if (existingNode is not JsonObject existingRule
                || existingRule["rule_id"] is not JsonValue ruleIdNode
                || ruleIdNode.TryGetValue<string>(out var ruleId) is false
                || string.IsNullOrWhiteSpace(ruleId))
            {
                continue;
            }

            var writableRule = existingRule.DeepClone() as JsonObject ?? new JsonObject();

            if (!byRuleId.TryGetValue(ruleId, out var editableRule))
            {
                updatedRules.Add(writableRule);
                continue;
            }

            writableRule["enabled"] = editableRule.Locked ? existingRule["enabled"]?.GetValue<bool>() ?? true : editableRule.Enabled;
            writableRule["severity"] = editableRule.Locked
                ? existingRule["severity"]?.GetValue<string>() ?? "blocker"
                : PreflightRuleDefinition.NormalizeSeverity(editableRule.Severity, "warning");
            updatedRules.Add(writableRule);
        }

        foreach (var rule in rules)
        {
            var exists = updatedRules
                .OfType<JsonObject>()
                .Any(node => string.Equals(node["rule_id"]?.GetValue<string>(), rule.RuleId, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                continue;
            }

            updatedRules.Add(new JsonObject
            {
                ["rule_id"] = rule.RuleId,
                ["category"] = rule.Category,
                ["display_name"] = rule.DisplayName,
                ["description"] = rule.Description,
                ["enabled"] = rule.Enabled,
                ["severity"] = PreflightRuleDefinition.NormalizeSeverity(rule.Severity, "warning"),
                ["locked"] = rule.Locked
            });
        }

        root["rules"] = updatedRules;

        Directory.CreateDirectory(Path.GetDirectoryName(rulesPath) ?? AppContext.BaseDirectory);
        File.WriteAllText(rulesPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonArray CreateStringArray(IEnumerable<string> values)
    {
        return new JsonArray(values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => JsonValue.Create(value))
            .ToArray());
    }

    private static JsonObject CreateEnterpriseWorkingReviewNode(SettingsWorkspaceDocument document)
    {
        return new JsonObject
        {
            ["enabled"] = document.EnterpriseWorkingEnabled,
            ["service_root"] = document.EnterpriseWorkingServiceRoot,
            ["workspace_name"] = document.EnterpriseWorkingWorkspaceName,
            ["publish_behavior"] = document.EnterpriseWorkingPublishBehavior,
            ["publish_timing"] = document.EnterpriseWorkingPublishTiming,
            ["restore_behavior"] = document.EnterpriseWorkingRestoreBehavior,
            ["allow_cross_machine_restore"] = document.EnterpriseWorkingAllowCrossMachineRestore,
            ["transaction_scope_field"] = document.EnterpriseWorkingTransactionScopeField,
            ["layers"] = new JsonObject
            {
                ["points"] = document.EnterpriseWorkingPointsLayer,
                ["lines"] = document.EnterpriseWorkingLinesLayer,
                ["polygons"] = document.EnterpriseWorkingPolygonsLayer,
                ["issues"] = document.EnterpriseWorkingIssuesLayer,
                ["case_index"] = document.EnterpriseWorkingCaseIndexLayer
            }
        };
    }

    private static string? BuildSettingsWarning(InnolaTransactionSettings transactionSettings, ProcessingEnvironmentSettings environmentSettings)
    {
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(transactionSettings.ReviewWorkspaceModeWarning))
        {
            warnings.Add(transactionSettings.ReviewWorkspaceModeWarning);
        }

        if (!string.IsNullOrWhiteSpace(transactionSettings.SupportedTransactionTypesWarning))
        {
            warnings.Add(transactionSettings.SupportedTransactionTypesWarning);
        }

        if (!string.IsNullOrWhiteSpace(transactionSettings.ComputeWorkflowStagesWarning))
        {
            warnings.Add(transactionSettings.ComputeWorkflowStagesWarning);
        }

        if (string.IsNullOrWhiteSpace(environmentSettings.PythonExecutable))
        {
            warnings.Add("Python executable is using the safe default.");
        }

        return warnings.Count == 0 ? null : string.Join(" ", warnings.Distinct(StringComparer.Ordinal));
    }

    private static string NormalizeGsiPasswordMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            GsiPasswordModeDirect => GsiPasswordModeDirect,
            _ => GsiPasswordModeEnvironmentVariable
        };
    }

    private static string NormalizeOpenAiExtractionProfile(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            OpenAiExtractionProfileBalanced => OpenAiExtractionProfileBalanced,
            OpenAiExtractionProfileHighAccuracy => OpenAiExtractionProfileHighAccuracy,
            _ => OpenAiExtractionProfileCustom
        };
    }

    private static bool IsSupportedOpenAiExtractionProfile(string? value)
    {
        return string.Equals(value, OpenAiExtractionProfileCustom, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, OpenAiExtractionProfileBalanced, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, OpenAiExtractionProfileHighAccuracy, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePdfViewerMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            InnolaTransactionSettings.PdfViewerModeExternalOnly => InnolaTransactionSettings.PdfViewerModeExternalOnly,
            _ => InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser
        };
    }

    private static string? ReadString(JsonObject? root, string name)
    {
        return root?[name]?.GetValue<string>();
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonObject? root, string name)
    {
        return root?[name]?.GetValue<bool>();
    }

    private static bool? ReadBool(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static int? ReadPositiveInt(JsonObject? root, string name)
    {
        if (root?[name] is null)
        {
            return null;
        }

        try
        {
            var value = root[name]!.GetValue<int>();
            return value > 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject? root, string name)
    {
        if (root?[name] is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        return array
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return array
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void SetString(JsonObject root, string name, string? value)
    {
        root[name] = value ?? string.Empty;
    }
}
