using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.WorkflowRules;

public static class WorkflowRuleSettingsLoader
{
    public static WorkflowRuleSettings Load()
    {
        var settingsPath = InnolaTransactionSettings.ResolveActiveSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return WorkflowRuleSettings.Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            return new WorkflowRuleSettings(
                ReadString(root, "ocr_engine") ?? WorkflowRuleSettings.Default.OcrEngine,
                ReadBool(root, "openai_enabled") ?? WorkflowRuleSettings.Default.OpenAiEnabled,
                ReadString(root, "openai_extraction_profile") ?? WorkflowRuleSettings.Default.OpenAiExtractionProfile,
                ReadString(root, "openai_model") ?? WorkflowRuleSettings.Default.OpenAiModel,
                ReadString(root, "openai_api_key_environment_variable") ?? WorkflowRuleSettings.Default.OpenAiApiKeyEnvironmentVariable,
                ReadString(root, "credential_profile") ?? WorkflowRuleSettings.Default.CredentialProfile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
        {
            return WorkflowRuleSettings.Default;
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
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }
}
