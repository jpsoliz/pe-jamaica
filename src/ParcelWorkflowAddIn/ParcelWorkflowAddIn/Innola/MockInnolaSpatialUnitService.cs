using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaSpatialUnitService : IInnolaSpatialUnitService
{
    public Task<InnolaSpatialUnitExaminationNumberResult> GetExaminationNumberAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string examinationFieldName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InnolaSpatialUnitExaminationNumberResult.Failed(
            "SpatialUnit examinationNumber is not available for this transaction.",
            "spatial_unit_examination_missing"));
    }

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
