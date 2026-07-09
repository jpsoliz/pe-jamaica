using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class ExtractionReviewDocument
{
    public string SchemaVersion { get; set; } = "1.0.0";

    public string TransactionNumber { get; set; } = string.Empty;

    public int ReviewVersion { get; set; }

    public string ReviewHash { get; set; } = string.Empty;

    public string? LastSavedAt { get; set; }

    public string? LastSavedBy { get; set; }

    public string? ExtractionSource { get; set; }

    public int RowCount { get; set; }

    public int SegmentRowCount { get; set; }

    public List<string> Errors { get; } = [];

    public List<ExtractionReviewRow> Rows { get; } = [];

    public List<ExtractionReviewSegment> Segments { get; } = [];

    public List<ExtractionReviewMetadataField> SurveyMetadataFields { get; } = [];

    public List<ExtractionReviewAdjacentOwner> AdjacentOwners { get; } = [];

    public List<ExtractionReviewNamedParty> Parties { get; } = [];

    public List<ExtractionReviewNamedParty> Representatives { get; } = [];

    public List<ExtractionReviewVolumeFolio> VolumeFolios { get; } = [];

    public JsonObject RootMetadata { get; set; } = [];
}

public sealed class ExtractionReviewMetadataField
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;

    public string Confidence { get; set; } = string.Empty;

    public string SourcePage { get; set; } = string.Empty;

    public string SourceZone { get; set; } = string.Empty;

    public string ReviewStatus { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public bool? Present { get; set; }

    public string OriginalValue { get; set; } = string.Empty;

    public string OriginalRawText { get; set; } = string.Empty;

    public bool? OriginalPresent { get; set; }

    public JsonObject RawField { get; set; } = [];

    public bool IsEdited =>
        !string.Equals(Value?.Trim(), OriginalValue?.Trim(), StringComparison.Ordinal)
        || !string.Equals(RawText?.Trim(), OriginalRawText?.Trim(), StringComparison.Ordinal)
        || Present != OriginalPresent
        || !string.IsNullOrWhiteSpace(ReviewStatus)
        || !string.IsNullOrWhiteSpace(ReviewNotes);
}

public sealed class ExtractionReviewAdjacentOwner
{
    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string RelatedSegmentFrom { get; set; } = string.Empty;

    public string RelatedSegmentTo { get; set; } = string.Empty;

    public string Volume { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public string SourcePage { get; set; } = string.Empty;

    public string SourceZone { get; set; } = string.Empty;

    public string ReviewStatus { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public JsonObject RawOwner { get; set; } = [];
}

public sealed class ExtractionReviewNamedParty
{
    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string SourcePage { get; set; } = string.Empty;

    public string SourceZone { get; set; } = string.Empty;

    public string ReviewStatus { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public JsonObject RawParty { get; set; } = [];
}

public sealed class ExtractionReviewVolumeFolio
{
    public string Volume { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;

    public string SourcePage { get; set; } = string.Empty;

    public string SourceZone { get; set; } = string.Empty;

    public string ReviewStatus { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public JsonObject RawVolumeFolio { get; set; } = [];
}

public sealed class ExtractionReviewSegment
{
    public string SegmentId { get; set; } = string.Empty;

    public int? Sequence { get; set; }

    public string FromPoint { get; set; } = string.Empty;

    public string ToPoint { get; set; } = string.Empty;

    public string BearingText { get; set; } = string.Empty;

    public string DistanceText { get; set; } = string.Empty;

    public string LengthText { get; set; } = string.Empty;

    public bool IncludeInBoundary { get; set; } = true;

    public string Confidence { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string SourcePage { get; set; } = string.Empty;

    public string SourceZone { get; set; } = string.Empty;

    public string SourceEvidence { get; set; } = string.Empty;

    public string ReviewFromPoint { get; set; } = string.Empty;

    public string ReviewToPoint { get; set; } = string.Empty;

    public string ReviewBearingText { get; set; } = string.Empty;

    public string ReviewDistanceText { get; set; } = string.Empty;

    public string ReviewLengthText { get; set; } = string.Empty;

    public int? ReviewSequence { get; set; }

    public bool? ReviewIncludeInBoundary { get; set; }

    public string ReviewStatus { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public string AdjacentOwner { get; set; } = string.Empty;

    public bool IsEdited { get; set; }

    public ExtractionReviewSegmentOriginalValues OriginalValues { get; set; } = new();

    public JsonObject RawSegment { get; set; } = [];

    public int EffectiveSequence => ReviewSequence ?? Sequence ?? int.MaxValue;

    public string EffectiveFromPoint => string.IsNullOrWhiteSpace(ReviewFromPoint) ? FromPoint : ReviewFromPoint;

    public string EffectiveToPoint => string.IsNullOrWhiteSpace(ReviewToPoint) ? ToPoint : ReviewToPoint;

    public string EffectiveBearingText => string.IsNullOrWhiteSpace(ReviewBearingText) ? BearingText : ReviewBearingText;

    public string EffectiveDistanceText => string.IsNullOrWhiteSpace(ReviewDistanceText) ? DistanceText : ReviewDistanceText;

    public string EffectiveLengthText => string.IsNullOrWhiteSpace(ReviewLengthText) ? LengthText : ReviewLengthText;

    public bool EffectiveIncludeInBoundary => ReviewIncludeInBoundary ?? IncludeInBoundary;
}

public sealed class ExtractionReviewSegmentOriginalValues
{
    public int? Sequence { get; set; }

    public string FromPoint { get; set; } = string.Empty;

    public string ToPoint { get; set; } = string.Empty;

    public string BearingText { get; set; } = string.Empty;

    public string DistanceText { get; set; } = string.Empty;

    public string LengthText { get; set; } = string.Empty;

    public bool IncludeInBoundary { get; set; } = true;
}

public sealed class ExtractionReviewRow
{
    public string RowId { get; set; } = string.Empty;

    public string ParcelGroupId { get; set; } = string.Empty;

    public string ParcelName { get; set; } = string.Empty;

    public string TraverseId { get; set; } = string.Empty;

    public int? SequenceInGroup { get; set; }

    public bool IsBoundaryBreak { get; set; }

    public string GroupConfidence { get; set; } = string.Empty;

    public string PointIdentifier { get; set; } = string.Empty;

    public string Easting { get; set; } = string.Empty;

    public string Northing { get; set; } = string.Empty;

    public string Length { get; set; } = string.Empty;

    public string ExtractionStatus { get; set; } = string.Empty;

    public string SourceEvidence { get; set; } = string.Empty;

    public bool Unresolved { get; set; }

    public string UnresolvedReason { get; set; } = string.Empty;

    public string ReviewNotes { get; set; } = string.Empty;

    public string RowProvenance { get; set; } = "extracted";

    public bool IsManual { get; set; }

    public bool IsEdited { get; set; }

    public ExtractionReviewOriginalValues OriginalValues { get; set; } = new();

    public JsonObject RawRow { get; set; } = [];
}

public sealed class ExtractionReviewOriginalValues
{
    public string PointIdentifier { get; set; } = string.Empty;

    public string Easting { get; set; } = string.Empty;

    public string Northing { get; set; } = string.Empty;

    public string Length { get; set; } = string.Empty;

    public string ExtractionStatus { get; set; } = string.Empty;

    public string SourceEvidence { get; set; } = string.Empty;
}

public sealed record ExtractionReviewSummary(
    int TotalRows,
    int EditedRows,
    int ManualRows,
    int UnresolvedRows,
    int MissingRequiredRows)
{
    public bool CanApprove => UnresolvedRows == 0 && MissingRequiredRows == 0 && TotalRows > 0;
}

public sealed record ExtractionReviewSaveResult(
    bool Success,
    string Message,
    ExtractionReviewDocument? Document,
    ExtractionReviewSummary? Summary)
{
    public static ExtractionReviewSaveResult Failed(string message) => new(false, message, null, null);
}

public sealed record ExtractionReviewApprovalResult(
    bool Success,
    string Message,
    ExtractionReviewSummary? Summary,
    string? ApprovedReviewPath)
{
    public static ExtractionReviewApprovalResult Failed(string message, ExtractionReviewSummary? summary) =>
        new(false, message, summary, null);
}
