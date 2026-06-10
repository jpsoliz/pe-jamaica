namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaLoginResult(bool Success, InnolaSession? Session, string? ErrorMessage)
{
    public static InnolaLoginResult Succeeded(InnolaSession session)
    {
        return new InnolaLoginResult(true, session, null);
    }

    public static InnolaLoginResult Failure(string errorMessage)
    {
        return new InnolaLoginResult(false, null, errorMessage);
    }
}
