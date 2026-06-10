using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.Preflight;
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
    private readonly List<SourceFileCopyResult> sourceFiles = [];
    private readonly List<string> intakeIssues = [];
    private readonly List<AvailableArtifact> availableArtifacts = [];
    private readonly List<PreflightCheck> preflightBlockers = [];
    private readonly List<PreflightCheck> preflightWarnings = [];
    private readonly List<PreflightCheck> preflightPassedChecks = [];

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
        : this(caseFolderStore, sourceFileCopyService, sourceInputProfileDetector, sourceFileActionService, sourceFileActionAuditService, new ManifestPreflightService())
    {
    }

    public WorkflowSession(
        CaseFolderStore caseFolderStore,
        SourceFileCopyService sourceFileCopyService,
        SourceInputProfileDetector sourceInputProfileDetector,
        SourceFileActionService sourceFileActionService,
        SourceFileActionAuditService sourceFileActionAuditService,
        ManifestPreflightService manifestPreflightService)
    {
        this.caseFolderStore = caseFolderStore;
        this.sourceFileCopyService = sourceFileCopyService;
        this.sourceInputProfileDetector = sourceInputProfileDetector;
        this.sourceFileActionService = sourceFileActionService;
        this.sourceFileActionAuditService = sourceFileActionAuditService;
        this.manifestPreflightService = manifestPreflightService;
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
        var updatedManifest = manifest with
        {
            Payload = manifest.Payload with { DetectedProfile = profile }
        };
        ManifestSerializer.Write(layout.ManifestPath, updatedManifest);
        InvalidatePreflight(layout);

        DetectedProfileLabel = profile.DisplayLabel;
        intakeIssues.Clear();
        intakeIssues.AddRange(profile.Issues);
        StatusText = profile.Status == "matched"
            ? $"Detected profile: {profile.DisplayLabel}."
            : profile.DisplayLabel;

        return profile;
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
        StatusText = result.RecoverabilityIssues.Count == 0
            ? "Case reopened"
            : "Case reopened with recoverability issues.";

        return result;
    }

    public SourceFileActionResult ExecuteSourceFileAction(SourceFileCopyResult sourceFile, SourceFileAction action, string? operatorId)
    {
        if (!CanRunIntakeCommand() || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
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
        if (!CanRunPreflight() || string.IsNullOrWhiteSpace(CaseFolderPath) || string.IsNullOrWhiteSpace(TransactionId))
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
        StatusText = "Preflight running: manifest checks.";

        try
        {
            SetWorkflowState(layout, WorkflowState.PreflightRunning);
            var summary = manifestPreflightService.Run(layout, operatorId);
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
                : "Preflight passed: manifest checks complete.";

            return summary;
        }
        catch (Exception exception) when (IsExpectedPreflightIoFailure(exception))
        {
            return BlockManifestPreflightFailure(layout, operatorId, exception);
        }
    }

    private bool CanRunIntakeCommand()
    {
        return CurrentState is WorkflowState.Intake or WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed;
    }

    private bool CanRunPreflight()
    {
        return CurrentState is WorkflowState.Intake or WorkflowState.PreflightBlocked or WorkflowState.PreflightPassed;
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

    private void ClearPreflightResults()
    {
        preflightBlockers.Clear();
        preflightWarnings.Clear();
        preflightPassedChecks.Clear();
    }
}
