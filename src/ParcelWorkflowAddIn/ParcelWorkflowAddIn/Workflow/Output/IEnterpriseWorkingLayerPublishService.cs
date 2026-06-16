using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.Workflow.Output;

public interface IEnterpriseWorkingLayerPublishService
{
    Task<EnterpriseWorkingLayerPublishResult> PublishAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        OutputSummaryDocument outputSummary,
        string? operatorId,
        CancellationToken cancellationToken = default);
}

public sealed record EnterpriseWorkingLayerPublishResult(
    bool Attempted,
    bool Success,
    string Message,
    string? SummaryPath,
    EnterpriseWorkingPublishSummary? Summary)
{
    public static EnterpriseWorkingLayerPublishResult Skipped(string message) =>
        new(false, true, message, null, null);

    public static EnterpriseWorkingLayerPublishResult Failed(string message, string? summaryPath, EnterpriseWorkingPublishSummary? summary) =>
        new(true, false, message, summaryPath, summary);

    public static EnterpriseWorkingLayerPublishResult Succeeded(string message, string summaryPath, EnterpriseWorkingPublishSummary summary) =>
        new(true, true, message, summaryPath, summary);
}
