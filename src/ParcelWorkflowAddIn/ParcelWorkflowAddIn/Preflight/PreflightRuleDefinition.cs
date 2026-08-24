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
    [property: JsonPropertyName("stage_id")] string StageId = "structure_check",
    [property: JsonPropertyName("workflow_effect")] string WorkflowEffect = "requires_disposition",
    [property: JsonPropertyName("evaluator_key")] string EvaluatorKey = "manual_review",
    [property: JsonPropertyName("report_visible")] bool ReportVisible = true,
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
            Severity = Locked ? Severity : NormalizeSeverity(overrideRule.Severity, Severity),
            StageId = Locked ? StageId : NormalizeStageId(overrideRule.StageId, StageId),
            WorkflowEffect = Locked ? WorkflowEffect : NormalizeWorkflowEffect(overrideRule.WorkflowEffect, WorkflowEffect),
            EvaluatorKey = Locked ? EvaluatorKey : NormalizeEvaluatorKey(overrideRule.EvaluatorKey, EvaluatorKey),
            ReportVisible = Locked ? ReportVisible : overrideRule.ReportVisible
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

    public bool AppliesToStage(string? stageId)
    {
        var normalizedStageId = NormalizeStageId(stageId, string.Empty);
        return string.Equals(normalizedStageId, PreflightCheckStageExtensions.CombinedStageId, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(normalizedStageId, PreflightCheckStageExtensions.StructureCheckStageId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(StageId, "supporting_document_check", StringComparison.OrdinalIgnoreCase))
            || string.Equals(StageId, normalizedStageId, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeStageId(string? value, string fallback = "structure_check")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "supporting_document_check" => "supporting_document_check",
            "preflight" => PreflightCheckStageExtensions.CombinedStageId,
            "structure_check" => "structure_check",
            "data_extraction" => "data_extraction",
            "georeference_check" => "georeference_check",
            "dimension_check" => "dimension_check",
            "validate_points_and_lines" => "validate_points_and_lines",
            "create_spatial_units" => "create_spatial_units",
            "final_review" => "final_review",
            "finalize" => "finalize",
            _ => fallback
        };
    }

    public static string NormalizeWorkflowEffect(string? value, string fallback = "requires_disposition")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "blocker" => "blocker",
            "requires_disposition" => "requires_disposition",
            "report_only" => "report_only",
            "info" => "info",
            _ => fallback
        };
    }

    public static string NormalizeEvaluatorKey(string? value, string fallback = "manual_review")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return SupportedEvaluatorKeys.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : fallback;
    }

    public static IReadOnlyList<string> SupportedStageIds { get; } = new[]
    {
        "supporting_document_check",
        "structure_check",
        "data_extraction",
        "georeference_check",
        "dimension_check",
        "validate_points_and_lines",
        "create_spatial_units",
        "final_review",
        "finalize"
    };

    public static IReadOnlyList<string> SupportedWorkflowEffects { get; } = new[]
    {
        "blocker",
        "requires_disposition",
        "report_only",
        "info"
    };

    public static IReadOnlyList<string> SupportedEvaluatorKeys { get; } = new[]
    {
        "detected_profile_presence",
        "detected_profile_complete",
        "required_source_roles",
        "source_file_integrity",
        "workflow_rule_resolution",
        "arcgis_sdk_lane",
        "workspace_access",
        "python_executable_health",
        "arcgis_unknown_version_behavior",
        "python_package_probe",
        "dwg_signature_check",
        "dwg_readiness_probe",
        "dwg_required_cad_layers",
        "georeference_source_presence",
        "tabular_coordinate_columns",
        "jamaica_coordinate_bounds",
        "georeference_spatial_validation_readiness",
        "dimension_source_presence",
        "dimension_geometry_construction_readiness",
        "pxa_memorandum_detected",
        "pxa_memorandum_surveyed_for_names_present",
        "pxa_memorandum_surveyed_property_name_present",
        "pxa_memorandum_property_name_near_diagram",
        "pxa_memorandum_document_area_present",
        "pxa_memorandum_objections_captured",
        "pxa_memorandum_surveyor_certification_present",
        "pxa_memorandum_instrument_group_complete",
        "pxa_memorandum_parish_present",
        "pxa_memorandum_north_arrow_present",
        "pxa_memorandum_scale_bar_present",
        "pxa_memorandum_notice_served_on_present",
        "pxa_memorandum_appearance_parties_present",
        "manual_review"
    };

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
            "memorandum" => "memorandum",
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
