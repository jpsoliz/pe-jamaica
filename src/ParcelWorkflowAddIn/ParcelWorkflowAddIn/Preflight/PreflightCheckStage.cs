namespace ParcelWorkflowAddIn.Preflight;

public enum PreflightCheckStage
{
    Combined,
    StructureCheck,
    GeoreferenceCheck,
    DimensionCheck
}

public static class PreflightCheckStageExtensions
{
    public const string CombinedStageId = "preflight";
    public const string StructureCheckStageId = "structure_check";
    public const string GeoreferenceCheckStageId = "georeference_check";
    public const string DimensionCheckStageId = "dimension_check";

    public static string ToStageId(this PreflightCheckStage stage) =>
        stage switch
        {
            PreflightCheckStage.StructureCheck => StructureCheckStageId,
            PreflightCheckStage.GeoreferenceCheck => GeoreferenceCheckStageId,
            PreflightCheckStage.DimensionCheck => DimensionCheckStageId,
            _ => CombinedStageId
        };

    public static string ToArtifactFileName(this PreflightCheckStage stage) =>
        stage switch
        {
            PreflightCheckStage.StructureCheck => "structure_check_summary.json",
            PreflightCheckStage.GeoreferenceCheck => "georeference_check_summary.json",
            PreflightCheckStage.DimensionCheck => "dimension_check_summary.json",
            _ => "preflight_summary.json"
        };
}
