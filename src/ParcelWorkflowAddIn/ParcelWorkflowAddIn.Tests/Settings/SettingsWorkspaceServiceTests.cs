using ParcelWorkflowAddIn.Settings;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Innola;
using System.IO;

namespace ParcelWorkflowAddIn.Tests.Settings;

internal static class SettingsWorkspaceServiceTests
{
    public static void SettingsWorkspaceExposesExpectedTabsAndLoadsCurrentFields()
    {
        using var tempDirectory = new TempDirectory();
        var settingsPath = Path.Combine(tempDirectory.Path, "WorkflowSettings.json");
        var rulesPath = Path.Combine(tempDirectory.Path, "PreflightRules.json");
        File.WriteAllText(settingsPath,
            """
            {
              "innola_server_url": "https://example.local/",
              "innola_transaction_mode": "live",
              "innola_process_step": "parcel_workflow",
              "case_folder_output_root": "C:\\Cases",
              "arcgis_python_executable": "C:\\Python\\python.exe",
              "ocr_engine": "openai",
              "openai_enabled": true,
              "openai_extraction_profile": "balanced",
              "openai_model": "gpt-4.1-mini",
              "openai_api_key_environment_variable": "OPENAI_API_KEY",
              "pdf_viewer_mode": "external_only",
              "review_workspace_mode": "enterprise_parcel_fabric",
              "spatial_output_add_cogo_attributes": true,
              "spatial_output_add_cogo_labels": true,
              "spatial_output_cogo_source_mode": "prefer_source",
              "enterprise_parcel_fabric_review": {
                "enabled": true,
                "service_root": "https://fabric.local/server/rest/services",
                "fabric_layer_url": "https://fabric.local/server/rest/services/Fabric/FeatureServer/0",
                "parcel_layer_url": "https://fabric.local/server/rest/services/Fabric/FeatureServer/3",
                "records_layer_url": "https://fabric.local/server/rest/services/Fabric/FeatureServer/5",
                "parcel_type_name": "compute_review",
                "record_name_pattern": "sidwell-record-{transaction_number}",
                "transaction_scope_field": "transaction_number",
                "transaction_id_field": "transaction_id",
                "review_state_field": "review_state",
                "publish_timing": "on_outputs",
                "build_behavior": "build_after_copy",
                "load_overlays": true,
                "overlay_source": "local_case_outputs",
                "allow_replace_transaction_scope": true,
                "require_active_map": true
              },
              "gsi_server_url": "https://gsi.local/",
              "gsi_username": "gsi-user",
              "gsi_password_mode": "environment_variable",
              "gsi_password_env_var": "GSI_PASSWORD"
            }
            """);
        File.WriteAllText(rulesPath,
            """
            {
              "schema_version": "1.0.0",
              "rules": [
                {
                  "rule_id": "detected_profile_presence",
                  "category": "manifest",
                  "display_name": "Detected profile present",
                  "description": "Detected intake profile must be present before preflight can continue.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "detected_profile_complete",
                  "category": "manifest",
                  "display_name": "Detected profile complete",
                  "description": "Incomplete intake remains blocked until required source roles are resolved.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "required_source_roles",
                  "category": "manifest",
                  "display_name": "Required source roles",
                  "description": "Each workflow profile must provide the required copied source roles.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "source_file_integrity",
                  "category": "manifest",
                  "display_name": "Copied source integrity",
                  "description": "Copied source paths must stay inside the case folder, exist, use supported extensions, and remain readable.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workflow_rule_resolution",
                  "category": "workflow_rule",
                  "display_name": "Workflow rule resolution",
                  "description": "Transactions must resolve to a current workflow rule and script plan.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_sdk_lane",
                  "category": "arcgis_pro",
                  "display_name": "ArcGIS Pro SDK lane",
                  "description": "SDK lane and target framework must match the supported ArcGIS Pro 3.6 add-in lane.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workspace_access",
                  "category": "write_access",
                  "display_name": "Workspace access",
                  "description": "Case folder working, output, and summary locations must remain writable.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "python_executable_health",
                  "category": "python",
                  "display_name": "Python executable health",
                  "description": "Configured Python executable must be set, exist, and be invokable.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_unknown_version_behavior",
                  "category": "arcgis_pro",
                  "display_name": "Unknown ArcGIS Pro version handling",
                  "description": "Controls whether unknown ArcGIS Pro version detection is treated as a warning or blocker.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "python_package_probe",
                  "category": "python",
                  "display_name": "Python package probe",
                  "description": "Checks configured required and optional Python packages such as ArcPy before downstream processing runs.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false
                },
                {
                  "rule_id": "dwg_signature_check",
                  "category": "dwg",
                  "display_name": "DWG file signature",
                  "description": "DWG reference files must be non-empty and contain a recognizable DWG signature.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "dwg_readiness_probe",
                  "category": "dwg",
                  "display_name": "DWG readiness probe",
                  "description": "Optional CAD sub-layer readiness probe for copied DWG references.",
                  "enabled": false,
                  "severity": "warning",
                  "locked": false
                }
              ]
            }
            """);

        var service = new SettingsWorkspaceService(new PreflightRuleCatalogLoader(rulesPath, settingsPath));
        var document = service.Load(settingsPath);

        TestAssert.Equal(5, SettingsWorkspaceDocument.TabNames.Count, "Tab count mismatch.");
        TestAssert.Equal("General", SettingsWorkspaceDocument.TabNames[0], "First tab mismatch.");
        TestAssert.Equal("Spatial Workspace", SettingsWorkspaceDocument.TabNames[4], "Last tab mismatch.");
        TestAssert.Equal("https://example.local/", document.InnolaServerUrl, "Innola server mismatch.");
        TestAssert.Equal("openai", document.OcrEngine, "OCR engine mismatch.");
        TestAssert.True(document.OpenAiEnabled, "OpenAI enabled mismatch.");
        TestAssert.Equal("balanced", document.OpenAiExtractionProfile, "OpenAI extraction profile mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.PdfViewerModeExternalOnly, document.PdfViewerMode, "PDF viewer mode mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, document.ReviewWorkspaceMode, "Review workspace mode mismatch.");
        TestAssert.True(document.SpatialOutputAddCogoAttributes, "COGO attributes toggle mismatch.");
        TestAssert.True(document.SpatialOutputAddCogoLabels, "COGO labels toggle mismatch.");
        TestAssert.Equal(SettingsWorkspaceService.SpatialOutputCogoSourceModePreferSource, document.SpatialOutputCogoSourceMode, "COGO source mode mismatch.");
        TestAssert.True(document.EnterpriseParcelFabricEnabled, "Enterprise Parcel Fabric enabled mismatch.");
        TestAssert.Equal("compute_review", document.EnterpriseParcelFabricParcelTypeName, "Enterprise Parcel Fabric parcel type mismatch.");
        TestAssert.Equal("https://fabric.local/server/rest/services/Fabric/FeatureServer/0", document.EnterpriseParcelFabricFabricLayerUrl, "Enterprise Parcel Fabric fabric layer mismatch.");
        TestAssert.Equal("https://gsi.local/", document.GsiServerUrl, "GSI server mismatch.");
        TestAssert.Equal("gsi-user", document.GsiUsername, "GSI user mismatch.");
        TestAssert.Equal(SettingsWorkspaceService.GsiPasswordModeEnvironmentVariable, document.GsiPasswordMode, "GSI password mode mismatch.");
        TestAssert.Equal(12, document.PreflightRules.Count, "Preflight rules count mismatch.");
    }

    public static void SettingsWorkspaceSaveRoundTripPersistsWorkflowAndRuleEdits()
    {
        using var tempDirectory = new TempDirectory();
        var settingsPath = Path.Combine(tempDirectory.Path, "WorkflowSettings.json");
        var rulesPath = Path.Combine(tempDirectory.Path, "PreflightRules.json");
        File.WriteAllText(settingsPath,
            """
            {
              "innola_server_url": "https://example.local/",
              "innola_transaction_mode": "mock",
              "innola_process_step": "parcel_workflow",
              "supported_transaction_types": ["Plan Examination"],
              "compute_workflow_stages": ["Compute Survey Plan"],
              "output_adapter_timeout_seconds": 120,
              "gsi_password_mode": "environment_variable",
              "gsi_password_env_var": "GSI_PASSWORD"
            }
            """);
        File.WriteAllText(rulesPath,
            """
            {
              "schema_version": "1.0.0",
              "rules": [
                {
                  "rule_id": "detected_profile_presence",
                  "category": "manifest",
                  "display_name": "Detected profile present",
                  "description": "Detected intake profile must be present before preflight can continue.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "detected_profile_complete",
                  "category": "manifest",
                  "display_name": "Detected profile complete",
                  "description": "Incomplete intake remains blocked until required source roles are resolved.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "required_source_roles",
                  "category": "manifest",
                  "display_name": "Required source roles",
                  "description": "Each workflow profile must provide the required copied source roles.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "source_file_integrity",
                  "category": "manifest",
                  "display_name": "Copied source integrity",
                  "description": "Copied source paths must stay inside the case folder, exist, use supported extensions, and remain readable.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workflow_rule_resolution",
                  "category": "workflow_rule",
                  "display_name": "Workflow rule resolution",
                  "description": "Transactions must resolve to a current workflow rule and script plan.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_sdk_lane",
                  "category": "arcgis_pro",
                  "display_name": "ArcGIS Pro SDK lane",
                  "description": "SDK lane and target framework must match the supported ArcGIS Pro 3.6 add-in lane.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "workspace_access",
                  "category": "write_access",
                  "display_name": "Workspace access",
                  "description": "Case folder working, output, and summary locations must remain writable.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "python_executable_health",
                  "category": "python",
                  "display_name": "Python executable health",
                  "description": "Configured Python executable must be set, exist, and be invokable.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "arcgis_unknown_version_behavior",
                  "category": "arcgis_pro",
                  "display_name": "Unknown ArcGIS Pro version handling",
                  "description": "Controls whether unknown ArcGIS Pro version detection is treated as a warning or blocker.",
                  "enabled": true,
                  "severity": "warning",
                  "locked": false
                },
                {
                  "rule_id": "python_package_probe",
                  "category": "python",
                  "display_name": "Python package probe",
                  "description": "Checks configured required and optional Python packages such as ArcPy before downstream processing runs.",
                  "enabled": true,
                  "severity": "configured",
                  "locked": false
                },
                {
                  "rule_id": "dwg_signature_check",
                  "category": "dwg",
                  "display_name": "DWG file signature",
                  "description": "DWG reference files must be non-empty and contain a recognizable DWG signature.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": true
                },
                {
                  "rule_id": "dwg_readiness_probe",
                  "category": "dwg",
                  "display_name": "DWG readiness probe",
                  "description": "Optional CAD sub-layer readiness probe for copied DWG references.",
                  "enabled": true,
                  "severity": "blocker",
                  "locked": false
                }
              ]
            }
            """);

        var service = new SettingsWorkspaceService(new PreflightRuleCatalogLoader(rulesPath, settingsPath));
        var document = service.Load(settingsPath);
        document.InnolaTransactionMode = "live";
        document.InnolaServerUrl = "https://saved.local/";
        document.SupportedTransactionTypes = new List<string> { "Plan Examination", "Cadastral Plan Examination" };
        document.ComputeWorkflowStages = new List<string> { "Compute Survey Plan", "Assign Computation Task" };
        document.OutputAdapterTimeoutSeconds = 600;
        document.OpenAiExtractionProfile = SettingsWorkspaceService.OpenAiExtractionProfileHighAccuracy;
        document.OpenAiModel = "gpt-4.1";
        document.PdfViewerMode = InnolaTransactionSettings.PdfViewerModeExternalOnly;
        document.ReviewWorkspaceMode = InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric;
        document.SpatialOutputAddCogoAttributes = true;
        document.SpatialOutputAddCogoLabels = true;
        document.SpatialOutputCogoSourceMode = SettingsWorkspaceService.SpatialOutputCogoSourceModePreferComputed;
        document.EnterpriseParcelFabricEnabled = true;
        document.EnterpriseParcelFabricServiceRoot = "https://fabric.local/server/rest/services";
        document.EnterpriseParcelFabricFabricLayerUrl = "https://fabric.local/server/rest/services/Fabric/FeatureServer/0";
        document.EnterpriseParcelFabricParcelLayerUrl = "https://fabric.local/server/rest/services/Fabric/FeatureServer/3";
        document.EnterpriseParcelFabricRecordsLayerUrl = "https://fabric.local/server/rest/services/Fabric/FeatureServer/5";
        document.EnterpriseParcelFabricParcelTypeName = "compute_review";
        document.EnterpriseParcelFabricRecordNamePattern = "sidwell-record-{transaction_number}";
        document.EnterpriseParcelFabricTransactionScopeField = "transaction_number";
        document.EnterpriseParcelFabricTransactionIdField = "transaction_id";
        document.EnterpriseParcelFabricReviewStateField = "review_state";
        document.EnterpriseParcelFabricPublishTiming = EnterpriseParcelFabricReviewSettings.PublishTimingOnFinalReview;
        document.EnterpriseParcelFabricBuildBehavior = EnterpriseParcelFabricReviewSettings.BuildBehaviorCopyOnly;
        document.EnterpriseParcelFabricLoadOverlays = false;
        document.EnterpriseParcelFabricOverlaySource = EnterpriseParcelFabricReviewSettings.OverlaySourceNone;
        document.EnterpriseParcelFabricAllowReplaceTransactionScope = false;
        document.EnterpriseParcelFabricRequireActiveMap = true;
        document.GsiServerUrl = "https://gsi.local/";
        document.GsiUsername = "gsi-user";
        document.GsiPasswordMode = SettingsWorkspaceService.GsiPasswordModeDirect;
        document.GsiPassword = "masked-secret";
        document.PreflightRules.Single(rule => rule.RuleId == "dwg_readiness_probe").Enabled = false;
        document.PreflightRules.Single(rule => rule.RuleId == "dwg_readiness_probe").Severity = "warning";

        service.Save(document);

        var reloaded = service.Load(settingsPath);
        TestAssert.Equal("live", reloaded.InnolaTransactionMode, "Innola mode save mismatch.");
        TestAssert.Equal("https://saved.local/", reloaded.InnolaServerUrl, "Innola server save mismatch.");
        TestAssert.Equal(2, reloaded.SupportedTransactionTypes.Count, "Supported transaction types save mismatch.");
        TestAssert.Equal(2, reloaded.ComputeWorkflowStages.Count, "Compute workflow stages save mismatch.");
        TestAssert.Equal(600, reloaded.OutputAdapterTimeoutSeconds, "Output timeout save mismatch.");
        TestAssert.Equal(SettingsWorkspaceService.OpenAiExtractionProfileHighAccuracy, reloaded.OpenAiExtractionProfile, "OpenAI extraction profile save mismatch.");
        TestAssert.Equal("gpt-4.1", reloaded.OpenAiModel, "OpenAI model save mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.PdfViewerModeExternalOnly, reloaded.PdfViewerMode, "PDF viewer mode save mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, reloaded.ReviewWorkspaceMode, "Review workspace mode save mismatch.");
        TestAssert.True(reloaded.SpatialOutputAddCogoAttributes, "COGO attributes save mismatch.");
        TestAssert.True(reloaded.SpatialOutputAddCogoLabels, "COGO labels save mismatch.");
        TestAssert.Equal(SettingsWorkspaceService.SpatialOutputCogoSourceModePreferComputed, reloaded.SpatialOutputCogoSourceMode, "COGO source mode save mismatch.");
        TestAssert.True(reloaded.EnterpriseParcelFabricEnabled, "Enterprise Parcel Fabric enabled save mismatch.");
        TestAssert.Equal("https://fabric.local/server/rest/services/Fabric/FeatureServer/0", reloaded.EnterpriseParcelFabricFabricLayerUrl, "Enterprise Parcel Fabric fabric layer save mismatch.");
        TestAssert.Equal(EnterpriseParcelFabricReviewSettings.PublishTimingOnFinalReview, reloaded.EnterpriseParcelFabricPublishTiming, "Enterprise Parcel Fabric publish timing save mismatch.");
        TestAssert.Equal(EnterpriseParcelFabricReviewSettings.BuildBehaviorCopyOnly, reloaded.EnterpriseParcelFabricBuildBehavior, "Enterprise Parcel Fabric build behavior save mismatch.");
        TestAssert.True(!reloaded.EnterpriseParcelFabricLoadOverlays, "Enterprise Parcel Fabric overlay load save mismatch.");
        TestAssert.Equal(SettingsWorkspaceService.GsiPasswordModeDirect, reloaded.GsiPasswordMode, "GSI password mode save mismatch.");
        TestAssert.Equal("masked-secret", reloaded.GsiPassword, "GSI password save mismatch.");
        var savedRule = reloaded.PreflightRules.Single(rule => rule.RuleId == "dwg_readiness_probe");
        TestAssert.True(!savedRule.Enabled, "Preflight rule enabled state save mismatch.");
        TestAssert.Equal("warning", savedRule.Severity, "Preflight rule severity save mismatch.");
    }

    public static void SettingsWorkspaceValidationRejectsInvalidEnterpriseAndSecretConfiguration()
    {
        var service = new SettingsWorkspaceService(new PreflightRuleCatalogLoader(Path.Combine(Path.GetTempPath(), "missing-rules.json")));
        var document = new SettingsWorkspaceDocument
        {
            ReviewWorkspaceMode = InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers,
            EnterpriseWorkingEnabled = false,
            EnterpriseWorkingPointsLayer = "",
            EnterpriseWorkingLinesLayer = "",
            EnterpriseWorkingPolygonsLayer = "",
            EnterpriseWorkingTransactionScopeField = "",
            SupportedTransactionTypes = new List<string> { "Plan Examination" },
            ComputeWorkflowStages = new List<string> { "Compute Survey Plan" },
            OutputAdapterTimeoutSeconds = 120,
            OpenAiExtractionProfile = SettingsWorkspaceService.OpenAiExtractionProfileCustom,
            SpatialOutputCogoSourceMode = "not_supported",
            GsiPasswordMode = SettingsWorkspaceService.GsiPasswordModeDirect,
            GsiPassword = ""
        };

        var messages = service.Validate(document);

        TestAssert.True(messages.Any(message => message.TabName == "Spatial Workspace" && message.FieldName == "Enterprise Working Review"), "Expected enterprise enablement validation message.");
        TestAssert.True(messages.Any(message => message.TabName == "Spatial Workspace" && message.FieldName == "Enterprise Targets"), "Expected enterprise targets validation message.");
        TestAssert.True(messages.Any(message => message.TabName == "Spatial Workspace" && message.FieldName == "COGO Source Mode"), "Expected COGO source mode validation message.");
        TestAssert.True(messages.Any(message => message.TabName == "Spatial Workspace" && message.FieldName == "ArcGIS Enterprise Password"), "Expected direct GSI password validation message.");
    }

    public static void SettingsWorkspaceValidationRejectsInvalidEnterpriseParcelFabricConfiguration()
    {
        var service = new SettingsWorkspaceService(new PreflightRuleCatalogLoader(Path.Combine(Path.GetTempPath(), "missing-rules.json")));
        var document = new SettingsWorkspaceDocument
        {
            ReviewWorkspaceMode = InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric,
            EnterpriseParcelFabricEnabled = false,
            EnterpriseParcelFabricFabricLayerUrl = "",
            EnterpriseParcelFabricRecordsLayerUrl = "",
            EnterpriseParcelFabricParcelTypeName = "",
            EnterpriseParcelFabricRecordNamePattern = "",
            EnterpriseParcelFabricTransactionScopeField = "",
            SupportedTransactionTypes = new List<string> { "Plan Examination" },
            ComputeWorkflowStages = new List<string> { "Compute Survey Plan" },
            OutputAdapterTimeoutSeconds = 120,
            OpenAiExtractionProfile = SettingsWorkspaceService.OpenAiExtractionProfileCustom,
            GsiPasswordMode = SettingsWorkspaceService.GsiPasswordModeEnvironmentVariable,
            GsiPasswordEnvironmentVariable = "GSI_PASSWORD"
        };

        var messages = service.Validate(document);

        TestAssert.True(messages.Any(message => message.TabName == "Spatial Workspace" && message.FieldName == "Enterprise Parcel Fabric"), "Expected Enterprise Parcel Fabric enablement validation message.");
        TestAssert.True(messages.Any(message => message.TabName == "Spatial Workspace" && message.FieldName == "Enterprise Parcel Fabric Targets"), "Expected Enterprise Parcel Fabric target validation message.");
    }

    public static void SettingsWorkspaceLoadSurfacesEnterpriseParcelFabricWarningsInSummary()
    {
        using var tempDirectory = new TempDirectory();
        var settingsPath = Path.Combine(tempDirectory.Path, "WorkflowSettings.json");
        File.WriteAllText(settingsPath,
            """
            {
              "review_workspace_mode": "enterprise_parcel_fabric",
              "enterprise_parcel_fabric_review": {
                "enabled": true,
                "fabric_layer_url": "https://fabric.local/server/rest/services/Fabric/FeatureServer/0",
                "parcel_type_name": "compute_review",
                "record_name_pattern": "sidwell-record-{transaction_number}",
                "transaction_scope_field": "transaction_number"
              }
            }
            """);

        var service = new SettingsWorkspaceService(new PreflightRuleCatalogLoader(Path.Combine(tempDirectory.Path, "missing-rules.json")));
        var document = service.Load(settingsPath);

        TestAssert.True(document.SettingsWarning?.Contains("records_layer_url", StringComparison.OrdinalIgnoreCase) == true, "Enterprise Parcel Fabric warning should surface in settings summary.");
    }
}
