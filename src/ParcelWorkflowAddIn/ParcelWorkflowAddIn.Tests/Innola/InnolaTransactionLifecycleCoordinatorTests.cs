using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.WorkflowRules;

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

    public static async Task SaveAndCloseUploadsResumePackageAndPersistsProgress()
    {
        using var tempRoot = new TempDirectory();
        var detailService = new MockInnolaTransactionDetailService();
        var lifecycleService = new CountingLifecycleService();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "TR100000004"), FixedNow());
        var loader = new InnolaTransactionLoadService(
            manager,
            detailService,
            new CaseFolderStore(() => FixedNow(), () => "run-lifecycle"),
            new AttachmentSourceFileWriter(() => FixedNow()),
            new SourceInputProfileDetector(() => FixedNow()),
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new CaseResumePackageService(() => FixedNow(), () => "test"),
            () => tempRoot.Path,
            () => FixedNow());

        var loaded = await loader.LoadSelectedTransactionAsync();
        TestAssert.True(loaded.Success, "Test setup should load a transaction.");

        var coordinator = new InnolaTransactionLifecycleCoordinator(
            manager,
            detailService,
            lifecycleService,
            new MockInnolaSpatialUnitService(),
            new DefaultTransactionCompletionReadinessService(),
            new WorkflowLifecycleAuditService(() => FixedNow()),
            new CaseResumePackageService(() => FixedNow(), () => "test"),
            () => FixedNow());

        await coordinator.StartOrClaimAsync();
        File.WriteAllText(Path.Combine(manager.LoadedCaseFolderPath!, "working", "approved_review.json"), "{\"status\":\"approved\"}");

        var result = await coordinator.SaveAndCloseAsync();

        TestAssert.True(result.Success, "Save and close should succeed.");
        TestAssert.Equal(1, lifecycleService.SaveCalls, "Save and close should persist lifecycle progress.");
        var detail = await detailService.GetTransactionDetailAsync(manager.CurrentSession!, manager.SelectedTransaction!);
        var resumeAttachment = detail.Detail!.Attachments.FirstOrDefault(attachment => InnolaResumePackageConventions.IsResumePackageAttachment(attachment, "TR100000004"));
        TestAssert.True(resumeAttachment is not null, "Resume package attachment should be uploaded.");
        var content = await detailService.GetAttachmentContentAsync(manager.CurrentSession!, detail.Detail, resumeAttachment!);
        TestAssert.True(content.Success && content.Content.Length > 0, "Uploaded resume package should be downloadable.");
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

    public static async Task CompleteCreatesSpatialUnitAndPersistsReturnedIdBeforeLifecycleComplete()
    {
        using var tempRoot = new TempDirectory();
        var service = new CountingLifecycleService();
        var spatialUnits = new RecordingSpatialUnitService(InnolaSpatialUnitSaveResult.Succeeded("su-100000004"));
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = Coordinator(manager, service, tempRoot.Path, new FakeReadiness(true), spatialUnits);

        await coordinator.StartOrClaimAsync();
        var layout = CaseFolderLayout.FromRootDirectory(manager.LoadedCaseFolderPath!);
        WriteDisposition(layout);

        var result = await coordinator.CompleteAsync();

        TestAssert.True(result.Success, "Complete should succeed when Spatial Unit save succeeds.");
        TestAssert.Equal(1, spatialUnits.CallCount, "Spatial Unit service should be called once.");
        TestAssert.Equal(1, service.CompleteCalls, "Lifecycle complete should run after Spatial Unit save.");
        var disposition = new ComputeReviewDispositionPersistenceService().Load(layout);
        TestAssert.Equal("saved", disposition?.SpatialUnitApiStatus, "Disposition should persist Spatial Unit API status.");
        TestAssert.Equal("su-100000004", disposition?.SpatialUnitId, "Disposition should persist returned Spatial Unit id.");
        TestAssert.Equal(InnolaResumePackageConventions.BuildCompletedAttachmentFileName("TR100000004"), disposition?.WorkingPackageFileName, "Disposition should persist completed package file name.");
        TestAssert.Equal(ShellState.CompletedAttachmentSourceType, disposition?.WorkingPackageSourceType, "Disposition should persist completed package source type.");
        TestAssert.Equal("uploaded", disposition?.WorkingPackageUploadStatus, "Disposition should persist completed package upload status.");
        var audit = File.ReadAllText(WorkflowLifecycleAuditService.GetAuditPath(layout));
        TestAssert.True(audit.Contains("compute_spatial_unit_saved", StringComparison.OrdinalIgnoreCase), "Audit should record Spatial Unit save.");
        TestAssert.True(audit.Contains("compute_working_package_uploaded", StringComparison.OrdinalIgnoreCase), "Audit should record package upload.");
    }

    public static async Task CompleteStopsBeforePackageUploadAndLifecycleCompleteWhenSpatialUnitFails()
    {
        using var tempRoot = new TempDirectory();
        var detailService = new MockInnolaTransactionDetailService();
        var lifecycleService = new CountingLifecycleService();
        var spatialUnits = new RecordingSpatialUnitService(InnolaSpatialUnitSaveResult.Failed("Could not create Spatial Unit. Try again.", "spatial_unit_unavailable"));
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = new InnolaTransactionLifecycleCoordinator(
            manager,
            detailService,
            lifecycleService,
            spatialUnits,
            new FakeReadiness(true),
            new WorkflowLifecycleAuditService(() => FixedNow()),
            new CaseResumePackageService(() => FixedNow(), () => "test"),
            () => FixedNow());

        await coordinator.StartOrClaimAsync();
        var layout = CaseFolderLayout.FromRootDirectory(manager.LoadedCaseFolderPath!);
        WriteDisposition(layout);
        var before = await detailService.GetTransactionDetailAsync(manager.CurrentSession!, manager.SelectedTransaction!);
        var beforeCount = before.Detail!.Attachments.Count;

        var result = await coordinator.CompleteAsync();

        TestAssert.True(!result.Success, "Complete should fail when Spatial Unit save fails.");
        TestAssert.Equal(1, spatialUnits.CallCount, "Spatial Unit service should be called once.");
        TestAssert.Equal(0, lifecycleService.CompleteCalls, "Lifecycle complete must not run after Spatial Unit failure.");
        var after = await detailService.GetTransactionDetailAsync(manager.CurrentSession!, manager.SelectedTransaction!);
        TestAssert.Equal(beforeCount, after.Detail!.Attachments.Count, "Completed package must not upload after Spatial Unit failure.");
        var disposition = new ComputeReviewDispositionPersistenceService().Load(layout);
        TestAssert.Equal(null, disposition?.SpatialUnitApiStatus, "Failed Spatial Unit save must not be recorded as saved.");
        var audit = File.ReadAllText(WorkflowLifecycleAuditService.GetAuditPath(layout));
        TestAssert.True(audit.Contains("compute_spatial_unit_save_failed", StringComparison.OrdinalIgnoreCase), "Audit should record Spatial Unit failure.");
    }

    public static async Task CompleteStopsBeforeLifecycleCompleteWhenWorkingPackageUploadFails()
    {
        using var tempRoot = new TempDirectory();
        var detailService = new FailingUploadDetailService();
        var lifecycleService = new CountingLifecycleService();
        var spatialUnits = new RecordingSpatialUnitService(InnolaSpatialUnitSaveResult.Succeeded("su-100000004"));
        var manager = await LoadedManager(tempRoot.Path);
        var coordinator = new InnolaTransactionLifecycleCoordinator(
            manager,
            detailService,
            lifecycleService,
            spatialUnits,
            new FakeReadiness(true),
            new WorkflowLifecycleAuditService(() => FixedNow()),
            new CaseResumePackageService(() => FixedNow(), () => "test"),
            () => FixedNow());

        await coordinator.StartOrClaimAsync();
        var layout = CaseFolderLayout.FromRootDirectory(manager.LoadedCaseFolderPath!);
        WriteDisposition(layout);

        var result = await coordinator.CompleteAsync();

        TestAssert.True(!result.Success, "Complete should fail when package upload fails.");
        TestAssert.Equal(1, spatialUnits.CallCount, "Spatial Unit save should run before package upload.");
        TestAssert.Equal(0, lifecycleService.CompleteCalls, "Lifecycle complete must not run after package upload failure.");
        var disposition = new ComputeReviewDispositionPersistenceService().Load(layout);
        TestAssert.Equal("saved", disposition?.SpatialUnitApiStatus, "Spatial Unit status should remain saved after later package failure.");
        TestAssert.Equal("failed", disposition?.WorkingPackageUploadStatus, "Disposition should persist package upload failure.");
        TestAssert.Equal(InnolaResumePackageConventions.BuildCompletedAttachmentFileName("TR100000004"), disposition?.WorkingPackageFileName, "Package file name should be persisted even when upload fails.");
        var audit = File.ReadAllText(WorkflowLifecycleAuditService.GetAuditPath(layout));
        TestAssert.True(audit.Contains("compute_working_package_upload_failed", StringComparison.OrdinalIgnoreCase), "Audit should record package upload failure.");
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
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new CaseResumePackageService(() => FixedNow(), () => "test"),
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
        ITransactionCompletionReadinessService? readiness = null,
        IInnolaSpatialUnitService? spatialUnitService = null)
    {
        return new InnolaTransactionLifecycleCoordinator(
            manager,
            new MockInnolaTransactionDetailService(),
            lifecycleService,
            spatialUnitService ?? new MockInnolaSpatialUnitService(),
            readiness ?? new DefaultTransactionCompletionReadinessService(),
            new WorkflowLifecycleAuditService(() => FixedNow()),
            new CaseResumePackageService(() => FixedNow(), () => "test"),
            () => FixedNow());
    }

    private static void WriteDisposition(CaseFolderLayout layout)
    {
        var document = new ComputeReviewDispositionDocument(
            "compute_review_disposition_v1",
            "100000004",
            "TR100000004",
            "task-100000004",
            "approved",
            "Approved for compute closeout.",
            "tester",
            FixedNow().UtcDateTime.ToString("O"),
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json"),
            "run-output",
            "written",
            Path.Combine(layout.OutputDirectory, "enterprise_working_disposition.json"),
            null,
            null,
            null,
            null,
            null);
        new ComputeReviewDispositionPersistenceService().Save(layout, document);
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
            "Plan Examination",
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

    private sealed class RecordingSpatialUnitService : IInnolaSpatialUnitService
    {
        private readonly InnolaSpatialUnitSaveResult result;

        public RecordingSpatialUnitService(InnolaSpatialUnitSaveResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public Task<InnolaSpatialUnitSaveResult> CreateOrUpdateAsync(
            InnolaSession session,
            SelectedInnolaTransaction transaction,
            string caseFolderPath,
            ComputeReviewDispositionDocument disposition,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FailingUploadDetailService : IInnolaTransactionDetailService
    {
        private readonly MockInnolaTransactionDetailService inner = new();

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            return inner.GetTransactionDetailAsync(session, selectedTransaction, cancellationToken);
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            return inner.GetAttachmentContentAsync(session, detail, attachment, cancellationToken);
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
            return Task.FromResult(InnolaAttachmentUploadResult.Failure("Could not upload completed case package. Try again.", "upload_failed"));
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
