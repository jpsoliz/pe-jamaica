using System.Net.Http;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Innola;

internal static class InnolaHttp
{
    public const string SessionCookieAccessToken = "__innola_session_cookie__";

    public static string NormalizeServerUrl(string serverUrl)
    {
        var trimmed = serverUrl.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }

    public static Uri BuildUri(string serverUrl, string relativePath)
    {
        return new Uri(new Uri(NormalizeServerUrl(serverUrl)), relativePath.TrimStart('/'));
    }

    public static void ApplyAuthHeaders(HttpRequestMessage request, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Equals(SessionCookieAccessToken, StringComparison.Ordinal))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation("Access-Token", accessToken);
    }

    public static string SafeRetryMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || message.Contains("{", StringComparison.Ordinal)
            || message.Contains("}", StringComparison.Ordinal))
        {
            return fallback;
        }

        return message;
    }

    public static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    public static string? ReadNestedString(JsonElement element, string objectName, params string[] names)
    {
        return element.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? ReadString(nested, names)
            : null;
    }

    public static DateTimeOffset? ReadDate(JsonElement element, params string[] names)
    {
        var raw = ReadString(element, names);
        return !string.IsNullOrWhiteSpace(raw) && DateTimeOffset.TryParse(raw, out var parsed)
            ? parsed
            : null;
    }
}
