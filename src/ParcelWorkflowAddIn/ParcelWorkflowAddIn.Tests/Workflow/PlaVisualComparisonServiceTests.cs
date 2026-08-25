using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Pla;
using ParcelWorkflowAddIn.Workflow.Review;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class PlaVisualComparisonServiceTests
{
    public static void GenerateVisualEvidencePersistsSvgAndMetadata()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WritePlaSelection(layout);
        var review = CreateReviewedBoundary();
        var output = CreateOutputSummary(layout);
        var service = new PlaVisualComparisonService(() => FixedNow());

        var result = service.GenerateVisualEvidence(layout, "100001219", review, output, "tester");

        TestAssert.True(result.Success, "PLA visual evidence generation should succeed.");
        TestAssert.True(File.Exists(result.Document!.GeometryVisualPath), "Generated visual SVG should exist.");
        TestAssert.True(File.Exists(PlaVisualComparisonService.GetComparisonArtifactPath(layout)), "Comparison metadata should persist.");
        TestAssert.Equal("approximate_visual_similarity", result.Document.ComparisonMode, "PLA comparison must stay approximate.");
        TestAssert.True(result.Document.Disclaimer.Contains("not survey-accurate", StringComparison.OrdinalIgnoreCase), "Visual evidence must not claim survey-accurate alignment.");
        TestAssert.True(result.Document.GeneratedGeometryPointCount > 0, "Generated visual metadata should include solved point count.");
        TestAssert.True(File.ReadAllText(result.Document.GeometryVisualPath).Contains("<svg", StringComparison.OrdinalIgnoreCase), "Generated visual artifact should be SVG.");
    }

    public static void SaveReviewDecisionPersistsStatusAndNotes()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WritePlaSelection(layout);
        var service = new PlaVisualComparisonService(() => FixedNow());
        var generated = service.GenerateVisualEvidence(layout, "100001219", CreateReviewedBoundary(), CreateOutputSummary(layout), "tester");

        var result = service.SaveReviewDecision(layout, "accepted", "Looks consistent for local-origin geometry.", "reviewer");
        var reloaded = service.Load(layout);

        TestAssert.True(generated.Success && result.Success, "Generate and review decision save should succeed.");
        TestAssert.Equal("accepted", reloaded?.ReviewerDecision, "Reviewer decision should persist.");
        TestAssert.Equal("Looks consistent for local-origin geometry.", reloaded?.ReviewerNotes, "Reviewer notes should persist.");
        TestAssert.Equal("reviewer", reloaded?.ReviewedBy, "Reviewer identity should persist.");
    }

    private static CaseFolderLayout CreateLayout(string root)
    {
        var layout = CaseFolderLayout.For(root, "100001219");
        Directory.CreateDirectory(layout.SourceDirectory);
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        return layout;
    }

    private static void WritePlaSelection(CaseFolderLayout layout)
    {
        var sourcePath = Path.Combine(layout.SourceDirectory, "1000-55.pdf");
        File.WriteAllText(sourcePath, "%PDF");
        var workingDirectory = PlaPlanEvidenceSelectionService.GetWorkingDirectory(layout);
        Directory.CreateDirectory(workingDirectory);
        File.WriteAllText(PlaPlanEvidenceSelectionService.GetPdfEvidencePath(layout), "%PDF selected");
        File.WriteAllText(
            PlaPlanEvidenceSelectionService.GetSelectionArtifactPath(layout),
            """
            {
              "schema_version": "1.0.0",
              "transaction_number": "100001219",
              "source_type": "st_plan_annexation_pdf",
              "source_relative_path": "source/1000-55.pdf",
              "selected_page_number": 2,
              "selection_type": "full_page",
              "page_width_points": 612,
              "page_height_points": 792,
              "generated_plan_evidence_path": "working/pla_plan_annexation/pla_selected_plan.pdf",
              "generated_plan_evidence_format": "pdf",
              "created_at_utc": "2026-08-24T12:00:00Z",
              "updated_at_utc": "2026-08-24T12:00:00Z"
            }
            """);
    }

    private static ExtractionReviewDocument CreateReviewedBoundary()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100001219",
            ExtractionSource = "pla_plan_annexation_selected_plan"
        };
        document.RootMetadata["primary_source_role"] = "plan_annexation_pdf";
        document.Segments.AddRange(new[]
        {
            Segment("s1", 1, "A", "B", "N 90 E", "10"),
            Segment("s2", 2, "B", "C", "S 0 E", "10"),
            Segment("s3", 3, "C", "D", "S 90 W", "10"),
            Segment("s4", 4, "D", "A", "N 0 E", "10")
        });
        document.SegmentRowCount = document.Segments.Count;
        return document;
    }

    private static ExtractionReviewSegment Segment(string id, int sequence, string from, string to, string bearing, string distance)
    {
        return new ExtractionReviewSegment
        {
            SegmentId = id,
            Sequence = sequence,
            FromPoint = from,
            ToPoint = to,
            BearingText = bearing,
            DistanceText = distance,
            IncludeInBoundary = true,
            Status = "reviewed"
        };
    }

    private static OutputSummaryDocument CreateOutputSummary(CaseFolderLayout layout)
    {
        var geoJsonPath = Path.Combine(layout.OutputDirectory, "parcel.geojson");
        File.WriteAllText(geoJsonPath, "{}");
        return new OutputSummaryDocument(
            "1.0.0",
            "100001219",
            "run-test",
            FixedNow().UtcDateTime.ToString("O"),
            "tester",
            string.Empty,
            new OutputSummaryPayload(
                "created",
                "normal",
                null,
                new[] { geoJsonPath },
                Array.Empty<string>(),
                null,
                null,
                geoJsonPath,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                BuiltParcelCount: 1,
                BuiltLineCount: 4,
                BuiltPointCount: 4,
                PointCount: 4,
                LineCount: 4,
                PolygonCount: 1,
                TemplateProjectPath: null,
                TemplateGdbPath: null,
                ReviewResultOwner: "approved_review"),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static DateTimeOffset FixedNow() => new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
}
