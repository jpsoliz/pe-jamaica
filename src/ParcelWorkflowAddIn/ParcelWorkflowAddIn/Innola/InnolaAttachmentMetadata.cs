namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaAttachmentMetadata(
    string AttachmentId,
    string FileName,
    string Extension,
    string? MimeType,
    string? SourceRole,
    string? Category,
    long? Size,
    string? Checksum,
    string ServiceReference,
    bool IsRequired,
    string? SourceType = null);
