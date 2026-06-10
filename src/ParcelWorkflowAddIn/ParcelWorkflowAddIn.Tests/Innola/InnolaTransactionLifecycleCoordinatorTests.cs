using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionLifecycleCoordinatorTests
{
    public static async Task ClaimRecordsOwnerInSessionManifestAndAudit()
    {
        using var tempRoot = new TempDirectory();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, new MockInnolaTransactionLifecycleService(), tempRoot.Path);

        var result = await coordinator.StartOrClaimAsync();

        TestAssert.True(result.Success, "Claim should succeed.");
        TestAssert.Equal(InnolaTransactionLifecycleStatus.InProgress, manager.LifecycleStatus, "Lifecycle state mismatch.");
        TestAssert.Equal("tester", manager.LifecycleOwnerUser, "Owner user mismatch.");
        var manifest = ManifestSerializer.Read(Path.Combine(manager.LoadedCaseFolderPath!, "manifest.json"));
        TestAssert.Equal("in_progress", manifest.Payload.InnolaLifecycle!.Status, "Manifest lifecycle status mismatch.");
        TestAssert.Equal("tester", manifest.Payload.InnolaLifecycle.ClaimedBy, "Manifest owner mismatch.");
        TestAssert.True(File.Exists(WorkflowLifecycleAuditService.GetAuditPath(CaseFolderLayout.FromRootDirectory(manager.LoadedCaseFolderPath!))), "Lifecycle audit should be written.");
        TestAssert.True(manager.CanOpenParcelWorkflow, "Parcel Workflow remains enabled after claim.");
    }

    public static async Task OwnershipConflictBlocksClaimAndSanitizesError()
    {
        using var tempRoot = new TempDirectory();
        var manager = await LoadedManager(tempRoot.Path, "task-100000005", "TR100000005");
        var coordinator = Coordinator(
            manager,
            new MockInnolaTransactionLifecycleService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["task-100000005"] = "other.user"
            }),
            tempRoot.Path);

        var result = await coordinator.StartOrClaimAsync();

        TestAssert.True(!result.Success, "Claim should fail for task owned by another user.");
        TestAssert.True(result.ErrorMessage!.Contains("another user", StringComparison.OrdinalIgnoreCase), "Ownership error should be clear.");
        TestAssert.True(!result.ErrorMessage.Contains("token", StringComparison.OrdinalIgnoreCase), "Ownership error should not leak token.");
        TestAssert.Equal(InnolaTransactionLifecycleStatus.Loaded, manager.LifecycleStatus, "Failed claim should preserve the loaded/not-started state.");
        TestAssert.True(!manager.HasActiveTransaction, "Failed claim should not make the transaction active.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Failed claim should not enable Parcel Workflow.");
    }

    public static async Task SaveProgressPersistsLifecycleAndDoesNotComplete()
    {
        using var tempRoot = new TempDirectory();
        var service = new CountingLifecycleService();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, service, tempRoot.Path);

        await coordinator.StartOrClaimAsync();
        var result = await coordinator.SaveProgressAsync();

        TestAssert.True(result.Success, "Save progress should succeed.");
        TestAssert.Equal(1, service.SaveCalls, "Save-state service should be called.");
        TestAssert.Equal(0, service.CompleteCalls, "Complete service must not be called during save.");
        var manifest = ManifestSerializer.Read(Path.Combine(manager.LoadedCaseFolderPath!, "manifest.json"));
        TestAssert.Equal("in_progress", manifest.Payload.InnolaLifecycle!.Status, "Saved lifecycle should remain in progress.");
        TestAssert.True(!string.IsNullOrWhiteSpace(manifest.Payload.InnolaLifecycle.LastSavedAt), "Last saved timestamp should be recorded.");
    }

    public static async Task CancelClearsActiveGateAndDoesNotComplete()
    {
        using var tempRoot = new TempDirectory();
        var service = new CountingLifecycleService();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, service, tempRoot.Path);

        await coordinator.StartOrClaimAsync();
        var caseFolderPath = manager.LoadedCaseFolderPath!;
        var result = coordinator.CancelActiveProcess();

        TestAssert.True(result.Success, "Cancel should succeed locally.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Cancel should disable Parcel Workflow.");
        TestAssert.Equal(0, service.CompleteCalls, "Cancel must not call Complete.");
        var manifest = ManifestSerializer.Read(Path.Combine(caseFolderPath, "manifest.json"));
        TestAssert.Equal("cancelled", manifest.Payload.InnolaLifecycle!.Status, "Manifest should record cancelled status.");
    }

    public static async Task CompleteIsBlockedUntilReadinessPasses()
    {
        using var tempRoot = new TempDirectory();
        var service = new CountingLifecycleService();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, service, tempRoot.Path, new FakeReadiness(false));

        await coordinator.StartOrClaimAsync();
        var result = await coordinator.CompleteAsync();

        TestAssert.True(!result.Success, "Complete should be blocked without readiness.");
        TestAssert.Equal(0, service.CompleteCalls, "Complete service should not be called when readiness is blocked.");
        TestAssert.Equal(InnolaTransactionLifecycleStatus.CompleteBlocked, manager.LifecycleStatus, "Session should show complete blocked.");
        var manifest = ManifestSerializer.Read(Path.Combine(manager.LoadedCaseFolderPath!, "manifest.json"));
        TestAssert.True(!manifest.Payload.InnolaLifecycle!.CompletionReady, "Manifest readiness should be false.");
    }

    public static async Task CompleteSuccessRecordsAuditAndClearsActiveGate()
    {
        using var tempRoot = new TempDirectory();
        var service = new CountingLifecycleService();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, service, tempRoot.Path, new FakeReadiness(true));

        await coordinator.StartOrClaimAsync();
        var caseFolderPath = manager.LoadedCaseFolderPath!;
        var result = await coordinator.CompleteAsync();

        TestAssert.True(result.Success, "Complete should succeed when ready and owned by current user.");
        TestAssert.Equal(1, service.CompleteCalls, "Complete service should be called once.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Complete should clear active workflow gates.");
        var manifest = ManifestSerializer.Read(Path.Combine(caseFolderPath, "manifest.json"));
        TestAssert.Equal("completed", manifest.Payload.InnolaLifecycle!.Status, "Manifest should record completed status.");
        TestAssert.Equal("tester", manifest.Payload.InnolaLifecycle.CompletedBy, "Manifest completed user mismatch.");
    }

    public static async Task LifecycleFailuresPreserveStateAndRedactSecrets()
    {
        using var tempRoot = new TempDirectory();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, new ThrowingLifecycleService(), tempRoot.Path);
        var selected = manager.SelectedTransaction;
        var loadedPath = manager.LoadedCaseFolderPath;

        var result = await coordinator.StartOrClaimAsync();

        TestAssert.True(!result.Success, "Thrown lifecycle failure should fail safely.");
        TestAssert.Equal(selected, manager.SelectedTransaction, "Failure should preserve selected transaction.");
        TestAssert.Equal(loadedPath, manager.LoadedCaseFolderPath, "Failure should preserve loaded Case Folder.");
        TestAssert.True(!result.ErrorMessage!.Contains("secret-password", StringComparison.OrdinalIgnoreCase), "Password must be redacted.");
        TestAssert.True(!result.ErrorMessage.Contains("token", StringComparison.OrdinalIgnoreCase), "Token must be redacted.");
    }

    public static async Task LiveLifecycleAdapterFailsUntilEndpointsAreConfigured()
    {
        using var tempRoot = new TempDirectory();
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, new InnolaTransactionLifecycleService(), tempRoot.Path);

        var result = await coordinator.StartOrClaimAsync();

        TestAssert.True(!result.Success, "Live lifecycle adapter should not report success until real endpoints are configured.");
        TestAssert.True(result.ErrorMessage!.Contains("not configured", StringComparison.OrdinalIgnoreCase), "Not-configured lifecycle error should be clear.");
        TestAssert.True(!manager.CanSaveProgress, "Failed live claim should not make the transaction owned/in progress.");
    }

    private static async Task<InnolaSessionManager> LoadedManager(string outputRoot, string taskId = "task-100000004", string transactionNumber = "TR100000004")
    {
        var manager = LoggedInManager();
        manager.SelectTransaction(Row(taskId, transactionNumber), FixedNow());
        var loader = new InnolaTransactionLoadService(
            manager,
            new MockInnolaTransactionDetailService(),
            new CaseFolderStore(() => FixedNow(), () => "run-lifecycle"),
            new AttachmentSourceFileWriter(() => FixedNow()),
            new SourceInputProfileDetector(() => FixedNow()),
            () => outputRoot,
            () => FixedNow());

        var loaded = await loader.LoadSelectedTransactionAsync();
        TestAssert.True(loaded.Success, "Test setup should load a transaction.");
        return manager;
    }

    private static InnolaTransactionLifecycleCoordinator Coordinator(
        InnolaSessionManager manager,
        IInnolaTransactionLifecycleService lifecycleService,
        string outputRoot,
        ITransactionCompletionReadinessService? readiness = null)
    {
        return new InnolaTransactionLifecycleCoordinator(
            manager,
            lifecycleService,
            readiness ?? new DefaultTransactionCompletionReadinessService(),
            new WorkflowLifecycleAuditService(() => FixedNow()),
            () => FixedNow());
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

    private static InnolaTransactionRow Row(string taskId, string transactionNumber)
    {
        return new InnolaTransactionRow(
            taskId,
            transactionNumber.TrimStart('T', 'R'),
            transactionNumber,
            "Computation Check",
            "parcel_workflow",
            InnolaTransactionStatus.Available,
            "John Johnson",
            "tester",
            "survey",
            FixedNow(),
            true,
            true,
            null,
            null);
    }

    private static DateTimeOffset FixedNow()
    {
        return new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class CountingLifecycleService : IInnolaTransactionLifecycleService
    {
        public int SaveCalls { get; private set; }

        public int CompleteCalls { get; private set; }

        public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("in_progress", request.Session.User.Username, request.Session.User.DisplayName, "Claimed."));
        }

        public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("in_progress", request.Session.User.Username, request.Session.User.DisplayName, "Saved."));
        }

        public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            return Task.FromResult(InnolaTransactionLifecycleResult.Succeeded("completed", request.Session.User.Username, request.Session.User.DisplayName, "Completed."));
        }
    }

    private sealed class ThrowingLifecycleService : IInnolaTransactionLifecycleService
    {
        public Task<InnolaTransactionLifecycleResult> ClaimAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("token secret-password {raw}");
        }

        public Task<InnolaTransactionLifecycleResult> SaveProgressAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("token secret-password {raw}");
        }

        public Task<InnolaTransactionLifecycleResult> CompleteAsync(InnolaTransactionLifecycleRequest request, CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("token secret-password {raw}");
        }
    }

    private sealed class FakeReadiness : ITransactionCompletionReadinessService
    {
        private readonly bool isReady;

        public FakeReadiness(bool isReady)
        {
            this.isReady = isReady;
        }

        public TransactionCompletionReadinessResult CheckReadiness(string caseFolderPath)
        {
            return isReady
                ? TransactionCompletionReadinessResult.Ready()
                : TransactionCompletionReadinessResult.Blocked("sync_readiness_not_met", "Complete is blocked until downstream sync/readiness criteria are met.");
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
