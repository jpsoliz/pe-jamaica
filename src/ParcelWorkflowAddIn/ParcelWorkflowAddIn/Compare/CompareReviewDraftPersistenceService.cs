using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareReviewDraftPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string DraftFileName => "compare_review_draft.json";

    public CompareReviewDraftDocument? Load(CaseFolderLayout layout)
    {
        var path = GetDraftPath(layout);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CompareReviewDraftDocument>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public CompareReviewDraftSaveResult Save(CaseFolderLayout layout, CompareReviewDraftDocument draft)
    {
        Directory.CreateDirectory(layout.WorkingDirectory);
        var path = GetDraftPath(layout);
        var document = draft with
        {
            SchemaVersion = string.IsNullOrWhiteSpace(draft.SchemaVersion) ? "1.0.0" : draft.SchemaVersion,
            SavedAtUtc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
        };
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        return new CompareReviewDraftSaveResult(true, "Compare progress saved.", path, document);
    }

    public string GetDraftPath(CaseFolderLayout layout)
    {
        return Path.Combine(layout.WorkingDirectory, DraftFileName);
    }
}

public sealed record CompareReviewDraftSaveResult(
    bool Success,
    string Message,
    string? Path,
    CompareReviewDraftDocument? Document);

public sealed record CompareReviewDraftDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_number")] string TransactionNumber,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("decision_state")] string DecisionState,
    [property: JsonPropertyName("saved_at_utc")] string? SavedAtUtc,
    [property: JsonPropertyName("survey_plan_summary")] string SurveyPlanSummary,
    [property: JsonPropertyName("legal_cadaster_summary")] string LegalCadasterSummary,
    [property: JsonPropertyName("fiscal_neighbor_summary")] string FiscalNeighborSummary,
    [property: JsonPropertyName("discrepancies")] IReadOnlyList<CompareDiscrepancyDraft> Discrepancies,
    [property: JsonPropertyName("transaction_id")] string? TransactionId = null,
    [property: JsonPropertyName("task_id")] string? TaskId = null,
    [property: JsonPropertyName("reviewer_id")] string? ReviewerId = null,
    [property: JsonPropertyName("reviewer_display_name")] string? ReviewerDisplayName = null,
    [property: JsonPropertyName("legal_evidence_reviewed")] bool LegalEvidenceReviewed = false,
    [property: JsonPropertyName("fiscal_evidence_reviewed")] bool FiscalEvidenceReviewed = false,
    [property: JsonPropertyName("manual_query_history")] IReadOnlyList<CompareEvidenceSearchResultDraft>? ManualQueryHistory = null,
    [property: JsonPropertyName("valuable_evidence")] IReadOnlyList<CompareValuableEvidenceDraft>? ValuableEvidence = null);

public sealed record CompareDiscrepancyDraft(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("is_resolved")] bool IsResolved);

public sealed record CompareEvidenceSearchResultDraft(
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("source_label")] string SourceLabel,
    [property: JsonPropertyName("query_key")] string QueryKey,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("party_role")] string? PartyRole,
    [property: JsonPropertyName("parcel_id")] string? ParcelId,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("folio")] string? Folio,
    [property: JsonPropertyName("land_valuation_number")] string? LandValuationNumber,
    [property: JsonPropertyName("parish")] string? Parish,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("queried_at_utc")] string QueriedAtUtc,
    [property: JsonPropertyName("diagnostic")] string? Diagnostic)
{
    public static CompareEvidenceSearchResultDraft FromModel(CompareEvidenceSearchResult result)
    {
        return new CompareEvidenceSearchResultDraft(
            result.SourceType,
            result.SourceLabel,
            result.QueryKey,
            result.DisplayName,
            result.PartyRole,
            result.ParcelId,
            result.Volume,
            result.Folio,
            result.LandValuationNumber,
            result.Parish,
            result.Status,
            result.QueriedAt.UtcDateTime.ToString("O"),
            result.Diagnostic);
    }

    public CompareEvidenceSearchResult ToModel()
    {
        return new CompareEvidenceSearchResult(
            SourceType,
            SourceLabel,
            QueryKey,
            DisplayName,
            PartyRole,
            ParcelId,
            Volume,
            Folio,
            LandValuationNumber,
            Parish,
            Status,
            DateTimeOffset.TryParse(QueriedAtUtc, out var queriedAt) ? queriedAt : DateTimeOffset.MinValue,
            Diagnostic);
    }
}

public sealed record CompareValuableEvidenceDraft(
    [property: JsonPropertyName("evidence_id")] string EvidenceId,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("source_label")] string SourceLabel,
    [property: JsonPropertyName("query_key")] string QueryKey,
    [property: JsonPropertyName("display_summary")] string DisplaySummary,
    [property: JsonPropertyName("role_tag")] string RoleTag,
    [property: JsonPropertyName("captured_at_utc")] string CapturedAtUtc,
    [property: JsonPropertyName("diagnostic")] string? Diagnostic)
{
    public static CompareValuableEvidenceDraft FromModel(CompareValuableEvidence evidence)
    {
        return new CompareValuableEvidenceDraft(
            evidence.EvidenceId,
            evidence.SourceType,
            evidence.SourceLabel,
            evidence.QueryKey,
            evidence.DisplaySummary,
            evidence.RoleTag,
            evidence.CapturedAt.UtcDateTime.ToString("O"),
            evidence.Diagnostic);
    }

    public CompareValuableEvidence ToModel()
    {
        return new CompareValuableEvidence(
            EvidenceId,
            SourceType,
            SourceLabel,
            QueryKey,
            DisplaySummary,
            RoleTag,
            DateTimeOffset.TryParse(CapturedAtUtc, out var capturedAt) ? capturedAt : DateTimeOffset.MinValue,
            Diagnostic);
    }
}
