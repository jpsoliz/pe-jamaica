namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionListResult(
    bool Success,
    IReadOnlyList<InnolaTransactionRow> Rows,
    string? ErrorMessage,
    string? ErrorCategory)
{
    public static InnolaTransactionListResult Succeeded(IEnumerable<InnolaTransactionRow> rows)
    {
        return new InnolaTransactionListResult(true, rows.ToArray(), null, null);
    }

    public static InnolaTransactionListResult Failure(string? message, string? category = null)
    {
        return new InnolaTransactionListResult(false, Array.Empty<InnolaTransactionRow>(), Sanitize(message), category);
    }

    private static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Could not refresh transactions. Try again.";
        }

        if (message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || message.Contains('{', StringComparison.Ordinal)
            || message.Contains(" at ", StringComparison.Ordinal))
        {
            return "Could not refresh transactions. Try again.";
        }

        return message;
    }
}
