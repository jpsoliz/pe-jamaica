using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Preflight;

public sealed class NoOpProcessingEnvironmentPreflightService : IProcessingEnvironmentPreflightService
{
    public Task<ProcessingEnvironmentPreflightResult> RunAsync(CaseFolderLayout layout, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessingEnvironmentPreflightResult.Empty);
    }
}
