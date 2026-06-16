using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed record WorkflowExecutionSettings(
    string PythonExecutable,
    string CreateParcelScriptPath,
    string OutputAdapterScriptPath,
    string ReviewWorkspaceMode,
    int OutputAdapterTimeoutSeconds,
    string? OutputTemplateProjectPath,
    string? OutputTemplateGdbPath,
    string ValidationAdapterScriptPath,
    string? ValidationRulesPath)
{
    private static readonly string DefaultScriptsRoot = @"C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts";
    private static readonly string? DefaultOutputAdapterPath = ResolveProjectFile(@"src\ProcessingTools\adapters\output_adapter.py");
    private static readonly string? DefaultValidationAdapterPath = ResolveProjectFile(@"src\ProcessingTools\adapters\validation_adapter.py");
    private static readonly string? DefaultValidationRulesPath = ResolveProjectFile(@"src\ProcessingTools\rules\rules.yaml");
    public const int DefaultOutputAdapterTimeoutSecondsNormal = 120;
    public const int DefaultOutputAdapterTimeoutSecondsParcelFabric = 600;

    public static WorkflowExecutionSettings Default { get; } = new(
        ProcessingEnvironmentSettings.Default.PythonExecutable,
        Path.Combine(DefaultScriptsRoot, "CreateParcelFromFile.py"),
        DefaultOutputAdapterPath ?? @"src\ProcessingTools\adapters\output_adapter.py",
        InnolaTransactionSettings.ReviewWorkspaceModeNormal,
        DefaultOutputAdapterTimeoutSecondsNormal,
        null,
        null,
        DefaultValidationAdapterPath ?? @"src\ProcessingTools\adapters\validation_adapter.py",
        DefaultValidationRulesPath);

    public static WorkflowExecutionSettings Load(string? settingsPath = null)
    {
        settingsPath ??= InnolaTransactionSettings.ResolveActiveSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            var pythonExecutable = ExpandPath(ReadString(root, "arcgis_python_executable") ?? Default.PythonExecutable) ?? Default.PythonExecutable;
            var scriptsRoot = ExpandPath(ReadString(root, "processing_scripts_root") ?? DefaultScriptsRoot) ?? DefaultScriptsRoot;
            var configuredScriptPath = ReadString(root, "create_parcel_script_path");
            var scriptPath = ExpandPath(string.IsNullOrWhiteSpace(configuredScriptPath)
                ? Path.Combine(scriptsRoot, "CreateParcelFromFile.py")
                : configuredScriptPath) ?? Path.Combine(scriptsRoot, "CreateParcelFromFile.py");
            var outputAdapterPath = ExpandPath(ReadString(root, "output_adapter_script_path")
                ?? Default.OutputAdapterScriptPath) ?? Default.OutputAdapterScriptPath;
            var reviewWorkspaceMode = NormalizeReviewWorkspaceMode(ReadString(root, "review_workspace_mode"));
            var outputAdapterTimeoutSeconds = ReadPositiveInt(root, "output_adapter_timeout_seconds")
                ?? GetDefaultOutputAdapterTimeoutSeconds(reviewWorkspaceMode);
            var outputTemplateProjectPath = ExpandPath(ReadString(root, "output_template_project_path"));
            var outputTemplateGdbPath = ExpandPath(ReadString(root, "output_template_gdb_path"));
            var validationAdapterPath = ExpandPath(ReadString(root, "validation_adapter_script_path")
                ?? Default.ValidationAdapterScriptPath) ?? Default.ValidationAdapterScriptPath;
            var validationRulesPath = ExpandPath(ReadString(root, "validation_rules_path")
                ?? Default.ValidationRulesPath);

            return new WorkflowExecutionSettings(
                pythonExecutable,
                scriptPath,
                outputAdapterPath,
                reviewWorkspaceMode,
                outputAdapterTimeoutSeconds,
                outputTemplateProjectPath,
                outputTemplateGdbPath,
                validationAdapterPath,
                validationRulesPath);
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

    private static int? ReadPositiveInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericValue) && numericValue > 0)
        {
            return numericValue;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out var parsedValue)
            && parsedValue > 0)
        {
            return parsedValue;
        }

        return null;
    }

    private static string? ExpandPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Environment.ExpandEnvironmentVariables(path);
    }

    private static string? ResolveProjectFile(string relativePath)
    {
        var searchRoots = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots)
        {
            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static string NormalizeReviewWorkspaceMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default.ReviewWorkspaceMode;
        }

        var normalized = value.Trim().Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers => InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers,
            InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLocal => InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy,
            InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy => InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy,
            "parcel-fabric" => InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy,
            "parcelfabric" => InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy,
            _ => InnolaTransactionSettings.ReviewWorkspaceModeNormal
        };
    }

    private static int GetDefaultOutputAdapterTimeoutSeconds(string reviewWorkspaceMode)
    {
        return string.Equals(reviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy, StringComparison.OrdinalIgnoreCase)
            ? DefaultOutputAdapterTimeoutSecondsParcelFabric
            : DefaultOutputAdapterTimeoutSecondsNormal;
    }
}
