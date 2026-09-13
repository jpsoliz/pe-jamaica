---
baseline_commit: ee8d565eba2a4960582b5608a2d497ea328ff31e
---

# Story 12.2: Centralize Innola Session Refresh For Workflow Finalize

Status: done

## Story

As an ArcGIS Pro examiner completing Parcel Workflow work,
I want the add-in to refresh the Innola session before every important Innola read/write,
so that long review sessions do not fail at Finalize, Save Progress, document loading, Plan Examination writeback, Spatial Unit writeback, or task completion because the original Innola session timed out.

## Business Context

Current testing shows a practical failure mode: the examiner can spend significant time editing and validating parcel data in ArcGIS Pro, then press `Finalize` after the Innola session has already expired. The workflow has already added targeted recovery for Spatial Unit save, but the Finalize path still contains several Innola operations that can fail independently if they receive a stale token.

This story hardens the current testing branch before broader PE/PXA refactor work. It should preserve the current workflow and UI behavior while making Innola access resilient and predictable.

## Acceptance Criteria

1. Given the user logged in during the current ArcGIS Pro session, when an Innola operation needs authentication, then the add-in can refresh the current session using the in-memory login context without asking the user to re-enter credentials.
2. Given credentials are only temporary, when ArcGIS Pro closes, logout runs, or refresh fails, then passwords, tokens, cookies, and certificate secrets are not persisted to disk, diagnostics, case folders, or settings.
3. Given an Innola read operation starts, including transaction list, transaction detail, source/attachment download, Plan lookup, Spatial Unit read, RT linked PE lookup, and Compare/cadaster lookup, then it obtains a current session through one shared session-ensure path before sending the request.
4. Given an Innola write operation starts, including Save Progress/resume upload, supporting document upload, Spatial Unit create/update, Plan Examination/Plan Check writeback, report attachment upload, and transaction lifecycle movement, then it obtains a current session through the same shared session-ensure path before sending the request.
5. Given the current token is stale before Finalize, when refresh succeeds, then Finalize continues through Enterprise review closeout, Spatial Unit save, SUID reference writeback, Compute report generation, Plan Examination writeback, package upload, and transaction complete without requiring manual relogin.
6. Given session refresh fails before an Innola operation, then the operation stops before remote mutation and shows a clear message: `Innola connection could not be restored. Please log in again and retry.`
7. Given an Innola write has ambiguous success risk, especially transaction complete or lifecycle movement, then the implementation must not blindly retry the write after a timeout or dropped response; it must either verify remote transaction/task state first or return a safe retry message.
8. Given an auth failure occurs after a request is sent, then the system attempts at most one controlled refresh/retry for safe reads and non-mutating requests.
9. Given a duplicate-prone write receives HTTP 401/403 before a remote mutation is accepted, then the operation may refresh and retry once only when the service can prove the original request was rejected before mutation.
10. Given a retry/refresh is running, then the relevant command remains gated and the status text identifies the operation being restored without exposing secrets.
11. Given refresh succeeds or fails, then local lifecycle audit/diagnostics record operation name, transaction number when available, outcome, and redacted error category.
12. Given tests run, then coverage proves session refresh success, refresh failure, no secret persistence, Finalize stale-token recovery, Plan Examination writeback using refreshed session, lifecycle complete no-blind-retry behavior, and existing Spatial Unit refresh behavior remains intact.

## Tasks / Subtasks

- [x] Add or standardize a central Innola session ensure service. (AC: 1-4, 6, 8-11)
  - [x] Reuse `InnolaSessionManager.RefreshCurrentSessionAsync`; do not create a second credential store.
  - [x] Add a shared method/service such as `EnsureCurrentSessionAsync(operationName, transactionNumber, cancellationToken)` that returns either a current `InnolaSession` or a redacted login-required failure.
  - [x] Preserve loaded transaction and lifecycle state when refresh succeeds.
  - [x] Clear/mark expired session state only when refresh fails and no usable session remains.

- [x] Wire the central session ensure path into ShellState-owned live Innola services. (AC: 3-4)
  - [x] Extend live service construction in `ShellState` so services that read/write Innola can request a refreshed session consistently.
  - [x] Keep mock services unchanged except for interface compatibility.
  - [x] Avoid pushing UI dependencies into low-level HTTP services.

- [x] Harden Compute Finalize session behavior end-to-end. (AC: 5-7, 10-11)
  - [x] Ensure Finalize obtains/refreshed session before Spatial Unit writeback.
  - [x] Ensure Plan Examination/Plan Check writeback uses the refreshed session, not the stale session captured at button click.
  - [x] Ensure report attachment/package upload uses a current session.
  - [x] Ensure transaction lifecycle complete uses a current session.
  - [x] Preserve existing local artifacts/disposition state when remote refresh fails.

- [x] Apply safe retry rules. (AC: 7-9)
  - [x] Safe reads may refresh/retry once after auth failure.
  - [x] Duplicate-prone writes must use verify-before-retry or stop safely.
  - [x] Do not retry transaction complete blindly after timeout, connection reset, or ambiguous response.
  - [x] Keep `InnolaApiRetryMode.VerifyBeforeRetry` semantics for lifecycle operations until reliable remote verification is implemented.

- [x] Add user-facing status and diagnostics. (AC: 6, 10-11)
  - [x] Use copy such as `Reconnecting to Innola...`, `Innola connection restored. Continuing...`, and `Innola connection could not be restored. Please log in again and retry.`
  - [x] Ensure status changes refresh Transaction Panel and Parcel Workflow command availability.
  - [x] Redact tokens, passwords, cookies, authorization headers, and certificate material.

- [x] Add automated tests. (AC: 1-12)
  - [x] `InnolaSessionManager` refresh success preserves loaded transaction state.
  - [x] refresh failure produces login-required result and no secret diagnostics.
  - [x] `InnolaSpatialUnitService` still refreshes before Spatial Unit write.
  - [x] `InnolaPlanCheckService` uses refreshed session for Plan Examination writeback.
  - [x] `InnolaTransactionLifecycleCoordinator.CompleteAsync` refreshes before each Innola-dependent Finalize phase that can be delayed by examiner work.
  - [x] lifecycle complete timeout/ambiguous failure does not perform blind duplicate completion.
  - [x] command gating/status text appears during refresh.

### Review Findings

- [x] [Review][Patch] Compare legal/cadaster lookup still bypasses the central session-ensure path. AC3 explicitly includes Compare/cadaster lookup, but `OpenCompareWorkspaceCore` still creates the live legal cadaster query service with `() => Session.CurrentSession`, and `InnolaBaUnitLegalCadasterQueryService` reads that provider directly before sending `/search` requests. A stale in-memory token can still produce the same no-connection/login-required behavior in Compare searches instead of restoring through `EnsureCurrentSessionAsync`. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs:337]
- [x] [Review][Patch] Safe reads do not refresh/retry after an authorization response. AC8 requires one controlled refresh/retry when a request receives auth failure after it was sent, but `InnolaApiResilience.SendAsync` returns immediately for 401/403 because `ShouldRetry` only retries transient status codes. Pre-refresh reduces failures, but a token that expires between ensure and the HTTP request still fails without the required refresh/retry path. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaApiResilience.cs:52]

## Dev Notes

### Existing Implementation To Reuse

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSession.cs`
  - Already contains `SessionPassword` for current ArcGIS Pro session only.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
  - `RefreshCurrentSessionAsync()` already logs in again using `ServerUrl`, `Username`, and in-memory `SessionPassword`.
  - `IsLoggedIn`, command gates, loaded transaction state, lifecycle state, and session events already live here.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaApiResilience.cs`
  - Existing shared HTTP retry/status classification.
  - `LoginRequiredMessage` is the current standard auth failure text.
  - `VerifyBeforeRetry` already represents duplicate-prone operation caution.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
  - Constructs live Innola services.
  - Spatial Unit service already receives `(_, cancellationToken) => Session.RefreshCurrentSessionAsync(cancellationToken)`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
  - `RefreshSessionBeforeSpatialUnitWriteAsync()` is the current narrow implementation and should be generalized or kept as a consumer of the new central pattern.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
  - Plan Examination writeback currently receives an `InnolaSession` directly and uses it for multiple GET/PUT/POST calls.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
  - Owns Finalize ordering and currently passes `sessionManager.CurrentSession!` into Spatial Unit, Plan Check, package upload, and lifecycle completion paths.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
  - Uses `InnolaApiRetryMode.VerifyBeforeRetry` and `MaxAttempts: 1` for lifecycle start/complete to avoid duplicate task movement.

### Required Design Direction

Do not scatter `RefreshCurrentSessionAsync()` calls by hand in every button handler. The preferred shape is a single session gateway near `InnolaSessionManager` or `ShellState` that all live Innola operations can use.

Suggested shape:

```csharp
public sealed record InnolaSessionEnsureResult(
    bool Success,
    InnolaSession? Session,
    string Message,
    string? ErrorCategory);
```

The exact names can follow repo style, but the behavior must be centralized:

- Use current session if it is present and authorized enough for the call.
- Refresh once using in-memory credentials when the operation needs a fresh session.
- Return a redacted login-required result when refresh fails.
- Notify UI state through the existing `SessionChanged`/status mechanisms.
- Never write credentials to settings, case folders, logs, or JSON evidence.

### Finalize Safety Rules

Finalize is not one API call. It can include:

1. Enterprise review closeout/disposition.
2. Innola Spatial Unit create/update.
3. Enterprise SUID reference writeback.
4. Compute examination report generation.
5. Innola Plan Examination/Plan Check writeback.
6. Working package upload.
7. Innola task complete.

The session can expire between any of those phases. The dev implementation should reacquire a current session before each Innola-dependent phase, not only once at the beginning.

### Non-Goals

- Do not persist passwords or tokens.
- Do not add a background keep-alive timer unless explicitly requested later.
- Do not change transaction workflow stages or business rules.
- Do not change PE/PXA extraction/refactor behavior.
- Do not automatically retry transaction completion after ambiguous network failure without remote state verification.
- Do not replace `InnolaApiResilience`; extend or compose with it.

### Testing Requirements

Minimum verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`
- Focused Innola tests through the project test harness.
- Existing full harness may still stop outside ArcGIS Pro at the known `ArcGIS.Desktop.Mapping` assembly boundary; relevant tests must pass before that point.

Focused test files likely to touch:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSessionManagerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaPlanCheckServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## References

- `_bmad-output/implementation-artifacts/2-22-add-innola-api-connection-health-retry-and-session-recovery.md`
- `_bmad-output/implementation-artifacts/7-11-write-innola-plan-check-list-on-compute-finalize.md`
- `_bmad-output/implementation-artifacts/12-1-current-testing-review-ux-and-extraction-polish.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaApiResilience.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-09-12: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- 2026-09-12: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed through all new Innola session/finalize tests and stopped at the known outside-Pro `ArcGIS.Desktop.Mapping` assembly boundary.
- 2026-09-12: `tools/package_addin.ps1 -Configuration Release` produced and registered add-in build `1.1.533`.
- 2026-09-12 review fix: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- 2026-09-12 review fix: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "innola ensure cancellation" "innola ensure late refresh" "innola spatial unit service stops when refresh fails"` passed 3 targeted regression tests.
- 2026-09-12 review fix: full harness still reaches the known outside-Pro `ArcGIS.Desktop.Mapping` assembly boundary after the new Innola session tests pass.
- 2026-09-12 review fix: `tools/package_addin.ps1 -Configuration Release` produced and registered add-in build `1.1.534`.
- 2026-09-12 reload crash follow-up: reported dump path `_13.6.0.59527\_0\_09\_12\_2026\_16\_05\_22.dmp` was not visible from the sandbox, but reload review found `EnsureCurrentSessionAsync` could raise `SessionChanged` after `ConfigureAwait(false)`.
- 2026-09-12 reload crash follow-up: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "innola session manager raises refresh" "innola ensure cancellation" "innola ensure late refresh" "transaction panel my tasks" "transaction panel search text"` passed 6 targeted tests.
- 2026-09-12 reload crash follow-up: `tools/package_addin.ps1 -Configuration Release` produced and registered add-in build `1.1.537`.
- 2026-09-12 Compare start crash follow-up: reported dump paths `_13.6.0.59527\_0\_09\_12\_2026\_16\_08\_32.dmp` and `_13.6.0.59527\_0\_09\_12\_2026\_16\_11\_43.dmp` were not visible from the sandbox, but Compare start review found `TransactionPanelState` session-change handling still trusted event thread affinity.
- 2026-09-12 Compare start crash follow-up: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "transaction panel background session change" "transaction panel compare workflow stage starts"` passed 2 targeted tests.
- 2026-09-12 Compare start crash follow-up: `tools/package_addin.ps1 -Configuration Release` produced and registered add-in build `1.1.539`.
- 2026-09-12 Compare TR100000896 load crash follow-up: reported dump path `_13.6.0.59527\_0\_09\_12\_2026\_16\_21\_22.dmp` was not visible from the sandbox; deeper review found other dockpanes also receive `SessionChanged`, so thread affinity needed to be enforced at the session event source.
- 2026-09-12 Compare TR100000896 load crash follow-up: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "transaction panel background session change" "transaction panel compare workflow stage starts" "compare workspace" "innola ensure"` passed 49 targeted tests.
- 2026-09-12 Compare TR100000896 load crash follow-up: `tools/package_addin.ps1 -Configuration Release` produced and registered add-in build `1.1.541`.
- 2026-09-13 code-review patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed.
- 2026-09-13 code-review patch: `dotnet exec src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compare innola baunit volume folio search refreshes session after auth failure" "compare innola baunit volume folio search retries cookie auth" "compare innola baunit volume folio search auth failure requests login" "compare cadaster factory creates innola baunit legal adapter" "innola lifecycle complete refreshes session before finalize phases" "innola lifecycle complete stops before mutation when refresh fails" "innola spatial unit service stops when refresh fails"` passed 7 targeted tests.
- 2026-09-13 code-review patch: `git diff --check` passed.
- 2026-09-13 code-review patch: `tools/package_addin.ps1 -Configuration Release` produced and registered add-in build `1.1.571`.

### Completion Notes List

- Added `InnolaSessionManager.EnsureCurrentSessionAsync` and `InnolaSessionEnsureResult` as the central in-memory session refresh gateway.
- Routed Compute Finalize remote phases through session ensure before Spatial Unit save, report attachment upload, Plan Examination writeback, working package upload, and lifecycle complete.
- Routed transaction-panel refresh/lookup and PLA-B/RT supporting document read paths through the shared session ensure helper.
- Routed transaction detail/source/resume package loading through the shared session ensure path before Innola downloads.
- Preserved `InnolaApiRetryMode.VerifyBeforeRetry`/single-attempt lifecycle semantics for duplicate-prone task movement.
- Added tests for refresh success, refresh failure without secret leakage, Finalize refreshed-token use, and stopping before remote mutation when refresh cannot be restored.
- Fixed critical review findings by treating refresh cancellation as cancelled instead of expired, guarding late refresh results from overwriting a newer session, and stopping Spatial Unit writes when preflight refresh returns no session.
- Added targeted regression coverage for cancellation session preservation, delayed refresh/new-login race behavior, and stale-session Spatial Unit write prevention.
- Fixed transaction reload crash risk by keeping session refresh notifications on the caller synchronization context, matching the existing login notification contract and preventing WPF list/property updates from being raised on a background continuation.
- Aligned the My Tasks filter test with the logged-user requirement: exact username and username-token assignees match; display-name-only assignees do not.
- Hardened `TransactionPanelState` session-change subscription so any background session mutation from Compare load/start flows is marshalled to the WPF dispatcher or captured synchronization context before updating observable rows/properties.
- Hardened `InnolaSessionManager.OnSessionChanged()` so all session subscribers are notified from the WPF dispatcher when a session mutation originates from a background continuation.
- Routed Compare legal/cadaster live lookup through `EnsureCurrentSessionAsync` and added one safe authorization-refresh retry for Innola Compare search requests.

### File List

- `_bmad-output/implementation-artifacts/12-2-centralize-innola-session-refresh-for-workflow-finalize.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/project-context.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionEnsureResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSessionManagerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaAuthServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
