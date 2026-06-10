namespace ParcelWorkflowAddIn.Innola;

public sealed class MockInnolaAuthService : IInnolaAuthService
{
    public InnolaSession? CurrentSession { get; private set; }

    public Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(username))
        {
            return Task.FromResult(InnolaLoginResult.Failure("Login failed. Check user name, password, and server."));
        }

        var normalizedServer = NormalizeServerUrl(serverUrl);
        var normalizedUser = username.Trim();
        CurrentSession = new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            normalizedServer,
            normalizedUser,
            password,
            $"mock-token-{Guid.NewGuid():N}",
            new InnolaUserContext(normalizedUser, normalizedUser, new[] { "survey", "qc" }, new[] { "mock" }),
            null);

        return Task.FromResult(InnolaLoginResult.Succeeded(CurrentSession));
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        CurrentSession = null;
        return Task.CompletedTask;
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        var trimmed = serverUrl.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/";
    }
}
