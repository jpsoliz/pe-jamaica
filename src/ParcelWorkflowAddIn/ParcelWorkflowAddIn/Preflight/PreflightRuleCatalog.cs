namespace ParcelWorkflowAddIn.Preflight;

public sealed record PreflightRuleCatalog(
    string SourcePath,
    bool UsingSafeDefaults,
    string? LoadWarning,
    IReadOnlyList<PreflightRuleDefinition> Rules)
{
    public PreflightRuleDefinition? TryGetRule(string ruleId)
    {
        return Rules.FirstOrDefault(rule => string.Equals(rule.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
    }

    public PreflightRuleDefinition GetRule(string ruleId)
    {
        return TryGetRule(ruleId)
            ?? throw new InvalidOperationException($"Unknown preflight rule: {ruleId}");
    }
}
