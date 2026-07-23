using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn;

internal sealed class JamaicaReviewWorkspaceViewModel : INotifyPropertyChanged
{
    private const double PreviewWidth = 280d;
    private const double PreviewHeight = 210d;
    private readonly ParcelWorkflowDockpaneViewModel parent;
    private JamaicaParcelGroupViewModel? selectedParcelGroup;
    private ExtractionReviewRowViewModel? selectedVisibleRow;
    private ExtractionReviewSegmentViewModel? selectedVisibleSegment;
    private bool isRefreshingProjection;
    private bool isApplyingSelectedParcelGroup;
    private bool suppressParentParcelContextSync;
    private bool suppressParentRowSelectionSync;
    private bool showAllParcelContext;

    internal JamaicaReviewWorkspaceViewModel(ParcelWorkflowDockpaneViewModel parent)
    {
        this.parent = parent;
        parent.PropertyChanged += OnParentPropertyChanged;
        parent.ReviewRows.CollectionChanged += OnReviewRowsCollectionChanged;
        parent.ReviewSegments.CollectionChanged += OnReviewSegmentsCollectionChanged;
        parent.ReviewMetadataFields.CollectionChanged += OnReviewMetadataCollectionChanged;
        parent.ReviewAdjacentOwners.CollectionChanged += OnReviewMetadataCollectionChanged;
        parent.ReviewNamedParties.CollectionChanged += OnReviewMetadataCollectionChanged;
        parent.ReviewVolumeFolios.CollectionChanged += OnReviewMetadataCollectionChanged;
        VisibleRows = [];
        VisibleSegments = [];
        VisibleMetadataFields = [];
        VisibleAdjacentOwners = [];
        VisibleNamedParties = [];
        VisibleVolumeFolios = [];
        ParcelGroups = [];
        RefreshProjection();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<JamaicaParcelGroupViewModel> ParcelGroups { get; }

    public ObservableCollection<ExtractionReviewRowViewModel> VisibleRows { get; }

    public ObservableCollection<ExtractionReviewSegmentViewModel> VisibleSegments { get; }

    public ObservableCollection<ExtractionReviewMetadataFieldViewModel> VisibleMetadataFields { get; }

    public ObservableCollection<ExtractionReviewAdjacentOwnerViewModel> VisibleAdjacentOwners { get; }

    public ObservableCollection<ExtractionReviewNamedPartyViewModel> VisibleNamedParties { get; }

    public ObservableCollection<ExtractionReviewVolumeFolioViewModel> VisibleVolumeFolios { get; }

    public string WindowTitle => "Points Validation Tool";

    public string ExperimentalBannerText => "Review source documents and extracted parcel points together before Create Spatial Units and map-based editing.";

    public string TransactionHeader => parent.HeaderTransactionText;

    public string TransactionTypeHeader => parent.HeaderTaskNameText;

    public string StageHeader => parent.CurrentStepBadge;

    public string WorkspaceStatus => parent.HasLoadedReviewData
        ? "Live extraction review artifact loaded from the current case."
        : "No review artifact loaded yet.";

    public string WorkspaceHostNote => "Verify source documents, confirm parcel interpretation, and prepare the case for Create Spatial Units and Final Review.";

    public bool UsesLiveArtifacts => parent.HasLoadedReviewData;

    public string DataBindingModeText => parent.HasLoadedReviewData
        ? "Live case artifact mode"
        : "No loaded review artifact";

    public string PlaceholderModeText => "Parcel preview and interpretation remain part of the active review workspace.";

    public IReadOnlyList<SourceFileListItem> ReviewSourceOptions => parent.ReviewSourceOptions;

    public SourceFileListItem? SelectedReviewSource
    {
        get => parent.SelectedReviewSource;
        set
        {
            if (!ReferenceEquals(parent.SelectedReviewSource, value))
            {
                parent.SelectedReviewSource = value;
            }
        }
    }

    public ICommand OpenReviewSourceCommand => parent.OpenReviewSourceCommand;

    public ICommand RevealReviewSourceCommand => parent.RevealReviewSourceCommand;

    public ICommand ReloadReviewViewerCommand => parent.ReloadReviewViewerCommand;

    public ICommand ToggleReviewViewerFitCommand => parent.ToggleReviewViewerFitCommand;

    public ICommand ZoomInReviewViewerCommand => parent.ZoomInReviewViewerCommand;

    public ICommand ZoomOutReviewViewerCommand => parent.ZoomOutReviewViewerCommand;

    public ICommand PreviousReviewViewerPageCommand => parent.PreviousReviewViewerPageCommand;

    public ICommand NextReviewViewerPageCommand => parent.NextReviewViewerPageCommand;

    public ICommand AddManualPointCommand => parent.AddManualPointCommand;

    public ICommand EditReviewPointCommand => parent.EditReviewPointCommand;

    public ICommand AddReviewSegmentCommand => parent.AddReviewSegmentCommand;

    public ICommand EditReviewSegmentCommand => parent.EditReviewSegmentCommand;

    public ICommand ExcludeReviewSegmentCommand => parent.ExcludeReviewSegmentCommand;

    public ICommand RebuildBoundaryPointsCommand => parent.RebuildBoundaryPointsCommand;

    public ICommand RemoveManualPointCommand => parent.RemoveManualPointCommand;

    public ICommand CancelPendingManualPointCommand => parent.CancelPendingManualPointCommand;

    public ICommand SaveReviewCommand => parent.SaveReviewCommand;

    public bool HasUnsavedReviewChanges => parent.HasUnsavedReviewChanges;

    public bool CanSaveReview => parent.CanSaveReviewChangesFromWorkspace;

    public bool ShowSaveReviewAction => CanSaveReview;

    public bool ReviewHasBlockers => parent.ReviewHasBlockers;

    public string SelectedReviewRowValidationIssueText => parent.SelectedReviewRowValidationIssueText;

    public bool CanCompleteValidation =>
        parent.HasLoadedReviewData
        && !parent.IsReviewLocked
        && !parent.ReviewHasBlockers
        && !parent.IsManualReviewEditMode;

    public bool ShowValidationCompleteAction => CanCompleteValidation;

    public bool HasReviewSegments => parent.ReviewSegments.Count > 0;

    public bool IsPxaSurveyPlanReview => parent.IsPxaSurveyPlanReview;

    public bool IsStandardPointReview => !IsPxaSurveyPlanReview;

    public bool HasStandardReviewSegments => IsStandardPointReview && HasReviewSegments;

    public string CenterReviewTitle => IsPxaSurveyPlanReview
        ? "PXA Survey Plan Review"
        : "Validate Points";

    public string SegmentReviewSummary =>
        HasReviewSegments
            ? $"{parent.ReviewSegments.Count} reviewed segment candidate(s). Edit the boundary chain before saving or completing validation."
            : "No segment candidates are available for this review artifact.";

    public bool HasPxaMetadata => VisibleMetadataFields.Count > 0
        || VisibleAdjacentOwners.Count > 0
        || VisibleNamedParties.Count > 0
        || VisibleVolumeFolios.Count > 0;

    public string PxaMetadataSummary => VisibleMetadataFields.Count > 0
        ? $"{VisibleMetadataFields.Count} survey metadata value(s), {VisibleNamedParties.Count} party / representative row(s), {VisibleVolumeFolios.Count} volume-folio row(s). Confirm extracted values before completing validation."
        : "No PXA survey metadata values were extracted yet.";

    public string PxaGeneralInfoSummary => VisibleMetadataFields.Count > 0 || VisibleVolumeFolios.Count > 0
        ? $"{VisibleMetadataFields.Count} general survey value(s), {VisibleVolumeFolios.Count} volume / folio row(s). Confirm document dates, instrument, surveyor, and registration details."
        : "No PXA general survey information was extracted yet.";

    public string PxaOwnersNeighborsSummary => VisibleNamedParties.Count > 0 || VisibleAdjacentOwners.Count > 0
        ? $"{VisibleNamedParties.Count} party / representative row(s), {VisibleAdjacentOwners.Count} adjacent owner / neighbor reference(s). Link neighbors to reviewed boundary segments when visible on the plan."
        : "No owner, representative, or neighbor references were extracted yet.";

    public string PxaAdjacentOwnerSummary => VisibleAdjacentOwners.Count > 0
        ? $"{VisibleAdjacentOwners.Count} adjacent owner / party reference(s). Link owners to reviewed boundary segments when visible on the plan."
        : "No adjacent owner / party references were extracted yet.";

    public string ViewerFileTitle => parent.ReviewViewerFileTitle;

    public string ActiveSourceInstruction => ReviewSourceOptions.Count > 1
        ? "One source document is shown at a time. Switch the active document here when you need to verify a different file."
        : "This workspace shows one source document at a time for focused verification.";

    public string ViewerRoleLabel => parent.ReviewViewerRoleLabel;

    public string ViewerDisplayPath => parent.ReviewViewerDisplayPath;

    public string ViewerModeLabel => parent.ReviewViewerModeLabel;

    public string ViewerLoadState => parent.ReviewViewerLoadState;

    public string ViewerGuidance => parent.ReviewViewerGuidance;

    public string ViewerFallbackMessage => parent.ReviewViewerFallbackMessage;

    public bool ViewerUsesImage => parent.ReviewViewerUsesImage;

    public bool ViewerUsesBrowser => parent.ReviewViewerUsesBrowser;

    public bool ViewerShowsFallback => parent.ReviewViewerShowsFallback;

    public bool ShowCustomViewerControls => parent.ReviewViewerUsesImage;

    public bool ShowPdfViewerHelp => parent.ReviewViewerUsesBrowser;

    public string PdfViewerHelpText => "Use the PDF toolbar below for page navigation and zoom.";

    public bool CanToggleViewerFit => parent.CanToggleReviewViewerFit;

    public string ViewerFitToggleText => parent.ReviewViewerFitToggleText;

    public bool CanZoomViewerIn => parent.CanZoomReviewViewerIn;

    public bool CanZoomViewerOut => parent.CanZoomReviewViewerOut;

    public bool CanGoToPreviousViewerPage => parent.CanGoToPreviousReviewViewerPage;

    public bool CanGoToNextViewerPage => parent.CanGoToNextReviewViewerPage;

    public string ViewerPageStatusText => parent.ReviewViewerPageStatusText;

    public string ViewerZoomText => parent.ReviewViewerZoomText;

    public Stretch ViewerImageStretch => parent.ReviewViewerImageStretch;

    public double ViewerImageScale => parent.ReviewViewerImageScale;

    public ImageSource? ViewerImageSource => parent.ReviewViewerImageSource;

    public Uri? ViewerBrowserUri => parent.ReviewViewerBrowserUri;

    public string ViewerNavigationKey => parent.ReviewViewerNavigationKey;

    public string UnsupportedFallbackSummary
    {
        get
        {
            var unsupported = ReviewSourceOptions
                .Where(item => !IsEmbeddable(item.SourceFile.FileType))
                .Select(item => item.FileLabel)
                .ToArray();

            return unsupported.Length > 0
                ? $"Fallback path demonstrated by unsupported source(s): {string.Join(", ", unsupported)}. Use Open source or Open in folder instead of embedded rendering."
                : "TXT/CSV and other unsupported formats should stay reviewable through the center grid while opening externally from the source tools.";
        }
    }

    public JamaicaParcelGroupViewModel? SelectedParcelGroup
    {
        get => selectedParcelGroup;
        set
        {
            if (!CanChangeParcelGroup && selectedParcelGroup is not null && !ReferenceEquals(selectedParcelGroup, value))
            {
                return;
            }

            if (ReferenceEquals(selectedParcelGroup, value))
            {
                return;
            }

            if (isApplyingSelectedParcelGroup)
            {
                selectedParcelGroup = value;
                return;
            }

            selectedParcelGroup = value;
            if (!suppressParentParcelContextSync)
            {
                parent.SetReviewWorkspaceParcelContext(value?.GroupId, value?.DisplayName, value?.TraverseId, refreshProperties: false);
            }

            selectedVisibleRow = null;
            RebuildVisibleRows();
            if (selectedVisibleRow is not null)
            {
                parent.SelectedReviewRow = selectedVisibleRow;
            }

            OnPropertyChanged(nameof(VisibleRows));
            OnPropertyChanged(nameof(SelectedParcelGroup));
            OnPropertyChanged(nameof(SelectedParcelTitle));
            OnPropertyChanged(nameof(ParcelInterpretationSummary));
            OnPropertyChanged(nameof(ParcelInterpretationIssues));
            OnPropertyChanged(nameof(ActiveParcelDiagnosticsSummary));
            OnPropertyChanged(nameof(ActiveParcelDiagnosticsOverview));
            OnPropertyChanged(nameof(ActiveParcelBlockedDiagnostics));
            OnPropertyChanged(nameof(ActiveParcelWarningDiagnostics));
            OnPropertyChanged(nameof(ActiveParcelPassedDiagnostics));
            OnPropertyChanged(nameof(HasBlockedDiagnostics));
            OnPropertyChanged(nameof(HasWarningDiagnostics));
            OnPropertyChanged(nameof(HasPassedDiagnostics));
            OnPropertyChanged(nameof(ActiveParcelDiagnosticsEmptyState));
            OnPropertyChanged(nameof(ParcelPreviewPoints));
            OnPropertyChanged(nameof(ParcelContextPreviewPaths));
            OnPropertyChanged(nameof(SelectedPointPreview));
            OnPropertyChanged(nameof(SelectedRowSummary));
            OnPropertyChanged(nameof(CanChangeParcelGroup));
        }
    }

    public ExtractionReviewRowViewModel? SelectedVisibleRow
    {
        get => selectedVisibleRow;
        set
        {
            if (ReferenceEquals(selectedVisibleRow, value))
            {
                return;
            }

            selectedVisibleRow = value;
            if (!suppressParentRowSelectionSync)
            {
                parent.SelectedReviewRow = value;
            }

            OnPropertyChanged(nameof(SelectedVisibleRow));
            OnPropertyChanged(nameof(SelectedPointPreview));
            OnPropertyChanged(nameof(SelectedRowSummary));
            OnPropertyChanged(nameof(ParcelPreviewPoints));
            OnPropertyChanged(nameof(ParcelContextPreviewPaths));
            if (EditReviewPointCommand is RelayCommand editCommand)
            {
                editCommand.RaiseCanExecuteChanged();
            }

            if (RemoveManualPointCommand is RelayCommand removeCommand)
            {
                removeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ExtractionReviewSegmentViewModel? SelectedVisibleSegment
    {
        get => selectedVisibleSegment;
        set
        {
            if (ReferenceEquals(selectedVisibleSegment, value))
            {
                return;
            }

            selectedVisibleSegment = value;
            OnPropertyChanged(nameof(SelectedVisibleSegment));
            if (EditReviewSegmentCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }

            if (ExcludeReviewSegmentCommand is RelayCommand excludeCommand)
            {
                excludeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedParcelTitle => SelectedParcelGroup is null
        ? "No parcel group selected"
        : $"{SelectedParcelGroup.DisplayName} - current review focus";

    public bool CanChangeParcelGroup => parent.CanChangeReviewParcelSelection && ParcelGroups.Count > 1;

    public bool IsParcelSelectionReadOnly => ParcelGroups.Count <= 1;

    public bool IsManualReviewEditMode => parent.IsManualReviewEditMode;

    public bool ShowAllParcelContext
    {
        get => showAllParcelContext;
        set
        {
            if (showAllParcelContext == value)
            {
                return;
            }

            showAllParcelContext = value;
            OnPropertyChanged(nameof(ShowAllParcelContext));
            OnPropertyChanged(nameof(ParcelContextPreviewPaths));
        }
    }

    public string ParcelInterpretationSummary
    {
        get
        {
            if (SelectedParcelGroup is null)
            {
                return "Parcel interpretation becomes available after review data is loaded.";
            }

            return $"{SelectedParcelGroup.RowCount} point row(s), {SelectedParcelGroup.UnresolvedCount} need review, {SelectedParcelGroup.EditedCount} edited, {SelectedParcelGroup.BoundaryBreakCount} boundary break(s).";
        }
    }

    public string ParcelInterpretationIssues
    {
        get
        {
            if (SelectedParcelGroup is null)
            {
                return "No parcel grouping issues available.";
            }

            var issues = new List<string>();
            if (SelectedParcelGroup.BoundaryBreakCount > 0)
            {
                issues.Add($"{SelectedParcelGroup.BoundaryBreakCount} boundary break marker(s) suggest this source may contain multiple parcel sequences.");
            }

            if (SelectedParcelGroup.UnresolvedCount > 0)
            {
                issues.Add($"{SelectedParcelGroup.UnresolvedCount} row(s) still need examiner confirmation before this parcel can move forward.");
            }

            if (SelectedParcelGroup.LowConfidenceCount > 0)
            {
                issues.Add($"{SelectedParcelGroup.LowConfidenceCount} row(s) were grouped with low or unknown confidence.");
            }

            return issues.Count > 0
                ? string.Join(Environment.NewLine, issues)
                : "No parcel-group warnings are active for this parcel right now.";
        }
    }

    public string ActiveParcelDiagnosticsSummary
    {
        get
        {
            if (SelectedParcelGroup is null)
            {
                return "Select a parcel to review its closure and readiness details.";
            }

            var closure = parent.ReviewValidationResult.ClosureResults
                .FirstOrDefault(result => string.Equals(result.ParcelGroupId, SelectedParcelGroup.GroupId, StringComparison.OrdinalIgnoreCase));
            var readinessResults = parent.ReviewValidationResult.ReadinessResults
                .Where(result => string.Equals(result.ParcelGroupId, SelectedParcelGroup.GroupId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var readinessBlocker = readinessResults.FirstOrDefault(result => result.Status == ReadinessValidationStatus.Blocker);
            var readinessWarning = readinessResults.FirstOrDefault(result => result.Status == ReadinessValidationStatus.Warning);

            if (readinessBlocker is not null)
            {
                return $"This parcel is not ready for Create Spatial Units. {readinessBlocker.Title}.";
            }

            if (readinessWarning is not null)
            {
                return $"This parcel can be reviewed further. {readinessWarning.Title}.";
            }

            if (closure is null)
            {
                return $"{SelectedParcelGroup.RowCount} point row(s) are in this parcel. No closure diagnostic is available yet.";
            }

            var status = closure.Status switch
            {
                ClosureValidationStatus.Passed => "Passed",
                ClosureValidationStatus.Warning => "Warning",
                ClosureValidationStatus.Blocker => "Blocked",
                _ => "Unknown"
            };

            var distance = closure.ClosureDistanceM.HasValue
                ? $"{closure.ClosureDistanceM.Value.ToString("0.###", CultureInfo.InvariantCulture)} m"
                : "--";
            var ratio = closure.MiscloseRatioDenominator.HasValue
                ? $"1:{Math.Round(closure.MiscloseRatioDenominator.Value):0}"
                : "--";

            return $"Closure {status}. Tolerance profile: {closure.ProfileTitle}. Misclose distance: {distance}. Misclose ratio: {ratio}.";
        }
    }

    public string ActiveParcelDiagnosticsOverview
    {
        get
        {
            if (SelectedParcelGroup is null)
            {
                return "Parcel-specific diagnostics appear here after a parcel is selected.";
            }

            var overview = new List<string>
            {
                $"{SelectedParcelGroup.RowCount} point row(s)",
                $"{SelectedParcelGroup.EditedCount} edited",
                $"{SelectedParcelGroup.UnresolvedCount} still need review",
                $"{SelectedParcelGroup.BoundaryBreakCount} boundary break(s)"
            };

            return string.Join(Environment.NewLine, overview.Distinct(StringComparer.Ordinal));
        }
    }

    public string ActiveParcelBlockedDiagnostics => string.Join(Environment.NewLine, GetDiagnosticsByStatus(DiagnosticBucket.Blocked));

    public string ActiveParcelWarningDiagnostics => string.Join(Environment.NewLine, GetDiagnosticsByStatus(DiagnosticBucket.Warning));

    public string ActiveParcelPassedDiagnostics => string.Join(Environment.NewLine, GetDiagnosticsByStatus(DiagnosticBucket.Passed));

    public bool HasBlockedDiagnostics => !string.IsNullOrWhiteSpace(ActiveParcelBlockedDiagnostics);

    public bool HasWarningDiagnostics => !string.IsNullOrWhiteSpace(ActiveParcelWarningDiagnostics);

    public bool HasPassedDiagnostics => !string.IsNullOrWhiteSpace(ActiveParcelPassedDiagnostics);

    public string ActiveParcelDiagnosticsEmptyState
    {
        get
        {
            if (SelectedParcelGroup is null)
            {
                return "Parcel-specific diagnostics appear here after you select a parcel.";
            }

            return !HasBlockedDiagnostics && !HasWarningDiagnostics && !HasPassedDiagnostics
                ? "No parcel-scoped validation diagnostics were produced for this parcel."
                : string.Empty;
        }
    }

    public string SelectedRowSummary => SelectedVisibleRow is null
        ? "Select a row to inspect its point, sequence, and preview marker."
        : $"Selected point {BlankIfEmpty(SelectedVisibleRow.PointIdentifier)} - Easting {BlankIfEmpty(SelectedVisibleRow.Easting)}, Northing {BlankIfEmpty(SelectedVisibleRow.Northing)}, Status {BlankIfEmpty(SelectedVisibleRow.ExtractionStatus)}.";

    public PointCollection ParcelPreviewPoints => BuildPreviewPoints();

    public IReadOnlyList<PreviewPath> ParcelContextPreviewPaths => BuildPreviewPaths();

    public PreviewMarker? SelectedPointPreview => BuildSelectedMarker();

    public string ApprovalGuidance => parent.ReviewGateText;

    public string ReviewBadge => parent.ReviewBadgeText;

    public bool IsReviewLocked => parent.IsReviewLocked;

    public void Detach()
    {
        parent.PropertyChanged -= OnParentPropertyChanged;
        parent.ReviewRows.CollectionChanged -= OnReviewRowsCollectionChanged;
        parent.ReviewSegments.CollectionChanged -= OnReviewSegmentsCollectionChanged;
        parent.ReviewMetadataFields.CollectionChanged -= OnReviewMetadataCollectionChanged;
        parent.ReviewAdjacentOwners.CollectionChanged -= OnReviewMetadataCollectionChanged;
        parent.ReviewNamedParties.CollectionChanged -= OnReviewMetadataCollectionChanged;
        parent.ReviewVolumeFolios.CollectionChanged -= OnReviewMetadataCollectionChanged;
    }

    public bool SaveReviewChanges()
    {
        return parent.SaveReviewChangesFromWorkspace();
    }

    public bool ContinueToCreateSpatialUnits()
    {
        return parent.ContinueToCreateSpatialUnitsFromWorkspace();
    }

    internal void EditSelectedSegment()
    {
        if (SelectedVisibleSegment is not null && EditReviewSegmentCommand.CanExecute(SelectedVisibleSegment))
        {
            EditReviewSegmentCommand.Execute(SelectedVisibleSegment);
        }
    }

    public bool DiscardUnsavedReviewChanges()
    {
        return parent.DiscardUnsavedReviewChangesFromWorkspace();
    }

    public void HandleWindowClosed(bool reviewSaved, bool continuedToCreateSpatialUnits, bool discardedUnsavedChanges)
    {
        parent.HandlePointsValidationWorkspaceClosed(reviewSaved, continuedToCreateSpatialUnits, discardedUnsavedChanges);
    }

    private IEnumerable<string> GetDiagnosticsByStatus(DiagnosticBucket bucket)
    {
        if (SelectedParcelGroup is null)
        {
            return Array.Empty<string>();
        }

        var details = new List<string>();

        var closure = parent.ReviewValidationResult.ClosureResults
            .FirstOrDefault(result => string.Equals(result.ParcelGroupId, SelectedParcelGroup.GroupId, StringComparison.OrdinalIgnoreCase));
        if (closure is not null)
        {
            var closureBucket = closure.Status switch
            {
                ClosureValidationStatus.Blocker => DiagnosticBucket.Blocked,
                ClosureValidationStatus.Warning => DiagnosticBucket.Warning,
                ClosureValidationStatus.Passed => DiagnosticBucket.Passed,
                _ => DiagnosticBucket.None
            };

            if (closureBucket == bucket && !string.IsNullOrWhiteSpace(closure.Message))
            {
                var prefix = string.IsNullOrWhiteSpace(closure.ProfileRuleId)
                    ? "Closure"
                    : $"Closure / {FormatRuleLabel(closure.ProfileRuleId, closure.ProfileTitle)}";
                details.Add($"{prefix}: {closure.Message}");
            }
        }

        foreach (var readinessResult in parent.ReviewValidationResult.ReadinessResults
                     .Where(result => string.Equals(result.ParcelGroupId, SelectedParcelGroup.GroupId, StringComparison.OrdinalIgnoreCase)))
        {
            var readinessBucket = readinessResult.Status switch
            {
                ReadinessValidationStatus.Blocker => DiagnosticBucket.Blocked,
                ReadinessValidationStatus.Warning => DiagnosticBucket.Warning,
                ReadinessValidationStatus.Passed => DiagnosticBucket.Passed,
                _ => DiagnosticBucket.None
            };

            if (readinessBucket == bucket && !string.IsNullOrWhiteSpace(readinessResult.Message))
            {
                var prefix = string.IsNullOrWhiteSpace(readinessResult.RuleId)
                    ? BlankIfEmpty(readinessResult.Title)
                    : FormatRuleLabel(readinessResult.RuleId, readinessResult.Title);
                details.Add($"{prefix}: {readinessResult.Message}");
            }
        }

        if (bucket == DiagnosticBucket.Blocked
            && parent.ReviewValidationResult.ParcelIssues.TryGetValue(SelectedParcelGroup.GroupId, out var parcelIssue)
            && !string.IsNullOrWhiteSpace(parcelIssue))
        {
            details.Add($"Parcel issue: {parcelIssue}");
        }

        return details.Distinct(StringComparer.Ordinal);
    }

    private void OnReviewRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshProjection();
    }

    private void OnReviewSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildVisibleSegments();
        OnPropertyChanged(nameof(HasReviewSegments));
        OnPropertyChanged(nameof(HasStandardReviewSegments));
        OnPropertyChanged(nameof(SegmentReviewSummary));
        OnPropertyChanged(nameof(ParcelPreviewPoints));
        OnPropertyChanged(nameof(SelectedPointPreview));
        OnPropertyChanged(nameof(CanCompleteValidation));
    }

    private void OnReviewMetadataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildVisibleMetadata();
        OnPropertyChanged(nameof(HasPxaMetadata));
        OnPropertyChanged(nameof(PxaMetadataSummary));
        OnPropertyChanged(nameof(PxaGeneralInfoSummary));
        OnPropertyChanged(nameof(PxaOwnersNeighborsSummary));
        OnPropertyChanged(nameof(PxaAdjacentOwnerSummary));
    }

    private void OnParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ParcelWorkflowDockpaneViewModel.SelectedReviewRow):
                selectedVisibleRow = parent.SelectedReviewRow;
                OnPropertyChanged(nameof(SelectedVisibleRow));
                OnPropertyChanged(nameof(SelectedRowSummary));
                OnPropertyChanged(nameof(ParcelPreviewPoints));
                OnPropertyChanged(nameof(ParcelContextPreviewPaths));
                OnPropertyChanged(nameof(SelectedPointPreview));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.SelectedReviewSource):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerFileTitle):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerRoleLabel):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerDisplayPath):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerModeLabel):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerLoadState):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerGuidance):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerFallbackMessage):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerUsesImage):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerUsesBrowser):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerShowsFallback):
            case nameof(ParcelWorkflowDockpaneViewModel.CanToggleReviewViewerFit):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerFitToggleText):
            case nameof(ParcelWorkflowDockpaneViewModel.CanZoomReviewViewerIn):
            case nameof(ParcelWorkflowDockpaneViewModel.CanZoomReviewViewerOut):
            case nameof(ParcelWorkflowDockpaneViewModel.CanGoToPreviousReviewViewerPage):
            case nameof(ParcelWorkflowDockpaneViewModel.CanGoToNextReviewViewerPage):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerPageStatusText):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerZoomText):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerImageStretch):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerImageScale):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerImageSource):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerBrowserUri):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewViewerNavigationKey):
                NotifyViewerProperties();
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewRows):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewSegments):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewMetadataFields):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewAdjacentOwners):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewNamedParties):
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewVolumeFolios):
            case nameof(ParcelWorkflowDockpaneViewModel.IsPxaSurveyPlanReview):
            case nameof(ParcelWorkflowDockpaneViewModel.HasLoadedReviewData):
            case nameof(ParcelWorkflowDockpaneViewModel.HasSingleReviewParcelGroup):
                RefreshProjection();
                OnPropertyChanged(nameof(HasReviewSegments));
                OnPropertyChanged(nameof(HasStandardReviewSegments));
                OnPropertyChanged(nameof(SegmentReviewSummary));
                OnPropertyChanged(nameof(HasPxaMetadata));
                OnPropertyChanged(nameof(PxaMetadataSummary));
                OnPropertyChanged(nameof(PxaGeneralInfoSummary));
                OnPropertyChanged(nameof(PxaOwnersNeighborsSummary));
                OnPropertyChanged(nameof(PxaAdjacentOwnerSummary));
                OnPropertyChanged(nameof(IsPxaSurveyPlanReview));
                OnPropertyChanged(nameof(IsStandardPointReview));
                OnPropertyChanged(nameof(CenterReviewTitle));
                OnPropertyChanged(nameof(HasUnsavedReviewChanges));
                OnPropertyChanged(nameof(CanSaveReview));
                OnPropertyChanged(nameof(ShowSaveReviewAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewGateText):
                OnPropertyChanged(nameof(ApprovalGuidance));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewHasBlockers):
            case nameof(ParcelWorkflowDockpaneViewModel.SelectedReviewRowValidationIssueText):
                OnPropertyChanged(nameof(ReviewHasBlockers));
                OnPropertyChanged(nameof(SelectedReviewRowValidationIssueText));
                OnPropertyChanged(nameof(CanCompleteValidation));
                OnPropertyChanged(nameof(ShowValidationCompleteAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewBadgeText):
                OnPropertyChanged(nameof(ReviewBadge));
                OnPropertyChanged(nameof(HasUnsavedReviewChanges));
                OnPropertyChanged(nameof(CanSaveReview));
                OnPropertyChanged(nameof(ShowSaveReviewAction));
                OnPropertyChanged(nameof(CanCompleteValidation));
                OnPropertyChanged(nameof(ShowValidationCompleteAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.IsReviewLocked):
                OnPropertyChanged(nameof(IsReviewLocked));
                OnPropertyChanged(nameof(CanSaveReview));
                OnPropertyChanged(nameof(ShowSaveReviewAction));
                OnPropertyChanged(nameof(CanCompleteValidation));
                OnPropertyChanged(nameof(ShowValidationCompleteAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.IsManualReviewEditMode):
            case nameof(ParcelWorkflowDockpaneViewModel.CanChangeReviewParcelSelection):
                OnPropertyChanged(nameof(IsManualReviewEditMode));
                OnPropertyChanged(nameof(CanChangeParcelGroup));
                OnPropertyChanged(nameof(IsParcelSelectionReadOnly));
                OnPropertyChanged(nameof(CanSaveReview));
                OnPropertyChanged(nameof(ShowSaveReviewAction));
                OnPropertyChanged(nameof(CanCompleteValidation));
                OnPropertyChanged(nameof(ShowValidationCompleteAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.HasUnsavedReviewChanges):
            case nameof(ParcelWorkflowDockpaneViewModel.CanSaveReviewChangesFromWorkspace):
                OnPropertyChanged(nameof(HasUnsavedReviewChanges));
                OnPropertyChanged(nameof(CanSaveReview));
                OnPropertyChanged(nameof(ShowSaveReviewAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.ReviewContentVersion):
                OnPropertyChanged(nameof(ParcelInterpretationSummary));
                OnPropertyChanged(nameof(ParcelInterpretationIssues));
                OnPropertyChanged(nameof(ActiveParcelDiagnosticsSummary));
                OnPropertyChanged(nameof(ActiveParcelDiagnosticsOverview));
                OnPropertyChanged(nameof(ActiveParcelBlockedDiagnostics));
                OnPropertyChanged(nameof(ActiveParcelWarningDiagnostics));
                OnPropertyChanged(nameof(ActiveParcelPassedDiagnostics));
                OnPropertyChanged(nameof(HasBlockedDiagnostics));
                OnPropertyChanged(nameof(HasWarningDiagnostics));
                OnPropertyChanged(nameof(HasPassedDiagnostics));
                OnPropertyChanged(nameof(ActiveParcelDiagnosticsEmptyState));
                OnPropertyChanged(nameof(ParcelPreviewPoints));
                OnPropertyChanged(nameof(ParcelContextPreviewPaths));
                OnPropertyChanged(nameof(SelectedPointPreview));
                OnPropertyChanged(nameof(SelectedRowSummary));
                OnPropertyChanged(nameof(HasReviewSegments));
                OnPropertyChanged(nameof(HasStandardReviewSegments));
                OnPropertyChanged(nameof(SegmentReviewSummary));
                OnPropertyChanged(nameof(HasPxaMetadata));
                OnPropertyChanged(nameof(PxaMetadataSummary));
                OnPropertyChanged(nameof(PxaGeneralInfoSummary));
                OnPropertyChanged(nameof(PxaOwnersNeighborsSummary));
                OnPropertyChanged(nameof(PxaAdjacentOwnerSummary));
                OnPropertyChanged(nameof(HasUnsavedReviewChanges));
                OnPropertyChanged(nameof(CanSaveReview));
                OnPropertyChanged(nameof(ShowSaveReviewAction));
                OnPropertyChanged(nameof(CanCompleteValidation));
                OnPropertyChanged(nameof(ShowValidationCompleteAction));
                break;
            case nameof(ParcelWorkflowDockpaneViewModel.CurrentStepBadge):
            case nameof(ParcelWorkflowDockpaneViewModel.HeaderTransactionText):
            case nameof(ParcelWorkflowDockpaneViewModel.HeaderTaskNameText):
                OnPropertyChanged(nameof(StageHeader));
                OnPropertyChanged(nameof(TransactionHeader));
                OnPropertyChanged(nameof(TransactionTypeHeader));
                break;
        }
    }

    private void RefreshProjection()
    {
        if (isRefreshingProjection)
        {
            return;
        }

        isRefreshingProjection = true;
        try
        {
            var grouped = parent.ReviewRows
                .GroupBy(row => string.IsNullOrWhiteSpace(row.ParcelGroupId) ? "Ungrouped" : row.ParcelGroupId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new JamaicaParcelGroupViewModel(
                    group.Key,
                    ResolveParcelDisplayName(group.Key, group.ToArray()),
                    group.OrderBy(row => row.SequenceInGroup ?? int.MaxValue)
                        .ThenBy(row => row.PointIdentifier, StringComparer.OrdinalIgnoreCase)
                        .ToArray()))
                .OrderBy(group => group.SortKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            ParcelGroups.Clear();
            foreach (var group in grouped)
            {
                ParcelGroups.Add(group);
            }

            var preferredGroupKey = selectedParcelGroup?.GroupId;
            var nextGroup = ParcelGroups.FirstOrDefault(group =>
                                string.Equals(group.GroupId, preferredGroupKey, StringComparison.OrdinalIgnoreCase))
                            ?? ParcelGroups.FirstOrDefault(group =>
                                string.Equals(group.GroupId, parent.SelectedReviewRow?.ParcelGroupId, StringComparison.OrdinalIgnoreCase))
                            ?? ParcelGroups.FirstOrDefault();

            suppressParentParcelContextSync = true;
            isApplyingSelectedParcelGroup = true;
            try
            {
                SelectedParcelGroup = nextGroup;
            }
            finally
            {
                isApplyingSelectedParcelGroup = false;
                suppressParentParcelContextSync = false;
            }

            RebuildVisibleRows();
            RebuildVisibleSegments();
            RebuildVisibleMetadata();
            OnPropertyChanged(nameof(UsesLiveArtifacts));
            OnPropertyChanged(nameof(WorkspaceStatus));
            OnPropertyChanged(nameof(DataBindingModeText));
            OnPropertyChanged(nameof(UnsupportedFallbackSummary));
            OnPropertyChanged(nameof(ApprovalGuidance));
            OnPropertyChanged(nameof(ReviewBadge));
            OnPropertyChanged(nameof(IsReviewLocked));
            OnPropertyChanged(nameof(IsManualReviewEditMode));
            OnPropertyChanged(nameof(CanChangeParcelGroup));
            OnPropertyChanged(nameof(IsParcelSelectionReadOnly));
            OnPropertyChanged(nameof(HasUnsavedReviewChanges));
            OnPropertyChanged(nameof(CanSaveReview));
            OnPropertyChanged(nameof(ShowSaveReviewAction));
            OnPropertyChanged(nameof(CanCompleteValidation));
            OnPropertyChanged(nameof(ShowValidationCompleteAction));
            OnPropertyChanged(nameof(HasReviewSegments));
            OnPropertyChanged(nameof(HasStandardReviewSegments));
            OnPropertyChanged(nameof(SegmentReviewSummary));
            OnPropertyChanged(nameof(HasPxaMetadata));
            OnPropertyChanged(nameof(PxaMetadataSummary));
            OnPropertyChanged(nameof(PxaGeneralInfoSummary));
            OnPropertyChanged(nameof(PxaOwnersNeighborsSummary));
            OnPropertyChanged(nameof(PxaAdjacentOwnerSummary));
            OnPropertyChanged(nameof(IsPxaSurveyPlanReview));
            OnPropertyChanged(nameof(IsStandardPointReview));
            OnPropertyChanged(nameof(CenterReviewTitle));
        }
        finally
        {
            isRefreshingProjection = false;
        }
    }

    private void RebuildVisibleRows()
    {
        var rows = SelectedParcelGroup?.Rows ?? parent.ReviewRows.ToArray();
        VisibleRows.Clear();
        foreach (var row in rows)
        {
            VisibleRows.Add(row);
        }

        suppressParentRowSelectionSync = true;
        try
        {
            SelectedVisibleRow = rows.FirstOrDefault(row => ReferenceEquals(row, parent.SelectedReviewRow))
                ?? rows.FirstOrDefault();
        }
        finally
        {
            suppressParentRowSelectionSync = false;
        }

        OnPropertyChanged(nameof(ParcelPreviewPoints));
        OnPropertyChanged(nameof(ParcelContextPreviewPaths));
        OnPropertyChanged(nameof(SelectedPointPreview));
        OnPropertyChanged(nameof(SelectedRowSummary));
    }

    private void RebuildVisibleSegments()
    {
        var previousSegmentId = SelectedVisibleSegment?.SegmentId;
        var segments = parent.ReviewSegments
            .OrderBy(segment => segment.Sequence ?? int.MaxValue)
            .ThenBy(segment => segment.FromPoint, StringComparer.OrdinalIgnoreCase)
            .ThenBy(segment => segment.ToPoint, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        VisibleSegments.Clear();
        foreach (var segment in segments)
        {
            VisibleSegments.Add(segment);
        }

        SelectedVisibleSegment = segments.FirstOrDefault(segment =>
                                     string.Equals(segment.SegmentId, previousSegmentId, StringComparison.OrdinalIgnoreCase))
                                 ?? segments.FirstOrDefault();
    }

    private void RebuildVisibleMetadata()
    {
        VisibleMetadataFields.Clear();
        foreach (var field in parent.ReviewMetadataFields
                     .OrderBy(field => field.Label, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(field => field.Key, StringComparer.OrdinalIgnoreCase))
        {
            VisibleMetadataFields.Add(field);
        }

        VisibleAdjacentOwners.Clear();
        foreach (var owner in parent.ReviewAdjacentOwners
                     .OrderBy(owner => owner.RelatedSegmentFrom, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(owner => owner.RelatedSegmentTo, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(owner => owner.Name, StringComparer.OrdinalIgnoreCase))
        {
            VisibleAdjacentOwners.Add(owner);
        }

        VisibleNamedParties.Clear();
        foreach (var party in parent.ReviewNamedParties
                     .OrderBy(party => party.SourceGroup, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(party => party.Role, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(party => party.Name, StringComparer.OrdinalIgnoreCase))
        {
            VisibleNamedParties.Add(party);
        }

        VisibleVolumeFolios.Clear();
        foreach (var volumeFolio in parent.ReviewVolumeFolios
                     .OrderBy(item => item.Volume, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Folio, StringComparer.OrdinalIgnoreCase))
        {
            VisibleVolumeFolios.Add(volumeFolio);
        }
    }

    private PointCollection BuildPreviewPoints()
    {
        if (IsPxaSurveyPlanReview)
        {
            var segmentDerivedPoints = BuildPreviewPointsFromReviewedSegments(VisibleSegments, true);
            if (segmentDerivedPoints.Count > 0)
            {
                return ScaleToPreview(segmentDerivedPoints);
            }

            var segmentPathRows = BuildPreviewRowsInReviewedSegmentPath(VisibleRows, VisibleSegments, true);
            if (segmentPathRows.Length > 0)
            {
                var segmentPathPoints = segmentPathRows
                    .Select((row, index) => TryBuildActualPoint(row, index))
                    .ToArray();
                if (segmentPathPoints.All(item => item.HasValue))
                {
                    return ScaleToPreview(segmentPathPoints.Select(item => item!.Value).ToArray());
                }
            }
        }

        var rows = BuildPreviewRowsInReviewedSegmentOrder();
        if (rows.Length == 0)
        {
            return [];
        }

        var actualPoints = rows
            .Select((row, index) => TryBuildActualPoint(row, index))
            .ToArray();

        if (actualPoints.All(item => item.HasValue))
        {
            var source = actualPoints.Select(item => item!.Value).ToArray();
            var shouldCloseRing = !IsPxaSurveyPlanReview
                || ReviewedSegmentChainCloses(VisibleSegments);
            return ScaleToPreview(shouldCloseRing
                ? ClosePreviewRingIfNeeded(source)
                : source);
        }

        return BuildSyntheticPreview(rows.Length);
    }

    private PreviewMarker? BuildSelectedMarker()
    {
        if (SelectedVisibleRow is null)
        {
            return null;
        }

        var points = ParcelPreviewPoints;
        var rows = BuildPreviewRowsInReviewedSegmentOrder();
        var index = Array.IndexOf(rows, SelectedVisibleRow);
        if (index < 0 || index >= points.Count)
        {
            return null;
        }

        return new PreviewMarker(points[index].X, points[index].Y, BlankIfEmpty(SelectedVisibleRow.PointIdentifier));
    }

    private ExtractionReviewRowViewModel[] BuildPreviewRowsInReviewedSegmentOrder()
    {
        return BuildPreviewRowsInReviewedSegmentOrder(VisibleRows, VisibleSegments, IsPxaSurveyPlanReview);
    }

    internal static ExtractionReviewRowViewModel[] BuildPreviewRowsInReviewedSegmentOrder(
        IReadOnlyCollection<ExtractionReviewRowViewModel> visibleRows,
        IReadOnlyCollection<ExtractionReviewSegmentViewModel> visibleSegments,
        bool isPxaSurveyPlanReview)
    {
        var rows = visibleRows.ToArray();
        if (rows.Length == 0 || visibleSegments.Count == 0 || !isPxaSurveyPlanReview)
        {
            return rows;
        }

        var rowsByPoint = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.PointIdentifier))
            .GroupBy(row => row.PointIdentifier.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var orderedRows = new List<ExtractionReviewRowViewModel>();
        foreach (var segment in visibleSegments
                     .Where(segment => segment.IncludeInBoundary)
                     .OrderBy(segment => segment.Sequence ?? int.MaxValue)
                     .ThenBy(segment => segment.FromPoint, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(segment => segment.ToPoint, StringComparer.OrdinalIgnoreCase))
        {
            AddSegmentPointIfKnown(segment.FromPoint);
            AddSegmentPointIfKnown(segment.ToPoint);
        }

        foreach (var row in rows)
        {
            if (!orderedRows.Contains(row))
            {
                orderedRows.Add(row);
            }
        }

        return orderedRows.ToArray();

        void AddSegmentPointIfKnown(string? pointId)
        {
            var key = (pointId ?? string.Empty).Trim();
            if (key.Length == 0 || !rowsByPoint.TryGetValue(key, out var row))
            {
                return;
            }

            if (orderedRows.LastOrDefault() == row)
            {
                return;
            }

            if (orderedRows.Contains(row))
            {
                return;
            }

            orderedRows.Add(row);
        }
    }

    internal static ExtractionReviewRowViewModel[] BuildPreviewRowsInReviewedSegmentPath(
        IReadOnlyCollection<ExtractionReviewRowViewModel> visibleRows,
        IReadOnlyCollection<ExtractionReviewSegmentViewModel> visibleSegments,
        bool isPxaSurveyPlanReview)
    {
        var rows = visibleRows.ToArray();
        if (rows.Length == 0 || visibleSegments.Count == 0 || !isPxaSurveyPlanReview)
        {
            return rows;
        }

        var rowsByPoint = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.PointIdentifier))
            .GroupBy(row => row.PointIdentifier.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var orderedRows = new List<ExtractionReviewRowViewModel>();
        var orderedSegments = visibleSegments
            .Where(segment => segment.IncludeInBoundary)
            .OrderBy(segment => segment.Sequence ?? int.MaxValue)
            .ThenBy(segment => segment.FromPoint, StringComparer.OrdinalIgnoreCase)
            .ThenBy(segment => segment.ToPoint, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderedSegments.Length == 0)
        {
            return Array.Empty<ExtractionReviewRowViewModel>();
        }

        AddSegmentPointIfKnown(orderedSegments[0].FromPoint);
        foreach (var segment in orderedSegments)
        {
            AddSegmentPointIfKnown(segment.ToPoint);
        }

        return orderedRows.ToArray();

        void AddSegmentPointIfKnown(string? pointId)
        {
            var key = (pointId ?? string.Empty).Trim();
            if (key.Length == 0 || !rowsByPoint.TryGetValue(key, out var row))
            {
                return;
            }

            orderedRows.Add(row);
        }
    }

    internal static IReadOnlyList<Point> BuildPreviewPointsFromReviewedSegments(
        IReadOnlyCollection<ExtractionReviewSegmentViewModel> visibleSegments,
        bool isPxaSurveyPlanReview)
    {
        if (visibleSegments.Count == 0 || !isPxaSurveyPlanReview)
        {
            return Array.Empty<Point>();
        }

        var orderedSegments = visibleSegments
            .Where(segment => segment.IncludeInBoundary)
            .OrderBy(segment => segment.Sequence ?? int.MaxValue)
            .ThenBy(segment => segment.FromPoint, StringComparer.OrdinalIgnoreCase)
            .ThenBy(segment => segment.ToPoint, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderedSegments.Length == 0)
        {
            return Array.Empty<Point>();
        }

        var points = new List<Point> { new(0d, 0d) };
        var easting = 0d;
        var northing = 0d;
        foreach (var segment in orderedSegments)
        {
            var delta = SurveyPlanBearingParser.ParseDelta(segment.BearingText, segment.DistanceText);
            if (!delta.Success)
            {
                return Array.Empty<Point>();
            }

            easting += delta.DeltaEasting;
            northing += delta.DeltaNorthing;
            points.Add(new Point(easting, northing));
        }

        return points.Count > 1 ? points : Array.Empty<Point>();
    }

    internal static bool ReviewedSegmentChainCloses(IReadOnlyCollection<ExtractionReviewSegmentViewModel> visibleSegments)
    {
        var orderedSegments = visibleSegments
            .Where(segment => segment.IncludeInBoundary)
            .OrderBy(segment => segment.Sequence ?? int.MaxValue)
            .ThenBy(segment => segment.FromPoint, StringComparer.OrdinalIgnoreCase)
            .ThenBy(segment => segment.ToPoint, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderedSegments.Length == 0)
        {
            return false;
        }

        var firstPoint = (orderedSegments[0].FromPoint ?? string.Empty).Trim();
        var lastPoint = (orderedSegments[^1].ToPoint ?? string.Empty).Trim();
        return firstPoint.Length > 0
               && lastPoint.Length > 0
               && string.Equals(firstPoint, lastPoint, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<PreviewPath> BuildPreviewPaths()
    {
        if (!ShowAllParcelContext || ParcelGroups.Count == 0 || SelectedParcelGroup is null)
        {
            return Array.Empty<PreviewPath>();
        }

        var rawGroups = new List<(JamaicaParcelGroupViewModel Group, Point[] Points)>();
        foreach (var group in ParcelGroups)
        {
            var actualPoints = group.Rows
                .Select((row, index) => TryBuildActualPoint(row, index))
                .ToArray();
            if (actualPoints.All(item => item.HasValue))
            {
                rawGroups.Add((group, ClosePreviewRingIfNeeded(actualPoints.Select(item => item!.Value).ToArray()).ToArray()));
            }
        }

        if (rawGroups.Count == 0)
        {
            return Array.Empty<PreviewPath>();
        }

        var allPoints = rawGroups.SelectMany(item => item.Points).ToArray();
        var minX = allPoints.Min(point => point.X);
        var maxX = allPoints.Max(point => point.X);
        var minY = allPoints.Min(point => point.Y);
        var maxY = allPoints.Max(point => point.Y);

        var width = Math.Max(maxX - minX, 1d);
        var height = Math.Max(maxY - minY, 1d);
        var scaleX = (PreviewWidth - 40d) / width;
        var scaleY = (PreviewHeight - 40d) / height;
        var scale = Math.Min(scaleX, scaleY);

        var contextBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7C3CC"));
        var results = new List<PreviewPath>();

        foreach (var item in rawGroups)
        {
            var scaled = new PointCollection();
            foreach (var point in item.Points)
            {
                var x = 20d + ((point.X - minX) * scale);
                var y = PreviewHeight - 20d - ((point.Y - minY) * scale);
                scaled.Add(new Point(x, y));
            }

            var isActive = string.Equals(item.Group.GroupId, SelectedParcelGroup.GroupId, StringComparison.OrdinalIgnoreCase);
            if (!isActive)
            {
                results.Add(new PreviewPath(
                    item.Group.GroupId,
                    scaled,
                    contextBrush,
                    1.2d,
                    0.55d));
            }
        }

        return results;
    }

    private static Point? TryBuildActualPoint(ExtractionReviewRowViewModel row, int index)
    {
        if (!TryParseCoordinate(row.Easting, out var easting) || !TryParseCoordinate(row.Northing, out var northing))
        {
            return null;
        }

        return new Point(easting, northing + index * 0.000001d);
    }

    private static bool TryParseCoordinate(string? value, out double coordinate)
    {
        var text = (value ?? string.Empty).Trim();
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out coordinate))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out coordinate);
    }

    private static PointCollection ScaleToPreview(IReadOnlyList<Point> source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var minX = source.Min(point => point.X);
        var maxX = source.Max(point => point.X);
        var minY = source.Min(point => point.Y);
        var maxY = source.Max(point => point.Y);

        var width = Math.Max(maxX - minX, 1d);
        var height = Math.Max(maxY - minY, 1d);
        var scaleX = (PreviewWidth - 40d) / width;
        var scaleY = (PreviewHeight - 40d) / height;
        var scale = Math.Min(scaleX, scaleY);

        var points = new PointCollection();
        foreach (var point in source)
        {
            var x = 20d + ((point.X - minX) * scale);
            var y = PreviewHeight - 20d - ((point.Y - minY) * scale);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static IReadOnlyList<Point> ClosePreviewRingIfNeeded(IReadOnlyList<Point> source)
    {
        if (source.Count < 3)
        {
            return source;
        }

        var first = source[0];
        var last = source[source.Count - 1];
        if (PointsAreEquivalent(first, last))
        {
            return source;
        }

        var closed = new List<Point>(source.Count + 1);
        closed.AddRange(source);
        closed.Add(first);
        return closed;
    }

    private static bool PointsAreEquivalent(Point first, Point second)
    {
        return Math.Abs(first.X - second.X) < 0.000001d &&
               Math.Abs(first.Y - second.Y) < 0.000001d;
    }

    private static PointCollection BuildSyntheticPreview(int rowCount)
    {
        var points = new PointCollection();
        if (rowCount <= 0)
        {
            return points;
        }

        var step = Math.Max(30d, (PreviewWidth - 60d) / Math.Max(1, rowCount - 1));
        for (var index = 0; index < rowCount; index++)
        {
            var x = 30d + (index * step);
            var y = 45d + ((index % 2 == 0 ? 1 : -1) * (18d + (index % 3) * 8d)) + (index * 6d);
            points.Add(new Point(Math.Min(PreviewWidth - 25d, x), Math.Max(22d, Math.Min(PreviewHeight - 25d, y))));
        }

        return points;
    }

    private static bool IsEmbeddable(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" => true,
            _ => false
        };
    }

    private static string FormatRuleLabel(string? ruleId, string? title = null)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var id = (ruleId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "Rule";
        }

        return id.ToLowerInvariant() switch
        {
            "closure_standard_plan_exam" => "Standard closed parcel tolerance for compute review",
            "closure_standard_compute_review" => "Standard closed parcel tolerance for compute review",
            "readiness_boundary_completeness" => "Boundary completeness",
            "readiness_shared_edge_consistency" => "Shared-edge consistency",
            "readiness_orphan_line_detection" => "Orphan line detection",
            "readiness_line_without_point_support" => "Line without point support",
            "readiness_minimum_segment_count" => "Minimum segment count",
            _ => HumanizeRuleId(id)
        };
    }

    private static string HumanizeRuleId(string value)
    {
        var cleaned = value.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Rule";
        }

        var words = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
        return string.Join(" ", words);
    }

    private static string ResolveParcelDisplayName(string groupId, IReadOnlyList<ExtractionReviewRowViewModel> rows)
    {
        if (rows.Count > 0)
        {
            var parcelName = rows[0].Model.ParcelName?.Trim();
            if (!string.IsNullOrWhiteSpace(parcelName))
            {
                return parcelName;
            }
        }

        if (string.Equals(groupId, "Ungrouped", StringComparison.OrdinalIgnoreCase))
        {
            return "Ungrouped";
        }

        return groupId.StartsWith("parcel", StringComparison.OrdinalIgnoreCase) ? groupId : $"Parcel {groupId}";
    }

    private static string BlankIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private void NotifyViewerProperties()
    {
        OnPropertyChanged(nameof(ReviewSourceOptions));
        OnPropertyChanged(nameof(SelectedReviewSource));
        OnPropertyChanged(nameof(ActiveSourceInstruction));
        OnPropertyChanged(nameof(ViewerFileTitle));
        OnPropertyChanged(nameof(ViewerRoleLabel));
        OnPropertyChanged(nameof(ViewerDisplayPath));
        OnPropertyChanged(nameof(ViewerModeLabel));
        OnPropertyChanged(nameof(ViewerLoadState));
        OnPropertyChanged(nameof(ViewerGuidance));
        OnPropertyChanged(nameof(ViewerFallbackMessage));
        OnPropertyChanged(nameof(ViewerUsesImage));
        OnPropertyChanged(nameof(ViewerUsesBrowser));
        OnPropertyChanged(nameof(ViewerShowsFallback));
        OnPropertyChanged(nameof(ShowCustomViewerControls));
        OnPropertyChanged(nameof(ShowPdfViewerHelp));
        OnPropertyChanged(nameof(PdfViewerHelpText));
        OnPropertyChanged(nameof(CanToggleViewerFit));
        OnPropertyChanged(nameof(ViewerFitToggleText));
        OnPropertyChanged(nameof(CanZoomViewerIn));
        OnPropertyChanged(nameof(CanZoomViewerOut));
        OnPropertyChanged(nameof(CanGoToPreviousViewerPage));
        OnPropertyChanged(nameof(CanGoToNextViewerPage));
        OnPropertyChanged(nameof(ViewerPageStatusText));
        OnPropertyChanged(nameof(ViewerZoomText));
        OnPropertyChanged(nameof(ViewerImageStretch));
        OnPropertyChanged(nameof(ViewerImageScale));
        OnPropertyChanged(nameof(ViewerImageSource));
        OnPropertyChanged(nameof(ViewerBrowserUri));
        OnPropertyChanged(nameof(ViewerNavigationKey));
        OnPropertyChanged(nameof(UnsupportedFallbackSummary));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class JamaicaParcelGroupViewModel
{
    public JamaicaParcelGroupViewModel(string groupId, string displayName, IReadOnlyList<ExtractionReviewRowViewModel> rows)
    {
        GroupId = groupId;
        DisplayName = displayName;
        Rows = rows;
    }

    public string GroupId { get; }

    public string DisplayName { get; }

    public string SortKey => GroupId;

    public IReadOnlyList<ExtractionReviewRowViewModel> Rows { get; }

    public string TraverseId => Rows.FirstOrDefault()?.TraverseId ?? GroupId;

    public int RowCount => Rows.Count;

    public int UnresolvedCount => Rows.Count(row => row.Unresolved || row.HasMissingRequiredValues);

    public int EditedCount => Rows.Count(row => row.IsEdited);

    public int BoundaryBreakCount => Rows.Count(row => row.IsBoundaryBreak);

    public int LowConfidenceCount => Rows.Count(row => row.GroupConfidence.Contains("low", StringComparison.OrdinalIgnoreCase)
        || row.GroupConfidence.Contains("unknown", StringComparison.OrdinalIgnoreCase));
}

internal sealed record PreviewMarker(double X, double Y, string Label);
internal sealed record PreviewPath(string GroupId, PointCollection Points, Brush Stroke, double StrokeThickness, double Opacity);

internal enum DiagnosticBucket
{
    None,
    Blocked,
    Warning,
    Passed
}
