using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

internal partial class MapGeoreferenceWindow : ProWindow
{
    private static MapGeoreferenceWindow? activeWindow;
    private readonly MapGeoreferenceViewModel viewModel;
    private WebView2? pdfWebView;
    private string? lastNavigationKey;

    internal MapGeoreferenceWindow(MapGeoreferenceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Owner = FrameworkApplication.Current?.MainWindow;
        Loaded += OnLoaded;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.Documents.PropertyChanged += OnDocumentsPropertyChanged;
    }

    internal static void ShowOrActivate(string transactionNumber)
    {
        if (activeWindow is { IsVisible: true })
        {
            activeWindow.viewModel.Reload();
            activeWindow.Activate();
            return;
        }

        var documents = new SupportingDocumentsDockpaneViewModel();
        var viewModel = new MapGeoreferenceViewModel(transactionNumber, documents);
        var window = new MapGeoreferenceWindow(viewModel);
        activeWindow = window;
        try
        {
            window.Show();
        }
        catch
        {
            activeWindow = null;
            viewModel.Dispose();
            throw;
        }
    }

    internal static void CloseIfOpen()
    {
        if (activeWindow is { IsVisible: true } window)
        {
            _ = new MapGeoreferenceOverlayService().RemoveOverlayAsync(window.viewModel.TransactionNumber);
            window.Close();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.Reload();
        await SafeRefreshPdfViewerAsync();
    }

    private async void OnDocumentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerNavigationKey)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerBrowserUri)
            or nameof(SupportingDocumentsDockpaneViewModel.SupportingDocumentViewerUsesBrowser))
        {
            await SafeRefreshPdfViewerAsync();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Documents.PropertyChanged -= OnDocumentsPropertyChanged;
        viewModel.Dispose();
        if (pdfWebView is not null)
        {
            MapGeoreferencePdfViewerHost.Children.Remove(pdfWebView);
            pdfWebView.Dispose();
            pdfWebView = null;
        }

        lastNavigationKey = null;
        if (ReferenceEquals(activeWindow, this))
        {
            activeWindow = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapGeoreferenceViewModel.OverlayPickerImageAvailable))
        {
            UpdatePdfWebViewVisibilityForPicker();
        }
    }

    private async Task SafeRefreshPdfViewerAsync()
    {
        try
        {
            await RefreshPdfViewerAsync();
        }
        catch (Exception exception)
        {
            SupportingDocumentsDiagnostics.Write($"M-Geo render failure: {exception.GetType().Name}: {exception.Message}");
            viewModel.MarkRenderFailure(exception.Message);
        }
    }

    private async Task RefreshPdfViewerAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(RefreshPdfViewerAsync).Task.Unwrap();
            return;
        }

        var browserUri = viewModel.Documents.SupportingDocumentViewerBrowserUri;
        var navigationKey = viewModel.Documents.SupportingDocumentViewerNavigationKey;
        if (!viewModel.Documents.SupportingDocumentViewerUsesBrowser || browserUri is null)
        {
            lastNavigationKey = null;
            if (pdfWebView?.CoreWebView2 is not null)
            {
                pdfWebView.CoreWebView2.Navigate("about:blank");
            }

            return;
        }

        if (!File.Exists(browserUri.LocalPath))
        {
            viewModel.MarkRenderFailure(
                $"Selected document is no longer available: {browserUri.LocalPath}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(navigationKey)
            && string.Equals(lastNavigationKey, navigationKey, StringComparison.Ordinal)
            && pdfWebView?.CoreWebView2 is not null)
        {
            return;
        }

        viewModel.MarkRenderAttempt(
            $"Loading {viewModel.Documents.SelectedSupportingDocument?.FileLabel ?? "selected document"} from {browserUri.LocalPath}");
        var webView = await EnsurePdfWebViewAsync();
        if (!string.Equals(navigationKey, viewModel.Documents.SupportingDocumentViewerNavigationKey, StringComparison.Ordinal)
            || !Equals(browserUri, viewModel.Documents.SupportingDocumentViewerBrowserUri))
        {
            return;
        }

        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.Navigate(browserUri.AbsoluteUri);
            lastNavigationKey = navigationKey;
            viewModel.MarkRenderReady("Embedded PDF loaded.");
        }
    }

    private async Task<WebView2> EnsurePdfWebViewAsync()
    {
        if (pdfWebView is null)
        {
            pdfWebView = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "MapGeoreferenceWindow")
                }
            };
            MapGeoreferencePdfViewerHost.Children.Add(pdfWebView);
        }

        UpdatePdfWebViewVisibilityForPicker();
        await pdfWebView.EnsureCoreWebView2Async();
        return pdfWebView;
    }

    private async void OnCapturePdfViewClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (pdfWebView?.CoreWebView2 is null)
            {
                viewModel.MarkRenderFailure("The embedded PDF viewer is not ready for capture yet.");
                return;
            }

            await using var stream = new MemoryStream();
            await pdfWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            stream.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            viewModel.SetCapturedPdfImage(bitmap);
            UpdatePdfWebViewVisibilityForPicker();
        }
        catch (Exception exception)
        {
            SupportingDocumentsDiagnostics.Write($"M-Geo PDF capture failure: {exception.GetType().Name}: {exception.Message}");
            viewModel.MarkRenderFailure($"Could not capture the PDF view for point picking: {exception.Message}");
        }
    }

    private void OnDocumentPickerImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (MapGeoreferencePickerImage.Source is not BitmapSource source)
        {
            return;
        }

        var position = e.GetPosition(MapGeoreferencePickerImage);
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }

        var x = position.X * source.PixelWidth / source.Width;
        var y = position.Y * source.PixelHeight / source.Height;
        if (x < 0 || y < 0 || x > source.PixelWidth || y > source.PixelHeight)
        {
            return;
        }

        if (viewModel.TryApplyDocumentImagePick(x, y))
        {
            e.Handled = true;
        }
    }

    private void UpdatePdfWebViewVisibilityForPicker()
    {
        if (pdfWebView is null)
        {
            return;
        }

        // WebView2 is hosted in its own HWND and can sit above WPF controls even
        // when a WPF image is later in the visual tree. Hide it while the captured
        // image picker is active so point clicks are handled by the WPF Image.
        pdfWebView.Visibility = viewModel.OverlayPickerImageAvailable
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
