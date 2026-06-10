namespace ParcelWorkflowAddIn.CaseFolders;

public sealed record SourceFileActionResult(
    bool Success,
    SourceFileAction Action,
    string? Path,
    string Status,
    string Message)
{
    public static SourceFileActionResult Succeeded(SourceFileAction action, string path, string status, string message)
    {
        return new SourceFileActionResult(true, action, path, status, message);
    }

    public static SourceFileActionResult Failed(SourceFileAction action, string? path, string status, string message)
    {
        return new SourceFileActionResult(false, action, path, status, message);
    }
}
