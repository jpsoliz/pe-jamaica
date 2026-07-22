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
            source.Contains("await CleanupActiveTransactionReviewMapAsync(completedTransactionNumber).ConfigureAwait(true);", StringComparison.Ordinal),
            "Finalize should remove the active transaction review map group before returning to the transaction list.");
        TestAssert.True(
            source.Contains("RemoveTransactionOutputsFromActiveMapAsync(transactionNumber)", StringComparison.Ordinal),
            "PE exit cleanup should remove the TR transaction review group from ArcGIS Pro Contents.");
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
