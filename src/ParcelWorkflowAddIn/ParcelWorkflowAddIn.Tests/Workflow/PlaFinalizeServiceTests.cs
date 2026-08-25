using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Pla;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class PlaFinalizeServiceTests
{
    public static async Task UploadGeneratedOutputsUsesResolvedPlaSourceTypes()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateReadyPlaCase(tempRoot.Path, outputCount: 2);
        var uploader = new RecordingPlaUploader();
        var service = new PlaFinalizeService(uploader, getUtcNow: () => FixedNow());

        var result = await service.UploadGeneratedOutputsAsync(layout, CreateTransaction(), "tester");
        var evidence = service.LoadEvidence(layout);

        TestAssert.True(result.Success, "PLA finalize should upload generated output documents.");
        TestAssert.Equal(2, uploader.Uploads.Count, "Two generated PLA output documents should upload.");
        TestAssert.Equal("st_plan_annex_output", uploader.Uploads[0].SourceType, "First PLA output must use the first configured source type.");
        TestAssert.Equal("st_plan_annex_output2", uploader.Uploads[1].SourceType, "Second PLA output must use the second configured source type.");
        TestAssert.Equal(PlaFinalizeService.UploadedStatus, evidence?.UploadStatus, "Finalize evidence should persist uploaded status.");
    }

    public static async Task UploadFailureBlocksRemainingOutputsAndPersistsRetryEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateReadyPlaCase(tempRoot.Path, outputCount: 2);
        var uploader = new RecordingPlaUploader(failOnUpload: 1);
        var service = new PlaFinalizeService(uploader, getUtcNow: () => FixedNow());

        var result = await service.UploadGeneratedOutputsAsync(layout, CreateTransaction(), "tester");
        var evidence = service.LoadEvidence(layout);

        TestAssert.False(result.Success, "PLA finalize should stop on upload failure.");
        TestAssert.Equal(1, uploader.Uploads.Count, "Finalize must not upload later outputs after a failure.");
        TestAssert.Equal(PlaFinalizeService.FailedStatus, evidence?.UploadStatus, "Failure evidence should persist for retry.");
        TestAssert.Equal("upload_failed", evidence?.ErrorCategory, "Failure evidence should keep a retryable category.");
    }

    public static async Task UploadGeneratedOutputsIgnoresStrayOutputPdfs()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateReadyPlaCase(tempRoot.Path, outputCount: 2);
        var staleDirectory = Path.Combine(layout.OutputDirectory, "old-run");
        Directory.CreateDirectory(staleDirectory);
        File.WriteAllText(Path.Combine(staleDirectory, "aaa-stale-output.pdf"), "%PDF stale");
        var uploader = new RecordingPlaUploader();
        var service = new PlaFinalizeService(uploader, getUtcNow: () => FixedNow());

        var result = await service.UploadGeneratedOutputsAsync(layout, CreateTransaction(), "tester");

        TestAssert.True(result.Success, "PLA finalize should upload explicit generated output documents.");
        TestAssert.Equal(2, uploader.Uploads.Count, "Only current output summary PDFs should upload.");
        TestAssert.True(uploader.Uploads.All(upload => !upload.PdfPath.Contains("old-run", StringComparison.OrdinalIgnoreCase)), "Stray PDFs under output should not upload.");
        TestAssert.Equal("pla-output-1.pdf", Path.GetFileName(uploader.Uploads[0].PdfPath), "Source type order should follow output summary artifact order.");
        TestAssert.Equal("st_plan_annex_output", uploader.Uploads[0].SourceType, "First explicit output keeps first PLA source type.");
        TestAssert.Equal("pla-output-2.pdf", Path.GetFileName(uploader.Uploads[1].PdfPath), "Second source type should not be shifted by stray PDFs.");
        TestAssert.Equal("st_plan_annex_output2", uploader.Uploads[1].SourceType, "Second explicit output keeps second PLA source type.");
    }

    public static void CompleteTransactionSkipsComputeDispositionForPlaBranch()
    {
        var source = File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs"));
        var guardIndex = source.IndexOf("if (!IsPlaPlanAnnexationWorkflow)", StringComparison.Ordinal);
        var publishIndex = source.IndexOf("PublishEnterpriseWorkingReviewAsync", StringComparison.Ordinal);
        var dispositionIndex = source.IndexOf("RecordComputeDispositionAsync", StringComparison.Ordinal);
        var lifecycleIndex = source.IndexOf("ShellState.LifecycleCoordinator.CompleteAsync", StringComparison.Ordinal);

        TestAssert.True(guardIndex >= 0, "Finalize should branch away from Compute publish/disposition for PLA.");
        TestAssert.True(publishIndex > guardIndex, "Compute publish should be inside the non-PLA branch.");
        TestAssert.True(dispositionIndex > guardIndex, "Compute disposition should be inside the non-PLA branch.");
        TestAssert.True(lifecycleIndex > dispositionIndex, "Lifecycle complete should remain after the non-PLA Compute branch.");
    }

    public static void ReopenLoadsPersistedFinalizeEvidence()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreateReadyPlaCase(tempRoot.Path, outputCount: 1);
        var service = new PlaFinalizeService(new RecordingPlaUploader(), getUtcNow: () => FixedNow());
        service.UploadGeneratedOutputsAsync(layout, CreateTransaction(), "tester").GetAwaiter().GetResult();

        var reopened = new PlaFinalizeService(new RecordingPlaUploader()).LoadEvidence(layout);

        TestAssert.Equal(PlaFinalizeService.UploadedStatus, reopened?.UploadStatus, "Reopen should load persisted PLA finalize evidence.");
        TestAssert.Equal("st_plan_annex_output", reopened?.UploadItems.Single().SourceType, "Reopen evidence should preserve source type.");
    }

    private static CaseFolderLayout CreateReadyPlaCase(string root, int outputCount)
    {
        var layout = CaseFolderLayout.For(root, "100001219");
        Directory.CreateDirectory(layout.SourceDirectory);
        Directory.CreateDirectory(layout.WorkingDirectory);
        Directory.CreateDirectory(layout.OutputDirectory);
        WriteManifest(layout);
        WritePlaSelection(layout);
        var output = WriteOutputSummary(layout, outputCount);
        var visual = new PlaVisualComparisonService(() => FixedNow());
        var generated = visual.GenerateVisualEvidence(layout, "100001219", CreateReviewedBoundary(), output, "tester");
        TestAssert.True(generated.Success, "Fixture should generate PLA visual comparison.");
        var decision = visual.SaveReviewDecision(layout, "accepted", "Ready to finalize.", "tester");
        TestAssert.True(decision.Success, "Fixture should save PLA visual comparison decision.");
        return layout;
    }

    private static void WriteManifest(CaseFolderLayout layout)
    {
        ManifestSerializer.Write(
            layout.ManifestPath,
            new ManifestDocument(
                "1.0.0",
                "100001219",
                "run-test",
                FixedNow().UtcDateTime.ToString("O"),
                "tester",
                null,
                new ManifestPayload(
                    "spatial_review_approved",
                    Array.Empty<ManifestSourceFile>(),
                    null,
                    null,
                    null,
                    null,
                    SourceInputProfile.PlaPlanAnnexation),
                Array.Empty<string>(),
                Array.Empty<string>()));
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

    private static OutputSummaryDocument WriteOutputSummary(CaseFolderLayout layout, int outputCount)
    {
        var artifactPaths = Enumerable.Range(1, outputCount)
            .Select(index =>
            {
                var path = Path.Combine(layout.OutputDirectory, $"pla-output-{index}.pdf");
                File.WriteAllText(path, "%PDF output");
                return path;
            })
            .ToArray();

        var summary = new OutputSummaryDocument(
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
                artifactPaths,
                Array.Empty<string>(),
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
        new OutputSummaryPersistenceService().Save(layout, summary);
        return summary;
    }

    private static ExtractionReviewDocument CreateReviewedBoundary()
    {
        var document = new ExtractionReviewDocument
        {
            TransactionNumber = "100001219",
            ExtractionSource = "pla_plan_annexation_selected_plan"
        };
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

    private static SelectedInnolaTransaction CreateTransaction()
    {
        return new SelectedInnolaTransaction(
            "task-1",
            "tx-1",
            "100001219",
            "Plan Annexed",
            "Compute Survey Plan",
            FixedNow(),
            TransactionType: "Plan Annexed");
    }

    private static DateTimeOffset FixedNow() => new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var sourceRoot = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn");
            if (Directory.Exists(sourceRoot))
            {
                var candidate = Directory.EnumerateFiles(sourceRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
    }

    private sealed class RecordingPlaUploader : IPlaGeneratedOutputAttachmentUploader
    {
        private readonly int? failOnUpload;

        public RecordingPlaUploader(int? failOnUpload = null)
        {
            this.failOnUpload = failOnUpload;
        }

        public List<(string PdfPath, string SourceType)> Uploads { get; } = new();

        public Task<PlaGeneratedOutputAttachmentResult> UploadAsync(
            SelectedInnolaTransaction transaction,
            string pdfPath,
            string sourceType,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add((pdfPath, sourceType));
            if (failOnUpload == Uploads.Count)
            {
                return Task.FromResult(PlaGeneratedOutputAttachmentResult.Failed("Upload failed. Try again.", "upload_failed"));
            }

            return Task.FromResult(PlaGeneratedOutputAttachmentResult.Succeeded(sourceType, pdfPath));
        }
    }
}
