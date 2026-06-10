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
            var normalizedServer = NormalizeServerUrl(serverUrl);
            var authUrl = new Uri(new Uri(normalizedServer), $"{InnolaSettings.RestPath.TrimStart('/')}{InnolaSettings.AuthenticationPath}");
            using var response = await httpClient.PostAsJsonAsync(authUrl, new LoginPasswordRequest(username, password), cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return InnolaLoginResult.Failure("Login failed. Check user name, password, and server.");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var loginPayload = ExtractLoginPayload(responseBody);
            if (string.IsNullOrWhiteSpace(loginPayload.AccessToken))
            {
                return InnolaLoginResult.Failure("Login failed. Check user name, password, and server.");
            }

            var user = new InnolaUserContext(
                loginPayload.Username ?? username,
                loginPayload.DisplayName ?? loginPayload.Username ?? username,
                loginPayload.Groups,
                loginPayload.Roles);
            CurrentSession = new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                normalizedServer,
                username,
                password,
                loginPayload.AccessToken,
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

    private static string NormalizeServerUrl(string serverUrl)
    {
        var trimmed = serverUrl.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    private static LoginPayload ExtractLoginPayload(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var payloadRoot = root.TryGetProperty("value", out var valueObject) && valueObject.ValueKind == JsonValueKind.Object
            ? valueObject
            : root;

        return new LoginPayload(
            ExtractString(payloadRoot, "accessToken", "access_token", "token", "AccessToken") ?? string.Empty,
            ExtractString(payloadRoot, "username", "userName", "login", "Login"),
            ExtractString(payloadRoot, "displayName", "fullName", "FullName", "name"),
            ExtractStringArray(payloadRoot, "groups", "Groups", "groupNames"),
            ExtractStringArray(payloadRoot, "roles", "Roles", "roleNames"));
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
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("password")] string Password);

    private sealed record LoginPayload(
        string AccessToken,
        string? Username,
        string? DisplayName,
        IReadOnlyList<string> Groups,
        IReadOnlyList<string> Roles);
}
