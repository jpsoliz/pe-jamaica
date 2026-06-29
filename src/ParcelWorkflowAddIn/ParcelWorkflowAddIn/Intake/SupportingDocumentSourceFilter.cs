using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.Intake;

public static class SupportingDocumentSourceFilter
{
    public static IReadOnlyList<ManifestSourceFile> Apply(
        IReadOnlyList<ManifestSourceFile> sourceFiles,
        ManifestSupportingDocumentOptions? options)
    {
        if (sourceFiles.Count == 0)
        {
            return sourceFiles;
        }

        var effectiveOptions = options ?? new ManifestSupportingDocumentOptions();
        return sourceFiles
            .Where(sourceFile => ShouldInclude(sourceFile, effectiveOptions))
            .ToArray();
    }

    private static bool ShouldInclude(ManifestSourceFile sourceFile, ManifestSupportingDocumentOptions options)
    {
        return SourceRole.Normalize(sourceFile.SourceRole) switch
        {
            SourceRole.CoordinateTextSource => options.ImportStructuredSurveyPoints,
            SourceRole.DwgSource => options.ImportAutoCadSurveySource,
            _ => true
        };
    }
}
