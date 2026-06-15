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
    [property: JsonPropertyName("correction")] string? Correction)
{
    public static PreflightCheck BlockerForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return new PreflightCheck(checkId, category, "blocker", "blocked", message, affectedPath, sourceRole, correction);
    }

    public static PreflightCheck Blocker(string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return BlockerForCategory("manifest", checkId, message, affectedPath, sourceRole, correction);
    }

    public static PreflightCheck WarningForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return new PreflightCheck(checkId, category, "warning", "warning", message, affectedPath, sourceRole, correction);
    }

    public static PreflightCheck DisabledForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return new PreflightCheck(checkId, category, "warning", "disabled", message, affectedPath, sourceRole, null);
    }

    public static PreflightCheck PassedForCategory(string category, string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return new PreflightCheck(checkId, category, "passed", "passed", message, affectedPath, sourceRole, null);
    }

    public static PreflightCheck Passed(string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return PassedForCategory("manifest", checkId, message, affectedPath, sourceRole);
    }
}
