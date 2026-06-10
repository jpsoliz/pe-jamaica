namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionLifecycleRequest(
    InnolaSession Session,
    SelectedInnolaTransaction Transaction,
    string CaseFolderPath,
    string? LifecycleStatus,
    string? Reason);
