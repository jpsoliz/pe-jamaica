using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ParcelWorkflowAddIn.Workflow.Pla;

internal static class ArcGisPlaBMapCleanupService
{
    public static async Task<PlaBMapCleanupResult> RemoveAsync(
        IReadOnlyList<string> groupNames,
        CancellationToken cancellationToken = default)
    {
        var names = groupNames
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (names.Length == 0)
        {
            return PlaBMapCleanupResult.Succeeded(0);
        }

        try
        {
            var removed = await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var map = MapView.Active?.Map;
                if (map is null)
                {
                    return 0;
                }

                var layers = map.GetLayersAsFlattenedList()
                    .Where(layer => names.Contains(layer.Name, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var layer in layers)
                {
                    map.RemoveLayer(layer);
                }

                return layers.Length;
            }).ConfigureAwait(false);
            return PlaBMapCleanupResult.Succeeded(removed);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or OperationCanceledException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            return PlaBMapCleanupResult.Failed($"PLA_B map cleanup could not remove loaded groups: {exception.Message}");
        }
    }
}

public sealed record PlaBMapCleanupResult(
    bool Success,
    string Message,
    int RemovedCount)
{
    public static PlaBMapCleanupResult Succeeded(int removedCount)
    {
        return new PlaBMapCleanupResult(true, $"Removed {removedCount} PLA_B map group(s).", removedCount);
    }

    public static PlaBMapCleanupResult Failed(string message)
    {
        return new PlaBMapCleanupResult(false, message, 0);
    }
}
