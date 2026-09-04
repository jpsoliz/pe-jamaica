using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace ParcelWorkflowAddIn.Workflow.FabricMaintenance;

public sealed class FabricMaintenancePromotionViewModel : INotifyPropertyChanged
{
    private readonly Action<string> showMessage;
    private readonly Func<string, bool> confirmAction;
    private readonly IFabricMaintenanceReviewLoadService reviewLoadService;
    private readonly IFabricMaintenanceFinalWriteCompletionService finalWriteCompletionService;
    private FabricMaintenanceTarget selectedTarget;
    private FabricMaintenancePromotionDecision selectedDecision;
    private string peNumber;
    private string decisionNotes = string.Empty;
    private string statusText = string.Empty;
    private FabricMaintenanceReadinessResult readiness;
    private bool isLoadParcelRunning;
    private bool isReviewLoaded;
    private bool isCancelRunning;
    private bool isConfirmFinalWriteRunning;
    private FabricMaintenanceFinalCandidate? selectedFinalCandidate;

    public FabricMaintenancePromotionViewModel(
        string currentTransactionNumber,
        string peNumber,
        FabricMaintenancePromotionSettings settings,
        string? initialStatusText = null,
        Action<string>? showMessage = null,
        Func<string, bool>? confirmAction = null,
        IFabricMaintenanceReviewLoadService? reviewLoadService = null,
        IFabricMaintenanceFinalWriteCompletionService? finalWriteCompletionService = null)
    {
        CurrentTransactionNumber = currentTransactionNumber;
        this.peNumber = peNumber;
        IsPeNumberEditable = string.IsNullOrWhiteSpace(peNumber);
        this.showMessage = showMessage ?? (_ => { });
        this.confirmAction = confirmAction ?? (_ => true);
        this.reviewLoadService = reviewLoadService ?? new DeferredFabricMaintenanceReviewLoadService();
        this.finalWriteCompletionService = finalWriteCompletionService ?? new DeferredFabricMaintenanceFinalWriteCompletionService();
        Settings = settings;
        WorkingReviewPlan = FabricMaintenanceWorkingReviewPlanner.BuildPlan(settings, currentTransactionNumber, peNumber);
        StatusText = string.IsNullOrWhiteSpace(initialStatusText) ? WorkingReviewPlan.Message : initialStatusText;
        SelectLegalCommand = new RelayCommand(() => SelectTarget(FabricMaintenanceTarget.Legal));
        SelectCadastralCommand = new RelayCommand(() => SelectTarget(FabricMaintenanceTarget.Fiscal));
        LoadParcelCommand = new RelayCommand(async () => await LoadParcelAsync().ConfigureAwait(true), () => CanLoadParcel);
        SelectWorkingFeaturesCommand = new RelayCommand(() => StatusText = "Select working_review features in the ArcGIS Pro map.");
        OpenAttributeTablesCommand = new RelayCommand(() => StatusText = "Open working_review and final cadastre attribute tables in ArcGIS Pro.");
        OpenTopologyToolsCommand = new RelayCommand(() => StatusText = "Open standard ArcGIS Pro topology/geoprocessing tools for this review.");
        ReplaceExistingCommand = new RelayCommand(() => SelectDecision(FabricMaintenancePromotionDecision.ReplaceExisting));
        KeepExistingDiscardWorkingCommand = new RelayCommand(() => SelectDecision(FabricMaintenancePromotionDecision.KeepExistingDiscardWorking));
        MergeUpdateAttributesOnlyCommand = new RelayCommand(() => SelectDecision(FabricMaintenancePromotionDecision.MergeUpdateAttributesOnly));
        SendBackForReviewCommand = new RelayCommand(() => SelectDecision(FabricMaintenancePromotionDecision.SendBackForReview));
        ApproveForFinalWriteCommand = new RelayCommand(ApproveForFinalWrite, () => CanApproveForFinalWrite);
        ConfirmFinalWriteCommand = new RelayCommand(async () => await ConfirmFinalWriteAsync().ConfigureAwait(true), () => CanConfirmFinalWrite);
        CancelCommand = new RelayCommand(async () => await CancelAsync().ConfigureAwait(true), () => CanCancel);
        ResetEvidenceChecks(0, 0);
        WorkingFeatureCounts = new FabricMaintenanceFeatureCounts(0, 0, 0, 0);
        CandidateStatus = "Select Legal or Cadastral, then Load Parcel.";
        readiness = new FabricMaintenanceReadinessResult(false, "Select a final target and implemented decision.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RequestClose;

    public string CurrentTransactionNumber { get; }

    public string PeNumber
    {
        get => peNumber;
        set
        {
            if (string.Equals(peNumber, value, StringComparison.Ordinal))
            {
                return;
            }

            peNumber = value;
            InvalidateLoadedReview();
            NotifyPropertyChanged(nameof(PeNumber));
            NotifyPropertyChanged(nameof(ParcelInReview));
            NotifyPropertyChanged(nameof(ConfirmationSummary));
            RefreshCandidatePlan();
            RefreshReadiness();
        }
    }

    public bool IsPeNumberEditable { get; }

    public string ParcelInReview
    {
        get => PeNumber;
        set => PeNumber = value;
    }

    public bool IsParcelInReviewEditable => IsPeNumberEditable;

    public FabricMaintenancePromotionSettings Settings { get; }

    public FabricMaintenanceWorkingReviewPlan WorkingReviewPlan { get; }

    public ObservableCollection<FabricMaintenanceCheckResult> ReviewChecks { get; } = [];

    public ObservableCollection<FabricMaintenanceCheckResult> AttributeChecks { get; } = [];

    public ObservableCollection<FabricMaintenanceReviewResultRow> ReviewResults { get; } = [];

    public ObservableCollection<FabricMaintenanceFinalCandidate> FinalCandidates { get; } = [];

    public ICommand SelectLegalCommand { get; }

    public ICommand SelectCadastralCommand { get; }

    public ICommand LoadParcelCommand { get; }

    public ICommand SelectWorkingFeaturesCommand { get; }

    public ICommand OpenAttributeTablesCommand { get; }

    public ICommand OpenTopologyToolsCommand { get; }

    public ICommand ReplaceExistingCommand { get; }

    public ICommand KeepExistingDiscardWorkingCommand { get; }

    public ICommand MergeUpdateAttributesOnlyCommand { get; }

    public ICommand SendBackForReviewCommand { get; }

    public ICommand ApproveForFinalWriteCommand { get; }

    public ICommand ConfirmFinalWriteCommand { get; }

    public ICommand CancelCommand { get; }

    public string StatusText
    {
        get => statusText;
        private set
        {
            statusText = value;
            NotifyPropertyChanged(nameof(StatusText));
        }
    }

    public string DecisionNotes
    {
        get => decisionNotes;
        set
        {
            decisionNotes = value;
            NotifyPropertyChanged(nameof(DecisionNotes));
            RefreshReadiness();
        }
    }

    public FabricMaintenanceTarget SelectedTarget
    {
        get => selectedTarget;
        private set
        {
            selectedTarget = value;
            NotifyPropertyChanged(nameof(SelectedTarget));
            NotifyPropertyChanged(nameof(TargetLabel));
            NotifyPropertyChanged(nameof(IsLegalTargetSelected));
            NotifyPropertyChanged(nameof(IsCadastralTargetSelected));
            NotifyPropertyChanged(nameof(ConfirmationSummary));
            RefreshReadiness();
        }
    }

    public bool IsLegalTargetSelected
    {
        get => SelectedTarget == FabricMaintenanceTarget.Legal;
        set
        {
            if (value)
            {
                SelectTarget(FabricMaintenanceTarget.Legal);
            }
        }
    }

    public bool IsCadastralTargetSelected
    {
        get => SelectedTarget == FabricMaintenanceTarget.Fiscal;
        set
        {
            if (value)
            {
                SelectTarget(FabricMaintenanceTarget.Fiscal);
            }
        }
    }

    public FabricMaintenancePromotionDecision SelectedDecision
    {
        get => selectedDecision;
        private set
        {
            selectedDecision = value;
            NotifyPropertyChanged(nameof(SelectedDecision));
            NotifyPropertyChanged(nameof(DecisionLabel));
            NotifyPropertyChanged(nameof(ConfirmationSummary));
            RefreshReadiness();
        }
    }

    public FabricMaintenanceFeatureCounts WorkingFeatureCounts { get; private set; }

    public int CandidateCount { get; private set; }

    public string? SelectedCandidateId { get; private set; }

    public FabricMaintenanceFinalCandidate? SelectedFinalCandidate
    {
        get => selectedFinalCandidate;
        set
        {
            selectedFinalCandidate = value;
            SelectedCandidateId = value?.CandidateId;
            NotifyPropertyChanged(nameof(SelectedFinalCandidate));
            NotifyPropertyChanged(nameof(SelectedCandidateId));
            NotifyPropertyChanged(nameof(ConfirmationSummary));
            RefreshReadiness();
        }
    }

    public string CandidateStatus { get; private set; }

    public bool FinalWriteApproved { get; private set; }

    public bool IsLoadParcelRunning
    {
        get => isLoadParcelRunning;
        private set
        {
            isLoadParcelRunning = value;
            NotifyPropertyChanged(nameof(IsLoadParcelRunning));
            NotifyPropertyChanged(nameof(CanLoadParcel));
            RaiseCommandState();
        }
    }

    public bool IsReviewLoaded
    {
        get => isReviewLoaded;
        private set
        {
            isReviewLoaded = value;
            NotifyPropertyChanged(nameof(IsReviewLoaded));
            NotifyPropertyChanged(nameof(CanLoadParcel));
            RaiseCommandState();
        }
    }

    public bool IsCancelRunning
    {
        get => isCancelRunning;
        private set
        {
            isCancelRunning = value;
            NotifyPropertyChanged(nameof(IsCancelRunning));
            NotifyPropertyChanged(nameof(CanCancel));
            RaiseCommandState();
        }
    }
    public bool IsConfirmFinalWriteRunning
    {
        get => isConfirmFinalWriteRunning;
        private set
        {
            isConfirmFinalWriteRunning = value;
            NotifyPropertyChanged(nameof(IsConfirmFinalWriteRunning));
            NotifyPropertyChanged(nameof(CanConfirmFinalWrite));
            RaiseCommandState();
        }
    }

    public bool CanLoadParcel => !IsLoadParcelRunning && !IsReviewLoaded;

    public bool CanCancel => !IsCancelRunning;

    public bool CanApproveForFinalWrite => readiness.IsReady;

    public bool CanConfirmFinalWrite => FinalWriteApproved && !IsConfirmFinalWriteRunning;

    public string TargetLabel => SelectedTarget switch
    {
        FabricMaintenanceTarget.Legal => "Legal",
        FabricMaintenanceTarget.Fiscal => "Cadastral",
        _ => "Not selected"
    };

    public string DecisionLabel => SelectedDecision switch
    {
        FabricMaintenancePromotionDecision.KeepExistingDiscardWorking => "Keep existing, discard working",
        FabricMaintenancePromotionDecision.SendBackForReview => "Send back for review",
        FabricMaintenancePromotionDecision.ReplaceExisting => "Replace existing",
        FabricMaintenancePromotionDecision.MergeUpdateAttributesOnly => "Merge/update attributes only",
        _ => "Not selected"
    };

    public string ReadinessText => readiness.Message;

    public string ConfirmationSummary =>
        $"TR {CurrentTransactionNumber}; Parcel in Review {ParcelInReview}; Target {TargetLabel}; Decision {DecisionLabel}; Working features P{WorkingFeatureCounts.Points}/L{WorkingFeatureCounts.Lines}/G{WorkingFeatureCounts.Polygons}; Candidate {SelectedCandidateId ?? "new final record candidate"}; Artifact final_cadastre_promotion_summary.json.";

    private void SelectTarget(FabricMaintenanceTarget target)
    {
        if (SelectedTarget == target)
        {
            return;
        }

        InvalidateLoadedReview();
        SelectedTarget = target;
        RefreshCandidatePlan();
    }

    private void RefreshCandidatePlan()
    {
        if (SelectedTarget == FabricMaintenanceTarget.None)
        {
            CandidateStatus = "Select Legal or Cadastral, then Load Parcel.";
            NotifyPropertyChanged(nameof(CandidateStatus));
            return;
        }

        var plan = FabricMaintenanceFinalTargetQueryPlanner.BuildPlan(
            Settings,
            SelectedTarget,
            new FabricMaintenanceCandidateSearchKeys(null, null, null, null));
        CandidateStatus = plan.IsValid
            ? $"{plan.TargetLabel} target selected. Press Load Parcel to query working_review by Parcel in Review and load spatial candidates."
            : plan.Message;
        NotifyPropertyChanged(nameof(CandidateStatus));
        StatusText = CandidateStatus;
    }

    private async Task LoadParcelAsync()
    {
        if (IsLoadParcelRunning)
        {
            return;
        }

        var plan = FabricMaintenanceReviewLoadPlanner.BuildPlan(Settings, CurrentTransactionNumber, ParcelInReview, SelectedTarget);
        if (!plan.IsValid)
        {
            StatusText = plan.Message;
            showMessage(plan.Message);
            return;
        }

        try
        {
            IsLoadParcelRunning = true;
            StatusText = "Loading Fabric Maintenance review context into ArcGIS Pro...";
            var result = await reviewLoadService.LoadAsync(plan).ConfigureAwait(true);
            ApplyReviewLoadResult(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"Fabric Maintenance review context could not be loaded: {exception.Message}";
            showMessage(StatusText);
        }
        finally
        {
            IsLoadParcelRunning = false;
        }
    }

    private void SelectDecision(FabricMaintenancePromotionDecision decision)
    {
        var state = BuildReviewState();
        var result = state.SelectDecision(decision);
        if (!result.IsExecutable)
        {
            showMessage(result.Message);
            SelectedDecision = FabricMaintenancePromotionDecision.None;
            StatusText = result.Message;
            return;
        }

        SelectedDecision = decision;
        StatusText = result.Message;
    }

    private void ApplyReviewLoadResult(FabricMaintenanceReviewLoadResult result)
    {
        WorkingFeatureCounts = result.WorkingFeatureCounts;
        CandidateCount = result.FinalCandidateCount;
        NotifyPropertyChanged(nameof(WorkingFeatureCounts));
        NotifyPropertyChanged(nameof(CandidateCount));
        NotifyPropertyChanged(nameof(ConfirmationSummary));
        ReviewResults.Clear();
        foreach (var row in result.ResultRows)
        {
            ReviewResults.Add(row);
        }

        FinalCandidates.Clear();
        foreach (var candidate in result.FinalCandidates)
        {
            FinalCandidates.Add(candidate);
        }

        SelectedFinalCandidate = FinalCandidates.Count == 1 ? FinalCandidates[0] : null;

        ReviewChecks.Clear();
        foreach (var check in result.TopologyChecks)
        {
            ReviewChecks.Add(check);
        }

        AttributeChecks.Clear();
        foreach (var check in result.AttributeChecks)
        {
            AttributeChecks.Add(check);
        }

        StatusText = result.Message;
        IsReviewLoaded = result.Success;
        RefreshReadiness();
    }

    private void InvalidateLoadedReview()
    {
        if (!IsReviewLoaded)
        {
            return;
        }

        IsReviewLoaded = false;
        FinalCandidates.Clear();
        SelectedFinalCandidate = null;
        StatusText = "Review context changed. Press Load Parcel to refresh the map and results.";
    }

    private async Task CancelAsync()
    {
        if (IsCancelRunning)
        {
            return;
        }

        try
        {
            IsCancelRunning = true;
            StatusText = "Closing Fabric Maintenance review and cleaning map layers...";
            var cleanup = await reviewLoadService.CleanupAsync(CurrentTransactionNumber).ConfigureAwait(true);
            StatusText = cleanup.Message;
            if (!cleanup.Success)
            {
                showMessage(cleanup.Message);
                return;
            }

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"Fabric Maintenance review cleanup could not be completed: {exception.Message}";
            showMessage(StatusText);
        }
        finally
        {
            IsCancelRunning = false;
        }
    }

    private void ResetEvidenceChecks(int workingParcelCount, int finalCandidateCount)
    {
        ReviewChecks.Clear();
        foreach (var check in FabricMaintenanceReviewEvidenceCatalog.TopologyChecks(workingParcelCount, finalCandidateCount))
        {
            ReviewChecks.Add(check);
        }

        AttributeChecks.Clear();
        foreach (var check in FabricMaintenanceReviewEvidenceCatalog.AttributeChecks())
        {
            AttributeChecks.Add(check);
        }
    }

    private void ApproveForFinalWrite()
    {
        FinalWriteApproved = true;
        NotifyPropertyChanged(nameof(FinalWriteApproved));
        NotifyPropertyChanged(nameof(CanConfirmFinalWrite));
        RaiseCommandState();
        StatusText = "Final write approved. Confirm final action on the Final Layer Write screen.";
    }

    private async Task ConfirmFinalWriteAsync()
    {
        if (IsConfirmFinalWriteRunning)
        {
            return;
        }

        if (!confirmAction($"Confirm final write for {ConfirmationSummary}"))
        {
            StatusText = "Fabric Maintenance final write confirmation cancelled.";
            return;
        }

        try
        {
            IsConfirmFinalWriteRunning = true;
            StatusText = "Confirming Fabric Maintenance final write and completing the Innola task...";
            var result = await finalWriteCompletionService.CompleteAsync(BuildReviewState()).ConfigureAwait(true);
            StatusText = result.Message;
            if (!result.Success)
            {
                showMessage(result.Message);
                return;
            }

            var cleanup = await reviewLoadService.CleanupAsync(CurrentTransactionNumber).ConfigureAwait(true);
            if (!cleanup.Success)
            {
                StatusText = cleanup.Message;
                showMessage(cleanup.Message);
                return;
            }

            StatusText = result.Message;
            showMessage(result.Message);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"Fabric Maintenance final write could not be completed: {exception.Message}";
            showMessage(StatusText);
        }
        finally
        {
            IsConfirmFinalWriteRunning = false;
        }
    }

    private void RefreshReadiness()
    {
        var review = BuildReviewState();
        readiness = FabricMaintenanceFinalWriteReadinessService.Evaluate(review);
        NotifyPropertyChanged(nameof(ReadinessText));
        NotifyPropertyChanged(nameof(CanApproveForFinalWrite));
        NotifyPropertyChanged(nameof(CanConfirmFinalWrite));
        NotifyPropertyChanged(nameof(ConfirmationSummary));
        RaiseCommandState();
    }

    private FabricMaintenanceReviewState BuildReviewState()
    {
        var state = FabricMaintenanceReviewState.Create(
            CurrentTransactionNumber,
            ParcelInReview,
            SelectedTarget,
            WorkingFeatureCounts,
            CandidateCount);
        state.SelectedCandidateId = SelectedCandidateId;
        state.DecisionNotes = DecisionNotes;
        if (SelectedDecision != FabricMaintenancePromotionDecision.None)
        {
            state.SelectDecision(SelectedDecision);
        }

        state.CheckResults.AddRange(ReviewChecks);
        state.CheckResults.AddRange(AttributeChecks);
        return state;
    }

    private void RaiseCommandState()
    {
        if (ApproveForFinalWriteCommand is RelayCommand approve)
        {
            approve.RaiseCanExecuteChanged();
        }

        if (ConfirmFinalWriteCommand is RelayCommand confirm)
        {
            confirm.RaiseCanExecuteChanged();
        }

        if (LoadParcelCommand is RelayCommand loadParcel)
        {
            loadParcel.RaiseCanExecuteChanged();
        }

        if (CancelCommand is RelayCommand cancel)
        {
            cancel.RaiseCanExecuteChanged();
        }
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
