using System;
using System.ComponentModel;
using System.Windows;
using ArcGIS.Desktop.Framework.Controls;

namespace ParcelWorkflowAddIn;

internal partial class JamaicaReviewWorkspaceWindow : ProWindow
{
    private readonly JamaicaReviewWorkspaceViewModel viewModel;
    private string? lastBrowserNavigationKey;

    internal JamaicaReviewWorkspaceWindow(JamaicaReviewWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => SyncEmbeddedPdfViewer();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JamaicaReviewWorkspaceViewModel.ViewerNavigationKey)
            || e.PropertyName == nameof(JamaicaReviewWorkspaceViewModel.ViewerUsesBrowser))
        {
            SyncEmbeddedPdfViewer();
        }
    }

    private void SyncEmbeddedPdfViewer()
    {
        if (ReviewPdfBrowser is null)
        {
            return;
        }

        if (!viewModel.ViewerUsesBrowser || viewModel.ViewerBrowserUri is null)
        {
            if (lastBrowserNavigationKey is not null)
            {
                ReviewPdfBrowser.Navigate("about:blank");
                lastBrowserNavigationKey = null;
            }

            return;
        }

        if (string.Equals(lastBrowserNavigationKey, viewModel.ViewerNavigationKey, StringComparison.Ordinal))
        {
            return;
        }

        lastBrowserNavigationKey = viewModel.ViewerNavigationKey;
        ReviewPdfBrowser.Navigate(viewModel.ViewerBrowserUri);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Detach();
    }
}
