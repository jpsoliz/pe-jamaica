using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Disposition;
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
using System.Threading;
using System.Globalization;
using System.Text.Json.Nodes;

namespace ParcelWorkflowAddIn;

internal sealed class ParcelWorkflowDockpaneViewModel : DockPane
{
    internal const string DockPaneId = "ParcelWorkflow_Dockpane";
    private readonly WorkflowSession workflowSession = new(new CaseFolderStore());
    private readonly PreflightRuleCatalog preflightRuleCatalog = new PreflightRuleCatalogLoader().Load();
    private readonly ExtractionReviewPersistenceService extractionReviewService = new();
    private readonly ParcelScopedManualPointService manualPointService = new();
    private readonly ManualBoundarySegmentService manualBoundarySegmentService = new();
    private readonly ParcelScopedReviewValidationService reviewValidationService = new();
    private readonly SurveyPlanBoundarySolver surveyPlanBoundarySolver = new();
    private readonly RelayCommand createCaseCommand;
    private readonly RelayCommand browseOutputLocationCommand;
    private readonly RelayCommand addSourceFilesCommand;
    private readonly RelayCommand refreshInputProfileCommand;
    private readonly RelayCommand reopenCaseCommand;
    private readonly RelayCommand openSourceFileCommand;
    private readonly RelayCommand revealSourceFileCommand;
    private readonly RelayCommand routeSourceFileToMapCommand;
    private readonly RelayCommand runPreflightCommand;
    private readonly RelayCommand runGeoreferenceCheckCommand;
    private readonly RelayCommand runDimensionCheckCommand;
    private readonly RelayCommand runExtractionReviewCommand;
    private readonly RelayCommand reprocessExtractionReviewCommand;
    private readonly RelayCommand useManualCogoFallbackCommand;
    private readonly RelayCommand runValidationCommand;
    private readonly RelayCommand runOutputsCommand;
    private readonly RelayCommand loadSpatialReviewLayersCommand;
    private readonly RelayCommand openCogoReaderCommand;
    private readonly RelayCommand approveSpatialReviewCommand;
    private readonly RelayCommand addManualPointCommand;
    private readonly RelayCommand editReviewPointCommand;
    private readonly RelayCommand addReviewSegmentCommand;
    private readonly RelayCommand editReviewSegmentCommand;
    private readonly RelayCommand excludeReviewSegmentCommand;
    private readonly RelayCommand rebuildBoundaryPointsCommand;
    private readonly RelayCommand removeManualPointCommand;
    private readonly RelayCommand cancelPendingManualPointCommand;
    private readonly RelayCommand saveReviewCommand;
    private readonly RelayCommand approveReviewCommand;
    private readonly RelayCommand togglePreflightDetailsCommand;
    private readonly RelayCommand toggleOutputPreviewCommand;
    private readonly RelayCommand toggleReviewDetailsCommand;
    private readonly RelayCommand openReviewSourceCommand;
    private readonly RelayCommand revealReviewSourceCommand;
    private readonly RelayCommand reloadReviewViewerCommand;
    private readonly RelayCommand toggleReviewViewerFitCommand;
    private readonly RelayCommand zoomInReviewViewerCommand;
    private readonly RelayCommand zoomOutReviewViewerCommand;
    private readonly RelayCommand previousReviewViewerPageCommand;
    private readonly RelayCommand nextReviewViewerPageCommand;
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
    private double reviewViewerZoom = 1.0d;
    private bool reviewDirty;
    private string? pendingManualRowId;
    private string? activeReviewParcelGroupId;
    private string? activeReviewParcelName;
    private string? activeReviewTraverseId;
    private bool pointEditorOpen;
    private int reviewContentVersion;
    private int reviewViewerReloadVersion;
    private int reviewViewerPageIndex;
    private int reviewViewerPageCount;
    private string? selectedReviewSourceCopiedPath;
    private string? reviewViewerStateCacheKey;
    private BitmapSource? reviewViewerImageSource;
    private readonly RenderedReviewDocumentService renderedReviewDocumentService = new();
    private CancellationTokenSource? reviewViewerLoadCancellation;
    private ReviewSourceViewerState reviewViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
    private JamaicaReviewWorkspaceWindow? experimentalReviewWorkspaceWindow;
    private IReadOnlyList<SourceFileListItem> sourceFileItems = Array.Empty<SourceFileListItem>();
    private bool importStructuredSurveyPoints;
    private bool importAutoCadSurveySource;

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
        runGeoreferenceCheckCommand = new RelayCommand(async () => await RunGeoreferenceCheckAsync(), () => CanRunGeoreferenceCheck);
        runDimensionCheckCommand = new RelayCommand(async () => await RunDimensionCheckAsync(), () => CanRunDimensionCheck);
        runExtractionReviewCommand = new RelayCommand(async () => await RunOrOpenExtractionReviewAsync(), () => CanRunExtractionReview);
        reprocessExtractionReviewCommand = new RelayCommand(async () => await ReprocessExtractionReviewAsync(), () => CanReprocessExtractionReview);
        useManualCogoFallbackCommand = new RelayCommand(async () => await UseManualCogoFallbackAsync(), () => CanUseManualCogoFallback);
        runValidationCommand = new RelayCommand(async () => await RunValidationAsync(), () => CanRunValidation);
        runOutputsCommand = new RelayCommand(async () => await RunOutputsAsync(), () => CanRunOutputs);
        loadSpatialReviewLayersCommand = new RelayCommand(async () => await LoadSpatialReviewLayersAsync(), () => CanLoadSpatialReviewLayers);
        openCogoReaderCommand = new RelayCommand(async () => await OpenCogoReaderAsync(), () => CanOpenCogoReader);
        approveSpatialReviewCommand = new RelayCommand(ApproveSpatialReview, () => CanApproveSpatialReview);
        addManualPointCommand = new RelayCommand(AddManualPoint, () => HasLoadedReviewData && !IsReviewLocked && !IsManualReviewEditMode && !pointEditorOpen);
        editReviewPointCommand = new RelayCommand(EditSelectedReviewPoint, () => HasLoadedReviewData && !IsReviewLocked && SelectedReviewRow is not null && !IsManualReviewEditMode && !pointEditorOpen);
        addReviewSegmentCommand = new RelayCommand(AddReviewSegment, () => HasLoadedReviewData && !IsReviewLocked && IsPxaSurveyPlanReview && !pointEditorOpen);
        editReviewSegmentCommand = new RelayCommand(EditReviewSegment, parameter => HasLoadedReviewData && !IsReviewLocked && parameter is ExtractionReviewSegmentViewModel && !pointEditorOpen);
        excludeReviewSegmentCommand = new RelayCommand(ExcludeReviewSegment, parameter => HasLoadedReviewData && !IsReviewLocked && parameter is ExtractionReviewSegmentViewModel && !pointEditorOpen);
        rebuildBoundaryPointsCommand = new RelayCommand(RebuildBoundaryPoints, () => HasLoadedReviewData && !IsReviewLocked && IsPxaSurveyPlanReview && ReviewSegments.Any(segment => segment.IncludeInBoundary) && !pointEditorOpen);
        removeManualPointCommand = new RelayCommand(RemoveSelectedManualPoint, () => HasLoadedReviewData && !IsReviewLocked && SelectedReviewRow is not null && !pointEditorOpen);
        cancelPendingManualPointCommand = new RelayCommand(CancelPendingManualPointEdit, () => CanCancelPendingManualPointEdit);
        saveReviewCommand = new RelayCommand(SaveReviewChanges, () => CanSaveReviewChangesFromWorkspace);
        approveReviewCommand = new RelayCommand(ApproveReview, () => HasLoadedReviewData && ReviewRows.Count > 0 && !IsReviewLocked && !ReviewHasBlockers && !IsManualReviewEditMode);
        togglePreflightDetailsCommand = new RelayCommand(TogglePreflightDetails, () => HasPreflightResults);
        toggleOutputPreviewCommand = new RelayCommand(ToggleOutputPreview);
        toggleReviewDetailsCommand = new RelayCommand(ToggleReviewDetails, () => HasLoadedReviewData);
        openReviewSourceCommand = new RelayCommand(OpenReviewSource, () => SelectedReviewSource is not null);
        revealReviewSourceCommand = new RelayCommand(RevealReviewSource, () => SelectedReviewSource is not null);
        reloadReviewViewerCommand = new RelayCommand(ReloadReviewViewer, () => SelectedReviewSource is not null);
        toggleReviewViewerFitCommand = new RelayCommand(ToggleReviewViewerFit, () => CanToggleReviewViewerFit);
        zoomInReviewViewerCommand = new RelayCommand(ZoomInReviewViewer, () => CanZoomReviewViewerIn);
        zoomOutReviewViewerCommand = new RelayCommand(ZoomOutReviewViewer, () => CanZoomReviewViewerOut);
        previousReviewViewerPageCommand = new RelayCommand(() => ChangeReviewViewerPage(-1), () => CanGoToPreviousReviewViewerPage);
        nextReviewViewerPageCommand = new RelayCommand(() => ChangeReviewViewerPage(1), () => CanGoToNextReviewViewerPage);
        openExperimentalReviewWorkspaceCommand = new RelayCommand(async () => await OpenExperimentalReviewWorkspaceAsync(), () => CanOpenExperimentalReviewWorkspace);
        startOrClaimTransactionCommand = new RelayCommand(async () => await StartOrClaimTransactionAsync(), () => CanUseWorkflowActions && ShellState.Session.CanStartOrClaimTransaction);
        suspendTransactionCommand = new RelayCommand(async () => await SuspendTransactionAsync(), () => CanUseWorkflowActions && ShellState.Session.CanSaveProgress);
        cancelProcessCommand = new RelayCommand(async () => await CancelProcessAsync(), () => CanUseWorkflowActions && ShellState.Session.CanCancelActiveProcess);
        completeTransactionCommand = new RelayCommand(async () => await CompleteTransactionAsync(), () => CanCompleteTransaction);
        ShellState.Session.SessionChanged += (_, _) => SyncLoadedCaseFolder();
        SyncLoadedCaseFolder();
    }

    internal static void Show()
    {
        FrameworkApplication.DockPaneManager.Find(DockPaneId)?.Activate();
    }

    public ObservableCollection<ExtractionReviewRowViewModel> ReviewRows { get; } = [];

    public ObservableCollection<ExtractionReviewSegmentViewModel> ReviewSegments { get; } = [];

    public ObservableCollection<ExtractionReviewMetadataFieldViewModel> ReviewMetadataFields { get; } = [];

    public ObservableCollection<ExtractionReviewAdjacentOwnerViewModel> ReviewAdjacentOwners { get; } = [];

    public ObservableCollection<ExtractionReviewNamedPartyViewModel> ReviewNamedParties { get; } = [];

    public ObservableCollection<ExtractionReviewVolumeFolioViewModel> ReviewVolumeFolios { get; } = [];

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

    public string CurrentStepBadge => GetWorkspaceStageLabel(ActiveWorkspaceStage, CurrentWorkflowState);

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
                return "Files clear";
            }

            return "Files pending";
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

    public IReadOnlyList<SourceFileListItem> SourceFiles => sourceFileItems;

    public IReadOnlyList<SourceFileListItem> SupportingDocumentDownloads => BuildSupportingDocumentDownloads();

    public IReadOnlyList<SupportingDocumentStatusItem> SupportingDocumentInventory => BuildSupportingDocumentInventory();

    public bool HasStructuredSurveyPointsSource => SourceFiles.Any(item =>
        string.Equals(item.SourceFile.SourceType, "st_survey_points", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceFile.SourceRole, SourceRole.CoordinateTextSource, StringComparison.OrdinalIgnoreCase));

    public bool HasAutoCadSurveySource => SourceFiles.Any(item =>
        string.Equals(item.SourceFile.SourceType, "st_autocad_file", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.SourceFile.SourceRole, SourceRole.DwgSource, StringComparison.OrdinalIgnoreCase));

    public bool ImportStructuredSurveyPoints
    {
        get => importStructuredSurveyPoints;
        set
        {
            if (importStructuredSurveyPoints == value)
            {
                return;
            }

            var originalStructured = importStructuredSurveyPoints;
            var originalAutoCad = importAutoCadSurveySource;
            importStructuredSurveyPoints = value;
            if (!workflowSession.SaveSupportingDocumentOptions(importStructuredSurveyPoints, importAutoCadSurveySource))
            {
                importStructuredSurveyPoints = originalStructured;
                importAutoCadSurveySource = originalAutoCad;
            }

            NotifyPropertyChanged(nameof(ImportStructuredSurveyPoints));
            NotifyPropertyChanged(nameof(StatusText));
        }
    }

    public bool ImportAutoCadSurveySource
    {
        get => importAutoCadSurveySource;
        set
        {
            if (importAutoCadSurveySource == value)
            {
                return;
            }

            var originalStructured = importStructuredSurveyPoints;
            var originalAutoCad = importAutoCadSurveySource;
            importAutoCadSurveySource = value;
            if (!workflowSession.SaveSupportingDocumentOptions(importStructuredSurveyPoints, importAutoCadSurveySource))
            {
                importStructuredSurveyPoints = originalStructured;
                importAutoCadSurveySource = originalAutoCad;
            }

            NotifyPropertyChanged(nameof(ImportAutoCadSurveySource));
            NotifyPropertyChanged(nameof(StatusText));
        }
    }

    public IReadOnlyList<WorkflowLifecycleStep> WorkflowSteps => BuildWorkflowSteps();

    public WorkflowWorkspaceStage ActiveWorkspaceStage => ResolveActiveWorkspaceStage();

    public bool HasActiveCase => !string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath);

    public bool IntakeReadyForPreflight =>
        HasActiveCase
        && SourceFiles.Count > 0
        && !string.Equals(DetectedProfileLabel, "Detected profile: not refreshed", StringComparison.OrdinalIgnoreCase);

    public bool HasExtractionArtifact => HasExtractionReviewArtifact(workflowSession);

    public bool IsIntakeStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.Intake;

    public bool IsPreflightStageActive =>
        ActiveWorkspaceStage is WorkflowWorkspaceStage.Preflight
            or WorkflowWorkspaceStage.StructureCheck
            or WorkflowWorkspaceStage.GeoreferenceCheck
            or WorkflowWorkspaceStage.DimensionCheck;

    public bool IsStructureCheckStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.StructureCheck;

    public bool IsGeoreferenceCheckStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.GeoreferenceCheck;

    public bool IsDimensionCheckStageActive => ActiveWorkspaceStage == WorkflowWorkspaceStage.DimensionCheck;

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

    public bool CanUseWorkflowActions => ShellState.CanOpenComputeWorkflow;

    public bool CanRunPreflight => CanUseWorkflowActions && workflowSession.CanRunStructureCheck;

    public bool CanRunGeoreferenceCheck => CanUseWorkflowActions && workflowSession.CanRunGeoreferenceCheck;

    public bool CanRunDimensionCheck => CanUseWorkflowActions && workflowSession.CanRunDimensionCheck;

    public bool CanRunExtractionReview => CanUseWorkflowActions && workflowSession.CanRunExtractionReview;

    public bool CanReprocessExtractionReview =>
        CanUseWorkflowActions
        && workflowSession.CanRunExtractionReview
        && (HasLoadedReviewData || workflowSession.HasUsableExtractionReview || HasExtractionReviewArtifact(workflowSession));

    public bool CanUseManualCogoFallback => CanUseWorkflowActions && workflowSession.CanChooseManualCogoReview && !IsReviewApproved;

    public bool CanRunOutputs => CanUseWorkflowActions && workflowSession.CanRunOutputs;

    public bool CanLoadSpatialReviewLayers =>
        CanUseWorkflowActions
        && workflowSession.CurrentOutputSummary is not null
        && CurrentWorkflowState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;

    public bool CanApproveSpatialReview => CanUseWorkflowActions && workflowSession.CanApproveSpatialReview;

    public bool CanCompleteTransaction => CanUseWorkflowActions && ShellState.Session.CanCompleteTransaction && workflowSession.CurrentState == WorkflowState.SpatialReviewApproved;

    public ICommand CreateCaseCommand => createCaseCommand;

    public ICommand BrowseOutputLocationCommand => browseOutputLocationCommand;

    public ICommand AddSourceFilesCommand => addSourceFilesCommand;

    public ICommand RefreshInputProfileCommand => refreshInputProfileCommand;

    public ICommand ReopenCaseCommand => reopenCaseCommand;

    public ICommand OpenSourceFileCommand => openSourceFileCommand;

    public ICommand RevealSourceFileCommand => revealSourceFileCommand;

    public ICommand RouteSourceFileToMapCommand => routeSourceFileToMapCommand;

    public ICommand RunPreflightCommand => runPreflightCommand;

    public ICommand RunGeoreferenceCheckCommand => runGeoreferenceCheckCommand;

    public ICommand RunDimensionCheckCommand => runDimensionCheckCommand;

    public ICommand RunExtractionReviewCommand => runExtractionReviewCommand;

    public ICommand ReprocessExtractionReviewCommand => reprocessExtractionReviewCommand;

    public ICommand UseManualCogoFallbackCommand => useManualCogoFallbackCommand;

    public ICommand RunValidationCommand => runValidationCommand;

    public ICommand RunOutputsCommand => runOutputsCommand;

    public ICommand LoadSpatialReviewLayersCommand => loadSpatialReviewLayersCommand;

    public ICommand OpenCogoReaderCommand => openCogoReaderCommand;

    public ICommand ApproveSpatialReviewCommand => approveSpatialReviewCommand;

    public ICommand AddManualPointCommand => addManualPointCommand;

    public ICommand EditReviewPointCommand => editReviewPointCommand;

    public ICommand AddReviewSegmentCommand => addReviewSegmentCommand;

    public ICommand EditReviewSegmentCommand => editReviewSegmentCommand;

    public ICommand ExcludeReviewSegmentCommand => excludeReviewSegmentCommand;

    public ICommand RebuildBoundaryPointsCommand => rebuildBoundaryPointsCommand;

    public ICommand RemoveManualPointCommand => removeManualPointCommand;

    public ICommand CancelPendingManualPointCommand => cancelPendingManualPointCommand;

    public ICommand SaveReviewCommand => saveReviewCommand;

    public ICommand ApproveReviewCommand => approveReviewCommand;

    public ICommand TogglePreflightDetailsCommand => togglePreflightDetailsCommand;

    public ICommand ToggleOutputPreviewCommand => toggleOutputPreviewCommand;

    public ICommand ToggleReviewDetailsCommand => toggleReviewDetailsCommand;

    public ICommand OpenReviewSourceCommand => openReviewSourceCommand;

    public ICommand RevealReviewSourceCommand => revealReviewSourceCommand;

    public ICommand ReloadReviewViewerCommand => reloadReviewViewerCommand;

    public ICommand ToggleReviewViewerFitCommand => toggleReviewViewerFitCommand;

    public ICommand ZoomInReviewViewerCommand => zoomInReviewViewerCommand;

    public ICommand ZoomOutReviewViewerCommand => zoomOutReviewViewerCommand;

    public ICommand PreviousReviewViewerPageCommand => previousReviewViewerPageCommand;

    public ICommand NextReviewViewerPageCommand => nextReviewViewerPageCommand;

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

    public IReadOnlyList<PreflightResultListItem> SupportingDocumentResults => FilterPreflightResults("supporting_document");

    public IReadOnlyList<PreflightResultListItem> StructureCheckResults => FilterPreflightResults("structure", "system");

    public IReadOnlyList<PreflightResultListItem> GeoreferenceResults => FilterPreflightResults("georeference");

    public IReadOnlyList<PreflightResultListItem> DimensionCheckResults => FilterPreflightResults("dimension");

    public bool HasPreflightResults => PreflightResults.Count > 0;

    public bool HasSupportingDocumentResults => SupportingDocumentResults.Count > 0;

    public bool HasSupportingDocumentInventory => SupportingDocumentInventory.Count > 0;

    public bool HasSupportingDocumentDownloads => SupportingDocumentDownloads.Count > 0;

    public bool HasStructureCheckResults => StructureCheckResults.Count > 0;

    public bool HasGeoreferenceResults => GeoreferenceResults.Count > 0;

    public bool HasDimensionCheckResults => DimensionCheckResults.Count > 0;

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

    public string StructureCheckBadge => BuildGroupedBadge(StructureCheckResults, workflowSession.IsPreflightRunning ? "Processing" : "Not processed");

    public string GeoreferenceBadge => BuildGroupedBadge(GeoreferenceResults, workflowSession.IsPreflightRunning ? "Pending" : "Not processed");

    public string DimensionCheckBadge => BuildGroupedBadge(DimensionCheckResults, workflowSession.IsPreflightRunning ? "Pending" : "Not processed");

    public string SourceIntakeBadge => SourceFiles.Count > 0 && SourceFiles.All(item => item.SourceFile.Copied)
        ? "Copied"
        : "Pending";

    public string SupportingDocumentBadge => BuildGroupedBadge(SupportingDocumentResults, SourceFiles.Count > 0 ? "Loaded" : "Not loaded");

    public string IntakeSummaryText =>
        SupportingDocumentDownloads.Count == 0
            ? "No transaction attachments have been loaded into the case folder yet."
            : $"{SupportingDocumentDownloads.Count} transaction attachment file(s) reviewed and copied from the selected transaction. {DetectedProfileLabel}";

    public string IntakeDetailText =>
        IntakeIssues.Count == 0
            ? "Supporting documents are copied into the case folder, matched to their expected document roles, and kept as source context for Structure Check, Georeference Check, Dimension Check, and Validate Points and Lines."
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
            if (!HasStructureCheckResults)
            {
                return "No structure-check results yet.";
            }

            var blockers = StructureCheckResults.Count(IsBlockingResult);
            var warnings = StructureCheckResults.Count(IsWarningResult);
            var passed = StructureCheckResults.Count(IsPassedResult);
            return $"{blockers} blocker(s), {warnings} warning(s), {passed} passed.";
        }
    }

    public string PreflightCollapsedHint => BuildGroupedHint(StructureCheckResults, "All current structure checks passed.");

    public string GeoreferenceSummaryText
    {
        get
        {
            if (!HasGeoreferenceResults)
            {
                return "No georeference-check results yet.";
            }

            var blockers = GeoreferenceResults.Count(IsBlockingResult);
            var warnings = GeoreferenceResults.Count(IsWarningResult);
            var passed = GeoreferenceResults.Count(IsPassedResult);
            return $"{blockers} blocker(s), {warnings} warning(s), {passed} passed.";
        }
    }

    public string GeoreferenceCollapsedHint => BuildGroupedHint(GeoreferenceResults, "Georeference readiness has not produced findings yet.");

    public string DimensionCheckSummaryText
    {
        get
        {
            if (!HasDimensionCheckResults)
            {
                return "No dimension-check results yet.";
            }

            var blockers = DimensionCheckResults.Count(IsBlockingResult);
            var warnings = DimensionCheckResults.Count(IsWarningResult);
            var passed = DimensionCheckResults.Count(IsPassedResult);
            return $"{blockers} blocker(s), {warnings} warning(s), {passed} passed.";
        }
    }

    public string DimensionCheckCollapsedHint => BuildGroupedHint(DimensionCheckResults, "Dimension readiness has not produced findings yet.");

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
            WorkflowState.ReviewManualPending => "Manual",
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
        HasLoadedReviewData
            ? "Continue in Points and Lines Validation Tool"
            : workflowSession.ExtractionResultRequiresDecision
                ? "Re-process extraction"
                : workflowSession.HasUsableExtractionReview
                    ? "Continue in Points and Lines Validation Tool"
                    : HasExtractionReviewArtifact(workflowSession)
                        ? "Open review data"
                        : "Run";

    public string ExtractionReviewHelpText =>
        workflowSession.ExtractionResultRequiresDecision || workflowSession.HasUsableExtractionReview
            ? workflowSession.CurrentExtractionDecisionGate.GuidanceText
            :
        workflowSession.CurrentState switch
        {
            WorkflowState.ExtractionRunning => "Validate Points and Lines preparation is running from the current compute plan.",
            WorkflowState.ExtractionFailed => "Validate Points and Lines preparation failed. Review the status line, then try again.",
            WorkflowState.ReviewManualPending => "Manual Mode is active. Edit, add, or remove points in Points Validation Tool before saving and approving the review.",
            WorkflowState.ReviewApproved => "Validate Points and Lines is approved. Points and Lines Validation Tool is now read-only for this saved case state.",
            WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed => "Validate Points and Lines is approved. Create Spatial Units now owns the next workflow gate and the validation tool remains read-only.",
            WorkflowState.OutputRunning or WorkflowState.OutputCreated => "Validate Points and Lines is approved. Create Spatial Units is now building the downstream parcel geometry package before Final Review.",
            WorkflowState.ReviewPending when HasExtractionReviewArtifact(workflowSession) => "Continue point and line review in Points and Lines Validation Tool, then approve the review before Create Spatial Units and Final Review.",
            WorkflowState.PreflightPassed => "Generate the extracted point and line package from the selected transaction attachments, then continue in Points and Lines Validation Tool to inspect and correct the parcel data.",
            WorkflowState.PreflightBlocked => "Validate Points and Lines is unavailable until Supporting Document Check, Structure Check, Georeference Check, and Dimension Check blockers are resolved.",
            _ => "Validate Points and Lines is enabled after Structure Check, Georeference Check, and Dimension Check complete."
        };

    public bool ShowExtractionDecisionGate => workflowSession.ExtractionResultRequiresDecision;

    public bool ShowReprocessExtractionAction =>
        HasActiveCase
        && (HasLoadedReviewData || workflowSession.HasUsableExtractionReview || HasExtractionReviewArtifact(workflowSession));

    public string ExtractionDecisionSummaryText => workflowSession.CurrentExtractionDecisionGate.SummaryText;

    public string ExtractionDecisionGuidanceText => workflowSession.CurrentExtractionDecisionGate.GuidanceText;

    public string ExtractionDecisionAttemptText =>
        workflowSession.CurrentExtractionDecisionGate.AttemptCount <= 0
            ? "No extraction attempt has been recorded yet."
            : workflowSession.CurrentExtractionDecisionGate.StronglyRecommendManual
                ? $"Attempt {workflowSession.CurrentExtractionDecisionGate.AttemptCount}. Manual review is now strongly recommended."
                : $"Attempt {workflowSession.CurrentExtractionDecisionGate.AttemptCount}. You can rerun extraction, or continue in Manual Mode.";

    public bool HasLoadedReviewData => loadedReviewDocument is not null;

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
                NotifyPropertyChanged(nameof(SelectedReviewRowValidationIssueText));
                editReviewPointCommand.RaiseCanExecuteChanged();
                removeManualPointCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<SourceFileListItem> ReviewSourceOptions => SupportingDocumentDownloads;

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
                reviewViewerPageIndex = 0;
                reviewViewerPageCount = 0;
                reviewViewerFitToPane = true;
                reviewViewerZoom = 1.0d;
                RefreshWorkflowProperties();
            }
        }
    }

    public string ReviewWorkspaceTitle => "Points Validation Tool";

    public bool CanOpenExperimentalReviewWorkspace =>
        CanUseWorkflowActions
        && HasActiveCase
        && (HasLoadedReviewData || workflowSession.HasUsableExtractionReview || IsManualCogoFallbackSelected);

    public bool ShowExperimentalReviewWorkspaceAction =>
        workflowSession.HasUsableExtractionReview || IsManualCogoFallbackSelected;

    public bool IsManualCogoFallbackSelected => workflowSession.CurrentState == WorkflowState.ReviewManualPending;

    public bool ShowManualCogoFallbackAction =>
        workflowSession.CanChooseManualCogoReview
        && workflowSession.CurrentState is WorkflowState.ReviewPending or WorkflowState.ReviewManualPending;

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

    public bool ReviewViewerUsesBrowser => reviewViewerState.UsesBrowser && !string.IsNullOrWhiteSpace(reviewViewerState.FullPath);

    public bool ReviewViewerShowsFallback => !reviewViewerState.CanRenderEmbedded || (reviewViewerState.UsesImage && reviewViewerImageSource is null);

    public bool CanToggleReviewViewerFit => ReviewViewerUsesImage;

    public bool CanZoomReviewViewerIn => ReviewViewerUsesImage;

    public bool CanZoomReviewViewerOut => ReviewViewerUsesImage && (!reviewViewerFitToPane || reviewViewerZoom > 0.30d);

    public bool CanGoToPreviousReviewViewerPage => ReviewViewerUsesImage && reviewViewerPageCount > 1 && reviewViewerPageIndex > 0;

    public bool CanGoToNextReviewViewerPage => ReviewViewerUsesImage && reviewViewerPageCount > 1 && reviewViewerPageIndex < reviewViewerPageCount - 1;

    public string ReviewViewerFitToggleText => reviewViewerFitToPane ? "Actual size" : "Fit";

    public string ReviewViewerPageStatusText =>
        reviewViewerPageCount > 1
            ? $"Page {reviewViewerPageIndex + 1} / {reviewViewerPageCount}"
            : "Single page";

    public string ReviewViewerZoomText => reviewViewerFitToPane ? "Fit" : $"{Math.Round(reviewViewerZoom * 100d)}%";

    public double ReviewViewerImageScale => reviewViewerFitToPane ? 1.0d : reviewViewerZoom;

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

            var fileLabel = source.FileLabel;
            if (IsPrimaryExtractionSourceFile(fileLabel))
            {
                return "Primary extraction source";
            }

            if (IsPlanReferenceSourceFile(fileLabel))
            {
                return "Plan / map reference";
            }

            if (IsStructuredSurveyPointsSource(source.SourceFile))
            {
                return importStructuredSurveyPoints
                    ? "Optional survey points import selected"
                    : "Optional survey points import not selected";
            }

            if (IsAutoCadSurveySource(source.SourceFile))
            {
                return importAutoCadSurveySource
                    ? "Optional AutoCAD import selected"
                    : "Optional AutoCAD import not selected";
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

            var fileLabel = source.FileLabel;
            if (IsPrimaryExtractionSourceFile(fileLabel))
            {
                return "This file is the active extraction input for parcel points in the current case.";
            }

            if (IsPlanReferenceSourceFile(fileLabel))
            {
                return "This file is kept as the plan or map reference while you validate extracted parcel points.";
            }

            if (IsStructuredSurveyPointsSource(source.SourceFile))
            {
                return importStructuredSurveyPoints
                    ? "This structured survey points file is selected as an optional downstream import for Create Spatial Units."
                    : "This structured survey points file is available, but it is not selected as an import source for this run.";
            }

            if (IsAutoCadSurveySource(source.SourceFile))
            {
                return importAutoCadSurveySource
                    ? "This AutoCAD survey source is selected as an optional downstream import for Create Spatial Units."
                    : "This AutoCAD survey source is available, but it is not selected as an import source for this run.";
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

    public string SelectedReviewRowValidationIssueText => SelectedReviewRow is null
        ? "Select a row to review blocker details."
        : string.IsNullOrWhiteSpace(SelectedReviewRow.ValidationIssueSummary)
            ? "Selected row has no active validation blocker."
            : SelectedReviewRow.ValidationIssueSummary;

    public bool ReviewHasSegmentSolverBlockers =>
        string.Equals(ReadBoundarySolverStatus(), "blocked", StringComparison.OrdinalIgnoreCase);

    public bool ReviewHasBlockers => HasLoadedReviewData && (ReviewValidationResult.HasBlockers || ReviewHasSegmentSolverBlockers);

    public ExtractionReviewSummary ReviewSummary =>
        loadedReviewDocument is null
            ? new ExtractionReviewSummary(0, 0, 0, 0, 0)
            : extractionReviewService.Summarize(loadedReviewDocument);

    public string ReviewSummaryText =>
        !HasLoadedReviewData
            ? "Continue point review in Points Validation Tool to inspect and correct the extracted parcel points."
            : IsManualCogoFallbackSelected
                ? $"{ReviewSummary.TotalRows} point row(s) loaded. Manual Mode is active; edit, add, or remove points before saving and approving the review."
            : IsReviewApproved
                ? $"{ReviewSummary.TotalRows} point row(s) loaded. Validate Points is approved and locked for editing."
                : IsManualReviewEditMode
                    ? $"{ReviewSummary.TotalRows} point row(s) loaded, {ReviewSummary.EditedRows} edited, {ReviewSummary.ManualRows} manual, {ReviewSummary.UnresolvedRows} unresolved. Manual point edit is active for the current parcel."
                    : $"{ReviewSummary.TotalRows} point row(s) loaded, {ReviewSummary.EditedRows} edited, {ReviewSummary.ManualRows} manual, {ReviewSummary.UnresolvedRows} unresolved.";

    public ParcelScopedReviewValidationResult ReviewValidationResult =>
        loadedReviewDocument is null
            ? new ParcelScopedReviewValidationResult(
                new[] { "Review data not loaded." },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<ParcelClosureReviewResult>(),
                Array.Empty<ParcelReadinessReviewResult>())
            : NormalizeReviewValidationResultForActiveReview(
                reviewValidationService.Validate(loadedReviewDocument.Rows, pendingManualRowId));

    public bool IsManualReviewEditMode => !string.IsNullOrWhiteSpace(pendingManualRowId);

    public bool CanCancelPendingManualPointEdit => IsManualReviewEditMode && !IsReviewLocked;

    private ParcelScopedReviewValidationResult NormalizeReviewValidationResultForActiveReview(ParcelScopedReviewValidationResult result)
    {
        if (!IsPxaSurveyPlanReview
            || !string.Equals(ReadBoundarySolverStatus(), "passed", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var closureResults = result.ClosureResults
            .Select(item => item.Status == ClosureValidationStatus.Blocker
                ? item with
                {
                    Status = ClosureValidationStatus.Passed,
                    ClosureDistanceM = 0d,
                    MiscloseRatioDenominator = null,
                    Message = "PXA reviewed boundary segment solver passed; point-row closure is superseded by the reviewed segment chain."
                }
                : item)
            .ToArray();
        var parcelIssues = result.ParcelIssues
            .Where(pair => !IsOnlyPxaClosureIssue(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var issues = result.Issues
            .Where(issue => !IsOnlyPxaClosureIssue(issue))
            .ToArray();
        return result with
        {
            Issues = issues,
            ParcelIssues = parcelIssues,
            ClosureResults = closureResults
        };
    }

    private static bool IsOnlyPxaClosureIssue(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains("exceeds the configured closure tolerance", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("missing", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("unresolved", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanChangeReviewParcelSelection => !IsManualReviewEditMode && !pointEditorOpen;

    public bool HasSingleReviewParcelGroup =>
        ReviewRows
            .Select(row => NormalizeReviewParcelGroupId(row.ParcelGroupId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count() <= 1;

    public string ReviewGateText =>
        !HasLoadedReviewData
            ? "Review data not loaded."
            : IsManualCogoFallbackSelected
                ? "Manual Mode is active. Use Points Validation Tool to edit, add, or remove points. Save and approve when the reviewed points are ready for Create Spatial Units."
            : IsReviewApproved
                ? "Validate Points is approved. Points Validation Tool stays available for verification, and the next steps are Create Spatial Units followed by Final Review."
            : IsManualReviewEditMode
                ? "Manual point edit is in progress. Save review or discard the pending row before switching parcels or approving."
                : ReviewHasSegmentSolverBlockers
                    ? BuildBoundarySolverSummaryText()
                : ReviewValidationResult.HasBlockers
                    ? ReviewValidationResult.SummaryText
                    : "Validate Points is complete for this stage. Continue in Points Validation Tool when you need parcel-by-parcel verification before Create Spatial Units.";

    public string ReviewBadgeText =>
        !HasLoadedReviewData
            ? "Not loaded"
            : reviewDirty
                ? "Unsaved"
                : IsManualReviewEditMode
                    ? "Editing"
                : IsManualCogoFallbackSelected
                    ? "Manual Mode"
                : IsReviewApproved
                    ? "Approved"
                    : "Loaded";

    internal bool HasUnsavedReviewChanges => reviewDirty;

    internal bool CanSaveReviewChangesFromWorkspace => HasLoadedReviewData && ReviewRows.Count > 0 && !IsReviewLocked && reviewDirty;

    public int ReviewContentVersion => reviewContentVersion;

    public bool IsPxaSurveyPlanReview { get; private set; }

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
            WorkflowState.SpatialReviewApproved => "Final Review is complete. The reviewed parcel layers are ready for final transaction completion.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending when IsManualSpatialReviewRoute => "Manual Mode prepared editable parcel layers. Use the ArcGIS Pro map to refine geometry while keeping the transaction PDFs open as source reference.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Open or reuse the generated parcel layers in ArcGIS Pro, inspect geometry in-map, and apply any needed snapping or COGO edits after Create Spatial Units finishes.",
            _ => "Final Review becomes available after spatial unit creation succeeds."
        };

    public string SpatialReviewHelpText =>
        workflowSession.CurrentState switch
        {
            WorkflowState.SpatialReviewApproved => "Final Review has been approved. If spatial units are regenerated later, this approval will be cleared automatically and the geometry must be reviewed again.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending when IsManualSpatialReviewRoute => "Manual Mode prepared map layers. Edit them directly in ArcGIS Pro and save progress there before final approval.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Use the ArcGIS Pro map as the editing surface. Standard edit, snapping, and COGO-capable tools should be used there rather than inside this dock pane.",
            _ => "Run Create Spatial Units first. When geometry is available, this stage will guide the in-map review handoff."
        };

    public bool HasSpatialReviewDiagnostics =>
        workflowSession.CurrentOutputSummary is not null
        && workflowSession.CurrentState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;

    public string SpatialReviewDiagnosticsText
    {
        get
        {
            var payload = workflowSession.CurrentOutputSummary?.Payload;
            if (payload is null || !HasSpatialReviewDiagnostics)
            {
                return string.Empty;
            }

            var mapMode = string.IsNullOrWhiteSpace(payload.MapLoadMode) ? "unknown" : payload.MapLoadMode;
            var readiness = workflowSession.CurrentValidationSummary?.Payload.ReadinessSummary;
            var readinessText = readiness is null
                ? string.Empty
                : $" Construction readiness - blocker {readiness.Blocker}, warning {readiness.Warning}, passed {readiness.Passed}, skipped {readiness.Skipped}.";
            return payload.RootLineFeatureClassDiagnostic is null
                ? $"COGO diagnostics: map load {mapMode}; bearing text {(payload.BearingTxtPopulated ? "yes" : "no")} ({payload.BearingTxtPopulatedCount}); distance text {(payload.DistanceTxtPopulated ? "yes" : "no")} ({payload.DistanceTxtPopulatedCount}); computed fallback lines {payload.ComputedCogoFallbackLineCount}."
                + readinessText
                : $"COGO diagnostics: map load {mapMode}; root bearing_txt {(payload.RootLineBearingTxtExists ? "present" : "missing")} ({payload.BearingTxtPopulatedCount}); root distance_txt {(payload.RootLineDistanceTxtExists ? "present" : "missing")} ({payload.DistanceTxtPopulatedCount}); root length_txt {(payload.RootLineLengthTxtExists ? "present" : "missing")} ({payload.RootLineLengthTxtPopulatedCount}); root distance_m {(payload.RootLineDistanceMExists ? "present" : "missing")} ({payload.RootLineDistanceMPopulatedCount}); computed fallback lines {payload.ComputedCogoFallbackLineCount}."
                + readinessText;
        }
    }

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
                WorkflowState.OutputRunning => "Create Spatial Units is running. Local geodatabase layers and geometry artifacts will appear here when the stage finishes.",
                WorkflowState.ValidationPassed => "Create Spatial Units is ready. Run it to build the transaction-local geodatabase and map-ready geometry.",
                _ => "Create Spatial Units stays unavailable until point validation is approved and quality requirements pass. This stage creates the geometry package used by Final Review."
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
            ? "Final Review is complete. Finalize records that the submitted geometry passed Compute review and closes the Innola task."
            : "Finalize becomes available after Create Spatial Units is reviewed in Final Review and that review is marked complete.";

    public string ReadyToCompleteHelpText =>
        workflowSession.CurrentState == WorkflowState.SpatialReviewApproved
            ? "Use the ArcGIS Pro map for a final visual check, compare the temporary geometry with official reference layers, then select Finalize to record the Compute disposition and close the Innola task. Select Suspend to save this state and come back later."
            : "Finish Create Spatial Units and complete Final Review first. That review is the gate that unlocks Compute closeout.";

    public string ValidationBadge =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ValidationRunning => "Processing",
            WorkflowState.ValidationBlocked => "Blocked",
            WorkflowState.ValidationPassed => "Passed",
            WorkflowState.OutputRunning => "Passed",
            WorkflowState.OutputCreated => "Passed",
            WorkflowState.ReviewApproved => "Ready",
            WorkflowState.ReviewManualPending => "Manual Mode",
            _ => "Not started"
        };

    public string ValidationSummaryText
    {
        get
        {
            var summary = workflowSession.CurrentValidationSummary;
            if (summary is null)
            {
                return workflowSession.CurrentState == WorkflowState.ReviewManualPending
                    ? "Create Spatial Units becomes available after Manual Mode points are saved and approved."
                    : workflowSession.CurrentState == WorkflowState.ReviewApproved
                    ? "Approved point-review data is ready for spatial-unit checks."
                    : "Create Spatial Units has not produced a summary yet.";
            }

            var counts = summary.Payload.FindingCounts;
            var closure = summary.Payload.ClosureSummary;
            var readiness = summary.Payload.ReadinessSummary;
            var closureText = closure is null
                ? string.Empty
                : $" Closure - blocker {closure.Blocker}, warning {closure.Warning}, passed {closure.Passed}.";
            var readinessText = readiness is null
                ? string.Empty
                : $" Construction readiness - blocker {readiness.Blocker}, warning {readiness.Warning}, passed {readiness.Passed}, skipped {readiness.Skipped}.";
            return $"Status: {summary.Payload.Status}. Findings - critical {counts.Critical}, high {counts.High}, warning {counts.Warning}, info {counts.Info}, passed {counts.Passed}.{closureText}{readinessText}";
        }
    }

    public string ValidationHelpText =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ValidationRunning => "Create Spatial Units checks are running against the approved review snapshot.",
            WorkflowState.ValidationBlocked => "Create Spatial Units checks completed with blocking findings. Review the validation summary before spatial creation.",
            WorkflowState.ValidationPassed => "Create Spatial Units checks passed. Run Create Spatial Units to generate the transaction-local geodatabase.",
            WorkflowState.OutputRunning => "Create Spatial Units checks passed. Create Spatial Units is currently building the local geometry package.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending when IsManualSpatialReviewRoute => "Manual Mode owns the reviewed geometry. Continue in Final Review and use the transaction PDFs as the source reference while editing.",
            WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending => "Create Spatial Units checks passed. Spatial outputs are created and now need Final Review in the ArcGIS Pro map.",
            WorkflowState.SpatialReviewApproved => "Create Spatial Units passed. Final Review is approved, so final completion may proceed when transaction-level readiness is met.",
            WorkflowState.ReviewManualPending => "Manual Mode is active. Edit, add, or remove points in Points Validation Tool, then save and approve the review before Create Spatial Units.",
            WorkflowState.ReviewApproved => "Run Create Spatial Units checks on the approved point-review data before spatial creation.",
            _ => "Create Spatial Units becomes available after Validate Points is approved."
        };

    private bool IsManualSpatialReviewRoute =>
        string.Equals(
            workflowSession.CurrentOutputSummary?.Payload.ReviewResultOwner,
            ReviewResultOwnership.ManualSpatialReview,
            StringComparison.OrdinalIgnoreCase);

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
        var running = workflowSession.RunStructureCheckAsync(Environment.UserName);
        RefreshWorkflowProperties();
        await running;
        RefreshWorkflowProperties();
    }

    public async Task RunDimensionCheckAsync()
    {
        var running = workflowSession.RunDimensionCheckAsync(Environment.UserName);
        RefreshWorkflowProperties();
        await running;
        RefreshWorkflowProperties();
    }

    public async Task RunGeoreferenceCheckAsync()
    {
        var running = workflowSession.RunGeoreferenceCheckAsync(Environment.UserName);
        RefreshWorkflowProperties();
        await running;
        RefreshWorkflowProperties();
    }

    private async Task RunOrOpenExtractionReviewAsync()
    {
        if (HasLoadedReviewData)
        {
            await OpenExperimentalReviewWorkspaceAsync().ConfigureAwait(true);
            RefreshWorkflowProperties();
            return;
        }

        if (workflowSession.HasUsableExtractionReview && !workflowSession.ExtractionResultRequiresDecision && !HasLoadedReviewData)
        {
            var loadedForTool = await EnsureExtractionReviewLoadedAsync().ConfigureAwait(true);
            if (loadedForTool)
            {
                await OpenExperimentalReviewWorkspaceAsync().ConfigureAwait(true);
            }

            RefreshWorkflowProperties();
            return;
        }

        await EnsureExtractionReviewLoadedAsync(workflowSession.ExtractionResultRequiresDecision).ConfigureAwait(true);
        RefreshWorkflowProperties();
    }

    private async Task ReprocessExtractionReviewAsync()
    {
        var reloaded = await EnsureExtractionReviewLoadedAsync(forceReprocess: true).ConfigureAwait(true);
        if (!reloaded)
        {
            RefreshWorkflowProperties();
            return;
        }

        workflowSession.SetValidationFailure("Validate Points was reprocessed from the current extraction source.");
        RefreshWorkflowProperties();
    }

    private async Task UseManualCogoFallbackAsync()
    {
        var confirmation = MessageBox.Show(
            "Enable Manual Mode for this case? You can edit, add, or remove points in Points Validation Tool before saving and approving the review.",
            "Enable Manual Mode",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            workflowSession.SetValidationFailure("Manual Mode cancelled.");
            RefreshWorkflowProperties();
            return;
        }

        RefreshWorkflowProperties();
        var switched = await workflowSession.UseManualCogoReviewAsync(Environment.UserName).ConfigureAwait(true);
        if (switched)
        {
            if (await EnsureExtractionReviewLoadedAsync().ConfigureAwait(true))
            {
                await OpenExperimentalReviewWorkspaceAsync().ConfigureAwait(true);
            }
        }

        RefreshWorkflowProperties();
    }

    private async Task<bool> EnsureExtractionReviewLoadedAsync(bool forceReprocess = false)
    {
        if (string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath))
        {
            workflowSession.SetValidationFailure("Create or reopen a Case Folder before opening extraction review.");
            RefreshWorkflowProperties();
            return false;
        }

        var layout = CaseFolderLayout.FromRootDirectory(workflowSession.CaseFolderPath);
        var artifactPath = forceReprocess ? null : SelectExtractionReviewArtifact(layout);
        if (artifactPath is null)
        {
            var extractionTask = workflowSession.RunDraftExtractionAsync(forceReprocess);
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
        pendingManualRowId = null;
        reviewContentVersion = 0;
        ReviewRows.Clear();
        ReviewSegments.Clear();
        ReviewMetadataFields.Clear();
        ReviewAdjacentOwners.Clear();
        ReviewNamedParties.Clear();
        ReviewVolumeFolios.Clear();
        foreach (var row in document.Rows)
        {
            ReviewRows.Add(new ExtractionReviewRowViewModel(row, OnReviewRowChanged));
        }

        foreach (var segment in document.Segments.OrderBy(segment => segment.EffectiveSequence))
        {
            ReviewSegments.Add(new ExtractionReviewSegmentViewModel(segment, OnReviewSegmentChanged));
        }

        foreach (var field in document.SurveyMetadataFields)
        {
            ReviewMetadataFields.Add(new ExtractionReviewMetadataFieldViewModel(field, OnReviewMetadataChanged));
        }

        foreach (var owner in document.AdjacentOwners)
        {
            ReviewAdjacentOwners.Add(new ExtractionReviewAdjacentOwnerViewModel(owner, OnReviewMetadataChanged));
        }

        foreach (var party in document.Parties)
        {
            ReviewNamedParties.Add(new ExtractionReviewNamedPartyViewModel(party, "Party / Owner", OnReviewMetadataChanged));
        }

        foreach (var representative in document.Representatives)
        {
            ReviewNamedParties.Add(new ExtractionReviewNamedPartyViewModel(representative, "Representative", OnReviewMetadataChanged));
        }

        foreach (var volumeFolio in document.VolumeFolios)
        {
            ReviewVolumeFolios.Add(new ExtractionReviewVolumeFolioViewModel(volumeFolio, OnReviewMetadataChanged));
        }

        IsPxaSurveyPlanReview = PxaSurveyPlanReviewRouting.IsPxaSurveyPlanDocument(document);
        SelectedReviewRow = ReviewRows.FirstOrDefault();
    }

    private void OnReviewRowChanged()
    {
        reviewDirty = true;
        reviewContentVersion++;
        if (loadedReviewDocument is not null)
        {
            foreach (var row in ReviewRows)
            {
                row.SyncBackToModel();
            }
        }

        RefreshWorkflowProperties();
    }

    private void OnReviewSegmentChanged()
    {
        reviewDirty = true;
        reviewContentVersion++;
        if (loadedReviewDocument is not null)
        {
            foreach (var segment in ReviewSegments)
            {
                segment.SyncBackToModel();
            }
        }

        RefreshWorkflowProperties();
    }

    private void OnReviewMetadataChanged()
    {
        reviewDirty = true;
        reviewContentVersion++;
        SyncReviewMetadataBackToDocument();
        RefreshWorkflowProperties();
    }

    private SurveyPlanBoundarySolverResult? ApplyBoundarySolverIfAvailable(bool useDerivedCoordinatesAsAnchors = false)
    {
        if (loadedReviewDocument is null || loadedReviewDocument.Segments.Count == 0)
        {
            return null;
        }

        foreach (var segment in ReviewSegments)
        {
            segment.SyncBackToModel();
        }

        SyncReviewMetadataBackToDocument();
        var beforeRowCount = loadedReviewDocument.Rows.Count;
        var result = surveyPlanBoundarySolver.Apply(
            loadedReviewDocument,
            ResolveReviewDocumentAreaSqM(loadedReviewDocument),
            useDerivedCoordinatesAsAnchors);
        var rowsChanged = SyncReviewRowViewModelsFromDocument();
        if (rowsChanged)
        {
            NotifyPropertyChanged(nameof(ReviewRows));
        }

        if (loadedReviewDocument.Rows.Count != beforeRowCount || rowsChanged)
        {
            reviewContentVersion++;
        }

        return result;
    }

    private bool SyncReviewRowViewModelsFromDocument()
    {
        if (loadedReviewDocument is null)
        {
            return false;
        }

        var rowsChanged = false;
        var knownRowsById = ReviewRows
            .Where(row => !string.IsNullOrWhiteSpace(row.RowId))
            .ToDictionary(row => row.RowId, StringComparer.Ordinal);
        foreach (var row in loadedReviewDocument.Rows)
        {
            if (!string.IsNullOrWhiteSpace(row.RowId) && knownRowsById.TryGetValue(row.RowId, out var existingRow))
            {
                rowsChanged = existingRow.RefreshFromModel() || rowsChanged;
                continue;
            }

            ReviewRows.Add(new ExtractionReviewRowViewModel(row, OnReviewRowChanged));
            rowsChanged = true;
        }

        return rowsChanged;
    }

    private static double? ResolveReviewDocumentAreaSqM(ExtractionReviewDocument document)
    {
        if (TryReadAreaValue(document.RootMetadata["survey_metadata"] as JsonObject, "document_area", out var area))
        {
            return area;
        }

        return TryReadAreaValue(document.RootMetadata, "document_area", out area)
            || TryReadAreaValue(document.RootMetadata, "area", out area)
            ? area
            : null;
    }

    private void SyncReviewMetadataBackToDocument()
    {
        if (loadedReviewDocument is null)
        {
            return;
        }

        foreach (var field in ReviewMetadataFields)
        {
            field.SyncBackToModel();
        }

        foreach (var owner in ReviewAdjacentOwners)
        {
            owner.SyncBackToModel();
        }

        foreach (var party in ReviewNamedParties)
        {
            party.SyncBackToModel();
        }

        foreach (var volumeFolio in ReviewVolumeFolios)
        {
            volumeFolio.SyncBackToModel();
        }
    }

    private static bool TryReadAreaValue(JsonObject? node, string propertyName, out double area)
    {
        area = 0d;
        if (node is null || node[propertyName] is null)
        {
            return false;
        }

        var valueNode = node[propertyName];
        if (valueNode is JsonObject objectNode)
        {
            valueNode = objectNode["value"];
        }

        if (valueNode is null)
        {
            return false;
        }

        var text = valueNode switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var doubleValue) => doubleValue.ToString(CultureInfo.InvariantCulture),
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var stringValue) => stringValue,
            _ => valueNode.ToJsonString()
        };

        return TryParseFirstReviewNumber(text, out area);
    }

    private static bool TryParseFirstReviewNumber(string? value, out double number)
    {
        number = 0d;
        var text = value ?? string.Empty;
        var normalized = new string(text
            .Select(character => char.IsDigit(character) || character is '.' or ',' or '-' ? character : ' ')
            .ToArray());
        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number))
            {
                return true;
            }
        }

        return false;
    }

    private string? ReadBoundarySolverStatus()
    {
        return loadedReviewDocument?.RootMetadata["boundary_solver"] is JsonObject solver
            ? ReadReviewJsonString(solver, "status")
            : null;
    }

    private string BuildBoundarySolverSummaryText()
    {
        if (loadedReviewDocument?.RootMetadata["boundary_solver"] is not JsonObject solver)
        {
            return "Reviewed segments have not produced boundary solver diagnostics yet.";
        }

        var status = ReadReviewJsonString(solver, "status") ?? "unknown";
        var findings = solver["findings"] as JsonArray;
        var findingText = findings is null
            ? string.Empty
            : string.Join(" ", findings
                .Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text : null)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        return string.IsNullOrWhiteSpace(findingText)
            ? $"Reviewed segment solver status: {status}."
            : $"Reviewed segment solver status: {status}. {findingText}";
    }

    private static string? ReadReviewJsonString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    internal void SetReviewWorkspaceParcelContext(string? parcelGroupId, string? parcelName, string? traverseId, bool refreshProperties = true)
    {
        var nextGroupId = NormalizeReviewParcelGroupId(parcelGroupId);
        var nextParcelName = string.IsNullOrWhiteSpace(parcelName) ? nextGroupId : parcelName.Trim();
        var nextTraverseId = string.IsNullOrWhiteSpace(traverseId) ? nextGroupId : traverseId.Trim();
        if (string.Equals(activeReviewParcelGroupId, nextGroupId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(activeReviewParcelName, nextParcelName, StringComparison.Ordinal)
            && string.Equals(activeReviewTraverseId, nextTraverseId, StringComparison.Ordinal))
        {
            return;
        }

        activeReviewParcelGroupId = nextGroupId;
        activeReviewParcelName = nextParcelName;
        activeReviewTraverseId = nextTraverseId;
        if (refreshProperties)
        {
            RefreshWorkflowProperties();
        }
    }

    private void AddManualPoint()
    {
        ClearPendingManualPointIfStale();
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

        var parcelGroupId = ResolveActiveReviewParcelGroupId();
        var parcelName = ResolveActiveReviewParcelName();
        var traverseId = ResolveActiveReviewTraverseId();
        var insertAfterRow = ResolveManualPointInsertAnchor(parcelGroupId);
        var manualRow = manualPointService.CreateManualRow(
            loadedReviewDocument,
            parcelGroupId,
            parcelName,
            traverseId,
            insertAfterRow?.SequenceInGroup,
            insertAfterRow?.PointIdentifier);
        var draft = PointEditDraft.CreateForNew(manualRow);
        if (!TryEditReviewPoint(draft, out var committedDraft))
        {
            workflowSession.SetValidationFailure($"Point add cancelled for parcel {parcelGroupId}.");
            RefreshWorkflowProperties();
            return;
        }

        committedDraft.ApplyTo(manualRow);
        InsertManualReviewRow(loadedReviewDocument.Rows, manualRow, insertAfterRow?.Model);
        var reviewRow = new ExtractionReviewRowViewModel(manualRow, OnReviewRowChanged);
        InsertManualReviewRowViewModel(reviewRow, insertAfterRow);
        RefreshReviewRowSequences(parcelGroupId);
        SetReviewWorkspaceParcelContext(parcelGroupId, parcelName, traverseId, refreshProperties: false);
        SelectedReviewRow = reviewRow;
        reviewDirty = true;
        pendingManualRowId = null;
        reviewContentVersion++;
        var sequenceText = manualRow.SequenceInGroup?.ToString(CultureInfo.InvariantCulture) ?? "next";
        workflowSession.SetValidationFailure($"Point inserted at sequence {sequenceText} in parcel {parcelGroupId}. Save review to persist the change.");
        RefreshWorkflowProperties();
    }

    private ExtractionReviewRowViewModel? ResolveManualPointInsertAnchor(string parcelGroupId)
    {
        var normalizedParcelGroupId = NormalizeReviewParcelGroupId(parcelGroupId);
        return SelectedReviewRow is { SequenceInGroup: > 0 } selectedRow
            && string.Equals(NormalizeReviewParcelGroupId(selectedRow.ParcelGroupId), normalizedParcelGroupId, StringComparison.OrdinalIgnoreCase)
            ? selectedRow
            : ReviewRows
                .Where(row => string.Equals(NormalizeReviewParcelGroupId(row.ParcelGroupId), normalizedParcelGroupId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
                .LastOrDefault();
    }

    private static void InsertManualReviewRow(IList<ExtractionReviewRow> rows, ExtractionReviewRow manualRow, ExtractionReviewRow? insertAfterRow)
    {
        var insertIndex = insertAfterRow is null ? -1 : rows.IndexOf(insertAfterRow);
        if (insertIndex >= 0)
        {
            rows.Insert(insertIndex + 1, manualRow);
            return;
        }

        rows.Add(manualRow);
    }

    private void InsertManualReviewRowViewModel(ExtractionReviewRowViewModel reviewRow, ExtractionReviewRowViewModel? insertAfterRow)
    {
        var insertIndex = insertAfterRow is null ? -1 : ReviewRows.IndexOf(insertAfterRow);
        if (insertIndex >= 0)
        {
            ReviewRows.Insert(insertIndex + 1, reviewRow);
            return;
        }

        ReviewRows.Add(reviewRow);
    }

    private void RefreshReviewRowSequences(string parcelGroupId)
    {
        var normalizedParcelGroupId = NormalizeReviewParcelGroupId(parcelGroupId);
        if (loadedReviewDocument is not null)
        {
            manualPointService.NormalizeSequences(loadedReviewDocument.Rows, normalizedParcelGroupId);
        }

        foreach (var row in ReviewRows.Where(row => string.Equals(NormalizeReviewParcelGroupId(row.ParcelGroupId), normalizedParcelGroupId, StringComparison.OrdinalIgnoreCase)))
        {
            row.RefreshSequenceInGroup();
        }
    }

    private void EditSelectedReviewPoint()
    {
        ClearPendingManualPointIfStale();
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (SelectedReviewRow is null)
        {
            workflowSession.SetValidationFailure("Select a point before editing it.");
            RefreshWorkflowProperties();
            return;
        }

        var targetRow = SelectedReviewRow;
        var draft = PointEditDraft.CreateForEdit(targetRow.Model);
        if (!TryEditReviewPoint(draft, out var committedDraft))
        {
            workflowSession.SetValidationFailure($"Point edit cancelled for parcel {targetRow.ParcelGroupId}.");
            RefreshWorkflowProperties();
            return;
        }

        targetRow.ApplyCommittedEdit(committedDraft);
        SetReviewWorkspaceParcelContext(targetRow.ParcelGroupId, targetRow.Model.ParcelName, targetRow.TraverseId, refreshProperties: false);
        SelectedReviewRow = targetRow;
        reviewDirty = true;
        reviewContentVersion++;
        workflowSession.SetValidationFailure($"Point {targetRow.PointIdentifier} updated. Save review to persist the change.");
        RefreshWorkflowProperties();
    }

    private void RemoveSelectedManualPoint()
    {
        ClearPendingManualPointIfStale();
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (loadedReviewDocument is null || SelectedReviewRow is null)
        {
            workflowSession.SetValidationFailure("Select a manual point before removing it.");
            RefreshWorkflowProperties();
            return;
        }

        var rowToRemove = SelectedReviewRow;
        var currentParcelGroupId = NormalizeReviewParcelGroupId(rowToRemove.ParcelGroupId);
        loadedReviewDocument.Rows.Remove(rowToRemove.Model);
        ReviewRows.Remove(rowToRemove);
        RefreshReviewRowSequences(currentParcelGroupId);
        var nextRow = ReviewRows.FirstOrDefault(row => string.Equals(NormalizeReviewParcelGroupId(row.ParcelGroupId), currentParcelGroupId, StringComparison.OrdinalIgnoreCase))
            ?? ReviewRows.LastOrDefault();
        SetReviewWorkspaceParcelContext(currentParcelGroupId, rowToRemove.Model.ParcelName, rowToRemove.TraverseId, refreshProperties: false);
        SelectedReviewRow = nextRow;
        reviewDirty = true;
        reviewContentVersion++;
        workflowSession.SetValidationFailure(rowToRemove.IsManual
            ? "Selected manual point removed. Save review to persist the change."
            : "Selected extracted point removed from the review dataset. Save review to persist the change.");
        if (string.Equals(pendingManualRowId, rowToRemove.RowId, StringComparison.Ordinal))
        {
            pendingManualRowId = null;
        }
        RefreshWorkflowProperties();
    }

    private bool TryEditReviewPoint(PointEditDraft draft, out PointEditDraft committedDraft)
    {
        committedDraft = draft;
        pointEditorOpen = true;
        RefreshWorkflowProperties();

        try
        {
            var dialogViewModel = new PointEditDialogViewModel(draft, ReviewRows.ToArray());
            var dialog = new PointEditDialogWindow(dialogViewModel);
            ApplyReviewDialogOwner(dialog);
            var accepted = dialog.ShowDialog() == true;
            committedDraft = dialogViewModel.CommittedDraft;
            return accepted;
        }
        finally
        {
            pointEditorOpen = false;
            RefreshWorkflowProperties();
        }
    }

    private void AddReviewSegment()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (!IsPxaSurveyPlanReview)
        {
            workflowSession.SetValidationFailure("Boundary segment add is available for PXA survey-plan review.");
            RefreshWorkflowProperties();
            return;
        }

        if (loadedReviewDocument is null)
        {
            return;
        }

        var manualSegment = manualBoundarySegmentService.CreateManualSegment(loadedReviewDocument);
        var reviewSegment = new ExtractionReviewSegmentViewModel(manualSegment, OnReviewSegmentChanged);
        pointEditorOpen = true;
        RefreshWorkflowProperties();
        try
        {
            var dialogViewModel = new SegmentEditDialogViewModel(reviewSegment, isNewSegment: true);
            var dialog = new SegmentEditDialogWindow(dialogViewModel);
            ApplyReviewDialogOwner(dialog);
            if (dialog.ShowDialog() != true)
            {
                workflowSession.SetValidationFailure("Boundary segment add cancelled.");
                return;
            }

            reviewSegment.SyncBackToModel();
            loadedReviewDocument.Segments.Add(manualSegment);
            ReviewSegments.Add(reviewSegment);
            ApplyBoundarySolverIfAvailable();
            reviewDirty = true;
            reviewContentVersion++;
            workflowSession.SetValidationFailure($"Boundary segment {reviewSegment.FromPoint}->{reviewSegment.ToPoint} added. Save review to persist the change.");
        }
        finally
        {
            pointEditorOpen = false;
            RefreshWorkflowProperties();
        }
    }

    private void EditReviewSegment(object? parameter)
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (parameter is not ExtractionReviewSegmentViewModel targetSegment)
        {
            workflowSession.SetValidationFailure("Select a boundary segment before editing it.");
            RefreshWorkflowProperties();
            return;
        }

        pointEditorOpen = true;
        RefreshWorkflowProperties();
        try
        {
            var dialogViewModel = new SegmentEditDialogViewModel(targetSegment);
            var dialog = new SegmentEditDialogWindow(dialogViewModel);
            ApplyReviewDialogOwner(dialog);
            if (dialog.ShowDialog() != true)
            {
                workflowSession.SetValidationFailure("Boundary segment edit cancelled.");
                return;
            }

            targetSegment.SyncBackToModel();
            ApplyBoundarySolverIfAvailable();
            reviewDirty = true;
            reviewContentVersion++;
            workflowSession.SetValidationFailure($"Boundary segment {targetSegment.FromPoint}->{targetSegment.ToPoint} updated. Save review to persist the change.");
        }
        finally
        {
            pointEditorOpen = false;
            RefreshWorkflowProperties();
        }
    }

    private void ExcludeReviewSegment(object? parameter)
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (parameter is not ExtractionReviewSegmentViewModel targetSegment)
        {
            workflowSession.SetValidationFailure("Select a boundary segment before excluding it.");
            RefreshWorkflowProperties();
            return;
        }

        targetSegment.IncludeInBoundary = false;
        targetSegment.Status = "Excluded from boundary";
        if (string.IsNullOrWhiteSpace(targetSegment.ReviewNotes))
        {
            targetSegment.ReviewNotes = "Excluded by examiner during segment review.";
        }

        targetSegment.SyncBackToModel();
        ApplyBoundarySolverIfAvailable();
        reviewDirty = true;
        reviewContentVersion++;
        workflowSession.SetValidationFailure($"Boundary segment {targetSegment.FromPoint}->{targetSegment.ToPoint} excluded from boundary. Save review to persist the change.");
        RefreshWorkflowProperties();
    }

    private void RebuildBoundaryPoints()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        if (!IsPxaSurveyPlanReview)
        {
            workflowSession.SetValidationFailure("Rebuild points from boundary is available for PXA survey-plan review.");
            RefreshWorkflowProperties();
            return;
        }

        if (loadedReviewDocument is null)
        {
            workflowSession.SetValidationFailure("Review data is not loaded.");
            RefreshWorkflowProperties();
            return;
        }

        var solverResult = ApplyBoundarySolverIfAvailable(useDerivedCoordinatesAsAnchors: true);
        if (solverResult is null)
        {
            workflowSession.SetValidationFailure("No reviewed boundary segments are available to rebuild points.");
            RefreshWorkflowProperties();
            return;
        }

        reviewDirty = true;
        reviewContentVersion++;
        var action = string.Equals(solverResult.Status, "blocked", StringComparison.OrdinalIgnoreCase)
            ? "Rebuild points found blockers"
            : solverResult.DerivedPointCount == 0
                ? "Rebuild points completed; existing reviewed point coordinates were preserved"
                : $"Rebuild points completed; {solverResult.DerivedPointCount} derived point(s) were updated";
        workflowSession.SetValidationFailure($"{action}. Save review to persist the change. {BuildBoundarySolverSummaryText()}");
        RefreshWorkflowProperties();
    }

    private void ApplyReviewDialogOwner(Window dialog)
    {
        var owner = experimentalReviewWorkspaceWindow ?? FrameworkApplication.Current.MainWindow;
        if (owner is not null && !ReferenceEquals(owner, dialog))
        {
            dialog.Owner = owner;
        }
    }

    private void CancelPendingManualPointEdit()
    {
        ClearPendingManualPointIfStale();
        if (loadedReviewDocument is null || string.IsNullOrWhiteSpace(pendingManualRowId))
        {
            workflowSession.SetValidationFailure("No in-progress manual point is available to discard.");
            RefreshWorkflowProperties();
            return;
        }

        var rowToDiscard = ReviewRows.FirstOrDefault(row => string.Equals(row.RowId, pendingManualRowId, StringComparison.Ordinal));
        if (rowToDiscard is null)
        {
            pendingManualRowId = null;
            workflowSession.SetValidationFailure("The in-progress manual point was already cleared.");
            RefreshWorkflowProperties();
            return;
        }

        loadedReviewDocument.Rows.Remove(rowToDiscard.Model);
        var currentParcelGroupId = NormalizeReviewParcelGroupId(rowToDiscard.ParcelGroupId);
        ReviewRows.Remove(rowToDiscard);
        var nextRow = ReviewRows.FirstOrDefault(row => string.Equals(NormalizeReviewParcelGroupId(row.ParcelGroupId), currentParcelGroupId, StringComparison.OrdinalIgnoreCase))
            ?? ReviewRows.LastOrDefault();
        SetReviewWorkspaceParcelContext(currentParcelGroupId, rowToDiscard.Model.ParcelName, rowToDiscard.TraverseId, refreshProperties: false);
        SelectedReviewRow = nextRow;
        pendingManualRowId = null;
        reviewDirty = true;
        reviewContentVersion++;
        workflowSession.SetValidationFailure("In-progress manual point discarded. Save review to persist the removal.");
        RefreshWorkflowProperties();
    }

    internal void DiscardPendingManualPointIfAny(bool silent = false)
    {
        ClearPendingManualPointIfStale();
        if (loadedReviewDocument is null || string.IsNullOrWhiteSpace(pendingManualRowId))
        {
            return;
        }

        var rowToDiscard = ReviewRows.FirstOrDefault(row => string.Equals(row.RowId, pendingManualRowId, StringComparison.Ordinal));
        if (rowToDiscard is null)
        {
            pendingManualRowId = null;
            return;
        }

        loadedReviewDocument.Rows.Remove(rowToDiscard.Model);
        var removedIndex = ReviewRows.IndexOf(rowToDiscard);
        ReviewRows.Remove(rowToDiscard);
        SelectedReviewRow = removedIndex >= 0 && removedIndex < ReviewRows.Count
            ? ReviewRows[removedIndex]
            : ReviewRows.LastOrDefault();
        pendingManualRowId = null;
        reviewDirty = true;
        reviewContentVersion++;
        if (!silent)
        {
            workflowSession.SetValidationFailure("In-progress manual point discarded.");
        }

        RefreshWorkflowProperties();
    }

    internal bool SaveReviewChangesFromWorkspace()
    {
        return SaveReviewChangesCore("Validated points saved. Continue into Create Spatial Units when you are ready.");
    }

    internal bool ContinueToCreateSpatialUnitsFromWorkspace()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Validated points are already approved. Continue in Create Spatial Units.");
            RefreshWorkflowProperties();
            return true;
        }

        if (reviewDirty && !SaveReviewChangesCore(null))
        {
            return false;
        }

        return ApproveReviewCore("Validated points are approved. Continue in Create Spatial Units.");
    }

    internal bool DiscardUnsavedReviewChangesFromWorkspace()
    {
        if (!reviewDirty && !IsManualReviewEditMode)
        {
            return true;
        }

        var reviewDocument = workflowSession.LoadExtractionReview();
        if (reviewDocument is null)
        {
            workflowSession.SetValidationFailure("Could not reload the last saved point review data.");
            RefreshWorkflowProperties();
            return false;
        }

        LoadReviewDocumentIntoPane(reviewDocument);
        workflowSession.SetValidationFailure("Unsaved point changes were discarded. Previous saved review data remains available.");
        RefreshWorkflowProperties();
        return true;
    }

    internal void HandlePointsValidationWorkspaceClosed(bool reviewSaved, bool continuedToCreateSpatialUnits, bool discardedUnsavedChanges)
    {
        if (experimentalReviewWorkspaceWindow is not null)
        {
            experimentalReviewWorkspaceWindow = null;
        }

        workflowSession.SetValidationFailure(
            PointsValidationWorkspaceMessages.BuildCloseStatusText(
                reviewSaved,
                continuedToCreateSpatialUnits,
                discardedUnsavedChanges,
                workflowSession.CurrentState));
        RefreshWorkflowProperties();
    }

    private void SaveReviewChanges()
    {
        SaveReviewChangesCore("Validated points saved. Continue into Create Spatial Units when you are ready.");
    }

    private bool SaveReviewChangesCore(string? successMessage)
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return false;
        }

        if (loadedReviewDocument is null || string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath))
        {
            workflowSession.SetValidationFailure("Review data is not loaded.");
            RefreshWorkflowProperties();
            return false;
        }

        var reviewIssues = reviewValidationService.Validate(loadedReviewDocument.Rows, pendingManualRowId, includePendingManualBarrier: false);
        if (!string.IsNullOrWhiteSpace(pendingManualRowId) && reviewIssues.HasBlockers)
        {
            workflowSession.SetValidationFailure(reviewIssues.SummaryText);
            RefreshWorkflowProperties();
            return false;
        }

        foreach (var row in ReviewRows)
        {
            row.SyncBackToModel();
        }

        foreach (var segment in ReviewSegments)
        {
            segment.SyncBackToModel();
        }

        SyncReviewMetadataBackToDocument();
        ApplyBoundarySolverIfAvailable();

        var saveResult = workflowSession.SaveExtractionReview(loadedReviewDocument, Environment.UserName);
        if (saveResult.Success && saveResult.Document is not null)
        {
            loadedReviewDocument = saveResult.Document;
            reviewDirty = false;
            pendingManualRowId = null;
            LoadReviewDocumentIntoPane(loadedReviewDocument);
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                workflowSession.SetValidationFailure(successMessage);
            }
        }

        RefreshWorkflowProperties();
        return saveResult.Success;
    }

    private void ApproveReview()
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return;
        }

        var confirmation = MessageBox.Show(
            "Approve Validate Points? After approval, Points Validation Tool will become read-only until the process is reset.",
            "Approve Validate Points",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            workflowSession.SetValidationFailure("Review approval cancelled.");
            RefreshWorkflowProperties();
            return;
        }

        ApproveReviewCore(null);
    }

    private bool ApproveReviewCore(string? successMessage)
    {
        if (IsReviewLocked)
        {
            workflowSession.SetValidationFailure("Review is already approved and locked.");
            RefreshWorkflowProperties();
            return false;
        }

        if (loadedReviewDocument is null)
        {
            workflowSession.SetValidationFailure("Review data is not loaded.");
            RefreshWorkflowProperties();
            return false;
        }

        if (reviewDirty)
        {
            SaveReviewChangesCore(null);
            if (reviewDirty)
            {
                return false;
            }
        }

        foreach (var row in ReviewRows)
        {
            row.SyncBackToModel();
        }

        foreach (var segment in ReviewSegments)
        {
            segment.SyncBackToModel();
        }

        SyncReviewMetadataBackToDocument();
        var solverResult = ApplyBoundarySolverIfAvailable();
        if (solverResult is not null && string.Equals(solverResult.Status, "blocked", StringComparison.OrdinalIgnoreCase))
        {
            workflowSession.SetValidationFailure(BuildBoundarySolverSummaryText());
            RefreshWorkflowProperties();
            return false;
        }

        var reviewIssues = NormalizeReviewValidationResultForActiveReview(
            reviewValidationService.Validate(loadedReviewDocument.Rows, pendingManualRowId));
        if (reviewIssues.HasBlockers)
        {
            workflowSession.SetValidationFailure(reviewIssues.SummaryText);
            RefreshWorkflowProperties();
            return false;
        }

        var approvalResult = workflowSession.ApproveExtractionReview(loadedReviewDocument, Environment.UserName);
        if (approvalResult.Success)
        {
            reviewDirty = false;
            pendingManualRowId = null;
            reviewDetailsExpanded = false;
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                workflowSession.SetValidationFailure(successMessage);
            }
        }

        RefreshWorkflowProperties();
        return approvalResult.Success;
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
        return $"{summary.Payload.PointCount} point(s), {summary.Payload.LineCount} line(s), and {summary.Payload.PolygonCount} polygon feature(s) were generated in the {workspaceMode}.{builtSummary} {BuildOutputDiagnosticsSummary(summary.Payload)}";
    }

    private static string BuildOutputDiagnosticsSummary(OutputSummaryPayload payload)
    {
        var mapMode = string.IsNullOrWhiteSpace(payload.MapLoadMode) ? "unknown" : payload.MapLoadMode;
        return payload.RootLineFeatureClassDiagnostic is null
            ? $"Diagnostics: map load {mapMode}; bearing text populated {(payload.BearingTxtPopulated ? "yes" : "no")} ({payload.BearingTxtPopulatedCount}); distance text populated {(payload.DistanceTxtPopulated ? "yes" : "no")} ({payload.DistanceTxtPopulatedCount}); computed fallback lines {payload.ComputedCogoFallbackLineCount}."
            : $"Diagnostics: map load {mapMode}; root bearing_txt {(payload.RootLineBearingTxtExists ? "present" : "missing")} ({payload.BearingTxtPopulatedCount}); root distance_txt {(payload.RootLineDistanceTxtExists ? "present" : "missing")} ({payload.DistanceTxtPopulatedCount}); root length_txt {(payload.RootLineLengthTxtExists ? "present" : "missing")} ({payload.RootLineLengthTxtPopulatedCount}); root distance_m {(payload.RootLineDistanceMExists ? "present" : "missing")} ({payload.RootLineDistanceMPopulatedCount}); computed fallback lines {payload.ComputedCogoFallbackLineCount}.";
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
        renderedReviewDocumentService.Invalidate(reviewViewerState.FullPath);
        RefreshWorkflowProperties();
    }

    private void ToggleReviewViewerFit()
    {
        reviewViewerFitToPane = !reviewViewerFitToPane;
        RefreshWorkflowProperties();
    }

    private void ZoomInReviewViewer()
    {
        if (!ReviewViewerUsesImage)
        {
            return;
        }

        reviewViewerFitToPane = false;
        reviewViewerZoom = Math.Min(4.0d, Math.Round((reviewViewerZoom + 0.20d) * 100d) / 100d);
        RefreshWorkflowProperties();
    }

    private void ZoomOutReviewViewer()
    {
        if (!ReviewViewerUsesImage)
        {
            return;
        }

        reviewViewerFitToPane = false;
        reviewViewerZoom = Math.Max(0.25d, Math.Round((reviewViewerZoom - 0.20d) * 100d) / 100d);
        RefreshWorkflowProperties();
    }

    private void ChangeReviewViewerPage(int delta)
    {
        if (reviewViewerPageCount <= 1)
        {
            return;
        }

        var nextPageIndex = Math.Clamp(reviewViewerPageIndex + delta, 0, reviewViewerPageCount - 1);
        if (nextPageIndex == reviewViewerPageIndex)
        {
            return;
        }

        reviewViewerPageIndex = nextPageIndex;
        reviewViewerStateCacheKey = null;
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
        workflowSession.SetValidationFailure("Points Validation Tool opened with the current case artifacts.");
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
        var availableReviewSources = ReviewSourceOptions;
        if (availableReviewSources.Count == 0)
        {
            return null;
        }

        var resolved = ReviewSourceSelectionResolver.Resolve(
            availableReviewSources.Select(item => item.SourceFile).ToArray(),
            selectedReviewSourceCopiedPath);

        if (resolved is null)
        {
            return null;
        }

        return availableReviewSources.FirstOrDefault(item =>
            string.Equals(item.SourceFile.CopiedPath, resolved.CopiedPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.SourceFile.FileName, resolved.FileName, StringComparison.OrdinalIgnoreCase))
            ?? availableReviewSources.FirstOrDefault(item => string.Equals(item.SourceFile.FileName, resolved.FileName, StringComparison.OrdinalIgnoreCase))
            ?? availableReviewSources.FirstOrDefault();
    }

    private void RefreshReviewViewerState()
    {
        reviewViewerLoadCancellation?.Cancel();
        reviewViewerLoadCancellation?.Dispose();
        reviewViewerLoadCancellation = new CancellationTokenSource();
        _ = RefreshReviewViewerStateAsync(reviewViewerLoadCancellation.Token);
    }

    private async Task RefreshReviewViewerStateAsync(CancellationToken cancellationToken)
    {
        var sourceFile = SelectedReviewSource?.SourceFile;
        var pdfViewerMode = InnolaTransactionSettings.Load().PdfViewerMode;
        var projected = ReviewSourceViewerStateProjector.Build(sourceFile, pdfViewerMode);
        var cacheKey = $"{projected.Mode}|{projected.FullPath}|{reviewViewerReloadVersion}|{reviewViewerPageIndex}";
        if (string.Equals(reviewViewerStateCacheKey, cacheKey, StringComparison.Ordinal))
        {
            return;
        }

        reviewViewerStateCacheKey = cacheKey;
        reviewViewerState = projected;
        reviewViewerImageSource = null;
        reviewViewerPageCount = 0;

        if (!reviewViewerState.UsesImage || string.IsNullOrWhiteSpace(reviewViewerState.FullPath))
        {
            RefreshViewerOnlyProperties();
            return;
        }

        try
        {
            var renderedPage = await renderedReviewDocumentService.RenderAsync(reviewViewerState.FullPath, reviewViewerPageIndex, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            reviewViewerPageIndex = renderedPage.PageIndex;
            reviewViewerPageCount = renderedPage.PageCount;
            reviewViewerImageSource = renderedPage.ImageSource;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            reviewViewerState = ReviewSourceViewerStateProjector.BuildRenderFailure(sourceFile, exception.Message);
        }
        finally
        {
            RefreshViewerOnlyProperties();
        }
    }

    public async Task StartOrClaimTransactionAsync()
    {
        if (!CanUseWorkflowActions)
        {
            workflowSession.SetValidationFailure("Selected transaction belongs to Compare stage. Use Compare workspace.");
            RefreshWorkflowProperties();
            return;
        }

        var result = await ShellState.LifecycleCoordinator.StartOrClaimAsync();
        if (!result.Success)
        {
            workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not start transaction. Try again.");
        }

        RefreshWorkflowProperties();
    }

    public async Task SuspendTransactionAsync()
    {
        if (!CanUseWorkflowActions)
        {
            workflowSession.SetValidationFailure("Selected transaction belongs to Compare stage. Use Compare workspace.");
            RefreshWorkflowProperties();
            return;
        }

        if (MessageBox.Show(
                "Suspend this case and save the current state back to Innola so it can be resumed later?",
                "Suspend Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var suspendedTransactionNumber = ShellState.Session.LoadedTransactionNumber ?? workflowSession.TransactionId ?? TransactionId;
        var result = await ShellState.LifecycleCoordinator.SaveAndCloseAsync();
        if (result.Success)
        {
            await CleanupActiveTransactionReviewMapAsync(suspendedTransactionNumber).ConfigureAwait(true);
            await ReturnToTransactionListAsync(
                suspendedTransactionNumber,
                result.StatusMessage ?? "Transaction suspended.",
                preserveSavedMarker: true,
                suppressTransactionFromList: false,
                refreshTransactions: false);
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not suspend transaction. Try again.");
        RefreshWorkflowProperties();
    }

    public async Task SaveProgressAsync()
    {
        if (!CanUseWorkflowActions)
        {
            workflowSession.SetValidationFailure("Selected transaction belongs to Compare stage. Use Compare workspace.");
            RefreshWorkflowProperties();
            return;
        }

        var result = await ShellState.LifecycleCoordinator.SaveProgressAsync();
        if (!result.Success)
        {
            workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not save progress. Try again.");
        }

        RefreshWorkflowProperties();
    }

    public async Task CancelProcessAsync()
    {
        if (!CanUseWorkflowActions)
        {
            workflowSession.SetValidationFailure("Selected transaction belongs to Compare stage. Use Compare workspace.");
            RefreshWorkflowProperties();
            return;
        }

        if (MessageBox.Show(
                "Discard the current local session and close this transaction without creating a new resume package?",
                "Cancel Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var cancelledTransactionNumber = ShellState.Session.LoadedTransactionNumber ?? workflowSession.TransactionId ?? TransactionId;
        var result = ShellState.LifecycleCoordinator.CancelActiveProcess();
        if (result.Success)
        {
            await CleanupActiveTransactionReviewMapAsync(cancelledTransactionNumber).ConfigureAwait(true);
            await ReturnToTransactionListAsync(
                cancelledTransactionNumber,
                "Transaction cancelled. Filters cleared.",
                preserveSavedMarker: false,
                suppressTransactionFromList: false,
                refreshTransactions: true).ConfigureAwait(true);
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not cancel the current process.");
        RefreshWorkflowProperties();
    }

    public async Task CompleteTransactionAsync(
        ComputeReviewDecision decision = ComputeReviewDecision.Approved,
        string? comment = null)
    {
        if (!CanUseWorkflowActions)
        {
            workflowSession.SetValidationFailure("Selected transaction belongs to Compare stage. Use Compare workspace.");
            RefreshWorkflowProperties();
            return;
        }

        if (MessageBox.Show(
                "Finalize this Compute review, record the approved disposition in the Enterprise working layers, upload the working package to Innola, and close the task?",
                "Finalize Compute Review",
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

        var dispositionResult = await workflowSession.RecordComputeDispositionAsync(
            decision,
            comment,
            Environment.UserName);
        if (!dispositionResult.Success)
        {
            RefreshWorkflowProperties();
            return;
        }

        var completedTransactionNumber = ShellState.Session.LoadedTransactionNumber ?? workflowSession.TransactionId ?? TransactionId;
        var result = await ShellState.LifecycleCoordinator.CompleteAsync();
        if (result.Success)
        {
            await CleanupActiveTransactionReviewMapAsync(completedTransactionNumber).ConfigureAwait(true);
            await ReturnToTransactionListAsync(
                completedTransactionNumber,
                result.StatusMessage ?? "Completed. Final package uploaded and transaction closed.",
                preserveSavedMarker: false,
                suppressTransactionFromList: true,
                refreshTransactions: true);
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Complete is blocked.");
        RefreshWorkflowProperties();
    }

    private async Task CleanupActiveTransactionReviewMapAsync(string? transactionNumber)
    {
        var cleanupResult = await outputMapIntegrationService
            .RemoveTransactionOutputsFromActiveMapAsync(transactionNumber)
            .ConfigureAwait(true);

        if (!cleanupResult.Success
            && !string.IsNullOrWhiteSpace(cleanupResult.Message)
            && !cleanupResult.Message.Contains("No active map review group", StringComparison.OrdinalIgnoreCase)
            && !cleanupResult.Message.Contains("No active ArcGIS Pro map", StringComparison.OrdinalIgnoreCase))
        {
            workflowSession.SetValidationFailure(cleanupResult.Message);
        }
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

    private static string GetStepStateForSupportingDocuments(WorkflowState state, bool intakeReadyForPreflight, IReadOnlyList<PreflightResultListItem> supportingDocumentResults)
    {
        if (HasBlockingGroup(supportingDocumentResults))
        {
            return "blocked";
        }

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
            WorkflowState.ReviewManualPending => "done",
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

    private IReadOnlyList<PreflightResultListItem> FilterPreflightResults(params string[] groups)
    {
        if (groups.Length == 0)
        {
            return PreflightResults;
        }

        var normalized = new HashSet<string>(
            groups.Where(group => !string.IsNullOrWhiteSpace(group))
                .Select(group => group.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        return PreflightResults
            .Where(result => normalized.Contains(ResolvePreflightRuleGroup(result.Check)))
            .ToArray();
    }

    private string ResolvePreflightRuleGroup(PreflightCheck check)
    {
        var ruleGroup = preflightRuleCatalog.TryGetRule(check.CheckId)?.Group;
        if (!string.IsNullOrWhiteSpace(ruleGroup))
        {
            return ruleGroup.Trim().ToLowerInvariant();
        }

        return check.Category.Trim().ToLowerInvariant() switch
        {
            "manifest" => "supporting_document",
            "workflow_rule" => "structure",
            "dwg" => "structure",
            "georeference" => "georeference",
            "dimension" => "dimension",
            "arcgis_pro" => "system",
            "write_access" => "system",
            "python" => "system",
            "system" => "system",
            _ => "structure"
        };
    }

    private static string BuildGroupedBadge(IReadOnlyList<PreflightResultListItem> results, string emptyLabel)
    {
        if (results.Count == 0)
        {
            return emptyLabel;
        }

        var blockers = results.Count(IsBlockingResult);
        if (blockers > 0)
        {
            return $"{blockers} blocker(s)";
        }

        var warnings = results.Count(IsWarningResult);
        if (warnings > 0)
        {
            return $"{warnings} warning(s)";
        }

        var passed = results.Count(IsPassedResult);
        return passed > 0 ? "Passed" : emptyLabel;
    }

    private static string BuildGroupedHint(IReadOnlyList<PreflightResultListItem> results, string emptyLabel)
    {
        if (results.Count == 0)
        {
            return emptyLabel;
        }

        var blocker = results.FirstOrDefault(IsBlockingResult);
        if (blocker is not null)
        {
            return blocker.Details;
        }

        var warning = results.FirstOrDefault(IsWarningResult);
        if (warning is not null)
        {
            return warning.Details;
        }

        return emptyLabel;
    }

    private static bool HasBlockingGroup(IReadOnlyList<PreflightResultListItem> results)
    {
        return results.Any(IsBlockingResult);
    }

    private static bool IsBlockingResult(PreflightResultListItem result) =>
        string.Equals(result.State, "block", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningResult(PreflightResultListItem result) =>
        string.Equals(result.State, "warn", StringComparison.OrdinalIgnoreCase);

    private static bool IsPassedResult(PreflightResultListItem result) =>
        string.Equals(result.State, "pass", StringComparison.OrdinalIgnoreCase);

    private static string GetStepStateForStructureCheck(WorkflowState state, bool intakeReadyForPreflight, IReadOnlyList<PreflightResultListItem> structureCheckResults)
    {
        if (HasBlockingGroup(structureCheckResults))
        {
            return "blocked";
        }

        return state switch
        {
            WorkflowState.NoCase => "pending",
            WorkflowState.Intake when intakeReadyForPreflight => "active",
            WorkflowState.Intake => "pending",
            WorkflowState.PreflightRunning => "active",
            WorkflowState.PreflightBlocked => structureCheckResults.Count == 0 ? "active" : "done",
            WorkflowState.PreflightPassed => "done",
            WorkflowState.ExtractionRunning => "done",
            WorkflowState.ExtractionFailed => "done",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewManualPending => "done",
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

    private static string GetStepStateForGeoreferenceCheck(WorkflowState state, IReadOnlyList<PreflightResultListItem> structureCheckResults, IReadOnlyList<PreflightResultListItem> georeferenceResults)
    {
        if (HasBlockingGroup(georeferenceResults))
        {
            return "blocked";
        }

        return state switch
        {
            WorkflowState.NoCase or WorkflowState.Intake => "pending",
            WorkflowState.PreflightRunning => structureCheckResults.Count > 0 ? "active" : "pending",
            WorkflowState.PreflightBlocked => georeferenceResults.Count > 0 ? "done" : "pending",
            WorkflowState.PreflightPassed => "done",
            WorkflowState.ExtractionRunning => "done",
            WorkflowState.ExtractionFailed => "done",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewManualPending => "done",
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

    private static string GetStepStateForDimensionCheck(WorkflowState state, IReadOnlyList<PreflightResultListItem> georeferenceResults, IReadOnlyList<PreflightResultListItem> dimensionResults)
    {
        if (HasBlockingGroup(dimensionResults))
        {
            return "blocked";
        }

        return state switch
        {
            WorkflowState.NoCase or WorkflowState.Intake => "pending",
            WorkflowState.PreflightRunning => georeferenceResults.Count > 0 ? "active" : "pending",
            WorkflowState.PreflightBlocked => dimensionResults.Count > 0 ? "done" : "pending",
            WorkflowState.PreflightPassed => "done",
            WorkflowState.ExtractionRunning => "done",
            WorkflowState.ExtractionFailed => "done",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewManualPending => "done",
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
            WorkflowState.ReviewManualPending => "active",
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
            WorkflowState.NoCase or WorkflowState.Intake or WorkflowState.PreflightRunning or WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed or WorkflowState.ExtractionRunning or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending or WorkflowState.ReviewManualPending => "pending",
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
        var supportingDocumentState = GetStepStateForSupportingDocuments(currentState, IntakeReadyForPreflight, SupportingDocumentResults);
        var structureCheckState = GetStepStateForStructureCheck(currentState, IntakeReadyForPreflight, StructureCheckResults);
        var georeferenceState = GetStepStateForGeoreferenceCheck(currentState, StructureCheckResults, GeoreferenceResults);
        var dimensionCheckState = GetStepStateForDimensionCheck(currentState, GeoreferenceResults, DimensionCheckResults);
        var extractionState = GetStepStateForExtractionReview(currentState, HasExtractionArtifact);
        var validationState = GetStepStateForValidation(currentState);
        var outputState = GetStepStateForOutputs(currentState);
        var spatialReviewState = GetStepStateForSpatialReview(currentState);
        var readyState = GetStepStateForReadyToComplete(currentState);
        var spatialUnitsState = MergeStepState(validationState, outputState);
        var finalReviewState = spatialReviewState;

        return new WorkflowLifecycleStep[]
        {
            new WorkflowLifecycleStep("Supporting Document Check", supportingDocumentState, GetLifecycleStepIcon(supportingDocumentState)),
            new WorkflowLifecycleStep("Structure Check", structureCheckState, GetLifecycleStepIcon(structureCheckState)),
            new WorkflowLifecycleStep("Georeference Check", georeferenceState, GetLifecycleStepIcon(georeferenceState)),
            new WorkflowLifecycleStep("Dimension Check", dimensionCheckState, GetLifecycleStepIcon(dimensionCheckState)),
            new WorkflowLifecycleStep("Validate Points and Lines", extractionState, GetLifecycleStepIcon(extractionState)),
            new WorkflowLifecycleStep("Create Spatial Units", spatialUnitsState, GetLifecycleStepIcon(spatialUnitsState)),
            new WorkflowLifecycleStep("Final Review", finalReviewState, GetLifecycleStepIcon(finalReviewState)),
            new WorkflowLifecycleStep("Finalize", readyState, GetLifecycleStepIcon(readyState))
        };
    }

    private static string MergeStepState(string first, string second)
    {
        if (string.Equals(first, "blocked", StringComparison.OrdinalIgnoreCase) || string.Equals(second, "blocked", StringComparison.OrdinalIgnoreCase))
        {
            return "blocked";
        }

        if (string.Equals(first, "active", StringComparison.OrdinalIgnoreCase) || string.Equals(second, "active", StringComparison.OrdinalIgnoreCase))
        {
            return "active";
        }

        if (string.Equals(first, "done", StringComparison.OrdinalIgnoreCase) || string.Equals(second, "done", StringComparison.OrdinalIgnoreCase))
        {
            return "done";
        }

        return "pending";
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

    private WorkflowWorkspaceStage ResolveActiveWorkspaceStage()
    {
        var stage = WorkflowWorkspacePlanner.ResolveActiveStage(CurrentWorkflowState, IntakeReadyForPreflight, HasExtractionArtifact);
        return stage == WorkflowWorkspaceStage.Preflight
            ? ResolveEarlyCheckWorkspaceStage()
            : stage;
    }

    private WorkflowWorkspaceStage ResolveEarlyCheckWorkspaceStage()
    {
        if (HasBlockingGroup(SupportingDocumentResults))
        {
            return WorkflowWorkspaceStage.Intake;
        }

        if (HasBlockingGroup(StructureCheckResults) || !HasStructureCheckResults)
        {
            return WorkflowWorkspaceStage.StructureCheck;
        }

        if (HasBlockingGroup(GeoreferenceResults) || !HasGeoreferenceResults)
        {
            return WorkflowWorkspaceStage.GeoreferenceCheck;
        }

        if (HasBlockingGroup(DimensionCheckResults) || !HasDimensionCheckResults)
        {
            return WorkflowWorkspaceStage.DimensionCheck;
        }

        return CurrentWorkflowState == WorkflowState.PreflightBlocked
            ? WorkflowWorkspaceStage.DimensionCheck
            : WorkflowWorkspaceStage.GeoreferenceCheck;
    }

    private string GetWorkspaceStageLabel(WorkflowWorkspaceStage stage, WorkflowState state) =>
        stage switch
        {
            WorkflowWorkspaceStage.Intake => "Supporting Document Check",
            WorkflowWorkspaceStage.Preflight => ResolveEarlyCheckWorkspaceLabel(state),
            WorkflowWorkspaceStage.StructureCheck => "Structure Check",
            WorkflowWorkspaceStage.GeoreferenceCheck => "Georeference Check",
            WorkflowWorkspaceStage.DimensionCheck => "Dimension Check",
            WorkflowWorkspaceStage.ExtractionReview => "Validate Points and Lines",
            WorkflowWorkspaceStage.Validation => "Create Spatial Units",
            WorkflowWorkspaceStage.Outputs => "Create Spatial Units",
            WorkflowWorkspaceStage.SpatialReview => "Final Review",
            WorkflowWorkspaceStage.ReadyToComplete => "Finalize",
            _ => "Supporting Document Check"
        };

    private string ResolveEarlyCheckWorkspaceLabel(WorkflowState state)
    {
        if (HasBlockingGroup(SupportingDocumentResults))
        {
            return "Supporting Document Check";
        }

        if (HasBlockingGroup(StructureCheckResults) || state == WorkflowState.PreflightRunning || !HasPreflightResults)
        {
            return "Structure Check";
        }

        if (HasBlockingGroup(GeoreferenceResults))
        {
            return "Georeference Check";
        }

        if (HasBlockingGroup(DimensionCheckResults))
        {
            return "Dimension Check";
        }

        return HasGeoreferenceResults ? "Dimension Check" : "Georeference Check";
    }

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
        sourceFileItems = workflowSession.SourceFiles.Select(sourceFile => new SourceFileListItem(sourceFile)).ToArray();
        LoadSupportingDocumentImportOptions();
        UpdateReviewRowValidationFlags();
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
        NotifyPropertyChanged(nameof(IsStructureCheckStageActive));
        NotifyPropertyChanged(nameof(IsGeoreferenceCheckStageActive));
        NotifyPropertyChanged(nameof(IsDimensionCheckStageActive));
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
        NotifyPropertyChanged(nameof(SupportingDocumentDownloads));
        NotifyPropertyChanged(nameof(SupportingDocumentInventory));
        NotifyPropertyChanged(nameof(HasStructuredSurveyPointsSource));
        NotifyPropertyChanged(nameof(HasAutoCadSurveySource));
        NotifyPropertyChanged(nameof(ImportStructuredSurveyPoints));
        NotifyPropertyChanged(nameof(ImportAutoCadSurveySource));
        NotifyPropertyChanged(nameof(SourceIntakeBadge));
        NotifyPropertyChanged(nameof(IntakeSummaryText));
        NotifyPropertyChanged(nameof(IntakeDetailText));
        NotifyPropertyChanged(nameof(IntakeSummaryExpanded));
        NotifyPropertyChanged(nameof(ExtractionReviewBadge));
        NotifyPropertyChanged(nameof(ExtractionReviewActionLabel));
        NotifyPropertyChanged(nameof(ExtractionReviewHelpText));
        NotifyPropertyChanged(nameof(ShowExtractionDecisionGate));
        NotifyPropertyChanged(nameof(ShowReprocessExtractionAction));
        NotifyPropertyChanged(nameof(ExtractionDecisionSummaryText));
        NotifyPropertyChanged(nameof(ExtractionDecisionGuidanceText));
        NotifyPropertyChanged(nameof(ExtractionDecisionAttemptText));
        NotifyPropertyChanged(nameof(HasLoadedReviewData));
        NotifyPropertyChanged(nameof(ReviewSummary));
        NotifyPropertyChanged(nameof(ReviewSummaryText));
        NotifyPropertyChanged(nameof(ReviewValidationResult));
        NotifyPropertyChanged(nameof(ReviewRows));
        NotifyPropertyChanged(nameof(ReviewSegments));
        NotifyPropertyChanged(nameof(ReviewMetadataFields));
        NotifyPropertyChanged(nameof(ReviewAdjacentOwners));
        NotifyPropertyChanged(nameof(ReviewNamedParties));
        NotifyPropertyChanged(nameof(ReviewVolumeFolios));
        NotifyPropertyChanged(nameof(IsPxaSurveyPlanReview));
        NotifyPropertyChanged(nameof(ReviewHasSegmentSolverBlockers));
        NotifyPropertyChanged(nameof(ReviewHasBlockers));
        NotifyPropertyChanged(nameof(ReviewGateText));
        NotifyPropertyChanged(nameof(ReviewBadgeText));
        NotifyPropertyChanged(nameof(ExtractionSummaryExpanded));
        NotifyPropertyChanged(nameof(IsReviewApproved));
        NotifyPropertyChanged(nameof(IsReviewLocked));
        NotifyPropertyChanged(nameof(IsManualReviewEditMode));
        NotifyPropertyChanged(nameof(HasUnsavedReviewChanges));
        NotifyPropertyChanged(nameof(CanSaveReviewChangesFromWorkspace));
        NotifyPropertyChanged(nameof(CanCancelPendingManualPointEdit));
        NotifyPropertyChanged(nameof(CanChangeReviewParcelSelection));
        NotifyPropertyChanged(nameof(HasSingleReviewParcelGroup));
        NotifyPropertyChanged(nameof(ReviewDetailsExpanded));
        NotifyPropertyChanged(nameof(ReviewDetailsToggleText));
        NotifyPropertyChanged(nameof(CanUseWorkflowActions));
        NotifyPropertyChanged(nameof(CanRunPreflight));
        NotifyPropertyChanged(nameof(CanRunGeoreferenceCheck));
        NotifyPropertyChanged(nameof(CanRunDimensionCheck));
        NotifyPropertyChanged(nameof(CanRunExtractionReview));
        NotifyPropertyChanged(nameof(CanReprocessExtractionReview));
        NotifyPropertyChanged(nameof(CanUseManualCogoFallback));
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
        NotifyPropertyChanged(nameof(SupportingDocumentResults));
        NotifyPropertyChanged(nameof(StructureCheckResults));
        NotifyPropertyChanged(nameof(GeoreferenceResults));
        NotifyPropertyChanged(nameof(DimensionCheckResults));
        NotifyPropertyChanged(nameof(HasPreflightResults));
        NotifyPropertyChanged(nameof(HasSupportingDocumentResults));
        NotifyPropertyChanged(nameof(HasSupportingDocumentInventory));
        NotifyPropertyChanged(nameof(HasSupportingDocumentDownloads));
        NotifyPropertyChanged(nameof(HasStructureCheckResults));
        NotifyPropertyChanged(nameof(HasGeoreferenceResults));
        NotifyPropertyChanged(nameof(HasDimensionCheckResults));
        NotifyPropertyChanged(nameof(PreflightBadge));
        NotifyPropertyChanged(nameof(SupportingDocumentBadge));
        NotifyPropertyChanged(nameof(StructureCheckBadge));
        NotifyPropertyChanged(nameof(GeoreferenceBadge));
        NotifyPropertyChanged(nameof(DimensionCheckBadge));
        NotifyPropertyChanged(nameof(PreflightDetailsExpanded));
        NotifyPropertyChanged(nameof(PreflightToggleText));
        NotifyPropertyChanged(nameof(PreflightSummaryText));
        NotifyPropertyChanged(nameof(PreflightCollapsedHint));
        NotifyPropertyChanged(nameof(GeoreferenceSummaryText));
        NotifyPropertyChanged(nameof(GeoreferenceCollapsedHint));
        NotifyPropertyChanged(nameof(DimensionCheckSummaryText));
        NotifyPropertyChanged(nameof(DimensionCheckCollapsedHint));
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
        NotifyPropertyChanged(nameof(CanZoomReviewViewerIn));
        NotifyPropertyChanged(nameof(CanZoomReviewViewerOut));
        NotifyPropertyChanged(nameof(CanGoToPreviousReviewViewerPage));
        NotifyPropertyChanged(nameof(CanGoToNextReviewViewerPage));
        NotifyPropertyChanged(nameof(ReviewViewerFitToggleText));
        NotifyPropertyChanged(nameof(ReviewViewerPageStatusText));
        NotifyPropertyChanged(nameof(ReviewViewerZoomText));
        NotifyPropertyChanged(nameof(ReviewViewerImageScale));
        NotifyPropertyChanged(nameof(ReviewViewerImageStretch));
        NotifyPropertyChanged(nameof(ReviewViewerImageSource));
        NotifyPropertyChanged(nameof(ReviewViewerBrowserUri));
        NotifyPropertyChanged(nameof(ReviewViewerNavigationKey));
        NotifyPropertyChanged(nameof(ReviewContentVersion));
        NotifyPropertyChanged(nameof(SelectedReviewRowDetailsTitle));
        NotifyPropertyChanged(nameof(SelectedReviewRowDetailsText));
        NotifyPropertyChanged(nameof(SelectedReviewRowValidationIssueText));
        NotifyPropertyChanged(nameof(ReviewWorkspaceTitle));
        NotifyPropertyChanged(nameof(ShowExperimentalReviewWorkspaceAction));
        NotifyPropertyChanged(nameof(IsManualCogoFallbackSelected));
        NotifyPropertyChanged(nameof(ShowManualCogoFallbackAction));
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
        NotifyPropertyChanged(nameof(HasSpatialReviewDiagnostics));
        NotifyPropertyChanged(nameof(SpatialReviewDiagnosticsText));
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
        runGeoreferenceCheckCommand.RaiseCanExecuteChanged();
        runDimensionCheckCommand.RaiseCanExecuteChanged();
        runExtractionReviewCommand.RaiseCanExecuteChanged();
        reprocessExtractionReviewCommand.RaiseCanExecuteChanged();
        useManualCogoFallbackCommand.RaiseCanExecuteChanged();
        runValidationCommand.RaiseCanExecuteChanged();
        runOutputsCommand.RaiseCanExecuteChanged();
        loadSpatialReviewLayersCommand.RaiseCanExecuteChanged();
        approveSpatialReviewCommand.RaiseCanExecuteChanged();
        addManualPointCommand.RaiseCanExecuteChanged();
        editReviewPointCommand.RaiseCanExecuteChanged();
        addReviewSegmentCommand.RaiseCanExecuteChanged();
        editReviewSegmentCommand.RaiseCanExecuteChanged();
        excludeReviewSegmentCommand.RaiseCanExecuteChanged();
        rebuildBoundaryPointsCommand.RaiseCanExecuteChanged();
        removeManualPointCommand.RaiseCanExecuteChanged();
        cancelPendingManualPointCommand.RaiseCanExecuteChanged();
        saveReviewCommand.RaiseCanExecuteChanged();
        approveReviewCommand.RaiseCanExecuteChanged();
        togglePreflightDetailsCommand.RaiseCanExecuteChanged();
        toggleOutputPreviewCommand.RaiseCanExecuteChanged();
        toggleReviewDetailsCommand.RaiseCanExecuteChanged();
        openReviewSourceCommand.RaiseCanExecuteChanged();
        revealReviewSourceCommand.RaiseCanExecuteChanged();
        reloadReviewViewerCommand.RaiseCanExecuteChanged();
        toggleReviewViewerFitCommand.RaiseCanExecuteChanged();
        zoomInReviewViewerCommand.RaiseCanExecuteChanged();
        zoomOutReviewViewerCommand.RaiseCanExecuteChanged();
        previousReviewViewerPageCommand.RaiseCanExecuteChanged();
        nextReviewViewerPageCommand.RaiseCanExecuteChanged();
        openExperimentalReviewWorkspaceCommand.RaiseCanExecuteChanged();
        startOrClaimTransactionCommand.RaiseCanExecuteChanged();
        suspendTransactionCommand.RaiseCanExecuteChanged();
        cancelProcessCommand.RaiseCanExecuteChanged();
        completeTransactionCommand.RaiseCanExecuteChanged();
    }

    private void LoadSupportingDocumentImportOptions()
    {
        if (!HasActiveCase)
        {
            importStructuredSurveyPoints = false;
            importAutoCadSurveySource = false;
            return;
        }

        var options = workflowSession.GetSupportingDocumentOptions();
        importStructuredSurveyPoints = options.ImportStructuredSurveyPoints;
        importAutoCadSurveySource = options.ImportAutoCadSurveySource;
    }

    private IReadOnlyList<SourceFileListItem> BuildSupportingDocumentDownloads()
    {
        if (sourceFileItems.Count == 0)
        {
            return Array.Empty<SourceFileListItem>();
        }

        return sourceFileItems
            .GroupBy(
                item => BuildSourceFileIdentity(item.SourceFile),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.RoleSortKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FileLabel, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<SupportingDocumentStatusItem> BuildSupportingDocumentInventory()
    {
        var definitions = InnolaTransactionSettings.Load().ComputeAttachmentSourceTypes
            .Where(definition => !definition.InternalOnly)
            .ToArray();
        if (definitions.Length == 0)
        {
            return Array.Empty<SupportingDocumentStatusItem>();
        }

        var downloadedFiles = SupportingDocumentDownloads;
        var primaryExtractionFile = GetReviewMetadataFileName("points_source") ?? GetReviewMetadataFileName("primary_source_file");
        var planReferenceFile = GetReviewMetadataFileName("plan_source");

        return definitions
            .Select(definition =>
            {
                var matches = downloadedFiles
                    .Where(item => SourceFileMatchesDefinition(item.SourceFile, definition))
                    .ToArray();
                var found = matches.Length > 0;
                var statusLabel = found
                    ? "Found"
                    : definition.Required
                        ? "Missing"
                        : "Optional";
                var fileList = matches
                    .Select(match => match.FileLabel)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var fileText = BuildSupportingDocumentUsageText(
                    definition,
                    fileList,
                    found,
                    primaryExtractionFile,
                    planReferenceFile);

                return new SupportingDocumentStatusItem(
                    definition.DisplayName,
                    SourceRole.DisplayName(definition.WorkflowRole),
                    statusLabel,
                    fileText,
                    definition.Required,
                    found);
            })
            .ToArray();
    }

    private string BuildSupportingDocumentUsageText(
        ComputeAttachmentSourceTypeDefinition definition,
        IReadOnlyList<string> fileList,
        bool found,
        string? primaryExtractionFile,
        string? planReferenceFile)
    {
        if (!found)
        {
            return definition.Required ? "Not provided" : "Not provided (optional)";
        }

        var fileText = string.Join(", ", fileList);
        var usage = definition.WorkflowRole switch
        {
            SourceRole.ComputationSheet => "Used for extraction",
            SourceRole.PlanMapReference => "Used for plan / map reference",
            SourceRole.CoordinateTextSource => importStructuredSurveyPoints
                ? "Optional import selected for Create Spatial Units"
                : "Optional import available, not selected",
            SourceRole.DwgSource => importAutoCadSurveySource
                ? "Optional import selected for Create Spatial Units"
                : "Optional import available, not selected",
            _ => definition.Required ? "Used in current case context" : "Available in current case context"
        };

        if (!string.IsNullOrWhiteSpace(primaryExtractionFile)
            && fileList.Any(file => string.Equals(file, primaryExtractionFile, StringComparison.OrdinalIgnoreCase)))
        {
            usage = "Used for extraction";
        }
        else if (!string.IsNullOrWhiteSpace(planReferenceFile)
                 && fileList.Any(file => string.Equals(file, planReferenceFile, StringComparison.OrdinalIgnoreCase)))
        {
            usage = "Used for plan / map reference";
        }

        return $"{fileText} - {usage}";
    }

    private string? GetReviewMetadataFileName(string propertyName)
    {
        if (loadedReviewDocument?.RootMetadata is null)
        {
            return null;
        }

        if (loadedReviewDocument.RootMetadata[propertyName] is not JsonValue value)
        {
            return null;
        }

        var raw = value.GetValue<string?>();
        return string.IsNullOrWhiteSpace(raw) ? null : Path.GetFileName(raw);
    }

    private bool IsPrimaryExtractionSourceFile(string? fileLabel)
    {
        if (string.IsNullOrWhiteSpace(fileLabel))
        {
            return false;
        }

        var primary = GetReviewMetadataFileName("points_source") ?? GetReviewMetadataFileName("primary_source_file");
        return !string.IsNullOrWhiteSpace(primary)
            && string.Equals(primary, fileLabel, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPlanReferenceSourceFile(string? fileLabel)
    {
        if (string.IsNullOrWhiteSpace(fileLabel))
        {
            return false;
        }

        var plan = GetReviewMetadataFileName("plan_source");
        return !string.IsNullOrWhiteSpace(plan)
            && string.Equals(plan, fileLabel, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStructuredSurveyPointsSource(SourceFileCopyResult sourceFile) =>
        string.Equals(sourceFile.SourceRole, SourceRole.CoordinateTextSource, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceFile.SourceType, "st_survey_points", StringComparison.OrdinalIgnoreCase);

    private static bool IsAutoCadSurveySource(SourceFileCopyResult sourceFile) =>
        string.Equals(sourceFile.SourceRole, SourceRole.DwgSource, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceFile.SourceType, "st_autocad_file", StringComparison.OrdinalIgnoreCase);

    private static bool SourceFileMatchesDefinition(SourceFileCopyResult sourceFile, ComputeAttachmentSourceTypeDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(sourceFile.SourceType)
            && string.Equals(sourceFile.SourceType, definition.SourceType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sourceFile.SourceRole)
            && string.Equals(sourceFile.SourceRole, definition.WorkflowRole, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sourceFile.SourceType) || !string.IsNullOrWhiteSpace(sourceFile.SourceRole))
        {
            return false;
        }

        return definition.SupportsExtension(Path.GetExtension(sourceFile.FileName));
    }

    private static string BuildSourceFileIdentity(SourceFileCopyResult sourceFile)
    {
        if (!string.IsNullOrWhiteSpace(sourceFile.CopiedPath))
        {
            return sourceFile.CopiedPath;
        }

        return string.Join(
            "|",
            sourceFile.FileName,
            sourceFile.SourceType ?? string.Empty,
            sourceFile.SourceRole ?? string.Empty,
            sourceFile.OriginalPath);
    }

    private void UpdateReviewRowValidationFlags()
    {
        if (ReviewRows.Count == 0)
        {
            return;
        }

        var reviewValidationResult = ReviewValidationResult;

        var duplicatePointKeys = ReviewRows
            .Where(row => !string.IsNullOrWhiteSpace(row.PointIdentifier))
            .GroupBy(
                row => $"{NormalizeReviewParcelGroupId(row.ParcelGroupId)}|{row.PointIdentifier.Trim()}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateSequenceKeys = ReviewRows
            .Where(row => row.SequenceInGroup.HasValue && row.SequenceInGroup.Value > 0)
            .GroupBy(
                row => $"{NormalizeReviewParcelGroupId(row.ParcelGroupId)}|{row.SequenceInGroup!.Value.ToString(CultureInfo.InvariantCulture)}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in ReviewRows)
        {
            var rowIssues = new List<string>();
            var parcelGroupId = NormalizeReviewParcelGroupId(row.ParcelGroupId);

            if (row.Unresolved)
            {
                rowIssues.Add("Row is unresolved.");
            }

            if (row.HasMissingRequiredValues)
            {
                rowIssues.Add("Point id or coordinates are missing.");
            }

            if (!string.IsNullOrWhiteSpace(row.Easting) && !TryParseReviewCoordinate(row.Easting))
            {
                rowIssues.Add("Easting is not a valid number.");
            }

            if (!string.IsNullOrWhiteSpace(row.Northing) && !TryParseReviewCoordinate(row.Northing))
            {
                rowIssues.Add("Northing is not a valid number.");
            }

            if (!string.IsNullOrWhiteSpace(row.PointIdentifier))
            {
                var duplicatePointKey = $"{parcelGroupId}|{row.PointIdentifier.Trim()}";
                if (duplicatePointKeys.Contains(duplicatePointKey))
                {
                    rowIssues.Add("Duplicate point id exists in this parcel.");
                }
            }

            if (row.SequenceInGroup.HasValue && row.SequenceInGroup.Value > 0)
            {
                var duplicateSequenceKey = $"{parcelGroupId}|{row.SequenceInGroup.Value.ToString(CultureInfo.InvariantCulture)}";
                if (duplicateSequenceKeys.Contains(duplicateSequenceKey))
                {
                    rowIssues.Add("Duplicate sequence exists in this parcel.");
                }
            }

            row.HasValidationBlocker = rowIssues.Count > 0;
            row.ValidationIssueSummary = string.Join(" ", rowIssues.Distinct(StringComparer.Ordinal));
        }
    }

    private static bool TryParseReviewCoordinate(string value)
    {
        var text = value.Trim();
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out _);
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

        if (!ShellState.IsSelectedTransactionComputeWorkflow)
        {
            ResetWorkflowView("Selected transaction belongs to Compare stage. Use Compare workspace.");
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

    private string ResolveActiveReviewParcelGroupId()
    {
        if (!string.IsNullOrWhiteSpace(activeReviewParcelGroupId))
        {
            return activeReviewParcelGroupId!;
        }

        if (!string.IsNullOrWhiteSpace(SelectedReviewRow?.ParcelGroupId))
        {
            return NormalizeReviewParcelGroupId(SelectedReviewRow.ParcelGroupId);
        }

        return NormalizeReviewParcelGroupId(ReviewRows.FirstOrDefault()?.ParcelGroupId);
    }

    private string ResolveActiveReviewParcelName()
    {
        if (!string.IsNullOrWhiteSpace(activeReviewParcelName))
        {
            return activeReviewParcelName!;
        }

        return ReviewRows.FirstOrDefault(row => string.Equals(NormalizeReviewParcelGroupId(row.ParcelGroupId), ResolveActiveReviewParcelGroupId(), StringComparison.OrdinalIgnoreCase))
            ?.Model.ParcelName
            ?.Trim()
            ?? ResolveActiveReviewParcelGroupId();
    }

    private string ResolveActiveReviewTraverseId()
    {
        if (!string.IsNullOrWhiteSpace(activeReviewTraverseId))
        {
            return activeReviewTraverseId!;
        }

        return ReviewRows.FirstOrDefault(row => string.Equals(NormalizeReviewParcelGroupId(row.ParcelGroupId), ResolveActiveReviewParcelGroupId(), StringComparison.OrdinalIgnoreCase))
            ?.TraverseId
            ?.Trim()
            ?? ResolveActiveReviewParcelGroupId();
    }

    private void ClearPendingManualPointIfStale()
    {
        if (string.IsNullOrWhiteSpace(pendingManualRowId))
        {
            return;
        }

        if (ReviewRows.Any(row => string.Equals(row.RowId, pendingManualRowId, StringComparison.Ordinal)))
        {
            return;
        }

        pendingManualRowId = null;
    }

    private static string NormalizeReviewParcelGroupId(string? parcelGroupId)
    {
        return string.IsNullOrWhiteSpace(parcelGroupId) || string.Equals(parcelGroupId.Trim(), "Parcel ?", StringComparison.OrdinalIgnoreCase)
            ? "parcel-001"
            : parcelGroupId.Trim();
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
        reviewViewerZoom = 1.0d;
        reviewDirty = false;
        pendingManualRowId = null;
        activeReviewParcelGroupId = null;
        activeReviewParcelName = null;
        activeReviewTraverseId = null;
        reviewContentVersion = 0;
        reviewViewerReloadVersion = 0;
        reviewViewerPageIndex = 0;
        reviewViewerPageCount = 0;
        selectedReviewSourceCopiedPath = null;
        reviewViewerStateCacheKey = null;
        reviewViewerImageSource = null;
        reviewViewerLoadCancellation?.Cancel();
        reviewViewerLoadCancellation?.Dispose();
        reviewViewerLoadCancellation = null;
        reviewViewerState = ReviewSourceViewerStateProjector.Build(null, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
        ReviewRows.Clear();
        ReviewSegments.Clear();
        RefreshWorkflowProperties();
    }

    private async Task ReturnToTransactionListAsync(
        string? transactionNumber,
        string statusText,
        bool preserveSavedMarker,
        bool suppressTransactionFromList,
        bool refreshTransactions)
    {
        ResetWorkflowView(statusText);

        if (FrameworkApplication.DockPaneManager.Find(TransactionPanelDockpaneViewModel.DockPaneId) is TransactionPanelDockpaneViewModel transactionPane)
        {
            await transactionPane.State.HandleWorkflowExitAsync(
                transactionNumber,
                statusText,
                preserveSavedMarker,
                suppressTransactionFromList,
                refreshTransactions).ConfigureAwait(true);
            transactionPane.Activate();
            return;
        }

        FrameworkApplication.DockPaneManager.Find(TransactionPanelDockpaneViewModel.DockPaneId)?.Activate();
    }

    private void RefreshViewerOnlyProperties()
    {
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
        NotifyPropertyChanged(nameof(CanZoomReviewViewerIn));
        NotifyPropertyChanged(nameof(CanZoomReviewViewerOut));
        NotifyPropertyChanged(nameof(CanGoToPreviousReviewViewerPage));
        NotifyPropertyChanged(nameof(CanGoToNextReviewViewerPage));
        NotifyPropertyChanged(nameof(ReviewViewerFitToggleText));
        NotifyPropertyChanged(nameof(ReviewViewerPageStatusText));
        NotifyPropertyChanged(nameof(ReviewViewerZoomText));
        NotifyPropertyChanged(nameof(ReviewViewerImageScale));
        NotifyPropertyChanged(nameof(ReviewViewerImageStretch));
        NotifyPropertyChanged(nameof(ReviewViewerImageSource));
        NotifyPropertyChanged(nameof(ReviewViewerBrowserUri));
        NotifyPropertyChanged(nameof(ReviewViewerNavigationKey));
        reloadReviewViewerCommand.RaiseCanExecuteChanged();
        toggleReviewViewerFitCommand.RaiseCanExecuteChanged();
        zoomInReviewViewerCommand.RaiseCanExecuteChanged();
        zoomOutReviewViewerCommand.RaiseCanExecuteChanged();
        previousReviewViewerPageCommand.RaiseCanExecuteChanged();
        nextReviewViewerPageCommand.RaiseCanExecuteChanged();
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

    public string RoleLabel => SourceRole.DisplayName(SourceFile.SourceRole);

    public string RowStatus => SourceFile.Copied ? "Copied" : SourceFile.Status;

    public string RoleSortKey => $"{SourceFile.SourceRole}|{SourceFile.SourceType}|{FileLabel}";
}

internal sealed record SupportingDocumentStatusItem(
    string DisplayName,
    string RoleLabel,
    string StatusLabel,
    string FileText,
    bool IsRequired,
    bool IsFound)
{
    public string RequirementLabel => IsRequired ? "Required" : "Optional";

    public Brush StatusBackground => IsFound
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F7ED"))
        : IsRequired
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCE8E6"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF4FF"));

    public Brush StatusBorder => IsFound
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F7D4F"))
        : IsRequired
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C53030"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9DB7E8"));

    public Brush StatusForeground => IsFound
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5631"))
        : IsRequired
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A1F17"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2933"));
}

internal sealed record WorkflowLifecycleStep(string Name, string State, string Icon);

internal sealed record PreflightResultListItem(string Severity, PreflightCheck Check)
{
    public string Result => NormalizeOutcome(Check.Outcome, Severity);

    public string CheckName => Humanize(Check.CheckId);

    public string Details => Check.Message;

    public string State =>
        Result switch
        {
            "blocked" or "failed" => "block",
            "warning" => "warn",
            "skipped" or "disabled" or "not_applicable" => "neutral",
            "passed" => "pass",
            _ => Severity.ToLowerInvariant()
        };

    private static string NormalizeOutcome(string? outcome, string severity)
    {
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            return outcome.Trim().ToLowerInvariant();
        }

        return severity.Trim().ToLowerInvariant() switch
        {
            "block" => "failed",
            "warn" => "warning",
            "pass" => "passed",
            _ => severity.Trim().ToLowerInvariant()
        };
    }

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
