using ParcelWorkflowAddIn.Innola;
using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionSettingsTests
{
    public static void CertificateSettingsMapFromWorkflowSettings()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "innola_client_certificate_enabled": true,
              "innola_client_certificate_store_location": "CurrentUser",
              "innola_client_certificate_store_name": "My",
              "innola_client_certificate_subject": "Jamaica eTitles Project Team",
              "innola_client_certificate_thumbprint": "45AF05AA01",
              "innola_allow_invalid_server_certificate": true,
              "innola_check_certificate_revocation_list": false
            }
            """);

        var settings = InnolaClientCertificateSettings.FromJson(document.RootElement);

        TestAssert.True(settings.Enabled, "Client certificate should be enabled.");
        TestAssert.Equal("CurrentUser", settings.StoreLocation, "Store location mismatch.");
        TestAssert.Equal("My", settings.StoreName, "Store name mismatch.");
        TestAssert.Equal("Jamaica eTitles Project Team", settings.SubjectName, "Subject mismatch.");
        TestAssert.Equal("45AF05AA01", settings.Thumbprint, "Thumbprint mismatch.");
        TestAssert.True(settings.AllowInvalidServerCertificate, "Dev server certificate bypass mismatch.");
        TestAssert.True(!settings.CheckCertificateRevocationList, "Revocation check mismatch.");
    }

    public static void SupportedTransactionTypesLoadFromConfiguration()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "parcel_fabric_local",
              "supported_transaction_types": [
                "Plan Examination",
                "Cadastral Plan Examination",
                "Plan Examination"
              ]
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(2, settings.SupportedTransactionTypes.Count, "Supported transaction type count mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLocal, settings.ReviewWorkspaceMode, "Review workspace mode mismatch.");
        TestAssert.Equal(null, settings.ReviewWorkspaceModeWarning, "Valid review workspace mode should not produce a warning.");
        TestAssert.Equal("Plan Examination", settings.SupportedTransactionTypes[0], "First supported transaction type mismatch.");
        TestAssert.Equal("Cadastral Plan Examination", settings.SupportedTransactionTypes[1], "Second supported transaction type mismatch.");
        TestAssert.Equal(null, settings.SupportedTransactionTypesWarning, "Valid list should not produce a warning.");
        TestAssert.Equal(3, settings.ComputeWorkflowStages.Count, "Default compute workflow stage count mismatch.");
    }

    public static void MissingSupportedTransactionTypesFallBackToSafeDefaults()
    {
        using var settingsFile = WriteSettingsFile("{}");

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(2, settings.SupportedTransactionTypes.Count, "Fallback supported transaction type count mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeNormal, settings.ReviewWorkspaceMode, "Fallback review workspace mode mismatch.");
        TestAssert.Equal("Plan Examination", settings.SupportedTransactionTypes[0], "Fallback first supported transaction type mismatch.");
        TestAssert.True(settings.ReviewWorkspaceModeWarning?.Contains("safe default", StringComparison.OrdinalIgnoreCase) == true, "Fallback review workspace mode warning mismatch.");
        TestAssert.True(settings.SupportedTransactionTypesWarning?.Contains("safe defaults", StringComparison.OrdinalIgnoreCase) == true, "Fallback warning mismatch.");
        TestAssert.True(settings.ComputeWorkflowStagesWarning?.Contains("safe defaults", StringComparison.OrdinalIgnoreCase) == true, "Fallback compute workflow stage warning mismatch.");
    }

    public static void InvalidSupportedTransactionTypesFallBackToSafeDefaults()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "supported_transaction_types": [null, "", "   "]
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(2, settings.SupportedTransactionTypes.Count, "Invalid list should fall back to safe defaults.");
        TestAssert.True(settings.SupportedTransactionTypesWarning?.Contains("empty or invalid", StringComparison.OrdinalIgnoreCase) == true, "Invalid list warning mismatch.");
    }

    public static void ComputeWorkflowStagesLoadFromConfiguration()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "compute_workflow_stages": [
                "Compute Survey Plan",
                "Compare Survey Plan",
                "Compute Survey Plan"
              ]
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(2, settings.ComputeWorkflowStages.Count, "Compute workflow stage count mismatch.");
        TestAssert.Equal("Compute Survey Plan", settings.ComputeWorkflowStages[0], "First compute workflow stage mismatch.");
        TestAssert.Equal("Compare Survey Plan", settings.ComputeWorkflowStages[1], "Second compute workflow stage mismatch.");
        TestAssert.Equal(null, settings.ComputeWorkflowStagesWarning, "Valid compute stage list should not produce a warning.");
    }

    public static void MissingTransactionTypeProfilesFallBackToSafeDefaults()
    {
        using var settingsFile = WriteSettingsFile("{}");

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);
        var pe = settings.ResolveComputeTransactionTypeProfile("Compute Survey Plan", "Assign Computation Task");
        var pxa = settings.ResolveComputeTransactionTypeProfile("PXA");

        TestAssert.True(settings.ComputeTransactionTypeProfilesWarning?.Contains("safe defaults", StringComparison.OrdinalIgnoreCase) == true, "Missing profiles should warn about safe defaults.");
        TestAssert.Equal("pe_computation_review", pe?.ProfileId, "PE safe default profile mismatch.");
        TestAssert.True(pe!.RequiredSourceRoles.Contains("computation_sheet"), "PE should require computation sheet.");
        TestAssert.True(pe.RequiredSourceRoles.Contains("plan_map_reference"), "PE should require plan/map reference.");
        TestAssert.Equal("pxa_single_parcel_survey_plan", pxa?.ProfileId, "PXA safe default profile mismatch.");
        TestAssert.True(pxa!.RequiredSourceRoles.Contains("survey_plan_pdf"), "PXA should require survey plan PDF.");
        TestAssert.True(!pxa.RequiredSourceRoles.Contains("computation_sheet"), "PXA should not require computation sheet.");
    }

    public static void ProfileHintBeatsBroadTransactionTypeName()
    {
        using var settingsFile = WriteSettingsFile("{}");

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);
        var profile = settings.ResolveComputeTransactionTypeProfile(
            "Plan Examination",
            "Assign Computation Task",
            "PXA");

        TestAssert.Equal("pxa_single_parcel_survey_plan", profile?.ProfileId, "Exact PXA profile hint should beat broad PE transaction/task labels.");
        TestAssert.True(profile!.RequiredSourceRoles.Contains("survey_plan_pdf"), "PXA hint should select survey-plan source requirements.");
    }

    public static void TransactionTypeProfilesLoadFromConfiguration()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "compute_transaction_type_profiles": [
                {
                  "profile_id": "custom_pxa",
                  "enabled": true,
                  "transaction_type_codes": ["PXA"],
                  "transaction_type_names": ["Survey Plan Approval"],
                  "workflow_profile": "pxa_single_parcel_survey_plan",
                  "required_source_roles": ["survey_plan_pdf"],
                  "optional_source_roles": ["coordinate_text_source", "dwg_source"],
                  "primary_extraction_role": "survey_plan_pdf",
                  "document_profile": "scanned_single_parcel_survey_plan_pdf"
                }
              ]
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);
        var profile = settings.ResolveComputeTransactionTypeProfile("Survey Plan Approval");

        TestAssert.Equal(null, settings.ComputeTransactionTypeProfilesWarning, "Valid profile configuration should not warn.");
        TestAssert.Equal("custom_pxa", profile?.ProfileId, "Configured profile id mismatch.");
        TestAssert.Equal("survey_plan_pdf", profile?.PrimaryExtractionRole, "Primary extraction role mismatch.");
        TestAssert.Equal("scanned_single_parcel_survey_plan_pdf", profile?.DocumentProfile, "Document profile mismatch.");
    }

    public static void ManualReviewRetryThresholdLoadsFromConfiguration()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "manual_review_retry_threshold": 5
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(5, settings.ManualReviewRetryThreshold, "Manual review retry threshold mismatch.");
    }

    public static void InvalidReviewWorkspaceModeFallsBackToNormal()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "mystery_mode"
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeNormal, settings.ReviewWorkspaceMode, "Invalid review workspace mode should fall back to normal.");
        TestAssert.True(settings.ReviewWorkspaceModeWarning?.Contains("safe default", StringComparison.OrdinalIgnoreCase) == true, "Invalid review workspace mode warning mismatch.");
    }

    public static void LegacyParcelFabricReviewWorkspaceModeNormalizesToLocalParcelFabric()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "parcel_fabric"
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLocal, settings.ReviewWorkspaceMode, "Legacy parcel fabric value should normalize to local parcel fabric.");
    }

    public static void EnterpriseWorkingLayersModeLoadsEnterpriseConfiguration()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "enterprise_working_layers",
              "enterprise_working_review": {
                "enabled": true,
                "workspace_name": "jamaica_review",
                "publish_behavior": "replace_transaction_scope",
                "publish_timing": "on_complete",
                "restore_behavior": "prefer_enterprise_then_local",
                "allow_cross_machine_restore": true,
                "transaction_scope_field": "transaction_number",
                "layers": {
                  "points": "https://example/points",
                  "lines": "https://example/lines",
                  "polygons": "https://example/polygons",
                  "case_index": "https://example/case_index"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, settings.ReviewWorkspaceMode, "Enterprise review workspace mode mismatch.");
        TestAssert.True(settings.EnterpriseWorkingReview.Enabled, "Enterprise working review should be enabled.");
        TestAssert.Equal("jamaica_review", settings.EnterpriseWorkingReview.WorkspaceName, "Workspace name mismatch.");
        TestAssert.Equal(EnterpriseWorkingReviewSettings.PublishTimingOnComplete, settings.EnterpriseWorkingReview.PublishTiming, "Publish timing mismatch.");
        TestAssert.Equal("https://example/points", settings.EnterpriseWorkingReview.Layers.Points, "Points layer mismatch.");
        TestAssert.True(settings.EnterpriseWorkingReview.Warning?.Contains("issues layer", StringComparison.OrdinalIgnoreCase) == true, "Missing optional issues layer warning mismatch.");
    }

    public static void EnterpriseWorkingPublishTimingDefaultsToOnComplete()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "enterprise_working_layers",
              "enterprise_working_review": {
                "enabled": true,
                "layers": {
                  "points": "https://example/points",
                  "lines": "https://example/lines",
                  "polygons": "https://example/polygons"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);
        TestAssert.Equal(EnterpriseWorkingReviewSettings.PublishTimingOnComplete, settings.EnterpriseWorkingReview.PublishTiming, "Enterprise publish timing should default to on_complete.");
    }

    public static void EnterpriseParcelFabricModeLoadsEnterpriseParcelFabricConfiguration()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "enterprise_parcel_fabric",
              "enterprise_parcel_fabric_review": {
                "enabled": true,
                "service_root": "https://example.local/server/rest/services",
                "fabric_layer_url": "https://example.local/server/rest/services/Parcels/FeatureServer/0",
                "parcel_layer_url": "https://example.local/server/rest/services/Parcels/FeatureServer/3",
                "records_layer_url": "https://example.local/server/rest/services/Parcels/FeatureServer/5",
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
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, settings.ReviewWorkspaceMode, "Enterprise Parcel Fabric review workspace mode mismatch.");
        TestAssert.True(settings.EnterpriseParcelFabricReview.Enabled, "Enterprise Parcel Fabric review should be enabled.");
        TestAssert.Equal("https://example.local/server/rest/services/Parcels/FeatureServer/0", settings.EnterpriseParcelFabricReview.FabricLayerUrl, "Fabric layer URL mismatch.");
        TestAssert.Equal("compute_review", settings.EnterpriseParcelFabricReview.ParcelTypeName, "Parcel type name mismatch.");
        TestAssert.Equal(EnterpriseParcelFabricReviewSettings.PublishTimingOnOutputs, settings.EnterpriseParcelFabricReview.PublishTiming, "Publish timing mismatch.");
        TestAssert.Equal(EnterpriseParcelFabricReviewSettings.BuildBehaviorBuildAfterCopy, settings.EnterpriseParcelFabricReview.BuildBehavior, "Build behavior mismatch.");
        TestAssert.Equal(null, settings.EnterpriseParcelFabricReview.Warning, "Valid Enterprise Parcel Fabric configuration should not warn.");
    }

    public static void NormalModeDoesNotWarnAboutMissingEnterpriseTargets()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "normal",
              "enterprise_working_review": {
                "enabled": false,
                "layers": {
                  "points": "",
                  "lines": "",
                  "polygons": ""
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeNormal, settings.ReviewWorkspaceMode, "Normal mode mismatch.");
        TestAssert.Equal(null, settings.EnterpriseWorkingReview.Warning, "Normal mode should not warn about enterprise targets that are not in use.");
    }

    public static void EnterpriseModeWarnsWhenRequiredTargetsAreMissing()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "enterprise_working_layers",
              "enterprise_working_review": {
                "enabled": true,
                "transaction_scope_field": "transaction_number",
                "layers": {
                  "points": "https://example/points"
                }
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, settings.ReviewWorkspaceMode, "Enterprise mode mismatch.");
        TestAssert.True(settings.EnterpriseWorkingReview.Warning?.Contains("geometry layer targets", StringComparison.OrdinalIgnoreCase) == true, "Enterprise mode should warn when required targets are missing.");
    }

    public static void EnterpriseParcelFabricModeWarnsWhenRequiredTargetsAreMissing()
    {
        using var settingsFile = WriteSettingsFile(
            """
            {
              "review_workspace_mode": "enterprise_parcel_fabric",
              "enterprise_parcel_fabric_review": {
                "enabled": true,
                "parcel_type_name": "",
                "record_name_pattern": "sidwell-record-{transaction_number}",
                "transaction_scope_field": ""
              }
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, settings.ReviewWorkspaceMode, "Enterprise Parcel Fabric mode mismatch.");
        TestAssert.True(settings.EnterpriseParcelFabricReview.Warning?.Contains("fabric_layer_url", StringComparison.OrdinalIgnoreCase) == true, "Enterprise Parcel Fabric mode should warn when required targets are missing.");
        TestAssert.True(settings.EnterpriseParcelFabricReview.Warning?.Contains("records_layer_url", StringComparison.OrdinalIgnoreCase) == true, "Enterprise Parcel Fabric mode should warn when records layer target is missing.");
    }

    public static void ConfigurationSummaryFormatsSupportedTransactionTypes()
    {
        var summary = InnolaTransactionSettings.FormatSupportedTransactionTypesDisplay(new[]
        {
            "Plan Examination",
            "Cadastral Plan Examination"
        });

        TestAssert.True(summary.Contains("Plan Examination", StringComparison.Ordinal), "Configuration summary should include first supported type.");
        TestAssert.True(summary.Contains("Cadastral Plan Examination", StringComparison.Ordinal), "Configuration summary should include second supported type.");
        TestAssert.True(summary.Contains("•", StringComparison.Ordinal), "Configuration summary should render bullet formatting.");
    }

    public static void ConfigurationSummaryFormatsReviewWorkspaceMode()
    {
        TestAssert.Equal("Normal", InnolaTransactionSettings.FormatReviewWorkspaceMode(InnolaTransactionSettings.ReviewWorkspaceModeNormal), "Normal review workspace display mismatch.");
        TestAssert.Equal("Local Parcel Fabric", InnolaTransactionSettings.FormatReviewWorkspaceMode(InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLocal), "Local Parcel Fabric review workspace display mismatch.");
        TestAssert.Equal("Enterprise Working Layers", InnolaTransactionSettings.FormatReviewWorkspaceMode(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers), "Enterprise working layers display mismatch.");
        TestAssert.Equal("Enterprise Parcel Fabric", InnolaTransactionSettings.FormatReviewWorkspaceMode(InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric), "Enterprise Parcel Fabric display mismatch.");
    }

    private static TempFile WriteSettingsFile(string json)
    {
        var tempFile = new TempFile();
        File.WriteAllText(tempFile.Path, json);
        return tempFile;
    }
}
