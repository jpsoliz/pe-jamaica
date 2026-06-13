using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Execution;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.Validation;
using ParcelWorkflowAddIn.WorkflowRules;
using System.IO;
using System.Text.Json;

namespace ParcelWorkflowAddIn.Workflow;

public sealed class WorkflowSession
{
    private readonly CaseFolderStore caseFolderStore;
    private readonly SourceFileCopyService sourceFileCopyService;
    private readonly SourceInputProfileDetector sourceInputProfileDetector;
    private readonly SourceFileActionService sourceFileActionService;
    private readonly SourceFileActionAuditService sourceFileActionAuditService;
    private readonly ManifestPreflightService manifestPreflightService;
    private readonly WorkflowRuleResolver workflowRuleResolver;
    private readonly Func<WorkflowRuleSettings> getWorkflowRuleSettings;
    private readonly IWorkflowScriptExecutor workflowScriptExecutor;
    private readonly ExtractionReviewPersistenceService extractionReviewService;
    private readonly IValidationExecutionService validationExecutionService;
    private readonly ValidationSummaryPersistenceService validationSummaryPersistenceService;
    private readonly IOutputExecutionService outputExecutionService;
    private readonly OutputSummaryPersistenceService outputSummaryPersistenceService;
    private readonly List<SourceFileCopyResult> sourceFiles = [];
    private readonly List<string> intakeIssues = [];
    private readonly List<AvailableArtifact> availableArtifacts = [];
    private readonly List<PreflightCheck> preflightBlockers = [];
    private readonly List<PreflightCheck> preflightWarnings = [];
    private readonly List<PreflightCheck> preflightPassedChecks = [];
    private bool preflightRunActive;
    private bool extractionRunActive;
    private bool validationRunActive;
    private bool outputRunActive;
    private ValidationSummaryDocument? currentValidationSummary;
    private OutputSummaryDocument? currentOutputSummary;

    public WorkflowSession(CaseFolderStore caseFolderStore)
        : this(caseFolderStore, new SourceFileCopyService(), new SourceInputProfileDetector())
    {
    }

    public WorkflowSession(CaseFolderStore caseFolderStore, SourceFileCopyService sourceFileCopyService)
        : this(caseFolderStore, sourceFileCopyService, new SourceInputProfileDetector())
    {
    }

    public WorkflowSession(CaseFolderStore caseFolderStore, SourceFileCopyService sourceFileCopyService, SourceInputProfileDetector sourceInputProfileDetector)
        : this(caseFolderStore, sourceFileCopyService, sourceInputProfileDetector, new SourceFileActionService(), new SourceFileActionAuditService())
    {
    }

    public WorkflowSession(
        CaseFolderStore caseFolderStore,
        SourceFileCopyService sourceFileCopyService,
        SourceInputProfileDetector sourceInputProfileDetector,
        SourceFileActionService sourceFileActionService,
        SourceFileActionAuditService sourceFileActionAuditService)
        : this(caseFolderStore, sourceFileCopyService, sourceInputProfileDetector, sourceFileActionService, sourceFileActionAuditService, ManifestPreflightService.CreateDefault())
    {
    }

    public WorkflowSession(
        CaseFolderStore caseFolderStore,
        SourceFileCopyService sourceFileCopyService,
        SourceInputProfileDetector sourceInputProfileDetector,
        SourceFileActionService sourceFileActionService,
        SourceFileActionAuditService sourceFileActionAuditService,
        ManifestPreflightService manifestPreflightService)
        : this(
            caseFolderStore,
            sourceFileCopyService,
            sourceInputProfileDetector,
            sourceFileActionService,
            sourceFileActionAuditService,
            manifestPreflightService,
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new WorkflowScriptExecutor(),
            new ExtractionReviewPersistenceService(),
            new ValidationAdapterExecutionService(),
            new ValidationSummaryPersistenceService(),
            new OutputAdapterExecutionService(),
            new OutputSummaryPersistenceService())
    {
    }

    public WorkflowSession(
        CaseFolderStore caseFolderStore,
        SourceFileCopyService sourceFileCopyService,
        SourceInputProfileDetector sourceInputProfileDetector,
        SourceFileActionService sourceFileActionService,
        SourceFileActionAuditService sourceFileActionAuditService,
        ManifestPreflightService manifestPreflightService,
        WorkflowRuleResolver workflowRuleResolver,
        Func<WorkflowRuleSettings> getWorkflowRuleSettings,
        IWorkflowScriptExecutor workflowScriptExecutor)
        : this(
            caseFolderStore,
            sourceFileCopyService,
            sourceInputProfileDetector,
            sourceFileActionService,
            sourceFileActionAuditService,
            manifestPreflightService,
            workflowRuleResolver,
            getWorkflowRuleSettings,
            workflowScriptExecutor,
            new ExtractionReviewPersistenceService(),
            new ValidationAdapterExecutionService(),
            new ValidationSummaryPersistenceService(),
            new OutputAdapterExecutionService(),
            new OutputSummaryPersistenceService())
    {
    }

    public WorkflowSession(
        CaseFolderStore caseFolderStore,
        SourceFileCopyService sourceFileCopyService,
        SourceInputProfileDetector sourceInputProfileDetector,
        SourceFileActionService sourceFileActionService,
        SourceFileActionAuditService sourceFileActionAuditService,
        ManifestPreflightService manifestPreflightService,
        WorkflowRuleResolver workflowRuleResolver,
        Func<WorkflowRuleSettings> getWorkflowRuleSettings,
        IWorkflowScriptExecutor workflowScriptExecutor,
        ExtractionReviewPersistenceService extractionReviewService,
        IValidationExecutionService validationExecutionService,
        ValidationSummaryPersistenceService validationSummaryPersistenceService)
        : this(
            caseFolderStore,
            sourceFileCopyService,
            sourceInputProfileDetector,
            sourceFileActionService,
            sourceFileActionAuditService,
            manifestPreflightService,
            workflowRuleResolver,
            getWorkflowRuleSettings,
            workflowScriptExecutor,
            extractionReviewService,
            validationExecutionService,
            validationSummaryPersistenceService,
            new OutputAdapterExecutionService(),
            new OutputSummaryPersistenceService())
    {
    }

    public WorkflowSession(
        CaseFolderStore caseFolderStore,
        SourceFileCopyService sourceFileCopyService,
        SourceInputProfileDetector sourceInputProfileDetector,
        SourceFileActionService sourceFileActionService,
        SourceFileActionAuditService sourceFileActionAuditService,
        ManifestPreflightService manifestPreflightService,
        WorkflowRuleResolver workflowRuleResolver,
        Func<WorkflowRuleSettings> getWorkflowRuleSettings,
        IWorkflowScriptExecutor workflowScriptExecutor,
        ExtractionReviewPersistenceService extractionReviewService,
        IValidationExecutionService validationExecutionService,
        ValidationSummaryPersistenceService validationSummaryPersistenceService,
        IOutputExecutionService outputExecutionService,
        OutputSummaryPersistenceService outputSummaryPersistenceService)
    {
        this.caseFolderStore = caseFolderStore;
        this.sourceFileCopyService = sourceFileCopyService;
        this.sourceInputProfileDetector = sourceInputProfileDetector;
        this.sourceFileActionService = sourceFileActionService;
        this.sourceFileActionAuditService = sourceFileActionAuditService;
        this.manifestPreflightService = manifestPreflightService;
        this.workflowRuleResolver = workflowRuleResolver;
        this.getWorkflowRuleSettings = getWorkflowRuleSettings;
        this.workflowScriptExecutor = workflowScriptExecutor;
        this.extractionReviewService = extractionReviewService;
        this.validationExecutionService = validationExecutionService;
        this.validationSummaryPersistenceService = validationSummaryPersistenceService;
        this.outputExecutionService = outputExecutionService;
        this.outputSummaryPersistenceService = outputSummaryPersistenceService;
    }

    public WorkflowState CurrentState { get; private set; } = WorkflowState.NoCase;

    public string? TransactionId { get; private set; }

    public string? CaseFolderPath { get; private set; }

    public string CurrentStep => CurrentState.ToDisplayName();

    public string StatusText { get; private set; } = "No active case";

    public IReadOnlyList<SourceFileCopyResult> SourceFiles => sourceFiles;

    public string DetectedProfileLabel { get; private set; } = "Detected profile: not refreshed";

    public IReadOnlyList<string> IntakeIssues => intakeIssues;

    public IReadOnlyList<AvailableArtifact> AvailableArtifacts => availableArtifacts;

    public IReadOnlyList<PreflightCheck> PreflightBlockers => preflightBlockers;

    public IReadOnlyList<PreflightCheck> PreflightWarnings => preflightWarnings;

    public IReadOnlyList<PreflightCheck> PreflightPassedChecks => preflightPassedChecks;

    public bool IsPreflightRunning => preflightRunActive;

    public bool IsExtractionRunning => extractionRunActive;

    public bool IsValidationRunning => validationRunActive;

    public bool IsOutputRunning => outputRunActive;

    public bool HasPreflightResult => CurrentState is WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed or WorkflowState.ExtractionRunning or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending or WorkflowState.ReviewApproved or WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed or WorkflowState.OutputRunning or WorkflowState.OutputCreated;

    public bool CanRunPreflight => CanRunPreflightState(CurrentState);

    public bool CanRunExtractionReview => !extractionRunActive && CanRunExtractionReviewState(CurrentState);

    public bool CanRunValidation => !validationRunActive && CanRunValidationState(CurrentState);

    public bool CanRunOutputs => !outputRunActive && CanRunOutputState(CurrentState);

    public ValidationSummaryDocument? CurrentValidationSummary => currentValidationSummary;

    public OutputSummaryDocument? CurrentOutputSummary => currentOutputSummary;

    public void ResetToDefault(string statusText = "No active case")
    {
        CurrentState = WorkflowState.NoCase;
        TransactionId = null;
        CaseFolderPath = null;
        sourceFiles.Clear();
        intakeIssues.Clear();
        availableArtifacts.Clear();
        ClearPreflightResults();
        DetectedProfileLabel = "Detected profile: not refreshed";
        StatusText = statusText;
        preflightRunActive = false;
        extractionRunActive = false;
        validationRunActive = false;
        outputRunActive = false;
        currentValidationSummary = null;
        currentOutputSummary = null;
    }

    public CaseFolderCreationResult CreateCase(string transactionId, string outputRoot, string? createdBy)
    {
        var result = caseFolderStore.CreateCase(outputRoot, transactionId, createdBy);
        if (!result.Success)
        {
            StatusText = result.ErrorMessage ?? "Case creation failed";
            return result;
        }

        CurrentState = WorkflowState.Intake;
        TransactionId = transactionId;
        CaseFolderPath = result.Layout!.RootDirectory;
        sourceFiles.Clear();
        intakeIssues.Clear();
        availableArtifacts.Clear();
        ClearPreflightResults();
        DetectedProfileLabel = "Detected profile: not refreshed";
        StatusText = "Case created";
        return result;
    }

    public void SetValidationFailure(string statusText)
    {
        StatusText = statusText;
    }

    public SourceFileCopyBatchResult AddSourceFiles(IReadOnlyList<string> sourcePaths, string? sourceRole = null)
    {
        if (!CanRunIntakeCommand() || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = "Create or reopen a Case Folder before adding source files.";
            return new SourceFileCopyBatchResult(Array.Empty<SourceFileCopyResult>());
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var result = sourceFileCopyService.CopySourceFiles(layout, sourcePaths, sourceRole);
        sourceFiles.AddRange(result.Results);

        var copiedCount = result.Results.Count(sourceFile => sourceFile.Copied);
        if (copiedCount > 0)
        {
            InvalidatePreflight(layout);
        }

        StatusText = copiedCount == 1
            ? "Copied 1 source file to Case Folder source area."
            : $"Copied {copiedCount} source files to Case Folder source area.";

        if (!result.Success && copiedCount == 0)
        {
            StatusText = result.Results.FirstOrDefault()?.Message ?? "No source files copied.";
        }

        return result;
    }

    public DetectedSourceInputProfile RefreshInputProfile()
    {
        if (!CanRunIntakeCommand() || string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Create or reopen a Case Folder before refreshing intake.";
            var failed = new DetectedSourceInputProfile(
                SourceInputProfile.IncompleteIntake,
                SourceInputProfile.IncompleteIntakeLabel,
                "incomplete",
                DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
                Array.Empty<string>(),
                new[] { StatusText });
            return failed;
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        var profile = sourceInputProfileDetector.Detect(manifest.Payload.SourceFiles);
        var ruleResolution = ResolveWorkflowRule(manifest, profile);
        var updatedManifest = manifest with
        {
            Payload = manifest.Payload with
            {
                DetectedProfile = profile,
                WorkflowProfile = ruleResolution?.ScriptPlan?.WorkflowProfile,
                WorkflowRuleId = ruleResolution?.ScriptPlan?.RuleId,
                WorkflowRuleVersion = ruleResolution?.ScriptPlan?.RuleVersion,
                ScriptPlan = ruleResolution?.ScriptPlan
            }
        };
        ManifestSerializer.Write(layout.ManifestPath, updatedManifest);
        InvalidatePreflight(layout);

        DetectedProfileLabel = profile.DisplayLabel;
        intakeIssues.Clear();
        intakeIssues.AddRange(profile.Issues);
        if (ruleResolution is { Success: false } && !string.IsNullOrWhiteSpace(ruleResolution.ErrorMessage))
        {
            intakeIssues.Add(ruleResolution.ErrorMessage);
        }

        StatusText = profile.Status == "matched"
            ? $"Detected profile: {profile.DisplayLabel}."
            : profile.DisplayLabel;

        return profile;
    }

    private WorkflowRuleResolutionResult? ResolveWorkflowRule(ManifestDocument manifest, DetectedSourceInputProfile profile)
    {
        if (manifest.Payload.InnolaTransaction is null)
        {
            return null;
        }

        return workflowRuleResolver.Resolve(new WorkflowRuleResolutionContext(
            manifest.Payload.InnolaTransaction.CaseType,
            manifest.Payload.InnolaTransaction.ProcessStep,
            profile,
            manifest.Payload.SourceFiles,
            getWorkflowRuleSettings()));
    }

    public CaseFolderReopenResult ReopenCaseFolder(string caseFolderPath)
    {
        var result = caseFolderStore.ReopenCaseFolder(caseFolderPath);
        if (!result.Success)
        {
            StatusText = "Case could not be reopened.";
            intakeIssues.Clear();
            intakeIssues.AddRange(result.RecoverabilityIssues.Select(issue => issue.Message));
            return result;
        }

        CurrentState = result.ResolvedState;
        TransactionId = result.Manifest!.TransactionId;
        CaseFolderPath = result.Layout!.RootDirectory;
        sourceFiles.Clear();
        sourceFiles.AddRange(result.SourceFiles);
        availableArtifacts.Clear();
        availableArtifacts.AddRange(result.AvailableArtifacts);
        intakeIssues.Clear();
        intakeIssues.AddRange(result.RecoverabilityIssues.Select(issue => issue.Message));
        DetectedProfileLabel = result.Manifest.Payload.DetectedProfile?.DisplayLabel ?? "Detected profile: not refreshed";
        LoadPreflightResults(result.Layout);
        currentValidationSummary = LoadValidationSummary(result.Layout, result.ResolvedState);
        currentOutputSummary = LoadOutputSummary(result.Layout, result.ResolvedState);
        StatusText = result.RecoverabilityIssues.Count == 0
            ? "Case reopened"
            : "Case reopened with recoverability issues.";

        return result;
    }

    public SourceFileActionResult ExecuteSourceFileAction(SourceFileCopyResult sourceFile, SourceFileAction action, string? operatorId)
    {
        if (!CanUseSourceFileActions() || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = "Create or reopen a Case Folder before using source file actions.";
            return SourceFileActionResult.Failed(action, sourceFile.CopiedPath, "blocked", StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var result = sourceFileActionService.Execute(layout, sourceFile, action);
        StatusText = result.Message;
        sourceFileActionAuditService.Record(layout, TransactionId, sourceFile, result, operatorId);
        return result;
    }

    public PreflightSummaryDocument RunManifestPreflight(string? operatorId)
    {
        return RunManifestPreflightAsync(operatorId).GetAwaiter().GetResult();
    }

    public Task<WorkflowScriptExecutionResult> RunDraftExtractionAsync(CancellationToken cancellationToken = default)
    {
        return RunDraftExtractionInternalAsync(cancellationToken);
    }

    public async Task<PreflightSummaryDocument> RunManifestPreflightAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (preflightRunActive)
        {
            StatusText = "Preflight is already running.";
            return new PreflightSummaryDocument(
                "1.0.0",
                TransactionId ?? string.Empty,
                "not-run",
                DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
                operatorId,
                string.Empty,
                new PreflightSummaryPayload("blocked", Array.Empty<PreflightCheck>(), Array.Empty<PreflightCheck>(), Array.Empty<PreflightCheck>()),
                Array.Empty<string>(),
                new[] { StatusText });
        }

        if (!CanRunPreflightStateInternal() || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = "Create or reopen a Case Folder before running preflight.";
            var failedCheck = PreflightCheck.Blocker(
                "active_case_required",
                StatusText,
                null,
                null,
                "Create or reopen a Case Folder.");
            ClearPreflightResults();
            preflightBlockers.Add(failedCheck);
            return new PreflightSummaryDocument(
                "1.0.0",
                TransactionId ?? string.Empty,
                "not-run",
                DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
                operatorId,
                string.Empty,
                new PreflightSummaryPayload("blocked", preflightBlockers.ToArray(), Array.Empty<PreflightCheck>(), Array.Empty<PreflightCheck>()),
                Array.Empty<string>(),
                new[] { StatusText });
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        CurrentState = WorkflowState.PreflightRunning;
        StatusText = "Preflight running: environment checks.";
        preflightRunActive = true;

        try
        {
            SetWorkflowState(layout, WorkflowState.PreflightRunning);
            var summary = await manifestPreflightService.RunAsync(layout, operatorId, cancellationToken).ConfigureAwait(false);
            ClearPreflightResults();
            preflightBlockers.AddRange(summary.Payload.Blockers);
            preflightWarnings.AddRange(summary.Payload.Warnings);
            preflightPassedChecks.AddRange(summary.Payload.PassedChecks);

            var finalState = summary.Payload.Blockers.Count > 0
                ? WorkflowState.PreflightBlocked
                : WorkflowState.PreflightPassed;
            SetWorkflowState(layout, finalState);
            UpsertAvailableArtifact(new AvailableArtifact("preflight_summary.json", layout.PreflightSummaryPath));

            StatusText = finalState == WorkflowState.PreflightBlocked
                ? $"Preflight blocked: {summary.Payload.Blockers[0].Message.ToLowerInvariant()}"
                : "Preflight passed: manifest and environment checks complete.";

            return summary;
        }
        catch (Exception exception) when (IsExpectedPreflightIoFailure(exception))
        {
            return BlockManifestPreflightFailure(layout, operatorId, exception);
        }
        finally
        {
            preflightRunActive = false;
        }
    }

    private bool CanRunIntakeCommand()
    {
        return CurrentState is WorkflowState.Intake or WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed;
    }

    private bool CanUseSourceFileActions()
    {
        return CurrentState is not WorkflowState.NoCase;
    }

    private bool CanRunPreflightStateInternal()
    {
        return !preflightRunActive && CanRunPreflightState(CurrentState);
    }

    private static bool CanRunPreflightState(WorkflowState state)
    {
        return state is WorkflowState.Intake or WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed;
    }

    private static bool CanRunExtractionReviewState(WorkflowState state)
    {
        return state is WorkflowState.PreflightPassed or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending;
    }

    private static bool CanRunValidationState(WorkflowState state)
    {
        return state is WorkflowState.ReviewApproved or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed;
    }

    private static bool CanRunOutputState(WorkflowState state)
    {
        return state is WorkflowState.ValidationPassed;
    }

    public ExtractionReviewDocument? LoadExtractionReview()
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Create or reopen a Case Folder before opening extraction review.";
            return null;
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var document = extractionReviewService.Load(layout);
        if (document is null)
        {
            StatusText = "Extraction review data is not available yet.";
            return null;
        }

        if (CurrentState == WorkflowState.PreflightPassed || CurrentState == WorkflowState.ExtractionFailed)
        {
            SetWorkflowState(layout, WorkflowState.ReviewPending);
        }

        var summary = extractionReviewService.Summarize(document);
        StatusText = summary.CanApprove
            ? $"Review loaded: {summary.TotalRows} row(s), ready for save or approval."
            : $"Review loaded: {summary.UnresolvedRows} unresolved and {summary.MissingRequiredRows} missing-value row(s) need attention.";
        return document;
    }

    public ExtractionReviewSaveResult SaveExtractionReview(ExtractionReviewDocument document, string? operatorId)
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            return ExtractionReviewSaveResult.Failed("Create or reopen a Case Folder before saving review data.");
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var saveResult = extractionReviewService.Save(layout, document, operatorId);
        if (!saveResult.Success || saveResult.Document is null)
        {
            StatusText = saveResult.Message;
            return saveResult;
        }

        RemoveValidationArtifacts(layout);
        RemoveOutputArtifacts(layout);
        UpsertAvailableArtifact(new AvailableArtifact("extraction_review_data.json", Path.Combine(layout.WorkingDirectory, "extraction_review_data.json")));
        availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, Path.Combine(layout.WorkingDirectory, "approved_review.json"), StringComparison.OrdinalIgnoreCase) && !File.Exists(artifact.Path));
        SetWorkflowState(layout, WorkflowState.ReviewPending);
        StatusText = saveResult.Message;
        return saveResult;
    }

    public ExtractionReviewApprovalResult ApproveExtractionReview(ExtractionReviewDocument document, string? operatorId)
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            return ExtractionReviewApprovalResult.Failed("Create or reopen a Case Folder before approving review data.", null);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var approvalResult = extractionReviewService.Approve(layout, document, operatorId);
        StatusText = approvalResult.Message;
        if (!approvalResult.Success)
        {
            return approvalResult;
        }

        if (!string.IsNullOrWhiteSpace(approvalResult.ApprovedReviewPath))
        {
            UpsertAvailableArtifact(new AvailableArtifact("approved_review.json", approvalResult.ApprovedReviewPath));
        }

        RemoveValidationArtifacts(layout);
        RemoveOutputArtifacts(layout);
        SetWorkflowState(layout, WorkflowState.ReviewApproved);
        return approvalResult;
    }

    public async Task<ValidationExecutionResult> RunValidationAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (validationRunActive)
        {
            StatusText = "Validation is already running.";
            return ValidationExecutionResult.Failed(StatusText);
        }

        if (!CanRunValidationState(CurrentState) || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = "Approve the current review before starting validation.";
            return ValidationExecutionResult.Failed(StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        ManifestDocument manifest;
        try
        {
            manifest = ManifestSerializer.Read(layout.ManifestPath);
        }
        catch (Exception exception) when (IsExpectedPreflightIoFailure(exception))
        {
            StatusText = "Validation could not start because the manifest could not be read.";
            return ValidationExecutionResult.Failed(StatusText);
        }

        var staleApprovalError = EnsureCurrentApproval(layout);
        if (!string.IsNullOrWhiteSpace(staleApprovalError))
        {
            SetWorkflowState(layout, WorkflowState.ReviewPending);
            StatusText = staleApprovalError;
            return ValidationExecutionResult.Failed(staleApprovalError);
        }

        validationRunActive = true;
        currentValidationSummary = null;
        SetWorkflowState(layout, WorkflowState.ValidationRunning);
        StatusText = "Validation running: evaluating approved review data.";

        try
        {
            var result = await validationExecutionService.RunAsync(layout, manifest, operatorId, cancellationToken).ConfigureAwait(false);
            if (!result.Success || result.Summary is null || string.IsNullOrWhiteSpace(result.SummaryPath))
            {
                SetWorkflowState(layout, WorkflowState.ValidationBlocked);
                StatusText = result.ErrorMessage ?? "Validation failed.";
                return result.Success ? ValidationExecutionResult.Failed(StatusText) : result;
            }

            currentValidationSummary = result.Summary;
            UpsertAvailableArtifact(new AvailableArtifact("validation_summary.json", result.SummaryPath));
            var blocked = validationSummaryPersistenceService.IsBlocked(result.Summary);
            SetWorkflowState(layout, blocked ? WorkflowState.ValidationBlocked : WorkflowState.ValidationPassed);
            StatusText = blocked
                ? "Validation blocked: blocking findings require correction before outputs."
                : "Validation passed: output stage is now eligible.";
            return result;
        }
        catch (OperationCanceledException)
        {
            SetWorkflowState(layout, WorkflowState.ValidationBlocked);
            StatusText = "Validation cancelled before completion.";
            return ValidationExecutionResult.Failed(StatusText);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or JsonException
            or ArgumentException)
        {
            SetWorkflowState(layout, WorkflowState.ValidationBlocked);
            StatusText = $"Validation failed: {exception.Message}";
            return ValidationExecutionResult.Failed(StatusText);
        }
        finally
        {
            validationRunActive = false;
        }
    }

    public async Task<OutputExecutionResult> RunOutputsAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (outputRunActive)
        {
            StatusText = "Outputs are already running.";
            return OutputExecutionResult.Failed(StatusText);
        }

        if (!CanRunOutputState(CurrentState) || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = "Validation must pass before output generation can start.";
            return OutputExecutionResult.Failed(StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var staleApprovalError = EnsureCurrentApproval(layout);
        if (!string.IsNullOrWhiteSpace(staleApprovalError))
        {
            SetWorkflowState(layout, WorkflowState.ReviewPending);
            StatusText = staleApprovalError;
            return OutputExecutionResult.Failed(staleApprovalError);
        }

        ManifestDocument manifest;
        try
        {
            manifest = ManifestSerializer.Read(layout.ManifestPath);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            StatusText = "Outputs could not start because the manifest could not be read.";
            return OutputExecutionResult.Failed(StatusText);
        }

        outputRunActive = true;
        currentOutputSummary = null;
        SetWorkflowState(layout, WorkflowState.OutputRunning);
        StatusText = "Outputs running: creating transaction geometry and local geodatabase.";

        try
        {
            var result = await outputExecutionService.RunAsync(layout, manifest, operatorId, cancellationToken).ConfigureAwait(false);
            if (!result.Success || result.Summary is null || string.IsNullOrWhiteSpace(result.SummaryPath))
            {
                SetWorkflowState(layout, WorkflowState.ValidationPassed);
                StatusText = result.ErrorMessage ?? "Output generation failed.";
                return result.Success ? OutputExecutionResult.Failed(StatusText) : result;
            }

            currentOutputSummary = result.Summary;
            foreach (var artifactPath in outputSummaryPersistenceService.GetArtifactPaths(layout, result.Summary))
            {
                if (File.Exists(artifactPath) || Directory.Exists(artifactPath))
                {
                    UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(artifactPath), artifactPath));
                }
            }

            SetWorkflowState(layout, WorkflowState.OutputCreated);
            StatusText = "Outputs created: local geometry is ready for map review.";
            return result;
        }
        catch (OperationCanceledException)
        {
            SetWorkflowState(layout, WorkflowState.ValidationPassed);
            StatusText = "Output generation cancelled before completion.";
            return OutputExecutionResult.Failed(StatusText);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or JsonException
            or ArgumentException)
        {
            SetWorkflowState(layout, WorkflowState.ValidationPassed);
            StatusText = $"Output generation failed: {exception.Message}";
            return OutputExecutionResult.Failed(StatusText);
        }
        finally
        {
            outputRunActive = false;
        }
    }

    private void SetWorkflowState(CaseFolderLayout layout, WorkflowState state)
    {
        CurrentState = state;
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = state.ToContractValue() } });
    }

    private void InvalidatePreflight(CaseFolderLayout layout)
    {
        ClearPreflightResults();
        availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, layout.PreflightSummaryPath, StringComparison.OrdinalIgnoreCase));
        RemoveExtractionArtifacts(layout);
        RemoveValidationArtifacts(layout);
        RemoveOutputArtifacts(layout);
        if (File.Exists(layout.PreflightSummaryPath))
        {
            try
            {
                File.Delete(layout.PreflightSummaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                intakeIssues.Add($"Preflight summary could not be cleared: {exception.Message}");
            }
        }

        SetWorkflowState(layout, WorkflowState.Intake);
    }

    private async Task<WorkflowScriptExecutionResult> RunDraftExtractionInternalAsync(CancellationToken cancellationToken)
    {
        if (extractionRunActive)
        {
            StatusText = "Extraction is already running.";
            return WorkflowScriptExecutionResult.Failed(StatusText);
        }

        if (!CanRunExtractionReviewState(CurrentState) || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = "Run preflight successfully before starting extraction review.";
            return WorkflowScriptExecutionResult.Failed(StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        if (File.Exists(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json")))
        {
            var existingArtifact = new AvailableArtifact("extraction_review_data.json", Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"));
            UpsertAvailableArtifact(existingArtifact);
            if (CurrentState != WorkflowState.ReviewPending)
            {
                SetWorkflowState(layout, WorkflowState.ReviewPending);
            }

            StatusText = "Extraction review artifact is ready to open.";
            return new WorkflowScriptExecutionResult(true, null, existingArtifact.Path, new[] { existingArtifact });
        }

        ManifestDocument manifest;
        try
        {
            manifest = ManifestSerializer.Read(layout.ManifestPath);
        }
        catch (Exception exception) when (IsExpectedPreflightIoFailure(exception))
        {
            StatusText = "Extraction could not start because the manifest could not be read.";
            return WorkflowScriptExecutionResult.Failed(StatusText);
        }

        extractionRunActive = true;
        CurrentState = WorkflowState.ExtractionRunning;
        StatusText = "Extraction running: generating draft review data.";

        try
        {
            SetWorkflowState(layout, WorkflowState.ExtractionRunning);
            var executionResult = await workflowScriptExecutor.ExecuteDraftExtractionAsync(layout, manifest, cancellationToken).ConfigureAwait(false);
            if (!executionResult.Success)
            {
                SetWorkflowState(layout, WorkflowState.ExtractionFailed);
                StatusText = executionResult.ErrorMessage ?? "Draft extraction failed.";
                return executionResult;
            }

            foreach (var artifact in executionResult.Artifacts)
            {
                UpsertAvailableArtifact(artifact);
            }

            SetWorkflowState(layout, WorkflowState.ReviewPending);
            StatusText = "Draft extraction complete: review artifact generated.";
            return executionResult;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            SetWorkflowState(layout, WorkflowState.ExtractionFailed);
            StatusText = $"Draft extraction failed: {exception.Message}";
            return WorkflowScriptExecutionResult.Failed(StatusText);
        }
        finally
        {
            extractionRunActive = false;
        }
    }

    private PreflightSummaryDocument BlockManifestPreflightFailure(CaseFolderLayout layout, string? operatorId, Exception exception)
    {
        var message = "Manifest could not be read.";
        var blocker = PreflightCheck.Blocker(
            "manifest_readable",
            message,
            layout.ManifestPath,
            null,
            "Repair or recreate the Case Folder manifest.");
        ClearPreflightResults();
        preflightBlockers.Add(blocker);
        CurrentState = WorkflowState.PreflightBlocked;
        StatusText = $"Preflight blocked: {message.ToLowerInvariant()}";

        var summary = new PreflightSummaryDocument(
            "1.0.0",
            TransactionId ?? string.Empty,
            $"preflight-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            operatorId,
            string.Empty,
            new PreflightSummaryPayload("blocked", preflightBlockers.ToArray(), Array.Empty<PreflightCheck>(), Array.Empty<PreflightCheck>()),
            Array.Empty<string>(),
            new[] { exception.Message });

        try
        {
            PreflightSummarySerializer.Write(layout.PreflightSummaryPath, summary);
            UpsertAvailableArtifact(new AvailableArtifact("preflight_summary.json", layout.PreflightSummaryPath));
        }
        catch (Exception writeException) when (IsExpectedPreflightIoFailure(writeException))
        {
            intakeIssues.Add($"Preflight summary could not be written: {writeException.Message}");
        }

        return summary;
    }

    private static bool IsExpectedPreflightIoFailure(Exception exception)
    {
        return exception is JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException;
    }

    private void LoadPreflightResults(CaseFolderLayout layout)
    {
        ClearPreflightResults();
        if (!File.Exists(layout.PreflightSummaryPath))
        {
            return;
        }

        try
        {
            var summary = PreflightSummarySerializer.Read(layout.PreflightSummaryPath);
            if (summary.Payload is null)
            {
                intakeIssues.Add("Preflight summary could not be read: payload is missing.");
                return;
            }

            preflightBlockers.AddRange(summary.Payload.Blockers);
            preflightWarnings.AddRange(summary.Payload.Warnings);
            preflightPassedChecks.AddRange(summary.Payload.PassedChecks);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            intakeIssues.Add($"Preflight summary could not be read: {exception.Message}");
        }
    }

    private void UpsertAvailableArtifact(AvailableArtifact artifact)
    {
        if (availableArtifacts.Any(existing => string.Equals(existing.Path, artifact.Path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        availableArtifacts.Add(artifact);
    }

    private void RemoveExtractionArtifacts(CaseFolderLayout layout)
    {
        var artifactPaths = new[]
        {
            Path.Combine(layout.WorkingDirectory, "CreateParcelFromFile_case.ini"),
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(layout.WorkingDirectory, "extraction_points.json"),
            Path.Combine(layout.WorkingDirectory, "normalized_points.json"),
            Path.Combine(layout.WorkingDirectory, "plan_ocr.json"),
            Path.Combine(layout.WorkingDirectory, "dwg_context.json"),
            Path.Combine(layout.WorkingDirectory, "approved_review.json")
        };

        foreach (var artifactPath in artifactPaths)
        {
            availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, artifactPath, StringComparison.OrdinalIgnoreCase));
            if (!File.Exists(artifactPath))
            {
                continue;
            }

            try
            {
                File.Delete(artifactPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                intakeIssues.Add($"Extraction artifact could not be cleared: {exception.Message}");
            }
        }
    }

    private void RemoveValidationArtifacts(CaseFolderLayout layout)
    {
        currentValidationSummary = null;
        var validationPath = Path.Combine(layout.WorkingDirectory, validationSummaryPersistenceService.ValidationArtifactFileName);
        availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, validationPath, StringComparison.OrdinalIgnoreCase));
        if (!File.Exists(validationPath))
        {
            return;
        }

        try
        {
            File.Delete(validationPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            intakeIssues.Add($"Validation artifact could not be cleared: {exception.Message}");
        }
    }

    private void RemoveOutputArtifacts(CaseFolderLayout layout)
    {
        currentOutputSummary = null;
        var candidateFiles = new[]
        {
            Path.Combine(layout.OutputDirectory, outputSummaryPersistenceService.OutputArtifactFileName),
            Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson")
        };

        foreach (var candidateFile in candidateFiles)
        {
            availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, candidateFile, StringComparison.OrdinalIgnoreCase));
            if (!File.Exists(candidateFile))
            {
                continue;
            }

            try
            {
                File.Delete(candidateFile);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                intakeIssues.Add($"Output artifact could not be cleared: {exception.Message}");
            }
        }

        if (Directory.Exists(layout.OutputDirectory))
        {
            foreach (var gdbDirectory in Directory.GetDirectories(layout.OutputDirectory, "*.gdb", SearchOption.TopDirectoryOnly))
            {
                availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, gdbDirectory, StringComparison.OrdinalIgnoreCase));
                try
                {
                    Directory.Delete(gdbDirectory, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    intakeIssues.Add($"Output geodatabase could not be cleared: {exception.Message}");
                }
            }
        }
    }

    private ValidationSummaryDocument? LoadValidationSummary(CaseFolderLayout layout, WorkflowState reopenedState)
    {
        if (reopenedState is not WorkflowState.ValidationBlocked
            and not WorkflowState.ValidationPassed
            and not WorkflowState.OutputRunning
            and not WorkflowState.OutputCreated)
        {
            return null;
        }

        try
        {
            return validationSummaryPersistenceService.Load(layout);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            intakeIssues.Add($"Validation summary could not be read: {exception.Message}");
            return null;
        }
    }

    private OutputSummaryDocument? LoadOutputSummary(CaseFolderLayout layout, WorkflowState reopenedState)
    {
        if (reopenedState is not WorkflowState.OutputCreated)
        {
            return null;
        }

        try
        {
            return outputSummaryPersistenceService.Load(layout);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            intakeIssues.Add($"Output summary could not be read: {exception.Message}");
            return null;
        }
    }

    private string? EnsureCurrentApproval(CaseFolderLayout layout)
    {
        var approvedPath = Path.Combine(layout.WorkingDirectory, "approved_review.json");
        if (!File.Exists(approvedPath))
        {
            RemoveValidationArtifacts(layout);
            RemoveOutputArtifacts(layout);
            return "Validation requires a current approved review snapshot. Please approve the review again before validation.";
        }

        var reviewDocument = extractionReviewService.Load(layout);
        if (reviewDocument is null)
        {
            RemoveValidationArtifacts(layout);
            RemoveOutputArtifacts(layout);
            return "Validation requires extraction review data. Reload extraction review and approve it again.";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(approvedPath));
            var root = document.RootElement;
            var approvedHash = ReadJsonString(root, "review_hash") ?? ReadJsonString(root, "review_data_hash");
            if (string.IsNullOrWhiteSpace(approvedHash) || !string.Equals(approvedHash, reviewDocument.ReviewHash, StringComparison.OrdinalIgnoreCase))
            {
                RemoveValidationArtifacts(layout);
                RemoveOutputArtifacts(layout);
                return "Validation blocked: review data changed after approval. Save any edits, then approve the review again.";
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            RemoveValidationArtifacts(layout);
            RemoveOutputArtifacts(layout);
            return $"Validation blocked: approved review snapshot could not be verified. {exception.Message}";
        }

        return null;
    }

    private static string? ReadJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private void ClearPreflightResults()
    {
        preflightBlockers.Clear();
        preflightWarnings.Clear();
        preflightPassedChecks.Clear();
    }
}
