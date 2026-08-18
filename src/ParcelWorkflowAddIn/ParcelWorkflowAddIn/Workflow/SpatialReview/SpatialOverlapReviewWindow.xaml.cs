using System.Windows;
using ArcGIS.Desktop.Framework.Controls;

namespace ParcelWorkflowAddIn.Workflow.SpatialReview;

internal partial class SpatialOverlapReviewWindow : ProWindow
{
    private static SpatialOverlapReviewWindow? activeWindow;
    private readonly SpatialOverlapReviewViewModel viewModel;

    internal SpatialOverlapReviewWindow(SpatialOverlapReviewViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Closed += OnClosed;
    }

    internal static void ShowOrActivate(SpatialOverlapReviewDocument document, string contextLabel)
    {
        if (activeWindow is { IsVisible: true })
        {
            activeWindow.viewModel.Load(document, contextLabel);
            if (activeWindow.WindowState == WindowState.Minimized)
            {
                activeWindow.WindowState = WindowState.Normal;
            }

            activeWindow.Activate();
            return;
        }

        var window = new SpatialOverlapReviewWindow(new SpatialOverlapReviewViewModel())
        {
            Owner = Application.Current?.MainWindow
        };
        window.viewModel.Load(document, contextLabel);
        activeWindow = window;
        try
        {
            window.Show();
        }
        catch
        {
            activeWindow = null;
            throw;
        }
    }

    internal static void RefreshIfOpen(SpatialOverlapReviewDocument document, string contextLabel)
    {
        if (activeWindow is { IsVisible: true })
        {
            activeWindow.viewModel.Load(document, contextLabel);
        }
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (ReferenceEquals(activeWindow, this))
        {
            activeWindow = null;
        }

        Closed -= OnClosed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
