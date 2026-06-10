using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Preflight;

public interface IProcessingEnvironmentPreflightService
{
    Task<ProcessingEnvironmentPreflightResult> RunAsync(CaseFolderLayout layout, CancellationToken cancellationToken = default);
}
