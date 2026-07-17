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
            "Save the current Compare data and regenerate the report?",
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public bool ConfirmSuspend()
    {
        return MessageBox.Show(
            "Suspend this Compare task? Current progress will be saved, the Compare map layers will be removed, and the workspace will close.",
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public bool ConfirmFinalize(bool reportAlreadyGenerated)
    {
        var message = reportAlreadyGenerated
            ? "Finalize this Compare task and move it to the next stage? The existing report will be regenerated and attached to the transaction."
            : "A Compare report has not been generated yet. Generate the report, finalize this task, and move it to the next stage?";
        return MessageBox.Show(
            message,
            "Compare Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
