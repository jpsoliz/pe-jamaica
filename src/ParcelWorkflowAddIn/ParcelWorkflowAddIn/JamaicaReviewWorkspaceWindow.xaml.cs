using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Framework.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

internal partial class JamaicaReviewWorkspaceWindow : ProWindow
{
    private readonly JamaicaReviewWorkspaceViewModel viewModel;

    internal JamaicaReviewWorkspaceWindow(JamaicaReviewWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshPdfViewerAsync();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(JamaicaReviewWorkspaceViewModel.ViewerBrowserUri)
            or nameof(JamaicaReviewWorkspaceViewModel.ViewerUsesBrowser))
        {
            await RefreshPdfViewerAsync();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Detach();
    }

    private async Task RefreshPdfViewerAsync()
    {
        if (ViewerPdfWebView is null)
        {
            return;
        }

        if (!viewModel.ViewerUsesBrowser || viewModel.ViewerBrowserUri is null)
        {
            if (ViewerPdfWebView.CoreWebView2 is not null)
            {
                ViewerPdfWebView.CoreWebView2.Navigate("about:blank");
            }

            return;
        }

        if (!File.Exists(viewModel.ViewerBrowserUri.LocalPath))
        {
            return;
        }

        ViewerPdfWebView.CreationProperties ??= new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "JamaicaReviewWorkspace")
        };

        await ViewerPdfWebView.EnsureCoreWebView2Async();
        ViewerPdfWebView.CoreWebView2.Navigate(viewModel.ViewerBrowserUri.AbsoluteUri);
    }
}
