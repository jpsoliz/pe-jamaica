using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class TransactionPanelDockpaneViewModel : DockPane
{
    internal const string DockPaneId = "ParcelWorkflow_TransactionPanelDockpane";

    public TransactionPanelDockpaneViewModel()
    {
        State = new TransactionPanelState(
            ShellState.Session,
            ShellState.Transactions,
            ShellState.TransactionProcessStep,
            ShellState.TransactionLoader);
    }

    public TransactionPanelState State { get; }
}
