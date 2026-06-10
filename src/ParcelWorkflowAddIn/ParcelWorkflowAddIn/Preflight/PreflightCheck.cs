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
    public static PreflightCheck Blocker(string checkId, string message, string? affectedPath = null, string? sourceRole = null, string? correction = null)
    {
        return new PreflightCheck(checkId, "manifest", "blocker", "blocked", message, affectedPath, sourceRole, correction);
    }

    public static PreflightCheck Passed(string checkId, string message, string? affectedPath = null, string? sourceRole = null)
    {
        return new PreflightCheck(checkId, "manifest", "passed", "passed", message, affectedPath, sourceRole, null);
    }
}
