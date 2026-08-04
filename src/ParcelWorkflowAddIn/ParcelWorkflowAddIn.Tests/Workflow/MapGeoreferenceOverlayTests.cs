using System.IO;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class MapGeoreferenceOverlayTests
{
    public static void ArtifactPlanUsesTransactionOutputGeodatabase()
    {
        var caseRoot = Path.Combine("C:", "Sidwell", "ParcelWorkflow", "ParcelWorkflowCases", "100000860");

        var plan = MapGeoreferenceOverlayArtifactPlan.Create(caseRoot, "100000860");

        TestAssert.Equal(
            Path.Combine(caseRoot, "output", "100000860_parcel_output.gdb"),
            plan.OutputGeodatabasePath,
            "M-Geo persisted raster should target the transaction output geodatabase.");
        TestAssert.Equal(
            "mgeo_overlay_100000860",
            plan.RasterDatasetName,
            "M-Geo persisted raster should use a transaction-specific raster dataset name.");
        TestAssert.Equal(
            Path.Combine(caseRoot, "output", "100000860_parcel_output.gdb", "mgeo_overlay_100000860"),
            plan.RasterDatasetPath,
            "M-Geo persisted raster path should live inside the transaction output geodatabase.");
        TestAssert.Equal(
            Path.Combine(caseRoot, "working", "mgeo_overlay", "mgeo_overlay_artifact.json"),
            plan.MetadataPath,
            "M-Geo metadata should stay in the working folder so reload can restore the saved overlay.");
    }

    public static void TransactionPanelRestoresSavedOutputOverlayBeforeOpeningReviewForm()
    {
        var source = File.ReadAllText(FindSourceFile("TransactionPanelState.cs"));

        TestAssert.True(
            source.Contains("TryRestoreLocalOverlayFromOutputGeodatabaseAsync(transactionNumber)", StringComparison.Ordinal),
            "The M-Geo button should first try to restore the transaction output-GDB overlay.");
        TestAssert.True(
            source.Contains("MapGeoreferenceWindow.ShowOrActivate(transactionNumber)", StringComparison.Ordinal),
            "The M-Geo button should still open the review form when no saved overlay can be restored.");
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
    }
}
