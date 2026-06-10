using System.Text.Json;
using System.IO;

namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionSettings(
    string Mode,
    string ProcessStep,
    string CaseFolderOutputRoot)
{
    public static InnolaTransactionSettings Default { get; } = new("mock", "parcel_workflow", DefaultCaseFolderOutputRoot());

    public static InnolaTransactionSettings Load()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "Settings", "WorkflowSettings.json");
        if (!File.Exists(settingsPath))
        {
            settingsPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "Settings",
                "WorkflowSettings.json"));
        }

        if (!File.Exists(settingsPath))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            var mode = ReadString(root, "innola_transaction_mode") ?? Default.Mode;
            var processStep = ReadString(root, "innola_process_step") ?? Default.ProcessStep;
            var outputRoot = ReadString(root, "case_folder_output_root");
            return new InnolaTransactionSettings(
                mode,
                processStep,
                string.IsNullOrWhiteSpace(outputRoot) ? Default.CaseFolderOutputRoot : ExpandPath(outputRoot));
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string DefaultCaseFolderOutputRoot()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documents)
            ? Path.Combine(AppContext.BaseDirectory, "CaseFolders")
            : Path.Combine(documents, "SidwellCo", "ParcelWorkflowCases");
    }

    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}
