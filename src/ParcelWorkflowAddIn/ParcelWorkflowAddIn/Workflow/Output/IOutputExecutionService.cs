using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.Workflow.Output;

public interface IOutputExecutionService
{
    Task<OutputExecutionResult> RunAsync(
        CaseFolderLayout layout,
        ManifestDocument manifest,
        string? operatorId,
        CancellationToken cancellationToken = default);
}

public sealed record OutputExecutionResult(
    bool Success,
    string? ErrorMessage,
    string? SummaryPath,
    OutputSummaryDocument? Summary)
{
    public static OutputExecutionResult Failed(string message)
    {
        return new OutputExecutionResult(false, message, null, null);
    }
}
