using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Pla;
using ParcelWorkflowAddIn.Workflow.Review;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ParcelWorkflowAddIn;

internal partial class SupportingDocumentCropWindow : ProWindow
{
    private readonly CaseFolderLayout layout;
    private readonly SelectedInnolaTransaction transaction;
    private readonly SourceFileCopyResult sourceFile;
    private readonly string? peNumber;
    private readonly DocumentCropRenderingService renderService;
    private readonly PlaBSupportingDocumentCropService cropService;
    private DocumentCropPreviewPage? previewPage;
    private Point dragStart;
    private bool isDragging;
    private DocumentCropRectangle? previewSelection;
    private CancellationTokenSource? loadCancellation;

    internal SupportingDocumentCropWindow(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        SourceFileCopyResult sourceFile,
        string? peNumber = null,
        DocumentCropRenderingService? renderService = null,
        PlaBSupportingDocumentCropService? cropService = null)
    {
        InitializeComponent();
        this.layout = layout;
        this.transaction = transaction;
        this.sourceFile = sourceFile;
        this.peNumber = peNumber;
        this.renderService = renderService ?? new DocumentCropRenderingService();
        this.cropService = cropService ?? new PlaBSupportingDocumentCropService();
        Owner = FrameworkApplication.Current?.MainWindow;
        Loaded += OnLoaded;
        Closed += OnClosed;
        ShellState.Session.SessionChanged += OnSessionChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachButton.IsEnabled = PlaBSupportingDocumentCropService.LoadCrop(layout, transaction.TransactionNumber) is not null;
        await LoadPreviewAsync(0);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ShellState.Session.SessionChanged -= OnSessionChanged;
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = null;
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => OnSessionChanged(sender, e));
            return;
        }

        var loadedCase = ShellState.Session.LoadedCaseFolderPath;
        var loadedTransaction = ShellState.Session.LoadedTransactionNumber;
        if (string.IsNullOrWhiteSpace(loadedCase)
            || !string.Equals(System.IO.Path.GetFullPath(loadedCase), System.IO.Path.GetFullPath(layout.RootDirectory), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(loadedTransaction)
                && !string.Equals(loadedTransaction, transaction.TransactionNumber, StringComparison.OrdinalIgnoreCase)))
        {
            loadCancellation?.Cancel();
            Close();
        }
    }

    private async Task LoadPreviewAsync(int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            StatusTextBlock.Text = "Selected document path is unavailable.";
            return;
        }

        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        try
        {
            StatusTextBlock.Text = "Rendering crop preview...";
            previewPage = await renderService.RenderPreviewAsync(sourceFile.CopiedPath, pageIndex, loadCancellation.Token).ConfigureAwait(true);
            PreviewImage.Source = previewPage.ImageSource;
            PreviewCanvas.Width = previewPage.ImageSource.PixelWidth;
            PreviewCanvas.Height = previewPage.ImageSource.PixelHeight;
            PageStatusTextBlock.Text = $"{previewPage.DocumentKind} {previewPage.PageIndex + 1} of {previewPage.PageCount}";
            ClearSelection();
            StatusTextBlock.Text = "Drag a rectangle over the page, then save PNG.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            StatusTextBlock.Text = $"Crop preview unavailable: {exception.Message}";
        }
    }

    private async void PreviousPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (previewPage is not null && previewPage.PageIndex > 0)
        {
            await LoadPreviewAsync(previewPage.PageIndex - 1);
        }
    }

    private async void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (previewPage is not null && previewPage.PageIndex + 1 < previewPage.PageCount)
        {
            await LoadPreviewAsync(previewPage.PageIndex + 1);
        }
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (previewPage is null)
        {
            return;
        }

        dragStart = ClampPoint(e.GetPosition(PreviewCanvas));
        isDragging = true;
        PreviewCanvas.CaptureMouse();
        UpdateSelectionRectangle(dragStart, dragStart);
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            UpdateSelectionRectangle(dragStart, ClampPoint(e.GetPosition(PreviewCanvas)));
        }
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        PreviewCanvas.ReleaseMouseCapture();
        UpdateSelectionRectangle(dragStart, ClampPoint(e.GetPosition(PreviewCanvas)));
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (previewPage is null || previewSelection is null || string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            StatusTextBlock.Text = "Draw a crop rectangle before saving.";
            return;
        }

        var dpi = SelectedDpi();
        var request = new DocumentCropExportRequest(
            sourceFile.CopiedPath,
            previewPage.PageIndex,
            ToSourceRectangle(previewSelection, previewPage),
            dpi,
            previewSelection,
            previewPage.ImageSource.PixelWidth,
            previewPage.ImageSource.PixelHeight);
        var validation = renderService.ValidateExportRequest(request);
        if (!validation.CanContinue)
        {
            StatusTextBlock.Text = validation.Message;
            return;
        }

        if (!string.IsNullOrWhiteSpace(validation.Warning))
        {
            var proceed = MessageBox.Show(
                this,
                validation.Warning,
                "Large crop",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.OK)
            {
                StatusTextBlock.Text = "Save canceled.";
                return;
            }
        }

        StatusTextBlock.Text = "Saving crop PNG...";
        var result = await cropService.SaveCropAsync(layout, transaction, peNumber, sourceFile, request).ConfigureAwait(true);
        AttachButton.IsEnabled = result.Success;
        if (!result.Success)
        {
            StatusTextBlock.Text = result.Message;
            return;
        }

        var savedPath = PlaBSupportingDocumentCropService.GetPngPath(layout);
        StatusTextBlock.Text = $"File was saved: {savedPath}";
        MessageBox.Show(
            this,
            $"File was saved:\n{savedPath}",
            "Crop saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        await ConfirmAndAttachSavedCropAsync(savedPath).ConfigureAwait(true);
    }

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        await ConfirmAndAttachSavedCropAsync(PlaBSupportingDocumentCropService.GetPngPath(layout)).ConfigureAwait(true);
    }

    private async Task ConfirmAndAttachSavedCropAsync(string pngPath)
    {
        var fileName = System.IO.Path.GetFileName(pngPath);
        var confirmation = MessageBox.Show(
            this,
            $"Do you want to attach {fileName} to TR {transaction.TransactionNumber}?",
            "Attach crop",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            StatusTextBlock.Text = $"File was saved: {pngPath}. Attach canceled.";
            return;
        }

        StatusTextBlock.Text = "Attaching saved crop to current transaction...";
        var result = await cropService.AttachSavedCropAsync(layout, transaction).ConfigureAwait(true);
        AttachButton.IsEnabled = true;
        if (!result.Success)
        {
            StatusTextBlock.Text = result.Message;
            return;
        }

        var completeMessage = $"Attachment complete: {fileName} was attached to TR {transaction.TransactionNumber}.";
        StatusTextBlock.Text = completeMessage;
        MessageBox.Show(
            this,
            completeMessage,
            "Attach complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ClearSelection()
    {
        previewSelection = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
    }

    private void UpdateSelectionRectangle(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        previewSelection = new DocumentCropRectangle(x, y, width, height);
        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
        SelectionRectangle.Visibility = width > 0 && height > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Point ClampPoint(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, PreviewCanvas.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, PreviewCanvas.Height)));
    }

    private static DocumentCropRectangle ToSourceRectangle(DocumentCropRectangle previewRectangle, DocumentCropPreviewPage page)
    {
        var xScale = page.SourceWidth / page.ImageSource.PixelWidth;
        var yScale = page.SourceHeight / page.ImageSource.PixelHeight;
        return new DocumentCropRectangle(
            previewRectangle.X * xScale,
            previewRectangle.Y * yScale,
            previewRectangle.Width * xScale,
            previewRectangle.Height * yScale);
    }

    private int SelectedDpi()
    {
        if (DpiComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
            && int.TryParse(item.Content?.ToString(), out var dpi)
            && DocumentCropRenderingService.SupportedDpiValues.Contains(dpi))
        {
            return dpi;
        }

        return DocumentCropRenderingService.DefaultDpi;
    }
}
