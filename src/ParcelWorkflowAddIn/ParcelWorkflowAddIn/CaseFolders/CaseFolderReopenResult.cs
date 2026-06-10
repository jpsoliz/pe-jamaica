using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record CaseFolderReopenResult(
    bool Success,
    CaseFolderLayout? Layout,
    ManifestDocument? Manifest,
    WorkflowState ResolvedState,
    IReadOnlyList<SourceFileCopyResult> SourceFiles,
    IReadOnlyList<AvailableArtifact> AvailableArtifacts,
    IReadOnlyList<RecoverabilityIssue> RecoverabilityIssues)
{
    public static CaseFolderReopenResult Failed(params RecoverabilityIssue[] issues)
    {
        return new CaseFolderReopenResult(
            false,
            null,
            null,
            WorkflowState.NoCase,
            Array.Empty<SourceFileCopyResult>(),
            Array.Empty<AvailableArtifact>(),
            issues);
    }
}
