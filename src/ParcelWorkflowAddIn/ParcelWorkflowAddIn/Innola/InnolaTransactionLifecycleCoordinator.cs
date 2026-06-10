using System.IO;
using System.Net.Http;
using ParcelWorkflowAddIn.CaseFolders;
using ParcelWorkflowAddIn.Contracts;

namespace ParcelWorkflowAddIn.Innola;

public sealed class InnolaTransactionLifecycleCoordinator
{
    private readonly InnolaSessionManager sessionManager;
    private readonly IInnolaTransactionLifecycleService lifecycleService;
    private readonly ITransactionCompletionReadinessService readinessService;
    private readonly WorkflowLifecycleAuditService auditService;
    private readonly Func<DateTimeOffset> getUtcNow;

    public InnolaTransactionLifecycleCoordinator(
        InnolaSessionManager sessionManager,
        IInnolaTransactionLifecycleService lifecycleService,
        ITransactionCompletionReadinessService readinessService,
        WorkflowLifecycleAuditService auditService,
        Func<DateTimeOffset>? getUtcNow = null)
    {
        this.sessionManager = sessionManager;
        this.lifecycleService = lifecycleService;
        this.readinessService = readinessService;
        this.auditService = auditService;
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
            return InnolaTransactionLoadResult.Succeeded(CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!), null, "Transaction is in progress.");
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
            return InnolaTransactionLoadResult.Succeeded(CaseFolderLayout.FromRootDirectory(sessionManager.LoadedCaseFolderPath!), null, "Progress saved. Transaction remains in progress.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            sessionManager.RestoreTransactionState(snapshot);
            return InnolaTransactionLoadResult.Failure("Could not save progress. Try again.");
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
        var now = NowString();
        UpdateManifestAndAudit("transaction_cancelled_locally", "succeeded", "Current process cancelled locally. Innola task was not completed.", null, "cancelled", cancelledAt: now);
        sessionManager.MarkTransactionCancelled(now, "Current process cancelled locally.");
        return InnolaTransactionLoadResult.Succeeded(layout, null, "Current process cancelled locally.");
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
            var result = await lifecycleService.CompleteAsync(CreateRequest("complete"), cancellationToken);
            if (!result.Success)
            {
                var message = SafeRetryMessage(result.Message, "Could not complete transaction. Try again.");
                sessionManager.MarkLifecycleError(message);
                UpdateManifestAndAudit("transaction_complete_failed", "failed", message, result.ErrorCategory, "error", completionReady: true, completionReadyReason: "ready", lastErrorCategory: result.ErrorCategory);
                return InnolaTransactionLoadResult.Failure(message);
            }

            var completedBy = sessionManager.CurrentUser!.Username;
            UpdateManifestAndAudit("transaction_complete_succeeded", "succeeded", "Transaction completed.", null, "completed", completionReady: true, completionReadyReason: "ready", completedBy: completedBy, completedAt: now);
            sessionManager.MarkTransactionCompleted(now, "Transaction completed.");
            return InnolaTransactionLoadResult.Succeeded(layout, null, "Transaction completed.");
        }
        catch (Exception exception) when (IsExpectedAdapterFailure(exception))
        {
            sessionManager.RestoreTransactionState(snapshot);
            return InnolaTransactionLoadResult.Failure("Could not complete transaction. Try again.");
        }
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
        string? lastErrorCategory = null)
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
            lastErrorCategory ?? current?.LastErrorCategory);

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
}
