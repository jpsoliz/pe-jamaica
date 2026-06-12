namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaTransactionService : IInnolaTransactionService
{
    private readonly IReadOnlyList<InnolaTransactionRow> rows;

    public MockInnolaTransactionService()
        : this(CreateDefaultRows())
    {
    }

    public MockInnolaTransactionService(IEnumerable<InnolaTransactionRow> rows)
    {
        this.rows = rows.ToArray();
    }

    public Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.AccessToken))
        {
            return Task.FromResult(InnolaTransactionListResult.Failure("Could not refresh transactions. Try again.", "unauthorized"));
        }

        var availableRows = InnolaTransactionFiltering.FilterAvailableRows(rows, query.ProcessStep);
        return Task.FromResult(InnolaTransactionListResult.Succeeded(availableRows));
    }

    private static IReadOnlyList<InnolaTransactionRow> CreateDefaultRows()
    {
        return new[]
        {
            Row("task-100000004", "100000004", "TR100000004", "Computation Check", "Alex Robinson", "PPE", "John Johnson", "parcel_workflow", "2024-10-15T09:24:00-05:00"),
            Row("task-100000005", "100000005", "TR100000005", "Prepare Rejection Letter", "John Johnson", "TR - Integration User", "registration", "parcel_workflow", "2024-10-15T09:38:00-05:00"),
            Row("task-100000009", "100000009", "TR100000009", "QC of Registration Cases", "John Johnson", "TDC - Robert Smith", "qc", "parcel_workflow", "2024-10-15T09:53:00-05:00"),
            Row("task-100000014", "100000014", "TR100000014", "QC of Registration Cases", "Sergiy Lizenko, Derven George Pullen", "TDC - Sergiy Lizenko", "qc", "parcel_workflow", "2024-10-15T10:15:00-05:00"),
            Row("task-100000015", "100000015", "TR100000015", "Enter Data", "John Johnson", "TSBD - Maksym Kalyta", "data-entry", "parcel_workflow", "2024-10-15T10:50:00-05:00"),
            Row("task-100000020", "100000020", "TR100000020", "Review Instrument", "Hermina Small", "TSBD - Maksym Kalyta", "review", "parcel_workflow", "2024-10-15T12:19:00-05:00"),
            new(
                "task-completed",
                "100000099",
                "TR100000099",
                "Completed Sample",
                "parcel_workflow",
                InnolaTransactionStatus.Completed,
                "Plan Examination",
                "Completed User",
                "Completed User",
                "completed",
                DateTimeOffset.Parse("2024-10-15T13:00:00-05:00"),
                false,
                false,
                "Task is already completed.",
                null),
            new(
                "task-wrong-step",
                "100000100",
                "TR100000100",
                "Wrong Step Sample",
                "post_registration",
                InnolaTransactionStatus.WrongStep,
                "Plan Examination",
                "Wrong Step User",
                "Wrong Step User",
                "other",
                DateTimeOffset.Parse("2024-10-15T13:30:00-05:00"),
                false,
                false,
                "Task belongs to another process step.",
                null)
        };
    }

    private static InnolaTransactionRow Row(
        string taskId,
        string transactionId,
        string transactionNumber,
        string taskName,
        string responsibleParty,
        string assignedUser,
        string assignedGroup,
        string processStep,
        string receivedAt)
    {
        return new InnolaTransactionRow(
            taskId,
            transactionId,
            transactionNumber,
            taskName,
            processStep,
            InnolaTransactionStatus.Available,
            "Plan Examination",
            responsibleParty,
            assignedUser,
            assignedGroup,
            DateTimeOffset.Parse(receivedAt),
            true,
            true,
            null,
            null);
    }
}
