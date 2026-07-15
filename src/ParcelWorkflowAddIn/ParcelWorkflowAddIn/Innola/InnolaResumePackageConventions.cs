namespace ParcelWorkflowAddIn.Innola;

internal static class InnolaResumePackageConventions
{
    public const string ResumeSourceType = "sidwell_resume_package";
    public const string CompletedSourceType = "sidwell_completed_package";
    private const string ResumeAttachmentPrefix = "sidwell-case-state-";
    private const string CompletedAttachmentPrefix = "sidwell-case-complete-";

    public static string BuildResumeAttachmentFileName(string transactionNumber)
    {
        return $"{ResumeAttachmentPrefix}{transactionNumber}.zip";
    }

    public static string BuildCompletedAttachmentFileName(string transactionNumber)
    {
        return $"{CompletedAttachmentPrefix}{transactionNumber}.zip";
    }

    public static bool IsResumePackageAttachment(InnolaAttachmentMetadata attachment, string transactionNumber)
    {
        if (!".zip".Equals(attachment.Extension, StringComparison.OrdinalIgnoreCase)
            && !attachment.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedName = BuildResumeAttachmentFileName(transactionNumber);
        if (attachment.FileName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Category)
            && attachment.Category.Contains(ResumeSourceType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return attachment.FileName.StartsWith(ResumeAttachmentPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCompletedPackageAttachment(InnolaAttachmentMetadata attachment, string transactionNumber)
    {
        if (!".zip".Equals(attachment.Extension, StringComparison.OrdinalIgnoreCase)
            && !attachment.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedName = BuildCompletedAttachmentFileName(transactionNumber);
        if (attachment.FileName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Category)
            && attachment.Category.Contains(CompletedSourceType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return attachment.FileName.StartsWith(CompletedAttachmentPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSystemPackageAttachment(InnolaAttachmentMetadata attachment, string transactionNumber)
    {
        return IsResumePackageAttachment(attachment, transactionNumber)
            || IsCompletedPackageAttachment(attachment, transactionNumber);
    }
}

public sealed record InnolaAttachmentUploadResult(
    bool Success,
    string? ErrorMessage,
    string? ErrorCategory)
{
    public static InnolaAttachmentUploadResult Succeeded()
    {
        return new InnolaAttachmentUploadResult(true, null, null);
    }

    public static InnolaAttachmentUploadResult Failure(string errorMessage, string? errorCategory = null)
    {
        return new InnolaAttachmentUploadResult(false, errorMessage, errorCategory);
    }
}
