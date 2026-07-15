namespace ParcelWorkflowAddIn.Innola;

internal enum ParcelWorkflowStageRoute
{
    Unsupported,
    Compute,
    Compare
}

internal static class ParcelWorkflowStageRouter
{
    public static ParcelWorkflowStageRoute Resolve(
        string? taskName,
        IReadOnlyCollection<string> computeWorkflowStages,
        IReadOnlyCollection<string> compareWorkflowStages)
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

        return ParcelWorkflowStageRoute.Unsupported;
    }

    public static bool IsComputeStage(
        string? taskName,
        IReadOnlyCollection<string> computeWorkflowStages,
        IReadOnlyCollection<string> compareWorkflowStages) =>
        Resolve(taskName, computeWorkflowStages, compareWorkflowStages) == ParcelWorkflowStageRoute.Compute;
}
