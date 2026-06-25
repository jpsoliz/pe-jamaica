using ParcelWorkflowAddIn.Intake;

namespace ParcelWorkflowAddIn.Innola;

internal static class ComputeAttachmentSourceTypeCatalog
{
    public static IReadOnlyList<ComputeAttachmentSourceTypeDefinition> SafeDefaults { get; } = new[]
    {
        new ComputeAttachmentSourceTypeDefinition(
            "st_surveyplan",
            SourceRole.PlanMapReference,
            "Survey plan / map reference",
            Required: true,
            InternalOnly: false,
            Extensions: new[] { ".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_surveysheet",
            SourceRole.ComputationSheet,
            "Survey / computation sheet",
            Required: true,
            InternalOnly: false,
            Extensions: new[] { ".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_survey_points",
            SourceRole.CoordinateTextSource,
            "Structured survey points",
            Required: false,
            InternalOnly: false,
            Extensions: new[] { ".txt", ".csv" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_autocad_file",
            SourceRole.DwgSource,
            "AutoCAD survey source",
            Required: false,
            InternalOnly: false,
            Extensions: new[] { ".dwg" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_survey_zip",
            SourceRole.WorkflowResumePackage,
            "Internal workflow package",
            Required: false,
            InternalOnly: true,
            Extensions: new[] { ".zip" })
    };

    public static IReadOnlyList<string> RequiredWorkflowRoles { get; } = SafeDefaults
        .Where(item => item.Required && !item.InternalOnly)
        .Select(item => item.WorkflowRole)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static IReadOnlyList<string> GeoreferenceSourceRoles { get; } = new[]
    {
        SourceRole.ComputationSheet,
        SourceRole.CoordinateTextSource,
        SourceRole.PlanMapReference
    };
}
