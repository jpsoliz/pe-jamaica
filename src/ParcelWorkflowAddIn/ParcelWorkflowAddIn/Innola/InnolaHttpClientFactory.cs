using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace ParcelWorkflowAddIn.Innola;

internal static class InnolaHttpClientFactory
{
    public static HttpClient Create(InnolaClientCertificateSettings certificateSettings)
    {
        var handler = new HttpClientHandler
        {
            CheckCertificateRevocationList = certificateSettings.CheckCertificateRevocationList
        };

        if (certificateSettings.AllowInvalidServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        if (TryFindCertificate(certificateSettings, out var certificate))
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(certificate);
        }

        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static bool TryFindCertificate(InnolaClientCertificateSettings settings, out X509Certificate2 certificate)
    {
        certificate = null!;
        if (!settings.Enabled)
        {
            return false;
        }

        if (!TryParseStoreLocation(settings.StoreLocation, out var location)
            || !TryParseStoreName(settings.StoreName, out var storeName))
        {
            return false;
        }

        using var store = new X509Store(storeName, location);
        store.Open(OpenFlags.ReadOnly);
        var candidates = store.Certificates
            .Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: true)
            .OfType<X509Certificate2>();

        if (!string.IsNullOrWhiteSpace(settings.Thumbprint))
        {
            var normalizedThumbprint = NormalizeThumbprint(settings.Thumbprint);
            candidates = candidates.Where(item => NormalizeThumbprint(item.Thumbprint).Equals(normalizedThumbprint, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(settings.SubjectName))
        {
            candidates = candidates.Where(item =>
                item.Subject.Contains(settings.SubjectName, StringComparison.OrdinalIgnoreCase)
                || item.GetNameInfo(X509NameType.SimpleName, forIssuer: false).Contains(settings.SubjectName, StringComparison.OrdinalIgnoreCase));
        }

        certificate = candidates
            .Where(item => item.HasPrivateKey)
            .OrderByDescending(item => item.NotAfter)
            .FirstOrDefault()!;

        return certificate is not null;
    }

    private static bool TryParseStoreLocation(string value, out StoreLocation location)
    {
        return Enum.TryParse(value, ignoreCase: true, out location);
    }

    private static bool TryParseStoreName(string value, out StoreName storeName)
    {
        return Enum.TryParse(value, ignoreCase: true, out storeName);
    }

    private static string NormalizeThumbprint(string value)
    {
        return value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal);
    }
}
