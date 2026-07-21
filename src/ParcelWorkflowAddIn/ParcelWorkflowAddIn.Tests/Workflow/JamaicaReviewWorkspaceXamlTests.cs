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
