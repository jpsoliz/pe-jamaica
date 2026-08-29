using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using Microsoft.Win32;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Pla;

namespace ParcelWorkflowAddIn;

public sealed class TransactionPanelState : INotifyPropertyChanged
{
    public static TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public static TimeSpan SearchRefreshDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    private readonly InnolaSessionManager session;
    private readonly IInnolaTransactionService transactionService;
    private readonly InnolaTransactionLoadService? transactionLoadService;
    private readonly InnolaTransactionLoadService? compareTransactionLoadService;
    private readonly InnolaTransactionLifecycleCoordinator? lifecycleCoordinator;
    private readonly IActiveTransactionSwitchDecisionProvider switchDecisionProvider;
    private readonly HashSet<string> supportedTransactionTypes;
    private readonly HashSet<string> computeWorkflowStages;
    private readonly HashSet<string> compareWorkflowStages;
    private readonly Action<string>? compareWorkspaceLauncher;
    private readonly Action<string, ICompareTaskLifecycleService?>? compareWorkspaceLifecycleLauncher;
    private readonly Action<string?, string?, Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>>, Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>>, Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>>, string?> plaBTestInputLauncher;
    private readonly Func<PlaBTestEmulationInputViewModel, CancellationToken, Task<PlaBTestInputPreparationResult>> plaBRecoveryPreparer;
    private readonly Func<SelectedInnolaTransaction, CancellationToken, Task<PlaBCurrentTransactionSourceDownloadResult>> plaBCurrentSourceDownloader;
    private readonly IInnolaSpatialUnitService plaBSpatialUnitService;
    private readonly IInnolaTransactionLifecycleService plaBTransactionLifecycleService;
    private readonly PlaBPlanAnnexationTaskSettings plaBPlanAnnexationTaskSettings;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<PlaBMapCleanupResult>> plaBMapCleanup;
    private readonly Func<string, string, CaseFolderCreationResult> plaBCaseFolderPreparer;
    private readonly Func<bool> isCompareWorkspaceOpen;
    private readonly Func<bool> supportingDocumentsLauncher;
    private readonly Action supportingDocumentsRefresher;
    private readonly Func<DateTimeOffset> clock;
    private readonly bool autoRefreshOnLogin;
    private readonly List<InnolaTransactionRow> allRows = new();
    private readonly HashSet<string> locallyCompletedTransactionNumbers = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? searchRefreshCancellation;
    private string selectedFilter = "All tasks";
    private string searchText = string.Empty;
    private string sortField = "Received";
    private string sortDirection = "Descending";
    private InnolaTransactionRow? selectedRow;
    private bool isLoading;
    private bool refreshAfterLoginQueued;
    private bool workingMapPreloadQueued;
    private string? savedTransactionNumber;
    private string statusText = "Not logged in.";
    private string? errorText;
    private int? lastRetrievedRecordCount;

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        Func<DateTimeOffset>? clock,
        IReadOnlyCollection<string>? supportedTransactionTypes = null,
        IReadOnlyCollection<string>? computeWorkflowStages = null,
        IReadOnlyCollection<string>? compareWorkflowStages = null,
        Action<string>? compareWorkspaceLauncher = null,
        Action<string, ICompareTaskLifecycleService?>? compareWorkspaceLifecycleLauncher = null)
        : this(session, transactionService, processStep, null, null, null, clock, false, supportedTransactionTypes, computeWorkflowStages, compareWorkflowStages, compareWorkspaceLauncher, compareWorkspaceLifecycleLauncher)
    {
    }

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        InnolaTransactionLoadService? transactionLoadService = null,
        Func<DateTimeOffset>? clock = null,
        IReadOnlyCollection<string>? supportedTransactionTypes = null,
        IReadOnlyCollection<string>? computeWorkflowStages = null,
        IReadOnlyCollection<string>? compareWorkflowStages = null,
        Action<string>? compareWorkspaceLauncher = null,
        Action<string, ICompareTaskLifecycleService?>? compareWorkspaceLifecycleLauncher = null)
        : this(session, transactionService, processStep, transactionLoadService, null, null, clock, false, supportedTransactionTypes, computeWorkflowStages, compareWorkflowStages, compareWorkspaceLauncher, compareWorkspaceLifecycleLauncher)
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
        bool autoRefreshOnLogin = false,
        IReadOnlyCollection<string>? supportedTransactionTypes = null,
        IReadOnlyCollection<string>? computeWorkflowStages = null,
        IReadOnlyCollection<string>? compareWorkflowStages = null,
        Action<string>? compareWorkspaceLauncher = null,
        Action<string, ICompareTaskLifecycleService?>? compareWorkspaceLifecycleLauncher = null,
        Action? supportingDocumentsRefresher = null,
        InnolaTransactionLoadService? compareTransactionLoadService = null,
        Func<bool>? supportingDocumentsLauncher = null,
        Func<bool>? isCompareWorkspaceOpen = null,
        Action<string?, string?, Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>>, Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>>, Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>>, string?>? plaBTestInputLauncher = null,
        Func<PlaBTestEmulationInputViewModel, CancellationToken, Task<PlaBTestInputPreparationResult>>? plaBRecoveryPreparer = null,
        Func<SelectedInnolaTransaction, CancellationToken, Task<PlaBCurrentTransactionSourceDownloadResult>>? plaBCurrentSourceDownloader = null,
        IInnolaSpatialUnitService? plaBSpatialUnitService = null,
        IInnolaTransactionLifecycleService? plaBTransactionLifecycleService = null,
        PlaBPlanAnnexationTaskSettings? plaBPlanAnnexationTaskSettings = null,
        Func<IReadOnlyList<string>, CancellationToken, Task<PlaBMapCleanupResult>>? plaBMapCleanup = null,
        Func<string, string, CaseFolderCreationResult>? plaBCaseFolderPreparer = null)
    {
        this.session = session;
        this.transactionService = transactionService;
        this.transactionLoadService = transactionLoadService;
        this.compareTransactionLoadService = compareTransactionLoadService;
        this.lifecycleCoordinator = lifecycleCoordinator;
        this.switchDecisionProvider = switchDecisionProvider ?? new StayOnCurrentTransactionDecisionProvider();
        this.supportedTransactionTypes = new HashSet<string>(
            (supportedTransactionTypes is { Count: > 0 }
                ? supportedTransactionTypes
                : ShellState.SupportedTransactionTypes),
            StringComparer.OrdinalIgnoreCase);
        this.computeWorkflowStages = new HashSet<string>(
            (computeWorkflowStages is { Count: > 0 }
                ? computeWorkflowStages
                : ShellState.ComputeWorkflowStages),
            StringComparer.OrdinalIgnoreCase);
        this.compareWorkflowStages = new HashSet<string>(
            compareWorkflowStages ?? ShellState.CompareWorkflowStages,
            StringComparer.OrdinalIgnoreCase);
        this.compareWorkspaceLauncher = compareWorkspaceLauncher;
        this.compareWorkspaceLifecycleLauncher = compareWorkspaceLifecycleLauncher;
        this.plaBTestInputLauncher = plaBTestInputLauncher ?? ShowPlaBTestInputWindow;
        this.plaBRecoveryPreparer = plaBRecoveryPreparer ?? PreparePlaBRecoveryAsync;
        this.plaBCurrentSourceDownloader = plaBCurrentSourceDownloader ?? DownloadPlaBCurrentTransactionSourcesAsync;
        this.plaBSpatialUnitService = plaBSpatialUnitService ?? ShellState.SpatialUnits;
        this.plaBTransactionLifecycleService = plaBTransactionLifecycleService ?? ShellState.TransactionLifecycle;
        this.plaBPlanAnnexationTaskSettings = plaBPlanAnnexationTaskSettings ?? ShellState.PlaBPlanAnnexationTask;
        this.plaBMapCleanup = plaBMapCleanup ?? ArcGisPlaBMapCleanupService.RemoveAsync;
        this.plaBCaseFolderPreparer = plaBCaseFolderPreparer ?? ((transactionNumber, username) =>
            PreparePlaBCaseFolder(InnolaTransactionSettings.Load(), transactionNumber, username));
        this.isCompareWorkspaceOpen = isCompareWorkspaceOpen ?? CompareWorkspaceWindowLifecycle.IsOpen;
        this.supportingDocumentsLauncher = supportingDocumentsLauncher ?? TryShowSupportingDocumentsSafely;
        this.supportingDocumentsRefresher = supportingDocumentsRefresher ?? (() => { });
        ProcessStep = string.IsNullOrWhiteSpace(processStep) ? "parcel_workflow" : processStep;
        this.clock = clock ?? (() => DateTimeOffset.Now);
        this.autoRefreshOnLogin = autoRefreshOnLogin;

        Rows = new ObservableCollection<InnolaTransactionRow>();
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => CanRefresh);
        LoadSelectedCommand = new RelayCommand(async () => await LoadSelectedTransactionAsync(), () => CanLoadSelectedTransaction);
        StartTransactionCommand = new RelayCommand(async () => await StartSelectedTransactionAsync(), () => CanStartTransaction);
        StopTaskCommand = new RelayCommand(async () => await SaveCurrentTransactionAsync(), () => CanStopTask);
        ViewDocumentsCommand = new RelayCommand(ViewLoadedDocuments, () => CanViewDocuments);
        ShowSupportingDocumentsCommand = new RelayCommand(ShowSupportingDocuments, () => CanShowSupportingDocuments);
        OpenMapGeoreferenceCommand = new RelayCommand(async () => await OpenMapGeoreferenceAsync(), () => CanOpenMapGeoreference);
        OpenTitlePlanImagePlacementCommand = new RelayCommand(OpenTitlePlanImagePlacement, () => CanOpenTitlePlanImagePlacement);
        OpenPlaBTestInputCommand = new RelayCommand(OpenPlaBTestInput, () => CanOpenPlaBTestInput);
        AddDocumentCommand = new RelayCommand(ChooseAndAddDocuments, () => CanAddDocument);
        CompleteTaskCommand = new RelayCommand(async () => await CompleteCurrentTransactionAsync(), () => CanCompleteTask);
        ReopenCompareCommand = new RelayCommand(async () => await ReopenCompareWorkspaceAsync(), () => CanReopenCompare);
        session.SessionChanged += (_, _) => HandleSessionChanged();
        RefreshSessionState();
        QueueWorkingMapPreloadAfterLogin();
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

    public ICommand ShowSupportingDocumentsCommand { get; }

    public ICommand OpenMapGeoreferenceCommand { get; }

    public ICommand OpenTitlePlanImagePlacementCommand { get; }

    public ICommand OpenPlaBTestInputCommand { get; }

    public ICommand AddDocumentCommand { get; }

    public ICommand CompleteTaskCommand { get; }

    public ICommand ReopenCompareCommand { get; }

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
            NotifyPropertyChanged(nameof(CanEditListCriteria));
            NotifyPropertyChanged(nameof(CanSearchTransactions));
            NotifyPropertyChanged(nameof(CanUseListControls));
            NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
            NotifyPropertyChanged(nameof(CanOpenPlaBTestInput));
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

    public bool CanEditListCriteria => IsLoggedIn && !IsLoading && !IsTransactionPanelLocked;

    public bool CanSearchTransactions => IsLoggedIn && !IsTransactionPanelLocked;

    public bool CanUseListControls => IsLoggedIn && !IsLoading && allRows.Count > 0 && !IsTransactionPanelLocked;

    public bool HasRows => Rows.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsEmpty => IsLoggedIn && !IsLoading && !HasError && Rows.Count == 0;

    public string? LoadedCaseFolderPath => session.LoadedCaseFolderPath;

    public string SavedTransactionStatusText => "Suspended";

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
            QueueSearchRefresh();
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
            NotifySelectionDetails();
            NotifyCommandStates();
            UpdateSelectionStatus();
        }
    }

    public bool HasSelectedRow => SelectedRow is not null;

    public string SelectedTransactionNumberText => DetailValue("Transaction", SelectedRow?.TransactionNumber);

    public string SelectedTransactionNumberValue => DetailDisplay(SelectedRow?.TransactionNumber);

    public string SelectedTaskText => DetailValue("Task", SelectedRow?.TaskName);

    public string SelectedTaskValue => DetailDisplay(SelectedRow?.TaskName);

    public string SelectedTransactionTypeText => DetailValue("Type", SelectedRow?.DisplayTransactionType);

    public string SelectedTransactionTypeValue => DetailDisplay(SelectedRow?.DisplayTransactionType);

    public string SelectedApplicantText => DetailValue("Applicant", SelectedRow?.DisplayApplicant);

    public string SelectedApplicantValue => DetailDisplay(SelectedRow?.DisplayApplicant);

    public string SelectedOwnerText => DetailValue("Owner / responsible", SelectedRow?.DisplayOwnerOrResponsibleParty);

    public string SelectedOwnerValue => DetailDisplay(SelectedRow?.DisplayOwnerOrResponsibleParty);

    public string SelectedSurveyorText => DetailValue("Surveyor", SelectedRow?.DisplaySurveyor);

    public string SelectedParishText => DetailValue("Parish", SelectedRow?.DisplayParish);

    public string SelectedReceivedText => DetailValue("Received / assigned", SelectedRow?.DisplayReceivedOrAssigned);

    public string SelectedAssignmentText => DetailValue("Assigned", SelectedRow?.DisplayAssignment);

    public string SelectedStatusText => DetailValue("Status", SelectedRow?.DisplayStatus);

    public string SelectedStatusValue => DetailDisplay(SelectedRow?.DisplayStatus);

    public string SelectedReadinessText => DetailValue("Readiness", SelectedRow?.DisplayLoadability);

    public string RefreshTooltip => CanRefresh ? "Refresh the Innola transaction list." : RefreshDisabledReason();

    public string StartTransactionTooltip => CanStartTransaction ? "Load and start the selected transaction." : StartTransactionDisabledReason();

    public string StopTaskTooltip => CanStopTask ? "Save current transaction progress and release it for later resume." : StopTaskDisabledReason();

    public string ViewDocumentsTooltip => CanViewDocuments ? "View local source and output files for the active transaction." : DocumentsDisabledReason();

    public string ShowSupportingDocumentsTooltip => CanShowSupportingDocuments ? "Open supporting documents for the active transaction." : DocumentsDisabledReason();

    public string OpenMapGeoreferenceTooltip => CanOpenMapGeoreference ? "Open map georeference review for the active transaction." : DocumentsDisabledReason();

    public string OpenTitlePlanImagePlacementTooltip => CanOpenTitlePlanImagePlacement
        ? "Place a title-plan image from transaction attachments for map comparison."
        : TitlePlanImagePlacementDisabledReason();

    public string OpenPlaBTestInputTooltip => CanOpenPlaBTestInput
        ? "Open Plan Annexation Task for the selected transaction."
        : PlaBTestInputDisabledReason();

    public string AddDocumentTooltip => CanAddDocument ? "Attach a document to the active transaction." : DocumentsDisabledReason();

    public string CompleteTaskTooltip => CanCompleteTask ? "Complete the active transaction in Innola." : CompleteTaskDisabledReason();

    public string ReopenCompareTooltip => CanReopenCompare ? "Reopen the active Compare workspace." : ReopenCompareDisabledReason();

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

    public bool CanShowSupportingDocuments => CanViewDocuments;

    public bool CanOpenMapGeoreference => CanViewDocuments;

    public bool CanOpenTitlePlanImagePlacement => CanViewDocuments && HasTitlePlanPlacementSourceAttachments();

    public bool CanOpenPlaBTestInput => IsLoggedIn
        && !IsLoading
        && session.HasActiveTransaction
        && PlaBPlanAnnexationTaskGate.Evaluate(ActivePlaBTransactionRow(), plaBPlanAnnexationTaskSettings).IsEligible;

    public bool CanAddDocument => CanViewDocuments;

    public bool CanCompleteTask => IsLoggedIn && !IsLoading && lifecycleCoordinator is not null && session.CanCompleteTransaction;

    public bool CanReopenCompare => IsLoggedIn
        && !IsLoading
        && session.HasActiveTransaction
        && IsActiveTransactionCompareStage
        && !isCompareWorkspaceOpen()
        && (compareWorkspaceLauncher is not null || compareWorkspaceLifecycleLauncher is not null);

    private bool IsActiveTransactionCompareStage => ParcelWorkflowStageRouter.Resolve(
        session.SelectedTransaction?.TaskName,
        computeWorkflowStages,
        compareWorkflowStages) == ParcelWorkflowStageRoute.Compare;

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
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

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
        await LoadSelectedTransactionAsync(transactionLoadService, cancellationToken);
    }

    private async Task LoadSelectedTransactionAsync(InnolaTransactionLoadService? loader, CancellationToken cancellationToken)
    {
        if (SelectedRow is null || !CanLoadSelectedTransaction)
        {
            return;
        }

        var requestedRow = SelectedRow;
        if (!ValidateSupportedTransactionType(requestedRow))
        {
            return;
        }

        if (!ValidateWorkflowStage(requestedRow, out _))
        {
            return;
        }

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
        ClearSearchText(SelectedRow?.TransactionNumber);
        if (loader is null)
        {
            StatusText = $"Selected transaction: {requestedRow.TransactionNumber}.";
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = $"Loading transaction: {requestedRow.TransactionNumber}.";
        try
        {
            var result = await loader.LoadSelectedTransactionAsync(cancellationToken);
            if (!result.Success)
            {
                session.RestoreTransactionState(previousTransactionState);
                ErrorText = result.ErrorMessage ?? "Could not load transaction. Try again.";
                StatusText = ErrorText;
                return;
            }

            StatusText = string.IsNullOrWhiteSpace(result.StatusMessage)
                ? $"Opened case {requestedRow.TransactionNumber}."
                : result.StatusMessage;
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

        if (!ValidateSupportedTransactionType(SelectedRow))
        {
            return;
        }

        if (!ValidateWorkflowStage(SelectedRow, out var workflowRoute))
        {
            return;
        }

        var requestedRow = SelectedRow;
        var requestedTransactionNumber = requestedRow.TransactionNumber;
        var openPlaBTaskAfterStart = false;
        if (workflowRoute == ParcelWorkflowStageRoute.PlaBPlanAnnexation)
        {
            if (!LoadPlaBPlanAnnexationTaskForStart(requestedRow))
            {
                return;
            }
        }
        else
        {
            await LoadSelectedTransactionAsync(workflowRoute, cancellationToken);
        }

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
                RestoreSelectedRow(session.SelectedTransaction);
                if (workflowRoute == ParcelWorkflowStageRoute.PlaBPlanAnnexation)
                {
                    openPlaBTaskAfterStart = true;
                }
                else
                {
                    OpenWorkflowWorkspace(requestedTransactionNumber, workflowRoute);
                    TryShowSupportingDocumentsWindow(requestedTransactionNumber);
                }
            }
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
            NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        }

        if (openPlaBTaskAfterStart)
        {
            OpenPlaBTestInput();
        }
    }

    private Task LoadSelectedTransactionAsync(ParcelWorkflowStageRoute workflowRoute, CancellationToken cancellationToken)
    {
        if (workflowRoute != ParcelWorkflowStageRoute.Compare || compareTransactionLoadService is null)
        {
            return LoadSelectedTransactionAsync(cancellationToken);
        }

        return LoadSelectedTransactionAsync(compareTransactionLoadService, cancellationToken);
    }

    private bool LoadPlaBPlanAnnexationTaskForStart(InnolaTransactionRow requestedRow)
    {
        if (session.CurrentSession is null)
        {
            ErrorText = "Plan Annexation Task requires an active Innola session.";
            StatusText = ErrorText;
            return false;
        }

        var caseFolder = plaBCaseFolderPreparer(
            requestedRow.TransactionNumber.Trim(),
            session.CurrentSession.User.Username);
        if (!caseFolder.Success || caseFolder.Layout is null)
        {
            ErrorText = caseFolder.ErrorMessage ?? "Plan Annexation Task could not prepare the transaction case folder.";
            StatusText = ErrorText;
            return false;
        }

        session.SelectTransaction(requestedRow, clock());
        session.MarkTransactionLoaded(
            requestedRow.TransactionNumber,
            caseFolder.Layout.RootDirectory,
            clock().ToString("O"),
            wasRestoredFromResumePackage: false);
        ClearSearchText(requestedRow.TransactionNumber);
        StatusText = $"Selected Plan Annexation Task transaction: {requestedRow.TransactionNumber}.";
        return true;
    }

    private void OpenParcelWorkflowDockpane(string requestedTransactionNumber)
    {
        const string autoOpenFailureMessage = "Transaction {0} loaded. Open Parcel Workflow manually from Transactions if required.";
        OpenParcelWorkflowDockpane(requestedTransactionNumber, autoOpenFailureMessage, 1);
    }

    private void OpenWorkflowWorkspace(string requestedTransactionNumber, ParcelWorkflowStageRoute workflowRoute)
    {
        if (workflowRoute == ParcelWorkflowStageRoute.Compare)
        {
            OpenCompareWorkspace(requestedTransactionNumber);
            return;
        }

        OpenParcelWorkflowDockpane(requestedTransactionNumber);
    }

    private void OpenCompareWorkspace(string requestedTransactionNumber)
    {
        if (CompareWorkspaceWindowLifecycle.TryActivateExisting())
        {
            StatusText = $"Compare workspace for {requestedTransactionNumber} is already open.";
            NotifyListState();
            return;
        }

        var lifecycleService = lifecycleCoordinator is null
            ? null
            : new TransactionPanelCompareTaskLifecycleService(this);

        if (compareWorkspaceLifecycleLauncher is not null)
        {
            compareWorkspaceLifecycleLauncher(requestedTransactionNumber, lifecycleService);
            return;
        }

        if (compareWorkspaceLauncher is not null)
        {
            compareWorkspaceLauncher(requestedTransactionNumber);
            return;
        }

        StatusText = $"Transaction {requestedTransactionNumber} is in progress. Compare workspace is temporarily disabled while the review UI is moved into the stable Parcel Workflow pane.";
    }

    public Task ReopenCompareWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (!CanReopenCompare || string.IsNullOrWhiteSpace(ActiveTransactionNumber))
        {
            StatusText = session.HasActiveTransaction
                ? ReopenCompareDisabledReason()
                : "No active Compare transaction is available to reopen.";
            NotifyListState();
            return Task.CompletedTask;
        }

        OpenCompareWorkspace(ActiveTransactionNumber);
        TryShowSupportingDocumentsWindow(ActiveTransactionNumber);
        StatusText = $"Reopened Compare workspace for {ActiveTransactionNumber}.";
        NotifyListState();
        return Task.CompletedTask;
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

    private void TryShowSupportingDocumentsWindow(string requestedTransactionNumber)
    {
        if (!supportingDocumentsLauncher())
        {
            Debug.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Supporting Documents window activation failed for transaction {0}.",
                    requestedTransactionNumber));
            StatusText = $"Transaction {requestedTransactionNumber} loaded. Supporting Documents could not open automatically; use SD from Transactions List.";
        }
    }

    private static bool TryShowSupportingDocumentsSafely()
    {
        try
        {
            return SupportingDocumentsDockpaneViewModel.TryShow();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Supporting Documents window activation failed: {exception.Message}");
            return false;
        }
    }

    public async Task SaveCurrentTransactionAsync(CancellationToken cancellationToken = default)
    {
        _ = await SuspendCurrentTransactionForCompareAsync(ActiveTransactionNumber, cancellationToken);
    }

    internal async Task<CompareTaskLifecycleResult> SuspendCurrentTransactionForCompareAsync(string? transactionNumber, CancellationToken cancellationToken = default)
    {
        if (!CanStopTask || lifecycleCoordinator is null)
        {
            return CompareTaskLifecycleResult.Failure("Suspend task is unavailable for the current transaction state.");
        }

        if (!MatchesActiveTransaction(transactionNumber))
        {
            return CompareTaskLifecycleResult.Failure("Suspend task is available only for the active Compare transaction.");
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = "Suspending current transaction.";
        try
        {
            var savedTransactionNumber = session.LoadedTransactionNumber;
            var result = await lifecycleCoordinator.SaveAndCloseAsync(cancellationToken);
            ApplyLifecycleResult(result, "Suspended. Transaction released for later resume.");
            if (result.Success)
            {
                SavedTransactionNumber = savedTransactionNumber;
                session.ClearLoadedTransaction();
                RestoreSelectedRow(savedTransactionNumber);
                StatusText = result.StatusMessage ?? "Suspended. Select a transaction to continue.";
                NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
            }

            return result.Success
                ? CompareTaskLifecycleResult.Succeeded(StatusText)
                : CompareTaskLifecycleResult.Failure(ErrorText ?? StatusText);
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    internal async Task<CompareTaskLifecycleResult> CancelCurrentTransactionForCompareAsync(string? transactionNumber, CancellationToken cancellationToken = default)
    {
        if (lifecycleCoordinator is null)
        {
            return CompareTaskLifecycleResult.Failure("Cancel task is unavailable for the current transaction state.");
        }

        if (!MatchesActiveTransaction(transactionNumber))
        {
            return CompareTaskLifecycleResult.Failure("Cancel task is available only for the active Compare transaction.");
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = "Cancelling and closing transaction.";
        try
        {
            var cancelledTransactionNumber = session.LoadedTransactionNumber;
            var result = lifecycleCoordinator.CancelActiveProcess();
            if (!result.Success)
            {
                ErrorText = result.ErrorMessage ?? "Could not cancel transaction. Try again.";
                StatusText = ErrorText;
                return CompareTaskLifecycleResult.Failure(StatusText);
            }

            SavedTransactionNumber = null;
            session.ClearLoadedTransaction();
            SelectedRow = null;
            searchText = string.Empty;
            selectedFilter = "All tasks";
            NotifyPropertyChanged(nameof(SearchText));
            NotifyPropertyChanged(nameof(SelectedFilter));
            StatusText = result.StatusMessage ?? $"Cancelled {cancelledTransactionNumber}.";
            NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
            await RefreshAsync(cancellationToken);
            ErrorText = null;
            StatusText = result.StatusMessage ?? $"Cancelled {cancelledTransactionNumber}.";
            return CompareTaskLifecycleResult.Succeeded(StatusText);
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    public async Task CompleteCurrentTransactionAsync(CancellationToken cancellationToken = default)
    {
        _ = await CompleteCurrentTransactionForCompareAsync(ActiveTransactionNumber, cancellationToken);
    }

    internal async Task<CompareTaskLifecycleResult> CompleteCurrentTransactionForCompareAsync(string? transactionNumber, CancellationToken cancellationToken = default)
    {
        if (!CanCompleteTask || lifecycleCoordinator is null)
        {
            return CompareTaskLifecycleResult.Failure("Complete task is unavailable for the current transaction state.");
        }

        if (!MatchesActiveTransaction(transactionNumber))
        {
            return CompareTaskLifecycleResult.Failure("Complete task is available only for the active Compare transaction.");
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = "Completing and closing transaction.";
        try
        {
            var completedTransactionNumber = session.LoadedTransactionNumber;
            var result = await lifecycleCoordinator.CompleteAsync(cancellationToken);
            ApplyLifecycleResult(result, "Completed. Final package uploaded and transaction closed.");
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

            return result.Success
                ? CompareTaskLifecycleResult.Succeeded(StatusText)
                : CompareTaskLifecycleResult.Failure(ErrorText ?? StatusText);
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    private bool MatchesActiveTransaction(string? transactionNumber)
    {
        return !string.IsNullOrWhiteSpace(transactionNumber)
            && ActiveTransactionNumber is not null
            && ActiveTransactionNumber.Equals(transactionNumber, StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleWorkflowExitAsync(
        string? transactionNumber,
        string statusText,
        bool preserveSavedMarker,
        bool suppressTransactionFromList,
        bool refreshTransactions,
        CancellationToken cancellationToken = default)
    {
        if (!IsLoggedIn)
        {
            return;
        }

        if (session.IsTransactionLoaded || session.HasActiveTransaction)
        {
            session.ClearLoadedTransaction();
        }

        ErrorText = null;
        SavedTransactionNumber = preserveSavedMarker ? transactionNumber : null;

        if (!preserveSavedMarker)
        {
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                searchText = string.Empty;
                NotifyPropertyChanged(nameof(SearchText));
            }

            if (!selectedFilter.Equals("All tasks", StringComparison.OrdinalIgnoreCase))
            {
                selectedFilter = "All tasks";
                NotifyPropertyChanged(nameof(SelectedFilter));
            }
        }

        if (suppressTransactionFromList && !string.IsNullOrWhiteSpace(transactionNumber))
        {
            locallyCompletedTransactionNumbers.Add(transactionNumber);
            SelectedRow = null;
        }
        else
        {
            RestoreSelectedRow(transactionNumber);
        }

        StatusText = statusText;
        NotifyListState();

        if (refreshTransactions)
        {
            await RefreshAsync(cancellationToken);
            ErrorText = null;
            StatusText = statusText;
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

    private void ShowSupportingDocuments()
    {
        if (!CanShowSupportingDocuments)
        {
            StatusText = DocumentsDisabledReason();
            return;
        }

        TryShowSupportingDocumentsWindow(session.LoadedTransactionNumber ?? "Transaction");
    }

    private async Task OpenMapGeoreferenceAsync()
    {
        if (!CanOpenMapGeoreference)
        {
            StatusText = DocumentsDisabledReason();
            return;
        }

        var transactionNumber = session.LoadedTransactionNumber ?? "Transaction";
        try
        {
            var restoreResult = await new MapGeoreferenceOverlayService()
                .TryRestorePersistedOverlayAsync(transactionNumber)
                .ConfigureAwait(true);
            if (restoreResult.Success)
            {
                StatusText = restoreResult.Message;
                return;
            }

            StatusText = $"{restoreResult.Message} Opening M-Geo review.";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or ArcGIS.Core.CalledOnWrongThreadException)
        {
            StatusText = $"Could not restore saved M-Geo overlay: {exception.Message}. Opening M-Geo review.";
        }

        MapGeoreferenceWindow.ShowOrActivate(transactionNumber);
    }

    private void OpenTitlePlanImagePlacement()
    {
        if (!CanOpenTitlePlanImagePlacement)
        {
            StatusText = TitlePlanImagePlacementDisabledReason();
            return;
        }

        var transactionNumber = session.LoadedTransactionNumber ?? "Transaction";
        MapGeoreferenceWindow.ShowOrActivate(transactionNumber, MapGeoreferenceWorkflowMode.ImageComparison);
        StatusText = $"Opening title-plan image placement for {transactionNumber}.";
    }

    private async void OpenPlaBTestInput()
    {
        if (!IsLoggedIn || IsLoading)
        {
            StatusText = PlaBTestInputDisabledReason();
            return;
        }

        var row = ActivePlaBTransactionRow();
        var gate = PlaBPlanAnnexationTaskGate.Evaluate(row, plaBPlanAnnexationTaskSettings);
        if (!gate.IsEligible)
        {
            StatusText = gate.Reason ?? PlaBTestInputDisabledReason();
            return;
        }

        var selected = session.SelectedTransaction!;
        var currentTransactionNumber = selected.TransactionNumber;
        var peNumber = string.Empty;
        var status = $"Loading SpatialUnit {plaBPlanAnnexationTaskSettings.SpatialUnitExaminationField} for transaction {currentTransactionNumber}.";

        if (session.CurrentSession is null)
        {
            status = "Plan Annexation Task requires an active Innola session.";
        }
        else
        {
            var lookup = await plaBSpatialUnitService
                .GetExaminationNumberAsync(
                    session.CurrentSession,
                    selected,
                    plaBPlanAnnexationTaskSettings.SpatialUnitExaminationField,
                    CancellationToken.None)
                .ConfigureAwait(true);
            if (lookup.Success && !string.IsNullOrWhiteSpace(lookup.ExaminationNumber))
            {
                peNumber = lookup.ExaminationNumber.Trim();
                status = $"Ready to process Plan Annexation for PE {peNumber}.";
            }
            else
            {
                status = lookup.Message;
            }
        }

        plaBTestInputLauncher(
            currentTransactionNumber,
            peNumber,
            input => PreparePlaBTestInputAsync(input),
            input => CompletePlaBPlanAnnexationTaskAsync(input, selected),
            input => CancelPlaBPlanAnnexationTaskAsync(input, selected),
            status);
        StatusText = status;
    }

    private InnolaTransactionRow? ActivePlaBTransactionRow()
    {
        if (!session.HasActiveTransaction || session.SelectedTransaction is null)
        {
            return null;
        }

        return SelectedRow is not null && IsSelectedTransactionRow(SelectedRow, session.SelectedTransaction)
            ? SelectedRow
            : FindActiveTransactionRow(session.SelectedTransaction);
    }

    private static void ShowPlaBTestInputWindow(
        string? currentTransactionNumber,
        string? peNumber,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>> prepareHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> completeHandler,
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>> cancelHandler,
        string? statusText)
    {
        PlaBTestInputWindow.ShowOrActivate(
            currentTransactionNumber,
            peNumber,
            prepareHandler,
            completeHandler,
            cancelHandler,
            statusText);
    }

    internal async Task<PlaBTestInputPreparationResult> PreparePlaBTestInputAsync(
        PlaBTestEmulationInputViewModel input,
        CancellationToken cancellationToken = default)
    {
        if (input is null || !input.CanPrepare)
        {
            return PlaBTestInputPreparationResult.Failed("Enter a current transaction number and valid PE number before preparing PLA_B.");
        }

        PlaBTestEmulationContext.Set(input.CurrentTransactionNumber, input.PeNumber);
        var result = await plaBRecoveryPreparer(input, cancellationToken).ConfigureAwait(true);
        StatusText = result.Success
            ? $"PLA_B recovery loaded for TR {input.CurrentTransactionNumber.Trim()} using PE {input.NormalizedPeNumber}."
            : result.Message;
        return result;
    }

    internal async Task<PlaBTaskCompletionResult> CompletePlaBPlanAnnexationTaskAsync(
        PlaBTestEmulationInputViewModel input,
        CancellationToken cancellationToken = default)
    {
        return await CompletePlaBPlanAnnexationTaskAsync(input, session.SelectedTransaction, cancellationToken).ConfigureAwait(true);
    }

    private async Task<PlaBTaskCompletionResult> CompletePlaBPlanAnnexationTaskAsync(
        PlaBTestEmulationInputViewModel input,
        SelectedInnolaTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        if (input is null || !input.CanComplete)
        {
            return PlaBTaskCompletionResult.Failed("Run Process successfully before completing Plan Annexation Preparation.");
        }

        if (session.CurrentSession is null || transaction is null)
        {
            return PlaBTaskCompletionResult.Failed("Plan Annexation completion requires an active Innola session and selected transaction.");
        }

        if (!string.Equals(
            InnolaTransactionNumbers.NormalizeWorkflowKey(transaction.TransactionNumber),
            InnolaTransactionNumbers.NormalizeWorkflowKey(input.CurrentTransactionNumber),
            StringComparison.OrdinalIgnoreCase))
        {
            return PlaBTaskCompletionResult.Failed("Plan Annexation completion is available only for the active transaction shown in the form.");
        }

        var caseFolderPath = session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(caseFolderPath))
        {
            caseFolderPath = PreparePlaBCaseFolder(
                InnolaTransactionSettings.Load(),
                input.CurrentTransactionNumber.Trim(),
                session.CurrentSession.User.Username).Layout?.RootDirectory;
        }

        if (string.IsNullOrWhiteSpace(caseFolderPath))
        {
            return PlaBTaskCompletionResult.Failed("Plan Annexation completion could not resolve the transaction case folder.");
        }

        var request = new InnolaTransactionLifecycleRequest(
            session.CurrentSession,
            transaction,
            caseFolderPath,
            "loaded",
            "pla_b_plan_annexation_complete",
            plaBPlanAnnexationTaskSettings.NextStageName);
        var result = await plaBTransactionLifecycleService.CompleteAsync(request, cancellationToken).ConfigureAwait(true);
        if (!result.Success)
        {
            return PlaBTaskCompletionResult.Failed(result.Message ?? "Could not complete Plan Annexation Preparation. Try again.");
        }

        await HandleWorkflowExitAsync(
            input.CurrentTransactionNumber.Trim(),
            "Plan Annexation Preparation completed and moved to Review and Sign Plan Annexed Diagram.",
            suppressTransactionFromList: true,
            preserveSavedMarker: false,
            refreshTransactions: false,
            cancellationToken: cancellationToken).ConfigureAwait(true);

        var cleanup = await plaBMapCleanup(input.ProcessMapGroupNames, cancellationToken).ConfigureAwait(true);
        if (!cleanup.Success)
        {
            return PlaBTaskCompletionResult.Succeeded(
                $"Plan Annexation Preparation completed, but map cleanup needs manual review: {cleanup.Message}");
        }

        return PlaBTaskCompletionResult.Succeeded("Plan Annexation Preparation completed and map content was cleared.");
    }

    private async Task<PlaBTaskCompletionResult> CancelPlaBPlanAnnexationTaskAsync(
        PlaBTestEmulationInputViewModel input,
        SelectedInnolaTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        if (lifecycleCoordinator is null)
        {
            return PlaBTaskCompletionResult.Failed("Cancel task is unavailable for the current transaction state.");
        }

        if (transaction is null || !MatchesActiveTransaction(transaction.TransactionNumber))
        {
            return PlaBTaskCompletionResult.Failed("Cancel is available only for the active Plan Annexation transaction.");
        }

        var cancelledTransactionNumber = transaction.TransactionNumber;
        var result = lifecycleCoordinator.CancelActiveProcess();
        if (!result.Success)
        {
            var message = result.ErrorMessage ?? "Could not cancel Plan Annexation task. Try again.";
            ErrorText = message;
            StatusText = message;
            return PlaBTaskCompletionResult.Failed(message);
        }

        await HandleWorkflowExitAsync(
            cancelledTransactionNumber,
            result.StatusMessage ?? $"Cancelled Plan Annexation task {cancelledTransactionNumber}.",
            preserveSavedMarker: false,
            suppressTransactionFromList: false,
            refreshTransactions: true,
            cancellationToken: cancellationToken).ConfigureAwait(true);

        var cleanup = await plaBMapCleanup(input?.ProcessMapGroupNames ?? Array.Empty<string>(), cancellationToken).ConfigureAwait(true);
        if (!cleanup.Success)
        {
            return PlaBTaskCompletionResult.Succeeded(
                $"{StatusText} Map cleanup needs manual review: {cleanup.Message}");
        }

        return PlaBTaskCompletionResult.Succeeded(StatusText);
    }

    private async Task<PlaBTestInputPreparationResult> PreparePlaBRecoveryAsync(
        PlaBTestEmulationInputViewModel input,
        CancellationToken cancellationToken)
    {
        if (session.CurrentSession is null)
        {
            return PlaBTestInputPreparationResult.Failed("PLA_B recovery requires an active Innola session.");
        }

        var settings = InnolaTransactionSettings.Load();
        var enterprisePlan = PlaBEnterpriseWorkingLayerLookupPlanner.Build(settings, input.PeNumber);
        if (!enterprisePlan.Success)
        {
            return PlaBTestInputPreparationResult.Failed(enterprisePlan.Message ?? "PLA_B working_review recovery could not be prepared.");
        }

        var normalizedPe = input.NormalizedPeNumber!;
        var currentSourceLoad = await LoadPlaBCurrentTransactionSourcesAsync(input.CurrentTransactionNumber, cancellationToken).ConfigureAwait(true);
        if (!currentSourceLoad.Success)
        {
            return PlaBTestInputPreparationResult.Failed(currentSourceLoad.Message);
        }

        var layoutResult = PreparePlaBCaseFolder(settings, input.CurrentTransactionNumber.Trim(), session.CurrentSession.User.Username);
        if (!layoutResult.Success || layoutResult.Layout is null)
        {
            return PlaBTestInputPreparationResult.Failed(layoutResult.ErrorMessage ?? "PLA_B recovery case folder could not be prepared.");
        }

        var peLookup = await new PlaBRelatedPeTransactionFinder(transactionService)
            .FindAsync(session.CurrentSession, normalizedPe, cancellationToken)
            .ConfigureAwait(true);
        if (!peLookup.Success || peLookup.Transaction is null)
        {
            return PlaBTestInputPreparationResult.Failed(peLookup.Message ?? "Related PE transaction could not be found.");
        }

        var detailService = ShellState.TransactionDetails;
        var peSelected = ToSelectedTransaction(peLookup.Transaction, clock());
        var detailResult = await detailService
            .GetTransactionDetailAsync(session.CurrentSession, peSelected, cancellationToken)
            .ConfigureAwait(true);
        if (!detailResult.Success || detailResult.Detail is null)
        {
            var reason = detailResult.ErrorMessage ?? "Related PE transaction detail could not be loaded.";
            var category = string.IsNullOrWhiteSpace(detailResult.ErrorCode) ? string.Empty : $" ({detailResult.ErrorCode})";
            return PlaBTestInputPreparationResult.Failed($"PLA_B could not load related PE transaction {normalizedPe}: {reason}{category}");
        }

        var package = await new PlaBPePackageDownloader(detailService)
            .DownloadAsync(session.CurrentSession, detailResult.Detail, layoutResult.Layout, cancellationToken)
            .ConfigureAwait(true);
        if (!package.Success || string.IsNullOrWhiteSpace(package.PackagePath))
        {
            return PlaBTestInputPreparationResult.Failed(package.Message ?? "Related PE package could not be downloaded.");
        }

        var gdb = PlaBPackageService.ExtractAndResolveOutputGdb(layoutResult.Layout, package.PackagePath, normalizedPe);
        if (!gdb.Success || string.IsNullOrWhiteSpace(gdb.GdbPath))
        {
            return PlaBTestInputPreparationResult.Failed(gdb.Message ?? "Related PE output geodatabase could not be resolved.");
        }

        var mapPlan = PlaBMapReviewPlanner.Build(input.CurrentTransactionNumber, normalizedPe, gdb.GdbPath);
        if (!mapPlan.Success)
        {
            return PlaBTestInputPreparationResult.Failed(mapPlan.Message ?? "PLA_B map recovery plan could not be built.");
        }

        var mapLoad = await new ArcGisPlaBMapRecoveryLoader()
            .LoadAsync(settings, detailResult.Detail, enterprisePlan, mapPlan, cancellationToken)
            .ConfigureAwait(true);
        if (!mapLoad.Success)
        {
            return PlaBTestInputPreparationResult.Failed(mapLoad.Message);
        }

        return PlaBTestInputPreparationResult.Succeeded(
            $"PLA_B recovery loaded.\nCurrent TR source files: {currentSourceLoad.SourceFileCount} in {currentSourceLoad.SourceDirectory}{FormatPlaBSourceWarnings(currentSourceLoad.Warnings)}\nCurrent TR group: {mapPlan.CurrentTransactionGroupName}\nWorking_review query: {enterprisePlan.ScopeField} = {enterprisePlan.ScopeValue}\nPE group: {mapPlan.PeTransactionGroupName}\nGDB: {gdb.GdbPath}",
            new[] { mapPlan.CurrentTransactionGroupName!, mapPlan.PeTransactionGroupName! });
    }

    private async Task<PlaBTestInputPreparationResult> OpenPlaBCurrentTransactionViewerAsync(
        PlaBTestEmulationInputViewModel input,
        CancellationToken cancellationToken = default)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.CurrentTransactionNumber))
        {
            return PlaBTestInputPreparationResult.Failed("Enter a current transaction number before opening the viewer.");
        }

        var currentSourceLoad = await LoadPlaBCurrentTransactionSourcesAsync(input.CurrentTransactionNumber, cancellationToken).ConfigureAwait(true);
        if (!currentSourceLoad.Success)
        {
            return PlaBTestInputPreparationResult.Failed(currentSourceLoad.Message);
        }

        return OpenLoadedSupportingDocuments(input.CurrentTransactionNumber.Trim(), currentSourceLoad);
    }

    private async Task<PlaBCurrentTransactionSourceLoadResult> LoadPlaBCurrentTransactionSourcesAsync(
        string transactionNumber,
        CancellationToken cancellationToken)
    {
        var requestedTransactionNumber = transactionNumber.Trim();
        IReadOnlyList<string> warnings = Array.Empty<string>();
        if (session.HasActiveTransaction && !IsLoadedTransaction(requestedTransactionNumber))
        {
            return PlaBCurrentTransactionSourceLoadResult.Failed(
                $"Active transaction {session.SelectedTransaction?.TransactionNumber} is in progress. Stop/save it before loading {requestedTransactionNumber} for PLA_B viewing.");
        }

        if (!IsLoadedTransaction(requestedTransactionNumber))
        {
            var rowResult = await FindOrFetchAvailableTransactionRowAsync(requestedTransactionNumber, cancellationToken).ConfigureAwait(true);
            if (!rowResult.Success || rowResult.Row is null)
            {
                return PlaBCurrentTransactionSourceLoadResult.Failed(rowResult.Message);
            }

            if (!rowResult.Row.IsLoadable)
            {
                return PlaBCurrentTransactionSourceLoadResult.Failed(rowResult.Row.UnavailableReason ?? $"Current transaction {requestedTransactionNumber} is not loadable.");
            }

            var previousSelection = SelectedRow;
            SelectedRow = rowResult.Row;
            session.SelectTransaction(rowResult.Row, clock());
            var sourceDownload = await plaBCurrentSourceDownloader(session.SelectedTransaction!, cancellationToken).ConfigureAwait(true);
            if (!sourceDownload.Success || sourceDownload.Layout is null || sourceDownload.Detail is null || string.IsNullOrWhiteSpace(sourceDownload.LoadedAt))
            {
                if (previousSelection is not null && !ReferenceEquals(previousSelection, rowResult.Row))
                {
                    SelectedRow = previousSelection;
                }

                return PlaBCurrentTransactionSourceLoadResult.Failed(sourceDownload.Message);
            }

            warnings = sourceDownload.Warnings;
            session.MarkTransactionLoaded(
                sourceDownload.Detail.TransactionNumber,
                sourceDownload.Layout.RootDirectory,
                sourceDownload.LoadedAt,
                wasRestoredFromResumePackage: false);
            NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        }

        var sourceDirectory = CaseFolderLayout.FromRootDirectory(session.LoadedCaseFolderPath!).SourceDirectory;
        var sourceFileCount = Directory.Exists(sourceDirectory)
            ? Directory.EnumerateFiles(sourceDirectory).Count()
            : 0;
        if (sourceFileCount > 0)
        {
            return PlaBCurrentTransactionSourceLoadResult.Succeeded(sourceDirectory, sourceFileCount, warnings);
        }

        var noFilesWarning = $"Current transaction {requestedTransactionNumber} loaded, but no files were downloaded to {sourceDirectory}.";
        return PlaBCurrentTransactionSourceLoadResult.Succeeded(
            sourceDirectory,
            0,
            warnings.Concat(new[] { noFilesWarning }).ToArray());
    }

    private Task<PlaBCurrentTransactionSourceDownloadResult> DownloadPlaBCurrentTransactionSourcesAsync(
        SelectedInnolaTransaction selected,
        CancellationToken cancellationToken)
    {
        return new PlaBCurrentTransactionSourceDownloadService(ShellState.TransactionDetails)
            .DownloadAsync(
                session.CurrentSession!,
                selected,
                InnolaTransactionSettings.Load().CaseFolderOutputRoot,
                session.CurrentSession!.User.Username,
                cancellationToken);
    }

    private async Task<PlaBTransactionRowLookupResult> FindOrFetchAvailableTransactionRowAsync(
        string transactionNumber,
        CancellationToken cancellationToken)
    {
        var localRow = FindAvailableTransactionRow(transactionNumber);
        if (localRow is not null)
        {
            return PlaBTransactionRowLookupResult.Succeeded(localRow);
        }

        if (session.CurrentSession is null)
        {
            return PlaBTransactionRowLookupResult.Failed("PLA_B current transaction lookup requires an active Innola session.");
        }

        var currentSession = session.CurrentSession;
        var result = await transactionService.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
            currentSession.ServerUrl,
            currentSession.AccessToken,
            currentSession.User.Username,
            currentSession.User.Groups,
            ProcessStep,
            null,
            transactionNumber,
            SortField,
            SortDirection), cancellationToken).ConfigureAwait(true);
        if (!result.Success)
        {
            return PlaBTransactionRowLookupResult.Failed(result.ErrorMessage ?? $"Current transaction {transactionNumber} could not be found.");
        }

        var normalized = InnolaTransactionNumbers.NormalizeWorkflowKey(transactionNumber);
        var row = result.Rows.FirstOrDefault(candidate =>
            string.Equals(
                InnolaTransactionNumbers.NormalizeWorkflowKey(candidate.TransactionNumber),
                normalized,
                StringComparison.OrdinalIgnoreCase));
        return row is null
            ? PlaBTransactionRowLookupResult.Failed($"Current transaction {transactionNumber} was not returned by Innola.")
            : PlaBTransactionRowLookupResult.Succeeded(row);
    }

    private PlaBTestInputPreparationResult OpenLoadedSupportingDocuments(
        string transactionNumber,
        PlaBCurrentTransactionSourceLoadResult sourceLoad)
    {
        if (!supportingDocumentsLauncher())
        {
            return PlaBTestInputPreparationResult.Failed(
                $"Transaction {transactionNumber} source files are loaded, but the document viewer could not be opened.");
        }

        return PlaBTestInputPreparationResult.Succeeded(
            $"Opened document viewer for current transaction {transactionNumber}.\nSource files: {sourceLoad.SourceFileCount} in {sourceLoad.SourceDirectory}{FormatPlaBSourceWarnings(sourceLoad.Warnings)}");
    }

    private static string FormatPlaBSourceWarnings(IReadOnlyList<string> warnings)
    {
        return warnings.Count == 0
            ? string.Empty
            : $"\nSkipped attachments: {warnings.Count}. First issue: {warnings[0]}";
    }

    private sealed record PlaBCurrentTransactionSourceLoadResult(
        bool Success,
        string Message,
        string? SourceDirectory,
        int SourceFileCount,
        IReadOnlyList<string> Warnings)
    {
        public static PlaBCurrentTransactionSourceLoadResult Succeeded(
            string sourceDirectory,
            int sourceFileCount,
            IReadOnlyList<string>? warnings = null)
        {
            return new PlaBCurrentTransactionSourceLoadResult(true, string.Empty, sourceDirectory, sourceFileCount, warnings ?? Array.Empty<string>());
        }

        public static PlaBCurrentTransactionSourceLoadResult Failed(string message)
        {
            return new PlaBCurrentTransactionSourceLoadResult(false, message, null, 0, Array.Empty<string>());
        }
    }

    private sealed record PlaBTransactionRowLookupResult(
        bool Success,
        InnolaTransactionRow? Row,
        string Message)
    {
        public static PlaBTransactionRowLookupResult Succeeded(InnolaTransactionRow row)
        {
            return new PlaBTransactionRowLookupResult(true, row, string.Empty);
        }

        public static PlaBTransactionRowLookupResult Failed(string message)
        {
            return new PlaBTransactionRowLookupResult(false, null, message);
        }
    }

    private bool IsLoadedTransaction(string transactionNumber)
    {
        return session.IsTransactionLoaded
            && !string.IsNullOrWhiteSpace(session.LoadedCaseFolderPath)
            && string.Equals(
                InnolaTransactionNumbers.NormalizeWorkflowKey(session.LoadedTransactionNumber ?? string.Empty),
                InnolaTransactionNumbers.NormalizeWorkflowKey(transactionNumber),
                StringComparison.OrdinalIgnoreCase);
    }

    private InnolaTransactionRow? FindAvailableTransactionRow(string transactionNumber)
    {
        var normalized = InnolaTransactionNumbers.NormalizeWorkflowKey(transactionNumber);
        return allRows.FirstOrDefault(row =>
            string.Equals(
                InnolaTransactionNumbers.NormalizeWorkflowKey(row.TransactionNumber),
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    private InnolaTransactionRow? FindActiveTransactionRow(SelectedInnolaTransaction selected)
    {
        var taskMatch = allRows.FirstOrDefault(row => IsSelectedTransactionRow(row, selected));
        if (taskMatch is not null)
        {
            return taskMatch;
        }

        var normalized = InnolaTransactionNumbers.NormalizeWorkflowKey(selected.TransactionNumber);
        return allRows.FirstOrDefault(row =>
            string.Equals(
                InnolaTransactionNumbers.NormalizeWorkflowKey(row.TransactionNumber),
                normalized,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.TaskName, selected.TaskName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.ProcessStep, selected.ProcessStep, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSelectedTransactionRow(InnolaTransactionRow row, SelectedInnolaTransaction selected)
    {
        if (!string.Equals(row.TaskId, selected.TaskId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(
                InnolaTransactionNumbers.NormalizeWorkflowKey(row.TransactionNumber),
                InnolaTransactionNumbers.NormalizeWorkflowKey(selected.TransactionNumber),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.ProcessStep, selected.ProcessStep, StringComparison.OrdinalIgnoreCase);
    }

    private static CaseFolderCreationResult PreparePlaBCaseFolder(InnolaTransactionSettings settings, string transactionNumber, string username)
    {
        var layout = CaseFolderLayout.For(settings.CaseFolderOutputRoot, transactionNumber);
        return Directory.Exists(layout.RootDirectory)
            ? CaseFolderCreationResult.Created(layout)
            : new CaseFolderStore().CreateCase(settings.CaseFolderOutputRoot, transactionNumber, username);
    }

    private static SelectedInnolaTransaction ToSelectedTransaction(InnolaTransactionRow row, DateTimeOffset selectedAt)
    {
        return new SelectedInnolaTransaction(
            row.TaskId,
            row.TransactionId,
            row.TransactionNumber,
            row.TaskName,
            row.ProcessStep,
            selectedAt,
            row.ApplicationId,
            row.TransactionType,
            row.Status,
            row.AssignedUser,
            row.AssignedGroup);
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
                supportingDocumentsRefresher();
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
        if (!session.IsTransactionLoaded)
        {
            MapGeoreferenceWindowLifecycle.CloseIfOpen();
        }

        if (!session.IsLoggedIn)
        {
            refreshAfterLoginQueued = false;
            workingMapPreloadQueued = false;
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

        QueueWorkingMapPreloadAfterLogin();
        QueueRefreshAfterLogin();
    }

    private void RefreshSessionState()
    {
        NotifyPropertyChanged(nameof(IsLoggedIn));
        NotifyPropertyChanged(nameof(CanRefresh));
        NotifyPropertyChanged(nameof(CanEditListCriteria));
        NotifyPropertyChanged(nameof(CanSearchTransactions));
        NotifyPropertyChanged(nameof(CanUseListControls));
        NotifyPropertyChanged(nameof(IsTransactionActive));
        NotifyPropertyChanged(nameof(IsTransactionPanelLocked));
        NotifyPropertyChanged(nameof(ActiveTransactionNumber));
        NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
        NotifyPropertyChanged(nameof(CanStartTransaction));
        NotifyPropertyChanged(nameof(CanStopTask));
        NotifyPropertyChanged(nameof(CanViewDocuments));
        NotifyPropertyChanged(nameof(CanShowSupportingDocuments));
        NotifyPropertyChanged(nameof(CanOpenMapGeoreference));
        NotifyPropertyChanged(nameof(CanOpenTitlePlanImagePlacement));
        NotifyPropertyChanged(nameof(CanOpenPlaBTestInput));
        NotifyPropertyChanged(nameof(CanAddDocument));
        NotifyPropertyChanged(nameof(CanCompleteTask));
        NotifyPropertyChanged(nameof(CanReopenCompare));
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
            .Where(IsDefaultActiveQueueRow)
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

    private void QueueSearchRefresh()
    {
        if (!IsLoggedIn || IsTransactionPanelLocked)
        {
            return;
        }

        searchRefreshCancellation?.Cancel();
        searchRefreshCancellation?.Dispose();
        searchRefreshCancellation = new CancellationTokenSource();
        var token = searchRefreshCancellation.Token;
        _ = RefreshAfterSearchDelayAsync(token);
    }

    private async Task RefreshAfterSearchDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (SearchRefreshDelay > TimeSpan.Zero)
            {
                await Task.Delay(SearchRefreshDelay, cancellationToken);
            }

            await RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearSearchText(string? selectedTransactionNumber)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        searchRefreshCancellation?.Cancel();
        searchRefreshCancellation?.Dispose();
        searchRefreshCancellation = null;
        searchText = string.Empty;
        ApplyView(selectedTransactionNumber);
        NotifyPropertyChanged(nameof(SearchText));
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

    private void RestoreSelectedRow(SelectedInnolaTransaction? transaction)
    {
        if (transaction is null)
        {
            SelectedRow = null;
            return;
        }

        SelectedRow = Rows.FirstOrDefault(row => IsSelectedTransactionRow(row, transaction))
            ?? Rows.FirstOrDefault(row =>
                row.TransactionNumber.Equals(transaction.TransactionNumber, StringComparison.OrdinalIgnoreCase)
                && row.TaskName.Equals(transaction.TaskName, StringComparison.OrdinalIgnoreCase))
            ?? Rows.FirstOrDefault(row => row.TransactionNumber.Equals(transaction.TransactionNumber, StringComparison.OrdinalIgnoreCase));
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

    private void QueueWorkingMapPreloadAfterLogin()
    {
        if (workingMapPreloadQueued || !IsLoggedIn || IsTransactionPanelLocked)
        {
            return;
        }

        workingMapPreloadQueued = true;
        ShellState.StartWorkingMapPreloadAfterLogin();
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
            return source.Where(row => MatchesCurrentUser(row.AssignedUser));
        }

        if (SelectedFilter.Equals("Group tasks", StringComparison.OrdinalIgnoreCase))
        {
            return source.Where(row => MatchesCurrentGroup(row.AssignedGroup));
        }

        return source;
    }

    private bool MatchesCurrentUser(string? assignedUser)
    {
        if (session.CurrentUser is null || string.IsNullOrWhiteSpace(assignedUser))
        {
            return false;
        }

        var userTokens = new[]
            {
                session.CurrentSession?.Username,
                session.CurrentUser.Username,
                session.CurrentUser.DisplayName
            }
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return SplitAssignmentTokens(assignedUser).Any(token =>
            userTokens.Any(userToken => IsUserTokenMatch(token, userToken)));
    }

    private bool MatchesCurrentGroup(string? assignedGroup)
    {
        if (string.IsNullOrWhiteSpace(assignedGroup) || session.CurrentUser is null)
        {
            return false;
        }

        var userGroups = session.CurrentUser.Groups
            .Concat(session.CurrentUser.Roles)
            .Select(NormalizeGroupToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (userGroups.Count == 0)
        {
            return false;
        }

        return SplitAssignmentTokens(assignedGroup)
            .Select(NormalizeGroupToken)
            .Any(userGroups.Contains);
    }

    private static IEnumerable<string> SplitAssignmentTokens(string value)
    {
        return value
            .Split(new[] { ',', ';', '|', '/', '\\', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(token => token.Split(new[] { " - ", ":", "=", "\t", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static bool IsUserTokenMatch(string token, string username)
    {
        return token.Equals(username, StringComparison.OrdinalIgnoreCase)
            || token.StartsWith(username + "@", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith(username + " ", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith(username + " - ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeGroupToken(string token)
    {
        var normalized = token.Trim();
        return normalized.StartsWith("ROLE_", StringComparison.OrdinalIgnoreCase)
            ? normalized[5..]
            : normalized;
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
            || Contains(row.Applicant, SearchText)
            || Contains(row.OwnerOrResponsibleParty, SearchText)
            || Contains(row.Surveyor, SearchText)
            || Contains(row.Parish, SearchText)
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

    private static string DetailValue(string label, string? value)
    {
        return $"{label}: {DetailDisplay(value)}";
    }

    private static string DetailDisplay(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Not provided"
            : value;
    }

    private bool IsDefaultActiveQueueRow(InnolaTransactionRow row)
    {
        return row.IsAvailable
            && row.IsLoadable
            && row.Status is not InnolaTransactionStatus.Completed
                and not InnolaTransactionStatus.Unavailable
                and not InnolaTransactionStatus.WrongStep
                and not InnolaTransactionStatus.Locked;
    }

    private string RefreshDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before refreshing transactions.";
        }

        if (IsLoading)
        {
            return "Refresh is already running.";
        }

        if (IsTransactionPanelLocked)
        {
            return $"Finish, suspend, or cancel transaction {ActiveTransactionNumber} before refreshing.";
        }

        return "Refresh is not available right now.";
    }

    private string StartTransactionDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before starting a transaction.";
        }

        if (IsLoading)
        {
            return "Wait for the transaction list to finish loading.";
        }

        if (lifecycleCoordinator is null)
        {
            return "Transaction lifecycle actions are not configured.";
        }

        if (session.HasActiveTransaction)
        {
            return $"Transaction {ActiveTransactionNumber} is already active.";
        }

        if (SelectedRow is null)
        {
            return "Select a transaction to start.";
        }

        return SelectedRow.DisplayLoadability;
    }

    private string StopTaskDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before saving progress.";
        }

        if (IsLoading)
        {
            return "Wait for the current action to finish.";
        }

        if (lifecycleCoordinator is null)
        {
            return "Transaction lifecycle actions are not configured.";
        }

        return "Start or reopen a transaction before saving progress.";
    }

    private string DocumentsDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before viewing transaction documents.";
        }

        if (IsLoading)
        {
            return "Wait for the current action to finish.";
        }

        return "Load a transaction before using documents.";
    }

    private string TitlePlanImagePlacementDisabledReason()
    {
        if (!CanViewDocuments)
        {
            return DocumentsDisabledReason();
        }

        return "No PDF or raster image attachments are available in the loaded transaction Case Folder source area.";
    }

    private string PlaBTestInputDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before opening PLA_B test input.";
        }

        if (IsLoading)
        {
            return "Wait for the current action to finish.";
        }

        if (!session.HasActiveTransaction || session.SelectedTransaction is null)
        {
            return "Start the In Plan Annexation Preparation task before opening Plan Annexation Task.";
        }

        var activeRow = ActivePlaBTransactionRow();
        var activeGate = PlaBPlanAnnexationTaskGate.Evaluate(activeRow, plaBPlanAnnexationTaskSettings);
        if (!activeGate.IsEligible)
        {
            var activeTaskName = session.SelectedTransaction.TaskName;
            return $"Start/run the In Plan Annexation Preparation task before opening Plan Annexation Task. Active task is {activeTaskName}.";
        }

        return PlaBPlanAnnexationTaskGate.Evaluate(SelectedRow, plaBPlanAnnexationTaskSettings).Reason
            ?? "Plan Annexation Task is not available for the selected transaction.";
    }

    private bool HasTitlePlanPlacementSourceAttachments()
    {
        var folder = session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        var sourceFolder = Path.Combine(folder, "source");
        if (!Directory.Exists(sourceFolder))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateFiles(sourceFolder)
                .Any(path => IsTitlePlanPlacementSourceExtension(Path.GetExtension(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool IsTitlePlanPlacementSourceExtension(string? extension)
    {
        return extension?.ToLowerInvariant() is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff";
    }

    private string CompleteTaskDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before completing a transaction.";
        }

        if (IsLoading)
        {
            return "Wait for the current action to finish.";
        }

        if (lifecycleCoordinator is null)
        {
            return "Transaction lifecycle actions are not configured.";
        }

        return "Complete is available after the active transaction is ready.";
    }

    private string ReopenCompareDisabledReason()
    {
        if (!IsLoggedIn)
        {
            return "Log in before reopening Compare.";
        }

        if (IsLoading)
        {
            return "Wait for the current action to finish.";
        }

        if (!session.HasActiveTransaction)
        {
            return "Start a Compare transaction before reopening it.";
        }

        if (!IsActiveTransactionCompareStage)
        {
            return "The active transaction is not a Compare task.";
        }

        if (isCompareWorkspaceOpen())
        {
            return "Compare workspace is already open.";
        }

        return "Compare workspace launcher is not configured.";
    }

    private void NotifySelectionDetails()
    {
        NotifyPropertyChanged(nameof(HasSelectedRow));
        NotifyPropertyChanged(nameof(SelectedTransactionNumberText));
        NotifyPropertyChanged(nameof(SelectedTransactionNumberValue));
        NotifyPropertyChanged(nameof(SelectedTaskText));
        NotifyPropertyChanged(nameof(SelectedTaskValue));
        NotifyPropertyChanged(nameof(SelectedTransactionTypeText));
        NotifyPropertyChanged(nameof(SelectedTransactionTypeValue));
        NotifyPropertyChanged(nameof(SelectedApplicantText));
        NotifyPropertyChanged(nameof(SelectedApplicantValue));
        NotifyPropertyChanged(nameof(SelectedOwnerText));
        NotifyPropertyChanged(nameof(SelectedOwnerValue));
        NotifyPropertyChanged(nameof(SelectedSurveyorText));
        NotifyPropertyChanged(nameof(SelectedParishText));
        NotifyPropertyChanged(nameof(SelectedReceivedText));
        NotifyPropertyChanged(nameof(SelectedAssignmentText));
        NotifyPropertyChanged(nameof(SelectedStatusText));
        NotifyPropertyChanged(nameof(SelectedStatusValue));
        NotifyPropertyChanged(nameof(SelectedReadinessText));
    }

    private void NotifyToolbarTooltips()
    {
        NotifyPropertyChanged(nameof(RefreshTooltip));
        NotifyPropertyChanged(nameof(StartTransactionTooltip));
        NotifyPropertyChanged(nameof(StopTaskTooltip));
        NotifyPropertyChanged(nameof(ViewDocumentsTooltip));
        NotifyPropertyChanged(nameof(ShowSupportingDocumentsTooltip));
        NotifyPropertyChanged(nameof(OpenMapGeoreferenceTooltip));
        NotifyPropertyChanged(nameof(OpenTitlePlanImagePlacementTooltip));
        NotifyPropertyChanged(nameof(OpenPlaBTestInputTooltip));
        NotifyPropertyChanged(nameof(AddDocumentTooltip));
        NotifyPropertyChanged(nameof(CompleteTaskTooltip));
        NotifyPropertyChanged(nameof(ReopenCompareTooltip));
    }

    private void NotifyListState()
    {
        NotifyPropertyChanged(nameof(HasRows));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(CanRefresh));
        NotifyPropertyChanged(nameof(CanEditListCriteria));
        NotifyPropertyChanged(nameof(CanSearchTransactions));
        NotifyPropertyChanged(nameof(CanUseListControls));
        NotifyPropertyChanged(nameof(IsTransactionActive));
        NotifyPropertyChanged(nameof(IsTransactionPanelLocked));
        NotifyPropertyChanged(nameof(ActiveTransactionNumber));
        NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
        NotifyPropertyChanged(nameof(CanStartTransaction));
        NotifyPropertyChanged(nameof(CanStopTask));
        NotifyPropertyChanged(nameof(CanViewDocuments));
        NotifyPropertyChanged(nameof(CanShowSupportingDocuments));
        NotifyPropertyChanged(nameof(CanOpenMapGeoreference));
        NotifyPropertyChanged(nameof(CanOpenTitlePlanImagePlacement));
        NotifyPropertyChanged(nameof(CanOpenPlaBTestInput));
        NotifyPropertyChanged(nameof(CanAddDocument));
        NotifyPropertyChanged(nameof(CanCompleteTask));
        NotifyPropertyChanged(nameof(CanReopenCompare));
        NotifySelectionDetails();
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

        if (ShowSupportingDocumentsCommand is RelayCommand showSupportingDocuments)
        {
            showSupportingDocuments.RaiseCanExecuteChanged();
        }

        if (OpenMapGeoreferenceCommand is RelayCommand openMapGeoreference)
        {
            openMapGeoreference.RaiseCanExecuteChanged();
        }

        if (OpenTitlePlanImagePlacementCommand is RelayCommand openTitlePlanImagePlacement)
        {
            openTitlePlanImagePlacement.RaiseCanExecuteChanged();
        }

        if (OpenPlaBTestInputCommand is RelayCommand openPlaBTestInput)
        {
            openPlaBTestInput.RaiseCanExecuteChanged();
        }

        if (AddDocumentCommand is RelayCommand addDocument)
        {
            addDocument.RaiseCanExecuteChanged();
        }

        if (CompleteTaskCommand is RelayCommand complete)
        {
            complete.RaiseCanExecuteChanged();
        }

        if (ReopenCompareCommand is RelayCommand reopenCompare)
        {
            reopenCompare.RaiseCanExecuteChanged();
        }

        NotifyToolbarTooltips();
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

    private bool ValidateSupportedTransactionType(InnolaTransactionRow row)
    {
        var normalizedType = row.TransactionType?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedType) && supportedTransactionTypes.Contains(normalizedType))
        {
            return true;
        }

        if (PlaBPlanAnnexationTaskGate.Evaluate(row, plaBPlanAnnexationTaskSettings).IsEligible)
        {
            return true;
        }

        var visibleType = string.IsNullOrWhiteSpace(normalizedType) ? "(blank)" : normalizedType;
        var supported = supportedTransactionTypes.Count == 0
            ? "none configured"
            : string.Join(", ", supportedTransactionTypes.OrderBy(type => type, StringComparer.OrdinalIgnoreCase));
        var message = $"Transaction {row.TransactionNumber} cannot be opened because transaction type '{visibleType}' is not supported by Parcel Workflow [Compute]. Supported types: {supported}.";
        ErrorText = message;
        StatusText = message;
        RestoreSelectedRow(row.TransactionNumber);
        return false;
    }

    private bool ValidateWorkflowStage(InnolaTransactionRow row, out ParcelWorkflowStageRoute route)
    {
        route = ResolveWorkflowStageRoute(row);
        if (route != ParcelWorkflowStageRoute.Unsupported)
        {
            return true;
        }

        var normalizedStage = row.TaskName?.Trim();
        var visibleStage = string.IsNullOrWhiteSpace(normalizedStage) ? "(blank)" : normalizedStage;
        var supported = BuildSupportedWorkflowStageMessage();
        var workflowLabel = compareWorkflowStages.Count == 0 ? "Compute" : "Compute/Compare";
        var message = $"Transaction {row.TransactionNumber} cannot be opened because task '{visibleStage}' is not configured for Parcel Workflow [{workflowLabel}]. Supported tasks: {supported}.";
        ErrorText = message;
        StatusText = message;
        RestoreSelectedRow(row.TransactionNumber);
        return false;
    }

    private ParcelWorkflowStageRoute ResolveWorkflowStageRoute(InnolaTransactionRow row) =>
        PlaBPlanAnnexationTaskGate.Evaluate(row, plaBPlanAnnexationTaskSettings).IsEligible
            ? ParcelWorkflowStageRoute.PlaBPlanAnnexation
            : ParcelWorkflowStageRouter.Resolve(row.TaskName, computeWorkflowStages, compareWorkflowStages);

    private string BuildSupportedWorkflowStageMessage()
    {
        var supportedStages = computeWorkflowStages
            .Concat(compareWorkflowStages)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stage => stage, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return supportedStages.Length == 0
            ? "none configured"
            : string.Join(", ", supportedStages);
    }

    private sealed class TransactionPanelCompareTaskLifecycleService : ICompareTaskLifecycleService
    {
        private readonly TransactionPanelState owner;

        public TransactionPanelCompareTaskLifecycleService(TransactionPanelState owner)
        {
            this.owner = owner;
        }

        public Task<CompareTaskLifecycleResult> SuspendAsync(string transactionNumber, CancellationToken cancellationToken = default)
        {
            return owner.SuspendCurrentTransactionForCompareAsync(transactionNumber, cancellationToken);
        }

        public Task<CompareTaskLifecycleResult> CancelAsync(string transactionNumber, CancellationToken cancellationToken = default)
        {
            return owner.CancelCurrentTransactionForCompareAsync(transactionNumber, cancellationToken);
        }

        public Task<CompareTaskLifecycleResult> CompleteAsync(string transactionNumber, CancellationToken cancellationToken = default)
        {
            return owner.CompleteCurrentTransactionForCompareAsync(transactionNumber, cancellationToken);
        }
    }
}
