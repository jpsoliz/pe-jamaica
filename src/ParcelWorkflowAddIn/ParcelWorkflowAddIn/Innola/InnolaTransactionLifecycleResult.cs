namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionLifecycleResult(
    bool Success,
    string Status,
    string? OwnerUser,
    string? OwnerDisplayName,
    string? Message,
    string? ErrorCategory)
{
    public static InnolaTransactionLifecycleResult Succeeded(
        string status,
        string? ownerUser,
        string? ownerDisplayName,
        string? message = null)
    {
        return new InnolaTransactionLifecycleResult(true, status, ownerUser, ownerDisplayName, message, null);
    }

    public static InnolaTransactionLifecycleResult Failure(string message, string errorCategory)
    {
        return new InnolaTransactionLifecycleResult(false, "error", null, null, message, errorCategory);
    }
}
