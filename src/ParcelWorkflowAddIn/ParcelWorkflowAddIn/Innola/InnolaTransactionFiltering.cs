namespace ParcelWorkflowAddIn.Innola;

public static class InnolaTransactionFiltering
{
    public static IReadOnlyList<InnolaTransactionRow> FilterAvailableRows(IEnumerable<InnolaTransactionRow> rows, string processStep)
    {
        return rows
            .Where(row => row.IsAvailable)
            .Where(row => row.IsLoadable)
            .Where(row => row.Status is not InnolaTransactionStatus.Completed
                and not InnolaTransactionStatus.Unavailable
                and not InnolaTransactionStatus.WrongStep
                and not InnolaTransactionStatus.Locked)
            .Where(row => string.IsNullOrWhiteSpace(row.ProcessStep)
                || string.Equals(row.ProcessStep, processStep, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
