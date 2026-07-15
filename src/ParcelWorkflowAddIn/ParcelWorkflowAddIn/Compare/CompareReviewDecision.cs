using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public static class CompareReviewDecisionValues
{
    public const string Approved = "approved";
    public const string Blocked = "blocked";
    public const string ReturnedToCompute = "returned_to_compute";
    public const string SavedProgress = "saved_progress";
}

public static class CompareReviewReadinessStatus
{
    public const string CommitReady = "commit_ready";
    public const string CommitBlocked = "commit_blocked";
}

public sealed record CompareReviewDecisionDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("task_id")] string TaskId,
    [property: JsonPropertyName("reviewer_id")] string? ReviewerId,
    [property: JsonPropertyName("reviewer_display_name")] string? ReviewerDisplayName,
    [property: JsonPropertyName("decided_at_utc")] string DecidedAtUtc,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("readiness_status")] string ReadinessStatus,
    [property: JsonPropertyName("evidence_refs")] IReadOnlyList<CompareReviewEvidenceRef> EvidenceRefs,
    [property: JsonPropertyName("discrepancies")] IReadOnlyList<CompareReviewDiscrepancySummary> Discrepancies);

public sealed record CompareReviewEvidenceRef(
    [property: JsonPropertyName("evidence_type")] string EvidenceType,
    [property: JsonPropertyName("relative_path")] string? RelativePath,
    [property: JsonPropertyName("summary")] string? Summary);

public sealed record CompareReviewDiscrepancySummary(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("is_resolved")] bool IsResolved,
    [property: JsonPropertyName("is_blocking")] bool IsBlocking);

public sealed record CompareReviewDecisionLoadResult(
    bool Success,
    string Message,
    string? ArtifactPath,
    CompareReviewDecisionDocument? Document)
{
    public static CompareReviewDecisionLoadResult Loaded(string path, CompareReviewDecisionDocument document)
    {
        return new CompareReviewDecisionLoadResult(true, "Compare decision loaded.", path, document);
    }

    public static CompareReviewDecisionLoadResult Failed(string message, string? path = null)
    {
        return new CompareReviewDecisionLoadResult(false, message, path, null);
    }
}

public sealed record CompareCommitReadinessResult(
    bool IsReady,
    string Code,
    string Message,
    CompareReviewDecisionDocument? Decision)
{
    public static CompareCommitReadinessResult Ready(CompareReviewDecisionDocument decision)
    {
        return new CompareCommitReadinessResult(true, "compare_approved", "Compare is approved and current for Commit.", decision);
    }

    public static CompareCommitReadinessResult Blocked(string code, string message, CompareReviewDecisionDocument? decision = null)
    {
        return new CompareCommitReadinessResult(false, code, message, decision);
    }
}

public sealed class CompareReviewDecisionPersistenceService
{
    public const string DecisionArtifactFileName = "compare_review_decision.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string GetDecisionPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, DecisionArtifactFileName);
    }

    public string Save(CaseFolderLayout layout, CompareReviewDecisionDocument document)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var path = GetDecisionPath(layout);
        File.WriteAllText(path, JsonSerializer.Serialize(Redact(document), JsonOptions));
        return path;
    }

    public CompareReviewDecisionDocument? Load(CaseFolderLayout layout)
    {
        var path = GetDecisionPath(layout);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<CompareReviewDecisionDocument>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public CompareReviewDecisionLoadResult LoadForTransaction(CaseFolderLayout layout, SelectedInnolaTransaction transaction)
    {
        var path = GetDecisionPath(layout);
        if (!File.Exists(path))
        {
            return CompareReviewDecisionLoadResult.Failed("Compare decision artifact is missing.", path);
        }

        try
        {
            var document = Load(layout);
            if (document is null)
            {
                return CompareReviewDecisionLoadResult.Failed("Compare decision artifact could not be read.", path);
            }

            if (!MatchesTransaction(document, transaction))
            {
                return CompareReviewDecisionLoadResult.Failed("Compare decision artifact does not belong to the selected transaction.", path);
            }

            return CompareReviewDecisionLoadResult.Loaded(path, document);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return CompareReviewDecisionLoadResult.Failed("Compare decision artifact could not be read.", path);
        }
    }

    public static bool MatchesTransaction(CompareReviewDecisionDocument document, SelectedInnolaTransaction transaction)
    {
        return string.Equals(document.TransactionId, transaction.TransactionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(document.TransactionNumber, transaction.TransactionNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static CompareReviewDecisionDocument Redact(CompareReviewDecisionDocument document)
    {
        return document with
        {
            Notes = LegalCadasterQueryResult.Redact(document.Notes),
            EvidenceRefs = document.EvidenceRefs.Select(reference => reference with
            {
                Summary = LegalCadasterQueryResult.Redact(reference.Summary)
            }).ToArray()
        };
    }
}

public sealed class CompareCommitReadinessService
{
    private readonly CompareReviewDecisionPersistenceService persistence;

    public CompareCommitReadinessService(CompareReviewDecisionPersistenceService? persistence = null)
    {
        this.persistence = persistence ?? new CompareReviewDecisionPersistenceService();
    }

    public CompareCommitReadinessResult CheckReadiness(CaseFolderLayout layout, SelectedInnolaTransaction transaction)
    {
        var load = persistence.LoadForTransaction(layout, transaction);
        if (!load.Success || load.Document is null)
        {
            return CompareCommitReadinessResult.Blocked("compare_decision_missing", load.Message);
        }

        var decision = load.Document;
        if (!string.Equals(decision.SchemaVersion, "1.0.0", StringComparison.OrdinalIgnoreCase)
            || !DateTimeOffset.TryParse(decision.DecidedAtUtc, out _))
        {
            return CompareCommitReadinessResult.Blocked(
                "compare_decision_stale",
                "Commit is blocked because the Compare decision artifact is stale or unreadable.",
                decision);
        }

        if (!string.Equals(decision.Decision, CompareReviewDecisionValues.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return CompareCommitReadinessResult.Blocked(
                "compare_decision_not_approved",
                "Commit is blocked until Compare is approved.",
                decision);
        }

        if (!string.Equals(decision.ReadinessStatus, CompareReviewReadinessStatus.CommitReady, StringComparison.OrdinalIgnoreCase))
        {
            return CompareCommitReadinessResult.Blocked(
                "compare_decision_not_ready",
                "Commit is blocked until Compare readiness is marked commit-ready.",
                decision);
        }

        if (decision.Discrepancies.Any(discrepancy => discrepancy.IsBlocking && !discrepancy.IsResolved))
        {
            return CompareCommitReadinessResult.Blocked(
                "compare_unresolved_blockers",
                "Commit is blocked because Compare has unresolved blocking discrepancies.",
                decision);
        }

        foreach (var evidenceRef in decision.EvidenceRefs.Where(reference => !string.IsNullOrWhiteSpace(reference.RelativePath)))
        {
            var path = Path.Combine(layout.RootDirectory, evidenceRef.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                return CompareCommitReadinessResult.Blocked(
                    "compare_evidence_ref_missing",
                    "Commit is blocked because Compare decision evidence references are missing.",
                    decision);
            }
        }

        return CompareCommitReadinessResult.Ready(decision);
    }
}
