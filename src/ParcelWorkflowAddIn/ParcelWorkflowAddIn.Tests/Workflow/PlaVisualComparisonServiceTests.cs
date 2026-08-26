using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Pla;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.SpatialReview;
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

    public static void LoadAcceptsTitlePlanOverlayAsVisualComparisonEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WritePlaSelection(layout);
        WriteTitlePlanOverlayArtifact(layout);
        var service = new PlaVisualComparisonService(() => FixedNow());

        var document = service.Load(layout);

        TestAssert.Equal(PlaVisualComparisonService.ComparisonModeTitlePlanOverlay, document?.ComparisonMode, "PLA visual comparison should load the title-plan overlay artifact when native comparison metadata is absent.");
        TestAssert.Equal("accepted", document?.ReviewerDecision, "A persisted title-plan overlay should satisfy the examiner visual-comparison decision gate.");
        TestAssert.True(document?.GeometryVisualPath.EndsWith("title_plan_overlay_100001219.png", StringComparison.OrdinalIgnoreCase) == true, "Visual comparison should point to the captured overlay image.");
    }

    public static void LoadDoesNotFallbackWhenNativeComparisonArtifactIsCorrupt()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WritePlaSelection(layout);
        WriteTitlePlanOverlayArtifact(layout);
        Directory.CreateDirectory(PlaVisualComparisonService.GetWorkingDirectory(layout));
        File.WriteAllText(PlaVisualComparisonService.GetComparisonArtifactPath(layout), "{ invalid json");
        var service = new PlaVisualComparisonService(() => FixedNow());

        var document = service.Load(layout);

        TestAssert.Equal(null, document, "Corrupt native PLA visual comparison metadata should not be silently replaced by overlay fallback evidence.");
    }

    public static void LoadRejectsTitlePlanOverlayForDifferentTransaction()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WritePlaSelection(layout);
        WriteTitlePlanOverlayArtifact(layout, transactionNumber: "100009999");
        var service = new PlaVisualComparisonService(() => FixedNow());

        var document = service.Load(layout);

        TestAssert.Equal(null, document, "Title-plan overlay fallback must not satisfy visual comparison readiness for a different transaction.");
    }

    public static void LoadAcceptsCurrentSpatialReviewApprovalAsVisualComparisonEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateLayout(tempRoot.Path);
        WritePlaSelection(layout);
        var output = CreateOutputSummary(layout);
        new OutputSummaryPersistenceService().Save(layout, output);
        new SpatialReviewApprovalPersistenceService().Save(layout, output, "reviewer");
        var service = new PlaVisualComparisonService(() => FixedNow());

        var document = service.Load(layout);

        TestAssert.Equal(PlaVisualComparisonService.ComparisonModeSpatialReviewApproval, document?.ComparisonMode, "Current spatial-review approval should bridge PLA visual comparison readiness when native comparison metadata is absent.");
        TestAssert.Equal("accepted", document?.ReviewerDecision, "Spatial review approval should satisfy the PLA visual comparison decision gate.");
        TestAssert.Equal("reviewer", document?.ReviewedBy, "Spatial review approval reviewer should be retained.");
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

    private static void WriteTitlePlanOverlayArtifact(CaseFolderLayout layout, string transactionNumber = "100001219")
    {
        var overlayDirectory = Path.Combine(layout.WorkingDirectory, "title_plan_overlay");
        Directory.CreateDirectory(overlayDirectory);
        var imagePath = Path.Combine(overlayDirectory, $"title_plan_overlay_{transactionNumber}.png");
        var worldPath = Path.ChangeExtension(imagePath, ".pgw");
        var projectionPath = Path.ChangeExtension(imagePath, ".prj");
        File.WriteAllText(imagePath, "png");
        File.WriteAllText(worldPath, "world");
        File.WriteAllText(projectionPath, "prj");

        var outputGdb = Path.Combine(layout.OutputDirectory, $"{transactionNumber}_parcel_output.gdb");
        Directory.CreateDirectory(outputGdb);
        var artifact = new MapGeoreferenceOverlayArtifactDocument(
            transactionNumber,
            imagePath,
            worldPath,
            projectionPath,
            outputGdb,
            $"title_plan_overlay_{transactionNumber}",
            Path.Combine(outputGdb, $"title_plan_overlay_{transactionNumber}"),
            FixedNow(),
            1200,
            900,
            new MapGeoreferenceImagePoint(10, 20),
            new MapGeoreferenceImagePoint(300, 220),
            new MapGeoreferenceCoordinatePoint(750000, 650000),
            new MapGeoreferenceCoordinatePoint(750050, 650025),
            nameof(MapGeoreferenceOverlayKind.TitlePlanComparison),
            "TwoPointSimilarity",
            Path.Combine(layout.SourceDirectory, "1000-55.pdf"),
            2);

        File.WriteAllText(
            MapGeoreferenceOverlayArtifactPlan.BuildMetadataPath(layout.RootDirectory, MapGeoreferenceOverlayKind.TitlePlanComparison),
            JsonSerializer.Serialize(artifact, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private static DateTimeOffset FixedNow() => new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
}
