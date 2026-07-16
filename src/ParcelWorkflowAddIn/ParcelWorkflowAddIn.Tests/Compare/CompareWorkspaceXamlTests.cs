namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareWorkspaceXamlTests
{
    public static void OwnershipEvidenceExposesFiscalNeighborCommand()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Command=\"{Binding RefreshEnterpriseCadasterEvidenceCommand}\"", StringComparison.Ordinal),
            "Compare ownership evidence UI should expose the Enterprise Legal/Fiscal spatial evidence refresh command required for Compare approval.");
        TestAssert.True(
            xaml.Contains("Refresh Legal/Fiscal spatial evidence", StringComparison.Ordinal),
            "Legal/Fiscal spatial evidence refresh action should be visible to the examiner.");
        TestAssert.True(
            xaml.Contains("Command=\"{Binding DataContext.SeedSearchFromEnterpriseEvidenceCommand", StringComparison.Ordinal),
            "Spatial evidence rows should expose a command that seeds Innola tabular search fields.");
    }

    public static void CompareWorkspaceExposesCollapsiblePdfPanelControls()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());
        var codeBehind = File.ReadAllText(FindCompareWorkspaceCodeBehind());

        TestAssert.True(
            xaml.Contains("x:Name=\"PdfPanelColumn\"", StringComparison.Ordinal),
            "Compare workspace should name the PDF column so it can collapse without rebuilding the window.");
        TestAssert.True(
            xaml.Contains("x:Name=\"PdfPanelSpacerColumn\"", StringComparison.Ordinal),
            "Compare workspace should name the spacer column so it can collapse with the PDF panel.");
        TestAssert.True(
            xaml.Contains("x:Name=\"PdfPanel\"", StringComparison.Ordinal),
            "Compare workspace should name the PDF panel for code-behind visibility updates.");
        TestAssert.True(
            xaml.Contains("Command=\"{Binding TogglePdfPanelCommand}\"", StringComparison.Ordinal),
            "Compare workspace should bind a visible command for hiding/restoring the PDF panel.");
        TestAssert.True(
            xaml.Contains("Text=\"{Binding PdfPanelToggleText}\"", StringComparison.Ordinal)
            || xaml.Contains("Content=\"{Binding PdfPanelToggleText}\"", StringComparison.Ordinal),
            "Compare workspace should display Hide PDF / Show PDF text from the ViewModel.");
        TestAssert.True(
            codeBehind.Contains("CompactCompareWindowWidth", StringComparison.Ordinal)
            && codeBehind.Contains("ExpandedCompareWindowMinWidth", StringComparison.Ordinal),
            "Compare workspace should resize to compact width when the PDF panel is hidden and restore expanded sizing when shown.");
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

    private static string FindCompareWorkspaceCodeBehind()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                "CompareWorkspaceWindow.xaml.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate CompareWorkspaceWindow.xaml.cs from the test output directory.");
    }
}
