using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public static class ValidationFindingDispositionDecision
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Override = "override";
    public const string ManualReview = "manual_review";
}

public sealed record ValidationFindingDispositionDocument(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc,
    [property: JsonPropertyName("items")] IReadOnlyList<ValidationFindingDispositionItem> Items);

public sealed record ValidationFindingDispositionItem(
    [property: JsonPropertyName("finding_key")] string FindingKey,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("operator_id")] string? OperatorId,
    [property: JsonPropertyName("decided_at_utc")] string DecidedAtUtc,
    [property: JsonPropertyName("evidence_ref")] string? EvidenceRef);

public sealed class ValidationFindingDispositionRow
{
    public ValidationFindingDispositionRow(
        string findingKey,
        string ruleId,
        string title,
        string severity,
        string status,
        string evidence,
        string recommendedAction,
        string decision,
        string decidedAtUtc,
        string comment)
    {
        FindingKey = findingKey;
        RuleId = ruleId;
        Title = title;
        Severity = severity;
        Status = status;
        Evidence = evidence;
        RecommendedAction = recommendedAction;
        Decision = decision;
        DecidedAtUtc = decidedAtUtc;
        Comment = comment;
    }

    public string FindingKey { get; }

    public string RuleId { get; }

    public string Title { get; }

    public string Severity { get; }

    public string Status { get; }

    public string Evidence { get; }

    public string RecommendedAction { get; }

    public string Decision { get; }

    public string DecidedAtUtc { get; }

    public string Comment { get; set; }

    public bool HasDisposition => !string.Equals(Decision, ValidationFindingDispositionDecision.Pending, StringComparison.OrdinalIgnoreCase);

    public string DecisionLabel => HasDisposition ? Decision.Replace('_', ' ') : "pending";
}

public static class ValidationFindingDispositionProjector
{
    public static IReadOnlyList<ValidationFindingDispositionRow> BuildRows(
        ValidationSummaryDocument? summary,
        ValidationFindingDispositionDocument? disposition)
    {
        if (summary is null || summary.Payload.Findings.Count == 0)
        {
            return Array.Empty<ValidationFindingDispositionRow>();
        }

        var dispositionByKey = (disposition?.Items ?? Array.Empty<ValidationFindingDispositionItem>())
            .GroupBy(item => item.FindingKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return summary.Payload.Findings
            .Select(finding => BuildRow(finding, dispositionByKey))
            .ToArray();
    }

    public static string BuildFindingKey(ValidationFinding finding)
    {
        return string.Join("|", finding.RuleId.Trim(), finding.Title.Trim(), finding.Severity.Trim(), finding.Status.Trim());
    }

    private static ValidationFindingDispositionRow BuildRow(
        ValidationFinding finding,
        IReadOnlyDictionary<string, ValidationFindingDispositionItem> dispositionByKey)
    {
        var key = BuildFindingKey(finding);
        dispositionByKey.TryGetValue(key, out var disposition);
        return new ValidationFindingDispositionRow(
            key,
            finding.RuleId,
            finding.Title,
            finding.Severity,
            finding.Status,
            finding.Evidence ?? string.Empty,
            finding.RecommendedAction ?? string.Empty,
            disposition?.Decision ?? ValidationFindingDispositionDecision.Pending,
            disposition?.DecidedAtUtc ?? string.Empty,
            disposition?.Comment ?? string.Empty);
    }
}


