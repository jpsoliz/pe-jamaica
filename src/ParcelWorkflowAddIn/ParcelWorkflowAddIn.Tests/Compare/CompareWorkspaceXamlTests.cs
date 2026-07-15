namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareWorkspaceXamlTests
{
    public static void OwnershipEvidenceExposesFiscalNeighborCommand()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Command=\"{Binding FindNeighborsCommand}\"", StringComparison.Ordinal),
            "Compare ownership evidence UI should expose the fiscal neighbor review command required for Compare approval.");
        TestAssert.True(
            xaml.Contains("Find fiscal neighbors", StringComparison.Ordinal),
            "Fiscal neighbor action should be visible to the examiner.");
    }

    private static string FindCompareWorkspaceXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                "CompareWorkspaceWindow.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate CompareWorkspaceWindow.xaml from the test output directory.");
    }
}
