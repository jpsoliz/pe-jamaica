using System.Text.Json;
using System.IO;

namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionSettings(
    string ServerUrl,
    string Mode,
    string ProcessStep,
    string CaseFolderOutputRoot,
    InnolaClientCertificateSettings ClientCertificate)
{
    public static InnolaTransactionSettings Default { get; } = new(
        InnolaSettings.DefaultServerUrl,
        "mock",
        "parcel_workflow",
        DefaultCaseFolderOutputRoot(),
        InnolaClientCertificateSettings.Default);

    public static InnolaTransactionSettings Load()
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
            var serverUrl = ReadString(root, "innola_server_url") ?? Default.ServerUrl;
            var mode = ReadString(root, "innola_transaction_mode") ?? Default.Mode;
            var processStep = ReadString(root, "innola_process_step") ?? Default.ProcessStep;
            var outputRoot = ReadString(root, "case_folder_output_root");
            var certificate = InnolaClientCertificateSettings.FromJson(root);
            return new InnolaTransactionSettings(
                InnolaHttp.NormalizeServerUrl(serverUrl),
                mode,
                processStep,
                string.IsNullOrWhiteSpace(outputRoot) ? Default.CaseFolderOutputRoot : ExpandPath(outputRoot),
                certificate);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or UriFormatException)
        {
            return Default;
        }
    }

    public static string SettingsPath => Path.Combine(GetAssemblyDirectory(), "Settings", "WorkflowSettings.json");

    private static string ResolveSettingsPath()
    {
        foreach (var candidate in GetSettingsPathCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return SettingsPath;
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
        var assemblyPath = typeof(InnolaTransactionSettings).Assembly.Location;
        var directory = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetDirectoryName(assemblyPath);
        return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
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

public sealed record InnolaClientCertificateSettings(
    bool Enabled,
    string StoreLocation,
    string StoreName,
    string? SubjectName,
    string? Thumbprint)
{
    public static InnolaClientCertificateSettings Default { get; } = new(
        true,
        "CurrentUser",
        "My",
        "Jamaica eTitles Project Team",
        null);

    public static InnolaClientCertificateSettings FromJson(JsonElement root)
    {
        var enabled = ReadBool(root, "innola_client_certificate_enabled") ?? Default.Enabled;
        var storeLocation = ReadString(root, "innola_client_certificate_store_location") ?? Default.StoreLocation;
        var storeName = ReadString(root, "innola_client_certificate_store_name") ?? Default.StoreName;
        var subject = ReadString(root, "innola_client_certificate_subject") ?? Default.SubjectName;
        var thumbprint = ReadString(root, "innola_client_certificate_thumbprint") ?? Default.Thumbprint;
        return new InnolaClientCertificateSettings(enabled, storeLocation, storeName, subject, thumbprint);
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
