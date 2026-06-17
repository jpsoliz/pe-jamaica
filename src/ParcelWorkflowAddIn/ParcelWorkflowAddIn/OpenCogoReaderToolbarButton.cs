using ArcGIS.Desktop.Framework.Contracts;

namespace ParcelWorkflowAddIn;

internal sealed class OpenCogoReaderToolbarButton : Button
{
    protected override void OnUpdate()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        Enabled = viewModel?.CanOpenCogoReader == true
            && viewModel.OpenCogoReaderCommand.CanExecute(null);
    }

    protected override void OnClick()
    {
        var viewModel = MapReviewToolbarContext.TryGetWorkflowPane();
        if (viewModel is null)
        {
            return;
        }

        if (!viewModel.OpenCogoReaderCommand.CanExecute(null))
        {
            return;
        }

        viewModel.OpenCogoReaderCommand.Execute(null);
    }
}
