using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class OutputSummaryPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string OutputArtifactFileName => "output_summary.json";

    public OutputSummaryDocument? Load(CaseFolderLayout layout)
    {
        var path = Path.Combine(layout.OutputDirectory, OutputArtifactFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OutputSummaryDocument>(File.ReadAllText(path), JsonOptions);
    }

    public void Save(CaseFolderLayout layout, OutputSummaryDocument document)
    {
        Directory.CreateDirectory(layout.OutputDirectory);
        var path = Path.Combine(layout.OutputDirectory, OutputArtifactFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
    }

    public IReadOnlyList<string> GetArtifactPaths(CaseFolderLayout layout, OutputSummaryDocument? summary)
    {
        var paths = new List<string>();
        var summaryPath = Path.Combine(layout.OutputDirectory, OutputArtifactFileName);
        if (File.Exists(summaryPath))
        {
            paths.Add(summaryPath);
        }

        if (summary is null)
        {
            return paths;
        }

        if (!string.IsNullOrWhiteSpace(summary.Payload.ResultGdbPath))
        {
            paths.Add(summary.Payload.ResultGdbPath);
        }

        foreach (var path in summary.Payload.ArtifactPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> GetMapLayerPaths(OutputSummaryDocument? summary)
    {
        if (summary is null)
        {
            return Array.Empty<string>();
        }

        return summary.Payload.MapLayerPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool IsCreated(OutputSummaryDocument? document)
    {
        return string.Equals(document?.Payload.Status, "created", StringComparison.OrdinalIgnoreCase);
    }
}
