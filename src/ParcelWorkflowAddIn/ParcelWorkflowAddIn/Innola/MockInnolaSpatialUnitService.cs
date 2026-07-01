using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaSpatialUnitService : IInnolaSpatialUnitService
{
    public Task<InnolaSpatialUnitSaveResult> CreateOrUpdateAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        ComputeReviewDispositionDocument disposition,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InnolaSpatialUnitSaveResult.Succeeded($"mock-spatial-unit-{transaction.TransactionNumber}"));
    }
}
