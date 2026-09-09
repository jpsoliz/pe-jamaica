using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaLoginPreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string preferencePath;

    public InnolaLoginPreferenceStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sidwell",
            "ParcelWorkflowAddIn",
            "innola_login_preferences.json"))
    {
    }

    public InnolaLoginPreferenceStore(string preferencePath)
    {
        this.preferencePath = preferencePath;
    }

    public InnolaLoginPreferences Load()
    {
        try
        {
            if (!File.Exists(preferencePath))
            {
                return InnolaLoginPreferences.Empty;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(preferencePath));
            var root = document.RootElement;
            return new InnolaLoginPreferences(
                ReadString(root, "remembered_username"),
                ReadBool(root, "remember_me") ?? false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException
            or InvalidOperationException)
        {
            return InnolaLoginPreferences.Empty;
        }
    }

    public void SaveRememberedUser(string username)
    {
        var trimmed = username.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            Clear();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(preferencePath)!);
        File.WriteAllText(preferencePath, JsonSerializer.Serialize(new
        {
            schema_version = "innola_login_preferences_v1",
            remember_me = true,
            remembered_username = trimmed
        }, JsonOptions));
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(preferencePath))
            {
                File.Delete(preferencePath);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            // Login should not fail because a non-secret preference could not be cleared.
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }
}

public sealed record InnolaLoginPreferences(string? RememberedUsername, bool RememberMe)
{
    public static InnolaLoginPreferences Empty { get; } = new(null, false);
}
