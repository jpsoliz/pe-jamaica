using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Validation;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ValidationFindingDispositionTests
{
    public static void PersistenceSaveLoadAndUpsertRoundTripsReviewerDecision()
    {
        using var tempRoot = new TempDirectory();
        var layout = CaseFolderLayout.For(tempRoot.Path, "TR100000896");
        var service = new ValidationFindingDispositionPersistenceService();
        var item = new ValidationFindingDispositionItem(
            "georeference.parish_point_within_boundary|Point outside parish|high|failed",
            "georeference.parish_point_within_boundary",
            "Point outside parish",
            "high",
            "failed",
            ValidationFindingDispositionDecision.ManualReview,
            "Reviewer wants GIS check.",
            "tester",
            "2026-09-02T12:00:00Z",
            "parish=Clarendon; outside_points=P1");

        var document = service.Upsert(layout, "TR100000896", item);
        var loaded = service.Load(layout);

        TestAssert.True(File.Exists(service.GetDispositionPath(layout)), "Disposition artifact should be written.");
        TestAssert.Equal("TR100000896", document.TransactionId, "Transaction id should be recorded.");
        TestAssert.True(loaded is not null, "Disposition artifact should reload.");
        TestAssert.Equal(1, loaded!.Items.Count, "One disposition should be recorded.");
        TestAssert.Equal(ValidationFindingDispositionDecision.ManualReview, loaded.Items[0].Decision, "Decision should round-trip.");
        TestAssert.Equal("tester", loaded.Items[0].OperatorId, "Operator should round-trip.");
    }

    public static void ProjectorBuildsRowsWithPersistedDispositionLabels()
    {
        var finding = new ValidationFinding(
            "pxa.plan_compute_sheet_consistency",
            "Plan values and embedded computation-sheet values do not match.",
            "high",
            "failed",
            "pages=2; mismatches=1",
            "Review the computation sheet and plan values before approval.");
        var summary = new ValidationSummaryDocument(
            "1.0.0",
            "TR100000896",
            "validation-test",
            "2026-09-02T12:00:00Z",
            "tester",
            "hash",
            new ValidationSummaryPayload(
                "blocked",
                "sidwell_validation_v1",
                "1.0.0",
                new ValidationFindingCounts(0, 1, 0, 0, 0),
                null,
                Array.Empty<ValidationClosureResult>(),
                null,
                Array.Empty<ValidationReadinessResult>(),
                null,
                Array.Empty<ValidationOrientationResult>(),
                new[] { finding }),
            Array.Empty<string>(),
            Array.Empty<string>());
        var key = ValidationFindingDispositionProjector.BuildFindingKey(finding);
        var disposition = new ValidationFindingDispositionDocument(
            "1.0.0",
            "TR100000896",
            "2026-09-02T12:05:00Z",
            new[]
            {
                new ValidationFindingDispositionItem(
                    key,
                    finding.RuleId,
                    finding.Title,
                    finding.Severity,
                    finding.Status,
                    ValidationFindingDispositionDecision.Override,
                    "Reviewer accepted after checking compute sheet.",
                    "tester",
                    "2026-09-02T12:05:00Z",
                    finding.Evidence)
            });

        var rows = ValidationFindingDispositionProjector.BuildRows(summary, disposition);
        var projectedFinding = rows.Single(row => row.RuleId == finding.RuleId && row.Status == finding.Status);

        TestAssert.Equal(5, rows.Count, "The real finding plus missing baseline validation rows should be shown.");
        TestAssert.Equal(ValidationFindingDispositionDecision.Override, projectedFinding.Decision, "Persisted decision should be projected.");
        TestAssert.Equal("override", projectedFinding.DecisionLabel, "Decision label should be readable.");
        TestAssert.Equal("pages=2; mismatches=1", projectedFinding.Evidence, "Evidence should be visible in the row.");
        TestAssert.Equal("Reviewer accepted after checking compute sheet.", projectedFinding.Comment, "Persisted comment should be projected for edit/re-save.");
        TestAssert.True(rows.Any(row => row.RuleId == "georeference.parish_point_within_boundary" && row.DisplayStatus == "Not available"), "Missing baseline checks should be visible with a Not available display status.");
        TestAssert.Equal("Plan and Compute Sheet Match", projectedFinding.DisplayRuleName, "Rule id should be shown as a friendly label.");
        TestAssert.Equal("Not found", projectedFinding.DisplayStatus, "Failed validation rows should show Not found.");
    }

    public static void ProjectorShowsBaselineRowsWhenNoFindingsAreLoaded()
    {
        var rows = ValidationFindingDispositionProjector.BuildRows(null, null);

        TestAssert.Equal(5, rows.Count, "All Story 4-13 validation points should be visible before findings are loaded.");
        TestAssert.True(rows.All(row => row.Status == "N/A"), "Placeholder validation rows should retain N/A source status.");
        TestAssert.True(rows.All(row => row.DisplayStatus == "Not available"), "Placeholder validation rows should show Not available status.");
        TestAssert.True(rows.Any(row => row.DisplayRuleName == "Parcel Boundary within Parish"), "Parish polygon boundary validation should be visible with a friendly rule name.");
        TestAssert.True(rows.Any(row => row.DisplayRuleName == "Printed Text Height"), "Printed text height validation should be visible with a friendly rule name.");
    }

    public static void ProjectorBuildsParishPointFindingText()
    {
        var passed = new ValidationFindingDispositionRow(
            "key",
            "georeference.parish_point_within_boundary",
            "Reviewed points are inside the extracted parish boundary.",
            "passed",
            "passed",
            "parish=Clarendon; checked_points=9; outside_points=none; parcel_name=survey-plan-parcel",
            string.Empty,
            ValidationFindingDispositionDecision.Pending,
            string.Empty,
            string.Empty);
        var blocked = new ValidationFindingDispositionRow(
            "key",
            "georeference.parish_point_within_boundary",
            "Reviewed points fall outside the extracted parish boundary.",
            "high",
            "blocker",
            "parish=Clarendon; checked_points=9; outside_points=666,667,668",
            string.Empty,
            ValidationFindingDispositionDecision.Pending,
            string.Empty,
            string.Empty);

        TestAssert.Equal("Points within Parish", passed.DisplayRuleName, "Point parish rule should use the examiner-facing label.");
        TestAssert.Equal("Found", passed.DisplayStatus, "Passed point parish validation should show Found.");
        TestAssert.Equal("All boundary points from parcel survey-plan-parcel are located inside the Clarendon parish.", passed.DisplayFinding, "Passed point parish validation should mention the parcel and parish.");
        TestAssert.Equal("Not found", blocked.DisplayStatus, "Blocked point parish validation should show Not found.");
        TestAssert.Equal("the following points [666,667,668] are located outside the Clarendon parish.", blocked.DisplayFinding, "Blocked point parish validation should list outside points and parish.");
    }
    public static void ProjectorExplainsComputeSheetAndPrintedTextFindings()
    {
        var missingSheet = new ValidationFindingDispositionRow(
            "key",
            "pxa.embedded_compute_sheet_detected",
            "No embedded computation sheet was detected in the source document.",
            "info",
            "not_available",
            "embedded_compute_sheet=not_detected",
            string.Empty,
            ValidationFindingDispositionDecision.Pending,
            string.Empty,
            string.Empty);
        var unavailableMatch = new ValidationFindingDispositionRow(
            "key",
            "pxa.plan_compute_sheet_consistency",
            "An embedded computation sheet was detected, but no comparable values were available.",
            "warning",
            "not_available",
            "pages=unknown; plan_points=9; sheet_points=0",
            string.Empty,
            ValidationFindingDispositionDecision.Pending,
            string.Empty,
            string.Empty);
        var printedText = new ValidationFindingDispositionRow(
            "key",
            "document.printed_text_height",
            "Printed ordinary text height satisfies the configured threshold.",
            "passed",
            "passed",
            "observed_mm=2.100; threshold_mm=2.0; measured_scope=ordinary_plan_text_excludes_titles_subtitles; excluded_title_subtitle_runs=2; page_standard=pdf_metadata",
            string.Empty,
            ValidationFindingDispositionDecision.Pending,
            string.Empty,
            string.Empty);

        TestAssert.Equal("Embedded Compute Sheet", missingSheet.DisplayRuleName, "Compute sheet detection should use a friendly rule name.");
        TestAssert.True(missingSheet.DisplayFinding.Contains("No embedded compute sheet structure has been captured yet", StringComparison.Ordinal), "Missing compute sheet finding should explain extraction capture is missing.");
        TestAssert.Equal("Not available", missingSheet.DisplayStatus, "Missing compute sheet extraction should show Not available.");
        TestAssert.Equal("Not available", unavailableMatch.DisplayStatus, "Incomplete comparison data should show Not available.");
        TestAssert.True(unavailableMatch.DisplayFinding.Contains("plan points: 9", StringComparison.Ordinal) && unavailableMatch.DisplayFinding.Contains("compute sheet points: 0", StringComparison.Ordinal), "Plan/compute finding should explain missing comparable values.");
        TestAssert.True(printedText.DisplayFinding.Contains("Ordinary plan text height", StringComparison.Ordinal) && printedText.DisplayFinding.Contains("Title/subtitle-like text runs excluded: 2", StringComparison.Ordinal), "Printed text finding should describe ordinary text measurement scope.");
    }

    public static void DockpaneXamlExposesValidationFindingDispositionActions()
    {
        var xaml = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpane.xaml"));
        var viewModel = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(xaml.Contains("ValidationFindingRows", StringComparison.Ordinal), "Validation card should bind to finding rows.");
        TestAssert.True(xaml.Contains("AcceptValidationFindingCommand", StringComparison.Ordinal), "Validation card should expose Accept action.");
        TestAssert.True(xaml.Contains("RejectValidationFindingCommand", StringComparison.Ordinal), "Validation card should expose Reject action.");
        TestAssert.True(xaml.Contains("OverrideValidationFindingCommand", StringComparison.Ordinal), "Validation card should expose Override action.");
        TestAssert.True(xaml.Contains("SendValidationFindingToManualReviewCommand", StringComparison.Ordinal), "Validation card should expose Manual Review action.");
        TestAssert.True(xaml.Contains("Text=\"{Binding Comment, UpdateSourceTrigger=PropertyChanged}\"", StringComparison.Ordinal), "Validation card should expose reviewer comment input.");
        TestAssert.True(viewModel.Contains("validation_finding_dispositions.json", StringComparison.Ordinal) || viewModel.Contains("ValidationFindingDispositionPersistenceService", StringComparison.Ordinal), "ViewModel should persist validation finding decisions.");
    }

    public static void ReviewWorkspaceExposesValidationFindingDecisionTab()
    {
        var xaml = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "JamaicaReviewWorkspaceWindow.xaml"));
        var workspaceViewModel = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "JamaicaReviewWorkspaceViewModel.cs"));
        var dockpaneViewModel = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpaneViewModel.cs"));
        var previewService = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Workflow", "Validation", "ValidationPreviewExecutionService.cs"));

        TestAssert.True(xaml.Contains("Header=\"Validation Findings\"", StringComparison.Ordinal), "Points Validation Tool should expose a dedicated validation findings tab.");
        TestAssert.True(xaml.Contains("Visibility=\"{Binding ShowValidationFindingsTab, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal), "Validation findings tab should be present in the PXA review form.");
        TestAssert.True(xaml.Contains("ItemsSource=\"{Binding ValidationFindingRows}\"", StringComparison.Ordinal), "Validation findings tab should bind to projected finding rows.");
        TestAssert.True(xaml.Contains("ValidationFindingsSummary", StringComparison.Ordinal) && xaml.Contains("ValidationFindingsHelpText", StringComparison.Ordinal), "Validation findings tab should summarize the decision context.");
        TestAssert.True(xaml.Contains("DisplayRuleName", StringComparison.Ordinal), "Validation findings tab should show friendly rule names.");
        TestAssert.True(xaml.Contains("DisplayFinding", StringComparison.Ordinal), "Validation findings tab should show examiner-facing finding text.");
        TestAssert.True(xaml.Contains("DisplayStatus", StringComparison.Ordinal), "Validation findings tab should show normalized status text.");
        TestAssert.False(xaml.Contains("Header=\"Decision\"", StringComparison.Ordinal), "Validation findings tab should not expose a Decision column.");
        TestAssert.True(
            workspaceViewModel.Contains("ValidationFindingRows => parent.ValidationFindingRows", StringComparison.Ordinal)
            && workspaceViewModel.Contains("ShowValidationFindingsTab => IsPxaSurveyPlanReview", StringComparison.Ordinal)
            && workspaceViewModel.Contains("ValidationFindingRows.CollectionChanged", StringComparison.Ordinal),
            "Workspace view-model should project validation findings and refresh when the parent rows change.");
        TestAssert.True(
            dockpaneViewModel.Contains("public ICommand AcceptValidationFindingCommand", StringComparison.Ordinal)
            && dockpaneViewModel.Contains("RecordValidationFindingDisposition(parameter, ValidationFindingDispositionDecision.Accepted)", StringComparison.Ordinal),
            "Dockpane view-model should expose concrete validation finding disposition commands for workspace reuse.");
        TestAssert.True(
            dockpaneViewModel.Contains("await RefreshValidationFindingRowsAsync().ConfigureAwait(true);", StringComparison.Ordinal)
            && dockpaneViewModel.Contains("workflowSession.CurrentValidationSummary ?? currentValidationPreviewSummary", StringComparison.Ordinal)
            && dockpaneViewModel.Contains("currentValidationPreviewSummary = null;", StringComparison.Ordinal),
            "Dockpane view-model should run validation preview for PXA review rows and let formal validation replace it.");
        TestAssert.True(
            previewService.Contains("validation_preview_summary.json", StringComparison.Ordinal)
            && previewService.Contains("--approved-review", StringComparison.Ordinal)
            && previewService.Contains("--review-data", StringComparison.Ordinal),
            "Validation preview should use the adapter against current review data without writing the formal validation summary.");
    }
    public static void ReviewWorkspaceOwnersNeighborsUsesEditableRoleCombos()
    {
        var xaml = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "JamaicaReviewWorkspaceWindow.xaml"));
        var workspaceViewModel = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "JamaicaReviewWorkspaceViewModel.cs"));
        var ownersStart = xaml.IndexOf("Header=\"Owners / Neighbors\"", StringComparison.Ordinal);
        var boundaryStart = xaml.IndexOf("Header=\"Boundary Segments\"", StringComparison.Ordinal);

        TestAssert.True(ownersStart >= 0 && boundaryStart > ownersStart, "Owners / Neighbors tab should be present before Boundary Segments.");
        var ownersTab = xaml[ownersStart..boundaryStart];

        TestAssert.True(ownersTab.Contains("OwnerNeighborRoleOptions", StringComparison.Ordinal), "Owners / Neighbors role column should bind to the controlled role options.");
        TestAssert.True(ownersTab.Contains("Text=\"{Binding Role, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", StringComparison.Ordinal), "Owners / Neighbors role choices should update the row Role while preserving existing role text.");
        TestAssert.True(workspaceViewModel.Contains("OwnerNeighborRoleOptions", StringComparison.Ordinal), "Workspace view-model should expose the role choices.");
        TestAssert.True(workspaceViewModel.Contains("\"Instigator\"", StringComparison.Ordinal), "Role choices should include Instigator.");
        TestAssert.True(workspaceViewModel.Contains("\"Owner\"", StringComparison.Ordinal), "Role choices should include Owner.");
        TestAssert.True(workspaceViewModel.Contains("\"Neighbor\"", StringComparison.Ordinal), "Role choices should include Neighbor.");
        TestAssert.True(workspaceViewModel.Contains("\"Representative\"", StringComparison.Ordinal), "Role choices should include Representative.");
        TestAssert.True(workspaceViewModel.Contains("\"Other\"", StringComparison.Ordinal), "Role choices should include Other.");
        TestAssert.False(ownersTab.Contains("Header=\"From\"", StringComparison.Ordinal), "Owners / Neighbors tab should not show From columns.");
        TestAssert.False(ownersTab.Contains("Header=\"To\"", StringComparison.Ordinal), "Owners / Neighbors tab should not show To columns.");
        TestAssert.True(ownersTab.Contains("Header=\"Lot Number\"", StringComparison.Ordinal), "Adjacent owner grid should expose Lot Number.");
        TestAssert.True(ownersTab.Contains("Binding=\"{Binding LotNumber, UpdateSourceTrigger=LostFocus}\"", StringComparison.Ordinal), "Adjacent owner Lot Number edits should commit when the cell edit finishes.");
        TestAssert.True(ownersTab.Contains("Header=\"Address\"", StringComparison.Ordinal), "Adjacent owner grid should expose Address.");
        TestAssert.True(ownersTab.Contains("Binding=\"{Binding Address, UpdateSourceTrigger=LostFocus}\"", StringComparison.Ordinal), "Adjacent owner Address edits should commit when the cell edit finishes.");
        TestAssert.True(ownersTab.Contains("Header=\"LandVal No.\"", StringComparison.Ordinal), "Adjacent owner grid should expose LandVal No.");
        TestAssert.True(ownersTab.Contains("Binding=\"{Binding LandValuationNumber, UpdateSourceTrigger=LostFocus}\"", StringComparison.Ordinal), "Adjacent owner LandVal No. edits should commit when the cell edit finishes.");
        TestAssert.True(ownersTab.Contains("Header=\"Exam No\"", StringComparison.Ordinal), "Adjacent owner grid should expose Exam No.");
        TestAssert.True(ownersTab.Contains("Binding=\"{Binding ExaminationNumber, UpdateSourceTrigger=LostFocus}\"", StringComparison.Ordinal), "Adjacent owner Exam No edits should commit when the cell edit finishes.");
        TestAssert.True(ownersTab.Contains("Header=\"Volume\"", StringComparison.Ordinal), "Adjacent owner grid should use the full Volume header.");
        TestAssert.False(ownersTab.Contains("Header=\"Vol.\"", StringComparison.Ordinal), "Adjacent owner grid should not abbreviate Volume as Vol.");
        TestAssert.True(ownersTab.Contains("Binding=\"{Binding Volume, UpdateSourceTrigger=LostFocus}\"", StringComparison.Ordinal), "Adjacent owner Volume edits should commit when the cell edit finishes.");
        TestAssert.True(ownersTab.Contains("Binding=\"{Binding Folio, UpdateSourceTrigger=LostFocus}\"", StringComparison.Ordinal), "Adjacent owner Folio edits should commit when the cell edit finishes.");
        TestAssert.False(ownersTab.Contains("Header=\"Status\"", StringComparison.Ordinal), "Owners / Neighbors tab should not show Status columns.");
    }
}