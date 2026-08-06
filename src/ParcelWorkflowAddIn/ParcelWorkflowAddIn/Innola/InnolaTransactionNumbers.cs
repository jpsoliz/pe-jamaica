namespace ParcelWorkflowAddIn.Innola;

public static class InnolaTransactionNumbers
{
    public static string NormalizeWorkflowKey(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 2
            && trimmed.StartsWith("TR", StringComparison.OrdinalIgnoreCase)
            && trimmed[2..].All(char.IsDigit))
        {
            return trimmed[2..];
        }

        return trimmed;
    }
}
