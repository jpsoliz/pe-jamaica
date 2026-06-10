namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaSessionManager
{
    private readonly IInnolaAuthService authService;

    public InnolaSessionManager(IInnolaAuthService authService)
    {
        this.authService = authService;
    }

    public event EventHandler? SessionChanged;

    public InnolaSessionStatus Status { get; private set; } = InnolaSessionStatus.LoggedOut;

    public InnolaSession? CurrentSession { get; private set; }

    public InnolaUserContext? CurrentUser => CurrentSession?.User;

    public string StatusText { get; private set; } = "Not logged in.";

    public bool IsLoggedIn => Status == InnolaSessionStatus.LoggedIn && CurrentSession is not null && !string.IsNullOrWhiteSpace(CurrentSession.AccessToken);

    public bool IsTransactionLoaded { get; private set; }

    public SelectedInnolaTransaction? SelectedTransaction { get; private set; }

    public string? LoadedTransactionNumber { get; private set; }

    public string? LoadedCaseFolderPath { get; private set; }

    public string? LoadedAt { get; private set; }

    public bool CanOpenLogin => Status != InnolaSessionStatus.Authenticating;

    public bool CanOpenAbout => true;

    public bool CanOpenConfiguration => true;

    public bool CanOpenTransactionPanel => IsLoggedIn;

    public bool CanOpenParcelWorkflow => IsLoggedIn && IsTransactionLoaded;

    public async Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        Status = InnolaSessionStatus.Authenticating;
        CurrentSession = null;
        StatusText = "Logging in.";
        OnSessionChanged();

        var result = await authService.LoginAsync(serverUrl, username, password, cancellationToken);
        if (!result.Success || result.Session is null)
        {
            Status = InnolaSessionStatus.LoggedOut;
            CurrentSession = null;
            IsTransactionLoaded = false;
            SelectedTransaction = null;
            StatusText = SanitizeStatus(result.ErrorMessage);
            OnSessionChanged();
            return InnolaLoginResult.Failure(StatusText);
        }

        ApplySuccessfulSession(result.Session);
        return result;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await authService.LogoutAsync(cancellationToken);
        Clear("Not logged in.", InnolaSessionStatus.LoggedOut);
    }

    public void ExpireSession()
    {
        Clear("Session expired. Log in again.", InnolaSessionStatus.SessionExpired);
    }

    public void SetTransactionLoadedForTesting(bool isLoaded)
    {
        IsTransactionLoaded = isLoaded;
        OnSessionChanged();
    }

    public void SelectTransaction(InnolaTransactionRow row, DateTimeOffset selectedAt)
    {
        if (!IsLoggedIn || !row.IsLoadable)
        {
            return;
        }

        SelectedTransaction = new SelectedInnolaTransaction(
            row.TaskId,
            row.TransactionId,
            row.TransactionNumber,
            row.TaskName,
            row.ProcessStep,
            selectedAt);
        ClearLoadedTransactionCore();
        OnSessionChanged();
    }

    public void ClearSelectedTransaction()
    {
        SelectedTransaction = null;
        ClearLoadedTransactionCore();
        OnSessionChanged();
    }

    public void ClearLoadedTransaction()
    {
        ClearLoadedTransactionCore();
        OnSessionChanged();
    }

    public void MarkTransactionLoaded(string transactionNumber, string caseFolderPath, string loadedAt)
    {
        if (!IsLoggedIn || SelectedTransaction is null)
        {
            ClearLoadedTransactionCore();
            OnSessionChanged();
            return;
        }

        IsTransactionLoaded = true;
        LoadedTransactionNumber = transactionNumber;
        LoadedCaseFolderPath = caseFolderPath;
        LoadedAt = loadedAt;
        OnSessionChanged();
    }

    public void ApplySuccessfulSession(InnolaSession session)
    {
        CurrentSession = session;
        Status = InnolaSessionStatus.LoggedIn;
        ClearLoadedTransactionCore();
        SelectedTransaction = null;
        var displayName = string.IsNullOrWhiteSpace(session.User.DisplayName)
            ? session.User.Username
            : session.User.DisplayName;
        StatusText = $"Logged in as {displayName}.";
        OnSessionChanged();
    }

    private void Clear(string statusText, InnolaSessionStatus status)
    {
        CurrentSession = null;
        ClearLoadedTransactionCore();
        SelectedTransaction = null;
        Status = status;
        StatusText = statusText;
        OnSessionChanged();
    }

    private static string SanitizeStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Login failed. Check user name, password, and server.";
        }

        if (message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access", StringComparison.OrdinalIgnoreCase))
        {
            return "Login failed. Check user name, password, and server.";
        }

        return message;
    }

    private void OnSessionChanged()
    {
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearLoadedTransactionCore()
    {
        IsTransactionLoaded = false;
        LoadedTransactionNumber = null;
        LoadedCaseFolderPath = null;
        LoadedAt = null;
    }
}
