namespace ParcelWorkflowAddIn.Innola;

public sealed class StayOnCurrentTransactionDecisionProvider : IActiveTransactionSwitchDecisionProvider
{
    public ActiveTransactionSwitchDecision Decide(SelectedInnolaTransaction activeTransaction, InnolaTransactionRow requestedTransaction)
    {
        return ActiveTransactionSwitchDecision.StayOnCurrentTransaction;
    }
}
