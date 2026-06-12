using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public sealed record ValidationSummaryDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("created_by")] string? CreatedBy,
    [property: JsonPropertyName("source_manifest_hash")] string SourceManifestHash,
    [property: JsonPropertyName("payload")] ValidationSummaryPayload Payload,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);

public sealed record ValidationSummaryPayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rule_profile")] string RuleProfile,
    [property: JsonPropertyName("rule_version")] string RuleVersion,
    [property: JsonPropertyName("finding_counts")] ValidationFindingCounts FindingCounts,
    [property: JsonPropertyName("findings")] IReadOnlyList<ValidationFinding> Findings);

public sealed record ValidationFindingCounts(
    [property: JsonPropertyName("critical")] int Critical,
    [property: JsonPropertyName("high")] int High,
    [property: JsonPropertyName("warning")] int Warning,
    [property: JsonPropertyName("info")] int Info,
    [property: JsonPropertyName("passed")] int Passed);

public sealed record ValidationFinding(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("evidence")] string? Evidence,
    [property: JsonPropertyName("recommended_action")] string? RecommendedAction);

public sealed record ValidationExecutionResult(
    bool Success,
    string? ErrorMessage,
    string? SummaryPath,
    ValidationSummaryDocument? Summary)
{
    public static ValidationExecutionResult Failed(string message)
    {
        return new ValidationExecutionResult(false, message, null, null);
    }
}
