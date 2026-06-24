using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;

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

        IEnumerable<string?> paths = summary.Payload.MapLayerPaths;

        if (string.Equals(summary.Payload.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(summary.Payload.ParcelFabricMode, "true", StringComparison.OrdinalIgnoreCase))
        {
            paths = new string?[]
            {
                summary.Payload.ReviewLayerPath,
                summary.Payload.PolygonFeatureClassPath,
                summary.Payload.LineFeatureClassPath,
                summary.Payload.PointFeatureClassPath
            };
        }
        else if (string.Equals(summary.Payload.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, StringComparison.OrdinalIgnoreCase))
        {
            var publishedLayers = summary.Payload.EnterpriseWorkingPublish?.PublishedLayers ?? Array.Empty<EnterpriseWorkingPublishedLayer>();
            var fabricLayer = publishedLayers.FirstOrDefault(layer =>
                string.Equals(layer.LayerRole, "fabric", StringComparison.OrdinalIgnoreCase))?.Target;
            var parcelLayer = publishedLayers.FirstOrDefault(layer =>
                string.Equals(layer.LayerRole, "parcels", StringComparison.OrdinalIgnoreCase))?.Target;

            paths = new string?[]
            {
                fabricLayer,
                parcelLayer,
                summary.Payload.PolygonFeatureClassPath,
                summary.Payload.LineFeatureClassPath,
                summary.Payload.PointFeatureClassPath
            };
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool IsCreated(OutputSummaryDocument? document)
    {
        return string.Equals(document?.Payload.Status, "created", StringComparison.OrdinalIgnoreCase);
    }
}
