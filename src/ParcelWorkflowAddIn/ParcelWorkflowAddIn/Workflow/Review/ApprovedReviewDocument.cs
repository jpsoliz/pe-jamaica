using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed record ApprovedReviewDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("review_version")] int ReviewVersion,
    [property: JsonPropertyName("review_hash")] string ReviewHash,
    [property: JsonPropertyName("approved_at")] string ApprovedAt,
    [property: JsonPropertyName("approved_by")] string? ApprovedBy,
    [property: JsonPropertyName("row_count")] int RowCount,
    [property: JsonPropertyName("edited_row_count")] int EditedRowCount,
    [property: JsonPropertyName("manual_row_count")] int ManualRowCount,
    [property: JsonPropertyName("unresolved_row_count")] int UnresolvedRowCount,
    [property: JsonPropertyName("missing_required_row_count")] int MissingRequiredRowCount,
    [property: JsonPropertyName("source_artifact")] string SourceArtifact);
