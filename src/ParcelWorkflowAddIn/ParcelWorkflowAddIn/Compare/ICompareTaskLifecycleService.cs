namespace ParcelWorkflowAddIn.Compare;

public interface ICompareTaskLifecycleService
{
    Task<CompareTaskLifecycleResult> SuspendAsync(string transactionNumber, CancellationToken cancellationToken = default);

    Task<CompareTaskLifecycleResult> CompleteAsync(string transactionNumber, CancellationToken cancellationToken = default);
}

public sealed record CompareTaskLifecycleResult(bool Success, string Message, bool ShouldCloseWorkspace)
{
    public static CompareTaskLifecycleResult Succeeded(string message)
    {
        return new CompareTaskLifecycleResult(true, message, true);
    }

    public static CompareTaskLifecycleResult Failure(string message)
    {
        return new CompareTaskLifecycleResult(false, message, false);
    }
}
