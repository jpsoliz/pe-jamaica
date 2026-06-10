namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionDetailService : IInnolaTransactionDetailService
{
    public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
        InnolaSession session,
        SelectedInnolaTransaction selectedTransaction,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InnolaTransactionDetailResult.Failure(
            "Transaction detail API mapping is not configured for this environment. Use mock mode or configure the Innola adapter.",
            "adapter_not_configured"));
    }

    public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
        InnolaSession session,
        InnolaTransactionDetail detail,
        InnolaAttachmentMetadata attachment,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InnolaAttachmentContentResult.Failure(
            "Attachment download API mapping is not configured for this environment. Use mock mode or configure the Innola adapter.",
            "adapter_not_configured"));
    }
}
