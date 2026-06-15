namespace ParcelWorkflowAddIn.Preflight;

public sealed record PreflightRuleCatalog(
    string SourcePath,
    bool UsingSafeDefaults,
    string? LoadWarning,
    IReadOnlyList<PreflightRuleDefinition> Rules)
{
    public PreflightRuleDefinition GetRule(string ruleId)
    {
        return Rules.FirstOrDefault(rule => string.Equals(rule.RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown preflight rule: {ruleId}");
    }
}
