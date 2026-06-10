---
baseline_commit: 5d3813e
---

# Story 2.7: Enforce Transaction Execution State and Panel Locking

Status: done

## Story

As a cadastral technical staff user,
I want the add-in to lock the transaction list while a selected transaction is being processed,
so that I cannot accidentally switch transactions or corrupt the active Parcel Workflow context.

## Acceptance Criteria

1. Given the user is logged out, when the Sidwell Co ribbon is shown, then Login, Configuration, and About are enabled, Transaction Panel is disabled, and Parcel Workflow is disabled.
2. Given the user logs in successfully, when mock or live session state becomes logged in, then Transaction Panel becomes enabled and auto-populates or refreshes the available transaction list, while Parcel Workflow remains disabled.
3. Given the user selects a transaction row but has not started it, then the row is visibly selected, Start is enabled, Stop/Save and Complete remain disabled, and Parcel Workflow remains disabled.
4. Given the user double-clicks a transaction row or clicks Start, when transaction load and claim/start succeed, then the selected transaction becomes the active transaction, the row remains highlighted, Transaction Panel list selection/filter/search/sort are locked, Start is disabled, Stop/Save is enabled, Complete follows existing readiness gating, and Parcel Workflow becomes enabled for the loaded Case Folder.
5. Given a transaction is active, when the user clicks another transaction row, changes filter/search/sort, refreshes in a way that would replace rows, or double-clicks another row, then the add-in prevents transaction switching and keeps the original active transaction selected unless the active transaction is saved/stopped, cancelled, or completed through the existing lifecycle controls.
6. Given the user clicks Stop/Save while a transaction is active, when save progress succeeds, then the full Case Folder and lifecycle audit are persisted, the Innola task remains in progress, Parcel Workflow is disabled for further editing, and the transaction list is unlocked and refreshed or restored with the saved transaction clearly marked as in progress by the current user.
7. Given the user clicks Complete while completion readiness passes, when completion succeeds, then the active transaction is cleared, Parcel Workflow is disabled, Transaction Panel is unlocked and refreshed, and the completed transaction no longer appears as available work.
8. Given Start, Stop/Save, Complete, refresh, or lock/unlock operations fail, then the previous valid active transaction state is preserved, the UI shows a retryable non-secret error, and no token, password, signed URL, raw request/response, or stack trace is written to UI text or Case Folder artifacts.
9. Given the add-in runs in mock mode, then a dry-run user can test the full state flow: login, transaction list populated, start by double-click or toolbar, panel lock, Parcel Workflow enabled, stop/save unlock, and completion-blocked messaging without live Innola network access.
10. Given this story is complete, then no extraction, validation, output generation, DWG readiness inspection, live CADINDEX sync, ArcGIS Enterprise writeback, or real Innola upload endpoint implementation is added.

## Tasks / Subtasks

- [x] Formalize transaction execution states in existing session/panel state. (AC: 1-8)
  - [x] Reuse `InnolaSessionManager` lifecycle state; do not add a parallel global state machine.
  - [x] Add or expose a panel-specific execution state such as logged out, logged in no transaction, selected not started, active in progress, saved/stopped, completed, and error.
  - [x] Ensure state is derived from `IsLoggedIn`, `SelectedTransaction`, `IsTransactionLoaded`, `LifecycleStatus`, and `LifecycleOwnerUser`.
  - [x] Raise property/command notifications for every gate affected by active transaction changes.
- [x] Tighten Sidwell Co ribbon command gates. (AC: 1, 2, 4, 6, 7)
  - [x] Confirm `ShowTransactionPanelButton` remains disabled while logged out and enabled after login.
  - [x] Confirm `ShowParcelWorkflowDockpaneButton` remains disabled until a transaction is successfully started/claimed, not merely selected.
  - [x] Ensure Stop/Save and Complete transitions disable Parcel Workflow when the active editing context is no longer valid.
  - [x] Preserve Configuration and About availability while logged out.
- [x] Auto-refresh Transaction Panel after login. (AC: 2, 8, 9)
  - [x] On successful login/session change, refresh available transactions once when the panel exists or activates.
  - [x] Avoid duplicate refreshes during an active refresh.
  - [x] In mock mode, show the configured mock transaction rows without requiring live network calls.
  - [x] Keep errors redacted and retryable if refresh fails.
- [x] Add double-click Start behavior. (AC: 3, 4, 5, 8)
  - [x] Bind transaction row double-click to the same command path as Start, not a separate selection-only path.
  - [x] Ensure double-click does not bypass load validation, claim/start, lifecycle audit, or active transaction switch guards.
  - [x] Keep single-click selection as selection-only and Parcel Workflow-disabled until Start succeeds.
- [x] Lock Transaction Panel controls while a transaction is active. (AC: 4, 5)
  - [x] Disable ListBox selection changes, filter/search/sort controls, and non-safe refresh behavior when `session.HasActiveTransaction` is true.
  - [x] Preserve the active row highlight even while the list is disabled.
  - [x] Keep View Documents available for the loaded transaction when appropriate.
  - [x] Keep Stop/Save available for the active owner.
  - [x] Keep Complete visible/available only according to existing readiness and ownership gates.
- [x] Implement Stop/Save unlock semantics. (AC: 6, 8, 9)
  - [x] Use `InnolaTransactionLifecycleCoordinator.SaveProgressAsync`; do not call Complete.
  - [x] After successful save, persist manifest/audit through existing lifecycle services.
  - [x] Clear or mark the active edit lock so Transaction Panel becomes selectable again and Parcel Workflow is disabled.
  - [x] Preserve enough saved transaction state to show the saved transaction as in progress by current user after refresh/mock refresh.
  - [x] On save failure, keep the active transaction locked and preserve Parcel Workflow context.
- [x] Implement completion unlock semantics. (AC: 7, 8)
  - [x] Continue using `InnolaTransactionLifecycleCoordinator.CompleteAsync` and readiness service.
  - [x] On completion success, clear active selection/load state, disable Parcel Workflow, unlock list controls, and refresh the transaction list.
  - [x] On completion blocked/failure, keep active state intact and show the existing safe readiness/failure message.
- [x] Improve status and visual cues. (AC: 3, 4, 5, 6, 7)
  - [x] Show clear status text for selected, starting, active/in progress, saved/stopped, completion blocked, completed, and error states.
  - [x] Style the active row so it remains visually distinct from ordinary selected rows.
  - [x] Keep the compact Sidwell/ArcGIS Pro-adjacent visual language: Segoe UI, neutral surfaces, tight spacing, no large marketing-style panels.
  - [x] Do not add visible instructional text explaining shortcuts or implementation details.
- [x] Add focused tests. (AC: 1-10)
  - [x] Test logged-out gates: Transaction Panel and Parcel Workflow disabled, Configuration/About enabled.
  - [x] Test successful login enables Transaction Panel and refreshes/populates mock rows while Parcel Workflow remains disabled.
  - [x] Test single-click selection does not enable Parcel Workflow.
  - [x] Test double-click/start loads, claims, locks list controls, highlights active row, and enables Parcel Workflow.
  - [x] Test active transaction blocks selecting/double-clicking another row and preserves active transaction state.
  - [x] Test Stop/Save persists lifecycle state, unlocks list, disables Parcel Workflow, and does not call Complete.
  - [x] Test Complete success clears active gates, disables Parcel Workflow, unlocks list, and refreshes transactions.
  - [x] Test failures preserve previous state and redact secrets from status/errors/artifacts.
  - [x] Test mock mode covers the dry-run flow without network access.
- [x] Validate and package. (AC: 1-10)
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.
  - [ ] Manual ArcGIS Pro smoke: mock login, verify list auto-populates, single-click does not enable Parcel Workflow, double-click/start locks panel and enables Parcel Workflow, Stop/Save unlocks list and disables Parcel Workflow, Complete remains readiness-gated.

### Review Findings

- [x] [Review][Patch] Failed Start can enable Parcel Workflow and lock the panel because lifecycle `Error` is treated as active/openable after a claim failure [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs:58]
- [x] [Review][Patch] Stop/Save only color-marks the saved row while clearing session lifecycle state, so the row can still read as available rather than clearly in progress by the current user [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:557]
- [x] [Review][Patch] Complete success depends entirely on refreshed service data to remove the completed transaction, so stale/mock results can keep completed work visible as available [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:589]

## Dev Notes

### Current State From Prior Stories

- Story 2.2 added Sidwell Co ribbon shell, session-only Innola login, command gates, Configuration/About windows, and the Transaction Panel command.
- Story 2.3 added available transaction listing, mock transaction mode, filter/search/sort, selection state, and kept Parcel Workflow disabled on selection alone.
- Story 2.4 added `InnolaTransactionLoadService`, transaction metadata/attachment loading into the Case Folder, and Parcel Workflow enablement after load validation.
- Story 2.5 added lifecycle services, claim/start, save progress, cancel, completion readiness, completion gates, `innola_lifecycle` manifest metadata, and `working\workflow_lifecycle_audit.json`.
- Story 2.6 added environment preflight and async preflight behavior. Do not alter preflight behavior in this story unless needed to preserve existing Parcel Workflow state after lock/unlock.
- Recent manual ArcGIS Pro smoke confirmed mock login and basic transaction flow work after version `0.1.2`; this story tightens the flow so users cannot keep browsing/switching transactions while one transaction is active.

### Existing Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
  - Current command set includes refresh, load, start, stop/save, view documents, add document placeholder, and complete.
  - Current `CanUseListControls` only checks login/loading/row count. Extend it to account for active transaction lock.
  - Current `CanStartTransaction` checks selected loadable row and `!session.HasActiveTransaction`. Preserve this gate.
  - Current `SaveCurrentTransactionAsync` saves progress but does not yet define the desired post-save unlock/Parcel Workflow disable semantics.
  - Current `CompleteCurrentTransactionAsync` refreshes after success; verify it clears all active gates and list lock state.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
  - Current ListBox has no double-click command and no disabled/active-row styling. Add both through bindings/behaviors consistent with WPF.
  - Keep the toolbar compact and icon-based.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
  - Current view model creates `TransactionPanelState` from `ShellState`. Add activation/refresh behavior here if it belongs at dockpane lifecycle level.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
  - Current gates: `CanOpenTransactionPanel`, `CanOpenParcelWorkflow`, `CanStartOrClaimTransaction`, `CanSaveProgress`, `CanCancelActiveProcess`, `CanCompleteTransaction`, `CanSwitchTransaction`, `HasActiveTransaction`.
  - Prefer adjusting these gates or adding small derived properties here rather than scattering state decisions in XAML.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowTransactionPanelButton.cs` and `ShowParcelWorkflowDockpaneButton.cs`
  - Confirm ribbon behavior matches ACs after state changes.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
  - Existing tests already cover refresh, selection, load, start, stop, switch guard, failed refresh, loading state, and logout. Extend these instead of creating a duplicate test style.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSessionManagerTests.cs`
  - Add/adjust gate tests if session-level properties change.

### Required State Model

Use this behavior as the implementation truth:

```text
LoggedOut:
  Login enabled; Transaction Panel disabled; Parcel Workflow disabled; Configuration/About enabled.

LoggedInNoTransaction:
  Transaction Panel enabled and populated; Parcel Workflow disabled.

TransactionSelectedNotStarted:
  Selected row highlighted; Start enabled; Parcel Workflow disabled.

TransactionInProgress:
  Selected row highlighted as active; list/filter/search/sort locked; Parcel Workflow enabled;
  Stop/Save enabled; Complete follows readiness gate.

TransactionSaved:
  Full Case Folder persisted; task remains in progress; list unlocked; Parcel Workflow disabled;
  saved transaction visible as in progress by current user when available.

TransactionCompleted:
  Active transaction cleared; list unlocked/refreshed; Parcel Workflow disabled.
```

### UX Requirements

- Follow `DESIGN.md`: Segoe UI, compact desktop sizing, neutral surfaces, visible separators, restrained color.
- Active transaction highlight must remain visible even if the list is disabled.
- Do not use a large explanatory banner. Use concise status text such as `Transaction TR100000004 is in progress.` or `Progress saved. Select a transaction to continue.`
- Double-click should behave exactly like Start because task-panel users expect double-click to execute/open the selected task.
- Avoid modal prompts for the normal active-lock path. While active, simply prevent selecting another transaction. The existing save/cancel/stay switch guard may remain for edge cases where replacement is attempted programmatically or from older paths.

### Security And Reliability Requirements

- Keep Innola password and token session-only. Do not persist them to settings, manifest, audit, reports, logs, or status text.
- Preserve previous valid state on any failed start/save/complete/refresh.
- Do not clear active transaction state before a lifecycle operation succeeds.
- Do not call live Innola lifecycle endpoints except through the existing `IInnolaTransactionLifecycleService` boundary.
- Mock mode must stay deterministic and network-free.
- Add-in remains on ArcGIS Pro 3.6/3.7 compatibility: `desktopVersion="3.6"`, `net8.0-windows`, Visual Studio 2022/ArcGIS Pro SDK 3.6 lane.

### Scope Boundaries

- Do not implement DWG readiness. The original DWG story shifts to the next backlog slot.
- Do not implement extraction, validation, output generation, report generation, CADINDEX sync, ArcGIS Enterprise writeback, or live Innola document upload.
- Do not rewrite the Transaction Panel as a new control framework. Use the existing WPF/MVVM pattern.
- Do not persist active lock state outside the Case Folder lifecycle artifacts already used by Stories 2.4-2.5 unless required for recovery.

### Testing Notes

- Keep automated tests network-free.
- Use existing fake/mock services and deterministic `TempDirectory` patterns.
- Register new tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`.
- Build/package commands may require elevated execution in this environment because the .NET/ArcGIS SDK reads Windows SDK metadata under `C:\Users\js91482\AppData\Local\Microsoft SDKs`.
- Manual ArcGIS Pro testing is required for double-click and disabled-list visual confirmation.

### References

- `_bmad-output/planning-artifacts/epics.md`: Epic 2 FR24-FR28, UX-DR19-UX-DR22, active transaction and Case Folder rules.
- `_bmad-output/planning-artifacts/architecture.md`: C# owns workflow state/command gates; Case Folder is system of record; ArcGIS Pro add-in WPF/MVVM; no live CADINDEX/Enterprise writeback in v1.
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`: compact ArcGIS Pro-adjacent UI tokens.
- `_bmad-output/implementation-artifacts/2-3-display-available-innola-transactions.md`: transaction list behavior, mock mode, selected transaction must not enable Parcel Workflow.
- `_bmad-output/implementation-artifacts/2-4-load-transaction-details-and-attachments-into-case-folder.md`: transaction load and attachment-to-Case-Folder behavior.
- `_bmad-output/implementation-artifacts/2-5-control-active-transaction-lifecycle-and-completion-gate.md`: lifecycle facade, save/cancel/complete gates, manifest/audit requirements.
- `_bmad-output/implementation-artifacts/2-6-validate-arcgis-pro-and-python-processing-environment.md`: current preflight state and no-DWG boundary.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`: primary state/command implementation target.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`: primary transaction panel UI target.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`: shell and lifecycle gate source.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` - passed.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` - passed, 113 tests.
- Review fix rerun: `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` - passed, 114 tests.
- `tools\validate_contracts.ps1` - passed.
- `tools\run_python_tests.ps1` - passed, 2 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed.
- `tools\package_addin.ps1` - passed after rerun with SDK-profile access; produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Completion Notes List

- Parcel Workflow ribbon gate now opens only for started/claimed transactions, not for load-only transaction metadata.
- Transaction Panel exposes active and saved transaction markers, locks refresh/filter/search/sort/selection while active, and keeps the active row selected.
- Stop/Save persists through the lifecycle coordinator, clears the active edit lock, disables Parcel Workflow, unlocks the panel, and marks the saved transaction row for context.
- Transaction Panel dockpane auto-refreshes when configured by the dockpane view model, while unit tests keep deterministic manual refresh behavior.
- Double-click on the transaction list uses the same Start command path as the toolbar button.
- Manual ArcGIS Pro smoke remains pending for local UI confirmation.
- Code review patches resolved: claim failure now restores loaded/not-started state, saved rows show explicit in-progress text, and completed transaction numbers are locally suppressed from stale refresh results.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionNumberEqualsConverter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-10 | 0.1 | Initial story context for transaction execution state, panel locking, double-click start, and active transaction unlock semantics. | Mary |
| 2026-06-10 | 1.0 | Implemented transaction active-state gates, panel locking, double-click Start, Stop/Save unlock behavior, row state cues, focused tests, and package validation. | Amelia |
| 2026-06-10 | 1.1 | Resolved code review findings for failed Start rollback, saved-row status, and stale completion refresh suppression. | Amelia |
