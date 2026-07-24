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
    private string? lastViewerNavigationKey;
    private bool allowClose;
    private WorkspaceCloseDisposition closeDisposition = WorkspaceCloseDisposition.None;

    internal JamaicaReviewWorkspaceWindow(JamaicaReviewWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
        var result = MessageBox.Show(
            this,
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
                this,
                "The selected point could not be deleted. Select a point and try again.",
                "Delete point unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
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
        viewModel.HandleWindowClosed(
            reviewSaved: closeDisposition is WorkspaceCloseDisposition.SavedOnly or WorkspaceCloseDisposition.SavedAndContinued,
            continuedToCreateSpatialUnits: closeDisposition == WorkspaceCloseDisposition.SavedAndContinued,
            discardedUnsavedChanges: closeDisposition == WorkspaceCloseDisposition.DiscardedUnsavedChanges);
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Detach();
    }

    private async Task RefreshPdfViewerAsync()
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

    private enum WorkspaceCloseDisposition
    {
        None,
        SavedOnly,
        SavedAndContinued,
        DiscardedUnsavedChanges
    }
}
