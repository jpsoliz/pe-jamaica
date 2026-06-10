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

    public InnolaTransactionLifecycleStatus LifecycleStatus { get; private set; } = InnolaTransactionLifecycleStatus.None;

    public string? LifecycleOwnerUser { get; private set; }

    public string? LifecycleOwnerDisplayName { get; private set; }

    public string? ClaimedAt { get; private set; }

    public string? LastSavedAt { get; private set; }

    public string? CompletedAt { get; private set; }

    public string? CancelledAt { get; private set; }

    public string? LifecycleStatusText { get; private set; }

    public bool CanOpenLogin => Status != InnolaSessionStatus.Authenticating;

    public bool CanOpenAbout => true;

    public bool CanOpenConfiguration => true;

    public bool CanOpenTransactionPanel => IsLoggedIn;

    public bool CanOpenParcelWorkflow => IsLoggedIn
        && IsTransactionLoaded
        && SelectedTransaction is not null
        && LifecycleStatus is InnolaTransactionLifecycleStatus.InProgress
            or InnolaTransactionLifecycleStatus.CompleteBlocked
            or InnolaTransactionLifecycleStatus.Error;

    public bool CanStartOrClaimTransaction => IsLoggedIn
        && IsTransactionLoaded
        && SelectedTransaction is not null
        && LifecycleStatus is InnolaTransactionLifecycleStatus.Loaded or InnolaTransactionLifecycleStatus.Error or InnolaTransactionLifecycleStatus.CompleteBlocked;

    public bool CanSaveProgress => IsLoggedIn
        && IsTransactionLoaded
        && SelectedTransaction is not null
        && IsCurrentUserLifecycleOwner
        && LifecycleStatus is InnolaTransactionLifecycleStatus.InProgress
            or InnolaTransactionLifecycleStatus.SaveProgress
            or InnolaTransactionLifecycleStatus.CompleteBlocked
            or InnolaTransactionLifecycleStatus.Error;

    public bool CanCancelActiveProcess => IsLoggedIn
        && IsTransactionLoaded
        && SelectedTransaction is not null
        && LifecycleStatus is not InnolaTransactionLifecycleStatus.Cancelled
        && LifecycleStatus is not InnolaTransactionLifecycleStatus.Completed;

    public bool CanCompleteTransaction => IsLoggedIn
        && IsTransactionLoaded
        && SelectedTransaction is not null
        && IsCurrentUserLifecycleOwner
        && LifecycleStatus is InnolaTransactionLifecycleStatus.InProgress
            or InnolaTransactionLifecycleStatus.SaveProgress
            or InnolaTransactionLifecycleStatus.CompleteBlocked
            or InnolaTransactionLifecycleStatus.Error;

    public bool CanSwitchTransaction => !IsTransactionLoaded
        || LifecycleStatus is InnolaTransactionLifecycleStatus.Completed
        || LifecycleStatus is InnolaTransactionLifecycleStatus.Cancelled;

    public bool HasActiveTransaction => IsLoggedIn
        && IsTransactionLoaded
        && SelectedTransaction is not null
        && LifecycleStatus is InnolaTransactionLifecycleStatus.InProgress
            or InnolaTransactionLifecycleStatus.SaveProgress
            or InnolaTransactionLifecycleStatus.CompleteBlocked
            or InnolaTransactionLifecycleStatus.Error;

    private bool IsCurrentUserLifecycleOwner => !string.IsNullOrWhiteSpace(LifecycleOwnerUser)
        && CurrentUser is not null
        && LifecycleOwnerUser.Equals(CurrentUser.Username, StringComparison.OrdinalIgnoreCase);


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

    public InnolaTransactionStateSnapshot CaptureTransactionState()
    {
        return new InnolaTransactionStateSnapshot(
            SelectedTransaction,
            IsTransactionLoaded,
            LoadedTransactionNumber,
            LoadedCaseFolderPath,
            LoadedAt,
            LifecycleStatus,
            LifecycleOwnerUser,
            LifecycleOwnerDisplayName,
            ClaimedAt,
            LastSavedAt,
            CompletedAt,
            CancelledAt,
            LifecycleStatusText);
    }

    public void RestoreTransactionState(InnolaTransactionStateSnapshot snapshot)
    {
        SelectedTransaction = snapshot.SelectedTransaction;
        IsTransactionLoaded = snapshot.IsTransactionLoaded;
        LoadedTransactionNumber = snapshot.LoadedTransactionNumber;
        LoadedCaseFolderPath = snapshot.LoadedCaseFolderPath;
        LoadedAt = snapshot.LoadedAt;
        LifecycleStatus = snapshot.LifecycleStatus;
        LifecycleOwnerUser = snapshot.LifecycleOwnerUser;
        LifecycleOwnerDisplayName = snapshot.LifecycleOwnerDisplayName;
        ClaimedAt = snapshot.ClaimedAt;
        LastSavedAt = snapshot.LastSavedAt;
        CompletedAt = snapshot.CompletedAt;
        CancelledAt = snapshot.CancelledAt;
        LifecycleStatusText = snapshot.LifecycleStatusText;
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
        LifecycleStatus = InnolaTransactionLifecycleStatus.Loaded;
        LifecycleOwnerUser = null;
        LifecycleOwnerDisplayName = null;
        ClaimedAt = null;
        LastSavedAt = null;
        CompletedAt = null;
        CancelledAt = null;
        LifecycleStatusText = $"Loaded transaction {transactionNumber}.";
        OnSessionChanged();
    }

    public void MarkTransactionClaimed(string ownerUser, string? ownerDisplayName, string claimedAt, string statusText)
    {
        if (!IsLoggedIn || !IsTransactionLoaded)
        {
            return;
        }

        LifecycleStatus = InnolaTransactionLifecycleStatus.InProgress;
        LifecycleOwnerUser = ownerUser;
        LifecycleOwnerDisplayName = ownerDisplayName;
        ClaimedAt = claimedAt;
        CancelledAt = null;
        CompletedAt = null;
        LifecycleStatusText = statusText;
        OnSessionChanged();
    }

    public void MarkProgressSaved(string savedAt, string statusText)
    {
        if (!IsLoggedIn || !IsTransactionLoaded)
        {
            return;
        }

        LifecycleStatus = InnolaTransactionLifecycleStatus.SaveProgress;
        LastSavedAt = savedAt;
        LifecycleStatusText = statusText;
        OnSessionChanged();
    }

    public void MarkCompletionBlocked(string reason, string statusText)
    {
        if (!IsLoggedIn || !IsTransactionLoaded)
        {
            return;
        }

        LifecycleStatus = InnolaTransactionLifecycleStatus.CompleteBlocked;
        LifecycleStatusText = statusText;
        OnSessionChanged();
    }

    public void MarkLifecycleError(string statusText)
    {
        if (!IsLoggedIn || !IsTransactionLoaded)
        {
            return;
        }

        LifecycleStatus = InnolaTransactionLifecycleStatus.Error;
        LifecycleStatusText = statusText;
        OnSessionChanged();
    }

    public void MarkTransactionCompleted(string completedAt, string statusText)
    {
        if (!IsLoggedIn || !IsTransactionLoaded)
        {
            return;
        }

        LifecycleStatus = InnolaTransactionLifecycleStatus.Completed;
        CompletedAt = completedAt;
        LifecycleStatusText = statusText;
        ClearLoadedTransactionCore();
        SelectedTransaction = null;
        OnSessionChanged();
    }

    public void MarkTransactionCancelled(string cancelledAt, string statusText)
    {
        if (!IsLoggedIn || !IsTransactionLoaded)
        {
            return;
        }

        LifecycleStatus = InnolaTransactionLifecycleStatus.Cancelled;
        CancelledAt = cancelledAt;
        LifecycleStatusText = statusText;
        ClearLoadedTransactionCore();
        SelectedTransaction = null;
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
        LifecycleStatus = InnolaTransactionLifecycleStatus.None;
        LifecycleOwnerUser = null;
        LifecycleOwnerDisplayName = null;
        ClaimedAt = null;
        LastSavedAt = null;
        CompletedAt = null;
        CancelledAt = null;
        LifecycleStatusText = null;
    }
}

public sealed record InnolaTransactionStateSnapshot(
    SelectedInnolaTransaction? SelectedTransaction,
    bool IsTransactionLoaded,
    string? LoadedTransactionNumber,
    string? LoadedCaseFolderPath,
    string? LoadedAt,
    InnolaTransactionLifecycleStatus LifecycleStatus,
    string? LifecycleOwnerUser,
    string? LifecycleOwnerDisplayName,
    string? ClaimedAt,
    string? LastSavedAt,
    string? CompletedAt,
    string? CancelledAt,
    string? LifecycleStatusText);
