using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Diagnostics;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Intake;
using ParcelWorkflowAddIn.WorkflowRules;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionLoadService
{
    private readonly InnolaSessionManager sessionManager;
    private readonly IInnolaTransactionDetailService detailService;
    private readonly CaseFolderStore caseFolderStore;
    private readonly AttachmentSourceFileWriter attachmentWriter;
    private readonly SourceInputProfileDetector profileDetector;
    private readonly WorkflowRuleResolver workflowRuleResolver;
    private readonly Func<WorkflowRuleSettings> getWorkflowRuleSettings;
    private readonly CaseResumePackageService resumePackageService;
    private readonly Func<string> getOutputRoot;
    private readonly Func<DateTimeOffset> getUtcNow;

    public InnolaTransactionLoadService(
        InnolaSessionManager sessionManager,
        IInnolaTransactionDetailService detailService,
        CaseFolderStore caseFolderStore,
        AttachmentSourceFileWriter attachmentWriter,
        SourceInputProfileDetector profileDetector,
        Func<string> getOutputRoot,
        Func<DateTimeOffset>? getUtcNow = null)
        : this(
            sessionManager,
            detailService,
            caseFolderStore,
            attachmentWriter,
            profileDetector,
            new WorkflowRuleResolver(),
            WorkflowRuleSettingsLoader.Load,
            new CaseResumePackageService(),
            getOutputRoot,
            getUtcNow)
    {
    }

    public InnolaTransactionLoadService(
        InnolaSessionManager sessionManager,
        IInnolaTransactionDetailService detailService,
        CaseFolderStore caseFolderStore,
        AttachmentSourceFileWriter attachmentWriter,
        SourceInputProfileDetector profileDetector,
        WorkflowRuleResolver workflowRuleResolver,
        Func<WorkflowRuleSettings> getWorkflowRuleSettings,
        CaseResumePackageService resumePackageService,
        Func<string> getOutputRoot,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.sessionManager = sessionManager;
        this.detailService = detailService;
        this.caseFolderStore = caseFolderStore;
        this.attachmentWriter = attachmentWriter;
        this.profileDetector = profileDetector;
        this.workflowRuleResolver = workflowRuleResolver;
        this.getWorkflowRuleSettings = getWorkflowRuleSettings;
        this.resumePackageService = resumePackageService;
        this.getOutputRoot = getOutputRoot;
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<InnolaTransactionLoadResult> LoadSelectedTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionManager.IsLoggedIn || sessionManager.CurrentSession is null)
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure("Log in before loading a transaction.");
        }

        if (sessionManager.SelectedTransaction is null)
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure("Select a transaction before loading.");
        }

        var session = sessionManager.CurrentSession;
        var selected = sessionManager.SelectedTransaction;
        InnolaTransactionDetailResult detailResult;
        try
        {
            detailResult = await detailService.GetTransactionDetailAsync(session, selected, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure("Could not load transaction. Try again.");
        }

        if (!detailResult.Success || detailResult.Detail is null)
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure(SafeRetryMessage(detailResult.ErrorMessage));
        }

        var detail = detailResult.Detail;
        if (!MatchesSelectedTransaction(selected, detail))
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure("Loaded transaction details did not match the selected transaction.");
        }

        var resumeAttachments = detail.Attachments
            .Where(attachment => InnolaResumePackageConventions.IsResumePackageAttachment(attachment, detail.TransactionNumber))
            .ToArray();
        var resumeAttachment = await ResolveLatestResumeAttachmentAsync(session, selected, detail, resumeAttachments, cancellationToken);
        var sourceAttachments = detail.Attachments
            .Where(attachment => !InnolaResumePackageConventions.IsResumePackageAttachment(attachment, detail.TransactionNumber))
            .ToArray();
        if ((sourceAttachments.Length == 0 && resumeAttachment is null)
            || sourceAttachments.Any(attachment => attachment.IsRequired && string.IsNullOrWhiteSpace(attachment.AttachmentId)))
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure("Transaction attachments are incomplete. Try again after Innola metadata is refreshed.");
        }

        var outputRoot = getOutputRoot();
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure("Case Folder output location is not configured.");
        }

        ResumePackageManifest? restoredResumeManifest = null;
        if (resumeAttachment is not null)
        {
            var restored = await RestoreCaseFolderFromResumePackageAsync(session, selected, detail, resumeAttachment, outputRoot, cancellationToken);
            if (!restored.Success)
            {
                sessionManager.ClearLoadedTransaction();
                return InnolaTransactionLoadResult.Failure(restored.ErrorMessage ?? "Saved resume package could not be restored.");
            }

            restoredResumeManifest = restored.Manifest;
        }

        var caseFolderResult = CreateOrReopenCaseFolder(outputRoot, detail.TransactionNumber, session.User.Username, detail);
        if (!caseFolderResult.Success || caseFolderResult.Layout is null || caseFolderResult.Manifest is null)
        {
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure(caseFolderResult.ErrorMessage ?? "Case Folder could not be prepared.");
        }

        var layout = caseFolderResult.Layout;
        var manifest = caseFolderResult.Manifest;
        var sourceFiles = manifest.Payload.SourceFiles.ToList();
        var provenance = (manifest.Payload.AttachmentProvenance ?? Array.Empty<ManifestAttachmentProvenance>()).ToList();
        var loadedAt = getUtcNow().UtcDateTime.ToString("O");
        var newlyWrittenFiles = new List<string>();

        foreach (var attachment in sourceAttachments)
        {
            if (provenance.Any(existing => existing.AttachmentId.Equals(attachment.AttachmentId, StringComparison.OrdinalIgnoreCase)
                && File.Exists(existing.CopiedPath)))
            {
                continue;
            }

            InnolaAttachmentContentResult content;
            try
            {
                content = await detailService.GetAttachmentContentAsync(session, detail, attachment, cancellationToken);
            }
            catch (Exception exception) when (IsExpectedAdapterFailure(exception))
            {
                CleanupNewlyWrittenFiles(newlyWrittenFiles);
                sessionManager.ClearLoadedTransaction();
                return InnolaTransactionLoadResult.Failure("Could not load transaction. Try again.");
            }

            if (!content.Success)
            {
                CleanupNewlyWrittenFiles(newlyWrittenFiles);
                sessionManager.ClearLoadedTransaction();
                return InnolaTransactionLoadResult.Failure(SafeRetryMessage(content.ErrorMessage));
            }

            var serviceReference = $"innola-attachment:{attachment.AttachmentId}";
            var written = attachmentWriter.Write(layout, serviceReference, attachment.FileName, content.Content, attachment.SourceRole);
            if (!written.Success || written.ManifestSourceFile is null)
            {
                CleanupNewlyWrittenFiles(newlyWrittenFiles);
                sessionManager.ClearLoadedTransaction();
                return InnolaTransactionLoadResult.Failure(written.ErrorMessage ?? "Attachment could not be copied to the Case Folder.");
            }

            newlyWrittenFiles.Add(written.ManifestSourceFile.CopiedPath);
            sourceFiles.Add(written.ManifestSourceFile);
            provenance.Add(new ManifestAttachmentProvenance(
                attachment.AttachmentId,
                attachment.FileName,
                NormalizeExtension(attachment),
                attachment.MimeType,
                attachment.SourceRole,
                attachment.Category,
                attachment.Size,
                attachment.Checksum,
                serviceReference,
                written.ManifestSourceFile.CopiedPath,
                written.CopiedAt ?? loadedAt));
        }

        var detectedProfile = profileDetector.Detect(sourceFiles);
        var ruleResolution = workflowRuleResolver.Resolve(new WorkflowRuleResolutionContext(
            detail.CaseType,
            detail.ProcessStep,
            detectedProfile,
            sourceFiles,
            getWorkflowRuleSettings()));
        var updatedManifest = manifest with
        {
            Payload = manifest.Payload with
            {
                WorkflowState = resumeAttachment is null ? "intake" : manifest.Payload.WorkflowState,
                SourceFiles = sourceFiles,
                DetectedProfile = resumeAttachment is null ? detectedProfile : manifest.Payload.DetectedProfile ?? detectedProfile,
                InnolaTransaction = new ManifestInnolaTransaction(
                    detail.TransactionId,
                    detail.TransactionNumber,
                    detail.TaskId,
                    detail.TaskName,
                    detail.ProcessStep,
                    detail.CaseType,
                    detail.ProfileHint,
                    session.User.Username,
                    detail.AssignedUser,
                    detail.AssignedGroup,
                    detail.OwnerUser,
                    detail.ClaimStatus,
                    loadedAt),
                AttachmentProvenance = provenance,
                WorkflowProfile = ruleResolution.ScriptPlan?.WorkflowProfile,
                WorkflowRuleId = ruleResolution.ScriptPlan?.RuleId,
                WorkflowRuleVersion = ruleResolution.ScriptPlan?.RuleVersion,
                ScriptPlan = ruleResolution.ScriptPlan
            }
        };

        try
        {
            ManifestSerializer.Write(layout.ManifestPath, updatedManifest);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            CleanupNewlyWrittenFiles(newlyWrittenFiles);
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Failure($"Case Folder manifest could not be updated: {exception.Message}");
        }

        var wasRestoredFromResumePackage = resumeAttachment is not null;
        sessionManager.MarkTransactionLoaded(detail.TransactionNumber, layout.RootDirectory, loadedAt, wasRestoredFromResumePackage, restoredResumeManifest?.SavedAt);
        var loadModePrefix = resumeAttachment is null ? "Opened new case for" : "Restored from saved case for";
        var status = ruleResolution.Success
            ? $"{loadModePrefix} {detail.TransactionNumber} into Case Folder with workflow rule {ruleResolution.ScriptPlan!.RuleId}: {layout.RootDirectory}"
            : $"{loadModePrefix} {detail.TransactionNumber} into Case Folder. {ruleResolution.ErrorMessage}";
        return InnolaTransactionLoadResult.Succeeded(layout, detectedProfile, wasRestoredFromResumePackage, status);
    }

    private async Task<ResumePackageRestoreResult> RestoreCaseFolderFromResumePackageAsync(
        InnolaSession session,
        SelectedInnolaTransaction selected,
        InnolaTransactionDetail detail,
        InnolaAttachmentMetadata resumeAttachment,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        InnolaAttachmentContentResult content;
        try
        {
            content = await detailService.GetAttachmentContentAsync(session, detail, resumeAttachment, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            return ResumePackageRestoreResult.Failed("Saved resume package could not be downloaded. Try again.");
        }

        if (!content.Success)
        {
            return ResumePackageRestoreResult.Failed(SafeRetryMessage(content.ErrorMessage));
        }

        return resumePackageService.Restore(outputRoot, selected, content.Content);
    }

    private async Task<InnolaAttachmentMetadata?> ResolveLatestResumeAttachmentAsync(
        InnolaSession session,
        SelectedInnolaTransaction selected,
        InnolaTransactionDetail detail,
        IReadOnlyList<InnolaAttachmentMetadata> resumeAttachments,
        CancellationToken cancellationToken)
    {
        if (resumeAttachments.Count == 0)
        {
            return null;
        }

        if (resumeAttachments.Count == 1)
        {
            return resumeAttachments[0];
        }

        var rankedCandidates = new List<(InnolaAttachmentMetadata Attachment, DateTimeOffset SavedAt)>();
        foreach (var attachment in resumeAttachments)
        {
            try
            {
                var content = await detailService.GetAttachmentContentAsync(session, detail, attachment, cancellationToken);
                if (!content.Success || content.Content.Length == 0)
                {
                    Debug.WriteLine(
                        $"Innola resume package candidate could not be read. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; Error={content.ErrorCode ?? "(none)"}.");
                    continue;
                }

                if (!TryReadResumeManifest(content.Content, out var resumeManifest))
                {
                    Debug.WriteLine(
                        $"Innola resume package candidate manifest unreadable. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}.");
                    continue;
                }

                if (!resumeManifest.TransactionNumber.Equals(selected.TransactionNumber, StringComparison.OrdinalIgnoreCase)
                    || !resumeManifest.TaskId.Equals(selected.TaskId, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine(
                        $"Innola resume package candidate skipped due to transaction mismatch. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; ManifestTransaction={resumeManifest.TransactionNumber}; ManifestTask={resumeManifest.TaskId}.");
                    continue;
                }

                if (!DateTimeOffset.TryParse(resumeManifest.SavedAt, out var savedAt))
                {
                    Debug.WriteLine(
                        $"Innola resume package candidate has invalid saved_at. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; SavedAt={resumeManifest.SavedAt}.");
                    continue;
                }

                rankedCandidates.Add((attachment, savedAt));
            }
            catch (Exception exception) when (exception is HttpRequestException
                or IOException
                or InvalidOperationException
                or NotSupportedException
                or UnauthorizedAccessException
                or TaskCanceledException)
            {
                Debug.WriteLine(
                    $"Innola resume package candidate lookup failed. TransactionNumber={detail.TransactionNumber}; Attachment={attachment.FileName}; Error={exception.GetType().Name}.");
            }
        }

        if (rankedCandidates.Count == 0)
        {
            Debug.WriteLine(
                $"Innola resume package selection fell back to first attachment. TransactionNumber={detail.TransactionNumber}; CandidateCount={resumeAttachments.Count}.");
            return resumeAttachments[0];
        }

        var selectedAttachment = rankedCandidates
            .OrderByDescending(candidate => candidate.SavedAt)
            .ThenByDescending(candidate => candidate.Attachment.AttachmentId, StringComparer.OrdinalIgnoreCase)
            .First()
            .Attachment;
        Debug.WriteLine(
            $"Innola resume package selected latest saved state. TransactionNumber={detail.TransactionNumber}; CandidateCount={rankedCandidates.Count}; Attachment={selectedAttachment.FileName}; AttachmentId={selectedAttachment.AttachmentId}.");
        return selectedAttachment;
    }

    private CaseFolderPreparationResult CreateOrReopenCaseFolder(
        string outputRoot,
        string transactionNumber,
        string username,
        InnolaTransactionDetail detail)
    {
        try
        {
            var fullOutputRoot = Path.GetFullPath(outputRoot);
            var layout = CaseFolderLayout.For(fullOutputRoot, transactionNumber);
            if (Directory.Exists(layout.RootDirectory))
            {
                var reopen = caseFolderStore.ReopenCaseFolder(layout.RootDirectory);
                if (!reopen.Success || reopen.Layout is null || reopen.Manifest is null)
                {
                    return CaseFolderPreparationResult.Failed("Existing Case Folder could not be reopened.");
                }

                if (!ExistingManifestMatches(reopen.Manifest, detail))
                {
                    return CaseFolderPreparationResult.Failed("Existing Case Folder belongs to a different Innola transaction.");
                }

                return CaseFolderPreparationResult.Prepared(reopen.Layout, reopen.Manifest);
            }

            var created = caseFolderStore.CreateCase(fullOutputRoot, transactionNumber, username);
            if (!created.Success || created.Layout is null)
            {
                return CaseFolderPreparationResult.Failed(created.ErrorMessage ?? "Case Folder could not be created.");
            }

            return CaseFolderPreparationResult.Prepared(created.Layout, ManifestSerializer.Read(created.Layout.ManifestPath));
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return CaseFolderPreparationResult.Failed($"Case Folder could not be prepared: {exception.Message}");
        }
    }

    private static bool ExistingManifestMatches(ManifestDocument manifest, InnolaTransactionDetail detail)
    {
        if (!manifest.TransactionId.Equals(detail.TransactionNumber, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var existing = manifest.Payload.InnolaTransaction;
        return existing is null
            || (existing.TaskId.Equals(detail.TaskId, StringComparison.OrdinalIgnoreCase)
                && existing.TransactionNumber.Equals(detail.TransactionNumber, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesSelectedTransaction(SelectedInnolaTransaction selected, InnolaTransactionDetail detail)
    {
        return selected.TaskId.Equals(detail.TaskId, StringComparison.OrdinalIgnoreCase)
            && selected.TransactionNumber.Equals(detail.TransactionNumber, StringComparison.OrdinalIgnoreCase)
            && selected.ProcessStep.Equals(detail.ProcessStep, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(InnolaAttachmentMetadata attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.Extension))
        {
            return attachment.Extension.StartsWith(".", StringComparison.Ordinal)
                ? attachment.Extension.ToLowerInvariant()
                : $".{attachment.Extension.ToLowerInvariant()}";
        }

        return Path.GetExtension(attachment.FileName).ToLowerInvariant();
    }

    private static string SafeRetryMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || message.Contains("{", StringComparison.Ordinal)
            || message.Contains("}", StringComparison.Ordinal))
        {
            return "Could not load transaction. Try again.";
        }

        return message;
    }

    private static bool IsExpectedAdapterFailure(Exception exception)
    {
        return exception is HttpRequestException
            or IOException
            or InvalidOperationException
            or NotSupportedException
            or UnauthorizedAccessException
            or TaskCanceledException;
    }

    private static void CleanupNewlyWrittenFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or ArgumentException)
            {
                // Best-effort cleanup. The load still fails and the manifest is not advanced.
            }
        }
    }

    private static bool TryReadResumeManifest(byte[] packageContent, out ResumePackageManifest manifest)
    {
        manifest = null!;

        try
        {
            using var archive = new ZipArchive(new MemoryStream(packageContent, writable: false), ZipArchiveMode.Read, leaveOpen: false);
            var resumeManifestEntry = archive.GetEntry(CaseResumePackageService.ResumeManifestFileName);
            if (resumeManifestEntry is null)
            {
                return false;
            }

            using var entryStream = resumeManifestEntry.Open();
            var parsed = System.Text.Json.JsonSerializer.Deserialize(entryStream, ResumeManifestJsonContext.Default.ResumePackageManifest);
            if (parsed is null)
            {
                return false;
            }

            manifest = parsed;
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException
            or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private sealed record CaseFolderPreparationResult(
        bool Success,
        CaseFolderLayout? Layout,
        ManifestDocument? Manifest,
        string? ErrorMessage)
    {
        public static CaseFolderPreparationResult Prepared(CaseFolderLayout layout, ManifestDocument manifest)
        {
            return new CaseFolderPreparationResult(true, layout, manifest, null);
        }

        public static CaseFolderPreparationResult Failed(string errorMessage)
        {
            return new CaseFolderPreparationResult(false, null, null, errorMessage);
        }
    }
}
