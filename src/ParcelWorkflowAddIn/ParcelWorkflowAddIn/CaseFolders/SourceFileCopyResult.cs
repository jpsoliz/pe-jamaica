namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record SourceFileCopyResult(
    string OriginalPath,
    string? CopiedPath,
    string FileName,
    string FileType,
    long? FileSize,
    string? SourceRole,
    string Status,
    string Message,
    bool Copied);
