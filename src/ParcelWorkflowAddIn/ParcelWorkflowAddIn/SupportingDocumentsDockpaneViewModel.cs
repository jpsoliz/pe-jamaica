using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Review;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace ParcelWorkflowAddIn;

internal sealed class SupportingDocumentsDockpaneViewModel : DockPane
{
    internal const string DockPaneId = "ParcelWorkflow_SupportingDocumentsDockpane";

    private readonly CaseFolderStore caseFolderStore = new();
    private readonly SourceFileActionService sourceFileActionService = new();
    private readonly RelayCommand openSupportingDocumentCommand;
    private readonly RelayCommand revealSupportingDocumentCommand;
    private readonly RelayCommand reloadSupportingDocumentViewerCommand;
    private string? activeCaseFolderPath;
    private string? transactionId;
    private IReadOnlyList<SourceFileListItem> sourceFileItems = Array.Empty<SourceFileListItem>();
    private string? selectedSupportingDocumentCopiedPath;
    private int supportingDocumentViewerReloadVersion;
    private ReviewSourceViewerState supportingDocumentViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
    private string supportingDocumentTextContent = string.Empty;
    private string? supportingDocumentTextLoadError;

    public SupportingDocumentsDockpaneViewModel()
    {
        openSupportingDocumentCommand = new RelayCommand(OpenSupportingDocument, () => SelectedSupportingDocument is not null);
        revealSupportingDocumentCommand = new RelayCommand(RevealSupportingDocument, () => SelectedSupportingDocument is not null);
        reloadSupportingDocumentViewerCommand = new RelayCommand(ReloadSupportingDocumentViewer, () => SelectedSupportingDocument is not null);
        ShellState.Session.SessionChanged += (_, _) => SyncLoadedCaseFolder();
        SyncLoadedCaseFolder();
    }

    internal static void Show()
    {
        FrameworkApplication.DockPaneManager.Find(DockPaneId)?.Activate();
    }

    internal static void HideIfOpen()
    {
        FrameworkApplication.DockPaneManager.Find(DockPaneId)?.Hide();
    }

    internal static void RefreshIfOpen()
    {
        if (FrameworkApplication.DockPaneManager.Find(DockPaneId) is SupportingDocumentsDockpaneViewModel pane)
        {
            pane.ReloadActiveCaseFolder();
        }
    }

    public string SupportingDocumentsTabTitle =>
        HasActiveCase
            ? $"Supporting Documents [{SupportingDocumentWorkspaceProjection.FormatTransactionLabel(transactionId)}]"
            : "Supporting Documents";

    public bool HasActiveCase => !string.IsNullOrWhiteSpace(activeCaseFolderPath);

    public ICommand OpenSupportingDocumentCommand => openSupportingDocumentCommand;

    public ICommand RevealSupportingDocumentCommand => revealSupportingDocumentCommand;

    public ICommand ReloadSupportingDocumentViewerCommand => reloadSupportingDocumentViewerCommand;

    public IReadOnlyList<SourceFileListItem> SupportingDocumentOptions => SupportingDocumentWorkspaceProjection.BuildReadableSupportingDocumentOptions(sourceFileItems);

    public bool HasSupportingDocumentOptions => SupportingDocumentOptions.Count > 0;

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

    public bool SupportingDocumentViewerUsesBrowser =>
        supportingDocumentViewerState.UsesBrowser && !string.IsNullOrWhiteSpace(supportingDocumentViewerState.FullPath);

    public bool SupportingDocumentViewerShowsText =>
        SelectedSupportingDocument is { } selected
        && SupportingDocumentWorkspaceProjection.IsTextDocument(selected.SourceFile)
        && string.IsNullOrWhiteSpace(supportingDocumentTextLoadError)
        && !string.IsNullOrWhiteSpace(supportingDocumentTextContent);

    public bool SupportingDocumentViewerShowsFallback =>
        SelectedSupportingDocument is null
        || (!SupportingDocumentViewerUsesBrowser && !SupportingDocumentViewerShowsText)
        || !string.IsNullOrWhiteSpace(supportingDocumentTextLoadError);

    public string SupportingDocumentTextContent => supportingDocumentTextContent;

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
        if (string.IsNullOrWhiteSpace(loadedCaseFolderPath) || !ShellState.IsSelectedTransactionComputeWorkflow)
        {
            Reset();
            HideIfOpen();
            return;
        }

        if (string.Equals(activeCaseFolderPath, loadedCaseFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            RefreshProperties();
            return;
        }

        var result = caseFolderStore.ReopenCaseFolder(loadedCaseFolderPath);
        if (!result.Success || result.Manifest is null)
        {
            Reset();
            return;
        }

        activeCaseFolderPath = loadedCaseFolderPath;
        transactionId = result.Manifest.TransactionId;
        sourceFileItems = result.SourceFiles.Select(sourceFile => new SourceFileListItem(sourceFile)).ToArray();
        RefreshSupportingDocumentViewerState();
        RefreshProperties();
    }

    private void ReloadActiveCaseFolder()
    {
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
            return;
        }

        selectedSupportingDocumentCopiedPath = sourceFile.CopiedPath;
        if (SupportingDocumentWorkspaceProjection.IsTextDocument(sourceFile))
        {
            LoadSupportingDocumentText(sourceFile);
        }

        supportingDocumentViewerState = ReviewSourceViewerStateProjector.Build(sourceFile, InnolaTransactionSettings.Load().PdfViewerMode);
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
        RefreshProperties();
    }

    private void ReloadSupportingDocumentViewer()
    {
        supportingDocumentViewerReloadVersion++;
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
        NotifyPropertyChanged(nameof(SupportingDocumentEmptyText));
        NotifyPropertyChanged(nameof(SelectedSupportingDocument));
        NotifyPropertyChanged(nameof(SelectedSupportingDocumentTitle));
        NotifyPropertyChanged(nameof(SelectedSupportingDocumentPath));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerModeLabel));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerLoadState));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerFallbackMessage));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerUsesBrowser));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerShowsText));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerShowsFallback));
        NotifyPropertyChanged(nameof(SupportingDocumentTextContent));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerBrowserUri));
        NotifyPropertyChanged(nameof(SupportingDocumentViewerNavigationKey));
        openSupportingDocumentCommand.RaiseCanExecuteChanged();
        revealSupportingDocumentCommand.RaiseCanExecuteChanged();
        reloadSupportingDocumentViewerCommand.RaiseCanExecuteChanged();
    }
}
