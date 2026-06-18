using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ReviewSourceViewerStateProjectorTests
{
    public static void ProjectorSupportsPdfSources()
    {
        using var temp = new TempDirectory();
        var pdfPath = Path.Combine(temp.Path, "computation.pdf");
        File.WriteAllText(pdfPath, "pdf");

        var state = ReviewSourceViewerStateProjector.Build(new SourceFileCopyResult(
            "incoming:computation.pdf",
            pdfPath,
            "computation.pdf",
            ".pdf",
            3,
            "computation_source",
            "copied",
            "Copied",
            true));

        TestAssert.Equal(ReviewSourceViewerMode.Pdf, state.Mode, "PDF source should project to embedded PDF mode.");
        TestAssert.True(state.CanRenderEmbedded, "PDF source should be embeddable.");
    }

    public static void ProjectorSupportsRasterAndImageSources()
    {
        using var temp = new TempDirectory();
        var tifPath = Path.Combine(temp.Path, "plan.tif");
        File.WriteAllText(tifPath, "tif");

        var state = ReviewSourceViewerStateProjector.Build(new SourceFileCopyResult(
            "incoming:plan.tif",
            tifPath,
            "plan.tif",
            ".tif",
            3,
            "plan_map_reference",
            "copied",
            "Copied",
            true));

        TestAssert.Equal(ReviewSourceViewerMode.RenderedDocument, state.Mode, "TIFF source should project to the unified rendered-document viewer mode.");
        TestAssert.True(state.CanRenderEmbedded, "TIFF source should be embeddable.");
    }

    public static void ProjectorFallsBackWhenSourceIsUnsupported()
    {
        using var temp = new TempDirectory();
        var txtPath = Path.Combine(temp.Path, "points.txt");
        File.WriteAllText(txtPath, "1,2,3");

        var state = ReviewSourceViewerStateProjector.Build(new SourceFileCopyResult(
            "incoming:points.txt",
            txtPath,
            "points.txt",
            ".txt",
            5,
            "points_computation",
            "copied",
            "Copied",
            true));

        TestAssert.Equal(ReviewSourceViewerMode.Unsupported, state.Mode, "Unsupported formats should fall back cleanly.");
        TestAssert.True(!state.CanRenderEmbedded, "Unsupported formats should not claim embedded support.");
    }

    public static void ProjectorFallsBackWhenSourceFileIsMissing()
    {
        var state = ReviewSourceViewerStateProjector.Build(new SourceFileCopyResult(
            "incoming:missing.pdf",
            "C:\\missing\\missing.pdf",
            "missing.pdf",
            ".pdf",
            5,
            "computation_source",
            "copied",
            "Copied",
            true));

        TestAssert.Equal(ReviewSourceViewerMode.Missing, state.Mode, "Missing files should surface fallback mode.");
        TestAssert.True(!state.CanRenderEmbedded, "Missing files should not claim embedded support.");
    }

    public static void ProjectorCanSurfaceRenderFailureFallback()
    {
        using var temp = new TempDirectory();
        var imagePath = Path.Combine(temp.Path, "bad.png");
        File.WriteAllText(imagePath, "not-an-image");

        var state = ReviewSourceViewerStateProjector.BuildRenderFailure(new SourceFileCopyResult(
            "incoming:bad.png",
            imagePath,
            "bad.png",
            ".png",
            12,
            "plan_map_reference",
            "copied",
            "Copied",
            true),
            "Image decode failed.");

        TestAssert.Equal(ReviewSourceViewerMode.Error, state.Mode, "Render failure should move the viewer into fallback error mode.");
        TestAssert.True(state.FallbackMessage.Contains("Image decode failed.", StringComparison.OrdinalIgnoreCase), "Render failure details should be preserved.");
    }
}
