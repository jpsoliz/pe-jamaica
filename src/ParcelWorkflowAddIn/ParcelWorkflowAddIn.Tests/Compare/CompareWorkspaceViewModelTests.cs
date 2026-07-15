using System.IO;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Compare;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareWorkspaceViewModelTests
{
    public static void IndependentLoadStatesKeepDocumentsWhenGeometryUnavailable()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();
        var state = new CompareWorkspaceLoadState(
            CompareDocumentLoadState.Loaded("Documents ready.", fixture.Layout.RootDirectory),
            CompareWorkingGeometryLoadResult.Blocked(
                CompareGeometryLoadStatus.MapUnavailable,
                "No active map.",
                null,
                retryable: true));

        viewModel.ApplyLoadState(state, fixture.Reopen());

        TestAssert.True(viewModel.DocumentsAvailable, "Documents should remain available.");
        TestAssert.False(viewModel.GeometryAvailable, "Geometry should remain unavailable.");
        TestAssert.True(viewModel.CanReloadGeometry, "Map unavailable should allow retry.");
        TestAssert.True(viewModel.CanQueryLegalEvidence, "Legal evidence queries should remain available when documents are loaded.");
        TestAssert.False(viewModel.CanQueryFiscalEvidence, "Fiscal neighbor queries should require geometry.");
        TestAssert.True(viewModel.CanQueryEvidence, "At least one evidence query path should remain available.");
        TestAssert.False(viewModel.CanApproveCompare, "Approval should be blocked without geometry.");
        TestAssert.Equal(1, viewModel.Documents.Count, "Source document should be loaded.");
    }

    public static void CommandAvailabilityKeepsGeometryEditingUnavailable()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.True(viewModel.GeometryReadOnly, "Geometry should be marked read-only.");
        TestAssert.False(viewModel.GeometryEditingAvailable, "Compare must not expose geometry editing actions.");
        TestAssert.True(viewModel.CanReloadGeometry, "Reload should stay available after geometry is loaded.");
        TestAssert.True(viewModel.CanQueryEvidence, "Evidence queries should be enabled when geometry is ready.");
        TestAssert.True(viewModel.CanQueryLegalEvidence, "Legal evidence queries should be enabled when documents are ready.");
        TestAssert.True(viewModel.CanQueryFiscalEvidence, "Fiscal neighbor queries should be enabled when geometry is ready.");
        TestAssert.True(viewModel.CanSaveProgress, "Save should be enabled after Case Folder load.");
    }

    public static void GeometryStatusIdentifiesActiveMapGroupAfterLoad()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.True(viewModel.GeometryStatus.Contains("Compare Review - 100000674", StringComparison.Ordinal), "Geometry status should identify the ArcGIS Pro group layer.");
        TestAssert.True(viewModel.GeometryStatus.Contains("1 polygon", StringComparison.OrdinalIgnoreCase), "Geometry status should include the loaded polygon count.");
    }

    public static async Task DiscrepancyBlocksApprovalUntilResolved()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        await viewModel.QueryParcelIdAsync();
        await viewModel.QueryFiscalNeighborsAsync();
        viewModel.MarkAllDiscrepanciesResolved();
        viewModel.AddDiscrepancy("Owner mismatch", "Legal cadaster", isResolved: false);

        TestAssert.True(viewModel.HasUnresolvedDiscrepancies, "Open discrepancy should be visible.");
        TestAssert.False(viewModel.CanApproveCompare, "Open discrepancy should block approval.");

        viewModel.MarkAllDiscrepanciesResolved();

        TestAssert.False(viewModel.HasUnresolvedDiscrepancies, "Resolved discrepancies should not block.");
        TestAssert.True(viewModel.CanApproveCompare, "Resolved discrepancies should allow approval when load state is ready.");
    }

    public static void SaveAndRestoreDraftPreservesNotesAndEvidence()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.Notes = "Reviewed survey plan against legal owner evidence.";
        viewModel.SurveyPlanSummary = "Survey plan owner: Jane Brown.";
        viewModel.LegalCadasterSummary = "Legal cadaster owner: Jane Brown.";
        viewModel.FiscalNeighborSummary = "Fiscal neighbors checked for context only.";
        viewModel.AddDiscrepancy("Neighbor name differs", "Fiscal cadaster", isResolved: true);

        viewModel.SaveProgressCommand.Execute(null);

        var restored = CreateViewModel();
        restored.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.Equal("Reviewed survey plan against legal owner evidence.", restored.Notes, "Notes should restore.");
        TestAssert.Equal("Survey plan owner: Jane Brown.", restored.SurveyPlanSummary, "Survey plan summary should restore.");
        TestAssert.Equal("Legal cadaster owner: Jane Brown.", restored.LegalCadasterSummary, "Legal summary should restore.");
        TestAssert.Equal("Fiscal neighbors checked for context only.", restored.FiscalNeighborSummary, "Fiscal summary should restore.");
        TestAssert.Equal(1, restored.Discrepancies.Count, "Discrepancy draft should restore.");
        TestAssert.True(restored.Discrepancies[0].IsResolved, "Resolved discrepancy state should restore.");
    }

    public static void ReturnToComputeCommandRefreshesAfterLoad()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();
        var refreshed = false;
        viewModel.ReturnToComputeCommand.CanExecuteChanged += (_, _) => refreshed = true;

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.True(refreshed, "Return to Compute command should refresh after Case Folder load.");
        TestAssert.True(viewModel.ReturnToComputeCommand.CanExecute(null), "Return to Compute should be enabled after Case Folder load.");
    }

    public static void ApprovalRequiresLegalAndFiscalEvidenceReview()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.False(viewModel.CanApproveCompare, "Approval should require legal and fiscal evidence review.");
        TestAssert.False(viewModel.LegalEvidenceReviewed, "Legal evidence should start unreviewed.");
        TestAssert.False(viewModel.FiscalEvidenceReviewed, "Fiscal evidence should start unreviewed.");
    }

    public static void PdfSelectorDefaultsToFirstPdfAndExcludesOtherAttachments()
    {
        using var fixture = CreateCaseFolderWithSources("plan-a.pdf", "plan-b.pdf", "notes.txt", "scan.png");
        var viewModel = CreateViewModel();

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.Equal(4, viewModel.Documents.Count, "All source documents should remain available to the model.");
        TestAssert.Equal(2, viewModel.PdfDocuments.Count, "PDF selector should only expose PDF attachments.");
        TestAssert.True(viewModel.HasPdfDocuments, "PDF selector should be enabled when PDFs exist.");
        TestAssert.Equal("plan-a.pdf", viewModel.SelectedDocument?.FileName, "First PDF should be selected by default.");
        TestAssert.Equal("plan-a.pdf", viewModel.PdfDocuments[0].FileName, "First selector row should be the first copied PDF.");
        TestAssert.Equal("plan-b.pdf", viewModel.PdfDocuments[1].FileName, "Second selector row should be the second copied PDF.");
    }

    public static void PdfSelectorSelectionChangeRefreshesViewerWithoutReloadingGeometry()
    {
        using var fixture = CreateCaseFolderWithSources("plan-a.pdf", "plan-b.pdf");
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        var geometryStatus = viewModel.GeometryStatus;

        viewModel.SelectedDocument = viewModel.PdfDocuments[1].SourceFile;

        TestAssert.Equal("plan-b.pdf", viewModel.SelectedDocument?.FileName, "Selected PDF should update from the combo selection.");
        TestAssert.True(viewModel.ViewerUsesBrowser, "Selected PDF should use the embedded browser viewer mode.");
        TestAssert.True(viewModel.ViewerFilePath?.EndsWith("plan-b.pdf", StringComparison.OrdinalIgnoreCase) == true, "Viewer path should point at the selected PDF.");
        TestAssert.Equal(geometryStatus, viewModel.GeometryStatus, "Changing PDF selection should not reload geometry state.");
        TestAssert.True(viewModel.GeometryAvailable, "Geometry should remain available after document selection changes.");
    }

    public static void PdfSelectorEmptyStateKeepsCompareUsableWhenNoPdfExists()
    {
        using var fixture = CreateCaseFolderWithSources("notes.txt", "scan.png");
        var viewModel = CreateViewModel();

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.Equal(2, viewModel.Documents.Count, "Non-PDF source documents should still be tracked.");
        TestAssert.Equal(0, viewModel.PdfDocuments.Count, "PDF selector should be empty when no PDFs exist.");
        TestAssert.False(viewModel.HasPdfDocuments, "PDF selector should be disabled when no PDFs exist.");
        TestAssert.True(viewModel.SelectedDocument is null, "No document should be selected in the PDF selector when no PDF exists.");
        TestAssert.True(viewModel.PdfDocumentSelectorStatus.Contains("No PDF", StringComparison.OrdinalIgnoreCase), "Empty selector should explain that no PDF is available.");
        TestAssert.True(viewModel.CanQueryLegalEvidence, "Compare should remain usable when documents load but no PDF is available.");
    }

    public static async Task ManualPidSearchUsesTypedValueAndShowsAllResults()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1"),
            LegalRecord("Janet Brown", "typed-999", "3", "4", "title-2")
        }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal(2, viewModel.QueryResults.Count, "Manual PID search should display every returned record.");
        TestAssert.True(viewModel.QueryResults.All(item => item.QueryKey.Contains("typed-999", StringComparison.Ordinal)), "Manual PID search should use the typed PID value.");
        TestAssert.True(viewModel.LegalEvidenceReviewed, "Manual legal search should count as legal evidence review.");
    }

    public static async Task ManualSearchValidationBlocksBlankRequiredValues()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.LandValuationNumber;
        viewModel.SearchLandValuationNumber = string.Empty;

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal(0, viewModel.QueryResults.Count, "Blank required value should not query or render results.");
        TestAssert.True(viewModel.SearchValidationMessage.Contains("Land Val No.", StringComparison.OrdinalIgnoreCase), "Validation should name the missing field.");
    }

    public static async Task ManualLandValAndNameSearchBuildExpectedQueryKeys()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "parcel-001", "123", "45", "title-1", landValuationNumber: "LV-77", parish: "Clarendon"),
            LegalRecord("Brown Holdings", "parcel-002", "124", "46", "title-2", landValuationNumber: "LV-88", parish: "Clarendon")
        }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.LandValuationNumber;
        viewModel.SearchLandValuationNumber = "LV-77";
        await viewModel.RunEvidenceSearchAsync();

        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Name;
        viewModel.SearchName = "Brown";
        viewModel.SearchParish = "Clarendon";
        await viewModel.RunEvidenceSearchAsync();

        TestAssert.True(viewModel.QueryResults.Any(item => item.QueryKey == "land_val_no=LV-77"), "Land Val No. query key should be retained.");
        TestAssert.True(viewModel.QueryResults.Any(item => item.QueryKey == "name=Brown;parish=Clarendon"), "Name and parish query key should be retained.");
    }

    public static async Task ValuableEvidencePersistsRoleTagAndRestoresWithoutRequery()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1", partyRole: "owner")
        }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        viewModel.ValuableEvidenceItems[0].RoleTag = CompareEvidenceRoleTag.InPossession;

        viewModel.SaveProgressCommand.Execute(null);

        var restored = CreateViewModel(new UnsupportedLegalCadasterQueryService());
        restored.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.Equal(1, restored.QueryResults.Count, "Query history should restore from draft without a service call.");
        TestAssert.Equal(1, restored.ValuableEvidenceItems.Count, "Valuable evidence should restore from draft.");
        TestAssert.Equal(CompareEvidenceRoleTag.InPossession, restored.ValuableEvidenceItems[0].RoleTag, "Edited role tag should persist.");
        TestAssert.True(restored.ValuableEvidenceItems[0].DisplaySummary.Contains("Jane Brown", StringComparison.Ordinal), "Retained evidence summary should restore.");
    }

    public static async Task NoRecordResultCannotBeMarkedValuable()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService());
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "missing-pid";

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal(1, viewModel.QueryResults.Count, "No-record query should remain visible in query history.");
        TestAssert.False(viewModel.QueryResults[0].CanMarkValuable, "No-record rows should not be markable as valuable evidence.");
        TestAssert.False(viewModel.MarkEvidenceResultValuableCommand.CanExecute(viewModel.QueryResults[0]), "Command should reject no-record rows.");

        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);

        TestAssert.Equal(0, viewModel.ValuableEvidenceItems.Count, "No-record row must not be retained as valuable evidence.");
    }

    public static async Task ValuableEvidenceIdsRemainUniqueAfterRemoveAndAdd()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1"),
            LegalRecord("Janet Brown", "typed-999", "3", "4", "title-2")
        }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();

        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        var firstId = viewModel.ValuableEvidenceItems[0].EvidenceId;
        viewModel.RemoveValuableEvidenceCommand.Execute(viewModel.ValuableEvidenceItems[0]);
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[1]);

        TestAssert.Equal(1, viewModel.ValuableEvidenceItems.Count, "One valuable evidence item should remain after remove/add.");
        TestAssert.True(!string.Equals(firstId, viewModel.ValuableEvidenceItems[0].EvidenceId, StringComparison.Ordinal), "Evidence IDs should not be reused after removal.");
    }

    private static CompareWorkspaceViewModel CreateViewModel(ILegalCadasterQueryService? legalService = null)
    {
        return new CompareWorkspaceViewModel(new SelectedInnolaTransaction(
            "task-1",
            "100000674",
            "TR100000674",
            "Compare Survey Plan",
            "Compare",
            DateTimeOffset.Parse("2026-07-14T00:00:00Z")),
            legalCadasterQueryService: legalService ?? new MockLegalCadasterQueryService());
    }

    private static LegalCadasterRecord LegalRecord(
        string owner,
        string parcelId,
        string volume,
        string folio,
        string titleId,
        string? landValuationNumber = null,
        string? parish = null,
        string? partyRole = null)
    {
        return new LegalCadasterRecord(
            owner,
            parcelId,
            volume,
            folio,
            titleId,
            "Legal cadaster",
            DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
            string.IsNullOrWhiteSpace(landValuationNumber)
                ? $"parcel_id={parcelId}"
                : $"land_val_no={landValuationNumber}",
            CompareEvidenceStatus.Ready,
            null,
            landValuationNumber,
            parish,
            partyRole);
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
                "Compare working layers loaded into ArcGIS Pro map group 'Compare Review - 100000674' for transaction_number '100000674' (1 polygon feature(s)). Map zoomed to the transaction layer.",
                plan,
                CompareMapIntegrationResult.Loaded(
                    "Compare working layers loaded into ArcGIS Pro map group 'Compare Review - 100000674' for transaction_number '100000674' (1 polygon feature(s)). Map zoomed to the transaction layer.",
                    Array.Empty<string>(),
                    "Compare Review - 100000674",
                    1)));
    }

    private static CompareCaseFixture CreateCaseFolderWithSource()
    {
        return CreateCaseFolderWithSources("survey-plan.pdf");
    }

    private static CompareCaseFixture CreateCaseFolderWithSources(params string[] fileNames)
    {
        var temp = new TempDirectory();
        var sourcePaths = new List<string>();
        foreach (var fileName in fileNames)
        {
            var sourcePath = Path.Combine(temp.Path, fileName);
            File.WriteAllText(sourcePath, $"fake content for {fileName}");
            sourcePaths.Add(sourcePath);
        }

        var store = new CaseFolderStore(
            () => DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
            () => "run-compare");
        var created = store.CreateCase(temp.Path, "TR100000674", "tester");
        if (!created.Success || created.Layout is null)
        {
            throw new InvalidOperationException(created.ErrorMessage);
        }

        var copied = new SourceFileCopyService().CopySourceFiles(created.Layout, sourcePaths, "survey_plan_pdf");
        if (!copied.Success)
        {
            throw new InvalidOperationException(string.Join(" ", copied.Results.Select(result => result.Message)));
        }

        var manifest = ManifestSerializer.Read(created.Layout.ManifestPath);
        ManifestSerializer.Write(created.Layout.ManifestPath, manifest with
        {
            Payload = manifest.Payload with { WorkflowState = WorkflowState.OutputCreated.ToContractValue() }
        });

        return new CompareCaseFixture(temp, store, created.Layout);
    }

    private sealed class CompareCaseFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;
        private readonly CaseFolderStore store;

        public CompareCaseFixture(TempDirectory tempDirectory, CaseFolderStore store, CaseFolderLayout layout)
        {
            this.tempDirectory = tempDirectory;
            this.store = store;
            Layout = layout;
        }

        public CaseFolderLayout Layout { get; }

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
