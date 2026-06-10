using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

public sealed class TransactionPanelState : INotifyPropertyChanged
{
    private readonly InnolaSessionManager session;
    private readonly IInnolaTransactionService transactionService;
    private readonly InnolaTransactionLoadService? transactionLoadService;
    private readonly Func<DateTimeOffset> clock;
    private readonly List<InnolaTransactionRow> allRows = new();
    private string selectedFilter = "All tasks";
    private string searchText = string.Empty;
    private string sortField = "Transaction no";
    private string sortDirection = "Ascending";
    private InnolaTransactionRow? selectedRow;
    private bool isLoading;
    private string statusText = "Not logged in.";
    private string? errorText;

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        Func<DateTimeOffset>? clock)
        : this(session, transactionService, processStep, null, clock)
    {
    }

    public TransactionPanelState(
        InnolaSessionManager session,
        IInnolaTransactionService transactionService,
        string processStep,
        InnolaTransactionLoadService? transactionLoadService = null,
        Func<DateTimeOffset>? clock = null)
    {
        this.session = session;
        this.transactionService = transactionService;
        this.transactionLoadService = transactionLoadService;
        ProcessStep = string.IsNullOrWhiteSpace(processStep) ? "parcel_workflow" : processStep;
        this.clock = clock ?? (() => DateTimeOffset.Now);

        Rows = new ObservableCollection<InnolaTransactionRow>();
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => CanRefresh);
        LoadSelectedCommand = new RelayCommand(async () => await LoadSelectedTransactionAsync(), () => CanLoadSelectedTransaction);
        session.SessionChanged += (_, _) => HandleSessionChanged();
        RefreshSessionState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<InnolaTransactionRow> Rows { get; }

    public IReadOnlyList<string> Filters { get; } = new[] { "All tasks", "My tasks", "Group tasks" };

    public IReadOnlyList<string> SortFields { get; } = new[] { "Transaction no", "Task name", "Received", "Status" };

    public IReadOnlyList<string> SortDirections { get; } = new[] { "Ascending", "Descending" };

    public ICommand RefreshCommand { get; }

    public ICommand LoadSelectedCommand { get; }

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

    public bool CanRefresh => IsLoggedIn && !IsLoading;

    public bool CanUseListControls => IsLoggedIn && !IsLoading && allRows.Count > 0;

    public bool HasRows => Rows.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsEmpty => IsLoggedIn && !IsLoading && !HasError && Rows.Count == 0;

    public string? LoadedCaseFolderPath => session.LoadedCaseFolderPath;

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
            var normalized = string.IsNullOrWhiteSpace(value) ? "Transaction no" : value;
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
            var normalized = string.IsNullOrWhiteSpace(value) ? "Ascending" : value;
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

    public bool CanLoadSelectedTransaction => IsLoggedIn && !IsLoading && SelectedRow is { IsLoadable: true };

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLoggedIn || session.CurrentSession is null)
        {
            allRows.Clear();
            Rows.Clear();
            SelectedRow = null;
            ErrorText = null;
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
            var result = await transactionService.GetAvailableTransactionsAsync(new InnolaTransactionQuery(
                currentSession.ServerUrl,
                currentSession.AccessToken,
                currentSession.User.Username,
                currentSession.User.Groups,
                ProcessStep,
                SelectedFilter,
                SearchText,
                SortField,
                SortDirection), cancellationToken);

            if (!result.Success)
            {
                allRows.Clear();
                Rows.Clear();
                SelectedRow = null;
                ErrorText = result.ErrorMessage ?? "Could not refresh transactions. Try again.";
                StatusText = "Could not refresh transactions. Try again.";
                return;
            }

            var previousTransactionNumber = SelectedRow?.TransactionNumber;
            allRows.Clear();
            allRows.AddRange(result.Rows);
            ApplyView(previousTransactionNumber);
            StatusText = Rows.Count == 0
                ? "No available transactions for this step."
                : $"{Rows.Count} available transaction{(Rows.Count == 1 ? string.Empty : "s")}.";
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
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

        session.SelectTransaction(SelectedRow, clock());
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
                ErrorText = result.ErrorMessage ?? "Could not load transaction. Try again.";
                StatusText = ErrorText;
                return;
            }

            StatusText = result.StatusMessage ?? $"Loaded transaction: {SelectedRow.TransactionNumber}.";
            NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        }
        finally
        {
            IsLoading = false;
            NotifyListState();
        }
    }

    private void HandleSessionChanged()
    {
        RefreshSessionState();
        if (!session.IsLoggedIn)
        {
            allRows.Clear();
            Rows.Clear();
            SelectedRow = null;
            ErrorText = null;
            StatusText = "Not logged in.";
            NotifyListState();
        }
    }

    private void RefreshSessionState()
    {
        NotifyPropertyChanged(nameof(IsLoggedIn));
        NotifyPropertyChanged(nameof(CanRefresh));
        NotifyPropertyChanged(nameof(CanUseListControls));
        NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
        NotifyPropertyChanged(nameof(LoadedCaseFolderPath));
        NotifyCommandStates();
    }

    private void ApplyView(string? previousTransactionNumber = null)
    {
        var filtered = ApplyFilter(allRows);
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
        NotifyPropertyChanged(nameof(CanLoadSelectedTransaction));
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
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static bool Contains(string? value, string query)
    {
        return value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
