using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.WorkflowRules;

public sealed class WorkflowRuleResolver
{
    private static readonly JsonSerializerOptions HashOptions = new()
    {
        WriteIndented = false
    };

    private readonly WorkflowRuleRegistry registry;
    private readonly Func<DateTimeOffset> getUtcNow;

    public WorkflowRuleResolver()
        : this(new WorkflowRuleRegistry(), () => DateTimeOffset.UtcNow)
    {
    }

    public WorkflowRuleResolver(WorkflowRuleRegistry registry, Func<DateTimeOffset>? getUtcNow = null)
    {
        this.registry = registry;
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public WorkflowRuleResolutionResult Resolve(WorkflowRuleResolutionContext context)
    {
        var rules = registry.Load().Rules
            .Where(rule => MatchesProcessStep(rule, context.ProcessStep))
            .Where(rule => MatchesProfileScope(rule, context))
            .Where(rule => MatchesSources(rule, context.SourceFiles))
            .ToArray();

        if (rules.Length == 0)
        {
            return WorkflowRuleResolutionResult.NoMatch(BuildNoMatchMessage(context));
        }

        var selected = rules
            .OrderByDescending(rule => MatchScore(rule, context))
            .ThenBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase)
            .First();

        var plan = BuildScriptPlan(selected, context);
        return WorkflowRuleResolutionResult.Matched(selected, plan);
    }

    public static string ComputeSourceManifestHash(IReadOnlyList<ManifestSourceFile> sources)
    {
        var stableSources = sources
            .Select(source => new
            {
                original_path = source.OriginalPath,
                copied_path = RelativeOrFileName(source.CopiedPath),
                file_type = source.FileType,
                file_size = source.FileSize,
                source_role = SourceRole.Normalize(source.SourceRole),
                source_type = source.SourceType
            })
            .OrderBy(source => source.source_role, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.copied_path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serialized = JsonSerializer.Serialize(stableSources, HashOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private WorkflowScriptPlan BuildScriptPlan(WorkflowRule rule, WorkflowRuleResolutionContext context)
    {
        var steps = rule.ScriptPlan.Select(step => new WorkflowScriptStep(
            step.StepName,
            step.Adapter,
            step.Script,
            step.InputRoles,
            step.OutputArtifacts,
            ResolveParameters(step.Parameters, context.Settings),
            step.TimeoutSeconds,
            context.Settings.OpenAiEnabled,
            SafeSettingIdentifier(context.Settings.OcrEngine, WorkflowRuleSettings.Default.OcrEngine),
            SafeSettingIdentifier(context.Settings.CredentialProfile, WorkflowRuleSettings.Default.CredentialProfile))).ToArray();

        return new WorkflowScriptPlan(
            "1.0.0",
            rule.RuleId,
            rule.RuleVersion,
            rule.WorkflowProfile,
            getUtcNow().UtcDateTime.ToString("O"),
            ComputeSourceManifestHash(context.SourceFiles),
            steps);
    }

    private static IReadOnlyDictionary<string, string> ResolveParameters(
        IReadOnlyDictionary<string, string> parameters,
        WorkflowRuleSettings settings)
    {
        return parameters
            .Where(parameter => !LooksSecretKey(parameter.Key) && !LooksSecretValue(parameter.Value))
            .Select(parameter => new KeyValuePair<string, string>(parameter.Key, ResolveSettingToken(parameter.Value, settings)))
            .Where(parameter => !LooksSecretValue(parameter.Value))
            .ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveSettingToken(string value, WorkflowRuleSettings settings)
    {
        return value switch
        {
            "{settings.ocr_engine}" => settings.OcrEngine,
            "{settings.openai_model}" => settings.OpenAiModel,
            "{settings.openai_api_key_environment_variable}" => settings.OpenAiApiKeyEnvironmentVariable,
            "{settings.credential_profile}" => settings.CredentialProfile,
            _ => value
        };
    }

    private static bool MatchesProcessStep(WorkflowRule rule, string processStep)
    {
        return rule.ProcessSteps is null
            || rule.ProcessSteps.Count == 0
            || rule.ProcessSteps.Any(step => step.Equals(processStep, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesProfileScope(WorkflowRule rule, WorkflowRuleResolutionContext context)
    {
        return MatchesAny(rule.TransactionTypeProfiles, context.TransactionTypeProfileId)
            && MatchesAny(rule.DocumentProfiles, context.DocumentProfile);
    }

    private static bool MatchesAny(IReadOnlyList<string>? candidates, string? value)
    {
        return candidates is null
            || candidates.Count == 0
            || (!string.IsNullOrWhiteSpace(value)
                && candidates.Any(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool MatchesSources(WorkflowRule rule, IReadOnlyList<ManifestSourceFile> sources)
    {
        foreach (var required in rule.RequiredSources)
        {
            var extensions = new HashSet<string>(required.Extensions.Select(NormalizeExtension), StringComparer.OrdinalIgnoreCase);
            var matched = sources.Any(source =>
                SourceRole.Matches(source.SourceRole, required.Role)
                && extensions.Contains(NormalizeExtension(source.FileType)));
            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static int MatchScore(WorkflowRule rule, WorkflowRuleResolutionContext context)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(context.TransactionType)
            && rule.TransactionTypes?.Any(type => type.Equals(context.TransactionType, StringComparison.OrdinalIgnoreCase)) == true)
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(context.DetectedProfile?.ProfileCode)
            && rule.DetectedProfiles?.Any(profile => profile.Equals(context.DetectedProfile.ProfileCode, StringComparison.OrdinalIgnoreCase)) == true)
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(context.TransactionTypeProfileId)
            && rule.TransactionTypeProfiles?.Any(profile => profile.Equals(context.TransactionTypeProfileId, StringComparison.OrdinalIgnoreCase)) == true)
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(context.DocumentProfile)
            && rule.DocumentProfiles?.Any(profile => profile.Equals(context.DocumentProfile, StringComparison.OrdinalIgnoreCase)) == true)
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(context.WorkflowProfile)
            && rule.WorkflowProfile.Equals(context.WorkflowProfile, StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        if (rule.ProcessSteps?.Any(step => step.Equals(context.ProcessStep, StringComparison.OrdinalIgnoreCase)) == true)
        {
            score += 2;
        }

        score += rule.RequiredSources.Count;
        return score;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith(".", StringComparison.Ordinal)
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
    }

    private static bool LooksSecretKey(string value)
    {
        return value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("certificate_private", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSecretValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.EndsWith("_API_KEY", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("_TOKEN", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("_PASSWORD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
            || value.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret-password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("access_token=", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeSettingIdentifier(string value, string fallback)
    {
        return LooksSecretKey(value) || LooksSecretValue(value)
            ? fallback
            : value;
    }

    private static string RelativeOrFileName(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path);
    }

    private static string BuildNoMatchMessage(WorkflowRuleResolutionContext context)
    {
        var sourceRoles = context.SourceFiles
            .Select(source => SourceRole.Normalize(source.SourceRole) ?? source.SourceRole ?? "unknown")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase);
        return "No workflow rule matches the transaction type/profile and copied source files. "
            + $"transaction_type={context.TransactionType ?? "unknown"}; "
            + $"transaction_profile={context.TransactionTypeProfileId ?? "unknown"}; "
            + $"workflow_profile={context.WorkflowProfile ?? "unknown"}; "
            + $"document_profile={context.DocumentProfile ?? "unknown"}; "
            + $"source_roles={string.Join(", ", sourceRoles)}.";
    }
}
