using System.Windows;

namespace ParcelWorkflowAddIn.Compare;

public interface ICompareWorkspacePromptService
{
    bool ConfirmCancel();

    bool ConfirmSave();

    void ShowSaveCompleted(string message);

    void ShowFinalizeCompleted(string message);

    bool ConfirmSuspend();

    bool ConfirmFinalize(bool reportAlreadyGenerated);
}

public sealed class AutoApproveCompareWorkspacePromptService : ICompareWorkspacePromptService
{
    public bool ConfirmCancel()
    {
        return true;
    }

    public bool ConfirmSave()
    {
        return true;
    }

    public void ShowSaveCompleted(string message)
    {
    }

    public void ShowFinalizeCompleted(string message)
    {
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
    public bool ConfirmCancel()
    {
        return MessageBox.Show(
            "Cancel this Compare workspace? No changes will be saved, and the form and Compare map content will be cleared.",
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public bool ConfirmSave()
    {
        return MessageBox.Show(
            "Save the current Compare status and regenerate the PDF report?",
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowSaveCompleted(string message)
    {
        MessageBox.Show(
            message,
            "Compare Workspace",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public void ShowFinalizeCompleted(string message)
    {
        MessageBox.Show(
            message,
            "Compare Workspace",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
