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

    public JsonObject RootMetadata { get; set; } = [];
}

public sealed class ExtractionReviewRow
{
    public string RowId { get; set; } = string.Empty;

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
