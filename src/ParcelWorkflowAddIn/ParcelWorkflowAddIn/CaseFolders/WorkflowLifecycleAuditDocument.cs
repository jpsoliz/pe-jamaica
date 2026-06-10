using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record WorkflowLifecycleAuditDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("events")] IReadOnlyList<WorkflowLifecycleAuditEvent> Events);

public sealed record WorkflowLifecycleAuditEvent(
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("task_id")] string? TaskId,
    [property: JsonPropertyName("transaction_number")] string? TransactionNumber,
    [property: JsonPropertyName("error_category")] string? ErrorCategory);
