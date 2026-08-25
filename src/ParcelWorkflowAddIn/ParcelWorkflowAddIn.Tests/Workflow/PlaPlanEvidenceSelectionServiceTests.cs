using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Tests;
using ParcelWorkflowAddIn.Workflow.Pla;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class PlaPlanEvidenceSelectionServiceTests
{
    public static async Task SaveSelectionPersistsFullPageMetadataAndPdfArtifact()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var renderer = new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(new byte[] { 37, 80, 68, 70 }, 612, 792));
        var service = new PlaPlanEvidenceSelectionService(renderer, () => FixedNow());

        var result = await service.SaveSelectionAsync(
            layout,
            "TR100001000",
            new PlaPlanEvidenceSelectionRequest(source, 3),
            CancellationToken.None);

        TestAssert.True(result.Success, "PLA selection save should succeed.");
        TestAssert.True(File.Exists(result.Selection!.GeneratedPlanEvidencePath), "PDF evidence artifact should be created.");
        TestAssert.Equal("pdf", result.Selection.GeneratedPlanEvidenceFormat, "Generated evidence format mismatch.");
        TestAssert.Equal(3, result.Selection.SelectedPageNumber, "Selected page should persist exactly.");
        TestAssert.Equal("full_page", result.Selection.SelectionType, "MVP selection must be full-page.");
        TestAssert.Equal(null, result.Selection.SelectionRegion, "Full-page MVP selection must not emit crop coordinates.");
        TestAssert.Equal(612, result.Selection.PageWidthPoints, "Page width mismatch.");
        TestAssert.Equal(792, result.Selection.PageHeightPoints, "Page height mismatch.");
        TestAssert.Equal(null, result.Selection.FallbackReason, "Successful PDF rendering should not set a fallback reason.");

        var persisted = PlaPlanEvidenceSelectionService.LoadSelection(layout);
        TestAssert.Equal(3, persisted?.SelectedPageNumber, "Reopened selected page mismatch.");
        TestAssert.Equal("source/1000-55.pdf", persisted?.SourceRelativePath, "Source path should be relative to case folder.");
        TestAssert.Equal("working/pla_plan_annexation/pla_selected_plan.pdf", persisted?.GeneratedPlanEvidenceRelativePath, "Evidence path should be relative to case folder.");
    }

    public static async Task SaveSelectionPersistsPngFallbackMetadata()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var renderer = new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Png(new byte[] { 137, 80, 78, 71 }, 600, 800, "PDF page generation unavailable in test renderer."));
        var service = new PlaPlanEvidenceSelectionService(renderer, () => FixedNow());

        var result = await service.SaveSelectionAsync(
            layout,
            "TR100001001",
            new PlaPlanEvidenceSelectionRequest(source, 1),
            CancellationToken.None);

        TestAssert.True(result.Success, "PNG fallback save should succeed.");
        TestAssert.Equal("png", result.Selection!.GeneratedPlanEvidenceFormat, "Fallback format mismatch.");
        TestAssert.True(result.Selection.FallbackReason?.Contains("PDF page generation unavailable", StringComparison.OrdinalIgnoreCase) == true, "Fallback reason should be persisted.");
        TestAssert.True(result.Selection.GeneratedPlanEvidenceRelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase), "PNG fallback path should use .png.");
    }

    public static void CandidateOptionsIncludeOnlyPlaPdfSources()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var otherPdf = new SourceFileCopyResult(
            "C:\\incoming\\other.pdf",
            WriteSource(layout, "other.pdf"),
            "other.pdf",
            ".pdf",
            4,
            SourceRole.SurveyPlanPdf,
            "copied",
            "Copied.",
            true,
            "st_survey_plan_pdf");
        var missingPla = source with { CopiedPath = Path.Combine(layout.SourceDirectory, "missing.pdf"), Copied = false };

        var options = PlaPlanEvidenceSelectionService.BuildSourceOptions(new[] { otherPdf, source, missingPla });

        TestAssert.Equal(1, options.Count, "Only copied PLA PDF sources should be selectable.");
        TestAssert.Equal("1000-55.pdf", options[0].FileName, "PLA option file mismatch.");
        TestAssert.Equal(SourceRole.PlanAnnexationPdf, options[0].SourceRole, "PLA option role mismatch.");
    }

    public static async Task SaveSelectionRejectsInvalidPageNumber()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(new byte[] { 1 }, 612, 792)), () => FixedNow());

        var result = await service.SaveSelectionAsync(
            layout,
            "TR100001002",
            new PlaPlanEvidenceSelectionRequest(source, 0),
            CancellationToken.None);

        TestAssert.True(!result.Success, "Page zero should fail.");
        TestAssert.True(result.Message.Contains("page", StringComparison.OrdinalIgnoreCase), "Invalid page diagnostic should mention page.");
    }

    public static async Task SavedSelectionIsDiscoveredAsAvailableArtifactOnReopen()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(new byte[] { 37, 80, 68, 70 }, 612, 792)), () => FixedNow());

        await service.SaveSelectionAsync(
            layout,
            "TR100001003",
            new PlaPlanEvidenceSelectionRequest(source, 2),
            CancellationToken.None);

        var reopen = new CaseFolderStore().ReopenCaseFolder(layout.RootDirectory);

        TestAssert.True(reopen.Success, "Case folder should reopen.");
        TestAssert.True(
            reopen.AvailableArtifacts.Any(artifact => artifact.ArtifactName == PlaPlanEvidenceSelectionService.SelectionArtifactFileName),
            "PLA selection metadata should be discoverable as a case artifact.");
        TestAssert.True(
            reopen.AvailableArtifacts.Any(artifact => artifact.ArtifactName == PlaPlanEvidenceSelectionService.PdfEvidenceFileName),
            "PLA generated evidence PDF should be discoverable as a case artifact.");
    }

    public static void ViewModelListsPlaPdfOptionsAndEnablesSave()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(new byte[] { 37, 80, 68, 70 }, 612, 792)), () => FixedNow());

        var viewModel = new PlaPlanEvidenceSelectionViewModel(layout, "TR100001004", new[] { source }, service);

        TestAssert.Equal(1, viewModel.SourceOptions.Count, "PLA selection workspace should list copied PLA PDFs.");
        TestAssert.Equal("1000-55.pdf", viewModel.SelectedSource?.FileName, "PLA selection workspace should default to the available PLA PDF.");
        TestAssert.True(viewModel.CanSaveSelection, "PLA selection workspace should enable save when source and page are valid.");
        TestAssert.True(viewModel.SaveSelectionCommand.CanExecute(null), "Save command should be executable for valid PLA selection.");
    }

    public static async Task ViewModelSavesAndRestoresSelectedSourcePageAndArtifactStatus()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(new byte[] { 37, 80, 68, 70 }, 612, 792)), () => FixedNow());
        var viewModel = new PlaPlanEvidenceSelectionViewModel(layout, "TR100001005", new[] { source }, service)
        {
            SelectedPageNumber = 4
        };

        var result = await viewModel.SaveSelectionAsync(CancellationToken.None);
        var reopened = new PlaPlanEvidenceSelectionViewModel(layout, "TR100001005", new[] { source }, service);

        TestAssert.True(result.Success, "PLA selection view model save should succeed.");
        TestAssert.Equal(4, reopened.SelectedPageNumber, "PLA selection workspace should restore selected page on reopen.");
        TestAssert.Equal("1000-55.pdf", reopened.SelectedSource?.FileName, "PLA selection workspace should restore selected source on reopen.");
        TestAssert.True(reopened.ArtifactStatusText.Contains("PDF", StringComparison.Ordinal), "PLA selection workspace should show generated artifact status.");
    }

    public static async Task ViewModelNotifiesParentAfterSuccessfulSave()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(new byte[] { 37, 80, 68, 70 }, 612, 792)), () => FixedNow());
        PlaPlanEvidenceSelectionSaveResult? callbackResult = null;
        var viewModel = new PlaPlanEvidenceSelectionViewModel(
            layout,
            "TR100001014",
            new[] { source },
            service,
            result => callbackResult = result);

        var result = await viewModel.SaveSelectionAsync(CancellationToken.None);

        TestAssert.True(result.Success, "PLA selection save should succeed.");
        TestAssert.True(callbackResult?.Success == true, "PLA selection workspace should notify the parent after a successful save.");
        TestAssert.Equal(result.Selection?.GeneratedPlanEvidenceRelativePath, callbackResult?.Selection?.GeneratedPlanEvidenceRelativePath, "Callback should receive the saved evidence result.");
    }

    public static void ViewModelBlocksInvalidPageSave()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var viewModel = new PlaPlanEvidenceSelectionViewModel(layout, "TR100001006", new[] { source })
        {
            SelectedPageNumber = 0
        };

        TestAssert.True(!viewModel.CanSaveSelection, "PLA selection workspace should block invalid page numbers.");
        TestAssert.True(!viewModel.SaveSelectionCommand.CanExecute(null), "Save command should not execute for invalid page numbers.");
    }

    public static async Task SaveSelectionRejectsEmptyRendererArtifact()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult.Pdf(Array.Empty<byte>(), 612, 792)), () => FixedNow());

        var result = await service.SaveSelectionAsync(
            layout,
            "TR100001007",
            new PlaPlanEvidenceSelectionRequest(source, 1),
            CancellationToken.None);

        TestAssert.True(!result.Success, "Empty renderer output should fail.");
        TestAssert.True(result.Message.Contains("empty", StringComparison.OrdinalIgnoreCase), "Empty output diagnostic should be explicit.");
        TestAssert.True(!File.Exists(PlaPlanEvidenceSelectionService.GetPdfEvidencePath(layout)), "Empty artifact should not be persisted.");
    }

    public static async Task SaveSelectionRemovesStaleOppositeEvidenceArtifact()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out var source);
        var service = new PlaPlanEvidenceSelectionService(new SequencePlaPlanEvidenceRenderer(
            PlaPlanEvidenceRenderResult.Pdf(new byte[] { 37, 80, 68, 70 }, 612, 792),
            PlaPlanEvidenceRenderResult.Png(new byte[] { 137, 80, 78, 71 }, 600, 800, "PNG fallback.")), () => FixedNow());

        var first = await service.SaveSelectionAsync(layout, "TR100001008", new PlaPlanEvidenceSelectionRequest(source, 1), CancellationToken.None);
        var second = await service.SaveSelectionAsync(layout, "TR100001008", new PlaPlanEvidenceSelectionRequest(source, 2), CancellationToken.None);

        TestAssert.True(first.Success && second.Success, "Both evidence saves should succeed.");
        TestAssert.True(!File.Exists(PlaPlanEvidenceSelectionService.GetPdfEvidencePath(layout)), "Stale PDF should be removed after PNG fallback save.");
        TestAssert.True(File.Exists(PlaPlanEvidenceSelectionService.GetPngEvidencePath(layout)), "Current PNG evidence should remain.");
    }

    public static void LoadSelectionRejectsPathsOutsideCaseFolder()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out _);
        Directory.CreateDirectory(PlaPlanEvidenceSelectionService.GetWorkingDirectory(layout));
        File.WriteAllText(
            PlaPlanEvidenceSelectionService.GetSelectionArtifactPath(layout),
            """
            {
              "schema_version": "1.0.0",
              "transaction_number": "TR100001009",
              "source_type": "st_plan_annexation_pdf",
              "source_relative_path": "../outside.pdf",
              "selected_page_number": 1,
              "selection_type": "full_page",
              "generated_plan_evidence_path": "working/pla_plan_annexation/pla_selected_plan.pdf",
              "generated_plan_evidence_format": "pdf",
              "created_at_utc": "2026-08-24T12:00:00Z",
              "updated_at_utc": "2026-08-24T12:00:00Z"
            }
            """);

        var selection = PlaPlanEvidenceSelectionService.LoadSelection(layout);

        TestAssert.Equal(null, selection, "Selection metadata with traversal paths should not load.");
    }

    public static void ViewModelDoesNotRestoreAmbiguousFilenameFallback()
    {
        using var tempRoot = new TempDirectory();
        var layout = CreatePlaCase(tempRoot.Path, out _);
        var sourceA = CreateSource(layout, "a/duplicate.pdf");
        var sourceB = CreateSource(layout, "b/duplicate.pdf");
        Directory.CreateDirectory(PlaPlanEvidenceSelectionService.GetWorkingDirectory(layout));
        File.WriteAllText(
            PlaPlanEvidenceSelectionService.GetSelectionArtifactPath(layout),
            """
            {
              "schema_version": "1.0.0",
              "transaction_number": "TR100001010",
              "source_type": "st_plan_annexation_pdf",
              "source_relative_path": "source/duplicate.pdf",
              "selected_page_number": 2,
              "selection_type": "full_page",
              "generated_plan_evidence_path": "working/pla_plan_annexation/pla_selected_plan.pdf",
              "generated_plan_evidence_format": "pdf",
              "created_at_utc": "2026-08-24T12:00:00Z",
              "updated_at_utc": "2026-08-24T12:00:00Z"
            }
            """);

        var viewModel = new PlaPlanEvidenceSelectionViewModel(layout, "TR100001010", new[] { sourceA, sourceB });

        TestAssert.Equal(null, viewModel.SelectedSource, "Ambiguous duplicate filenames should not restore to an arbitrary source.");
    }

    public static void ProductionDockpaneExposesPlaSelectionWorkspace()
    {
        var xaml = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpane.xaml"));
        var viewModel = File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(xaml.Contains("ShowPlaPlanEvidenceSelection", StringComparison.Ordinal), "Dockpane should expose PLA selection visibility binding.");
        TestAssert.True(xaml.Contains("IsPlaPlanEvidenceSelectionStageActive", StringComparison.Ordinal), "Dockpane should expose first-class PLA plan evidence selection stage.");
        TestAssert.True(xaml.Contains("Select Plan Evidence", StringComparison.Ordinal), "Dockpane should label PLA plan evidence selection as a first-class step.");
        TestAssert.True(xaml.Contains("PlaPlanEvidenceSelection.SourceOptions", StringComparison.Ordinal), "Dockpane should bind PLA source options.");
        TestAssert.True(xaml.Contains("PlaPlanEvidenceSelection.SaveSelectionCommand", StringComparison.Ordinal), "Dockpane should bind PLA save command.");
        TestAssert.True(viewModel.Contains("IsPlaPlanAnnexationWorkflow", StringComparison.Ordinal), "View model should gate PLA selection by workflow profile.");
        TestAssert.True(viewModel.Contains("HasCompletePlaPlanEvidenceSelection", StringComparison.Ordinal), "View model should gate PLA extraction by saved selection artifact.");
    }

    public static void WorkflowRulesRoutesPlaScriptToSelectionArtifact()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Settings", "WorkflowRules.json")));
        var rules = document.RootElement.GetProperty("rules").EnumerateArray();
        var plaRule = rules.First(rule => string.Equals(rule.GetProperty("workflow_profile").GetString(), SourceInputProfile.PlaPlanAnnexation, StringComparison.OrdinalIgnoreCase));
        var step = plaRule.GetProperty("script_plan").EnumerateArray().First();

        TestAssert.Equal("select_plan_annexation_pdf_page", step.GetProperty("script").GetString(), "PLA workflow should route to the selection script.");
        TestAssert.Equal(
            "working/pla_plan_annexation/pla_plan_evidence_selection.json",
            step.GetProperty("output_artifacts")[0].GetString(),
            "PLA selection step should output the saved selection artifact.");
    }

    private static CaseFolderLayout CreatePlaCase(string outputRoot, out SourceFileCopyResult source)
    {
        var store = new CaseFolderStore(() => FixedNow(), () => "run-pla-selection");
        var created = store.CreateCase(outputRoot, "TR100001000", "tester");
        var layout = created.Layout!;
        var copiedPath = WriteSource(layout, "1000-55.pdf");
        source = new SourceFileCopyResult(
            "C:\\incoming\\1000-55.pdf",
            copiedPath,
            "1000-55.pdf",
            ".pdf",
            new FileInfo(copiedPath).Length,
            SourceRole.PlanAnnexationPdf,
            "copied",
            "Copied.",
            true,
            "st_plan_annexation_pdf");
        return layout;
    }

    private static string WriteSource(CaseFolderLayout layout, string fileName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(layout.SourceDirectory, fileName))!);
        var path = Path.Combine(layout.SourceDirectory, fileName);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        return path;
    }

    private static SourceFileCopyResult CreateSource(CaseFolderLayout layout, string relativePath)
    {
        var copiedPath = WriteSource(layout, relativePath);
        return new SourceFileCopyResult(
            $"C:\\incoming\\{Path.GetFileName(relativePath)}",
            copiedPath,
            Path.GetFileName(relativePath),
            ".pdf",
            new FileInfo(copiedPath).Length,
            SourceRole.PlanAnnexationPdf,
            "copied",
            "Copied.",
            true,
            "st_plan_annexation_pdf");
    }

    private static DateTimeOffset FixedNow() => new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class StubPlaPlanEvidenceRenderer : IPlaPlanEvidenceRenderer
    {
        private readonly PlaPlanEvidenceRenderResult result;

        public StubPlaPlanEvidenceRenderer(PlaPlanEvidenceRenderResult result)
        {
            this.result = result;
        }

        public Task<PlaPlanEvidenceRenderResult> RenderAsync(PlaPlanEvidenceRenderRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class SequencePlaPlanEvidenceRenderer : IPlaPlanEvidenceRenderer
    {
        private readonly Queue<PlaPlanEvidenceRenderResult> results;

        public SequencePlaPlanEvidenceRenderer(params PlaPlanEvidenceRenderResult[] results)
        {
            this.results = new Queue<PlaPlanEvidenceRenderResult>(results);
        }

        public Task<PlaPlanEvidenceRenderResult> RenderAsync(PlaPlanEvidenceRenderRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(results.Dequeue());
        }
    }
}
