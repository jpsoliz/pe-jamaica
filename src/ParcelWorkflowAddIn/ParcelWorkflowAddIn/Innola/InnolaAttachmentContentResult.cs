namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaAttachmentContentResult(
    bool Success,
    byte[] Content,
    string? ErrorMessage,
    string? ErrorCode)
{
    public static InnolaAttachmentContentResult Succeeded(byte[] content)
    {
        return new InnolaAttachmentContentResult(true, content, null, null);
    }

    public static InnolaAttachmentContentResult Failure(string errorMessage, string? errorCode = null)
    {
        return new InnolaAttachmentContentResult(false, Array.Empty<byte>(), errorMessage, errorCode);
    }
}
