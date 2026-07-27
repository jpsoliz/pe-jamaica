using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

internal partial class SupportingDocumentsWindow : ProWindow
{
    private static SupportingDocumentsWindow? activeWindow;
    private readonly SupportingDocumentsDockpaneViewModel viewModel;
    private WebView2? supportingDocumentPdfWebView;
    private string? lastSupportingDocumentNavigationKey;

    internal SupportingDocumentsWindow(SupportingDocumentsDockpaneViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Owner = FrameworkApplication.Current?.MainWindow;
        Loaded += OnLoaded;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    internal static void ShowOrActivate(SupportingDocumentsDockpaneViewModel viewModel)
    {
        if (activeWindow is { IsVisible: true })
        {
            activeWindow.viewModel.ReloadActiveCaseFolder();
            activeWindow.Activate();
            return;
        }

        activeWindow = new SupportingDocumentsWindow(viewModel);
        activeWindow.Show();
    }

    internal static SupportingDocumentsDockpaneViewModel? ActiveViewModel =>
        activeWindow is { IsVisible: true } ? activeWindow.viewModel : null;

    internal static void RefreshIfOpen()
    {
        if (activeWindow is { IsVisible: true })
        {
            activeWindow.viewModel.ReloadActiveCaseFolder();
        }
    }

    internal static void CloseIfOpen()
    {
        if (activeWindow is { IsVisible: true } window)
        {
            window.Close();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.ReloadActiveCaseFolder();
        await SafeRefreshSupportingDocumentPdfViewerAsync();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerNavigationKey)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerBrowserUri)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerUsesBrowser))
        {
            await SafeRefreshSupportingDocumentPdfViewerAsync();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (ReferenceEquals(activeWindow, this))
        {
            activeWindow = null;
        }
    }

    private async Task SafeRefreshSupportingDocumentPdfViewerAsync()
    {
        try
        {
            await RefreshSupportingDocumentPdfViewerAsync();
        }
        catch (Exception exception)
        {
            SupportingDocumentsDiagnostics.Write($"Window render failure: {exception.GetType().Name}: {exception.Message}");
            viewModel.MarkSupportingDocumentRenderFailure(exception.Message);
        }
    }

    private async Task RefreshSupportingDocumentPdfViewerAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(RefreshSupportingDocumentPdfViewerAsync).Task.Unwrap();
            return;
        }

        if (!viewModel.SupportingDocumentViewerUsesBrowser || viewModel.SupportingDocumentViewerBrowserUri is null)
        {
            lastSupportingDocumentNavigationKey = null;
            if (supportingDocumentPdfWebView?.CoreWebView2 is not null)
            {
                supportingDocumentPdfWebView.CoreWebView2.Navigate("about:blank");
            }

            return;
        }

        if (!File.Exists(viewModel.SupportingDocumentViewerBrowserUri.LocalPath))
        {
            viewModel.MarkSupportingDocumentRenderFailure(
                $"Selected document is no longer available: {viewModel.SupportingDocumentViewerBrowserUri.LocalPath}");
            return;
        }

        var navigationKey = viewModel.SupportingDocumentViewerNavigationKey;
        if (!string.IsNullOrWhiteSpace(navigationKey)
            && string.Equals(lastSupportingDocumentNavigationKey, navigationKey, StringComparison.Ordinal)
            && supportingDocumentPdfWebView?.CoreWebView2 is not null)
        {
            return;
        }

        viewModel.MarkSupportingDocumentRenderAttempt(
            $"Loading {viewModel.SelectedSupportingDocument?.FileLabel ?? "selected document"} from {viewModel.SupportingDocumentViewerBrowserUri.LocalPath}");
        var webView = await EnsureSupportingDocumentPdfWebViewAsync();
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.Navigate(viewModel.SupportingDocumentViewerBrowserUri.AbsoluteUri);
            lastSupportingDocumentNavigationKey = navigationKey;
            viewModel.MarkSupportingDocumentRenderReady("Embedded PDF loaded.");
        }
    }

    private async Task<WebView2> EnsureSupportingDocumentPdfWebViewAsync()
    {
        if (supportingDocumentPdfWebView is null)
        {
            supportingDocumentPdfWebView = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "SupportingDocumentsWindow")
                }
            };
            SupportingDocumentPdfViewerHost.Children.Add(supportingDocumentPdfWebView);
        }

        await supportingDocumentPdfWebView.EnsureCoreWebView2Async();
        return supportingDocumentPdfWebView;
    }
}
