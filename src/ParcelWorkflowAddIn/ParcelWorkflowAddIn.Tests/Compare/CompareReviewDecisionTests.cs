using System.IO;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareReviewDecisionTests
{
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-07-15T10:00:00Z");

    public static void DraftSaveLoadIncludesReviewerAndTransaction()
    {
        using var fixture = CompareDecisionFixture.Create();
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.Notes = "Reviewed owner evidence.";

        viewModel.SaveProgressCommand.Execute(null);

        var draft = new CompareReviewDraftPersistenceService().Load(fixture.Layout);
        TestAssert.Equal("TR100000674", draft?.TransactionNumber, "Draft transaction number mismatch.");
        TestAssert.Equal("100000674", draft?.TransactionId, "Draft transaction id mismatch.");
        TestAssert.Equal("task-1", draft?.TaskId, "Draft task id mismatch.");
        TestAssert.Equal("tester", draft?.ReviewerId, "Draft reviewer mismatch.");
        TestAssert.True(!string.IsNullOrWhiteSpace(draft?.SavedAtUtc), "Draft timestamp should be recorded.");
    }

    public static async Task ApprovedDecisionSaveLoadUnlocksCommitReadiness()
    {
        using var fixture = CompareDecisionFixture.Create();
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.Notes = "Evidence reconciled.";
        await viewModel.QueryParcelIdAsync();
        await viewModel.QueryFiscalNeighborsAsync();
        viewModel.MarkAllDiscrepanciesResolved();

        viewModel.ApproveCompareCommand.Execute(null);

        var decision = new CompareReviewDecisionPersistenceService().Load(fixture.Layout);
        TestAssert.Equal(CompareReviewDecisionValues.Approved, decision?.Decision, "Decision should be approved.");
        TestAssert.Equal(CompareReviewReadinessStatus.CommitReady, decision?.ReadinessStatus, "Approved Compare should be commit-ready.");
        TestAssert.Equal("tester", decision?.ReviewerId, "Decision reviewer mismatch.");
        TestAssert.True(decision!.EvidenceRefs.Any(item => item.RelativePath == "working/compare_review_draft.json"), "Decision should reference draft by relative path.");

        var readiness = new CompareCommitReadinessService().CheckReadiness(fixture.Layout, Transaction());
        TestAssert.True(readiness.IsReady, "Approved current Compare decision should unlock Commit readiness.");
    }

    public static void PriorDecisionRestoreDisplaysEvidenceRefsAndDiscrepancies()
    {
        using var fixture = CompareDecisionFixture.Create();
        var document = ApprovedDocument(Transaction()) with
        {
            EvidenceRefs = new[]
            {
                new CompareReviewEvidenceRef("legal_cadaster", null, "Legal owner evidence restored.")
            },
            Discrepancies = new[]
            {
                new CompareReviewDiscrepancySummary("Prior accepted issue", "Legal cadaster", "Resolved", true, false)
            }
        };
        new CompareReviewDecisionPersistenceService().Save(fixture.Layout, document);

        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.True(viewModel.EvidenceItems.Any(item =>
            item.Source.Equals("Compare decision", StringComparison.OrdinalIgnoreCase)
            && item.Summary.Contains("Legal owner evidence restored", StringComparison.OrdinalIgnoreCase)), "Prior decision evidence refs should display.");
        TestAssert.True(viewModel.Discrepancies.Any(item =>
            item.Title.Equals("Prior accepted issue", StringComparison.OrdinalIgnoreCase)), "Prior decision discrepancy summary should restore.");
    }

    public static void BlockedAndReturnedDecisionsDoNotUnlockCommit()
    {
        using var blockedFixture = CompareDecisionFixture.Create();
        var blocked = CreateViewModel();
        blocked.ApplyLoadState(ReadyState(blockedFixture.Layout.RootDirectory), blockedFixture.Reopen());
        blocked.BlockCompareCommand.Execute(null);

        var blockedReadiness = new CompareCommitReadinessService().CheckReadiness(blockedFixture.Layout, Transaction());
        TestAssert.False(blockedReadiness.IsReady, "Blocked Compare should not unlock Commit.");
        TestAssert.Equal("compare_decision_not_approved", blockedReadiness.Code, "Blocked readiness code mismatch.");

        using var returnedFixture = CompareDecisionFixture.Create();
        var returned = CreateViewModel();
        returned.ApplyLoadState(ReadyState(returnedFixture.Layout.RootDirectory), returnedFixture.Reopen());
        returned.ReturnToComputeCommand.Execute(null);

        var returnedReadiness = new CompareCommitReadinessService().CheckReadiness(returnedFixture.Layout, Transaction());
        TestAssert.False(returnedReadiness.IsReady, "Returned Compare should not unlock Commit.");
        TestAssert.Equal("compare_decision_not_approved", returnedReadiness.Code, "Returned readiness code mismatch.");
    }

    public static void MismatchedTransactionBlocksCommitReadiness()
    {
        using var fixture = CompareDecisionFixture.Create();
        var document = ApprovedDocument(Transaction()) with { TransactionNumber = "TR999999999" };
        new CompareReviewDecisionPersistenceService().Save(fixture.Layout, document);

        var readiness = new CompareCommitReadinessService().CheckReadiness(fixture.Layout, Transaction());

        TestAssert.False(readiness.IsReady, "Mismatched decision should block Commit.");
        TestAssert.Equal("compare_decision_missing", readiness.Code, "Mismatch should be treated as unavailable readiness evidence.");
    }

    public static void MissingAndStaleDecisionBlocksCommitReadiness()
    {
        using var missingFixture = CompareDecisionFixture.Create();
        var missing = new CompareCommitReadinessService().CheckReadiness(missingFixture.Layout, Transaction());
        TestAssert.False(missing.IsReady, "Missing decision should block Commit.");
        TestAssert.Equal("compare_decision_missing", missing.Code, "Missing readiness code mismatch.");

        using var staleFixture = CompareDecisionFixture.Create();
        new CompareReviewDecisionPersistenceService().Save(
            staleFixture.Layout,
            ApprovedDocument(Transaction()) with { DecidedAtUtc = "not-a-date" });
        var stale = new CompareCommitReadinessService().CheckReadiness(staleFixture.Layout, Transaction());
        TestAssert.False(stale.IsReady, "Stale decision should block Commit.");
        TestAssert.Equal("compare_decision_stale", stale.Code, "Stale readiness code mismatch.");
    }

    private static CompareWorkspaceViewModel CreateViewModel()
    {
        return new CompareWorkspaceViewModel(
            Transaction(),
            getUtcNow: () => FixedNow,
            reviewerId: "tester",
            reviewerDisplayName: "Test User");
    }

    private static CompareReviewDecisionDocument ApprovedDocument(SelectedInnolaTransaction transaction)
    {
        return new CompareReviewDecisionDocument(
            "1.0.0",
            transaction.TransactionId,
            transaction.TransactionNumber,
            transaction.TaskId,
            "tester",
            "Test User",
            FixedNow.UtcDateTime.ToString("O"),
            CompareReviewDecisionValues.Approved,
            "Ready.",
            CompareReviewReadinessStatus.CommitReady,
            Array.Empty<CompareReviewEvidenceRef>(),
            Array.Empty<CompareReviewDiscrepancySummary>());
    }

    private static SelectedInnolaTransaction Transaction()
    {
        return new SelectedInnolaTransaction(
            "task-1",
            "100000674",
            "TR100000674",
            "Compare Survey Plan",
            "Compare",
            FixedNow);
    }

    private static CompareWorkspaceLoadState ReadyState(string caseFolderPath)
    {
        var plan = new CompareWorkingGeometryLoadPlan(
            true,
            "100000674",
            "TR100000674",
            null,
            "transaction_number",
            "100000674",
            "transaction_number = '100000674'",
            Array.Empty<CompareWorkingLayerRequest>(),
            null);

        return new CompareWorkspaceLoadState(
            CompareDocumentLoadState.Loaded("Documents ready.", caseFolderPath),
            CompareWorkingGeometryLoadResult.Ready(
                "Geometry ready.",
                plan,
                CompareMapIntegrationResult.Loaded("Geometry ready.", Array.Empty<string>(), "Compare Review - 100000674", 1)));
    }

    private sealed class CompareDecisionFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;
        private readonly CaseFolderStore store;

        private CompareDecisionFixture(TempDirectory tempDirectory, CaseFolderStore store, CaseFolderLayout layout)
        {
            this.tempDirectory = tempDirectory;
            this.store = store;
            Layout = layout;
        }

        public CaseFolderLayout Layout { get; }

        public static CompareDecisionFixture Create()
        {
            var temp = new TempDirectory();
            var sourcePath = Path.Combine(temp.Path, "survey-plan.pdf");
            File.WriteAllText(sourcePath, "fake pdf");
            var store = new CaseFolderStore(() => FixedNow, () => "run-compare-decision");
            var created = store.CreateCase(temp.Path, "TR100000674", "tester");
            if (!created.Success || created.Layout is null)
            {
                throw new InvalidOperationException(created.ErrorMessage);
            }

            var copied = new SourceFileCopyService().CopySourceFiles(created.Layout, new[] { sourcePath }, "survey_plan_pdf");
            if (!copied.Success)
            {
                throw new InvalidOperationException(string.Join(" ", copied.Results.Select(result => result.Message)));
            }

            var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
            ManifestSerializer.Write(created.Layout.ManifestPath, manifest with
            {
                Payload = manifest.Payload with { WorkflowState = WorkflowState.OutputCreated.ToContractValue() }
            });
            return new CompareDecisionFixture(temp, store, created.Layout);
        }

        public CaseFolderReopenResult Reopen()
        {
            return store.ReopenCaseFolder(Layout.RootDirectory);
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }
}
