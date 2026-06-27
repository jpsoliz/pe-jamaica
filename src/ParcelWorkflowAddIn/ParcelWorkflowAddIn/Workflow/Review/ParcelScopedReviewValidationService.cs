using System.Globalization;
using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Execution;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ParcelScopedReviewValidationService
{
    private readonly Func<ClosureToleranceCatalog> getClosureToleranceCatalog;
    private readonly Func<ParcelReadinessCatalog> getParcelReadinessCatalog;

    public ParcelScopedReviewValidationService()
        : this(() => ClosureToleranceCatalog.Load(), () => ParcelReadinessCatalog.Load())
    {
    }

    internal ParcelScopedReviewValidationService(
        Func<ClosureToleranceCatalog> getClosureToleranceCatalog,
        Func<ParcelReadinessCatalog> getParcelReadinessCatalog)
    {
        this.getClosureToleranceCatalog = getClosureToleranceCatalog;
        this.getParcelReadinessCatalog = getParcelReadinessCatalog;
    }

    public ParcelScopedReviewValidationResult Validate(
        IReadOnlyList<ExtractionReviewRow> rows,
        string? pendingManualRowId = null,
        bool includePendingManualBarrier = true)
    {
        var issues = new List<string>();
        var parcelIssues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows.Count == 0)
        {
            issues.Add("No review rows are loaded.");
            return new ParcelScopedReviewValidationResult(issues, parcelIssues, Array.Empty<ParcelClosureReviewResult>(), Array.Empty<ParcelReadinessReviewResult>());
        }

        var unresolvedCount = rows.Count(row => row.Unresolved);
        if (unresolvedCount > 0)
        {
            issues.Add($"{unresolvedCount} unresolved row(s) still need examiner confirmation.");
        }

        var missingRequiredCount = rows.Count(row =>
            string.IsNullOrWhiteSpace(row.PointIdentifier)
            || string.IsNullOrWhiteSpace(row.Easting)
            || string.IsNullOrWhiteSpace(row.Northing));
        if (missingRequiredCount > 0)
        {
            issues.Add($"{missingRequiredCount} row(s) are missing point id or coordinates.");
        }

        var invalidCoordinateCount = rows.Count(row =>
            !string.IsNullOrWhiteSpace(row.Easting)
            && !TryParseCoordinate(row.Easting, out _)
            || !string.IsNullOrWhiteSpace(row.Northing)
            && !TryParseCoordinate(row.Northing, out _));
        if (invalidCoordinateCount > 0)
        {
            issues.Add($"{invalidCoordinateCount} row(s) contain invalid numeric coordinates.");
        }

        var manualWithoutParcelCount = rows.Count(row =>
            row.IsManual
            && (string.IsNullOrWhiteSpace(row.ParcelGroupId) || string.Equals(row.ParcelGroupId.Trim(), "Parcel ?", StringComparison.OrdinalIgnoreCase)));
        if (manualWithoutParcelCount > 0)
        {
            issues.Add($"{manualWithoutParcelCount} manual row(s) are missing parcel assignment.");
        }

        foreach (var parcelGroup in rows.GroupBy(row => NormalizeParcelGroup(row.ParcelGroupId), StringComparer.OrdinalIgnoreCase))
        {
            var parcelLabel = parcelGroup.Key;
            var duplicatePointIds = parcelGroup
                .Where(row => !string.IsNullOrWhiteSpace(row.PointIdentifier))
                .GroupBy(row => row.PointIdentifier.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePointIds.Length > 0)
            {
                var message = $"Parcel {parcelLabel} has duplicate point id(s): {string.Join(", ", duplicatePointIds)}.";
                issues.Add(message);
                parcelIssues[parcelLabel] = AppendIssue(parcelIssues, parcelLabel, message);
            }

            var invalidSequenceRows = parcelGroup.Count(row => row.SequenceInGroup is null or <= 0);
            if (invalidSequenceRows > 0)
            {
                var message = $"Parcel {parcelLabel} has {invalidSequenceRows} row(s) with missing or invalid sequence.";
                issues.Add(message);
                parcelIssues[parcelLabel] = AppendIssue(parcelIssues, parcelLabel, message);
            }

            var duplicateSequences = parcelGroup
                .Where(row => row.SequenceInGroup.HasValue && row.SequenceInGroup.Value > 0)
                .GroupBy(row => row.SequenceInGroup!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key.ToString(CultureInfo.InvariantCulture))
                .ToArray();
            if (duplicateSequences.Length > 0)
            {
                var message = $"Parcel {parcelLabel} has duplicate sequence value(s): {string.Join(", ", duplicateSequences)}.";
                issues.Add(message);
                parcelIssues[parcelLabel] = AppendIssue(parcelIssues, parcelLabel, message);
            }
        }

        var closureResults = ComputeClosureResults(rows);
        var readinessResults = ComputeReadinessResults(rows);
        foreach (var closureResult in closureResults)
        {
            if (closureResult.Status == ClosureValidationStatus.Passed)
            {
                continue;
            }

            var label = closureResult.ParcelGroupId;
            parcelIssues[label] = AppendIssue(parcelIssues, label, closureResult.Message);
            if (closureResult.Status == ClosureValidationStatus.Blocker)
            {
                issues.Add(closureResult.Message);
            }
        }

        foreach (var readinessResult in readinessResults)
        {
            var label = readinessResult.ParcelGroupId;
            if (!string.IsNullOrWhiteSpace(label)
                && readinessResult.Status is ReadinessValidationStatus.Blocker or ReadinessValidationStatus.Warning)
            {
                parcelIssues[label] = AppendIssue(parcelIssues, label, readinessResult.Message);
            }

            if (readinessResult.Status == ReadinessValidationStatus.Blocker)
            {
                issues.Add(readinessResult.Message);
            }
        }

        if (includePendingManualBarrier && !string.IsNullOrWhiteSpace(pendingManualRowId))
        {
            issues.Add("Save or discard the in-progress manual point before approval or parcel switching.");
        }

        return new ParcelScopedReviewValidationResult(
            issues.Distinct(StringComparer.Ordinal).ToArray(),
            parcelIssues,
            closureResults,
            readinessResults);
    }

    private IReadOnlyList<ParcelClosureReviewResult> ComputeClosureResults(IReadOnlyList<ExtractionReviewRow> rows)
    {
        var catalog = getClosureToleranceCatalog();
        var defaultProfile = catalog.DefaultProfile;
        var sourceMode = InferSourceMode(rows);
        var results = new List<ParcelClosureReviewResult>();

        foreach (var parcelGroup in rows.GroupBy(row => NormalizeParcelGroup(row.ParcelGroupId), StringComparer.OrdinalIgnoreCase))
        {
            var orderedRows = parcelGroup
                .OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
                .ThenBy(row => row.PointIdentifier, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var parcelType = InferParcelType(orderedRows, defaultProfile.ParcelType, sourceMode);
            var profile = catalog.Resolve(parcelType);
            var coordinates = orderedRows
                .Where(row => TryParseCoordinate(row.Easting, out _) && TryParseCoordinate(row.Northing, out _))
                .Select(row => new CoordinatePoint(ParseCoordinate(row.Easting), ParseCoordinate(row.Northing)))
                .ToArray();

            if (!profile.Enabled)
            {
                results.Add(new ParcelClosureReviewResult(
                    parcelGroup.Key,
                    orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroup.Key,
                    parcelType,
                    ClosureValidationStatus.Passed,
                    "Closure validation profile is disabled.",
                    null,
                    null,
                    profile.RuleId,
                    profile.Title,
                    profile.MaxClosureDistanceM,
                    profile.WarningClosureDistanceM,
                    profile.MinMiscloseRatioDenominator,
                    profile.WarningMiscloseRatioDenominator));
                continue;
            }

            if (coordinates.Length < 2)
            {
                results.Add(new ParcelClosureReviewResult(
                    parcelGroup.Key,
                    orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroup.Key,
                    parcelType,
                    ClosureValidationStatus.Warning,
                    $"Parcel {parcelGroup.Key} does not have enough numeric coordinate rows to compute closure.",
                    null,
                    null,
                    profile.RuleId,
                    profile.Title,
                    profile.MaxClosureDistanceM,
                    profile.WarningClosureDistanceM,
                    profile.MinMiscloseRatioDenominator,
                    profile.WarningMiscloseRatioDenominator));
                continue;
            }

            var start = coordinates[0];
            var end = coordinates[^1];
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var closureDistance = Math.Sqrt((dx * dx) + (dy * dy));
            var totalLength = 0d;
            for (var index = 1; index < coordinates.Length; index++)
            {
                var previous = coordinates[index - 1];
                var current = coordinates[index];
                totalLength += Math.Sqrt(Math.Pow(current.X - previous.X, 2) + Math.Pow(current.Y - previous.Y, 2));
            }

            double? miscloseRatio = null;
            if (closureDistance > 0d && totalLength > 0d)
            {
                miscloseRatio = totalLength / closureDistance;
            }

            var exceedsBlockDistance = profile.MaxClosureDistanceM.HasValue && closureDistance > profile.MaxClosureDistanceM.Value;
            var exceedsWarningDistance = profile.WarningClosureDistanceM.HasValue && closureDistance > profile.WarningClosureDistanceM.Value;
            var belowBlockRatio = miscloseRatio.HasValue && profile.MinMiscloseRatioDenominator.HasValue && miscloseRatio.Value < profile.MinMiscloseRatioDenominator.Value;
            var belowWarningRatio = miscloseRatio.HasValue && profile.WarningMiscloseRatioDenominator.HasValue && miscloseRatio.Value < profile.WarningMiscloseRatioDenominator.Value;

            var status = ClosureValidationStatus.Passed;
            var message = $"Parcel {parcelGroup.Key} closure is within tolerance.";

            if (profile.AllowOpenBoundary)
            {
                if (exceedsWarningDistance || belowWarningRatio)
                {
                    status = ClosureValidationStatus.Warning;
                    message = $"Parcel {parcelGroup.Key} remains outside the configured open-boundary closure tolerance.";
                }
            }
            else if (exceedsBlockDistance || belowBlockRatio)
            {
                status = profile.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? ClosureValidationStatus.Warning
                    : ClosureValidationStatus.Blocker;
                message = $"Parcel {parcelGroup.Key} exceeds the configured closure tolerance.";
            }
            else if (exceedsWarningDistance || belowWarningRatio)
            {
                status = ClosureValidationStatus.Warning;
                message = $"Parcel {parcelGroup.Key} exceeds the configured closure warning tolerance.";
            }

            results.Add(new ParcelClosureReviewResult(
                parcelGroup.Key,
                orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroup.Key,
                parcelType,
                status,
                message,
                closureDistance,
                miscloseRatio,
                profile.RuleId,
                profile.Title,
                profile.MaxClosureDistanceM,
                profile.WarningClosureDistanceM,
                profile.MinMiscloseRatioDenominator,
                profile.WarningMiscloseRatioDenominator));
        }

        return results;
    }

    private IReadOnlyList<ParcelReadinessReviewResult> ComputeReadinessResults(IReadOnlyList<ExtractionReviewRow> rows)
    {
        var catalog = getParcelReadinessCatalog();
        var sourceMode = InferSourceMode(rows);
        var groupedRows = rows
            .GroupBy(row => NormalizeParcelGroup(row.ParcelGroupId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
                    .ThenBy(row => row.PointIdentifier, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var edgeUsage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var parcelEdges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in groupedRows)
        {
            var edges = new List<string>();
            string previousPointId = string.Empty;
            foreach (var row in pair.Value)
            {
                var currentPointId = NormalizePointId(row.PointIdentifier);
                var fromPointId = ExtractRawText(row, "from_point", "from_pt", "start_pt");
                var toPointId = ExtractRawText(row, "to_point", "to_pt", "end_pt");
                var edgeStart = !string.IsNullOrWhiteSpace(fromPointId) ? fromPointId : previousPointId;
                var edgeEnd = !string.IsNullOrWhiteSpace(toPointId) ? toPointId : currentPointId;
                if (!string.IsNullOrWhiteSpace(edgeStart) && !string.IsNullOrWhiteSpace(edgeEnd))
                {
                    var edgeKey = NormalizeEdgeKey(edgeStart, edgeEnd);
                    edges.Add(edgeKey);
                    if (!edgeUsage.TryGetValue(edgeKey, out var parcels))
                    {
                        parcels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        edgeUsage[edgeKey] = parcels;
                    }

                    parcels.Add(pair.Key);
                }

                if (!string.IsNullOrWhiteSpace(currentPointId))
                {
                    previousPointId = currentPointId;
                }
            }

            parcelEdges[pair.Key] = edges;
        }

        var results = new List<ParcelReadinessReviewResult>();
        foreach (var pair in groupedRows)
        {
            var parcelGroupId = pair.Key;
            var orderedRows = pair.Value;
            var parcelType = InferParcelType(orderedRows, catalog.DefaultParcelType, sourceMode);
            var parcelName = orderedRows.FirstOrDefault()?.ParcelName ?? parcelGroupId;
            var pointIds = orderedRows
                .Select(row => NormalizePointId(row.PointIdentifier))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validSequences = orderedRows
                .Where(row => row.SequenceInGroup is > 0)
                .Select(row => row.SequenceInGroup!.Value)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var missingSequences = validSequences.Length == 0
                ? Array.Empty<int>()
                : Enumerable.Range(1, validSequences[^1]).Except(validSequences).ToArray();

            var referencedPointMisses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var referencedPointSegmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orphanSegmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string previousPointId = string.Empty;
            foreach (var row in orderedRows)
            {
                var currentPointId = NormalizePointId(row.PointIdentifier);
                var fromPointId = ExtractRawText(row, "from_point", "from_pt", "start_pt");
                var toPointId = ExtractRawText(row, "to_point", "to_pt", "end_pt");
                var segmentId = ResolveSegmentIdentifier(row);

                if (!string.IsNullOrWhiteSpace(fromPointId) && !pointIds.Contains(fromPointId))
                {
                    referencedPointMisses.Add(fromPointId);
                    referencedPointSegmentIds.Add(segmentId);
                }

                if (!string.IsNullOrWhiteSpace(toPointId) && !pointIds.Contains(toPointId))
                {
                    referencedPointMisses.Add(toPointId);
                    referencedPointSegmentIds.Add(segmentId);
                }

                if (!string.IsNullOrWhiteSpace(previousPointId)
                    && !string.IsNullOrWhiteSpace(fromPointId)
                    && !string.Equals(previousPointId, fromPointId, StringComparison.OrdinalIgnoreCase))
                {
                    orphanSegmentIds.Add(segmentId);
                }

                if (!string.IsNullOrWhiteSpace(currentPointId)
                    && !string.IsNullOrWhiteSpace(toPointId)
                    && !string.Equals(currentPointId, toPointId, StringComparison.OrdinalIgnoreCase))
                {
                    orphanSegmentIds.Add(segmentId);
                }

                if (!string.IsNullOrWhiteSpace(currentPointId))
                {
                    previousPointId = currentPointId;
                }
            }

            var edgeCounts = parcelEdges.TryGetValue(parcelGroupId, out var parcelEdgeList)
                ? parcelEdgeList.GroupBy(edge => edge, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sharedEdgeConflictCount = edgeCounts.Values.Count(count => count > 1)
                                          + edgeCounts.Keys.Count(edgeKey => edgeUsage.TryGetValue(edgeKey, out var parcels) && parcels.Count > 2);

            foreach (var profile in catalog.Resolve(parcelType))
            {
                var status = ReadinessValidationStatus.Passed;
                var message = $"Parcel {parcelGroupId} passed {profile.Title.ToLowerInvariant()}.";
                var affectedPointIds = Array.Empty<string>();
                var affectedSegmentIds = Array.Empty<string>();
                var boundaryGapCount = 0;
                var orphanLineCount = 0;
                var sharedEdgeCount = 0;
                var skipReason = default(string);

                if (!profile.Enabled)
                {
                    status = ReadinessValidationStatus.Skipped;
                    message = $"{profile.Title} is disabled for parcel type {parcelType}.";
                    skipReason = "Rule disabled in readiness profile.";
                }
                else if (string.Equals(profile.Category, "minimum_segment_count", StringComparison.OrdinalIgnoreCase))
                {
                    if (orderedRows.Length < profile.MinSegmentCount)
                    {
                        status = ResolveReadinessStatus(profile.Severity);
                        message = $"Parcel {parcelGroupId} only has {orderedRows.Length} segment row(s); at least {profile.MinSegmentCount} are required.";
                    }
                    else
                    {
                        message = $"Parcel {parcelGroupId} meets the minimum segment count.";
                    }
                }
                else if (string.Equals(profile.Category, "boundary_completeness", StringComparison.OrdinalIgnoreCase))
                {
                    if (!profile.RequireContiguousSequence)
                    {
                        status = ReadinessValidationStatus.Skipped;
                        message = $"{profile.Title} is disabled for parcel type {parcelType}.";
                        skipReason = "Boundary completeness behavior is disabled in readiness profile.";
                    }
                    else
                    {
                        boundaryGapCount = missingSequences.Length;
                        if (boundaryGapCount > 0)
                        {
                            status = ResolveReadinessStatus(profile.Severity);
                            affectedSegmentIds = missingSequences.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();
                            message = $"Parcel {parcelGroupId} has {boundaryGapCount} missing sequence value(s): {string.Join(", ", affectedSegmentIds)}.";
                        }
                        else
                        {
                            message = $"Parcel {parcelGroupId} has a contiguous parcel sequence.";
                        }
                    }
                }
                else if (string.Equals(profile.Category, "line_without_point_support", StringComparison.OrdinalIgnoreCase))
                {
                    if (!profile.RequireReferencedPoints)
                    {
                        status = ReadinessValidationStatus.Skipped;
                        message = $"{profile.Title} is disabled for parcel type {parcelType}.";
                        skipReason = "Referenced-point support behavior is disabled in readiness profile.";
                    }
                    else if (referencedPointMisses.Count > 0)
                    {
                        status = ResolveReadinessStatus(profile.Severity);
                        affectedPointIds = referencedPointMisses.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                        affectedSegmentIds = referencedPointSegmentIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                        message = $"Parcel {parcelGroupId} references point id(s) that are not present in the parcel set: {string.Join(", ", affectedPointIds)}.";
                    }
                    else
                    {
                        message = $"Parcel {parcelGroupId} line references are supported by parcel points.";
                    }
                }
                else if (string.Equals(profile.Category, "orphan_line_detection", StringComparison.OrdinalIgnoreCase))
                {
                    if (!profile.RequireChainConsistency)
                    {
                        status = ReadinessValidationStatus.Skipped;
                        message = $"{profile.Title} is disabled for parcel type {parcelType}.";
                        skipReason = "Chain-consistency behavior is disabled in readiness profile.";
                    }
                    else
                    {
                        orphanLineCount = orphanSegmentIds.Count;
                        if (orphanLineCount > 0)
                        {
                            status = ResolveReadinessStatus(profile.Severity);
                            affectedSegmentIds = orphanSegmentIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
                            message = $"Parcel {parcelGroupId} has {orphanLineCount} segment(s) that do not follow the parcel chain cleanly.";
                        }
                        else
                        {
                            message = $"Parcel {parcelGroupId} line chain follows the parcel sequence.";
                        }
                    }
                }
                else if (string.Equals(profile.Category, "shared_edge_consistency", StringComparison.OrdinalIgnoreCase))
                {
                    if (!profile.DetectDuplicateEdges)
                    {
                        status = ReadinessValidationStatus.Skipped;
                        message = $"{profile.Title} is disabled for parcel type {parcelType}.";
                        skipReason = "Shared-edge conflict detection is disabled in readiness profile.";
                    }
                    else
                    {
                        sharedEdgeCount = sharedEdgeConflictCount;
                        if (sharedEdgeCount > 0)
                        {
                            status = ResolveReadinessStatus(profile.Severity);
                            message = $"Parcel {parcelGroupId} has {sharedEdgeCount} shared-edge or duplicate-edge conflict(s) to review.";
                        }
                        else
                        {
                            message = $"Parcel {parcelGroupId} did not produce shared-edge conflicts.";
                        }
                    }
                }

                results.Add(new ParcelReadinessReviewResult(
                    parcelGroupId,
                    parcelName,
                    parcelType,
                    profile.RuleId,
                    profile.Title,
                    profile.Category,
                    profile.Severity,
                    status,
                    message,
                    affectedPointIds,
                    affectedSegmentIds,
                    boundaryGapCount,
                    sharedEdgeCount,
                    orphanLineCount,
                    !profile.Enabled,
                    skipReason));
            }
        }

        return results;
    }

    private static string AppendIssue(IDictionary<string, string> issues, string key, string message)
    {
        if (!issues.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            return message;
        }

        return $"{existing} {message}";
    }

    private static string InferSourceMode(IReadOnlyList<ExtractionReviewRow> rows)
    {
        var rawValues = rows
            .Select(row => row.RawRow?["source_doc"]?.GetValue<string>() ?? row.RawRow?["source_mode"]?.GetValue<string>() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return rawValues.Any(value => value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ? "imported_coordinates"
            : "source_documents";
    }

    private static string InferParcelType(IReadOnlyList<ExtractionReviewRow> rows, string defaultParcelType, string sourceMode)
    {
        var explicitType = rows
            .Select(row => row.RawRow?["parcel_type"]?.GetValue<string>() ?? row.RawRow?["review_parcel_type"]?.GetValue<string>() ?? string.Empty)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(explicitType))
        {
            return explicitType.Trim();
        }

        if (rows.Any(row => row.IsBoundaryBreak))
        {
            return "open_boundary";
        }

        if (string.Equals(sourceMode, "imported_coordinates", StringComparison.OrdinalIgnoreCase))
        {
            return "imported_coordinates";
        }

        return string.IsNullOrWhiteSpace(defaultParcelType) ? "standard_closed" : defaultParcelType;
    }

    private static bool TryParseCoordinate(string value, out double coordinate)
    {
        var text = value.Trim();
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out coordinate))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out coordinate);
    }

    private static double ParseCoordinate(string value)
    {
        TryParseCoordinate(value, out var coordinate);
        return coordinate;
    }

    private static string NormalizeParcelGroup(string? parcelGroupId)
    {
        return string.IsNullOrWhiteSpace(parcelGroupId) ? "Unassigned" : parcelGroupId.Trim();
    }

    private static string NormalizePointId(string? pointId)
    {
        return string.IsNullOrWhiteSpace(pointId) ? string.Empty : pointId.Trim();
    }

    private static string ExtractRawText(ExtractionReviewRow row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.RawRow.TryGetPropertyValue(key, out var valueNode))
            {
                var text = valueNode?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static string ResolveSegmentIdentifier(ExtractionReviewRow row)
    {
        var raw = ExtractRawText(row, "segment_no", "segment_index", "seq");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        return row.SequenceInGroup?.ToString(CultureInfo.InvariantCulture) ?? row.RowId;
    }

    private static string NormalizeEdgeKey(string pointA, string pointB)
    {
        return string.Compare(pointA, pointB, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{pointA}|{pointB}"
            : $"{pointB}|{pointA}";
    }

    private static ReadinessValidationStatus ResolveReadinessStatus(string severity)
    {
        return severity.Equals("warning", StringComparison.OrdinalIgnoreCase)
               || severity.Equals("info", StringComparison.OrdinalIgnoreCase)
            ? ReadinessValidationStatus.Warning
            : ReadinessValidationStatus.Blocker;
    }
}

public sealed record ParcelScopedReviewValidationResult(
    IReadOnlyList<string> Issues,
    IReadOnlyDictionary<string, string> ParcelIssues,
    IReadOnlyList<ParcelClosureReviewResult> ClosureResults,
    IReadOnlyList<ParcelReadinessReviewResult> ReadinessResults)
{
    public bool HasBlockers => Issues.Count > 0
                               || ClosureResults.Any(result => result.Status == ClosureValidationStatus.Blocker)
                               || ReadinessResults.Any(result => result.Status == ReadinessValidationStatus.Blocker);

    public string SummaryText => !HasBlockers
        ? "Review is complete for this stage."
        : Issues.Count > 0
            ? Issues[0]
            : ReadinessResults.FirstOrDefault(result => result.Status == ReadinessValidationStatus.Blocker)?.Message
              ?? ClosureResults.First(result => result.Status == ClosureValidationStatus.Blocker).Message;
}

public sealed record ParcelClosureReviewResult(
    string ParcelGroupId,
    string ParcelName,
    string ParcelType,
    ClosureValidationStatus Status,
    string Message,
    double? ClosureDistanceM,
    double? MiscloseRatioDenominator,
    string ProfileRuleId,
    string ProfileTitle,
    double? MaxClosureDistanceM,
    double? WarningClosureDistanceM,
    double? MinMiscloseRatioDenominator,
    double? WarningMiscloseRatioDenominator);

public enum ClosureValidationStatus
{
    Passed,
    Warning,
    Blocker
}

public sealed record ParcelReadinessReviewResult(
    string ParcelGroupId,
    string ParcelName,
    string ParcelType,
    string RuleId,
    string Title,
    string Category,
    string Severity,
    ReadinessValidationStatus Status,
    string Message,
    IReadOnlyList<string> AffectedPointIds,
    IReadOnlyList<string> AffectedSegmentIds,
    int BoundaryGapCount,
    int SharedEdgeConflictCount,
    int OrphanLineCount,
    bool RuleDisabled,
    string? RuleSkipReason);

public enum ReadinessValidationStatus
{
    Passed,
    Warning,
    Blocker,
    Skipped
}

internal sealed record ClosureToleranceProfile(
    string RuleId,
    string Title,
    string ParcelType,
    bool Enabled,
    string Severity,
    bool AllowOpenBoundary,
    double? MaxClosureDistanceM,
    double? WarningClosureDistanceM,
    double? MinMiscloseRatioDenominator,
    double? WarningMiscloseRatioDenominator);

internal sealed class ClosureToleranceCatalog
{
    public ClosureToleranceProfile DefaultProfile { get; set; } = new(
        "closure_default_standard",
        "Standard parcel closure tolerance",
        "standard_closed",
        true,
        "blocker",
        false,
        0.3,
        0.15,
        2500,
        4000);

    public Dictionary<string, ClosureToleranceProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ClosureToleranceProfile Resolve(string parcelType)
    {
        return Profiles.TryGetValue(parcelType, out var profile) ? profile : DefaultProfile;
    }

    public static ClosureToleranceCatalog Load(string? settingsPath = null)
    {
        settingsPath ??= InnolaTransactionSettings.ResolveActiveSettingsPath();
        var executionSettings = WorkflowExecutionSettings.Load(settingsPath);
        var catalog = ParseRuleCatalog(executionSettings.ValidationRulesPath);
        ApplyOverrides(catalog, settingsPath);
        return catalog;
    }

    private static ClosureToleranceCatalog ParseRuleCatalog(string? rulesPath)
    {
        var catalog = new ClosureToleranceCatalog();
        if (string.IsNullOrWhiteSpace(rulesPath) || !File.Exists(rulesPath))
        {
            return catalog;
        }

        var lines = File.ReadAllLines(rulesPath);
        var section = string.Empty;
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var profiles = new Dictionary<string, ClosureToleranceProfile>(StringComparer.OrdinalIgnoreCase);
        var skipIndent = -1;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            if (indent == 0)
            {
                if (string.Equals(section, "closure_tolerance_profiles", StringComparison.OrdinalIgnoreCase))
                {
                    CommitProfile(current, profiles);
                    current.Clear();
                }

                section = trimmed.TrimEnd(':');
                skipIndent = -1;
                continue;
            }

            if (skipIndent >= 0 && indent > skipIndent)
            {
                continue;
            }

            if (skipIndent >= 0 && indent <= skipIndent)
            {
                skipIndent = -1;
            }

            var working = trimmed;
            if (working.StartsWith("- ", StringComparison.Ordinal))
            {
                if (string.Equals(section, "closure_tolerance_profiles", StringComparison.OrdinalIgnoreCase))
                {
                    CommitProfile(current, profiles);
                    current.Clear();
                }

                working = working[2..].TrimStart();
            }

            var splitIndex = working.IndexOf(':');
            if (splitIndex < 0)
            {
                continue;
            }

            var key = working[..splitIndex].Trim();
            var value = working[(splitIndex + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(value))
            {
                skipIndent = indent;
                continue;
            }

            current[key] = value;

            if (string.Equals(section, "closure_tolerance_defaults", StringComparison.OrdinalIgnoreCase))
            {
                catalog.DefaultProfile = ToProfile(current, catalog.DefaultProfile);
            }
        }

        if (string.Equals(section, "closure_tolerance_profiles", StringComparison.OrdinalIgnoreCase))
        {
            CommitProfile(current, profiles);
        }

        catalog.Profiles = profiles;
        return catalog;
    }

    private static void ApplyOverrides(ClosureToleranceCatalog catalog, string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (!document.RootElement.TryGetProperty("closure_tolerance_profile_overrides", out var overrides)
            || overrides.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (overrides.TryGetProperty("default_parcel_type", out var defaultParcelType)
            && defaultParcelType.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(defaultParcelType.GetString()))
        {
            var updatedDefault = catalog.DefaultProfile with { ParcelType = defaultParcelType.GetString()!.Trim() };
            catalog.DefaultProfile = updatedDefault;
        }

        if (!overrides.TryGetProperty("profiles", out var profileOverrides)
            || profileOverrides.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in profileOverrides.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var baseProfile = catalog.Profiles.TryGetValue(property.Name, out var existing)
                ? existing
                : new ClosureToleranceProfile(property.Name, property.Name, property.Name, true, "blocker", false, null, null, null, null);
            catalog.Profiles[property.Name] = ApplyProfileOverride(baseProfile, property.Value);
        }
    }

    private static ClosureToleranceProfile ApplyProfileOverride(ClosureToleranceProfile profile, JsonElement value)
    {
        return profile with
        {
            Enabled = ReadBool(value, "enabled") ?? profile.Enabled,
            Severity = ReadString(value, "severity") ?? profile.Severity,
            AllowOpenBoundary = ReadBool(value, "allow_open_boundary") ?? profile.AllowOpenBoundary,
            MaxClosureDistanceM = ReadDouble(value, "max_closure_distance_m") ?? profile.MaxClosureDistanceM,
            WarningClosureDistanceM = ReadDouble(value, "warning_closure_distance_m") ?? profile.WarningClosureDistanceM,
            MinMiscloseRatioDenominator = ReadDouble(value, "min_misclose_ratio_denominator") ?? profile.MinMiscloseRatioDenominator,
            WarningMiscloseRatioDenominator = ReadDouble(value, "warning_misclose_ratio_denominator") ?? profile.WarningMiscloseRatioDenominator
        };
    }

    private static void CommitProfile(Dictionary<string, string> current, Dictionary<string, ClosureToleranceProfile> profiles)
    {
        if (current.Count == 0)
        {
            return;
        }

        var parcelType = GetValue(current, "parcel_type") ?? GetValue(current, "rule_id");
        if (string.IsNullOrWhiteSpace(parcelType))
        {
            return;
        }

        profiles[parcelType] = ToProfile(current, new ClosureToleranceProfile(
            GetValue(current, "rule_id") ?? parcelType,
            GetValue(current, "title") ?? parcelType,
            parcelType,
            true,
            "blocker",
            false,
            null,
            null,
            null,
            null));
    }

    private static ClosureToleranceProfile ToProfile(Dictionary<string, string> values, ClosureToleranceProfile baseProfile)
    {
        return baseProfile with
        {
            RuleId = GetValue(values, "rule_id") ?? baseProfile.RuleId,
            Title = GetValue(values, "title") ?? baseProfile.Title,
            ParcelType = GetValue(values, "parcel_type") ?? baseProfile.ParcelType,
            Enabled = TryParseBool(GetValue(values, "enabled")) ?? baseProfile.Enabled,
            Severity = GetValue(values, "severity") ?? baseProfile.Severity,
            AllowOpenBoundary = TryParseBool(GetValue(values, "allow_open_boundary")) ?? baseProfile.AllowOpenBoundary,
            MaxClosureDistanceM = TryParseDouble(GetValue(values, "max_closure_distance_m")) ?? baseProfile.MaxClosureDistanceM,
            WarningClosureDistanceM = TryParseDouble(GetValue(values, "warning_closure_distance_m")) ?? baseProfile.WarningClosureDistanceM,
            MinMiscloseRatioDenominator = TryParseDouble(GetValue(values, "min_misclose_ratio_denominator")) ?? baseProfile.MinMiscloseRatioDenominator,
            WarningMiscloseRatioDenominator = TryParseDouble(GetValue(values, "warning_misclose_ratio_denominator")) ?? baseProfile.WarningMiscloseRatioDenominator
        };
    }

    private static string? GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static bool? TryParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

internal readonly record struct CoordinatePoint(double X, double Y);

internal sealed record ParcelReadinessRuleProfile(
    string RuleId,
    string Title,
    string Category,
    string ParcelType,
    bool Enabled,
    string Severity,
    int MinSegmentCount,
    bool RequireContiguousSequence,
    bool RequireReferencedPoints,
    bool RequireChainConsistency,
    bool DetectDuplicateEdges);

internal sealed class ParcelReadinessCatalog
{
    public string DefaultParcelType { get; set; } = "standard_closed";

    public Dictionary<string, ParcelReadinessRuleProfile> DefaultProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Dictionary<string, ParcelReadinessRuleProfile>> ProfilesByParcelType { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ParcelReadinessRuleProfile> Resolve(string parcelType)
    {
        var results = new Dictionary<string, ParcelReadinessRuleProfile>(DefaultProfiles, StringComparer.OrdinalIgnoreCase);
        if (ProfilesByParcelType.TryGetValue(parcelType, out var parcelProfiles))
        {
            foreach (var pair in parcelProfiles)
            {
                results[pair.Key] = pair.Value;
            }
        }

        return results.Values.OrderBy(profile => profile.Category, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ParcelReadinessCatalog Load(string? settingsPath = null)
    {
        settingsPath ??= InnolaTransactionSettings.ResolveActiveSettingsPath();
        var executionSettings = WorkflowExecutionSettings.Load(settingsPath);
        var catalog = ParseRuleCatalog(executionSettings.ValidationRulesPath);
        ApplyOverrides(catalog, settingsPath);
        return catalog;
    }

    private static ParcelReadinessCatalog ParseRuleCatalog(string? rulesPath)
    {
        var catalog = new ParcelReadinessCatalog();
        if (string.IsNullOrWhiteSpace(rulesPath) || !File.Exists(rulesPath))
        {
            return catalog;
        }

        var lines = File.ReadAllLines(rulesPath);
        var section = string.Empty;
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var skipIndent = -1;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            if (indent == 0)
            {
                if (string.Equals(section, "parcel_construction_readiness_profiles", StringComparison.OrdinalIgnoreCase))
                {
                    CommitReadinessProfile(catalog, current);
                    current.Clear();
                }

                section = trimmed.TrimEnd(':');
                skipIndent = -1;
                continue;
            }

            if (skipIndent >= 0 && indent > skipIndent)
            {
                continue;
            }

            if (skipIndent >= 0 && indent <= skipIndent)
            {
                skipIndent = -1;
            }

            var working = trimmed;
            if (working.StartsWith("- ", StringComparison.Ordinal))
            {
                if (string.Equals(section, "parcel_construction_readiness_profiles", StringComparison.OrdinalIgnoreCase))
                {
                    CommitReadinessProfile(catalog, current);
                    current.Clear();
                }

                working = working[2..].TrimStart();
            }

            var splitIndex = working.IndexOf(':');
            if (splitIndex < 0)
            {
                continue;
            }

            var key = working[..splitIndex].Trim();
            var value = working[(splitIndex + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(value))
            {
                skipIndent = indent;
                continue;
            }

            current[key] = value;

            if (string.Equals(section, "parcel_construction_readiness_defaults", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(key, "parcel_type", StringComparison.OrdinalIgnoreCase))
                {
                    catalog.DefaultParcelType = value;
                }
            }
        }

        if (string.Equals(section, "parcel_construction_readiness_profiles", StringComparison.OrdinalIgnoreCase))
        {
            CommitReadinessProfile(catalog, current);
        }

        return catalog;
    }

    private static void CommitReadinessProfile(ParcelReadinessCatalog catalog, Dictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        var category = GetValue(values, "category");
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        var parcelType = GetValue(values, "parcel_type") ?? catalog.DefaultParcelType;
        var profile = new ParcelReadinessRuleProfile(
            GetValue(values, "rule_id") ?? $"readiness_{category}",
            GetValue(values, "title") ?? category,
            category,
            parcelType,
            TryParseBool(GetValue(values, "enabled")) ?? true,
            GetValue(values, "severity") ?? "blocker",
            (int)Math.Round(TryParseDouble(GetValue(values, "min_segment_count")) ?? 3d),
            TryParseBool(GetValue(values, "require_contiguous_sequence")) ?? true,
            TryParseBool(GetValue(values, "require_referenced_points")) ?? true,
            TryParseBool(GetValue(values, "require_chain_consistency")) ?? true,
            TryParseBool(GetValue(values, "detect_duplicate_edges")) ?? true);

        if (!catalog.ProfilesByParcelType.TryGetValue(parcelType, out var parcelProfiles))
        {
            parcelProfiles = new Dictionary<string, ParcelReadinessRuleProfile>(StringComparer.OrdinalIgnoreCase);
            catalog.ProfilesByParcelType[parcelType] = parcelProfiles;
        }

        parcelProfiles[category] = profile;
        if (!catalog.DefaultProfiles.ContainsKey(category) && string.Equals(parcelType, catalog.DefaultParcelType, StringComparison.OrdinalIgnoreCase))
        {
            catalog.DefaultProfiles[category] = profile;
        }
    }

    private static string? GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static bool? TryParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static void ApplyOverrides(ParcelReadinessCatalog catalog, string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (!document.RootElement.TryGetProperty("parcel_construction_readiness_profile_overrides", out var overrides)
            || overrides.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (overrides.TryGetProperty("default_parcel_type", out var defaultParcelType)
            && defaultParcelType.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(defaultParcelType.GetString()))
        {
            catalog.DefaultParcelType = defaultParcelType.GetString()!.Trim();
        }

        if (overrides.TryGetProperty("profiles", out var profiles)
            && profiles.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in profiles.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var parts = property.Name.Split(new[] { "::" }, StringSplitOptions.None);
                if (parts.Length != 2)
                {
                    continue;
                }

                var parcelType = parts[0];
                var category = parts[1];
                if (!catalog.ProfilesByParcelType.TryGetValue(parcelType, out var parcelProfiles)
                    || !parcelProfiles.TryGetValue(category, out var existing))
                {
                    continue;
                }

                var updated = existing with
                {
                    Enabled = ReadBool(property.Value, "enabled") ?? existing.Enabled,
                    Severity = ReadString(property.Value, "severity") ?? existing.Severity,
                    MinSegmentCount = ReadInt(property.Value, "min_segment_count") ?? existing.MinSegmentCount,
                    RequireContiguousSequence = ReadBool(property.Value, "require_contiguous_sequence") ?? existing.RequireContiguousSequence,
                    RequireReferencedPoints = ReadBool(property.Value, "require_referenced_points") ?? existing.RequireReferencedPoints,
                    RequireChainConsistency = ReadBool(property.Value, "require_chain_consistency") ?? existing.RequireChainConsistency,
                    DetectDuplicateEdges = ReadBool(property.Value, "detect_duplicate_edges") ?? existing.DetectDuplicateEdges
                };

                parcelProfiles[category] = updated;
                if (catalog.DefaultProfiles.ContainsKey(category)
                    && string.Equals(parcelType, catalog.DefaultParcelType, StringComparison.OrdinalIgnoreCase))
                {
                    catalog.DefaultProfiles[category] = updated;
                }
            }
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }
}
