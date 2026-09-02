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

    public string DisplayRuleName => BuildDisplayRuleName(RuleId);

    public string DisplayFinding => BuildDisplayFinding(RuleId, Title, Status, Evidence, RecommendedAction);

    public string DisplayStatus => BuildDisplayStatus(Status);

    public bool HasDisposition => !string.Equals(Decision, ValidationFindingDispositionDecision.Pending, StringComparison.OrdinalIgnoreCase);

    public string DecisionLabel => HasDisposition ? Decision.Replace('_', ' ') : "pending";

    private static string BuildDisplayRuleName(string ruleId)
    {
        return ruleId switch
        {
            "georeference.parish_point_within_boundary" => "Points within Parish",
            "spatial_units.parish_polygon_within_boundary" => "Parcel Boundary within Parish",
            "pxa.embedded_compute_sheet_detected" => "Embedded Compute Sheet",
            "pxa.plan_compute_sheet_consistency" => "Plan and Compute Sheet Match",
            "document.printed_text_height" => "Printed Text Height",
            _ => string.IsNullOrWhiteSpace(ruleId) ? "Validation Point" : ruleId.Replace('_', ' ')
        };
    }

    private static string BuildDisplayStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "passed" => "Found",
            "found" => "Found",
            "blocker" => "Not found",
            "failed" => "Not found",
            "needs_review" => "Not found",
            "warning" => "Not found",
            "not_available" => "Not available",
            "n/a" => "Not available",
            "skipped" => "Not available",
            "disabled" => "Not available",
            "" => "Not available",
            _ => status
        };
    }

    private static string BuildDisplayFinding(
        string ruleId,
        string title,
        string status,
        string evidence,
        string recommendedAction)
    {
        if (string.Equals(ruleId, "georeference.parish_point_within_boundary", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPointWithinParishFinding(status, evidence);
        }

        if (string.Equals(ruleId, "spatial_units.parish_polygon_within_boundary", StringComparison.OrdinalIgnoreCase))
        {
            return BuildParcelBoundaryWithinParishFinding(status, evidence);
        }

        if (string.Equals(ruleId, "pxa.embedded_compute_sheet_detected", StringComparison.OrdinalIgnoreCase))
        {
            return BuildEmbeddedComputeSheetFinding(status, evidence);
        }

        if (string.Equals(ruleId, "pxa.plan_compute_sheet_consistency", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPlanComputeSheetMatchFinding(status, evidence);
        }

        if (string.Equals(ruleId, "document.printed_text_height", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPrintedTextHeightFinding(status, evidence);
        }

        if (BuildDisplayStatus(status) == "Not available")
        {
            return string.IsNullOrWhiteSpace(evidence) || string.Equals(evidence, "N/A", StringComparison.OrdinalIgnoreCase)
                ? "Validation data is not available yet."
                : evidence;
        }

        if (!string.IsNullOrWhiteSpace(title) && !string.Equals(title, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return title;
        }

        return string.IsNullOrWhiteSpace(recommendedAction) ? "Validation result is available for examiner review." : recommendedAction;
    }

    private static string BuildPointWithinParishFinding(string status, string evidence)
    {
        var values = ParseEvidence(evidence);
        values.TryGetValue("parish", out var parish);
        values.TryGetValue("outside_points", out var outsidePoints);
        values.TryGetValue("parcel", out var parcel);
        values.TryGetValue("parcel_name", out var parcelName);
        values.TryGetValue("parcel_group_id", out var parcelGroupId);

        var parishText = string.IsNullOrWhiteSpace(parish) ? "the parish" : $"the {parish} parish";
        var parcelText = FirstAvailable(parcelName, parcel, parcelGroupId) is { Length: > 0 } name
            ? $"parcel {name}"
            : "the parcel";
        var displayStatus = BuildDisplayStatus(status);

        if (displayStatus == "Found")
        {
            return $"All boundary points from {parcelText} are located inside {parishText}.";
        }

        if (displayStatus == "Not found")
        {
            var points = string.IsNullOrWhiteSpace(outsidePoints) || string.Equals(outsidePoints, "none", StringComparison.OrdinalIgnoreCase)
                ? "one or more boundary points"
                : $"the following points [{outsidePoints}]";
            return $"{points} are located outside {parishText}.";
        }

        return string.IsNullOrWhiteSpace(parish)
            ? "Point-within-parish validation is not available yet."
            : $"Point-within-parish validation is not available yet for {parishText}.";
    }

    private static string BuildParcelBoundaryWithinParishFinding(string status, string evidence)
    {
        var values = ParseEvidence(evidence);
        values.TryGetValue("parish", out var parish);
        values.TryGetValue("outside_points", out var outsidePoints);
        var parishText = string.IsNullOrWhiteSpace(parish) ? "the parish" : $"the {parish} parish";
        var displayStatus = BuildDisplayStatus(status);

        if (displayStatus == "Found")
        {
            return $"The parcel boundary is located inside {parishText}.";
        }

        if (displayStatus == "Not found")
        {
            var points = string.IsNullOrWhiteSpace(outsidePoints) || string.Equals(outsidePoints, "none", StringComparison.OrdinalIgnoreCase)
                ? "one or more boundary points"
                : $"the following points [{outsidePoints}]";
            return $"The parcel boundary is not fully inside {parishText}; {points} require review.";
        }

        return string.IsNullOrWhiteSpace(parish)
            ? "Parcel-boundary parish validation is not available yet."
            : $"Parcel-boundary parish validation is not available yet for {parishText}.";
    }

    private static string BuildEmbeddedComputeSheetFinding(string status, string evidence)
    {
        var values = ParseEvidence(evidence);
        values.TryGetValue("pages", out var pages);
        var displayStatus = BuildDisplayStatus(status);
        if (displayStatus == "Found")
        {
            return string.IsNullOrWhiteSpace(pages) || string.Equals(pages, "unknown", StringComparison.OrdinalIgnoreCase)
                ? "An embedded compute sheet was extracted from the source document."
                : $"An embedded compute sheet was extracted from page(s) {pages}.";
        }

        if (displayStatus == "Not found")
        {
            return "An embedded compute sheet was not extracted from the source document.";
        }

        return "No embedded compute sheet structure has been captured yet. If the sheet is visible in the PDF, extraction must capture its rows before plan-to-compute comparison can run.";
    }

    private static string BuildPlanComputeSheetMatchFinding(string status, string evidence)
    {
        var values = ParseEvidence(evidence);
        values.TryGetValue("matches", out var matches);
        values.TryGetValue("mismatches", out var mismatches);
        values.TryGetValue("plan_points", out var planPoints);
        values.TryGetValue("sheet_points", out var sheetPoints);
        var displayStatus = BuildDisplayStatus(status);
        if (displayStatus == "Found")
        {
            return $"Plan and embedded compute sheet values match within tolerance ({matches ?? "0"} comparable value(s), {mismatches ?? "0"} mismatch(es)).";
        }

        if (displayStatus == "Not found")
        {
            return $"Plan and embedded compute sheet values do not match within tolerance ({mismatches ?? "one or more"} mismatch(es)).";
        }

        if (!string.IsNullOrWhiteSpace(planPoints) || !string.IsNullOrWhiteSpace(sheetPoints))
        {
            return $"Plan-to-compute comparison is not available because comparable values are incomplete (plan points: {planPoints ?? "unknown"}, compute sheet points: {sheetPoints ?? "unknown"}).";
        }

        return "Plan-to-compute comparison requires extracted plan rows and extracted embedded compute-sheet rows with comparable point, coordinate, bearing, distance, or area values.";
    }

    private static string BuildPrintedTextHeightFinding(string status, string evidence)
    {
        var values = ParseEvidence(evidence);
        values.TryGetValue("observed_mm", out var observed);
        values.TryGetValue("threshold_mm", out var threshold);
        values.TryGetValue("page_standard", out var pageStandard);
        values.TryGetValue("excluded_title_subtitle_runs", out var excluded);
        var displayStatus = BuildDisplayStatus(status);
        var paperSource = string.IsNullOrWhiteSpace(pageStandard) ? "the detected paper size" : pageStandard;
        var excludedText = string.IsNullOrWhiteSpace(excluded) ? string.Empty : $" Title/subtitle-like text runs excluded: {excluded}.";

        if (displayStatus == "Found")
        {
            return $"Ordinary plan text height is {observed ?? "available"} mm and satisfies the {threshold ?? "configured"} mm threshold based on {paperSource}.{excludedText}";
        }

        if (displayStatus == "Not found")
        {
            return $"Ordinary plan text height is {observed ?? "below threshold"} mm and does not satisfy the {threshold ?? "configured"} mm threshold based on {paperSource}.{excludedText}";
        }

        return "Printed text-height validation is not available because ordinary plan text metrics were not captured. Titles and subtitles are excluded from this check.";
    }
    private static Dictionary<string, string> ParseEvidence(string evidence)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in evidence.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || separator == part.Length - 1)
            {
                continue;
            }

            values[part[..separator].Trim()] = part[(separator + 1)..].Trim();
        }

        return values;
    }

    private static string? FirstAvailable(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}

public static class ValidationFindingDispositionProjector
{
    private const string NotAvailable = "N/A";

    private static readonly IReadOnlyList<ValidationFinding> BaselineFindings =
    [
        new(
            "georeference.parish_point_within_boundary",
            "Points within Parish",
            NotAvailable,
            NotAvailable,
            NotAvailable,
            NotAvailable),
        new(
            "spatial_units.parish_polygon_within_boundary",
            "Parcel Boundary within Parish",
            NotAvailable,
            NotAvailable,
            NotAvailable,
            NotAvailable),
        new(
            "pxa.embedded_compute_sheet_detected",
            "Embedded Compute Sheet",
            NotAvailable,
            NotAvailable,
            NotAvailable,
            NotAvailable),
        new(
            "pxa.plan_compute_sheet_consistency",
            "Plan and Compute Sheet Match",
            NotAvailable,
            NotAvailable,
            NotAvailable,
            NotAvailable),
        new(
            "document.printed_text_height",
            "Printed Text Height",
            NotAvailable,
            NotAvailable,
            NotAvailable,
            NotAvailable)
    ];

    public static IReadOnlyList<ValidationFindingDispositionRow> BuildRows(
        ValidationSummaryDocument? summary,
        ValidationFindingDispositionDocument? disposition)
    {
        var dispositionByKey = (disposition?.Items ?? Array.Empty<ValidationFindingDispositionItem>())
            .GroupBy(item => item.FindingKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var summaryFindings = summary?.Payload.Findings ?? Array.Empty<ValidationFinding>();
        var summaryRuleIds = summaryFindings
            .Select(finding => finding.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingBaselineFindings = BaselineFindings
            .Where(finding => !summaryRuleIds.Contains(finding.RuleId));

        return summaryFindings
            .Concat(missingBaselineFindings)
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
