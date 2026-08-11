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

    public static void TitlePlanArtifactPlanUsesSeparateNames()
    {
        var caseRoot = Path.Combine("C:", "Sidwell", "ParcelWorkflow", "ParcelWorkflowCases", "100000860");

        var plan = MapGeoreferenceOverlayArtifactPlan.Create(
            caseRoot,
            "100000860",
            MapGeoreferenceOverlayKind.TitlePlanComparison);

        TestAssert.Equal(
            "title_plan_overlay_100000860",
            plan.RasterDatasetName,
            "Title-plan comparison overlays should not use the M-Geo raster dataset name.");
        TestAssert.Equal(
            Path.Combine(caseRoot, "working", "title_plan_overlay", "title_plan_overlay_artifact.json"),
            plan.MetadataPath,
            "Title-plan comparison metadata should be stored separately from M-Geo metadata.");
    }

    public static void TransactionPanelExposesTitlePlanLauncherBesideMGeo()
    {
        var xaml = File.ReadAllText(FindSourceFile("TransactionPanelDockpane.xaml"));
        var source = File.ReadAllText(FindSourceFile("TransactionPanelState.cs"));

        TestAssert.True(
            xaml.Contains("OpenTitlePlanImagePlacementCommand", StringComparison.Ordinal),
            "Transaction List toolbar should bind a dedicated title-plan image placement command.");
        TestAssert.True(
            xaml.Contains("ToolTip=\"Map review tools\"", StringComparison.Ordinal)
                && xaml.Contains("Header=\"M-Geo coordinate overlay\"", StringComparison.Ordinal)
                && xaml.Contains("Header=\"Title Plan Placement\"", StringComparison.Ordinal),
            "M-Geo and title-plan placement should be grouped under a compact Map Tools menu.");
        TestAssert.True(
            xaml.IndexOf("OpenMapGeoreferenceCommand", StringComparison.Ordinal)
                < xaml.IndexOf("OpenTitlePlanImagePlacementCommand", StringComparison.Ordinal),
            "The title-plan placement launcher should sit near the M-Geo toolbar action.");
        TestAssert.True(
            xaml.Contains("ToolTip=\"Document actions\"", StringComparison.Ordinal)
                && xaml.Contains("ToolTip=\"Compare actions\"", StringComparison.Ordinal)
                && xaml.Contains("<Setter Property=\"Width\" Value=\"40\" />", StringComparison.Ordinal),
            "The transaction toolbar should group secondary workflow actions instead of rendering a crowded right-side button strip.");
        TestAssert.True(
            source.Contains("CanOpenTitlePlanImagePlacement", StringComparison.Ordinal),
            "Title-plan launcher should have an explicit enabled-state gate.");
        TestAssert.True(
            source.Contains("TitlePlanImagePlacementDisabledReason", StringComparison.Ordinal),
            "Title-plan launcher should expose a specific disabled tooltip reason.");
    }

    public static void ImageComparisonModePersistsSelectedPageMetadata()
    {
        var source = File.ReadAllText(FindSourceFile("MapGeoreferenceViewModel.cs"));
        var overlaySource = File.ReadAllText(FindSourceFile("MapGeoreferenceOverlayService.cs"));

        TestAssert.True(
            source.Contains("MapGeoreferenceWorkflowMode.ImageComparison", StringComparison.Ordinal),
            "The shared georeference window should expose a distinct image-comparison mode.");
        TestAssert.True(
            source.Contains("SelectedSourcePageNumber", StringComparison.Ordinal),
            "The image-comparison workflow should track the selected source PDF page.");
        TestAssert.True(
            overlaySource.Contains("SelectedSourcePageNumber", StringComparison.Ordinal),
            "Overlay metadata should persist the selected source page.");
        TestAssert.True(
            overlaySource.Contains("TwoPointSimilarity", StringComparison.Ordinal),
            "Overlay metadata should identify the similarity transform used by the MVP placement.");
    }

    public static void TitlePlanWindowReuseRequiresMatchingTransaction()
    {
        var source = File.ReadAllText(FindSourceFile("MapGeoreferenceWindow.xaml.cs"));

        TestAssert.True(
            source.Contains("viewModel.TransactionNumber, transactionNumber", StringComparison.Ordinal),
            "M-Geo/title-plan windows should only be reused when the existing window is for the same transaction.");
    }

    public static void PdfPageSelectionDrivesBrowserNavigation()
    {
        var source = File.ReadAllText(FindSourceFile("MapGeoreferenceWindow.xaml.cs"));
        var viewModelSource = File.ReadAllText(FindSourceFile("MapGeoreferenceViewModel.cs"));

        TestAssert.True(
            source.Contains("BuildPdfBrowserUri", StringComparison.Ordinal)
                && source.Contains("Fragment = $\"page=", StringComparison.Ordinal),
            "PDF page selection should be included in the WebView navigation URI before capture.");
        TestAssert.True(
            source.Contains("string.Equals(navigationKey, BuildPdfNavigationKey", StringComparison.Ordinal),
            "PDF page-aware navigation should not be cancelled by comparing against the raw document navigation key.");
        TestAssert.True(
            viewModelSource.Contains("SelectedSupportingDocumentIsPdf ? TryReadSelectedPageNumber() : null", StringComparison.Ordinal),
            "Raster image overlays should not persist a fake PDF page number.");
    }

    public static void OverlayMetadataIsSavedOnlyAfterLayerLoad()
    {
        var source = File.ReadAllText(FindSourceFile("MapGeoreferenceOverlayService.cs"));
        var failureIndex = source.IndexOf("if (!loadResult.Success)", StringComparison.Ordinal);
        var saveIndex = source.IndexOf("SaveOverlayArtifact(artifact, ResolveCaseRootFromArtifact(artifact), kind)", StringComparison.Ordinal);

        TestAssert.True(
            failureIndex >= 0 && saveIndex > failureIndex,
            "Overlay metadata should not be saved before the map layer load succeeds.");
        TestAssert.True(
            source.Contains("TryDeleteOverlayFiles(artifact)", StringComparison.Ordinal),
            "Failed map layer loads should clean up generated overlay sidecar files.");
    }

    public static void TitlePlanOverlayBlocksWrongMapSpatialReference()
    {
        var source = File.ReadAllText(FindSourceFile("MapGeoreferenceOverlayService.cs"));

        TestAssert.True(
            source.Contains("kind == MapGeoreferenceOverlayKind.TitlePlanComparison && mapWkid != Jad2001Wkid", StringComparison.Ordinal),
            "Title-plan overlays should block on a non-JAD2001 active map instead of mutating the map spatial reference.");
        TestAssert.True(
            source.Contains("kind == MapGeoreferenceOverlayKind.MGeo", StringComparison.Ordinal)
                && source.Contains("SetSpatialReference", StringComparison.Ordinal),
            "Existing M-Geo behavior should remain isolated from the title-plan SR block.");
    }

    public static void TitlePlanMapPointCaptureUsesArcGisMapTool()
    {
        var project = File.ReadAllText(FindSourceFile("ParcelWorkflowAddIn.csproj"));
        var toolSource = File.ReadAllText(FindSourceFile("TitlePlanMapPointTool.cs"));
        var viewModelSource = File.ReadAllText(FindSourceFile("MapGeoreferenceViewModel.cs"));
        var daml = File.ReadAllText(FindSourceFile("Config.daml"));

        TestAssert.True(
            project.Contains("ArcGIS.Desktop.Extensions", StringComparison.Ordinal),
            "The add-in project should reference ArcGIS.Desktop.Extensions so ArcGIS.Desktop.Mapping.MapTool compiles and packages.");
        TestAssert.True(
            toolSource.Contains("internal sealed class TitlePlanMapPointTool : MapTool", StringComparison.Ordinal)
                && toolSource.Contains("SketchType = SketchGeometryType.Point", StringComparison.Ordinal)
                && toolSource.Contains("ApplyCapturedMapPoint", StringComparison.Ordinal),
            "Title-plan map point capture should use an ArcGIS Pro point sketch tool and route the clicked coordinate to the placement view model.");
        TestAssert.True(
            viewModelSource.Contains("PickMapPoint1Command", StringComparison.Ordinal)
                && viewModelSource.Contains("FrameworkApplication.SetCurrentToolAsync(TitlePlanMapPointTool.ToolId)", StringComparison.Ordinal),
            "The title-plan placement form should expose commands that activate the ArcGIS Pro map point picker.");
        TestAssert.True(
            daml.Contains("ParcelWorkflow_TitlePlanMapPointTool", StringComparison.Ordinal)
                && daml.Contains("className=\"TitlePlanMapPointTool\"", StringComparison.Ordinal),
            "The ArcGIS Pro map point tool must be registered in Config.daml so it is included in the add-in package.");
    }

    public static void TitlePlanFormExposesTransparencyAndRemoveRetry()
    {
        var xaml = File.ReadAllText(FindSourceFile("MapGeoreferenceWindow.xaml"));
        var viewModelSource = File.ReadAllText(FindSourceFile("MapGeoreferenceViewModel.cs"));
        var overlaySource = File.ReadAllText(FindSourceFile("MapGeoreferenceOverlayService.cs"));

        TestAssert.True(
            xaml.Contains("OverlayTransparencyPercent", StringComparison.Ordinal)
                && viewModelSource.Contains("OverlayTransparencyPercent", StringComparison.Ordinal)
                && overlaySource.Contains("TransparencyPercent", StringComparison.Ordinal),
            "Title-plan comparison overlays should expose and persist user-selected transparency.");
        TestAssert.True(
            xaml.Contains("Remove Overlay", StringComparison.Ordinal)
                && viewModelSource.Contains("RemoveComparisonOverlayAsync", StringComparison.Ordinal)
                && viewModelSource.Contains("MapGeoreferenceOverlayKind.TitlePlanComparison", StringComparison.Ordinal),
            "The title-plan placement form should let the examiner remove/retry a mistaken comparison overlay.");
        TestAssert.True(
            xaml.Contains("<GridSplitter Grid.Column=\"1\"", StringComparison.Ordinal)
                && xaml.Contains("ResizeBehavior=\"PreviousAndNext\"", StringComparison.Ordinal),
            "The title-plan placement form should let examiners resize the document and control panels.");
        TestAssert.True(
            viewModelSource.Contains("Capture page", StringComparison.Ordinal)
                && viewModelSource.Contains("Plan 1", StringComparison.Ordinal)
                && xaml.Contains("Content=\"Map 1\"", StringComparison.Ordinal),
            "The title-plan placement form should use compact point-picking labels that fit the side panel.");
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
