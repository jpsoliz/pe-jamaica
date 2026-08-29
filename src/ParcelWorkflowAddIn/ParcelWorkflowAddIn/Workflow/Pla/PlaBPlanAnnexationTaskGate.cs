using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn.Workflow.Pla;

public static class PlaBPlanAnnexationTaskGate
{
    public static PlaBPlanAnnexationTaskGateResult Evaluate(
        InnolaTransactionRow? row,
        PlaBPlanAnnexationTaskSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return PlaBPlanAnnexationTaskGateResult.Blocked("Plan Annexation Task is disabled in settings.");
        }

        if (row is null)
        {
            return PlaBPlanAnnexationTaskGateResult.Blocked("Select an eligible First Registration transaction.");
        }

        if (!row.IsLoadable)
        {
            return PlaBPlanAnnexationTaskGateResult.Blocked(row.UnavailableReason ?? "Selected transaction is not loadable.");
        }

        if (!Matches(row.TransactionType, settings.MainTransactionType))
        {
            return PlaBPlanAnnexationTaskGateResult.Blocked(
                $"Plan Annexation Task requires transaction type {settings.MainTransactionType}.");
        }

        if (!Matches(row.TaskName, settings.PreparationStageName))
        {
            return PlaBPlanAnnexationTaskGateResult.Blocked(
                $"Plan Annexation Task requires stage {settings.PreparationStageName}.");
        }

        var hasWorkflowMetadata = HasAnyWorkflowMetadata(row);
        if (hasWorkflowMetadata
            && !Matches(row.SubworkflowName, settings.SubworkflowName)
            && !ContainsName(row.WorkflowNames, settings.SubworkflowName))
        {
            return PlaBPlanAnnexationTaskGateResult.Blocked(
                $"Plan Annexation Task requires subworkflow {settings.SubworkflowName}.");
        }

        foreach (var required in settings.RequiredWorkflowNames.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (Matches(required, settings.SubworkflowName) && !hasWorkflowMetadata)
            {
                continue;
            }

            if (!ContainsName(row.WorkflowNames, required)
                && !Matches(row.WorkflowName, required)
                && !Matches(row.SubworkflowName, required)
                && !Matches(row.TransactionType, required))
            {
                return PlaBPlanAnnexationTaskGateResult.Blocked(
                    $"Plan Annexation Task requires workflow {required}.");
            }
        }

        return PlaBPlanAnnexationTaskGateResult.Allowed();
    }

    private static bool HasAnyWorkflowMetadata(InnolaTransactionRow row)
    {
        return !string.IsNullOrWhiteSpace(row.WorkflowName)
            || !string.IsNullOrWhiteSpace(row.SubworkflowName)
            || row.WorkflowNames?.Any(value => !string.IsNullOrWhiteSpace(value)) == true;
    }

    private static bool ContainsName(IReadOnlyList<string>? values, string expected)
    {
        return values?.Any(value => Matches(value, expected)) == true;
    }

    private static bool Matches(string? actual, string expected)
    {
        return !string.IsNullOrWhiteSpace(actual)
            && actual.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record PlaBPlanAnnexationTaskGateResult(bool IsEligible, string? Reason)
{
    public static PlaBPlanAnnexationTaskGateResult Allowed() => new(true, null);

    public static PlaBPlanAnnexationTaskGateResult Blocked(string reason) => new(false, reason);
}
