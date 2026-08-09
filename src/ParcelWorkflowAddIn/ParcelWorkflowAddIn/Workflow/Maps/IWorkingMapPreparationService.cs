using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Maps;

public interface IWorkingMapPreparationService
{
    Task<WorkingMapPreparationResult> PrepareWorkingMapAsync(
        WorkingMapSettings settings,
        InnolaTransactionDetail detail,
        CancellationToken cancellationToken = default);
}

public sealed record WorkingMapPreparationResult(
    bool Success,
    string Message,
    IReadOnlyList<string> Warnings)
{
    public static WorkingMapPreparationResult Succeeded(string message, IReadOnlyList<string>? warnings = null)
    {
        return new WorkingMapPreparationResult(true, message, warnings ?? Array.Empty<string>());
    }

    public static WorkingMapPreparationResult Failed(string message, IReadOnlyList<string>? warnings = null)
    {
        return new WorkingMapPreparationResult(false, message, warnings ?? Array.Empty<string>());
    }
}

public sealed class NoOpWorkingMapPreparationService : IWorkingMapPreparationService
{
    public static NoOpWorkingMapPreparationService Instance { get; } = new();

    private NoOpWorkingMapPreparationService()
    {
    }

    public Task<WorkingMapPreparationResult> PrepareWorkingMapAsync(
        WorkingMapSettings settings,
        InnolaTransactionDetail detail,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WorkingMapPreparationResult.Succeeded("Working map preparation skipped."));
    }
}

public sealed record WorkingMapPreparationPlan(
    bool Success,
    bool Enabled,
    string MapName,
    bool CreateIfMissing,
    bool ReuseExisting,
    bool ActivateOnTransactionLoad,
    bool CleanupTransactionGroupsOnClose,
    string DefaultBasemap,
    IReadOnlyList<string> AlternateBasemaps,
    WorkingMapExtent ZoomExtent,
    IReadOnlyList<WorkingMapLayerPlan> Layers,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Blockers);

public sealed record WorkingMapLayerPlan(
    string Name,
    string SourceType,
    string Url,
    string Group,
    bool Required,
    bool Visible,
    int Order,
    double Opacity,
    string? BasemapRole,
    double? MinScale,
    double? MaxScale);

public sealed record WorkingMapExistingLayerSnapshot(string Name, string? Uri);

public sealed record WorkingMapPreparedStatus(
    bool IsPrepared,
    IReadOnlyList<WorkingMapLayerPlan> MissingRequiredLayers);

public static class WorkingMapPreparationPlanner
{
    public static WorkingMapPreparationPlan Build(WorkingMapSettings settings, InnolaTransactionDetail? detail)
    {
        var warnings = new List<string>();
        var blockers = new List<string>();
        var layers = new List<WorkingMapLayerPlan>();

        if (!string.IsNullOrWhiteSpace(settings.Warning))
        {
            warnings.Add(settings.Warning);
        }

        foreach (var layer in settings.ReferenceLayers.OrderBy(layer => layer.Order).ThenBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(layer.Url))
            {
                var message = $"Working map reference layer '{layer.Name}' has no configured URL.";
                if (layer.Required)
                {
                    blockers.Add(message);
                }
                else
                {
                    warnings.Add(message);
                }

                continue;
            }

            layers.Add(new WorkingMapLayerPlan(
                layer.Name,
                layer.SourceType,
                layer.Url,
                string.IsNullOrWhiteSpace(layer.Group) ? "Reference Layers" : layer.Group,
                layer.Required,
                layer.Visible,
                layer.Order,
                layer.Opacity,
                layer.BasemapRole,
                layer.MinScale,
                layer.MaxScale));
        }

        var zoomExtent = ResolveZoomExtent(settings, detail, warnings, blockers);
        return new WorkingMapPreparationPlan(
            blockers.Count == 0,
            settings.Enabled,
            settings.MapName,
            settings.CreateIfMissing,
            settings.ReuseExisting,
            settings.ActivateOnTransactionLoad,
            settings.CleanupTransactionGroupsOnClose,
            settings.DefaultBasemap,
            settings.AlternateBasemaps,
            zoomExtent,
            layers,
            warnings,
            blockers);
    }

    private static WorkingMapExtent ResolveZoomExtent(
        WorkingMapSettings settings,
        InnolaTransactionDetail? detail,
        List<string> warnings,
        List<string> blockers)
    {
        if (!settings.ZoomToTransactionParish || !settings.ParishLookup.Enabled)
        {
            return settings.DefaultExtent;
        }

        var parish = detail?.Parish;
        if (string.IsNullOrWhiteSpace(parish))
        {
            return settings.DefaultExtent;
        }

        var normalized = WorkingMapParishLookupSettings.NormalizeParishKey(parish);
        if (settings.ParishLookup.KnownExtents.TryGetValue(normalized, out var extent))
        {
            return extent;
        }

        var message = $"Parish '{parish}' was not found in configured parish extents; using the configured default Jamaica extent.";
        if (settings.ParishLookup.Required)
        {
            blockers.Add(message);
        }
        else
        {
            warnings.Add(message);
        }

        return settings.DefaultExtent;
    }
}

public sealed class ArcGisWorkingMapPreparationService : IWorkingMapPreparationService
{
    private const int Jad2001Wkid = 3448;

    public async Task<WorkingMapPreparationResult> PrepareWorkingMapAsync(
        WorkingMapSettings settings,
        InnolaTransactionDetail detail,
        CancellationToken cancellationToken = default)
    {
        var plan = WorkingMapPreparationPlanner.Build(settings, detail);
        if (!plan.Enabled)
        {
            return WorkingMapPreparationResult.Succeeded("Working map preparation is disabled.", plan.Warnings);
        }

        if (!plan.Success)
        {
            return WorkingMapPreparationResult.Failed(string.Join(" ", plan.Blockers), plan.Warnings);
        }

        var warnings = plan.Warnings.ToList();
        MapView? preparedView;
        try
        {
            preparedView = await EnsureMapViewAsync(plan, cancellationToken).ConfigureAwait(false);
            if (preparedView?.Map is null)
            {
                return WorkingMapPreparationResult.Failed($"Working map '{plan.MapName}' could not be opened or created.", warnings);
            }

            var existingLayers = await CaptureExistingLayerSnapshotsAsync(preparedView.Map, cancellationToken).ConfigureAwait(false);
            var preparedStatus = EvaluatePreparedMap(plan, existingLayers);
            var missingForegroundLayers = MissingForegroundReferenceLayers(plan, existingLayers);

            await ZoomToExtentAsync(preparedView, plan.ZoomExtent).ConfigureAwait(false);
            await ApplyExistingReferenceLayerPropertiesAsync(preparedView.Map, plan, cancellationToken).ConfigureAwait(false);

            if (missingForegroundLayers.Count > 0)
            {
                await AddReferenceLayersAsync(
                    preparedView.Map,
                    plan with { Layers = missingForegroundLayers },
                    warnings,
                    cancellationToken).ConfigureAwait(false);
            }

            if (preparedStatus.IsPrepared && missingForegroundLayers.Count == 0)
            {
                return WorkingMapPreparationResult.Succeeded(
                    $"Working map '{plan.MapName}' is ready; existing map and required layers were reused.",
                    warnings);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or UriFormatException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            return WorkingMapPreparationResult.Failed($"Working map could not be prepared: {exception.Message}", warnings);
        }

        return WorkingMapPreparationResult.Succeeded($"Working map '{plan.MapName}' is ready.", warnings);
    }

    private static async Task<MapView?> EnsureMapViewAsync(WorkingMapPreparationPlan plan, CancellationToken cancellationToken)
    {
        var activeView = MapView.Active;
        if (activeView?.Map is not null && activeView.Map.Name.Equals(plan.MapName, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureJad2001SpatialReferenceAsync(activeView.Map, cancellationToken).ConfigureAwait(false);
            return activeView;
        }

        Map? map = null;
        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan.ReuseExisting)
            {
                foreach (var item in Project.Current.GetItems<MapProjectItem>())
                {
                    var candidate = item.GetMap();
                    if (candidate.Name.Equals(plan.MapName, StringComparison.OrdinalIgnoreCase))
                    {
                        map = candidate;
                        break;
                    }
                }
            }

            if (map is null && plan.CreateIfMissing)
            {
                map = MapFactory.Instance.CreateMap(plan.MapName, MapType.Map, MapViewingMode.Map, ResolveBasemap(plan.DefaultBasemap));
            }

            if (map is not null)
            {
                EnsureJad2001SpatialReference(map);
            }
        }).ConfigureAwait(false);

        if (map is null)
        {
            return null;
        }

        if (!plan.ActivateOnTransactionLoad && activeView?.Map is not null)
        {
            await EnsureJad2001SpatialReferenceAsync(activeView.Map, cancellationToken).ConfigureAwait(false);
            return activeView;
        }

        if (activeView?.Map is not null && activeView.Map.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureJad2001SpatialReferenceAsync(activeView.Map, cancellationToken).ConfigureAwait(false);
            return activeView;
        }

        var pane = await ProApp.Panes.CreateMapPaneAsync(map).ConfigureAwait(false);
        return pane is IMapPane mapPane ? mapPane.MapView : MapView.Active;
    }

    private static async Task EnsureJad2001SpatialReferenceAsync(Map map, CancellationToken cancellationToken)
    {
        await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureJad2001SpatialReference(map);
        }).ConfigureAwait(false);
    }

    private static void EnsureJad2001SpatialReference(Map map)
    {
        map.SetSpatialReference(SpatialReferenceBuilder.CreateSpatialReference(Jad2001Wkid));
    }

    private static async Task AddReferenceLayersAsync(
        Map map,
        WorkingMapPreparationPlan plan,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        await QueuedTask.Run(() =>
        {
            foreach (var layer in plan.Layers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ShouldAddReferenceLayer(layer, plan.DefaultBasemap))
                {
                    continue;
                }

                if (LayerAlreadyExists(map, layer))
                {
                    ApplySafeLayerProperties(FindLayer(map, layer), layer);
                    continue;
                }

                try
                {
                    var parent = EnsureGroupLayer(map, layer.Group);
                    var created = LayerFactory.Instance.CreateLayer(new Uri(layer.Url), parent);
                    if (created is null)
                    {
                        HandleLayerCreateFailure(layer, warnings, "ArcGIS Pro did not return a layer.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(layer.Name) && !created.Name.Equals(layer.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        created.SetName(layer.Name);
                    }

                    ApplySafeLayerProperties(created, layer);
                }
                catch (Exception exception) when (exception is ArgumentException
                    or InvalidOperationException
                    or NotSupportedException
                    or UriFormatException
                    or ArcGIS.Core.CalledOnWrongThreadException)
                {
                    HandleLayerCreateFailure(layer, warnings, exception.Message);
                }
            }
        }).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<WorkingMapExistingLayerSnapshot>> CaptureExistingLayerSnapshotsAsync(
        Map map,
        CancellationToken cancellationToken)
    {
        return await QueuedTask.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FlattenLayers(map.Layers)
                .Select(layer => new WorkingMapExistingLayerSnapshot(layer.Name, layer.URI))
                .ToArray();
        }).ConfigureAwait(false);
    }

    private static async Task ApplyExistingReferenceLayerPropertiesAsync(
        Map map,
        WorkingMapPreparationPlan plan,
        CancellationToken cancellationToken)
    {
        await QueuedTask.Run(() =>
        {
            foreach (var layer in plan.Layers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplySafeLayerProperties(FindLayer(map, layer), layer);
            }
        }).ConfigureAwait(false);
    }

    private static Basemap ResolveBasemap(string? configuredBasemap)
    {
        var normalized = configuredBasemap?.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "esri_world_imagery" or "imagery" or "satellite" => Basemap.Satellite,
            "world_topographic" or "topographic" => Basemap.Topographic,
            "open_basemap" or "openstreetmap" or "openstreetmap_vector" => Basemap.OpenStreetMap,
            "streets" or "streets_vector" => Basemap.StreetsVector,
            "none" => Basemap.None,
            _ => Basemap.ProjectDefault
        };
    }

    internal static bool ShouldAddReferenceLayer(WorkingMapLayerPlan layer, string? configuredDefaultBasemap)
    {
        if (string.IsNullOrWhiteSpace(layer.BasemapRole))
        {
            return true;
        }

        return !BasemapRoleMatchesDefault(layer.BasemapRole, configuredDefaultBasemap);
    }

    internal static bool ShouldPrepareLayerInForeground(WorkingMapLayerPlan layer)
    {
        return layer.Required || layer.Visible;
    }

    internal static WorkingMapPreparedStatus EvaluatePreparedMap(
        WorkingMapPreparationPlan plan,
        IReadOnlyList<WorkingMapExistingLayerSnapshot> existingLayers)
    {
        var missingRequiredLayers = plan.Layers
            .Where(layer => layer.Required)
            .Where(layer => ShouldAddReferenceLayer(layer, plan.DefaultBasemap))
            .Where(layer => !LayerSnapshotExists(existingLayers, layer))
            .ToArray();
        return new WorkingMapPreparedStatus(missingRequiredLayers.Length == 0, missingRequiredLayers);
    }

    private static IReadOnlyList<WorkingMapLayerPlan> MissingForegroundReferenceLayers(
        WorkingMapPreparationPlan plan,
        IReadOnlyList<WorkingMapExistingLayerSnapshot> existingLayers)
    {
        return plan.Layers
            .Where(layer => ShouldAddReferenceLayer(layer, plan.DefaultBasemap))
            .Where(ShouldPrepareLayerInForeground)
            .Where(layer => !LayerSnapshotExists(existingLayers, layer))
            .ToArray();
    }

    private static bool BasemapRoleMatchesDefault(string basemapRole, string? configuredDefaultBasemap)
    {
        var role = NormalizeBasemapToken(basemapRole);
        var configured = NormalizeBasemapToken(configuredDefaultBasemap);
        return role switch
        {
            "imagery" => configured is "esri_world_imagery" or "imagery" or "satellite",
            "streets" => configured is "open_basemap" or "openstreetmap" or "openstreetmap_vector" or "streets" or "streets_vector",
            "topographic" => configured is "world_topographic" or "topographic",
            _ => string.Equals(role, configured, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeBasemapToken(string? value)
    {
        return value?.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant() ?? string.Empty;
    }

    private static void HandleLayerCreateFailure(WorkingMapLayerPlan layer, List<string> warnings, string message)
    {
        var text = $"Working map layer '{layer.Name}' could not be added: {message}";
        if (layer.Required)
        {
            throw new InvalidOperationException(text);
        }

        warnings.Add(text);
    }

    private static GroupLayer EnsureGroupLayer(Map map, string groupLayerName)
    {
        var group = map.Layers.OfType<GroupLayer>()
            .FirstOrDefault(layer => layer.Name.Equals(groupLayerName, StringComparison.OrdinalIgnoreCase));
        return group ?? LayerFactory.Instance.CreateGroupLayer(map, 0, groupLayerName);
    }

    private static bool LayerAlreadyExists(Map map, WorkingMapLayerPlan plan)
    {
        return FindLayer(map, plan) is not null;
    }

    private static bool LayerSnapshotExists(
        IReadOnlyList<WorkingMapExistingLayerSnapshot> existingLayers,
        WorkingMapLayerPlan plan)
    {
        return existingLayers.Any(layer => layer.Name.Equals(plan.Name, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(layer.Uri) && layer.Uri.Equals(plan.Url, StringComparison.OrdinalIgnoreCase)));
    }

    private static Layer? FindLayer(Map map, WorkingMapLayerPlan plan)
    {
        return FlattenLayers(map.Layers)
            .FirstOrDefault(layer => layer.Name.Equals(plan.Name, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(layer.URI) && layer.URI.Equals(plan.Url, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ApplySafeLayerProperties(Layer? layer, WorkingMapLayerPlan plan)
    {
        if (layer is null)
        {
            return;
        }

        layer.SetVisibility(plan.Visible);
        if (layer is FeatureLayer featureLayer)
        {
            featureLayer.SetTransparency((int)Math.Round((1 - plan.Opacity) * 100));
        }
        else
        {
            layer.SetTransparency((int)Math.Round((1 - plan.Opacity) * 100));
        }

        var definition = layer.GetDefinition() as CIMBasicFeatureLayer;
        if (definition is not null)
        {
            if (plan.MinScale is not null)
            {
                definition.MinScale = plan.MinScale.Value;
            }

            if (plan.MaxScale is not null)
            {
                definition.MaxScale = plan.MaxScale.Value;
            }

            layer.SetDefinition(definition);
        }
    }

    private static IEnumerable<Layer> FlattenLayers(IEnumerable<Layer> layers)
    {
        foreach (var layer in layers)
        {
            yield return layer;
            if (layer is CompositeLayer compositeLayer)
            {
                foreach (var child in FlattenLayers(compositeLayer.Layers))
                {
                    yield return child;
                }
            }
        }
    }

    private static async Task ZoomToExtentAsync(MapView mapView, WorkingMapExtent extent)
    {
        var spatialReference = SpatialReferenceBuilder.CreateSpatialReference(extent.Wkid);
        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            extent.XMin,
            extent.YMin,
            extent.XMax,
            extent.YMax,
            spatialReference);
        await mapView.ZoomToAsync(envelope).ConfigureAwait(false);
    }
}
