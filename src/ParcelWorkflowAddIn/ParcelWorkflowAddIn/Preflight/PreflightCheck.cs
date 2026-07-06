using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Preflight;

public sealed record PreflightCheck(
    [property: JsonPropertyName("check_id")] string CheckId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("affected_path")] string? AffectedPath,
    [property: JsonPropertyName("source_role")] string? SourceRole,
    [property: JsonPropertyName("correction")] string? Correction,
    [property: JsonPropertyName("outcome")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Outcome = null,
    [property: JsonPropertyName("workflow_effect")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WorkflowEffect = null,
    [property: JsonPropertyName("display_name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: JsonPropertyName("evidence")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, IReadOnlyList<string>>? Evidence = null)
{
    public static PreflightCheck BlockerForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return new PreflightCheck(checkId, category, "blocker", "blocked", message, affectedPath, sourceRole, correction, "failed", "blocker");
    }

    public static PreflightCheck Blocker(string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return BlockerForCategory("manifest", checkId, message, affectedPath, sourceRole, correction);
    }

    public static PreflightCheck WarningForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return new PreflightCheck(checkId, category, "warning", "warning", message, affectedPath, sourceRole, correction, "warning", "report_only");
    }

    public static PreflightCheck DisabledForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return new PreflightCheck(checkId, category, "disabled", "disabled", message, affectedPath, sourceRole, null, "disabled", "info");
    }

    public static PreflightCheck PassedForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return new PreflightCheck(checkId, category, "passed", "passed", message, affectedPath, sourceRole, null, "passed", "info");
    }

    public static PreflightCheck Passed(string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return PassedForCategory("manifest", checkId, message, affectedPath, sourceRole);
    }

    public PreflightCheck WithOutcome(string outcome, IReadOnlyDictionary<string, IReadOnlyList<string>>? evidence = null)
    {
        return this with { Outcome = outcome, Evidence = evidence };
    }

    public PreflightCheck WithWorkflowEffect(string workflowEffect)
    {
        return this with { WorkflowEffect = workflowEffect };
    }

    public PreflightCheck WithDisplayName(string displayName)
    {
        return this with { DisplayName = displayName };
    }
}
