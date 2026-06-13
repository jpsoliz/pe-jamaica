using System.IO;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ParcelWorkflowAddIn.Workflow.Output;

public interface IOutputMapIntegrationService
{
    Task<OutputMapIntegrationResult> AddOutputsToActiveMapAsync(OutputSummaryDocument? summary, CancellationToken cancellationToken = default);
}

public sealed record OutputMapIntegrationResult(
    bool Success,
    string Message,
    IReadOnlyList<string> LoadedLayerPaths)
{
    public static OutputMapIntegrationResult Skipped(string message)
    {
        return new OutputMapIntegrationResult(false, message, Array.Empty<string>());
    }
}

public sealed class ArcGisOutputMapIntegrationService : IOutputMapIntegrationService
{
    private readonly OutputSummaryPersistenceService persistenceService = new();

    public async Task<OutputMapIntegrationResult> AddOutputsToActiveMapAsync(OutputSummaryDocument? summary, CancellationToken cancellationToken = default)
    {
        if (summary is null)
        {
            return OutputMapIntegrationResult.Skipped("Output summary is not available for map loading.");
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return OutputMapIntegrationResult.Skipped("Outputs were created, but no active map is available to load the generated layers.");
        }

        var layerPaths = persistenceService.GetMapLayerPaths(summary)
            .Where(OutputMapPathResolver.OutputPathExists)
            .ToArray();
        if (layerPaths.Length == 0)
        {
            return OutputMapIntegrationResult.Skipped("Outputs were created, but no map-loadable feature layers were produced.");
        }

        var loadedLayers = new List<Layer>();
        await QueuedTask.Run(() =>
        {
            foreach (var layerPath in layerPaths)
            {
                var existing = mapView.Map.Layers.FirstOrDefault(layer =>
                    string.Equals(layer.URI, new Uri(layerPath).AbsoluteUri, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(layer.Name, Path.GetFileName(layerPath), StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    loadedLayers.Add(existing);
                    continue;
                }

                var created = LayerFactory.Instance.CreateLayer(new Uri(layerPath), mapView.Map);
                if (created is not null)
                {
                    loadedLayers.Add(created);
                }
            }
        }).ConfigureAwait(false);

        if (loadedLayers.Count == 0)
        {
            return OutputMapIntegrationResult.Skipped("Outputs were created, but ArcGIS Pro could not add the generated layers to the active map.");
        }

        try
        {
            await mapView.ZoomToAsync(loadedLayers).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new OutputMapIntegrationResult(
                true,
                "Output layers were added to the active map, but zoom could not be completed automatically.",
                layerPaths);
        }

        return new OutputMapIntegrationResult(
            true,
            "Output layers were added to the active map and zoomed for review.",
            layerPaths);
    }
}

internal static class OutputMapPathResolver
{
    public static bool OutputPathExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            return true;
        }

        var gdbMarker = ".gdb" + Path.DirectorySeparatorChar;
        var index = path.IndexOf(gdbMarker, StringComparison.OrdinalIgnoreCase);
        if (index <= 0)
        {
            return false;
        }

        var gdbPath = path[..(index + 4)];
        return Directory.Exists(gdbPath);
    }
}
