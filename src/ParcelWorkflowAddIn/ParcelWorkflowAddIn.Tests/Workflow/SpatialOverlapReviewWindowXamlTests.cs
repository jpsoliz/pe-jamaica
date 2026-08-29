namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class SpatialOverlapReviewWindowXamlTests
{
    public static void SpatialOverlapReviewWindowRendersEvidenceSurface()
    {
        var xaml = File.ReadAllText(FindRepoFile("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Workflow", "SpatialReview", "SpatialOverlapReviewWindow.xaml"));
        var codeBehind = File.ReadAllText(FindRepoFile("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "Workflow", "SpatialReview", "SpatialOverlapReviewWindow.xaml.cs"));

        TestAssert.True(
            xaml.Contains("Text=\"{Binding NoOverlapText}\"", StringComparison.Ordinal)
            && xaml.Contains("Visibility=\"{Binding HasNoOverlapResult, Converter={StaticResource BoolToVisibility}}\"", StringComparison.Ordinal),
            "The overlap review window should render the explicit no-overlap empty state.");
        TestAssert.True(
            xaml.Contains("ItemsSource=\"{Binding Records}\"", StringComparison.Ordinal)
            && xaml.Contains("ItemsSource=\"{Binding SnapshotRefs}\"", StringComparison.Ordinal),
            "The overlap review window should bind both overlap rows and snapshot references.");
        foreach (var header in new[]
                 {
                     "Header=\"Overlap Id\"",
                     "Header=\"Group Id\"",
                     "Header=\"Identifiers\"",
                     "Header=\"Enrichment\""
                 })
        {
            TestAssert.True(
                xaml.Contains(header, StringComparison.Ordinal),
                $"The overlap review window should expose the grid column {header}.");
        }

        TestAssert.True(
            codeBehind.Contains("ShowOrActivate", StringComparison.Ordinal)
            && codeBehind.Contains("RefreshIfOpen", StringComparison.Ordinal),
            "The overlap review surface should support both opening and refresh without rebuilding the workflow.");
    }

    public static void ParcelWorkflowDockpaneHidesComputeOverlapReviewCommands()
    {
        var xaml = File.ReadAllText(FindRepoFile("src", "ParcelWorkflowAddIn", "ParcelWorkflowAddIn", "ParcelWorkflowDockpane.xaml"));

        TestAssert.True(
            !xaml.Contains("Content=\"Run Overlap Review\"", StringComparison.Ordinal)
            && !xaml.Contains("Content=\"View Overlap Review\"", StringComparison.Ordinal),
            "The compute workflow dockpane should hide overlap review commands in Final Review.");
    }

    private static string FindRepoFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeSegments)} from the test output directory.");
    }
}
