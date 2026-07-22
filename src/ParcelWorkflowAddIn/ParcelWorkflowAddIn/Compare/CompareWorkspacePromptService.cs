using System.Windows;

namespace ParcelWorkflowAddIn.Compare;

public interface ICompareWorkspacePromptService
{
    bool ConfirmSave();

    bool ConfirmSuspend();

    bool ConfirmFinalize(bool reportAlreadyGenerated);
}

public sealed class AutoApproveCompareWorkspacePromptService : ICompareWorkspacePromptService
{
    public bool ConfirmSave()
    {
        return true;
    }

    public bool ConfirmSuspend()
    {
        return true;
    }

    public bool ConfirmFinalize(bool reportAlreadyGenerated)
    {
        return true;
    }
}

public sealed class MessageBoxCompareWorkspacePromptService : ICompareWorkspacePromptService
{
    public bool ConfirmSave()
    {
        return MessageBox.Show(
            "Save the current Compare status and regenerate the PDF report?",
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public bool ConfirmSuspend()
    {
        return MessageBox.Show(
            "Suspend this Compare task? Current status will be saved and uploaded to the transaction, then the form and Compare map content will be cleared.",
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public bool ConfirmFinalize(bool reportAlreadyGenerated)
    {
        var message = reportAlreadyGenerated
            ? "Finalize this Compare task? The current status will be saved, the PDF report will be regenerated and uploaded to the transaction, and the form and Compare map content will be cleared."
            : "Finalize this Compare task? A PDF report will be generated, uploaded to the transaction, and the form and Compare map content will be cleared.";
        return MessageBox.Show(
            message,
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
