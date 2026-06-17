using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

public partial class ParcelWorkflowDockpane : UserControl
{
    public ParcelWorkflowDockpane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshPdfViewerAsync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ParcelWorkflowDockpaneViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ParcelWorkflowDockpaneViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerBrowserUri)
            or nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerUsesBrowser))
        {
            await RefreshPdfViewerAsync();
        }
    }

    private async Task RefreshPdfViewerAsync()
    {
        if (DockpanePdfWebView is null || DataContext is not ParcelWorkflowDockpaneViewModel viewModel)
        {
            return;
        }

        if (!viewModel.ReviewViewerUsesBrowser || viewModel.ReviewViewerBrowserUri is null)
        {
            if (DockpanePdfWebView.CoreWebView2 is not null)
            {
                DockpanePdfWebView.CoreWebView2.Navigate("about:blank");
            }

            return;
        }

        if (!File.Exists(viewModel.ReviewViewerBrowserUri.LocalPath))
        {
            return;
        }

        DockpanePdfWebView.CreationProperties ??= new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "ParcelWorkflowDockpane")
        };

        await DockpanePdfWebView.EnsureCoreWebView2Async();
        DockpanePdfWebView.CoreWebView2.Navigate(viewModel.ReviewViewerBrowserUri.AbsoluteUri);
    }
}
