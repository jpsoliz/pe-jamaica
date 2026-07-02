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
    IReadOnlyList<string> LoadedLayerPaths,
    string? GroupLayerName = null)
{
    public static OutputMapIntegrationResult Skipped(string message)
    {
        return new OutputMapIntegrationResult(false, message, Array.Empty<string>(), null);
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
        var zoomLayers = new List<Layer>();
        var stylingWarnings = new List<string>();
        var groupLayerName = OutputMapReviewStyling.BuildTransactionGroupLayerName(summary);
        await QueuedTask.Run(() =>
        {
            var reviewGroup = EnsureTransactionReviewGroup(mapView.Map, groupLayerName);
            var hasSupportingLayers = layerPaths.Any(OutputMapReviewStyling.IsSupportingLayerPath);
            GroupLayer? computedReviewGroup = null;
            GroupLayer? supportingSourcesGroup = null;
            if (hasSupportingLayers)
            {
                try
                {
                    computedReviewGroup = EnsureTransactionReviewSubgroup(reviewGroup, OutputMapReviewStyling.ComputedParcelReviewGroupName);
                }
                catch (Exception ex)
                {
                    stylingWarnings.Add($"Computed parcel review subgroup could not be created; primary layers will load under '{reviewGroup.Name}': {ex.Message}");
                }

                try
                {
                    supportingSourcesGroup = EnsureTransactionReviewSubgroup(reviewGroup, OutputMapReviewStyling.SupportingSourcesGroupName);
                }
                catch (Exception ex)
                {
                    stylingWarnings.Add($"Supporting sources subgroup could not be created; supporting layers will load under '{reviewGroup.Name}' hidden by default: {ex.Message}");
                }
            }

            var styledLayerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layerPath in layerPaths)
            {
                RemoveExistingReviewLayers(mapView.Map, layerPath);
                var parentGroup = hasSupportingLayers
                    ? OutputMapReviewStyling.IsSupportingLayerPath(layerPath)
                        ? supportingSourcesGroup
                        : computedReviewGroup
                    : reviewGroup;
                Layer? created = null;
                try
                {
                    created = LayerFactory.Instance.CreateLayer(new Uri(layerPath), parentGroup ?? reviewGroup);
                }
                catch (Exception ex) when (OutputMapReviewStyling.IsSupportingLayerPath(layerPath))
                {
                    stylingWarnings.Add($"Supporting source layer '{Path.GetFileName(layerPath)}' could not be added to the map and was skipped: {ex.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    stylingWarnings.Add($"Computed parcel layer '{Path.GetFileName(layerPath)}' could not be added to the map and was skipped: {ex.Message}");
                    continue;
                }

                if (created is not null)
                {
                    TryConfigureReviewLayer(created, summary.Payload, stylingWarnings, styledLayerKeys);
                    var hideByDefault = OutputMapReviewStyling.ShouldHideLayerByDefault(layerPath);
                    if (hideByDefault)
                    {
                        created.SetVisibility(false);
                    }
                    if (OutputMapReviewStyling.ShouldIncludeLayerInInitialZoom(layerPath))
                    {
                        zoomLayers.Add(created);
                    }

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
            await mapView.ZoomToAsync(zoomLayers.Count > 0 ? zoomLayers : loadedLayers).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new OutputMapIntegrationResult(
                true,
                BuildResultMessage(summary, stylingWarnings, "Output layers were added to the active map, but zoom could not be completed automatically."),
                layerPaths,
                groupLayerName);
        }

        return new OutputMapIntegrationResult(
            true,
            BuildResultMessage(summary, stylingWarnings, OutputMapReviewStyling.BuildSuccessMessage(summary)),
            layerPaths,
            groupLayerName);
    }

    private static GroupLayer EnsureTransactionReviewGroup(Map map, string groupLayerName)
    {
        var existingGroup = map.Layers.OfType<GroupLayer>()
            .FirstOrDefault(layer => string.Equals(layer.Name, groupLayerName, StringComparison.OrdinalIgnoreCase));
        if (existingGroup is not null)
        {
            return existingGroup;
        }

        return LayerFactory.Instance.CreateGroupLayer(map, 0, groupLayerName);
    }

    private static GroupLayer EnsureTransactionReviewSubgroup(GroupLayer parentGroup, string groupLayerName)
    {
        var existingGroup = parentGroup.Layers.OfType<GroupLayer>()
            .FirstOrDefault(layer => string.Equals(layer.Name, groupLayerName, StringComparison.OrdinalIgnoreCase));
        if (existingGroup is not null)
        {
            return existingGroup;
        }

        return LayerFactory.Instance.CreateGroupLayer(parentGroup, 0, groupLayerName);
    }

    private static void RemoveExistingReviewLayers(Map map, string layerPath)
    {
        var layerUri = new Uri(layerPath).AbsoluteUri;
        foreach (var layer in FlattenLayers(map.Layers).ToArray())
        {
            if (string.Equals(layer.URI, layerUri, StringComparison.OrdinalIgnoreCase)
                || string.Equals(layer.Name, Path.GetFileName(layerPath), StringComparison.OrdinalIgnoreCase))
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

    private static void TryConfigureReviewLayer(Layer layer, OutputSummaryPayload payload, ICollection<string> warnings, ISet<string> styledLayerKeys)
    {
        if (layer is CompositeLayer compositeLayer)
        {
            foreach (var childLayer in compositeLayer.Layers)
            {
                TryConfigureReviewLayer(childLayer, payload, warnings, styledLayerKeys);
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
            OutputMapReviewStyling.ApplyReviewStyling(featureLayer, fieldNames, warnings, payload);
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
        if (Directory.Exists(gdbPath))
        {
            return true;
        }

        return Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}

internal static class OutputMapReviewStyling
{
    private const string ParcelPointsLayer = "parcel_points";
    private const string ParcelLinesLayer = "parcel_lines";
    private const string ParcelPolygonsLayer = "parcel_polygons";
    private const string SurveyPointLayer = "survey_point_layer";
    private const string SurveyCadReferenceLayer = "survey_cad_reference";
    private const string ParcelFabricLayer = "local_parcel_fabric";
    private const string FabricPointsLayer = "Points";
    private const string FabricConnectionLinesLayer = "Connection Lines";
    private const string FabricParcelTypeSuffix = "_Lines";
    public const string ComputedParcelReviewGroupName = "Computed Parcel Review";
    public const string SupportingSourcesGroupName = "Supporting Sources";

    public static IReadOnlyList<string> OrderLayerPaths(IEnumerable<string> layerPaths)
    {
        return layerPaths
            .OrderBy(GetLayerOrder)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string BuildSuccessMessage(OutputSummaryDocument summary)
    {
        var diagnosticSuffix = BuildCogoDiagnosticSuffix(summary.Payload);

        if (string.Equals(summary.Payload.ReviewResultOwner, ReviewResultOwnership.ManualSpatialReview, StringComparison.OrdinalIgnoreCase))
        {
            return $"Manual review workspace layers were added to the active map and zoomed for editing. This standard review surface supports COGO-style labels, snapping, and map-based parcel correction without requiring Parcel Fabric.{diagnosticSuffix}";
        }

        if (string.Equals(summary.Payload.ReviewWorkspaceMode, Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, StringComparison.OrdinalIgnoreCase))
        {
            return $"Working Parcel Fabric review layers were added to the active map and zoomed for review. Use ArcGIS Pro parcel, COGO, snapping, and editing tools to inspect and refine the transaction geometry.{diagnosticSuffix}";
        }

        return string.Equals(summary.Payload.ReviewWorkspaceMode, Innola.InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy, StringComparison.OrdinalIgnoreCase)
            ? string.Equals(summary.Payload.ParcelFabricMode, "true", StringComparison.OrdinalIgnoreCase)
                ? $"Parcel Fabric review layers were added to the active map and zoomed for review. Use ArcGIS Pro parcel, snapping, and editing tools to inspect and refine the transaction geometry.{diagnosticSuffix}"
                : $"Parcel Fabric pilot review layers were added to the active map and zoomed for review. Use ArcGIS Pro parcel, snapping, and editing tools for examination.{diagnosticSuffix}"
            : $"COGO-ready non-fabric review layers were added to the active map and zoomed for review. Use ArcGIS Pro snapping, selection, and standard editing tools to inspect points, lines, and parcel boundaries.{diagnosticSuffix}";
    }

    public static string BuildTransactionGroupLayerName(OutputSummaryDocument summary)
    {
        var transactionNumber = string.IsNullOrWhiteSpace(summary.TransactionId) ? "Unknown" : summary.TransactionId.Trim();
        return $"TR {transactionNumber} - Review";
    }

    public static string GetLayerGroupName(string path)
    {
        return IsSupportingLayerPath(path)
            ? SupportingSourcesGroupName
            : ComputedParcelReviewGroupName;
    }

    public static bool ShouldHideLayerByDefault(string path)
    {
        return IsSupportingLayerPath(path);
    }

    public static bool ShouldIncludeLayerInInitialZoom(string path)
    {
        return !IsSupportingLayerPath(path);
    }

    public static bool IsSupportingLayerPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var layerName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(layerName, SurveyPointLayer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(layerName, SurveyCadReferenceLayer, StringComparison.OrdinalIgnoreCase)
            || layerName.StartsWith(SurveyCadReferenceLayer + "_", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCogoDiagnosticSuffix(OutputSummaryPayload payload)
    {
        var mapMode = string.IsNullOrWhiteSpace(payload.MapLoadMode) ? "unknown" : payload.MapLoadMode;
        var rootDiagnostic = payload.RootLineFeatureClassDiagnostic;
        if (rootDiagnostic is null)
        {
            return $" Diagnostics: map load {mapMode}; bearing text populated {(payload.BearingTxtPopulated ? "yes" : "no")} ({payload.BearingTxtPopulatedCount}); distance text populated {(payload.DistanceTxtPopulated ? "yes" : "no")} ({payload.DistanceTxtPopulatedCount}); computed fallback lines {payload.ComputedCogoFallbackLineCount}.";
        }

        var diagnosticSource = string.Equals(mapMode, "fabric", StringComparison.OrdinalIgnoreCase) ? "root parcel_lines + fabric review" : "root parcel_lines";
        var mismatchWarning = payload.PayloadBearingTxtPopulatedCount > 0 && !payload.BearingTxtPopulated
            || payload.PayloadDistanceTxtPopulatedCount > 0 && !payload.DistanceTxtPopulated
            ? " Payload/feature-class mismatch detected."
            : string.Empty;
        return $" Diagnostics: map load {mapMode}; source {diagnosticSource}; root bearing_txt {(payload.RootLineBearingTxtExists ? "present" : "missing")} ({payload.BearingTxtPopulatedCount}); root distance_txt {(payload.RootLineDistanceTxtExists ? "present" : "missing")} ({payload.DistanceTxtPopulatedCount}); root length_txt {(payload.RootLineLengthTxtExists ? "present" : "missing")} ({payload.RootLineLengthTxtPopulatedCount}); root distance_m {(payload.RootLineDistanceMExists ? "present" : "missing")} ({payload.RootLineDistanceMPopulatedCount}); computed fallback lines {payload.ComputedCogoFallbackLineCount}.{mismatchWarning}";
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

    public static void ApplyReviewStyling(FeatureLayer featureLayer, IReadOnlySet<string> fieldNames, ICollection<string> warnings, OutputSummaryPayload payload)
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
            if (ShouldApplyLabels(payload, role))
            {
                ApplyPointLabels(featureLayer, definition, fieldNames, warnings);
            }
            return;
        }

        if (role == ReviewLayerRole.Lines)
        {
            ApplyLineRenderer(featureLayer, warnings);
            if (ShouldApplyLabels(payload, role))
            {
                ApplyLineLabels(featureLayer, definition, fieldNames, warnings);
            }
            return;
        }

        if (role == ReviewLayerRole.Polygons)
        {
            ApplyPolygonRenderer(featureLayer, warnings);
            if (ShouldApplyLabels(payload, role))
            {
                ApplyPolygonLabels(featureLayer, definition, fieldNames, warnings);
            }
        }
    }

    private static bool ShouldApplyLabels(OutputSummaryPayload payload, ReviewLayerRole role)
    {
        if (!string.Equals(payload.ReviewWorkspaceMode, Innola.InnolaTransactionSettings.ReviewWorkspaceModeNormal, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!payload.AddCogoLabels)
        {
            return false;
        }

        return role switch
        {
            ReviewLayerRole.Points => true,
            ReviewLayerRole.Lines => payload.AddCogoAttributes,
            ReviewLayerRole.Polygons => true,
            _ => false
        };
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
            expression =
                $"When(" +
                $"IsEmpty($feature.{bearingField}) && IsEmpty($feature.{lengthTextField}), '', " +
                $"IsEmpty($feature.{bearingField}), $feature.{lengthTextField}, " +
                $"IsEmpty($feature.{lengthTextField}), $feature.{bearingField}, " +
                $"$feature.{bearingField} + TextFormatting.NewLine + $feature.{lengthTextField})";
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

        ApplySingleLabelClass(featureLayer, definition, "COGO Segment", expression, BuildPoiStyleLineLabelSymbol());
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

    private static void ApplySingleLabelClass(FeatureLayer featureLayer, CIMFeatureLayer definition, string className, string expression, CIMSymbolReference? textSymbol = null)
    {
        var labelClass = definition.LabelClasses?.FirstOrDefault() ?? new CIMLabelClass();
        labelClass.Name = className;
        labelClass.ExpressionEngine = LabelExpressionEngine.Arcade;
        labelClass.Expression = expression;
        labelClass.Visibility = true;
        if (textSymbol is not null)
        {
            labelClass.TextSymbol = textSymbol;
        }
        definition.LabelClasses = new[] { labelClass };
        definition.LabelVisibility = true;
        featureLayer.SetDefinition(definition);
    }

    private static CIMSymbolReference BuildPoiStyleLineLabelSymbol()
    {
        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(
            ColorFactory.Instance.CreateRGBColor(255, 255, 255, 75),
            9.0,
            "Landform",
            "Physical Region");

        textSymbol.HorizontalAlignment = HorizontalAlignment.Center;
        textSymbol.VerticalAlignment = VerticalAlignment.Center;

        return textSymbol.MakeSymbolReference();
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
