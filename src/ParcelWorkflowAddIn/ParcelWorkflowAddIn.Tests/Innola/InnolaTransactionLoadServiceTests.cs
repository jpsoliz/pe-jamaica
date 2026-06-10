using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionLoadServiceTests
{
    public static async Task SuccessfulMockLoadCreatesCaseFolderAndEnablesParcelWorkflow()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, new MockInnolaTransactionDetailService(), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(result.Success, "Mock transaction load should succeed.");
        TestAssert.True(manager.CanOpenParcelWorkflow, "Parcel Workflow should be enabled after load success.");
        TestAssert.Equal("TR100000004", manager.LoadedTransactionNumber, "Loaded transaction mismatch.");
        TestAssert.True(File.Exists(Path.Combine(result.Layout!.RootDirectory, "manifest.json")), "Manifest should exist.");

        var manifest = ManifestSerializer.Read(result.Layout.ManifestPath);
        TestAssert.Equal("TR100000004", manifest.TransactionId, "Manifest transaction id mismatch.");
        TestAssert.Equal("task-100000004", manifest.Payload.InnolaTransaction!.TaskId, "Innola task id mismatch.");
        TestAssert.Equal("tester", manifest.Payload.InnolaTransaction.SelectedUser, "Selected user mismatch.");
        TestAssert.Equal(4, manifest.Payload.AttachmentProvenance!.Count, "Attachment provenance count mismatch.");
        TestAssert.True(manifest.Payload.SourceFiles.Count >= 4, "Source files should include copied attachments.");
        TestAssert.Equal(SourceInputProfile.ScenarioB, manifest.Payload.DetectedProfile!.ProfileCode, "Profile should detect scenario B.");
        TestAssert.True(manifest.Payload.AttachmentProvenance.All(item => File.Exists(item.CopiedPath)), "Copied attachment paths should exist.");

        var caseText = File.ReadAllText(manifest.Payload.AttachmentProvenance[0].CopiedPath);
        TestAssert.True(!File.ReadAllText(result.Layout.ManifestPath).Contains("token-abc", StringComparison.OrdinalIgnoreCase), "Manifest must not contain access token.");
        TestAssert.True(!File.ReadAllText(result.Layout.ManifestPath).Contains("secret-password", StringComparison.OrdinalIgnoreCase), "Manifest must not contain session password.");
        TestAssert.True(caseText.Contains("PDF", StringComparison.OrdinalIgnoreCase), "Fixture content should be written.");
    }

    public static async Task LoadWithoutLoggedInSessionFailsWithoutEnablingParcelWorkflow()
    {
        using var tempRoot = new TempDirectory();
        var manager = new InnolaSessionManager(new FakeAuthService());
        var detailService = new CountingDetailService();
        var service = LoadService(manager, detailService, tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Load without login should fail.");
        TestAssert.Equal(0, detailService.DetailCalls, "Detail service should not be called without login.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled.");
    }

    public static async Task LoadWithoutSelectedTransactionFailsWithoutCallingDetailService()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        var detailService = new CountingDetailService();
        var service = LoadService(manager, detailService, tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Load without selected transaction should fail.");
        TestAssert.Equal(0, detailService.DetailCalls, "Detail service should not be called without selection.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled.");
    }

    public static async Task DetailMismatchFailsSafely()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var detail = Detail("task-other", "100000999", "TR100000999", "Computation Check", DefaultAttachments());
        var service = LoadService(manager, new CountingDetailService(detail), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Mismatched detail should fail.");
        TestAssert.True(!Directory.Exists(Path.Combine(tempRoot.Path, "TR100000004")), "Mismatch should not create selected transaction folder.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled.");
    }

    public static async Task UnsupportedAttachmentExtensionBlocksLoad()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var badAttachment = new InnolaAttachmentMetadata("att-bad", "script.exe", ".exe", "application/octet-stream", null, "bad", 4, null, "mock-attachment:att-bad", true);
        var service = LoadService(manager, new CountingDetailService(Detail("task-100000004", "100000004", "TR100000004", "Computation Check", new[] { badAttachment })), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Unsupported attachment should block load.");
        TestAssert.True(result.ErrorMessage!.Contains("Unsupported attachment file type", StringComparison.OrdinalIgnoreCase), "Unsupported extension error should be clear.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled after attachment failure.");
    }

    public static async Task AttachmentFileNameTraversalBlocksLoad()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var badAttachment = new InnolaAttachmentMetadata("att-path", "..\\escape.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 4, null, "mock-attachment:att-path", true);
        var service = LoadService(manager, new CountingDetailService(Detail("task-100000004", "100000004", "TR100000004", "Computation Check", new[] { badAttachment })), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Path traversal attachment should block load.");
        TestAssert.True(!File.Exists(Path.Combine(tempRoot.Path, "escape.pdf")), "Path traversal should not write outside the Case Folder.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled after unsafe file name.");
    }

    public static async Task DuplicateAttachmentNamesDoNotOverwriteExistingFiles()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var duplicateAttachments = new[]
        {
            new InnolaAttachmentMetadata("att-one", "plan.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 4, null, "mock-attachment:att-one", true),
            new InnolaAttachmentMetadata("att-two", "plan.pdf", ".pdf", "application/pdf", SourceRole.ComputationSource, "computation", 4, null, "mock-attachment:att-two", true)
        };
        var service = LoadService(manager, new CountingDetailService(Detail("task-100000004", "100000004", "TR100000004", "Computation Check", duplicateAttachments)), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(result.Success, "Duplicate safe names should load with unique copied names.");
        var manifest = ManifestSerializer.Read(result.Layout!.ManifestPath);
        var copiedNames = manifest.Payload.AttachmentProvenance!.Select(item => Path.GetFileName(item.CopiedPath)).ToArray();
        TestAssert.True(copiedNames.Contains("plan.pdf"), "First duplicate name should be preserved.");
        TestAssert.True(copiedNames.Contains("plan_2.pdf"), "Second duplicate name should be made unique.");
    }

    public static async Task ExistingCaseFolderForSameTransactionReopensWithoutDuplicatingAttachments()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, new MockInnolaTransactionDetailService(), tempRoot.Path);

        var first = await service.LoadSelectedTransactionAsync();
        var second = await service.LoadSelectedTransactionAsync();

        TestAssert.True(first.Success && second.Success, "Existing same transaction Case Folder should reopen.");
        var manifest = ManifestSerializer.Read(second.Layout!.ManifestPath);
        TestAssert.Equal(4, manifest.Payload.AttachmentProvenance!.Count, "Reopen should not duplicate provenance.");
    }

    public static async Task ExistingCaseFolderMismatchBlocksLoad()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => FixedNow(), () => "run-existing");
        var created = store.CreateCase(tempRoot.Path, "TR100000004", "tester");
        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);
        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with
        {
            Payload = manifest.Payload with
            {
                InnolaTransaction = new ManifestInnolaTransaction(
                    "other",
                    "TR100000004",
                    "task-other",
                    "Other Task",
                    "parcel_workflow",
                    null,
                    null,
                    "tester",
                    null,
                    null,
                    null,
                    null,
                    FixedNow().UtcDateTime.ToString("O"))
            }
        });
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, new MockInnolaTransactionDetailService(), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Existing mismatched Case Folder should block load.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled.");
    }

    public static void ManifestWithoutInnolaMetadataDeserializes()
    {
        using var tempRoot = new TempDirectory();
        var store = new CaseFolderStore(() => FixedNow(), () => "run-old");
        var created = store.CreateCase(tempRoot.Path, "TR-SMD-0000001", "tester");

        var manifest = ManifestSerializer.Read(created.Layout!.ManifestPath);

        TestAssert.Equal(null, manifest.Payload.InnolaTransaction, "Old manifest should not require Innola metadata.");
        TestAssert.Equal(null, manifest.Payload.AttachmentProvenance, "Old manifest should not require provenance.");
    }

    private static InnolaTransactionLoadService LoadService(
        InnolaSessionManager manager,
        IInnolaTransactionDetailService detailService,
        string outputRoot)
    {
        return new InnolaTransactionLoadService(
            manager,
            detailService,
            new CaseFolderStore(() => FixedNow(), () => "run-load"),
            new AttachmentSourceFileWriter(() => FixedNow()),
            new SourceInputProfileDetector(() => FixedNow()),
            () => outputRoot,
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

    private static InnolaTransactionRow Row(string taskId, string transactionId, string transactionNumber, string taskName)
    {
        return new InnolaTransactionRow(
            taskId,
            transactionId,
            transactionNumber,
            taskName,
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

    private static InnolaTransactionDetail Detail(
        string taskId,
        string transactionId,
        string transactionNumber,
        string taskName,
        IReadOnlyList<InnolaAttachmentMetadata> attachments)
    {
        return new InnolaTransactionDetail(
            transactionId,
            transactionNumber,
            taskId,
            taskName,
            "parcel_workflow",
            "parcel_workflow",
            "scenario_b",
            "tester",
            "survey",
            null,
            "available",
            attachments);
    }

    private static IReadOnlyList<InnolaAttachmentMetadata> DefaultAttachments()
    {
        return new[]
        {
            new InnolaAttachmentMetadata("att-plan", "plan.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 4, null, "mock-attachment:att-plan", true)
        };
    }

    private static DateTimeOffset FixedNow()
    {
        return new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class CountingDetailService : IInnolaTransactionDetailService
    {
        private readonly InnolaTransactionDetail? detail;

        public CountingDetailService(InnolaTransactionDetail? detail = null)
        {
            this.detail = detail;
        }

        public int DetailCalls { get; private set; }

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Task.FromResult(detail is null
                ? InnolaTransactionDetailResult.Failure("No detail.", "not_found")
                : InnolaTransactionDetailResult.Succeeded(detail));
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
