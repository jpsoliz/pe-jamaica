using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Workflow.Output;

public interface IEnterpriseWorkingStateRestoreService
{
    EnterpriseWorkingStateRestoreResult Restore(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        WorkflowState resolvedState,
        OutputSummaryDocument? localOutputSummary);
}

public sealed record EnterpriseWorkingStateRestoreResult(
    bool Attempted,
    bool EnterpriseStateFound,
    string RestoreSource,
    string StatusMessage,
    OutputSummaryDocument? RestoredOutputSummary,
    IReadOnlyList<AvailableArtifact> AddedArtifacts,
    IReadOnlyList<RecoverabilityIssue> RecoverabilityIssues)
{
    public const string RestoreSourceNone = "none";
    public const string RestoreSourceLocalOnly = "local_only";
    public const string RestoreSourceEnterpriseOnly = "enterprise_only";
    public const string RestoreSourceLocalAndEnterprise = "local_and_enterprise";

    public static EnterpriseWorkingStateRestoreResult Skipped(string restoreSource, string statusMessage) =>
        new(false, false, restoreSource, statusMessage, null, Array.Empty<AvailableArtifact>(), Array.Empty<RecoverabilityIssue>());
}
