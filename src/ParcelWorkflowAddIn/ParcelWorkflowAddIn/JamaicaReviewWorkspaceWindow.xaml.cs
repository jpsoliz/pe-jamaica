using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ParcelWorkflowAddIn;

internal partial class JamaicaReviewWorkspaceWindow : ProWindow
{
    private readonly JamaicaReviewWorkspaceViewModel viewModel;
    private string? lastViewerNavigationKey;
    private bool allowClose;
    private bool renderExceptionHandled;
    private string? selectedTabHeader;
    private string? lastControlContext;
    private ReviewWorkspaceBindingTraceListener? bindingTraceListener;
    private WorkspaceCloseDisposition closeDisposition = WorkspaceCloseDisposition.None;

    internal JamaicaReviewWorkspaceWindow(JamaicaReviewWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ReviewWorkspaceDiagnostics.Write(
            viewModel.CaseFolderPath,
            "workspace_window_constructed",
            context: viewModel.BuildDiagnosticsSnapshot("window_constructor", selectedTabHeader, lastControlContext));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteSaveFlow(closeAfterSave: false, triggerClose: true);
    }

    private void ValidationCompleteButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteValidationCompleteFlow(triggerClose: true);
    }

    private void SegmentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        viewModel.EditSelectedSegment();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RemovePointButton_Click(object sender, RoutedEventArgs e)
    {
        var pointLabel = string.IsNullOrWhiteSpace(viewModel.SelectedVisibleRow?.PointIdentifier)
            ? "the selected point"
            : $"point {viewModel.SelectedVisibleRow.PointIdentifier}";
        var owner = FrameworkApplication.Current?.MainWindow ?? this;
        var result = MessageBox.Show(
            owner,
            $"Delete {pointLabel} from this review?{Environment.NewLine}{Environment.NewLine}This removes the point from the current Points Validation Tool list. Save the review to persist the change.",
            "Delete point",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (!viewModel.RemoveSelectedPointFromWorkspace())
        {
            MessageBox.Show(
                owner,
                "The selected point could not be deleted. Select a point and try again.",
                "Delete point unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachBindingDiagnostics();
        selectedTabHeader = ResolveSelectedTabHeader();
        ReviewWorkspaceDiagnostics.Write(
            viewModel.CaseFolderPath,
            "workspace_window_loaded",
            context: viewModel.BuildDiagnosticsSnapshot("window_loaded", selectedTabHeader, lastControlContext));
        await RefreshPdfViewerAsync();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(JamaicaReviewWorkspaceViewModel.ViewerNavigationKey)
            or nameof(JamaicaReviewWorkspaceViewModel.ViewerBrowserUri)
            or nameof(JamaicaReviewWorkspaceViewModel.ViewerUsesBrowser))
        {
            await RefreshPdfViewerAsync();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        if (TryPrepareCloseDisposition())
        {
            allowClose = true;
            return;
        }

        e.Cancel = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ReviewWorkspaceDiagnostics.Write(
            viewModel.CaseFolderPath,
            "workspace_window_closed",
            context: viewModel.BuildDiagnosticsSnapshot("window_closed", selectedTabHeader, lastControlContext));
        Dispatcher.UnhandledException -= OnDispatcherUnhandledException;
        DetachBindingDiagnostics();
        viewModel.HandleWindowClosed(
            reviewSaved: closeDisposition is WorkspaceCloseDisposition.SavedOnly or WorkspaceCloseDisposition.SavedAndContinued,
            continuedToCreateSpatialUnits: closeDisposition == WorkspaceCloseDisposition.SavedAndContinued,
            discardedUnsavedChanges: closeDisposition == WorkspaceCloseDisposition.DiscardedUnsavedChanges);
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Detach();
    }

    private async Task RefreshPdfViewerAsync()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(RefreshPdfViewerAsync).Task.Unwrap();
                return;
            }

            if (ViewerPdfWebView is null)
            {
                return;
            }

            if (!viewModel.ViewerUsesBrowser || viewModel.ViewerBrowserUri is null)
            {
                lastViewerNavigationKey = null;
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

            var navigationKey = viewModel.ViewerNavigationKey;
            if (!string.IsNullOrWhiteSpace(navigationKey)
                && string.Equals(lastViewerNavigationKey, navigationKey, StringComparison.Ordinal)
                && ViewerPdfWebView.CoreWebView2 is not null)
            {
                return;
            }

            ViewerPdfWebView.CreationProperties ??= new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Path.GetTempPath(), "SidwellCo", "WebView2", "JamaicaReviewWorkspace")
            };

            await ViewerPdfWebView.EnsureCoreWebView2Async();
            if (ViewerPdfWebView.CoreWebView2 is not null)
            {
                ViewerPdfWebView.CoreWebView2.Navigate(viewModel.ViewerBrowserUri.AbsoluteUri);
                lastViewerNavigationKey = navigationKey;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or COMException or ArgumentException)
        {
            ReviewWorkspaceDiagnostics.Write(
                viewModel.CaseFolderPath,
                "viewer_refresh_exception",
                exception,
                viewModel.BuildDiagnosticsSnapshot("viewer_refresh", selectedTabHeader, lastControlContext));
            throw;
        }
    }

    private void ReviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, ReviewTabs))
        {
            return;
        }

        selectedTabHeader = ResolveSelectedTabHeader();
        lastControlContext = $"Tab:{selectedTabHeader}";
        ReviewWorkspaceDiagnostics.Write(
            viewModel.CaseFolderPath,
            "workspace_tab_selected",
            context: viewModel.BuildDiagnosticsSnapshot("tab_selection", selectedTabHeader, lastControlContext));
    }

    private void ReviewDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            lastControlContext = BuildGridContext(grid);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var controlContext = BuildExceptionControlContext(e.Exception);
        ReviewWorkspaceDiagnostics.Write(
            viewModel.CaseFolderPath,
            "dispatcher_unhandled_exception",
            e.Exception,
            viewModel.BuildDiagnosticsSnapshot("dispatcher_unhandled_exception", selectedTabHeader, controlContext));

        if (!IsReviewWorkspaceRenderException(e.Exception))
        {
            return;
        }

        e.Handled = true;
        if (renderExceptionHandled)
        {
            return;
        }

        renderExceptionHandled = true;
        var logPath = ReviewWorkspaceDiagnostics.GetPrimaryLogPath(viewModel.CaseFolderPath);
        MessageBox.Show(
            this,
            $"Points Validation Tool hit a display error and wrote diagnostics to:{Environment.NewLine}{logPath}{Environment.NewLine}{Environment.NewLine}Close and reopen the tool after reviewing the log.",
            "Review display diagnostics captured",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            allowClose = true;
            Close();
        }), DispatcherPriority.Background);
    }

    private void AttachBindingDiagnostics()
    {
        if (bindingTraceListener is not null)
        {
            return;
        }

        bindingTraceListener = new ReviewWorkspaceBindingTraceListener(message =>
        {
            ReviewWorkspaceDiagnostics.Write(
                viewModel.CaseFolderPath,
                "wpf_binding_trace",
                context: viewModel.BuildDiagnosticsSnapshot("binding_trace", selectedTabHeader, message));
        });
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTraceListener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    private void DetachBindingDiagnostics()
    {
        if (bindingTraceListener is null)
        {
            return;
        }

        PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingTraceListener);
        bindingTraceListener.Dispose();
        bindingTraceListener = null;
    }

    private string? ResolveSelectedTabHeader()
    {
        return ReviewTabs?.SelectedItem is TabItem { Header: { } header }
            ? header.ToString()
            : null;
    }

    private string BuildExceptionControlContext(Exception exception)
    {
        var stack = exception.StackTrace ?? string.Empty;
        var stackContext = stack.Contains("DataGrid", StringComparison.OrdinalIgnoreCase)
            ? "DataGrid"
            : stack.Contains("Grid.Measure", StringComparison.OrdinalIgnoreCase)
                ? "Grid.Measure"
                : stack.Contains("Binding", StringComparison.OrdinalIgnoreCase)
                    ? "Binding"
                    : null;
        return string.Join("; ", new[] { lastControlContext, stackContext }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildGridContext(DataGrid grid)
    {
        var selectedItemType = grid.SelectedItem?.GetType().Name;
        var selectedItemText = grid.SelectedItem?.ToString();
        return $"DataGrid:{grid.Name}; Items={grid.Items.Count}; SelectedType={selectedItemType}; Selected={selectedItemText}";
    }

    private static bool IsReviewWorkspaceRenderException(Exception exception)
    {
        if (exception is not ArgumentException and not InvalidOperationException)
        {
            return false;
        }

        var stack = exception.StackTrace ?? string.Empty;
        return stack.Contains("System.Windows.Controls.Grid", StringComparison.OrdinalIgnoreCase)
            || stack.Contains("System.Windows.Controls.DataGrid", StringComparison.OrdinalIgnoreCase)
            || stack.Contains("System.Windows.ContextLayoutManager", StringComparison.OrdinalIgnoreCase)
            || stack.Contains("System.Windows.Data", StringComparison.OrdinalIgnoreCase);
    }

    private bool ExecuteSaveFlow(bool closeAfterSave, bool triggerClose)
    {
        if (!viewModel.CanSaveReview)
        {
            MessageBox.Show(
                this,
                "Save is not available for the current Points Validation Tool state. Review the point rows and validation messages, then try Save again.",
                "Save unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (!viewModel.SaveReviewChanges())
        {
            MessageBox.Show(
                this,
                "Save did not complete. The Points Validation Tool will stay open so you can review the current point changes.",
                "Save did not complete",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (closeAfterSave)
        {
            closeDisposition = WorkspaceCloseDisposition.SavedOnly;
            if (triggerClose)
            {
                allowClose = true;
                Close();
            }
        }

        return true;
    }

    private void ExecuteValidationCompleteFlow(bool triggerClose)
    {
        if (!viewModel.CanCompleteValidation)
        {
            MessageBox.Show(
                this,
                viewModel.ValidationCompletionStatusText,
                "Validation cannot be completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (viewModel.HasUnsavedReviewChanges)
        {
            var savePromptResult = MessageBox.Show(
                this,
                "Point changes are still unsaved. Choose Yes to save them and continue into Create Spatial Units. Choose No to stay in Points Validation Tool.",
                "Unsaved point changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (savePromptResult != MessageBoxResult.Yes)
            {
                return;
            }

            if (!ExecuteSaveFlow(closeAfterSave: false, triggerClose: false))
            {
                return;
            }
        }

        if (!viewModel.ContinueToCreateSpatialUnits())
        {
            MessageBox.Show(
                this,
                viewModel.ValidationCompletionStatusText,
                "Validation did not complete",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        closeDisposition = WorkspaceCloseDisposition.SavedAndContinued;
        if (triggerClose)
        {
            allowClose = true;
            Close();
        }
    }

    private bool TryPrepareCloseDisposition()
    {
        if (!viewModel.HasUnsavedReviewChanges)
        {
            closeDisposition = WorkspaceCloseDisposition.None;
            return true;
        }

        var saveResult = MessageBox.Show(
            this,
            BuildClosePromptMessage(),
            "Close Points Validation Tool",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (saveResult == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (saveResult == MessageBoxResult.Yes)
        {
            return ExecuteSaveFlow(closeAfterSave: true, triggerClose: false)
                && (allowClose || closeDisposition is WorkspaceCloseDisposition.SavedOnly or WorkspaceCloseDisposition.SavedAndContinued);
        }

        if (!viewModel.DiscardUnsavedReviewChanges())
        {
            return false;
        }

        closeDisposition = WorkspaceCloseDisposition.DiscardedUnsavedChanges;
        return true;
    }

    private string BuildClosePromptMessage()
    {
        var saveState = viewModel.CanSaveReview
            ? "Save is available for these point changes."
            : "Save is not available for the current review state. Choose No only if you want to discard unsaved changes and close.";

        return $"Unsaved point changes were detected.{Environment.NewLine}{Environment.NewLine}"
            + $"{saveState}{Environment.NewLine}{Environment.NewLine}"
            + "Yes = save changes and close."
            + $"{Environment.NewLine}No = discard unsaved changes and close."
            + $"{Environment.NewLine}Cancel = stay in Points Validation Tool.";
    }

    private sealed class ReviewWorkspaceBindingTraceListener : TraceListener
    {
        private readonly Action<string> writeMessage;
        private string pendingMessage = string.Empty;

        internal ReviewWorkspaceBindingTraceListener(Action<string> writeMessage)
        {
            this.writeMessage = writeMessage;
        }

        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                pendingMessage += message;
            }
        }

        public override void WriteLine(string? message)
        {
            var fullMessage = string.Concat(pendingMessage, message).Trim();
            pendingMessage = string.Empty;
            if (!string.IsNullOrWhiteSpace(fullMessage))
            {
                writeMessage(fullMessage);
            }
        }
    }

    private enum WorkspaceCloseDisposition
    {
        None,
        SavedOnly,
        SavedAndContinued,
        DiscardedUnsavedChanges
    }
}
