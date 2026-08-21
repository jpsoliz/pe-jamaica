using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class PreflightRuleCatalogLoader
{
    private const string PreferredRulesFileName = "StructureRules.json";
    private const string LegacyRulesFileName = "PreflightRules.json";
    private const string RequiredCadLayersRuleId = "dwg_required_cad_layers";
    private const string SupportedSchemaVersion = "1.0.0";
    private readonly string? rulesPathOverride;
    private readonly string? settingsPathOverride;

    private static readonly IReadOnlyList<PreflightRuleDefinition> DefaultRules = new[]
    {
        new PreflightRuleDefinition("detected_profile_presence", "supporting_document", "manifest", "Detected profile present", "Detected intake profile must be present before Structure Check can continue.", true, "blocker", true),
        new PreflightRuleDefinition("detected_profile_complete", "supporting_document", "manifest", "Detected profile complete", "Incomplete supporting documents remain blocked until required source roles are resolved.", true, "blocker", true),
        new PreflightRuleDefinition("required_source_roles", "supporting_document", "manifest", "Required source roles", "Each compute transaction must provide the required copied source roles before structure and georeference work can begin.", true, "blocker", true, SourceRoles: new[] { "computation_sheet", "plan_map_reference" }),
        new PreflightRuleDefinition("source_file_integrity", "structure", "manifest", "Copied source integrity", "Copied source paths must stay inside the case folder, exist, use supported extensions, and remain readable.", true, "blocker", true),
        new PreflightRuleDefinition("workflow_rule_resolution", "structure", "workflow_rule", "Workflow rule resolution", "Transactions must resolve to a current workflow rule and script plan.", true, "blocker", true),
        new PreflightRuleDefinition("arcgis_sdk_lane", "system", "arcgis_pro", "ArcGIS Pro SDK lane", "SDK lane and target framework must match the supported ArcGIS Pro 3.6 add-in lane.", true, "blocker", true),
        new PreflightRuleDefinition("workspace_access", "system", "write_access", "Workspace access", "Case folder working, output, and summary locations must remain writable.", true, "blocker", true),
        new PreflightRuleDefinition("python_executable_health", "system", "python", "Python executable health", "Configured Python executable must be set, exist, and be invokable.", true, "blocker", true),
        new PreflightRuleDefinition("arcgis_unknown_version_behavior", "system", "arcgis_pro", "Unknown ArcGIS Pro version handling", "Controls whether unknown ArcGIS Pro version detection is treated as a warning or blocker.", true, "warning", false),
        new PreflightRuleDefinition("python_package_probe", "system", "python", "Python package probe", "Checks configured required and optional Python packages such as ArcPy before downstream processing runs.", true, "configured", false),
        new PreflightRuleDefinition("dwg_signature_check", "structure", "dwg", "DWG file signature", "DWG reference files must be non-empty and contain a recognizable DWG signature.", true, "blocker", true, SourceRoles: new[] { "dwg_source" }, FileTypes: new[] { ".dwg" }, DwgReadinessRequired: true),
        new PreflightRuleDefinition("dwg_readiness_probe", "structure", "dwg", "DWG readiness probe", "Optional CAD sub-layer readiness probe for copied DWG references.", true, "blocker", false, SourceRoles: new[] { "dwg_source" }, FileTypes: new[] { ".dwg" }, DwgReadinessRequired: true),
        new PreflightRuleDefinition(
            RequiredCadLayersRuleId,
            "structure",
            "dwg",
            "Required DWG CAD layers",
            "Validates that DWG sources include expected CAD layer categories for points, lines, and annotation.",
            true,
            "blocker",
            false,
            SourceRoles: new[] { "dwg_source" },
            FileTypes: new[] { ".dwg" },
            DwgReadinessRequired: true,
            RequiredCadLayers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["points"] = new[] { "POINTS", "SURVEY_POINTS", "PNT", "POINT" },
                ["lines"] = new[] { "LINES", "BOUNDARY", "LINEWORK", "POLYLINE", "POLYLINES" },
                ["annotation"] = new[] { "TEXT", "ANNOTATION", "ANNO" }
            }),
        new PreflightRuleDefinition("georeference_source_presence", "georeference", "georeference", "Georeference source presence", "At least one source with usable coordinate context must be present before Validate Points and Lines can begin.", true, "blocker", true, SourceRoles: new[] { "computation_sheet", "coordinate_text_source", "plan_map_reference", "survey_plan_pdf" }, AllowTabularGeoreference: true),
        new PreflightRuleDefinition("tabular_coordinate_columns", "georeference", "georeference", "Tabular coordinate columns", "TXT/CSV coordinate sources should expose Easting/Northing-style columns when they are used for georeference support.", true, "warning", false, SourceRoles: new[] { "coordinate_text_source" }, FileTypes: new[] { ".txt", ".csv" }, TabularCoordinatesRequired: true),
        new PreflightRuleDefinition("jamaica_coordinate_bounds", "georeference", "georeference", "Jamaica coordinate bounds", "When tabular coordinates are available, the sample coordinate pairs should fall within Jamaica working bounds.", true, "warning", false, SourceRoles: new[] { "coordinate_text_source" }, FileTypes: new[] { ".txt", ".csv" }, MinimumCoordinatePairs: 1, RequireJamaicaBounds: true, AllowTabularGeoreference: true),
        new PreflightRuleDefinition("georeference_spatial_validation_readiness", "georeference", "georeference", "Concrete georeference validation", "Georeference Check should run a concrete coordinate, JAD2001, parish, or location validation; warning by default until parish/JAD2001 validators are configured.", true, "warning", false, SourceRoles: new[] { "computation_sheet", "coordinate_text_source", "plan_map_reference", "survey_plan_pdf" }),
        new PreflightRuleDefinition("dimension_source_presence", "dimension", "dimension", "Dimension source presence", "A computation sheet, survey plan PDF, or configured spatial line source must be available before Validate Points and Lines can begin.", true, "blocker", true, SourceRoles: new[] { "computation_sheet", "coordinate_text_source", "survey_plan_pdf" }),
        new PreflightRuleDefinition("dimension_geometry_construction_readiness", "dimension", "dimension", "Dimension geometry construction readiness", "Dimension Check should verify bearings, distances, point references, closure, or an equivalent geometry-construction readiness artifact; warning by default until the validator is configured.", true, "warning", false, SourceRoles: new[] { "computation_sheet", "coordinate_text_source", "survey_plan_pdf" }),
        new PreflightRuleDefinition("pxa_memorandum_detected", "memorandum", "pxa_memorandum", "Memorandum text detected", "PXA memorandum rules apply only when the source document contains MEMORANDUM text.", true, "configured", false, StageId: "data_extraction", WorkflowEffect: "info", EvaluatorKey: "pxa_memorandum_detected", TransactionTypeProfiles: new[] { "pxa" }, DocumentProfiles: new[] { "scanned_single_parcel_survey_plan_pdf", "survey_plan_pdf" }),
        new PreflightRuleDefinition("pxa_memorandum_surveyed_for_names_present", "memorandum", "pxa_memorandum", "Survey made at the instance of", "Detected memorandum should list the party at whose instance the survey was made or surveyed for.", true, "configured", false, StageId: "validate_points_and_lines", WorkflowEffect: "requires_disposition", EvaluatorKey: "pxa_memorandum_surveyed_for_names_present", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_surveyed_property_name_present", "memorandum", "pxa_memorandum", "Surveyed property name", "Detected memorandum should list the name of the surveyed property.", true, "configured", false, StageId: "validate_points_and_lines", WorkflowEffect: "requires_disposition", EvaluatorKey: "pxa_memorandum_surveyed_property_name_present", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_property_name_near_diagram", "memorandum", "pxa_memorandum", "Property name near parcel diagram", "Review whether the surveyed property name is printed near the parcel diagram when visual evidence is available.", true, "warning", false, StageId: "validate_points_and_lines", WorkflowEffect: "report_only", EvaluatorKey: "pxa_memorandum_property_name_near_diagram", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_instrument_group_complete", "memorandum", "pxa_memorandum", "Instrument check evidence", "Instrument name/type, instrument check date, and instrument check result should be reviewed as one grouped memorandum finding.", true, "configured", false, StageId: "validate_points_and_lines", WorkflowEffect: "requires_disposition", EvaluatorKey: "pxa_memorandum_instrument_group_complete", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_parish_present", "memorandum", "pxa_memorandum", "Parish", "Detected memorandum should provide or confirm the parish.", true, "configured", false, StageId: "validate_points_and_lines", WorkflowEffect: "requires_disposition", EvaluatorKey: "pxa_memorandum_parish_present", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_north_arrow_present", "memorandum", "pxa_memorandum", "North arrow", "Map evidence should record whether a north arrow is present.", true, "warning", false, StageId: "validate_points_and_lines", WorkflowEffect: "report_only", EvaluatorKey: "pxa_memorandum_north_arrow_present", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_scale_bar_present", "memorandum", "pxa_memorandum", "Scale bar", "Map evidence should record whether a scale bar is present.", true, "warning", false, StageId: "validate_points_and_lines", WorkflowEffect: "report_only", EvaluatorKey: "pxa_memorandum_scale_bar_present", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_notice_served_on_present", "memorandum", "pxa_memorandum", "Notices served on", "Detected memorandum should list parties on whom notice was served.", true, "configured", false, StageId: "validate_points_and_lines", WorkflowEffect: "requires_disposition", EvaluatorKey: "pxa_memorandum_notice_served_on_present", TransactionTypeProfiles: new[] { "pxa" }),
        new PreflightRuleDefinition("pxa_memorandum_appearance_parties_present", "memorandum", "pxa_memorandum", "Appeared parties", "Detected memorandum should list parties who appeared personally or by representative.", true, "configured", false, StageId: "validate_points_and_lines", WorkflowEffect: "requires_disposition", EvaluatorKey: "pxa_memorandum_appearance_parties_present", TransactionTypeProfiles: new[] { "pxa" })
    }
    .Select(rule => rule with
    {
        StageId = InferStageId(rule.RuleId, rule.Group),
        WorkflowEffect = InferWorkflowEffect(rule.RuleId, rule.Severity),
        EvaluatorKey = PreflightRuleDefinition.NormalizeEvaluatorKey(rule.RuleId, "manual_review"),
        ReportVisible = true
    })
    .ToArray();

    public PreflightRuleCatalogLoader()
    {
    }

    public PreflightRuleCatalogLoader(string? rulesPathOverride, string? settingsPathOverride = null)
    {
        this.rulesPathOverride = rulesPathOverride;
        this.settingsPathOverride = settingsPathOverride;
    }

    public PreflightRuleCatalog Load()
    {
        var catalogPath = ResolveRulesPath(settingsPathOverride, rulesPathOverride);
        if (!File.Exists(catalogPath))
        {
            return new PreflightRuleCatalog(
                catalogPath,
                UsingSafeDefaults: true,
                LoadWarning: $"Structure rules file was not found. Safe defaults are active from {PreferredRulesFileName}.",
                DefaultRules);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var validationIssues = new List<string>();
            var catalogRules = ReadAuthoritativeRules(document.RootElement, validationIssues);
            if (validationIssues.Count > 0)
            {
                return new PreflightRuleCatalog(
                    catalogPath,
                    UsingSafeDefaults: true,
                    LoadWarning: BuildFallbackWarning(validationIssues),
                    DefaultRules);
            }

            return new PreflightRuleCatalog(catalogPath, UsingSafeDefaults: false, LoadWarning: null, catalogRules);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            return new PreflightRuleCatalog(
                catalogPath,
                UsingSafeDefaults: true,
                LoadWarning: $"Structure rules could not be loaded ({exception.GetType().Name}). Safe defaults are active.",
                DefaultRules);
        }
    }

    public static string ResolveRulesPath(string? settingsPathOverride = null, string? rulesPathOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(rulesPathOverride))
        {
            return Environment.ExpandEnvironmentVariables(rulesPathOverride);
        }

        var settingsPath = settingsPathOverride ?? ProcessingEnvironmentSettings.ResolveSettingsPath();
        var settingsDirectory = Path.GetDirectoryName(settingsPath) ?? Path.Combine(AppContext.BaseDirectory, "Settings");
        var configuredPath = TryReadConfiguredPath(settingsPath);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.IsPathRooted(configuredPath)
                ? Environment.ExpandEnvironmentVariables(configuredPath)
                : Path.GetFullPath(Path.Combine(settingsDirectory, Environment.ExpandEnvironmentVariables(configuredPath)));
        }

        var preferredPath = Path.Combine(settingsDirectory, PreferredRulesFileName);
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var legacyPath = Path.Combine(settingsDirectory, LegacyRulesFileName);
        return File.Exists(legacyPath) ? legacyPath : preferredPath;
    }

    private static IReadOnlyList<PreflightRuleDefinition> ReadAuthoritativeRules(JsonElement root, List<string> validationIssues)
    {
        var schemaVersion = ReadString(root, "schema_version");
        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            validationIssues.Add($"schema_version must be {SupportedSchemaVersion}.");
        }

        var parsedRules = ReadRuleDefinitions(root, validationIssues).ToList();
        var byRuleId = new Dictionary<string, PreflightRuleDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in parsedRules)
        {
            if (!byRuleId.TryAdd(rule.RuleId, rule))
            {
                validationIssues.Add($"Duplicate rule_id '{rule.RuleId}'.");
            }
        }

        foreach (var defaultRule in DefaultRules)
        {
            if (!byRuleId.TryGetValue(defaultRule.RuleId, out var configuredRule))
            {
                if (string.Equals(defaultRule.RuleId, RequiredCadLayersRuleId, StringComparison.OrdinalIgnoreCase))
                {
                    parsedRules.Add(defaultRule);
                    byRuleId[defaultRule.RuleId] = defaultRule;
                    continue;
                }

                validationIssues.Add($"Missing required rule '{defaultRule.RuleId}'.");
                continue;
            }

            if (!string.Equals(configuredRule.Group, defaultRule.Group, StringComparison.OrdinalIgnoreCase))
            {
                validationIssues.Add($"Rule '{defaultRule.RuleId}' must keep group '{defaultRule.Group}'.");
            }

            if (defaultRule.Locked)
            {
                if (!configuredRule.Locked)
                {
                    validationIssues.Add($"Locked rule '{defaultRule.RuleId}' must remain locked.");
                }

                if (!configuredRule.Enabled)
                {
                    validationIssues.Add($"Locked rule '{defaultRule.RuleId}' cannot be disabled.");
                }

                if (!string.Equals(configuredRule.Severity, defaultRule.Severity, StringComparison.OrdinalIgnoreCase))
                {
                    validationIssues.Add($"Locked rule '{defaultRule.RuleId}' must keep severity '{defaultRule.Severity}'.");
                }

                if (!string.Equals(configuredRule.StageId, defaultRule.StageId, StringComparison.OrdinalIgnoreCase))
                {
                    validationIssues.Add($"Locked rule '{defaultRule.RuleId}' must keep stage_id '{defaultRule.StageId}'.");
                }

                if (!string.Equals(configuredRule.EvaluatorKey, defaultRule.EvaluatorKey, StringComparison.OrdinalIgnoreCase))
                {
                    validationIssues.Add($"Locked rule '{defaultRule.RuleId}' must keep evaluator_key '{defaultRule.EvaluatorKey}'.");
                }
            }
        }

        return validationIssues.Count > 0 ? DefaultRules : parsedRules;
    }

    private static IReadOnlyList<PreflightRuleDefinition> ReadRuleDefinitions(JsonElement root, List<string> validationIssues)
    {
        if (!root.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            validationIssues.Add("rules must be a JSON array.");
            return Array.Empty<PreflightRuleDefinition>();
        }

        var parsed = new List<PreflightRuleDefinition>();
        var index = 0;
        foreach (var item in rules.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                validationIssues.Add($"Rule entry {index} must be a JSON object.");
                continue;
            }

            var ruleId = RequiredString(item, "rule_id", validationIssues, index);
            var category = RequiredString(item, "category", validationIssues, index);
            var group = RequiredGroup(item, validationIssues, index);
            var displayName = RequiredString(item, "display_name", validationIssues, index);
            var description = RequiredString(item, "description", validationIssues, index);
            var severity = RequiredSeverity(item, validationIssues, index);
            var enabled = RequiredBool(item, "enabled", validationIssues, index);
            var locked = RequiredBool(item, "locked", validationIssues, index);

            if (ruleId is null
                || category is null
                || group is null
                || displayName is null
                || description is null
                || severity is null
                || enabled is null
                || locked is null)
            {
                continue;
            }

            var stageFallback = InferStageId(ruleId, group);
            var configuredStageId = ReadString(item, "stage_id");
            var stageId = PreflightRuleDefinition.NormalizeStageId(configuredStageId, stageFallback);
            if (!string.IsNullOrWhiteSpace(configuredStageId)
                && string.IsNullOrWhiteSpace(PreflightRuleDefinition.NormalizeStageId(configuredStageId, string.Empty)))
            {
                validationIssues.Add($"Rule entry {index} has unsupported stage_id '{configuredStageId}'.");
                continue;
            }

            var workflowEffectFallback = InferWorkflowEffect(ruleId, severity);
            var configuredWorkflowEffect = ReadString(item, "workflow_effect");
            var workflowEffect = PreflightRuleDefinition.NormalizeWorkflowEffect(configuredWorkflowEffect, workflowEffectFallback);
            if (!string.IsNullOrWhiteSpace(configuredWorkflowEffect)
                && string.IsNullOrWhiteSpace(PreflightRuleDefinition.NormalizeWorkflowEffect(configuredWorkflowEffect, string.Empty)))
            {
                validationIssues.Add($"Rule entry {index} has unsupported workflow_effect '{configuredWorkflowEffect}'.");
                continue;
            }

            var evaluatorFallback = PreflightRuleDefinition.NormalizeEvaluatorKey(ruleId, "manual_review");
            var configuredEvaluatorKey = ReadString(item, "evaluator_key");
            var evaluatorKey = PreflightRuleDefinition.NormalizeEvaluatorKey(configuredEvaluatorKey, evaluatorFallback);
            if (!string.IsNullOrWhiteSpace(configuredEvaluatorKey)
                && string.IsNullOrWhiteSpace(PreflightRuleDefinition.NormalizeEvaluatorKey(configuredEvaluatorKey, string.Empty)))
            {
                validationIssues.Add($"Rule entry {index} has unsupported evaluator_key '{configuredEvaluatorKey}'.");
                continue;
            }

            parsed.Add(new PreflightRuleDefinition(
                ruleId,
                group,
                category,
                displayName,
                description,
                enabled.Value,
                severity,
                locked.Value,
                stageId,
                workflowEffect,
                evaluatorKey,
                ReadOptionalBool(item, "report_visible") ?? true,
                ReadStringArray(item, "transaction_types"),
                ReadStringArray(item, "workflow_stages"),
                ReadStringArray(item, "transaction_type_profiles"),
                ReadStringArray(item, "document_profiles"),
                ReadStringArray(item, "source_roles"),
                ReadStringArray(item, "file_types"),
                ReadOptionalBool(item, "embedded_text_preferred"),
                ReadOptionalBool(item, "ocr_fallback_allowed"),
                ReadOptionalBool(item, "dwg_readiness_required"),
                ReadOptionalBool(item, "tabular_coordinates_required"),
                ReadOptionalInt(item, "minimum_coordinate_pairs"),
                ReadOptionalBool(item, "require_jamaica_bounds"),
                ReadOptionalBool(item, "allow_tabular_georeference"),
                ReadStringArrayMap(item, "required_cad_layers")));
        }

        return parsed;
    }

    private static string? TryReadConfiguredPath(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            return ReadString(document.RootElement, "structure_rules_path")
                ?? ReadString(document.RootElement, "preflight_rules_path");
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;
    }

    private static bool? ReadOptionalBool(JsonElement element, string name)
    {
        return ReadBool(element, name);
    }

    private static int? ReadOptionalInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? ReadStringArrayMap(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array || string.IsNullOrWhiteSpace(property.Name))
            {
                continue;
            }

            var aliases = property.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (aliases.Length > 0)
            {
                map[property.Name.Trim()] = aliases;
            }
        }

        return map.Count == 0 ? null : map;
    }

    private static string? RequiredString(JsonElement element, string name, List<string> validationIssues, int index)
    {
        var value = ReadString(element, name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        validationIssues.Add($"Rule entry {index} is missing required '{name}'.");
        return null;
    }

    private static string? RequiredGroup(JsonElement element, List<string> validationIssues, int index)
    {
        var group = ReadString(element, "group");
        if (string.IsNullOrWhiteSpace(group))
        {
            validationIssues.Add($"Rule entry {index} is missing required 'group'.");
            return null;
        }

        var normalized = PreflightRuleDefinition.NormalizeGroup(group, string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            validationIssues.Add($"Rule entry {index} has unsupported group '{group}'.");
            return null;
        }

        return normalized;
    }

    private static bool? RequiredBool(JsonElement element, string name, List<string> validationIssues, int index)
    {
        var value = ReadBool(element, name);
        if (value.HasValue)
        {
            return value.Value;
        }

        validationIssues.Add($"Rule entry {index} is missing required boolean '{name}'.");
        return null;
    }

    private static string? RequiredSeverity(JsonElement element, List<string> validationIssues, int index)
    {
        var severity = ReadString(element, "severity");
        if (string.IsNullOrWhiteSpace(severity))
        {
            validationIssues.Add($"Rule entry {index} is missing required 'severity'.");
            return null;
        }

        var normalized = PreflightRuleDefinition.NormalizeSeverity(severity, string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            validationIssues.Add($"Rule entry {index} has unsupported severity '{severity}'.");
            return null;
        }

        return normalized;
    }

    private static string BuildFallbackWarning(IReadOnlyList<string> validationIssues)
    {
        return $"Structure rules file is partially invalid. Safe defaults are active. {string.Join(" ", validationIssues)}";
    }

    private static string InferStageId(string ruleId, string group)
    {
        if (string.Equals(group, "supporting_document", StringComparison.OrdinalIgnoreCase))
        {
            return "supporting_document_check";
        }

        if (string.Equals(group, "georeference", StringComparison.OrdinalIgnoreCase))
        {
            return "georeference_check";
        }

        if (string.Equals(group, "dimension", StringComparison.OrdinalIgnoreCase))
        {
            return "dimension_check";
        }

        if (string.Equals(group, "memorandum", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(ruleId, "pxa_memorandum_detected", StringComparison.OrdinalIgnoreCase)
                ? "data_extraction"
                : "validate_points_and_lines";
        }

        return ruleId.Trim().ToLowerInvariant() switch
        {
            "detected_profile_presence" or "detected_profile_complete" or "required_source_roles" => "supporting_document_check",
            "georeference_source_presence" or "tabular_coordinate_columns" or "jamaica_coordinate_bounds" or "georeference_spatial_validation_readiness" => "georeference_check",
            "dimension_source_presence" or "dimension_geometry_construction_readiness" => "dimension_check",
            _ => "structure_check"
        };
    }

    private static string InferWorkflowEffect(string ruleId, string severity)
    {
        if (string.Equals(ruleId, "pxa_memorandum_detected", StringComparison.OrdinalIgnoreCase))
        {
            return "info";
        }

        if (string.Equals(ruleId, "pxa_memorandum_property_name_near_diagram", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruleId, "pxa_memorandum_north_arrow_present", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruleId, "pxa_memorandum_scale_bar_present", StringComparison.OrdinalIgnoreCase))
        {
            return "report_only";
        }

        return PreflightRuleDefinition.NormalizeSeverity(severity, "warning") switch
        {
            "blocker" => "blocker",
            "configured" => "requires_disposition",
            _ => "report_only"
        };
    }
}
