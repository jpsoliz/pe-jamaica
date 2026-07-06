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
    string? ErrorCategory,
    IReadOnlyList<InnolaSpatialUnitPolygonReference>? PolygonReferences = null)
{
    public static InnolaSpatialUnitSaveResult Succeeded(
        string? spatialUnitId,
        string? message = null,
        IReadOnlyList<InnolaSpatialUnitPolygonReference>? polygonReferences = null)
    {
        return new InnolaSpatialUnitSaveResult(true, spatialUnitId, message ?? "Spatial Unit saved.", null, polygonReferences);
    }

    public static InnolaSpatialUnitSaveResult Failed(string message, string? errorCategory = null)
    {
        return new InnolaSpatialUnitSaveResult(false, null, message, errorCategory);
    }
}

public sealed record InnolaSpatialUnitPolygonReference(
    string? ParcelName,
    string? SpatialUnitId,
    string? SpatialUnitSuid);
