using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class SurveyPlanBoundarySolver
{
    private const double CoordinateConflictToleranceM = 0.25d;
    private const double ClosureWarningToleranceM = 1.0d;

    public SurveyPlanBoundarySolverResult Apply(
        ExtractionReviewDocument document,
        double? documentAreaSqM,
        bool useDerivedCoordinatesAsAnchors = false,
        bool repairPrematureClosingLabels = false,
        bool replaceConflictingCoordinatesFromReviewedSegments = false,
        bool mergeGeneratedBoundaryPointsWithReferenceRows = true,
        bool removeInactiveManualRows = false)
    {
        var result = Solve(
            document,
            documentAreaSqM,
            useDerivedCoordinatesAsAnchors,
            repairPrematureClosingLabels,
            replaceConflictingCoordinatesFromReviewedSegments);
        if (replaceConflictingCoordinatesFromReviewedSegments && mergeGeneratedBoundaryPointsWithReferenceRows)
        {
            result = MergeGeneratedBoundaryPointsWithReferenceRows(document, result);
        }

        ApplyDerivedRows(document, result, replaceConflictingCoordinatesFromReviewedSegments, removeInactiveManualRows);
        document.RootMetadata["boundary_solver"] = JsonSerializer.SerializeToNode(new
        {
            status = result.Status,
            geometry_source = "reviewed_boundary_segments",
            derived_point_count = result.DerivedPointCount,
            closure_distance_m = result.ClosureDistanceM,
            computed_area_sq_m = result.ComputedAreaSqM,
            document_area_sq_m = result.DocumentAreaSqM,
            area_delta_sq_m = result.AreaDeltaSqM,
            area_delta_percent = result.AreaDeltaPercent,
            findings = result.Findings
        }) as JsonObject;
        return result;
    }

    private static SurveyPlanBoundarySolverResult MergeGeneratedBoundaryPointsWithReferenceRows(
        ExtractionReviewDocument document,
        SurveyPlanBoundarySolverResult result)
    {
        if (result.DerivedPoints.Count == 0)
        {
            return result;
        }

        var activePointIds = new HashSet<string>(BuildReviewedBoundaryPointOrder(document), StringComparer.OrdinalIgnoreCase);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var findings = result.Findings.ToList();

        foreach (var derivedPoint in result.DerivedPoints)
        {
            if (replacements.ContainsKey(derivedPoint.PointId))
            {
                continue;
            }

            var matchingReference = document.Rows
                .Select(row => new
                {
                    Row = row,
                    PointId = NormalizePointId(row.PointIdentifier)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.PointId)
                               && !activePointIds.Contains(item.PointId)
                               && !item.Row.IsManual
                               && !IsDerivedFromReviewedSegments(item.Row)
                               && TryParseCoordinate(item.Row.Easting, out _)
                               && TryParseCoordinate(item.Row.Northing, out _))
                .FirstOrDefault(item =>
                {
                    TryParseCoordinate(item.Row.Easting, out var easting);
                    TryParseCoordinate(item.Row.Northing, out var northing);
                    return Distance(
                        new SolverPoint(item.PointId, easting, northing, item.Row.ExtractionStatus, item.Row.SourceEvidence),
                        derivedPoint) <= CoordinateConflictToleranceM;
                });

            if (matchingReference is null)
            {
                continue;
            }

            replacements[derivedPoint.PointId] = matchingReference.PointId;
            activePointIds.Remove(derivedPoint.PointId);
            activePointIds.Add(matchingReference.PointId);
            findings.Add($"Rebuild matched generated point {derivedPoint.PointId} to extracted reference point {matchingReference.PointId}; the reference point was kept in the boundary sequence.");
        }

        if (replacements.Count == 0)
        {
            return result;
        }

        foreach (var segment in document.Segments.Where(segment => segment.EffectiveIncludeInBoundary))
        {
            var fromPoint = NormalizePointId(segment.EffectiveFromPoint);
            if (replacements.TryGetValue(fromPoint, out var replacementFromPoint))
            {
                ApplyReviewedFromPoint(segment, replacementFromPoint);
            }

            var toPoint = NormalizePointId(segment.EffectiveToPoint);
            if (replacements.TryGetValue(toPoint, out var replacementToPoint))
            {
                ApplyReviewedToPoint(segment, replacementToPoint);
            }
        }

        var derivedPoints = result.DerivedPoints
            .Where(point => !replacements.ContainsKey(point.PointId))
            .ToArray();
        var status = string.Equals(result.Status, "blocked", StringComparison.OrdinalIgnoreCase)
            ? result.Status
            : findings.Count > 0 ? "warning" : result.Status;
        return result with
        {
            Status = status,
            DerivedPoints = derivedPoints,
            Findings = findings
        };
    }

    public SurveyPlanBoundarySolverResult Solve(
        ExtractionReviewDocument document,
        double? documentAreaSqM,
        bool useDerivedCoordinatesAsAnchors = false,
        bool repairPrematureClosingLabels = false,
        bool replaceConflictingCoordinatesFromReviewedSegments = false)
    {
        var findings = new List<string>();
        var segments = document.Segments
            .Where(segment => segment.EffectiveIncludeInBoundary)
            .OrderBy(segment => segment.EffectiveSequence)
            .ToArray();
        if (segments.Length == 0)
        {
            findings.Add("No reviewed boundary segments are available.");
            return SurveyPlanBoundarySolverResult.Blocked(findings);
        }

        if (repairPrematureClosingLabels)
        {
            RepairPrematureClosingPointLabels(segments, findings);
        }

        var coordinates = LoadPointCoordinates(document.Rows, findings, useDerivedCoordinatesAsAnchors);
        var derivedPoints = new Dictionary<string, SolverPoint>(StringComparer.OrdinalIgnoreCase);
        var parsedSegments = new List<SolverSegment>();
        foreach (var segment in segments)
        {
            var fromPoint = NormalizePointId(segment.EffectiveFromPoint);
            var toPoint = NormalizePointId(segment.EffectiveToPoint);
            if (string.IsNullOrWhiteSpace(fromPoint) || string.IsNullOrWhiteSpace(toPoint))
            {
                findings.Add($"Segment {SegmentLabel(segment)} is missing a from/to point.");
                continue;
            }

            var delta = SurveyPlanBearingParser.ParseDelta(segment.EffectiveBearingText, segment.EffectiveDistanceText);
            if (!delta.Success)
            {
                findings.Add($"Segment {fromPoint}->{toPoint} has an invalid bearing/distance: {delta.ErrorMessage}");
                continue;
            }

            parsedSegments.Add(new SolverSegment(segment, fromPoint, toPoint, delta.DeltaEasting, delta.DeltaNorthing));
        }

        if (parsedSegments.Count == 0)
        {
            return SurveyPlanBoundarySolverResult.Blocked(findings);
        }

        for (var pass = 0; pass < parsedSegments.Count * 2; pass++)
        {
            var changed = false;
            foreach (var segment in parsedSegments)
            {
                if (coordinates.TryGetValue(segment.FromPoint, out var fromCoordinate))
                {
                    var derived = new SolverPoint(
                        segment.ToPoint,
                        fromCoordinate.Easting + segment.DeltaEasting,
                        fromCoordinate.Northing + segment.DeltaNorthing,
                        "derived_from_reviewed_segments",
                        $"{segment.FromPoint}->{segment.ToPoint}");
                    changed |= AddOrCompareCoordinate(coordinates, derivedPoints, derived, findings, replaceConflictingCoordinatesFromReviewedSegments);
                }

                if (coordinates.TryGetValue(segment.ToPoint, out var toCoordinate))
                {
                    var derived = new SolverPoint(
                        segment.FromPoint,
                        toCoordinate.Easting - segment.DeltaEasting,
                        toCoordinate.Northing - segment.DeltaNorthing,
                        "derived_from_reviewed_segments",
                        $"{segment.FromPoint}->{segment.ToPoint}");
                    changed |= AddOrCompareCoordinate(coordinates, derivedPoints, derived, findings, replaceConflictingCoordinatesFromReviewedSegments);
                }
            }

            if (!changed)
            {
                break;
            }
        }

        foreach (var segment in parsedSegments)
        {
            if (!coordinates.ContainsKey(segment.FromPoint))
            {
                findings.Add($"Reviewed segment {segment.FromPoint}->{segment.ToPoint} still has no coordinate for point {segment.FromPoint}.");
            }

            if (!coordinates.ContainsKey(segment.ToPoint))
            {
                findings.Add($"Reviewed segment {segment.FromPoint}->{segment.ToPoint} still has no coordinate for point {segment.ToPoint}.");
            }
        }

        for (var index = 1; index < parsedSegments.Count; index++)
        {
            var previous = parsedSegments[index - 1];
            var current = parsedSegments[index];
            if (!string.Equals(previous.ToPoint, current.FromPoint, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add($"Reviewed boundary chain break: segment {previous.FromPoint}->{previous.ToPoint} is followed by {current.FromPoint}->{current.ToPoint}.");
            }
        }

        var duplicateReferences = parsedSegments
            .SelectMany(segment => new[] { segment.FromPoint, segment.ToPoint })
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 2)
            .Select(group => group.Key)
            .ToArray();
        foreach (var duplicate in duplicateReferences)
        {
            findings.Add($"Point {duplicate} is referenced more than twice in the reviewed boundary chain.");
        }

        var closureDistance = ComputeClosureDistance(parsedSegments, coordinates);
        if (closureDistance.HasValue && closureDistance.Value > ClosureWarningToleranceM)
        {
            findings.Add($"Reviewed boundary closure distance is {closureDistance.Value.ToString("0.###", CultureInfo.InvariantCulture)} m.");
        }

        var area = ComputeArea(parsedSegments, coordinates);
        double? areaDelta = null;
        double? areaDeltaPercent = null;
        if (area.HasValue && documentAreaSqM.HasValue && documentAreaSqM.Value > 0d)
        {
            areaDelta = Math.Abs(area.Value - documentAreaSqM.Value);
            areaDeltaPercent = areaDelta.Value / documentAreaSqM.Value * 100d;
        }

        var blocker = findings.Any(finding =>
            finding.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || finding.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || finding.Contains("no coordinate", StringComparison.OrdinalIgnoreCase)
            || finding.Contains("chain break", StringComparison.OrdinalIgnoreCase)
            || finding.Contains("conflict", StringComparison.OrdinalIgnoreCase));

        return new SurveyPlanBoundarySolverResult(
            blocker ? "blocked" : findings.Count > 0 ? "warning" : "passed",
            derivedPoints.Values.ToArray(),
            closureDistance,
            area,
            documentAreaSqM,
            areaDelta,
            areaDeltaPercent,
            findings);
    }

    private static int RepairPrematureClosingPointLabels(IReadOnlyList<ExtractionReviewSegment> segments, List<string> findings)
    {
        if (segments.Count < 3)
        {
            return 0;
        }

        var firstPoint = NormalizePointId(segments[0].EffectiveFromPoint);
        if (string.IsNullOrWhiteSpace(firstPoint))
        {
            return 0;
        }

        var finalToPoint = NormalizePointId(segments[^1].EffectiveToPoint);
        var preserveFinalClosure = string.Equals(firstPoint, finalToPoint, StringComparison.OrdinalIgnoreCase)
            || ReviewedSegmentDeltasClose(segments);
        var labels = segments
            .SelectMany(segment => new[] { NormalizePointId(segment.EffectiveFromPoint), NormalizePointId(segment.EffectiveToPoint) })
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();
        var usedLabels = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);
        var generator = PointLabelGenerator.Create(labels);
        var visitedBoundaryLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { firstPoint };
        var currentPoint = firstPoint;
        var repairs = 0;
        var generatedLabels = new List<string>();

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var fromPoint = NormalizePointId(segment.EffectiveFromPoint);
            var toPoint = NormalizePointId(segment.EffectiveToPoint);

            if (index > 0
                && !string.IsNullOrWhiteSpace(currentPoint)
                && !string.Equals(fromPoint, currentPoint, StringComparison.OrdinalIgnoreCase)
                && visitedBoundaryLabels.Contains(fromPoint))
            {
                ApplyReviewedFromPoint(segment, currentPoint);
                fromPoint = currentPoint;
                repairs++;
            }

            if (index == segments.Count - 1 && preserveFinalClosure)
            {
                if (!string.Equals(toPoint, firstPoint, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyReviewedToPoint(segment, firstPoint);
                    toPoint = firstPoint;
                    repairs++;
                }
            }
            else if (visitedBoundaryLabels.Contains(toPoint))
            {
                var replacement = generator.Next(usedLabels);
                usedLabels.Add(replacement);
                generatedLabels.Add(replacement);
                ApplyReviewedToPoint(segment, replacement);
                toPoint = replacement;
                repairs++;
            }

            if (!string.IsNullOrWhiteSpace(toPoint))
            {
                visitedBoundaryLabels.Add(toPoint);
            }

            currentPoint = toPoint;
        }

        if (repairs > 0)
        {
            findings.Add($"Rebuild repaired repeated point labels before final closure at {firstPoint}; generated intermediate point label(s): {string.Join(", ", generatedLabels.Distinct(StringComparer.OrdinalIgnoreCase))}.");
        }

        return repairs;
    }

    private static bool ReviewedSegmentDeltasClose(IReadOnlyList<ExtractionReviewSegment> segments)
    {
        var deltaEasting = 0d;
        var deltaNorthing = 0d;
        foreach (var segment in segments)
        {
            var delta = SurveyPlanBearingParser.ParseDelta(segment.EffectiveBearingText, segment.EffectiveDistanceText);
            if (!delta.Success)
            {
                return false;
            }

            deltaEasting += delta.DeltaEasting;
            deltaNorthing += delta.DeltaNorthing;
        }

        return Math.Sqrt(deltaEasting * deltaEasting + deltaNorthing * deltaNorthing) <= ClosureWarningToleranceM;
    }

    private static void ApplyReviewedFromPoint(ExtractionReviewSegment segment, string pointId)
    {
        if (string.Equals(segment.EffectiveFromPoint, pointId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        segment.ReviewFromPoint = pointId;
        segment.RawSegment["review_from_point"] = pointId;
        segment.IsEdited = true;
    }

    private static void ApplyReviewedToPoint(ExtractionReviewSegment segment, string pointId)
    {
        if (string.Equals(segment.EffectiveToPoint, pointId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        segment.ReviewToPoint = pointId;
        segment.RawSegment["review_to_point"] = pointId;
        segment.IsEdited = true;
    }

    private static Dictionary<string, SolverPoint> LoadPointCoordinates(
        IReadOnlyList<ExtractionReviewRow> rows,
        List<string> findings,
        bool useDerivedCoordinatesAsAnchors)
    {
        var coordinates = new Dictionary<string, SolverPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!useDerivedCoordinatesAsAnchors && IsDerivedFromReviewedSegments(row))
            {
                continue;
            }

            var pointId = NormalizePointId(row.PointIdentifier);
            if (string.IsNullOrWhiteSpace(pointId))
            {
                continue;
            }

            if (!TryParseCoordinate(row.Easting, out var easting) || !TryParseCoordinate(row.Northing, out var northing))
            {
                continue;
            }

            var point = new SolverPoint(pointId, easting, northing, row.ExtractionStatus, row.SourceEvidence);
            if (coordinates.TryGetValue(pointId, out var existing)
                && Distance(existing, point) > CoordinateConflictToleranceM)
            {
                findings.Add($"Point {pointId} has conflicting coordinate rows.");
                continue;
            }

            coordinates[pointId] = point;
        }

        return coordinates;
    }

    private static bool AddOrCompareCoordinate(
        Dictionary<string, SolverPoint> coordinates,
        Dictionary<string, SolverPoint> derivedPoints,
        SolverPoint derived,
        List<string> findings,
        bool replaceConflictingCoordinatesFromReviewedSegments)
    {
        if (coordinates.TryGetValue(derived.PointId, out var existing))
        {
            if (Distance(existing, derived) > CoordinateConflictToleranceM)
            {
                if (replaceConflictingCoordinatesFromReviewedSegments)
                {
                    coordinates[derived.PointId] = derived;
                    derivedPoints[derived.PointId] = derived;
                    findings.Add($"Point {derived.PointId} was recalculated from the reviewed boundary path at segment {derived.SourceSegment}.");
                    return true;
                }

                findings.Add($"Point {derived.PointId} has existing coordinates that do not match the reviewed boundary path at segment {derived.SourceSegment}. If the boundary notes are correct, rebuild/replace this point from the reviewed segments; otherwise edit the segment bearing or distance.");
            }

            return false;
        }

        coordinates[derived.PointId] = derived;
        derivedPoints[derived.PointId] = derived;
        return true;
    }

    private static void ApplyDerivedRows(
        ExtractionReviewDocument document,
        SurveyPlanBoundarySolverResult result,
        bool replaceExistingCoordinatesFromReviewedSegments,
        bool removeInactiveManualRows)
    {
        var maxSequence = document.Rows
            .Where(row => row.SequenceInGroup.HasValue)
            .Select(row => row.SequenceInGroup!.Value)
            .DefaultIfEmpty(0)
            .Max();
        var parcelGroupId = document.Rows.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.ParcelGroupId))?.ParcelGroupId ?? "parcel-001";
        var parcelName = document.Rows.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.ParcelName))?.ParcelName ?? parcelGroupId;
        var traverseId = document.Rows.FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.TraverseId))?.TraverseId ?? parcelGroupId;

        foreach (var point in result.DerivedPoints)
        {
            var existingRow = document.Rows.FirstOrDefault(row =>
                string.Equals(NormalizePointId(row.PointIdentifier), point.PointId, StringComparison.OrdinalIgnoreCase));
            if (existingRow is not null)
            {
                if (replaceExistingCoordinatesFromReviewedSegments
                    || IsDerivedFromReviewedSegments(existingRow)
                    || !TryParseCoordinate(existingRow.Easting, out _)
                    || !TryParseCoordinate(existingRow.Northing, out _))
                {
                    ApplyDerivedCoordinateToExistingRow(existingRow, point);
                }

                continue;
            }

            maxSequence++;
            document.Rows.Add(new ExtractionReviewRow
            {
                RowId = $"derived-{point.PointId}",
                ParcelGroupId = parcelGroupId,
                ParcelName = parcelName,
                TraverseId = traverseId,
                SequenceInGroup = maxSequence,
                PointIdentifier = point.PointId,
                Easting = point.Easting.ToString("0.###", CultureInfo.InvariantCulture),
                Northing = point.Northing.ToString("0.###", CultureInfo.InvariantCulture),
                ExtractionStatus = "derived_from_reviewed_segments",
                SourceEvidence = $"Reviewed segment {point.SourceSegment}",
                RowProvenance = "derived_from_reviewed_segments",
                IsEdited = true,
                ReviewNotes = "Derived by deterministic boundary solver from reviewed segment sequence.",
                RawRow = new JsonObject
                {
                    ["point_id"] = point.PointId,
                    ["easting"] = point.Easting,
                    ["northing"] = point.Northing,
                    ["status"] = "derived_from_reviewed_segments",
                    ["source_evidence"] = $"Reviewed segment {point.SourceSegment}",
                    ["row_provenance"] = "derived_from_reviewed_segments"
                },
                OriginalValues = new ExtractionReviewOriginalValues
                {
                    PointIdentifier = point.PointId,
                    Easting = point.Easting.ToString("0.###", CultureInfo.InvariantCulture),
                    Northing = point.Northing.ToString("0.###", CultureInfo.InvariantCulture),
                    ExtractionStatus = "derived_from_reviewed_segments",
                    SourceEvidence = $"Reviewed segment {point.SourceSegment}"
                }
            });
        }

        ApplyReviewedBoundarySequence(document, replaceExistingCoordinatesFromReviewedSegments, removeInactiveManualRows);
    }

    private static void ApplyReviewedBoundarySequence(
        ExtractionReviewDocument document,
        bool removeInactiveRows,
        bool removeInactiveManualRows)
    {
        var orderedPointIds = BuildReviewedBoundaryPointOrder(document);
        if (orderedPointIds.Count == 0)
        {
            return;
        }

        RemoveInactiveBoundaryRows(document, orderedPointIds, removeInactiveRows, removeInactiveManualRows);

        var sequence = 1;
        foreach (var pointId in orderedPointIds)
        {
            var row = document.Rows.FirstOrDefault(item =>
                string.Equals(NormalizePointId(item.PointIdentifier), pointId, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                continue;
            }

            row.SequenceInGroup = sequence++;
            row.IsEdited = true;
            row.RawRow["sequence"] = row.SequenceInGroup;
            row.RawRow["seq"] = row.SequenceInGroup;
            row.RawRow["review_sequence"] = row.SequenceInGroup;
        }
    }

    private static void RemoveInactiveBoundaryRows(
        ExtractionReviewDocument document,
        IReadOnlyCollection<string> orderedPointIds,
        bool removeAllNonManualInactiveRows,
        bool removeInactiveManualRows)
    {
        var activePointIds = new HashSet<string>(
            orderedPointIds.Select(NormalizePointId).Where(pointId => !string.IsNullOrWhiteSpace(pointId)),
            StringComparer.OrdinalIgnoreCase);

        for (var index = document.Rows.Count - 1; index >= 0; index--)
        {
            var row = document.Rows[index];
            var pointId = NormalizePointId(row.PointIdentifier);
            if (string.IsNullOrWhiteSpace(pointId)
                || activePointIds.Contains(pointId)
                || (row.IsManual && !removeInactiveManualRows)
                || (!removeAllNonManualInactiveRows && !IsDerivedFromReviewedSegments(row)))
            {
                continue;
            }

            document.Rows.RemoveAt(index);
        }
    }

    private static bool IsDerivedFromReviewedSegments(ExtractionReviewRow row)
    {
        return string.Equals(row.ExtractionStatus, "derived_from_reviewed_segments", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.RowProvenance, "derived_from_reviewed_segments", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadRawString(row.RawRow, "status"), "derived_from_reviewed_segments", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadRawString(row.RawRow, "row_provenance"), "derived_from_reviewed_segments", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadRawString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    private static IReadOnlyList<string> BuildReviewedBoundaryPointOrder(ExtractionReviewDocument document)
    {
        var segments = document.Segments
            .Where(segment => segment.EffectiveIncludeInBoundary)
            .OrderBy(segment => segment.EffectiveSequence)
            .ToArray();
        if (segments.Length == 0)
        {
            return Array.Empty<string>();
        }

        var orderedPointIds = new List<string>();
        var firstPoint = NormalizePointId(segments[0].EffectiveFromPoint);
        if (!string.IsNullOrWhiteSpace(firstPoint))
        {
            orderedPointIds.Add(firstPoint);
        }

        foreach (var segment in segments)
        {
            var toPoint = NormalizePointId(segment.EffectiveToPoint);
            if (string.IsNullOrWhiteSpace(toPoint))
            {
                continue;
            }

            if (orderedPointIds.Count > 0
                && string.Equals(orderedPointIds[0], toPoint, StringComparison.OrdinalIgnoreCase)
                && ReferenceEquals(segment, segments[^1]))
            {
                continue;
            }

            orderedPointIds.Add(toPoint);
        }

        return orderedPointIds
            .Where(pointId => !string.IsNullOrWhiteSpace(pointId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ApplyDerivedCoordinateToExistingRow(ExtractionReviewRow row, SolverPoint point)
    {
        var easting = point.Easting.ToString("0.###", CultureInfo.InvariantCulture);
        var northing = point.Northing.ToString("0.###", CultureInfo.InvariantCulture);
        row.Easting = easting;
        row.Northing = northing;
        row.ExtractionStatus = "derived_from_reviewed_segments";
        row.SourceEvidence = $"Reviewed segment {point.SourceSegment}";
        row.RowProvenance = "derived_from_reviewed_segments";
        row.IsEdited = true;
        row.ReviewNotes = "Derived by deterministic boundary solver from reviewed segment sequence.";
        row.RawRow["easting"] = point.Easting;
        row.RawRow["northing"] = point.Northing;
        row.RawRow["status"] = "derived_from_reviewed_segments";
        row.RawRow["source_evidence"] = $"Reviewed segment {point.SourceSegment}";
        row.RawRow["row_provenance"] = "derived_from_reviewed_segments";
        row.OriginalValues.Easting ??= easting;
        row.OriginalValues.Northing ??= northing;
        row.OriginalValues.ExtractionStatus ??= "derived_from_reviewed_segments";
        row.OriginalValues.SourceEvidence ??= $"Reviewed segment {point.SourceSegment}";
    }

    private static double? ComputeClosureDistance(IReadOnlyList<SolverSegment> segments, IReadOnlyDictionary<string, SolverPoint> coordinates)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        var firstPointId = segments[0].FromPoint;
        var lastPointId = segments[^1].ToPoint;
        if (!coordinates.TryGetValue(firstPointId, out var first) || !coordinates.TryGetValue(lastPointId, out var last))
        {
            return null;
        }

        return Distance(first, last);
    }

    private static double? ComputeArea(IReadOnlyList<SolverSegment> segments, IReadOnlyDictionary<string, SolverPoint> coordinates)
    {
        if (segments.Count < 3)
        {
            return null;
        }

        var orderedPointIds = new List<string> { segments[0].FromPoint };
        orderedPointIds.AddRange(segments.Select(segment => segment.ToPoint));
        if (orderedPointIds.Count > 1 && string.Equals(orderedPointIds[0], orderedPointIds[^1], StringComparison.OrdinalIgnoreCase))
        {
            orderedPointIds.RemoveAt(orderedPointIds.Count - 1);
        }

        var points = new List<SolverPoint>();
        foreach (var pointId in orderedPointIds)
        {
            if (!coordinates.TryGetValue(pointId, out var point))
            {
                return null;
            }

            points.Add(point);
        }

        if (points.Count < 3)
        {
            return null;
        }

        var sum = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index];
            var next = points[(index + 1) % points.Count];
            sum += current.Easting * next.Northing - next.Easting * current.Northing;
        }

        return Math.Abs(sum) / 2d;
    }

    private static double Distance(SolverPoint first, SolverPoint second)
    {
        var dx = second.Easting - first.Easting;
        var dy = second.Northing - first.Northing;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool TryParseCoordinate(string? value, out double coordinate)
    {
        return double.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out coordinate)
            || double.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out coordinate);
    }

    private static string NormalizePointId(string? value) => (value ?? string.Empty).Trim();

    private static string SegmentLabel(ExtractionReviewSegment segment)
    {
        return !string.IsNullOrWhiteSpace(segment.SegmentId)
            ? segment.SegmentId
            : segment.EffectiveSequence.ToString(CultureInfo.InvariantCulture);
    }
}

public static class SurveyPlanBearingParser
{
    public static SurveyPlanBearingParseResult ParseDelta(string? bearingText, string? distanceText)
    {
        if (!TryParseDistance(distanceText, out var distance))
        {
            return SurveyPlanBearingParseResult.Failed("distance is missing or invalid");
        }

        var text = NormalizeBearingText(bearingText);
        if (text.Length < 3)
        {
            return SurveyPlanBearingParseResult.Failed("bearing is missing or invalid");
        }

        var first = text[0];
        var last = text[^1];
        if ((first is not 'N' and not 'S') || (last is not 'E' and not 'W'))
        {
            return SurveyPlanBearingParseResult.Failed("bearing must start with N/S and end with E/W");
        }

        var middle = text[1..^1].Trim();
        var parts = middle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .ToArray();
        if (parts.Length == 0 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
        {
            return SurveyPlanBearingParseResult.Failed("bearing degrees are invalid");
        }

        var minutes = parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMinutes)
            ? parsedMinutes
            : 0d;
        var seconds = parts.Length > 2 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSeconds)
            ? parsedSeconds
            : 0d;
        var angle = (degrees + minutes / 60d + seconds / 3600d) * Math.PI / 180d;
        var northMagnitude = Math.Cos(angle) * distance;
        var eastMagnitude = Math.Sin(angle) * distance;
        var deltaNorthing = first == 'N' ? northMagnitude : -northMagnitude;
        var deltaEasting = last == 'E' ? eastMagnitude : -eastMagnitude;
        return SurveyPlanBearingParseResult.Succeeded(deltaEasting, deltaNorthing);
    }

    private static string NormalizeBearingText(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace("°", " ")
            .Replace("º", " ")
            .Replace("'", " ")
            .Replace("\"", " ")
            .Replace("’", " ")
            .Replace("”", " ")
            .Replace("`", " ");
    }

    private static bool TryParseDistance(string? value, out double distance)
    {
        var text = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("metres", string.Empty)
            .Replace("meters", string.Empty)
            .Replace("meter", string.Empty)
            .Replace("metre", string.Empty)
            .Replace("m", string.Empty)
            .Trim();
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out distance);
    }
}

public sealed record SurveyPlanBearingParseResult(bool Success, double DeltaEasting, double DeltaNorthing, string ErrorMessage)
{
    public static SurveyPlanBearingParseResult Succeeded(double deltaEasting, double deltaNorthing) =>
        new(true, deltaEasting, deltaNorthing, string.Empty);

    public static SurveyPlanBearingParseResult Failed(string errorMessage) =>
        new(false, 0d, 0d, errorMessage);
}

public sealed record SurveyPlanBoundarySolverResult(
    string Status,
    IReadOnlyList<SolverPoint> DerivedPoints,
    double? ClosureDistanceM,
    double? ComputedAreaSqM,
    double? DocumentAreaSqM,
    double? AreaDeltaSqM,
    double? AreaDeltaPercent,
    IReadOnlyList<string> Findings)
{
    public int DerivedPointCount => DerivedPoints.Count;

    public static SurveyPlanBoundarySolverResult Blocked(IReadOnlyList<string> findings) =>
        new("blocked", Array.Empty<SolverPoint>(), null, null, null, null, null, findings);
}

public sealed record SolverPoint(string PointId, double Easting, double Northing, string Status, string SourceSegment);

internal sealed record SolverSegment(
    ExtractionReviewSegment Source,
    string FromPoint,
    string ToPoint,
    double DeltaEasting,
    double DeltaNorthing);

internal sealed class PointLabelGenerator
{
    private readonly bool numericStyle;
    private int nextNumericLabel;

    private PointLabelGenerator(bool numericStyle, int nextNumericLabel)
    {
        this.numericStyle = numericStyle;
        this.nextNumericLabel = nextNumericLabel;
    }

    public static PointLabelGenerator Create(IReadOnlyList<string> labels)
    {
        var numericLabels = labels
            .Select(label => int.TryParse(label, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var alphabeticCount = labels.Count(label => IsAlphabeticLabel(label));
        var numericStyle = alphabeticCount >= numericLabels.Length;
        return new PointLabelGenerator(
            numericStyle,
            numericLabels.Length == 0 ? 1 : numericLabels.Max() + 1);
    }

    public string Next(HashSet<string> usedLabels)
    {
        return numericStyle
            ? NextNumeric(usedLabels)
            : NextAlphabetic(usedLabels);
    }

    private string NextNumeric(HashSet<string> usedLabels)
    {
        while (usedLabels.Contains(nextNumericLabel.ToString(CultureInfo.InvariantCulture)))
        {
            nextNumericLabel++;
        }

        return nextNumericLabel++.ToString(CultureInfo.InvariantCulture);
    }

    private static string NextAlphabetic(HashSet<string> usedLabels)
    {
        for (var value = 1; value < 10000; value++)
        {
            var label = ToAlphabeticLabel(value);
            if (!usedLabels.Contains(label))
            {
                return label;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique alphabetic point label.");
    }

    private static bool IsAlphabeticLabel(string label)
    {
        return !string.IsNullOrWhiteSpace(label)
            && label.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    private static string ToAlphabeticLabel(int value)
    {
        var buffer = new Stack<char>();
        while (value > 0)
        {
            value--;
            buffer.Push((char)('A' + value % 26));
            value /= 26;
        }

        return new string(buffer.ToArray());
    }
}
