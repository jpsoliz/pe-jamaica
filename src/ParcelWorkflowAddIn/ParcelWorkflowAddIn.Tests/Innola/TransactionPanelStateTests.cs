using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class TransactionPanelStateTests
{
    public static async Task LoggedOutPanelDoesNotCallTransactionService()
    {
        var service = new FakeTransactionService();
        var manager = new InnolaSessionManager(new FakeAuthService());
        var panel = new TransactionPanelState(manager, service, "parcel_workflow");

        await panel.RefreshAsync();

        TestAssert.Equal(0, service.CallCount, "Logged-out refresh should not call transaction service.");
        TestAssert.Equal("Not logged in.", panel.StatusText, "Logged-out status mismatch.");
        TestAssert.True(!panel.CanRefresh, "Refresh should be disabled while logged out.");
        TestAssert.True(!panel.CanLoadSelectedTransaction, "Load should be disabled while logged out.");
        TestAssert.Equal(0, panel.Rows.Count, "Logged-out panel should not show rows.");
    }

    public static async Task LoggedInRefreshUsesSessionQueryAndShowsRows()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-2", "TR100000005", "Prepare Rejection Letter", "Group One", "2024-10-15T09:38:00-05:00"),
                Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00")
            })
        };
        var manager = LoggedInManager();
        var panel = new TransactionPanelState(manager, service, "parcel_workflow");

        await panel.RefreshAsync();

        TestAssert.Equal(1, service.CallCount, "Refresh should call transaction service once.");
        TestAssert.Equal("tester", service.LastQuery?.Username, "Query user mismatch.");
        TestAssert.Equal("parcel_workflow", service.LastQuery?.ProcessStep, "Query process step mismatch.");
        TestAssert.True(service.LastQuery!.Groups.Contains("survey"), "Query should include user groups.");
        TestAssert.Equal(2, panel.Rows.Count, "Panel row count mismatch.");
        TestAssert.Equal("TR100000004", panel.Rows[0].TransactionNumber, "Default sort should be transaction number ascending.");
        TestAssert.Equal("2 available transactions.", panel.StatusText, "Refresh status mismatch.");
    }

    public static async Task SearchSortAndSelectionUpdatePanelState()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-2", "TR100000009", "QC of Registration Cases", "qc", "2024-10-15T09:53:00-05:00")
            })
        };
        var manager = LoggedInManager();
        var panel = new TransactionPanelState(manager, service, "parcel_workflow", () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));

        await panel.RefreshAsync();
        panel.SearchText = "QC";

        TestAssert.Equal(1, panel.Rows.Count, "Search should filter rows.");
        TestAssert.Equal("TR100000009", panel.Rows[0].TransactionNumber, "Search result mismatch.");

        panel.SearchText = string.Empty;
        panel.SortField = "Received";
        panel.SortDirection = "Descending";

        TestAssert.Equal("TR100000009", panel.Rows[0].TransactionNumber, "Sort descending by received date mismatch.");

        panel.SelectedRow = panel.Rows[0];
        TestAssert.True(panel.CanLoadSelectedTransaction, "Selected loadable row should enable load.");
        panel.LoadSelectedTransaction();

        TestAssert.Equal("TR100000009", manager.SelectedTransaction?.TransactionNumber, "Selected transaction state mismatch.");
        TestAssert.True(!manager.IsTransactionLoaded, "Selecting a transaction must not mark parcel workflow loaded.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled after selection.");
        TestAssert.Equal("Selected transaction: TR100000009.", panel.StatusText, "Selection status mismatch.");
    }

    public static async Task LoadActionLoadsTransactionAndEnablesParcelWorkflow()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var loader = new InnolaTransactionLoadService(
            manager,
            new MockInnolaTransactionDetailService(),
            new CaseFolderStore(clock, () => "run-panel-load"),
            new AttachmentSourceFileWriter(clock),
            new SourceInputProfileDetector(clock),
            () => tempRoot.Path,
            clock);
        var panel = new TransactionPanelState(manager, service, "parcel_workflow", loader, clock);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.LoadSelectedTransactionAsync();

        TestAssert.True(manager.CanOpenParcelWorkflow, "Panel load should enable Parcel Workflow after validation.");
        TestAssert.True(Directory.Exists(manager.LoadedCaseFolderPath!), "Panel load should create a Case Folder.");
        TestAssert.True(panel.StatusText.Contains("Loaded TR100000004", StringComparison.OrdinalIgnoreCase), "Panel load status should confirm loaded transaction.");
        TestAssert.Equal(manager.LoadedCaseFolderPath, panel.LoadedCaseFolderPath, "Panel should expose loaded Case Folder path.");
    }

    public static async Task FailedRefreshShowsRetryableRedactedError()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Failure("token secret-password { payload } at Stack.Trace", "bad-response")
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();

        TestAssert.Equal("Could not refresh transactions. Try again.", panel.ErrorText, "Error text mismatch.");
        TestAssert.Equal("Could not refresh transactions. Try again.", panel.StatusText, "Status text mismatch.");
        TestAssert.True(!panel.ErrorText!.Contains("secret-password", StringComparison.Ordinal), "Password must not leak to error text.");
        TestAssert.True(!panel.ErrorText.Contains("token", StringComparison.OrdinalIgnoreCase), "Token must not leak to error text.");
        TestAssert.Equal(0, panel.Rows.Count, "Failed refresh should not show stale rows in this story.");
    }

    public static async Task LoadingRefreshDisablesListControls()
    {
        var service = new DelayedTransactionService();
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        var refreshTask = panel.RefreshAsync();
        await service.RequestStarted.Task;

        TestAssert.True(panel.IsLoading, "Panel should be loading while refresh awaits service.");
        TestAssert.True(!panel.CanRefresh, "Refresh should be disabled while loading.");
        TestAssert.True(!panel.CanUseListControls, "Filter/search/sort controls should be disabled while loading.");
        TestAssert.True(!panel.CanLoadSelectedTransaction, "Load should be disabled while loading.");

        service.Complete();
        await refreshTask;

        TestAssert.True(!panel.IsLoading, "Panel should leave loading state after refresh completes.");
        TestAssert.True(panel.CanUseListControls, "List controls should re-enable after rows load.");
    }

    public static async Task LogoutClearsSelectedTransactionRowsAndKeepsParcelWorkflowDisabled()
    {
        var manager = LoggedInManager();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") })
        };
        var panel = new TransactionPanelState(manager, service, "parcel_workflow");

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        panel.LoadSelectedTransaction();
        await manager.LogoutAsync();

        TestAssert.Equal(null, manager.SelectedTransaction, "Logout should clear selected transaction.");
        TestAssert.True(!manager.CanOpenTransactionPanel, "Transaction panel should be gated after logout.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled after logout.");
        TestAssert.Equal(0, panel.Rows.Count, "Logout should clear panel rows.");
        TestAssert.Equal("Not logged in.", panel.StatusText, "Logout panel status mismatch.");
    }

    private static InnolaSessionManager LoggedInManager()
    {
        var manager = new InnolaSessionManager(new FakeAuthService());
        manager.ApplySuccessfulSession(new InnolaSession(
            InnolaSessionStatus.LoggedIn,
            "https://eltrs.innola-solutions.com/",
            "tester",
            "secret-password",
            "token-abc",
            new InnolaUserContext("tester", "Test User", new[] { "survey", "qc" }, Array.Empty<string>()),
            null));
        return manager;
    }

    private static InnolaTransactionRow Row(string taskId, string transactionNumber, string taskName, string assignedGroup, string receivedAt)
    {
        return new InnolaTransactionRow(
            taskId,
            transactionNumber.TrimStart('T', 'R'),
            transactionNumber,
            taskName,
            "parcel_workflow",
            InnolaTransactionStatus.Available,
            "John Johnson",
            assignedGroup == "tester" ? "tester" : null,
            assignedGroup,
            DateTimeOffset.Parse(receivedAt),
            true,
            true,
            null,
            null);
    }

    private sealed class FakeTransactionService : IInnolaTransactionService
    {
        public int CallCount { get; private set; }

        public InnolaTransactionQuery? LastQuery { get; private set; }

        public InnolaTransactionListResult Result { get; set; } = InnolaTransactionListResult.Succeeded(Array.Empty<InnolaTransactionRow>());

        public Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastQuery = query;
            return Task.FromResult(Result);
        }
    }

    private sealed class DelayedTransactionService : IInnolaTransactionService
    {
        private readonly TaskCompletionSource<InnolaTransactionListResult> completion = new();

        public TaskCompletionSource<bool> RequestStarted { get; } = new();

        public Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
        {
            RequestStarted.TrySetResult(true);
            return completion.Task;
        }

        public void Complete()
        {
            completion.TrySetResult(InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00")
            }));
        }
    }

    private sealed class FakeAuthService : IInnolaAuthService
    {
        public InnolaSession? CurrentSession { get; private set; }

        public Task<InnolaLoginResult> LoginAsync(string serverUrl, string username, string password, CancellationToken cancellationToken = default)
        {
            CurrentSession = new InnolaSession(
                InnolaSessionStatus.LoggedIn,
                serverUrl,
                username,
                password,
                "token-abc",
                new InnolaUserContext(username, username, Array.Empty<string>(), Array.Empty<string>()),
                null);
            return Task.FromResult(InnolaLoginResult.Succeeded(CurrentSession));
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            CurrentSession = null;
            return Task.CompletedTask;
        }
    }
}
