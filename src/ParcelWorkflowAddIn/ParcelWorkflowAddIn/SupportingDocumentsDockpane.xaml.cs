using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

public class SupportingDocumentsDockpane : UserControl
{
    private string? lastSupportingDocumentNavigationKey;
    private readonly WebView2? supportingDocumentPdfWebView;

    public SupportingDocumentsDockpane()
    {
        var loadedControl = (UserControl)Application.LoadComponent(
            new Uri("/ParcelWorkflowAddIn;component/SupportingDocumentsDockpane.xaml", UriKind.Relative));

        Content = loadedControl.Content;
        Resources = loadedControl.Resources;
        Background = loadedControl.Background;
        FontFamily = loadedControl.FontFamily;
        supportingDocumentPdfWebView = loadedControl.FindName("SupportingDocumentPdfWebView") as WebView2;

        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await SafeRefreshSupportingDocumentPdfViewerAsync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SupportingDocumentsDockpaneViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is SupportingDocumentsDockpaneViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
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

    private async Task SafeRefreshSupportingDocumentPdfViewerAsync()
    {
        try
        {
            await RefreshSupportingDocumentPdfViewerAsync();
        }
        catch (Exception exception)
        {
            MarkRenderFailure(exception);
        }
    }

    private async Task RefreshSupportingDocumentPdfViewerAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(RefreshSupportingDocumentPdfViewerAsync).Task.Unwrap();
            return;
        }

        if (supportingDocumentPdfWebView is null || DataContext is not SupportingDocumentsDockpaneViewModel viewModel)
        {
            return;
        }

        if (!viewModel.SupportingDocumentViewerUsesBrowser || viewModel.SupportingDocumentViewerBrowserUri is null)
        {
            lastSupportingDocumentNavigationKey = null;
            if (supportingDocumentPdfWebView.CoreWebView2 is not null)
            {
                supportingDocumentPdfWebView.CoreWebView2.Navigate("about:blank");
            }

            return;
        }

        if (!File.Exists(viewModel.SupportingDocumentViewerBrowserUri.LocalPath))
        {
            return;
        }

        var navigationKey = viewModel.SupportingDocumentViewerNavigationKey;
        if (!string.IsNullOrWhiteSpace(navigationKey)
            && string.Equals(lastSupportingDocumentNavigationKey, navigationKey, StringComparison.Ordinal)
            && supportingDocumentPdfWebView.CoreWebView2 is not null)
        {
            return;
        }

        supportingDocumentPdfWebView.CreationProperties ??= new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "SupportingDocumentsDockpane")
        };

        try
        {
            await supportingDocumentPdfWebView.EnsureCoreWebView2Async();
            if (supportingDocumentPdfWebView.CoreWebView2 is not null)
            {
                supportingDocumentPdfWebView.CoreWebView2.Navigate(viewModel.SupportingDocumentViewerBrowserUri.AbsoluteUri);
                lastSupportingDocumentNavigationKey = navigationKey;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or UnauthorizedAccessException
            or FileNotFoundException
            or IOException
            or UriFormatException
            or NotSupportedException
            or System.Runtime.InteropServices.COMException)
        {
            MarkRenderFailure(exception);
        }
    }

    private void MarkRenderFailure(Exception exception)
    {
        if (DataContext is SupportingDocumentsDockpaneViewModel viewModel)
        {
            viewModel.MarkSupportingDocumentRenderFailure(exception.Message);
        }
    }
}
