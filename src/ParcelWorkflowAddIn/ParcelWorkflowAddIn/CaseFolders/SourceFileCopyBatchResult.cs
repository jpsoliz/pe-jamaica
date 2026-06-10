namespace ParcelWorkflowAddIn.CaseFolders;

public sealed class SourceFileCopyBatchResult
{
    public SourceFileCopyBatchResult(IReadOnlyList<SourceFileCopyResult> results)
    {
        Results = results;
    }

    public IReadOnlyList<SourceFileCopyResult> Results { get; }

    public bool Success => Results.Count > 0 && Results.All(result => result.Copied);
}
