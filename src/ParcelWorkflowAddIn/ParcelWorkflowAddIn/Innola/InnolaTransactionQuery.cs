namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaTransactionQuery(
    string ServerUrl,
    string AccessToken,
    string Username,
    IReadOnlyList<string> Groups,
    string ProcessStep,
    string? Filter,
    string? Search,
    string? SortField,
    string? SortDirection);
