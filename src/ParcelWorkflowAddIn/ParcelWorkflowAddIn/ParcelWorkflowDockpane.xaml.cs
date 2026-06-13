using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ParcelWorkflowAddIn;

public partial class ParcelWorkflowDockpane : UserControl
{
    private ParcelWorkflowDockpaneViewModel? viewModel;
    private string? lastBrowserNavigationKey;

    public ParcelWorkflowDockpane()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncEmbeddedPdfViewer();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        viewModel = e.NewValue as ParcelWorkflowDockpaneViewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        lastBrowserNavigationKey = null;
        SyncEmbeddedPdfViewer();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerNavigationKey)
            || e.PropertyName == nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerUsesBrowser))
        {
            SyncEmbeddedPdfViewer();
        }
    }

    private void SyncEmbeddedPdfViewer()
    {
        if (ReviewPdfBrowser is null || viewModel is null)
        {
            return;
        }

        if (!viewModel.ReviewViewerUsesBrowser || viewModel.ReviewViewerBrowserUri is null)
        {
            if (lastBrowserNavigationKey is not null)
            {
                ReviewPdfBrowser.Navigate("about:blank");
                lastBrowserNavigationKey = null;
            }

            return;
        }

        if (string.Equals(lastBrowserNavigationKey, viewModel.ReviewViewerNavigationKey, StringComparison.Ordinal))
        {
            return;
        }

        lastBrowserNavigationKey = viewModel.ReviewViewerNavigationKey;
        ReviewPdfBrowser.Navigate(viewModel.ReviewViewerBrowserUri);
    }
}
