---
baseline_commit: 5d3813e
---

# Story 2.5: Control Active Transaction Lifecycle and Completion Gate

Status: done

## Story

As a cadastral technical staff user,
I want the add-in to control active transaction ownership, save/cancel behavior, and completion,
so that I cannot accidentally overwrite work or complete a transaction I did not start.

## Acceptance Criteria

1. Given a transaction has been loaded from Innola into a valid local Case Folder, when the user starts or claims the transaction, then the add-in calls a mockable Innola lifecycle service boundary and records the task id, started/claimed user identity, claim status, and timestamp in session state and `manifest.json`.
2. Given the transaction is in progress for the logged-in user, then Parcel Workflow actions remain enabled for that loaded Case Folder; given the transaction is owned by another user, then start/complete actions are blocked with a clear non-secret ownership message and the local workflow is not allowed to complete that task.
3. Given a transaction is loaded and active, when the user attempts to load a different transaction, then the add-in requires one of: save the full Case Folder, cancel the current process, or stay on the current transaction.
4. Given the user chooses Save Progress, then the add-in persists the full Case Folder lifecycle state locally, records the save action in the local audit trail, keeps the Innola task in progress, and does not call Complete.
5. Given the user chooses Cancel Current Process, then the add-in records the cancellation decision in the local audit trail, clears the active transaction gates, disables Parcel Workflow for that transaction, and does not call Complete.
6. Given the user chooses Stay on Current Transaction, then no new transaction is selected or loaded, no Case Folder is overwritten, and the existing active transaction remains the active workflow context.
7. Given the parcel workflow has not reached the configured downstream sync/readiness gate, then the Complete action is visible but disabled or returns a blocked state explaining that sync/readiness criteria are not met.
8. Given completion readiness criteria are met and the logged-in user is the user who started/claimed the task, when the user clicks Complete, then the add-in calls the Innola completion service operation, records completion success/failure in the Case Folder audit trail and manifest, clears the active transaction gates on success, and requires a transaction refresh before another task can be completed.
9. Given any lifecycle API call fails, times out, returns unauthorized, returns an ownership conflict, or returns an invalid response, then the UI shows a retryable non-secret error, the previous valid session/Case Folder state is preserved, and no token/password/raw response/signed URL is written to artifacts or status text.
10. Given this story is complete, then no extraction, validation, output generation, ArcGIS Enterprise writeback, or real CADINDEX sync is implemented; Story 2.5 only adds the lifecycle facade, local state/audit records, UI gates, and completion readiness hook.

## Tasks / Subtasks

- [x] Add Innola transaction lifecycle contracts. (AC: 1, 2, 4, 5, 8, 9)
  - [x] Add service interfaces under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/`, such as `IInnolaTransactionLifecycleService`.
  - [x] Add add-in-owned lifecycle models for claim/start, save progress, cancel, completion readiness, complete, and ownership conflict results.
  - [x] Include task id, transaction id/number, process step, logged-in username, display name, group context, lifecycle status, owner/claimed user, timestamps, and sanitized error category/message.
  - [x] Keep live API response assumptions inside the Innola adapter; UI, workflow, and tests depend on add-in-owned models only.
  - [x] Do not persist or expose access tokens, passwords, signed URLs, raw request/response bodies, or full exception dumps.
- [x] Add mock/dry-run lifecycle provider. (AC: 1, 2, 4, 5, 7, 8, 9)
  - [x] Extend mock mode so loaded transactions can be claimed by the current mock user.
  - [x] Provide test fixtures for claim success, already-owned-by-current-user, owned-by-other-user, save progress, cancel local process, complete blocked by readiness, complete success, and service failure.
  - [x] Ensure mock mode still requires a logged-in session, selected transaction, and successfully loaded Case Folder.
  - [x] Keep the mock lifecycle behavior deterministic for ArcGIS Pro dry-run and automated tests.
- [x] Extend `InnolaSessionManager` and shell gating for active lifecycle state. (AC: 1, 2, 3, 5, 6, 8, 9)
  - [x] Add state for active transaction lifecycle, including loaded task id, lifecycle status, owner/claimed user, started/claimed timestamp, last saved timestamp, completion pending/blocked/completed state, and active Case Folder path.
  - [x] Add command gate properties for `CanStartOrClaimTransaction`, `CanSaveProgress`, `CanCancelActiveProcess`, `CanCompleteTransaction`, `CanSwitchTransaction`, and `CanOpenParcelWorkflow`.
  - [x] Ensure logout/session expiry clears in-memory lifecycle state and disables transaction-dependent commands.
  - [x] Ensure lifecycle state can be captured/restored alongside existing selected/loaded transaction state so failed operations preserve the previous valid context.
- [x] Extend Case Folder manifest and audit records. (AC: 1, 3, 4, 5, 8, 9)
  - [x] Extend `ManifestDocument`/`ManifestPayload` with lowercase snake_case lifecycle fields, for example `innola_lifecycle`.
  - [x] Record claim/start, save progress, cancel decision, completion readiness check, complete attempt, complete success/failure, owner user, timestamps, and sanitized error categories.
  - [x] Use the existing source action audit service where appropriate, or add a small workflow audit writer under `CaseFolders/` if lifecycle events do not fit the source-file audit shape.
  - [x] Preserve backward compatibility for Epic 1, Story 2.1, Story 2.2, Story 2.3, and Story 2.4 manifests that do not contain lifecycle metadata.
  - [x] Ensure saving progress writes the full current Case Folder state and leaves the task in progress, not completed.
- [x] Add transaction switch guard to the Transaction Panel. (AC: 3, 4, 5, 6, 9)
  - [x] Update `TransactionPanelState.LoadSelectedTransactionAsync` so a different selected row cannot replace an active loaded transaction without a save/cancel/stay decision.
  - [x] Expose the decision through a testable prompt/decision provider instead of hardwiring WPF dialog logic inside the state service.
  - [x] On Save Progress, persist lifecycle/manifest/audit state before allowing the new transaction load attempt.
  - [x] On Cancel Current Process, record cancellation and clear active gates before allowing the new transaction load attempt.
  - [x] On Stay, keep the previous active transaction, selected transaction, loaded Case Folder path, and Parcel Workflow gate unchanged.
  - [x] If the later new transaction load fails, preserve the last valid saved/cancelled/session state according to the user's chosen action.
- [x] Add lifecycle controls and status to the Transaction Panel and Parcel Workflow pane. (AC: 1, 2, 4, 5, 7, 8, 9)
  - [x] Add clear status text for loaded, claimed/in-progress, saved, cancelled, complete blocked, complete pending, completed, and lifecycle error states.
  - [x] Add or wire commands for Start/Claim, Save Progress, Cancel Process, and Complete in the pane where the user naturally manages the loaded parcel workflow.
  - [x] Keep Login and About available while logged out; keep Transaction Panel gated by login; keep Parcel Workflow gated by successful transaction load and lifecycle rules.
  - [x] Complete must be visible for workflow clarity but unavailable until completion readiness criteria pass.
  - [x] Keep layout consistent with the Sidwell Co shell and current WPF/MVVM pattern; do not introduce unrelated UI frameworks.
- [x] Add completion readiness hook without implementing Enterprise sync. (AC: 7, 8, 10)
  - [x] Add a small testable completion readiness service/facade, for example `ITransactionCompletionReadinessService`.
  - [x] Default readiness to blocked until a future sync/output story provides positive local evidence.
  - [x] Allow tests to inject a ready result so Complete success paths can be validated without implementing real CADINDEX or ArcGIS Enterprise sync.
  - [x] Record readiness checks in manifest/audit without claiming authoritative Enterprise sync status.
- [x] Add focused lifecycle tests. (AC: 1-10)
  - [x] Test successful claim/start records owner/timestamp in session and manifest and keeps Parcel Workflow enabled.
  - [x] Test transaction owned by another user blocks claim/complete and keeps errors sanitized.
  - [x] Test switching to a different transaction is blocked until save/cancel/stay decision is supplied.
  - [x] Test Save Progress persists lifecycle manifest/audit state and does not call Complete.
  - [x] Test Cancel Current Process clears active gates, disables Parcel Workflow, records audit, and does not call Complete.
  - [x] Test Stay preserves current active transaction and prevents replacement.
  - [x] Test Complete is blocked until readiness service returns ready.
  - [x] Test Complete success calls lifecycle service only for the claimed/current user, records success, clears active gates, and requires refresh/reload for subsequent work.
  - [x] Test lifecycle service exceptions/timeouts/invalid responses preserve previous valid state and return non-secret retryable errors.
  - [x] Test generated manifests/audit records do not contain fake secrets, raw responses, tokens, passwords, or signed URLs.
- [x] Validate and package. (AC: 1-10)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.
  - [x] ArcGIS Pro 3.6 smoke package ready: package generated successfully; manual ArcGIS Pro UI smoke remains user-run by opening the add-in, logging/mock logging in, refreshing/loading a transaction, claiming/starting it, verifying Save/Cancel/Complete gates, and verifying Complete is blocked until readiness evidence exists.

### Review Findings

- [x] [Review][Patch] Live lifecycle adapter reports claim/save/complete success without calling Innola [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs:5]
- [x] [Review][Patch] Switch decisions are not recorded in the lifecycle audit trail [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:312]
- [x] [Review][Patch] Save-progress switch loses the saved active transaction if the replacement load fails [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:338]

## Dev Notes

### Current State From Prior Stories

- Story 2.4 is marked done and introduced `InnolaTransactionLoadService`, transaction detail/attachment models, mock detail/download behavior, transaction load into Case Folder, manifest `innola_transaction` and `attachment_provenance`, source profile refresh, and Parcel Workflow enablement only after load validation passes.
- `InnolaSessionManager` currently tracks login, selected transaction, loaded transaction number, loaded Case Folder path, and `CanOpenParcelWorkflow => IsLoggedIn && IsTransactionLoaded`.
- `TransactionPanelState.LoadSelectedTransactionAsync` currently selects a row, captures prior transaction state, calls `InnolaTransactionLoadService.LoadSelectedTransactionAsync`, restores previous state on load failure, and has no save/cancel/stay guard for an active transaction.
- `ParcelWorkflowDockpaneViewModel` syncs to the loaded Case Folder after session changes/show and still preserves manual Case Folder operations for tests/recovery.
- The worktree may contain uncommitted Story 2.4 review-fix/status changes. Do not revert unrelated user or prior-story changes; work with the current files.

### Innola Workflow Rules From Approved Course Correction

- Initial configured Innola environment is `https://eltrs.innola-solutions.com/`.
- Innola passwords/tokens may live only in ArcGIS Pro session memory. They must not be written to disk.
- The transaction list contains only tasks available for the current workflow step and assigned to the logged-in user or to a group the user belongs to.
- Attachments are discovered from Innola metadata that provides file identity, name, type/extension, and download information.
- Saving temporary work means saving the full local Case Folder. It keeps the Innola task in progress.
- A transaction/stage remains in progress until the user explicitly completes the task.
- A transaction can be worked or reviewed multiple times before completion.
- A transaction can only be completed by the user who started/claimed it.
- If a user tries to load a new transaction, the add-in must control the flow so replacement happens only after save or cancel, or does not happen when the user chooses to stay.

### Recommended API Boundary

Exact live Innola lifecycle endpoints have not been confirmed from Swagger in this environment. Implement behind interfaces and keep names adaptable. The approved course-correction proposal suggested:

```text
POST /transactions/{transaction_id}/claim
POST /transactions/{transaction_id}/save-state
POST /transactions/{transaction_id}/complete
```

Prior Innola add-in source may contain useful patterns at:

```text
D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Services\InnolaApiService.cs
D:\Code\Innola_Code\arcgis-pro-nscrp-develop\src\Windows\TaskList\SourceFilesWindowViewModel.cs
```

Treat that code as reference only. The Sidwell add-in should own its contracts and avoid copying unrelated UI or credential persistence behavior.

### Manifest Guidance

Current Story 2.4 payload additions are expected to include `innola_transaction` and `attachment_provenance`. Add lifecycle metadata as an optional sibling, for example:

```json
{
  "innola_lifecycle": {
    "transaction_id": "100000004",
    "transaction_number": "TR100000004",
    "task_id": "task-100000004",
    "process_step": "parcel_workflow",
    "status": "in_progress",
    "claimed_by": "tester",
    "claimed_display_name": "Test User",
    "claimed_at": "2026-06-10T18:30:00Z",
    "last_saved_at": "2026-06-10T18:40:00Z",
    "cancelled_at": null,
    "completion_ready": false,
    "completion_ready_reason": "sync_readiness_not_met",
    "completed_by": null,
    "completed_at": null,
    "last_error_category": null
  }
}
```

Use lowercase snake_case JSON fields. Avoid storing secrets, raw service payloads, or signed URLs.

### Audit Guidance

The architecture requires Case Folder artifacts to be the recovery and audit truth. Lifecycle actions that should be auditable:

- `transaction_claim_started`
- `transaction_save_progress`
- `transaction_cancelled_locally`
- `transaction_completion_readiness_checked`
- `transaction_complete_attempted`
- `transaction_complete_succeeded`
- `transaction_complete_failed`
- `transaction_switch_blocked`
- `transaction_switch_saved`
- `transaction_switch_cancelled`
- `transaction_switch_stayed`

If reusing `SourceFileActionAuditService` would distort the model, add a small workflow lifecycle audit artifact rather than overloading source-file terminology. Keep audit records machine-readable and sanitized.

### Completion Readiness Boundary

Story 2.5 must not implement extraction, validation, output generation, ArcGIS Enterprise writeback, or real CADINDEX sync. Add only a readiness hook/facade. The default implementation should report blocked until later stories create positive local evidence such as an output summary or sync readiness artifact.

This keeps the Complete button in the UI now while preserving the architecture decision that v1 has a fake/no-op CADINDEX sync facade and no live Enterprise dependency.

### UI Guidance

- Keep the Sidwell Co ribbon and pane structure from Stories 2.2-2.4.
- Logged out: Login and About remain available; transaction/workflow actions stay gated.
- Logged in without loaded transaction: Transaction Panel is available; Parcel Workflow remains disabled.
- Loaded but not claimed/in-progress: Start/Claim should be available when ownership allows it.
- In progress for current user: Save Progress and Cancel Process are available; Complete is visible but blocked until readiness passes.
- Owned by another user: show a concise ownership conflict and prevent completion.
- The switch guard should be testable without modal UI. Use an injected decision provider so automated tests can choose save, cancel, or stay.

### Security And Reliability Requirements

- All lifecycle failures must be sanitized into non-secret categories such as unauthorized, timeout, ownership_conflict, invalid_response, not_ready, write_failure, or service_unavailable.
- Never write passwords, tokens, raw request bodies, raw response bodies, signed URLs, or full exception dumps to `manifest.json`, audit artifacts, logs/reports, status text, or test artifacts.
- Preserve the previous valid loaded transaction and Case Folder state after failed lifecycle calls.
- Avoid required state that exists only in ArcGIS Pro project/session memory. Reopening the Case Folder should recover the lifecycle state needed to explain the current work and completion status.
- Maintain ArcGIS Pro 3.6 compatibility and the existing `net8.0-windows` add-in project structure.

### Testing Notes

- Keep C# tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests` and register them in `Program.cs`.
- Prefer service/state tests over WPF visual tests.
- Use fake lifecycle services, fake completion readiness service, and deterministic temp Case Folders.
- Avoid live network in automated tests.
- Existing Story 1, Story 2.1, Story 2.2, Story 2.3, and Story 2.4 tests must keep passing.

### References

- `_bmad-output/planning-artifacts/epics.md`: Story 2.5 acceptance criteria.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-10.md`: approved Innola workflow correction and lifecycle rules.
- `_bmad-output/planning-artifacts/architecture.md`: Case Folder as system of record, file-based contracts, workflow state machine, security, and no live CADINDEX/Enterprise dependency.
- `_bmad-output/implementation-artifacts/2-4-load-transaction-details-and-attachments-into-case-folder.md`: completed transaction load behavior and explicit Story 2.5 boundary.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`: current login, selected transaction, loaded transaction, and Parcel Workflow gate state.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`: current transaction refresh/load state and likely switch-guard insertion point.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`: current load orchestration service to call after lifecycle switch guard permits load.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`: manifest contract to extend with lifecycle metadata.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionAuditService.cs`: existing local audit pattern that may be reused or mirrored.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`: current workflow state and Case Folder synchronization.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `tools\validate_contracts.ps1` - passed.
- `tools\run_python_tests.ps1` - passed, 2 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 98 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed, 0 warnings, 0 errors.
- `tools\package_addin.ps1` - passed; package produced at `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.
- Review fix rerun: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 101 tests.
- Review fix rerun: `tools\validate_contracts.ps1` - passed.
- Review fix rerun: `tools\run_python_tests.ps1` - passed, 2 tests.
- Review fix rerun: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed, 0 warnings, 0 errors.
- Review fix rerun: `tools\package_addin.ps1` - passed; package produced at `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Completion Notes List

- Added an Innola lifecycle service boundary with mock and placeholder live adapters for claim/start, save-state, and complete.
- Added session lifecycle state and gates for start/claim, save progress, cancel process, complete, active switch control, and Parcel Workflow enablement.
- Added `innola_lifecycle` manifest metadata and a dedicated `working\workflow_lifecycle_audit.json` local audit artifact.
- Added a completion readiness facade that blocks Complete by default until future sync/readiness evidence exists; tests can inject readiness for complete success.
- Added Parcel Workflow pane lifecycle controls: Start, Save Progress, Cancel Process, and Complete.
- Added transaction switch guard with an injectable decision provider for Save, Cancel, or Stay behavior.
- Added lifecycle and switch-guard tests covering claim, ownership conflict, save, cancel, readiness blocking, complete success, exception redaction, stay, and cancel replacement.
- Live Innola lifecycle endpoint shapes remain behind interfaces until the official API contract is confirmed.
- Fixed review findings: live lifecycle adapter now fails safely until endpoints are configured, switch stay/save/cancel decisions are audited, and failed replacement load after Save Progress restores the saved active transaction.

### File List

- `_bmad-output/implementation-artifacts/2-5-control-active-transaction-lifecycle-and-completion-gate.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/WorkflowLifecycleAuditDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/WorkflowLifecycleAuditService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ActiveTransactionSwitchDecision.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IActiveTransactionSwitchDecisionProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ITransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleRequest.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleStatus.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/StayOnCurrentTransactionDecisionProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/TransactionCompletionReadinessResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-10 | 0.1 | Initial story context for active Innola transaction lifecycle, save/cancel guard, and completion gate. | Mary |
| 2026-06-10 | 1.0 | Implemented lifecycle facade, session gates, manifest/audit lifecycle records, Parcel Workflow controls, switch guard, readiness gate, tests, validation, and packaging. | Codex |
| 2026-06-10 | 1.1 | Addressed code review findings for live lifecycle safe failure, switch-decision audit, and save-progress replacement failure recovery. | Codex |

