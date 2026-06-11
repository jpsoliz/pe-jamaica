using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Innola;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow;
using System.Linq;
using System.Windows.Input;

namespace ParcelWorkflowAddIn;

internal sealed class ParcelWorkflowDockpaneViewModel : DockPane
{
    internal const string DockPaneId = "ParcelWorkflow_Dockpane";
    private readonly WorkflowSession workflowSession = new(new CaseFolderStore());
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
    private readonly RelayCommand startOrClaimTransactionCommand;
    private readonly RelayCommand saveProgressCommand;
    private readonly RelayCommand cancelProcessCommand;
    private readonly RelayCommand completeTransactionCommand;
    private string? outputLocation;
    private string? transactionId;

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
        runExtractionReviewCommand = new RelayCommand(PrepareExtractionReview, () => CanRunExtractionReview);
        startOrClaimTransactionCommand = new RelayCommand(async () => await StartOrClaimTransactionAsync(), () => ShellState.Session.CanStartOrClaimTransaction);
        saveProgressCommand = new RelayCommand(async () => await SaveProgressAsync(), () => ShellState.Session.CanSaveProgress);
        cancelProcessCommand = new RelayCommand(CancelProcess, () => ShellState.Session.CanCancelActiveProcess);
        completeTransactionCommand = new RelayCommand(async () => await CompleteTransactionAsync(), () => ShellState.Session.CanCompleteTransaction);
        ShellState.Session.SessionChanged += (_, _) => SyncLoadedCaseFolder();
        SyncLoadedCaseFolder();
    }

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
        ? "Transaction not selected"
        : TransactionId;

    public string HeaderTaskNameText
    {
        get
        {
            var taskName = ShellState.Session.SelectedTransaction?.TaskName;
            return string.IsNullOrWhiteSpace(taskName) ? "Task not selected" : taskName;
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

    private void PrepareExtractionReview()
    {
        workflowSession.SetValidationFailure("Extraction review is available after running Process.");
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
        workflowSession.SetValidationFailure(result.StatusMessage ?? result.ErrorMessage ?? "Current process cancelled locally.");
        RefreshWorkflowProperties();
    }

    public async Task CompleteTransactionAsync()
    {
        var result = await ShellState.LifecycleCoordinator.CompleteAsync();
        workflowSession.SetValidationFailure(result.StatusMessage ?? result.ErrorMessage ?? "Complete is blocked.");
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
            _ => "pending"
        };
    }

    private static string GetStepStateForExtractionReview(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.NoCase => "pending",
            WorkflowState.Intake => "pending",
            WorkflowState.PreflightRunning => "pending",
            WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed => "active",
            _ => "pending"
        };
    }

    private IReadOnlyList<WorkflowLifecycleStep> BuildWorkflowSteps()
    {
        var currentState = workflowSession.CurrentState;

        return new WorkflowLifecycleStep[]
        {
            new WorkflowLifecycleStep("Intake", GetStepStateForIntake(currentState)),
            new WorkflowLifecycleStep("Preflight", GetStepStateForPreflight(currentState)),
            new WorkflowLifecycleStep("Extraction Review", GetStepStateForExtractionReview(currentState)),
            new WorkflowLifecycleStep("Validation", currentState == WorkflowState.NoCase ? "pending" : "pending"),
            new WorkflowLifecycleStep("Outputs", currentState == WorkflowState.NoCase ? "pending" : "pending"),
            new WorkflowLifecycleStep("Sync Ready", currentState == WorkflowState.NoCase ? "pending" : "pending")
        };
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
        NotifyPropertyChanged(nameof(PreflightBadge));
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

internal sealed record WorkflowLifecycleStep(string Name, string State);

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
