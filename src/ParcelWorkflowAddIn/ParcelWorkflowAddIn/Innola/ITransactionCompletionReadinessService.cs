namespace ParcelWorkflowAddIn.Innola;

public interface ITransactionCompletionReadinessService
{
    TransactionCompletionReadinessResult CheckReadiness(string caseFolderPath);
}
