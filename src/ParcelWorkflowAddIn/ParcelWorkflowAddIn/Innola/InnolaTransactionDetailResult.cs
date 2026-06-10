namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionDetailResult(
    bool Success,
    InnolaTransactionDetail? Detail,
    string? ErrorMessage,
    string? ErrorCode)
{
    public static InnolaTransactionDetailResult Succeeded(InnolaTransactionDetail detail)
    {
        return new InnolaTransactionDetailResult(true, detail, null, null);
    }

    public static InnolaTransactionDetailResult Failure(string errorMessage, string? errorCode = null)
    {
        return new InnolaTransactionDetailResult(false, null, errorMessage, errorCode);
    }
}
