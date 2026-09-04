using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.FabricMaintenance;
using ParcelWorkflowAddIn.Workflow.Pla;
using ParcelWorkflowAddIn.Workflow.RtExamination;
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

    public static void PlaBTestInputRequiresLogin()
    {
        var launched = false;
        var panel = new TransactionPanelState(
            new InnolaSessionManager(new FakeAuthService()),
            new FakeTransactionService(),
            "parcel_workflow",
            transactionLoadService: null,
            plaBTestInputLauncher: (_, _, _, _, _, _) => launched = true);

        TestAssert.False(panel.CanOpenPlaBTestInput, "PLA_B test input should be disabled while logged out.");
        TestAssert.False(panel.OpenPlaBTestInputCommand.CanExecute(null), "PLA_B test input command should not execute while logged out.");

        panel.OpenPlaBTestInputCommand.Execute(null);

        TestAssert.False(launched, "PLA_B test input launcher must not run while logged out.");
        TestAssert.Equal("Log in before opening PLA_B test input.", panel.StatusText, "PLA_B logged-out status mismatch.");
    }

    public static async Task PlaBTestInputLaunchesWithSelectedTransactionAfterLogin()
    {
        string? launchedTransactionNumber = null;
        string? launchedPeNumber = "unchanged";
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTestInputPreparationResult>>? prepare = null;
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>>? complete = null;
        string? status = null;
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row(
                    "task-1",
                    "TR100000111",
                    "In Plan Annexation Preparation",
                    "survey",
                    "2026-08-27T09:00:00-05:00",
                    "First Registration",
                    "First Registration",
                    "Plan Annexation",
                    new[] { "First Registration", "Plan Annexation" })
            })
        };
        var manager = LoggedInManager();
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            plaBTestInputLauncher: (transactionNumber, peNumber, prepareHandler, completeHandler, _, statusText) =>
            {
                launchedTransactionNumber = transactionNumber;
                launchedPeNumber = peNumber;
                prepare = prepareHandler;
                complete = completeHandler;
                status = statusText;
            },
            plaBSpatialUnitService: new FixedExaminationNumberSpatialUnitService("100000631"));

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        manager.SelectTransaction(panel.Rows[0], new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        manager.MarkTransactionLoaded(panel.Rows[0].TransactionNumber, Path.GetTempPath(), "2026-08-27T10:00:00.0000000Z", false);
        manager.MarkTransactionClaimed("tester", "Test User", "2026-08-27T10:00:00.0000000Z", "Transaction is in progress.");

        TestAssert.True(panel.CanOpenPlaBTestInput, "PLA_B test input should be enabled after login.");
        TestAssert.True(panel.OpenPlaBTestInputCommand.CanExecute(null), "PLA_B test input command should be executable after login.");

        panel.OpenPlaBTestInputCommand.Execute(null);
        for (var attempt = 0; attempt < 25 && launchedTransactionNumber is null; attempt++)
        {
            await Task.Delay(10);
        }

        TestAssert.Equal("TR100000111", launchedTransactionNumber, "PLA_B test input should receive the selected current transaction number.");
        TestAssert.Equal("100000631", launchedPeNumber, "PLA_B task input should receive PE from SpatialUnit.examinationNumber.");
        TestAssert.True(prepare is not null, "PLA_B test input should receive a prepare callback.");
        TestAssert.True(complete is not null, "PLA_B test input should receive a complete callback.");
        TestAssert.True(status?.Contains("Ready to process Plan Annexation", StringComparison.Ordinal) == true, "PLA_B launch status mismatch.");
    }

    public static async Task PlaBTaskRequiresStartedActiveTransaction()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row(
                    "task-1",
                    "TR100000111",
                    "In Plan Annexation Preparation",
                    "survey",
                    "2026-08-27T09:00:00-05:00",
                    "First Registration",
                    "First Registration",
                    "Plan Annexation",
                    new[] { "First Registration", "Plan Annexation" })
            })
        };
        var panel = new TransactionPanelState(
            LoggedInManager(),
            service,
            "parcel_workflow",
            transactionLoadService: null,
            lifecycleCoordinator: null);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];

        TestAssert.False(panel.CanOpenPlaBTestInput, "PLA_B task form should remain disabled until the selected transaction is started/active.");
        TestAssert.True(panel.OpenPlaBTestInputTooltip.Contains("Start", StringComparison.OrdinalIgnoreCase)
            && panel.OpenPlaBTestInputTooltip.Contains("In Plan Annexation Preparation", StringComparison.Ordinal), "Disabled tooltip should explain the start requirement.");
    }

    public static async Task PlaBTaskRequiresActivePlanAnnexationTaskWhenTransactionHasMultipleRows()
    {
        string? launchedTransactionNumber = null;
        var sendNoticeRow = Row(
            "task-send-notice",
            "TR100001349",
            "Send Notices by Mail",
            "survey",
            "2026-08-27T09:00:00-05:00",
            "First Registration",
            "First Registration",
            null,
            new[] { "First Registration", "Plan Annexation" });
        var planAnnexRow = Row(
            "task-plan-annex",
            "TR100001349",
            "In Plan Annexation Preparation",
            "survey",
            "2026-08-27T09:05:00-05:00",
            "First Registration",
            "First Registration",
            "Plan Annexation",
            new[] { "First Registration", "Plan Annexation" });
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { sendNoticeRow, planAnnexRow })
        };
        var manager = LoggedInManager();
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            plaBTestInputLauncher: (transactionNumber, _, _, _, _, _) => launchedTransactionNumber = transactionNumber,
            plaBSpatialUnitService: new FixedExaminationNumberSpatialUnitService("100000631"));

        await panel.RefreshAsync();
        panel.SelectedRow = planAnnexRow;
        manager.SelectTransaction(sendNoticeRow, new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
        manager.MarkTransactionLoaded(sendNoticeRow.TransactionNumber, Path.GetTempPath(), "2026-08-27T10:00:00.0000000Z", false);
        manager.MarkTransactionClaimed("tester", "Test User", "2026-08-27T10:00:00.0000000Z", "Transaction is in progress.");

        TestAssert.False(panel.CanOpenPlaBTestInput, "PLA_B form must stay disabled when another task for the same TR is active.");
        TestAssert.True(panel.OpenPlaBTestInputTooltip.Contains("Send Notices by Mail", StringComparison.Ordinal), "Disabled tooltip should identify the active non-PLA_B task.");

        manager.SelectTransaction(planAnnexRow, new DateTimeOffset(2026, 8, 27, 10, 5, 0, TimeSpan.Zero));
        manager.MarkTransactionLoaded(planAnnexRow.TransactionNumber, Path.GetTempPath(), "2026-08-27T10:05:00.0000000Z", false);
        manager.MarkTransactionClaimed("tester", "Test User", "2026-08-27T10:05:00.0000000Z", "Transaction is in progress.");

        TestAssert.True(panel.CanOpenPlaBTestInput, "PLA_B form should enable once the Plan Annexation task itself is active.");
        panel.OpenPlaBTestInputCommand.Execute(null);
        for (var attempt = 0; attempt < 25 && launchedTransactionNumber is null; attempt++)
        {
            await Task.Delay(10);
        }

        TestAssert.Equal("TR100001349", launchedTransactionNumber, "PLA_B form should launch for the active Plan Annexation task.");
    }

    public static async Task PlaBStartAllowsFirstRegistrationPreparationAndOpensTaskForm()
    {
        using var temp = new TempDirectory();
        string? launchedTransactionNumber = null;
        string? launchedPeNumber = null;
        Func<PlaBTestEmulationInputViewModel, Task<PlaBTaskCompletionResult>>? cancel = null;
        IReadOnlyList<string>? cleanupGroups = null;
        var planAnnexRow = Row(
            "task-plan-annex",
            "TR100001349",
            "In Plan Annexation Preparation",
            "survey",
            "2026-08-27T09:05:00-05:00",
            "First Registration");
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { planAnnexRow })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 8, 27, 10, 5, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            lifecycleCoordinator: LifecycleCoordinator(manager, clock),
            clock: clock,
            supportedTransactionTypes: new[] { "Plan Examination" },
            plaBTestInputLauncher: (transactionNumber, peNumber, _, _, cancelHandler, _) =>
            {
                launchedTransactionNumber = transactionNumber;
                launchedPeNumber = peNumber;
                cancel = cancelHandler;
            },
            plaBSpatialUnitService: new FixedExaminationNumberSpatialUnitService("100000631"),
            plaBMapCleanup: (groups, _) =>
            {
                cleanupGroups = groups;
                return Task.FromResult(PlaBMapCleanupResult.Succeeded(groups.Count));
            },
            plaBCaseFolderPreparer: (transactionNumber, username) => new CaseFolderStore(clock, () => "run-pla-b-start")
                .CreateCase(temp.Path, transactionNumber, username));

        await panel.RefreshAsync();
        panel.SelectedRow = planAnnexRow;

        TestAssert.True(panel.CanStartTransaction, "Plan Annexation preparation row should be startable even when older supported_transaction_types omit First Registration.");
        await panel.StartSelectedTransactionAsync();
        for (var attempt = 0; attempt < 25 && launchedTransactionNumber is null; attempt++)
        {
            await Task.Delay(10);
        }

        TestAssert.True(manager.HasActiveTransaction, "Starting Plan Annexation preparation should claim the Innola task.");
        TestAssert.Equal("task-plan-annex", manager.SelectedTransaction?.TaskId, "PLA_B start should preserve the exact selected task id.");
        TestAssert.Equal("TR100001349", launchedTransactionNumber, "PLA_B start should open the task form for the selected transaction.");
        TestAssert.Equal("100000631", launchedPeNumber, "PLA_B start should populate PE from SpatialUnit.examinationNumber.");
        TestAssert.True(cancel is not null, "PLA_B form should receive a cancel callback.");

        var cancelResult = await cancel!(new PlaBTestEmulationInputViewModel
        {
            CurrentTransactionNumber = "100001349",
            PeNumber = "100000631",
            ProcessSucceeded = true,
            ProcessMapGroupNames = new[] { "PLA TR100001349 - Current Transaction", "PE 100000631 - Approved PE Output" }
        });

        TestAssert.True(cancelResult.Success, $"PLA_B cancel should accept the numeric form transaction when the active transaction has a TR prefix. {cancelResult.Message}");
        TestAssert.Equal(2, cleanupGroups?.Count ?? 0, "PLA_B cancel should clean tracked Process map groups.");
        TestAssert.True(!manager.HasActiveTransaction, "PLA_B cancel should release the active transaction list lock.");
        TestAssert.True(!manager.IsTransactionLoaded, "PLA_B cancel should clear the loaded transaction.");
    }

    public static async Task FabricMaintenanceStartUsesSelectedTaskWhenTransactionHasMultipleRows()
    {
        using var temp = new TempDirectory();
        string? launchedTransactionNumber = null;
        string? launchedPeNumber = null;
        var annotateRow = Row(
            "task-annotate",
            "100000859",
            "Annotate R# & Photocopy Final Survey Plan",
            "survey",
            "2026-08-31T09:00:00-05:00",
            "Plan Examination by Area",
            "TEST_6, GIS",
            null,
            new[] { "TEST_6, GIS" });
        var fabricRow = Row(
            "task-fabric",
            "100000859",
            "In Parcel Fabric Update",
            "survey",
            "2026-08-31T09:05:00-05:00",
            "Plan Examination by Area",
            "TEST_6, GIS",
            null,
            new[] { "TEST_6, GIS" });
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { annotateRow, fabricRow })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 8, 31, 10, 5, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            lifecycleCoordinator: LifecycleCoordinator(manager, clock),
            clock: clock,
            supportedTransactionTypes: new[] { "Plan Examination" },
            plaBSpatialUnitService: new FixedExaminationNumberSpatialUnitService("100000814"),
            fabricMaintenancePromotionSettings: FabricMaintenancePromotionSettings.Default,
            fabricMaintenanceWorkspaceLauncher: (transactionNumber, peNumber, _) =>
            {
                launchedTransactionNumber = transactionNumber;
                launchedPeNumber = peNumber;
            },
            plaBCaseFolderPreparer: (transactionNumber, username) => new CaseFolderStore(clock, () => "run-fabric-maintenance-start")
                .CreateCase(temp.Path, transactionNumber, username));

        await panel.RefreshAsync();
        panel.SelectedRow = fabricRow;
        await panel.StartSelectedTransactionAsync();

        for (var attempt = 0; attempt < 25 && launchedTransactionNumber is null; attempt++)
        {
            await Task.Delay(10);
        }

        TestAssert.True(manager.HasActiveTransaction, "Starting Fabric Maintenance should claim the selected Innola task.");
        TestAssert.Equal("task-fabric", manager.SelectedTransaction?.TaskId, "Fabric Maintenance start should preserve the exact selected task id.");
        TestAssert.Equal("In Parcel Fabric Update", manager.SelectedTransaction?.TaskName, "Fabric Maintenance start should not bind to the first same-TR task.");
        TestAssert.Equal("100000859", launchedTransactionNumber, "Fabric Maintenance workspace should open for the selected transaction number.");
        TestAssert.Equal("100000814", launchedPeNumber, "Fabric Maintenance workspace should receive PE from SpatialUnitExt.examinationNumber.");
    }

    public static async Task FabricMaintenanceStartOpensWorkspaceWithEditablePeWhenSpatialUnitPeIsMissing()
    {
        using var temp = new TempDirectory();
        string? launchedTransactionNumber = null;
        string? launchedPeNumber = null;
        string? launchedStatus = null;
        var fabricRow = Row(
            "task-fabric",
            "100000859",
            "In Parcel Fabric Update",
            "survey",
            "2026-08-31T09:05:00-05:00",
            "Plan Examination by Area",
            "TEST_6, GIS",
            null,
            new[] { "TEST_6, GIS" });
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { fabricRow })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 8, 31, 10, 5, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            lifecycleCoordinator: LifecycleCoordinator(manager, clock),
            clock: clock,
            supportedTransactionTypes: new[] { "Plan Examination" },
            plaBSpatialUnitService: new FixedExaminationNumberSpatialUnitService(null),
            fabricMaintenancePromotionSettings: FabricMaintenancePromotionSettings.Default,
            fabricMaintenanceWorkspaceLauncher: (transactionNumber, peNumber, status) =>
            {
                launchedTransactionNumber = transactionNumber;
                launchedPeNumber = peNumber;
                launchedStatus = status;
            },
            plaBCaseFolderPreparer: (transactionNumber, username) => new CaseFolderStore(clock, () => "run-fabric-missing-pe")
                .CreateCase(temp.Path, transactionNumber, username));

        await panel.RefreshAsync();
        panel.SelectedRow = fabricRow;
        await panel.StartSelectedTransactionAsync();

        for (var attempt = 0; attempt < 25 && launchedTransactionNumber is null; attempt++)
        {
            await Task.Delay(10);
        }

        TestAssert.Equal("100000859", launchedTransactionNumber, "Missing PE should not block opening Fabric Maintenance workspace.");
        TestAssert.Equal(string.Empty, launchedPeNumber, "Missing PE should launch with a blank editable PE field.");
        TestAssert.True(launchedStatus?.Contains("Enter the PE number manually", StringComparison.Ordinal) == true, "Missing PE launch status should instruct manual entry.");
        TestAssert.Equal(launchedStatus, panel.StatusText, "Panel status should match the missing-PE manual-entry instruction.");
    }

    public static async Task PlaBTestOpenViewerDownloadsCurrentTransactionSources()
    {
        var xaml = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "PlaBTestInputWindow.xaml"));

        TestAssert.False(xaml.Contains("Open Viewer", StringComparison.Ordinal), "PLA_B task form should not expose the old test Open Viewer button.");
    }

    public static async Task PlaBTestPrepareBuildsRecoveryPlanWithoutStartingWorkflow()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(Array.Empty<InnolaTransactionRow>())
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
        PlaBTestEmulationInputViewModel? preparedInput = null;
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            LifecycleCoordinator(manager, clock),
            null,
            clock,
            supportedTransactionTypes: new[] { "Plan Examination" },
            computeWorkflowStages: new[] { "Computation Check" },
            plaBRecoveryPreparer: (input, _) =>
            {
                preparedInput = input;
                return Task.FromResult(PlaBTestInputPreparationResult.Succeeded(
                    "PLA_B recovery loaded.\nCurrent TR group: PLA 100001266 - Current Transaction\nWorking_review query: transaction_number = 100000630\nPE group: PE 100000630 - Approved PE Output\nGDB: 100000630_parcel_output.gdb"));
            });

        await panel.RefreshAsync();
        var result = await panel.PreparePlaBTestInputAsync(new PlaBTestEmulationInputViewModel
        {
            CurrentTransactionNumber = "100001266",
            PeNumber = "PE-100000630"
        });

        TestAssert.True(result.Success, $"PLA_B prepare should build the recovery plan. {result.Message}");
        TestAssert.Equal(null, manager.LoadedTransactionNumber, "PLA_B prepare must not load the normal Parcel Workflow transaction.");
        TestAssert.False(manager.HasActiveTransaction, "PLA_B prepare must not start or claim the normal transaction.");
        TestAssert.Equal("100000630", PlaBTestEmulationContext.GetForTransaction("TR100001266")?.PeNumber, "PLA_B prepare should stage the normalized PE number.");
        TestAssert.Equal("100000630", preparedInput?.NormalizedPeNumber, "PLA_B prepare should pass normalized PE input to the recovery preparer.");
        TestAssert.True(result.Message.Contains("Working_review query: transaction_number = 100000630", StringComparison.Ordinal), "PLA_B prepare should expose the working_review query.");
        TestAssert.True(result.Message.Contains("100000630_parcel_output.gdb", StringComparison.Ordinal), "PLA_B prepare should expose the expected PE output GDB.");
        TestAssert.False(result.Message.Contains("No PLA_A workflow was opened", StringComparison.Ordinal), "PLA_B prepare should not show obsolete test workflow text.");
    }

    public static async Task PlaBCompleteUsesConfiguredTransitionAndCleansProcessGroups()
    {
        using var temp = new TempDirectory();
        var manager = LoggedInManager();
        var now = new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
        var row = Row(
            "task-1",
            "TR100000111",
            "In Plan Annexation Preparation",
            "survey",
            "2026-08-27T09:00:00-05:00",
            "First Registration",
            "First Registration",
            "Plan Annexation",
            new[] { "First Registration", "Plan Annexation" });
        manager.SelectTransaction(row, now);
        manager.MarkTransactionLoaded(row.TransactionNumber, temp.Path, now.ToString("O"), false);
        var lifecycle = new RecordingCompleteLifecycleService();
        IReadOnlyList<string>? cleanupGroups = null;
        var panel = new TransactionPanelState(
            manager,
            new FakeTransactionService(),
            "parcel_workflow",
            transactionLoadService: null,
            plaBTransactionLifecycleService: lifecycle,
            plaBMapCleanup: (groups, _) =>
            {
                cleanupGroups = groups;
                return Task.FromResult(PlaBMapCleanupResult.Succeeded(groups.Count));
            });
        var input = new PlaBTestEmulationInputViewModel
        {
            CurrentTransactionNumber = "TR100000111",
            PeNumber = "100000631",
            ProcessSucceeded = true,
            ProcessMapGroupNames = new[] { "PLA TR100000111 - Current Transaction", "PE 100000631 - Approved PE Output" }
        };

        var result = await panel.CompletePlaBPlanAnnexationTaskAsync(input);

        TestAssert.True(result.Success, result.Message);
        TestAssert.Equal(1, lifecycle.CompleteCalls, "PLA_B Complete should call Innola lifecycle once.");
        TestAssert.Equal("Review and Sign Plan Annexed Diagram", lifecycle.LastRequest?.DesiredTransitionName, "PLA_B Complete should request the configured next stage.");
        TestAssert.Equal(2, cleanupGroups?.Count ?? 0, "PLA_B Complete should clean only tracked Process map groups.");
        TestAssert.True(!manager.HasActiveTransaction, "PLA_B Complete should release the active transaction list lock.");
        TestAssert.True(!manager.IsTransactionLoaded, "PLA_B Complete should clear the loaded transaction.");
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

    public static async Task LoadSelectedTransactionPreservesSelectedTaskWhenDuplicateTransactionRowsExist()
    {
        using var tempRoot = new TempDirectory();
        var annotateRow = Row(
            "task-annotate",
            "100000859",
            "Annotate R# & Photocopy Final Survey Plan",
            "survey",
            "2026-08-31T09:00:00-05:00",
            "Plan Examination by Area",
            "TEST_6, GIS",
            null,
            new[] { "TEST_6, GIS" });
        var fabricRow = Row(
            "task-fabric",
            "100000859",
            "In Parcel Fabric Update",
            "survey",
            "2026-08-31T09:05:00-05:00",
            "Plan Examination by Area",
            "TEST_6, GIS",
            null,
            new[] { "TEST_6, GIS" });
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { annotateRow, fabricRow })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 8, 31, 10, 5, 0, TimeSpan.Zero);
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            transactionLoadService: null,
            lifecycleCoordinator: LifecycleCoordinator(manager, clock),
            clock: clock,
            supportedTransactionTypes: new[] { "Plan Examination" },
            fabricMaintenancePromotionSettings: FabricMaintenancePromotionSettings.Default);

        await panel.RefreshAsync();
        panel.SearchText = "859";
        panel.SelectedRow = fabricRow;
        await panel.LoadSelectedTransactionAsync();

        TestAssert.Equal(string.Empty, panel.SearchText, "Load should clear the duplicate transaction search text.");
        TestAssert.Equal("task-fabric", manager.SelectedTransaction?.TaskId, "Load should select the exact Fabric Maintenance task in session state.");
        TestAssert.Equal("task-fabric", panel.SelectedRow?.TaskId, "Load should keep the Fabric Maintenance row selected after clearing search.");
        TestAssert.Equal("In Parcel Fabric Update", panel.SelectedRow?.TaskName, "Load should not jump to the first same-number transaction row.");
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
        TestAssert.True(panel.StatusText.Contains("task 'Compare Survey Plan' is not configured", StringComparison.OrdinalIgnoreCase), "Unsupported workflow stage status should name the blocked selected task.");
        TestAssert.True(panel.StatusText.Contains("Compute Survey Plan", StringComparison.OrdinalIgnoreCase), "Unsupported workflow stage status should include compute support guidance.");
        TestAssert.True(panel.StatusText.Contains("In RT Examination", StringComparison.OrdinalIgnoreCase), "Unsupported workflow stage status should include RT support guidance when configured.");
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
        var supportingDocumentLaunchCount = 0;
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
            compareWorkspaceLauncher: transactionNumber => launchedTransactions.Add(transactionNumber),
            supportingDocumentsLauncher: () =>
            {
                supportingDocumentLaunchCount++;
                return true;
            });

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(InnolaTransactionLifecycleStatus.InProgress, manager.LifecycleStatus, "Compare start should claim the transaction before launch.");
        TestAssert.Equal(1, launchedTransactions.Count, "Compare workspace should launch once.");
        TestAssert.Equal("TR100000004", launchedTransactions[0], "Compare workspace launch transaction mismatch.");
        TestAssert.Equal(1, supportingDocumentLaunchCount, "Compare start should open the Supporting Documents WPF window once.");
        TestAssert.True(manager.CanOpenParcelWorkflow, "Claimed Compare transaction should keep active transaction gates enabled.");
    }


    public static async Task RtExaminationStageStartsAndLaunchesWorkspaceForAnyTransactionType()
    {
        using var tempRoot = new TempDirectory();
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-100000004", "TR100000004", "In RT Examination", "tester", "2026-09-04T09:24:00-05:00", "First Registration")
            })
        };
        var manager = LoggedInManager();
        var clock = () => new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        var launched = new List<(string TransactionNumber, string? StatusText)>();
        var supportingDocumentLaunchCount = 0;
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock),
            null,
            clock,
            supportedTransactionTypes: new[] { "Plan Examination" },
            computeWorkflowStages: new[] { "Compute Survey Plan" },
            compareWorkflowStages: new[] { "Compare Survey Plan" },
            compareTransactionLoadService: Loader(manager, tempRoot.Path, clock, new AppRtExaminationDetailService()),
            supportingDocumentsLauncher: () =>
            {
                supportingDocumentLaunchCount++;
                return true;
            },
            rtExaminationSettings: RtExaminationSettings.Default,
            rtExaminationWorkspaceLauncher: (transactionNumber, statusText) => launched.Add((transactionNumber, statusText)));

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(InnolaTransactionLifecycleStatus.InProgress, manager.LifecycleStatus, "RT Examination start should claim the transaction before launch.");
        TestAssert.Equal("TR100000004", manager.SelectedTransaction?.TransactionNumber, "RT Examination should load the selected transaction.");
        TestAssert.Equal(1, launched.Count, "RT Examination workspace should launch once.");
        TestAssert.Equal("TR100000004", launched[0].TransactionNumber, "RT Examination workspace launch transaction mismatch.");
        TestAssert.True(launched[0].StatusText?.Contains("RT Examination", StringComparison.OrdinalIgnoreCase) == true, "RT launch should include a stage-aware status message.");
        TestAssert.Equal(1, supportingDocumentLaunchCount, "RT Examination start should open Supporting Documents once.");
        TestAssert.True(manager.CanOpenParcelWorkflow, "Claimed RT Examination transaction should keep active transaction gates enabled.");
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
        var supportingDocumentLaunchCount = 0;
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
            },
            supportingDocumentsLauncher: () =>
            {
                supportingDocumentLaunchCount++;
                return true;
            });

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(1, lifecycleService.ClaimCalls, "Initial Compare start should claim once.");
        TestAssert.Equal(1, launchedTransactions.Count, "Initial Compare start should launch once.");
        TestAssert.Equal(1, supportingDocumentLaunchCount, "Initial Compare start should open Supporting Documents once.");
        TestAssert.True(panel.CanReopenCompare, "Active Compare task should expose Reopen Compare.");

        await panel.ReopenCompareWorkspaceAsync();

        TestAssert.Equal(1, lifecycleService.ClaimCalls, "Reopen Compare must not claim/start the task again.");
        TestAssert.Equal(2, launchedTransactions.Count, "Reopen Compare should launch another Compare window instance.");
        TestAssert.Equal(2, supportingDocumentLaunchCount, "Reopen Compare should reopen or activate Supporting Documents.");
        TestAssert.True(compareLifecycleBridge is not null, "Compare launch should receive a lifecycle bridge.");

        var suspendResult = await compareLifecycleBridge!.SuspendAsync("TR100000004");

        TestAssert.True(suspendResult.Success, "Lifecycle bridge should suspend through the panel path.");
        TestAssert.Equal(1, lifecycleService.SaveProgressCalls, "Suspend should save progress through the existing lifecycle service.");
        TestAssert.False(panel.IsTransactionPanelLocked, "Suspend from Compare should unlock the transaction panel.");
        TestAssert.Equal("100000004", panel.SavedTransactionNumber, "Suspended Compare task should remain marked as saved for resume.");
    }

    public static async Task ActiveCompareTaskDisablesCmpWhenCompareWorkspaceIsOpen()
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
        var compareWorkspaceOpen = false;
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
            compareWorkspaceLifecycleLauncher: (transactionNumber, _) =>
            {
                launchedTransactions.Add(transactionNumber);
                compareWorkspaceOpen = true;
            },
            supportingDocumentsLauncher: () => true,
            isCompareWorkspaceOpen: () => compareWorkspaceOpen);

        await panel.RefreshAsync();
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();

        TestAssert.Equal(1, launchedTransactions.Count, "Initial Compare start should launch the workspace.");
        TestAssert.False(panel.CanReopenCompare, "CMP should be disabled while the Compare workspace is already open.");
        TestAssert.True(panel.ReopenCompareTooltip.Contains("already open", StringComparison.OrdinalIgnoreCase), "CMP tooltip should explain why the button is disabled.");
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

    public static void ProductionTransactionPanelLaunchesCompareWithSafeLoader()
    {
        var source = File.ReadAllText(FindSourceFile("TransactionPanelDockpaneViewModel.cs"));

        TestAssert.True(
            source.Contains("compareWorkspaceLifecycleLauncher: ShellState.OpenCompareWorkspace", StringComparison.Ordinal)
            && !source.Contains("compareWorkspaceLauncher: ShellState.OpenCompareWorkspace", StringComparison.Ordinal),
            "Production transaction panel must launch the Compare WPF through the lifecycle-aware launcher only.");
        TestAssert.True(
            source.Contains("compareTransactionLoadService: ShellState.CompareTransactionLoader", StringComparison.Ordinal),
            "Production transaction panel must use the Compare-safe loader so Compare starts do not prepare the ArcGIS map before routing.");
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
        var supportingDocumentLaunchCount = 0;
        var panel = new TransactionPanelState(
            manager,
            service,
            "parcel_workflow",
            Loader(manager, tempRoot.Path, clock),
            LifecycleCoordinator(manager, clock),
            null,
            clock,
            supportingDocumentsLauncher: () =>
            {
                supportingDocumentLaunchCount++;
                return true;
            });

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
        TestAssert.True(panel.CanShowSupportingDocuments, "Supporting Documents should be enabled after load/start.");
        TestAssert.Equal(1, supportingDocumentLaunchCount, "Compute start should open the Supporting Documents WPF window once.");
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
        panel.SelectedRow = panel.Rows[0];
        await panel.StartSelectedTransactionAsync();
        panel.SelectedFilter = "My tasks";
        panel.SearchText = "TR100000004";
        LifecycleCoordinator(manager, clock).CancelActiveProcess();

        await panel.HandleWorkflowExitAsync(
            "TR100000004",
            "Cancelled locally.",
            preserveSavedMarker: false,
            suppressTransactionFromList: false,
            refreshTransactions: true);

        TestAssert.True(!panel.IsTransactionPanelLocked, "Cancel exit should unlock the transaction list.");
        TestAssert.True(panel.CanRefresh, "Cancel exit should re-enable refresh.");
        TestAssert.True(panel.CanUseListControls, "Cancel exit should restore list controls.");
        TestAssert.Equal("All tasks", panel.SelectedFilter, "Cancel exit should clear the transaction filter.");
        TestAssert.Equal(string.Empty, panel.SearchText, "Cancel exit should clear transaction search text.");
        TestAssert.True(service.CallCount >= 2, "Cancel exit should refresh the transaction list.");
        TestAssert.Equal("All tasks", service.LastQuery?.Filter, "Cancel refresh should request the full task list.");
        TestAssert.Equal(string.Empty, service.LastQuery?.Search, "Cancel refresh should not keep stale search text.");
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

    public static async Task ActiveQueueHidesCompletedAndUnavailableRows()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00"),
                Row("task-2", "TR100000005", "Completed Task", "tester", "2024-10-15T09:38:00-05:00") with
                {
                    Status = InnolaTransactionStatus.Completed
                },
                Row("task-3", "TR100000006", "Locked Task", "tester", "2024-10-15T09:53:00-05:00") with
                {
                    Status = InnolaTransactionStatus.Locked,
                    IsLoadable = false,
                    UnavailableReason = "Assigned to another user."
                }
            })
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();

        TestAssert.Equal(1, panel.Rows.Count, "The default active queue should only show active loadable transactions.");
        TestAssert.Equal("TR100000004", panel.Rows[0].TransactionNumber, "Active row mismatch.");
        TestAssert.True(!panel.Rows.Any(row => row.TransactionNumber == "TR100000005"), "Completed transactions must be hidden by default.");
        TestAssert.True(!panel.Rows.Any(row => row.TransactionNumber == "TR100000006"), "Unavailable transactions must be hidden by default.");
    }

    public static async Task SelectedTransactionDetailsProjectRowMetadataAndSearch()
    {
        var service = new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[]
            {
                Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") with
                {
                    Applicant = "Alex Robinson",
                    OwnerOrResponsibleParty = "Estate of Henry Brown",
                    Surveyor = "Mary Blake",
                    Parish = "St. Ann"
                },
                Row("task-2", "TR100000005", "Compute Survey Plan", "survey", "2024-10-15T09:38:00-05:00") with
                {
                    Applicant = "Different Applicant",
                    Surveyor = "Other Surveyor",
                    Parish = "St. Mary"
                }
            })
        };
        var panel = new TransactionPanelState(LoggedInManager(), service, "parcel_workflow");

        await panel.RefreshAsync();
        panel.SearchText = "St. Ann";
        panel.SelectedRow = panel.Rows[0];

        TestAssert.Equal(1, panel.Rows.Count, "Search should include selected detail fields such as parish.");
        TestAssert.Equal("Transaction: TR100000004", panel.SelectedTransactionNumberText, "Selected transaction detail mismatch.");
        TestAssert.Equal("Task: Computation Check", panel.SelectedTaskText, "Selected task detail mismatch.");
        TestAssert.Equal("Type: Plan Examination", panel.SelectedTransactionTypeText, "Selected type detail mismatch.");
        TestAssert.Equal("Applicant: Alex Robinson", panel.SelectedApplicantText, "Selected applicant detail mismatch.");
        TestAssert.Equal("Owner / responsible: Estate of Henry Brown", panel.SelectedOwnerText, "Selected owner detail mismatch.");
        TestAssert.Equal("Surveyor: Mary Blake", panel.SelectedSurveyorText, "Selected surveyor detail mismatch.");
        TestAssert.Equal("Parish: St. Ann", panel.SelectedParishText, "Selected parish detail mismatch.");
        TestAssert.Equal("Assigned: tester", panel.SelectedAssignmentText, "Selected assignment detail mismatch.");
        TestAssert.Equal("Status: Available", panel.SelectedStatusText, "Selected status detail mismatch.");
        TestAssert.Equal("Readiness: Ready to load", panel.SelectedReadinessText, "Selected readiness detail mismatch.");
    }

    public static async Task DisabledToolbarTooltipsExplainUnavailableActions()
    {
        var loggedOutPanel = new TransactionPanelState(new InnolaSessionManager(new FakeAuthService()), new FakeTransactionService(), "parcel_workflow");

        TestAssert.True(loggedOutPanel.RefreshTooltip.Contains("Log in", StringComparison.OrdinalIgnoreCase), "Refresh disabled reason should explain login requirement.");
        TestAssert.True(loggedOutPanel.StartTransactionTooltip.Contains("Log in", StringComparison.OrdinalIgnoreCase), "Start disabled reason should explain login requirement.");

        var loggedInPanel = new TransactionPanelState(LoggedInManager(), new FakeTransactionService
        {
            Result = InnolaTransactionListResult.Succeeded(new[] { Row("task-1", "TR100000004", "Computation Check", "tester", "2024-10-15T09:24:00-05:00") })
        }, "parcel_workflow");

        await loggedInPanel.RefreshAsync();

        TestAssert.True(loggedInPanel.StartTransactionTooltip.Contains("not configured", StringComparison.OrdinalIgnoreCase), "Start disabled reason should explain missing lifecycle configuration.");
        TestAssert.True(loggedInPanel.ViewDocumentsTooltip.Contains("Load a transaction", StringComparison.OrdinalIgnoreCase), "Documents disabled reason should explain loading requirement.");
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
        Func<DateTimeOffset> clock,
        IInnolaTransactionDetailService? detailService = null)
    {
        return new InnolaTransactionLoadService(
            manager,
            detailService ?? new MockInnolaTransactionDetailService(),
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

    private static InnolaTransactionRow Row(
        string taskId,
        string transactionNumber,
        string taskName,
        string assignedGroup,
        string receivedAt,
        string transactionType = "Plan Examination",
        string? workflowName = null,
        string? subworkflowName = null,
        IReadOnlyList<string>? workflowNames = null)
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
            null,
            null,
            null,
            null,
            null,
            null,
            workflowName,
            subworkflowName,
            workflowNames);
    }

    private static InnolaTransactionRow FindRow(TransactionPanelState panel, string transactionNumber)
    {
        return panel.Rows.First(row => row.TransactionNumber.Equals(transactionNumber, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
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
        TestAssert.Equal(canViewDocuments, panel.CanShowSupportingDocuments, $"SD/Supporting Documents property mismatch {context}.");
        TestAssert.Equal(canViewDocuments, panel.ShowSupportingDocumentsCommand.CanExecute(null), $"SD/Supporting Documents command mismatch {context}.");
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

    private sealed class FixedExaminationNumberSpatialUnitService : IInnolaSpatialUnitService
    {
        private readonly string? examinationNumber;

        public FixedExaminationNumberSpatialUnitService(string? examinationNumber)
        {
            this.examinationNumber = examinationNumber;
        }

        public Task<InnolaSpatialUnitExaminationNumberResult> GetExaminationNumberAsync(
            InnolaSession session,
            SelectedInnolaTransaction transaction,
            string examinationFieldName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(examinationNumber)
                ? InnolaSpatialUnitExaminationNumberResult.Failed("SpatialUnit examinationNumber is missing.")
                : InnolaSpatialUnitExaminationNumberResult.Succeeded(examinationNumber));
        }

        public Task<InnolaSpatialUnitSaveResult> CreateOrUpdateAsync(
            InnolaSession session,
            SelectedInnolaTransaction transaction,
            string caseFolderPath,
            ComputeReviewDispositionDocument disposition,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaSpatialUnitSaveResult.Failed("Not used by transaction panel tests.", "not_used"));
        }
    }

    private sealed class RecordingCompleteLifecycleService : IInnolaTransactionLifecycleService
    {
        public int CompleteCalls { get; private set; }

        public InnolaTransactionLifecycleRequest? LastRequest { get; private set; }

        public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("in_progress", request.Session.User.Username, request.Session.User.DisplayName, "Claimed."));
        }

        public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("in_progress", request.Session.User.Username, request.Session.User.DisplayName, "Saved."));
        }

        public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            LastRequest = request;
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("completed", request.Session.User.Username, request.Session.User.DisplayName, "Completed."));
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

    private sealed class AppRtExaminationDetailService : IInnolaTransactionDetailService
    {
        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionDetailResult.Succeeded(new InnolaTransactionDetail(
                selectedTransaction.TransactionId,
                selectedTransaction.TransactionNumber,
                selectedTransaction.TaskId,
                selectedTransaction.TaskName,
                selectedTransaction.ProcessStep,
                "APP",
                "APP",
                selectedTransaction.AssignedUser,
                selectedTransaction.AssignedGroup,
                null,
                "in_progress",
                Array.Empty<InnolaAttachmentMetadata>())));
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaAttachmentContentResult.Failure("No attachment expected for RT Examination profile.", "unexpected"));
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
