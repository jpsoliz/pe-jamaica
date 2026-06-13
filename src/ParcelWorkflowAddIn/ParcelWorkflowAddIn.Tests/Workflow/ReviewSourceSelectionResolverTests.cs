using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ReviewSourceSelectionResolverTests
{
    public static void ResolverPrefersComputationThenPointsThenPlan()
    {
        var sources = new[]
        {
            new SourceFileCopyResult("incoming:plan.pdf", "C:\\case\\source\\plan.pdf", "plan.pdf", ".pdf", 5, "plan_map_reference", "copied", "Copied", true),
            new SourceFileCopyResult("incoming:computation.pdf", "C:\\case\\source\\computation.pdf", "computation.pdf", ".pdf", 5, "computation_source", "copied", "Copied", true)
        };

        var selected = ReviewSourceSelectionResolver.Resolve(sources, null);

        TestAssert.Equal("computation.pdf", selected?.FileName, "Resolver should prefer computation source by default.");
    }

    public static void ResolverKeepsExplicitSelectionWhenAvailable()
    {
        var sources = new[]
        {
            new SourceFileCopyResult("incoming:computation.pdf", "C:\\case\\source\\computation.pdf", "computation.pdf", ".pdf", 5, "computation_source", "copied", "Copied", true),
            new SourceFileCopyResult("incoming:plan.pdf", "C:\\case\\source\\plan.pdf", "plan.pdf", ".pdf", 5, "plan_map_reference", "copied", "Copied", true)
        };

        var selected = ReviewSourceSelectionResolver.Resolve(sources, "C:\\case\\source\\plan.pdf");

        TestAssert.Equal("plan.pdf", selected?.FileName, "Resolver should respect the explicit selected source when it still exists.");
    }

    public static void ResolverFallsBackWhenExplicitSelectionDisappears()
    {
        var sources = new[]
        {
            new SourceFileCopyResult("incoming:points.csv", "C:\\case\\source\\points.csv", "points.csv", ".csv", 5, "points_computation", "copied", "Copied", true),
            new SourceFileCopyResult("incoming:plan.pdf", "C:\\case\\source\\plan.pdf", "plan.pdf", ".pdf", 5, "plan_map_reference", "copied", "Copied", true)
        };

        var selected = ReviewSourceSelectionResolver.Resolve(sources, "C:\\case\\source\\missing.pdf");

        TestAssert.Equal("points.csv", selected?.FileName, "Resolver should fall back to preferred available sources when the prior selection is gone.");
    }
}
