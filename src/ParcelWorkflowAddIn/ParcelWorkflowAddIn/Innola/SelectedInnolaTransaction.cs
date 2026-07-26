namespace ParcelWorkflowAddIn.Innola;

public sealed record SelectedInnolaTransaction(
    string TaskId,
    string TransactionId,
    string TransactionNumber,
    string TaskName,
    string ProcessStep,
    DateTimeOffset SelectedAt,
    string? ApplicationId = null,
    string? TransactionType = null,
    InnolaTransactionStatus Status = InnolaTransactionStatus.Unknown,
    string? AssignedUser = null,
    string? AssignedGroup = null);
