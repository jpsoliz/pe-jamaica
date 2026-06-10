namespace ParcelWorkflowAddIn.Innola;

public sealed record SelectedInnolaTransaction(
    string TaskId,
    string TransactionId,
    string TransactionNumber,
    string TaskName,
    string ProcessStep,
    DateTimeOffset SelectedAt);
