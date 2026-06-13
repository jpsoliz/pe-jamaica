using ParcelWorkflowAddIn.CaseFolders;

namespace ParcelWorkflowAddIn.Workflow.Review;

internal static class ReviewSourceSelectionResolver
{
    public static SourceFileCopyResult? Resolve(IReadOnlyList<SourceFileCopyResult> sourceFiles, string? selectedCopiedPath)
    {
        if (sourceFiles.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(selectedCopiedPath))
        {
            var selected = sourceFiles.FirstOrDefault(source =>
                string.Equals(source.CopiedPath, selectedCopiedPath, StringComparison.OrdinalIgnoreCase));

            if (selected is not null)
            {
                return selected;
            }
        }

        return sourceFiles.FirstOrDefault(item => string.Equals(item.SourceRole, "computation_source", StringComparison.OrdinalIgnoreCase))
            ?? sourceFiles.FirstOrDefault(item => string.Equals(item.SourceRole, "points_computation", StringComparison.OrdinalIgnoreCase))
            ?? sourceFiles.FirstOrDefault(item => string.Equals(item.SourceRole, "plan_map_reference", StringComparison.OrdinalIgnoreCase))
            ?? sourceFiles.FirstOrDefault();
    }
}
