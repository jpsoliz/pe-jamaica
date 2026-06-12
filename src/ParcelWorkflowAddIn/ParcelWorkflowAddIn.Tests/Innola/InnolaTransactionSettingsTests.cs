using ParcelWorkflowAddIn.Innola;
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
}
