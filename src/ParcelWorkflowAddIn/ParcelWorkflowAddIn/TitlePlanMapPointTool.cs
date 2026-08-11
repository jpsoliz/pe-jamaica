using System.Windows;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;

namespace ParcelWorkflowAddIn;

internal sealed class TitlePlanMapPointTool : MapTool
{
    internal const string ToolId = "ParcelWorkflow_TitlePlanMapPointTool";
    private const int Jad2001Wkid = 3448;
    private static readonly object SyncRoot = new();
    private static WeakReference<MapGeoreferenceViewModel>? armedViewModel;
    private static MapPointPickTarget armedTarget;

    public TitlePlanMapPointTool()
    {
        IsSketchTool = true;
        SketchType = SketchGeometryType.Point;
        SketchOutputMode = SketchOutputMode.Map;
    }

    internal static void Arm(MapGeoreferenceViewModel viewModel, MapPointPickTarget target)
    {
        lock (SyncRoot)
        {
            armedViewModel = new WeakReference<MapGeoreferenceViewModel>(viewModel);
            armedTarget = target;
        }
    }

    internal static void ClearArmedTarget(MapGeoreferenceViewModel viewModel)
    {
        lock (SyncRoot)
        {
            if (armedViewModel is not null
                && armedViewModel.TryGetTarget(out var target)
                && ReferenceEquals(target, viewModel))
            {
                armedViewModel = null;
            }
        }
    }

    protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
        MapGeoreferenceViewModel? viewModel;
        MapPointPickTarget target;
        lock (SyncRoot)
        {
            viewModel = armedViewModel is not null && armedViewModel.TryGetTarget(out var current)
                ? current
                : null;
            target = armedTarget;
            armedViewModel = null;
        }

        if (viewModel is null)
        {
            return Task.FromResult(true);
        }

        if (geometry is not MapPoint point)
        {
            Dispatch(viewModel, () => viewModel.MarkMapPointPickFailure("ArcGIS Pro did not return a map point for the click."));
            return Task.FromResult(true);
        }

        var mapSpatialReference = MapView.Active?.Map?.SpatialReference;
        var wkid = ResolveWkid(mapSpatialReference ?? point.SpatialReference);
        if (wkid != Jad2001Wkid)
        {
            Dispatch(viewModel, () => viewModel.MarkMapPointPickFailure(
                $"The active map must be JAD2001 / EPSG:{Jad2001Wkid} before capturing title-plan map points."));
            return Task.FromResult(true);
        }

        Dispatch(viewModel, () => viewModel.ApplyCapturedMapPoint(target, point.X, point.Y));
        _ = FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
        return Task.FromResult(true);
    }

    private static int ResolveWkid(SpatialReference? spatialReference)
    {
        if (spatialReference is null)
        {
            return 0;
        }

        return spatialReference.LatestWkid > 0
            ? spatialReference.LatestWkid
            : spatialReference.Wkid;
    }

    private static void Dispatch(MapGeoreferenceViewModel viewModel, Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
