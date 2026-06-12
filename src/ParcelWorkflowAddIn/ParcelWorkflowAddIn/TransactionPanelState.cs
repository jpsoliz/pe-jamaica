using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using Microsoft.Win32;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

public sealed class TransactionPanelState : INotifyPropertyChanged
{
    public static TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(15);

    private readonly InnolaSessionManager session;
    private readonly IInnolaTransactionService transactionService;
    private readonly InnolaTransactionLoadService? transactionLoadService;
    private readonly InnolaTransactionLifecycleCoordinator? lifecycleCoordinator;
    private readonly IActiveTransactionSwitchDecisionProvider switchDecisionProvider;
    private readonly Func<DateTimeOffset> clock;
    private readonly bool autoRefreshOnLogin;
    private readonly List<InnolaTransactionRow> allRows = new();
    private readonly HashSet<string> locallyCompletedTransactionNumbers = new(StringComparer.OrdinalIgnoreCase);
    private string selectedFilter = "All tasks";
    private string searchText = string.Empty;
    private string sortField = "Received";
    private string sortDirection = "Descending";
    private InnolaTransactionRow? selectedRow;
    private bool isLoading;
    private bool refreshAfterLoginQueued;
    private string? savedTransactionNumber;
    private string statusText = "Not logged in.";
    private string? errorText;
    private int? lastRetrievedRecordCount;

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        Func<DateTimeOffset>? clock)
        : this(session, transactionService, processStep, null, null, null, clock)
    {
    }

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        InnolaTransactionLoadService? transactionLoadService = null,
        Func<DateTimeOffset>? clock = null)
        : this(session, transactionService, processStep, transactionLoadService, null, null, clock)
    {
    }

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        InnolaTransactionLoadService? transactionLoadService,
        InnolaTransactionLifecycleCoordinator? lifecycleCoordinator = null,
        IActiveTransactionSwitchDecisionProvider? switchDecisionProvider = null,
        Func<DateTimeOffset>? clock = null,
        bool autoRefreshOnLogin = false)
    {
        this.session = session;
        this.transactionService = transactionService;
        this.transactionLoadService = transactionLoadService;
        this.lifecycleCoordinator = lifecycleCoordinator;
        this.switchDecisionProvider = switchDecisionProvider ?? new StayOnCurrentTransactionDecisionProvider();
        ProcessStep = string.IsNullOrWhiteSpace(processStep) ? "parcel_workflow" : processStep;
        this.clock = clock ?? (() => DateTimeOffset.Now);
        this.autoRefreshOnLogin = autoRefreshOnLogin;

        Rows = new ObservableCollection<InnolaTransactionRow>();
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => CanRefresh);
        LoadSelectedCommand = new RelayCommand(async () => await LoadSelectedTransactionAsync(), () => CanLoadSelectedTransaction);
        StartTransactionCommand = new RelayCommand(async () => await StartSelectedTransactionAsync(), () => CanStartTransaction);
        StopTaskCommand = new RelayCommand(async () => await SaveCurrentTransactionAsync(), () => CanStopTask);
        ViewDocumentsCommand = new RelayCommand(ViewLoadedDocuments, () => CanViewDocuments);
        AddDocumentCommand = new RelayCommand(ChooseAndAddDocuments, () => CanAddDocument);
        CompleteTaskCommand = new RelayCommand(async () => await CompleteCurrentTransactionAsync(), () => CanCompleteTask);
        session.SessionChanged += (_, _) => HandleSessionChanged();
        RefreshSessionState();
        QueueRefreshAfterLogin();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<InnolaTransactionRow> Rows { get; }

    public IReadOnlyList<string> Filters { get; } = new[] { "All tasks", "My tasks", "Group tasks" };

    public IReadOnlyList<string> SortFields { get; } = new[] { "Received", "Transaction no", "Task name", "Status" };

    public IReadOnlyList<string> SortDirections { get; } = new[] { "Ascending", "Descending" };

    public ICommand RefreshCommand { get; }

    public ICommand LoadSelectedCommand { get; }

    public ICommand StartTransactionCommand { get; }

    public ICommand StopTaskCommand { get; }

    public ICommand ViewDocumentsCommand { get; }

    public ICommand AddDocumentCommand { get; }

    public ICommand CompleteTaskCommand { get; }

    public string ProcessStep { get; }

    public bool IsLoggedIn => session.IsLoggedIn;

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (isLoading == value)
            {
                return;
            }

            isLoading = value;
            NotifyPropertyChanged(nameof(IsLoading));
            NotifyPropertyChanged(nameof(CanRefresh));
            NotifyPropertyChanged(nameof(CanUseListControls));
            NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
            NotifyPropertyChanged(nameof(IsEmpty));
            NotifyCommandStates();
        }
    }

    public bool IsTransactionActive => session.HasActiveTransaction;

    public bool IsTransactionPanelLocked => IsTransactionActive;

    public string? ActiveTransactionNumber => IsTransactionActive
        ? session.SelectedTransaction?.TransactionNumber
        : null;

    public string? SavedTransactionNumber
    {
        get => savedTransactionNumber;
        private set
        {
            if (savedTransactionNumber == value)
            {
                return;
            }

            savedTransactionNumber = value;
            NotifyPropertyChanged(nameof(SavedTransactionNumber));
        }
    }

    public bool CanRefresh => IsLoggedIn && !IsLoading && !IsTransactionPanelLocked;

    public bool CanUseListControls => IsLoggedIn && !IsLoading && allRows.Count > 0 && !IsTransactionPanelLocked;

    public bool HasRows => Rows.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsEmpty => IsLoggedIn && !IsLoading && !HasError && Rows.Count == 0;

    public string? LoadedCaseFolderPath => session.LoadedCaseFolderPath;

    public string SavedTransactionStatusText => "In Progress by current user";

    public string ConnectionUserText
    {
        get
        {
            if (!IsLoggedIn || session.CurrentSession is null)
            {
                return "User: not logged in";
            }

            var user = session.CurrentUser;
            var displayName = string.IsNullOrWhiteSpace(user?.DisplayName)
                ? session.CurrentSession.Username
                : user.DisplayName;
            return $"User: {displayName}";
        }
    }

    public string ConnectionServerText => IsLoggedIn && session.CurrentSession is not null
        ? $"Server: {session.CurrentSession.ServerUrl}"
        : "Server: not connected";

    public string ConnectionModeText => $"Mode: {ShellState.TransactionMode}";

    public string ClientCertificateText => ShellState.ClientCertificateStatus;

    public string RetrievedRecordCountText => lastRetrievedRecordCount.HasValue
        ? $"Records retrieved: {lastRetrievedRecordCount.Value}"
        : "Records retrieved: not refreshed";

    public string StatusText
    {
        get => statusText;
        private set
        {
            if (statusText == value)
            {
                return;
            }

            statusText = value;
            NotifyPropertyChanged(nameof(StatusText));
        }
    }

    public string? ErrorText
    {
        get => errorText;
        private set
        {
            if (errorText == value)
            {
                return;
            }

            errorText = value;
            NotifyPropertyChanged(nameof(ErrorText));
            NotifyPropertyChanged(nameof(HasError));
            NotifyPropertyChanged(nameof(IsEmpty));
        }
    }

    public string SelectedFilter
    {
        get => selectedFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "All tasks" : value;
            if (IsTransactionPanelLocked)
            {
                StatusText = $"Active transaction {ActiveTransactionNumber} is in progress. Stop/save or complete it before changing filters.";
                return;
            }

            if (selectedFilter == normalized)
            {
                return;
            }

            selectedFilter = normalized;
            ApplyView();
            NotifyPropertyChanged(nameof(SelectedFilter));
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (IsTransactionPanelLocked)
            {
                StatusText = $"Active transaction {ActiveTransactionNumber} is in progress. Stop/save or complete it before searching.";
                return;
            }

            if (searchText == normalized)
            {
                return;
            }

            searchText = normalized;
            ApplyView();
            NotifyPropertyChanged(nameof(SearchText));
        }
    }

    public string SortField
    {
        get => sortField;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Received" : value;
            if (IsTransactionPanelLocked)
            {
                StatusText = $"Active transaction {ActiveTransactionNumber} is in progress. Stop/save or complete it before sorting.";
                return;
            }

            if (sortField == normalized)
            {
                return;
            }

            sortField = normalized;
            ApplyView();
            NotifyPropertyChanged(nameof(SortField));
        }
    }

    public string SortDirection
    {
        get => sortDirection;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Descending" : value;
            if (IsTransactionPanelLocked)
            {
                StatusText = $"Active transaction {ActiveTransactionNumber} is in progress. Stop/save or complete it before sorting.";
                return;
            }

            if (sortDirection == normalized)
            {
                return;
            }

            sortDirection = normalized;
            ApplyView();
            NotifyPropertyChanged(nameof(SortDirection));
        }
    }

    public InnolaTransactionRow? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (IsTransactionPanelLocked && value is not null && !IsActiveRow(value))
            {
                RestoreSelectedRow(ActiveTransactionNumber);
                StatusText = $"Active transaction {ActiveTransactionNumber} remains selected.";
                return;
            }

            if (IsTransactionPanelLocked && value is null && selectedRow is not null)
            {
                RestoreSelectedRow(ActiveTransactionNumber);
                StatusText = $"Active transaction {ActiveTransactionNumber} remains selected.";
                return;
            }

            if (ReferenceEquals(selectedRow, value))
            {
                return;
            }

            selectedRow = value;
            NotifyPropertyChanged(nameof(SelectedRow));
            NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
            NotifyCommandStates();
            UpdateSelectionStatus();
        }
    }

    public bool CanLoadSelectedTransaction => IsLoggedIn
        && !IsLoading
        && !IsTransactionPanelLocked
        && SelectedRow is { IsLoadable: true };

    public bool CanStartTransaction => IsLoggedIn
        && !IsLoading
        && lifecycleCoordinator is not null
        && SelectedRow is { IsLoadable: true }
        && !session.HasActiveTransaction;

    public bool CanStopTask => IsLoggedIn && !IsLoading && lifecycleCoordinator is not null && session.CanSaveProgress;

    public bool CanViewDocuments => IsLoggedIn
        && !IsLoading
        && session.IsTransactionLoaded
        && !string.IsNullOrWhiteSpace(session.LoadedCaseFolderPath);

    public bool CanAddDocument => CanViewDocuments;

    public bool CanCompleteTask => IsLoggedIn && !IsLoading && lifecycleCoordinator is not null && session.CanCompleteTransaction;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsTransactionPanelLocked)
        {
            StatusText = $"Active transaction {ActiveTransactionNumber} is in progress. Stop/save or complete it before refreshing.";
            NotifyListState();
            return;
        }

        if (!IsLoggedIn || session.CurrentSession is null)
        {
            allRows.Clear();
            Rows.Clear();
            SelectedRow = null;
            ErrorText = null;
            LastRetrievedRecordCount = null;
            StatusText = "Not logged in.";
            NotifyListState();
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = "Refreshing transactions.";
        try
        {
            var currentSession = session.CurrentSession;
            using var timeout = new CancellationTokenSource(RefreshTimeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            var result = await transactionService.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
                currentSession.ServerUrl,
                currentSession.AccessToken,
                currentSession.User.Username,
                currentSession.User.Groups,
                ProcessStep,
                SelectedFilter,
                SearchText,
                SortField,
                SortDirection), linkedCancellation.Token);

            if (!result.Success)
            {
                ErrorText = FormatRefreshFailure(result);
                StatusText = ErrorText;
                Debug.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Innola transaction refresh failed. User={0}; Server={1}; ErrorCategory={2}; ProcessStep={3}",
                        currentSession.User.Username,
                        currentSession.ServerUrl,
                        result.ErrorCategory ?? "unknown",
                        ProcessStep));
                return;
            }

            var previousTransactionNumber = SelectedRow?.TransactionNumber;
            allRows.Clear();
            allRows.AddRange(result.Rows);
            LastRetrievedRecordCount = result.Rows.Count;
            Debug.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Innola transaction refresh succeeded. User={0}; Server={1}; Records={2}; ProcessStep={3}",
                    currentSession.User.Username,
                    currentSession.ServerUrl,
                    LastRetrievedRecordCount,
                    ProcessStep));
            ApplyView(previousTransactionNumber);
            StatusText = Rows.Count == 0
                ? "No available transactions for this step."
                : $"{Rows.Count} available transaction{(Rows.Count == 1 ? string.Empty : "s")}.";
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Transaction refresh timed out. Try again.";
            StatusText = ErrorText;
            Debug.WriteLine("Innola transaction refresh timed out.");
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    private static string FormatRefreshFailure(InnolaTransactionListResult result)
    {
        var message = result.ErrorMessage ?? "Could not refresh transactions. Try again.";
        return string.IsNullOrWhiteSpace(result.ErrorCategory)
            ? message
            : $"{message} ({result.ErrorCategory})";
    }

    public void LoadSelectedTransaction()
    {
        LoadSelectedTransactionAsync().GetAwaiter().GetResult();
    }

    public async Task LoadSelectedTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedRow is null || !CanLoadSelectedTransaction)
        {
            return;
        }

        var requestedRow = SelectedRow;
        var previousTransactionState = session.CaptureTransactionState();
        if (session.HasActiveTransaction
            && session.SelectedTransaction is not null
            && !session.SelectedTransaction.TransactionNumber.Equals(requestedRow.TransactionNumber, StringComparison.OrdinalIgnoreCase))
        {
            var decision = switchDecisionProvider.Decide(session.SelectedTransaction, requestedRow);
            if (decision == ActiveTransactionSwitchDecision.StayOnCurrentTransaction)
            {
                lifecycleCoordinator?.RecordSwitchDecision(
                    "transaction_switch_stayed",
                    "succeeded",
                    $"Stayed on active transaction {previousTransactionState.SelectedTransaction?.TransactionNumber}.");
                RestoreSelectedRow(previousTransactionState.SelectedTransaction?.TransactionNumber);
                StatusText = $"Active transaction {previousTransactionState.SelectedTransaction?.TransactionNumber} remains loaded.";
                return;
            }

            if (lifecycleCoordinator is null)
            {
                RestoreSelectedRow(previousTransactionState.SelectedTransaction?.TransactionNumber);
                StatusText = "Save or cancel the active transaction before loading another.";
                ErrorText = StatusText;
                return;
            }

            if (decision == ActiveTransactionSwitchDecision.CancelCurrentProcess)
            {
                lifecycleCoordinator.RecordSwitchDecision(
                    "transaction_switch_cancelled",
                    "succeeded",
                    $"Cancelled active transaction {previousTransactionState.SelectedTransaction?.TransactionNumber} before loading {requestedRow.TransactionNumber}.");
            }

            InnolaTransactionStateSnapshot? savedTransactionState = null;
            var lifecycleResult = decision == ActiveTransactionSwitchDecision.SaveProgress
                ? await lifecycleCoordinator.SaveProgressAsync(cancellationToken)
                : lifecycleCoordinator.CancelActiveProcess();
            if (!lifecycleResult.Success)
            {
                RestoreSelectedRow(previousTransactionState.SelectedTransaction?.TransactionNumber);
                ErrorText = lifecycleResult.ErrorMessage ?? "Active transaction could not be released. Try again.";
                StatusText = ErrorText;
                return;
            }

            if (decision == ActiveTransactionSwitchDecision.SaveProgress)
            {
                savedTransactionState = session.CaptureTransactionState();
                lifecycleCoordinator.RecordSwitchDecision(
                    "transaction_switch_saved",
                    "succeeded",
                    $"Saved active transaction {savedTransactionState.SelectedTransaction?.TransactionNumber} before loading {requestedRow.TransactionNumber}.");
            }
            session.ClearSelectedTransaction();
            previousTransactionState = savedTransactionState ?? session.CaptureTransactionState();
            SelectedRow = requestedRow;
        }

        session.SelectTransaction(requestedRow, clock());
        if (transactionLoadService is null)
        {
            StatusText = $"Selected transaction: {SelectedRow.TransactionNumber}.";
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = $"Loading transaction: {SelectedRow.TransactionNumber}.";
        try
        {
            var result = await transactionLoadService.LoadSelectedTransactionAsync(cancellationToken);
            if (!result.Success)
            {
                session.RestoreTransactionState(previousTransactionState);
                ErrorText = result.ErrorMessage ?? "Could not load transaction. Try again.";
                StatusText = ErrorText;
                return;
            }

            StatusText = string.IsNullOrWhiteSpace(result.StatusMessage)
                ? $"Loaded {SelectedRow.TransactionNumber}."
                : $"Loaded {SelectedRow.TransactionNumber}: {result.StatusMessage}";
            NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    public async Task StartSelectedTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStartTransaction || lifecycleCoordinator is null || SelectedRow is null)
        {
            return;
        }

        var requestedTransactionNumber = SelectedRow.TransactionNumber;
        await LoadSelectedTransactionAsync(cancellationToken);
        if (!session.IsTransactionLoaded
            || session.SelectedTransaction is null
            || !session.SelectedTransaction.TransactionNumber.Equals(requestedTransactionNumber, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = $"Starting transaction: {requestedTransactionNumber}.";
        try
        {
            var result = await lifecycleCoordinator.StartOrClaimAsync(cancellationToken);
            ApplyLifecycleResult(result, $"Transaction {requestedTransactionNumber} is in progress.");
            if (result.Success)
            {
                SavedTransactionNumber = null;
                RestoreSelectedRow(requestedTransactionNumber);
                OpenParcelWorkflowDockpane(requestedTransactionNumber);
            }
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
            NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        }
    }

    private void OpenParcelWorkflowDockpane(string requestedTransactionNumber)
    {
        const string autoOpenFailureMessage = "Transaction {0} loaded. Open Parcel Workflow manually from Transactions if required.";
        OpenParcelWorkflowDockpane(requestedTransactionNumber, autoOpenFailureMessage, 1);
    }

    private void OpenParcelWorkflowDockpane(string requestedTransactionNumber, string? notFoundMessage = null, int attempt = 1)
    {
        notFoundMessage ??= $"Transaction {requestedTransactionNumber} loaded. Open Parcel Workflow manually if required.";
        const int maxAttempts = 8;

        try
        {
            var activate = () =>
            {
                var pane = FrameworkApplication.DockPaneManager.Find(ParcelWorkflowDockpaneViewModel.DockPaneId);
                if (pane is null)
                {
                    if (attempt >= maxAttempts)
                    {
                        StatusText = string.Format(CultureInfo.CurrentCulture, notFoundMessage, requestedTransactionNumber);
                    }
                    else if (System.Windows.Application.Current is not null)
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(
                            () => OpenParcelWorkflowDockpane(requestedTransactionNumber, notFoundMessage, attempt + 1),
                            System.Windows.Threading.DispatcherPriority.Background);
                    }

                    return;
                }

                pane.Activate();
            };

            if (System.Windows.Application.Current is null)
            {
                activate();
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(activate);
            }
        }
        catch (InvalidOperationException)
        {
            // Best effort: keep transaction flow running even if UI cannot be activated in this context.
            StatusText = string.Format(CultureInfo.CurrentCulture, notFoundMessage, requestedTransactionNumber);
        }
        catch (Exception)
        {
            StatusText = string.Format(CultureInfo.CurrentCulture, notFoundMessage, requestedTransactionNumber);
        }
    }

    public async Task SaveCurrentTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStopTask || lifecycleCoordinator is null)
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = "Saving current transaction progress.";
        try
        {
            var savedTransactionNumber = session.LoadedTransactionNumber;
            var result = await lifecycleCoordinator.SaveProgressAsync(cancellationToken);
            ApplyLifecycleResult(result, "Progress saved. Transaction remains in progress.");
            if (result.Success)
            {
                SavedTransactionNumber = savedTransactionNumber;
                session.ClearLoadedTransaction();
                RestoreSelectedRow(savedTransactionNumber);
                StatusText = result.StatusMessage ?? "Progress saved. Select a transaction to continue.";
                NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
            }
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    public async Task CompleteCurrentTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!CanCompleteTask || lifecycleCoordinator is null)
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = "Completing transaction.";
        try
        {
            var completedTransactionNumber = session.LoadedTransactionNumber;
            var result = await lifecycleCoordinator.CompleteAsync(cancellationToken);
            ApplyLifecycleResult(result, "Transaction completed.");
            if (result.Success)
            {
                SavedTransactionNumber = null;
                if (!string.IsNullOrWhiteSpace(completedTransactionNumber))
                {
                    locallyCompletedTransactionNumbers.Add(completedTransactionNumber);
                }

                SelectedRow = null;
                await RefreshAsync(cancellationToken);
            }
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    private void ViewLoadedDocuments()
    {
        var folder = session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText = "Load a transaction before viewing documents.";
            return;
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(folder);
            var window = new TransactionDocumentsWindow(session.LoadedTransactionNumber ?? "Transaction", layout);
            window.Show();
            StatusText = $"Viewing local source and output files for {session.LoadedTransactionNumber}.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            ErrorText = "Could not open the transaction documents list.";
            StatusText = ErrorText;
        }
    }

    private void ChooseAndAddDocuments()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add document to transaction",
            Multiselect = true,
            Filter = "Supported source files (*.pdf;*.dwg;*.txt;*.csv;*.tif;*.tiff;*.png;*.jpg;*.jpeg)|*.pdf;*.dwg;*.txt;*.csv;*.tif;*.tiff;*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddDocumentsToLoadedTransaction(dialog.FileNames);
        }
    }

    public void AddDocumentsToLoadedTransaction(IReadOnlyList<string> sourcePaths)
    {
        var folder = session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText = "Load a transaction before adding documents.";
            return;
        }

        if (sourcePaths.Count == 0)
        {
            StatusText = "No documents selected.";
            return;
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(folder);
            var result = new SourceFileCopyService(() => clock().ToUniversalTime()).CopySourceFiles(layout, sourcePaths);
            var copied = result.Results.Count(item => item.Copied);
            var failures = result.Results.Where(item => !item.Copied).Select(item => item.Message).Distinct().ToArray();
            if (copied > 0)
            {
                ErrorText = failures.Length == 0 ? null : string.Join(" ", failures);
                StatusText = failures.Length == 0
                    ? $"Added {copied} document{(copied == 1 ? string.Empty : "s")} to {session.LoadedTransactionNumber}."
                    : $"Added {copied} document{(copied == 1 ? string.Empty : "s")}; {failures.Length} failed.";
                return;
            }

            ErrorText = failures.Length == 0 ? "No documents were added." : string.Join(" ", failures);
            StatusText = ErrorText;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorText = "Could not add documents to the transaction.";
            StatusText = ErrorText;
        }
    }

    private void ApplyLifecycleResult(InnolaTransactionLoadResult result, string successStatus)
    {
        if (!result.Success)
        {
            ErrorText = result.ErrorMessage ?? "Transaction action could not be completed. Try again.";
            StatusText = ErrorText;
            return;
        }

        StatusText = result.StatusMessage ?? successStatus;
        ErrorText = null;
    }

    private void HandleSessionChanged()
    {
        RefreshSessionState();
        if (!session.IsLoggedIn)
        {
            refreshAfterLoginQueued = false;
            SavedTransactionNumber = null;
            locallyCompletedTransactionNumbers.Clear();
            allRows.Clear();
            Rows.Clear();
            SelectedRow = null;
            ErrorText = null;
            LastRetrievedRecordCount = null;
            StatusText = "Not logged in.";
            NotifyListState();
            return;
        }

        QueueRefreshAfterLogin();
    }

    private void RefreshSessionState()
    {
        NotifyPropertyChanged(nameof(IsLoggedIn));
        NotifyPropertyChanged(nameof(CanRefresh));
        NotifyPropertyChanged(nameof(CanUseListControls));
        NotifyPropertyChanged(nameof(IsTransactionActive));
        NotifyPropertyChanged(nameof(IsTransactionPanelLocked));
        NotifyPropertyChanged(nameof(ActiveTransactionNumber));
        NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
        NotifyPropertyChanged(nameof(CanStartTransaction));
        NotifyPropertyChanged(nameof(CanStopTask));
        NotifyPropertyChanged(nameof(CanViewDocuments));
        NotifyPropertyChanged(nameof(CanAddDocument));
        NotifyPropertyChanged(nameof(CanCompleteTask));
        NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        NotifyPropertyChanged(nameof(ConnectionUserText));
        NotifyPropertyChanged(nameof(ConnectionServerText));
        NotifyPropertyChanged(nameof(ConnectionModeText));
        NotifyPropertyChanged(nameof(ClientCertificateText));
        NotifyCommandStates();
    }

    private void ApplyView(string? previousTransactionNumber = null)
    {
        var filtered = ApplyFilter(allRows)
            .Where(row => !locallyCompletedTransactionNumbers.Contains(row.TransactionNumber));
        filtered = ApplySearch(filtered);
        filtered = ApplySort(filtered);

        Rows.Clear();
        foreach (var row in filtered)
        {
            Rows.Add(row);
        }

        if (previousTransactionNumber is not null)
        {
            SelectedRow = Rows.FirstOrDefault(row => row.TransactionNumber.Equals(previousTransactionNumber, StringComparison.OrdinalIgnoreCase));
        }
        else if (SelectedRow is not null && !Rows.Contains(SelectedRow))
        {
            SelectedRow = null;
        }

        NotifyListState();
    }

    private void RestoreSelectedRow(string? transactionNumber)
    {
        if (string.IsNullOrWhiteSpace(transactionNumber))
        {
            SelectedRow = null;
            return;
        }

        SelectedRow = Rows.FirstOrDefault(row => row.TransactionNumber.Equals(transactionNumber, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsActiveRow(InnolaTransactionRow row)
    {
        return ActiveTransactionNumber is not null
            && row.TransactionNumber.Equals(ActiveTransactionNumber, StringComparison.OrdinalIgnoreCase);
    }

    private void QueueRefreshAfterLogin()
    {
        if (!autoRefreshOnLogin || refreshAfterLoginQueued || !IsLoggedIn || Rows.Count > 0 || IsTransactionPanelLocked)
        {
            return;
        }

        refreshAfterLoginQueued = true;
        _ = RefreshAfterLoginAsync();
    }

    private async Task RefreshAfterLoginAsync()
    {
        try
        {
            await RefreshAsync();
        }
        finally
        {
            refreshAfterLoginQueued = false;
        }
    }

    private IEnumerable<InnolaTransactionRow> ApplyFilter(IEnumerable<InnolaTransactionRow> source)
    {
        if (SelectedFilter.Equals("My tasks", StringComparison.OrdinalIgnoreCase))
        {
            return source.Where(row => !string.IsNullOrWhiteSpace(row.AssignedUser)
                && session.CurrentUser is not null
                && row.AssignedUser.Contains(session.CurrentUser.Username, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedFilter.Equals("Group tasks", StringComparison.OrdinalIgnoreCase))
        {
            return source.Where(row => !string.IsNullOrWhiteSpace(row.AssignedGroup));
        }

        return source;
    }

    private IEnumerable<InnolaTransactionRow> ApplySearch(IEnumerable<InnolaTransactionRow> source)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return source;
        }

        return source.Where(row =>
            Contains(row.TransactionNumber, SearchText)
            || Contains(row.TaskName, SearchText)
            || Contains(row.ResponsibleParty, SearchText)
            || Contains(row.AssignedUser, SearchText)
            || Contains(row.AssignedGroup, SearchText));
    }

    private IEnumerable<InnolaTransactionRow> ApplySort(IEnumerable<InnolaTransactionRow> source)
    {
        var descending = SortDirection.Equals("Descending", StringComparison.OrdinalIgnoreCase);
        var sorted = SortField switch
        {
            "Task name" => source.OrderBy(row => row.TaskName, StringComparer.OrdinalIgnoreCase),
            "Received" => source.OrderBy(row => row.ReceivedAt ?? DateTimeOffset.MinValue),
            "Status" => source.OrderBy(row => row.DisplayStatus, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(row => row.TransactionNumber, StringComparer.OrdinalIgnoreCase)
        };

        return descending ? sorted.Reverse() : sorted;
    }

    private void UpdateSelectionStatus()
    {
        if (SelectedRow is not null)
        {
            StatusText = $"Selected transaction: {SelectedRow.TransactionNumber}.";
        }
    }

    private void NotifyListState()
    {
        NotifyPropertyChanged(nameof(HasRows));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(CanUseListControls));
        NotifyPropertyChanged(nameof(IsTransactionActive));
        NotifyPropertyChanged(nameof(IsTransactionPanelLocked));
        NotifyPropertyChanged(nameof(ActiveTransactionNumber));
        NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
        NotifyPropertyChanged(nameof(CanStartTransaction));
        NotifyPropertyChanged(nameof(CanStopTask));
        NotifyPropertyChanged(nameof(CanViewDocuments));
        NotifyPropertyChanged(nameof(CanAddDocument));
        NotifyPropertyChanged(nameof(CanCompleteTask));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        if (RefreshCommand is RelayCommand refresh)
        {
            refresh.RaiseCanExecuteChanged();
        }

        if (LoadSelectedCommand is RelayCommand load)
        {
            load.RaiseCanExecuteChanged();
        }

        if (StartTransactionCommand is RelayCommand start)
        {
            start.RaiseCanExecuteChanged();
        }

        if (StopTaskCommand is RelayCommand stop)
        {
            stop.RaiseCanExecuteChanged();
        }

        if (ViewDocumentsCommand is RelayCommand viewDocuments)
        {
            viewDocuments.RaiseCanExecuteChanged();
        }

        if (AddDocumentCommand is RelayCommand addDocument)
        {
            addDocument.RaiseCanExecuteChanged();
        }

        if (CompleteTaskCommand is RelayCommand complete)
        {
            complete.RaiseCanExecuteChanged();
        }
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int? LastRetrievedRecordCount
    {
        get => lastRetrievedRecordCount;
        set
        {
            if (lastRetrievedRecordCount == value)
            {
                return;
            }

            lastRetrievedRecordCount = value;
            NotifyPropertyChanged(nameof(RetrievedRecordCountText));
        }
    }

    private static bool Contains(string? value, string query)
    {
        return value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
