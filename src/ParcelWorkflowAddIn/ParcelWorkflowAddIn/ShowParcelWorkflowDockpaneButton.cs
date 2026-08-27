using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;

namespace ParcelWorkflowAddIn;

internal sealed class ShowParcelWorkflowDockpaneButton : Button
{
    protected override void OnUpdate()
    {
        try
        {
            Enabled = ShellState.CanOpenComputeWorkflow;
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
            if (!ShellState.CanOpenComputeWorkflow)
            {
                return;
            }

            FrameworkApplication.DockPaneManager.Find(ParcelWorkflowDockpaneViewModel.DockPaneId)?.Activate();
        }
        catch (Exception exception)
        {
            RibbonCommandErrorReporter.Show("Parcel Workflow", exception);
        }
    }
}
