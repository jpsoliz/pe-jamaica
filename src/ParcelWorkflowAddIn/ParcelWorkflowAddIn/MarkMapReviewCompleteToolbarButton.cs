using ArcGIS.Desktop.Framework.Contracts;

namespace ParcelWorkflowAddIn;

internal sealed class MarkMapReviewCompleteToolbarButton : Button
{
    protected override void OnUpdate()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        Enabled = viewModel?.IsSpatialReviewStageActive == true
            && viewModel.ApproveSpatialReviewCommand.CanExecute(null);
    }

    protected override void OnClick()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        if (viewModel is null)
        {
            return;
        }

        if (!viewModel.ApproveSpatialReviewCommand.CanExecute(null))
        {
            return;
        }

        viewModel.ApproveSpatialReviewCommand.Execute(null);
    }
}
