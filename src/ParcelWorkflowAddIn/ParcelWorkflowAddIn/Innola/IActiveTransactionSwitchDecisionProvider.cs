namespace ParcelWorkflowAddIn.Innola;

public interface IActiveTransactionSwitchDecisionProvider
{
    ActiveTransactionSwitchDecision Decide(SelectedInnolaTransaction activeTransaction, InnolaTransactionRow requestedTransaction);
}
