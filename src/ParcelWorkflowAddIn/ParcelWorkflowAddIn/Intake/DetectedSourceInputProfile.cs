using System.Text.Json.Serialization;

namespace ParcelWorkflowAddIn.Intake;

public sealed record DetectedSourceInputProfile(
    [property: JsonPropertyName("profile_code")] string ProfileCode,
    [property: JsonPropertyName("display_label")] string DisplayLabel,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("detected_at")] string DetectedAt,
    [property: JsonPropertyName("missing_roles")] IReadOnlyList<string> MissingRoles,
    [property: JsonPropertyName("issues")] IReadOnlyList<string> Issues);
