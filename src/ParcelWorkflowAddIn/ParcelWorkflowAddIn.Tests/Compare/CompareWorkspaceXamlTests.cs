namespace ParcelWorkflowAddIn.Tests.Compare;

internal static class CompareWorkspaceXamlTests
{
    public static void OwnershipEvidenceExposesFiscalNeighborCommand()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Command=\"{Binding ReloadGeometryCommand}\"", StringComparison.Ordinal),
            "Compare ownership evidence UI should expose a Load Compare Layers command that loads or reloads the Compare working layers.");
        TestAssert.True(
            xaml.Contains("Text=\"Load Compare Layers\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareLoad.png\"", StringComparison.Ordinal)
            && !xaml.Contains("Refresh Legal/Fiscal spatial evidence", StringComparison.Ordinal),
            "Compare map loading should be presented as Load Compare Layers with the load icon.");
        TestAssert.False(
            xaml.Contains("Text=\"{Binding FiscalEvidenceStatus}\"", StringComparison.Ordinal),
            "Compare should not show fiscal neighbor status in the top toolbar.");
        TestAssert.False(
            xaml.Contains("Content=\"Show Map\"", StringComparison.Ordinal)
            || xaml.Contains("Content=\"Show active map\"", StringComparison.Ordinal),
            "Compare should not expose a Show Map button in the compact toolbar.");
        TestAssert.False(
            xaml.Contains("Content=\"Refresh\" Command=\"{Binding ReloadGeometryCommand}\"", StringComparison.Ordinal),
            "Compare should not expose a second generic Refresh button when Load Compare Layers owns the map-context refresh.");
        TestAssert.False(
            xaml.Contains("Command=\"{Binding DataContext.SeedSearchFromEnterpriseEvidenceCommand", StringComparison.Ordinal),
            "The compact Compare form should not render the unused spatial evidence seed grid.");
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
            xaml.Contains("x:Name=\"PdfPanelSplitter\"", StringComparison.Ordinal)
            && xaml.Contains("ResizeDirection=\"Columns\"", StringComparison.Ordinal)
            && xaml.Contains("ResizeBehavior=\"PreviousAndNext\"", StringComparison.Ordinal),
            "Compare workspace should expose a splitter so the document and Compare panels can be resized.");
        TestAssert.True(
            xaml.Contains("Command=\"{Binding TogglePdfPanelCommand}\"", StringComparison.Ordinal),
            "Compare workspace should bind a visible command for hiding/restoring the PDF panel.");
        TestAssert.True(
            xaml.Contains("Text=\"{Binding PdfPanelToggleText}\"", StringComparison.Ordinal)
            || xaml.Contains("Content=\"{Binding PdfPanelToggleText}\"", StringComparison.Ordinal),
            "Compare workspace should display Hide Files / Show Files text from the ViewModel.");
        TestAssert.True(
            codeBehind.Contains("CompactCompareWindowWidth", StringComparison.Ordinal)
            && codeBehind.Contains("ExpandedCompareWindowMinWidth", StringComparison.Ordinal),
            "Compare workspace should resize to compact width when the PDF panel is hidden and restore expanded sizing when shown.");
        TestAssert.True(
            codeBehind.Contains("PdfPanelSplitter.Visibility", StringComparison.Ordinal),
            "Compare workspace should hide the resize splitter when the PDF panel is collapsed.");
    }

    public static void CompareWorkspaceExposesTaskLifecycleCommands()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Text=\"Save\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareSave.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding SaveProgressCommand}\"", StringComparison.Ordinal),
            "Compare should expose Save as a draft save and report-generation command with the save icon.");
        TestAssert.True(
            xaml.Contains("<StackPanel Grid.Column=\"0\" Orientation=\"Horizontal\" HorizontalAlignment=\"Left\">", StringComparison.Ordinal),
            "Save, Suspend and Finalize should sit in the fixed footer row at the lower left, aligned with Cancel.");
        TestAssert.True(
            xaml.Contains("Width=\"100\"", StringComparison.Ordinal)
            && !xaml.Contains("<UniformGrid Columns=\"3\">", StringComparison.Ordinal),
            "Footer lifecycle buttons should use compact fixed sizing instead of stretching across the Compare content area.");
        TestAssert.True(
            xaml.Contains("Visibility=\"{Binding IsFinalizeOperationRunning, Converter={StaticResource BoolToVisibility}}\"", StringComparison.Ordinal)
            && xaml.Contains("IsIndeterminate=\"True\"", StringComparison.Ordinal)
            && xaml.Contains("Text=\"{Binding FinalizeOperationRunningText}\"", StringComparison.Ordinal),
            "Compare should show an indeterminate footer progress indicator while Finalize is saving.");
        TestAssert.True(
            xaml.Contains("Saves the current Compare status and regenerates the PDF report", StringComparison.Ordinal),
            "Save should explain that it saves the current status and regenerates the PDF report.");
        TestAssert.True(
            xaml.Contains("Text=\"Suspend\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareSuspend.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding SuspendTaskCommand}\"", StringComparison.Ordinal),
            "Compare should expose Suspend for save-and-close lifecycle release with the suspend icon.");
        TestAssert.False(
            xaml.Contains("Content=\"Complete task\"", StringComparison.Ordinal)
            || xaml.Contains("Content=\"Block\"", StringComparison.Ordinal)
            || xaml.Contains("Command=\"{Binding CompleteTaskCommand}\"", StringComparison.Ordinal)
            || xaml.Contains("Command=\"{Binding BlockCompareCommand}\"", StringComparison.Ordinal),
            "Compare should not expose Block or Complete task in the simplified action row.");
        TestAssert.True(
            xaml.Contains("Text=\"Cancel\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareCancel.png\"", StringComparison.Ordinal)
            && xaml.Contains("Cancels the Compare workspace without saving", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding CancelTaskCommand}\"", StringComparison.Ordinal),
            "Compare should label the no-save cleanup action as Cancel with the cancel icon.");
        TestAssert.True(
            xaml.Contains("regenerates and uploads the PDF report", StringComparison.Ordinal),
            "Finalize should explain that it saves, regenerates and uploads the PDF report, then clears the workspace.");
        TestAssert.True(
            xaml.Contains("Text=\"Finalize\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareFinalize.png\"", StringComparison.Ordinal)
            && !xaml.Contains("Content=\"Approve Compare\"", StringComparison.Ordinal)
            && !xaml.Contains("Content=\"Return to Compute\"", StringComparison.Ordinal),
            "Compare should use Finalize as the approving action with the finalize icon and should not expose Return to Compute.");
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
                     "Visibility=\"{Binding HasRelatedPartyMatches",
                     "Visibility=\"{Binding HasValuableEvidenceItems"
                 })
        {
            TestAssert.True(
                xaml.Contains(visibilityBinding, StringComparison.Ordinal),
                $"Empty evidence panels should be collapsed through {visibilityBinding}.");
        }

        TestAssert.True(
            xaml.Contains("MaxHeight=\"220\"", StringComparison.Ordinal)
            && xaml.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal),
            "Large query result sets should scroll inside the results grid instead of stretching the whole Compare form.");
        TestAssert.True(
            xaml.Contains("Text=\"Search Results\"", StringComparison.Ordinal),
            "Compare should title the Innola search results grid.");
        TestAssert.True(
            xaml.Contains("Text=\"Related Party Matches\"", StringComparison.Ordinal)
            && xaml.Contains("ItemsSource=\"{Binding RelatedPartyMatches}\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding DataContext.MarkPartyMatchValuableCommand", StringComparison.Ordinal),
            "Compare should render party-shaped Innola rows separately with a Keep action.");
        TestAssert.True(
            xaml.Contains("Content=\"Keep\"", StringComparison.Ordinal)
            && !xaml.Contains("Content=\"Valuable\"", StringComparison.Ordinal),
            "Compare search result actions should use Keep instead of Valuable.");
        TestAssert.True(
            xaml.Contains("<Border Grid.Row=\"1\"", StringComparison.Ordinal)
            && xaml.Contains("Text=\"{Binding EvidenceSearchStatusMessage}\"", StringComparison.Ordinal)
            && xaml.Contains("<StackPanel Grid.Row=\"2\" Margin=\"0,6,0,0\">", StringComparison.Ordinal),
            "Ownership search controls and feedback should be grouped in a bordered container with the status text at the bottom-left.");
        TestAssert.False(
            xaml.Contains("ItemsSource=\"{Binding EnterpriseCadasterEvidenceRows}\"", StringComparison.Ordinal)
            || xaml.Contains("ItemsSource=\"{Binding Discrepancies}\"", StringComparison.Ordinal),
            "Compare should not render unused empty spatial evidence or discrepancy grids below Valuable Evidence.");
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
                     "Header=\"Evidence\"",
                     "DisplayMemberBinding=\"{Binding DisplaySummary}\"",
                     "Command=\"{Binding DataContext.RemoveValuableEvidenceCommand"
                 })
        {
            TestAssert.True(
                xaml.Contains(expected, StringComparison.Ordinal),
                $"Valuable evidence rows should render retained values through {expected}.");
        }

        TestAssert.False(
            xaml.Contains("Header=\"Source\"", StringComparison.Ordinal),
            "Valuable evidence should keep source metadata in the model but hide the Source column from the compact Compare grid.");
    }

    public static void CompareWorkspaceExposesOverlapReviewControls()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Text=\"Run Overlap Review\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareRun.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding RunOverlapReviewCommand}\"", StringComparison.Ordinal),
            "Compare should expose a Run Overlap Review command in the review shell with the run icon.");
        TestAssert.True(
            xaml.Contains("Text=\"View Overlap Review\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/CompareView.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding OpenOverlapReviewCommand}\"", StringComparison.Ordinal),
            "Compare should expose a View Overlap Review command for the dedicated evidence surface with the view icon.");
        TestAssert.True(
            xaml.Contains("Text=\"{Binding OverlapReviewStatus}\"", StringComparison.Ordinal),
            "Compare should surface overlap review status text after the button runs.");
    }

    public static void CompareWorkspaceExposesComputedParticipantsTitleImageOnlyThere()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Text=\"Computed Participants\"", StringComparison.Ordinal)
            && xaml.Contains("ItemsSource=\"{Binding ComputedParticipants}\"", StringComparison.Ordinal),
            "Compare should expose computed participants separately from generic ownership search results.");
        TestAssert.True(
            xaml.Contains("Text=\"Search Title Image\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/SearchTitleImages.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding SearchTitleImageCommand}\"", StringComparison.Ordinal),
            "Search Title Image should bind to the computed participant command with the dedicated title icon.");
        TestAssert.True(
            xaml.Contains("SelectedItem=\"{Binding SelectedComputedParticipant", StringComparison.Ordinal)
            && xaml.Contains("Text=\"{Binding ComputedParticipantsStatus}\"", StringComparison.Ordinal),
            "Computed participants should support row selection and status feedback.");
        TestAssert.True(
            xaml.Contains("MaxHeight=\"170\"", StringComparison.Ordinal)
            && xaml.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal),
            "Computed participants should stay bounded for long Compute participant lists.");
        TestAssert.False(
            xaml.Contains("Command=\"{Binding DataContext.SearchTitleImageCommand", StringComparison.Ordinal),
            "Generic search result rows should not expose Search Title Image as a per-row command.");
    }

    public static void CompareWorkspaceUsesSearchIcons()
    {
        var xaml = File.ReadAllText(FindCompareWorkspaceXaml());

        TestAssert.True(
            xaml.Contains("Text=\"Search\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/Search.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding RunEvidenceSearchCommand}\"", StringComparison.Ordinal),
            "Ownership Evidence Search should use the dedicated search icon.");
        TestAssert.True(
            xaml.Contains("Text=\"Clear\"", StringComparison.Ordinal)
            && xaml.Contains("Source=\"Images/CompareIcons/ClearSearch.png\"", StringComparison.Ordinal)
            && xaml.Contains("Command=\"{Binding ClearEvidenceSearchFieldsCommand}\"", StringComparison.Ordinal),
            "Ownership Evidence Clear should use the dedicated clear-search icon.");
        TestAssert.False(
            xaml.Contains("Content=\"Search\" Command=\"{Binding RunEvidenceSearchCommand}\"", StringComparison.Ordinal)
            || xaml.Contains("Content=\"Clear\" Command=\"{Binding ClearEvidenceSearchFieldsCommand}\"", StringComparison.Ordinal),
            "Search and Clear should be icon buttons with explicit text content, not plain content-only buttons.");
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
