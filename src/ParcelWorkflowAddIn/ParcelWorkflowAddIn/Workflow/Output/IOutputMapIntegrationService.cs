using System.IO;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
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
            return OutputMapIntegrationResult.Skipped("Output files were created, but no active ArcGIS Pro map was available to load the layers automatically.");
        }

        var layerPaths = persistenceService.GetMapLayerPaths(summary)
            .Where(OutputMapPathResolver.OutputPathExists)
            .Pipe(OutputMapReviewStyling.OrderLayerPaths)
            .ToArray();
        if (layerPaths.Length == 0)
        {
            return OutputMapIntegrationResult.Skipped("Outputs were created, but no map-loadable feature layers were produced.");
        }

        var loadedLayers = new List<Layer>();
        var stylingWarnings = new List<string>();
        await QueuedTask.Run(() =>
        {
            var styledLayerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layerPath in layerPaths)
            {
                var existing = mapView.Map.Layers.FirstOrDefault(layer =>
                    string.Equals(layer.URI, new Uri(layerPath).AbsoluteUri, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(layer.Name, Path.GetFileName(layerPath), StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    loadedLayers.Add(existing);
                    TryConfigureReviewLayer(existing, stylingWarnings, styledLayerKeys);
                    continue;
                }

                var created = LayerFactory.Instance.CreateLayer(new Uri(layerPath), mapView.Map);
                if (created is not null)
                {
                    TryConfigureReviewLayer(created, stylingWarnings, styledLayerKeys);
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
                BuildResultMessage(summary, stylingWarnings, "Output layers were added to the active map, but zoom could not be completed automatically."),
                layerPaths);
        }

        return new OutputMapIntegrationResult(
            true,
            BuildResultMessage(summary, stylingWarnings, OutputMapReviewStyling.BuildSuccessMessage(summary)),
            layerPaths);
    }

    private static string BuildResultMessage(OutputSummaryDocument summary, IReadOnlyList<string> stylingWarnings, string baseMessage)
    {
        if (stylingWarnings.Count <= 0)
        {
            return baseMessage;
        }

        var warningSummary = stylingWarnings.Count == 1
            ? stylingWarnings[0]
            : $"{stylingWarnings.Count} map-style warnings were recorded.";
        return $"{baseMessage} {warningSummary}";
    }

    private static void TryConfigureReviewLayer(Layer layer, ICollection<string> warnings, ISet<string> styledLayerKeys)
    {
        if (layer is CompositeLayer compositeLayer)
        {
            foreach (var childLayer in compositeLayer.Layers)
            {
                TryConfigureReviewLayer(childLayer, warnings, styledLayerKeys);
            }
        }

        if (layer is not FeatureLayer featureLayer)
        {
            return;
        }

        var layerKey = string.IsNullOrWhiteSpace(layer.URI) ? layer.Name : layer.URI;
        if (!styledLayerKeys.Add(layerKey))
        {
            return;
        }

        try
        {
            var fieldNames = OutputMapReviewStyling.TryReadFieldNames(featureLayer);
            OutputMapReviewStyling.ApplyReviewStyling(featureLayer, fieldNames, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Map styling skipped for layer '{layer.Name}': {ex.Message}");
        }
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

internal static class OutputMapReviewStyling
{
    private const string ParcelPointsLayer = "parcel_points";
    private const string ParcelLinesLayer = "parcel_lines";
    private const string ParcelPolygonsLayer = "parcel_polygons";
    private const string ParcelFabricLayer = "local_parcel_fabric";
    private const string FabricPointsLayer = "Points";
    private const string FabricConnectionLinesLayer = "Connection Lines";
    private const string FabricParcelTypeSuffix = "_Lines";

    public static IReadOnlyList<string> OrderLayerPaths(IEnumerable<string> layerPaths)
    {
        return layerPaths
            .OrderBy(GetLayerOrder)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string BuildSuccessMessage(OutputSummaryDocument summary)
    {
        if (string.Equals(summary.Payload.ReviewResultOwner, ReviewResultOwnership.ManualSpatialReview, StringComparison.OrdinalIgnoreCase))
        {
            return "Manual review workspace layers were added to the active map and zoomed for editing. This standard review surface supports COGO-style labels, snapping, and map-based parcel correction without requiring Parcel Fabric.";
        }

        return string.Equals(summary.Payload.ReviewWorkspaceMode, Innola.InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy, StringComparison.OrdinalIgnoreCase)
            ? string.Equals(summary.Payload.ParcelFabricMode, "true", StringComparison.OrdinalIgnoreCase)
                ? "Parcel Fabric review layers were added to the active map and zoomed for review. Use ArcGIS Pro parcel, snapping, and editing tools to inspect and refine the transaction geometry."
                : "Parcel Fabric pilot review layers were added to the active map and zoomed for review. Use ArcGIS Pro parcel, snapping, and editing tools for examination."
            : "COGO-ready non-fabric review layers were added to the active map and zoomed for review. Use ArcGIS Pro snapping, selection, and standard editing tools to inspect points, lines, and parcel boundaries.";
    }

    public static HashSet<string> TryReadFieldNames(FeatureLayer featureLayer)
    {
        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var table = featureLayer.GetTable();
        if (table is null)
        {
            return fieldNames;
        }

        var definition = table.GetDefinition();
        foreach (var field in definition.GetFields())
        {
            fieldNames.Add(field.Name);
        }

        return fieldNames;
    }

    public static void ApplyReviewStyling(FeatureLayer featureLayer, IReadOnlySet<string> fieldNames, ICollection<string> warnings)
    {
        var definition = featureLayer.GetDefinition() as CIMFeatureLayer;
        if (definition is null)
        {
            warnings.Add($"Map styling skipped for layer '{featureLayer.Name}' because the layer definition could not be read.");
            return;
        }

        var role = DetermineLayerRole(featureLayer.Name, fieldNames);
        if (role == ReviewLayerRole.Points)
        {
            ApplyPointRenderer(featureLayer, warnings);
            ApplyPointLabels(featureLayer, definition, fieldNames, warnings);
            return;
        }

        if (role == ReviewLayerRole.Lines)
        {
            ApplyLineRenderer(featureLayer, warnings);
            ApplyLineLabels(featureLayer, definition, fieldNames, warnings);
            return;
        }

        if (role == ReviewLayerRole.Polygons)
        {
            ApplyPolygonRenderer(featureLayer, warnings);
            ApplyPolygonLabels(featureLayer, definition, fieldNames, warnings);
        }
    }

    private static void ApplyPointLabels(FeatureLayer featureLayer, CIMFeatureLayer definition, IReadOnlySet<string> fieldNames, ICollection<string> warnings)
    {
        var pointField = FirstAvailableField(fieldNames, "point_id", "point_identifier", "point_name", "pointname", "name");
        if (pointField is null)
        {
            warnings.Add("Point labels were skipped because no point identifier field was available.");
            return;
        }

        ApplySingleLabelClass(featureLayer, definition, "Point ID", $"$feature.{pointField}");
    }

    private static void ApplyLineLabels(FeatureLayer featureLayer, CIMFeatureLayer definition, IReadOnlySet<string> fieldNames, ICollection<string> warnings)
    {
        var bearingField = FirstAvailableField(fieldNames, "bearing_txt", "bearing", "course", "direction");
        var lengthTextField = FirstAvailableField(fieldNames, "length_txt", "length_txt2", "distance_txt", "distance");
        var distanceField = FirstAvailableField(fieldNames, "distance_m", "distance", "distance_value");

        string? expression = null;
        if (bearingField is not null && lengthTextField is not null)
        {
            expression = $"Trim(When(IsEmpty($feature.{bearingField}), '', $feature.{bearingField}) + ' ' + When(IsEmpty($feature.{lengthTextField}), '', $feature.{lengthTextField}))";
        }
        else if (bearingField is not null)
        {
            expression = $"$feature.{bearingField}";
        }
        else if (lengthTextField is not null)
        {
            expression = $"$feature.{lengthTextField}";
        }
        else if (distanceField is not null)
        {
            expression = $"Text($feature.{distanceField}, '#,##0.###')";
        }

        if (string.IsNullOrWhiteSpace(expression))
        {
            warnings.Add("Line labels were skipped because no bearing or distance fields were available.");
            return;
        }

        ApplySingleLabelClass(featureLayer, definition, "COGO Segment", expression);
    }

    private static void ApplyPolygonLabels(FeatureLayer featureLayer, CIMFeatureLayer definition, IReadOnlySet<string> fieldNames, ICollection<string> warnings)
    {
        var polygonField = FirstAvailableField(fieldNames, "parcel_name", "parcel_id", "name");
        if (polygonField is null)
        {
            warnings.Add("Polygon labels were skipped because no parcel name field was available.");
            return;
        }

        ApplySingleLabelClass(featureLayer, definition, "Parcel", $"$feature.{polygonField}");
    }

    private static void ApplySingleLabelClass(FeatureLayer featureLayer, CIMFeatureLayer definition, string className, string expression)
    {
        var labelClass = definition.LabelClasses?.FirstOrDefault() ?? new CIMLabelClass();
        labelClass.Name = className;
        labelClass.ExpressionEngine = LabelExpressionEngine.Arcade;
        labelClass.Expression = expression;
        labelClass.Visibility = true;
        definition.LabelClasses = new[] { labelClass };
        definition.LabelVisibility = true;
        featureLayer.SetDefinition(definition);
    }

    private static void ApplyPointRenderer(FeatureLayer featureLayer, ICollection<string> warnings)
    {
        try
        {
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                ColorFactory.Instance.CreateRGBColor(194, 65, 12),
                6.0,
                SimpleMarkerStyle.Circle);
            featureLayer.SetRenderer(new CIMSimpleRenderer
            {
                Symbol = pointSymbol.MakeSymbolReference()
            });
        }
        catch (Exception ex)
        {
            warnings.Add($"Point symbology was not applied: {ex.Message}");
        }
    }

    private static void ApplyLineRenderer(FeatureLayer featureLayer, ICollection<string> warnings)
    {
        try
        {
            var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                ColorFactory.Instance.CreateRGBColor(36, 87, 122),
                1.6,
                SimpleLineStyle.Solid);
            featureLayer.SetRenderer(new CIMSimpleRenderer
            {
                Symbol = lineSymbol.MakeSymbolReference()
            });
        }
        catch (Exception ex)
        {
            warnings.Add($"Line symbology was not applied: {ex.Message}");
        }
    }

    private static void ApplyPolygonRenderer(FeatureLayer featureLayer, ICollection<string> warnings)
    {
        try
        {
            var outline = SymbolFactory.Instance.ConstructStroke(
                ColorFactory.Instance.CreateRGBColor(75, 104, 122),
                1.25,
                SimpleLineStyle.Solid);
            var polygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                ColorFactory.Instance.CreateRGBColor(222, 228, 232, 35),
                SimpleFillStyle.Solid,
                outline);
            featureLayer.SetRenderer(new CIMSimpleRenderer
            {
                Symbol = polygonSymbol.MakeSymbolReference()
            });
        }
        catch (Exception ex)
        {
            warnings.Add($"Polygon symbology was not applied: {ex.Message}");
        }
    }

    private static int GetLayerOrder(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.ToLowerInvariant() switch
        {
            ParcelFabricLayer => -10,
            ParcelPolygonsLayer => 0,
            ParcelLinesLayer => 1,
            ParcelPointsLayer => 2,
            _ => 99
        };
    }

    private static ReviewLayerRole DetermineLayerRole(string layerName, IReadOnlySet<string> fieldNames)
    {
        var normalizedLayerName = layerName.Trim();
        if (IsPointLayerName(normalizedLayerName) || HasAnyField(fieldNames, "point_id", "point_identifier", "point_name", "pointname"))
        {
            return ReviewLayerRole.Points;
        }

        if (IsLineLayerName(normalizedLayerName) || HasAnyField(fieldNames, "bearing_txt", "bearing", "course", "direction", "distance_m", "length_txt", "start_pt", "end_pt"))
        {
            return ReviewLayerRole.Lines;
        }

        if (IsPolygonLayerName(normalizedLayerName) || HasAnyField(fieldNames, "parcel_name", "parcel_id", "point_cnt"))
        {
            return ReviewLayerRole.Polygons;
        }

        return ReviewLayerRole.Unknown;
    }

    private static bool IsPointLayerName(string layerName)
    {
        return string.Equals(layerName, ParcelPointsLayer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(layerName, FabricPointsLayer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLineLayerName(string layerName)
    {
        return string.Equals(layerName, ParcelLinesLayer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(layerName, FabricConnectionLinesLayer, StringComparison.OrdinalIgnoreCase)
            || layerName.EndsWith(FabricParcelTypeSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPolygonLayerName(string layerName)
    {
        return string.Equals(layerName, ParcelPolygonsLayer, StringComparison.OrdinalIgnoreCase)
            || (!IsLineLayerName(layerName)
                && !IsPointLayerName(layerName)
                && layerName.StartsWith("compute_review", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnyField(IReadOnlySet<string> fieldNames, params string[] candidates)
    {
        return candidates.Any(fieldNames.Contains);
    }

    private static string? FirstAvailableField(IReadOnlySet<string> fieldNames, params string[] candidates)
    {
        return candidates.FirstOrDefault(fieldNames.Contains);
    }

    private enum ReviewLayerRole
    {
        Unknown = 0,
        Points,
        Lines,
        Polygons,
    }
}

internal static class EnumerablePipeExtensions
{
    public static TResult Pipe<TValue, TResult>(this TValue value, Func<TValue, TResult> transform)
    {
        return transform(value);
    }
}
