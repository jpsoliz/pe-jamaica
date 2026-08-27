using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace ParcelWorkflowAddIn;

internal sealed class ShowParcelSearchDockpaneButton : Button
{
    protected override void OnUpdate()
    {
        Enabled = true;
    }

    protected override void OnClick()
    {
        try
        {
            FrameworkApplication.DockPaneManager.Find(ParcelSearchDockpaneViewModel.DockPaneId)?.Activate();
        }
        catch (Exception exception)
        {
            RibbonCommandErrorReporter.Show("Search", exception);
        }
    }
}
