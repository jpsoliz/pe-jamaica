using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Workflow.Output;

public interface IEnterpriseWorkingDispositionService
{
    Task<EnterpriseWorkingDispositionResult> RecordDispositionAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        ComputeReviewDispositionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EnterpriseWorkingDispositionResult(
    bool Success,
    string Message,
    string? EvidencePath,
    IReadOnlyList<string> Errors)
{
    public static EnterpriseWorkingDispositionResult Succeeded(string message, string evidencePath)
    {
        return new EnterpriseWorkingDispositionResult(true, message, evidencePath, Array.Empty<string>());
    }

    public static EnterpriseWorkingDispositionResult Failed(string message, IReadOnlyList<string> errors)
    {
        return new EnterpriseWorkingDispositionResult(false, message, null, errors);
    }
}
