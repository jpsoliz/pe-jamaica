using ParcelWorkflowAddIn.Workflow.Disposition;

namespace ParcelWorkflowAddIn.Innola;

public interface IInnolaPlanCheckService
{
    Task<InnolaPlanCheckWritebackResult> WriteAsync(
        InnolaSession session,
        SelectedInnolaTransaction transaction,
        string caseFolderPath,
        ComputeReviewDispositionDocument disposition,
        CancellationToken cancellationToken = default);
}

public sealed record InnolaPlanCheckWritebackResult(
    bool Success,
    string Message,
    string? ErrorCategory,
    IReadOnlyList<InnolaPlanCheckUpdate> Updates)
{
    public static InnolaPlanCheckWritebackResult Succeeded(
        string? message = null,
        IReadOnlyList<InnolaPlanCheckUpdate>? updates = null)
    {
        return new InnolaPlanCheckWritebackResult(true, message ?? "Innola Plan Check values saved.", null, updates ?? Array.Empty<InnolaPlanCheckUpdate>());
    }

    public static InnolaPlanCheckWritebackResult Failed(string message, string? errorCategory = null)
    {
        return new InnolaPlanCheckWritebackResult(false, message, errorCategory, Array.Empty<InnolaPlanCheckUpdate>());
    }
}

public sealed record InnolaPlanCheckUpdate(
    string CheckType,
    bool? PreviousPassed,
    bool? NewPassed,
    string? PreviousDescription,
    string? NewDescription);
