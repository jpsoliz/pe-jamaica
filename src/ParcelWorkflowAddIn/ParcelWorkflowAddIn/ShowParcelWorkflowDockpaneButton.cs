using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowParcelWorkflowDockpaneButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = ShellState.Session.CanOpenParcelWorkflow;
    }

    protected override void OnClick()
    {
        if (!ShellState.Session.CanOpenParcelWorkflow)
        {
            return;
        }

        FrameworkApplication.DockPaneManager.Find(ParcelWorkflowDockpaneViewModel.DockPaneId)?.Activate();
    }
}
