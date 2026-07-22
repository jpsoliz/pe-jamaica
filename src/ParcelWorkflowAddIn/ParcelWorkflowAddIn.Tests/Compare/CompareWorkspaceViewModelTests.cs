using System.IO;
using System.Text.Json;
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

    public static async Task FinalizeUsesValuableEvidenceAndNotesEvenWhenDiscrepancyExists()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1")
        }));

        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        viewModel.Notes = "Evidence reconciled against the survey plan.";
        viewModel.MarkAllDiscrepanciesResolved();
        viewModel.AddDiscrepancy("Owner mismatch", "Legal cadaster", isResolved: false);

        TestAssert.True(viewModel.HasUnresolvedDiscrepancies, "Open discrepancy should be visible.");
        TestAssert.True(viewModel.CanApproveCompare, "Finalize should follow valuable evidence plus Decision Notes even when a discrepancy is visible.");

        viewModel.MarkAllDiscrepanciesResolved();

        TestAssert.False(viewModel.HasUnresolvedDiscrepancies, "Resolved discrepancies should still be tracked.");
        TestAssert.True(viewModel.CanApproveCompare, "Resolved discrepancies should keep Finalize enabled when evidence and notes remain ready.");
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

    public static void SaveGeneratesCompareReviewReportAndCompletionMessage()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.Notes = "Reviewed survey plan against legal owner evidence.";
        viewModel.SurveyPlanSummary = "Survey plan owner: TRACEY, HOPETON SCOTT.";
        viewModel.LegalCadasterSummary = "Legal cadaster owner: TRACEY, HOPETON SCOTT.";
        viewModel.FiscalNeighborSummary = "Fiscal context reviewed.";
        viewModel.ValuableEvidenceItems.Add(new CompareValuableEvidenceItem(new CompareValuableEvidence(
            "evidence-1",
            CompareEvidenceSourceType.LegalCadaster,
            "Innola Owner Search",
            "volume=1486;folio=393",
            "TRACEY, HOPETON SCOTT; Role: Fee Simple; PID: 10843842",
            CompareEvidenceRoleTag.Owner,
            DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
            null)));
        viewModel.SaveProgressCommand.Execute(null);

        var reportPath = Path.Combine(fixture.Layout.ReportsDirectory, CompareReviewReportService.ReportFileName);
        var pdfReportPath = Path.Combine(fixture.Layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName);
        TestAssert.True(File.Exists(reportPath), "Save should generate the Compare review report JSON.");
        TestAssert.True(File.Exists(pdfReportPath), "Save should generate the Compare review report PDF.");
        var pdfText = File.ReadAllText(pdfReportPath);
        TestAssert.True(pdfText.StartsWith("%PDF-1.4", StringComparison.Ordinal), "PDF report should be a PDF document.");
        TestAssert.True(pdfText.Contains("Valuable Evidence", StringComparison.Ordinal), "PDF report should title the retained evidence section.");
        TestAssert.True(pdfText.Contains("1. Owner: TRACEY, HOPETON SCOTT", StringComparison.Ordinal), "PDF report should number valuable evidence rows.");
        TestAssert.True(pdfText.Contains("Notes", StringComparison.Ordinal), "PDF report should include notes section.");
        TestAssert.True(pdfText.Contains("Reviewed survey plan against legal owner evidence.", StringComparison.Ordinal), "PDF report should include decision notes.");
        TestAssert.False(pdfText.Contains("Review Summary", StringComparison.Ordinal), "PDF report should not include internal review summary sections.");
        TestAssert.Equal("Save complete. Compare report generated and overwritten.", viewModel.StatusText, "Save should show a clear completion message.");

        using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = document.RootElement;
        TestAssert.Equal("1.0.0", root.GetProperty("schema_version").GetString(), "Report schema version mismatch.");
        TestAssert.Equal("TR100000674", root.GetProperty("transaction_number").GetString(), "Report transaction number mismatch.");
        TestAssert.Equal("saved_progress", root.GetProperty("decision_state").GetString(), "Report decision state mismatch.");
        TestAssert.Equal("Reviewed survey plan against legal owner evidence.", root.GetProperty("notes").GetString(), "Report notes mismatch.");
        TestAssert.True(root.GetProperty("artifact_refs").EnumerateArray().Any(), "Report should include artifact references.");
    }

    public static async Task FinalizeAttachesComparePdfReportBeforeCompletingTask()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService
        {
            CompleteResult = CompareTaskLifecycleResult.Succeeded("Completed from test.")
        };
        var attachmentService = new RecordingCompareReportAttachmentService();
        var viewModel = CreateViewModel(
            new MockLegalCadasterQueryService(new[]
            {
                LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1")
            }),
            lifecycle,
            attachmentService);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        viewModel.Notes = "Evidence reconciled against the survey plan.";
        viewModel.MarkAllDiscrepanciesResolved();
        var pdfReportPath = Path.Combine(fixture.Layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName);
        Directory.CreateDirectory(fixture.Layout.ReportsDirectory);
        File.WriteAllText(pdfReportPath, "stale report");

        viewModel.ApproveCompareCommand.Execute(null);
        await lifecycle.CompleteObserved.Task;

        TestAssert.Equal(1, attachmentService.UploadCalls, "Finalize should attach the generated Compare PDF report once.");
        TestAssert.Equal("TR100000674", attachmentService.LastTransactionNumber, "Report attachment should target the selected transaction.");
        TestAssert.True(File.Exists(attachmentService.LastPdfReportPath), "Report attachment should use an existing generated PDF.");
        var uploadedPdfText = File.ReadAllText(attachmentService.LastPdfReportPath!);
        TestAssert.True(uploadedPdfText.StartsWith("%PDF-1.4", StringComparison.Ordinal), "Finalize should regenerate the PDF before upload.");
        TestAssert.True(uploadedPdfText.Contains("Evidence reconciled against the survey plan.", StringComparison.Ordinal), "Finalize should upload a report generated from the current notes.");
        TestAssert.False(uploadedPdfText.Contains("stale report", StringComparison.Ordinal), "Finalize should overwrite stale PDF report content before upload.");
        TestAssert.Equal(1, lifecycle.CompleteCalls, "Finalize should complete only after the report attachment step succeeds.");
        var tracePath = Path.Combine(fixture.Layout.WorkingDirectory, "compare_finalize_trace.json");
        TestAssert.True(File.Exists(tracePath), "Finalize should write a diagnostic trace.");
        var traceText = File.ReadAllText(tracePath);
        TestAssert.True(traceText.Contains("\"step\": \"report_generated\"", StringComparison.Ordinal), "Finalize trace should record report generation.");
        TestAssert.True(traceText.Contains("\"step\": \"upload_started\"", StringComparison.Ordinal), "Finalize trace should record upload start.");
        TestAssert.True(traceText.Contains("\"step\": \"upload_result\"", StringComparison.Ordinal), "Finalize trace should record upload result.");
        TestAssert.True(traceText.Contains("\"source_type\": \"st_compare_report\"", StringComparison.Ordinal), "Finalize trace should record Compare report attachment type.");
        TestAssert.True(traceText.Contains("\"pdf_report_exists\": \"True\"", StringComparison.Ordinal), "Finalize trace should record generated PDF existence.");
    }

    public static void SaveDraftDoesNotCallTaskLifecycle()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService();
        var viewModel = CreateViewModel(taskLifecycleService: lifecycle);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        viewModel.SaveProgressCommand.Execute(null);

        TestAssert.Equal(0, lifecycle.SuspendCalls, "Save draft must not suspend or release the task.");
        TestAssert.Equal(0, lifecycle.CompleteCalls, "Save draft must not complete the task.");
        TestAssert.Equal("Draft", viewModel.DecisionStatus, "Save draft should keep draft decision status.");
    }

    public static void SavePromptCanCancelWithoutWritingReport()
    {
        using var fixture = CreateCaseFolderWithSource();
        var prompt = new RecordingCompareWorkspacePromptService
        {
            SaveResult = false
        };
        var viewModel = CreateViewModel(promptService: prompt);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        viewModel.SaveProgressCommand.Execute(null);

        TestAssert.Equal(1, prompt.SaveCalls, "Save should ask for user confirmation.");
        TestAssert.Equal("Save cancelled.", viewModel.StatusText, "Cancelled Save should show a visible status.");
        TestAssert.False(File.Exists(Path.Combine(fixture.Layout.ReportsDirectory, CompareReviewReportService.ReportFileName)), "Cancelled Save should not write the JSON report.");
        TestAssert.False(File.Exists(Path.Combine(fixture.Layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName)), "Cancelled Save should not write the PDF report.");
    }

    public static async Task SuspendTaskSavesDraftAndRequestsCloseOnSuccess()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService
        {
            SuspendResult = CompareTaskLifecycleResult.Succeeded("Suspended from test.")
        };
        var viewModel = CreateViewModel(taskLifecycleService: lifecycle);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        viewModel.SuspendTaskCommand.Execute(null);
        await lifecycle.SuspendObserved.Task;

        TestAssert.Equal(1, lifecycle.SuspendCalls, "Suspend task should call the lifecycle bridge once.");
        TestAssert.Equal("TR100000674", lifecycle.LastTransactionNumber, "Suspend should target the active Compare transaction.");
        TestAssert.True(closeRequested, "Successful suspend should ask the window to close after cleanup.");
        TestAssert.Equal("Suspended from test.", viewModel.StatusText, "Suspend result message should be shown.");
    }

    public static async Task SuspendTaskCleansCompareMapAndWorkspaceOnSuccess()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService
        {
            SuspendResult = CompareTaskLifecycleResult.Succeeded("Suspended from test.")
        };
        var mapIntegration = new RecordingCompareMapIntegrationService();
        var viewModel = CreateViewModel(
            taskLifecycleService: lifecycle,
            mapIntegrationService: mapIntegration);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        viewModel.SuspendTaskCommand.Execute(null);
        await lifecycle.SuspendObserved.Task;

        TestAssert.Equal(1, mapIntegration.CleanupCalls, "Successful suspend should remove the active Compare map group.");
        TestAssert.Equal("Compare Review - 100000674", mapIntegration.LastGroupLayerName, "Map cleanup should target the loaded Compare group.");
        TestAssert.False(viewModel.DocumentsAvailable, "Successful suspend cleanup should clear document availability.");
        TestAssert.False(viewModel.GeometryAvailable, "Successful suspend cleanup should clear geometry availability.");
        TestAssert.Equal(0, viewModel.Documents.Count, "Successful suspend cleanup should clear the form documents.");
    }

    public static async Task CloseWorkspaceCleansCompareMapAndWorkspace()
    {
        using var fixture = CreateCaseFolderWithSource();
        var mapIntegration = new RecordingCompareMapIntegrationService();
        var viewModel = CreateViewModel(mapIntegrationService: mapIntegration);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        await viewModel.CloseWorkspaceAsync();

        TestAssert.Equal(1, mapIntegration.CleanupCalls, "Closing the Compare workspace should remove the active Compare map group.");
        TestAssert.Equal("Compare Review - 100000674", mapIntegration.LastGroupLayerName, "Close cleanup should target the loaded Compare group.");
        TestAssert.False(viewModel.DocumentsAvailable, "Close cleanup should clear document availability.");
        TestAssert.False(viewModel.GeometryAvailable, "Close cleanup should clear geometry availability.");
        TestAssert.Equal(0, viewModel.Documents.Count, "Close cleanup should clear the form documents.");
    }

    public static async Task SuspendTaskFailureKeepsWorkspaceOpen()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService
        {
            SuspendResult = CompareTaskLifecycleResult.Failure("Could not suspend. Try again.")
        };
        var viewModel = CreateViewModel(taskLifecycleService: lifecycle);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        viewModel.SuspendTaskCommand.Execute(null);
        await lifecycle.SuspendObserved.Task;

        TestAssert.False(closeRequested, "Failed suspend must keep Compare open for retry.");
        TestAssert.Equal("Could not suspend. Try again.", viewModel.StatusText, "Failed suspend should show sanitized retryable message.");
    }

    public static async Task FinalizeCompletesTaskAndClosesOnSuccess()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService
        {
            CompleteResult = CompareTaskLifecycleResult.Succeeded("Completed from test.")
        };
        var viewModel = CreateViewModel(
            new MockLegalCadasterQueryService(new[]
            {
                LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1")
            }),
            lifecycle);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.False(viewModel.CanApproveCompare, "Finalize should be disabled before evidence and notes are ready.");

        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        viewModel.Notes = "Evidence reconciled against the survey plan.";
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        viewModel.MarkAllDiscrepanciesResolved();
        TestAssert.True(viewModel.CanApproveCompare, "Evidence and notes should enable Finalize.");
        viewModel.ApproveCompareCommand.Execute(null);
        await lifecycle.CompleteObserved.Task;

        TestAssert.Equal(1, lifecycle.CompleteCalls, "Finalize should call the lifecycle bridge once.");
        TestAssert.True(closeRequested, "Successful finalize should ask the window to close after cleanup.");
        TestAssert.Equal("Completed from test.", viewModel.StatusText, "Finalize result message should be shown.");
    }

    public static async Task FinalizeClearsWorkspaceAndDisablesCompletionAfterTaskClose()
    {
        using var fixture = CreateCaseFolderWithSource();
        var lifecycle = new RecordingCompareTaskLifecycleService();
        var mapIntegration = new RecordingCompareMapIntegrationService();
        var viewModel = CreateViewModel(
            new MockLegalCadasterQueryService(new[]
            {
                LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1")
            }),
            lifecycle,
            mapIntegrationService: mapIntegration);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        viewModel.Notes = "Evidence reconciled against the survey plan.";
        viewModel.MarkAllDiscrepanciesResolved();
        viewModel.ApproveCompareCommand.Execute(null);
        await lifecycle.CompleteObserved.Task;

        TestAssert.False(viewModel.GeometryAvailable, "Successful Finalize should clear geometry availability after task close.");
        TestAssert.Equal(0, viewModel.ValuableEvidenceItems.Count, "Successful Finalize should clear retained evidence from the closed workspace.");
        TestAssert.False(viewModel.CompleteTaskCommand.CanExecute(null), "Complete task should be disabled after Finalize closes and clears the workspace.");
        TestAssert.Equal(1, mapIntegration.CleanupCalls, "Successful Finalize should remove Compare map content after task close.");
        var tracePath = Path.Combine(fixture.Layout.WorkingDirectory, "compare_finalize_trace.json");
        TestAssert.True(File.Exists(tracePath), "Finalize should write cleanup diagnostics.");
        var traceText = File.ReadAllText(tracePath);
        TestAssert.True(traceText.Contains("\"step\": \"cleanup_started\"", StringComparison.Ordinal), "Finalize trace should record cleanup start.");
        TestAssert.True(traceText.Contains("\"step\": \"map_cleanup_result\"", StringComparison.Ordinal), "Finalize trace should record map cleanup result.");
        TestAssert.True(traceText.Contains("\"step\": \"form_cleanup_result\"", StringComparison.Ordinal), "Finalize trace should record form cleanup result.");
    }

    public static async Task FinalizeRequiresValuableEvidenceAndDecisionNotes()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService(new[]
        {
            LegalRecord("Jane Brown", "typed-999", "1", "2", "title-1")
        }));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.False(viewModel.CanApproveCompare, "Finalize should require retained valuable evidence and Decision Notes.");

        viewModel.Notes = "Evidence reconciled against the survey plan.";
        TestAssert.False(viewModel.CanApproveCompare, "Decision Notes alone should not enable Finalize.");

        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "typed-999";
        await viewModel.RunEvidenceSearchAsync();
        viewModel.MarkEvidenceResultValuableCommand.Execute(viewModel.QueryResults[0]);
        TestAssert.True(viewModel.CanApproveCompare, "Finalize should enable once valuable evidence and Decision Notes exist.");

        viewModel.RemoveValuableEvidenceCommand.Execute(viewModel.ValuableEvidenceItems[0]);
        TestAssert.False(viewModel.CanApproveCompare, "Removing valuable evidence should disable Finalize again.");
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

    public static void PdfPanelToggleDefaultsVisibleAndPreservesSelectedDocument()
    {
        using var fixture = CreateCaseFolderWithSources("plan-a.pdf", "plan-b.pdf");
        var viewModel = CreateViewModel();
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedDocument = viewModel.PdfDocuments[1].SourceFile;
        var selectedDocument = viewModel.SelectedDocument;
        var navigationKey = viewModel.ViewerNavigationKey;

        TestAssert.True(viewModel.IsPdfPanelVisible, "PDF panel should be visible by default.");
        TestAssert.False(viewModel.IsPdfPanelHidden, "Hidden helper should be false by default.");
        TestAssert.Equal("Hide Files", viewModel.PdfPanelToggleText, "Visible panel should expose Hide Files action.");

        viewModel.TogglePdfPanelCommand.Execute(null);

        TestAssert.False(viewModel.IsPdfPanelVisible, "Toggle should hide PDF panel.");
        TestAssert.True(viewModel.IsPdfPanelHidden, "Hidden helper should track collapsed PDF panel.");
        TestAssert.Equal("Show Files", viewModel.PdfPanelToggleText, "Hidden panel should expose Show Files action.");
        TestAssert.True(ReferenceEquals(selectedDocument, viewModel.SelectedDocument), "Toggling PDF panel should preserve selected document.");
        TestAssert.Equal(navigationKey, viewModel.ViewerNavigationKey, "Toggling PDF panel should not change viewer navigation state.");

        viewModel.TogglePdfPanelCommand.Execute(null);

        TestAssert.True(viewModel.IsPdfPanelVisible, "Second toggle should restore PDF panel.");
        TestAssert.True(ReferenceEquals(selectedDocument, viewModel.SelectedDocument), "Restoring PDF panel should preserve selected document.");
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
        TestAssert.Equal("PID search: 2 records found.", viewModel.EvidenceSearchStatusMessage, "Search status should report returned record count.");
    }

    public static async Task ManualSearchWritesTraceWithoutSaveProgress()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService());
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.VolumeFolio;
        viewModel.SearchVolume = "1486";
        viewModel.SearchFolio = "393";

        await viewModel.RunEvidenceSearchAsync();

        var tracePath = Path.Combine(fixture.Layout.WorkingDirectory, "compare_legal_query_trace.json");
        TestAssert.True(File.Exists(tracePath), "Manual legal search should write an immediate trace file.");
        var trace = File.ReadAllText(tracePath);
        TestAssert.True(trace.Contains("\"query_kind\": \"volume_folio\"", StringComparison.Ordinal), "Trace should capture the query kind.");
        TestAssert.True(trace.Contains("\"query_key\": \"volume=1486;folio=393\"", StringComparison.Ordinal), "Trace should capture the searched Volume/Folio.");
        TestAssert.True(trace.Contains("\"record_count\": 0", StringComparison.Ordinal), "Trace should capture no-record result count.");
        TestAssert.True(!trace.Contains("token", StringComparison.OrdinalIgnoreCase), "Trace must not contain auth token labels.");
        TestAssert.True(!trace.Contains("password", StringComparison.OrdinalIgnoreCase), "Trace must not contain password labels.");
        TestAssert.Equal("Volume/Folio search: no records found.", viewModel.EvidenceSearchStatusMessage, "Search status should report no-record results.");
    }

    public static async Task ManualSearchFailureMessageIsVisible()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new UnsupportedLegalCadasterQueryService(
            "Innola BA Unit search could not be completed. Try again.",
            "Innola BA Unit search returned Unauthorized."));
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.VolumeFolio;
        viewModel.SearchVolume = "1486";
        viewModel.SearchFolio = "393";

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal("Volume/Folio search: search failed. Try again.", viewModel.EvidenceSearchStatusMessage, "Search status should report failed queries without diagnostic noise.");
        TestAssert.Equal(0, viewModel.QueryResults.Count, "Failed legal searches should not add blank query result rows.");
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

    public static async Task ManualVolumeFolioSearchValidationBlocksNonNumericValues()
    {
        using var fixture = CreateCaseFolderWithSource();
        var service = new CountingLegalCadasterQueryService();
        var viewModel = CreateViewModel(service);
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.VolumeFolio;
        viewModel.SearchVolume = "ABC";
        viewModel.SearchFolio = "583";

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal(0, viewModel.QueryResults.Count, "Invalid Volume/Folio should not query or render results.");
        TestAssert.Equal(0, service.VolumeFolioCallCount, "Invalid Volume/Folio should be blocked before service call.");
        TestAssert.True(viewModel.SearchValidationMessage.Contains("numeric", StringComparison.OrdinalIgnoreCase), "Validation should explain numeric requirement.");
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
        TestAssert.True(viewModel.QueryResults.Any(item => item.QueryKey == "land_val_no=LV-77"), "Land Val No. query key should be shown after the Land Val No. search.");

        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Name;
        viewModel.SearchName = "Brown";
        await viewModel.RunEvidenceSearchAsync();

        TestAssert.False(viewModel.QueryResults.Any(item => item.QueryKey == "land_val_no=LV-77"), "Manual search results should show the current query, not stale rows from the prior search.");
        TestAssert.True(viewModel.QueryResults.Any(item => item.QueryKey == "name=Brown"), "Owner name query key should be shown after the Name search.");
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

    public static async Task NoRecordResultDoesNotRenderBlankSearchRow()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new MockLegalCadasterQueryService());
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
        viewModel.SearchPid = "missing-pid";

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal(0, viewModel.QueryResults.Count, "No-record query should not render a blank Search Results row.");
        TestAssert.False(viewModel.HasQueryResults, "Search Results grid should collapse when no returned records exist.");
        TestAssert.Equal("PID search: no records found.", viewModel.EvidenceSearchStatusMessage, "No-record query should remain visible as a status message.");
        TestAssert.Equal(0, viewModel.ValuableEvidenceItems.Count, "No-record search must not be retained as valuable evidence.");
    }

    public static async Task PartyMatchesRenderSeparatelyAndPersistWhenKept()
    {
        using var fixture = CreateCaseFolderWithSource();
        var viewModel = CreateViewModel(new PartyMatchLegalCadasterQueryService());
        viewModel.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());
        viewModel.SelectedEvidenceSearchMode = CompareEvidenceSearchMode.LandValuationNumber;
        viewModel.SearchLandValuationNumber = "16505010005";

        await viewModel.RunEvidenceSearchAsync();

        TestAssert.Equal(0, viewModel.QueryResults.Count, "Party-shaped rows should not render in the property Search Results grid.");
        TestAssert.False(viewModel.HasQueryResults, "Property Search Results should stay collapsed for party-only matches.");
        TestAssert.Equal(1, viewModel.RelatedPartyMatches.Count, "Party-shaped rows should render in Related Party Matches.");
        TestAssert.True(viewModel.HasRelatedPartyMatches, "Related Party Matches should be visible when party rows are returned.");
        TestAssert.Equal("KING, WILTON F.", viewModel.RelatedPartyMatches[0].PartyName, "Party match name mismatch.");
        TestAssert.Equal("100778284", viewModel.RelatedPartyMatches[0].Prid, "Party match PRID mismatch.");

        viewModel.MarkPartyMatchValuableCommand.Execute(viewModel.RelatedPartyMatches[0]);

        TestAssert.Equal(1, viewModel.ValuableEvidenceItems.Count, "Keeping a party match should add Valuable Evidence.");
        TestAssert.True(viewModel.ValuableEvidenceItems[0].DisplaySummary.Contains("Party match: KING, WILTON F.", StringComparison.Ordinal), "Valuable party summary should be explicit.");
        TestAssert.True(viewModel.ValuableEvidenceItems[0].DisplaySummary.Contains("PRID: 100778284", StringComparison.Ordinal), "Valuable party summary should retain PRID.");

        viewModel.SaveProgressCommand.Execute(null);

        var restored = CreateViewModel(new UnsupportedLegalCadasterQueryService());
        restored.ApplyLoadState(ReadyState(fixture.Layout.RootDirectory), fixture.Reopen());

        TestAssert.Equal(1, restored.ValuableEvidenceItems.Count, "Kept party evidence should restore from the Compare draft.");
        TestAssert.True(restored.ValuableEvidenceItems[0].DisplaySummary.Contains("KING, WILTON F.", StringComparison.Ordinal), "Restored party evidence should retain party name.");
        TestAssert.True(restored.ValuableEvidenceItems[0].DisplaySummary.Contains("PRID: 100778284", StringComparison.Ordinal), "Restored party evidence should retain PRID.");
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

    private static CompareWorkspaceViewModel CreateViewModel(
        ILegalCadasterQueryService? legalService = null,
        ICompareTaskLifecycleService? taskLifecycleService = null,
        ICompareReportAttachmentService? reportAttachmentService = null,
        ICompareMapIntegrationService? mapIntegrationService = null,
        ICompareWorkspacePromptService? promptService = null)
    {
        return new CompareWorkspaceViewModel(new SelectedInnolaTransaction(
            "task-1",
            "100000674",
            "TR100000674",
            "Compare Survey Plan",
            "Compare",
            DateTimeOffset.Parse("2026-07-14T00:00:00Z")),
            legalCadasterQueryService: legalService ?? new MockLegalCadasterQueryService(),
            taskLifecycleService: taskLifecycleService,
            reportAttachmentService: reportAttachmentService,
            mapIntegrationService: mapIntegrationService,
            promptService: promptService);
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

    private sealed class CountingLegalCadasterQueryService : ILegalCadasterQueryService
    {
        public int VolumeFolioCallCount { get; private set; }

        public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(string parcelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LegalCadasterQueryResult.NoRecord(new LegalCadasterQuery("parcel_id", parcelId, null, null), DateTimeOffset.UtcNow));
        }

        public Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(string volume, string folio, CancellationToken cancellationToken = default)
        {
            VolumeFolioCallCount++;
            return Task.FromResult(LegalCadasterQueryResult.NoRecord(new LegalCadasterQuery("volume_folio", null, volume, folio), DateTimeOffset.UtcNow));
        }

        public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(string landValuationNumber, string? parish = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LegalCadasterQueryResult.NoRecord(new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber, null, parish), DateTimeOffset.UtcNow));
        }

        public Task<LegalCadasterQueryResult> QueryByNameAsync(string name, string? parish = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LegalCadasterQueryResult.NoRecord(new LegalCadasterQuery("name", null, null, null, null, name, parish), DateTimeOffset.UtcNow));
        }
    }

    private sealed class PartyMatchLegalCadasterQueryService : ILegalCadasterQueryService
    {
        public Task<LegalCadasterQueryResult> QueryByParcelIdAsync(string parcelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreatePartyResult(new LegalCadasterQuery("parcel_id", parcelId, null, null)));
        }

        public Task<LegalCadasterQueryResult> QueryByVolumeFolioAsync(string volume, string folio, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreatePartyResult(new LegalCadasterQuery("volume_folio", null, volume, folio)));
        }

        public Task<LegalCadasterQueryResult> QueryByLandValuationNumberAsync(string landValuationNumber, string? parish = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreatePartyResult(new LegalCadasterQuery("land_valuation_number", null, null, null, landValuationNumber, null, parish)));
        }

        public Task<LegalCadasterQueryResult> QueryByNameAsync(string name, string? parish = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreatePartyResult(new LegalCadasterQuery("name", null, null, null, null, name, parish)));
        }

        private static LegalCadasterQueryResult CreatePartyResult(LegalCadasterQuery query)
        {
            var queryKey = LegalCadasterQueryResult.BuildLegalQueryKey(query);
            return new LegalCadasterQueryResult(
                true,
                false,
                query,
                Array.Empty<LegalCadasterRecord>(),
                CompareEvidenceStatus.NoRecordReturned,
                "Party matches returned",
                null,
                PartyRecords: new[]
                {
                    new LegalCadasterPartyRecord(
                        "KING, WILTON F.",
                        "100778284",
                        "Cave Mountain, Cave Post Office Westmoreland",
                        "TX-55",
                        "reg_status_current",
                        "party_type_individual",
                        "Innola Owner Search",
                        DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
                        queryKey)
                });
        }
    }

    private sealed class RecordingCompareTaskLifecycleService : ICompareTaskLifecycleService
    {
        public TaskCompletionSource<string> SuspendObserved { get; } = new();

        public TaskCompletionSource<string> CompleteObserved { get; } = new();

        public CompareTaskLifecycleResult SuspendResult { get; set; } = CompareTaskLifecycleResult.Succeeded("Suspended.");

        public CompareTaskLifecycleResult CompleteResult { get; set; } = CompareTaskLifecycleResult.Succeeded("Completed.");

        public int SuspendCalls { get; private set; }

        public int CompleteCalls { get; private set; }

        public string? LastTransactionNumber { get; private set; }

        public Task<CompareTaskLifecycleResult> SuspendAsync(string transactionNumber, CancellationToken cancellationToken = default)
        {
            SuspendCalls++;
            LastTransactionNumber = transactionNumber;
            SuspendObserved.TrySetResult(transactionNumber);
            return Task.FromResult(SuspendResult);
        }

        public Task<CompareTaskLifecycleResult> CompleteAsync(string transactionNumber, CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            LastTransactionNumber = transactionNumber;
            CompleteObserved.TrySetResult(transactionNumber);
            return Task.FromResult(CompleteResult);
        }
    }

    private sealed class RecordingCompareReportAttachmentService : ICompareReportAttachmentService
    {
        public int UploadCalls { get; private set; }

        public string? LastTransactionNumber { get; private set; }

        public string? LastPdfReportPath { get; private set; }

        public Task<CompareReportAttachmentResult> UploadAsync(
            SelectedInnolaTransaction transaction,
            string pdfReportPath,
            CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            LastTransactionNumber = transaction.TransactionNumber;
            LastPdfReportPath = pdfReportPath;
            return Task.FromResult(CompareReportAttachmentResult.Succeeded(CompareReportAttachmentService.SourceType, pdfReportPath));
        }
    }

    private sealed class RecordingCompareMapIntegrationService : ICompareMapIntegrationService
    {
        public int CleanupCalls { get; private set; }

        public string? LastGroupLayerName { get; private set; }

        public Task<CompareMapIntegrationResult> AddTransactionGeometryToActiveMapAsync(
            CompareWorkingGeometryLoadPlan plan,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CompareMapIntegrationResult.Loaded(
                "Loaded in test.",
                Array.Empty<string>(),
                ArcGisCompareMapIntegrationService.BuildGroupLayerName(plan),
                1));
        }

        public Task<CompareMapCleanupResult> RemoveTransactionGeometryFromActiveMapAsync(
            string groupLayerName,
            CancellationToken cancellationToken = default)
        {
            CleanupCalls++;
            LastGroupLayerName = groupLayerName;
            return Task.FromResult(CompareMapCleanupResult.Removed(groupLayerName, 1));
        }
    }

    private sealed class RecordingCompareWorkspacePromptService : ICompareWorkspacePromptService
    {
        public bool SaveResult { get; init; } = true;

        public bool SuspendResult { get; init; } = true;

        public bool FinalizeResult { get; init; } = true;

        public int SaveCalls { get; private set; }

        public int SuspendCalls { get; private set; }

        public int FinalizeCalls { get; private set; }

        public bool ConfirmSave()
        {
            SaveCalls++;
            return SaveResult;
        }

        public bool ConfirmSuspend()
        {
            SuspendCalls++;
            return SuspendResult;
        }

        public bool ConfirmFinalize(bool reportAlreadyGenerated)
        {
            FinalizeCalls++;
            return FinalizeResult;
        }
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
