using ArcGIS.Core.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

public interface ISpatialOverlapReviewService
{
    Task<SpatialOverlapReviewExecutionResult> RunAsync(
        SpatialOverlapReviewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record SpatialOverlapReviewExecutionResult(
    bool Success,
    string Message,
    SpatialOverlapReviewDocument? Document = null)
{
    public static SpatialOverlapReviewExecutionResult Blocked(string message)
    {
        return new SpatialOverlapReviewExecutionResult(false, message, null);
    }
}

public sealed record SpatialOverlapReviewRequest(
    string Scope,
    string TransactionId,
    string TransactionNumber,
    string? ReviewGroupLayerName,
    IReadOnlyList<string> ReviewLayerNameCandidates,
    IReadOnlyList<SpatialOverlapReviewTargetLayer> TargetLayers,
    double RelationshipToleranceMeters);

public sealed record SpatialOverlapReviewTargetLayer(
    string LayerRole,
    string SourceName,
    string? DisplayName,
    string? SublayerName,
    string? LayerUrl,
    CompareEnterpriseCadasterSourceSettings FieldMap);

public sealed class ArcGisSpatialOverlapReviewService : ISpatialOverlapReviewService
{
    public async Task<SpatialOverlapReviewExecutionResult> RunAsync(
        SpatialOverlapReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetLayers.Count == 0)
        {
            return SpatialOverlapReviewExecutionResult.Blocked("No overlap-review target layers are enabled in settings.");
        }

        var mapView = MapView.Active;
        if (mapView?.Map is null)
        {
            return SpatialOverlapReviewExecutionResult.Blocked("Run Overlap Review requires an active ArcGIS Pro map.");
        }

        var snapshot = await QueuedTask.Run(
            () => BuildSnapshot(mapView.Map, request, cancellationToken)).ConfigureAwait(true);

        if (!snapshot.Success)
        {
            return SpatialOverlapReviewExecutionResult.Blocked(snapshot.Message);
        }

        var layerResults = snapshot.LayerResults.ToArray();
        var records = snapshot.Records.ToArray();
        var overlapLayerCount = layerResults.Count(result =>
            string.Equals(result.ResultType, "overlap", StringComparison.OrdinalIgnoreCase));
        var noOverlapLayerCount = layerResults.Count(result =>
            string.Equals(result.ResultType, "no_overlap", StringComparison.OrdinalIgnoreCase));
        var missingDependencyCount = layerResults.Count(result => result.MissingDependency);
        var summaryMessage = records.Length == 0
            ? $"Overlap Review checked {layerResults.Length} configured layer(s) and found no overlaps."
            : $"Overlap Review found {records.Length} overlap record(s) across {overlapLayerCount} configured layer(s).";

        var document = new SpatialOverlapReviewDocument(
            "spatial-overlap-review/v1",
            request.Scope,
            request.TransactionId,
            request.TransactionNumber,
            DateTimeOffset.UtcNow.ToString("O"),
            request.ReviewGroupLayerName,
            snapshot.ReviewLayerName,
            snapshot.ReviewAreaSquareMeters,
            new SpatialOverlapReviewSummary(
                SpatialOverlapReviewStatuses.Ready,
                summaryMessage,
                request.TargetLayers.Count,
                layerResults.Length,
                missingDependencyCount,
                records.Length,
                noOverlapLayerCount),
            layerResults,
            records,
            snapshot.Warnings.ToArray(),
            Array.Empty<string>(),
            Array.Empty<SpatialOverlapReviewSnapshotRef>());

        return new SpatialOverlapReviewExecutionResult(true, summaryMessage, document);
    }

    private static SpatialOverlapReviewSnapshot BuildSnapshot(
        Map map,
        SpatialOverlapReviewRequest request,
        CancellationToken cancellationToken)
    {
        var reviewLayer = ResolveReviewLayer(map, request);
        if (reviewLayer is null)
        {
            var scopeLabel = string.Equals(request.Scope, SpatialOverlapReviewScopes.Compare, StringComparison.OrdinalIgnoreCase)
                ? "Compare"
                : "Compute";
            return SpatialOverlapReviewSnapshot.Blocked(
                $"{scopeLabel} review geometry is not loaded in the active map. Load the review layers first.");
        }

        var reviewGeometries = ReadFeatureGeometries(reviewLayer);
        if (reviewGeometries.Count == 0)
        {
            return SpatialOverlapReviewSnapshot.Blocked(
                $"Review layer '{reviewLayer.Name}' is present, but no polygon geometry could be read from it.");
        }

        Geometry? reviewGeometry = reviewGeometries.Count == 1
            ? reviewGeometries[0]
            : GeometryEngine.Instance.Union(reviewGeometries);
        if (reviewGeometry is null || reviewGeometry.IsEmpty)
        {
            return SpatialOverlapReviewSnapshot.Blocked(
                $"Review layer '{reviewLayer.Name}' did not produce a usable overlap geometry.");
        }

        var reviewArea = Math.Max(0d, GeometryEngine.Instance.Area(reviewGeometry));
        var layerResults = new List<SpatialOverlapReviewLayerResult>();
        var records = new List<SpatialOverlapReviewRecord>();
        var warnings = new List<string>();

        foreach (var target in request.TargetLayers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetLayer = ResolveTargetLayer(map, target);
            if (targetLayer is null)
            {
                layerResults.Add(new SpatialOverlapReviewLayerResult(
                    target.LayerRole,
                    target.DisplayName ?? target.SourceName,
                    target.SourceName,
                    true,
                    "missing_dependency",
                    $"Configured layer '{target.DisplayName ?? target.SourceName}' is not currently loaded in the active map.",
                    0,
                    true));
                continue;
            }

            IReadOnlyList<SpatialOverlapReviewRecord> overlapRecords;
            try
            {
                overlapRecords = ReadOverlapRecords(targetLayer, target, reviewGeometry, reviewArea, request.RelationshipToleranceMeters, warnings);
            }
            catch (InvalidOperationException exception)
            {
                var message = $"Layer '{targetLayer.Name}' could not be compared to the review geometry: {exception.Message}";
                warnings.Add(message);
                layerResults.Add(new SpatialOverlapReviewLayerResult(
                    target.LayerRole,
                    targetLayer.Name,
                    target.SourceName,
                    true,
                    "incompatible_spatial_reference",
                    message,
                    0,
                    false,
                    targetLayer.Name));
                continue;
            }

            if (overlapRecords.Count == 0)
            {
                layerResults.Add(new SpatialOverlapReviewLayerResult(
                    target.LayerRole,
                    targetLayer.Name,
                    target.SourceName,
                    true,
                    "no_overlap",
                    $"No overlaps were found in '{targetLayer.Name}'.",
                    0,
                    false,
                    targetLayer.Name));
                continue;
            }

            records.AddRange(overlapRecords);
            layerResults.Add(new SpatialOverlapReviewLayerResult(
                target.LayerRole,
                targetLayer.Name,
                target.SourceName,
                true,
                "overlap",
                $"{overlapRecords.Count} overlap record(s) were found in '{targetLayer.Name}'.",
                overlapRecords.Count,
                false,
                targetLayer.Name));
        }

        if (layerResults.Count == 0)
        {
            return SpatialOverlapReviewSnapshot.Blocked("No overlap-review target layers could be evaluated.");
        }

        return new SpatialOverlapReviewSnapshot(
            true,
            string.Empty,
            reviewLayer.Name,
            reviewArea,
            layerResults,
            records,
            warnings);
    }

    private static FeatureLayer? ResolveReviewLayer(Map map, SpatialOverlapReviewRequest request)
    {
        IEnumerable<Layer> candidates;
        if (!string.IsNullOrWhiteSpace(request.ReviewGroupLayerName))
        {
            var group = map.Layers
                .OfType<GroupLayer>()
                .FirstOrDefault(layer => string.Equals(layer.Name, request.ReviewGroupLayerName, StringComparison.OrdinalIgnoreCase));
            candidates = group is null ? FlattenLayers(map.Layers) : FlattenLayers(group.Layers);
        }
        else
        {
            candidates = FlattenLayers(map.Layers);
        }

        return candidates
            .OfType<FeatureLayer>()
            .FirstOrDefault(layer => request.ReviewLayerNameCandidates.Any(candidate =>
                layer.Name.Contains(candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private static FeatureLayer? ResolveTargetLayer(Map map, SpatialOverlapReviewTargetLayer target)
    {
        var layerUrl = NormalizeFeatureServiceUrl(target.LayerUrl);
        return FlattenLayers(map.Layers)
            .OfType<FeatureLayer>()
            .FirstOrDefault(layer =>
                Matches(layer.Name, target.DisplayName)
                || Matches(layer.Name, target.SublayerName)
                || Matches(layer.Name, target.SourceName)
                || (!string.IsNullOrWhiteSpace(layerUrl) && string.Equals(NormalizeFeatureServiceUrl(layer.URI), layerUrl, StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<SpatialOverlapReviewRecord> ReadOverlapRecords(
        FeatureLayer featureLayer,
        SpatialOverlapReviewTargetLayer target,
        Geometry reviewGeometry,
        double reviewArea,
        double tolerance,
        List<string> warnings)
    {
        var records = new List<SpatialOverlapReviewRecord>();
        using var cursor = featureLayer.Search(new SpatialQueryFilter
        {
            FilterGeometry = reviewGeometry,
            SpatialRelationship = SpatialRelationship.Intersects
        });

        var objectIdField = ResolveFieldName(featureLayer, target.FieldMap.ObjectIdField) ?? featureLayer.GetTable().GetDefinition().GetObjectIDField();
        while (cursor.MoveNext())
        {
            if (cursor.Current is not Feature feature)
            {
                continue;
            }

            var shape = feature.GetShape();
            if (shape is null || shape.IsEmpty)
            {
                continue;
            }

            shape = NormalizeForReview(shape, reviewGeometry.SpatialReference, featureLayer.Name);

            var intersection = GeometryEngine.Instance.Intersection(shape, reviewGeometry);
            if (intersection is null || intersection.IsEmpty)
            {
                continue;
            }

            var overlapArea = Math.Max(0d, GeometryEngine.Instance.Area(intersection));
            var overlapPercentage = reviewArea <= 0d
                ? 0d
                : Math.Round((overlapArea / reviewArea) * 100d, 3, MidpointRounding.AwayFromZero);
            var contains = GeometryEngine.Instance.Contains(reviewGeometry, shape);
            var within = GeometryEngine.Instance.Within(reviewGeometry, shape);
            var relationship = CompareEnterpriseCadasterEvidenceClassifier.ClassifyFromMetrics(
                sameReviewMatch: false,
                contains,
                within,
                overlapArea,
                sharedBoundaryLength: 0d,
                intersects: true,
                tolerance);

            string? parcelId = ReadFieldValue(feature, featureLayer, target.FieldMap.ParcelIdField);
            string? pid = ReadFieldValue(feature, featureLayer, target.FieldMap.PidField);
            string? volume = ReadFieldValue(feature, featureLayer, target.FieldMap.VolumeField);
            string? folio = ReadFieldValue(feature, featureLayer, target.FieldMap.FolioField);
            string? landVal = ReadFieldValue(feature, featureLayer, target.FieldMap.LandValuationNumberField);
            string? pe = ReadFieldValue(feature, featureLayer, target.FieldMap.PeNumberField);
            string? dp = ReadFieldValue(feature, featureLayer, target.FieldMap.DpNumberField);
            string? rNumber = ReadFieldValue(feature, featureLayer, target.FieldMap.RNumberField);
            string? objectId = ReadFieldValue(feature, featureLayer, objectIdField);
            var featureIdentity = FirstNonBlank(parcelId, pid, landVal, pe, dp, rNumber, volume, objectId)
                ?? $"{target.SourceName} feature";
            var overlapGroupId = BuildOverlapGroupId(target.LayerRole, featureIdentity, objectId);
            var overlapId = $"{overlapGroupId}:{records.Count + 1}";

            records.Add(new SpatialOverlapReviewRecord(
                target.LayerRole,
                featureLayer.Name,
                target.SourceName,
                relationship,
                featureIdentity,
                objectId,
                parcelId,
                pid,
                volume,
                folio,
                landVal,
                pe,
                dp,
                rNumber,
                Math.Round(overlapArea, 3, MidpointRounding.AwayFromZero),
                overlapPercentage,
                overlapGroupId,
                overlapId,
                "not_requested"));
        }

        if (records.Count == 0 && featureLayer.ShapeType is esriGeometryType.esriGeometryPolyline or esriGeometryType.esriGeometryPoint)
        {
            warnings.Add($"Layer '{featureLayer.Name}' intersected the review geometry, but it does not produce polygon overlap area.");
        }

        return records;
    }

    private static Geometry NormalizeForReview(Geometry shape, SpatialReference? reviewSpatialReference, string layerName)
    {
        if (reviewSpatialReference is null || shape.SpatialReference is null)
        {
            return shape;
        }

        if (SpatialReferencesMatch(shape.SpatialReference, reviewSpatialReference))
        {
            return shape;
        }

        try
        {
            var projected = GeometryEngine.Instance.Project(shape, reviewSpatialReference);
            if (projected is null || projected.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Layer spatial reference could not be projected into review spatial reference for '{layerName}'.");
            }

            return projected;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Layer spatial reference could not be projected into review spatial reference for '{layerName}'.",
                exception);
        }
    }

    private static bool SpatialReferencesMatch(SpatialReference left, SpatialReference right)
    {
        if (left.Wkid > 0 && right.Wkid > 0)
        {
            return left.Wkid == right.Wkid
                || (left.LatestWkid > 0 && left.LatestWkid == right.Wkid)
                || (right.LatestWkid > 0 && right.LatestWkid == left.Wkid)
                || (left.LatestWkid > 0 && right.LatestWkid > 0 && left.LatestWkid == right.LatestWkid);
        }

        return string.Equals(left.Wkt, right.Wkt, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Geometry> ReadFeatureGeometries(FeatureLayer featureLayer)
    {
        var geometries = new List<Geometry>();
        using var cursor = featureLayer.Search();
        while (cursor.MoveNext())
        {
            if (cursor.Current is Feature feature)
            {
                var shape = feature.GetShape();
                if (shape is not null && !shape.IsEmpty)
                {
                    geometries.Add(shape);
                }
            }
        }

        return geometries;
    }

    private static string? ResolveFieldName(FeatureLayer featureLayer, string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            return null;
        }

        return featureLayer.GetTable().GetDefinition().GetFields()
            .FirstOrDefault(field => string.Equals(field.Name, preferredName, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private static string? ReadFieldValue(Feature feature, FeatureLayer featureLayer, string? fieldName)
    {
        var resolved = ResolveFieldName(featureLayer, fieldName);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return null;
        }

        try
        {
            var value = feature[resolved!];
            return value?.ToString()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Layer> FlattenLayers(IEnumerable<Layer> layers)
    {
        foreach (var layer in layers)
        {
            yield return layer;
            if (layer is CompositeLayer composite)
            {
                foreach (var child in FlattenLayers(composite.Layers))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool Matches(string? actual, string? expected)
    {
        return !string.IsNullOrWhiteSpace(actual)
            && !string.IsNullOrWhiteSpace(expected)
            && actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeFeatureServiceUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().TrimEnd('/');
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string BuildOverlapGroupId(string layerRole, string featureIdentity, string? objectId)
    {
        var identity = string.IsNullOrWhiteSpace(objectId) ? featureIdentity : objectId;
        return $"{SanitizeIdPart(layerRole)}::{SanitizeIdPart(identity)}";
    }

    private static string SanitizeIdPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
                continue;
            }

            if (length > 0 && buffer[length - 1] != '-')
            {
                buffer[length++] = '-';
            }
        }

        while (length > 0 && buffer[length - 1] == '-')
        {
            length--;
        }

        return length == 0 ? "unknown" : new string(buffer[..length]);
    }

    private sealed record SpatialOverlapReviewSnapshot(
        bool Success,
        string Message,
        string? ReviewLayerName,
        double ReviewAreaSquareMeters,
        IReadOnlyList<SpatialOverlapReviewLayerResult> LayerResults,
        IReadOnlyList<SpatialOverlapReviewRecord> Records,
        IReadOnlyList<string> Warnings)
    {
        public static SpatialOverlapReviewSnapshot Blocked(string message)
        {
            return new SpatialOverlapReviewSnapshot(false, message, null, 0d, Array.Empty<SpatialOverlapReviewLayerResult>(), Array.Empty<SpatialOverlapReviewRecord>(), Array.Empty<string>());
        }
    }
}
