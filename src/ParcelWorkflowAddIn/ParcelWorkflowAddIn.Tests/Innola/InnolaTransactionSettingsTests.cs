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
              "review_workspace_mode": "parcel_fabric",
              "supported_transaction_types": [
                "Plan Examination",
                "Cadastral Plan Examination",
                "Plan Examination"
              ]
            }
            """);

        var settings = InnolaTransactionSettings.Load(settingsFile.Path);

        TestAssert.Equal(2, settings.SupportedTransactionTypes.Count, "Supported transaction type count mismatch.");
        TestAssert.Equal(InnolaTransactionSettings.ReviewWorkspaceModeParcelFabric, settings.ReviewWorkspaceMode, "Review workspace mode mismatch.");
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
        TestAssert.Equal("Parcel Fabric", InnolaTransactionSettings.FormatReviewWorkspaceMode(InnolaTransactionSettings.ReviewWorkspaceModeParcelFabric), "Parcel Fabric review workspace display mismatch.");
    }

    private static TempFile WriteSettingsFile(string json)
    {
        var tempFile = new TempFile();
        File.WriteAllText(tempFile.Path, json);
        return tempFile;
    }
}
