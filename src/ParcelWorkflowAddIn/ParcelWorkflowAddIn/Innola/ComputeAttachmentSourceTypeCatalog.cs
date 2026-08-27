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
            "st_survey_plan_pdf",
            SourceRole.SurveyPlanPdf,
            "Survey plan PDF",
            Required: false,
            InternalOnly: false,
            Extensions: new[] { ".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_plan_annexation_pdf",
            SourceRole.PlanAnnexationPdf,
            "Plan annexation PDF",
            Required: true,
            InternalOnly: false,
            Extensions: new[] { ".pdf" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_survey_diagram",
            SourceRole.SurveyDiagramPdf,
            "Survey diagram PDF",
            Required: true,
            InternalOnly: false,
            Extensions: new[] { ".pdf" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_survey_diagram_png",
            SourceRole.PlaGeneratedOutput,
            "PLA_B survey diagram selection PNG",
            Required: false,
            InternalOnly: true,
            Extensions: new[] { ".png" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_plan_annex_image",
            SourceRole.PlaGeneratedOutput,
            "PLA_B plan annexation image crop",
            Required: false,
            InternalOnly: true,
            Extensions: new[] { ".png" }),
        new ComputeAttachmentSourceTypeDefinition(
            "pla_b_recovery",
            SourceRole.PlaBRecovery,
            "PLA_B recovery plan",
            Required: false,
            InternalOnly: true,
            Extensions: Array.Empty<string>()),
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
            Extensions: new[] { ".zip" }),
        new ComputeAttachmentSourceTypeDefinition(
            "st_compute_report",
            SourceRole.ComputeReport,
            "Compute report",
            Required: false,
            InternalOnly: true,
            Extensions: new[] { ".pdf" })
    };

    public static IReadOnlyList<string> RequiredWorkflowRoles { get; } = new[]
    {
        SourceRole.PlanMapReference,
        SourceRole.ComputationSheet
    };

    public static IReadOnlyList<string> GeoreferenceSourceRoles { get; } = new[]
    {
        SourceRole.ComputationSheet,
        SourceRole.CoordinateTextSource,
        SourceRole.PlanMapReference,
        SourceRole.SurveyPlanPdf
    };
}
