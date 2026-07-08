using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class SurveyPlanBoundarySolver
{
    private const double CoordinateConflictToleranceM = 0.25d;
    private const double ClosureWarningToleranceM = 1.0d;

    public SurveyPlanBoundarySolverResult Apply(ExtractionReviewDocument document, double? documentAreaSqM)
    {
        var result = Solve(document, documentAreaSqM);
        ApplyDerivedRows(document, result);
        document.RootMetadata["boundary_solver"] = JsonSerializer.SerializeToNode(new
        {
            status = result.Status,
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

    public SurveyPlanBoundarySolverResult Solve(ExtractionReviewDocument document, double? documentAreaSqM)
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

        var coordinates = LoadPointCoordinates(document.Rows, findings);
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
                    changed |= AddOrCompareCoordinate(coordinates, derivedPoints, derived, findings);
                }

                if (coordinates.TryGetValue(segment.ToPoint, out var toCoordinate))
                {
                    var derived = new SolverPoint(
                        segment.FromPoint,
                        toCoordinate.Easting - segment.DeltaEasting,
                        toCoordinate.Northing - segment.DeltaNorthing,
                        "derived_from_reviewed_segments",
                        $"{segment.FromPoint}->{segment.ToPoint}");
                    changed |= AddOrCompareCoordinate(coordinates, derivedPoints, derived, findings);
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

    private static Dictionary<string, SolverPoint> LoadPointCoordinates(IReadOnlyList<ExtractionReviewRow> rows, List<string> findings)
    {
        var coordinates = new Dictionary<string, SolverPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
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
        List<string> findings)
    {
        if (coordinates.TryGetValue(derived.PointId, out var existing))
        {
            if (Distance(existing, derived) > CoordinateConflictToleranceM)
            {
                findings.Add($"Derived coordinate conflict for point {derived.PointId} from segment {derived.SourceSegment}; printed/reviewed coordinate was preserved.");
            }

            return false;
        }

        coordinates[derived.PointId] = derived;
        derivedPoints[derived.PointId] = derived;
        return true;
    }

    private static void ApplyDerivedRows(ExtractionReviewDocument document, SurveyPlanBoundarySolverResult result)
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
            if (document.Rows.Any(row => string.Equals(NormalizePointId(row.PointIdentifier), point.PointId, StringComparison.OrdinalIgnoreCase)))
            {
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
