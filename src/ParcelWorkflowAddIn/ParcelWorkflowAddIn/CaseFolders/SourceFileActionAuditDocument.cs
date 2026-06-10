using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record SourceFileActionAuditDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("events")] IReadOnlyList<SourceFileActionAuditEvent> Events);

public sealed record SourceFileActionAuditEvent(
    [property: JsonPropertyName("recorded_at")] string RecordedAt,
    [property: JsonPropertyName("operator_id")] string OperatorId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("source_file_name")] string SourceFileName,
    [property: JsonPropertyName("copied_path")] string? CopiedPath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message);
