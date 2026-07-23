using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.WorkflowRules;

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
                Row("task-2", "TR100000005", "Compute Survey Plan", "Group One", "2024-10-15T09:38:00-05:00"),
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

    public static async Task SearchRemainsEnabledWhenRefreshReturnsNoRows()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(Array.Empty<InnolaTransactionRow>())
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();

        TestAssert.Equal(0, panel.Rows.Count, "Empty refresh should leave no visible rows.");
        TestAssert.True(panel.CanSearchTransactions, "Search should remain enabled so the user can correct or broaden the search.");
        TestAssert.True(panel.CanEditListCriteria, "Filter and sort criteria should remain editable after an empty refresh.");
        TestAssert.True(!panel.CanUseListControls, "Row interaction should remain disabled when there are no rows.");
    }

    public static async Task SearchSortAndSelectionUpdatePanelState()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-2", "TR100000009", "Compute Survey Plan", "qc", "2024-10-15T09:53:00-05:00")
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

    public static async Task MyTasksFilterMatchesLoggedInUserOnly()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "survey", "2024-10-15T09:24:00-05:00") with { AssignedUser = "tester" },
                Row("task-2", "TR100000005", "Compute Survey Plan", "survey", "2024-10-15T09:38:00-05:00") with { AssignedUser = "tester2" },
                Row("task-3", "TR100000006", "Compute Survey Plan", "survey", "2024-10-15T09:53:00-05:00") with { AssignedUser = "Test User (tester)" },
                Row("task-4", "TR100000007", "Compute Survey Plan", "survey", "2024-10-15T10:08:00-05:00") with { AssignedUser = "Test User" },
                Row("task-5", "TR100000008", "Compute Survey Plan", "survey", "2024-10-15T10:23:00-05:00") with { AssignedUser = null }
            })
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();
        panel.SelectedFilter = "My tasks";

        TestAssert.Equal(3, panel.Rows.Count, "My tasks should only show rows assigned to the logged-in user.");
        TestAssert.True(panel.Rows.Any(row => row.TransactionNumber == "TR100000004"), "Exact assigned user should match.");
        TestAssert.True(panel.Rows.Any(row => row.TransactionNumber == "TR100000006"), "Display text containing the username token should match.");
        TestAssert.True(panel.Rows.Any(row => row.TransactionNumber == "TR100000007"), "Display-name-only assignee should match the logged-in user.");
        TestAssert.True(!panel.Rows.Any(row => row.TransactionNumber == "TR100000005"), "Substring user names should not match.");
    }

    public static async Task GroupTasksFilterMatchesLoggedInGroupsOnly()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "ROLE_Survey", "2024-10-15T09:24:00-05:00"),
                Row("task-2", "TR100000005", "Compute Survey Plan", "finance", "2024-10-15T09:38:00-05:00"),
                Row("task-3", "TR100000006", "Compute Survey Plan", "qc", "2024-10-15T09:53:00-05:00"),
                Row("task-4", "TR100000007", "Compute Survey Plan", "", "2024-10-15T10:08:00-05:00")
            })
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();
        panel.SelectedFilter = "Group tasks";

        TestAssert.Equal(2, panel.Rows.Count, "Group tasks should only show rows assigned to one of the logged-in user's groups.");
        TestAssert.True(panel.Rows.Any(row => row.TransactionNumber == "TR100000004"), "ROLE_ prefixed group should match the user's survey group.");
        TestAssert.True(panel.Rows.Any(row => row.TransactionNumber == "TR100000006"), "Direct group should match the user's qc group.");
        TestAssert.True(!panel.Rows.Any(row => row.TransactionNumber == "TR100000005"), "Unrelated groups should not match.");
    }

    public static async Task SearchTextRefreshesFromServerForMissingTransactionNumber()
    {
        var previousDelay = TransactionPanelState.SearchRefreshDelay;
        TransactionPanelState.SearchRefreshDelay = TimeSpan.Zero;
        try
        {
            var service = new SearchAwareTransactionService();
            var manager = LoggedInManager();
            var panel = new TransactionPanelState(manager, service, "parcel_workflow", () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));

            await panel.RefreshAsync();
            TestAssert.Equal(1, panel.Rows.Count, "Initial list should only contain the first page row.");
            TestAssert.Equal("TR100000004", panel.Rows[0].TransactionNumber, "Initial row mismatch.");

            panel.SearchText = "100000400";

            var completed = await Task.WhenAny(service.SearchObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            TestAssert.True(ReferenceEquals(completed, service.SearchObserved.Task), "Search text should trigger a server search.");
            await WaitForAsync(() => panel.Rows.Any(row => row.TransactionNumber == "TR100000400"));

            TestAssert.Equal("100000400", service.SearchObserved.Task.Result, "Server search query mismatch.");
            TestAssert.Equal("TR100000400", panel.Rows[0].TransactionNumber, "Remote search should surface the requested transaction.");
        }
        finally
        {
            TransactionPanelState.SearchRefreshDelay = previousDelay;
        }
    }

    public static async Task LoadSelectedTransactionClearsStaleSearchText()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000400", "TR100000400", "Computation Check", "tester", "2024-10-15T09:24:00-05:00")
            })
        };
        var manager = LoggedInManager();
        var panel = new TransactionPanelState(manager, service, "parcel_workflow", () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));

        await panel.RefreshAsync();
        panel.SearchText = "100000400";
        panel.SelectedRow = panel.Rows[0];
        await panel.LoadSelectedTransactionAsync();

        TestAssert.Equal(string.Empty, panel.SearchText, "Successful load should clear stale transaction search text.");
        TestAssert.Equal("TR100000400", panel.SelectedRow?.TransactionNumber, "Loaded transaction row should remain selected.");
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
        TestAssert.True(panel.StatusText.Contains("Opened new case", StringComparison.OrdinalIgnoreCase), "Panel load status should confirm opened case state.");
        TestAssert.Equal(manager.LoadedCaseFolderPath, panel.LoadedCaseFolderPath, "Panel should expose loaded Case Folder path.");
        TestAssert.True(panel.CanStartTransaction, "Loaded selected transaction should be ready to start.");
        TestAssert.True(!panel.CanStopTask, "Stop should remain disabled before start.");
        TestAssert.True(!panel.CanCompleteTask, "Complete should remain disabled before start.");
    }

    public static async Task UnsupportedTransactionTypeBlocksWorkflowLoadBeforeCaseFolderCreation()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00", "Survey Update")
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
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" });

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.LoadSelectedTransactionAsync();

        TestAssert.Equal(null, manager.SelectedTransaction, "Unsupported transaction should not become selected in session state.");
        TestAssert.True(!manager.IsTransactionLoaded, "Unsupported transaction should not load a case folder.");
        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Unsupported transaction row should remain selected.");
        TestAssert.Equal("Transaction TR100000004 cannot be opened because transaction type 'Survey Update' is not supported by Parcel Workflow [Compute]. Supported types: Cadastral Plan Examination, Plan Examination.", panel.StatusText, "Unsupported transaction status mismatch.");
        TestAssert.Equal(panel.StatusText, panel.ErrorText, "Unsupported transaction should surface a matching blocking error.");
    }

    public static async Task UnsupportedWorkflowStageBlocksComputeWorkflowLaunch()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Compare Survey Plan", "tester", "2024-10-15T09:24:00-05:00", "Plan Examination")
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
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" },
            compareWorkflowStages: Array.Empty<string>());

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.LoadSelectedTransactionAsync();

        TestAssert.Equal(null, manager.SelectedTransaction, "Unsupported workflow stage should not become selected in session state.");
        TestAssert.True(!manager.IsTransactionLoaded, "Unsupported workflow stage should not load a case folder.");
        TestAssert.Equal("Transaction TR100000004 cannot be opened because task 'Compare Survey Plan' is not configured for Parcel Workflow [Compute]. Supported tasks: Assign Computation Task, Computation Check, Compute Survey Plan.", panel.StatusText, "Unsupported workflow stage status mismatch.");
        TestAssert.Equal(panel.StatusText, panel.ErrorText, "Unsupported workflow stage should surface a matching blocking error.");
    }

    public static async Task CompareWorkflowStageLoadsSelectedTransaction()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Compare", "tester", "2024-10-15T09:24:00-05:00", "Plan Examination")
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
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" },
            compareWorkflowStages: new[] { "Compare", "Compare Survey Plan" });

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.LoadSelectedTransactionAsync();

        TestAssert.Equal("TR100000004", manager.SelectedTransaction?.TransactionNumber, "Compare workflow stage should become selected in session state.");
        TestAssert.True(manager.IsTransactionLoaded, "Compare workflow stage should load a case folder.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Compare load should not enable workflow actions before start.");
        TestAssert.True(panel.CanStartTransaction, "Compare workflow stage should be startable after load.");
    }

    public static async Task CompareWorkflowStageStartsAndLaunchesCompareWorkspace()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Compare", "tester", "2024-10-15T09:24:00-05:00", "Plan Examination")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var launchedTransactions = new List<string>();
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock),
            null,
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" },
            compareWorkflowStages: new[] { "Compare", "Compare Survey Plan" },
            compareWorkspaceLauncher: transactionNumber => launchedTransactions.Add(transactionNumber));

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(InnolaTransactionLifecycleStatus.InProgress, manager.LifecycleStatus, "Compare start should claim the transaction before launch.");
        TestAssert.Equal(1, launchedTransactions.Count, "Compare workspace should launch once.");
        TestAssert.Equal("TR100000004", launchedTransactions[0], "Compare workspace launch transaction mismatch.");
        TestAssert.True(manager.CanOpenParcelWorkflow, "Claimed Compare transaction should keep active transaction gates enabled.");
    }

    public static async Task ActiveCompareTaskCanReopenWithoutClaimingAgainAndSuspend()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Compare Survey Plan", "tester", "2024-10-15T09:24:00-05:00", "Plan Examination")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var lifecycleService = new CountingTransactionLifecycleService();
        var launchedTransactions = new List<string>();
        ICompareTaskLifecycleService? compareLifecycleBridge = null;
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock, lifecycleService: lifecycleService),
            null,
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" },
            compareWorkflowStages: new[] { "Compare", "Compare Survey Plan" },
            compareWorkspaceLifecycleLauncher: (transactionNumber, lifecycleBridge) =>
            {
                launchedTransactions.Add(transactionNumber);
                compareLifecycleBridge = lifecycleBridge;
            });

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(1, lifecycleService.ClaimCalls, "Initial Compare start should claim once.");
        TestAssert.Equal(1, launchedTransactions.Count, "Initial Compare start should launch once.");
        TestAssert.True(panel.CanReopenCompare, "Active Compare task should expose Reopen Compare.");

        await panel.ReopenCompareWorkspaceAsync();

        TestAssert.Equal(1, lifecycleService.ClaimCalls, "Reopen Compare must not claim/start the task again.");
        TestAssert.Equal(2, launchedTransactions.Count, "Reopen Compare should launch another Compare window instance.");
        TestAssert.True(compareLifecycleBridge is not null, "Compare launch should receive a lifecycle bridge.");

        var suspendResult = await compareLifecycleBridge!.SuspendAsync("TR100000004");

        TestAssert.True(suspendResult.Success, "Lifecycle bridge should suspend through the panel path.");
        TestAssert.Equal(1, lifecycleService.SaveProgressCalls, "Suspend should save progress through the existing lifecycle service.");
        TestAssert.False(panel.IsTransactionPanelLocked, "Suspend from Compare should unlock the transaction panel.");
        TestAssert.Equal("TR100000004", panel.SavedTransactionNumber, "Suspended Compare task should remain marked as saved for resume.");
    }

    public static void CompareWorkflowStageDoesNotResolveAsComputeWorkspace()
    {
        var computeStages = new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" };
        var compareStages = new[] { "Compare", "Compare Survey Plan" };

        var compareRoute = ParcelWorkflowStageRouter.Resolve("Compare Survey Plan", computeStages, compareStages);
        var computeRoute = ParcelWorkflowStageRouter.Resolve("Computation Check", computeStages, compareStages);

        TestAssert.Equal(ParcelWorkflowStageRoute.Compare, compareRoute, "Compare Survey Plan must route to Compare.");
        TestAssert.Equal(ParcelWorkflowStageRoute.Compute, computeRoute, "Computation Check must route to Compute.");
        TestAssert.True(!ParcelWorkflowStageRouter.IsComputeStage("Compare Survey Plan", computeStages, compareStages), "Compare stages must not enable the Compute workspace.");
    }

    public static async Task CompareWorkflowStageDoesNotLaunchWhenOwnershipStartFails()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Compare", "tester", "2024-10-15T09:24:00-05:00", "Plan Examination")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var launchedTransactions = new List<string>();
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock, lifecycleService: new FailingClaimLifecycleService()),
            null,
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" },
            compareWorkflowStages: new[] { "Compare", "Compare Survey Plan" },
            compareWorkspaceLauncher: transactionNumber => launchedTransactions.Add(transactionNumber));

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(0, launchedTransactions.Count, "Compare workspace must not launch when ownership/start fails.");
        TestAssert.Equal(InnolaTransactionLifecycleStatus.Loaded, manager.LifecycleStatus, "Failed Compare claim should preserve the loaded transaction state.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Failed Compare claim should not enable active workflow gates.");
        TestAssert.True(panel.StatusText.Contains("already in progress", StringComparison.OrdinalIgnoreCase), "Ownership failure should surface a retryable ownership message.");
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

    public static async Task ToolbarCommandsStaySynchronizedAcrossTransactionStates()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-100000005", "TR100000005", "Compare Survey Plan", "tester", "2024-10-15T09:38:00-05:00")
            })
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
            clock,
            supportedTransactionTypes: new[] { "Plan Examination", "Cadastral Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan", "Assign Computation Task", "Computation Check" },
            compareWorkflowStages: new[] { "Compare", "Compare Survey Plan" },
            compareWorkspaceLauncher: _ => { });

        await panel.RefreshAsync();
        AssertToolbarCommandState(panel, true, false, false, false, false, false, false, "without a selected row");

        panel.SelectedRow = FindRow(panel, "TR100000004");
        AssertToolbarCommandState(panel, true, true, false, false, false, false, false, "with a selected Compute row before load");

        await panel.LoadSelectedTransactionAsync();
        AssertToolbarCommandState(panel, true, true, false, false, true, true, false, "with a loaded but unclaimed Compute transaction");

        await panel.StartSelectedTransactionAsync();
        AssertToolbarCommandState(panel, false, false, false, true, true, true, true, "with an active Compute transaction");

        await panel.SaveCurrentTransactionAsync();
        AssertToolbarCommandState(panel, true, true, false, false, false, false, false, "after suspending the Compute transaction");

        panel.SelectedRow = FindRow(panel, "TR100000005");
        await panel.StartSelectedTransactionAsync();
        AssertToolbarCommandState(panel, false, false, true, true, true, true, true, "with an active Compare transaction");

        await panel.CompleteCurrentTransactionAsync();
        AssertToolbarCommandState(panel, true, false, false, false, false, false, false, "after completing the Compare transaction");
    }

    public static async Task AddDocumentsCopiesFilesIntoLoadedTransaction()
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
        var extraDocument = Path.Combine(tempRoot.Path, "extra-plan.pdf");
        File.WriteAllText(extraDocument, "%PDF-1.4 extra plan");

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();
        panel.AddDocumentsToLoadedTransaction(new[] { extraDocument });

        TestAssert.True(panel.StatusText.Contains("Added 1 document", StringComparison.OrdinalIgnoreCase), "Add document status mismatch.");
        var manifest = ManifestSerializer.Read(Path.Combine(manager.LoadedCaseFolderPath!, "manifest.json"));
        TestAssert.True(manifest.Payload.SourceFiles.Any(source => Path.GetFileName(source.CopiedPath) == "extra-plan.pdf"), "Added document should be copied into manifest source files.");
        TestAssert.True(File.Exists(Path.Combine(manager.LoadedCaseFolderPath!, "source", "extra-plan.pdf")), "Added document should be copied into source folder.");
    }

    public static async Task ActiveTransactionLocksSelectionSearchSortAndRefresh()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-100000005", "TR100000005", "Compute Survey Plan", "tester", "2024-10-15T09:38:00-05:00")
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

    public static async Task WorkflowExitSuspendRestoresTransactionListContext()
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
        manager.ClearLoadedTransaction();

        await panel.HandleWorkflowExitAsync(
            "TR100000004",
            "Suspended. Resume package uploaded and case is ready to reopen later.",
            preserveSavedMarker: true,
            suppressTransactionFromList: false,
            refreshTransactions: false);

        TestAssert.True(!panel.IsTransactionPanelLocked, "Suspend exit should restore transaction list interaction.");
        TestAssert.True(panel.CanRefresh, "Suspend exit should re-enable refresh.");
        TestAssert.True(panel.CanUseListControls, "Suspend exit should restore list controls.");
        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Suspend exit should keep the transaction selected for context.");
        TestAssert.Equal("TR100000004", panel.SavedTransactionNumber, "Suspend exit should mark the transaction as saved.");
    }

    public static async Task WorkflowExitCancelRestoresTransactionListContext()
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
        LifecycleCoordinator(manager, clock).CancelActiveProcess();

        await panel.HandleWorkflowExitAsync(
            "TR100000004",
            "Cancelled locally.",
            preserveSavedMarker: false,
            suppressTransactionFromList: false,
            refreshTransactions: false);

        TestAssert.True(!panel.IsTransactionPanelLocked, "Cancel exit should unlock the transaction list.");
        TestAssert.True(panel.CanRefresh, "Cancel exit should re-enable refresh.");
        TestAssert.True(panel.CanUseListControls, "Cancel exit should restore list controls.");
        TestAssert.Equal("TR100000004", panel.SelectedRow?.TransactionNumber, "Cancel exit should keep the transaction selected for context.");
        TestAssert.Equal(null, panel.SavedTransactionNumber, "Cancel exit should not mark the transaction as saved.");
    }

    public static async Task WorkflowExitCompleteRefreshesAndSuppressesCompletedTransaction()
    {
        using var tempRoot = new TempDirectory();
        var staleRow = Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00");
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { staleRow })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
        var coordinator = LifecycleCoordinator(manager, clock, new AlwaysReadyCompletionReadinessService());
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            coordinator,
            null,
            clock);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();
        await coordinator.CompleteAsync();

        await panel.HandleWorkflowExitAsync(
            "TR100000004",
            "Completed. Final package uploaded and transaction closed.",
            preserveSavedMarker: false,
            suppressTransactionFromList: true,
            refreshTransactions: true);

        TestAssert.True(!panel.IsTransactionPanelLocked, "Complete exit should unlock the transaction list.");
        TestAssert.True(panel.CanRefresh, "Complete exit should leave refresh enabled.");
        TestAssert.Equal(0, panel.Rows.Count, "Complete exit should suppress stale completed rows after refresh.");
        TestAssert.Equal(null, panel.SelectedRow, "Complete exit should clear selection.");
    }

    public static async Task FailedLoadPreservesPreviouslyLoadedTransaction()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-100000005", "TR100000005", "Compute Survey Plan", "tester", "2024-10-15T09:38:00-05:00")
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
                Row("task-100000005", "TR100000005", "Compute Survey Plan", "tester", "2024-10-15T09:38:00-05:00")
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

        TestAssert.Equal("Could not refresh transactions. Try again. (bad-response)", panel.ErrorText, "Error text mismatch.");
        TestAssert.Equal("Could not refresh transactions. Try again. (bad-response)", panel.StatusText, "Status text mismatch.");
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
        TestAssert.True(!panel.CanUseListControls, "Row interaction should be disabled while loading.");
        TestAssert.True(!panel.CanEditListCriteria, "Filter and sort controls should be disabled while loading.");
        TestAssert.True(panel.CanSearchTransactions, "Search should remain editable while loading so typing is not frozen.");
        TestAssert.True(!panel.CanLoadSelectedTransaction, "Load should be disabled while loading.");

        service.Complete();
        await refreshTask;

        TestAssert.True(!panel.IsLoading, "Panel should leave loading state after refresh completes.");
        TestAssert.True(panel.CanUseListControls, "List controls should re-enable after rows load.");
    }

    public static async Task RefreshTimeoutReleasesDisabledControls()
    {
        var originalTimeout = TransactionPanelState.RefreshTimeout;
        TransactionPanelState.RefreshTimeout = TimeSpan.FromMilliseconds(20);
        try
        {
            var service = new CancellableDelayedTransactionService();
            var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

            await panel.RefreshAsync();

            TestAssert.True(!panel.IsLoading, "Refresh timeout should release loading state.");
            TestAssert.True(panel.CanRefresh, "Refresh should be enabled again after timeout.");
            TestAssert.Equal("Transaction refresh timed out. Try again.", panel.StatusText, "Timeout status mismatch.");
            TestAssert.Equal("Transaction refresh timed out. Try again.", panel.ErrorText, "Timeout error mismatch.");
        }
        finally
        {
            TransactionPanelState.RefreshTimeout = originalTimeout;
        }
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
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new CaseResumePackageService(clock, () => "test"),
            () => outputRoot,
            clock);
    }

    private static InnolaTransactionLifecycleCoordinator LifecycleCoordinator(
        InnolaSessionManager manager,
        Func<DateTimeOffset> clock,
        ITransactionCompletionReadinessService? readinessService = null,
        IInnolaTransactionLifecycleService? lifecycleService = null)
    {
        return new InnolaTransactionLifecycleCoordinator(
            manager,
            new MockInnolaTransactionDetailService(),
            lifecycleService ?? new MockInnolaTransactionLifecycleService(),
            new MockInnolaSpatialUnitService(),
            readinessService ?? new DefaultTransactionCompletionReadinessService(),
            new WorkflowLifecycleAuditService(clock),
            new CaseResumePackageService(clock, () => "test"),
            clock);
    }

    private static InnolaTransactionRow Row(string taskId, string transactionNumber, string taskName, string assignedGroup, string receivedAt, string transactionType = "Plan Examination")
    {
        return new InnolaTransactionRow(
            taskId,
            transactionNumber.TrimStart('T', 'R'),
            transactionNumber,
            taskName,
            "parcel_workflow",
            InnolaTransactionStatus.Available,
            transactionType,
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

    private static void AssertToolbarCommandState(
        TransactionPanelState panel,
        bool canRefresh,
        bool canStart,
        bool canReopenCompare,
        bool canStop,
        bool canViewDocuments,
        bool canAddDocument,
        bool canComplete,
        string context)
    {
        TestAssert.Equal(canRefresh, panel.CanRefresh, $"Refresh property mismatch {context}.");
        TestAssert.Equal(canRefresh, panel.RefreshCommand.CanExecute(null), $"Refresh command mismatch {context}.");
        TestAssert.Equal(canStart, panel.CanStartTransaction, $"Start property mismatch {context}.");
        TestAssert.Equal(canStart, panel.StartTransactionCommand.CanExecute(null), $"Start command mismatch {context}.");
        TestAssert.Equal(canReopenCompare, panel.CanReopenCompare, $"CMP/Reopen Compare property mismatch {context}.");
        TestAssert.Equal(canReopenCompare, panel.ReopenCompareCommand.CanExecute(null), $"CMP/Reopen Compare command mismatch {context}.");
        TestAssert.Equal(canStop, panel.CanStopTask, $"Stop/Suspend property mismatch {context}.");
        TestAssert.Equal(canStop, panel.StopTaskCommand.CanExecute(null), $"Stop/Suspend command mismatch {context}.");
        TestAssert.Equal(canViewDocuments, panel.CanViewDocuments, $"View Documents property mismatch {context}.");
        TestAssert.Equal(canViewDocuments, panel.ViewDocumentsCommand.CanExecute(null), $"View Documents command mismatch {context}.");
        TestAssert.Equal(canAddDocument, panel.CanAddDocument, $"Add Document property mismatch {context}.");
        TestAssert.Equal(canAddDocument, panel.AddDocumentCommand.CanExecute(null), $"Add Document command mismatch {context}.");
        TestAssert.Equal(canComplete, panel.CanCompleteTask, $"Complete property mismatch {context}.");
        TestAssert.Equal(canComplete, panel.CompleteTaskCommand.CanExecute(null), $"Complete command mismatch {context}.");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        TestAssert.True(condition(), "Condition was not satisfied before timeout.");
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

    private sealed class SearchAwareTransactionService : IInnolaTransactionService
    {
        public TaskCompletionSource<string?> SearchObserved { get; } = new();

        public Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
        {
            if (string.Equals(query.Search, "100000400", StringComparison.OrdinalIgnoreCase))
            {
                SearchObserved.TrySetResult(query.Search);
                return Task.FromResult(InnolaTransactionListResult.Succeeded(new[]
                {
                    Row("task-100000400", "TR100000400", "Computation Check", "tester", "2024-10-15T09:53:00-05:00")
                }));
            }

            return Task.FromResult(InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00")
            }));
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

    private sealed class FailingClaimLifecycleService : IInnolaTransactionLifecycleService
    {
        public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Failure(
                "Transaction is already in progress by another user.",
                "ownership_conflict"));
        }

        public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Failure("Not claimed.", "ownership_conflict"));
        }

        public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Failure("Not claimed.", "ownership_conflict"));
        }
    }

    private sealed class CountingTransactionLifecycleService : IInnolaTransactionLifecycleService
    {
        private string? owner;

        public int ClaimCalls { get; private set; }

        public int SaveProgressCalls { get; private set; }

        public int CompleteCalls { get; private set; }

        public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            ClaimCalls++;
            owner = request.Session.User.Username;
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
                "in_progress",
                request.Session.User.Username,
                request.Session.User.DisplayName,
                "Transaction is in progress."));
        }

        public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            SaveProgressCalls++;
            if (!string.Equals(owner, request.Session.User.Username, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(InnolaTransactionLifecycleResult.Failure("Not claimed.", "ownership_conflict"));
            }

            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
                "in_progress",
                request.Session.User.Username,
                request.Session.User.DisplayName,
                "Progress saved."));
        }

        public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            if (!string.Equals(owner, request.Session.User.Username, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(InnolaTransactionLifecycleResult.Failure("Not claimed.", "ownership_conflict"));
            }

            owner = null;
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded(
                "completed",
                request.Session.User.Username,
                request.Session.User.DisplayName,
                "Transaction completed."));
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

    private sealed class CancellableDelayedTransactionService : IInnolaTransactionService
    {
        public async Task<InnolaTransactionListResult> GetAvailableTransactionsAsync(InnolaTransactionQuery query, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            return InnolaTransactionListResult.Succeeded(Array.Empty<InnolaTransactionRow>());
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

        public Task<InnolaAttachmentUploadResult> UploadAttachmentAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            string fileName,
            string contentType,
            byte[] content,
            string sourceType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaAttachmentUploadResult.Succeeded());
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
