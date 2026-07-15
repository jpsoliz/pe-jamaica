using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowParcelWorkflowDockpaneButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = ShellState.CanOpenComputeWorkflow;
    }

    protected override void OnClick()
    {
        if (!ShellState.CanOpenComputeWorkflow)
        {
            return;
        }

        FrameworkApplication.DockPaneManager.Find(ParcelWorkflowDockpaneViewModel.DockPaneId)?.Activate();
    }
}
