namespace ParcelWorkflowAddIn.Innola;

public sealed record TransactionCompletionReadinessResult(
    bool IsReady,
    string Reason,
    string Message)
{
    public static TransactionCompletionReadinessResult Ready(string message = "Completion readiness criteria met.")
    {
        return new TransactionCompletionReadinessResult(true, "ready", message);
    }

    public static TransactionCompletionReadinessResult Blocked(string reason, string message)
    {
        return new TransactionCompletionReadinessResult(false, reason, message);
    }
}
