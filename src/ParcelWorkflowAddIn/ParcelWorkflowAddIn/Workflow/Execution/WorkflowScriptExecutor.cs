using System.IO;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed class WorkflowScriptExecutor : IWorkflowScriptExecutor
{
    private readonly IReadOnlyDictionary<string, IWorkflowScriptAdapter> adapters;
    private readonly Func<WorkflowRuleSettings> getRuleSettings;
    private readonly Func<WorkflowExecutionSettings> getExecutionSettings;

    public WorkflowScriptExecutor()
        : this(
            new IWorkflowScriptAdapter[]
            {
                new CreateParcelDraftExtractionAdapter()
            },
            WorkflowRuleSettingsLoader.Load,
            WorkflowExecutionSettings.Load)
    {
    }

    public WorkflowScriptExecutor(
        IEnumerable<IWorkflowScriptAdapter> adapters,
        Func<WorkflowRuleSettings> getRuleSettings,
        Func<WorkflowExecutionSettings> getExecutionSettings)
    {
        this.adapters = adapters.ToDictionary(adapter => adapter.AdapterId, StringComparer.OrdinalIgnoreCase);
        this.getRuleSettings = getRuleSettings;
        this.getExecutionSettings = getExecutionSettings;
    }

    public async Task<WorkflowScriptExecutionResult> ExecuteDraftExtractionAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        CancellationToken cancellationToken = default)
    {
        var scriptPlan = manifest.Payload.ScriptPlan;
        if (scriptPlan is null || scriptPlan.Steps.Count == 0)
        {
            return WorkflowScriptExecutionResult.Failed("No script plan is available for extraction.");
        }

        var sharedItems = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var artifacts = new List<AvailableArtifact>();
        var ruleSettings = getRuleSettings();
        var executionSettings = getExecutionSettings();

        foreach (var step in scriptPlan.Steps)
        {
            if (!adapters.TryGetValue(step.Adapter, out var adapter))
            {
                return WorkflowScriptExecutionResult.Failed($"No workflow adapter is registered for '{step.Adapter}'.");
            }

            var context = new WorkflowScriptExecutionContext(
                layout,
                manifest,
                scriptPlan,
                step,
                ruleSettings,
                executionSettings,
                sharedItems);

            var stepResult = await adapter.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            if (!stepResult.Success)
            {
                return WorkflowScriptExecutionResult.Failed(stepResult.ErrorMessage ?? $"Step '{step.StepName}' failed.");
            }

            foreach (var artifactPath in stepResult.ArtifactPaths.Where(File.Exists))
            {
                artifacts.Add(new AvailableArtifact(Path.GetFileName(artifactPath), artifactPath));
            }
        }

        var reviewArtifactPath = Path.Combine(layout.WorkingDirectory, "extraction_review_data.json");
        if (!File.Exists(reviewArtifactPath))
        {
            return WorkflowScriptExecutionResult.Failed("Draft extraction completed without creating extraction_review_data.json.");
        }

        if (!artifacts.Any(artifact => string.Equals(artifact.Path, reviewArtifactPath, StringComparison.OrdinalIgnoreCase)))
        {
            artifacts.Add(new AvailableArtifact("extraction_review_data.json", reviewArtifactPath));
        }

        return new WorkflowScriptExecutionResult(true, null, reviewArtifactPath, artifacts);
    }
}
