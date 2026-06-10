namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionLifecycleService : IInnolaTransactionLifecycleService
{
    public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NotConfigured("Innola claim endpoint is not configured."));
    }

    public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NotConfigured("Innola save-state endpoint is not configured."));
    }

    public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NotConfigured("Innola complete endpoint is not configured."));
    }

    private static InnolaTransactionLifecycleResult NotConfigured(string message)
    {
        return InnolaTransactionLifecycleResult.Failure(message, "lifecycle_endpoint_not_configured");
    }
}
