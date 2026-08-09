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
            source.Contains("TryRestorePersistedOverlayAsync(transactionNumber)", StringComparison.Ordinal),
            "The M-Geo button should restore the persisted overlay, including the saved image fallback when the output-GDB raster cannot be loaded.");
        TestAssert.True(
            source.Contains("MapGeoreferenceWindow.ShowOrActivate(transactionNumber)", StringComparison.Ordinal),
            "The M-Geo button should still open the review form when no saved overlay can be restored.");
    }

    public static void PersistedOverlayRestoreFallsBackToSavedImage()
    {
        var source = File.ReadAllText(FindSourceFile("MapGeoreferenceOverlayService.cs"));

        TestAssert.True(
            source.Contains("outputGeodatabaseRestoreFailure", StringComparison.Ordinal),
            "Persisted restore should remember output-GDB raster load failures.");
        TestAssert.True(
            source.Contains("File.Exists(artifact.ImagePath)", StringComparison.Ordinal),
            "Persisted restore should fall back to the saved georeferenced image when it is still available.");
        TestAssert.True(
            source.Contains("management.DefineProjection", StringComparison.Ordinal),
            "Saved image fallback should define the raster projection before adding it to ArcGIS Pro.");
        TestAssert.True(
            source.Contains("SpatialReferenceBuilder.CreateSpatialReference(Jad2001Wkid)", StringComparison.Ordinal),
            "Saved image fallback should define the overlay as JAD2001 / EPSG:3448.");
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
