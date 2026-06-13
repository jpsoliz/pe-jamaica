using System.IO;
using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.Review;

internal static class ReviewSourceViewerStateProjector
{
    public static ReviewSourceViewerState Build(SourceFileCopyResult? sourceFile)
    {
        if (sourceFile is null)
        {
            return new ReviewSourceViewerState(
                ReviewSourceViewerMode.None,
                "No active source selected",
                "Source",
                "No point-bearing source document is available.",
                "Unavailable",
                "Waiting for source",
                "Load or verify source files before reviewing extracted points.",
                "Open source and Reveal stay available when a source document is present.",
                null);
        }

        var roleLabel = HumanizeRole(sourceFile.SourceRole);
        var displayPath = $"source/{sourceFile.FileName}";

        if (!sourceFile.Copied || string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            return new ReviewSourceViewerState(
                ReviewSourceViewerMode.Missing,
                sourceFile.FileName,
                roleLabel,
                displayPath,
                "Copied file unavailable",
                "Viewer unavailable",
                "The selected source has not been copied into the Case Folder yet.",
                "Use Open source or Reveal after the copied case file is available.",
                null);
        }

        if (!File.Exists(sourceFile.CopiedPath))
        {
            return new ReviewSourceViewerState(
                ReviewSourceViewerMode.Missing,
                sourceFile.FileName,
                roleLabel,
                displayPath,
                "Missing file",
                "Viewer unavailable",
                "The copied Case Folder file is missing, so the embedded viewer cannot load it.",
                "Use Reveal to inspect the case folder and restore the missing source file if needed.",
                sourceFile.CopiedPath);
        }

        var extension = (sourceFile.FileType ?? Path.GetExtension(sourceFile.FileName)).ToLowerInvariant();
        switch (extension)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
                return new ReviewSourceViewerState(
                    ReviewSourceViewerMode.Image,
                    sourceFile.FileName,
                    roleLabel,
                    displayPath,
                    "Image source",
                    "Embedded image ready",
                    "Verify extracted points directly against the embedded image. Use Fit/Actual size as needed.",
                    "If the image does not render correctly, use Open source or Reveal.",
                    sourceFile.CopiedPath);
            case ".tif":
            case ".tiff":
                return new ReviewSourceViewerState(
                    ReviewSourceViewerMode.Image,
                    sourceFile.FileName,
                    roleLabel,
                    displayPath,
                    "Scanned raster source",
                    "Embedded raster ready",
                    "Verify extracted points against the embedded TIFF/TIF image. Use Fit/Actual size as needed.",
                    "If the raster does not render correctly, use Open source or Reveal.",
                    sourceFile.CopiedPath);
            case ".pdf":
                return new ReviewSourceViewerState(
                    ReviewSourceViewerMode.Pdf,
                    sourceFile.FileName,
                    roleLabel,
                    displayPath,
                    "PDF source",
                    "Embedded PDF ready",
                    "Verify extracted points against the embedded PDF. Use Reload if the PDF host needs to refresh.",
                    "If the PDF host cannot render this file in-pane, use Open source or Reveal.",
                    sourceFile.CopiedPath);
            default:
                return new ReviewSourceViewerState(
                    ReviewSourceViewerMode.Unsupported,
                    sourceFile.FileName,
                    roleLabel,
                    displayPath,
                    "Unsupported format",
                    "Fallback only",
                    "This source format is not rendered inside the add-in in this build.",
                    $"Embedded viewing is not available for {extension}. Use Open source or Reveal instead.",
                    sourceFile.CopiedPath);
        }
    }

    public static ReviewSourceViewerState BuildRenderFailure(SourceFileCopyResult? sourceFile, string? failureReason)
    {
        var state = Build(sourceFile);
        var reason = string.IsNullOrWhiteSpace(failureReason) ? "The embedded viewer could not render this file." : failureReason.Trim();

        return state with
        {
            Mode = ReviewSourceViewerMode.Error,
            LoadState = "Render failed",
            Guidance = "The source is still available, but embedded rendering failed for this attempt.",
            FallbackMessage = $"{reason} Use Open source or Reveal as the fallback path."
        };
    }

    private static string HumanizeRole(string? role)
    {
        return role switch
        {
            "computation_source" => "Computation",
            "points_computation" => "Points",
            "plan_map_reference" => "Plan",
            "dwg_reference" => "DWG",
            null or "" => "Source",
            _ => role.Replace("_", " ")
        };
    }
}
