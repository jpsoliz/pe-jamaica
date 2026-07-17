using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Enterprise.PortalAuth;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public sealed class ArcGisCompareMapIntegrationService : ICompareMapIntegrationService
{
    private readonly IPortalAuthProvider portalAuthProvider;
    private readonly Func<InnolaTransactionSettings> getSettings;

    public ArcGisCompareMapIntegrationService()
        : this(CompositePortalAuthProvider.CreateDefault(), InnolaTransactionSettings.Load)
    {
    }

    public ArcGisCompareMapIntegrationService(
        IPortalAuthProvider portalAuthProvider,
        Func<InnolaTransactionSettings>? getSettings = null)
    {
        this.portalAuthProvider = portalAuthProvider;
        this.getSettings = getSettings ?? InnolaTransactionSettings.Load;
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
        var mapWarnings = new List<string>();
        var cadasterContextSummaries = new List<CompareCadasterMapContextSummary>();
        var groupLayerName = BuildGroupLayerName(plan);
        var enterpriseCadasterSettings = getSettings().CompareEnterpriseCadaster;
        int? polygonFeatureCount = null;
        try
        {
            await QueuedTask.Run(() =>
            {
                RemoveStaleCompareGroups(mapView.Map, groupLayerName);
                var groupLayer = EnsureGroupLayer(mapView.Map, groupLayerName);
                ClearGroupLayer(mapView.Map, groupLayer);
                var reviewGeometries = new List<Geometry>();
                foreach (var request in OrderLayers(plan.Layers))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RemoveExistingLayer(mapView.Map, request.LayerUrl);
                    var layer = LayerFactory.Instance.CreateLayer(new Uri(request.LayerUrl), groupLayer);
                    if (layer is FeatureLayer featureLayer)
                    {
                        ApplyDefinitionQuery(featureLayer, request.DefinitionQuery);
                        featureLayer.SetEditable(false);
                        ApplyWorkingLayerStyle(featureLayer, request.Role, mapWarnings);
                        ApplyWorkingLayerLabels(featureLayer, request.Role, mapWarnings);
                        if (request.Role == CompareWorkingLayerRole.Polygons)
                        {
                            polygonFeatureCount = CountFeatures(featureLayer, request.DefinitionQuery);
                            reviewGeometries.AddRange(ReadFeatureGeometries(featureLayer, request.DefinitionQuery));
                        }
                    }

                    loadedLayerUrls.Add(request.LayerUrl);
                    if (request.Role == CompareWorkingLayerRole.Polygons)
                    {
                        zoomLayers.Add(layer);
                    }
                }

                cadasterContextSummaries.AddRange(AddEnterpriseCadasterContextLayers(
                    mapView.Map,
                    groupLayer,
                    plan,
                    enterpriseCadasterSettings,
                    reviewGeometries,
                    loadedLayerUrls,
                    mapWarnings,
                    cancellationToken));
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
                BuildLoadedMessage(plan, groupLayerName, polygonFeatureCount, zoomed: false, cadasterContextSummaries, mapWarnings),
                loadedLayerUrls,
                groupLayerName,
                polygonFeatureCount);
        }

        return CompareMapIntegrationResult.Loaded(
            BuildLoadedMessage(plan, groupLayerName, polygonFeatureCount, zoomed: true, cadasterContextSummaries, mapWarnings),
            loadedLayerUrls,
            groupLayerName,
            polygonFeatureCount);
    }

    public static string BuildGroupLayerName(CompareWorkingGeometryLoadPlan plan)
    {
        return $"Compare Review - {plan.ScopeValue}";
    }

    public async Task<CompareMapCleanupResult> RemoveTransactionGeometryFromActiveMapAsync(
        string groupLayerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupLayerName))
        {
            return CompareMapCleanupResult.Skipped("No Compare map group was available for cleanup.");
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return CompareMapCleanupResult.Skipped("No active ArcGIS Pro map was available for Compare cleanup.");
        }

        var removedCount = 0;
        try
        {
            await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var groupLayers = mapView.Map.Layers.OfType<GroupLayer>()
                    .Where(layer => string.Equals(layer.Name, groupLayerName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (groupLayers.Length == 0)
                {
                    return;
                }

                foreach (var groupLayer in groupLayers)
                {
                    removedCount += FlattenLayers(groupLayer.Layers).Count() + 1;
                    mapView.Map.RemoveLayer(groupLayer);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            return CompareMapCleanupResult.Skipped($"Compare map cleanup could not be completed: {exception.Message}");
        }

        return removedCount == 0
            ? CompareMapCleanupResult.Skipped($"No active map group named '{groupLayerName}' was found.")
            : CompareMapCleanupResult.Removed(groupLayerName, removedCount);
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

    private static void RemoveStaleCompareGroups(Map map, string currentGroupLayerName)
    {
        foreach (var groupLayer in map.Layers.OfType<GroupLayer>()
            .Where(layer => layer.Name.StartsWith("Compare Review - ", StringComparison.OrdinalIgnoreCase)
                && !layer.Name.Equals(currentGroupLayerName, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            map.RemoveLayer(groupLayer);
        }
    }

    private static void ClearGroupLayer(Map map, GroupLayer groupLayer)
    {
        foreach (var layer in FlattenLayers(groupLayer.Layers).ToArray())
        {
            map.RemoveLayer(layer);
        }
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

    private static IReadOnlyList<CompareCadasterMapContextSummary> AddEnterpriseCadasterContextLayers(
        Map map,
        GroupLayer groupLayer,
        CompareWorkingGeometryLoadPlan workingPlan,
        CompareEnterpriseCadasterSettings settings,
        IReadOnlyList<Geometry> reviewGeometries,
        List<string> loadedLayerUrls,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var summaries = new List<CompareCadasterMapContextSummary>();
        if (!settings.Enabled)
        {
            return summaries;
        }

        if (reviewGeometries.Count == 0)
        {
            warnings.Add("Legal/Fiscal context layers were skipped because the working_review polygon geometry could not be read.");
            return summaries;
        }

        var queryPlan = CompareEnterpriseCadasterEvidenceService.BuildQueryPlan(
            new SelectedInnolaTransaction(
                workingPlan.TransactionId,
                workingPlan.TransactionId,
                workingPlan.TransactionNumber,
                "Compare Survey Plan",
                "Compare",
                DateTimeOffset.UtcNow),
            workingPlan,
            settings);
        if (!queryPlan.IsValid)
        {
            warnings.Add(queryPlan.InvalidReason ?? "Legal/Fiscal context layer query plan is invalid.");
            return summaries;
        }

        foreach (var request in queryPlan.LayerRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                RemoveExistingLayer(map, request.LayerUrl);
                var layer = LayerFactory.Instance.CreateLayer(new Uri(request.LayerUrl), groupLayer);
                if (layer is not FeatureLayer featureLayer)
                {
                    map.RemoveLayer(layer);
                    warnings.Add($"{request.SourceName} context layer was skipped because it did not load as a feature layer.");
                    continue;
                }

                featureLayer.SetEditable(false);
                var objectIdField = ResolveObjectIdField(featureLayer, request.FieldMap.ObjectIdField);
                var objectIds = QueryContextObjectIds(featureLayer, reviewGeometries, request.ResultLimit);
                if (objectIds.Count == 0)
                {
                    map.RemoveLayer(featureLayer);
                    summaries.Add(new CompareCadasterMapContextSummary(request.SourceName, 0));
                    continue;
                }

                featureLayer.SetName($"{request.SourceName} - adjacent context ({objectIds.Count})");
                featureLayer.SetDefinitionQuery(BuildObjectIdDefinitionQuery(objectIdField, objectIds));
                ApplyContextLayerStyle(featureLayer, request.SourceKind, warnings);
                ApplyContextLayerLabels(featureLayer, request, warnings);
                loadedLayerUrls.Add(request.LayerUrl);
                summaries.Add(new CompareCadasterMapContextSummary(request.SourceName, objectIds.Count));
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException
                or NotSupportedException
                or UriFormatException
                or ArcGIS.Core.CalledOnWrongThreadException)
            {
                warnings.Add($"{request.SourceName} context layer could not be loaded: {exception.Message}");
            }
        }

        return summaries;
    }

    private static IReadOnlyList<Geometry> ReadFeatureGeometries(FeatureLayer featureLayer, string definitionQuery)
    {
        var geometries = new List<Geometry>();
        using var cursor = featureLayer.Search(new QueryFilter
        {
            WhereClause = definitionQuery,
            RowCount = 25
        });

        while (cursor.MoveNext())
        {
            if (cursor.Current is Feature feature)
            {
                var shape = feature.GetShape();
                if (shape is not null)
                {
                    geometries.Add(shape);
                }
            }
        }

        return geometries;
    }

    private static IReadOnlyList<long> QueryContextObjectIds(
        FeatureLayer featureLayer,
        IReadOnlyList<Geometry> reviewGeometries,
        int resultLimit)
    {
        var objectIds = new SortedSet<long>();
        var limit = Math.Max(1, resultLimit);
        foreach (var geometry in reviewGeometries)
        {
            if (objectIds.Count >= limit)
            {
                break;
            }

            using var cursor = featureLayer.Search(new SpatialQueryFilter
            {
                FilterGeometry = geometry,
                SpatialRelationship = SpatialRelationship.Intersects,
                WhereClause = "1=1",
                RowCount = limit - objectIds.Count
            });

            while (cursor.MoveNext())
            {
                objectIds.Add(cursor.Current.GetObjectID());
                if (objectIds.Count >= limit)
                {
                    break;
                }
            }
        }

        return objectIds.ToArray();
    }

    internal static string BuildObjectIdDefinitionQuery(string objectIdField, IReadOnlyCollection<long> objectIds)
    {
        if (string.IsNullOrWhiteSpace(objectIdField) || !IsSafeFieldName(objectIdField))
        {
            throw new ArgumentException("ObjectID field is not safe for a definition query.", nameof(objectIdField));
        }

        if (objectIds.Count == 0)
        {
            return "1 = 0";
        }

        return $"{objectIdField.Trim()} IN ({string.Join(",", objectIds.OrderBy(id => id))})";
    }

    private static string ResolveObjectIdField(FeatureLayer featureLayer, string? configuredObjectIdField)
    {
        try
        {
            using var table = featureLayer.GetTable();
            var objectIdField = table?.GetDefinition()?.GetObjectIDField();
            if (!string.IsNullOrWhiteSpace(objectIdField))
            {
                return objectIdField;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
        }

        return string.IsNullOrWhiteSpace(configuredObjectIdField)
            ? "OBJECTID"
            : configuredObjectIdField.Trim();
    }

    private static void ApplyContextLayerStyle(FeatureLayer featureLayer, string sourceKind, ICollection<string> warnings)
    {
        try
        {
            featureLayer.SetTransparency(35);
            var isLegal = sourceKind.Equals(CompareEnterpriseCadasterSourceKind.Legal, StringComparison.OrdinalIgnoreCase);
            var fill = isLegal
                ? ColorFactory.Instance.CreateRGBColor(37, 99, 235, 22)
                : ColorFactory.Instance.CreateRGBColor(217, 119, 6, 20);
            var outline = SymbolFactory.Instance.ConstructStroke(
                isLegal
                    ? ColorFactory.Instance.CreateRGBColor(29, 78, 216)
                    : ColorFactory.Instance.CreateRGBColor(180, 83, 9),
                isLegal ? 1.6 : 1.25,
                isLegal ? SimpleLineStyle.Solid : SimpleLineStyle.Dash);
            var symbol = SymbolFactory.Instance.ConstructPolygonSymbol(fill, SimpleFillStyle.Solid, outline);
            featureLayer.SetRenderer(new CIMSimpleRenderer
            {
                Symbol = symbol.MakeSymbolReference()
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            warnings.Add($"{featureLayer.Name} context styling was skipped: {exception.Message}");
        }
    }

    private static void ApplyWorkingLayerStyle(
        FeatureLayer featureLayer,
        CompareWorkingLayerRole role,
        ICollection<string> warnings)
    {
        if (role != CompareWorkingLayerRole.Points)
        {
            return;
        }

        try
        {
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                ColorFactory.Instance.CreateRGBColor(255, 255, 255, 100),
                5.0,
                SimpleMarkerStyle.Circle);
            pointSymbol.SetSize(5.0);
            featureLayer.SetRenderer(new CIMSimpleRenderer
            {
                Symbol = pointSymbol.MakeSymbolReference()
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            warnings.Add($"{featureLayer.Name} point styling was skipped: {exception.Message}");
        }
    }

    private static void ApplyWorkingLayerLabels(
        FeatureLayer featureLayer,
        CompareWorkingLayerRole role,
        ICollection<string> warnings)
    {
        try
        {
            var definition = featureLayer.GetDefinition() as CIMFeatureLayer;
            if (definition is null)
            {
                return;
            }

            var fieldNames = ReadFieldNames(featureLayer);
            var label = role switch
            {
                CompareWorkingLayerRole.Points => BuildPointLabelExpression(fieldNames),
                CompareWorkingLayerRole.Lines => BuildLineLabelExpression(fieldNames),
                CompareWorkingLayerRole.Polygons => BuildPolygonLabelExpression(fieldNames),
                _ => null
            };
            if (label is null)
            {
                return;
            }

            ApplySingleLabelClass(
                featureLayer,
                definition,
                label.Value.ClassName,
                label.Value.Expression,
                label.Value.Symbol ?? BuildDarkTextSymbol(label.Value.FontSize));
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            warnings.Add($"{featureLayer.Name} labels were skipped: {exception.Message}");
        }
    }

    private static void ApplyContextLayerLabels(
        FeatureLayer featureLayer,
        CompareEnterpriseCadasterLayerRequest request,
        ICollection<string> warnings)
    {
        try
        {
            var definition = featureLayer.GetDefinition() as CIMFeatureLayer;
            if (definition is null)
            {
                return;
            }

            var fieldNames = ReadFieldNames(featureLayer);
            var expression = BuildContextLayerLabelExpression(fieldNames, request);
            if (string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            ApplySingleLabelClass(
                featureLayer,
                definition,
                request.SourceKind.Equals(CompareEnterpriseCadasterSourceKind.Legal, StringComparison.OrdinalIgnoreCase)
                    ? "Legal context"
                    : "Fiscal context",
                expression,
                BuildDarkTextSymbol(9.0));
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            warnings.Add($"{featureLayer.Name} labels were skipped: {exception.Message}");
        }
    }

    private static CompareMapLabel? BuildPointLabelExpression(IReadOnlySet<string> fieldNames)
    {
        var pointIdField = FirstAvailableField(fieldNames, "point_id");
        if (pointIdField is null)
        {
            return null;
        }

        var expression = string.Join(
            Environment.NewLine,
            "var pt = Geometry($feature);",
            string.Empty,
            $"return Text(pt.y, \"####.000\") +",
            "TextFormatting.NewLine +",
            "Text(pt.x, \"####.000\") +",
            "TextFormatting.NewLine +",
            FieldLookup(pointIdField) + ";");

        return new CompareMapLabel("Point", expression, 8.5, BuildDarkTextSymbol(8.5, "Arial"));
    }

    private static CompareMapLabel? BuildLineLabelExpression(IReadOnlySet<string> fieldNames)
    {
        var distanceField = FirstAvailableField(fieldNames, "distance_txt");
        return distanceField is null
            ? null
            : new CompareMapLabel("Boundary", "return " + FieldLookup(distanceField) + ";", 8.5, BuildDarkTextSymbol(8.5, "Arial"));
    }

    private static CompareMapLabel BuildPolygonLabelExpression(IReadOnlySet<string> fieldNames)
    {
        var parcelNameField = FirstAvailableField(fieldNames, "parcel_name");
        var parcelNameExpression = parcelNameField is null
            ? "\"\""
            : $"DefaultValue({FieldLookup(parcelNameField)}, \"\")";

        var expression = string.Join(
            Environment.NewLine,
            $"var parcelName = {parcelNameExpression};",
            "var areaSqM = AreaGeodetic(Geometry($feature), \"square-meters\");",
            "var areaAcres = AreaGeodetic(Geometry($feature), \"acres\");",
            string.Empty,
            "return parcelName +",
            "    TextFormatting.NewLine +",
            "    Text(areaSqM, \"#,###.00\") + \" m²\" +",
            "    TextFormatting.NewLine +",
            "    Text(areaAcres, \"#,###.000\") + \" acres\";");

        return new CompareMapLabel("Parcel", expression, 9.0, BuildDarkTextSymbol(9.0));
    }

    private static string? BuildContextLayerLabelExpression(
        IReadOnlySet<string> fieldNames,
        CompareEnterpriseCadasterLayerRequest request)
    {
        return request.SourceKind.Equals(CompareEnterpriseCadasterSourceKind.Legal, StringComparison.OrdinalIgnoreCase)
            ? BuildLegalContextLabelExpression(fieldNames)
            : BuildFiscalContextLabelExpression(fieldNames);
    }

    private static string? BuildFiscalContextLabelExpression(IReadOnlySet<string> fieldNames)
    {
        var lvNumber = FirstAvailableField(fieldNames, "lv_number", "Lv_number");
        var pid = FirstAvailableField(fieldNames, "PID", "pid");
        var volume = FirstAvailableField(fieldNames, "LT_Volume", "lt_volume");
        var folio = FirstAvailableField(fieldNames, "LT_Folio", "lt_folio");
        var lotNumber = FirstAvailableField(fieldNames, "lot_number", "Lot_Number");
        if (lvNumber is null && pid is null && volume is null && folio is null && lotNumber is null)
        {
            return null;
        }

        var expression = new List<string> { "var lines = [];" };
        AddOptionalLabelLine(expression, "lv", "LV#:", lvNumber);
        AddOptionalLabelLine(expression, "pid", "PID:", pid);
        AddOptionalCombinedLabelLine(expression, "volfol", "Vol/Fol:", volume, folio);
        AddOptionalLabelLine(expression, "lot", "Lot #:", lotNumber);
        expression.Add("return Concatenate(lines, TextFormatting.NewLine);");
        return string.Join(Environment.NewLine, expression);
    }

    private static string? BuildLegalContextLabelExpression(IReadOnlySet<string> fieldNames)
    {
        var lotNumber = FirstAvailableField(fieldNames, "Lot_number", "lot_number", "lot_Number");
        var volFol = FirstAvailableField(fieldNames, "Vol_fol", "vol_fol", "VOL_FOL");
        var dpNumber = FirstAvailableField(fieldNames, "DP_number", "dp_number", "DP_Number");
        var peNumber = FirstAvailableField(fieldNames, "PE_number", "pe_number", "PE_Number");
        if (lotNumber is null && volFol is null && dpNumber is null && peNumber is null)
        {
            return null;
        }

        var expression = new List<string> { "var lines = [];" };
        AddOptionalLabelLine(expression, "lot", "Lot #:", lotNumber);
        AddOptionalLabelLine(expression, "volfol", "Vol/Fol:", volFol);
        AddOptionalLabelLine(expression, "dp", "DP#:", dpNumber);
        AddOptionalLabelLine(expression, "pe", "PE#:", peNumber);
        expression.Add("return Concatenate(lines, TextFormatting.NewLine);");
        return string.Join(Environment.NewLine, expression);
    }

    private static IReadOnlySet<string> ReadFieldNames(FeatureLayer featureLayer)
    {
        try
        {
            using var table = featureLayer.GetTable();
            var fields = table?.GetDefinition()?.GetFields();
            return fields is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : fields.Select(field => field.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? FirstAvailableField(IReadOnlySet<string> fieldNames, params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var match = fieldNames.FirstOrDefault(field => string.Equals(field, candidate.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null && IsSafeFieldName(match))
            {
                return match;
            }
        }

        return null;
    }

    private static string FieldText(string fieldName)
    {
        return $"Text($feature.{fieldName})";
    }

    private static string FieldTextOrEmpty(string? fieldName)
    {
        return fieldName is null ? "\"\"" : $"Text(DefaultValue({FieldLookup(fieldName)}, \"\"))";
    }

    private static void AddOptionalLabelLine(
        ICollection<string> expression,
        string variableName,
        string label,
        string? fieldName)
    {
        if (fieldName is null)
        {
            return;
        }

        expression.Add($"var {variableName} = Trim(Text(DefaultValue({FieldLookup(fieldName)}, \"\")));");
        expression.Add($"if (!IsEmpty({variableName})) {{ Push(lines, \"{label} \" + {variableName}); }}");
    }

    private static void AddOptionalCombinedLabelLine(
        ICollection<string> expression,
        string variableName,
        string label,
        string? firstFieldName,
        string? secondFieldName)
    {
        if (firstFieldName is null && secondFieldName is null)
        {
            return;
        }

        expression.Add($"var {variableName}First = {FieldTextOrEmpty(firstFieldName)};");
        expression.Add($"var {variableName}Second = {FieldTextOrEmpty(secondFieldName)};");
        expression.Add($"{variableName}First = Trim({variableName}First);");
        expression.Add($"{variableName}Second = Trim({variableName}Second);");
        expression.Add($"if (!IsEmpty({variableName}First) || !IsEmpty({variableName}Second)) {{ Push(lines, \"{label} \" + {variableName}First + \"/\" + {variableName}Second); }}");
    }

    private static string FieldLookup(string fieldName)
    {
        return $"$feature[\"{fieldName}\"]";
    }

    private static void ApplySingleLabelClass(
        FeatureLayer featureLayer,
        CIMFeatureLayer definition,
        string className,
        string expression,
        CIMSymbolReference? textSymbol = null)
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

    private static CIMSymbolReference BuildDarkTextSymbol(double size, string fontFamily = "Tahoma", bool withWhiteHalo = false)
    {
        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(
            ColorFactory.Instance.CreateRGBColor(17, 24, 39, 100),
            size,
            fontFamily,
            "Regular");

        textSymbol.HorizontalAlignment = HorizontalAlignment.Center;
        textSymbol.VerticalAlignment = VerticalAlignment.Center;
        if (withWhiteHalo)
        {
            textSymbol.HaloSize = 1.25;
            textSymbol.HaloSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                ColorFactory.Instance.CreateRGBColor(255, 255, 255, 100));
        }

        return textSymbol.MakeSymbolReference();
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
        bool zoomed,
        IReadOnlyList<CompareCadasterMapContextSummary>? cadasterContextSummaries = null,
        IReadOnlyList<string>? warnings = null)
    {
        var polygonText = polygonFeatureCount.HasValue
            ? $"{polygonFeatureCount.Value} polygon feature(s)"
            : "polygon features";
        var zoomText = zoomed
            ? "Map zoomed to the transaction layer."
            : "Layers were added, but the map could not zoom automatically.";
        var contextText = BuildCadasterContextMessage(cadasterContextSummaries);
        var warningText = warnings is { Count: > 0 }
            ? $" Context warnings: {string.Join(" ", warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))}"
            : string.Empty;

        return $"Compare working layers loaded into ArcGIS Pro map group '{groupLayerName}' for {plan.ScopeField} '{plan.ScopeValue}' ({polygonText}). {contextText} {zoomText}{warningText}";
    }

    private static string BuildCadasterContextMessage(IReadOnlyList<CompareCadasterMapContextSummary>? summaries)
    {
        if (summaries is null || summaries.Count == 0)
        {
            return "Legal/Fiscal context layers were not configured for map loading.";
        }

        var loaded = summaries.Where(summary => summary.FeatureCount > 0).ToArray();
        if (loaded.Length == 0)
        {
            return "No adjacent Legal/Fiscal context features were found.";
        }

        return "Context layers: " + string.Join(
            "; ",
            summaries.Select(summary => $"{summary.SourceName} {summary.FeatureCount} feature(s)")) + ".";
    }

    private static bool IsSafeFieldName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || (!char.IsLetter(trimmed[0]) && trimmed[0] != '_'))
        {
            return false;
        }

        return trimmed.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private sealed record CompareCadasterMapContextSummary(string SourceName, int FeatureCount);

    private readonly record struct CompareMapLabel(
        string ClassName,
        string Expression,
        double FontSize,
        CIMSymbolReference? Symbol);
}
