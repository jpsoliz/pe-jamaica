using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.Workflow.Execution;

public interface IWorkflowScriptExecutor
{
    Task<WorkflowScriptExecutionResult> ExecuteDraftExtractionAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        CancellationToken cancellationToken = default);
}
