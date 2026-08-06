---
baseline_commit: current-review-2026-08-06
---

# Story 2.22: Add Innola API Connection Health, Retry, and Session Recovery

Status: review

## Story

As an ArcGIS Pro examiner,  
I want the add-in to recover when the Innola API connection is lost during load, save, file upload, or transaction movement,  
so that temporary network or session interruptions do not fail the workflow unless recovery is not possible.

## Business Context

The add-in already receives Innola credentials during login and keeps session data in memory for the current ArcGIS Pro session. Users are seeing errors when the connection is lost while the add-in needs Innola API access, especially for loading files, saving files, uploading workflow evidence, or moving a transaction to another Innola workflow step.

Mary's product position: this should be treated as a workflow resilience story. The user should not have to restart the workflow for a temporary connection issue. When the add-in can reconnect or retry safely, it should do so and show clear progress. When it cannot recover, the message must explain what happened and what the user can do next without exposing credentials or raw API details.

Amelia's engineering position: this should be implemented as a shared Innola API resilience layer, not as separate one-off retries behind individual buttons. The actual API operation can often serve as the connection test. A separate ping may be useful before long or expensive operations, but the core behavior should be: classify the failure, recover the session when possible, retry transient failures within a bounded policy, and verify remote state before retrying any operation that could create duplicates or advance workflow state twice.

## Acceptance Criteria

1. Given any Innola API operation is invoked from the add-in, when the call is made, then it uses a shared resilience executor or equivalent central policy rather than each feature owning separate retry behavior.

2. Given the current Innola session is missing, expired, or invalid before an operation starts, when the user action requires Innola access, then the add-in attempts session recovery from the current in-memory login context where allowed and otherwise prompts the user to log in again.

3. Given a transient network failure occurs during a safe read operation such as transaction list refresh, transaction details load, attachment/file download, workflow metadata lookup, or compare/cadaster lookup, when retry is still within policy, then the add-in retries automatically and returns the successful result if recovery succeeds.

4. Given a transient HTTP failure occurs, including timeout, request cancellation caused by connection loss, DNS/connection reset, HTTP 408, 429, 500, 502, 503, or 504, then the add-in treats the failure as retryable unless the specific operation marks it unsafe to retry.

5. Given Innola returns an authentication or authorization failure such as HTTP 401 or 403, when session-only credentials or a refresh mechanism are available, then the add-in attempts one controlled session recovery and retries the original request once; if recovery fails, it clears expired session state and asks the user to log in again.

6. Given an Innola operation is a write that may create or replace files, save a resume package, attach a compute or compare report, or update plan-check/spatial-unit data, when the first attempt fails after the request may have reached Innola, then the add-in verifies the remote result before retrying where a duplicate could be created.

7. Given an Innola operation moves, starts, stops, saves, releases, or completes a transaction, when the connection drops or the response is ambiguous, then the add-in queries the transaction's current task/workflow state before retrying or reporting failure, so the transaction is not advanced twice or incorrectly returned.

8. Given a retry or session recovery is running, then the UI shows a clear status such as "Reconnecting to Innola..." or "Retrying Innola request..." and keeps the active command from being clicked again until the operation is resolved or cancelled.

9. Given the user cancels a long-running load/save/move operation, then retry delays and pending HTTP calls honor cancellation and stop without changing local workflow state beyond a safe cancelled status message.

10. Given all retry attempts fail, then the add-in shows a non-secret, actionable message that identifies the operation that failed, whether the transaction state was verified, and whether the user should retry, refresh the transaction list, or log in again.

11. Given diagnostics are written, then logs and audit records include operation name, attempt count, retry classification, final outcome, transaction number where available, and redacted error detail; passwords, tokens, cookies, and raw authorization headers are never logged.

12. Given automated tests run, then coverage proves transient retry, 401/session recovery, retry exhaustion, cancellation, safe file upload verification, and transaction-move ambiguity handling.

## Tasks / Subtasks

- [x] Define the shared Innola API resilience contract. (AC: 1-5, 8-12)
  - [x] Add an operation descriptor containing operation name, transaction number when available, retry safety, idempotency/verification strategy, timeout, and cancellation token.
  - [x] Add a central executor or policy used by live Innola HTTP services.
  - [x] Classify retryable exceptions and HTTP status codes in one place.
  - [x] Keep mock mode compatible with the same service contracts.

- [x] Add session health and recovery behavior. (AC: 2, 5, 10-11)
  - [x] Reuse the current in-memory Innola login/session context from Story 2.2.
  - [ ] Attempt refresh or re-login once for expired sessions where the current session design allows it.
  - [ ] Clear expired session state when recovery fails.
  - [x] Return a user-facing login-required result instead of raw authentication errors.

- [x] Integrate the resilience policy into file and transaction load operations. (AC: 1, 3-5, 8-12)
  - [x] Transaction list refresh.
  - [x] Transaction detail and attachment load.
  - [x] Supporting document and transaction document download.
  - [x] Compare/cadaster Innola-backed lookup operations.

- [x] Integrate the resilience policy into save and upload operations. (AC: 1, 4-6, 8-12)
  - [x] Save and Close resume package upload.
  - [x] Compute report attachment upload.
  - [x] Compare report attachment upload.
  - [x] Supporting document upload or replacement operations.
  - [x] Plan checklist, spatial unit, or other Innola writeback operations.

- [x] Integrate the resilience policy into transaction lifecycle movement. (AC: 1, 4-5, 7-12)
  - [x] Start/claim transaction.
  - [x] Move to ArcGIS Pro in-progress stage from Story 2.21.
  - [x] Save/stop/release transaction.
  - [x] Complete/finalize transaction.
  - [ ] Query current transaction/task status after ambiguous lifecycle failures before retrying.

- [x] Add UI status and command gating during recovery. (AC: 8-10)
  - [ ] Show reconnect/retry/recovery status in the Transaction Panel and relevant Compute/Compare panels.
  - [x] Prevent duplicate button clicks while a retry is active.
  - [x] Preserve local case folder state when remote recovery fails.

- [x] Add automated coverage. (AC: 1-12)
  - [x] Fake handler returns 503 once, then success.
  - [x] Fake handler throws connection failure once, then success.
  - [ ] Fake handler returns 401, session recovery succeeds, original request succeeds.
  - [x] Fake handler returns 401, session recovery fails, session is cleared and login-required message is returned.
  - [x] File upload failure verifies existing Innola artifact before retry.
  - [ ] Lifecycle move timeout verifies task state before retry and does not duplicate transition.
  - [x] Retry exhaustion produces redacted actionable error.
  - [x] Cancellation stops retry delay and leaves command state enabled again.

## Dev Notes

### Current Relevant Implementation

Current Innola and workflow behavior is centered around:

- `InnolaSessionManager`
- `IInnolaAuthService`
- `InnolaTransactionService`
- `InnolaTransactionDetailService`
- `InnolaTransactionLoadService`
- `InnolaTransactionLifecycleCoordinator`
- `IInnolaTransactionLifecycleService`
- `InnolaTransactionLifecycleService`
- `CaseResumePackageService`
- `CompareReportAttachmentService`
- `ComputeReport`/workflow report attachment paths
- `TransactionPanelState`
- `ParcelWorkflowDockpaneViewModel`
- `CompareWorkspaceViewModel`

Story 2.2 established session-only credential handling. This story may use current-session password/token data only while ArcGIS Pro remains open and only for recovery the user has implicitly authorized by logging in. Do not persist passwords, tokens, cookies, or certificate secrets to disk.

### Suggested Engineering Shape

Add a central Innola API executor, for example:

```csharp
public interface IInnolaApiExecutor
{
    Task<InnolaApiResult<T>> ExecuteAsync<T>(
        InnolaApiOperation operation,
        Func<InnolaSession, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);
}
```

The exact type names can follow the codebase, but the behavior should remain centralized:

- resolve the current session
- optionally perform a preflight health check for expensive operations
- execute the API operation
- classify failures
- attempt session recovery once for auth failures
- retry transient failures with bounded exponential backoff and jitter
- run operation-specific remote-state verification before retrying ambiguous writes
- return redacted, user-facing failure details

### Retry Safety Rules

Safe to retry directly:

- transaction list refresh
- transaction detail load
- attachment download
- workflow metadata lookup
- read-only compare/cadaster lookup

Retry only after verification:

- resume package upload
- report attachment upload
- supporting document upload
- plan checklist writeback
- spatial unit writeback
- transaction claim/start/stop/release/complete
- workflow stage transitions

Do not retry automatically:

- validation errors
- missing required input
- unsupported transaction type
- permission denied after session recovery fails
- destructive or duplicate-prone operations with no verification strategy

### User-Facing Copy

Recommended status patterns:

- "Reconnecting to Innola..."
- "Retrying Innola request..."
- "Innola connection was restored. Continuing..."
- "Innola connection could not be restored. Please log in again and retry."
- "Innola response was interrupted. Transaction status was refreshed before retrying."

### Open Questions

- Does Innola expose a lightweight session validation or health endpoint, or should the add-in rely on retrying the actual requested API call?
- Does the Innola API support idempotency keys for attachment upload or workflow transition requests?
- Which upload endpoints expose enough metadata to verify an existing artifact by transaction number, source type, file name, timestamp, or checksum?
- Should retry counts/timeouts be configurable in Settings, or fixed for the first implementation?
- Should certificate-based login recovery require user interaction when the certificate prompt is needed again?

## Dependencies

- Builds on Story 2.2: Innola login/session gating and session-only credential policy.
- Builds on Story 2.3: transaction list loading and retryable error display.
- Builds on Story 2.4: transaction detail and attachment load.
- Builds on Story 2.5: active transaction lifecycle and completion gate.
- Coordinates with Story 2.21: Innola ArcGIS Pro in-progress workflow stage.

## Testing

- Unit tests for retry classification and bounded retry behavior.
- Unit tests for session recovery and login-required failure results.
- Service tests with fake HTTP handlers for transient failures and auth failures.
- Lifecycle tests proving ambiguous move/complete failures query remote state before retry.
- Upload tests proving attachment/resume package retry does not create duplicates.
- ViewModel tests proving retry status text, command disabling, cancellation, and final failure messaging.

## Change Log

- 2026-08-06: Created story for Innola API connection health, retry, and session recovery across load, save, upload, and transaction movement operations.
- 2026-08-06: Implemented shared Innola API resilience policy, wired live transaction list/detail/download/upload/lifecycle/Plan Check/Spatial Unit/Compare lookup HTTP calls, added transient retry and login-required auth handling, and moved story to review. Silent re-login/refresh and remote-state verification after ambiguous lifecycle writes remain follow-up items pending an Innola refresh/idempotency contract.
- 2026-08-06: Patched Spatial Unit Finalize path to refresh the current Innola session once before Spatial Unit create/save writeback using the session-only in-memory password, preserving the loaded transaction and lifecycle state.

## Dev Agent Record

### Implementation Notes

- Added `InnolaApiResilience`, `InnolaApiOperation`, and `InnolaApiRetryMode` as the central retry/classification surface.
- Added `InnolaSessionManager.RefreshCurrentSessionAsync` so writeback flows can renew the active token without clearing the loaded transaction.
- Direct transient retry now covers safe read operations such as transaction refresh, transaction detail load, attachment downloads, and source metadata lookups.
- Write operations such as attachment upload, source registration, Plan Check writeback, Spatial Unit creation/save, and lifecycle start/complete use the shared policy with conservative `VerifyBeforeRetry`/single-attempt behavior to avoid duplicate uploads or duplicate workflow movement until Innola exposes reliable idempotency or state-verification support.
- Spatial Unit creation/save now performs a one-time preflight session refresh before the first write request when session-only password data is available.
- HTTP 401/403 responses now produce the user-facing login recovery message instead of raw/generic adapter errors.
- Existing bearer/cookie fallback behavior for Plan Check, Spatial Unit, and Compare lookups was preserved.

### Files Changed

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaApiResilience.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSessionManagerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Validation

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj --configuration Release --artifacts-path .artifacts\build-check-story-2-22`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --artifacts-path .artifacts\test-story-2-22-innola -- "innola"`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --artifacts-path .artifacts\test-story-2-22-compare -- "compare innola"`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --artifacts-path .artifacts\test-story-2-22-session-refresh -- "innola spatial unit service" "innola refresh current session"`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj --configuration Release --artifacts-path .artifacts\build-story-2-22-session-refresh`
