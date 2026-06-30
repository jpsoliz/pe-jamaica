using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Settings;

public sealed class SettingsWorkspaceService
{
    public const string GsiPasswordModeEnvironmentVariable = "environment_variable";
    public const string GsiPasswordModeDirect = "direct";
    public const string OpenAiExtractionProfileCustom = "custom";
    public const string OpenAiExtractionProfileBalanced = "balanced";
    public const string OpenAiExtractionProfileHighAccuracy = "high_accuracy";
    public const string SpatialOutputCogoSourceModeSourceThenComputed = "source_then_computed";
    public const string SpatialOutputCogoSourceModePreferSource = "prefer_source";
    public const string SpatialOutputCogoSourceModePreferComputed = "prefer_computed";

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
        var closureCatalog = ClosureToleranceCatalog.Load(settingsPath);
        var readinessCatalog = ReadinessSettingsCatalog.Load(
            ReadString(settingsRoot, "validation_rules_path") ?? executionSettings.ValidationRulesPath,
            settingsRoot);

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
            ComputeAttachmentSourceTypesJson = ResolveComputeAttachmentSourceTypesJson(settingsRoot, transactionSettings),
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
            SpatialOutputAddCogoAttributes = executionSettings.SpatialOutputAddCogoAttributes,
            SpatialOutputAddCogoLabels = executionSettings.SpatialOutputAddCogoLabels,
            SpatialOutputCogoSourceMode = NormalizeSpatialOutputCogoSourceMode(ReadString(settingsRoot, "spatial_output_cogo_source_mode") ?? executionSettings.SpatialOutputCogoSourceMode),
            ClosureDefaultMaxClosureDistanceM = FormatNullableDouble(closureCatalog.Resolve(closureCatalog.DefaultProfile.ParcelType).MaxClosureDistanceM),
            ClosureDefaultMinMiscloseRatioDenominator = FormatNullableDouble(closureCatalog.Resolve(closureCatalog.DefaultProfile.ParcelType).MinMiscloseRatioDenominator),
            ClosureDefaultWarningClosureDistanceM = FormatNullableDouble(closureCatalog.Resolve(closureCatalog.DefaultProfile.ParcelType).WarningClosureDistanceM),
            ClosureDefaultWarningMiscloseRatioDenominator = FormatNullableDouble(closureCatalog.Resolve(closureCatalog.DefaultProfile.ParcelType).WarningMiscloseRatioDenominator),
            ClosureToleranceProfileOverridesJson = ReadJson(settingsRoot, "closure_tolerance_profile_overrides"),
            ReadinessDefaultParcelType = readinessCatalog.DefaultParcelType,
            ReadinessDefaultEnabled = readinessCatalog.DefaultEnabled,
            ReadinessDefaultSeverity = readinessCatalog.DefaultSeverity,
            ReadinessDefaultMinSegmentCount = readinessCatalog.DefaultMinSegmentCount,
            ReadinessDefaultRequireContiguousSequence = readinessCatalog.DefaultRequireContiguousSequence,
            ReadinessDefaultRequireReferencedPoints = readinessCatalog.DefaultRequireReferencedPoints,
            ReadinessDefaultRequireChainConsistency = readinessCatalog.DefaultRequireChainConsistency,
            ReadinessDefaultDetectDuplicateEdges = readinessCatalog.DefaultDetectDuplicateEdges,
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
            EnterpriseParcelFabricEnabled = transactionSettings.EnterpriseParcelFabricReview.Enabled,
            EnterpriseParcelFabricServiceRoot = transactionSettings.EnterpriseParcelFabricReview.ServiceRoot ?? string.Empty,
            EnterpriseParcelFabricFabricLayerUrl = transactionSettings.EnterpriseParcelFabricReview.FabricLayerUrl ?? string.Empty,
            EnterpriseParcelFabricParcelLayerUrl = transactionSettings.EnterpriseParcelFabricReview.ParcelLayerUrl ?? string.Empty,
            EnterpriseParcelFabricRecordsLayerUrl = transactionSettings.EnterpriseParcelFabricReview.RecordsLayerUrl ?? string.Empty,
            EnterpriseParcelFabricParcelTypeName = transactionSettings.EnterpriseParcelFabricReview.ParcelTypeName,
            EnterpriseParcelFabricRecordNamePattern = transactionSettings.EnterpriseParcelFabricReview.RecordNamePattern,
            EnterpriseParcelFabricTransactionScopeField = transactionSettings.EnterpriseParcelFabricReview.TransactionScopeField,
            EnterpriseParcelFabricTransactionIdField = transactionSettings.EnterpriseParcelFabricReview.TransactionIdField,
            EnterpriseParcelFabricReviewStateField = transactionSettings.EnterpriseParcelFabricReview.ReviewStateField,
            EnterpriseParcelFabricPublishTiming = transactionSettings.EnterpriseParcelFabricReview.PublishTiming,
            EnterpriseParcelFabricBuildBehavior = transactionSettings.EnterpriseParcelFabricReview.BuildBehavior,
            EnterpriseParcelFabricLoadOverlays = transactionSettings.EnterpriseParcelFabricReview.LoadOverlays,
            EnterpriseParcelFabricOverlaySource = transactionSettings.EnterpriseParcelFabricReview.OverlaySource,
            EnterpriseParcelFabricAllowReplaceTransactionScope = transactionSettings.EnterpriseParcelFabricReview.AllowReplaceTransactionScope,
            EnterpriseParcelFabricRequireActiveMap = transactionSettings.EnterpriseParcelFabricReview.RequireActiveMap,
            GsiServerUrl = ReadString(settingsRoot, "gsi_server_url") ?? string.Empty,
            GsiUsername = ReadString(settingsRoot, "gsi_username") ?? string.Empty,
            GsiPasswordMode = NormalizeGsiPasswordMode(ReadString(settingsRoot, "gsi_password_mode")),
            GsiPasswordEnvironmentVariable = ReadString(settingsRoot, "gsi_password_env_var") ?? "GSI_PASSWORD",
            GsiPassword = ReadString(settingsRoot, "gsi_password") ?? string.Empty,
            PreflightRules = ruleCatalog.Rules.Select(EditablePreflightRule.FromDefinition).ToList(),
            ReadinessRules = readinessCatalog.Rules
                .Select(rule => new EditableReadinessRule
                {
                    RuleId = rule.RuleId,
                    Title = rule.Title,
                    Category = rule.Category,
                    ParcelType = rule.ParcelType,
                    ScopeSummary = rule.ScopeSummary,
                    IsDefaultFallback = rule.IsDefaultFallback,
                    Enabled = rule.Enabled,
                    Severity = rule.Severity,
                    MinSegmentCount = rule.MinSegmentCount,
                    RequireContiguousSequence = rule.RequireContiguousSequence,
                    RequireReferencedPoints = rule.RequireReferencedPoints,
                    RequireChainConsistency = rule.RequireChainConsistency,
                    DetectDuplicateEdges = rule.DetectDuplicateEdges
                })
                .ToList()
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

        if (string.IsNullOrWhiteSpace(document.ComputeAttachmentSourceTypesJson))
        {
            messages.Add(new("Innola Integration", "Compute Attachment Source Types", "Compute attachment source types JSON is required."));
        }
        else
        {
            try
            {
                JsonNode.Parse(document.ComputeAttachmentSourceTypesJson);
            }
            catch (JsonException exception)
            {
                messages.Add(new("Innola Integration", "Compute Attachment Source Types", $"Compute attachment source types must be valid JSON. {exception.Message}"));
            }
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

        if (!IsSupportedSpatialOutputCogoSourceMode(document.SpatialOutputCogoSourceMode))
        {
            messages.Add(new("Spatial Workspace", "COGO Source Mode", $"COGO source mode '{document.SpatialOutputCogoSourceMode}' is not supported."));
        }

        var closureMaxDistance = ParsePositiveDouble(document.ClosureDefaultMaxClosureDistanceM);
        if (closureMaxDistance is null)
        {
            messages.Add(new("Spatial Workspace", "Closure max distance", "Closure max distance must be a positive number."));
        }

        var closureWarningDistance = ParsePositiveDouble(document.ClosureDefaultWarningClosureDistanceM);
        if (closureWarningDistance is null)
        {
            messages.Add(new("Spatial Workspace", "Closure warning distance", "Closure warning distance must be a positive number."));
        }

        var closureBlockRatio = ParsePositiveDouble(document.ClosureDefaultMinMiscloseRatioDenominator);
        if (closureBlockRatio is null)
        {
            messages.Add(new("Spatial Workspace", "Closure blocker ratio", "Closure blocker ratio must be a positive number."));
        }

        var closureWarningRatio = ParsePositiveDouble(document.ClosureDefaultWarningMiscloseRatioDenominator);
        if (closureWarningRatio is null)
        {
            messages.Add(new("Spatial Workspace", "Closure warning ratio", "Closure warning ratio must be a positive number."));
        }

        if (closureMaxDistance is not null && closureWarningDistance is not null && closureWarningDistance > closureMaxDistance)
        {
            messages.Add(new("Spatial Workspace", "Closure warning distance", "Closure warning distance should be less than or equal to the blocker distance."));
        }

        if (closureBlockRatio is not null && closureWarningRatio is not null && closureWarningRatio < closureBlockRatio)
        {
            messages.Add(new("Spatial Workspace", "Closure warning ratio", "Closure warning ratio should be greater than or equal to the blocker ratio denominator."));
        }

        if (!string.IsNullOrWhiteSpace(document.ClosureToleranceProfileOverridesJson))
        {
            try
            {
                JsonNode.Parse(document.ClosureToleranceProfileOverridesJson);
            }
            catch (JsonException exception)
            {
                messages.Add(new("Spatial Workspace", "Closure Tolerance Overrides", $"Closure tolerance overrides must be valid JSON. {exception.Message}"));
            }
        }

        if (string.IsNullOrWhiteSpace(document.ReadinessDefaultParcelType))
        {
            messages.Add(new("Spatial Workspace", "Readiness Default Parcel Type", "A default parcel type is required for readiness validation."));
        }

        if (document.ReadinessDefaultMinSegmentCount <= 0)
        {
            messages.Add(new("Spatial Workspace", "Readiness Default Minimum Segment Count", "Default readiness minimum segment count must be a positive integer."));
        }

        if (!IsSupportedReadinessSeverity(document.ReadinessDefaultSeverity))
        {
            messages.Add(new("Spatial Workspace", "Readiness Default Severity", $"Readiness default severity '{document.ReadinessDefaultSeverity}' is not supported."));
        }

        foreach (var rule in document.ReadinessRules)
        {
            if (!IsSupportedReadinessSeverity(rule.Severity))
            {
                messages.Add(new("Spatial Workspace", $"Readiness Rule {rule.Title}", $"Severity '{rule.Severity}' is not supported."));
            }

            if (string.Equals(rule.Category, "minimum_segment_count", StringComparison.OrdinalIgnoreCase) && rule.MinSegmentCount <= 0)
            {
                messages.Add(new("Spatial Workspace", $"Readiness Rule {rule.Title}", "Minimum segment count must be a positive integer."));
            }
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

        if (string.Equals(document.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, StringComparison.OrdinalIgnoreCase))
        {
            if (!document.EnterpriseParcelFabricEnabled)
            {
                messages.Add(new("Spatial Workspace", "Enterprise Parcel Fabric", "Enterprise Parcel Fabric mode requires Enterprise Parcel Fabric review to be enabled."));
            }

            if (string.IsNullOrWhiteSpace(document.EnterpriseParcelFabricFabricLayerUrl)
                || string.IsNullOrWhiteSpace(document.EnterpriseParcelFabricRecordsLayerUrl)
                || string.IsNullOrWhiteSpace(document.EnterpriseParcelFabricParcelTypeName)
                || string.IsNullOrWhiteSpace(document.EnterpriseParcelFabricRecordNamePattern)
                || string.IsNullOrWhiteSpace(document.EnterpriseParcelFabricTransactionScopeField))
            {
                messages.Add(new("Spatial Workspace", "Enterprise Parcel Fabric Targets", "Enterprise Parcel Fabric mode requires fabric layer URL, records layer URL, parcel type name, record name pattern, and transaction scope field values."));
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
        SetJson(root, "compute_attachment_source_types", document.ComputeAttachmentSourceTypesJson);
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
        root["spatial_output_add_cogo_attributes"] = document.SpatialOutputAddCogoAttributes;
        root["spatial_output_add_cogo_labels"] = document.SpatialOutputAddCogoLabels;
        SetString(root, "spatial_output_cogo_source_mode", NormalizeSpatialOutputCogoSourceMode(document.SpatialOutputCogoSourceMode));
        SetJson(root, "closure_tolerance_profile_overrides", BuildClosureToleranceOverridesJson(document));
        SetJson(root, "parcel_construction_readiness_profile_overrides", BuildReadinessOverridesJson(document));
        root["enterprise_working_review"] = CreateEnterpriseWorkingReviewNode(document);
        root["enterprise_parcel_fabric_review"] = CreateEnterpriseParcelFabricReviewNode(document);
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

            writableRule["group"] = editableRule.Group;
            writableRule["category"] = editableRule.Category;
            writableRule["display_name"] = editableRule.DisplayName;
            writableRule["description"] = editableRule.Description;
            writableRule["locked"] = editableRule.Locked;
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
                ["group"] = rule.Group,
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

    private static string BuildClosureToleranceOverridesJson(SettingsWorkspaceDocument document)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(document.ClosureToleranceProfileOverridesJson))
        {
            root = new JsonObject();
        }
        else
        {
            root = JsonNode.Parse(document.ClosureToleranceProfileOverridesJson) as JsonObject ?? new JsonObject();
        }

        var profiles = root["profiles"] as JsonObject ?? new JsonObject();
        root["profiles"] = profiles;

        var standardClosed = profiles["standard_closed"] as JsonObject ?? new JsonObject();
        profiles["standard_closed"] = standardClosed;

        standardClosed["max_closure_distance_m"] = ParsePositiveDouble(document.ClosureDefaultMaxClosureDistanceM);
        standardClosed["min_misclose_ratio_denominator"] = ParsePositiveDouble(document.ClosureDefaultMinMiscloseRatioDenominator);
        standardClosed["warning_closure_distance_m"] = ParsePositiveDouble(document.ClosureDefaultWarningClosureDistanceM);
        standardClosed["warning_misclose_ratio_denominator"] = ParsePositiveDouble(document.ClosureDefaultWarningMiscloseRatioDenominator);

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildReadinessOverridesJson(SettingsWorkspaceDocument document)
    {
        var root = new JsonObject
        {
            ["default_parcel_type"] = document.ReadinessDefaultParcelType,
            ["default_profile"] = new JsonObject
            {
                ["enabled"] = document.ReadinessDefaultEnabled,
                ["severity"] = document.ReadinessDefaultSeverity,
                ["min_segment_count"] = document.ReadinessDefaultMinSegmentCount,
                ["require_contiguous_sequence"] = document.ReadinessDefaultRequireContiguousSequence,
                ["require_referenced_points"] = document.ReadinessDefaultRequireReferencedPoints,
                ["require_chain_consistency"] = document.ReadinessDefaultRequireChainConsistency,
                ["detect_duplicate_edges"] = document.ReadinessDefaultDetectDuplicateEdges
            }
        };

        var profiles = new JsonObject();
        foreach (var rule in document.ReadinessRules)
        {
            var key = $"{rule.ParcelType}::{rule.Category}";
            profiles[key] = new JsonObject
            {
                ["rule_id"] = rule.RuleId,
                ["title"] = rule.Title,
                ["category"] = rule.Category,
                ["parcel_type"] = rule.ParcelType,
                ["enabled"] = rule.Enabled,
                ["severity"] = rule.Severity,
                ["min_segment_count"] = rule.MinSegmentCount,
                ["require_contiguous_sequence"] = rule.RequireContiguousSequence,
                ["require_referenced_points"] = rule.RequireReferencedPoints,
                ["require_chain_consistency"] = rule.RequireChainConsistency,
                ["detect_duplicate_edges"] = rule.DetectDuplicateEdges
            };
        }

        root["profiles"] = profiles;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatNullableDouble(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static double? ParsePositiveDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value) && value > 0d)
        {
            return value;
        }

        if (double.TryParse(text.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value) && value > 0d)
        {
            return value;
        }

        return null;
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

    private static JsonObject CreateEnterpriseParcelFabricReviewNode(SettingsWorkspaceDocument document)
    {
        return new JsonObject
        {
            ["enabled"] = document.EnterpriseParcelFabricEnabled,
            ["service_root"] = document.EnterpriseParcelFabricServiceRoot,
            ["fabric_layer_url"] = document.EnterpriseParcelFabricFabricLayerUrl,
            ["parcel_layer_url"] = document.EnterpriseParcelFabricParcelLayerUrl,
            ["records_layer_url"] = document.EnterpriseParcelFabricRecordsLayerUrl,
            ["parcel_type_name"] = document.EnterpriseParcelFabricParcelTypeName,
            ["record_name_pattern"] = document.EnterpriseParcelFabricRecordNamePattern,
            ["transaction_scope_field"] = document.EnterpriseParcelFabricTransactionScopeField,
            ["transaction_id_field"] = document.EnterpriseParcelFabricTransactionIdField,
            ["review_state_field"] = document.EnterpriseParcelFabricReviewStateField,
            ["publish_timing"] = document.EnterpriseParcelFabricPublishTiming,
            ["build_behavior"] = document.EnterpriseParcelFabricBuildBehavior,
            ["load_overlays"] = document.EnterpriseParcelFabricLoadOverlays,
            ["overlay_source"] = document.EnterpriseParcelFabricOverlaySource,
            ["allow_replace_transaction_scope"] = document.EnterpriseParcelFabricAllowReplaceTransactionScope,
            ["require_active_map"] = document.EnterpriseParcelFabricRequireActiveMap
        };
    }

    private sealed record ReadinessSettingsRule(
        string RuleId,
        string Title,
        string Category,
        string ParcelType,
        string ScopeSummary,
        bool IsDefaultFallback,
        bool Enabled,
        string Severity,
        int MinSegmentCount,
        bool RequireContiguousSequence,
        bool RequireReferencedPoints,
        bool RequireChainConsistency,
        bool DetectDuplicateEdges);

    private sealed class ReadinessSettingsCatalog
    {
        public string DefaultParcelType { get; set; } = "standard_closed";
        public bool DefaultEnabled { get; set; } = true;
        public string DefaultSeverity { get; set; } = "blocker";
        public int DefaultMinSegmentCount { get; set; } = 3;
        public bool DefaultRequireContiguousSequence { get; set; } = true;
        public bool DefaultRequireReferencedPoints { get; set; } = true;
        public bool DefaultRequireChainConsistency { get; set; } = true;
        public bool DefaultDetectDuplicateEdges { get; set; } = true;
        public List<ReadinessSettingsRule> Rules { get; } = new();

        public static ReadinessSettingsCatalog Load(string? rulesPath, JsonObject? settingsRoot)
        {
            var catalog = Parse(rulesPath);
            ApplyOverrides(catalog, settingsRoot);
            return catalog;
        }

        private static ReadinessSettingsCatalog Parse(string? rulesPath)
        {
            var catalog = new ReadinessSettingsCatalog();
            if (string.IsNullOrWhiteSpace(rulesPath) || !File.Exists(rulesPath))
            {
                return catalog;
            }

            var lines = File.ReadAllLines(rulesPath);
            var section = string.Empty;
            var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var skipIndent = -1;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var indent = line.Length - line.TrimStart().Length;
                if (indent == 0)
                {
                    if (string.Equals(section, "parcel_construction_readiness_profiles", StringComparison.OrdinalIgnoreCase))
                    {
                        CommitRule(catalog, current);
                        current.Clear();
                    }

                    section = trimmed.TrimEnd(':');
                    skipIndent = -1;
                    continue;
                }

                if (skipIndent >= 0 && indent > skipIndent)
                {
                    continue;
                }

                if (skipIndent >= 0 && indent <= skipIndent)
                {
                    skipIndent = -1;
                }

                var working = trimmed;
                if (working.StartsWith("- ", StringComparison.Ordinal))
                {
                    if (string.Equals(section, "parcel_construction_readiness_profiles", StringComparison.OrdinalIgnoreCase))
                    {
                        CommitRule(catalog, current);
                        current.Clear();
                    }

                    working = working[2..].TrimStart();
                }

                var splitIndex = working.IndexOf(':');
                if (splitIndex < 0)
                {
                    continue;
                }

                var key = working[..splitIndex].Trim();
                var value = working[(splitIndex + 1)..].Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(value))
                {
                    skipIndent = indent;
                    continue;
                }

                current[key] = value;

                if (string.Equals(section, "parcel_construction_readiness_defaults", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyDefaultValue(catalog, key, value);
                }
            }

            if (string.Equals(section, "parcel_construction_readiness_profiles", StringComparison.OrdinalIgnoreCase))
            {
                CommitRule(catalog, current);
            }

            return catalog;
        }

        private static void ApplyOverrides(ReadinessSettingsCatalog catalog, JsonObject? settingsRoot)
        {
            if (settingsRoot?["parcel_construction_readiness_profile_overrides"] is not JsonObject overrides)
            {
                return;
            }

            if (overrides["default_parcel_type"] is JsonValue defaultParcelTypeValue
                && defaultParcelTypeValue.TryGetValue<string>(out var defaultParcelType)
                && !string.IsNullOrWhiteSpace(defaultParcelType))
            {
                catalog.DefaultParcelType = defaultParcelType.Trim();
            }

            if (overrides["default_profile"] is JsonObject defaultProfile)
            {
                catalog.DefaultEnabled = defaultProfile["enabled"]?.GetValue<bool>() ?? catalog.DefaultEnabled;
                catalog.DefaultSeverity = defaultProfile["severity"]?.GetValue<string>() ?? catalog.DefaultSeverity;
                catalog.DefaultMinSegmentCount = defaultProfile["min_segment_count"]?.GetValue<int>() ?? catalog.DefaultMinSegmentCount;
                catalog.DefaultRequireContiguousSequence = defaultProfile["require_contiguous_sequence"]?.GetValue<bool>() ?? catalog.DefaultRequireContiguousSequence;
                catalog.DefaultRequireReferencedPoints = defaultProfile["require_referenced_points"]?.GetValue<bool>() ?? catalog.DefaultRequireReferencedPoints;
                catalog.DefaultRequireChainConsistency = defaultProfile["require_chain_consistency"]?.GetValue<bool>() ?? catalog.DefaultRequireChainConsistency;
                catalog.DefaultDetectDuplicateEdges = defaultProfile["detect_duplicate_edges"]?.GetValue<bool>() ?? catalog.DefaultDetectDuplicateEdges;
            }

            if (overrides["profiles"] is not JsonObject profiles)
            {
                return;
            }

            for (var index = 0; index < catalog.Rules.Count; index++)
            {
                var rule = catalog.Rules[index];
                var key = $"{rule.ParcelType}::{rule.Category}";
                if (profiles[key] is not JsonObject ruleOverride)
                {
                    continue;
                }

                catalog.Rules[index] = rule with
                {
                    Enabled = ruleOverride["enabled"]?.GetValue<bool>() ?? rule.Enabled,
                    Severity = ruleOverride["severity"]?.GetValue<string>() ?? rule.Severity,
                    MinSegmentCount = ruleOverride["min_segment_count"]?.GetValue<int>() ?? rule.MinSegmentCount,
                    RequireContiguousSequence = ruleOverride["require_contiguous_sequence"]?.GetValue<bool>() ?? rule.RequireContiguousSequence,
                    RequireReferencedPoints = ruleOverride["require_referenced_points"]?.GetValue<bool>() ?? rule.RequireReferencedPoints,
                    RequireChainConsistency = ruleOverride["require_chain_consistency"]?.GetValue<bool>() ?? rule.RequireChainConsistency,
                    DetectDuplicateEdges = ruleOverride["detect_duplicate_edges"]?.GetValue<bool>() ?? rule.DetectDuplicateEdges
                };
            }
        }

        private static void ApplyDefaultValue(ReadinessSettingsCatalog catalog, string key, string value)
        {
            switch (key)
            {
                case "parcel_type":
                    catalog.DefaultParcelType = value;
                    break;
                case "enabled":
                    catalog.DefaultEnabled = bool.TryParse(value, out var enabled) ? enabled : catalog.DefaultEnabled;
                    break;
                case "severity":
                    catalog.DefaultSeverity = value;
                    break;
                case "min_segment_count":
                    catalog.DefaultMinSegmentCount = int.TryParse(value, out var minSegmentCount) ? minSegmentCount : catalog.DefaultMinSegmentCount;
                    break;
                case "require_contiguous_sequence":
                    catalog.DefaultRequireContiguousSequence = bool.TryParse(value, out var contiguous) ? contiguous : catalog.DefaultRequireContiguousSequence;
                    break;
                case "require_referenced_points":
                    catalog.DefaultRequireReferencedPoints = bool.TryParse(value, out var referenced) ? referenced : catalog.DefaultRequireReferencedPoints;
                    break;
                case "require_chain_consistency":
                    catalog.DefaultRequireChainConsistency = bool.TryParse(value, out var chain) ? chain : catalog.DefaultRequireChainConsistency;
                    break;
                case "detect_duplicate_edges":
                    catalog.DefaultDetectDuplicateEdges = bool.TryParse(value, out var duplicateEdges) ? duplicateEdges : catalog.DefaultDetectDuplicateEdges;
                    break;
            }
        }

        private static void CommitRule(ReadinessSettingsCatalog catalog, Dictionary<string, string> values)
        {
            if (values.Count == 0)
            {
                return;
            }

            var category = GetValue(values, "category");
            if (string.IsNullOrWhiteSpace(category))
            {
                return;
            }

            var parcelType = GetValue(values, "parcel_type") ?? catalog.DefaultParcelType;
            var isDefault = string.Equals(parcelType, catalog.DefaultParcelType, StringComparison.OrdinalIgnoreCase);
            catalog.Rules.Add(new ReadinessSettingsRule(
                GetValue(values, "rule_id") ?? $"readiness_{category}",
                GetValue(values, "title") ?? category.Replace("_", " "),
                category,
                parcelType,
                isDefault
                    ? $"Default fallback · Parcel type: {parcelType}"
                    : $"Scoped rule · Parcel type: {parcelType}",
                isDefault,
                TryParseBool(GetValue(values, "enabled")) ?? catalog.DefaultEnabled,
                GetValue(values, "severity") ?? catalog.DefaultSeverity,
                TryParseInt(GetValue(values, "min_segment_count")) ?? catalog.DefaultMinSegmentCount,
                TryParseBool(GetValue(values, "require_contiguous_sequence")) ?? catalog.DefaultRequireContiguousSequence,
                TryParseBool(GetValue(values, "require_referenced_points")) ?? catalog.DefaultRequireReferencedPoints,
                TryParseBool(GetValue(values, "require_chain_consistency")) ?? catalog.DefaultRequireChainConsistency,
                TryParseBool(GetValue(values, "detect_duplicate_edges")) ?? catalog.DefaultDetectDuplicateEdges));
        }

        private static string? GetValue(Dictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out var value) ? value : null;
        }

        private static bool? TryParseBool(string? value)
        {
            return bool.TryParse(value, out var parsed) ? parsed : null;
        }

        private static int? TryParseInt(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }
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

        if (!string.IsNullOrWhiteSpace(transactionSettings.EnterpriseWorkingReview.Warning))
        {
            warnings.Add(transactionSettings.EnterpriseWorkingReview.Warning);
        }

        if (!string.IsNullOrWhiteSpace(transactionSettings.EnterpriseParcelFabricReview.Warning))
        {
            warnings.Add(transactionSettings.EnterpriseParcelFabricReview.Warning);
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

    private static string NormalizeSpatialOutputCogoSourceMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            SpatialOutputCogoSourceModePreferSource => SpatialOutputCogoSourceModePreferSource,
            SpatialOutputCogoSourceModePreferComputed => SpatialOutputCogoSourceModePreferComputed,
            _ => SpatialOutputCogoSourceModeSourceThenComputed
        };
    }

    private static bool IsSupportedSpatialOutputCogoSourceMode(string? value)
    {
        return string.Equals(value, SpatialOutputCogoSourceModeSourceThenComputed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, SpatialOutputCogoSourceModePreferSource, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, SpatialOutputCogoSourceModePreferComputed, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedReadinessSeverity(string? value)
    {
        return string.Equals(value, "blocker", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "info", StringComparison.OrdinalIgnoreCase);
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

    private static string ReadJson(JsonObject? root, string name)
    {
        if (root?[name] is null)
        {
            return string.Empty;
        }

        return root[name]!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void SetJson(JsonObject root, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            root[name] = new JsonObject();
            return;
        }

        root[name] = JsonNode.Parse(value);
    }

    private static string ResolveComputeAttachmentSourceTypesJson(JsonObject? root, InnolaTransactionSettings transactionSettings)
    {
        var configured = ReadJson(root, "compute_attachment_source_types");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var nodes = transactionSettings.ComputeAttachmentSourceTypes
            .Select(item => new JsonObject
            {
                ["source_type"] = item.SourceType,
                ["workflow_role"] = item.WorkflowRole,
                ["display_name"] = item.DisplayName,
                ["required"] = item.Required,
                ["internal_only"] = item.InternalOnly,
                ["extensions"] = new JsonArray(item.Extensions.Select(extension => (JsonNode?)JsonValue.Create(extension)).ToArray())
            })
            .ToArray();

        return new JsonArray(nodes).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
