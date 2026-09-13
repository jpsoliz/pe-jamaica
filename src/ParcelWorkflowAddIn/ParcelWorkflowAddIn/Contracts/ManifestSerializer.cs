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

        var serialized = JsonSerializer.Serialize(manifest, Options);
        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(directory) ? Directory.GetCurrentDirectory() : directory,
            $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, serialized);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static ManifestDocument Read(string path)
    {
        return JsonSerializer.Deserialize<ManifestDocument>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Manifest could not be read: {path}");
    }
}
