using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.Innola;
using System.Diagnostics;

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
        try
        {
            SupportingDocumentsDockpaneViewModel.Show();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Supporting Documents dockpane activation failed from ribbon: {exception.Message}");
        }
    }
}
