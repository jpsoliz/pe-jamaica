using ArcGIS.Desktop.Framework.Contracts;

namespace ParcelWorkflowAddIn;

internal sealed class OpenWorkflowPaneToolbarButton : Button
{
    protected override void OnUpdate()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        Enabled = viewModel?.CanUseWorkflowActions == true;
    }

    protected override void OnClick()
    {
        ParcelWorkflowDockpaneViewModel.Show();
    }
}
