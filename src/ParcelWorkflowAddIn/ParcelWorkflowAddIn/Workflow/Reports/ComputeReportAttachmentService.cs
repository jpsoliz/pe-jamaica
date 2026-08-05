using System.IO;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Reports;

public interface IComputeReportAttachmentService
{
    Task<ComputeReportAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string pdfReportPath,
        CancellationToken cancellationToken = default);
}

public sealed class ComputeReportAttachmentService : IComputeReportAttachmentService
{
    public const string SourceType = "st_compute_report";
    public const string ContentType = "application/pdf";

    private readonly Func<InnolaSession?> getSession;
    private readonly IInnolaTransactionDetailService detailService;

    public ComputeReportAttachmentService(
        Func<InnolaSession?> getSession,
        IInnolaTransactionDetailService detailService)
    {
        this.getSession = getSession;
        this.detailService = detailService;
    }

    public async Task<ComputeReportAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string pdfReportPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfReportPath) || !File.Exists(pdfReportPath))
        {
            return ComputeReportAttachmentResult.Failed("Compute PDF report must be generated before Finalize.", "report_missing");
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return ComputeReportAttachmentResult.Failed("Compute report could not be attached because the Innola session is not available.", "session_unavailable");
        }

        try
        {
            var content = await File.ReadAllBytesAsync(pdfReportPath, cancellationToken).ConfigureAwait(false);
            var upload = await detailService.UploadAttachmentAsync(
                session,
                transaction,
                Path.GetFileName(pdfReportPath),
                ContentType,
                content,
                SourceType,
                cancellationToken).ConfigureAwait(false);

            return upload.Success
                ? ComputeReportAttachmentResult.Succeeded(SourceType, pdfReportPath)
                : ComputeReportAttachmentResult.Failed(
                    upload.ErrorMessage ?? "Compute report could not be attached to the transaction.",
                    upload.ErrorCategory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ComputeReportAttachmentResult.Failed($"Compute report could not be attached: {exception.Message}", exception.GetType().Name);
        }
    }
}

public sealed record ComputeReportAttachmentResult(
    bool Success,
    string Message,
    string? SourceType,
    string? PdfReportPath,
    string? ErrorCategory = null)
{
    public static ComputeReportAttachmentResult Succeeded(string sourceType, string pdfReportPath)
    {
        return new ComputeReportAttachmentResult(true, "Compute PDF report attached to the transaction.", sourceType, pdfReportPath);
    }

    public static ComputeReportAttachmentResult Failed(string message, string? errorCategory = null)
    {
        return new ComputeReportAttachmentResult(false, message, null, null, errorCategory);
    }
}
