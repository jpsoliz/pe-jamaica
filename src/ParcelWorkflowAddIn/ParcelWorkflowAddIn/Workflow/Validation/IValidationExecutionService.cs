using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.Workflow.Validation;

public interface IValidationExecutionService
{
    Task<ValidationExecutionResult> RunAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        string? operatorId,
        CancellationToken cancellationToken = default);
}
