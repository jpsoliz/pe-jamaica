namespace ParcelWorkflowAddIn.Tests.Workflow;

using ParcelWorkflowAddIn.Workflow.Review;

internal static class JamaicaReviewWorkspaceXamlTests
{
    public static void PxaReviewUsesTabScopedCommandsAndHidesGlobalPointToolbar()
    {
        var xaml = File.ReadAllText(FindWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Visibility=\"{Binding IsStandardPointReview, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal),
            "Global point action toolbar should be hidden when the PXA tabbed workspace is active.");
        TestAssert.True(
            xaml.Contains("Command=\"{Binding AddReviewSegmentCommand}\"", StringComparison.Ordinal),
            "Boundary Segments tab should expose Add segment in its tab-scoped command strip.");
        TestAssert.True(
            xaml.Contains("Command=\"{Binding ExcludeReviewSegmentCommand}\"", StringComparison.Ordinal),
            "Boundary Segments tab should expose Exclude segment in its tab-scoped command strip.");
        TestAssert.True(
            xaml.Contains("Command=\"{Binding RebuildBoundaryPointsCommand}\"", StringComparison.Ordinal)
            && xaml.Contains("Rebuild points from boundary", StringComparison.Ordinal),
            "Boundary Segments tab should expose explicit point rebuild from reviewed boundary segments.");
        TestAssert.True(
            xaml.Contains("Header=\"Use for points\"", StringComparison.Ordinal),
            "Boundary segment grids should label the solver include flag as Use for points.");
        TestAssert.True(
            xaml.Contains("<TabItem Header=\"Points\">", StringComparison.Ordinal)
            && xaml.Contains("Review printed, derived, and manually added points for this parcel.", StringComparison.Ordinal),
            "Points tab should own its point action strip instead of relying on the global PE toolbar.");
    }

    public static void PxaReviewCloseAndEditDialogsUseOwnedWindows()
    {
        var windowCode = File.ReadAllText(FindSourceFile("JamaicaReviewWorkspaceWindow.xaml.cs"));
        var dockpaneCode = File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(
            windowCode.Contains("MessageBox.Show(\r\n            this,", StringComparison.Ordinal)
            || windowCode.Contains("MessageBox.Show(\n            this,", StringComparison.Ordinal),
            "Close and validation prompts should be owned by the PXA review workspace window.");
        TestAssert.True(
            dockpaneCode.Contains("ApplyReviewDialogOwner(dialog);", StringComparison.Ordinal)
            && dockpaneCode.Contains("private void ApplyReviewDialogOwner(Window dialog)", StringComparison.Ordinal),
            "PXA point and segment edit dialogs should be owned by the active review workspace window.");
    }

    public static void PointDeleteUsesConfirmationAndRefreshesVisibleRows()
    {
        var xaml = File.ReadAllText(FindWorkspaceXaml());
        var windowCode = File.ReadAllText(FindSourceFile("JamaicaReviewWorkspaceWindow.xaml.cs"));
        var workspaceViewModelCode = File.ReadAllText(FindSourceFile("JamaicaReviewWorkspaceViewModel.cs"));

        TestAssert.True(
            xaml.Contains("Click=\"RemovePointButton_Click\"", StringComparison.Ordinal)
            && xaml.Contains("IsEnabled=\"{Binding CanRemoveSelectedPoint}\"", StringComparison.Ordinal),
            "Point delete buttons should route through the window so deletion can be confirmed before the remove command runs.");
        TestAssert.True(
            windowCode.Contains("Delete point", StringComparison.Ordinal)
            && windowCode.Contains("MessageBoxButton.YesNo", StringComparison.Ordinal)
            && windowCode.Contains("MessageBoxImage.Warning", StringComparison.Ordinal)
            && windowCode.Contains("FrameworkApplication.Current?.MainWindow", StringComparison.Ordinal)
            && windowCode.Contains("viewModel.RemoveSelectedPointFromWorkspace()", StringComparison.Ordinal),
            "Point delete should ask for an ArcGIS Pro main-window-owned confirmation and only then invoke the remove workflow.");
        TestAssert.True(
            workspaceViewModelCode.Contains("parent.RemoveSelectedManualPointFromWorkspace();", StringComparison.Ordinal)
            && workspaceViewModelCode.Contains("RefreshProjection();", StringComparison.Ordinal),
            "The workspace should refresh the visible point list immediately after a confirmed point delete.");
        TestAssert.True(
            File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs")).Contains("Delete point", StringComparison.Ordinal)
            && File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs")).Contains("MessageBoxButton.YesNo", StringComparison.Ordinal)
            && File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs")).Contains("MessageBoxImage.Warning", StringComparison.Ordinal),
            "The shared point remove command should ask for confirmation so no delete route can bypass the prompt.");
    }

    public static void FooterActionsOnlyShowWhenPressableAndSaveStateIsExplicitlyNotified()
    {
        var xaml = File.ReadAllText(FindWorkspaceXaml());
        var workspaceViewModelCode = File.ReadAllText(FindSourceFile("JamaicaReviewWorkspaceViewModel.cs"));
        var dockpaneCode = File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(
            xaml.Contains("Visibility=\"{Binding ShowValidationCompleteAction, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal),
            "Validation Complete should be hidden when validation cannot currently be completed.");
        TestAssert.True(
            xaml.Contains("Visibility=\"{Binding ShowSaveReviewAction, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal),
            "Save should be hidden when there are no pressable review changes to save.");
        TestAssert.True(
            xaml.Contains("<Button Content=\"Close\"\r\n                  Width=\"92\"\r\n                  Click=\"CloseButton_Click\" />", StringComparison.Ordinal)
            || xaml.Contains("<Button Content=\"Close\"\n                  Width=\"92\"\n                  Click=\"CloseButton_Click\" />", StringComparison.Ordinal),
            "Close should remain directly pressable and must not be hidden or disabled with Save / Validation Complete.");
        TestAssert.True(
            workspaceViewModelCode.Contains("case nameof(ParcelWorkflowDockpaneViewModel.HasUnsavedReviewChanges):", StringComparison.Ordinal)
            && workspaceViewModelCode.Contains("case nameof(ParcelWorkflowDockpaneViewModel.CanSaveReviewChangesFromWorkspace):", StringComparison.Ordinal),
            "The workspace should directly refresh Save when the parent dirty/save state changes.");
        TestAssert.True(
            workspaceViewModelCode.Contains("case nameof(ParcelWorkflowDockpaneViewModel.ReviewContentVersion):", StringComparison.Ordinal)
            && workspaceViewModelCode.Contains("RefreshProjection();", StringComparison.Ordinal),
            "The workspace should rebuild visible point rows when reviewed content changes in place.");
        var windowCode = File.ReadAllText(FindSourceFile("JamaicaReviewWorkspaceWindow.xaml.cs"));
        TestAssert.True(
            windowCode.Contains("Save is available for these point changes.", StringComparison.Ordinal)
            && windowCode.Contains("Save is not available for the current review state.", StringComparison.Ordinal),
            "Close should tell the user whether Save is available before closing with unsaved point changes.");
        TestAssert.True(
            windowCode.Contains("Save did not complete. The Points Validation Tool will stay open", StringComparison.Ordinal)
            && windowCode.Contains("Save is not available for the current Points Validation Tool state.", StringComparison.Ordinal),
            "Save-and-close should show a message when Save cannot run or does not complete.");
        TestAssert.True(
            dockpaneCode.Contains("NotifyPropertyChanged(nameof(HasUnsavedReviewChanges));", StringComparison.Ordinal)
            && dockpaneCode.Contains("NotifyPropertyChanged(nameof(CanSaveReviewChangesFromWorkspace));", StringComparison.Ordinal),
            "The dockpane should explicitly notify derived dirty/save state after review edits.");
        TestAssert.True(
            dockpaneCode.Contains("&& !IsPxaReviewedBoundarySegmentChainClosed()", StringComparison.Ordinal)
            && dockpaneCode.Contains("private bool IsPxaReviewedBoundarySegmentChainClosed()", StringComparison.Ordinal)
            && dockpaneCode.Contains("return string.Equals(firstFrom, finalTo, StringComparison.OrdinalIgnoreCase);", StringComparison.Ordinal),
            "PXA validation completion should not remain blocked by stale solver metadata when the reviewed segment chain is currently closed.");
    }

    public static void PxaParcelPreviewUsesUniqueReviewedSegmentPointOrder()
    {
        var rows = new[]
        {
            Row("A", "703671.70", "668854.41"),
            Row("B", "703710.10", "668915.10"),
            Row("C", "703683.70", "668899.20"),
            Row("D", "703700.10", "668861.00"),
            Row("E", "703718.40", "668832.60"),
            Row("F", "703760.50", "668877.30"),
            Row("G", "703830.80", "668948.90"),
            Row("H", "703846.20", "668936.10")
        };
        var segments = new[]
        {
            Segment(1, "A", "C"),
            Segment(2, "C", "D"),
            Segment(3, "D", "E"),
            Segment(4, "E", "B"),
            Segment(5, "B", "F"),
            Segment(6, "F", "G"),
            Segment(7, "G", "H"),
            Segment(8, "H", "A")
        };

        var orderedRows = JamaicaReviewWorkspaceViewModel.BuildPreviewRowsInReviewedSegmentOrder(rows, segments, true);

        TestAssert.Equal("A,C,D,E,B,F,G,H", string.Join(",", orderedRows.Select(row => row.PointIdentifier)), "PXA preview should follow reviewed segment order without adding the closing point as a duplicate row.");
        TestAssert.True(JamaicaReviewWorkspaceViewModel.ReviewedSegmentChainCloses(segments), "The preview should recognize the explicit H-A closing segment.");
    }

    public static void PxaParcelPreviewGeometryPreservesRepeatedSegmentVertices()
    {
        var rows = new[]
        {
            Row("A", "0", "0"),
            Row("Y", "1", "0"),
            Row("C", "2", "0"),
            Row("D", "2", "1"),
            Row("E", "3", "1"),
            Row("F", "4", "1"),
            Row("G", "5", "1"),
            Row("B", "5", "0"),
            Row("X", "4", "0"),
            Row("W", "3", "0")
        };
        var segments = new[]
        {
            Segment(1, "A", "Y"),
            Segment(2, "Y", "C"),
            Segment(3, "C", "D"),
            Segment(4, "D", "E"),
            Segment(5, "E", "F"),
            Segment(6, "F", "G"),
            Segment(7, "G", "B"),
            Segment(8, "B", "X"),
            Segment(9, "X", "W"),
            Segment(10, "W", "C"),
            Segment(11, "C", "D"),
            Segment(12, "D", "A")
        };

        var uniqueRows = JamaicaReviewWorkspaceViewModel.BuildPreviewRowsInReviewedSegmentOrder(rows, segments, true);
        var geometryRows = JamaicaReviewWorkspaceViewModel.BuildPreviewRowsInReviewedSegmentPath(rows, segments, true);

        TestAssert.Equal("A,Y,C,D,E,F,G,B,X,W", string.Join(",", uniqueRows.Select(row => row.PointIdentifier)), "The point review order should stay unique.");
        TestAssert.Equal("A,Y,C,D,E,F,G,B,X,W,C,D,A", string.Join(",", geometryRows.Select(row => row.PointIdentifier)), "The preview geometry should follow the reviewed segment path, including repeated vertices.");
    }

    public static void PxaParcelPreviewCanUseReviewedSegmentBearingsWhenRowsConflict()
    {
        var segments = new[]
        {
            Segment(1, "13", "14", "N90°00'E", "10"),
            Segment(2, "14", "15", "N00°00'E", "5"),
            Segment(3, "15", "16", "N90°00'W", "10"),
            Segment(4, "16", "13", "S00°00'E", "5")
        };

        var points = JamaicaReviewWorkspaceViewModel.BuildPreviewPointsFromReviewedSegments(segments, true);

        TestAssert.Equal(5, points.Count, "The preview should derive one vertex per reviewed segment plus the starting vertex.");
        TestAssert.True(Math.Abs(points[^1].X) < 0.001d && Math.Abs(points[^1].Y) < 0.001d, "Segment-derived preview should close when the reviewed bearings and distances close.");
    }

    public static void VisibleRowsAreRebuiltFromLiveReviewRowsAfterDelete()
    {
        var liveRows = new List<ExtractionReviewRowViewModel>
        {
            Row("A", "0", "0"),
            Row("B", "1", "1"),
            Row("C", "2", "2")
        };
        liveRows[0].Model.SequenceInGroup = 1;
        liveRows[1].Model.SequenceInGroup = 2;
        liveRows[2].Model.SequenceInGroup = 3;

        var staleGroupSnapshot = liveRows.ToArray();
        liveRows.RemoveAt(1);

        var visibleRows = JamaicaReviewWorkspaceViewModel.BuildVisibleRowsForParcel(liveRows, "parcel-001");

        TestAssert.Equal("A,C", string.Join(",", visibleRows.Select(row => row.PointIdentifier)), "Visible PXA point rows should be rebuilt from live ReviewRows after a delete.");
        TestAssert.True(staleGroupSnapshot.Any(row => row.PointIdentifier == "B"), "The test must simulate a stale parcel-group snapshot that still contains the deleted point.");
    }

    public static void LoginServerAddressIsConfigurationOnly()
    {
        var xaml = File.ReadAllText(FindSourceFile("LoginWindow.xaml"));
        var code = File.ReadAllText(FindSourceFile("LoginWindow.xaml.cs"));

        TestAssert.True(
            !xaml.Contains("x:Name=\"ServerTextBox\"", StringComparison.Ordinal)
            && xaml.Contains("x:Name=\"ServerTextBlock\"", StringComparison.Ordinal),
            "Login server address should be display-only text, not an editable TextBox.");
        TestAssert.True(
            code.Contains("ServerTextBlock.Text = ShellState.ConfiguredServerUrl;", StringComparison.Ordinal)
            && code.Contains("ShellState.Session.LoginAsync(ShellState.ConfiguredServerUrl", StringComparison.Ordinal),
            "Login server address should continue to come from the configured Innola server URL and never from user-edited text.");
    }

    private static ExtractionReviewRowViewModel Row(string pointIdentifier, string easting, string northing)
    {
        return new ExtractionReviewRowViewModel(
            new ExtractionReviewRow
            {
                ParcelGroupId = "parcel-001",
                PointIdentifier = pointIdentifier,
                Easting = easting,
                Northing = northing
            },
            () => { });
    }

    private static ExtractionReviewSegmentViewModel Segment(int sequence, string fromPoint, string toPoint)
    {
        return Segment(sequence, fromPoint, toPoint, "N90°00'E", "1");
    }

    private static ExtractionReviewSegmentViewModel Segment(int sequence, string fromPoint, string toPoint, string bearing, string distance)
    {
        return new ExtractionReviewSegmentViewModel(
            new ExtractionReviewSegment
            {
                Sequence = sequence,
                FromPoint = fromPoint,
                ToPoint = toPoint,
                BearingText = bearing,
                DistanceText = distance,
                IncludeInBoundary = true
            },
            () => { });
    }

    private static string FindWorkspaceXaml()
    {
        return FindSourceFile("JamaicaReviewWorkspaceWindow.xaml");
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
