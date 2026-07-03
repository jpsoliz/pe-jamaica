using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Preflight;

public sealed record PreflightSummaryDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("stage_id")] string? StageId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("created_by")] string? CreatedBy,
    [property: JsonPropertyName("source_manifest_hash")] string SourceManifestHash,
    [property: JsonPropertyName("payload")] PreflightSummaryPayload Payload,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);

public sealed record PreflightSummaryPayload(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("blockers")] IReadOnlyList<PreflightCheck> Blockers,
    [property: JsonPropertyName("warnings")] IReadOnlyList<PreflightCheck> Warnings,
    [property: JsonPropertyName("passed_checks")] IReadOnlyList<PreflightCheck> PassedChecks);
