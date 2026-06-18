using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

public class ParcelWorkflowDockpane : UserControl
{
    private string? lastViewerNavigationKey;
    private readonly WebView2? dockpanePdfWebView;

    public ParcelWorkflowDockpane()
    {
        var loadedControl = (UserControl)Application.LoadComponent(
            new Uri("/ParcelWorkflowAddIn;component/ParcelWorkflowDockpane.xaml", UriKind.Relative));

        Content = loadedControl.Content;
        Resources = loadedControl.Resources;
        Background = loadedControl.Background;
        FontFamily = loadedControl.FontFamily;
        dockpanePdfWebView = loadedControl.FindName("DockpanePdfWebView") as WebView2;

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
        if (e.PropertyName is nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerNavigationKey)
            or nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerBrowserUri)
            or nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerUsesBrowser))
        {
            await RefreshPdfViewerAsync();
        }
    }

    private async Task RefreshPdfViewerAsync()
    {
        if (dockpanePdfWebView is null || DataContext is not ParcelWorkflowDockpaneViewModel viewModel)
        {
            return;
        }

        if (!viewModel.ReviewViewerUsesBrowser || viewModel.ReviewViewerBrowserUri is null)
        {
            lastViewerNavigationKey = null;
            if (dockpanePdfWebView.CoreWebView2 is not null)
            {
                dockpanePdfWebView.CoreWebView2.Navigate("about:blank");
            }

            return;
        }

        if (!File.Exists(viewModel.ReviewViewerBrowserUri.LocalPath))
        {
            return;
        }

        var navigationKey = viewModel.ReviewViewerNavigationKey;
        if (!string.IsNullOrWhiteSpace(navigationKey)
            && string.Equals(lastViewerNavigationKey, navigationKey, StringComparison.Ordinal)
            && dockpanePdfWebView.CoreWebView2 is not null)
        {
            return;
        }

        dockpanePdfWebView.CreationProperties ??= new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "ParcelWorkflowDockpane")
        };

        await dockpanePdfWebView.EnsureCoreWebView2Async();
        if (dockpanePdfWebView.CoreWebView2 is not null)
        {
            dockpanePdfWebView.CoreWebView2.Navigate(viewModel.ReviewViewerBrowserUri.AbsoluteUri);
            lastViewerNavigationKey = navigationKey;
        }
    }
}
