using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaAuthService : IInnolaAuthService
{
    private readonly HttpClient httpClient;

    public InnolaAuthService()
        : this(new HttpClient())
    {
    }

    public InnolaAuthService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
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
            using var response = await httpClient.PostAsJsonAsync(authUrl, new LoginPasswordRequest(
                true,
                true,
                username,
                InnolaSettings.DefaultAuthModule,
                password,
                InnolaSettings.DefaultAuthVersion), cancellationToken).ConfigureAwait(false);

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

            return InnolaLoginResult.Succeeded(CurrentSession);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
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
