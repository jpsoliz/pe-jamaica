using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.WorkflowRules;

public sealed record WorkflowRuleResolutionContext(
    string? TransactionType,
    string ProcessStep,
    DetectedSourceInputProfile? DetectedProfile,
    IReadOnlyList<ManifestSourceFile> SourceFiles,
    WorkflowRuleSettings Settings);

public sealed record WorkflowRuleSettings(
    string OcrEngine,
    bool OpenAiEnabled,
    string OpenAiExtractionProfile,
    string OpenAiModel,
    string OpenAiApiKeyEnvironmentVariable,
    string CredentialProfile)
{
    public static WorkflowRuleSettings Default { get; } = new(
        "local",
        false,
        "custom",
        string.Empty,
        "OPENAI_API_KEY",
        "local");
}

public sealed record WorkflowRuleResolutionResult(
    bool Success,
    WorkflowRule? Rule,
    WorkflowScriptPlan? ScriptPlan,
    string? ErrorMessage)
{
    public static WorkflowRuleResolutionResult Matched(WorkflowRule rule, WorkflowScriptPlan scriptPlan)
    {
        return new WorkflowRuleResolutionResult(true, rule, scriptPlan, null);
    }

    public static WorkflowRuleResolutionResult NoMatch(string message)
    {
        return new WorkflowRuleResolutionResult(false, null, null, message);
    }
}
