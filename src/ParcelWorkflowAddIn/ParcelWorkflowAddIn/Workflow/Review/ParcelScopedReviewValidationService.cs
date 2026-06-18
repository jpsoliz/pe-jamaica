using System.Globalization;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ParcelScopedReviewValidationService
{
    public ParcelScopedReviewValidationResult Validate(
        IReadOnlyList<ExtractionReviewRow> rows,
        string? pendingManualRowId = null,
        bool includePendingManualBarrier = true)
    {
        var issues = new List<string>();
        if (rows.Count == 0)
        {
            issues.Add("No review rows are loaded.");
            return new ParcelScopedReviewValidationResult(issues);
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
                issues.Add($"Parcel {parcelLabel} has duplicate point id(s): {string.Join(", ", duplicatePointIds)}.");
            }

            var invalidSequenceRows = parcelGroup.Count(row => row.SequenceInGroup is null or <= 0);
            if (invalidSequenceRows > 0)
            {
                issues.Add($"Parcel {parcelLabel} has {invalidSequenceRows} row(s) with missing or invalid sequence.");
            }

            var duplicateSequences = parcelGroup
                .Where(row => row.SequenceInGroup.HasValue && row.SequenceInGroup.Value > 0)
                .GroupBy(row => row.SequenceInGroup!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key.ToString(CultureInfo.InvariantCulture))
                .ToArray();
            if (duplicateSequences.Length > 0)
            {
                issues.Add($"Parcel {parcelLabel} has duplicate sequence value(s): {string.Join(", ", duplicateSequences)}.");
            }
        }

        if (includePendingManualBarrier && !string.IsNullOrWhiteSpace(pendingManualRowId))
        {
            issues.Add("Save or discard the in-progress manual point before approval or parcel switching.");
        }

        return new ParcelScopedReviewValidationResult(issues);
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

    private static string NormalizeParcelGroup(string? parcelGroupId)
    {
        return string.IsNullOrWhiteSpace(parcelGroupId) ? "Unassigned" : parcelGroupId.Trim();
    }
}

public sealed record ParcelScopedReviewValidationResult(IReadOnlyList<string> Issues)
{
    public bool HasBlockers => Issues.Count > 0;

    public string SummaryText => !HasBlockers
        ? "Review is complete for this stage."
        : Issues.Count == 1
            ? Issues[0]
            : $"{Issues[0]} {Issues[1]}";
}
