using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed record WorkflowExecutionSettings(
    string PythonExecutable,
    string CreateParcelScriptPath)
{
    private static readonly string DefaultScriptsRoot = @"C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts";

    public static WorkflowExecutionSettings Default { get; } = new(
        ProcessingEnvironmentSettings.Default.PythonExecutable,
        Path.Combine(DefaultScriptsRoot, "CreateParcelFromFile.py"));

    public static WorkflowExecutionSettings Load()
    {
        var settingsPath = InnolaTransactionSettings.ResolveActiveSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            var pythonExecutable = ExpandPath(ReadString(root, "arcgis_python_executable") ?? Default.PythonExecutable);
            var scriptsRoot = ExpandPath(ReadString(root, "processing_scripts_root") ?? DefaultScriptsRoot);
            var configuredScriptPath = ReadString(root, "create_parcel_script_path");
            var scriptPath = ExpandPath(string.IsNullOrWhiteSpace(configuredScriptPath)
                ? Path.Combine(scriptsRoot, "CreateParcelFromFile.py")
                : configuredScriptPath);

            return new WorkflowExecutionSettings(pythonExecutable, scriptPath);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
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

    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}
