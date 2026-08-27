using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowTransactionPanelButton : Button
{
    protected override void OnUpdate()
    {
        try
        {
            Enabled = ShellState.Session.CanOpenTransactionPanel;
        }
        catch
        {
            Enabled = false;
        }
    }

    protected override void OnClick()
    {
        try
        {
            if (!ShellState.Session.CanOpenTransactionPanel)
            {
                return;
            }

            FrameworkApplication.DockPaneManager.Find(TransactionPanelDockpaneViewModel.DockPaneId)?.Activate();
        }
        catch (Exception exception)
        {
            RibbonCommandErrorReporter.Show("Transaction Panel", exception);
        }
    }
}
