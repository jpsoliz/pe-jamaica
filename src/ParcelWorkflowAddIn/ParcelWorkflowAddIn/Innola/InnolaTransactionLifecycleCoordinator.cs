using System.IO;
using System.Net.Http;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;
using ParcelWorkflowAddIn.Workflow.Disposition;
using ParcelWorkflowAddIn.Workflow.Output;
using ParcelWorkflowAddIn.Workflow.Reports;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionLifecycleCoordinator
{
    private readonly InnolaSessionManager sessionManager;
    private readonly IInnolaTransactionDetailService detailService;
    private readonly IInnolaTransactionLifecycleService lifecycleService;
    private readonly IInnolaSpatialUnitService spatialUnitService;
    private readonly ITransactionCompletionReadinessService readinessService;
    private readonly WorkflowLifecycleAuditService auditService;
    private readonly CaseResumePackageService resumePackageService;
    private readonly ComputeReviewDispositionPersistenceService dispositionPersistenceService;
    private readonly IComputeExaminationReportService computeExaminationReportService;
    private readonly Func<DateTimeOffset> getUtcNow;

    public InnolaTransactionLifecycleCoordinator(
        InnolaSessionManager sessionManager,
        IInnolaTransactionDetailService detailService,
        IInnolaTransactionLifecycleService lifecycleService,
        IInnolaSpatialUnitService spatialUnitService,
        ITransactionCompletionReadinessService readinessService,
        WorkflowLifecycleAuditService auditService,
        CaseResumePackageService resumePackageService,
        Func<DateTimeOffset>? getUtcNow = null,
        IComputeExaminationReportService? computeExaminationReportService = null)
    {
        this.sessionManager = sessionManager;
        this.detailService = detailService;
        this.lifecycleService = lifecycleService;
        this.spatialUnitService = spatialUnitService;
        this.readinessService = readinessService;
        this.auditService = auditService;
        this.resumePackageService = resumePackageService;
        dispositionPersistenceService = new ComputeReviewDispositionPersistenceService();
        this.computeExaminationReportService = computeExaminationReportService ?? new ComputeExaminationReportService();
        this.getUtcNow = getUtcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<InnolaTransactionLoadResult> StartOrClaimAsync(CancellationToken cancellationToken = default)
    {
        var validation = ValidateActiveTransaction();
        if (validation is not null)
        {
            return InnolaTransactionLoadResult.Failure(validation);
        }

        var snapshot = sessionManager.CaptureTransactionState();
        var now = NowString();
        try
        {
            var request = CreateRequest("claim");
            var result = await lifecycleService.ClaimAsync(request, cancellationToken);
            if (!result.Success)
            {
                var message = SafeRetryMessage(result.Message, "Could not start transaction. Try again.");
                UpdateManifestAndAudit(
                    "transaction_claim_started",
                    "failed",
                    message,
                    result.ErrorCategory ?? "service_unavailable",
                    status: "error",
                    claimedBy: null,
                    claimedDisplayName: null,
                    claimedAt: null,
                    lastSavedAt: sessionManager.LastSavedAt,
                    cancelledAt: null,
                    completionReady: false,
                    completionReadyReason: null,
                    completedBy: null,
                    completedAt: null,
                    lastErrorCategory: result.ErrorCategory);
                sessionManager.RestoreTransactionState(snapshot);
                return InnolaTransactionLoadResult.Failure(message);
            }

            var owner = result.OwnerUser ?? request.Session.User.Username;
            var ownerDisplay = result.OwnerDisplayName ?? request.Session.User.DisplayName;
            sessionManager.MarkTransactionClaimed(owner, ownerDisplay, now, "Transaction is in progress.");
            UpdateManifestAndAudit(
                "transaction_claim_started",
                "succeeded",
                result.Message ?? "Transaction is in progress.",
                null,
                status: "in_progress",
                claimedBy: owner,
                claimedDisplayName: ownerDisplay,
                claimedAt: now,
                lastSavedAt: sessionManager.LastSavedAt,
                cancelledAt: null,
                completionReady: false,
                completionReadyReason: null,
                completedBy: null,
                completedAt: null,
                lastErrorCategory: null);
            return InnolaTransactionLoadResult.Succeeded(CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!), null, sessionManager.WasRestoredFromResumePackage, "Transaction is in progress.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            sessionManager.RestoreTransactionState(snapshot);
            return InnolaTransactionLoadResult.Failure("Could not start transaction. Try again.");
        }
    }

    public async Task<InnolaTransactionLoadResult> SaveProgressAsync(CancellationToken cancellationToken = default)
    {
        var validation = ValidateActiveTransaction(requireOwner: true);
        if (validation is not null)
        {
            return InnolaTransactionLoadResult.Failure(validation);
        }

        var snapshot = sessionManager.CaptureTransactionState();
        var now = NowString();
        try
        {
            var result = await lifecycleService.SaveProgressAsync(CreateRequest("save_progress"), cancellationToken);
            if (!result.Success)
            {
                var message = SafeRetryMessage(result.Message, "Could not save progress. Try again.");
                sessionManager.MarkLifecycleError(message);
                UpdateManifestAndAudit("transaction_save_progress", "failed", message, result.ErrorCategory, "error", lastErrorCategory: result.ErrorCategory);
                return InnolaTransactionLoadResult.Failure(message);
            }

            sessionManager.MarkProgressSaved(now, "Progress saved. Transaction remains in progress.");
            UpdateManifestAndAudit("transaction_save_progress", "succeeded", "Progress saved. Transaction remains in progress.", null, "in_progress", lastSavedAt: now);
            return InnolaTransactionLoadResult.Succeeded(CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!), null, sessionManager.WasRestoredFromResumePackage, "Progress saved. Transaction remains in progress.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            sessionManager.RestoreTransactionState(snapshot);
            return InnolaTransactionLoadResult.Failure("Could not save progress. Try again.");
        }
    }

    public async Task<InnolaTransactionLoadResult> SaveAndCloseAsync(CancellationToken cancellationToken = default)
    {
        var validation = ValidateActiveTransaction(requireOwner: true);
        if (validation is not null)
        {
            return InnolaTransactionLoadResult.Failure(validation);
        }

        var snapshot = sessionManager.CaptureTransactionState();
        try
        {
            var layout = CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!);
            var uploadResult = await UploadCasePackageAsync(
                layout,
                sessionManager.SelectedTransaction!,
                InnolaResumePackageConventions.BuildResumeAttachmentFileName,
                ShellState.ResumeAttachmentSourceType,
                includeFullOutputArtifacts: false,
                cancellationToken);
            if (!uploadResult.Success)
            {
                return InnolaTransactionLoadResult.Failure(uploadResult.ErrorMessage ?? "Could not upload saved resume package. Try again.");
            }

            var lifecycleResult = await SaveProgressAsync(cancellationToken);
            if (!lifecycleResult.Success)
            {
                return lifecycleResult;
            }

            UpdateManifestAndAudit(
                "transaction_saved_for_resume",
                "succeeded",
                "Resume package uploaded and transaction saved for later resume.",
                null,
                status: "in_progress");
            sessionManager.ClearLoadedTransaction();
            return InnolaTransactionLoadResult.Succeeded(layout, null, true, "Suspended. Resume package uploaded and case is ready to reopen later.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            sessionManager.RestoreTransactionState(snapshot);
            return InnolaTransactionLoadResult.Failure("Could not save and close transaction. Try again.");
        }
    }

    public InnolaTransactionLoadResult CancelActiveProcess()
    {
        var validation = ValidateActiveTransaction();
        if (validation is not null)
        {
            return InnolaTransactionLoadResult.Failure(validation);
        }

        var layout = CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!);
        var wasRestoredFromResumePackage = sessionManager.WasRestoredFromResumePackage;
        var now = NowString();
        UpdateManifestAndAudit("transaction_cancelled_locally", "succeeded", "Cancelled locally. Innola task was not completed.", null, "cancelled", cancelledAt: now);
        sessionManager.MarkTransactionCancelled(now, "Cancelled locally.");
        return InnolaTransactionLoadResult.Succeeded(layout, null, wasRestoredFromResumePackage, "Cancelled locally.");
    }

    public void RecordSwitchDecision(string action, string status, string? message = null, string? errorCategory = null)
    {
        var validation = ValidateActiveTransaction();
        if (validation is not null)
        {
            return;
        }

        UpdateManifestAndAudit(
            action,
            status,
            message,
            errorCategory,
            status: LifecycleStatusForManifest(),
            lastErrorCategory: errorCategory);
    }

    public async Task<InnolaTransactionLoadResult> CompleteAsync(CancellationToken cancellationToken = default)
    {
        var validation = ValidateActiveTransaction(requireOwner: true);
        if (validation is not null)
        {
            return InnolaTransactionLoadResult.Failure(validation);
        }

        var layout = CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!);
        var readiness = readinessService.CheckReadiness(layout.RootDirectory);
        UpdateManifestAndAudit(
            "transaction_completion_readiness_checked",
            readiness.IsReady ? "passed" : "blocked",
            readiness.Message,
            readiness.IsReady ? null : readiness.Reason,
            readiness.IsReady ? LifecycleStatusForManifest() : "complete_blocked",
            completionReady: readiness.IsReady,
            completionReadyReason: readiness.Reason,
            lastErrorCategory: readiness.IsReady ? null : readiness.Reason);

        if (!readiness.IsReady)
        {
            sessionManager.MarkCompletionBlocked(readiness.Reason, readiness.Message);
            return InnolaTransactionLoadResult.Failure(readiness.Message);
        }

        var snapshot = sessionManager.CaptureTransactionState();
        var now = NowString();
        try
        {
            var disposition = dispositionPersistenceService.Load(layout);
            if (disposition is not null)
            {
                var plannedPackageFileName = InnolaResumePackageConventions.BuildCompletedAttachmentFileName(sessionManager.SelectedTransaction!.TransactionNumber);
                disposition = SaveWorkingPackageState(
                    layout,
                    disposition,
                    plannedPackageFileName,
                    ShellState.CompletedAttachmentSourceType,
                    "pending");

                UpdateManifestAndAudit(
                    "compute_spatial_unit_save_started",
                    "started",
                    "Starting Innola Spatial Unit save.",
                    null,
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready",
                    spatialUnitApiStatus: "started");

                var spatialUnitResult = await spatialUnitService.CreateOrUpdateAsync(
                    sessionManager.CurrentSession!,
                    sessionManager.SelectedTransaction!,
                    layout.RootDirectory,
                    disposition,
                    cancellationToken).ConfigureAwait(false);
                if (!spatialUnitResult.Success)
                {
                    var message = SafeRetryMessage(spatialUnitResult.Message, "Could not create Spatial Unit. Try again.");
                    sessionManager.MarkLifecycleError(message);
                    UpdateManifestAndAudit(
                        "compute_spatial_unit_save_failed",
                        "failed",
                        message,
                        spatialUnitResult.ErrorCategory,
                        "error",
                        completionReady: true,
                        completionReadyReason: "ready",
                        lastErrorCategory: spatialUnitResult.ErrorCategory);
                    return InnolaTransactionLoadResult.Failure(message);
                }

                var updatedDisposition = disposition with
                {
                    SpatialUnitApiStatus = "saved",
                    SpatialUnitId = spatialUnitResult.SpatialUnitId
                };
                dispositionPersistenceService.Save(layout, updatedDisposition);
                UpdateManifestAndAudit(
                    "compute_spatial_unit_saved",
                    "succeeded",
                    spatialUnitResult.Message,
                    null,
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready",
                    spatialUnitId: spatialUnitResult.SpatialUnitId,
                    spatialUnitApiStatus: "saved");

                var enterpriseDispositionService = new JsonEnterpriseWorkingDispositionService();
                var caseIndexReferenceResult = await enterpriseDispositionService
                    .RecordSpatialUnitReferenceAsync(
                        layout,
                        ManifestSerializer.Read(layout.ManifestPath),
                        spatialUnitResult.SpatialUnitId,
                        "saved",
                        cancellationToken).ConfigureAwait(false);
                UpdateManifestAndAudit(
                    caseIndexReferenceResult.Success
                        ? "compute_spatial_unit_case_index_reference_saved"
                        : "compute_spatial_unit_case_index_reference_failed",
                    caseIndexReferenceResult.Success ? "succeeded" : "warning",
                    caseIndexReferenceResult.Success
                        ? caseIndexReferenceResult.Message
                        : string.Join("; ", caseIndexReferenceResult.Errors),
                    caseIndexReferenceResult.Success ? null : "enterprise_case_index_reference",
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready",
                    spatialUnitId: spatialUnitResult.SpatialUnitId,
                    spatialUnitApiStatus: "saved");

                UpdateManifestAndAudit(
                    "compute_spatial_unit_polygon_suid_reference_started",
                    "started",
                    "Starting Enterprise working_polygons SUID writeback using the SUID field.",
                    null,
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready",
                    spatialUnitId: spatialUnitResult.SpatialUnitId,
                    spatialUnitApiStatus: "saved");

                var polygonSuidReferenceResult = await enterpriseDispositionService
                    .RecordPolygonSpatialUnitReferencesAsync(
                        layout,
                        ManifestSerializer.Read(layout.ManifestPath),
                        spatialUnitResult.PolygonReferences,
                        cancellationToken).ConfigureAwait(false);
                UpdateManifestAndAudit(
                    polygonSuidReferenceResult.Success
                        ? "compute_spatial_unit_polygon_suid_reference_saved"
                        : "compute_spatial_unit_polygon_suid_reference_failed",
                    polygonSuidReferenceResult.Success ? "succeeded" : "warning",
                    polygonSuidReferenceResult.Success
                        ? polygonSuidReferenceResult.Message
                        : string.Join("; ", polygonSuidReferenceResult.Errors),
                    polygonSuidReferenceResult.Success ? null : "enterprise_polygon_suid_reference",
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready",
                    spatialUnitId: spatialUnitResult.SpatialUnitId,
                    spatialUnitApiStatus: "saved");

                disposition = updatedDisposition;
            }

            if (disposition is not null)
            {
                var reportResult = await computeExaminationReportService.GenerateAsync(
                    layout,
                    sessionManager.SelectedTransaction!,
                    disposition,
                    sessionManager.CurrentUser?.Username,
                    cancellationToken).ConfigureAwait(false);
                if (!reportResult.Success || string.IsNullOrWhiteSpace(reportResult.ReportPath))
                {
                    var message = SafeRetryMessage(reportResult.Message, "Could not generate Compute examination report. Try again.");
                    sessionManager.MarkLifecycleError(message);
                    UpdateManifestAndAudit(
                        "compute_examination_report_failed",
                        "failed",
                        message,
                        reportResult.ErrorCategory,
                        "error",
                        completionReady: true,
                        completionReadyReason: "ready",
                        lastErrorCategory: reportResult.ErrorCategory);
                    return InnolaTransactionLoadResult.Failure(message);
                }

                disposition = disposition with
                {
                    ComputeExaminationReportRef = reportResult.ReportPath
                };
                dispositionPersistenceService.Save(layout, disposition);
                UpdateManifestAndAudit(
                    "compute_examination_report_generated",
                    "succeeded",
                    "Compute examination report generated.",
                    null,
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready");
            }

            var packageFileName = disposition?.WorkingPackageFileName
                ?? InnolaResumePackageConventions.BuildCompletedAttachmentFileName(sessionManager.SelectedTransaction!.TransactionNumber);

            var packageUpload = await UploadCasePackageAsync(
                layout,
                sessionManager.SelectedTransaction!,
                _ => packageFileName,
                ShellState.CompletedAttachmentSourceType,
                includeFullOutputArtifacts: true,
                cancellationToken);
            if (!packageUpload.Success)
            {
                if (disposition is not null)
                {
                    disposition = SaveWorkingPackageState(
                        layout,
                        disposition,
                        packageUpload.FileName,
                        packageUpload.SourceType,
                        "failed");
                }

                var packageMessage = packageUpload.ErrorMessage ?? "Could not upload completed case package. Try again.";
                UpdateManifestAndAudit(
                    "compute_working_package_upload_failed",
                    "failed",
                    packageMessage,
                    packageUpload.ErrorCategory,
                    "error",
                    completionReady: true,
                    completionReadyReason: "ready",
                    lastErrorCategory: packageUpload.ErrorCategory,
                    spatialUnitId: disposition?.SpatialUnitId,
                    spatialUnitApiStatus: disposition?.SpatialUnitApiStatus,
                    workingPackageFileName: packageUpload.FileName,
                    workingPackageUploadStatus: "failed");
                sessionManager.MarkLifecycleError(packageMessage);
                return InnolaTransactionLoadResult.Failure(packageMessage);
            }

            if (disposition is not null)
            {
                disposition = SaveWorkingPackageState(
                    layout,
                    disposition,
                    packageUpload.FileName,
                    packageUpload.SourceType,
                    "uploaded");
                UpdateManifestAndAudit(
                    "compute_working_package_uploaded",
                    "succeeded",
                    "Compute working package uploaded.",
                    null,
                    LifecycleStatusForManifest(),
                    completionReady: true,
                    completionReadyReason: "ready",
                    spatialUnitId: disposition.SpatialUnitId,
                    spatialUnitApiStatus: disposition.SpatialUnitApiStatus,
                    workingPackageFileName: disposition.WorkingPackageFileName,
                    workingPackageUploadStatus: disposition.WorkingPackageUploadStatus);
            }

            var result = await lifecycleService.CompleteAsync(CreateRequest("complete"), cancellationToken);
            if (!result.Success)
            {
                var message = SafeRetryMessage(result.Message, "Could not complete transaction. Try again.");
                sessionManager.MarkLifecycleError(message);
                UpdateManifestAndAudit("transaction_complete_failed", "failed", message, result.ErrorCategory, "error", completionReady: true, completionReadyReason: "ready", lastErrorCategory: result.ErrorCategory);
                return InnolaTransactionLoadResult.Failure(message);
            }

            var completedBy = sessionManager.CurrentUser!.Username;
            var wasRestoredFromResumePackage = sessionManager.WasRestoredFromResumePackage;
            UpdateManifestAndAudit("transaction_complete_succeeded", "succeeded", "Completed. Final package uploaded and transaction closed.", null, "completed", completionReady: true, completionReadyReason: "ready", completedBy: completedBy, completedAt: now);
            sessionManager.MarkTransactionCompleted(now, "Completed. Final package uploaded and transaction closed.");
            return InnolaTransactionLoadResult.Succeeded(layout, null, wasRestoredFromResumePackage, "Completed. Final package uploaded and transaction closed.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            sessionManager.RestoreTransactionState(snapshot);
            return InnolaTransactionLoadResult.Failure("Could not complete transaction. Try again.");
        }
    }

    private async Task<CasePackageUploadResult> UploadCasePackageAsync(
        CaseFolderLayout layout,
        SelectedInnolaTransaction transaction,
        Func<string, string> attachmentNameBuilder,
        string sourceType,
        bool includeFullOutputArtifacts,
        CancellationToken cancellationToken)
    {
        string? packagePath = null;
        var fileName = attachmentNameBuilder(transaction.TransactionNumber);
        try
        {
            var package = resumePackageService.Build(layout, transaction, sessionManager.CurrentUser?.Username, includeFullOutputArtifacts);
            if (!package.Success || string.IsNullOrWhiteSpace(package.PackagePath) || string.IsNullOrWhiteSpace(package.ContentType))
            {
                return CasePackageUploadResult.Failure(fileName, sourceType, package.ErrorMessage ?? "Resume package could not be created.");
            }

            packagePath = package.PackagePath;
            var content = await File.ReadAllBytesAsync(package.PackagePath, cancellationToken);
            var result = await detailService.UploadAttachmentAsync(
                sessionManager.CurrentSession!,
                transaction,
                fileName,
                package.ContentType,
                content,
                sourceType,
                cancellationToken);
            return result.Success
                ? CasePackageUploadResult.Succeeded(fileName, sourceType)
                : CasePackageUploadResult.Failure(fileName, sourceType, result.ErrorMessage ?? "Could not upload case package.", result.ErrorCategory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            return CasePackageUploadResult.Failure(fileName, sourceType, "Could not package case state for upload.", exception.GetType().Name);
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(packagePath) && File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }
            }
            catch (Exception)
            {
                // Best effort cleanup.
            }
        }
    }

    private ComputeReviewDispositionDocument SaveWorkingPackageState(
        CaseFolderLayout layout,
        ComputeReviewDispositionDocument disposition,
        string fileName,
        string sourceType,
        string uploadStatus)
    {
        var updated = disposition with
        {
            WorkingPackageFileName = fileName,
            WorkingPackageSourceType = sourceType,
            WorkingPackageUploadStatus = uploadStatus
        };
        dispositionPersistenceService.Save(layout, updated);
        return updated;
    }

    private string? ValidateActiveTransaction(bool requireOwner = false)
    {
        if (!sessionManager.IsLoggedIn || sessionManager.CurrentSession is null)
        {
            return "Log in before using transaction lifecycle actions.";
        }

        if (!sessionManager.IsTransactionLoaded || sessionManager.SelectedTransaction is null || string.IsNullOrWhiteSpace(sessionManager.LoadedCaseFolderPath))
        {
            return "Load a transaction before using lifecycle actions.";
        }

        if (requireOwner && !sessionManager.CanSaveProgress)
        {
            return "Only the user who started the transaction can perform this action.";
        }

        return null;
    }

    private InnolaTransactionLifecycleRequest CreateRequest(string reason)
    {
        return new InnolaTransactionLifecycleRequest(
            sessionManager.CurrentSession!,
            sessionManager.SelectedTransaction!,
            sessionManager.LoadedCaseFolderPath!,
            LifecycleStatusForManifest(),
            reason);
    }

    private void UpdateManifestAndAudit(
        string action,
        string auditStatus,
        string? message,
        string? errorCategory,
        string? status = null,
        string? claimedBy = null,
        string? claimedDisplayName = null,
        string? claimedAt = null,
        string? lastSavedAt = null,
        string? cancelledAt = null,
        bool? completionReady = null,
        string? completionReadyReason = null,
        string? completedBy = null,
        string? completedAt = null,
        string? lastErrorCategory = null,
        string? spatialUnitId = null,
        string? spatialUnitApiStatus = null,
        string? workingPackageFileName = null,
        string? workingPackageUploadStatus = null)
    {
        var layout = CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!);
        var manifest = ManifestSerializer.Read(layout.ManifestPath);
        var current = manifest.Payload.InnolaLifecycle;
        var selected = sessionManager.SelectedTransaction!;
        var updated = new ManifestInnolaLifecycle(
            selected.TransactionId,
            selected.TransactionNumber,
            selected.TaskId,
            selected.ProcessStep,
            status ?? current?.Status ?? LifecycleStatusForManifest(),
            claimedBy ?? current?.ClaimedBy ?? sessionManager.LifecycleOwnerUser,
            claimedDisplayName ?? current?.ClaimedDisplayName ?? sessionManager.LifecycleOwnerDisplayName,
            claimedAt ?? current?.ClaimedAt ?? sessionManager.ClaimedAt,
            lastSavedAt ?? current?.LastSavedAt ?? sessionManager.LastSavedAt,
            cancelledAt ?? current?.CancelledAt ?? sessionManager.CancelledAt,
            completionReady ?? current?.CompletionReady ?? false,
            completionReadyReason ?? current?.CompletionReadyReason,
            completedBy ?? current?.CompletedBy,
            completedAt ?? current?.CompletedAt,
            lastErrorCategory ?? current?.LastErrorCategory,
            spatialUnitId ?? current?.SpatialUnitId,
            spatialUnitApiStatus ?? current?.SpatialUnitApiStatus,
            workingPackageFileName ?? current?.WorkingPackageFileName,
            workingPackageUploadStatus ?? current?.WorkingPackageUploadStatus);

        ManifestSerializer.Write(layout.ManifestPath, manifest with { Payload = manifest.Payload with { InnolaLifecycle = updated } });
        auditService.Record(
            layout,
            manifest.TransactionId,
            action,
            auditStatus,
            sessionManager.CurrentUser?.Username,
            message,
            selected.TaskId,
            selected.TransactionNumber,
            errorCategory);
    }

    private string LifecycleStatusForManifest()
    {
        return sessionManager.LifecycleStatus switch
        {
            InnolaTransactionLifecycleStatus.InProgress => "in_progress",
            InnolaTransactionLifecycleStatus.SaveProgress => "in_progress",
            InnolaTransactionLifecycleStatus.Cancelled => "cancelled",
            InnolaTransactionLifecycleStatus.CompleteBlocked => "complete_blocked",
            InnolaTransactionLifecycleStatus.Completing => "completing",
            InnolaTransactionLifecycleStatus.Completed => "completed",
            InnolaTransactionLifecycleStatus.Error => "error",
            InnolaTransactionLifecycleStatus.Loaded => "loaded",
            _ => "loaded"
        };
    }

    private string NowString()
    {
        return getUtcNow().UtcDateTime.ToString("O");
    }

    private static string SafeRetryMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || message.Contains("{", StringComparison.Ordinal)
            || message.Contains("}", StringComparison.Ordinal))
        {
            return fallback;
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

    private sealed record CasePackageUploadResult(
        bool Success,
        string FileName,
        string SourceType,
        string? ErrorMessage,
        string? ErrorCategory)
    {
        public static CasePackageUploadResult Succeeded(string fileName, string sourceType)
        {
            return new CasePackageUploadResult(true, fileName, sourceType, null, null);
        }

        public static CasePackageUploadResult Failure(string fileName, string sourceType, string errorMessage, string? errorCategory = null)
        {
            return new CasePackageUploadResult(false, fileName, sourceType, errorMessage, errorCategory);
        }
    }
}
