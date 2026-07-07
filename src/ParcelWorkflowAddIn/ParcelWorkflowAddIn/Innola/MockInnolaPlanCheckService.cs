using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaPlanCheckService : IInnolaPlanCheckService
{
    public Task<InnolaPlanCheckWritebackResult> WriteAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        ComputeReviewDispositionDocument disposition,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InnolaPlanCheckWritebackResult.Succeeded("Mock Innola Plan Check values saved."));
    }
}
