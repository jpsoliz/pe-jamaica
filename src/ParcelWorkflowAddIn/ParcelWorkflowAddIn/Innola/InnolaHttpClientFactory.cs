using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace ParcelWorkflowAddIn.Innola;

internal static class InnolaHttpClientFactory
{
    private static readonly CookieContainer SharedCookieContainer = new();

    public static HttpClient Create(InnolaClientCertificateSettings certificateSettings)
    {
        var handler = new HttpClientHandler
        {
            CheckCertificateRevocationList = certificateSettings.CheckCertificateRevocationList,
            CookieContainer = SharedCookieContainer,
            UseCookies = true
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

    public static bool HasCookie(string serverUrl, string cookieName)
    {
        try
        {
            var uri = new Uri(InnolaHttp.NormalizeServerUrl(serverUrl));
            return SharedCookieContainer
                .GetCookies(uri)
                .Cast<Cookie>()
                .Any(cookie => cookie.Name.Equals(cookieName, StringComparison.OrdinalIgnoreCase));
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    public static void EnsureCookie(string serverUrl, string cookieName, string value)
    {
        try
        {
            var uri = new Uri(InnolaHttp.NormalizeServerUrl(serverUrl));
            var hasCookie = SharedCookieContainer
                .GetCookies(uri)
                .Cast<Cookie>()
                .Any(cookie => cookie.Name.Equals(cookieName, StringComparison.OrdinalIgnoreCase));
            if (hasCookie)
            {
                return;
            }

            SharedCookieContainer.Add(uri, new Cookie(cookieName, value, "/", uri.Host)
            {
                Secure = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            });
        }
        catch (CookieException)
        {
        }
        catch (UriFormatException)
        {
        }
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

    public static InnolaClientCertificateDiagnostic DiagnoseCertificate(InnolaClientCertificateSettings settings)
    {
        if (!settings.Enabled)
        {
            return new InnolaClientCertificateDiagnostic(false, "disabled", null, null, 0, "Client certificate is disabled in settings.");
        }

        if (!TryParseStoreLocation(settings.StoreLocation, out var location))
        {
            return new InnolaClientCertificateDiagnostic(false, "invalid_store_location", null, null, 0, $"Invalid certificate store location: {settings.StoreLocation}");
        }

        if (!TryParseStoreName(settings.StoreName, out var storeName))
        {
            return new InnolaClientCertificateDiagnostic(false, "invalid_store_name", null, null, 0, $"Invalid certificate store name: {settings.StoreName}");
        }

        try
        {
            using var store = new X509Store(storeName, location);
            store.Open(OpenFlags.ReadOnly);
            var validCertificates = store.Certificates
                .Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: true)
                .OfType<X509Certificate2>()
                .ToArray();
            var candidates = validCertificates.AsEnumerable();

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

            var matches = candidates.ToArray();
            var selected = matches
                .Where(item => item.HasPrivateKey)
                .OrderByDescending(item => item.NotAfter)
                .FirstOrDefault();

            if (selected is null)
            {
                var reason = matches.Length == 0
                    ? "No valid certificate matched the configured subject/thumbprint."
                    : "Matching certificate found, but it does not have an accessible private key.";
                return new InnolaClientCertificateDiagnostic(
                    false,
                    matches.Length == 0 ? "not_found" : "missing_private_key",
                    settings.StoreLocation,
                    settings.StoreName,
                    matches.Length,
                    reason);
            }

            return new InnolaClientCertificateDiagnostic(
                true,
                "selected",
                settings.StoreLocation,
                settings.StoreName,
                matches.Length,
                $"Selected certificate subject '{selected.GetNameInfo(X509NameType.SimpleName, forIssuer: false)}' valid until {selected.NotAfter:O}.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            return new InnolaClientCertificateDiagnostic(false, "store_error", settings.StoreLocation, settings.StoreName, 0, exception.Message);
        }
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

public sealed record InnolaClientCertificateDiagnostic(
    bool Selected,
    string Status,
    string? StoreLocation,
    string? StoreName,
    int MatchingCertificateCount,
    string Message);
