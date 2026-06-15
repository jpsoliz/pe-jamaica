using System.Text.RegularExpressions;
using System.IO;
using System.Text.Json;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Workflow;

namespace ParcelWorkflowAddIn.CaseFolders;

public sealed partial class CaseFolderStore
{
    private readonly Func<DateTimeOffset> getUtcNow;
    private readonly Func<string> createRunId;

    public CaseFolderStore()
        : this(() => DateTimeOffset.UtcNow, () => $"run-{Guid.NewGuid():N}")
    {
    }

    public CaseFolderStore(Func<DateTimeOffset> getUtcNow, Func<string> createRunId)
    {
        this.getUtcNow = getUtcNow;
        this.createRunId = createRunId;
    }

    public CaseFolderCreationResult CreateCase(string outputRoot, string transactionId, string? createdBy)
    {
        if (!IsValidTransactionId(transactionId))
        {
            return CaseFolderCreationResult.Failed("Transaction ID must use a safe Sidwell or Innola transaction format, such as TR-SMD-0000001, TR100000004, or 100000206.");
        }

        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            return CaseFolderCreationResult.Failed("Output location is required.");
        }

        try
        {
            var fullOutputRoot = Path.GetFullPath(outputRoot);
            var layout = CaseFolderLayout.For(fullOutputRoot, transactionId);
            var fullCaseRoot = Path.GetFullPath(layout.RootDirectory);

            if (!IsPathInside(fullOutputRoot, fullCaseRoot))
            {
                return CaseFolderCreationResult.Failed("Transaction folder must stay inside the selected output location.");
            }

            if (Directory.Exists(fullCaseRoot))
            {
                return CaseFolderCreationResult.Failed($"Case Folder already exists: {fullCaseRoot}");
            }

            Directory.CreateDirectory(fullOutputRoot);
            Directory.CreateDirectory(layout.SourceDirectory);
            Directory.CreateDirectory(layout.WorkingDirectory);
            Directory.CreateDirectory(layout.ReportsDirectory);
            Directory.CreateDirectory(layout.LogsDirectory);

            var manifest = ManifestDocument.CreateInitial(transactionId, createRunId(), getUtcNow(), createdBy);
            ManifestSerializer.Write(layout.ManifestPath, manifest);

            return CaseFolderCreationResult.Created(layout);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return CaseFolderCreationResult.Failed($"Case Folder could not be created: {exception.Message}");
        }
    }

    public CaseFolderReopenResult ReopenCaseFolder(string caseFolderPath)
    {
        if (string.IsNullOrWhiteSpace(caseFolderPath))
        {
            return CaseFolderReopenResult.Failed(new RecoverabilityIssue(
                "missing_case_folder_path",
                "blocked",
                "Case Folder path is required.",
                null,
                true));
        }

        CaseFolderLayout layout;
        try
        {
            layout = CaseFolderLayout.FromRootDirectory(caseFolderPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return CaseFolderReopenResult.Failed(new RecoverabilityIssue(
                "invalid_case_folder_path",
                "blocked",
                $"Case Folder path could not be read: {exception.Message}",
                caseFolderPath,
                true));
        }

        if (!Directory.Exists(layout.RootDirectory))
        {
            return CaseFolderReopenResult.Failed(new RecoverabilityIssue(
                "missing_case_folder",
                "blocked",
                "Case Folder does not exist.",
                layout.RootDirectory,
                true));
        }

        if (!File.Exists(layout.ManifestPath))
        {
            return CaseFolderReopenResult.Failed(new RecoverabilityIssue(
                "missing_manifest",
                "blocked",
                "Manifest could not be read.",
                layout.ManifestPath,
                true));
        }

        ManifestDocument manifest;
        try
        {
            manifest = ManifestSerializer.Read(layout.ManifestPath);
            ValidateManifest(manifest);
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or InvalidOperationException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return CaseFolderReopenResult.Failed(new RecoverabilityIssue(
                "corrupt_manifest",
                "blocked",
                $"Manifest could not be read: {exception.Message}",
                layout.ManifestPath,
                true));
        }

        var issues = new List<RecoverabilityIssue>();
        AddMissingDirectoryIssues(layout, issues);

        var sourceFiles = BuildSourceRows(manifest, issues);
        var artifacts = DiscoverAvailableArtifacts(layout);
        var resolvedState = ResolveWorkflowState(manifest.Payload.WorkflowState, issues);

        if (manifest.Payload.DetectedProfile is null)
        {
            issues.Add(new RecoverabilityIssue(
                "missing_detected_profile",
                "warning",
                "Detected profile is not refreshed. Use Refresh Intake to detect the source profile.",
                layout.ManifestPath,
                false));
        }

        return new CaseFolderReopenResult(
            true,
            layout,
            manifest,
            resolvedState,
            sourceFiles,
            artifacts,
            issues);
    }

    private static bool IsValidTransactionId(string transactionId)
    {
        return !string.IsNullOrWhiteSpace(transactionId)
            && TransactionIdPattern().IsMatch(transactionId)
            && transactionId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !transactionId.Contains("..", StringComparison.Ordinal)
            && !transactionId.Contains(Path.DirectorySeparatorChar)
            && !transactionId.Contains(Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathInside(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateManifest(ManifestDocument manifest)
    {
        if (manifest.Payload is null)
        {
            throw new InvalidOperationException("Manifest payload is missing.");
        }

        if (string.IsNullOrWhiteSpace(manifest.TransactionId))
        {
            throw new InvalidOperationException("Manifest transaction ID is missing.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Payload.WorkflowState))
        {
            throw new InvalidOperationException("Manifest workflow state is missing.");
        }

        if (manifest.Payload.SourceFiles is null)
        {
            throw new InvalidOperationException("Manifest source file list is missing.");
        }
    }

    private static void AddMissingDirectoryIssues(CaseFolderLayout layout, List<RecoverabilityIssue> issues)
    {
        foreach (var requiredDirectory in new[]
        {
            layout.SourceDirectory,
            layout.WorkingDirectory,
            layout.OutputDirectory,
            layout.ReportsDirectory,
            layout.LogsDirectory
        })
        {
            if (!Directory.Exists(requiredDirectory))
            {
                issues.Add(new RecoverabilityIssue(
                    "missing_required_directory",
                    "warning",
                    "Required Case Folder directory is missing.",
                    requiredDirectory,
                    false));
            }
        }
    }

    private static IReadOnlyList<SourceFileCopyResult> BuildSourceRows(ManifestDocument manifest, List<RecoverabilityIssue> issues)
    {
        var rows = new List<SourceFileCopyResult>();
        foreach (var source in manifest.Payload.SourceFiles)
        {
            var fileName = string.IsNullOrWhiteSpace(source.CopiedPath)
                ? Path.GetFileName(source.OriginalPath)
                : Path.GetFileName(source.CopiedPath);
            var copiedPathExists = !string.IsNullOrWhiteSpace(source.CopiedPath) && File.Exists(source.CopiedPath);

            if (!copiedPathExists)
            {
                issues.Add(new RecoverabilityIssue(
                    "missing_copied_source_file",
                    "warning",
                    "Copied source file is missing from the Case Folder.",
                    source.CopiedPath,
                    false));
            }

            rows.Add(new SourceFileCopyResult(
                source.OriginalPath,
                source.CopiedPath,
                fileName,
                source.FileType,
                source.FileSize,
                source.SourceRole,
                copiedPathExists ? "copied" : "missing",
                copiedPathExists ? "Copied source file loaded from Case Folder." : "Copied source file is missing from the Case Folder.",
                copiedPathExists));
        }

        return rows;
    }

    private static IReadOnlyList<AvailableArtifact> DiscoverAvailableArtifacts(CaseFolderLayout layout)
    {
        var candidates = new[]
        {
            layout.PreflightSummaryPath,
            Path.Combine(layout.WorkingDirectory, "extraction_review_data.json"),
            Path.Combine(layout.WorkingDirectory, "extraction_points.json"),
            Path.Combine(layout.WorkingDirectory, "normalized_points.json"),
            Path.Combine(layout.WorkingDirectory, "plan_ocr.json"),
            Path.Combine(layout.WorkingDirectory, "dwg_context.json"),
            Path.Combine(layout.WorkingDirectory, "approved_review.json"),
            Path.Combine(layout.WorkingDirectory, "validation_summary.json"),
            Path.Combine(layout.WorkingDirectory, "spatial_review_approval.json"),
            Path.Combine(layout.OutputDirectory, "output_summary.json"),
            Path.Combine(layout.LogsDirectory, "process.log"),
            Path.Combine(layout.OutputDirectory, "extracted_geometry.geojson")
        };

        return candidates
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Select(path => new AvailableArtifact(Path.GetFileName(path), path))
            .Concat(Directory.Exists(layout.OutputDirectory)
                ? Directory.GetDirectories(layout.OutputDirectory, "*.gdb", SearchOption.TopDirectoryOnly)
                    .Select(path => new AvailableArtifact(Path.GetFileName(path), path))
                : Array.Empty<AvailableArtifact>())
            .GroupBy(artifact => artifact.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static WorkflowState ResolveWorkflowState(string workflowState, List<RecoverabilityIssue> issues)
    {
        if (string.Equals(workflowState, WorkflowState.Intake.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.Intake;
        }

        if (string.Equals(workflowState, WorkflowState.PreflightBlocked.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.PreflightBlocked;
        }

        if (string.Equals(workflowState, WorkflowState.PreflightPassed.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.PreflightPassed;
        }

        if (string.Equals(workflowState, WorkflowState.ExtractionRunning.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new RecoverabilityIssue(
                "interrupted_extraction",
                "warning",
                "Case was reopened from an interrupted extraction run. Retry extraction from the workflow pane.",
                null,
                false));
            return WorkflowState.PreflightPassed;
        }

        if (string.Equals(workflowState, WorkflowState.ExtractionFailed.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.ExtractionFailed;
        }

        if (string.Equals(workflowState, WorkflowState.ReviewPending.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.ReviewPending;
        }

        if (string.Equals(workflowState, WorkflowState.ReviewApproved.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.ReviewApproved;
        }

        if (string.Equals(workflowState, WorkflowState.ValidationRunning.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new RecoverabilityIssue(
                "interrupted_validation",
                "warning",
                "Case was reopened from an interrupted validation run. Re-run validation from the workflow pane.",
                null,
                false));
            return WorkflowState.ReviewApproved;
        }

        if (string.Equals(workflowState, WorkflowState.ValidationBlocked.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.ValidationBlocked;
        }

        if (string.Equals(workflowState, WorkflowState.ValidationPassed.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.ValidationPassed;
        }

        if (string.Equals(workflowState, WorkflowState.OutputRunning.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new RecoverabilityIssue(
                "interrupted_output_generation",
                "warning",
                "Case was reopened from an interrupted output generation run. Run Outputs again from the workflow pane.",
                null,
                false));
            return WorkflowState.ValidationPassed;
        }

        if (string.Equals(workflowState, WorkflowState.OutputCreated.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.SpatialReviewPending;
        }

        if (string.Equals(workflowState, WorkflowState.SpatialReviewPending.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.SpatialReviewPending;
        }

        if (string.Equals(workflowState, WorkflowState.SpatialReviewApproved.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowState.SpatialReviewApproved;
        }

        if (string.Equals(workflowState, WorkflowState.PreflightRunning.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new RecoverabilityIssue(
                "interrupted_preflight",
                "warning",
                "Case was reopened from an interrupted preflight run. Run Preflight again.",
                null,
                false));
            return WorkflowState.Intake;
        }

        if (string.Equals(workflowState, WorkflowState.NoCase.ToContractValue(), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new RecoverabilityIssue(
                "unsupported_workflow_state",
                "warning",
                "Case uses a workflow state that cannot be resumed as an active case in this build.",
                null,
                false));
            return WorkflowState.NoCase;
        }

        issues.Add(new RecoverabilityIssue(
            "unsupported_workflow_state",
            "warning",
            $"Case uses workflow state '{workflowState}', which is not yet supported by this build. Reopened at Intake.",
            null,
            false));
        return WorkflowState.Intake;
    }

    [GeneratedRegex("^(TR-SMD-[0-9]{7}|TR[0-9]{9}|[0-9]{9})$", RegexOptions.CultureInvariant)]
    private static partial Regex TransactionIdPattern();
}
