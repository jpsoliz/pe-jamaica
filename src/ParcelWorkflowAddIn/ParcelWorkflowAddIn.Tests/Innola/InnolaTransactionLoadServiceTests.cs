using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.WorkflowRules;
using System.IO.Compression;

namespace ParcelWorkflowAddIn.Tests.Innola;

internal static class InnolaTransactionLoadServiceTests
{
    public static async Task SuccessfulMockLoadCreatesCaseFolderAndKeepsParcelWorkflowDisabledUntilClaim()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, new MockInnolaTransactionDetailService(), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(result.Success, "Mock transaction load should succeed.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should stay disabled until the transaction is started/claimed.");
        TestAssert.Equal("TR100000004", manager.LoadedTransactionNumber, "Loaded transaction mismatch.");
        TestAssert.True(File.Exists(Path.Combine(result.Layout!.RootDirectory, "manifest.json")), "Manifest should exist.");

        var manifest = ManifestSerializer.Read(result.Layout.ManifestPath);
        TestAssert.Equal("TR100000004", manifest.TransactionId, "Manifest transaction id mismatch.");
        TestAssert.Equal("task-100000004", manifest.Payload.InnolaTransaction!.TaskId, "Innola task id mismatch.");
        TestAssert.Equal("tester", manifest.Payload.InnolaTransaction.SelectedUser, "Selected user mismatch.");
        TestAssert.Equal(2, manifest.Payload.AttachmentProvenance!.Count, "Attachment provenance count mismatch.");
        TestAssert.True(manifest.Payload.SourceFiles.Count >= 2, "Source files should include copied attachments.");
        TestAssert.Equal(SourceInputProfile.ScenarioA, manifest.Payload.DetectedProfile!.ProfileCode, "Profile should detect scenario A.");
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

    public static async Task LaterAttachmentFailureCleansPreviouslyWrittenFiles()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var attachments = new[]
        {
            new InnolaAttachmentMetadata("att-one", "plan.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 4, null, "mock-attachment:att-one", true),
            new InnolaAttachmentMetadata("att-two", "points.csv", ".csv", "text/csv", SourceRole.PointsComputation, "points", 4, null, "mock-attachment:att-two", true)
        };
        var service = LoadService(
            manager,
            new FailingSecondAttachmentService(Detail("task-100000004", "100000004", "TR100000004", "Computation Check", attachments)),
            tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Load should fail when a later attachment fails.");
        var sourceDirectory = Path.Combine(tempRoot.Path, "TR100000004", "source");
        TestAssert.True(!Directory.Exists(sourceDirectory) || Directory.GetFiles(sourceDirectory).Length == 0, "Previously written attachment files should be cleaned up after failed load.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled after partial attachment failure.");
    }

    public static async Task DetailAdapterExceptionReturnsRetryableNonSecretError()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, new ThrowingDetailService(), tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(!result.Success, "Thrown adapter failure should be converted to a failed load result.");
        TestAssert.Equal("Could not load transaction. Try again.", result.ErrorMessage, "Adapter exception should return a safe retryable message.");
        TestAssert.True(!manager.CanOpenParcelWorkflow, "Parcel Workflow should remain disabled after adapter exception.");
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
        TestAssert.Equal(2, manifest.Payload.AttachmentProvenance!.Count, "Reopen should not duplicate provenance.");
    }

    public static async Task ResumePackageRestoresSavedWorkflowState()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        var detailService = new MockInnolaTransactionDetailService();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, detailService, tempRoot.Path);

        var first = await service.LoadSelectedTransactionAsync();
        TestAssert.True(first.Success, "Initial load should succeed.");

        var layout = first.Layout!;
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(layout.ManifestPath, manifest with
        {
            Payload = manifest.Payload with
            {
                WorkflowState = "review_approved"
            }
        });
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "approved_review.json"), "{\"review_data_hash\":\"abc\"}");

        var resumeService = new CaseResumePackageService(() => FixedNow(), () => "test");
        var package = resumeService.Build(layout, manager.SelectedTransaction!, "tester");
        TestAssert.True(package.Success, "Resume package should build.");
        var packageBytes = await File.ReadAllBytesAsync(package.PackagePath!);
        var upload = await detailService.UploadAttachmentAsync(
            manager.CurrentSession!,
            manager.SelectedTransaction!,
            package.AttachmentFileName!,
            package.ContentType!,
            packageBytes,
            InnolaResumePackageConventions.ResumeSourceType);
        TestAssert.True(upload.Success, "Resume package should upload to mock detail service.");

        Directory.Delete(layout.RootDirectory, recursive: true);
        manager.ClearLoadedTransaction();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());

        var second = await service.LoadSelectedTransactionAsync();

        TestAssert.True(second.Success, $"Load from resume package should succeed. Error: {second.ErrorMessage}");
        TestAssert.True(second.StatusMessage!.Contains("Restored from saved case", StringComparison.OrdinalIgnoreCase), "Status should indicate saved-case restore.");
        var reopenedSession = new global::ParcelWorkflowAddIn.Workflow.WorkflowSession(new CaseFolderStore());
        var reopen = reopenedSession.ReopenCaseFolder(second.Layout!.RootDirectory);
        TestAssert.True(reopen.Success, "Restored case folder should reopen.");
        TestAssert.Equal(global::ParcelWorkflowAddIn.Workflow.WorkflowState.ReviewApproved, reopen.ResolvedState, "Saved workflow state should be restored.");
        TestAssert.True(File.Exists(Path.Combine(second.Layout.WorkingDirectory, "approved_review.json")), "Saved review artifact should be restored.");
    }

    public static async Task ResumePackageRestorePrefersNewestSavedPackageWhenMultipleExist()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());

        var firstLayout = BuildResumeCaseFolder(tempRoot.Path, "TR100000004", "intake", "old", new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        var secondLayout = BuildResumeCaseFolder(tempRoot.Path, "TR100000004", "review_approved", "new", new DateTimeOffset(2026, 6, 10, 13, 0, 0, TimeSpan.Zero));

        var oldPackage = BuildResumePackage(firstLayout, manager.SelectedTransaction!, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        var newPackage = BuildResumePackage(secondLayout, manager.SelectedTransaction!, new DateTimeOffset(2026, 6, 10, 13, 0, 0, TimeSpan.Zero));

        var attachments = new[]
        {
            new InnolaAttachmentMetadata("resume-old", "sidwell-case-state-TR100000004.zip", ".zip", "application/zip", null, InnolaResumePackageConventions.ResumeSourceType, null, null, "mock-attachment:resume-old", true),
            new InnolaAttachmentMetadata("resume-new", "sidwell-case-state-TR100000004.zip", ".zip", "application/zip", null, InnolaResumePackageConventions.ResumeSourceType, null, null, "mock-attachment:resume-new", true)
        };

        var detail = Detail("task-100000004", "100000004", "TR100000004", "Computation Check", attachments);
        var service = LoadService(
            manager,
            new MultiResumeAttachmentDetailService(
                detail,
                new Dictionary<string, byte[]>
                {
                    ["resume-old"] = oldPackage,
                    ["resume-new"] = newPackage
                }),
            tempRoot.Path);

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(result.Success, "Latest saved resume package should restore successfully.");
        var reopenedSession = new global::ParcelWorkflowAddIn.Workflow.WorkflowSession(new CaseFolderStore());
        var reopen = reopenedSession.ReopenCaseFolder(result.Layout!.RootDirectory);
        TestAssert.True(reopen.Success, "Restored latest case folder should reopen.");
        TestAssert.Equal(global::ParcelWorkflowAddIn.Workflow.WorkflowState.ReviewApproved, reopen.ResolvedState, "Newest saved workflow state should win.");
        TestAssert.True(File.Exists(Path.Combine(result.Layout.WorkingDirectory, "new.txt")), "Newest resume package artifacts should be restored.");
        TestAssert.True(!File.Exists(Path.Combine(result.Layout.WorkingDirectory, "old.txt")), "Older resume package artifacts should not be restored.");
    }

    public static async Task ResumePackageExcludesHeavyOutputArtifactsButKeepsWorkingState()
    {
        using var tempRoot = new TempDirectory();
        var manager = LoggedInManager();
        var detailService = new MockInnolaTransactionDetailService();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var service = LoadService(manager, detailService, tempRoot.Path);

        var first = await service.LoadSelectedTransactionAsync();
        TestAssert.True(first.Success, "Initial load should succeed.");

        var layout = first.Layout!;
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, "approved_review.json"), "{\"review_data_hash\":\"abc\"}");
        Directory.CreateDirectory(layout.OutputDirectory);
        File.WriteAllText(Path.Combine(layout.OutputDirectory, "output_summary.json"), "{\"status\":\"ready\"}");
        var gdbDirectory = Path.Combine(layout.OutputDirectory, "parcel_output.gdb");
        Directory.CreateDirectory(gdbDirectory);
        File.WriteAllText(Path.Combine(gdbDirectory, "a00000001.gdbtable"), "large-ish binary placeholder");

        var resumeService = new CaseResumePackageService(() => FixedNow(), () => "test");
        var package = resumeService.Build(layout, manager.SelectedTransaction!, "tester");
        TestAssert.True(package.Success, "Resume package should build.");

        using var archive = new ZipArchive(File.OpenRead(package.PackagePath!), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
        TestAssert.True(names.Contains("manifest.json"), "Resume package should keep manifest.");
        TestAssert.True(names.Contains("working/approved_review.json"), "Resume package should keep review state.");
        TestAssert.True(names.Contains("output/output_summary.json"), "Resume package should keep lightweight output summary.");
        TestAssert.True(!names.Any(name => name.Contains(".gdb/", StringComparison.OrdinalIgnoreCase)), "Resume package should exclude file geodatabase payload.");
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

    public static async Task SuccessfulLoadPersistsWorkflowRuleAndScriptPlan()
    {
        using var tempRoot = new TempDirectory();
        using var rules = TempFile.FromExisting(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Settings", "WorkflowRules.json"));
        var manager = LoggedInManager();
        manager.SelectTransaction(Row("task-100000004", "100000004", "TR100000004", "Computation Check"), FixedNow());
        var attachments = new[]
        {
            new InnolaAttachmentMetadata("att-computation", "BELLEV029GEOLANCOMSHEET.pdf", ".pdf", "application/pdf", SourceRole.ComputationSource, "computation", 4, null, "mock-attachment:att-computation", true),
            new InnolaAttachmentMetadata("att-plan", "BELLEV029GEOLAN20230811.pdf", ".pdf", "application/pdf", SourceRole.PlanMapReference, "plan", 4, null, "mock-attachment:att-plan", true)
        };
        var service = LoadService(
            manager,
            new CountingDetailService(Detail("task-100000004", "100000004", "TR100000004", "Computation Check", attachments)),
            tempRoot.Path,
            new WorkflowRuleResolver(new WorkflowRuleRegistry(() => rules.Path), () => FixedNow()));

        var result = await service.LoadSelectedTransactionAsync();

        TestAssert.True(result.Success, "Load should succeed.");
        var manifest = ManifestSerializer.Read(result.Layout!.ManifestPath);
        TestAssert.Equal("scenario_a_two_pdf", manifest.Payload.WorkflowProfile, "Workflow profile should be persisted.");
        TestAssert.Equal("scenario_a_two_pdf_v1", manifest.Payload.WorkflowRuleId, "Workflow rule id should be persisted.");
        TestAssert.Equal("1.0.0", manifest.Payload.WorkflowRuleVersion, "Workflow rule version should be persisted.");
        TestAssert.True(manifest.Payload.ScriptPlan is not null, "Script plan should be persisted.");
        TestAssert.Equal(2, manifest.Payload.ScriptPlan!.Steps.Count, "Script plan should include both Scenario A steps.");
        var manifestText = File.ReadAllText(result.Layout.ManifestPath);
        TestAssert.True(!manifestText.Contains("token-abc", StringComparison.OrdinalIgnoreCase), "Manifest must not contain access token.");
        TestAssert.True(!manifestText.Contains("secret-password", StringComparison.OrdinalIgnoreCase), "Manifest must not contain session password.");
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
        string outputRoot,
        WorkflowRuleResolver? workflowRuleResolver = null)
    {
        return new InnolaTransactionLoadService(
            manager,
            detailService,
            new CaseFolderStore(() => FixedNow(), () => "run-load"),
            new AttachmentSourceFileWriter(() => FixedNow()),
            new SourceInputProfileDetector(() => FixedNow()),
            workflowRuleResolver ?? new WorkflowRuleResolver(),
            () => new WorkflowRuleSettings("openai", false, "balanced", "gpt-4.1-mini", "OPENAI_API_KEY", "local"),
            new CaseResumePackageService(() => FixedNow(), () => "test"),
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

    private static CaseFolderLayout BuildResumeCaseFolder(string outputRoot, string transactionNumber, string workflowState, string marker, DateTimeOffset now)
    {
        var store = new CaseFolderStore(() => now, () => $"run-{marker}");
        var created = store.CreateCase(outputRoot, transactionNumber, "tester");
        var layout = created.Layout!;
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(layout.ManifestPath, manifest with
        {
            Payload = manifest.Payload with
            {
                WorkflowState = workflowState,
                InnolaTransaction = new ManifestInnolaTransaction(
                    "100000004",
                    transactionNumber,
                    "task-100000004",
                    "Computation Check",
                    "parcel_workflow",
                    "parcel_workflow",
                    null,
                    "tester",
                    null,
                    null,
                    null,
                    null,
                    now.UtcDateTime.ToString("O"))
            }
        });
        File.WriteAllText(Path.Combine(layout.WorkingDirectory, $"{marker}.txt"), marker);
        return layout;
    }

    private static byte[] BuildResumePackage(CaseFolderLayout layout, SelectedInnolaTransaction transaction, DateTimeOffset now)
    {
        var resumeService = new CaseResumePackageService(() => now, () => "test");
        var package = resumeService.Build(layout, transaction, "tester");
        TestAssert.True(package.Success, "Resume package should build for test fixture.");
        return File.ReadAllBytes(package.PackagePath!);
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

    private sealed class FailingSecondAttachmentService : IInnolaTransactionDetailService
    {
        private readonly InnolaTransactionDetail detail;
        private int contentCalls;

        public FailingSecondAttachmentService(InnolaTransactionDetail detail)
        {
            this.detail = detail;
        }

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionDetailResult.Succeeded(detail));
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            contentCalls++;
            return Task.FromResult(contentCalls == 1
                ? InnolaAttachmentContentResult.Succeeded(new byte[] { 1, 2, 3, 4 })
                : InnolaAttachmentContentResult.Failure("Attachment content was not found.", "not_found"));
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

    private sealed class ThrowingDetailService : IInnolaTransactionDetailService
    {
        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("token secret-password raw response");
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Should not be called.");
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
            throw new InvalidOperationException("Should not be called.");
        }
    }

    private sealed class MultiResumeAttachmentDetailService : IInnolaTransactionDetailService
    {
        private readonly InnolaTransactionDetail detail;
        private readonly IReadOnlyDictionary<string, byte[]> attachmentContentById;

        public MultiResumeAttachmentDetailService(InnolaTransactionDetail detail, IReadOnlyDictionary<string, byte[]> attachmentContentById)
        {
            this.detail = detail;
            this.attachmentContentById = attachmentContentById;
        }

        public Task<InnolaTransactionDetailResult> GetTransactionDetailAsync(
            InnolaSession session,
            SelectedInnolaTransaction selectedTransaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(InnolaTransactionDetailResult.Succeeded(detail));
        }

        public Task<InnolaAttachmentContentResult> GetAttachmentContentAsync(
            InnolaSession session,
            InnolaTransactionDetail detail,
            InnolaAttachmentMetadata attachment,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(attachmentContentById.TryGetValue(attachment.AttachmentId, out var content)
                ? InnolaAttachmentContentResult.Succeeded(content)
                : InnolaAttachmentContentResult.Failure("Attachment content was not found.", "not_found"));
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
