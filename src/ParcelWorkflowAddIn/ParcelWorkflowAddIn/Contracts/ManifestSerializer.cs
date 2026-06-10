using System.Text.Json;
using System.IO;

namespace ParcelWorkflowAddIn.Contracts;

public static class ManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Write(string path, ManifestDocument manifest)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(manifest, Options));
    }

    public static ManifestDocument Read(string path)
    {
        return JsonSerializer.Deserialize<ManifestDocument>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Manifest could not be read: {path}");
    }
}
