using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaAuthService : IInnolaAuthService
{
    private readonly HttpClient httpClient;
    private readonly InnolaLoginTraceService? loginTrace;
    private readonly InnolaClientCertificateSettings? certificateSettings;

    public InnolaAuthService()
        : this(new HttpClient())
    {
    }

    public InnolaAuthService(HttpClient httpClient, InnolaLoginTraceService? loginTrace = null, InnolaClientCertificateSettings? certificateSettings = null)
    {
        this.httpClient = httpClient;
        this.loginTrace = loginTrace;
        this.certificateSettings = certificateSettings;
    }

    public InnolaSession? CurrentSession { get; private set; }

    public async Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return InnolaLoginResult.Failure("Login failed. Check user name, password, and server.");
        }

        try
        {
            var normalizedServer = InnolaHttp.NormalizeServerUrl(serverUrl);
            var authUrl = InnolaHttp.BuildUri(normalizedServer, $"{InnolaSettings.RestPath}{InnolaSettings.AuthenticationPath}");
            TraceCertificate(normalizedServer);
            Trace("login_started", true, "Innola login request started.", new Dictionary<string, string?>
            {
                ["server_url"] = normalizedServer,
                ["auth_path"] = $"{InnolaSettings.RestPath}{InnolaSettings.AuthenticationPath}",
                ["username_present"] = (!string.IsNullOrWhiteSpace(username)).ToString()
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = CreateLoginContent(username, password)
            };
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Trace("login_http_response", response.IsSuccessStatusCode, GetLoginFailureMessage(response.StatusCode), new Dictionary<string, string?>
            {
                ["server_url"] = normalizedServer,
                ["status_code"] = ((int)response.StatusCode).ToString(),
                ["reason_phrase"] = response.ReasonPhrase
            });

            if (!response.IsSuccessStatusCode)
            {
                return InnolaLoginResult.Failure(GetLoginFailureMessage(response.StatusCode));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var loginPayload = ExtractLoginPayload(responseBody);
            var accessToken = string.IsNullOrWhiteSpace(loginPayload.AccessToken)
                ? InnolaHttp.SessionCookieAccessToken
                : loginPayload.AccessToken;
            var currentUser = await GetCurrentUserAsync(normalizedServer, accessToken, cancellationToken).ConfigureAwait(false);
            if (currentUser is null && string.IsNullOrWhiteSpace(loginPayload.AccessToken))
            {
                Trace("login_missing_token", false, "Login succeeded but no API token was returned.", new Dictionary<string, string?>
                {
                    ["server_url"] = normalizedServer
                });
                return InnolaLoginResult.Failure("Login succeeded but no API token was returned.");
            }

            var user = new InnolaUserContext(
                currentUser?.Username ?? loginPayload.Username ?? username,
                currentUser?.DisplayName ?? loginPayload.DisplayName ?? loginPayload.Username ?? username,
                currentUser?.Groups ?? loginPayload.Groups,
                currentUser?.Roles ?? loginPayload.Roles);
            CurrentSession = new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                normalizedServer,
                username,
                password,
                accessToken,
                user,
                null);

            Trace("login_succeeded", true, "Innola login succeeded.", new Dictionary<string, string?>
            {
                ["server_url"] = normalizedServer,
                ["current_user_resolved"] = (currentUser is not null).ToString(),
                ["group_count"] = user.Groups.Count.ToString(),
                ["role_count"] = user.Roles.Count.ToString()
            });
            return InnolaLoginResult.Succeeded(CurrentSession);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Trace("login_timeout", false, "Login timed out. Check server, certificate, and network.", null);
            return InnolaLoginResult.Failure("Login timed out. Check server, certificate, and network.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            Trace("login_failed_exception", false, "Login failed. Check user name, password, and server.", new Dictionary<string, string?>
            {
                ["exception_type"] = exception.GetType().Name,
                ["exception_message"] = exception.Message
            });
            return InnolaLoginResult.Failure("Login failed. Check user name, password, and server.");
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        CurrentSession = null;
        return Task.CompletedTask;
    }

    private static LoginPayload ExtractLoginPayload(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var payloadRoot = root.TryGetProperty("value", out var valueObject) && valueObject.ValueKind == JsonValueKind.Object
            ? valueObject
            : root;

        return new LoginPayload(
            ExtractString(payloadRoot, "access-token", "auth-token", "accessToken", "access_token", "token", "AccessToken") ?? string.Empty,
            ExtractString(payloadRoot, "username", "userName", "login", "Login"),
            ExtractString(payloadRoot, "displayName", "fullName", "FullName", "name"),
            ExtractStringArray(payloadRoot, "groups", "Groups", "groupNames"),
            ExtractStringArray(payloadRoot, "roles", "Roles", "roleNames"));
    }

    private async Task<LoginUserPayload?> GetCurrentUserAsync(string normalizedServer, string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, InnolaHttp.BuildUri(normalizedServer, $"{InnolaSettings.RestPath}{InnolaSettings.CurrentUserDetailsPath}"));
            InnolaHttp.ApplyAuthHeaders(request, accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            var payloadRoot = root.TryGetProperty("value", out var valueObject) && valueObject.ValueKind == JsonValueKind.Object
                ? valueObject
                : root;

            if (payloadRoot.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var username = ExtractString(payloadRoot, "userName", "username", "login", "Login");
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return new LoginUserPayload(
                username,
                ExtractString(payloadRoot, "fullName", "displayName", "name") ?? username,
                ExtractStringArray(payloadRoot, "groups", "Groups", "groupNames"),
                ExtractStringArray(payloadRoot, "roles", "Roles", "roleNames"));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            return null;
        }
    }

    private static string? ExtractString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractStringArray(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    .Select(item => item.GetString()!)
                    .ToArray();
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }
        }

        return Array.Empty<string>();
    }

    private sealed record LoginPasswordRequest(
        [property: JsonPropertyName("createSession")] bool CreateSession,
        [property: JsonPropertyName("generateAccessToken")] bool GenerateAccessToken,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("module")] string Module,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("version")] string Version);

    private static StringContent CreateLoginContent(string username, string password)
    {
        var request = new LoginPasswordRequest(
            true,
            true,
            username,
            InnolaSettings.DefaultAuthModule,
            password,
            InnolaSettings.DefaultAuthVersion);
        return new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
    }

    private void TraceCertificate(string normalizedServer)
    {
        if (certificateSettings is null)
        {
            return;
        }

        var diagnostic = InnolaHttpClientFactory.DiagnoseCertificate(certificateSettings);
        Trace("client_certificate", diagnostic.Selected, diagnostic.Message, new Dictionary<string, string?>
        {
            ["server_url"] = normalizedServer,
            ["status"] = diagnostic.Status,
            ["store_location"] = diagnostic.StoreLocation,
            ["store_name"] = diagnostic.StoreName,
            ["matching_certificate_count"] = diagnostic.MatchingCertificateCount.ToString(),
            ["subject_configured"] = (!string.IsNullOrWhiteSpace(certificateSettings.SubjectName)).ToString(),
            ["thumbprint_configured"] = (!string.IsNullOrWhiteSpace(certificateSettings.Thumbprint)).ToString()
        });
    }

    private void Trace(string step, bool success, string message, IReadOnlyDictionary<string, string?>? details)
    {
        loginTrace?.Append(step, success, message, details);
    }

    private sealed record LoginPayload(
        string AccessToken,
        string? Username,
        string? DisplayName,
        IReadOnlyList<string> Groups,
        IReadOnlyList<string> Roles);

    private sealed record LoginUserPayload(
        string Username,
        string DisplayName,
        IReadOnlyList<string> Groups,
        IReadOnlyList<string> Roles);

    private static string GetLoginFailureMessage(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Login failed. User is not permitted for this Innola module.",
            System.Net.HttpStatusCode.Forbidden => "Login failed. Check user name and password.",
            System.Net.HttpStatusCode.BadRequest => "Login failed. The server rejected the login request.",
            _ => $"Login failed. Server returned {(int)statusCode}."
        };
    }
}
