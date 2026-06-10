using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Contracts;

public sealed record ManifestDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("created_by")] string? CreatedBy,
    [property: JsonPropertyName("source_manifest_hash")] string? SourceManifestHash,
    [property: JsonPropertyName("payload")] ManifestPayload Payload,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors)
{
    public static ManifestDocument CreateInitial(
        string transactionId,
        string runId,
        DateTimeOffset createdAt,
        string? createdBy)
    {
        return new ManifestDocument(
            "1.0.0",
            transactionId,
            runId,
            createdAt.UtcDateTime.ToString("O"),
            createdBy,
            null,
            new ManifestPayload("intake", Array.Empty<ManifestSourceFile>(), null, null, null),
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}

public sealed record ManifestPayload(
    [property: JsonPropertyName("workflow_state")] string WorkflowState,
    [property: JsonPropertyName("source_files")] IReadOnlyList<ManifestSourceFile> SourceFiles,
    [property: JsonPropertyName("detected_profile")] DetectedSourceInputProfile? DetectedProfile,
    [property: JsonPropertyName("innola_transaction")] ManifestInnolaTransaction? InnolaTransaction = null,
    [property: JsonPropertyName("attachment_provenance")] IReadOnlyList<ManifestAttachmentProvenance>? AttachmentProvenance = null);

public sealed record ManifestSourceFile(
    [property: JsonPropertyName("original_path")] string OriginalPath,
    [property: JsonPropertyName("copied_path")] string CopiedPath,
    [property: JsonPropertyName("file_type")] string FileType,
    [property: JsonPropertyName("file_size")] long FileSize,
    [property: JsonPropertyName("copied_at")] string CopiedAt,
    [property: JsonPropertyName("source_role")] string? SourceRole);

public sealed record ManifestInnolaTransaction(
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string TaskId,
    [property: JsonPropertyName("task_name")] string TaskName,
    [property: JsonPropertyName("process_step")] string ProcessStep,
    [property: JsonPropertyName("case_type")] string? CaseType,
    [property: JsonPropertyName("profile_hint")] string? ProfileHint,
    [property: JsonPropertyName("selected_user")] string SelectedUser,
    [property: JsonPropertyName("assigned_user")] string? AssignedUser,
    [property: JsonPropertyName("assigned_group")] string? AssignedGroup,
    [property: JsonPropertyName("owner_user")] string? OwnerUser,
    [property: JsonPropertyName("claim_status")] string? ClaimStatus,
    [property: JsonPropertyName("loaded_at")] string LoadedAt);

public sealed record ManifestAttachmentProvenance(
    [property: JsonPropertyName("attachment_id")] string AttachmentId,
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("extension")] string Extension,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("source_role")] string? SourceRole,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("file_size")] long? FileSize,
    [property: JsonPropertyName("checksum")] string? Checksum,
    [property: JsonPropertyName("service_reference")] string ServiceReference,
    [property: JsonPropertyName("copied_path")] string CopiedPath,
    [property: JsonPropertyName("copied_at")] string CopiedAt);
