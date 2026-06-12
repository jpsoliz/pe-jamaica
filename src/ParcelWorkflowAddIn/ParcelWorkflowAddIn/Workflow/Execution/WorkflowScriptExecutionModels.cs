using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public sealed class WorkflowScriptExecutionContext
{
    public WorkflowScriptExecutionContext(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        WorkflowScriptPlan scriptPlan,
        WorkflowScriptStep step,
        WorkflowRuleSettings ruleSettings,
        WorkflowExecutionSettings executionSettings,
        IDictionary<string, object?> sharedItems)
    {
        Layout = layout;
        Manifest = manifest;
        ScriptPlan = scriptPlan;
        Step = step;
        RuleSettings = ruleSettings;
        ExecutionSettings = executionSettings;
        SharedItems = sharedItems;
    }

    public CaseFolderLayout Layout { get; }

    public ManifestDocument Manifest { get; }

    public WorkflowScriptPlan ScriptPlan { get; }

    public WorkflowScriptStep Step { get; }

    public WorkflowRuleSettings RuleSettings { get; }

    public WorkflowExecutionSettings ExecutionSettings { get; }

    public IDictionary<string, object?> SharedItems { get; }
}

public sealed record WorkflowScriptStepExecutionResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<string> ArtifactPaths)
{
    public static WorkflowScriptStepExecutionResult Passed(params string[] artifactPaths)
    {
        return new WorkflowScriptStepExecutionResult(true, null, artifactPaths);
    }

    public static WorkflowScriptStepExecutionResult Failed(string message)
    {
        return new WorkflowScriptStepExecutionResult(false, message, Array.Empty<string>());
    }
}

public sealed record WorkflowScriptExecutionResult(
    bool Success,
    string? ErrorMessage,
    string? ReviewArtifactPath,
    IReadOnlyList<AvailableArtifact> Artifacts)
{
    public static WorkflowScriptExecutionResult Failed(string message)
    {
        return new WorkflowScriptExecutionResult(false, message, null, Array.Empty<AvailableArtifact>());
    }
}
