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

    public static void CompareWorkspaceExposesTaskLifecycleCommands()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Content=\"Save draft\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding SaveProgressCommand}\"", StringComparison.Ordinal),
            "Compare should expose Save draft as an evidence-only draft save command.");
        TestAssert.True(
            xaml.Contains("Content=\"Suspend task\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding SuspendTaskCommand}\"", StringComparison.Ordinal),
            "Compare should expose Suspend task for save-and-close lifecycle release.");
        TestAssert.True(
            xaml.Contains("Content=\"Complete task\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding CompleteTaskCommand}\"", StringComparison.Ordinal),
            "Compare should expose Complete task for approved Compare completion.");
        TestAssert.True(
            xaml.Contains("Content=\"Close window\"", StringComparison.Ordinal),
            "Compare should clearly label the window-only close action.");
        TestAssert.True(
            xaml.Contains("VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal),
            "Compare evidence and decision controls should remain reachable in compact windows.");
    }

    public static void CompareWorkspaceRendersPortalLikeQueryResultColumns()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        foreach (var header in new[]
                 {
                     "Header=\"Volume/Folio\"",
                     "Header=\"Type\"",
                     "Header=\"Tenure\"",
                     "Header=\"PID\"",
                     "Header=\"LandVal No.\"",
                     "Header=\"Owner\"",
                     "Header=\"Parish\"",
                     "Header=\"Date Registered\""
                 })
        {
            TestAssert.True(
                xaml.Contains(header, StringComparison.Ordinal),
                $"Compare query results should expose the portal-like grid column {header}.");
        }

        foreach (var visibilityBinding in new[]
                 {
                     "Visibility=\"{Binding HasQueryResults",
                     "Visibility=\"{Binding HasValuableEvidenceItems",
                     "Visibility=\"{Binding HasEnterpriseCadasterEvidenceRows",
                     "Visibility=\"{Binding HasEvidenceItems",
                     "Visibility=\"{Binding HasDiscrepancies"
                 })
        {
            TestAssert.True(
                xaml.Contains(visibilityBinding, StringComparison.Ordinal),
                $"Empty evidence panels should be collapsed through {visibilityBinding}.");
        }
    }

    public static void CompareWorkspaceLabelsValuableEvidenceList()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Text=\"Valuable Evidence\"", StringComparison.Ordinal),
            "Compare should title the retained valuable evidence list so marked rows are discoverable.");
        TestAssert.True(
            xaml.Contains("ItemsSource=\"{Binding ValuableEvidenceItems}\"", StringComparison.Ordinal)
            && xaml.Contains("Retained search results for the Compare decision.", StringComparison.Ordinal),
            "Compare should explain that valuable evidence rows are retained for the decision.");
        foreach (var expected in new[]
                 {
                     "Header=\"Role\"",
                     "Header=\"Source\"",
                     "Header=\"Evidence\"",
                     "DisplayMemberBinding=\"{Binding SourceLabel}\"",
                     "DisplayMemberBinding=\"{Binding DisplaySummary}\"",
                     "Command=\"{Binding DataContext.RemoveValuableEvidenceCommand"
                 })
        {
            TestAssert.True(
                xaml.Contains(expected, StringComparison.Ordinal),
                $"Valuable evidence rows should render retained values through {expected}.");
        }
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
