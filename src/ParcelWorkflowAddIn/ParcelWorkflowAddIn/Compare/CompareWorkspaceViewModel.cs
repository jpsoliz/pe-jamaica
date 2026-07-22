using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Workflow.Review;

namespace ParcelWorkflowAddIn.Compare;

public sealed class CompareWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly SelectedInnolaTransaction transaction;
    private readonly CompareWorkspaceLoadService? loadService;
    private readonly CaseFolderStore caseFolderStore;
    private readonly CompareReviewDraftPersistenceService draftPersistence;
    private readonly CompareReviewDecisionPersistenceService decisionPersistence;
    private readonly CompareReviewReportService reportService;
    private readonly CompareSurveyPlanEvidenceService surveyPlanEvidenceService;
    private readonly ILegalCadasterQueryService legalCadasterQueryService;
    private readonly IFiscalCadasterQueryService fiscalCadasterQueryService;
    private readonly ICompareEnterpriseCadasterEvidenceService enterpriseCadasterEvidenceService;
    private readonly CompareLegalQueryTracePersistenceService legalQueryTracePersistence;
    private readonly CompareFinalizeTracePersistenceService finalizeTracePersistence;
    private readonly CompareEvidenceComparisonService evidenceComparisonService;
    private readonly ICompareTaskLifecycleService? taskLifecycleService;
    private readonly ICompareReportAttachmentService? reportAttachmentService;
    private readonly ICompareMapIntegrationService? mapIntegrationService;
    private readonly ICompareWorkspacePromptService promptService;
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly string? reviewerId;
    private readonly string? reviewerDisplayName;
    private readonly List<CompareReviewEvidenceRef> restoredDecisionEvidenceRefs = new();
    private CaseFolderLayout? layout;
    private CompareWorkingGeometryLoadPlan? currentGeometryPlan;
    private string? currentCompareGroupLayerName;
    private SourceFileCopyResult? selectedDocument;
    private ReviewSourceViewerState viewerState = ReviewSourceViewerStateProjector.Build(null);
    private string notes = string.Empty;
    private string documentStatus = "Documents pending.";
    private string geometryStatus = "Geometry pending.";
    private string legalEvidenceStatus = "Legal cadaster evidence not queried yet.";
    private string fiscalEvidenceStatus = "Fiscal neighbor evidence not queried yet.";
    private string surveyPlanSummary = "Survey plan interpretation will appear here after extraction evidence is available.";
    private string legalCadasterSummary = "Legal cadaster query results will appear here in Story 8.4.";
    private string fiscalNeighborSummary = "Fiscal neighbor query results will appear here in Story 8.4.";
    private string decisionStatus = "Draft";
    private string selectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
    private string searchPid = string.Empty;
    private string searchVolume = string.Empty;
    private string searchFolio = string.Empty;
    private string searchLandValuationNumber = string.Empty;
    private string searchName = string.Empty;
    private string searchParish = string.Empty;
    private string searchValidationMessage = string.Empty;
    private string evidenceSearchStatusMessage = "No legal cadaster search has been run.";
    private bool isLoading;
    private bool isPdfPanelVisible = true;
    private bool documentsAvailable;
    private bool geometryAvailable;
    private bool geometryRetryable;
    private bool geometryBlocksApproval = true;
    private bool legalEvidenceReviewed;
    private bool fiscalEvidenceReviewed;
    private string? statusText;

    public CompareWorkspaceViewModel(
        SelectedInnolaTransaction transaction,
        CompareWorkspaceLoadService? loadService = null,
        CaseFolderStore? caseFolderStore = null,
        CompareReviewDraftPersistenceService? draftPersistence = null,
        CompareReviewDecisionPersistenceService? decisionPersistence = null,
        CompareReviewReportService? reportService = null,
        CompareSurveyPlanEvidenceService? surveyPlanEvidenceService = null,
        ILegalCadasterQueryService? legalCadasterQueryService = null,
        IFiscalCadasterQueryService? fiscalCadasterQueryService = null,
        ICompareEnterpriseCadasterEvidenceService? enterpriseCadasterEvidenceService = null,
        CompareLegalQueryTracePersistenceService? legalQueryTracePersistence = null,
        CompareFinalizeTracePersistenceService? finalizeTracePersistence = null,
        CompareEvidenceComparisonService? evidenceComparisonService = null,
        ICompareTaskLifecycleService? taskLifecycleService = null,
        ICompareReportAttachmentService? reportAttachmentService = null,
        ICompareMapIntegrationService? mapIntegrationService = null,
        ICompareWorkspacePromptService? promptService = null,
        Func<DateTimeOffset>? getUtcNow = null,
        string? reviewerId = null,
        string? reviewerDisplayName = null)
    {
        this.transaction = transaction;
        this.loadService = loadService;
        this.caseFolderStore = caseFolderStore ?? new CaseFolderStore();
        this.draftPersistence = draftPersistence ?? new CompareReviewDraftPersistenceService();
        this.decisionPersistence = decisionPersistence ?? new CompareReviewDecisionPersistenceService();
        this.reportService = reportService ?? new CompareReviewReportService(getUtcNow);
        this.surveyPlanEvidenceService = surveyPlanEvidenceService ?? new CompareSurveyPlanEvidenceService();
        this.legalCadasterQueryService = legalCadasterQueryService ?? new UnsupportedLegalCadasterQueryService();
        this.fiscalCadasterQueryService = fiscalCadasterQueryService ?? new UnsupportedFiscalCadasterQueryService();
        this.enterpriseCadasterEvidenceService = enterpriseCadasterEvidenceService ?? new CompareEnterpriseCadasterEvidenceService();
        this.legalQueryTracePersistence = legalQueryTracePersistence ?? new CompareLegalQueryTracePersistenceService();
        this.finalizeTracePersistence = finalizeTracePersistence ?? new CompareFinalizeTracePersistenceService();
        this.evidenceComparisonService = evidenceComparisonService ?? new CompareEvidenceComparisonService();
        this.taskLifecycleService = taskLifecycleService;
        this.reportAttachmentService = reportAttachmentService;
        this.mapIntegrationService = mapIntegrationService;
        this.promptService = promptService ?? new AutoApproveCompareWorkspacePromptService();
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
        this.reviewerId = reviewerId;
        this.reviewerDisplayName = reviewerDisplayName;

        ReloadGeometryCommand = new RelayCommand(() => _ = ReloadGeometryAsync(), () => CanReloadGeometry);
        QueryParcelIdCommand = new RelayCommand(() => _ = QueryParcelIdAsync(), () => CanQueryLegalEvidence);
        QueryVolumeFolioCommand = new RelayCommand(() => _ = QueryVolumeFolioAsync(), () => CanQueryLegalEvidence);
        FindNeighborsCommand = new RelayCommand(() => _ = QueryFiscalNeighborsAsync(), () => CanQueryFiscalEvidence);
        RefreshEnterpriseCadasterEvidenceCommand = new RelayCommand(() => _ = QueryEnterpriseCadasterEvidenceAsync(), () => CanQueryFiscalEvidence);
        SeedSearchFromEnterpriseEvidenceCommand = new RelayCommand(
            parameter => SeedSearchFromEnterpriseEvidence(parameter as CompareEnterpriseCadasterEvidenceRowItem),
            parameter => parameter is CompareEnterpriseCadasterEvidenceRowItem && !IsLoading);
        RunEvidenceSearchCommand = new RelayCommand(() => _ = RunEvidenceSearchAsync(), () => CanRunEvidenceSearch);
        ClearEvidenceSearchFieldsCommand = new RelayCommand(ClearEvidenceSearchFields, () => !IsLoading);
        MarkEvidenceResultValuableCommand = new RelayCommand(
            parameter => MarkEvidenceResultValuable(parameter as CompareEvidenceSearchResultItem),
            parameter => parameter is CompareEvidenceSearchResultItem item && item.CanMarkValuable && !IsLoading);
        MarkPartyMatchValuableCommand = new RelayCommand(
            parameter => MarkPartyMatchValuable(parameter as ComparePartySearchResultItem),
            parameter => parameter is ComparePartySearchResultItem item && item.CanMarkValuable && !IsLoading);
        RemoveValuableEvidenceCommand = new RelayCommand(
            parameter => RemoveValuableEvidence(parameter as CompareValuableEvidenceItem),
            parameter => parameter is CompareValuableEvidenceItem && !IsLoading);
        TogglePdfPanelCommand = new RelayCommand(TogglePdfPanel);
        SaveProgressCommand = new RelayCommand(SaveProgress, () => CanSaveProgress);
        SuspendTaskCommand = new RelayCommand(async () => await SuspendTaskAsync(), () => CanSuspendTask);
        CompleteTaskCommand = new RelayCommand(async () => await CompleteTaskAsync(), () => CanCompleteTask);
        BlockCompareCommand = new RelayCommand(BlockCompare, () => CanBlockCompare);
        ApproveCompareCommand = new RelayCommand(async () => await FinalizeCompareAsync(), () => CanApproveCompare);

        QueryResults.CollectionChanged += (_, _) => NotifyPropertyChanged(nameof(HasQueryResults));
        RelatedPartyMatches.CollectionChanged += (_, _) => NotifyPropertyChanged(nameof(HasRelatedPartyMatches));
        ValuableEvidenceItems.CollectionChanged += (_, _) =>
        {
            NotifyPropertyChanged(nameof(HasValuableEvidenceItems));
            RaiseCommandStates();
        };
        EnterpriseCadasterEvidenceRows.CollectionChanged += (_, _) => NotifyPropertyChanged(nameof(HasEnterpriseCadasterEvidenceRows));
        EvidenceItems.CollectionChanged += (_, _) => NotifyPropertyChanged(nameof(HasEvidenceItems));
        Discrepancies.CollectionChanged += (_, _) => NotifyPropertyChanged(nameof(HasDiscrepancies));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CloseRequested;

    public string TransactionNumber => transaction.TransactionNumber;

    public string TaskName => transaction.TaskName;

    public ObservableCollection<CompareDocumentItem> Documents { get; } = new();

    public ObservableCollection<CompareDocumentItem> PdfDocuments { get; } = new();

    public ObservableCollection<CompareEvidenceItem> EvidenceItems { get; } = new();

    public ObservableCollection<CompareEvidenceSearchResultItem> QueryResults { get; } = new();

    public ObservableCollection<ComparePartySearchResultItem> RelatedPartyMatches { get; } = new();

    public ObservableCollection<CompareValuableEvidenceItem> ValuableEvidenceItems { get; } = new();

    public ObservableCollection<CompareEnterpriseCadasterEvidenceRowItem> EnterpriseCadasterEvidenceRows { get; } = new();

    public ObservableCollection<CompareDiscrepancyItem> Discrepancies { get; } = new();

    public bool HasQueryResults => QueryResults.Count > 0;

    public bool HasRelatedPartyMatches => RelatedPartyMatches.Count > 0;

    public bool HasValuableEvidenceItems => ValuableEvidenceItems.Count > 0;

    public bool HasEnterpriseCadasterEvidenceRows => EnterpriseCadasterEvidenceRows.Count > 0;

    public bool HasEvidenceItems => EvidenceItems.Count > 0;

    public bool HasDiscrepancies => Discrepancies.Any(item =>
        !string.IsNullOrWhiteSpace(item.Title)
        || !string.IsNullOrWhiteSpace(item.Source)
        || !string.IsNullOrWhiteSpace(item.Status));

    public IReadOnlyList<string> EvidenceSearchModes { get; } = new[]
    {
        CompareEvidenceSearchMode.Pid,
        CompareEvidenceSearchMode.VolumeFolio,
        CompareEvidenceSearchMode.LandValuationNumber,
        CompareEvidenceSearchMode.Name
    };

    public IReadOnlyList<string> EvidenceRoleTags { get; } = new[]
    {
        CompareEvidenceRoleTag.Owner,
        CompareEvidenceRoleTag.Occupant,
        CompareEvidenceRoleTag.InPossession,
        CompareEvidenceRoleTag.Neighbor,
        CompareEvidenceRoleTag.Other
    };

    public ICommand ReloadGeometryCommand { get; }

    public ICommand QueryParcelIdCommand { get; }

    public ICommand QueryVolumeFolioCommand { get; }

    public ICommand FindNeighborsCommand { get; }

    public ICommand RefreshEnterpriseCadasterEvidenceCommand { get; }

    public ICommand SeedSearchFromEnterpriseEvidenceCommand { get; }

    public ICommand RunEvidenceSearchCommand { get; }

    public ICommand ClearEvidenceSearchFieldsCommand { get; }

    public ICommand MarkEvidenceResultValuableCommand { get; }

    public ICommand MarkPartyMatchValuableCommand { get; }

    public ICommand RemoveValuableEvidenceCommand { get; }

    public ICommand TogglePdfPanelCommand { get; }

    public ICommand SaveProgressCommand { get; }

    public ICommand SuspendTaskCommand { get; }

    public ICommand CompleteTaskCommand { get; }

    public ICommand BlockCompareCommand { get; }

    public ICommand ApproveCompareCommand { get; }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetField(ref isLoading, value, nameof(IsLoading));
    }

    public bool IsPdfPanelVisible
    {
        get => isPdfPanelVisible;
        private set
        {
            if (!SetField(ref isPdfPanelVisible, value, nameof(IsPdfPanelVisible)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(IsPdfPanelHidden));
            NotifyPropertyChanged(nameof(PdfPanelToggleText));
        }
    }

    public bool IsPdfPanelHidden => !IsPdfPanelVisible;

    public string PdfPanelToggleText => IsPdfPanelVisible ? "Hide Files" : "Show Files";

    public SourceFileCopyResult? SelectedDocument
    {
        get => selectedDocument;
        set
        {
            if (selectedDocument == value)
            {
                return;
            }

            selectedDocument = value;
            ViewerState = ReviewSourceViewerStateProjector.Build(value, InnolaTransactionSettings.PdfViewerModeEmbeddedBrowser);
            NotifyPropertyChanged(nameof(SelectedDocument));
        }
    }

    private void TogglePdfPanel()
    {
        IsPdfPanelVisible = !IsPdfPanelVisible;
    }

    private ReviewSourceViewerState ViewerState
    {
        get => viewerState;
        set
        {
            viewerState = value;
            NotifyPropertyChanged(nameof(ViewerTitle));
            NotifyPropertyChanged(nameof(ViewerFallbackMessage));
            NotifyPropertyChanged(nameof(ViewerFilePath));
            NotifyPropertyChanged(nameof(ViewerUsesBrowser));
            NotifyPropertyChanged(nameof(ViewerBrowserUri));
            NotifyPropertyChanged(nameof(ViewerNavigationKey));
            NotifyPropertyChanged(nameof(ViewerShowsImage));
            NotifyPropertyChanged(nameof(ViewerImagePath));
        }
    }

    public string ViewerTitle => ViewerState.Title;

    public string ViewerFallbackMessage => ViewerState.FallbackMessage;

    public string? ViewerFilePath => ViewerState.FullPath;

    public bool ViewerUsesBrowser => ViewerState.Mode == ReviewSourceViewerMode.Pdf && ViewerBrowserUri is not null;

    public Uri? ViewerBrowserUri => !string.IsNullOrWhiteSpace(ViewerState.FullPath) && File.Exists(ViewerState.FullPath)
        ? new Uri(ViewerState.FullPath)
        : null;

    public string ViewerNavigationKey => $"{ViewerState.Mode}:{ViewerState.FullPath}";

    public bool ViewerShowsImage => ViewerState.Mode == ReviewSourceViewerMode.RenderedDocument
        && !string.IsNullOrWhiteSpace(ViewerState.FullPath)
        && File.Exists(ViewerState.FullPath);

    public string? ViewerImagePath => ViewerShowsImage ? ViewerState.FullPath : null;

    public bool HasPdfDocuments => PdfDocuments.Count > 0;

    public string PdfDocumentSelectorStatus => HasPdfDocuments
        ? $"{PdfDocuments.Count} PDF source document(s) available."
        : "No PDF source documents are available for embedded Compare review.";

    public string DocumentStatus
    {
        get => documentStatus;
        private set => SetField(ref documentStatus, value, nameof(DocumentStatus));
    }

    public string GeometryStatus
    {
        get => geometryStatus;
        private set => SetField(ref geometryStatus, value, nameof(GeometryStatus));
    }

    public string LegalEvidenceStatus
    {
        get => legalEvidenceStatus;
        private set => SetField(ref legalEvidenceStatus, value, nameof(LegalEvidenceStatus));
    }

    public string FiscalEvidenceStatus
    {
        get => fiscalEvidenceStatus;
        private set => SetField(ref fiscalEvidenceStatus, value, nameof(FiscalEvidenceStatus));
    }

    public string SurveyPlanSummary
    {
        get => surveyPlanSummary;
        set => SetField(ref surveyPlanSummary, value, nameof(SurveyPlanSummary));
    }

    public string LegalCadasterSummary
    {
        get => legalCadasterSummary;
        set => SetField(ref legalCadasterSummary, value, nameof(LegalCadasterSummary));
    }

    public string FiscalNeighborSummary
    {
        get => fiscalNeighborSummary;
        set => SetField(ref fiscalNeighborSummary, value, nameof(FiscalNeighborSummary));
    }

    public string Notes
    {
        get => notes;
        set
        {
            if (SetField(ref notes, value, nameof(Notes)))
            {
                RaiseCommandStates();
            }
        }
    }

    public string DecisionStatus
    {
        get => decisionStatus;
        private set => SetField(ref decisionStatus, value, nameof(DecisionStatus));
    }

    public string? StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value, nameof(StatusText));
    }

    public string SelectedEvidenceSearchMode
    {
        get => selectedEvidenceSearchMode;
        set
        {
            if (SetField(ref selectedEvidenceSearchMode, value, nameof(SelectedEvidenceSearchMode)))
            {
                SearchValidationMessage = string.Empty;
                NotifyPropertyChanged(nameof(IsPidSearchMode));
                NotifyPropertyChanged(nameof(IsVolumeFolioSearchMode));
                NotifyPropertyChanged(nameof(IsLandValuationNumberSearchMode));
                NotifyPropertyChanged(nameof(IsNameSearchMode));
                RaiseCommandStates();
            }
        }
    }

    public bool IsPidSearchMode => SelectedEvidenceSearchMode.Equals(CompareEvidenceSearchMode.Pid, StringComparison.OrdinalIgnoreCase);

    public bool IsVolumeFolioSearchMode => SelectedEvidenceSearchMode.Equals(CompareEvidenceSearchMode.VolumeFolio, StringComparison.OrdinalIgnoreCase);

    public bool IsLandValuationNumberSearchMode => SelectedEvidenceSearchMode.Equals(CompareEvidenceSearchMode.LandValuationNumber, StringComparison.OrdinalIgnoreCase);

    public bool IsNameSearchMode => SelectedEvidenceSearchMode.Equals(CompareEvidenceSearchMode.Name, StringComparison.OrdinalIgnoreCase);

    public string SearchPid
    {
        get => searchPid;
        set
        {
            if (SetField(ref searchPid, value, nameof(SearchPid)))
            {
                SearchValidationMessage = string.Empty;
            }
        }
    }

    public string SearchVolume
    {
        get => searchVolume;
        set
        {
            if (SetField(ref searchVolume, value, nameof(SearchVolume)))
            {
                SearchValidationMessage = string.Empty;
            }
        }
    }

    public string SearchFolio
    {
        get => searchFolio;
        set
        {
            if (SetField(ref searchFolio, value, nameof(SearchFolio)))
            {
                SearchValidationMessage = string.Empty;
            }
        }
    }

    public string SearchLandValuationNumber
    {
        get => searchLandValuationNumber;
        set
        {
            if (SetField(ref searchLandValuationNumber, value, nameof(SearchLandValuationNumber)))
            {
                SearchValidationMessage = string.Empty;
            }
        }
    }

    public string SearchName
    {
        get => searchName;
        set
        {
            if (SetField(ref searchName, value, nameof(SearchName)))
            {
                SearchValidationMessage = string.Empty;
            }
        }
    }

    public string SearchParish
    {
        get => searchParish;
        set
        {
            if (SetField(ref searchParish, value, nameof(SearchParish)))
            {
                SearchValidationMessage = string.Empty;
            }
        }
    }

    public string SearchValidationMessage
    {
        get => searchValidationMessage;
        private set => SetField(ref searchValidationMessage, value, nameof(SearchValidationMessage));
    }

    public string EvidenceSearchStatusMessage
    {
        get => evidenceSearchStatusMessage;
        private set => SetField(ref evidenceSearchStatusMessage, value, nameof(EvidenceSearchStatusMessage));
    }

    public bool DocumentsAvailable
    {
        get => documentsAvailable;
        private set => SetField(ref documentsAvailable, value, nameof(DocumentsAvailable));
    }

    public bool GeometryAvailable
    {
        get => geometryAvailable;
        private set => SetField(ref geometryAvailable, value, nameof(GeometryAvailable));
    }

    public bool GeometryReadOnly => true;

    public bool GeometryEditingAvailable => false;

    public bool CanReloadGeometry => !IsLoading && (geometryRetryable || GeometryAvailable);

    public bool CanQueryEvidence => CanQueryLegalEvidence || CanQueryFiscalEvidence;

    public bool CanQueryLegalEvidence => layout is not null && DocumentsAvailable && !IsLoading;

    public bool CanQueryFiscalEvidence => GeometryAvailable && !IsLoading;

    public bool CanRunEvidenceSearch => CanQueryLegalEvidence;

    public bool CanSaveProgress => layout is not null && !IsLoading;

    public bool CanSuspendTask => layout is not null && !IsLoading && taskLifecycleService is not null;

    public bool CanCompleteTask => layout is not null
        && !IsLoading
        && taskLifecycleService is not null
        && DecisionStatus.Equals("Finalized", StringComparison.OrdinalIgnoreCase)
        && CanApproveCompare;

    public bool CanBlockCompare => layout is not null && !IsLoading;

    public bool HasUnresolvedDiscrepancies => Discrepancies.Any(item => !item.IsResolved);

    public bool LegalEvidenceReviewed
    {
        get => legalEvidenceReviewed;
        private set => SetField(ref legalEvidenceReviewed, value, nameof(LegalEvidenceReviewed));
    }

    public bool FiscalEvidenceReviewed
    {
        get => fiscalEvidenceReviewed;
        private set => SetField(ref fiscalEvidenceReviewed, value, nameof(FiscalEvidenceReviewed));
    }

    public bool CanApproveCompare => layout is not null
        && DocumentsAvailable
        && GeometryAvailable
        && !geometryBlocksApproval
        && ValuableEvidenceItems.Count > 0
        && !string.IsNullOrWhiteSpace(Notes)
        && !IsLoading;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (loadService is null)
        {
            return;
        }

        IsLoading = true;
        DocumentStatus = "Loading attached documents.";
        GeometryStatus = "Loading transaction-scoped working geometry.";
        try
        {
            var state = await loadService.LoadAsync(cancellationToken);
            CaseFolderReopenResult? reopened = null;
            if (!string.IsNullOrWhiteSpace(state.Documents.CaseFolderPath))
            {
                reopened = caseFolderStore.ReopenCaseFolder(state.Documents.CaseFolderPath);
            }

            ApplyLoadState(state, reopened);
        }
        finally
        {
            IsLoading = false;
            RaiseCommandStates();
        }
    }

    public async Task CloseWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await CleanupAfterTaskExitAsync(cancellationToken);
    }

    public void ReportWorkspaceError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        StatusText = message;
    }

    public async Task ReloadGeometryAsync(CancellationToken cancellationToken = default)
    {
        if (loadService is null)
        {
            return;
        }

        IsLoading = true;
        GeometryStatus = "Reloading transaction-scoped working geometry.";
        try
        {
            ApplyGeometryState(await loadService.LoadGeometryAsync(cancellationToken));
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    public void ApplyLoadState(CompareWorkspaceLoadState state, CaseFolderReopenResult? reopenedCaseFolder)
    {
        DocumentsAvailable = state.Documents.Success;
        DocumentStatus = state.Documents.Message;

        ApplyGeometryState(state.Geometry);

        if (reopenedCaseFolder?.Success == true && reopenedCaseFolder.Layout is not null)
        {
            layout = reopenedCaseFolder.Layout;
            Documents.Clear();
            PdfDocuments.Clear();
            foreach (var source in reopenedCaseFolder.SourceFiles)
            {
                var item = new CompareDocumentItem(source);
                Documents.Add(item);
                if (item.IsPdf)
                {
                    PdfDocuments.Add(item);
                }
            }

            RefreshPdfDocumentSelectorState();
            SelectedDocument = PdfDocuments.FirstOrDefault()?.SourceFile;
            ApplySurveyPlanEvidence();
            RestoreDraft(reopenedCaseFolder.Layout);
            RestoreDecision(reopenedCaseFolder.Layout);
        }

        RefreshEvidenceItems();
        RaiseStateProperties();
    }

    private void ApplyGeometryState(CompareWorkingGeometryLoadResult geometry)
    {
        GeometryAvailable = geometry.Success;
        geometryRetryable = geometry.Retryable;
        geometryBlocksApproval = geometry.BlocksApproval;
        currentGeometryPlan = geometry.Success ? geometry.Plan : currentGeometryPlan;
        currentCompareGroupLayerName = geometry.MapResult?.GroupLayerName ?? currentCompareGroupLayerName;
        GeometryStatus = geometry.Message;
    }

    public void AddDiscrepancy(string title, string source, bool isResolved = false)
    {
        Discrepancies.Add(new CompareDiscrepancyItem(title, source, isResolved ? "Resolved" : "Open", isResolved));
        RaiseStateProperties();
    }

    public void MarkAllDiscrepanciesResolved()
    {
        foreach (var discrepancy in Discrepancies)
        {
            discrepancy.IsResolved = true;
            discrepancy.Status = "Resolved";
        }

        RaiseStateProperties();
    }

    public async Task QueryParcelIdAsync(CancellationToken cancellationToken = default)
    {
        var plan = LoadSurveyPlanEvidence();
        if (string.IsNullOrWhiteSpace(plan.ParcelId))
        {
            AddEvidenceDiscrepancy(new CompareEvidenceDiscrepancy(
                "Survey plan parcel ID is missing",
                "Survey plan",
                CompareEvidenceStatus.NoRecordReturned,
                false,
                "Parcel ID is required before querying legal cadaster by parcel ID."));
            LegalEvidenceStatus = "No record returned";
            LegalEvidenceReviewed = true;
            RaiseStateProperties();
            return;
        }

        IsLoading = true;
        LegalEvidenceStatus = "Querying legal cadaster by parcel ID.";
        try
        {
            ApplyLegalResult(await legalCadasterQueryService
                .QueryByParcelIdAsync(plan.ParcelId, cancellationToken), plan);
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    public async Task QueryVolumeFolioAsync(CancellationToken cancellationToken = default)
    {
        var plan = LoadSurveyPlanEvidence();
        if (string.IsNullOrWhiteSpace(plan.Volume) || string.IsNullOrWhiteSpace(plan.Folio))
        {
            AddEvidenceDiscrepancy(new CompareEvidenceDiscrepancy(
                "Survey plan volume/folio is missing",
                "Survey plan",
                CompareEvidenceStatus.NoRecordReturned,
                false,
                "Volume and folio are required before querying legal cadaster."));
            LegalEvidenceStatus = "No record returned";
            LegalEvidenceReviewed = true;
            RaiseStateProperties();
            return;
        }

        IsLoading = true;
        LegalEvidenceStatus = "Querying legal cadaster by volume/folio.";
        try
        {
            ApplyLegalResult(await legalCadasterQueryService
                .QueryByVolumeFolioAsync(plan.Volume, plan.Folio, cancellationToken), plan);
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    public async Task QueryFiscalNeighborsAsync(CancellationToken cancellationToken = default)
    {
        var plan = LoadSurveyPlanEvidence();
        IsLoading = true;
        FiscalEvidenceStatus = "Querying fiscal cadaster neighbors.";
        try
        {
            ApplyFiscalResult(await fiscalCadasterQueryService
                .QueryNeighborsAsync(transaction, currentGeometryPlan, cancellationToken), plan);
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    public async Task QueryEnterpriseCadasterEvidenceAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        FiscalEvidenceStatus = "Refreshing Legal/Fiscal cadaster neighbor evidence.";
        try
        {
            ApplyEnterpriseCadasterEvidenceResult(await enterpriseCadasterEvidenceService
                .QueryAsync(transaction, currentGeometryPlan, cancellationToken)
                .ConfigureAwait(false));
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    public async Task RunEvidenceSearchAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBuildManualSearchRequest(out var request, out var validationMessage))
        {
            SearchValidationMessage = validationMessage;
            StatusText = validationMessage;
            return;
        }

        SearchValidationMessage = string.Empty;
        QueryResults.Clear();
        RelatedPartyMatches.Clear();
        IsLoading = true;
        LegalEvidenceStatus = $"Querying legal cadaster by {SelectedEvidenceSearchMode}.";
        EvidenceSearchStatusMessage = $"Searching legal cadaster by {SelectedEvidenceSearchMode}...";
        try
        {
            var result = request.QueryKind switch
            {
                "parcel_id" => await legalCadasterQueryService.QueryByParcelIdAsync(request.Pid!, cancellationToken),
                "volume_folio" => await legalCadasterQueryService.QueryByVolumeFolioAsync(request.Volume!, request.Folio!, cancellationToken),
                "land_valuation_number" => await legalCadasterQueryService.QueryByLandValuationNumberAsync(request.LandValuationNumber!, request.Parish, cancellationToken),
                "name" => await legalCadasterQueryService.QueryByNameAsync(request.Name!, null, cancellationToken),
                _ => LegalCadasterQueryResult.Failed(
                    new LegalCadasterQuery(request.QueryKind, request.Pid, request.Volume, request.Folio, request.LandValuationNumber, request.Name, request.Parish),
                    "Unsupported evidence search mode.")
            };

            ApplyManualLegalSearchResult(result, LoadSurveyPlanEvidence());
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    private bool TryBuildManualSearchRequest(out CompareEvidenceSearchRequest request, out string validationMessage)
    {
        validationMessage = string.Empty;
        request = new CompareEvidenceSearchRequest(string.Empty, null, null, null, null, null, null);

        if (IsPidSearchMode)
        {
            if (string.IsNullOrWhiteSpace(SearchPid))
            {
                validationMessage = "PID is required before searching.";
                return false;
            }

            request = new CompareEvidenceSearchRequest("parcel_id", SearchPid.Trim(), null, null, null, null, NullIfBlank(SearchParish));
            return true;
        }

        if (IsVolumeFolioSearchMode)
        {
            if (string.IsNullOrWhiteSpace(SearchVolume) || string.IsNullOrWhiteSpace(SearchFolio))
            {
                validationMessage = "Volume and folio are required before searching.";
                return false;
            }

            if (!int.TryParse(SearchVolume.Trim(), out _) || !int.TryParse(SearchFolio.Trim(), out _))
            {
                validationMessage = "Volume and folio must be numeric before searching.";
                return false;
            }

            request = new CompareEvidenceSearchRequest("volume_folio", null, SearchVolume.Trim(), SearchFolio.Trim(), null, null, NullIfBlank(SearchParish));
            return true;
        }

        if (IsLandValuationNumberSearchMode)
        {
            if (string.IsNullOrWhiteSpace(SearchLandValuationNumber))
            {
                validationMessage = "Land Val No. is required before searching.";
                return false;
            }

            request = new CompareEvidenceSearchRequest("land_valuation_number", null, null, null, SearchLandValuationNumber.Trim(), null, NullIfBlank(SearchParish));
            return true;
        }

        if (IsNameSearchMode)
        {
            if (string.IsNullOrWhiteSpace(SearchName))
            {
                validationMessage = "Owner name is required before searching.";
                return false;
            }

            request = new CompareEvidenceSearchRequest("name", null, null, null, null, SearchName.Trim(), null);
            return true;
        }

        validationMessage = "Select an evidence search mode before searching.";
        return false;
    }

    private void ApplyManualLegalSearchResult(LegalCadasterQueryResult result, CompareSurveyPlanEvidence plan)
    {
        if (result.Records.Count > 0)
        {
            foreach (var item in result.Records.Select(CompareEvidenceSearchResult.FromLegalRecord))
            {
                QueryResults.Add(new CompareEvidenceSearchResultItem(item));
            }
        }

        foreach (var partyRecord in result.PartyRecords ?? Array.Empty<LegalCadasterPartyRecord>())
        {
            RelatedPartyMatches.Add(new ComparePartySearchResultItem(partyRecord));
        }

        ApplyLegalResult(result, plan);
    }

    private void ClearEvidenceSearchFields()
    {
        SearchPid = string.Empty;
        SearchVolume = string.Empty;
        SearchFolio = string.Empty;
        SearchLandValuationNumber = string.Empty;
        SearchName = string.Empty;
        SearchParish = string.Empty;
        SearchValidationMessage = string.Empty;
        EvidenceSearchStatusMessage = "Search fields cleared. No new legal cadaster search has been run.";
    }

    private void MarkEvidenceResultValuable(CompareEvidenceSearchResultItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (!item.CanMarkValuable)
        {
            StatusText = "Only returned evidence records can be marked valuable.";
            RaiseStateProperties();
            return;
        }

        var evidence = item.ToValuableEvidence(
            $"evidence-{Guid.NewGuid():N}",
            getUtcNow());
        ValuableEvidenceItems.Add(new CompareValuableEvidenceItem(evidence));
        StatusText = "Evidence marked valuable for Compare decision.";
        RaiseStateProperties();
    }

    private void MarkPartyMatchValuable(ComparePartySearchResultItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (!item.CanMarkValuable)
        {
            StatusText = "Only returned party matches can be marked valuable.";
            RaiseStateProperties();
            return;
        }

        var evidence = item.ToValuableEvidence(
            $"evidence-{Guid.NewGuid():N}",
            getUtcNow());
        ValuableEvidenceItems.Add(new CompareValuableEvidenceItem(evidence));
        StatusText = "Party match marked valuable for Compare decision.";
        RaiseStateProperties();
    }

    private void RemoveValuableEvidence(CompareValuableEvidenceItem? item)
    {
        if (item is null)
        {
            return;
        }

        ValuableEvidenceItems.Remove(item);
        StatusText = "Valuable evidence removed.";
        RaiseStateProperties();
    }

    private CompareSurveyPlanEvidence LoadSurveyPlanEvidence()
    {
        var evidence = surveyPlanEvidenceService.Load(layout, TransactionNumber);
        SurveyPlanSummary = BuildSurveyPlanSummary(evidence);
        RefreshEvidenceItems();
        return evidence;
    }

    private void ApplySurveyPlanEvidence()
    {
        var evidence = LoadSurveyPlanEvidence();
        if (string.IsNullOrWhiteSpace(SearchPid) && !string.IsNullOrWhiteSpace(evidence.ParcelId))
        {
            SearchPid = evidence.ParcelId;
        }

        if (string.IsNullOrWhiteSpace(SearchVolume) && !string.IsNullOrWhiteSpace(evidence.Volume))
        {
            SearchVolume = evidence.Volume;
        }

        if (string.IsNullOrWhiteSpace(SearchFolio) && !string.IsNullOrWhiteSpace(evidence.Folio))
        {
            SearchFolio = evidence.Folio;
        }
    }

    private void ApplyLegalResult(LegalCadasterQueryResult result, CompareSurveyPlanEvidence plan)
    {
        LegalEvidenceReviewed = true;
        LegalEvidenceStatus = result.Message;
        EvidenceSearchStatusMessage = BuildEvidenceSearchStatusMessage(result);
        legalQueryTracePersistence.Append(layout, TransactionNumber, result, getUtcNow());
        if (result.Success && result.Records.Count > 0)
        {
            LegalCadasterSummary = string.Join(Environment.NewLine, result.Records.Select(record =>
                $"Owner: {Display(record.OwnerName)}; Parcel: {Display(record.ParcelId)}; Volume/Folio: {Display(record.Volume)}/{Display(record.Folio)}; Title: {Display(record.TitleRecordId)}; Source: {record.SourceLabel}; Queried: {record.QueriedAt:O}; Status: {record.Status}"));
        }

        foreach (var discrepancy in evidenceComparisonService.CompareLegal(plan, result))
        {
            AddEvidenceDiscrepancy(discrepancy);
        }

        RefreshEvidenceItems();
    }

    private static string BuildEvidenceSearchStatusMessage(LegalCadasterQueryResult result)
    {
        var label = EvidenceSearchLabel(result.Query);
        if (!result.Success)
        {
            return $"{label}: search failed. Try again.";
        }

        var propertyCount = result.Records.Count;
        var partyCount = result.PartyRecords?.Count ?? 0;
        if (propertyCount == 0 && partyCount == 0)
        {
            return $"{label}: no records found.";
        }

        var parts = new List<string>();
        if (propertyCount > 0)
        {
            parts.Add($"{propertyCount} {Plural(propertyCount, "record", "records")} found");
        }

        if (partyCount > 0)
        {
            parts.Add($"{partyCount} related party {Plural(partyCount, "match", "matches")} found");
        }

        return $"{label}: {string.Join(", ", parts)}.";
    }

    private static string EvidenceSearchLabel(LegalCadasterQuery query)
    {
        return query.QueryKind switch
        {
            "parcel_id" => "PID search",
            "volume_folio" => "Volume/Folio search",
            "land_valuation_number" => "Land Val No. search",
            "name" => "Owner search",
            "name_parish" => "Owner search",
            _ => "Search"
        };
    }

    private static string Plural(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }

    private void ApplyFiscalResult(FiscalCadasterNeighborQueryResult result, CompareSurveyPlanEvidence plan)
    {
        FiscalEvidenceReviewed = true;
        FiscalEvidenceStatus = result.Message;
        if (result.Success && result.Records.Count > 0)
        {
            FiscalNeighborSummary = string.Join(Environment.NewLine, result.Records.Select(record =>
                $"Neighbor parcel: {Display(record.ParcelId)}; Relationship: {Display(record.SpatialRelationship)}; Side: {Display(record.BoundarySide)}; Display: {Display(record.OwnerOrTaxpayerDisplay)}; Source: {record.SourceLabel}; Queried: {record.QueriedAt:O}; Status: {record.Status}"));
        }

        foreach (var discrepancy in evidenceComparisonService.CompareFiscalNeighbors(plan, result))
        {
            AddEvidenceDiscrepancy(discrepancy);
        }

        RefreshEvidenceItems();
    }

    private void ApplyEnterpriseCadasterEvidenceResult(CompareEnterpriseCadasterEvidenceResult result)
    {
        FiscalEvidenceReviewed = true;
        FiscalEvidenceStatus = result.Message;
        if (!result.Success)
        {
            FiscalNeighborSummary = result.Diagnostic ?? result.Message;
            StatusText = result.Diagnostic ?? result.Message;
            RefreshEvidenceItems();
            return;
        }

        EnterpriseCadasterEvidenceRows.Clear();
        foreach (var record in CompareEnterpriseCadasterEvidenceClassifier.Sort(result.Records))
        {
            var item = new CompareEnterpriseCadasterEvidenceRowItem(record);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CompareEnterpriseCadasterEvidenceRowItem.IsIncluded))
                {
                    FiscalNeighborSummary = BuildEnterpriseCadasterSummary();
                    RefreshEvidenceItems();
                    RaiseStateProperties();
                }
            };
            EnterpriseCadasterEvidenceRows.Add(item);
        }

        FiscalNeighborSummary = EnterpriseCadasterEvidenceRows.Count == 0
            ? result.Diagnostic ?? "No Legal/Fiscal neighbor evidence returned."
            : BuildEnterpriseCadasterSummary();
        StatusText = result.Diagnostic;
        RefreshEvidenceItems();
    }

    private string BuildEnterpriseCadasterSummary()
    {
        if (EnterpriseCadasterEvidenceRows.Count == 0)
        {
            return "No Legal/Fiscal neighbor evidence rows are loaded.";
        }

        var included = EnterpriseCadasterEvidenceRows.Count(row => row.IsIncluded);
        var excluded = EnterpriseCadasterEvidenceRows.Count - included;
        var sourceCounts = EnterpriseCadasterEvidenceRows
            .GroupBy(row => row.SourceLabel)
            .Select(group => $"{group.Key}: {group.Count()}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        var relationships = EnterpriseCadasterEvidenceRows
            .GroupBy(row => row.SpatialRelationship)
            .Select(group => $"{group.Key}: {group.Count()}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        return $"Enterprise cadaster evidence loaded. Included: {included}; Excluded: {excluded}; Sources: {string.Join(", ", sourceCounts)}; Relationships: {string.Join(", ", relationships)}.";
    }

    private void SeedSearchFromEnterpriseEvidence(CompareEnterpriseCadasterEvidenceRowItem? row)
    {
        if (row is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(row.Volume) || !string.IsNullOrWhiteSpace(row.Folio))
        {
            SelectedEvidenceSearchMode = CompareEvidenceSearchMode.VolumeFolio;
            SearchVolume = NullIfBlank(row.Volume) ?? string.Empty;
            SearchFolio = NullIfBlank(row.Folio) ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(row.Pid) || !string.IsNullOrWhiteSpace(row.ParcelId))
        {
            SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Pid;
            SearchPid = NullIfBlank(row.Pid) ?? NullIfBlank(row.ParcelId) ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(row.LandValuationNumber))
        {
            SelectedEvidenceSearchMode = CompareEvidenceSearchMode.LandValuationNumber;
            SearchLandValuationNumber = row.LandValuationNumber;
        }
        else if (!string.IsNullOrWhiteSpace(row.DisplayName))
        {
            SelectedEvidenceSearchMode = CompareEvidenceSearchMode.Name;
            SearchName = row.DisplayName;
        }

        SearchParish = NullIfBlank(row.Parish) ?? SearchParish;
        EvidenceSearchStatusMessage = $"Search fields loaded from {row.SourceLabel} row {row.PrimaryIdentifier}.";
        SearchValidationMessage = string.Empty;
        RaiseStateProperties();
    }

    private void RestoreDraft(CaseFolderLayout currentLayout)
    {
        var draft = draftPersistence.Load(currentLayout);
        if (draft is null
            || !draft.TransactionNumber.Equals(TransactionNumber, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(draft.SchemaVersion, "1.0.0", StringComparison.OrdinalIgnoreCase)
            || !DateTimeOffset.TryParse(draft.SavedAtUtc, out _))
        {
            return;
        }

        Notes = draft.Notes;
        DecisionStatus = string.IsNullOrWhiteSpace(draft.DecisionState) ? "Draft" : draft.DecisionState;
        LegalEvidenceReviewed = draft.LegalEvidenceReviewed;
        FiscalEvidenceReviewed = draft.FiscalEvidenceReviewed;
        SurveyPlanSummary = draft.SurveyPlanSummary;
        LegalCadasterSummary = draft.LegalCadasterSummary;
        FiscalNeighborSummary = draft.FiscalNeighborSummary;
        Discrepancies.Clear();
        foreach (var discrepancy in draft.Discrepancies)
        {
            Discrepancies.Add(new CompareDiscrepancyItem(discrepancy.Title, discrepancy.Source, discrepancy.Status, discrepancy.IsResolved));
        }

        QueryResults.Clear();
        foreach (var result in draft.ManualQueryHistory ?? Array.Empty<CompareEvidenceSearchResultDraft>())
        {
            if (ShouldRestoreManualQueryResult(result))
            {
                QueryResults.Add(new CompareEvidenceSearchResultItem(result.ToModel()));
            }
        }

        ValuableEvidenceItems.Clear();
        foreach (var evidence in draft.ValuableEvidence ?? Array.Empty<CompareValuableEvidenceDraft>())
        {
            ValuableEvidenceItems.Add(new CompareValuableEvidenceItem(evidence.ToModel()));
        }

        EnterpriseCadasterEvidenceRows.Clear();
        foreach (var evidence in draft.EnterpriseCadasterEvidence ?? Array.Empty<CompareEnterpriseCadasterEvidenceDraft>())
        {
            EnterpriseCadasterEvidenceRows.Add(new CompareEnterpriseCadasterEvidenceRowItem(evidence.ToModel()));
        }
    }

    private static bool ShouldRestoreManualQueryResult(CompareEvidenceSearchResultDraft result)
    {
        if (result.Status.Equals(CompareEvidenceStatus.Ready, StringComparison.OrdinalIgnoreCase)
            || result.Status.Equals(CompareEvidenceStatus.NoRecordReturned, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasValue(result.DisplayName)
            || HasValue(result.PartyRole)
            || HasValue(result.ParcelId)
            || HasValue(result.LandValuationNumber)
            || HasValue(result.Parish)
            || HasValue(result.PropertyType)
            || HasValue(result.Tenure)
            || HasValue(result.RegisteredAtUtc);
    }

    private static bool HasValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private void SaveProgress()
    {
        if (!promptService.ConfirmSave())
        {
            StatusText = "Save cancelled.";
            return;
        }

        _ = SaveProgress(CompareReviewDecisionValues.SavedProgress, "Draft");
    }

    private async Task SuspendTaskAsync(CancellationToken cancellationToken = default)
    {
        if (taskLifecycleService is null)
        {
            StatusText = "Suspend task is unavailable because Compare is not connected to the transaction lifecycle.";
            return;
        }

        if (!promptService.ConfirmSuspend())
        {
            StatusText = "Suspend cancelled.";
            return;
        }

        var draftResult = SaveProgress(CompareReviewDecisionValues.SavedProgress, "Draft");
        if (draftResult is null)
        {
            return;
        }

        IsLoading = true;
        StatusText = "Suspending Compare task.";
        try
        {
            var result = await taskLifecycleService.SuspendAsync(TransactionNumber, cancellationToken);
            StatusText = result.Message;
            if (result.Success && result.ShouldCloseWorkspace)
            {
                await CleanupAfterTaskExitAsync(cancellationToken);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    private async Task CompleteTaskAsync(CancellationToken cancellationToken = default)
    {
        if (taskLifecycleService is null)
        {
            StatusText = "Complete task is unavailable because Compare is not connected to the transaction lifecycle.";
            return;
        }

        if (!CanCompleteTask)
        {
            StatusText = "Finalize is blocked until Compare readiness passes.";
            return;
        }

        var reportAlreadyGenerated = ComparePdfReportExists();
        if (!promptService.ConfirmFinalize(reportAlreadyGenerated))
        {
            StatusText = "Finalize cancelled.";
            return;
        }

        SaveDecision(CompareReviewDecisionValues.Approved, "Finalized");
        IsLoading = true;
        StatusText = "Preparing Compare report attachment.";
        try
        {
            if (!await UploadCompareReportIfConfiguredAsync(cancellationToken))
            {
                return;
            }

            StatusText = "Completing Compare task.";
            var result = await taskLifecycleService.CompleteAsync(TransactionNumber, cancellationToken);
            StatusText = result.Message;
            if (result.Success && result.ShouldCloseWorkspace)
            {
                await CleanupAfterTaskExitAsync(cancellationToken);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    private CompareReviewDraftSaveResult? SaveProgress(string decisionState, string displayStatus)
    {
        if (layout is null)
        {
            StatusText = "Open a Compare Case Folder before saving progress.";
            return null;
        }

        var draft = new CompareReviewDraftDocument(
            "1.0.0",
            TransactionNumber,
            Notes,
            decisionState,
            null,
            SurveyPlanSummary,
            LegalCadasterSummary,
            FiscalNeighborSummary,
            Discrepancies.Select(item => new CompareDiscrepancyDraft(item.Title, item.Source, item.Status, item.IsResolved)).ToArray(),
            transaction.TransactionId,
            transaction.TaskId,
            reviewerId,
            reviewerDisplayName,
            LegalEvidenceReviewed,
            FiscalEvidenceReviewed,
            QueryResults.Select(item => CompareEvidenceSearchResultDraft.FromModel(item.Result)).ToArray(),
            ValuableEvidenceItems.Select(item => CompareValuableEvidenceDraft.FromModel(item.ToModel())).ToArray(),
            EnterpriseCadasterEvidenceRows.Select(item => CompareEnterpriseCadasterEvidenceDraft.FromModel(item.ToModel())).ToArray());
        var result = draftPersistence.Save(layout, draft);
        var reportResult = result.Document is null
            ? CompareReviewReportResult.Failed("Compare report could not be generated because the draft was not saved.")
            : reportService.Generate(layout, transaction, result.Document);
        DecisionStatus = displayStatus;
        StatusText = BuildSaveProgressStatus(result, reportResult);
        return result with
        {
            Message = StatusText ?? result.Message
        };
    }

    private void BlockCompare()
    {
        SaveDecision(CompareReviewDecisionValues.Blocked, "Blocked");
    }

    private async Task FinalizeCompareAsync(CancellationToken cancellationToken = default)
    {
        if (!CanApproveCompare)
        {
            StatusText = "Finalize is blocked until documents and geometry are ready, at least one valuable evidence row is retained, and Notes are completed.";
            TraceFinalizeStep("blocked_readiness", false, StatusText, BuildFinalizeTraceDetails());
            return;
        }

        var reportAlreadyGenerated = ComparePdfReportExists();
        if (!promptService.ConfirmFinalize(reportAlreadyGenerated))
        {
            StatusText = "Finalize cancelled.";
            TraceFinalizeStep("cancelled_by_user", false, StatusText, BuildFinalizeTraceDetails());
            return;
        }

        TraceFinalizeStep(
            "started",
            true,
            "Finalize started.",
            BuildFinalizeTraceDetails(new Dictionary<string, string?>
            {
                ["report_already_generated"] = reportAlreadyGenerated.ToString(CultureInfo.InvariantCulture)
            }));
        SaveDecision(CompareReviewDecisionValues.Approved, "Finalized");
        TraceFinalizeStep(
            "report_generated",
            ComparePdfReportExists(),
            ComparePdfReportExists()
                ? "Finalize saved the decision and generated the PDF report."
                : "Finalize saved the decision, but the PDF report was not found.",
            BuildFinalizeTraceDetails());
        IsLoading = true;
        StatusText = "Preparing Compare report attachment.";
        try
        {
            if (!await UploadCompareReportIfConfiguredAsync(cancellationToken))
            {
                TraceFinalizeStep("stopped_before_lifecycle", false, "Finalize stopped before transaction completion because report upload did not succeed.", BuildFinalizeTraceDetails());
                return;
            }

            if (taskLifecycleService is null)
            {
                TraceFinalizeStep("lifecycle_missing", false, "Finalize cannot complete the transaction because the lifecycle service is not configured.", BuildFinalizeTraceDetails());
                return;
            }

            StatusText = "Finalizing Compare task.";
            var result = await taskLifecycleService.CompleteAsync(TransactionNumber, cancellationToken);
            StatusText = result.Message;
            TraceFinalizeStep(
                "lifecycle_complete_result",
                result.Success,
                result.Message,
                BuildFinalizeTraceDetails(new Dictionary<string, string?>
                {
                    ["should_close_workspace"] = result.ShouldCloseWorkspace.ToString(CultureInfo.InvariantCulture)
                }));
            if (result.Success && result.ShouldCloseWorkspace)
            {
                await CleanupAfterTaskExitAsync(cancellationToken);
                CloseRequested?.Invoke(this, EventArgs.Empty);
                TraceFinalizeStep("close_requested", true, "Finalize requested the Compare window to close after cleanup.", BuildFinalizeTraceDetails());
            }
            else
            {
                TraceFinalizeStep(
                    "cleanup_skipped",
                    false,
                    "Finalize did not clean the form or map because lifecycle completion did not return success with ShouldCloseWorkspace.",
                    BuildFinalizeTraceDetails(new Dictionary<string, string?>
                    {
                        ["lifecycle_success"] = result.Success.ToString(CultureInfo.InvariantCulture),
                        ["should_close_workspace"] = result.ShouldCloseWorkspace.ToString(CultureInfo.InvariantCulture)
                    }));
            }
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    private bool ComparePdfReportExists()
    {
        return layout is not null
            && File.Exists(Path.Combine(layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName));
    }

    private async Task CleanupAfterTaskExitAsync(CancellationToken cancellationToken)
    {
        var groupLayerName = currentCompareGroupLayerName;
        if (string.IsNullOrWhiteSpace(groupLayerName))
        {
            groupLayerName = $"Compare Review - {CompareWorkingGeometryService.NormalizeTransactionNumber(TransactionNumber)}";
        }

        TraceFinalizeStep(
            "cleanup_started",
            true,
            "Finalize cleanup started.",
            BuildFinalizeTraceDetails(new Dictionary<string, string?>
            {
                ["group_layer_name"] = groupLayerName,
                ["map_integration_service_configured"] = (mapIntegrationService is not null).ToString(CultureInfo.InvariantCulture)
            }));
        if (mapIntegrationService is not null)
        {
            var cleanup = await mapIntegrationService.RemoveTransactionGeometryFromActiveMapAsync(
                groupLayerName,
                cancellationToken);
            TraceFinalizeStep(
                "map_cleanup_result",
                cleanup.Success,
                cleanup.Message,
                BuildFinalizeTraceDetails(new Dictionary<string, string?>
                {
                    ["group_layer_name"] = groupLayerName
                }));
            if (!cleanup.Success)
            {
                StatusText = cleanup.Message;
            }
        }
        else
        {
            TraceFinalizeStep(
                "map_cleanup_skipped",
                false,
                "Map cleanup was skipped because no map integration service is configured.",
                BuildFinalizeTraceDetails(new Dictionary<string, string?>
                {
                    ["group_layer_name"] = groupLayerName
                }));
        }

        ClearWorkspaceStateAfterTaskExit();
        TraceFinalizeStep("form_cleanup_result", true, "Compare form state was cleared after Finalize.", BuildFinalizeTraceDetails());
    }

    private void ClearWorkspaceStateAfterTaskExit()
    {
        Documents.Clear();
        PdfDocuments.Clear();
        EvidenceItems.Clear();
        QueryResults.Clear();
        RelatedPartyMatches.Clear();
        ValuableEvidenceItems.Clear();
        EnterpriseCadasterEvidenceRows.Clear();
        Discrepancies.Clear();
        selectedDocument = null;
        currentGeometryPlan = null;
        currentCompareGroupLayerName = null;
        documentsAvailable = false;
        geometryAvailable = false;
        geometryRetryable = false;
        geometryBlocksApproval = true;
        DocumentStatus = "Compare workspace cleared.";
        GeometryStatus = "Compare map layers cleared.";
        RaiseStateProperties();
    }

    private async Task<bool> UploadCompareReportIfConfiguredAsync(CancellationToken cancellationToken)
    {
        if (reportAttachmentService is null)
        {
            TraceFinalizeStep("upload_skipped", false, "Compare report upload was skipped because no attachment service is configured.", BuildFinalizeTraceDetails());
            return true;
        }

        if (layout is null)
        {
            StatusText = "Open a Compare Case Folder before attaching the Compare report.";
            TraceFinalizeStep("upload_blocked_no_case_folder", false, StatusText, BuildFinalizeTraceDetails());
            return false;
        }

        var pdfPath = Path.Combine(layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName);
        TraceFinalizeStep(
            "upload_started",
            File.Exists(pdfPath),
            File.Exists(pdfPath)
                ? "Compare report upload started."
                : "Compare report upload started, but the PDF file was not found.",
            BuildFinalizeTraceDetails());
        var result = await reportAttachmentService.UploadAsync(transaction, pdfPath, cancellationToken);
        StatusText = result.Message;
        TraceFinalizeStep(
            "upload_result",
            result.Success,
            result.Message,
            BuildFinalizeTraceDetails(new Dictionary<string, string?>
            {
                ["source_type"] = result.SourceType,
                ["uploaded_pdf_path"] = result.PdfReportPath
            }));
        return result.Success;
    }

    private void TraceFinalizeStep(
        string step,
        bool success,
        string? message,
        IReadOnlyDictionary<string, string?>? details = null)
    {
        finalizeTracePersistence.Append(
            layout,
            TransactionNumber,
            getUtcNow(),
            step,
            success,
            message ?? string.Empty,
            details);
    }

    private IReadOnlyDictionary<string, string?> BuildFinalizeTraceDetails(
        IReadOnlyDictionary<string, string?>? additional = null)
    {
        var details = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["transaction_id"] = transaction.TransactionId,
            ["transaction_number"] = transaction.TransactionNumber,
            ["task_id"] = transaction.TaskId,
            ["case_folder_open"] = (layout is not null).ToString(CultureInfo.InvariantCulture),
            ["report_attachment_service_configured"] = (reportAttachmentService is not null).ToString(CultureInfo.InvariantCulture),
            ["task_lifecycle_service_configured"] = (taskLifecycleService is not null).ToString(CultureInfo.InvariantCulture),
            ["map_integration_service_configured"] = (mapIntegrationService is not null).ToString(CultureInfo.InvariantCulture),
            ["documents_available"] = documentsAvailable.ToString(CultureInfo.InvariantCulture),
            ["geometry_available"] = geometryAvailable.ToString(CultureInfo.InvariantCulture),
            ["current_compare_group_layer_name"] = currentCompareGroupLayerName
        };

        if (layout is not null)
        {
            details["case_folder"] = layout.RootDirectory;
            details["working_directory"] = layout.WorkingDirectory;
            details["reports_directory"] = layout.ReportsDirectory;
            AddFileDetails(details, "json_report", Path.Combine(layout.ReportsDirectory, CompareReviewReportService.ReportFileName));
            AddFileDetails(details, "pdf_report", Path.Combine(layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName));
            AddFileDetails(details, "decision", Path.Combine(layout.WorkingDirectory, CompareReviewDecisionPersistenceService.DecisionArtifactFileName));
        }

        if (additional is not null)
        {
            foreach (var pair in additional)
            {
                details[pair.Key] = pair.Value;
            }
        }

        return details;
    }

    private static void AddFileDetails(IDictionary<string, string?> details, string prefix, string path)
    {
        details[$"{prefix}_path"] = path;
        var exists = File.Exists(path);
        details[$"{prefix}_exists"] = exists.ToString(CultureInfo.InvariantCulture);
        if (exists)
        {
            var info = new FileInfo(path);
            details[$"{prefix}_bytes"] = info.Length.ToString(CultureInfo.InvariantCulture);
            details[$"{prefix}_last_write_utc"] = info.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private void SaveDecision(string decision, string displayStatus)
    {
        if (layout is null)
        {
            StatusText = "Open a Compare Case Folder before recording a decision.";
            return;
        }

        var draftResult = SaveProgress(decision, displayStatus);
        var isApproved = decision.Equals(CompareReviewDecisionValues.Approved, StringComparison.OrdinalIgnoreCase);
        var document = new CompareReviewDecisionDocument(
            "1.0.0",
            transaction.TransactionId,
            transaction.TransactionNumber,
            transaction.TaskId,
            reviewerId,
            reviewerDisplayName,
            getUtcNow().UtcDateTime.ToString("O"),
            decision,
            Notes,
            isApproved ? CompareReviewReadinessStatus.CommitReady : CompareReviewReadinessStatus.CommitBlocked,
            BuildEvidenceRefs(draftResult?.Path),
            Discrepancies.Select(item => new CompareReviewDiscrepancySummary(
                item.Title,
                item.Source,
                item.Status,
                item.IsResolved,
                !item.IsResolved)).ToArray());
        var path = decisionPersistence.Save(layout, document);
        DecisionStatus = displayStatus;
        StatusText = $"{displayStatus} decision recorded: {Path.GetFileName(path)}.";
        RaiseStateProperties();
    }

    private IReadOnlyList<CompareReviewEvidenceRef> BuildEvidenceRefs(string? draftPath)
    {
        var refs = new List<CompareReviewEvidenceRef>
        {
            new CompareReviewEvidenceRef("compare_review_draft", ToRelativeCasePath(draftPath), "Draft notes and evidence summaries."),
            new CompareReviewEvidenceRef("survey_plan", null, SurveyPlanSummary),
            new CompareReviewEvidenceRef("legal_cadaster", null, LegalCadasterSummary),
            new CompareReviewEvidenceRef("fiscal_cadaster", null, FiscalNeighborSummary)
        };

        refs.AddRange(ValuableEvidenceItems.Select(item => new CompareReviewEvidenceRef(
            item.SourceType,
            null,
            $"{item.RoleTag}: {item.DisplaySummary}")));

        refs.AddRange(EnterpriseCadasterEvidenceRows
            .Where(item => item.IsIncluded)
            .Select(item => new CompareReviewEvidenceRef(
                item.SourceKind.Equals(CompareEnterpriseCadasterSourceKind.Legal, StringComparison.OrdinalIgnoreCase)
                    ? "legal_cadaster_spatial"
                    : "fiscal_cadaster_spatial",
                null,
                item.Summary)));

        var reportPath = layout is null
            ? null
            : Path.Combine(layout.ReportsDirectory, CompareReviewReportService.ReportFileName);
        if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath))
        {
            refs.Add(new CompareReviewEvidenceRef(
                "compare_review_report",
                ToRelativeCasePath(reportPath),
                "Compare review report."));
        }

        var pdfReportPath = layout is null
            ? null
            : Path.Combine(layout.ReportsDirectory, CompareReviewReportService.PdfReportFileName);
        if (!string.IsNullOrWhiteSpace(pdfReportPath) && File.Exists(pdfReportPath))
        {
            refs.Add(new CompareReviewEvidenceRef(
                "compare_review_report_pdf",
                ToRelativeCasePath(pdfReportPath),
                "Compare review report PDF."));
        }

        return refs;
    }

    private static string BuildSaveProgressStatus(CompareReviewDraftSaveResult draftResult, CompareReviewReportResult reportResult)
    {
        if (!draftResult.Success)
        {
            return draftResult.Message;
        }

        return reportResult.Success
            ? "Save complete. Compare report generated and overwritten."
            : $"Compare saved. {reportResult.Message}";
    }

    private string? ToRelativeCasePath(string? path)
    {
        if (layout is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relative = Path.GetRelativePath(layout.RootDirectory, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative)
            ? null
            : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private void RestoreDecision(CaseFolderLayout currentLayout)
    {
        var result = decisionPersistence.LoadForTransaction(currentLayout, transaction);
        if (!result.Success || result.Document is null)
        {
            return;
        }

        DecisionStatus = result.Document.Decision switch
        {
            CompareReviewDecisionValues.Approved => "Finalized",
            CompareReviewDecisionValues.Blocked => "Blocked",
            CompareReviewDecisionValues.ReturnedToCompute => "Returned to Compute",
            _ => DecisionStatus
        };
        if (!string.IsNullOrWhiteSpace(result.Document.Notes))
        {
            Notes = result.Document.Notes;
        }

        restoredDecisionEvidenceRefs.Clear();
        restoredDecisionEvidenceRefs.AddRange(result.Document.EvidenceRefs);
        if (result.Document.Decision.Equals(CompareReviewDecisionValues.Approved, StringComparison.OrdinalIgnoreCase))
        {
            LegalEvidenceReviewed = true;
            FiscalEvidenceReviewed = true;
        }

        if (Discrepancies.Count == 0)
        {
            foreach (var discrepancy in result.Document.Discrepancies)
            {
                Discrepancies.Add(new CompareDiscrepancyItem(
                    discrepancy.Title,
                    discrepancy.Source,
                    discrepancy.Status,
                    discrepancy.IsResolved));
            }
        }

        RefreshEvidenceItems();
        RaiseStateProperties();
    }

    private void AddEvidenceDiscrepancy(CompareEvidenceDiscrepancy discrepancy)
    {
        if (Discrepancies.Any(item => item.Title.Equals(discrepancy.Title, StringComparison.OrdinalIgnoreCase)
            && item.Source.Equals(discrepancy.EvidenceSource, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Discrepancies.Add(new CompareDiscrepancyItem(
            discrepancy.Title,
            discrepancy.EvidenceSource,
            discrepancy.Status,
            discrepancy.IsResolved));
        StatusText = discrepancy.Diagnostic;
        RaiseStateProperties();
    }

    private static string BuildSurveyPlanSummary(CompareSurveyPlanEvidence evidence)
    {
        return $"Owner: {Display(evidence.OwnerName)}; Parcel: {Display(evidence.ParcelId)}; Volume/Folio: {Display(evidence.Volume)}/{Display(evidence.Folio)}; Adjacent owner labels: {evidence.AdjacentOwnerLabels.Count}; Source: Survey plan.";
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value.Trim();
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void RefreshEvidenceItems()
    {
        EvidenceItems.Clear();
        EvidenceItems.Add(new CompareEvidenceItem("Survey plan interpretation", SurveyPlanSummary, "Survey plan"));
        EvidenceItems.Add(new CompareEvidenceItem("Legal cadaster owner records", LegalCadasterSummary, "Legal cadaster"));
        EvidenceItems.Add(new CompareEvidenceItem("Fiscal neighbor records", FiscalNeighborSummary, "Fiscal cadaster - neighbor context only"));
        if (EnterpriseCadasterEvidenceRows.Count > 0)
        {
            EvidenceItems.Add(new CompareEvidenceItem(
                "Enterprise Legal/Fiscal spatial evidence",
                BuildEnterpriseCadasterSummary(),
                "ArcGIS Enterprise cadaster layers"));
        }
        foreach (var reference in restoredDecisionEvidenceRefs)
        {
            EvidenceItems.Add(new CompareEvidenceItem(
                $"Prior decision evidence: {reference.EvidenceType}",
                string.IsNullOrWhiteSpace(reference.Summary)
                    ? reference.RelativePath ?? "(no summary)"
                    : reference.Summary,
                "Compare decision"));
        }
    }

    private void RaiseStateProperties()
    {
        NotifyPropertyChanged(nameof(CanReloadGeometry));
        NotifyPropertyChanged(nameof(CanQueryEvidence));
        NotifyPropertyChanged(nameof(CanQueryLegalEvidence));
        NotifyPropertyChanged(nameof(CanQueryFiscalEvidence));
        NotifyPropertyChanged(nameof(CanRunEvidenceSearch));
        NotifyPropertyChanged(nameof(CanSaveProgress));
        NotifyPropertyChanged(nameof(CanSuspendTask));
        NotifyPropertyChanged(nameof(CanCompleteTask));
        NotifyPropertyChanged(nameof(CanBlockCompare));
        NotifyPropertyChanged(nameof(CanApproveCompare));
        NotifyPropertyChanged(nameof(HasUnresolvedDiscrepancies));
        NotifyPropertyChanged(nameof(LegalEvidenceReviewed));
        NotifyPropertyChanged(nameof(FiscalEvidenceReviewed));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[]
        {
            ReloadGeometryCommand,
            QueryParcelIdCommand,
            QueryVolumeFolioCommand,
            FindNeighborsCommand,
            RefreshEnterpriseCadasterEvidenceCommand,
            SeedSearchFromEnterpriseEvidenceCommand,
            RunEvidenceSearchCommand,
            ClearEvidenceSearchFieldsCommand,
            MarkEvidenceResultValuableCommand,
            RemoveValuableEvidenceCommand,
            SaveProgressCommand,
            SuspendTaskCommand,
            CompleteTaskCommand,
            BlockCompareCommand,
            ApproveCompareCommand
        })
        {
            if (command is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }
    }

    private bool SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RefreshPdfDocumentSelectorState()
    {
        NotifyPropertyChanged(nameof(HasPdfDocuments));
        NotifyPropertyChanged(nameof(PdfDocumentSelectorStatus));
    }
}

public sealed record CompareDocumentItem(SourceFileCopyResult SourceFile)
{
    public string FileName => SourceFile.FileName;

    public string Type => SourceFile.FileType;

    public string Role => SourceFile.SourceRole ?? "Source";

    public string Status => SourceFile.Status;

    public bool IsPdf => string.Equals(SourceFile.FileType, ".pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetExtension(SourceFile.FileName), ".pdf", StringComparison.OrdinalIgnoreCase);
}

public sealed record CompareEvidenceItem(string Title, string Summary, string Source);

public sealed record CompareEvidenceSearchResultItem(CompareEvidenceSearchResult Result)
{
    public string SourceType => Result.SourceType;

    public string SourceLabel => Result.SourceLabel;

    public string QueryKey => Result.QueryKey;

    public string DisplayName => string.IsNullOrWhiteSpace(Result.DisplayName) ? "(no name)" : Result.DisplayName.Trim();

    public string Owner => DisplayName;

    public string PropertyType => string.IsNullOrWhiteSpace(Result.PropertyType) ? "(blank)" : Result.PropertyType.Trim();

    public string Tenure => string.IsNullOrWhiteSpace(Result.Tenure) ? "(blank)" : Result.Tenure.Trim();

    public string DateRegistered => Result.RegisteredAt is null
        ? "(blank)"
        : Result.RegisteredAt.Value.ToString("dd/MMM/yyyy", CultureInfo.InvariantCulture);

    public string PartyRole => string.IsNullOrWhiteSpace(Result.PartyRole) ? "(role not specified)" : Result.PartyRole.Trim();

    public string ParcelId => string.IsNullOrWhiteSpace(Result.ParcelId) ? "(blank)" : Result.ParcelId.Trim();

    public string VolumeFolio => string.IsNullOrWhiteSpace(Result.Volume) && string.IsNullOrWhiteSpace(Result.Folio)
        ? "(blank)"
        : $"{Result.Volume ?? string.Empty}/{Result.Folio ?? string.Empty}";

    public string LandValuationNumber => string.IsNullOrWhiteSpace(Result.LandValuationNumber) ? "(blank)" : Result.LandValuationNumber.Trim();

    public string Parish => string.IsNullOrWhiteSpace(Result.Parish) ? "(blank)" : Result.Parish.Trim();

    public string Status => Result.Status;

    public string Summary => Result.DisplaySummary;

    public bool CanMarkValuable => Result.Status.Equals(CompareEvidenceStatus.Ready, StringComparison.OrdinalIgnoreCase)
        || Result.Status.Equals(CompareEvidenceStatus.Ambiguous, StringComparison.OrdinalIgnoreCase);

    public CompareValuableEvidence ToValuableEvidence(string evidenceId, DateTimeOffset capturedAt)
    {
        return new CompareValuableEvidence(
            evidenceId,
            Result.SourceType,
            Result.SourceLabel,
            Result.QueryKey,
            Result.DisplaySummary,
            DefaultRoleTag(Result),
            capturedAt,
            Result.Diagnostic);
    }

    private static string DefaultRoleTag(CompareEvidenceSearchResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.PartyRole))
        {
            if (result.PartyRole.Contains("occup", StringComparison.OrdinalIgnoreCase))
            {
                return CompareEvidenceRoleTag.Occupant;
            }

            if (result.PartyRole.Contains("possess", StringComparison.OrdinalIgnoreCase))
            {
                return CompareEvidenceRoleTag.InPossession;
            }

            if (result.PartyRole.Contains("neighbor", StringComparison.OrdinalIgnoreCase))
            {
                return CompareEvidenceRoleTag.Neighbor;
            }
        }

        return result.SourceType.Equals(CompareEvidenceSourceType.FiscalCadaster, StringComparison.OrdinalIgnoreCase)
            ? CompareEvidenceRoleTag.Neighbor
            : CompareEvidenceRoleTag.Owner;
    }
}

public sealed record ComparePartySearchResultItem(LegalCadasterPartyRecord PartyRecord)
{
    public string SourceLabel => PartyRecord.SourceLabel;

    public string QueryKey => PartyRecord.QueryKey;

    public string PartyName => string.IsNullOrWhiteSpace(PartyRecord.PartyName) ? "(no name)" : PartyRecord.PartyName.Trim();

    public string Prid => string.IsNullOrWhiteSpace(PartyRecord.Prid) ? "(blank)" : PartyRecord.Prid.Trim();

    public string FullAddress => string.IsNullOrWhiteSpace(PartyRecord.FullAddress) ? "(blank)" : PartyRecord.FullAddress.Trim();

    public string TaxNumber => string.IsNullOrWhiteSpace(PartyRecord.TaxNumber) ? "(blank)" : PartyRecord.TaxNumber.Trim();

    public string PartyStatus => string.IsNullOrWhiteSpace(PartyRecord.PartyStatus) ? "(blank)" : PartyRecord.PartyStatus.Trim();

    public string PartyType => string.IsNullOrWhiteSpace(PartyRecord.PartyType) ? "(blank)" : PartyRecord.PartyType.Trim();

    public bool CanMarkValuable => true;

    public CompareValuableEvidence ToValuableEvidence(string evidenceId, DateTimeOffset capturedAt)
    {
        return new CompareValuableEvidence(
            evidenceId,
            CompareEvidenceSourceType.LegalCadaster,
            string.IsNullOrWhiteSpace(PartyRecord.SourceLabel) ? "Innola Party Match" : PartyRecord.SourceLabel,
            PartyRecord.QueryKey,
            $"Party match: {PartyRecord.DisplaySummary}",
            CompareEvidenceRoleTag.Owner,
            capturedAt,
            PartyRecord.Diagnostic);
    }
}

public sealed class CompareEnterpriseCadasterEvidenceRowItem : INotifyPropertyChanged
{
    private bool isIncluded;

    public CompareEnterpriseCadasterEvidenceRowItem(CompareEnterpriseCadasterEvidenceRecord record)
    {
        SourceKind = record.SourceKind;
        SourceLabel = record.SourceLabel;
        LayerUrl = record.LayerUrl;
        ObjectId = record.ObjectId;
        GlobalId = record.GlobalId;
        Suid = record.Suid;
        ParcelId = record.ParcelId;
        Pid = record.Pid;
        Volume = record.Volume;
        Folio = record.Folio;
        LandValuationNumber = record.LandValuationNumber;
        OwnerName = record.OwnerName;
        OccupantName = record.OccupantName;
        TaxpayerName = record.TaxpayerName;
        Parish = record.Parish;
        SpatialRelationship = record.SpatialRelationship;
        isIncluded = record.IsIncluded;
        QueriedAt = record.QueriedAt;
        Status = record.Status;
        Diagnostic = record.Diagnostic;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SourceKind { get; }

    public string SourceLabel { get; }

    public string LayerUrl { get; }

    public string? ObjectId { get; }

    public string? GlobalId { get; }

    public string? Suid { get; }

    public string? ParcelId { get; }

    public string? Pid { get; }

    public string? Volume { get; }

    public string? Folio { get; }

    public string? LandValuationNumber { get; }

    public string? OwnerName { get; }

    public string? OccupantName { get; }

    public string? TaxpayerName { get; }

    public string? Parish { get; }

    public string SpatialRelationship { get; }

    public DateTimeOffset QueriedAt { get; }

    public string Status { get; }

    public string? Diagnostic { get; }

    public bool IsIncluded
    {
        get => isIncluded;
        set
        {
            if (isIncluded == value)
            {
                return;
            }

            isIncluded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
        }
    }

    public string DisplayName
    {
        get
        {
            var display = FirstNonBlank(OwnerName, OccupantName, TaxpayerName);
            return string.IsNullOrWhiteSpace(display) ? "(no party)" : display;
        }
    }

    public string PrimaryIdentifier => FirstNonBlank(Pid, ParcelId, LandValuationNumber, ObjectId, GlobalId) ?? "(blank)";

    public string VolumeFolio => string.IsNullOrWhiteSpace(Volume) && string.IsNullOrWhiteSpace(Folio)
        ? "(blank)"
        : $"{Volume ?? string.Empty}/{Folio ?? string.Empty}";

    public string Summary => ToModel().DisplaySummary;

    public CompareEnterpriseCadasterEvidenceRecord ToModel()
    {
        return new CompareEnterpriseCadasterEvidenceRecord(
            SourceKind,
            SourceLabel,
            LayerUrl,
            ObjectId,
            GlobalId,
            Suid,
            ParcelId,
            Pid,
            Volume,
            Folio,
            LandValuationNumber,
            OwnerName,
            OccupantName,
            TaxpayerName,
            Parish,
            SpatialRelationship,
            IsIncluded,
            QueriedAt,
            Status,
            Diagnostic);
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}

public sealed class CompareValuableEvidenceItem : INotifyPropertyChanged
{
    private string roleTag;

    public CompareValuableEvidenceItem(CompareValuableEvidence evidence)
    {
        EvidenceId = evidence.EvidenceId;
        SourceType = evidence.SourceType;
        SourceLabel = evidence.SourceLabel;
        QueryKey = evidence.QueryKey;
        DisplaySummary = evidence.DisplaySummary;
        roleTag = string.IsNullOrWhiteSpace(evidence.RoleTag) ? CompareEvidenceRoleTag.Owner : evidence.RoleTag;
        CapturedAt = evidence.CapturedAt;
        Diagnostic = evidence.Diagnostic;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EvidenceId { get; }

    public string SourceType { get; }

    public string SourceLabel { get; }

    public string QueryKey { get; }

    public string DisplaySummary { get; }

    public DateTimeOffset CapturedAt { get; }

    public string? Diagnostic { get; }

    public string RoleTag
    {
        get => roleTag;
        set
        {
            if (roleTag.Equals(value, StringComparison.Ordinal))
            {
                return;
            }

            roleTag = string.IsNullOrWhiteSpace(value) ? CompareEvidenceRoleTag.Other : value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RoleTag)));
        }
    }

    public CompareValuableEvidence ToModel()
    {
        return new CompareValuableEvidence(
            EvidenceId,
            SourceType,
            SourceLabel,
            QueryKey,
            DisplaySummary,
            RoleTag,
            CapturedAt,
            Diagnostic);
    }
}

public sealed class CompareDiscrepancyItem : INotifyPropertyChanged
{
    private string status;
    private bool isResolved;

    public CompareDiscrepancyItem(string title, string source, string status, bool isResolved)
    {
        Title = title;
        Source = source;
        this.status = status;
        this.isResolved = isResolved;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string Source { get; }

    public string Status
    {
        get => status;
        set
        {
            status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    public bool IsResolved
    {
        get => isResolved;
        set
        {
            isResolved = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsResolved)));
        }
    }
}
