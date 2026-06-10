namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaUserContext(
    string Username,
    string DisplayName,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Roles);
