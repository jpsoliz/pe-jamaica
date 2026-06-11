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
        TestAssert.Equal("User: not logged in", panel.ConnectionUserText, "Logged-out user footer mismatch.");
        TestAssert.Equal("Server: not connected", panel.ConnectionServerText, "Logged-out server footer mismatch.");
        TestAssert.True(panel.ConnectionModeText.StartsWith("Mode: ", StringComparison.Ordinal), "Logged-out mode footer mismatch.");
        TestAssert.Equal("Records retrieved: not refreshed", panel.RetrievedRecordCountText, "Logged-out count footer mismatch.");
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
        TestAssert.Equal("TR100000005", panel.Rows[0].TransactionNumber, "Default sort should show newest received transactions first.");
        TestAssert.Equal("2 available transactions.", panel.StatusText, "Refresh status mismatch.");
        TestAssert.Equal("User: Test User", panel.ConnectionUserText, "Logged-in user footer mismatch.");
        TestAssert.Equal("Server: https://eltrs.innola-solutions.com/", panel.ConnectionServerText, "Logged-in server footer mismatch.");
        TestAssert.True(panel.ConnectionModeText.StartsWith("Mode: ", StringComparison.Ordinal), "Logged-in mode footer mismatch.");
        TestAssert.Equal("Records retrieved: 2", panel.RetrievedRecordCountText, "Refresh count footer mismatch.");
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

    public static async Task LoadActionLoadsTransactionAndKeepsParcelWorkflowDisabledUntilStart()
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
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            loader,
            LifecycleCoordinator(manager, clock),
            null,
            clock);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.LoadSelectedTransactionAsync();

        TestAssert.True(!manager.CanOpenParcelWorkflow, "Panel load should keep Parcel Workflow disabled until Start claims the task.");
        TestAssert.True(Directory.Exists(manager.LoadedCaseFolderPath!), "Panel load should create a Case Folder.");
        TestAssert.True(panel.StatusText.Contains("Loaded TR100000004", StringComparison.OrdinalIgnoreCase), "Panel load status should confirm loaded transaction.");
        TestAssert.Equal(manager.LoadedCaseFolderPath, panel.LoadedCaseFolderPath, "Panel should expose loaded Case Folder path.");
        TestAssert.True(panel.CanStartTransaction, "Loaded selected transaction should be ready to start.");
        TestAssert.True(!panel.CanStopTask, "Stop should remain disabled before start.");
        TestAssert.True(!panel.CanCompleteTask, "Complete should remain disabled before start.");
    }

    public static async Task StartActionLoadsAndClaimsTransaction()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock),
            null,
            clock);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.True(manager.CanOpenParcelWorkflow, "Start should leave Parcel Workflow enabled.");
        TestAssert.Equal(InnolaTransactionLifecycleStatus.InProgress, manager.LifecycleStatus, "Start should claim the transaction.");
        TestAssert.Equal("tester", manager.LifecycleOwnerUser, "Claimed owner mismatch.");
        TestAssert.True(panel.IsTransactionPanelLocked, "Transaction list should lock while the selected task is active.");
        TestAssert.Equal("TR100000004", panel.ActiveTransactionNumber, "Active transaction number mismatch.");
        TestAssert.True(!panel.CanUseListControls, "Filter/search/sort controls should lock after start.");
        TestAssert.True(!panel.CanRefresh, "Refresh should be disabled while active transaction is in progress.");
        TestAssert.True(!panel.CanStartTransaction, "Start should be disabled after the task is in progress.");
        TestAssert.True(panel.CanStopTask, "Stop should be enabled after start.");
        TestAssert.True(panel.CanCompleteTask, "Complete should be enabled after start.");
        TestAssert.True(panel.CanViewDocuments, "Documents should be enabled after load/start.");
    }

    public static async Task ActiveTransactionLocksSelectionSearchSortAndRefresh()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-100000005", "TR100000005", "Prepare Rejection Letter", "tester", "2024-10-15T09:38:00-05:00")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock),
            null,
            clock);

        await panel.RefreshAsync();
        panel.SelectedRow = FindRow(panel, "TR100000004");
        await panel.StartSelectedTransactionAsync();
        var callsAfterStart = service.CallCount;

        panel.SelectedRow = FindRow(panel, "TR100000005");
        panel.SearchText = "Prepare";
        panel.SortField = "Received";
        panel.SortDirection = "Descending";
        await panel.RefreshAsync();

        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Locked panel should keep active row selected.");
        TestAssert.Equal(string.Empty, panel.SearchText, "Search should not change while active transaction is locked.");
        TestAssert.Equal("Received", panel.SortField, "Sort field should not change while locked.");
        TestAssert.Equal("Descending", panel.SortDirection, "Sort direction should not change while locked.");
        TestAssert.Equal(callsAfterStart, service.CallCount, "Refresh should not call transaction service while active transaction is locked.");
    }

    public static async Task StopActionSavesProgressWithoutCompleting()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock),
            null,
            clock);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();
        await panel.SaveCurrentTransactionAsync();

        TestAssert.Equal(InnolaTransactionLifecycleStatus.None, manager.LifecycleStatus, "Explicit Stop/Save should release the active UI state after saving progress.");
        TestAssert.True(!manager.IsTransactionLoaded, "Stop should close the active Parcel Workflow session.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Stop should disable Parcel Workflow.");
        TestAssert.True(!panel.IsTransactionPanelLocked, "Stop should unlock the transaction panel.");
        TestAssert.True(panel.CanUseListControls, "Stop should restore filter/search/sort controls.");
        TestAssert.True(panel.CanRefresh, "Stop should allow the list to refresh again.");
        TestAssert.True(!panel.CanCompleteTask, "Complete should be disabled after stopping the active task.");
        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Stopped transaction row should remain selected for context.");
        TestAssert.Equal("TR100000004", panel.SavedTransactionNumber, "Stopped transaction should remain marked as saved/in progress in the panel.");
    }

    public static async Task CompleteSuccessSuppressesCompletedTransactionFromStaleRefresh()
    {
        using var tempRoot = new TempDirectory();
        var staleRow = Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00");
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { staleRow })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock, new AlwaysReadyCompletionReadinessService()),
            null,
            clock);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();
        await panel.CompleteCurrentTransactionAsync();

        TestAssert.True(!manager.CanOpenParcelWorkflow, "Complete should disable Parcel Workflow.");
        TestAssert.True(!panel.IsTransactionPanelLocked, "Complete should unlock the panel.");
        TestAssert.Equal(0, panel.Rows.Count, "Completed transaction should not remain visible even when refresh returns stale available rows.");
        TestAssert.Equal(null, panel.SelectedRow, "Complete should clear row selection.");
    }

    public static async Task FailedLoadPreservesPreviouslyLoadedTransaction()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-100000005", "TR100000005", "Prepare Rejection Letter", "tester", "2024-10-15T09:38:00-05:00")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var loader = new InnolaTransactionLoadService(
            manager,
            new FirstOnlyDetailService(),
            new CaseFolderStore(clock, () => "run-panel-load"),
            new AttachmentSourceFileWriter(clock),
            new SourceInputProfileDetector(clock),
            () => tempRoot.Path,
            clock);
        var panel = new TransactionPanelState(manager, service, "parcel_workflow", loader, clock);

        await panel.RefreshAsync();
        panel.SelectedRow = FindRow(panel, "TR100000004");
        await panel.LoadSelectedTransactionAsync();
        var firstLoadedPath = manager.LoadedCaseFolderPath;

        panel.SelectedRow = FindRow(panel, "TR100000005");
        await panel.LoadSelectedTransactionAsync();

        TestAssert.True(!manager.CanOpenParcelWorkflow, "Loaded but unclaimed workflow should remain disabled after failed new load.");
        TestAssert.Equal("TR100000004", manager.LoadedTransactionNumber, "Failed load should preserve previous loaded transaction number.");
        TestAssert.Equal(firstLoadedPath, manager.LoadedCaseFolderPath, "Failed load should preserve previous Case Folder path.");
        TestAssert.Equal("TR100000004", manager.SelectedTransaction?.TransactionNumber, "Failed load should restore previous selected transaction.");
        TestAssert.True(panel.ErrorText is not null, "Failed load should show an error.");
    }

    public static async Task ActiveTransactionStayDecisionPreventsReplacement()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-100000005", "TR100000005", "Prepare Rejection Letter", "tester", "2024-10-15T09:38:00-05:00")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var loader = Loader(manager, tempRoot.Path, clock);
        var coordinator = LifecycleCoordinator(manager, clock);
        var panel = new TransactionPanelState(manager, service, "parcel_workflow", loader, coordinator, new FixedDecisionProvider(ActiveTransactionSwitchDecision.StayOnCurrentTransaction), clock);

        await panel.RefreshAsync();
        panel.SelectedRow = FindRow(panel, "TR100000004");
        await panel.LoadSelectedTransactionAsync();
        await coordinator.StartOrClaimAsync();

        panel.SelectedRow = FindRow(panel, "TR100000005");
        await panel.LoadSelectedTransactionAsync();

        TestAssert.Equal("TR100000004", manager.LoadedTransactionNumber, "Stay decision should preserve current loaded transaction.");
        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Stay decision should restore active row selection.");
    }

    public static async Task FailedRefreshShowsRetryableRedactedErrorAndPreservesRows()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") })
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        service.Result = InnolaTransactionListResult.Failure("token secret-password { payload } at Stack.Trace", "bad-response");

        await panel.RefreshAsync();

        TestAssert.Equal("Could not refresh transactions. Try again.", panel.ErrorText, "Error text mismatch.");
        TestAssert.Equal("Could not refresh transactions. Try again.", panel.StatusText, "Status text mismatch.");
        TestAssert.True(!panel.ErrorText!.Contains("secret-password", StringComparison.Ordinal), "Password must not leak to error text.");
        TestAssert.True(!panel.ErrorText.Contains("token", StringComparison.OrdinalIgnoreCase), "Token must not leak to error text.");
        TestAssert.Equal(1, panel.Rows.Count, "Failed refresh should preserve previous valid rows.");
        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Failed refresh should preserve selected row.");
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
        TestAssert.Equal("User: not logged in", panel.ConnectionUserText, "Logout user footer mismatch.");
        TestAssert.Equal("Server: not connected", panel.ConnectionServerText, "Logout server footer mismatch.");
        TestAssert.True(panel.ConnectionModeText.StartsWith("Mode: ", StringComparison.Ordinal), "Logout mode footer mismatch.");
        TestAssert.Equal("Records retrieved: not refreshed", panel.RetrievedRecordCountText, "Logout count footer mismatch.");
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

    private static InnolaTransactionLoadService Loader(
        InnolaSessionManager manager,
        string outputRoot,
        Func<DateTimeOffset> clock)
    {
        return new InnolaTransactionLoadService(
            manager,
            new MockInnolaTransactionDetailService(),
            new CaseFolderStore(clock, () => "run-panel-load"),
            new AttachmentSourceFileWriter(clock),
            new SourceInputProfileDetector(clock),
            () => outputRoot,
            clock);
    }

    private static InnolaTransactionLifecycleCoordinator LifecycleCoordinator(
        InnolaSessionManager manager,
        Func<DateTimeOffset> clock,
        ITransactionCompletionReadinessService? readinessService = null)
    {
        return new InnolaTransactionLifecycleCoordinator(
            manager,
            new MockInnolaTransactionLifecycleService(),
            readinessService ?? new DefaultTransactionCompletionReadinessService(),
            new WorkflowLifecycleAuditService(clock),
            clock);
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

    private static InnolaTransactionRow FindRow(TransactionPanelState panel, string transactionNumber)
    {
        return panel.Rows.First(row => row.TransactionNumber.Equals(transactionNumber, StringComparison.OrdinalIgnoreCase));
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

    private sealed class FixedDecisionProvider : IActiveTransactionSwitchDecisionProvider
    {
        private readonly ActiveTransactionSwitchDecision decision;

        public FixedDecisionProvider(ActiveTransactionSwitchDecision decision)
        {
            this.decision = decision;
        }

        public ActiveTransactionSwitchDecision Decide(SelectedInnolaTransaction activeTransaction, InnolaTransactionRow requestedTransaction)
        {
            return decision;
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

    private sealed class FirstOnlyDetailService : IInnolaTransactionDetailService
    {
        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            if (!selectedTransaction.TaskId.Equals("task-100000004", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(InnolaTransactionDetailResult.Failure("Attachment service unavailable.", "unavailable"));
            }

            return Task.FromResult(InnolaTransactionDetailResult.Succeeded(new InnolaTransactionDetail(
                "100000004",
                "TR100000004",
                "task-100000004",
                "Computation Check",
                "parcel_workflow",
                "parcel_workflow",
                "scenario_a",
                "tester",
                "survey",
                null,
                "available",
                new[]
                {
                    new InnolaAttachmentMetadata("att-computation", "computation.pdf", ".pdf", "application/pdf", SourceRole.ComputationSource, "computation", 4, null, "mock-attachment:att-computation", true),
                    new InnolaAttachmentMetadata("att-plan", "plan.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 4, null, "mock-attachment:att-plan", true)
                })));
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaAttachmentContentResult.Succeeded(new byte[] { 1, 2, 3, 4 }));
        }
    }

    private sealed class AlwaysReadyCompletionReadinessService : ITransactionCompletionReadinessService
    {
        public TransactionCompletionReadinessResult CheckReadiness(string caseFolderPath)
        {
            return TransactionCompletionReadinessResult.Ready();
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
