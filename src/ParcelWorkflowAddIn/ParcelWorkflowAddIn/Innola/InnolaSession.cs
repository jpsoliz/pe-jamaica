namespace ParcelWorkflowAddIn.Innola;

public sealed record InnolaSession(
    InnolaSessionStatus Status,
    string ServerUrl,
    string Username,
    string? SessionPassword,
    string AccessToken,
    InnolaUserContext User,
    DateTimeOffset? ExpiresAt);
