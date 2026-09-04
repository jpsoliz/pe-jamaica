using ParcelWorkflowAddIn.Workflow.RtExamination;

namespace ParcelWorkflowAddIn.Innola;

internal enum ParcelWorkflowStageRoute
{
    Unsupported,
    Compute,
    Compare,
    PlaBPlanAnnexation,
    FabricMaintenancePromotion,
    RtExamination
}

internal static class ParcelWorkflowStageRouter
{
    public static ParcelWorkflowStageRoute Resolve(
        string? taskName,
        IReadOnlyCollection<string> computeWorkflowStages,
        IReadOnlyCollection<string> compareWorkflowStages,
        RtExaminationSettings? rtExaminationSettings = null)
    {
        var normalizedStage = taskName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedStage))
        {
            return ParcelWorkflowStageRoute.Unsupported;
        }

        if (computeWorkflowStages.Contains(normalizedStage, StringComparer.OrdinalIgnoreCase))
        {
            return ParcelWorkflowStageRoute.Compute;
        }

        if (compareWorkflowStages.Contains(normalizedStage, StringComparer.OrdinalIgnoreCase))
        {
            return ParcelWorkflowStageRoute.Compare;
        }

        if ((rtExaminationSettings ?? RtExaminationSettings.Default).MatchesStage(normalizedStage))
        {
            return ParcelWorkflowStageRoute.RtExamination;
        }

        return ParcelWorkflowStageRoute.Unsupported;
    }

    public static bool IsComputeStage(
        string? taskName,
        IReadOnlyCollection<string> computeWorkflowStages,
        IReadOnlyCollection<string> compareWorkflowStages) =>
        Resolve(taskName, computeWorkflowStages, compareWorkflowStages) == ParcelWorkflowStageRoute.Compute;
}