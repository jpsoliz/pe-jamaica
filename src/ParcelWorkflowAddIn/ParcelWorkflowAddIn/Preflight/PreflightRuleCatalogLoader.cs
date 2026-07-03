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
        new PreflightRuleDefinition("georeference_source_presence", "georeference", "georeference", "Dimension source presence", "At least one source with usable coordinate context must be present before Validate Points can begin.", true, "blocker", true, SourceRoles: new[] { "computation_sheet", "coordinate_text_source", "plan_map_reference" }, AllowTabularGeoreference: true),
        new PreflightRuleDefinition("tabular_coordinate_columns", "georeference", "georeference", "Tabular coordinate columns", "TXT/CSV coordinate sources should expose Easting/Northing-style columns when they are used for georeference support.", true, "warning", false, SourceRoles: new[] { "coordinate_text_source" }, FileTypes: new[] { ".txt", ".csv" }, TabularCoordinatesRequired: true),
        new PreflightRuleDefinition("jamaica_coordinate_bounds", "georeference", "georeference", "Jamaica coordinate bounds", "When tabular coordinates are available, the sample coordinate pairs should fall within Jamaica working bounds.", true, "warning", false, SourceRoles: new[] { "coordinate_text_source" }, FileTypes: new[] { ".txt", ".csv" }, MinimumCoordinatePairs: 1, RequireJamaicaBounds: true, AllowTabularGeoreference: true)
    };

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

            parsed.Add(new PreflightRuleDefinition(
                ruleId,
                group,
                category,
                displayName,
                description,
                enabled.Value,
                severity,
                locked.Value,
                ReadStringArray(item, "transaction_types"),
                ReadStringArray(item, "workflow_stages"),
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
}
