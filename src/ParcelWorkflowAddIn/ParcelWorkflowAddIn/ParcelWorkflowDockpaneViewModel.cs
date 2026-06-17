using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Contracts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework.Controls;

namespace ParcelWorkflowAddIn;

internal sealed class ParcelWorkflowDockpaneViewModel : DockPane
{
    internal const string DockPaneId = "ParcelWorkflow_Dockpane";
    private readonly WorkflowSession workflowSession = new(new CaseFolderStore());
    private readonly ExtractionReviewPersistenceService extractionReviewService = new();
    private readonly RelayCommand createCaseCommand;
    private readonly RelayCommand browseOutputLocationCommand;
    private readonly RelayCommand addSourceFilesCommand;
    private readonly RelayCommand refreshInputProfileCommand;
    private readonly RelayCommand reopenCaseCommand;
    private readonly RelayCommand openSourceFileCommand;
    private readonly RelayCommand revealSourceFileCommand;
    private readonly RelayCommand routeSourceFileToMapCommand;
    private readonly RelayCommand runPreflightCommand;
    private readonly RelayCommand runExtractionReviewCommand;
    private readonly RelayCommand runValidationCommand;
    private readonly RelayCommand runOutputsCommand;
    private readonly RelayCommand loadSpatialReviewLayersCommand;
    private readonly RelayCommand openCogoReaderCommand;
    private readonly RelayCommand approveSpatialReviewCommand;
    private readonly RelayCommand addManualPointCommand;
    private readonly RelayCommand saveReviewCommand;
    private readonly RelayCommand approveReviewCommand;
    private readonly RelayCommand togglePreflightDetailsCommand;
    private readonly RelayCommand toggleOutputPreviewCommand;
    private readonly RelayCommand toggleReviewDetailsCommand;
    private readonly RelayCommand openReviewSourceCommand;
    private readonly RelayCommand revealReviewSourceCommand;
    private readonly RelayCommand reloadReviewViewerCommand;
    private readonly RelayCommand toggleReviewViewerFitCommand;
    private readonly RelayCommand openExperimentalReviewWorkspaceCommand;
    private readonly RelayCommand startOrClaimTransactionCommand;
    private readonly RelayCommand suspendTransactionCommand;
    private readonly RelayCommand cancelProcessCommand;
    private readonly RelayCommand completeTransactionCommand;
    private readonly IOutputMapIntegrationService outputMapIntegrationService = new ArcGisOutputMapIntegrationService();
    private string? outputLocation;
    private string? transactionId;
    private ExtractionReviewDocument? loadedReviewDocument;
    private ExtractionReviewRowViewModel? selectedReviewRow;
    private bool preflightDetailsExpanded;
    private bool outputPreviewExpanded;
    private bool reviewDetailsExpanded = true;
    private bool intakeSummaryExpanded;
    private bool preflightSummaryExpanded;
    private bool extractionSummaryExpanded;
    private bool validationSummaryExpanded;
    private bool outputsSummaryExpanded;
    private bool reviewViewerFitToPane = true;
    private bool reviewDirty;
    private int reviewViewerReloadVersion;
    private string? selectedReviewSourceCopiedPath;
    private string? reviewViewerStateCacheKey;
    private BitmapSource? reviewViewerImageSource;
    private ReviewSourceViewerState reviewViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
    private JamaicaReviewWorkspaceWindow? experimentalReviewWorkspaceWindow;

    public ParcelWorkflowDockpaneViewModel()
    {
        createCaseCommand = new RelayCommand(CreateCase);
        browseOutputLocationCommand = new RelayCommand(BrowseOutputLocation);
        addSourceFilesCommand = new RelayCommand(AddSourceFilesFromDialog, () => CanAddSourceFiles);
        refreshInputProfileCommand = new RelayCommand(RefreshInputProfile);
        reopenCaseCommand = new RelayCommand(ReopenCaseFromDialog);
        openSourceFileCommand = new RelayCommand(parameter => ExecuteSourceFileAction(parameter, SourceFileAction.Open), CanExecuteSourceFileAction);
        revealSourceFileCommand = new RelayCommand(parameter => ExecuteSourceFileAction(parameter, SourceFileAction.Reveal), CanExecuteSourceFileAction);
        routeSourceFileToMapCommand = new RelayCommand(parameter => ExecuteSourceFileAction(parameter, SourceFileAction.RouteToMap), CanExecuteSourceFileAction);
        runPreflightCommand = new RelayCommand(async () => await RunPreflightAsync(), () => CanRunPreflight);
        runExtractionReviewCommand = new RelayCommand(async () => await RunOrOpenExtractionReviewAsync(), () => CanRunExtractionReview);
        runValidationCommand = new RelayCommand(async () => await RunValidationAsync(), () => CanRunValidation);
        runOutputsCommand = new RelayCommand(async () => await RunOutputsAsync(), () => CanRunOutputs);
        loadSpatialReviewLayersCommand = new RelayCommand(async () => await LoadSpatialReviewLayersAsync(), () => CanLoadSpatialReviewLayers);
        openCogoReaderCommand = new RelayCommand(async () => await OpenCogoReaderAsync(), () => CanOpenCogoReader);
        approveSpatialReviewCommand = new RelayCommand(ApproveSpatialReview, () => CanApproveSpatialReview);
        addManualPointCommand = new RelayCommand(AddManualPoint, () => HasLoadedReviewData && !IsReviewLocked);
        saveReviewCommand = new RelayCommand(SaveReviewChanges, () => HasLoadedReviewData && ReviewRows.Count > 0 && !IsReviewLocked);
        approveReviewCommand = new RelayCommand(ApproveReview, () => HasLoadedReviewData && ReviewRows.Count > 0 && !IsReviewLocked);
        togglePreflightDetailsCommand = new RelayCommand(TogglePreflightDetails, () => HasPreflightResults);
        toggleOutputPreviewCommand = new RelayCommand(ToggleOutputPreview);
        toggleReviewDetailsCommand = new RelayCommand(ToggleReviewDetails, () => HasLoadedReviewData);
        openReviewSourceCommand = new RelayCommand(OpenReviewSource, () => SelectedReviewSource is not null);
        revealReviewSourceCommand = new RelayCommand(RevealReviewSource, () => SelectedReviewSource is not null);
        reloadReviewViewerCommand = new RelayCommand(ReloadReviewViewer, () => SelectedReviewSource is not null);
        toggleReviewViewerFitCommand = new RelayCommand(ToggleReviewViewerFit, () => CanToggleReviewViewerFit);
        openExperimentalReviewWorkspaceCommand = new RelayCommand(async () => await OpenExperimentalReviewWorkspaceAsync(), () => CanOpenExperimentalReviewWorkspace);
        startOrClaimTransactionCommand = new RelayCommand(async () => await StartOrClaimTransactionAsync(), () => ShellState.Session.CanStartOrClaimTransaction);
        suspendTransactionCommand = new RelayCommand(async () => await SuspendTransactionAsync(), () => ShellState.Session.CanSaveProgress);
        cancelProcessCommand = new RelayCommand(CancelProcess, () => ShellState.Session.CanCancelActiveProcess);
        completeTransactionCommand = new RelayCommand(async () => await CompleteTransactionAsync(), () => CanCompleteTransaction);
        ShellState.Session.SessionChanged += (_, _) => SyncLoadedCaseFolder();
        SyncLoadedCaseFolder();
    }

    internal static void Show()
    {
        FrameworkApplication.DockPaneManager.Find(DockPaneId)?.Activate();
    }

    public ObservableCollection<ExtractionReviewRowViewModel> ReviewRows { get; } = [];

    public string? TransactionId
    {
        get => transactionId ?? workflowSession.TransactionId;
        set => SetProperty(ref transactionId, value, () => TransactionId);
    }

    public string? OutputLocation
    {
        get => outputLocation;
        set => SetProperty(ref outputLocation, value, () => OutputLocation);
    }

    public WorkflowState CurrentWorkflowState => workflowSession.CurrentState;

    public string CurrentStep => workflowSession.CurrentStep;

    public string StatusText => workflowSession.StatusText;

    public string LifecycleStatusText => ShellState.Session.LifecycleStatusText ?? "No active transaction lifecycle.";

    public string HeaderTransactionText => string.IsNullOrWhiteSpace(TransactionId)
        ? "Transaction Number: not selected"
        : $"Transaction Number: {TransactionId}";

    public string HeaderTaskNameText
    {
        get
        {
            var transactionType = ResolveSelectedTransactionType();
            return string.IsNullOrWhiteSpace(transactionType) ? "Transaction Type: not available" : $"Transaction Type: {transactionType}";
        }
    }

    public string CurrentStepBadge => GetWorkspaceStageLabel(ActiveWorkspaceStage);

    public string ScoreBadge
    {
        get
        {
            if (CurrentWorkflowState is WorkflowState.ValidationBlocked)
            {
                return "Validation blocked";
            }

            if (CurrentWorkflowState is WorkflowState.SpatialReviewApproved)
            {
                return "Ready to complete";
            }

            if (CurrentWorkflowState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending)
            {
                return "Spatial review pending";
            }

            if (CurrentWorkflowState is WorkflowState.ValidationPassed or WorkflowState.OutputRunning)
            {
                return "Validation passed";
            }

            if (CurrentWorkflowState is WorkflowState.ReviewApproved)
            {
                return "Validation ready";
            }

            if (HasPreflightIssues)
            {
                return $"{PreflightBlockers.Count + PreflightWarnings.Count} issue(s)";
            }

            if (HasPreflightResults)
            {
                return "Checks clear";
            }

            return "Checks pending";
        }
    }

    public string ModeBadge
    {
        get
        {
            if (!HasActiveCase)
            {
                return "No Case";
            }

            if (!ShellState.Session.WasRestoredFromResumePackage)
            {
                return "New Case";
            }

            var lastSavedAt = FormatBadgeDateTime(ShellState.Session.LastSavedAt);
            return string.IsNullOrWhiteSpace(lastSavedAt)
                ? "Existing Case"
                : $"Existing Case - Last update: {lastSavedAt}";
        }
    }

    public bool CanAddSourceFiles => false;

    public IReadOnlyList<SourceFileListItem> SourceFiles =>
        workflowSession.SourceFiles.Select(sourceFile => new SourceFileListItem(sourceFile)).ToArray();

    public IReadOnlyList<WorkflowLifecycleStep> WorkflowSteps => BuildWorkflowSteps();

    public WorkflowWorkspaceStage ActiveWorkspaceStage => WorkflowWorkspacePlanner.ResolveActiveStage(CurrentWorkflowState, IntakeReadyForPreflight, HasExtractionArtifact);

    public bool HasActiveCase => !string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath);

    public bool IntakeReadyForPreflight =>
        HasActiveCase
        && SourceFiles.Count > 0
        && !string.Equals(DetectedProfileLabel, "Detected profile: not refreshed", StringComparison.OrdinalIgnoreCase);

    public bool HasExtractionArtifact => HasExtractionReviewArtifact(workflowSession);

    public bool IsIntakeStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.Intake;

    public bool IsPreflightStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.Preflight;

    public bool IsExtractionReviewStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.ExtractionReview;

    public bool IsValidationStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.Validation;

    public bool IsOutputsStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.Outputs;

    public bool IsSpatialReviewStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.SpatialReview;

    public bool CanOpenCogoReader => HasActiveCase && CanUseWorkflowActions;

    public bool ShowIntakeSummary => HasActiveCase && !IsIntakeStageActive;

    public bool ShowPreflightSummary => HasPreflightResults && !IsPreflightStageActive;

    public bool ShowExtractionSummary => HasExtractionArtifact && !IsExtractionReviewStageActive;

    public bool ShowValidationSummaryCard =>
        (workflowSession.CurrentValidationSummary is not null
        || CurrentWorkflowState is WorkflowState.ReviewApproved or WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed or WorkflowState.OutputRunning or WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved)
        && !IsValidationStageActive;

    public bool ShowOutputsSummary => (HasOutputArtifacts || CurrentWorkflowState is WorkflowState.ValidationPassed or WorkflowState.OutputRunning or WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved) && !IsOutputsStageActive;

    public bool CanUseWorkflowActions => ShellState.Session.CanOpenParcelWorkflow;

    public bool CanRunPreflight => CanUseWorkflowActions && workflowSession.CanRunPreflight && !workflowSession.IsPreflightRunning;

    public bool CanRunExtractionReview => CanUseWorkflowActions && workflowSession.CanRunExtractionReview;

    public bool CanRunOutputs => CanUseWorkflowActions && workflowSession.CanRunOutputs;

    public bool CanLoadSpatialReviewLayers =>
        CanUseWorkflowActions
        && workflowSession.CurrentOutputSummary is not null
        && CurrentWorkflowState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;

    public bool CanApproveSpatialReview => CanUseWorkflowActions && workflowSession.CanApproveSpatialReview;

    public bool CanCompleteTransaction => ShellState.Session.CanCompleteTransaction && workflowSession.CurrentState == WorkflowState.SpatialReviewApproved;

    public ICommand CreateCaseCommand => createCaseCommand;

    public ICommand BrowseOutputLocationCommand => browseOutputLocationCommand;

    public ICommand AddSourceFilesCommand => addSourceFilesCommand;

    public ICommand RefreshInputProfileCommand => refreshInputProfileCommand;

    public ICommand ReopenCaseCommand => reopenCaseCommand;

    public ICommand OpenSourceFileCommand => openSourceFileCommand;

    public ICommand RevealSourceFileCommand => revealSourceFileCommand;

    public ICommand RouteSourceFileToMapCommand => routeSourceFileToMapCommand;

    public ICommand RunPreflightCommand => runPreflightCommand;

    public ICommand RunExtractionReviewCommand => runExtractionReviewCommand;

    public ICommand RunValidationCommand => runValidationCommand;

    public ICommand RunOutputsCommand => runOutputsCommand;

    public ICommand LoadSpatialReviewLayersCommand => loadSpatialReviewLayersCommand;

    public ICommand OpenCogoReaderCommand => openCogoReaderCommand;

    public ICommand ApproveSpatialReviewCommand => approveSpatialReviewCommand;

    public ICommand AddManualPointCommand => addManualPointCommand;

    public ICommand SaveReviewCommand => saveReviewCommand;

    public ICommand ApproveReviewCommand => approveReviewCommand;

    public ICommand TogglePreflightDetailsCommand => togglePreflightDetailsCommand;

    public ICommand ToggleOutputPreviewCommand => toggleOutputPreviewCommand;

    public ICommand ToggleReviewDetailsCommand => toggleReviewDetailsCommand;

    public ICommand OpenReviewSourceCommand => openReviewSourceCommand;

    public ICommand RevealReviewSourceCommand => revealReviewSourceCommand;

    public ICommand ReloadReviewViewerCommand => reloadReviewViewerCommand;

    public ICommand ToggleReviewViewerFitCommand => toggleReviewViewerFitCommand;

    public ICommand OpenExperimentalReviewWorkspaceCommand => openExperimentalReviewWorkspaceCommand;

    public ICommand StartOrClaimTransactionCommand => startOrClaimTransactionCommand;

    public ICommand SuspendTransactionCommand => suspendTransactionCommand;

    public ICommand CancelProcessCommand => cancelProcessCommand;

    public ICommand CompleteTransactionCommand => completeTransactionCommand;

    public string DetectedProfileLabel => workflowSession.DetectedProfileLabel;

    public IReadOnlyList<string> IntakeIssues => workflowSession.IntakeIssues;

    public IReadOnlyList<AvailableArtifact> AvailableArtifacts => workflowSession.AvailableArtifacts;

    public IReadOnlyList<PreflightCheck> PreflightBlockers => workflowSession.PreflightBlockers;

    public IReadOnlyList<PreflightCheck> PreflightWarnings => workflowSession.PreflightWarnings;

    public IReadOnlyList<PreflightCheck> PreflightPassedChecks => workflowSession.PreflightPassedChecks;

    public IReadOnlyList<PreflightResultListItem> PreflightResults =>
        PreflightBlockers.Select(check => new PreflightResultListItem("Block", check))
            .Concat(PreflightWarnings.Select(check => new PreflightResultListItem("Warn", check)))
            .Concat(PreflightPassedChecks.Select(check => new PreflightResultListItem("Pass", check)))
            .ToArray();

    public bool HasPreflightResults => PreflightResults.Count > 0;

    public bool PreflightDetailsExpanded => preflightDetailsExpanded;

    public string PreflightToggleText => PreflightDetailsExpanded ? "Hide details" : "Show details";

    public string PreflightBadge
    {
        get
        {
            if (workflowSession.IsPreflightRunning)
            {
                return "Processing";
            }

            if (PreflightBlockers.Count > 0)
            {
                return $"{PreflightBlockers.Count} blocker(s)";
            }

            if (PreflightWarnings.Count > 0)
            {
                return $"{PreflightWarnings.Count} warning(s)";
            }

            if (PreflightPassedChecks.Count > 0)
            {
                return "Passed";
            }

            return "Not processed";
        }
    }

    public string SourceIntakeBadge => SourceFiles.Count > 0 && SourceFiles.All(item => item.SourceFile.Copied)
        ? "Copied"
        : "Pending";

    public string IntakeSummaryText =>
        SourceFiles.Count == 0
            ? "No source files copied into the case folder yet."
            : $"{SourceFiles.Count} source file(s) copied. {DetectedProfileLabel}";

    public string IntakeDetailText =>
        IntakeIssues.Count == 0
            ? "Source intake is preserved as case-folder context for downstream stages."
            : string.Join(Environment.NewLine, IntakeIssues);

    public bool IntakeSummaryExpanded
    {
        get => intakeSummaryExpanded;
        set => SetProperty(ref intakeSummaryExpanded, value, () => IntakeSummaryExpanded);
    }

    public string PreflightSummaryText
    {
        get
        {
            if (!HasPreflightResults)
            {
                return "No processing check results yet.";
            }

            return $"{PreflightBlockers.Count} blocker(s), {PreflightWarnings.Count} warning(s), {PreflightPassedChecks.Count} passed.";
        }
    }

    public string PreflightCollapsedHint =>
        PreflightBlockers.Count > 0
            ? $"Blocking now: {PreflightBlockers[0].Message}"
            : PreflightWarnings.Count > 0
                ? $"Attention: {PreflightWarnings[0].Message}"
                : "All current processing checks passed.";

    public bool PreflightSummaryExpanded
    {
        get => preflightSummaryExpanded;
        set => SetProperty(ref preflightSummaryExpanded, value, () => PreflightSummaryExpanded);
    }

    public string ExtractionReviewBadge =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ExtractionRunning => "Processing",
            WorkflowState.ExtractionFailed => "Blocked",
            WorkflowState.ReviewApproved => "Approved",
            WorkflowState.ValidationRunning => "Approved",
            WorkflowState.ValidationBlocked => "Approved",
            WorkflowState.ValidationPassed => "Approved",
            WorkflowState.OutputRunning => "Approved",
            WorkflowState.OutputCreated => "Approved",
            WorkflowState.ReviewPending => "Ready",
            WorkflowState.PreflightPassed when HasExtractionReviewArtifact(workflowSession) => "Ready",
            WorkflowState.PreflightPassed => "Ready to run",
            WorkflowState.PreflightBlocked => "Blocked",
            _ => "Not started"
        };

    public string ExtractionReviewActionLabel =>
        HasLoadedReviewData ? "Reload" : HasExtractionReviewArtifact(workflowSession) ? "Open" : "Run";

    public string ExtractionReviewHelpText =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ExtractionRunning => "Draft extraction is running from the current script plan.",
            WorkflowState.ExtractionFailed => "Draft extraction failed. Review the status line, then try again.",
            WorkflowState.ReviewApproved => "Review data is approved. The review workspace is now read-only for this case state.",
            WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed => "Review data is approved. Validation now owns the next workflow gate and review remains read-only.",
            WorkflowState.OutputRunning or WorkflowState.OutputCreated => "Review data is approved. Outputs now own the downstream geometry stage and review remains read-only.",
            WorkflowState.ReviewPending when HasExtractionReviewArtifact(workflowSession) => "Draft review data is ready to inspect and correct before parcel build.",
            WorkflowState.PreflightPassed => "Extraction review will generate draft review data from the selected transaction files.",
            WorkflowState.PreflightBlocked => "Review Extracted Points is unavailable until processing check blockers are resolved.",
            _ => "Review Extracted Points is enabled after Processing Checks complete."
        };

    public bool HasLoadedReviewData => loadedReviewDocument is not null && ReviewRows.Count > 0;

    public ExtractionReviewRowViewModel? SelectedReviewRow
    {
        get => selectedReviewRow;
        set
        {
            if (!EqualityComparer<ExtractionReviewRowViewModel?>.Default.Equals(selectedReviewRow, value))
            {
                SetProperty(ref selectedReviewRow, value, () => SelectedReviewRow);
                NotifyPropertyChanged(nameof(SelectedReviewRowDetailsTitle));
                NotifyPropertyChanged(nameof(SelectedReviewRowDetailsText));
            }
        }
    }

    public IReadOnlyList<SourceFileListItem> ReviewSourceOptions => SourceFiles;

    public SourceFileListItem? SelectedReviewSource
    {
        get => ResolveReviewSource();
        set
        {
            var nextPath = value?.SourceFile.CopiedPath;
            if (!string.Equals(selectedReviewSourceCopiedPath, nextPath, StringComparison.OrdinalIgnoreCase))
            {
                selectedReviewSourceCopiedPath = nextPath;
                reviewViewerStateCacheKey = null;
                RefreshWorkflowProperties();
            }
        }
    }

    public string ReviewWorkspaceTitle => "Source Verification Workspace";

    public bool CanOpenExperimentalReviewWorkspace => CanUseWorkflowActions && HasActiveCase && (HasLoadedReviewData || HasExtractionArtifact || workflowSession.CanRunExtractionReview);

    public string SelectedReviewSourceTitle => SelectedReviewSource is null
        ? "Source document not resolved"
        : $"{SelectedReviewSource.RoleLabel}: {SelectedReviewSource.FileLabel}";

    public string SelectedReviewSourcePath => SelectedReviewSource?.SourceRelativePath ?? "No point-bearing source document is available.";

    public ReviewSourceViewerState ReviewViewerState => reviewViewerState;

    public string ReviewViewerFileTitle => reviewViewerState.Title;

    public string ReviewViewerRoleLabel => reviewViewerState.RoleLabel;

    public string ReviewViewerDisplayPath => reviewViewerState.DisplayPath;

    public string ReviewViewerModeLabel => reviewViewerState.ModeLabel;

    public string ReviewViewerLoadState => reviewViewerState.LoadState;

    public string ReviewViewerGuidance => reviewViewerState.Guidance;

    public string ReviewViewerFallbackMessage => reviewViewerState.FallbackMessage;

    public bool ReviewViewerUsesImage => reviewViewerState.UsesImage && reviewViewerImageSource is not null;

    public bool ReviewViewerUsesBrowser => reviewViewerState.UsesBrowser;

    public bool ReviewViewerShowsFallback => !reviewViewerState.CanRenderEmbedded || (reviewViewerState.UsesImage && reviewViewerImageSource is null);

    public bool CanToggleReviewViewerFit => ReviewViewerUsesImage;

    public string ReviewViewerFitToggleText => reviewViewerFitToPane ? "Actual size" : "Fit to pane";

    public Stretch ReviewViewerImageStretch => reviewViewerFitToPane ? Stretch.Uniform : Stretch.None;

    public ImageSource? ReviewViewerImageSource => reviewViewerImageSource;

    public Uri? ReviewViewerBrowserUri =>
        ReviewViewerUsesBrowser && !string.IsNullOrWhiteSpace(reviewViewerState.FullPath)
            ? new Uri(reviewViewerState.FullPath, UriKind.Absolute)
            : null;

    public string ReviewViewerNavigationKey =>
        ReviewViewerUsesBrowser && !string.IsNullOrWhiteSpace(reviewViewerState.FullPath)
            ? $"{reviewViewerState.FullPath}|{reviewViewerReloadVersion}"
            : $"no-browser|{reviewViewerReloadVersion}";

    public string SelectedReviewSourceMode
    {
        get
        {
            var source = SelectedReviewSource;
            if (source is null)
            {
                return "Unavailable";
            }

            return source.SourceFile.FileType.ToLowerInvariant() switch
            {
                ".png" or ".jpg" or ".jpeg" => "Image source",
                ".tif" or ".tiff" => "Scanned raster source",
                ".pdf" => "PDF source",
                _ => "Source document"
            };
        }
    }

    public string SelectedReviewSourceGuidance
    {
        get
        {
            var source = SelectedReviewSource;
            if (source is null)
            {
                return "Load or verify source files before reviewing extracted points.";
            }

            return source.SourceFile.FileType.ToLowerInvariant() switch
            {
                ".png" or ".jpg" or ".jpeg" => "Use the source image as the visual reference while checking extracted points.",
                ".tif" or ".tiff" => "Use the scanned raster as the visual reference. Open in the associated viewer for zoom/pan as needed.",
                ".pdf" => "Use the source PDF as the verification reference. Open it in the associated viewer while correcting point rows.",
                _ => "Open the source document in its associated viewer while verifying point edits."
            };
        }
    }

    public string SelectedReviewRowDetailsTitle => SelectedReviewRow is null
        ? "Row details"
        : $"Point details: {SelectedReviewRow.PointIdentifier}";

    public bool ReviewDetailsExpanded => reviewDetailsExpanded;

    public string ReviewDetailsToggleText => ReviewDetailsExpanded ? "Hide details" : "Show details";

    public bool IsReviewApproved => IsReviewLockedState(workflowSession.CurrentState);

    public bool IsReviewLocked => IsReviewApproved;

    public string SelectedReviewRowDetailsText
    {
        get
        {
            if (SelectedReviewRow is null)
            {
                return "Select a point row to view original extracted values, source evidence, unresolved reason, and notes.";
            }

            return $"Original point: {BlankIfEmpty(SelectedReviewRow.OriginalPointIdentifier)}{Environment.NewLine}"
                + $"Original easting: {BlankIfEmpty(SelectedReviewRow.OriginalEasting)}{Environment.NewLine}"
                + $"Original northing: {BlankIfEmpty(SelectedReviewRow.OriginalNorthing)}{Environment.NewLine}"
                + $"Original length: {BlankIfEmpty(SelectedReviewRow.Model.OriginalValues.Length)}{Environment.NewLine}"
                + $"Original status: {BlankIfEmpty(SelectedReviewRow.OriginalStatus)}{Environment.NewLine}"
                + $"Evidence: {BlankIfEmpty(SelectedReviewRow.SourceEvidence)}{Environment.NewLine}"
                + $"Unresolved reason: {BlankIfEmpty(SelectedReviewRow.UnresolvedReason)}{Environment.NewLine}"
                + $"Notes: {BlankIfEmpty(SelectedReviewRow.ReviewNotes)}{Environment.NewLine}"
                + $"Row source: {SelectedReviewRow.RowProvenance}";
        }
    }

    public bool ReviewHasBlockers => ReviewSummary.UnresolvedRows > 0 || ReviewSummary.MissingRequiredRows > 0;

    public ExtractionReviewSummary ReviewSummary =>
        loadedReviewDocument is null
            ? new ExtractionReviewSummary(0, 0, 0, 0, 0)
            : extractionReviewService.Summarize(loadedReviewDocument);

    public string ReviewSummaryText =>
        !HasLoadedReviewData
            ? "Open extraction review to inspect and correct extracted points."
            : IsReviewApproved
                ? $"{ReviewSummary.TotalRows} point row(s) loaded, review approved and locked for editing."
                : $"{ReviewSummary.TotalRows} point row(s) loaded, {ReviewSummary.EditedRows} edited, {ReviewSummary.ManualRows} manual, {ReviewSummary.UnresolvedRows} unresolved.";

    public string ReviewGateText =>
        !HasLoadedReviewData
            ? "Review data not loaded."
            : IsReviewApproved
                ? "Review approved. Details remain available for verification, but editing is disabled."
            : ReviewSummary.CanApprove
                ? "Review is complete for this stage."
                : $"{ReviewSummary.UnresolvedRows} unresolved row(s) and {ReviewSummary.MissingRequiredRows} row(s) missing required values block approval.";

    public string ReviewBadgeText =>
        !HasLoadedReviewData
            ? "Not loaded"
            : reviewDirty
                ? "Unsaved"
                : IsReviewApproved
                    ? "Approved"
                    : "Loaded";

    public bool ExtractionSummaryExpanded
    {
        get => extractionSummaryExpanded;
        set => SetProperty(ref extractionSummaryExpanded, value, () => ExtractionSummaryExpanded);
    }

    public bool OutputPreviewExpanded => outputPreviewExpanded;

    public string OutputPreviewToggleText => OutputPreviewExpanded ? "Hide preview" : "Show preview";

    public IReadOnlyList<AvailableArtifact> OutputArtifacts => ResolveOutputArtifacts();

    public bool HasReadyToCompleteStage => ActiveWorkspaceStage == WorkflowWorkspaceStage.ReadyToComplete;

    public string SpatialReviewBadge =>
        workflowSession.CurrentState switch
        {
            WorkflowState.SpatialReviewApproved => "Approved",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Pending review",
            _ => "Waiting"
        };

    public string SpatialReviewSummaryText =>
        workflowSession.CurrentState switch
        {
            WorkflowState.SpatialReviewApproved => "Map-based spatial review is complete. The reviewed parcel layers are ready for final transaction completion.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Open or reuse the generated parcel layers in ArcGIS Pro, inspect geometry in-map, and apply any needed snapping or COGO edits before marking the review complete.",
            _ => "Spatial review becomes available after output generation succeeds."
        };

    public string SpatialReviewHelpText =>
        workflowSession.CurrentState switch
        {
            WorkflowState.SpatialReviewApproved => "Spatial review has been approved. If outputs are regenerated later, this approval will be cleared automatically and the geometry must be reviewed again.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Use the ArcGIS Pro map as the editing surface. Standard edit, snapping, and COGO-capable tools should be used there rather than inside this dock pane.",
            _ => "Run Outputs first. When geometry is available, this stage will guide the in-map review handoff."
        };

    public string OutputPreviewSummaryText =>
        OutputArtifacts.Count == 0
            ? "No generated output package yet."
            : $"{OutputArtifacts.Count} output artifact(s) are available.";

    public bool HasOutputArtifacts => OutputArtifacts.Count > 0;

    public string OutputPreviewBodyText =>
        HasOutputArtifacts
            ? BuildOutputPreviewBodyText()
            : workflowSession.CurrentState switch
            {
                WorkflowState.OutputRunning => "Output generation is running. Local geodatabase layers and geometry artifacts will appear here when the stage finishes.",
                WorkflowState.ValidationPassed => "Validation passed. Run Outputs to build the transaction-local geodatabase and map-ready geometry.",
                _ => "Output preview stays unavailable until validation passes. This stage creates the local geometry package used by Spatial Review."
            };

    public bool OutputsSummaryExpanded
    {
        get => outputsSummaryExpanded;
        set => SetProperty(ref outputsSummaryExpanded, value, () => OutputsSummaryExpanded);
    }

    public bool CanRunValidation => CanUseWorkflowActions && workflowSession.CanRunValidation;

    public string OutputBadge =>
        workflowSession.CurrentState switch
        {
            WorkflowState.OutputRunning => "Processing",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Ready for review",
            WorkflowState.SpatialReviewApproved => "Complete",
            WorkflowState.ValidationPassed => "Ready",
            _ => "Not started"
        };

    public string ReadyToCompleteBadge =>
        workflowSession.CurrentState == WorkflowState.SpatialReviewApproved
            ? "Ready"
            : "Pending";

    public string ReadyToCompleteSummaryText =>
        workflowSession.CurrentState == WorkflowState.SpatialReviewApproved
            ? "Map review is complete. When you approve the transaction, the reviewed result will be shared and the task will be finalized."
            : "Ready to Complete becomes available after outputs are reviewed and spatial review is marked complete.";

    public string ReadyToCompleteHelpText =>
        workflowSession.CurrentState == WorkflowState.SpatialReviewApproved
            ? "Use the ArcGIS Pro map to do a final visual check, confirm the output artifacts look right, then select Approve to publish the completed review and close the transaction. Select Suspend if you need to save this state and come back later."
            : "Finish Outputs and complete Spatial Review first. That review is the gate that unlocks final transaction completion.";

    public string ValidationBadge =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ValidationRunning => "Processing",
            WorkflowState.ValidationBlocked => "Blocked",
            WorkflowState.ValidationPassed => "Passed",
            WorkflowState.OutputRunning => "Passed",
            WorkflowState.OutputCreated => "Passed",
            WorkflowState.ReviewApproved => "Ready",
            _ => "Not started"
        };

    public string ValidationSummaryText
    {
        get
        {
            var summary = workflowSession.CurrentValidationSummary;
            if (summary is null)
            {
                return workflowSession.CurrentState == WorkflowState.ReviewApproved
                    ? "Approved review data is ready for validation."
                    : "Validation has not produced a summary yet.";
            }

            var counts = summary.Payload.FindingCounts;
            return $"Status: {summary.Payload.Status}. Findings - critical {counts.Critical}, high {counts.High}, warning {counts.Warning}, info {counts.Info}, passed {counts.Passed}.";
        }
    }

    public string ValidationHelpText =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ValidationRunning => "Validation is running against the approved review snapshot.",
            WorkflowState.ValidationBlocked => "Validation completed with blocking findings. Review the validation summary before output generation.",
            WorkflowState.ValidationPassed => "Validation passed. Run Outputs to generate the transaction-local geodatabase.",
            WorkflowState.OutputRunning => "Validation passed. Output generation is currently building the local geometry package.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Validation passed. Local outputs are created and now need spatial review in the ArcGIS Pro map.",
            WorkflowState.SpatialReviewApproved => "Validation passed. Spatial review is approved, so final completion may proceed when transaction-level readiness is met.",
            WorkflowState.ReviewApproved => "Run validation on the approved review data before outputs.",
            _ => "Validation becomes available after extraction review is approved."
        };

    public bool ValidationSummaryExpanded
    {
        get => validationSummaryExpanded;
        set => SetProperty(ref validationSummaryExpanded, value, () => ValidationSummaryExpanded);
    }

    private bool HasPreflightIssues => PreflightBlockers.Count + PreflightWarnings.Count > 0;

    public void CreateCase()
    {
        if (string.IsNullOrWhiteSpace(TransactionId) || string.IsNullOrWhiteSpace(OutputLocation))
        {
            workflowSession.SetValidationFailure("Transaction ID and output location are required.");
            RefreshWorkflowProperties();
            return;
        }

        workflowSession.CreateCase(TransactionId, OutputLocation, Environment.UserName);
        RefreshWorkflowProperties();
    }

    public void AddSourceFiles(IReadOnlyList<string> sourcePaths, string? sourceRole = null)
    {
        workflowSession.AddSourceFiles(sourcePaths, sourceRole);
        RefreshWorkflowProperties();
    }

    public void RefreshInputProfile()
    {
        workflowSession.RefreshInputProfile();
        RefreshWorkflowProperties();
    }

    public void ReopenCaseFolder(string caseFolderPath)
    {
        workflowSession.ReopenCaseFolder(caseFolderPath);
        transactionId = workflowSession.TransactionId;
        RefreshWorkflowProperties();
    }

    public void RunPreflight()
    {
        RunPreflightAsync().GetAwaiter().GetResult();
    }

    public async Task RunPreflightAsync()
    {
        var running = workflowSession.RunManifestPreflightAsync(Environment.UserName);
        RefreshWorkflowProperties();
        await running;
        RefreshWorkflowProperties();
    }

    private async Task RunOrOpenExtractionReviewAsync()
    {
        await EnsureExtractionReviewLoadedAsync().ConfigureAwait(true);
        RefreshWorkflowProperties();
    }

    private async Task<bool> EnsureExtractionReviewLoadedAsync()
    {
        if (string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath))
        {
            workflowSession.SetValidationFailure("Create or reopen a Case Folder before opening extraction review.");
            RefreshWorkflowProperties();
            return false;
        }

        var layout = CaseFolderLayout.FromRootDirectory(workflowSession.CaseFolderPath);
        var artifactPath = SelectExtractionReviewArtifact(layout);
        if (artifactPath is null)
        {
            var extractionTask = workflowSession.RunDraftExtractionAsync();
            RefreshWorkflowProperties();
            var extractionResult = await extractionTask.ConfigureAwait(true);
            if (!extractionResult.Success)
            {
                RefreshWorkflowProperties();
                return false;
            }

            artifactPath = SelectExtractionReviewArtifact(layout);
            RefreshWorkflowProperties();
            if (artifactPath is null)
            {
                workflowSession.SetValidationFailure("Draft extraction completed but no extraction review artifact was found.");
                RefreshWorkflowProperties();
                return false;
            }
        }

        var reviewDocument = workflowSession.LoadExtractionReview();
        if (reviewDocument is null)
        {
            RefreshWorkflowProperties();
            return false;
        }

        LoadReviewDocumentIntoPane(reviewDocument);
        workflowSession.SetValidationFailure($"Extraction review loaded from {Path.GetFileName(artifactPath)}.");
        return true;
    }

    private void LoadReviewDocumentIntoPane(ExtractionReviewDocument document)
    {
        loadedReviewDocument = document;
        reviewDirty = false;
        ReviewRows.Clear();
        foreach (var row in document.Rows)
        {
            ReviewRows.Add(new ExtractionReviewRowViewModel(row, OnReviewRowChanged));
        }

        SelectedReviewRow = ReviewRows.FirstOrDefault();
    }

    private void OnReviewRowChanged()
    {
        reviewDirty = true;
        if (loadedReviewDocument is not null)
        {
            foreach (var row in ReviewRows)
            {
                row.SyncBackToModel();
            }
        }

        RefreshWorkflowProperties();
    }

    private void AddManualPoint()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (loadedReviewDocument is null)
        {
            return;
        }

        var nextIndex = ReviewRows.Count + 1;
        var manualRow = new ExtractionReviewRow
        {
            RowId = $"manual-{nextIndex:000}",
            PointIdentifier = $"P-{nextIndex:000}",
            Easting = string.Empty,
            Northing = string.Empty,
            Length = string.Empty,
            ExtractionStatus = "Manual entry",
            SourceEvidence = "Manual correction",
            RowProvenance = "manual",
            IsManual = true,
            IsEdited = true,
            OriginalValues = new ExtractionReviewOriginalValues()
        };
        loadedReviewDocument.Rows.Add(manualRow);
        var reviewRow = new ExtractionReviewRowViewModel(manualRow, OnReviewRowChanged);
        ReviewRows.Add(reviewRow);
        SelectedReviewRow = reviewRow;
        reviewDirty = true;
        workflowSession.SetValidationFailure("Manual review row added. Complete point id and coordinates before approval.");
        RefreshWorkflowProperties();
    }

    private void SaveReviewChanges()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (loadedReviewDocument is null || string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath))
        {
            workflowSession.SetValidationFailure("Review data is not loaded.");
            RefreshWorkflowProperties();
            return;
        }

        foreach (var row in ReviewRows)
        {
            row.SyncBackToModel();
        }

        var saveResult = workflowSession.SaveExtractionReview(loadedReviewDocument, Environment.UserName);
        if (saveResult.Success && saveResult.Document is not null)
        {
            loadedReviewDocument = saveResult.Document;
            reviewDirty = false;
        }

        RefreshWorkflowProperties();
    }

    private void ApproveReview()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (loadedReviewDocument is null)
        {
            workflowSession.SetValidationFailure("Review data is not loaded.");
            RefreshWorkflowProperties();
            return;
        }

        if (reviewDirty)
        {
            SaveReviewChanges();
            if (reviewDirty)
            {
                return;
            }
        }

        foreach (var row in ReviewRows)
        {
            row.SyncBackToModel();
        }

        var confirmation = MessageBox.Show(
            "Approve this extraction review? After approval, the review workspace will become read-only until the process is reset.",
            "Approve Extraction Review",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            workflowSession.SetValidationFailure("Review approval cancelled.");
            RefreshWorkflowProperties();
            return;
        }

        var approvalResult = workflowSession.ApproveExtractionReview(loadedReviewDocument, Environment.UserName);
        if (approvalResult.Success)
        {
            reviewDirty = false;
            reviewDetailsExpanded = false;
        }

        RefreshWorkflowProperties();
    }

    private async Task RunValidationAsync()
    {
        var validationTask = workflowSession.RunValidationAsync(Environment.UserName);
        RefreshWorkflowProperties();
        await validationTask.ConfigureAwait(true);
        RefreshWorkflowProperties();
    }

    private async Task RunOutputsAsync()
    {
        var outputTask = workflowSession.RunOutputsAsync(Environment.UserName);
        RefreshWorkflowProperties();
        var result = await outputTask.ConfigureAwait(true);
        if (result.Success)
        {
            var mapResult = await outputMapIntegrationService.AddOutputsToActiveMapAsync(workflowSession.CurrentOutputSummary).ConfigureAwait(true);
            workflowSession.SetValidationFailure(mapResult.Message);
            outputPreviewExpanded = true;
        }

        RefreshWorkflowProperties();
    }

    private async Task LoadSpatialReviewLayersAsync()
    {
        var mapResult = await outputMapIntegrationService.AddOutputsToActiveMapAsync(workflowSession.CurrentOutputSummary).ConfigureAwait(true);
        workflowSession.SetValidationFailure(mapResult.Message);
        RefreshWorkflowProperties();
    }

    private async Task OpenCogoReaderAsync()
    {
        const string cogoReaderCommandId = "esri_editing_openCogoReaderPaneButton";

        try
        {
            var executeCommand = FrameworkApplication.ExecuteCommand(cogoReaderCommandId);
            if (executeCommand is null)
            {
                workflowSession.SetValidationFailure("COGO Reader command is not available in this ArcGIS Pro session.");
                RefreshWorkflowProperties();
                return;
            }

            await executeCommand().ConfigureAwait(true);
            workflowSession.SetValidationFailure("Requested ArcGIS Pro to open COGO Reader. If nothing opens, confirm the active map supports parcel fabric editing and the built-in command is enabled.");
        }
        catch (Exception exception)
        {
            workflowSession.SetValidationFailure($"Could not launch COGO Reader: {exception.Message}");
        }

        RefreshWorkflowProperties();
    }

    private void ApproveSpatialReview()
    {
        var confirmation = MessageBox.Show(
            "Mark spatial review complete? This confirms the generated parcel layers were reviewed in the ArcGIS Pro map and are ready for final transaction completion.",
            "Approve Spatial Review",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            workflowSession.SetValidationFailure("Spatial review approval cancelled.");
            RefreshWorkflowProperties();
            return;
        }

        workflowSession.ApproveSpatialReview(Environment.UserName);
        RefreshWorkflowProperties();
    }

    private void TogglePreflightDetails()
    {
        preflightDetailsExpanded = !preflightDetailsExpanded;
        RefreshWorkflowProperties();
    }

    private void ToggleOutputPreview()
    {
        outputPreviewExpanded = !outputPreviewExpanded;
        RefreshWorkflowProperties();
    }

    private IReadOnlyList<AvailableArtifact> ResolveOutputArtifacts()
    {
        var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "output_summary.json",
            "extracted_geometry.geojson"
        };

        return AvailableArtifacts
            .Where(artifact =>
                allowedNames.Contains(artifact.ArtifactName)
                || artifact.ArtifactName.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase)
                || artifact.Path.Contains($"{Path.DirectorySeparatorChar}output{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .GroupBy(artifact => artifact.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private string BuildOutputPreviewBodyText()
    {
        var summary = workflowSession.CurrentOutputSummary;
        if (summary is null)
        {
            return "Generated output artifacts are available for inspection before final transaction completion.";
        }

        var workspaceMode = string.Equals(summary.Payload.ReviewWorkspaceMode, Innola.InnolaTransactionSettings.ReviewWorkspaceModeParcelFabricLegacy, StringComparison.OrdinalIgnoreCase)
            ? string.Equals(summary.Payload.ParcelFabricMode, "true", StringComparison.OrdinalIgnoreCase)
                ? "Parcel Fabric review workspace"
                : "Parcel Fabric pilot review workspace"
            : "local transaction workspace";
        var builtSummary = string.Equals(summary.Payload.ParcelFabricMode, "true", StringComparison.OrdinalIgnoreCase)
            ? $" Built fabric content: {summary.Payload.BuiltPointCount} point(s), {summary.Payload.BuiltLineCount} line(s), and {summary.Payload.BuiltParcelCount} parcel polygon(s)."
            : string.Empty;
        return $"{summary.Payload.PointCount} point(s), {summary.Payload.LineCount} line(s), and {summary.Payload.PolygonCount} polygon feature(s) were generated in the {workspaceMode}.{builtSummary}";
    }

    private void ToggleReviewDetails()
    {
        reviewDetailsExpanded = !reviewDetailsExpanded;
        RefreshWorkflowProperties();
    }

    private void ReloadReviewViewer()
    {
        reviewViewerReloadVersion++;
        reviewViewerStateCacheKey = null;
        RefreshWorkflowProperties();
    }

    private void ToggleReviewViewerFit()
    {
        reviewViewerFitToPane = !reviewViewerFitToPane;
        RefreshWorkflowProperties();
    }

    private void OpenReviewSource()
    {
        if (SelectedReviewSource is { } source)
        {
            ExecuteSourceFileAction(source, SourceFileAction.Open);
        }
    }

    private void RevealReviewSource()
    {
        if (SelectedReviewSource is { } source)
        {
            ExecuteSourceFileAction(source, SourceFileAction.Reveal);
        }
    }

    private async Task OpenExperimentalReviewWorkspaceAsync()
    {
        var loaded = await EnsureExtractionReviewLoadedAsync().ConfigureAwait(true);
        if (!loaded)
        {
            RefreshWorkflowProperties();
            return;
        }

        if (experimentalReviewWorkspaceWindow is not null)
        {
            experimentalReviewWorkspaceWindow.Activate();
            experimentalReviewWorkspaceWindow.Focus();
            workflowSession.SetValidationFailure("Experimental Jamaica review workspace is already open.");
            RefreshWorkflowProperties();
            return;
        }

        var viewModel = new JamaicaReviewWorkspaceViewModel(this);
        experimentalReviewWorkspaceWindow = new JamaicaReviewWorkspaceWindow(viewModel)
        {
            Owner = FrameworkApplication.Current.MainWindow
        };
        experimentalReviewWorkspaceWindow.Closed += (_, _) => experimentalReviewWorkspaceWindow = null;
        experimentalReviewWorkspaceWindow.Show();
        workflowSession.SetValidationFailure("Experimental Jamaica review workspace opened with the current case artifacts.");
        RefreshWorkflowProperties();
    }

    private string? SelectExtractionReviewArtifact(CaseFolderLayout layout)
    {
        var preferredPaths = new[]
        {
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(layout.WorkingDirectory, "approved_review.json"),
            Path.Combine(layout.WorkingDirectory, "extraction_review.geojson"),
            Path.Combine(layout.WorkingDirectory, "extraction_points.json"),
            Path.Combine(layout.WorkingDirectory, "normalized_points.json"),
            Path.Combine(layout.WorkingDirectory, "plan_ocr.json"),
            Path.Combine(layout.WorkingDirectory, "dwg_context.json")
        };

        var preferredMatch = preferredPaths.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(preferredMatch))
        {
            return preferredMatch;
        }

        return workflowSession.AvailableArtifacts
            .Where(artifact => IsExtractionReviewArtifact(artifact.Path))
            .Select(artifact => artifact.Path)
            .FirstOrDefault(File.Exists);
    }

    private static bool IsExtractionReviewArtifact(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals("extraction_review_data.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("approved_review.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("extraction_review.geojson", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("extraction_points.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("normalized_points.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("plan_ocr.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("dwg_context.json", StringComparison.OrdinalIgnoreCase);
    }

    private SourceFileListItem? ResolveReviewSource()
    {
        if (SourceFiles.Count == 0)
        {
            return null;
        }

        var resolved = ReviewSourceSelectionResolver.Resolve(
            SourceFiles.Select(item => item.SourceFile).ToArray(),
            selectedReviewSourceCopiedPath);

        if (resolved is null)
        {
            return null;
        }

        return SourceFiles.FirstOrDefault(item =>
            string.Equals(item.SourceFile.CopiedPath, resolved.CopiedPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SourceFile.FileName, resolved.FileName, StringComparison.OrdinalIgnoreCase))
            ?? SourceFiles.FirstOrDefault(item => string.Equals(item.SourceFile.FileName, resolved.FileName, StringComparison.OrdinalIgnoreCase))
            ?? SourceFiles.FirstOrDefault();
    }

    private void RefreshReviewViewerState()
    {
        var sourceFile = SelectedReviewSource?.SourceFile;
        var pdfViewerMode = InnolaTransactionSettings.Load().PdfViewerMode;
        var projected = ReviewSourceViewerStateProjector.Build(sourceFile, pdfViewerMode);
        var cacheKey = $"{projected.Mode}|{projected.FullPath}|{reviewViewerReloadVersion}";
        if (string.Equals(reviewViewerStateCacheKey, cacheKey, StringComparison.Ordinal))
        {
            return;
        }

        reviewViewerStateCacheKey = cacheKey;
        reviewViewerState = projected;
        reviewViewerImageSource = null;

        if (!reviewViewerState.UsesImage || string.IsNullOrWhiteSpace(reviewViewerState.FullPath))
        {
            return;
        }

        try
        {
            reviewViewerImageSource = LoadReviewViewerImage(reviewViewerState.FullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            reviewViewerState = ReviewSourceViewerStateProjector.BuildRenderFailure(sourceFile, exception.Message);
        }
    }

    private static BitmapSource LoadReviewViewerImage(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault()
            ?? throw new FileFormatException("The image file does not contain a readable frame.");
        frame.Freeze();
        return frame;
    }

    public async Task StartOrClaimTransactionAsync()
    {
        var result = await ShellState.LifecycleCoordinator.StartOrClaimAsync();
        if (!result.Success)
        {
            workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not start transaction. Try again.");
        }

        RefreshWorkflowProperties();
    }

    public async Task SuspendTransactionAsync()
    {
        if (MessageBox.Show(
                "Suspend this case and save the current state back to Innola so it can be resumed later?",
                "Suspend Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await ShellState.LifecycleCoordinator.SaveAndCloseAsync();
        if (result.Success)
        {
            ResetWorkflowView(result.StatusMessage ?? "Transaction suspended.");
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not suspend transaction. Try again.");
        RefreshWorkflowProperties();
    }

    public async Task SaveProgressAsync()
    {
        var result = await ShellState.LifecycleCoordinator.SaveProgressAsync();
        if (!result.Success)
        {
            workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not save progress. Try again.");
        }

        RefreshWorkflowProperties();
    }

    public void CancelProcess()
    {
        if (MessageBox.Show(
                "Discard the current local session and close this transaction without creating a new resume package?",
                "Cancel Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var result = ShellState.LifecycleCoordinator.CancelActiveProcess();
        if (result.Success)
        {
            ResetWorkflowView(result.StatusMessage ?? "Cancelled locally.");
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not cancel the current process.");
        RefreshWorkflowProperties();
    }

    public async Task CompleteTransactionAsync()
    {
        if (MessageBox.Show(
                "Approve this transaction, publish the completed review for shared visibility, upload the completed package to Innola, and mark the task complete?",
                "Approve Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var publishResult = await workflowSession.PublishEnterpriseWorkingReviewAsync(Environment.UserName);
        if (publishResult.Attempted && !publishResult.Success)
        {
            RefreshWorkflowProperties();
            return;
        }

        var result = await ShellState.LifecycleCoordinator.CompleteAsync();
        if (result.Success)
        {
            ResetWorkflowView(result.StatusMessage ?? "Completed. Final package uploaded and transaction closed.");
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Complete is blocked.");
        RefreshWorkflowProperties();
    }

    protected override void OnShow(bool isVisible)
    {
        base.OnShow(isVisible);
        if (isVisible)
        {
            SyncLoadedCaseFolder();
        }
    }

    private void AddSourceFilesFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Supported source files (*.pdf;*.dwg;*.txt;*.csv;*.tif;*.tiff;*.png;*.jpg;*.jpeg)|*.pdf;*.dwg;*.txt;*.csv;*.tif;*.tiff;*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddSourceFiles(dialog.FileNames);
        }
    }

    private void BrowseOutputLocation()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Case Folder Output Location"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputLocation = dialog.FolderName;
        }
    }

    private void ReopenCaseFromDialog()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Reopen Case Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            ReopenCaseFolder(dialog.FolderName);
        }
    }

    private void ExecuteSourceFileAction(object? parameter, SourceFileAction action)
    {
        if (!CanUseWorkflowActions || parameter is not SourceFileListItem sourceFile)
        {
            return;
        }

        workflowSession.ExecuteSourceFileAction(sourceFile.SourceFile, action, Environment.UserName);
        RefreshWorkflowProperties();
    }

    private bool CanExecuteSourceFileAction(object? parameter)
    {
        return CanUseWorkflowActions && parameter is SourceFileListItem { SourceFile: { Copied: true, CopiedPath: not null } };
    }

    private static string GetStepStateForIntake(WorkflowState state)
    {
        return state == WorkflowState.NoCase ? "pending" : "done";
    }

    private static string GetStepStateForIntake(WorkflowState state, bool intakeReadyForPreflight)
    {
        return state switch
        {
            WorkflowState.NoCase => "pending",
            WorkflowState.Intake when intakeReadyForPreflight => "done",
            WorkflowState.Intake => "active",
            WorkflowState.PreflightRunning => "done",
            WorkflowState.PreflightBlocked => "done",
            WorkflowState.PreflightPassed => "done",
            WorkflowState.ExtractionRunning => "done",
            WorkflowState.ExtractionFailed => "done",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewApproved => "done",
            WorkflowState.ValidationRunning => "done",
            WorkflowState.ValidationBlocked => "done",
            WorkflowState.ValidationPassed => "done",
            WorkflowState.OutputRunning => "done",
            WorkflowState.OutputCreated => "done",
            WorkflowState.SpatialReviewPending => "done",
            WorkflowState.SpatialReviewApproved => "done",
            _ => "pending"
        };
    }

    private static string GetStepStateForPreflight(WorkflowState state, bool intakeReadyForPreflight)
    {
        return state switch
        {
            WorkflowState.NoCase => "pending",
            WorkflowState.Intake when intakeReadyForPreflight => "active",
            WorkflowState.Intake => "active",
            WorkflowState.PreflightRunning => "active",
            WorkflowState.PreflightBlocked => "blocked",
            WorkflowState.PreflightPassed => "done",
            WorkflowState.ExtractionRunning => "done",
            WorkflowState.ExtractionFailed => "done",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewApproved => "done",
            WorkflowState.ValidationRunning => "done",
            WorkflowState.ValidationBlocked => "done",
            WorkflowState.ValidationPassed => "done",
            WorkflowState.OutputRunning => "done",
            WorkflowState.OutputCreated => "done",
            WorkflowState.SpatialReviewPending => "done",
            WorkflowState.SpatialReviewApproved => "done",
            _ => "pending"
        };
    }

    private static string GetStepStateForExtractionReview(WorkflowState state, bool hasReviewArtifact)
    {
        return state switch
        {
            WorkflowState.NoCase => "pending",
            WorkflowState.Intake => "pending",
            WorkflowState.PreflightRunning => "pending",
            WorkflowState.PreflightBlocked => "blocked",
            WorkflowState.ExtractionRunning => "active",
            WorkflowState.ExtractionFailed => "blocked",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewApproved => "done",
            WorkflowState.ValidationRunning => "done",
            WorkflowState.ValidationBlocked => "done",
            WorkflowState.ValidationPassed => "done",
            WorkflowState.OutputRunning => "done",
            WorkflowState.OutputCreated => "done",
            WorkflowState.SpatialReviewPending => "done",
            WorkflowState.SpatialReviewApproved => "done",
            WorkflowState.PreflightPassed when hasReviewArtifact => "done",
            WorkflowState.PreflightPassed => "active",
            _ => "pending"
        };
    }

    private static string GetStepStateForValidation(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.ValidationRunning => "active",
            WorkflowState.ValidationBlocked => "blocked",
            WorkflowState.ValidationPassed => "done",
            WorkflowState.OutputRunning => "done",
            WorkflowState.OutputCreated => "done",
            WorkflowState.SpatialReviewPending => "done",
            WorkflowState.SpatialReviewApproved => "done",
            WorkflowState.ReviewApproved => "active",
            WorkflowState.NoCase or WorkflowState.Intake or WorkflowState.PreflightRunning or WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed or WorkflowState.ExtractionRunning or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending => "pending",
            _ => "pending"
        };
    }

    private static string GetStepStateForOutputs(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.OutputRunning => "active",
            WorkflowState.OutputCreated => "done",
            WorkflowState.SpatialReviewPending => "done",
            WorkflowState.SpatialReviewApproved => "done",
            WorkflowState.ValidationPassed => "active",
            WorkflowState.ValidationBlocked => "pending",
            _ => "pending"
        };
    }

    private static string GetStepStateForSpatialReview(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.OutputCreated => "active",
            WorkflowState.SpatialReviewPending => "active",
            WorkflowState.SpatialReviewApproved => "done",
            _ => "pending"
        };
    }

    private static string GetStepStateForReadyToComplete(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.SpatialReviewApproved => "active",
            _ => "pending"
        };
    }

    private static bool HasExtractionReviewArtifact(WorkflowSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaseFolderPath))
        {
            return false;
        }

        var layout = CaseFolderLayout.FromRootDirectory(session.CaseFolderPath);
        var candidates = new[]
        {
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(layout.WorkingDirectory, "approved_review.json"),
            Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson"),
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            Path.Combine(layout.LogsDirectory, "process.log")
        };

        return candidates.Any(File.Exists);
    }

    private IReadOnlyList<WorkflowLifecycleStep> BuildWorkflowSteps()
    {
        var currentState = workflowSession.CurrentState;
        var intakeState = GetStepStateForIntake(currentState, IntakeReadyForPreflight);
        var preflightState = GetStepStateForPreflight(currentState, IntakeReadyForPreflight);
        var extractionState = GetStepStateForExtractionReview(currentState, HasExtractionArtifact);
        var validationState = GetStepStateForValidation(currentState);
        var outputState = GetStepStateForOutputs(currentState);
        var spatialReviewState = GetStepStateForSpatialReview(currentState);
        var readyState = GetStepStateForReadyToComplete(currentState);

        return new WorkflowLifecycleStep[]
        {
            new WorkflowLifecycleStep("Sources", intakeState, GetLifecycleStepIcon(intakeState)),
            new WorkflowLifecycleStep("Checks", preflightState, GetLifecycleStepIcon(preflightState)),
            new WorkflowLifecycleStep("Point Review", extractionState, GetLifecycleStepIcon(extractionState)),
            new WorkflowLifecycleStep("Quality", validationState, GetLifecycleStepIcon(validationState)),
            new WorkflowLifecycleStep("Outputs", outputState, GetLifecycleStepIcon(outputState)),
            new WorkflowLifecycleStep("Map Review", spatialReviewState, GetLifecycleStepIcon(spatialReviewState)),
            new WorkflowLifecycleStep("Finalize", readyState, GetLifecycleStepIcon(readyState))
        };
    }

    private static bool IsReviewLockedState(WorkflowState state)
    {
        return state is WorkflowState.ReviewApproved or WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed or WorkflowState.OutputRunning or WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;
    }

    private static string GetLifecycleStepIcon(string state) =>
        state switch
        {
            "done" => "✔",
            "active" => "◐",
            "blocked" => "⚠",
            "pending" => "○",
            _ => "—"
        };

    private static string GetWorkspaceStageLabel(WorkflowWorkspaceStage stage) =>
        stage switch
        {
            WorkflowWorkspaceStage.Intake => "Transaction Sources",
            WorkflowWorkspaceStage.Preflight => "Processing Checks",
            WorkflowWorkspaceStage.ExtractionReview => "Review Extracted Points",
            WorkflowWorkspaceStage.Validation => "Quality Check",
            WorkflowWorkspaceStage.Outputs => "Create Spatial Outputs",
            WorkflowWorkspaceStage.SpatialReview => "Map Review",
            WorkflowWorkspaceStage.ReadyToComplete => "Finalize Case",
            _ => "Transaction Sources"
        };

    private string? ResolveSelectedTransactionType()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath))
            {
                var layout = CaseFolderLayout.FromRootDirectory(workflowSession.CaseFolderPath);
                if (File.Exists(layout.ManifestPath))
                {
                    var manifest = ManifestSerializer.Read(layout.ManifestPath);
                    var transactionStage = manifest.Payload.InnolaTransaction?.TaskName;
                    if (!string.IsNullOrWhiteSpace(transactionStage))
                    {
                        return transactionStage.Trim();
                    }

                    var caseType = manifest.Payload.InnolaTransaction?.CaseType;
                    if (!string.IsNullOrWhiteSpace(caseType))
                    {
                        return caseType.Trim();
                    }
                }
            }
        }
        catch (Exception)
        {
            // Keep UI resilient if manifest is temporarily unavailable while loading.
        }

        if (ShellState.Session.SelectedTransaction is not null && !string.IsNullOrWhiteSpace(ShellState.Session.SelectedTransaction.TaskName))
        {
            return ShellState.Session.SelectedTransaction.TaskName.Trim();
        }

        if (ShellState.Session.SelectedTransaction is not null && !string.IsNullOrWhiteSpace(ShellState.Session.SelectedTransaction.TransactionType))
        {
            return ShellState.Session.SelectedTransaction.TransactionType.Trim();
        }

        return TransactionId is null ? "not available" : null;
    }

    private void RefreshWorkflowProperties()
    {
        RefreshReviewViewerState();
        NotifyPropertyChanged(nameof(TransactionId));
        NotifyPropertyChanged(nameof(OutputLocation));
        NotifyPropertyChanged(nameof(CurrentWorkflowState));
        NotifyPropertyChanged(nameof(CurrentStep));
        NotifyPropertyChanged(nameof(StatusText));
        NotifyPropertyChanged(nameof(LifecycleStatusText));
        NotifyPropertyChanged(nameof(HeaderTransactionText));
        NotifyPropertyChanged(nameof(HeaderTaskNameText));
        NotifyPropertyChanged(nameof(CurrentStepBadge));
        NotifyPropertyChanged(nameof(ScoreBadge));
        NotifyPropertyChanged(nameof(ModeBadge));
        NotifyPropertyChanged(nameof(HasActiveCase));
        NotifyPropertyChanged(nameof(HasExtractionArtifact));
        NotifyPropertyChanged(nameof(ActiveWorkspaceStage));
        NotifyPropertyChanged(nameof(IsIntakeStageActive));
        NotifyPropertyChanged(nameof(IsPreflightStageActive));
        NotifyPropertyChanged(nameof(IsExtractionReviewStageActive));
        NotifyPropertyChanged(nameof(IsValidationStageActive));
        NotifyPropertyChanged(nameof(IsOutputsStageActive));
        NotifyPropertyChanged(nameof(IsSpatialReviewStageActive));
        NotifyPropertyChanged(nameof(HasReadyToCompleteStage));
        NotifyPropertyChanged(nameof(ShowIntakeSummary));
        NotifyPropertyChanged(nameof(ShowPreflightSummary));
        NotifyPropertyChanged(nameof(ShowExtractionSummary));
        NotifyPropertyChanged(nameof(ShowValidationSummaryCard));
        NotifyPropertyChanged(nameof(ShowOutputsSummary));
        NotifyPropertyChanged(nameof(SourceFiles));
        NotifyPropertyChanged(nameof(SourceIntakeBadge));
        NotifyPropertyChanged(nameof(IntakeSummaryText));
        NotifyPropertyChanged(nameof(IntakeDetailText));
        NotifyPropertyChanged(nameof(IntakeSummaryExpanded));
        NotifyPropertyChanged(nameof(ExtractionReviewBadge));
        NotifyPropertyChanged(nameof(ExtractionReviewActionLabel));
        NotifyPropertyChanged(nameof(ExtractionReviewHelpText));
        NotifyPropertyChanged(nameof(HasLoadedReviewData));
        NotifyPropertyChanged(nameof(ReviewSummary));
        NotifyPropertyChanged(nameof(ReviewSummaryText));
        NotifyPropertyChanged(nameof(ReviewHasBlockers));
        NotifyPropertyChanged(nameof(ReviewGateText));
        NotifyPropertyChanged(nameof(ReviewBadgeText));
        NotifyPropertyChanged(nameof(ExtractionSummaryExpanded));
        NotifyPropertyChanged(nameof(IsReviewApproved));
        NotifyPropertyChanged(nameof(IsReviewLocked));
        NotifyPropertyChanged(nameof(ReviewDetailsExpanded));
        NotifyPropertyChanged(nameof(ReviewDetailsToggleText));
        NotifyPropertyChanged(nameof(CanUseWorkflowActions));
        NotifyPropertyChanged(nameof(CanRunPreflight));
        NotifyPropertyChanged(nameof(CanRunExtractionReview));
        NotifyPropertyChanged(nameof(CanOpenExperimentalReviewWorkspace));
        NotifyPropertyChanged(nameof(CanRunValidation));
        NotifyPropertyChanged(nameof(CanRunOutputs));
        NotifyPropertyChanged(nameof(CanLoadSpatialReviewLayers));
        NotifyPropertyChanged(nameof(CanApproveSpatialReview));
        NotifyPropertyChanged(nameof(CanCompleteTransaction));
        NotifyPropertyChanged(nameof(WorkflowSteps));
        NotifyPropertyChanged(nameof(DetectedProfileLabel));
        NotifyPropertyChanged(nameof(IntakeIssues));
        NotifyPropertyChanged(nameof(AvailableArtifacts));
        NotifyPropertyChanged(nameof(PreflightBlockers));
        NotifyPropertyChanged(nameof(PreflightWarnings));
        NotifyPropertyChanged(nameof(PreflightPassedChecks));
        NotifyPropertyChanged(nameof(PreflightResults));
        NotifyPropertyChanged(nameof(HasPreflightResults));
        NotifyPropertyChanged(nameof(PreflightBadge));
        NotifyPropertyChanged(nameof(PreflightDetailsExpanded));
        NotifyPropertyChanged(nameof(PreflightToggleText));
        NotifyPropertyChanged(nameof(PreflightSummaryText));
        NotifyPropertyChanged(nameof(PreflightCollapsedHint));
        NotifyPropertyChanged(nameof(PreflightSummaryExpanded));
        NotifyPropertyChanged(nameof(SelectedReviewRow));
        NotifyPropertyChanged(nameof(SelectedReviewSource));
        NotifyPropertyChanged(nameof(ReviewSourceOptions));
        NotifyPropertyChanged(nameof(SelectedReviewSourceTitle));
        NotifyPropertyChanged(nameof(SelectedReviewSourcePath));
        NotifyPropertyChanged(nameof(SelectedReviewSourceMode));
        NotifyPropertyChanged(nameof(SelectedReviewSourceGuidance));
        NotifyPropertyChanged(nameof(ReviewViewerState));
        NotifyPropertyChanged(nameof(ReviewViewerFileTitle));
        NotifyPropertyChanged(nameof(ReviewViewerRoleLabel));
        NotifyPropertyChanged(nameof(ReviewViewerDisplayPath));
        NotifyPropertyChanged(nameof(ReviewViewerModeLabel));
        NotifyPropertyChanged(nameof(ReviewViewerLoadState));
        NotifyPropertyChanged(nameof(ReviewViewerGuidance));
        NotifyPropertyChanged(nameof(ReviewViewerFallbackMessage));
        NotifyPropertyChanged(nameof(ReviewViewerUsesImage));
        NotifyPropertyChanged(nameof(ReviewViewerUsesBrowser));
        NotifyPropertyChanged(nameof(ReviewViewerShowsFallback));
        NotifyPropertyChanged(nameof(CanToggleReviewViewerFit));
        NotifyPropertyChanged(nameof(ReviewViewerFitToggleText));
        NotifyPropertyChanged(nameof(ReviewViewerImageStretch));
        NotifyPropertyChanged(nameof(ReviewViewerImageSource));
        NotifyPropertyChanged(nameof(ReviewViewerBrowserUri));
        NotifyPropertyChanged(nameof(ReviewViewerNavigationKey));
        NotifyPropertyChanged(nameof(SelectedReviewRowDetailsTitle));
        NotifyPropertyChanged(nameof(SelectedReviewRowDetailsText));
        NotifyPropertyChanged(nameof(ReviewWorkspaceTitle));
        NotifyPropertyChanged(nameof(OutputPreviewExpanded));
        NotifyPropertyChanged(nameof(OutputPreviewToggleText));
        NotifyPropertyChanged(nameof(OutputArtifacts));
        NotifyPropertyChanged(nameof(OutputPreviewSummaryText));
        NotifyPropertyChanged(nameof(HasOutputArtifacts));
        NotifyPropertyChanged(nameof(OutputPreviewBodyText));
        NotifyPropertyChanged(nameof(OutputsSummaryExpanded));
        NotifyPropertyChanged(nameof(ValidationBadge));
        NotifyPropertyChanged(nameof(ValidationSummaryText));
        NotifyPropertyChanged(nameof(ValidationHelpText));
        NotifyPropertyChanged(nameof(ValidationSummaryExpanded));
        NotifyPropertyChanged(nameof(OutputBadge));
        NotifyPropertyChanged(nameof(SpatialReviewBadge));
        NotifyPropertyChanged(nameof(SpatialReviewSummaryText));
        NotifyPropertyChanged(nameof(SpatialReviewHelpText));
        NotifyPropertyChanged(nameof(ReadyToCompleteBadge));
        NotifyPropertyChanged(nameof(ReadyToCompleteSummaryText));
        NotifyPropertyChanged(nameof(ReadyToCompleteHelpText));
        createCaseCommand.RaiseCanExecuteChanged();
        browseOutputLocationCommand.RaiseCanExecuteChanged();
        addSourceFilesCommand.RaiseCanExecuteChanged();
        refreshInputProfileCommand.RaiseCanExecuteChanged();
        reopenCaseCommand.RaiseCanExecuteChanged();
        openSourceFileCommand.RaiseCanExecuteChanged();
        revealSourceFileCommand.RaiseCanExecuteChanged();
        routeSourceFileToMapCommand.RaiseCanExecuteChanged();
        runPreflightCommand.RaiseCanExecuteChanged();
        runExtractionReviewCommand.RaiseCanExecuteChanged();
        runValidationCommand.RaiseCanExecuteChanged();
        runOutputsCommand.RaiseCanExecuteChanged();
        loadSpatialReviewLayersCommand.RaiseCanExecuteChanged();
        approveSpatialReviewCommand.RaiseCanExecuteChanged();
        addManualPointCommand.RaiseCanExecuteChanged();
        saveReviewCommand.RaiseCanExecuteChanged();
        approveReviewCommand.RaiseCanExecuteChanged();
        togglePreflightDetailsCommand.RaiseCanExecuteChanged();
        toggleOutputPreviewCommand.RaiseCanExecuteChanged();
        toggleReviewDetailsCommand.RaiseCanExecuteChanged();
        openReviewSourceCommand.RaiseCanExecuteChanged();
        revealReviewSourceCommand.RaiseCanExecuteChanged();
        reloadReviewViewerCommand.RaiseCanExecuteChanged();
        toggleReviewViewerFitCommand.RaiseCanExecuteChanged();
        openExperimentalReviewWorkspaceCommand.RaiseCanExecuteChanged();
        startOrClaimTransactionCommand.RaiseCanExecuteChanged();
        suspendTransactionCommand.RaiseCanExecuteChanged();
        cancelProcessCommand.RaiseCanExecuteChanged();
        completeTransactionCommand.RaiseCanExecuteChanged();
    }

    private void SyncLoadedCaseFolder()
    {
        var loadedCaseFolderPath = ShellState.Session.LoadedCaseFolderPath;
        if (string.IsNullOrWhiteSpace(loadedCaseFolderPath))
        {
            if (workflowSession.CurrentState != WorkflowState.NoCase
                || !string.IsNullOrWhiteSpace(transactionId)
                || !string.IsNullOrWhiteSpace(outputLocation))
            {
                ResetWorkflowView(ShellState.Session.LifecycleStatusText ?? "No active case");
            }

            return;
        }

        if (workflowSession.CaseFolderPath?.Equals(loadedCaseFolderPath, StringComparison.OrdinalIgnoreCase) == true)
        {
            RefreshWorkflowProperties();
            return;
        }

        workflowSession.ReopenCaseFolder(loadedCaseFolderPath);
        transactionId = workflowSession.TransactionId;
        outputLocation = System.IO.Path.GetDirectoryName(loadedCaseFolderPath);
        RefreshWorkflowProperties();
    }

    private void ResetWorkflowView(string statusText)
    {
        workflowSession.ResetToDefault(statusText);
        transactionId = null;
        outputLocation = null;
        loadedReviewDocument = null;
        selectedReviewRow = null;
        preflightDetailsExpanded = false;
        outputPreviewExpanded = false;
        reviewDetailsExpanded = true;
        intakeSummaryExpanded = false;
        preflightSummaryExpanded = false;
        extractionSummaryExpanded = false;
        validationSummaryExpanded = false;
        outputsSummaryExpanded = false;
        reviewViewerFitToPane = true;
        reviewDirty = false;
        reviewViewerReloadVersion = 0;
        selectedReviewSourceCopiedPath = null;
        reviewViewerStateCacheKey = null;
        reviewViewerImageSource = null;
        reviewViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
        ReviewRows.Clear();
        RefreshWorkflowProperties();
    }

    private static string BlankIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static string? FormatBadgeDateTime(string? isoDateTime)
    {
        if (string.IsNullOrWhiteSpace(isoDateTime))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(isoDateTime, out var parsed))
        {
            return isoDateTime;
        }

        return parsed.ToLocalTime().ToString("MM/dd/yyyy HH:mm");
    }
}

internal sealed record SourceFileListItem(SourceFileCopyResult SourceFile)
{
    public string FileLabel => SourceFile.FileName;

    public string SourceRelativePath => $"source/{SourceFile.FileName}";

    public string RoleLabel => SourceFile.SourceRole switch
    {
        "computation_source" => "Computation",
        "points_computation" => "Points",
        "plan_map_reference" => "Plan",
        "dwg_reference" => "DWG",
        null or "" => "Source",
        _ => SourceFile.SourceRole.Replace("_", " ")
    };

    public string RowStatus => SourceFile.Copied ? "Copied" : SourceFile.Status;
}

internal sealed record WorkflowLifecycleStep(string Name, string State, string Icon);

internal sealed record PreflightResultListItem(string Result, PreflightCheck Check)
{
    public string CheckName => Humanize(Check.CheckId);

    public string Details => Check.Message;

    public string State => Result.ToLowerInvariant();

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Check";
        }

        var words = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
