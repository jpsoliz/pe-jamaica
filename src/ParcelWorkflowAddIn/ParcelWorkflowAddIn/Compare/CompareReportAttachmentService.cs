using System.IO;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Compare;

public interface ICompareReportAttachmentService
{
    Task<CompareReportAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string pdfReportPath,
        CancellationToken cancellationToken = default);
}

public sealed class CompareReportAttachmentService : ICompareReportAttachmentService
{
    public const string SourceType = "st_compare_report";
    public const string ContentType = "application/pdf";

    private readonly Func<InnolaSession?> getSession;
    private readonly IInnolaTransactionDetailService detailService;

    public CompareReportAttachmentService(
        Func<InnolaSession?> getSession,
        IInnolaTransactionDetailService detailService)
    {
        this.getSession = getSession;
        this.detailService = detailService;
    }

    public async Task<CompareReportAttachmentResult> UploadAsync(
        SelectedInnolaTransaction transaction,
        string pdfReportPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfReportPath) || !File.Exists(pdfReportPath))
        {
            return CompareReportAttachmentResult.Failed("Compare PDF report must be generated before Finalize.");
        }

        var session = getSession();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return CompareReportAttachmentResult.Failed("Compare report could not be attached because the Innola session is not available.");
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
                ? CompareReportAttachmentResult.Succeeded(SourceType, pdfReportPath)
                : CompareReportAttachmentResult.Failed(upload.ErrorMessage ?? "Compare report could not be attached to the transaction.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CompareReportAttachmentResult.Failed($"Compare report could not be attached: {exception.Message}");
        }
    }
}

public sealed record CompareReportAttachmentResult(
    bool Success,
    string Message,
    string? SourceType,
    string? PdfReportPath)
{
    public static CompareReportAttachmentResult Succeeded(string sourceType, string pdfReportPath)
    {
        return new CompareReportAttachmentResult(true, "Compare PDF report attached to the transaction.", sourceType, pdfReportPath);
    }

    public static CompareReportAttachmentResult Failed(string message)
    {
        return new CompareReportAttachmentResult(false, message, null, null);
    }
}
