using ParcelWorkflowAddIn.Preflight;

namespace ParcelWorkflowAddIn.Workflow.Review;

public sealed class PxaMemorandumReviewRuleService
{
    private const string RequiresDisposition = "requires_disposition";
    private const string ReportOnly = "report_only";
    private readonly RuleSpec[] rules;

    private static readonly RuleSpec[] DefaultRules =
    [
        new("pxa_memorandum_detected", "memorandum_detection", "Memorandum Detection", "Memorandum text detected", "info"),
        new("pxa_memorandum_surveyed_for_names_present", "property_survey_request", "Property / Survey Request", "Survey made at the instance of", RequiresDisposition),
        new("pxa_memorandum_surveyed_property_name_present", "property_survey_request", "Property / Survey Request", "Surveyed property name", RequiresDisposition),
        new("pxa_memorandum_property_name_near_diagram", "property_survey_request", "Property / Survey Request", "Property name near parcel diagram", ReportOnly),
        new("pxa_memorandum_instrument_group_complete", "instrument_check", "Instrument Check", "Instrument check evidence", RequiresDisposition),
        new("pxa_memorandum_parish_present", "location_map_evidence", "Location / Map Evidence", "Parish", RequiresDisposition),
        new("pxa_memorandum_north_arrow_present", "location_map_evidence", "Location / Map Evidence", "North arrow", ReportOnly),
        new("pxa_memorandum_scale_bar_present", "location_map_evidence", "Location / Map Evidence", "Scale bar", ReportOnly),
        new("pxa_memorandum_notice_served_on_present", "notice_attendance", "Notice / Attendance", "Notices served on", RequiresDisposition),
        new("pxa_memorandum_appearance_parties_present", "notice_attendance", "Notice / Attendance", "Appeared parties", RequiresDisposition)
    ];

    public PxaMemorandumReviewRuleService()
        : this(LoadCatalogRules())
    {
    }

    internal PxaMemorandumReviewRuleService(IReadOnlyList<PreflightRuleDefinition> catalogRules)
    {
        rules = BuildRuleSpecs(catalogRules);
    }

    public IReadOnlyList<ExtractionReviewMemorandumRuleResult> Evaluate(ExtractionReviewDocument document)
    {
        if (!document.MemorandumDetected)
        {
            return rules
                .Select(rule => rule.Enabled
                    ? Create(rule, "not_applicable", "Memorandum was not detected for this PXA source document.")
                    : Create(rule, "disabled", "Rule is disabled in the compute rule catalog."))
                .ToArray();
        }

        return
        [
            EvaluateDetected(document, rules[0]),
            EvaluateParty(document, rules[1], "surveyed_for", "Surveyed-for party is missing."),
            EvaluateField(document, rules[2], "surveyed_property_name", "Surveyed property name is missing."),
            EvaluatePresenceField(document, rules[3], "property_name_near_parcel_diagram", "Diagram proximity evidence is missing."),
            EvaluateInstrumentGroup(document, rules[4]),
            EvaluateField(document, rules[5], "parish", "Parish is missing."),
            EvaluatePresenceField(document, rules[6], "north_arrow", "North arrow evidence is missing."),
            EvaluatePresenceField(document, rules[7], "scale_bar", "Scale bar evidence is missing."),
            EvaluateParty(document, rules[8], "notice_served_on", "Notice-served-on parties are missing."),
            EvaluateParty(document, rules[9], "appeared", "Appeared parties are missing.")
        ];
    }

    public IReadOnlyList<ExtractionReviewMemorandumGroup> BuildGroups(IReadOnlyList<ExtractionReviewMemorandumRuleResult> results)
    {
        return rules
            .GroupBy(rule => new { rule.GroupId, rule.GroupName })
            .Select(group =>
            {
                var matchingResults = results
                    .Where(result => string.Equals(result.Group, group.Key.GroupId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var resultGroup = new ExtractionReviewMemorandumGroup
                {
                    GroupId = group.Key.GroupId,
                    DisplayName = group.Key.GroupName,
                    PassedCount = matchingResults.Count(result => result.Outcome == "passed"),
                    NeedsReviewCount = matchingResults.Count(result => result.Outcome == "needs_review"),
                    FailedCount = matchingResults.Count(result => result.Outcome == "failed"),
                    NotAvailableCount = matchingResults.Count(result => result.Outcome == "not_available"),
                    NotApplicableCount = matchingResults.Count(result => result.Outcome == "not_applicable")
                };
                foreach (var result in matchingResults)
                {
                    resultGroup.Rules.Add(result);
                }

                resultGroup.Summary = BuildSummary(resultGroup);
                return resultGroup;
            })
            .ToArray();
    }

    private static ExtractionReviewMemorandumRuleResult EvaluateDetected(ExtractionReviewDocument document, RuleSpec rule)
    {
        if (!rule.Enabled)
        {
            return Create(rule, "disabled", "Rule is disabled in the compute rule catalog.");
        }

        var memorandum = document.RootMetadata["document_sections"]?["memorandum"] as System.Text.Json.Nodes.JsonObject;
        var evidenceValue = ReadFirstString(memorandum, "matched_text", "text");
        return Create(
            rule,
            "passed",
            "Memorandum text was detected.",
            evidenceValue,
            ReadFirstString(memorandum, "source_page", "page"),
            ReadFirstString(memorandum, "source_zone", "zone"));
    }

    private static ExtractionReviewMemorandumRuleResult EvaluateParty(
        ExtractionReviewDocument document,
        RuleSpec rule,
        string role,
        string missingMessage)
    {
        if (!rule.Enabled)
        {
            return Create(rule, "disabled", "Rule is disabled in the compute rule catalog.");
        }

        var parties = document.MemorandumParties
            .Where(party => string.Equals(party.Role, role, StringComparison.OrdinalIgnoreCase))
            .Where(party => !string.IsNullOrWhiteSpace(party.Name))
            .ToArray();
        if (parties.Length == 0)
        {
            return Create(rule, "not_available", missingMessage);
        }

        var first = parties[0];
        var evidenceValue = string.Join("; ", parties.Select(FormatMemorandumPartyValue).Where(value => !string.IsNullOrWhiteSpace(value)));
        var message = parties.Length == 1 && IsNoAppearanceEvidence(first)
            ? $"{evidenceValue} is recorded for examiner review."
            : $"{parties.Length} value(s) available for examiner review.";
        return Create(rule, ResolveEvidenceOutcome(first.ReviewStatus, first.SourceZone, null), message, evidenceValue, first.SourcePage, first.SourceZone);
    }

    private static ExtractionReviewMemorandumRuleResult EvaluateField(
        ExtractionReviewDocument document,
        RuleSpec rule,
        string fieldKey,
        string missingMessage)
    {
        if (!rule.Enabled)
        {
            return Create(rule, "disabled", "Rule is disabled in the compute rule catalog.");
        }

        var field = FindField(document, fieldKey);
        if (field is null || string.IsNullOrWhiteSpace(FirstNonBlank(field.Value, field.RawText)))
        {
            return Create(rule, "not_available", missingMessage);
        }

        var evidenceValue = FirstNonBlank(field.Value, field.RawText) ?? string.Empty;
        return Create(rule, ResolveEvidenceOutcome(field.ReviewStatus, field.SourceZone, field.Confidence), "Evidence value is available for examiner review.", evidenceValue, field.SourcePage, field.SourceZone);
    }

    private static ExtractionReviewMemorandumRuleResult EvaluatePresenceField(
        ExtractionReviewDocument document,
        RuleSpec rule,
        string fieldKey,
        string missingMessage)
    {
        if (!rule.Enabled)
        {
            return Create(rule, "disabled", "Rule is disabled in the compute rule catalog.");
        }

        var field = FindField(document, fieldKey);
        if (field?.Present != true)
        {
            return Create(rule, "not_available", missingMessage, FirstNonBlank(field?.Value, field?.RawText) ?? string.Empty, field?.SourcePage, field?.SourceZone);
        }

        var evidenceValue = FirstNonBlank(field.Value, field.RawText) ?? (field.Present == true ? "Present" : string.Empty);
        return Create(rule, ResolveEvidenceOutcome(field.ReviewStatus, field.SourceZone, field.Confidence), "Presence evidence is available for examiner review.", evidenceValue, field.SourcePage, field.SourceZone);
    }

    private static ExtractionReviewMemorandumRuleResult EvaluateInstrumentGroup(ExtractionReviewDocument document, RuleSpec rule)
    {
        if (!rule.Enabled)
        {
            return Create(rule, "disabled", "Rule is disabled in the compute rule catalog.");
        }

        var instrument = FindField(document, "survey_instrument");
        var date = FindField(document, "instrument_check_date");
        var result = FindField(document, "instrument_check_result");
        var requiredFields = new[] { instrument, date, result };
        if (requiredFields.Any(field => field is null || string.IsNullOrWhiteSpace(FirstNonBlank(field.Value, field.RawText))))
        {
            return Create(rule, "not_available", "Instrument name/type, check date, or check result is missing.");
        }

        var outcome = requiredFields.Any(field => ResolveEvidenceOutcome(field!.ReviewStatus, field.SourceZone, field.Confidence) == "needs_review")
            ? "needs_review"
            : "passed";
        var evidenceValue = string.Join(
            "; ",
            new[]
            {
                FormatFieldValue("Instrument", instrument),
                FormatFieldValue("Check date", date),
                FormatFieldValue("Result", result)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return Create(rule, outcome, "Instrument check date and result are grouped with the instrument evidence.", evidenceValue, instrument?.SourcePage, instrument?.SourceZone);
    }

    private static ExtractionReviewMetadataField? FindField(ExtractionReviewDocument document, string key)
    {
        return document.SurveyMetadataFields.FirstOrDefault(field =>
            string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static ExtractionReviewMemorandumRuleResult Create(
        RuleSpec rule,
        string outcome,
        string message,
        string? evidenceValue = null,
        string? sourcePage = null,
        string? sourceZone = null)
    {
        return new ExtractionReviewMemorandumRuleResult
        {
            RuleId = rule.RuleId,
            Group = rule.GroupId,
            Label = rule.Label,
            Outcome = outcome,
            ReviewerStatus = ToReviewerStatus(outcome),
            WorkflowEffect = rule.WorkflowEffect,
            Message = message,
            EvidenceValue = evidenceValue ?? string.Empty,
            SourcePage = sourcePage ?? string.Empty,
            SourceZone = sourceZone ?? string.Empty,
            ReportVisible = rule.ReportVisible
        };
    }

    private static RuleSpec[] BuildRuleSpecs(IReadOnlyList<PreflightRuleDefinition> catalogRules)
    {
        var catalogById = catalogRules
            .GroupBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return DefaultRules
            .Select(defaultRule => catalogById.TryGetValue(defaultRule.RuleId, out var catalogRule)
                ? defaultRule with
                {
                    Label = string.IsNullOrWhiteSpace(catalogRule.DisplayName) ? defaultRule.Label : catalogRule.DisplayName,
                    WorkflowEffect = PreflightRuleDefinition.NormalizeWorkflowEffect(catalogRule.WorkflowEffect, defaultRule.WorkflowEffect),
                    Enabled = catalogRule.Enabled,
                    ReportVisible = catalogRule.ReportVisible
                }
                : defaultRule)
            .ToArray();
    }

    private static IReadOnlyList<PreflightRuleDefinition> LoadCatalogRules()
    {
        return new PreflightRuleCatalogLoader().Load().Rules;
    }

    private static string ResolveEvidenceOutcome(string? reviewStatus, string? sourceZone, string? confidence)
    {
        if (IsAccepted(reviewStatus))
        {
            return "passed";
        }

        if (IsRejected(reviewStatus))
        {
            return "failed";
        }

        if (!string.IsNullOrWhiteSpace(sourceZone) || IsLowConfidence(confidence))
        {
            return "needs_review";
        }

        return "needs_review";
    }

    private static bool IsAccepted(string? value)
    {
        return string.Equals(value, "accepted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "approved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "passed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRejected(string? value)
    {
        return string.Equals(value, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "rejected", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowConfidence(string? confidence)
    {
        return double.TryParse(confidence, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            && value < 0.8d;
    }

    private static string ToReviewerStatus(string outcome)
    {
        return outcome switch
        {
            "passed" => "Passed",
            "needs_review" => "Needs Review",
            "failed" => "Failed",
            "not_available" => "Not Available",
            "not_applicable" => "Not Applicable",
            "disabled" => "Disabled",
            "skipped" => "Skipped",
            _ => outcome
        };
    }

    private static string BuildSummary(ExtractionReviewMemorandumGroup group)
    {
        var parts = new[]
        {
            FormatCount(group.PassedCount, "passed"),
            FormatCount(group.NeedsReviewCount, "needs review"),
            FormatCount(group.FailedCount, "failed"),
            FormatCount(group.NotAvailableCount, "not available"),
            FormatCount(group.NotApplicableCount, "not applicable")
        }.Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(" / ", parts);
    }

    private static string FormatCount(int count, string label) => count > 0 ? $"{count} {label}" : string.Empty;

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string FormatMemorandumPartyValue(ExtractionReviewMemorandumParty party)
    {
        var name = party.Name;
        if (!string.IsNullOrWhiteSpace(party.Representative))
        {
            return $"{name} by {party.Representative}";
        }

        return name;
    }

    private static bool IsNoAppearanceEvidence(ExtractionReviewMemorandumParty party)
    {
        return string.Equals(party.AppearanceMode, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(party.Name, "No one appeared", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatFieldValue(string label, ExtractionReviewMetadataField? field)
    {
        var value = FirstNonBlank(field?.Value, field?.RawText);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value}";
    }

    private static string ReadFirstString(System.Text.Json.Nodes.JsonObject? node, params string[] propertyNames)
    {
        if (node is null)
        {
            return string.Empty;
        }

        foreach (var propertyName in propertyNames)
        {
            var value = ReadScalarString(node[propertyName]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string? ReadScalarString(System.Text.Json.Nodes.JsonNode? value)
    {
        if (value is not System.Text.Json.Nodes.JsonValue jsonValue)
        {
            return null;
        }

        if (jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        return null;
    }

    private sealed record RuleSpec(
        string RuleId,
        string GroupId,
        string GroupName,
        string Label,
        string WorkflowEffect,
        bool Enabled = true,
        bool ReportVisible = true);
}
