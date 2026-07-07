using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Execution;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Review;
using ParcelWorkflowAddIn.Workflow.SpatialReview;
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
    private readonly IEnterpriseWorkingLayerPublishService enterpriseWorkingLayerPublishService;
    private readonly IEnterpriseWorkingStateRestoreService enterpriseWorkingStateRestoreService;
    private readonly IEnterpriseWorkingDispositionService enterpriseWorkingDispositionService;
    private readonly ComputeReviewDispositionPersistenceService computeReviewDispositionPersistenceService;
    private readonly SpatialReviewApprovalPersistenceService spatialReviewApprovalPersistenceService;
    private readonly ExtractionDecisionGateService extractionDecisionGateService;
    private readonly WorkflowLifecycleAuditService workflowLifecycleAuditService;
    private readonly Func<Innola.InnolaTransactionSettings> getTransactionSettings;
    private readonly List<SourceFileCopyResult> sourceFiles = [];
    private readonly List<string> intakeIssues = [];
    private readonly List<AvailableArtifact> availableArtifacts = [];
    private readonly List<PreflightCheck> preflightBlockers = [];
    private readonly List<PreflightCheck> preflightWarnings = [];
    private readonly List<PreflightCheck> preflightPassedChecks = [];
    private readonly List<PreflightCheck> structureCheckBlockers = [];
    private readonly List<PreflightCheck> structureCheckWarnings = [];
    private readonly List<PreflightCheck> structureCheckPassedChecks = [];
    private readonly List<PreflightCheck> georeferenceCheckBlockers = [];
    private readonly List<PreflightCheck> georeferenceCheckWarnings = [];
    private readonly List<PreflightCheck> georeferenceCheckPassedChecks = [];
    private readonly List<PreflightCheck> dimensionCheckBlockers = [];
    private readonly List<PreflightCheck> dimensionCheckWarnings = [];
    private readonly List<PreflightCheck> dimensionCheckPassedChecks = [];
    private bool preflightRunActive;
    private bool extractionRunActive;
    private bool validationRunActive;
    private bool outputRunActive;
    private ValidationSummaryDocument? currentValidationSummary;
    private OutputSummaryDocument? currentOutputSummary;
    private ExtractionDecisionGateState extractionDecisionGateState = ExtractionDecisionGateState.Empty;
    private ExtractionDecisionGateResult extractionDecisionGateResult = ExtractionDecisionGateResult.NotEvaluated;

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
            new OutputSummaryPersistenceService(),
            new JsonEnterpriseReviewPublishService(),
            new JsonEnterpriseWorkingStateRestoreService(),
            new JsonEnterpriseWorkingDispositionService(),
            new ComputeReviewDispositionPersistenceService(),
            new SpatialReviewApprovalPersistenceService(),
            new ExtractionDecisionGateService(),
            new WorkflowLifecycleAuditService(),
            Innola.InnolaTransactionSettings.Load)
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
            new OutputSummaryPersistenceService(),
            new JsonEnterpriseReviewPublishService(),
            new JsonEnterpriseWorkingStateRestoreService(),
            new JsonEnterpriseWorkingDispositionService(),
            new ComputeReviewDispositionPersistenceService(),
            new SpatialReviewApprovalPersistenceService(),
            new ExtractionDecisionGateService(),
            new WorkflowLifecycleAuditService(),
            Innola.InnolaTransactionSettings.Load)
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
            new OutputSummaryPersistenceService(),
            new JsonEnterpriseReviewPublishService(),
            new JsonEnterpriseWorkingStateRestoreService(),
            new JsonEnterpriseWorkingDispositionService(),
            new ComputeReviewDispositionPersistenceService(),
            new SpatialReviewApprovalPersistenceService(),
            new ExtractionDecisionGateService(),
            new WorkflowLifecycleAuditService(),
            Innola.InnolaTransactionSettings.Load)
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
        OutputSummaryPersistenceService outputSummaryPersistenceService,
        IEnterpriseWorkingLayerPublishService enterpriseWorkingLayerPublishService,
        IEnterpriseWorkingStateRestoreService enterpriseWorkingStateRestoreService,
        IEnterpriseWorkingDispositionService enterpriseWorkingDispositionService,
        ComputeReviewDispositionPersistenceService computeReviewDispositionPersistenceService,
        SpatialReviewApprovalPersistenceService spatialReviewApprovalPersistenceService)
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
            outputExecutionService,
            outputSummaryPersistenceService,
            enterpriseWorkingLayerPublishService,
            enterpriseWorkingStateRestoreService,
            enterpriseWorkingDispositionService,
            computeReviewDispositionPersistenceService,
            spatialReviewApprovalPersistenceService,
            new ExtractionDecisionGateService(),
            new WorkflowLifecycleAuditService())
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
        OutputSummaryPersistenceService outputSummaryPersistenceService,
        IEnterpriseWorkingLayerPublishService enterpriseWorkingLayerPublishService,
        IEnterpriseWorkingStateRestoreService enterpriseWorkingStateRestoreService,
        SpatialReviewApprovalPersistenceService spatialReviewApprovalPersistenceService)
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
            outputExecutionService,
            outputSummaryPersistenceService,
            enterpriseWorkingLayerPublishService,
            enterpriseWorkingStateRestoreService,
            new JsonEnterpriseWorkingDispositionService(),
            new ComputeReviewDispositionPersistenceService(),
            spatialReviewApprovalPersistenceService,
            new ExtractionDecisionGateService(),
            new WorkflowLifecycleAuditService())
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
        OutputSummaryPersistenceService outputSummaryPersistenceService,
        IEnterpriseWorkingLayerPublishService enterpriseWorkingLayerPublishService,
        IEnterpriseWorkingStateRestoreService enterpriseWorkingStateRestoreService,
        IEnterpriseWorkingDispositionService enterpriseWorkingDispositionService,
        ComputeReviewDispositionPersistenceService computeReviewDispositionPersistenceService,
        SpatialReviewApprovalPersistenceService spatialReviewApprovalPersistenceService,
        ExtractionDecisionGateService extractionDecisionGateService,
        WorkflowLifecycleAuditService workflowLifecycleAuditService,
        Func<Innola.InnolaTransactionSettings>? getTransactionSettings = null)
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
        this.enterpriseWorkingLayerPublishService = enterpriseWorkingLayerPublishService;
        this.enterpriseWorkingStateRestoreService = enterpriseWorkingStateRestoreService;
        this.enterpriseWorkingDispositionService = enterpriseWorkingDispositionService;
        this.computeReviewDispositionPersistenceService = computeReviewDispositionPersistenceService;
        this.spatialReviewApprovalPersistenceService = spatialReviewApprovalPersistenceService;
        this.extractionDecisionGateService = extractionDecisionGateService;
        this.workflowLifecycleAuditService = workflowLifecycleAuditService;
        this.getTransactionSettings = getTransactionSettings ?? Innola.InnolaTransactionSettings.Load;
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

    public IReadOnlyList<PreflightCheck> StructureCheckBlockers => structureCheckBlockers;

    public IReadOnlyList<PreflightCheck> StructureCheckWarnings => structureCheckWarnings;

    public IReadOnlyList<PreflightCheck> StructureCheckPassedChecks => structureCheckPassedChecks;

    public IReadOnlyList<PreflightCheck> GeoreferenceCheckBlockers => georeferenceCheckBlockers;

    public IReadOnlyList<PreflightCheck> GeoreferenceCheckWarnings => georeferenceCheckWarnings;

    public IReadOnlyList<PreflightCheck> GeoreferenceCheckPassedChecks => georeferenceCheckPassedChecks;

    public IReadOnlyList<PreflightCheck> DimensionCheckBlockers => dimensionCheckBlockers;

    public IReadOnlyList<PreflightCheck> DimensionCheckWarnings => dimensionCheckWarnings;

    public IReadOnlyList<PreflightCheck> DimensionCheckPassedChecks => dimensionCheckPassedChecks;

    public bool IsPreflightRunning => preflightRunActive;

    public bool IsExtractionRunning => extractionRunActive;

    public bool IsValidationRunning => validationRunActive;

    public bool IsOutputRunning => outputRunActive;

    public bool HasPreflightResult => CurrentState is WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed or WorkflowState.ExtractionRunning or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending or WorkflowState.ReviewManualPending or WorkflowState.ReviewApproved or WorkflowState.ValidationRunning or WorkflowState.ValidationBlocked or WorkflowState.ValidationPassed or WorkflowState.OutputRunning or WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending or WorkflowState.SpatialReviewApproved;

    public bool CanRunPreflight => CanRunPreflightState(CurrentState);

    public bool CanRunStructureCheck => !preflightRunActive && CanRunPreflightState(CurrentState);

    public bool CanRunGeoreferenceCheck => !preflightRunActive && CanRunPreflightState(CurrentState) && IsStructureCheckPassed();

    public bool CanRunDimensionCheck => !preflightRunActive && CanRunPreflightState(CurrentState) && IsStructureCheckPassed() && IsGeoreferenceCheckPassed();

    public bool CanRunExtractionReview => !extractionRunActive
        && CanRunExtractionReviewState(CurrentState)
        && IsStructureCheckPassed()
        && IsGeoreferenceCheckPassed()
        && IsDimensionCheckPassed();

    public bool CanChooseManualCogoReview =>
        !extractionRunActive
        && CanChooseManualCogoReviewState(CurrentState)
        && (HasExtractionReviewArtifactForCurrentCase() || extractionDecisionGateResult.StronglyRecommendManual);

    public bool CanRunValidation => !validationRunActive && CanRunValidationState(CurrentState);

    public bool CanRunOutputs => !outputRunActive && CanRunOutputState(CurrentState);

    public bool CanApproveSpatialReview =>
        currentOutputSummary is not null
        && CurrentState is WorkflowState.OutputCreated or WorkflowState.SpatialReviewPending;

    public ValidationSummaryDocument? CurrentValidationSummary => currentValidationSummary;

    public OutputSummaryDocument? CurrentOutputSummary => currentOutputSummary;

    public ExtractionDecisionGateResult CurrentExtractionDecisionGate => extractionDecisionGateResult;

    public bool HasUsableExtractionReview => extractionDecisionGateResult.HasUsableReview;

    public bool ExtractionResultRequiresDecision => extractionDecisionGateResult.RequiresDecision;

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
        extractionDecisionGateState = ExtractionDecisionGateState.Empty;
        extractionDecisionGateResult = ExtractionDecisionGateResult.NotEvaluated;
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
        var effectiveSourceFiles = SupportingDocumentSourceFilter.Apply(
            manifest.Payload.SourceFiles,
            manifest.Payload.SupportingDocumentOptions);
        var profile = sourceInputProfileDetector.Detect(effectiveSourceFiles);
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

        var effectiveSourceFiles = SupportingDocumentSourceFilter.Apply(
            manifest.Payload.SourceFiles,
            manifest.Payload.SupportingDocumentOptions);

        return workflowRuleResolver.Resolve(new WorkflowRuleResolutionContext(
            manifest.Payload.InnolaTransaction.CaseType,
            manifest.Payload.InnolaTransaction.ProcessStep,
            profile,
            effectiveSourceFiles,
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
        RestoreExtractionDecisionGate(result.Layout);
        var enterpriseRestore = enterpriseWorkingStateRestoreService.Restore(result.Layout, result.Manifest, result.ResolvedState, currentOutputSummary);
        intakeIssues.AddRange(enterpriseRestore.RecoverabilityIssues.Select(issue => issue.Message));
        foreach (var artifact in enterpriseRestore.AddedArtifacts)
        {
            UpsertAvailableArtifact(artifact);
        }

        if (enterpriseRestore.RestoredOutputSummary is not null)
        {
            currentOutputSummary = enterpriseRestore.RestoredOutputSummary;
            outputSummaryPersistenceService.Save(result.Layout, currentOutputSummary);
            foreach (var artifactPath in outputSummaryPersistenceService.GetArtifactPaths(result.Layout, currentOutputSummary))
            {
                if (File.Exists(artifactPath) || Directory.Exists(artifactPath))
                {
                    UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(artifactPath), artifactPath));
                }
            }
        }

        RestoreSpatialReviewState(result.Layout, result.ResolvedState);
        RestoreComputeCloseoutState(result.Layout, currentOutputSummary);
        StatusText = result.RecoverabilityIssues.Count == 0 && enterpriseRestore.RecoverabilityIssues.Count == 0
            ? enterpriseRestore.StatusMessage
            : enterpriseRestore.EnterpriseStateFound
                ? enterpriseRestore.StatusMessage
                : "Case reopened. Some saved artifacts could not be restored - please review results.";

        return result;
    }

    private void RestoreComputeCloseoutState(CaseFolderLayout layout, OutputSummaryDocument? outputSummary)
    {
        var disposition = computeReviewDispositionPersistenceService.Load(layout);
        if (disposition is null)
        {
            return;
        }

        UpsertAvailableArtifact(new AvailableArtifact(
            ComputeReviewDispositionPersistenceService.DispositionArtifactFileName,
            computeReviewDispositionPersistenceService.GetDispositionPath(layout)));
        UpsertCloseoutArtifact(layout, disposition.EnterpriseDispositionRef);
        UpsertCloseoutArtifact(layout, disposition.ComputeExaminationReportRef);

        if (outputSummary is not null
            && !string.IsNullOrWhiteSpace(disposition.PublishRunId)
            && !string.Equals(outputSummary.RunId, disposition.PublishRunId, StringComparison.OrdinalIgnoreCase))
        {
            intakeIssues.Add("Compute disposition was restored, but the current output summary run id differs from the decided Enterprise publish run. Review closeout evidence before Finalize.");
        }
    }

    private void UpsertCloseoutArtifact(CaseFolderLayout layout, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var resolvedPath = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(layout.RootDirectory, path.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(resolvedPath))
        {
            UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(resolvedPath), resolvedPath));
        }
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

    public PreflightSummaryDocument RunStructureCheck(string? operatorId)
    {
        return RunStructureCheckAsync(operatorId).GetAwaiter().GetResult();
    }

    public PreflightSummaryDocument RunDimensionCheck(string? operatorId)
    {
        return RunDimensionCheckAsync(operatorId).GetAwaiter().GetResult();
    }

    public PreflightSummaryDocument RunGeoreferenceCheck(string? operatorId)
    {
        return RunGeoreferenceCheckAsync(operatorId).GetAwaiter().GetResult();
    }

    public Task<WorkflowScriptExecutionResult> RunDraftExtractionAsync(CancellationToken cancellationToken = default)
    {
        return RunDraftExtractionInternalAsync(forceReprocess: false, cancellationToken);
    }

    public Task<WorkflowScriptExecutionResult> RunDraftExtractionAsync(bool forceReprocess, CancellationToken cancellationToken = default)
    {
        return RunDraftExtractionInternalAsync(forceReprocess, cancellationToken);
    }

    public async Task<PreflightSummaryDocument> RunManifestPreflightAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        var structureSummary = await RunStructureCheckAsync(operatorId, cancellationToken).ConfigureAwait(false);
        if (string.Equals(structureSummary.RunId, "not-run", StringComparison.OrdinalIgnoreCase))
        {
            return structureSummary;
        }

        if (structureSummary.Payload.Blockers.Count > 0)
        {
            WriteLegacyPreflightSummaryIfPossible();
            return structureSummary;
        }

        var georeferenceSummary = await RunGeoreferenceCheckAsync(operatorId, cancellationToken).ConfigureAwait(false);
        if (georeferenceSummary.Payload.Blockers.Count > 0)
        {
            WriteLegacyPreflightSummaryIfPossible();
            return georeferenceSummary;
        }

        var dimensionSummary = await RunDimensionCheckAsync(operatorId, cancellationToken).ConfigureAwait(false);
        WriteLegacyPreflightSummaryIfPossible();
        return dimensionSummary;
    }

    public async Task<PreflightSummaryDocument> RunStructureCheckAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        return await RunEarlyCheckStageAsync(
            PreflightCheckStage.StructureCheck,
            operatorId,
            () => manifestPreflightService.RunStructureCheckAsync(CaseFolderLayout.FromRootDirectory(CaseFolderPath!), operatorId, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PreflightSummaryDocument> RunDimensionCheckAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (!IsStructureCheckPassed())
        {
            StatusText = "Run Structure Check successfully before running Dimension Check.";
            var summary = BuildNotRunSummary(
                PreflightCheckStage.DimensionCheck,
                operatorId,
                "structure_check_required",
                StatusText,
                "Resolve Structure Check blockers first.");
            SetDimensionCheckResults(summary);
            RebuildPreflightResults();
            return summary;
        }

        if (!IsGeoreferenceCheckPassed())
        {
            StatusText = "Run Georeference Check successfully before running Dimension Check.";
            var summary = BuildNotRunSummary(
                PreflightCheckStage.DimensionCheck,
                operatorId,
                "georeference_check_required",
                StatusText,
                "Resolve Georeference Check blockers first.");
            SetDimensionCheckResults(summary);
            RebuildPreflightResults();
            return summary;
        }

        return await RunEarlyCheckStageAsync(
            PreflightCheckStage.DimensionCheck,
            operatorId,
            () => manifestPreflightService.RunDimensionCheckAsync(CaseFolderLayout.FromRootDirectory(CaseFolderPath!), operatorId, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PreflightSummaryDocument> RunGeoreferenceCheckAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (!IsStructureCheckPassed())
        {
            StatusText = "Run Structure Check successfully before running Georeference Check.";
            var summary = BuildNotRunSummary(
                PreflightCheckStage.GeoreferenceCheck,
                operatorId,
                "structure_check_required",
                StatusText,
                "Resolve Structure Check blockers first.");
            SetGeoreferenceCheckResults(summary);
            RebuildPreflightResults();
            return summary;
        }

        return await RunEarlyCheckStageAsync(
            PreflightCheckStage.GeoreferenceCheck,
            operatorId,
            () => manifestPreflightService.RunGeoreferenceCheckAsync(CaseFolderLayout.FromRootDirectory(CaseFolderPath!), operatorId, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<PreflightSummaryDocument> RunEarlyCheckStageAsync(
        PreflightCheckStage stage,
        string? operatorId,
        Func<Task<PreflightSummaryDocument>> runStage,
        CancellationToken cancellationToken)
    {
        if (preflightRunActive)
        {
            StatusText = $"{GetStageDisplayName(stage)} is already running.";
            return new PreflightSummaryDocument(
                "1.0.0",
                TransactionId ?? string.Empty,
                stage.ToStageId(),
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
            StatusText = $"Create or reopen a Case Folder before running {GetStageDisplayName(stage)}.";
            var failedSummary = BuildNotRunSummary(
                stage,
                operatorId,
                "active_case_required",
                StatusText,
                "Create or reopen a Case Folder.");
            SetStageResults(stage, failedSummary);
            RebuildPreflightResults();
            return failedSummary;
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        CurrentState = WorkflowState.PreflightRunning;
        StatusText = stage switch
        {
            PreflightCheckStage.StructureCheck => "Structure Check is running: validating transaction attachments, source structure, DWG readiness, and processing prerequisites.",
            PreflightCheckStage.GeoreferenceCheck => "Georeference Check is running: validating coordinate source, JAD2001 readiness, and location bounds.",
            PreflightCheckStage.DimensionCheck => "Dimension Check is running: validating dimension and geometry-construction readiness.",
            _ => "Early compute checks are running."
        };
        preflightRunActive = true;

        try
        {
            SetWorkflowState(layout, WorkflowState.PreflightRunning);
            var summary = await runStage().ConfigureAwait(false);
            SetStageResults(stage, summary);
            if (stage == PreflightCheckStage.StructureCheck)
            {
                ClearGeoreferenceCheckResults(layout);
                ClearDimensionCheckResults(layout);
            }
            else if (stage == PreflightCheckStage.GeoreferenceCheck)
            {
                ClearDimensionCheckResults(layout);
            }

            RebuildPreflightResults();

            var finalState = IsStructureCheckPassed() && IsGeoreferenceCheckPassed() && IsDimensionCheckPassed()
                ? WorkflowState.PreflightPassed
                : WorkflowState.PreflightBlocked;
            SetWorkflowState(layout, finalState);
            UpsertAvailableArtifact(new AvailableArtifact(stage.ToArtifactFileName(), ResolveStageSummaryPath(layout, stage)));
            WriteLegacyPreflightSummaryIfPossible();

            if (summary.Payload.Blockers.Count > 0)
            {
                StatusText = $"{GetStageDisplayName(stage)} blocked: {summary.Payload.Blockers[0].Message.ToLowerInvariant()}";
            }
            else if (finalState == WorkflowState.PreflightPassed)
            {
                StatusText = "Structure Check, Georeference Check, and Dimension Check passed: attached files are ready for Validate Points and Lines.";
            }
            else
            {
                StatusText = stage switch
                {
                    PreflightCheckStage.StructureCheck => "Structure Check passed. Run Georeference Check before Dimension Check.",
                    PreflightCheckStage.GeoreferenceCheck => "Georeference Check passed. Run Dimension Check before starting Validate Points and Lines.",
                    _ => "Dimension Check completed, but all early checks must pass before Validate Points and Lines."
                };
            }

            return summary;
        }
        catch (Exception exception) when (IsExpectedPreflightIoFailure(exception))
        {
            return BlockManifestPreflightFailure(layout, stage, operatorId, exception);
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
        return state is WorkflowState.PreflightPassed or WorkflowState.ExtractionFailed or WorkflowState.ReviewPending or WorkflowState.ReviewManualPending;
    }

    private static bool CanChooseManualCogoReviewState(WorkflowState state)
    {
        return state is WorkflowState.ReviewPending or WorkflowState.ReviewManualPending;
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
        RefreshExtractionDecisionGate(layout, document);
        StatusText = extractionDecisionGateResult.RequiresDecision
            ? extractionDecisionGateResult.GuidanceText
            : summary.CanApprove
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
        SetWorkflowState(layout, CurrentState == WorkflowState.ReviewManualPending ? WorkflowState.ReviewManualPending : WorkflowState.ReviewPending);
        StatusText = saveResult.Message;
        return saveResult;
    }

    public async Task<bool> UseManualCogoReviewAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Create or reopen a Case Folder before preparing the manual review workspace.";
            return false;
        }

        if (!CanChooseManualCogoReview)
        {
            StatusText = "Manual review workspace is only available after extracted point review data exists for this case.";
            return false;
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
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
            StatusText = $"Manual review workspace could not start because the manifest could not be read. {exception.Message}";
            return false;
        }

        RemoveValidationArtifacts(layout);
        RemoveOutputArtifacts(layout);
        SetWorkflowState(layout, WorkflowState.ReviewManualPending);
        RecordManualReviewDecision(layout, operatorId);
        StatusText = "Manual review workspace is being prepared from the current case review data.";

        outputRunActive = true;
        currentOutputSummary = null;

        try
        {
            var result = await outputExecutionService.RunManualReviewAsync(layout, manifest, operatorId, cancellationToken).ConfigureAwait(false);
            if (!result.Success || result.Summary is null || string.IsNullOrWhiteSpace(result.SummaryPath))
            {
                SetWorkflowState(layout, WorkflowState.ReviewManualPending);
                StatusText = result.ErrorMessage ?? "Manual COGO review workspace could not be prepared.";
                return false;
            }

            currentOutputSummary = result.Summary;
            var publishResult = EnterpriseWorkingLayerPublishResult.Skipped("Manual review workspace keeps enterprise-backed collaboration optional until reviewed geometry is finalized.");

            foreach (var artifactPath in outputSummaryPersistenceService.GetArtifactPaths(layout, currentOutputSummary))
            {
                if (File.Exists(artifactPath) || Directory.Exists(artifactPath))
                {
                    UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(artifactPath), artifactPath));
                }
            }

            if (!string.IsNullOrWhiteSpace(publishResult.SummaryPath) && File.Exists(publishResult.SummaryPath))
            {
                UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(publishResult.SummaryPath), publishResult.SummaryPath));
            }

            RemoveSpatialReviewArtifacts(layout);
            SetWorkflowState(layout, WorkflowState.SpatialReviewPending);
            workflowLifecycleAuditService.Record(
                layout,
                TransactionId ?? string.Empty,
                "manual_cogo_workspace_prepared",
                "succeeded",
                operatorId,
                "Manual review workspace prepared and moved the case into Final Review.",
                manifest.Payload.InnolaTransaction?.TaskId,
                manifest.Payload.InnolaTransaction?.TransactionNumber,
                currentOutputSummary.Payload.ReviewWorkspaceMode);
            StatusText = "Manual review workspace prepared. The ArcGIS Pro map is ready for editing, and the transaction PDFs remain the source reference.";
            return true;
        }
        catch (OperationCanceledException)
        {
            SetWorkflowState(layout, WorkflowState.ReviewManualPending);
            StatusText = "Manual review workspace preparation was cancelled.";
            return false;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or JsonException
            or ArgumentException)
        {
            SetWorkflowState(layout, WorkflowState.ReviewManualPending);
            StatusText = $"Manual review workspace failed to prepare. {exception.Message}";
            return false;
        }
        finally
        {
            outputRunActive = false;
        }
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

    public SpatialReviewApprovalValidationResult ApproveSpatialReview(string? operatorId)
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Create or reopen a Case Folder before approving spatial review.";
            return SpatialReviewApprovalValidationResult.Invalid(StatusText);
        }

        if (CurrentState is not WorkflowState.OutputCreated and not WorkflowState.SpatialReviewPending)
        {
            StatusText = "Spatial review can only be approved after outputs are ready for map review.";
            return SpatialReviewApprovalValidationResult.Invalid(StatusText);
        }

        if (currentOutputSummary is null)
        {
            StatusText = "Spatial review approval requires a current output summary. Run Outputs again if needed.";
            return SpatialReviewApprovalValidationResult.Invalid(StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        try
        {
            var approval = spatialReviewApprovalPersistenceService.Save(layout, currentOutputSummary, operatorId);
            UpsertAvailableArtifact(new AvailableArtifact(spatialReviewApprovalPersistenceService.ApprovalArtifactFileName, spatialReviewApprovalPersistenceService.GetApprovalPath(layout)));
            SetWorkflowState(layout, WorkflowState.SpatialReviewApproved);
            StatusText = "Spatial review approved. The case is ready for final completion when transaction-level completion is appropriate.";
            return SpatialReviewApprovalValidationResult.Current(approval);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or JsonException or ArgumentException)
        {
            StatusText = $"Spatial review approval failed: {exception.Message}";
            return SpatialReviewApprovalValidationResult.Invalid(StatusText);
        }
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
            var publishResult = EnterpriseWorkingLayerPublishResult.Skipped("Enterprise review publish will occur at the configured downstream stage.");
            if (ShouldPublishEnterpriseAtOutputStage())
            {
                publishResult = await enterpriseWorkingLayerPublishService.PublishAsync(layout, manifest, currentOutputSummary, operatorId, cancellationToken).ConfigureAwait(false);
                PersistEnterprisePublishResult(layout, publishResult);
            }

            foreach (var artifactPath in outputSummaryPersistenceService.GetArtifactPaths(layout, currentOutputSummary))
            {
                if (File.Exists(artifactPath) || Directory.Exists(artifactPath))
                {
                    UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(artifactPath), artifactPath));
                }
            }

            if (!string.IsNullOrWhiteSpace(publishResult.SummaryPath) && File.Exists(publishResult.SummaryPath))
            {
                UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(publishResult.SummaryPath), publishResult.SummaryPath));
            }

            RemoveSpatialReviewArtifacts(layout);
            SetWorkflowState(layout, WorkflowState.SpatialReviewPending);
            var enterprisePublishMode = GetEnterprisePublishMode();
            StatusText = publishResult.Attempted && !publishResult.Success
                ? enterprisePublishMode == Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric
                    ? "Outputs created locally, but Enterprise Parcel Fabric publish failed. Local geometry is still ready for spatial review in the map."
                    : "Outputs created locally, but enterprise working-layer publish failed. Local geometry is still ready for spatial review in the map."
                : publishResult.Attempted
                    ? enterprisePublishMode == Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric
                        ? "Outputs created and Enterprise Parcel Fabric publish completed. Geometry is ready for final review in the map."
                        : "Outputs created and enterprise working-layer publish completed. Geometry is ready for spatial review in the map."
                    : "Outputs created: local geometry is ready for spatial review in the map.";
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

    public async Task<EnterpriseWorkingLayerPublishResult> PublishEnterpriseWorkingReviewAsync(string? operatorId, CancellationToken cancellationToken = default)
    {
        if (CurrentState != WorkflowState.SpatialReviewApproved || string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Complete spatial review before publishing enterprise review geometry.";
            return EnterpriseWorkingLayerPublishResult.Failed(StatusText, null, null);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
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
            StatusText = "Enterprise review publish could not start because the manifest could not be read.";
            return EnterpriseWorkingLayerPublishResult.Failed(StatusText, null, null);
        }

        currentOutputSummary ??= outputSummaryPersistenceService.Load(layout);
        if (currentOutputSummary is null)
        {
            StatusText = "Enterprise review publish could not start because no output summary is available.";
            return EnterpriseWorkingLayerPublishResult.Failed(StatusText, null, null);
        }

        if (string.Equals(currentOutputSummary.Payload.ReviewResultOwner, ReviewResultOwnership.ManualSpatialReview, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Enterprise publish is skipped for manual review workspace cases until the manual-to-enterprise promotion path is completed.";
            return EnterpriseWorkingLayerPublishResult.Skipped(StatusText);
        }

        if (!ShouldPublishEnterpriseAtFinalStage())
        {
            StatusText = "Enterprise publish is not configured for final-review timing.";
            return EnterpriseWorkingLayerPublishResult.Skipped(StatusText);
        }

        var publishResult = await enterpriseWorkingLayerPublishService.PublishAsync(layout, manifest, currentOutputSummary, operatorId, cancellationToken).ConfigureAwait(false);
        PersistEnterprisePublishResult(layout, publishResult);

        if (!string.IsNullOrWhiteSpace(publishResult.SummaryPath) && File.Exists(publishResult.SummaryPath))
        {
            UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(publishResult.SummaryPath), publishResult.SummaryPath));
        }

        StatusText = publishResult.Success
            ? GetEnterprisePublishMode() == Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric
                ? "Enterprise Parcel Fabric publish completed. The validated review is now ready for shared final review."
                : "Enterprise working-layer publish completed. The completed review is now ready for shared visibility."
            : GetEnterprisePublishMode() == Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric
                ? BuildEnterprisePublishFailureStatus("Enterprise Parcel Fabric publish failed. Local outputs remain available.", publishResult)
                : BuildEnterprisePublishFailureStatus("Enterprise working-layer publish failed. Local outputs remain available.", publishResult);
        return publishResult;
    }

    private static string BuildEnterprisePublishFailureStatus(string prefix, EnterpriseWorkingLayerPublishResult publishResult)
    {
        var detail = publishResult.Summary?.Errors.FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
            ?? publishResult.Message;
        if (string.IsNullOrWhiteSpace(detail)
            || string.Equals(detail.Trim().TrimEnd('.'), prefix.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
        {
            return prefix;
        }

        return $"{prefix} {detail}";
    }

    public async Task<ComputeReviewDispositionResult> RecordComputeDispositionAsync(
        ComputeReviewDecision decision,
        string? comment,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        if (CurrentState != WorkflowState.SpatialReviewApproved || string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Compute disposition can only be recorded after Final Review is marked reviewed.";
            return ComputeReviewDispositionResult.Failed(StatusText);
        }

        if (decision is ComputeReviewDecision.Rejected or ComputeReviewDecision.Postponed
            && string.IsNullOrWhiteSpace(comment))
        {
            StatusText = "Reject and Postpone require a review comment.";
            return ComputeReviewDispositionResult.Failed(StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        ManifestDocument manifest;
        try
        {
            manifest = ManifestSerializer.Read(layout.ManifestPath);
            currentOutputSummary ??= outputSummaryPersistenceService.Load(layout);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            StatusText = "Compute disposition could not start because current case artifacts could not be read.";
            return ComputeReviewDispositionResult.Failed(StatusText);
        }

        if (currentOutputSummary is null)
        {
            StatusText = "Compute disposition requires a current output summary.";
            return ComputeReviewDispositionResult.Failed(StatusText);
        }

        var enterprisePublish = currentOutputSummary.Payload.EnterpriseWorkingPublish;
        var enterprisePublishPath = Path.Combine(layout.OutputDirectory, "enterprise_working_publish.json");
        if (enterprisePublish is null || !File.Exists(enterprisePublishPath))
        {
            StatusText = "Compute disposition requires current Enterprise working publish evidence.";
            return ComputeReviewDispositionResult.Failed(StatusText);
        }

        var request = new ComputeReviewDispositionRequest(
            decision,
            comment,
            operatorId,
            currentOutputSummary,
            enterprisePublish,
            Path.Combine(layout.OutputDirectory, outputSummaryPersistenceService.OutputArtifactFileName),
            enterprisePublishPath);
        var enterpriseResult = await enterpriseWorkingDispositionService
            .RecordDispositionAsync(layout, manifest, request, cancellationToken)
            .ConfigureAwait(false);
        if (!enterpriseResult.Success)
        {
            StatusText = enterpriseResult.Message;
            return ComputeReviewDispositionResult.Failed(StatusText);
        }

        var transaction = manifest.Payload.InnolaTransaction;
        var document = new ComputeReviewDispositionDocument(
            "1.0.0",
            manifest.TransactionId,
            transaction?.TransactionNumber ?? manifest.TransactionId,
            transaction?.TaskId,
            decision.ToContractValue(),
            comment,
            operatorId,
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            request.OutputSummaryPath,
            request.EnterprisePublishPath,
            currentOutputSummary.RunId,
            "recorded",
            enterpriseResult.EvidencePath,
            "not_started",
            null,
            null,
            null,
            null);
        var artifactPath = computeReviewDispositionPersistenceService.Save(layout, document);
        UpsertAvailableArtifact(new AvailableArtifact(ComputeReviewDispositionPersistenceService.DispositionArtifactFileName, artifactPath));
        workflowLifecycleAuditService.Record(
            layout,
            TransactionId ?? manifest.TransactionId,
            $"compute_review_{decision.ToContractValue()}",
            "succeeded",
            operatorId,
            $"Compute review {decision.ToContractValue()} disposition recorded.",
            transaction?.TaskId,
            transaction?.TransactionNumber ?? manifest.TransactionId,
            enterpriseResult.EvidencePath);
        if (!string.IsNullOrWhiteSpace(enterpriseResult.EvidencePath) && File.Exists(enterpriseResult.EvidencePath))
        {
            UpsertAvailableArtifact(new AvailableArtifact(Path.GetFileName(enterpriseResult.EvidencePath), enterpriseResult.EvidencePath));
        }

        StatusText = $"Compute review {decision.ToContractValue()} disposition recorded.";
        return ComputeReviewDispositionResult.Succeeded(StatusText, artifactPath, document);
    }

    private void PersistEnterprisePublishResult(CaseFolderLayout layout, EnterpriseWorkingLayerPublishResult publishResult)
    {
        if (!publishResult.Attempted || publishResult.Summary is null || currentOutputSummary is null)
        {
            return;
        }

        currentOutputSummary = currentOutputSummary with
        {
            Payload = currentOutputSummary.Payload with
            {
                EnterpriseWorkingPublish = publishResult.Summary,
                ArtifactPaths = MergeArtifactPaths(
                    currentOutputSummary.Payload.ArtifactPaths,
                    publishResult.SummaryPath)
            }
        };
        outputSummaryPersistenceService.Save(layout, currentOutputSummary);
    }

    private bool ShouldPublishEnterpriseAtOutputStage()
    {
        var settings = getTransactionSettings();
        return settings.ReviewWorkspaceMode switch
        {
            Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers => string.Equals(
                settings.EnterpriseWorkingReview.PublishTiming,
                Innola.EnterpriseWorkingReviewSettings.PublishTimingOnOutputs,
                StringComparison.OrdinalIgnoreCase),
            Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric => string.Equals(
                settings.EnterpriseParcelFabricReview.PublishTiming,
                Innola.EnterpriseParcelFabricReviewSettings.PublishTimingOnOutputs,
                StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private bool ShouldPublishEnterpriseAtFinalStage()
    {
        var settings = getTransactionSettings();
        return settings.ReviewWorkspaceMode switch
        {
            Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseWorkingLayers => string.Equals(
                settings.EnterpriseWorkingReview.PublishTiming,
                Innola.EnterpriseWorkingReviewSettings.PublishTimingOnComplete,
                StringComparison.OrdinalIgnoreCase),
            Innola.InnolaTransactionSettings.ReviewWorkspaceModeEnterpriseParcelFabric => string.Equals(
                settings.EnterpriseParcelFabricReview.PublishTiming,
                Innola.EnterpriseParcelFabricReviewSettings.PublishTimingOnFinalReview,
                StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private string GetEnterprisePublishMode()
    {
        return getTransactionSettings().ReviewWorkspaceMode;
    }

    public ManifestSupportingDocumentOptions GetSupportingDocumentOptions()
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            return new ManifestSupportingDocumentOptions();
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
            var manifest = ManifestSerializer.Read(layout.ManifestPath);
            return manifest.Payload.SupportingDocumentOptions ?? new ManifestSupportingDocumentOptions();
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            StatusText = $"Supporting document options could not be read: {exception.Message}";
            return new ManifestSupportingDocumentOptions();
        }
    }

    public bool SaveSupportingDocumentOptions(bool importStructuredSurveyPoints, bool importAutoCadSurveySource)
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            StatusText = "Create or reopen a Case Folder before saving supporting document options.";
            return false;
        }

        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
            var manifest = ManifestSerializer.Read(layout.ManifestPath);
            var supportingDocumentOptions = new ManifestSupportingDocumentOptions(
                importStructuredSurveyPoints,
                importAutoCadSurveySource);
            var effectiveSourceFiles = SupportingDocumentSourceFilter.Apply(
                manifest.Payload.SourceFiles,
                supportingDocumentOptions);
            var profile = sourceInputProfileDetector.Detect(effectiveSourceFiles);
            var updatedManifest = manifest with
            {
                Payload = manifest.Payload with
                {
                    SupportingDocumentOptions = supportingDocumentOptions,
                    DetectedProfile = profile
                }
            };
            var ruleResolution = ResolveWorkflowRule(updatedManifest, profile);
            ManifestSerializer.Write(
                layout.ManifestPath,
                updatedManifest with
                {
                    Payload = updatedManifest.Payload with
                    {
                        WorkflowProfile = ruleResolution?.ScriptPlan?.WorkflowProfile,
                        WorkflowRuleId = ruleResolution?.ScriptPlan?.RuleId,
                        WorkflowRuleVersion = ruleResolution?.ScriptPlan?.RuleVersion,
                        ScriptPlan = ruleResolution?.ScriptPlan
                    }
                });
            InvalidatePreflight(layout);
            DetectedProfileLabel = profile.DisplayLabel;
            intakeIssues.Clear();
            intakeIssues.AddRange(profile.Issues);
            if (ruleResolution is { Success: false } && !string.IsNullOrWhiteSpace(ruleResolution.ErrorMessage))
            {
                intakeIssues.Add(ruleResolution.ErrorMessage);
            }

            StatusText = "Supporting document options saved.";
            return true;
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            StatusText = $"Supporting document options could not be saved: {exception.Message}";
            return false;
        }
    }

    private void SetWorkflowState(CaseFolderLayout layout, WorkflowState state)
    {
        CurrentState = state;
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        ManifestSerializer.Write(layout.ManifestPath, manifest with { Payload = manifest.Payload with { WorkflowState = state.ToContractValue() } });
    }

    private static IReadOnlyList<string> MergeArtifactPaths(IReadOnlyList<string> existingPaths, string? additionalPath)
    {
        if (string.IsNullOrWhiteSpace(additionalPath))
        {
            return existingPaths;
        }

        return existingPaths
            .Append(additionalPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void InvalidatePreflight(CaseFolderLayout layout)
    {
        ClearPreflightResults();
        var preflightArtifactPaths = new[]
        {
            layout.PreflightSummaryPath,
            layout.StructureCheckSummaryPath,
            layout.GeoreferenceCheckSummaryPath,
            layout.DimensionCheckSummaryPath
        };
        availableArtifacts.RemoveAll(artifact => preflightArtifactPaths.Any(path => string.Equals(artifact.Path, path, StringComparison.OrdinalIgnoreCase)));
        RemoveExtractionArtifacts(layout);
        RemoveValidationArtifacts(layout);
        RemoveOutputArtifacts(layout);
        foreach (var summaryPath in preflightArtifactPaths)
        {
            if (!File.Exists(summaryPath))
            {
                continue;
            }

            try
            {
                File.Delete(summaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                intakeIssues.Add($"Early-check summary could not be cleared: {exception.Message}");
            }
        }

        SetWorkflowState(layout, WorkflowState.Intake);
    }

    private async Task<WorkflowScriptExecutionResult> RunDraftExtractionInternalAsync(bool forceReprocess, CancellationToken cancellationToken)
    {
        if (extractionRunActive)
        {
            StatusText = "Extraction is already running.";
            return WorkflowScriptExecutionResult.Failed(StatusText);
        }

        if (!CanRunExtractionReviewState(CurrentState)
            || !IsStructureCheckPassed()
            || !IsGeoreferenceCheckPassed()
            || !IsDimensionCheckPassed()
            || string.IsNullOrWhiteSpace(CaseFolderPath)
            || string.IsNullOrWhiteSpace(TransactionId))
        {
            StatusText = !IsStructureCheckPassed()
                ? "Run Structure Check successfully before starting Validate Points and Lines."
                : !IsGeoreferenceCheckPassed()
                    ? "Run Georeference Check successfully before starting Validate Points and Lines."
                    : !IsDimensionCheckPassed()
                        ? "Run Dimension Check successfully before starting Validate Points and Lines."
                        : "Run Structure Check, Georeference Check, and Dimension Check successfully before starting Validate Points and Lines.";
            return WorkflowScriptExecutionResult.Failed(StatusText);
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        RestoreExtractionDecisionGate(layout);
        if (!forceReprocess && File.Exists(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json")))
        {
            var existingArtifact = new AvailableArtifact("extraction_review_data.json", Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"));
            UpsertAvailableArtifact(existingArtifact);
            RefreshExtractionDecisionGate(layout);
            if (CurrentState != WorkflowState.ReviewPending)
            {
                SetWorkflowState(layout, WorkflowState.ReviewPending);
            }

            StatusText = extractionDecisionGateResult.RequiresDecision
                ? extractionDecisionGateResult.GuidanceText
                : "Validate Points and Lines is ready in Points and Lines Validation Tool.";
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
        StatusText = "Validate Points and Lines preparation is running: generating draft parcel review data.";
        var attemptStartedAt = DateTimeOffset.UtcNow;
        var attemptMethod = ResolveExtractionAttemptMethod(manifest);
        var nextAttemptCount = Math.Max(extractionDecisionGateState.AttemptCount + 1, 1);

        try
        {
            SetWorkflowState(layout, WorkflowState.ExtractionRunning);
            var executionResult = await workflowScriptExecutor.ExecuteDraftExtractionAsync(layout, manifest, cancellationToken).ConfigureAwait(false);
            if (!executionResult.Success)
            {
                extractionDecisionGateState = extractionDecisionGateState with
                {
                    AttemptCount = nextAttemptCount,
                    LastAttemptAt = attemptStartedAt.UtcDateTime.ToString("O"),
                    LastMethod = attemptMethod,
                    LastRoute = "reprocess_extraction",
                    LastQualityStatus = "failed"
                };
                extractionDecisionGateService.SaveState(layout, extractionDecisionGateState);
                workflowLifecycleAuditService.Record(
                    layout,
                    TransactionId,
                    "point_review_extraction_attempt",
                    "failed",
                    null,
                    $"Attempt {nextAttemptCount} failed.",
                    manifest.Payload.InnolaTransaction?.TaskId,
                    manifest.Payload.InnolaTransaction?.TransactionNumber,
                    "extraction_failed");
                SetWorkflowState(layout, WorkflowState.ExtractionFailed);
                StatusText = executionResult.ErrorMessage ?? "Draft extraction failed.";
                return executionResult;
            }

            foreach (var artifact in executionResult.Artifacts)
            {
                UpsertAvailableArtifact(artifact);
            }

            RefreshExtractionDecisionGate(layout);
            var weakAttemptCount = extractionDecisionGateResult.RequiresDecision
                ? extractionDecisionGateState.WeakAttemptCount + 1
                : 0;
            extractionDecisionGateState = extractionDecisionGateState with
            {
                AttemptCount = nextAttemptCount,
                WeakAttemptCount = weakAttemptCount,
                LastAttemptAt = attemptStartedAt.UtcDateTime.ToString("O"),
                LastMethod = attemptMethod,
                LastRoute = extractionDecisionGateResult.RequiresDecision ? "decision_gate" : "jamaica_cogo_tool",
                LastQualityStatus = extractionDecisionGateResult.QualityStatus,
                Notes = extractionDecisionGateResult.Issues.Concat(extractionDecisionGateResult.Warnings).ToArray()
            };
            extractionDecisionGateService.SaveState(layout, extractionDecisionGateState);
            RefreshExtractionDecisionGate(layout);
            workflowLifecycleAuditService.Record(
                layout,
                TransactionId,
                "point_review_extraction_attempt",
                extractionDecisionGateResult.QualityStatus,
                null,
                $"Attempt {nextAttemptCount}: {extractionDecisionGateResult.SummaryText}",
                manifest.Payload.InnolaTransaction?.TaskId,
                manifest.Payload.InnolaTransaction?.TransactionNumber,
                extractionDecisionGateResult.RequiresDecision ? "extraction_quality_gate" : null);
            SetWorkflowState(layout, WorkflowState.ReviewPending);
            StatusText = extractionDecisionGateResult.RequiresDecision
                ? extractionDecisionGateResult.GuidanceText
                : "Validate Points and Lines is ready in Points and Lines Validation Tool.";
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

    private void RestoreExtractionDecisionGate(CaseFolderLayout layout)
    {
        extractionDecisionGateState = extractionDecisionGateService.LoadState(layout);
        RefreshExtractionDecisionGate(layout);
    }

    private void RefreshExtractionDecisionGate(CaseFolderLayout layout, ExtractionReviewDocument? loadedDocument = null)
    {
        var document = loadedDocument ?? extractionReviewService.Load(layout);
        extractionDecisionGateResult = extractionDecisionGateService.Evaluate(document, extractionDecisionGateState);
    }

    private void RecordManualReviewDecision(CaseFolderLayout layout, string? operatorId)
    {
        extractionDecisionGateState = extractionDecisionGateState with
        {
            LastRoute = "manual_cogo_review",
            LastQualityStatus = extractionDecisionGateResult.QualityStatus,
            Notes = extractionDecisionGateResult.Issues.Concat(extractionDecisionGateResult.Warnings).ToArray()
        };
        extractionDecisionGateService.SaveState(layout, extractionDecisionGateState);
        workflowLifecycleAuditService.Record(
            layout,
            TransactionId ?? string.Empty,
            "point_review_route_decision",
            "manual_cogo_review",
            operatorId,
            "Manual review workspace selected from the extraction decision gate.",
            null,
            null,
            null);
    }

    private static string ResolveExtractionAttemptMethod(ManifestDocument manifest)
    {
        var step = manifest.Payload.ScriptPlan?.Steps.FirstOrDefault();
        if (step is null)
        {
            return "draft_extraction";
        }

        return string.IsNullOrWhiteSpace(step.Adapter)
            ? step.StepName
            : $"{step.Adapter}:{step.StepName}";
    }

    private PreflightSummaryDocument BlockManifestPreflightFailure(CaseFolderLayout layout, PreflightCheckStage stage, string? operatorId, Exception exception)
    {
        var message = "Manifest could not be read.";
        var blocker = PreflightCheck.Blocker(
            "manifest_readable",
            message,
            layout.ManifestPath,
            null,
            "Repair or recreate the Case Folder manifest.");
        var summary = new PreflightSummaryDocument(
            "1.0.0",
            TransactionId ?? string.Empty,
            stage.ToStageId(),
            $"preflight-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            operatorId,
            string.Empty,
            new PreflightSummaryPayload("blocked", new[] { blocker }, Array.Empty<PreflightCheck>(), Array.Empty<PreflightCheck>()),
            Array.Empty<string>(),
            new[] { exception.Message });

        SetStageResults(stage, summary);
        RebuildPreflightResults();
        CurrentState = WorkflowState.PreflightBlocked;
        StatusText = $"{GetStageDisplayName(stage)} blocked: {message.ToLowerInvariant()}";

        try
        {
            var summaryPath = ResolveStageSummaryPath(layout, stage);
            PreflightSummarySerializer.Write(summaryPath, summary);
            UpsertAvailableArtifact(new AvailableArtifact(stage.ToArtifactFileName(), summaryPath));
            WriteLegacyPreflightSummaryIfPossible();
        }
        catch (Exception writeException) when (IsExpectedPreflightIoFailure(writeException))
        {
            intakeIssues.Add($"Early-check summary could not be written: {writeException.Message}");
        }

        return summary;
    }

    private PreflightSummaryDocument BuildNotRunSummary(
        PreflightCheckStage stage,
        string? operatorId,
        string checkId,
        string message,
        string correction)
    {
        var blocker = PreflightCheck.Blocker(checkId, message, null, null, correction);
        return new PreflightSummaryDocument(
            "1.0.0",
            TransactionId ?? string.Empty,
            stage.ToStageId(),
            "not-run",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            operatorId,
            string.Empty,
            new PreflightSummaryPayload("blocked", new[] { blocker }, Array.Empty<PreflightCheck>(), Array.Empty<PreflightCheck>()),
            Array.Empty<string>(),
            new[] { message });
    }

    private void SetStageResults(PreflightCheckStage stage, PreflightSummaryDocument summary)
    {
        if (stage == PreflightCheckStage.DimensionCheck)
        {
            SetDimensionCheckResults(summary);
            return;
        }

        if (stage == PreflightCheckStage.GeoreferenceCheck)
        {
            SetGeoreferenceCheckResults(summary);
            return;
        }

        structureCheckBlockers.Clear();
        structureCheckWarnings.Clear();
        structureCheckPassedChecks.Clear();
        structureCheckBlockers.AddRange(summary.Payload.Blockers);
        structureCheckWarnings.AddRange(summary.Payload.Warnings);
        structureCheckPassedChecks.AddRange(summary.Payload.PassedChecks);
    }

    private void SetDimensionCheckResults(PreflightSummaryDocument summary)
    {
        dimensionCheckBlockers.Clear();
        dimensionCheckWarnings.Clear();
        dimensionCheckPassedChecks.Clear();
        dimensionCheckBlockers.AddRange(summary.Payload.Blockers);
        dimensionCheckWarnings.AddRange(summary.Payload.Warnings);
        dimensionCheckPassedChecks.AddRange(summary.Payload.PassedChecks);
    }

    private void SetGeoreferenceCheckResults(PreflightSummaryDocument summary)
    {
        georeferenceCheckBlockers.Clear();
        georeferenceCheckWarnings.Clear();
        georeferenceCheckPassedChecks.Clear();
        georeferenceCheckBlockers.AddRange(summary.Payload.Blockers);
        georeferenceCheckWarnings.AddRange(summary.Payload.Warnings);
        georeferenceCheckPassedChecks.AddRange(summary.Payload.PassedChecks);
    }

    private void ClearGeoreferenceCheckResults(CaseFolderLayout layout)
    {
        georeferenceCheckBlockers.Clear();
        georeferenceCheckWarnings.Clear();
        georeferenceCheckPassedChecks.Clear();
        availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, layout.GeoreferenceCheckSummaryPath, StringComparison.OrdinalIgnoreCase));
        if (!File.Exists(layout.GeoreferenceCheckSummaryPath))
        {
            return;
        }

        try
        {
            File.Delete(layout.GeoreferenceCheckSummaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            intakeIssues.Add($"Georeference Check summary could not be cleared: {exception.Message}");
        }
    }

    private void ClearDimensionCheckResults(CaseFolderLayout layout)
    {
        dimensionCheckBlockers.Clear();
        dimensionCheckWarnings.Clear();
        dimensionCheckPassedChecks.Clear();
        availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, layout.DimensionCheckSummaryPath, StringComparison.OrdinalIgnoreCase));
        if (!File.Exists(layout.DimensionCheckSummaryPath))
        {
            return;
        }

        try
        {
            File.Delete(layout.DimensionCheckSummaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            intakeIssues.Add($"Dimension Check summary could not be cleared: {exception.Message}");
        }
    }

    private static string ResolveStageSummaryPath(CaseFolderLayout layout, PreflightCheckStage stage) =>
        stage switch
        {
            PreflightCheckStage.DimensionCheck => layout.DimensionCheckSummaryPath,
            PreflightCheckStage.GeoreferenceCheck => layout.GeoreferenceCheckSummaryPath,
            PreflightCheckStage.StructureCheck => layout.StructureCheckSummaryPath,
            _ => layout.PreflightSummaryPath
        };

    private static string GetStageDisplayName(PreflightCheckStage stage) =>
        stage switch
        {
            PreflightCheckStage.DimensionCheck => "Dimension Check",
            PreflightCheckStage.GeoreferenceCheck => "Georeference Check",
            PreflightCheckStage.StructureCheck => "Structure Check",
            _ => "Early compute checks"
        };

    private void WriteLegacyPreflightSummaryIfPossible()
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
        {
            return;
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        var status = preflightBlockers.Count > 0 ? "blocked" : IsStructureCheckPassed() && IsGeoreferenceCheckPassed() && IsDimensionCheckPassed() ? "passed" : "blocked";
        var summary = new PreflightSummaryDocument(
            "1.0.0",
            TransactionId,
            PreflightCheckStage.Combined.ToStageId(),
            $"preflight-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"),
            null,
            string.Empty,
            new PreflightSummaryPayload(status, preflightBlockers.ToArray(), preflightWarnings.ToArray(), preflightPassedChecks.ToArray()),
            preflightWarnings.Select(warning => warning.Message).ToArray(),
            preflightBlockers.Select(blocker => blocker.Message).ToArray());

        try
        {
            PreflightSummarySerializer.Write(layout.PreflightSummaryPath, summary);
            UpsertAvailableArtifact(new AvailableArtifact("preflight_summary.json", layout.PreflightSummaryPath));
        }
        catch (Exception exception) when (IsExpectedPreflightIoFailure(exception))
        {
            intakeIssues.Add($"Legacy preflight summary could not be written: {exception.Message}");
        }
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
        var loadedSplitSummary = false;
        loadedSplitSummary |= TryLoadStageSummary(layout.StructureCheckSummaryPath, PreflightCheckStage.StructureCheck);
        loadedSplitSummary |= TryLoadStageSummary(layout.GeoreferenceCheckSummaryPath, PreflightCheckStage.GeoreferenceCheck);
        loadedSplitSummary |= TryLoadStageSummary(layout.DimensionCheckSummaryPath, PreflightCheckStage.DimensionCheck);
        if (loadedSplitSummary)
        {
            RebuildPreflightResults();
            return;
        }

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

            structureCheckBlockers.AddRange(summary.Payload.Blockers.Where(check => !IsGeoreferenceCheck(check) && !IsDimensionCheck(check)));
            structureCheckWarnings.AddRange(summary.Payload.Warnings.Where(check => !IsGeoreferenceCheck(check) && !IsDimensionCheck(check)));
            structureCheckPassedChecks.AddRange(summary.Payload.PassedChecks.Where(check => !IsGeoreferenceCheck(check) && !IsDimensionCheck(check)));
            georeferenceCheckBlockers.AddRange(summary.Payload.Blockers.Where(IsGeoreferenceCheck));
            georeferenceCheckWarnings.AddRange(summary.Payload.Warnings.Where(IsGeoreferenceCheck));
            georeferenceCheckPassedChecks.AddRange(summary.Payload.PassedChecks.Where(IsGeoreferenceCheck));
            dimensionCheckBlockers.AddRange(summary.Payload.Blockers.Where(IsDimensionCheck));
            dimensionCheckWarnings.AddRange(summary.Payload.Warnings.Where(IsDimensionCheck));
            dimensionCheckPassedChecks.AddRange(summary.Payload.PassedChecks.Where(IsDimensionCheck));
            RebuildPreflightResults();
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

    private bool TryLoadStageSummary(string path, PreflightCheckStage stage)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var summary = PreflightSummarySerializer.Read(path);
            if (summary.Payload is null)
            {
                intakeIssues.Add($"{GetStageDisplayName(stage)} summary could not be read: payload is missing.");
                return false;
            }

            if (stage == PreflightCheckStage.DimensionCheck && ContainsGeoreferenceResults(summary))
            {
                MigrateLegacyDimensionSummaryWithGeoreferenceRows(summary);
                return true;
            }

            SetStageResults(stage, summary);
            return true;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            intakeIssues.Add($"{GetStageDisplayName(stage)} summary could not be read: {exception.Message}");
            return false;
        }
    }

    private static bool IsGeoreferenceCheck(PreflightCheck check) =>
        string.Equals(check.Category, "georeference", StringComparison.OrdinalIgnoreCase);

    private static bool IsDimensionCheck(PreflightCheck check) =>
        string.Equals(check.Category, "dimension", StringComparison.OrdinalIgnoreCase);

    private void MigrateLegacyDimensionSummaryWithGeoreferenceRows(PreflightSummaryDocument summary)
    {
        var georeferenceSummary = FilterStageSummary(summary, PreflightCheckStage.GeoreferenceCheck, IsGeoreferenceCheck);
        if (HasAnyFindings(georeferenceSummary))
        {
            SetGeoreferenceCheckResults(georeferenceSummary);
        }

        var dimensionSummary = FilterStageSummary(summary, PreflightCheckStage.DimensionCheck, IsDimensionCheck);
        SetDimensionCheckResults(dimensionSummary);
        if (!HasAnyFindings(dimensionSummary))
        {
            intakeIssues.Add("Legacy Dimension Check summary contained georeference results. Rerun Dimension Check before Validate Points and Lines.");
        }
    }

    private static bool ContainsGeoreferenceResults(PreflightSummaryDocument summary) =>
        summary.Payload.Blockers.Any(IsGeoreferenceCheck)
        || summary.Payload.Warnings.Any(IsGeoreferenceCheck)
        || summary.Payload.PassedChecks.Any(IsGeoreferenceCheck);

    private static bool HasAnyFindings(PreflightSummaryDocument summary) =>
        summary.Payload.Blockers.Count > 0
        || summary.Payload.Warnings.Count > 0
        || summary.Payload.PassedChecks.Count > 0;

    private static PreflightSummaryDocument FilterStageSummary(
        PreflightSummaryDocument summary,
        PreflightCheckStage stage,
        Func<PreflightCheck, bool> predicate)
    {
        var blockers = summary.Payload.Blockers.Where(predicate).ToArray();
        var warnings = summary.Payload.Warnings.Where(predicate).ToArray();
        var passed = summary.Payload.PassedChecks.Where(predicate).ToArray();
        var status = blockers.Length > 0 ? "blocked" : passed.Length > 0 ? "passed" : "not_started";

        return summary with
        {
            StageId = stage.ToStageId(),
            Payload = new PreflightSummaryPayload(status, blockers, warnings, passed)
        };
    }

    private void UpsertAvailableArtifact(AvailableArtifact artifact)
    {
        if (availableArtifacts.Any(existing => string.Equals(existing.Path, artifact.Path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        availableArtifacts.Add(artifact);
    }

    private bool HasExtractionReviewArtifactForCurrentCase()
    {
        if (string.IsNullOrWhiteSpace(CaseFolderPath))
        {
            return false;
        }

        var layout = CaseFolderLayout.FromRootDirectory(CaseFolderPath);
        return File.Exists(Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"))
            || File.Exists(Path.Combine(layout.WorkingDirectory, "approved_review.json"));
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
        RemoveSpatialReviewArtifacts(layout);
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
            and not WorkflowState.OutputCreated
            and not WorkflowState.SpatialReviewPending
            and not WorkflowState.SpatialReviewApproved)
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
        if (reopenedState is not WorkflowState.OutputCreated
            and not WorkflowState.SpatialReviewPending
            and not WorkflowState.SpatialReviewApproved)
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

    private void RestoreSpatialReviewState(CaseFolderLayout layout, WorkflowState reopenedState)
    {
        if (reopenedState == WorkflowState.OutputCreated)
        {
            SetWorkflowState(layout, WorkflowState.SpatialReviewPending);
            CurrentState = WorkflowState.SpatialReviewPending;
            return;
        }

        if (reopenedState is not WorkflowState.SpatialReviewPending and not WorkflowState.SpatialReviewApproved)
        {
            return;
        }

        var approvalPath = spatialReviewApprovalPersistenceService.GetApprovalPath(layout);
        if (File.Exists(approvalPath))
        {
            UpsertAvailableArtifact(new AvailableArtifact(spatialReviewApprovalPersistenceService.ApprovalArtifactFileName, approvalPath));
        }

        if (reopenedState != WorkflowState.SpatialReviewApproved)
        {
            return;
        }

        var validation = spatialReviewApprovalPersistenceService.ValidateCurrent(layout, currentOutputSummary);
        if (validation.IsCurrent)
        {
            return;
        }

        RemoveSpatialReviewArtifacts(layout);
        intakeIssues.Add(validation.ErrorMessage ?? "Spatial review approval could not be verified.");
        SetWorkflowState(layout, WorkflowState.SpatialReviewPending);
        CurrentState = WorkflowState.SpatialReviewPending;
    }

    private void RemoveSpatialReviewArtifacts(CaseFolderLayout layout)
    {
        var approvalPath = spatialReviewApprovalPersistenceService.GetApprovalPath(layout);
        RemoveGeneratedArtifact(approvalPath, "Spatial review approval");
        RemoveGeneratedArtifact(
            computeReviewDispositionPersistenceService.GetDispositionPath(layout),
            "Compute review disposition");
        RemoveGeneratedArtifact(
            Path.Combine(layout.WorkingDirectory, "enterprise_working_disposition.json"),
            "Enterprise working disposition");
        RemoveGeneratedArtifact(
            Path.Combine(layout.ReportsDirectory, "compute_examination_report.json"),
            "Compute examination report");
    }

    private void RemoveGeneratedArtifact(string artifactPath, string displayName)
    {
        availableArtifacts.RemoveAll(artifact => string.Equals(artifact.Path, artifactPath, StringComparison.OrdinalIgnoreCase));
        if (!File.Exists(artifactPath))
        {
            return;
        }

        try
        {
            File.Delete(artifactPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            intakeIssues.Add($"{displayName} artifact could not be cleared: {exception.Message}");
        }
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
        structureCheckBlockers.Clear();
        structureCheckWarnings.Clear();
        structureCheckPassedChecks.Clear();
        georeferenceCheckBlockers.Clear();
        georeferenceCheckWarnings.Clear();
        georeferenceCheckPassedChecks.Clear();
        dimensionCheckBlockers.Clear();
        dimensionCheckWarnings.Clear();
        dimensionCheckPassedChecks.Clear();
    }

    private void RebuildPreflightResults()
    {
        preflightBlockers.Clear();
        preflightWarnings.Clear();
        preflightPassedChecks.Clear();
        preflightBlockers.AddRange(structureCheckBlockers);
        preflightBlockers.AddRange(georeferenceCheckBlockers);
        preflightBlockers.AddRange(dimensionCheckBlockers);
        preflightWarnings.AddRange(structureCheckWarnings);
        preflightWarnings.AddRange(georeferenceCheckWarnings);
        preflightWarnings.AddRange(dimensionCheckWarnings);
        preflightPassedChecks.AddRange(structureCheckPassedChecks);
        preflightPassedChecks.AddRange(georeferenceCheckPassedChecks);
        preflightPassedChecks.AddRange(dimensionCheckPassedChecks);
    }

    private bool IsStructureCheckPassed() =>
        HasRealPassedCheck(structureCheckPassedChecks) && structureCheckBlockers.Count == 0;

    private bool IsGeoreferenceCheckPassed() =>
        HasRealPassedCheck(georeferenceCheckPassedChecks) && georeferenceCheckBlockers.Count == 0;

    private bool IsDimensionCheckPassed() =>
        HasRealPassedCheck(dimensionCheckPassedChecks) && dimensionCheckBlockers.Count == 0;

    private static bool HasRealPassedCheck(IEnumerable<PreflightCheck> checks)
    {
        return checks.Any(check =>
            string.Equals(check.Status, "passed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(check.Outcome ?? "passed", "passed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(check.Severity, "disabled", StringComparison.OrdinalIgnoreCase));
    }
}
