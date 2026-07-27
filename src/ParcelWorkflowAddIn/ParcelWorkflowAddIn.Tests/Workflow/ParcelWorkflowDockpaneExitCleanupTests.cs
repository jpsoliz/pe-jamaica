namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ParcelWorkflowDockpaneExitCleanupTests
{
    public static void SuccessfulProcessExitsCleanupTransactionReviewMapGroup()
    {
        var source = File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(
            source.Contains("await CleanupActiveTransactionReviewMapAsync(suspendedTransactionNumber).ConfigureAwait(true);", StringComparison.Ordinal),
            "Suspend should remove the active transaction review map group before returning to the transaction list.");
        TestAssert.True(
            source.Contains("await CleanupActiveTransactionReviewMapAsync(cancelledTransactionNumber).ConfigureAwait(true);", StringComparison.Ordinal),
            "Cancel should remove the active transaction review map group before returning to the transaction list.");
        TestAssert.True(
            source.Contains("CompleteTransactionSuccessUiAsync(", StringComparison.Ordinal)
            && source.Contains("await CleanupActiveTransactionReviewMapAsync(transactionNumber).ConfigureAwait(true);", StringComparison.Ordinal),
            "Finalize should remove the active transaction review map group through the success UI path before returning to the transaction list.");
        TestAssert.True(
            source.Contains("RemoveTransactionOutputsFromActiveMapAsync(transactionNumber)", StringComparison.Ordinal),
            "PE exit cleanup should remove the TR transaction review group from ArcGIS Pro Contents.");
    }

    public static void FinalizeSuccessShowsCompletionDialogAndRecoversCompletedManifest()
    {
        var source = File.ReadAllText(FindSourceFile("ParcelWorkflowDockpaneViewModel.cs"));

        TestAssert.True(
            source.Contains("\"Finalize Complete\"", StringComparison.Ordinal)
            && source.Contains("was completed successfully", StringComparison.Ordinal)
            && source.Contains("MessageBoxImage.Information", StringComparison.Ordinal),
            "Finalize success should show a clear completion dialog.");
        TestAssert.True(
            source.Contains("TryResolveCompletedTransactionStatus(completedCaseFolderPath", StringComparison.Ordinal)
            && source.Contains("manifest.Payload.InnolaLifecycle?.Status", StringComparison.Ordinal)
            && source.Contains("\"completed\"", StringComparison.Ordinal),
            "Finalize failure handling should recover when the local manifest already proves the transaction completed.");
    }

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ParcelWorkflowAddIn",
                "ParcelWorkflowAddIn",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from the test output directory.");
    }
}
