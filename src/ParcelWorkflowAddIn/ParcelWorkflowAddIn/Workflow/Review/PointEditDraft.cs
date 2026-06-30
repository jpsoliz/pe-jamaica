using System.Globalization;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class PointEditDraft
{
    public string RowId { get; set; } = string.Empty;

    public string ParcelGroupId { get; set; } = string.Empty;

    public string ParcelName { get; set; } = string.Empty;

    public string TraverseId { get; set; } = string.Empty;

    public int SequenceInGroup { get; set; }

    public bool IsNewPoint { get; set; }

    public bool IsManualRow { get; set; }

    public string PointIdentifier { get; set; } = string.Empty;

    public string Easting { get; set; } = string.Empty;

    public string Northing { get; set; } = string.Empty;

    public string Length { get; set; } = string.Empty;

    public string ExtractionStatus { get; set; } = string.Empty;

    public string SourceEvidence { get; set; } = string.Empty;

    public bool Unresolved { get; set; }

    public string UnresolvedReason { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public static PointEditDraft CreateForNew(ExtractionReviewRow row)
    {
        return new PointEditDraft
        {
            RowId = row.RowId,
            ParcelGroupId = row.ParcelGroupId,
            ParcelName = row.ParcelName,
            TraverseId = row.TraverseId,
            SequenceInGroup = row.SequenceInGroup ?? 0,
            IsNewPoint = true,
            IsManualRow = row.IsManual,
            PointIdentifier = row.PointIdentifier,
            Easting = row.Easting,
            Northing = row.Northing,
            Length = row.Length,
            ExtractionStatus = row.ExtractionStatus,
            SourceEvidence = row.SourceEvidence,
            Unresolved = row.Unresolved,
            UnresolvedReason = row.UnresolvedReason,
            ReviewNotes = row.ReviewNotes
        };
    }

    public static PointEditDraft CreateForEdit(ExtractionReviewRow row)
    {
        return new PointEditDraft
        {
            RowId = row.RowId,
            ParcelGroupId = row.ParcelGroupId,
            ParcelName = row.ParcelName,
            TraverseId = row.TraverseId,
            SequenceInGroup = row.SequenceInGroup ?? 0,
            IsNewPoint = false,
            IsManualRow = row.IsManual,
            PointIdentifier = row.PointIdentifier,
            Easting = row.Easting,
            Northing = row.Northing,
            Length = row.Length,
            ExtractionStatus = row.ExtractionStatus,
            SourceEvidence = row.SourceEvidence,
            Unresolved = row.Unresolved,
            UnresolvedReason = row.UnresolvedReason,
            ReviewNotes = row.ReviewNotes
        };
    }

    public IReadOnlyList<string> Validate(IEnumerable<ExtractionReviewRowViewModel> existingRows)
    {
        var errors = new List<string>();
        var pointIdentifier = Normalize(PointIdentifier);
        var easting = Normalize(Easting);
        var northing = Normalize(Northing);
        var length = Normalize(Length);

        if (string.IsNullOrWhiteSpace(pointIdentifier))
        {
            errors.Add("Point identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(easting))
        {
            errors.Add("Easting is required.");
        }
        else if (!IsNumeric(easting))
        {
            errors.Add("Easting must be numeric.");
        }

        if (string.IsNullOrWhiteSpace(northing))
        {
            errors.Add("Northing is required.");
        }
        else if (!IsNumeric(northing))
        {
            errors.Add("Northing must be numeric.");
        }

        if (!string.IsNullOrWhiteSpace(length) && !IsNumeric(length))
        {
            errors.Add("Length must be numeric when provided.");
        }

        if (Unresolved && string.IsNullOrWhiteSpace(Normalize(UnresolvedReason)))
        {
            errors.Add("Provide an unresolved reason or clear the unresolved flag.");
        }

        var duplicatePointId = existingRows.Any(row =>
            !string.Equals(row.RowId, RowId, StringComparison.Ordinal)
            && string.Equals(Normalize(row.ParcelGroupId), Normalize(ParcelGroupId), StringComparison.OrdinalIgnoreCase)
            && string.Equals(Normalize(row.PointIdentifier), pointIdentifier, StringComparison.OrdinalIgnoreCase));

        if (duplicatePointId)
        {
            errors.Add($"Point identifier '{pointIdentifier}' is already used in parcel {Normalize(ParcelGroupId)}.");
        }

        return errors;
    }

    public void ApplyTo(ExtractionReviewRow row)
    {
        row.PointIdentifier = Normalize(PointIdentifier);
        row.Easting = Normalize(Easting);
        row.Northing = Normalize(Northing);
        row.Length = Normalize(Length);
        row.ExtractionStatus = Normalize(ExtractionStatus);
        row.SourceEvidence = Normalize(SourceEvidence);
        row.Unresolved = Unresolved;
        row.UnresolvedReason = Normalize(UnresolvedReason);
        row.ReviewNotes = Normalize(ReviewNotes);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool IsNumeric(string value)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _)
            || double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out _);
    }
}
