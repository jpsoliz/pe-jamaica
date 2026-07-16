using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

public partial class CompareWorkspaceWindow : ProWindow
{
    private static readonly GridLength VisiblePdfPanelWidth = new(390);
    private static readonly GridLength VisiblePdfPanelSpacerWidth = new(8);
    private static readonly GridLength CollapsedPanelWidth = new(0);
    private const double ExpandedCompareWindowMinWidth = 880;
    private const double CompactCompareWindowMinWidth = 620;
    private const double CompactCompareWindowWidth = 640;
    private readonly Compare.CompareWorkspaceViewModel viewModel;
    private WebView2? documentWebView;
    private string? lastNavigationKey;
    private double expandedWidthBeforePdfCollapse = 1040;
    private bool wasPdfPanelVisible = true;
    private bool webViewUnavailable;

    public CompareWorkspaceWindow(Compare.CompareWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyPdfPanelLayout();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.LoadAsync();
            await RefreshViewerAsync();
        }
        catch (Exception exception)
        {
            viewModel.ReportWorkspaceError($"Compare workspace could not finish loading. {exception.Message}");
            ShowFallback("Compare workspace could not finish loading. Close and reopen the transaction, or continue with the map if it is already loaded.");
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Compare.CompareWorkspaceViewModel.IsPdfPanelVisible))
        {
            ApplyPdfPanelLayout();
            return;
        }

        if (e.PropertyName is nameof(Compare.CompareWorkspaceViewModel.ViewerNavigationKey)
            or nameof(Compare.CompareWorkspaceViewModel.ViewerBrowserUri)
            or nameof(Compare.CompareWorkspaceViewModel.ViewerImagePath))
        {
            try
            {
                await RefreshViewerAsync();
            }
            catch (Exception exception)
            {
                viewModel.ReportWorkspaceError($"Compare document viewer could not refresh. {exception.Message}");
                ShowFallback("The selected document could not be displayed in the embedded viewer. The Compare workspace remains available.");
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowActiveMapButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        Owner?.Activate();
        Application.Current?.MainWindow?.Activate();
    }

    private void ApplyPdfPanelLayout()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ApplyPdfPanelLayout);
            return;
        }

        var isVisible = viewModel.IsPdfPanelVisible;
        PdfPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        PdfPanelColumn.Width = isVisible ? VisiblePdfPanelWidth : CollapsedPanelWidth;
        PdfPanelSpacerColumn.Width = isVisible ? VisiblePdfPanelSpacerWidth : CollapsedPanelWidth;

        if (isVisible)
        {
            MinWidth = ExpandedCompareWindowMinWidth;
            if (!wasPdfPanelVisible)
            {
                Width = Math.Max(expandedWidthBeforePdfCollapse, ExpandedCompareWindowMinWidth);
            }
        }
        else
        {
            if (wasPdfPanelVisible)
            {
                expandedWidthBeforePdfCollapse = Math.Max(Width, ExpandedCompareWindowMinWidth);
            }

            MinWidth = CompactCompareWindowMinWidth;
            Width = CompactCompareWindowWidth;
        }

        wasPdfPanelVisible = isVisible;
    }

    private async Task RefreshViewerAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(RefreshViewerAsync).Task.Unwrap();
            return;
        }

        DocumentWebViewHost.Visibility = Visibility.Collapsed;
        ImageScrollViewer.Visibility = Visibility.Collapsed;
        ShowFallback();
        DocumentImage.Source = null;

        if (viewModel.ViewerUsesBrowser && viewModel.ViewerBrowserUri is not null)
        {
            if (!await EnsureWebViewAsync())
            {
                ShowFallback("The embedded PDF viewer is unavailable. Compare remains open; use the attached file outside the embedded viewer if needed.");
                return;
            }

            DocumentWebViewHost.Visibility = Visibility.Visible;
            DocumentFallbackText.Visibility = Visibility.Collapsed;
            if (documentWebView?.CoreWebView2 is not null && lastNavigationKey != viewModel.ViewerNavigationKey)
            {
                documentWebView.CoreWebView2.Navigate(viewModel.ViewerBrowserUri.AbsoluteUri);
                lastNavigationKey = viewModel.ViewerNavigationKey;
            }

            return;
        }

        if (viewModel.ViewerShowsImage && viewModel.ViewerImagePath is not null && File.Exists(viewModel.ViewerImagePath))
        {
            ImageScrollViewer.Visibility = Visibility.Visible;
            DocumentFallbackText.Visibility = Visibility.Collapsed;
            DocumentImage.Source = new BitmapImage(new Uri(viewModel.ViewerImagePath));
        }
    }

    private async Task<bool> EnsureWebViewAsync()
    {
        if (webViewUnavailable)
        {
            return false;
        }

        if (documentWebView?.CoreWebView2 is not null)
        {
            return true;
        }

        try
        {
            if (documentWebView is null)
            {
                documentWebView = new WebView2
                {
                    CreationProperties = new CoreWebView2CreationProperties
                    {
                        UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "CompareWorkspace")
                    }
                };
                DocumentWebViewHost.Children.Add(documentWebView);
            }

            await documentWebView.EnsureCoreWebView2Async();
            return documentWebView.CoreWebView2 is not null;
        }
        catch (Exception exception)
        {
            webViewUnavailable = true;
            viewModel.ReportWorkspaceError($"Embedded PDF viewer is unavailable. {exception.Message}");
            DocumentWebViewHost.Children.Clear();
            documentWebView = null;
            return false;
        }
    }

    private void ShowFallback(string? message = null)
    {
        DocumentWebViewHost.Visibility = Visibility.Collapsed;
        DocumentFallbackText.Text = message ?? viewModel.ViewerFallbackMessage;
        DocumentFallbackText.Visibility = Visibility.Visible;
    }
}
