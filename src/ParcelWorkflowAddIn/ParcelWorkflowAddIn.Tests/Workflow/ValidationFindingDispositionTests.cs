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

        TestAssert.Equal(1, rows.Count, "One validation finding row should be shown.");
        TestAssert.Equal(ValidationFindingDispositionDecision.Override, rows[0].Decision, "Persisted decision should be projected.");
        TestAssert.Equal("override", rows[0].DecisionLabel, "Decision label should be readable.");
        TestAssert.Equal("pages=2; mismatches=1", rows[0].Evidence, "Evidence should be visible in the row.");
        TestAssert.Equal("Reviewer accepted after checking compute sheet.", rows[0].Comment, "Persisted comment should be projected for edit/re-save.");
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
}



