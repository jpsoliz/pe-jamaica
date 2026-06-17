using ArcGIS.Desktop.Framework;

namespace ParcelWorkflowAddIn;

internal static class MapReviewToolbarContext
{
    public static ParcelWorkflowDockpaneViewModel? TryGetWorkflowPane()
    {
        return FrameworkApplication.DockPaneManager.Find(ParcelWorkflowDockpaneViewModel.DockPaneId) as ParcelWorkflowDockpaneViewModel;
    }
}
