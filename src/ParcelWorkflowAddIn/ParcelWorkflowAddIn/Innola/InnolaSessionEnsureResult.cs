namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaSessionEnsureResult(
    bool Success,
    InnolaSession? Session,
    string Message,
    string? ErrorCategory)
{
    public static InnolaSessionEnsureResult Succeeded(InnolaSession session, string message)
    {
        return new InnolaSessionEnsureResult(true, session, message, null);
    }

    public static InnolaSessionEnsureResult Failed(string message, string? errorCategory)
    {
        return new InnolaSessionEnsureResult(false, null, message, errorCategory);
    }

    public static InnolaSessionEnsureResult Cancelled()
    {
        return new InnolaSessionEnsureResult(false, null, "Innola session refresh was cancelled.", "cancelled");
    }
}
