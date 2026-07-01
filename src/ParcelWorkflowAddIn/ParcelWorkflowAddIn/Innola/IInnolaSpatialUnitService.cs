using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Innola;

public interface IInnolaSpatialUnitService
{
    Task<InnolaSpatialUnitSaveResult> CreateOrUpdateAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        ComputeReviewDispositionDocument disposition,
        CancellationToken cancellationToken = default);
}

public sealed record InnolaSpatialUnitSaveResult(
    bool Success,
    string? SpatialUnitId,
    string Message,
    string? ErrorCategory)
{
    public static InnolaSpatialUnitSaveResult Succeeded(string? spatialUnitId, string? message = null)
    {
        return new InnolaSpatialUnitSaveResult(true, spatialUnitId, message ?? "Spatial Unit saved.", null);
    }

    public static InnolaSpatialUnitSaveResult Failed(string message, string? errorCategory = null)
    {
        return new InnolaSpatialUnitSaveResult(false, null, message, errorCategory);
    }
}
