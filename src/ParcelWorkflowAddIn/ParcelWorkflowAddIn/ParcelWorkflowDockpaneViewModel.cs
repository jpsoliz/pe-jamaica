using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Contracts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

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
    private readonly RelayCommand addManualPointCommand;
    private readonly RelayCommand saveReviewCommand;
    private readonly RelayCommand approveReviewCommand;
    private readonly RelayCommand togglePreflightDetailsCommand;
    private readonly RelayCommand toggleOutputPreviewCommand;
    private readonly RelayCommand toggleReviewDetailsCommand;
    private readonly RelayCommand openReviewSourceCommand;
    private readonly RelayCommand revealReviewSourceCommand;
    private readonly RelayCommand startOrClaimTransactionCommand;
    private readonly RelayCommand saveProgressCommand;
    private readonly RelayCommand cancelProcessCommand;
    private readonly RelayCommand completeTransactionCommand;
    private string? outputLocation;
    private string? transactionId;
    private ExtractionReviewDocument? loadedReviewDocument;
    private ExtractionReviewRowViewModel? selectedReviewRow;
    private bool preflightDetailsExpanded;
    private bool outputPreviewExpanded;
    private bool reviewDetailsExpanded = true;
    private bool reviewDirty;

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
        addManualPointCommand = new RelayCommand(AddManualPoint, () => HasLoadedReviewData && !IsReviewLocked);
        saveReviewCommand = new RelayCommand(SaveReviewChanges, () => HasLoadedReviewData && ReviewRows.Count > 0 && !IsReviewLocked);
        approveReviewCommand = new RelayCommand(ApproveReview, () => HasLoadedReviewData && ReviewRows.Count > 0 && !IsReviewLocked);
        togglePreflightDetailsCommand = new RelayCommand(TogglePreflightDetails, () => HasPreflightResults);
        toggleOutputPreviewCommand = new RelayCommand(ToggleOutputPreview);
        toggleReviewDetailsCommand = new RelayCommand(ToggleReviewDetails, () => HasLoadedReviewData);
        openReviewSourceCommand = new RelayCommand(OpenReviewSource, () => SelectedReviewSource is not null);
        revealReviewSourceCommand = new RelayCommand(RevealReviewSource, () => SelectedReviewSource is not null);
        startOrClaimTransactionCommand = new RelayCommand(async () => await StartOrClaimTransactionAsync(), () => ShellState.Session.CanStartOrClaimTransaction);
        saveProgressCommand = new RelayCommand(async () => await SaveProgressAsync(), () => ShellState.Session.CanSaveProgress);
        cancelProcessCommand = new RelayCommand(CancelProcess, () => ShellState.Session.CanCancelActiveProcess);
        completeTransactionCommand = new RelayCommand(async () => await CompleteTransactionAsync(), () => ShellState.Session.CanCompleteTransaction);
        ShellState.Session.SessionChanged += (_, _) => SyncLoadedCaseFolder();
        SyncLoadedCaseFolder();
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
        ? "Transaction #: not selected"
        : $"Transaction #: {TransactionId}";

    public string HeaderTaskNameText
    {
        get
        {
            var transactionType = ResolveSelectedTransactionType();
            return string.IsNullOrWhiteSpace(transactionType) ? "Type: not available" : $"Type: {transactionType}";
        }
    }

    public string CurrentStepBadge => CurrentStep;

    public string ScoreBadge => HasPreflightIssues ? $"{PreflightBlockers.Count + PreflightWarnings.Count} issue(s)" : "Score pending";

    public string ModeBadge => "Local v1";

    public bool CanAddSourceFiles => false;

    public IReadOnlyList<SourceFileListItem> SourceFiles =>
        workflowSession.SourceFiles.Select(sourceFile => new SourceFileListItem(sourceFile)).ToArray();

    public IReadOnlyList<WorkflowLifecycleStep> WorkflowSteps => BuildWorkflowSteps();

    public bool CanUseWorkflowActions => ShellState.Session.CanOpenParcelWorkflow;

    public bool CanRunPreflight => CanUseWorkflowActions && workflowSession.CanRunPreflight && !workflowSession.IsPreflightRunning;

    public bool CanRunExtractionReview => CanUseWorkflowActions && workflowSession.CanRunExtractionReview;

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

    public ICommand AddManualPointCommand => addManualPointCommand;

    public ICommand SaveReviewCommand => saveReviewCommand;

    public ICommand ApproveReviewCommand => approveReviewCommand;

    public ICommand TogglePreflightDetailsCommand => togglePreflightDetailsCommand;

    public ICommand ToggleOutputPreviewCommand => toggleOutputPreviewCommand;

    public ICommand ToggleReviewDetailsCommand => toggleReviewDetailsCommand;

    public ICommand OpenReviewSourceCommand => openReviewSourceCommand;

    public ICommand RevealReviewSourceCommand => revealReviewSourceCommand;

    public ICommand StartOrClaimTransactionCommand => startOrClaimTransactionCommand;

    public ICommand SaveProgressCommand => saveProgressCommand;

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

    public string PreflightSummaryText
    {
        get
        {
            if (!HasPreflightResults)
            {
                return "No preflight results yet.";
            }

            return $"{PreflightBlockers.Count} blocker(s), {PreflightWarnings.Count} warning(s), {PreflightPassedChecks.Count} passed.";
        }
    }

    public string PreflightCollapsedHint =>
        PreflightBlockers.Count > 0
            ? $"Blocking now: {PreflightBlockers[0].Message}"
            : PreflightWarnings.Count > 0
                ? $"Attention: {PreflightWarnings[0].Message}"
                : "All current preflight checks passed.";

    public string ExtractionReviewBadge =>
        workflowSession.CurrentState switch
        {
            WorkflowState.ExtractionRunning => "Processing",
            WorkflowState.ExtractionFailed => "Blocked",
            WorkflowState.ReviewApproved => "Approved",
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
            WorkflowState.ReviewPending when HasExtractionReviewArtifact(workflowSession) => "Draft review data is ready to inspect and correct before parcel build.",
            WorkflowState.PreflightPassed => "Extraction review will generate draft review data from the selected transaction files.",
            WorkflowState.PreflightBlocked => "Extraction review is unavailable until preflight blockers are resolved.",
            _ => "Extraction review is enabled after Process completes."
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

    public SourceFileListItem? SelectedReviewSource => ResolveReviewSource();

    public string ReviewWorkspaceTitle => "Source Verification Workspace";

    public string SelectedReviewSourceTitle => SelectedReviewSource is null
        ? "Source document not resolved"
        : $"{SelectedReviewSource.RoleLabel}: {SelectedReviewSource.FileLabel}";

    public string SelectedReviewSourcePath => SelectedReviewSource?.SourceRelativePath ?? "No point-bearing source document is available.";

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

    public bool IsReviewApproved => workflowSession.CurrentState == WorkflowState.ReviewApproved;

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
                : workflowSession.CurrentState == WorkflowState.ReviewApproved
                    ? "Approved"
                    : "Loaded";

    public bool OutputPreviewExpanded => outputPreviewExpanded;

    public string OutputPreviewToggleText => OutputPreviewExpanded ? "Hide preview" : "Show preview";

    public string OutputPreviewSummaryText =>
        AvailableArtifacts.Count == 0
            ? "No generated output package yet."
            : $"{AvailableArtifacts.Count} case artifact(s) are available.";

    public bool HasOutputArtifacts => AvailableArtifacts.Count > 0;

    public string OutputPreviewBodyText =>
        HasOutputArtifacts
            ? "Generated case artifacts are available for inspection before final transaction completion."
            : "This is a planned output-stage workspace. It will preview generated deliverables such as geometry outputs, validation summaries, and package metadata. In the current implementation, review approval is the last completed workflow step before transaction-level Save or Approve.";

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
        if (string.IsNullOrWhiteSpace(workflowSession.CaseFolderPath))
        {
            workflowSession.SetValidationFailure("Create or reopen a Case Folder before opening extraction review.");
            RefreshWorkflowProperties();
            return;
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
                return;
            }

            artifactPath = SelectExtractionReviewArtifact(layout);
            RefreshWorkflowProperties();
            if (artifactPath is null)
            {
                workflowSession.SetValidationFailure("Draft extraction completed but no extraction review artifact was found.");
                RefreshWorkflowProperties();
                return;
            }
        }

        var reviewDocument = workflowSession.LoadExtractionReview();
        if (reviewDocument is null)
        {
            RefreshWorkflowProperties();
            return;
        }

        LoadReviewDocumentIntoPane(reviewDocument);
        workflowSession.SetValidationFailure($"Extraction review loaded from {Path.GetFileName(artifactPath)}.");

        RefreshWorkflowProperties();
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

    private void ToggleReviewDetails()
    {
        reviewDetailsExpanded = !reviewDetailsExpanded;
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

        return SourceFiles.FirstOrDefault(item => string.Equals(item.SourceFile.SourceRole, "computation_source", StringComparison.OrdinalIgnoreCase))
            ?? SourceFiles.FirstOrDefault(item => string.Equals(item.SourceFile.SourceRole, "points_computation", StringComparison.OrdinalIgnoreCase))
            ?? SourceFiles.FirstOrDefault(item => string.Equals(item.SourceFile.SourceRole, "plan_map_reference", StringComparison.OrdinalIgnoreCase))
            ?? SourceFiles.FirstOrDefault();
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
        var result = ShellState.LifecycleCoordinator.CancelActiveProcess();
        if (result.Success)
        {
            ResetWorkflowView(result.StatusMessage ?? "Current process cancelled locally.");
            return;
        }

        workflowSession.SetValidationFailure(result.ErrorMessage ?? "Could not cancel the current process.");
        RefreshWorkflowProperties();
    }

    public async Task CompleteTransactionAsync()
    {
        var result = await ShellState.LifecycleCoordinator.CompleteAsync();
        if (result.Success)
        {
            ResetWorkflowView(result.StatusMessage ?? "Transaction completed.");
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

    private static string GetStepStateForPreflight(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.NoCase => "pending",
            WorkflowState.Intake => "active",
            WorkflowState.PreflightRunning => "active",
            WorkflowState.PreflightBlocked => "blocked",
            WorkflowState.PreflightPassed => "done",
            WorkflowState.ExtractionRunning => "done",
            WorkflowState.ExtractionFailed => "done",
            WorkflowState.ReviewPending => "done",
            WorkflowState.ReviewApproved => "done",
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
            WorkflowState.PreflightPassed when hasReviewArtifact => "done",
            WorkflowState.PreflightPassed => "active",
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
        var extractionState = GetStepStateForExtractionReview(currentState, HasExtractionReviewArtifact(workflowSession));
        var validationState = currentState == WorkflowState.ReviewApproved ? "done" : "pending";

        return new WorkflowLifecycleStep[]
        {
            new WorkflowLifecycleStep("Intake", GetStepStateForIntake(currentState), GetLifecycleStepIcon(GetStepStateForIntake(currentState))),
            new WorkflowLifecycleStep("Preflight", GetStepStateForPreflight(currentState), GetLifecycleStepIcon(GetStepStateForPreflight(currentState))),
            new WorkflowLifecycleStep("Extraction Review", extractionState, GetLifecycleStepIcon(extractionState)),
            new WorkflowLifecycleStep("Validation", validationState, GetLifecycleStepIcon(validationState)),
            new WorkflowLifecycleStep("Outputs", "pending", GetLifecycleStepIcon("pending")),
            new WorkflowLifecycleStep("Ready to Complete", "pending", GetLifecycleStepIcon("pending"))
        };
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

        return TransactionId is null ? "not available" : null;
    }

    private void RefreshWorkflowProperties()
    {
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
        NotifyPropertyChanged(nameof(SourceFiles));
        NotifyPropertyChanged(nameof(SourceIntakeBadge));
        NotifyPropertyChanged(nameof(ExtractionReviewBadge));
        NotifyPropertyChanged(nameof(ExtractionReviewActionLabel));
        NotifyPropertyChanged(nameof(ExtractionReviewHelpText));
        NotifyPropertyChanged(nameof(HasLoadedReviewData));
        NotifyPropertyChanged(nameof(ReviewSummary));
        NotifyPropertyChanged(nameof(ReviewSummaryText));
        NotifyPropertyChanged(nameof(ReviewHasBlockers));
        NotifyPropertyChanged(nameof(ReviewGateText));
        NotifyPropertyChanged(nameof(ReviewBadgeText));
        NotifyPropertyChanged(nameof(IsReviewApproved));
        NotifyPropertyChanged(nameof(IsReviewLocked));
        NotifyPropertyChanged(nameof(ReviewDetailsExpanded));
        NotifyPropertyChanged(nameof(ReviewDetailsToggleText));
        NotifyPropertyChanged(nameof(CanUseWorkflowActions));
        NotifyPropertyChanged(nameof(CanRunPreflight));
        NotifyPropertyChanged(nameof(CanRunExtractionReview));
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
        NotifyPropertyChanged(nameof(SelectedReviewRow));
        NotifyPropertyChanged(nameof(SelectedReviewSource));
        NotifyPropertyChanged(nameof(SelectedReviewSourceTitle));
        NotifyPropertyChanged(nameof(SelectedReviewSourcePath));
        NotifyPropertyChanged(nameof(SelectedReviewSourceMode));
        NotifyPropertyChanged(nameof(SelectedReviewSourceGuidance));
        NotifyPropertyChanged(nameof(SelectedReviewRowDetailsTitle));
        NotifyPropertyChanged(nameof(SelectedReviewRowDetailsText));
        NotifyPropertyChanged(nameof(ReviewWorkspaceTitle));
        NotifyPropertyChanged(nameof(OutputPreviewExpanded));
        NotifyPropertyChanged(nameof(OutputPreviewToggleText));
        NotifyPropertyChanged(nameof(OutputPreviewSummaryText));
        NotifyPropertyChanged(nameof(HasOutputArtifacts));
        NotifyPropertyChanged(nameof(OutputPreviewBodyText));
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
        addManualPointCommand.RaiseCanExecuteChanged();
        saveReviewCommand.RaiseCanExecuteChanged();
        approveReviewCommand.RaiseCanExecuteChanged();
        togglePreflightDetailsCommand.RaiseCanExecuteChanged();
        toggleOutputPreviewCommand.RaiseCanExecuteChanged();
        toggleReviewDetailsCommand.RaiseCanExecuteChanged();
        openReviewSourceCommand.RaiseCanExecuteChanged();
        revealReviewSourceCommand.RaiseCanExecuteChanged();
        startOrClaimTransactionCommand.RaiseCanExecuteChanged();
        saveProgressCommand.RaiseCanExecuteChanged();
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
        reviewDirty = false;
        ReviewRows.Clear();
        RefreshWorkflowProperties();
    }

    private static string BlankIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
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
