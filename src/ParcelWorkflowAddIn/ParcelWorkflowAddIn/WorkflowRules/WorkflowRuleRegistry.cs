using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.WorkflowRules;

public sealed class WorkflowRuleRegistry
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string> getRulesPath;

    public WorkflowRuleRegistry()
        : this(ResolveDefaultRulesPath)
    {
    }

    public WorkflowRuleRegistry(Func<string> getRulesPath)
    {
        this.getRulesPath = getRulesPath;
    }

    public WorkflowRuleDocument Load()
    {
        var rulesPath = getRulesPath();
        if (!File.Exists(rulesPath))
        {
            return new WorkflowRuleDocument("1.0.0", Array.Empty<WorkflowRule>());
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowRuleDocument>(File.ReadAllText(rulesPath), Options)
                ?? new WorkflowRuleDocument("1.0.0", Array.Empty<WorkflowRule>());
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
        {
            return new WorkflowRuleDocument("1.0.0", Array.Empty<WorkflowRule>());
        }
    }

    public static string ResolveDefaultRulesPath()
    {
        foreach (var candidate in GetRulesPathCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(GetAssemblyDirectory(), "Settings", "WorkflowRules.json");
    }

    private static IEnumerable<string> GetRulesPathCandidates()
    {
        var assemblyDirectory = GetAssemblyDirectory();
        yield return Path.Combine(assemblyDirectory, "Settings", "WorkflowRules.json");
        yield return Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "Settings", "WorkflowRules.json"));
        yield return Path.Combine(AppContext.BaseDirectory, "Settings", "WorkflowRules.json");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Settings", "WorkflowRules.json"));
    }

    private static string GetAssemblyDirectory()
    {
        var assemblyPath = typeof(WorkflowRuleRegistry).Assembly.Location;
        var directory = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetDirectoryName(assemblyPath);
        return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
    }
}
