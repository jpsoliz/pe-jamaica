using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowTransactionPanelButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = ShellState.Session.CanOpenTransactionPanel;
    }

    protected override void OnClick()
    {
        if (!ShellState.Session.CanOpenTransactionPanel)
        {
            return;
        }

        FrameworkApplication.DockPaneManager.Find(TransactionPanelDockpaneViewModel.DockPaneId)?.Activate();
    }
}
