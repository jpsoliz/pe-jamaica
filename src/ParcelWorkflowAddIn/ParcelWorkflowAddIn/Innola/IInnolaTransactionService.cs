namespace ParcelWorkflowAddIn.Innola;

public interface IInnolaTransactionService
{
    Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default);
}
