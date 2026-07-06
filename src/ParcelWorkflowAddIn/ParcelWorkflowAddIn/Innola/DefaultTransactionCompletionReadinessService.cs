using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;

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

            try
            {
                var layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
                var disposition = JsonSerializer.Deserialize<ComputeReviewDispositionDocument>(File.ReadAllText(dispositionPath), JsonOptions);
                if (disposition is null || !string.Equals(disposition.Decision, ComputeReviewDecision.Approved.ToContractValue(), StringComparison.OrdinalIgnoreCase))
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "compute_disposition_not_approved",
                        "Complete is blocked until the Compute review disposition is approved.");
                }

                var outputSummaryPath = ResolveCasePath(layout, disposition.OutputSummaryRef);
                if (string.IsNullOrWhiteSpace(outputSummaryPath) || !File.Exists(outputSummaryPath))
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "output_summary_missing",
                        "Complete is blocked because the Compute disposition references a missing output summary.");
                }

                var outputSummary = JsonSerializer.Deserialize<OutputSummaryDocument>(File.ReadAllText(outputSummaryPath), JsonOptions);
                if (outputSummary is null || !string.Equals(outputSummary.TransactionId, disposition.TransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "output_summary_mismatch",
                        "Complete is blocked because the output summary does not match the Compute disposition.");
                }

                if (!string.Equals(outputSummary.RunId, disposition.PublishRunId, StringComparison.OrdinalIgnoreCase))
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "enterprise_publish_stale",
                        "Complete is blocked because the Enterprise publish evidence does not match the latest output run.");
                }

                var enterprisePublishPath = ResolveCasePath(layout, disposition.EnterprisePublishRef) ?? publishPath;
                if (!File.Exists(enterprisePublishPath))
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "enterprise_publish_missing",
                        "Complete is blocked because the Compute disposition references missing Enterprise publish evidence.");
                }

                var publish = JsonSerializer.Deserialize<EnterpriseWorkingPublishSummary>(File.ReadAllText(enterprisePublishPath), JsonOptions);
                if (publish is null
                    || !string.Equals(publish.TransactionId, disposition.TransactionId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(publish.TransactionNumber, disposition.TransactionNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "enterprise_publish_mismatch",
                        "Complete is blocked because the Enterprise publish evidence does not match the transaction.");
                }

                if (string.Equals(publish.Status, "failed", StringComparison.OrdinalIgnoreCase)
                    || publish.Errors.Count > 0
                    || publish.PublishedLayers.Count == 0)
                {
                    return TransactionCompletionReadinessResult.Blocked(
                        "enterprise_publish_not_current",
                        "Complete is blocked until Enterprise working publish evidence is successful and current.");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
            {
                return TransactionCompletionReadinessResult.Blocked(
                    "completion_readiness_unreadable",
                    "Complete is blocked because closeout readiness evidence could not be read.");
            }

            return TransactionCompletionReadinessResult.Ready();
        }

        return TransactionCompletionReadinessResult.Blocked(
            "sync_readiness_not_met",
            "Complete is blocked until downstream sync/readiness criteria are met.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string? ResolveCasePath(CaseFolderLayout layout, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(layout.RootDirectory, path.Replace('/', Path.DirectorySeparatorChar));
    }
}
