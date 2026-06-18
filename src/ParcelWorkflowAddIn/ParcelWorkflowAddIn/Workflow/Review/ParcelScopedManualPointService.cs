namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ParcelScopedManualPointService
{
    public ExtractionReviewRow CreateManualRow(
        ExtractionReviewDocument document,
        string parcelGroupId,
        string? parcelName,
        string? traverseId)
    {
        var normalizedGroupId = NormalizeRequired(parcelGroupId, "parcel");
        var normalizedParcelName = NormalizeOptional(parcelName) ?? normalizedGroupId;
        var normalizedTraverseId = NormalizeOptional(traverseId) ?? normalizedGroupId;
        var nextSequence = document.Rows
            .Where(row => string.Equals(row.ParcelGroupId, normalizedGroupId, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.SequenceInGroup ?? 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return new ExtractionReviewRow
        {
            RowId = $"manual-{NormalizeToken(normalizedGroupId)}-{nextSequence:000}",
            ParcelGroupId = normalizedGroupId,
            ParcelName = normalizedParcelName,
            TraverseId = normalizedTraverseId,
            SequenceInGroup = nextSequence,
            GroupConfidence = "manual_current_parcel",
            PointIdentifier = $"{normalizedGroupId}_P{nextSequence}",
            Easting = string.Empty,
            Northing = string.Empty,
            Length = string.Empty,
            ExtractionStatus = "Manual entry",
            SourceEvidence = "Manual correction",
            RowProvenance = "manual",
            IsManual = true,
            IsEdited = true,
            OriginalValues = new ExtractionReviewOriginalValues()
        };
    }

    private static string NormalizeRequired(string? value, string fallback)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeToken(string value)
    {
        return value
            .Trim()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal);
    }
}
