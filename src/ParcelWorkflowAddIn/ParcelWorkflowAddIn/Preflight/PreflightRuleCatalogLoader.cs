using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class PreflightRuleCatalogLoader
{
    private const string DefaultRulesFileName = "PreflightRules.json";
    private const string SupportedSchemaVersion = "1.0.0";
    private readonly string? rulesPathOverride;
    private readonly string? settingsPathOverride;

    private static readonly IReadOnlyList<PreflightRuleDefinition> DefaultRules = new[]
    {
        new PreflightRuleDefinition("detected_profile_presence", "manifest", "Detected profile present", "Detected intake profile must be present before Processing Checks can continue.", true, "blocker", true),
        new PreflightRuleDefinition("detected_profile_complete", "manifest", "Detected profile complete", "Incomplete intake remains blocked until required source roles are resolved.", true, "blocker", true),
        new PreflightRuleDefinition("required_source_roles", "manifest", "Required source roles", "Each workflow profile must provide the required copied source roles.", true, "blocker", true),
        new PreflightRuleDefinition("source_file_integrity", "manifest", "Copied source integrity", "Copied source paths must stay inside the case folder, exist, use supported extensions, and remain readable.", true, "blocker", true),
        new PreflightRuleDefinition("workflow_rule_resolution", "workflow_rule", "Workflow rule resolution", "Transactions must resolve to a current workflow rule and script plan.", true, "blocker", true),
        new PreflightRuleDefinition("arcgis_sdk_lane", "arcgis_pro", "ArcGIS Pro SDK lane", "SDK lane and target framework must match the supported ArcGIS Pro 3.6 add-in lane.", true, "blocker", true),
        new PreflightRuleDefinition("workspace_access", "write_access", "Workspace access", "Case folder working, output, and summary locations must remain writable.", true, "blocker", true),
        new PreflightRuleDefinition("python_executable_health", "python", "Python executable health", "Configured Python executable must be set, exist, and be invokable.", true, "blocker", true),
        new PreflightRuleDefinition("arcgis_unknown_version_behavior", "arcgis_pro", "Unknown ArcGIS Pro version handling", "Controls whether unknown ArcGIS Pro version detection is treated as a warning or blocker.", true, "warning", false),
        new PreflightRuleDefinition("python_package_probe", "python", "Python package probe", "Checks configured required and optional Python packages such as ArcPy before downstream processing runs.", true, "configured", false),
        new PreflightRuleDefinition("dwg_signature_check", "dwg", "DWG file signature", "DWG reference files must be non-empty and contain a recognizable DWG signature.", true, "blocker", true),
        new PreflightRuleDefinition("dwg_readiness_probe", "dwg", "DWG readiness probe", "Optional CAD sub-layer readiness probe for copied DWG references.", true, "blocker", false)
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
                LoadWarning: $"Preflight rules file was not found. Safe defaults are active from {DefaultRulesFileName}.",
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
                LoadWarning: $"Preflight rules could not be loaded ({exception.GetType().Name}). Safe defaults are active.",
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

        return Path.Combine(settingsDirectory, DefaultRulesFileName);
    }

    private static IReadOnlyList<PreflightRuleDefinition> ReadAuthoritativeRules(JsonElement root, List<string> validationIssues)
    {
        var schemaVersion = ReadString(root, "schema_version");
        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            validationIssues.Add($"schema_version must be {SupportedSchemaVersion}.");
        }

        var parsedRules = ReadRuleDefinitions(root, validationIssues);
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
                validationIssues.Add($"Missing required rule '{defaultRule.RuleId}'.");
                continue;
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
            var displayName = RequiredString(item, "display_name", validationIssues, index);
            var description = RequiredString(item, "description", validationIssues, index);
            var severity = RequiredSeverity(item, validationIssues, index);
            var enabled = RequiredBool(item, "enabled", validationIssues, index);
            var locked = RequiredBool(item, "locked", validationIssues, index);

            if (ruleId is null
                || category is null
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
                category,
                displayName,
                description,
                enabled.Value,
                severity,
                locked.Value));
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
            return ReadString(document.RootElement, "preflight_rules_path");
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
        return $"Preflight rules file is partially invalid. Safe defaults are active. {string.Join(" ", validationIssues)}";
    }
}
