using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace ParcelWorkflowAddIn;

internal sealed class ShowParcelSearchDockpaneButton : Button
{
    protected override void OnClick()
    {
        FrameworkApplication.DockPaneManager.Find(ParcelSearchDockpaneViewModel.DockPaneId)?.Activate();
    }
}
