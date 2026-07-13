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

    private static string FindWorkspaceXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                "JamaicaReviewWorkspaceWindow.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate JamaicaReviewWorkspaceWindow.xaml from the test output directory.");
    }
}
