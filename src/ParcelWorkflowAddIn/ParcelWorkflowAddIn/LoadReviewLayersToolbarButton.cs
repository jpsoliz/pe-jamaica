using ArcGIS.Desktop.Framework.Contracts;
using System.Windows.Input;

namespace ParcelWorkflowAddIn;

internal sealed class LoadReviewLayersToolbarButton : Button
{
    protected override void OnUpdate()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        Enabled = viewModel?.IsSpatialReviewStageActive == true
            && viewModel.LoadSpatialReviewLayersCommand.CanExecute(null);
    }

    protected override void OnClick()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        if (viewModel is null)
        {
            return;
        }

        if (!viewModel.LoadSpatialReviewLayersCommand.CanExecute(null))
        {
            return;
        }

        viewModel.LoadSpatialReviewLayersCommand.Execute(null);
    }
}
