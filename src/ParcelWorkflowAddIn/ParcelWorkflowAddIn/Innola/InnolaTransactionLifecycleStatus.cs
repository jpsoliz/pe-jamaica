namespace ParcelWorkflowAddIn.Innola;

public enum InnolaTransactionLifecycleStatus
{
    None,
    Loaded,
    InProgress,
    SaveProgress,
    Cancelled,
    CompleteBlocked,
    Completing,
    Completed,
    Error
}
