using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Enterprise.PortalAuth;

namespace ParcelWorkflowAddIn.Compare;

public sealed class ArcGisCompareMapIntegrationService : ICompareMapIntegrationService
{
    private readonly IPortalAuthProvider portalAuthProvider;

    public ArcGisCompareMapIntegrationService()
        : this(CompositePortalAuthProvider.CreateDefault())
    {
    }

    public ArcGisCompareMapIntegrationService(IPortalAuthProvider portalAuthProvider)
    {
        this.portalAuthProvider = portalAuthProvider;
    }

    public async Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
        CompareWorkingGeometryLoadPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!plan.IsValid)
        {
            return CompareMapIntegrationResult.Failed(plan.InvalidReason ?? "Compare geometry load plan is invalid.");
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return CompareMapIntegrationResult.MapUnavailable("No active ArcGIS Pro map is available. Open a map and retry Compare geometry loading.");
        }

        var authResult = await TryAuthenticateAsync(plan, cancellationToken).ConfigureAwait(false);
        if (!authResult.Success)
        {
            return CompareMapIntegrationResult.Failed(authResult.ErrorMessage ?? "ArcGIS Portal authentication failed for Compare working layers.");
        }

        var loadedLayerUrls = new List<string>();
        var zoomLayers = new List<Layer>();
        var groupLayerName = BuildGroupLayerName(plan);
        int? polygonFeatureCount = null;
        try
        {
            await QueuedTask.Run(() =>
            {
                var groupLayer = EnsureGroupLayer(mapView.Map, groupLayerName);
                foreach (var request in OrderLayers(plan.Layers))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RemoveExistingLayer(mapView.Map, request.LayerUrl);
                    var layer = LayerFactory.Instance.CreateLayer(new Uri(request.LayerUrl), groupLayer);
                    if (layer is FeatureLayer featureLayer)
                    {
                        ApplyDefinitionQuery(featureLayer, request.DefinitionQuery);
                        featureLayer.SetEditable(false);
                        if (request.Role == CompareWorkingLayerRole.Polygons)
                        {
                            polygonFeatureCount = CountFeatures(featureLayer, request.DefinitionQuery);
                        }
                    }

                    loadedLayerUrls.Add(request.LayerUrl);
                    if (request.Role == CompareWorkingLayerRole.Polygons)
                    {
                        zoomLayers.Add(layer);
                    }
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or UriFormatException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            return CompareMapIntegrationResult.Failed($"Compare working layers could not be loaded into the active map: {exception.Message}");
        }

        if (loadedLayerUrls.Count == 0)
        {
            return CompareMapIntegrationResult.Failed("Compare working layers could not be added to the active map.");
        }

        if (polygonFeatureCount == 0)
        {
            return new CompareMapIntegrationResult(
                CompareMapIntegrationStatus.NoPolygons,
                $"No working_review polygons were found for {plan.ScopeField} '{plan.ScopeValue}'.",
                loadedLayerUrls,
                groupLayerName,
                0);
        }

        try
        {
            await mapView.ZoomToAsync(zoomLayers.Count > 0 ? zoomLayers : mapView.Map.Layers).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return CompareMapIntegrationResult.Loaded(
                BuildLoadedMessage(plan, groupLayerName, polygonFeatureCount, zoomed: false),
                loadedLayerUrls,
                groupLayerName,
                polygonFeatureCount);
        }

        return CompareMapIntegrationResult.Loaded(
            BuildLoadedMessage(plan, groupLayerName, polygonFeatureCount, zoomed: true),
            loadedLayerUrls,
            groupLayerName,
            polygonFeatureCount);
    }

    public static string BuildGroupLayerName(CompareWorkingGeometryLoadPlan plan)
    {
        return $"Compare Review - {plan.ScopeValue}";
    }

    private async Task<PortalAuthResult> TryAuthenticateAsync(
        CompareWorkingGeometryLoadPlan plan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plan.PortalUrl))
        {
            return PortalAuthResult.Succeeded("arcgis-pro-session", "not_required");
        }

        var primaryLayer = plan.Layers.FirstOrDefault()?.LayerUrl;
        return await portalAuthProvider.GetTokenAsync(
            new PortalAuthRequest(plan.PortalUrl, primaryLayer, "compare_geometry_load"),
            cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<CompareWorkingLayerRequest> OrderLayers(IEnumerable<CompareWorkingLayerRequest> layers)
    {
        return layers.OrderBy(layer => layer.Role switch
        {
            CompareWorkingLayerRole.Polygons => 0,
            CompareWorkingLayerRole.Lines => 1,
            CompareWorkingLayerRole.Points => 2,
            _ => 3
        });
    }

    private static GroupLayer EnsureGroupLayer(Map map, string groupLayerName)
    {
        var existingGroup = map.Layers.OfType<GroupLayer>()
            .FirstOrDefault(layer => string.Equals(layer.Name, groupLayerName, StringComparison.OrdinalIgnoreCase));
        if (existingGroup is not null)
        {
            return existingGroup;
        }

        return LayerFactory.Instance.CreateGroupLayer(map, 0, groupLayerName);
    }

    private static void RemoveExistingLayer(Map map, string layerUrl)
    {
        foreach (var layer in FlattenLayers(map.Layers).ToArray())
        {
            if (string.Equals(layer.URI, new Uri(layerUrl).AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                map.RemoveLayer(layer);
            }
        }
    }

    private static IEnumerable<Layer> FlattenLayers(IEnumerable<Layer> layers)
    {
        foreach (var layer in layers)
        {
            yield return layer;
            if (layer is CompositeLayer compositeLayer)
            {
                foreach (var childLayer in FlattenLayers(compositeLayer.Layers))
                {
                    yield return childLayer;
                }
            }
        }
    }

    private static void ApplyDefinitionQuery(FeatureLayer featureLayer, string definitionQuery)
    {
        featureLayer.SetDefinitionQuery(definitionQuery);
    }

    private static int? CountFeatures(FeatureLayer featureLayer, string definitionQuery)
    {
        try
        {
            using var table = featureLayer.GetTable();
            if (table is null)
            {
                return null;
            }

            var count = table.GetCount(new QueryFilter { WhereClause = definitionQuery });
            return count > int.MaxValue ? int.MaxValue : Convert.ToInt32(count);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            return null;
        }
    }

    private static string BuildLoadedMessage(
        CompareWorkingGeometryLoadPlan plan,
        string groupLayerName,
        int? polygonFeatureCount,
        bool zoomed)
    {
        var polygonText = polygonFeatureCount.HasValue
            ? $"{polygonFeatureCount.Value} polygon feature(s)"
            : "polygon features";
        var zoomText = zoomed
            ? "Map zoomed to the transaction layer."
            : "Layers were added, but the map could not zoom automatically.";

        return $"Compare working layers loaded into ArcGIS Pro map group '{groupLayerName}' for {plan.ScopeField} '{plan.ScopeValue}' ({polygonText}). {zoomText}";
    }
}
