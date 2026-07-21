namespace ParcelWorkflowAddIn.Tests.Workflow;

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
