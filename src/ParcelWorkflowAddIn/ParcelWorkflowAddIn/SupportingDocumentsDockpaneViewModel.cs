using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Review;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Input;

namespace ParcelWorkflowAddIn;

internal sealed class SupportingDocumentsDockpaneViewModel : INotifyPropertyChanged
{
    internal const string DockPaneId = "ParcelWorkflow_SupportingDocumentsDockpane";

    private readonly CaseFolderStore caseFolderStore = new();
    private readonly SourceFileActionService sourceFileActionService = new();
    private readonly RelayCommand openSupportingDocumentCommand;
    private readonly RelayCommand revealSupportingDocumentCommand;
    private readonly RelayCommand reloadSupportingDocumentViewerCommand;
    private readonly RenderedReviewDocumentService renderedReviewDocumentService = new();
    private string? activeCaseFolderPath;
    private string? transactionId;
    private IReadOnlyList<SourceFileListItem> sourceFileItems = Array.Empty<SourceFileListItem>();
    private string? selectedSupportingDocumentCopiedPath;
    private int supportingDocumentViewerReloadVersion;
    private ReviewSourceViewerState supportingDocumentViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
    private string supportingDocumentTextContent = string.Empty;
    private string? supportingDocumentTextLoadError;
    private string supportingDocumentViewerStatusDetail = "No supporting document selected.";
    private ImageSource? supportingDocumentViewerImageSource;
    private CancellationTokenSource? supportingDocumentViewerLoadCancellation;
    private string caption = "Supporting Documents";
    private string tabText = "Supporting Documents";

    public event PropertyChangedEventHandler? PropertyChanged;

    public SupportingDocumentsDockpaneViewModel()
    {
        SupportingDocumentsDiagnostics.Write("Supporting Documents view-model constructor entered.");
        openSupportingDocumentCommand = new RelayCommand(OpenSupportingDocument, () => SelectedSupportingDocument is not null);
        revealSupportingDocumentCommand = new RelayCommand(RevealSupportingDocument, () => SelectedSupportingDocument is not null);
        reloadSupportingDocumentViewerCommand = new RelayCommand(ReloadSupportingDocumentViewer, () => SelectedSupportingDocument is not null);
        ShellState.Session.SessionChanged += (_, _) => SyncLoadedCaseFolder();
        SyncLoadedCaseFolder();
        SupportingDocumentsDiagnostics.Write($"Supporting Documents view-model constructed. Title: {SupportingDocumentsTabTitle}");
    }

    internal static void Show()
    {
        _ = TryShow();
    }

    internal static bool TryShow()
    {
        try
        {
            var viewModel = SupportingDocumentsWindow.ActiveViewModel ?? new SupportingDocumentsDockpaneViewModel();
            SupportingDocumentsDiagnostics.Write($"TryShow using WPF window. Title before reload: {viewModel.SupportingDocumentsTabTitle}");
            viewModel.ReloadActiveCaseFolder();
            SupportingDocumentsWindow.ShowOrActivate(viewModel);
            SupportingDocumentsDiagnostics.Write($"TryShow opened WPF window. Title after reload: {viewModel.SupportingDocumentsTabTitle}; documents={viewModel.SupportingDocumentOptions.Count}");
            return true;
        }
        catch (Exception exception)
        {
            SupportingDocumentsDiagnostics.Write($"TryShow WPF window exception: {exception.GetType().Name}: {exception.Message}");
            Debug.WriteLine($"Supporting Documents window activation failed: {exception.Message}");
            return false;
        }
    }

    internal static void HideIfOpen()
    {
        SupportingDocumentsWindow.CloseIfOpen();
    }

    internal static void RefreshIfOpen()
    {
        SupportingDocumentsWindow.RefreshIfOpen();
    }

    public string SupportingDocumentsTabTitle =>
        HasActiveCase
            ? $"Supporting Documents [{SupportingDocumentWorkspaceProjection.FormatTransactionLabel(transactionId)}]"
            : "Supporting Documents";

    public string Caption
    {
        get => caption;
        private set
        {
            if (!string.Equals(caption, value, StringComparison.Ordinal))
            {
                caption = value;
                NotifyPropertyChanged(nameof(Caption));
            }
        }
    }

    public string TabText
    {
        get => tabText;
        private set
        {
            if (!string.Equals(tabText, value, StringComparison.Ordinal))
            {
                tabText = value;
                NotifyPropertyChanged(nameof(TabText));
            }
        }
    }

    public bool HasActiveCase => !string.IsNullOrWhiteSpace(activeCaseFolderPath);

    public ICommand OpenSupportingDocumentCommand => openSupportingDocumentCommand;

    public ICommand RevealSupportingDocumentCommand => revealSupportingDocumentCommand;

    public ICommand ReloadSupportingDocumentViewerCommand => reloadSupportingDocumentViewerCommand;

    public IReadOnlyList<SourceFileListItem> SupportingDocumentOptions => SupportingDocumentWorkspaceProjection.BuildReadableSupportingDocumentOptions(sourceFileItems);

    public bool HasSupportingDocumentOptions => SupportingDocumentOptions.Count > 0;

    public string SupportingDocumentListSummary
    {
        get
        {
            if (!HasActiveCase)
            {
                return "Load a transaction to review supporting documents.";
            }

            var readableCount = SupportingDocumentOptions.Count;
            if (readableCount > 0)
            {
                return $"{readableCount} readable document(s) available.";
            }

            return sourceFileItems.Count == 0
                ? "No copied source files were restored for this transaction."
                : $"{sourceFileItems.Count} copied source file(s), 0 readable document(s) for this panel.";
        }
    }

    public string SupportingDocumentEmptyText =>
        HasActiveCase
            ? "No readable supporting documents are available for this transaction."
            : "Load a transaction to review supporting documents.";

    public SourceFileListItem? SelectedSupportingDocument
    {
        get => ResolveSupportingDocument();
        set
        {
            var nextPath = value?.SourceFile.CopiedPath;
            if (!string.Equals(selectedSupportingDocumentCopiedPath, nextPath, StringComparison.OrdinalIgnoreCase))
            {
                selectedSupportingDocumentCopiedPath = nextPath;
                RefreshSupportingDocumentViewerState();
                RefreshProperties();
            }
        }
    }

    public string SelectedSupportingDocumentTitle => SelectedSupportingDocument is null
        ? "No document selected"
        : $"{SelectedSupportingDocument.RoleLabel}: {SelectedSupportingDocument.FileLabel}";

    public string SelectedSupportingDocumentPath => SelectedSupportingDocument?.SourceRelativePath ?? SupportingDocumentEmptyText;

    public string SupportingDocumentViewerModeLabel => SupportingDocumentViewerShowsText ? "Text preview" : supportingDocumentViewerState.ModeLabel;

    public string SupportingDocumentViewerLoadState
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(supportingDocumentTextLoadError))
            {
                return "Text preview unavailable";
            }

            return SupportingDocumentViewerShowsText ? "Text ready" : supportingDocumentViewerState.LoadState;
        }
    }

    public string SupportingDocumentViewerFallbackMessage =>
        !string.IsNullOrWhiteSpace(supportingDocumentTextLoadError)
            ? supportingDocumentTextLoadError
            : supportingDocumentViewerState.FallbackMessage;

    public string SupportingDocumentViewerStatusDetail => supportingDocumentViewerStatusDetail;

    public bool SupportingDocumentViewerUsesBrowser =>
        supportingDocumentViewerState.UsesBrowser && !string.IsNullOrWhiteSpace(supportingDocumentViewerState.FullPath);

    public bool SupportingDocumentViewerUsesImage =>
        supportingDocumentViewerState.UsesImage && supportingDocumentViewerImageSource is not null;

    public bool SupportingDocumentViewerShowsText =>
        SelectedSupportingDocument is { } selected
        && SupportingDocumentWorkspaceProjection.IsTextDocument(selected.SourceFile)
        && string.IsNullOrWhiteSpace(supportingDocumentTextLoadError)
        && !string.IsNullOrWhiteSpace(supportingDocumentTextContent);

    public bool SupportingDocumentViewerShowsFallback =>
        SelectedSupportingDocument is null
        || (!SupportingDocumentViewerUsesBrowser && !SupportingDocumentViewerUsesImage && !SupportingDocumentViewerShowsText)
        || !string.IsNullOrWhiteSpace(supportingDocumentTextLoadError);

    public string SupportingDocumentTextContent => supportingDocumentTextContent;

    public ImageSource? SupportingDocumentViewerImageSource => supportingDocumentViewerImageSource;

    public Uri? SupportingDocumentViewerBrowserUri =>
        SupportingDocumentViewerUsesBrowser && !string.IsNullOrWhiteSpace(supportingDocumentViewerState.FullPath)
            ? new Uri(supportingDocumentViewerState.FullPath, UriKind.Absolute)
            : null;

    public string SupportingDocumentViewerNavigationKey =>
        SupportingDocumentViewerUsesBrowser && !string.IsNullOrWhiteSpace(supportingDocumentViewerState.FullPath)
            ? $"{supportingDocumentViewerState.FullPath}|{supportingDocumentViewerReloadVersion}"
            : $"no-supporting-browser|{supportingDocumentViewerReloadVersion}";

    private void SyncLoadedCaseFolder()
    {
        var loadedCaseFolderPath = ShellState.Session.LoadedCaseFolderPath;
        SupportingDocumentsDiagnostics.Write($"SyncLoadedCaseFolder. LoadedCaseFolderPath='{loadedCaseFolderPath ?? "(null)"}'.");
        if (string.IsNullOrWhiteSpace(loadedCaseFolderPath))
        {
            Reset();
            HideIfOpen();
            SupportingDocumentsDiagnostics.Write("SyncLoadedCaseFolder reset because no transaction is loaded.");
            return;
        }

        if (string.Equals(activeCaseFolderPath, loadedCaseFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            RefreshProperties();
            SupportingDocumentsDiagnostics.Write($"SyncLoadedCaseFolder refreshed existing active case. documents={SupportingDocumentOptions.Count}; sourceFiles={sourceFileItems.Count}");
            return;
        }

        var result = caseFolderStore.ReopenCaseFolder(loadedCaseFolderPath);
        if (!result.Success || result.Manifest is null)
        {
            Reset();
            var issues = string.Join("; ", result.RecoverabilityIssues.Select(issue => issue.Message));
            SupportingDocumentsDiagnostics.Write($"SyncLoadedCaseFolder failed to reopen case. Success={result.Success}; Issues='{issues}'.");
            return;
        }

        activeCaseFolderPath = loadedCaseFolderPath;
        transactionId = result.Manifest.TransactionId;
        sourceFileItems = result.SourceFiles.Select(sourceFile => new SourceFileListItem(sourceFile)).ToArray();
        RefreshSupportingDocumentViewerState();
        RefreshProperties();
        SupportingDocumentsDiagnostics.Write($"SyncLoadedCaseFolder loaded case. transaction={transactionId}; sourceFiles={sourceFileItems.Count}; readableDocuments={SupportingDocumentOptions.Count}; selected='{SelectedSupportingDocument?.FileLabel ?? "(none)"}'.");
    }

    internal void ReloadActiveCaseFolder()
    {
        SupportingDocumentsDiagnostics.Write($"ReloadActiveCaseFolder requested. PriorSelectedPath='{selectedSupportingDocumentCopiedPath ?? "(null)"}'.");
        var priorSelectedPath = selectedSupportingDocumentCopiedPath;
        activeCaseFolderPath = null;
        selectedSupportingDocumentCopiedPath = priorSelectedPath;
        SyncLoadedCaseFolder();
    }

    private SourceFileListItem? ResolveSupportingDocument()
    {
        var availableDocuments = SupportingDocumentOptions;
        if (availableDocuments.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(selectedSupportingDocumentCopiedPath))
        {
            var selected = availableDocuments.FirstOrDefault(item =>
                string.Equals(item.SourceFile.CopiedPath, selectedSupportingDocumentCopiedPath, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return availableDocuments.FirstOrDefault();
    }

    private void RefreshSupportingDocumentViewerState()
    {
        var sourceFile = SelectedSupportingDocument?.SourceFile;
        supportingDocumentTextContent = string.Empty;
        supportingDocumentTextLoadError = null;

        if (sourceFile is null)
        {
            selectedSupportingDocumentCopiedPath = null;
            supportingDocumentViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
            supportingDocumentViewerStatusDetail = SupportingDocumentEmptyText;
            return;
        }

        selectedSupportingDocumentCopiedPath = sourceFile.CopiedPath;
        if (SupportingDocumentWorkspaceProjection.IsTextDocument(sourceFile))
        {
            LoadSupportingDocumentText(sourceFile);
        }

        supportingDocumentViewerState = ReviewSourceViewerStateProjector.Build(sourceFile, InnolaTransactionSettings.Load().PdfViewerMode);
        supportingDocumentViewerImageSource = null;
        supportingDocumentViewerStatusDetail = string.IsNullOrWhiteSpace(supportingDocumentViewerState.FullPath)
            ? supportingDocumentViewerState.FallbackMessage
            : $"Selected file: {supportingDocumentViewerState.FullPath}";
        RefreshSupportingDocumentImageViewer(sourceFile);
    }

    private void RefreshSupportingDocumentImageViewer(SourceFileCopyResult sourceFile)
    {
        supportingDocumentViewerLoadCancellation?.Cancel();
        supportingDocumentViewerLoadCancellation?.Dispose();
        supportingDocumentViewerLoadCancellation = null;

        if (!supportingDocumentViewerState.UsesImage || string.IsNullOrWhiteSpace(supportingDocumentViewerState.FullPath))
        {
            return;
        }

        var sourcePath = supportingDocumentViewerState.FullPath;
        supportingDocumentViewerLoadCancellation = new CancellationTokenSource();
        _ = RefreshSupportingDocumentImageViewerAsync(sourceFile, sourcePath, supportingDocumentViewerLoadCancellation.Token);
    }

    private async Task RefreshSupportingDocumentImageViewerAsync(SourceFileCopyResult sourceFile, string sourcePath, CancellationToken cancellationToken)
    {
        try
        {
            MarkSupportingDocumentRenderAttempt($"Rendering {sourceFile.FileName} from {sourcePath}");
            var renderedPage = await renderedReviewDocumentService.RenderAsync(sourcePath, 0, cancellationToken).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            supportingDocumentViewerImageSource = renderedPage.ImageSource;
            MarkSupportingDocumentRenderReady("Embedded image loaded.");
            RefreshProperties();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSupportingDocumentRenderFailure(exception.Message);
        }
    }

    private void LoadSupportingDocumentText(SourceFileCopyResult sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile.CopiedPath) || !File.Exists(sourceFile.CopiedPath))
        {
            supportingDocumentTextLoadError = "The copied text file is missing from the case folder. Other supporting documents remain available.";
            return;
        }

        try
        {
            supportingDocumentTextContent = File.ReadAllText(sourceFile.CopiedPath);
            if (string.IsNullOrEmpty(supportingDocumentTextContent))
            {
                supportingDocumentTextContent = "The copied text file is empty.";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            supportingDocumentTextLoadError = $"The copied text file could not be read: {exception.Message}";
        }
    }

    private void OpenSupportingDocument()
    {
        ExecuteSourceFileAction(SourceFileAction.Open);
    }

    private void RevealSupportingDocument()
    {
        ExecuteSourceFileAction(SourceFileAction.Reveal);
    }

    private void ExecuteSourceFileAction(SourceFileAction action)
    {
        var selected = SelectedSupportingDocument;
        if (selected is null || string.IsNullOrWhiteSpace(activeCaseFolderPath))
        {
            return;
        }

        CaseFolderLayout layout;
        try
        {
            layout = CaseFolderLayout.FromRootDirectory(activeCaseFolderPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            supportingDocumentTextLoadError = $"The active case folder could not be read: {exception.Message}";
            RefreshProperties();
            return;
        }

        var result = sourceFileActionService.Execute(layout, selected.SourceFile, action);
        if (result.Success)
        {
            return;
        }

        if (action == SourceFileAction.Reveal
            && string.Equals(result.Status, "missing", StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(layout.SourceDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = layout.SourceDirectory,
                UseShellExecute = true
            });
        }

        supportingDocumentTextLoadError = result.Message;
        RefreshSupportingDocumentViewerState();
        supportingDocumentTextLoadError = result.Message;
        RefreshProperties();
    }

    internal void MarkSupportingDocumentRenderFailure(string? failureReason)
    {
        supportingDocumentViewerState = ReviewSourceViewerStateProjector.BuildRenderFailure(
            SelectedSupportingDocument?.SourceFile,
            failureReason);
        supportingDocumentViewerImageSource = null;
        supportingDocumentViewerStatusDetail = supportingDocumentViewerState.FallbackMessage;
        RefreshProperties();
    }

    internal void MarkSupportingDocumentRenderAttempt(string message)
    {
        supportingDocumentViewerStatusDetail = message;
        NotifyPropertyChanged(nameof(SupportingDocumentViewerStatusDetail));
    }

    internal void MarkSupportingDocumentRenderReady(string message)
    {
        supportingDocumentViewerStatusDetail = message;
        NotifyPropertyChanged(nameof(SupportingDocumentViewerStatusDetail));
    }

    private void ReloadSupportingDocumentViewer()
    {
        supportingDocumentViewerReloadVersion++;
        ReloadActiveCaseFolder();
        if (activeCaseFolderPath is not null)
        {
            return;
        }

        RefreshSupportingDocumentViewerState();
        RefreshProperties();
    }

    private void Reset()
    {
        activeCaseFolderPath = null;
        transactionId = null;
        sourceFileItems = Array.Empty<SourceFileListItem>();
        selectedSupportingDocumentCopiedPath = null;
        supportingDocumentViewerReloadVersion = 0;
        supportingDocumentViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
        supportingDocumentTextContent = string.Empty;
        supportingDocumentTextLoadError = null;
        supportingDocumentViewerImageSource = null;
        supportingDocumentViewerLoadCancellation?.Cancel();
        supportingDocumentViewerLoadCancellation?.Dispose();
        supportingDocumentViewerLoadCancellation = null;
        supportingDocumentViewerStatusDetail = "No supporting document selected.";
        RefreshProperties();
    }

    private void RefreshProperties()
    {
        Caption = SupportingDocumentsTabTitle;
        TabText = SupportingDocumentsTabTitle;
        NotifyPropertyChanged(nameof(SupportingDocumentsTabTitle));
        NotifyPropertyChanged(nameof(HasActiveCase));
        NotifyPropertyChanged(nameof(SupportingDocumentOptions));
        NotifyPropertyChanged(nameof(HasSupportingDocumentOptions));
        NotifyPropertyChanged(nameof(SupportingDocumentListSummary));
        NotifyPropertyChanged(nameof(SupportingDocumentEmptyText));
        NotifyPropertyChanged(nameof(SelectedSupportingDocument));
        NotifyPropertyChanged(nameof(SelectedSupportingDocumentTitle));
        NotifyPropertyChanged(nameof(SelectedSupportingDocumentPath));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerModeLabel));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerLoadState));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerStatusDetail));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerFallbackMessage));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerUsesBrowser));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerUsesImage));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerShowsText));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerShowsFallback));
        NotifyPropertyChanged(nameof(SupportingDocumentTextContent));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerImageSource));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerBrowserUri));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerNavigationKey));
        openSupportingDocumentCommand.RaiseCanExecuteChanged();
        revealSupportingDocumentCommand.RaiseCanExecuteChanged();
        reloadSupportingDocumentViewerCommand.RaiseCanExecuteChanged();
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
