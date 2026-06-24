using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Output;

public sealed class JsonEnterpriseReviewPublishService : IEnterpriseWorkingLayerPublishService
{
    private readonly Func<InnolaTransactionSettings> getSettings;
    private readonly JsonEnterpriseWorkingLayerPublishService workingLayerPublishService;
    private readonly JsonEnterpriseParcelFabricPublishService parcelFabricPublishService;

    public JsonEnterpriseReviewPublishService()
        : this(InnolaTransactionSettings.Load)
    {
    }

    internal JsonEnterpriseReviewPublishService(Func<InnolaTransactionSettings> getSettings)
    {
        this.getSettings = getSettings;
        workingLayerPublishService = new JsonEnterpriseWorkingLayerPublishService(getSettings);
        parcelFabricPublishService = new JsonEnterpriseParcelFabricPublishService(getSettings);
    }

    public Task<EnterpriseWorkingLayerPublishResult> PublishAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        var settings = getSettings();
        if (string.Equals(settings.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric, StringComparison.OrdinalIgnoreCase))
        {
            return parcelFabricPublishService.PublishAsync(layout, manifest, outputSummary, operatorId, cancellationToken);
        }

        return workingLayerPublishService.PublishAsync(layout, manifest, outputSummary, operatorId, cancellationToken);
    }
}
