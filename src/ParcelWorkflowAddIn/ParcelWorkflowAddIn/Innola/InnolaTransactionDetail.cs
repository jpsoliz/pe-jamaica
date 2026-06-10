namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionDetail(
    string TransactionId,
    string TransactionNumber,
    string TaskId,
    string TaskName,
    string ProcessStep,
    string? CaseType,
    string? ProfileHint,
    string? AssignedUser,
    string? AssignedGroup,
    string? OwnerUser,
    string? ClaimStatus,
    IReadOnlyList<InnolaAttachmentMetadata> Attachments);
