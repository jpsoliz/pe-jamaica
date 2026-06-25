using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;

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

        return sourceFiles.FirstOrDefault(item => SourceRole.Matches(item.SourceRole, SourceRole.ComputationSheet))
            ?? sourceFiles.FirstOrDefault(item => SourceRole.Matches(item.SourceRole, SourceRole.CoordinateTextSource))
            ?? sourceFiles.FirstOrDefault(item => SourceRole.Matches(item.SourceRole, SourceRole.PlanMapReference))
            ?? sourceFiles.FirstOrDefault();
    }
}
