using System.IO;

namespace ParcelWorkflowAddIn.Innola;

public sealed class DefaultTransactionCompletionReadinessService : ITransactionCompletionReadinessService
{
    public TransactionCompletionReadinessResult CheckReadiness(string caseFolderPath)
    {
        var settings = InnolaTransactionSettings.Load();
        if (string.Equals(settings.ReviewWorkspaceMode, InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers, StringComparison.OrdinalIgnoreCase))
        {
            var dispositionPath = Path.Combine(caseFolderPath, "working", "compute_review_disposition.json");
            if (!File.Exists(dispositionPath))
            {
                return TransactionCompletionReadinessResult.Blocked(
                    "compute_disposition_missing",
                    "Complete is blocked until the Compute review disposition is recorded.");
            }

            var publishPath = Path.Combine(caseFolderPath, "output", "enterprise_working_publish.json");
            if (!File.Exists(publishPath))
            {
                return TransactionCompletionReadinessResult.Blocked(
                    "enterprise_publish_missing",
                    "Complete is blocked until Enterprise working publish evidence is available.");
            }

            return TransactionCompletionReadinessResult.Ready();
        }

        return TransactionCompletionReadinessResult.Blocked(
            "sync_readiness_not_met",
            "Complete is blocked until downstream sync/readiness criteria are met.");
    }
}
