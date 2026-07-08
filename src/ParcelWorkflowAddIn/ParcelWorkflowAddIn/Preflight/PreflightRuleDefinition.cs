using System.Text.Json.Serialization;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Preflight;

public sealed record PreflightRuleDefinition(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("locked")] bool Locked,
    [property: JsonPropertyName("transaction_types")] IReadOnlyList<string>? TransactionTypes = null,
    [property: JsonPropertyName("workflow_stages")] IReadOnlyList<string>? WorkflowStages = null,
    [property: JsonPropertyName("transaction_type_profiles")] IReadOnlyList<string>? TransactionTypeProfiles = null,
    [property: JsonPropertyName("document_profiles")] IReadOnlyList<string>? DocumentProfiles = null,
    [property: JsonPropertyName("source_roles")] IReadOnlyList<string>? SourceRoles = null,
    [property: JsonPropertyName("file_types")] IReadOnlyList<string>? FileTypes = null,
    [property: JsonPropertyName("embedded_text_preferred")] bool? EmbeddedTextPreferred = null,
    [property: JsonPropertyName("ocr_fallback_allowed")] bool? OcrFallbackAllowed = null,
    [property: JsonPropertyName("dwg_readiness_required")] bool? DwgReadinessRequired = null,
    [property: JsonPropertyName("tabular_coordinates_required")] bool? TabularCoordinatesRequired = null,
    [property: JsonPropertyName("minimum_coordinate_pairs")] int? MinimumCoordinatePairs = null,
    [property: JsonPropertyName("require_jamaica_bounds")] bool? RequireJamaicaBounds = null,
    [property: JsonPropertyName("allow_tabular_georeference")] bool? AllowTabularGeoreference = null,
    [property: JsonPropertyName("required_cad_layers")] IReadOnlyDictionary<string, IReadOnlyList<string>>? RequiredCadLayers = null)
{
    public PreflightRuleDefinition(
        string ruleId,
        string category,
        string displayName,
        string description,
        bool enabled,
        string severity,
        bool locked)
        : this(
            ruleId,
            InferGroupFromCategory(category),
            category,
            displayName,
            description,
            enabled,
            severity,
            locked)
    {
    }

    public PreflightRuleDefinition Merge(PreflightRuleDefinition overrideRule)
    {
        return this with
        {
            Enabled = Locked ? Enabled : overrideRule.Enabled,
            Severity = Locked ? Severity : NormalizeSeverity(overrideRule.Severity, Severity)
        };
    }

    public bool AppliesToTransaction(string? transactionType, string? workflowStage)
    {
        return MatchesAny(TransactionTypes, transactionType)
            && MatchesAny(WorkflowStages, workflowStage);
    }

    public bool AppliesToTransactionProfile(string? transactionProfile, string? documentProfile)
    {
        return MatchesAny(TransactionTypeProfiles, transactionProfile)
            && MatchesAny(DocumentProfiles, documentProfile);
    }

    public bool AppliesToSource(string? sourceRole, string? fileType)
    {
        return MatchesAnySourceRole(SourceRoles, sourceRole)
            && MatchesAny(FileTypes, fileType);
    }

    public static string NormalizeGroup(string? value, string fallback = "structure")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "supporting_document" => "supporting_document",
            "structure" => "structure",
            "georeference" => "georeference",
            "dimension" => "dimension",
            "system" => "system",
            _ => fallback
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

    private static bool MatchesAny(IReadOnlyList<string>? candidates, string? value)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return candidates.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAnySourceRole(IReadOnlyList<string>? candidates, string? value)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return candidates.Any(candidate => SourceRole.Matches(value, candidate));
    }

    private static string InferGroupFromCategory(string? category)
    {
        return category?.Trim().ToLowerInvariant() switch
        {
            "manifest" => "supporting_document",
            "workflow_rule" => "structure",
            "dwg" => "structure",
            "georeference" => "georeference",
            "dimension" => "dimension",
            "arcgis_pro" => "system",
            "write_access" => "system",
            "python" => "system",
            _ => "structure"
        };
    }
}
