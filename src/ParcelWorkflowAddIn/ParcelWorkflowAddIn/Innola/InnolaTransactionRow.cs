namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionRow(
    string TaskId,
    string TransactionId,
    string TransactionNumber,
    string TaskName,
    string ProcessStep,
    InnolaTransactionStatus Status,
    string? TransactionType,
    string? ResponsibleParty,
    string? AssignedUser,
    string? AssignedGroup,
    DateTimeOffset? ReceivedAt,
    bool IsAvailable,
    bool IsLoadable,
    string? UnavailableReason,
    string? BrowserUrl,
    string? ApplicationId = null)
{
    public string DisplayParty => FirstNonEmpty(ResponsibleParty, AssignedUser, AssignedGroup, "Unassigned");

    public string DisplayAssignment => FirstNonEmpty(AssignedUser, AssignedGroup, "Available");

    public string DisplayTimestamp => ReceivedAt?.ToLocalTime().ToString("dd/MMM/yyyy HH:mm") ?? string.Empty;

    public string DisplayTransactionType => string.IsNullOrWhiteSpace(TransactionType) ? "Transaction" : TransactionType;

    public string DisplayStatus => Status switch
    {
        InnolaTransactionStatus.Available => "Available",
        InnolaTransactionStatus.InProgress => "In Progress",
        InnolaTransactionStatus.Completed => "Completed",
        InnolaTransactionStatus.Unavailable => "Unavailable",
        InnolaTransactionStatus.WrongStep => "Wrong Step",
        InnolaTransactionStatus.Locked => "Locked",
        _ => "Unknown"
    };

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
