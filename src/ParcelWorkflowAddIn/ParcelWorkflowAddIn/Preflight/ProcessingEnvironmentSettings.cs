using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Preflight;

public sealed record ProcessingEnvironmentSettings(
    string ArcGisProSdkLane,
    string TargetFramework,
    string PythonExecutable,
    IReadOnlyList<string> RequiredPackages,
    IReadOnlyList<string> OptionalPackages,
    bool ArcPyRequired,
    bool UnknownArcGisVersionIsWarning)
{
    public static ProcessingEnvironmentSettings Default { get; } = new(
        "3.6",
        "net8.0-windows",
        @"C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe",
        new[] { "arcpy" },
        Array.Empty<string>(),
        ArcPyRequired: true,
        UnknownArcGisVersionIsWarning: true);

    public static ProcessingEnvironmentSettings Load()
    {
        var settingsPath = ResolveSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            var requiredPackages = ReadStringArray(root, "required_python_packages");
            var optionalPackages = ReadStringArray(root, "optional_python_packages");
            return new ProcessingEnvironmentSettings(
                ReadString(root, "arcgis_pro_sdk_lane") ?? Default.ArcGisProSdkLane,
                ReadString(root, "target_framework") ?? Default.TargetFramework,
                ExpandPath(ReadString(root, "arcgis_python_executable") ?? Default.PythonExecutable),
                requiredPackages.Count == 0 ? Default.RequiredPackages : requiredPackages,
                optionalPackages,
                ReadBool(root, "arcpy_required") ?? Default.ArcPyRequired,
                ReadBool(root, "unknown_arcgis_version_is_warning") ?? Default.UnknownArcGisVersionIsWarning);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            return Default;
        }
    }

    private static string ResolveSettingsPath()
    {
        foreach (var candidate in GetSettingsPathCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(GetAssemblyDirectory(), "Settings", "WorkflowSettings.json");
    }

    private static IEnumerable<string> GetSettingsPathCandidates()
    {
        var assemblyDirectory = GetAssemblyDirectory();
        yield return Path.Combine(assemblyDirectory, "Settings", "WorkflowSettings.json");
        yield return Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "Settings", "WorkflowSettings.json"));
        yield return Path.Combine(AppContext.BaseDirectory, "Settings", "WorkflowSettings.json");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Settings", "WorkflowSettings.json"));
    }

    private static string GetAssemblyDirectory()
    {
        var assemblyPath = typeof(ProcessingEnvironmentSettings).Assembly.Location;
        var directory = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetDirectoryName(assemblyPath);
        return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
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

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}
