using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Preflight;

public static class PreflightSummarySerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Write(string path, PreflightSummaryDocument summary)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(summary, Options));
    }

    public static PreflightSummaryDocument Read(string path)
    {
        return JsonSerializer.Deserialize<PreflightSummaryDocument>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Preflight summary could not be read: {path}");
    }
}
