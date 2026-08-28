using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn;

internal static class ReviewWorkspaceDiagnostics
{
    private const string LogFileName = "review_workspace_diagnostics.jsonl";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static void Write(string? caseFolderPath, string eventName, Exception? exception = null, object? context = null)
    {
        try
        {
            var entry = new
            {
                timestamp = DateTimeOffset.Now.ToString("O"),
                event_name = Clean(eventName),
                exception = exception is null ? null : BuildException(exception),
                context
            };
            var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
            foreach (var path in ResolveLogPaths(caseFolderPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                Directory.CreateDirectory(directory);
                File.AppendAllText(path, line);
            }
        }
        catch
        {
        }
    }

    internal static string GetPrimaryLogPath(string? caseFolderPath)
    {
        if (!string.IsNullOrWhiteSpace(caseFolderPath))
        {
            return Path.Combine(caseFolderPath, "working", LogFileName);
        }

        return Path.Combine(GetLocalLogRoot(), LogFileName);
    }

    private static IEnumerable<string> ResolveLogPaths(string? caseFolderPath)
    {
        yield return Path.Combine(GetLocalLogRoot(), LogFileName);
        if (!string.IsNullOrWhiteSpace(caseFolderPath))
        {
            yield return Path.Combine(caseFolderPath, "working", LogFileName);
        }
    }

    private static string GetLocalLogRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SidwellCo",
            "ParcelWorkflow",
            "logs");
    }

    private static object BuildException(Exception exception)
    {
        return new
        {
            type = exception.GetType().FullName,
            message = Clean(exception.Message),
            hresult = $"0x{exception.HResult:X8}",
            stack_trace = Clean(exception.StackTrace),
            inner = exception.InnerException is null ? null : BuildException(exception.InnerException)
        };
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return normalized.Length <= 4000
            ? normalized
            : normalized[..4000] + "...";
    }
}
