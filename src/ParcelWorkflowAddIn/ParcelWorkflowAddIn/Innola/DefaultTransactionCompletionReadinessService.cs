namespace ParcelWorkflowAddIn.Innola;

public sealed class DefaultTransactionCompletionReadinessService : ITransactionCompletionReadinessService
{
    public TransactionCompletionReadinessResult CheckReadiness(string caseFolderPath)
    {
        return TransactionCompletionReadinessResult.Blocked(
            "sync_readiness_not_met",
            "Complete is blocked until downstream sync/readiness criteria are met.");
    }
}
