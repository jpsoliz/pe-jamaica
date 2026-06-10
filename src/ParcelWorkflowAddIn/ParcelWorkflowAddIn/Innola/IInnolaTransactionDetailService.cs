namespace ParcelWorkflowAddIn.Innola;

public interface IInnolaTransactionDetailService
{
    Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        CancellationToken cancellationToken = default);

    Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
        InnolaSession session,
        InnolaTransactionDetail detail,
        InnolaAttachmentMetadata attachment,
        CancellationToken cancellationToken = default);
}
