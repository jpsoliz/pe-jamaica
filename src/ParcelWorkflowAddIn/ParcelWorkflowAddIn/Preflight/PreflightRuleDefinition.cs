using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Preflight;

public sealed record PreflightRuleDefinition(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("locked")] bool Locked)
{
    public PreflightRuleDefinition Merge(PreflightRuleDefinition overrideRule)
    {
        return this with
        {
            Enabled = Locked ? Enabled : overrideRule.Enabled,
            Severity = Locked ? Severity : NormalizeSeverity(overrideRule.Severity, Severity)
        };
    }

    public static string NormalizeSeverity(string? value, string fallback = "warning")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "warning" => "warning",
            "blocker" => "blocker",
            "configured" => "configured",
            _ => fallback
        };
    }
}
