---
baseline_commit: handoff-2026-06-17
---

# Story 2.17: Stabilize Transaction Return And Refresh After Workflow Exit

Status: review

## Story

As a cadastral examiner working from the Transaction List,  
I want the add-in to reliably return me to the transaction list after canceling or completing a workflow,  
so that I can safely move to the next transaction without stale locks, stale selection state, or inconsistent refresh behavior.

## Acceptance Criteria

1. Given a transaction is active in `Parcel Workflow [Compute]`, when the user cancels the workflow process from the shell or after returning from a subordinate review workspace, then the add-in returns to the Transaction List context, clears the active workflow lock, and disables the workflow for that transaction until it is started again.
2. Given a transaction completes successfully, when the completion flow finishes, then the add-in returns to the Transaction List context, clears the active transaction state, and refreshes the transaction list so the completed transaction no longer appears as available work when Innola no longer returns it.
3. Given the workflow returns to the Transaction List after cancel, suspend, or complete, when the list is shown again, then search, filter, sort, row selection, and action buttons are restored to a valid interactive state regardless of whether the last in-workflow activity was shell-only, Jamaica COGO Tool review, or a manual-review branch.
4. Given the user presses Refresh in the Transaction List after workflow exit, when the refresh succeeds, then the visible list reflects the latest available transactions and does not retain stale active-row markers from the prior workflow session.
5. Given refresh fails in mock or live mode, when the failure is handled, then the user sees a clear non-secret error and the existing list remains usable rather than blank or partially locked.
6. Given a transaction is reopened after prior cancel or suspend activity, when the examiner starts it again, then the workflow opens cleanly from the correct saved or fresh state without carrying stale list-only UI state into the workflow shell.
7. Given this story is complete, then focused verification covers cancel-to-list, complete-to-list, refresh-after-exit, and repeated open/close cycles in both mock mode and the current live-mode behavior where feasible.

## Tasks / Subtasks

- [x] Audit the current workflow exit paths. (AC: 1-6)
  - [x] Trace cancel, suspend, complete, and close-window paths from `Parcel Workflow [Compute]` back into Transaction List state.
  - [x] Identify where selection, active transaction, and refresh state can become inconsistent.
  - [x] Preserve existing resume/save semantics while fixing list-return behavior.

- [x] Stabilize return-to-list behavior after workflow exit. (AC: 1-3, 6)
  - [x] Ensure Cancel returns the user to the Transaction List context and clears workflow-only UI state.
  - [x] Ensure Complete returns the user to the Transaction List context and clears active transaction state.
  - [x] Ensure suspend/save returns control cleanly without leaving stale active markers or disabled list controls.

- [x] Make Refresh reliable after workflow exit. (AC: 3-5, 7)
  - [x] Reconcile refresh behavior after cancel, suspend, and complete.
  - [x] Preserve the last valid list when refresh fails.
  - [x] Keep mock and live refresh paths behaviorally consistent from the user’s point of view.

- [x] Add focused tests and smoke guidance. (AC: 7)
  - [x] Cover cancel-to-list state reset.
  - [x] Cover complete-to-list state reset.
  - [x] Cover refresh after workflow exit.
  - [x] Cover repeated start/cancel/start and start/complete/refresh cycles.

## Dev Notes

### Why This Story Exists

- The user has observed that returning from the workflow to the Transaction List is not yet reliable enough for normal transaction-by-transaction work.
- This is not just a polish issue; it affects throughput and operator confidence because examiners need to move from one task to the next without wondering whether the list reflects the real Innola state.

### Architectural Direction

- Treat the Transaction List as the authoritative post-exit landing surface for the current add-in shell.
- Preserve the separation between:
  - transaction-list session state,
  - active workflow session state,
  - and saved/resumable case state.
- Do not silently keep the user inside a stale workflow context after cancel or complete.

### Scope Boundaries

- This story does not redesign stage names or workflow microcopy.
- This story does not change the business rules for suspend/resume packages.
- This story does not decide whether extraction should be rerun or routed to manual review; that decision belongs after Point Review extraction results are available.
- This story does not change extraction, validation, or output-generation logic.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`

## References

- `_bmad-output/implementation-artifacts/2-5-control-active-transaction-lifecycle-and-completion-gate.md`
- `_bmad-output/implementation-artifacts/2-7-enforce-transaction-execution-state-and-panel-locking.md`
- `_bmad-output/implementation-artifacts/2-8a-wire-live-innola-api-contracts-while-preserving-mock-mode.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added an explicit transaction-list workflow-exit handoff so `Parcel Workflow [Compute]` can return control to the Transaction List after suspend, cancel, and complete.
- Wired the workflow pane to activate the Transaction List pane after successful exit actions and to pass along saved/completed transaction context.
- Added transaction-panel state handling for workflow exit so saved transactions remain selected for context and completed transactions are locally suppressed even if refresh returns stale rows.
- Tightened lifecycle gate expressions in `InnolaSessionManager` so active-lock behavior is deterministic after workflow exit.
- Added focused transaction-panel tests for suspend, cancel, and complete exit handling in addition to the existing stale-refresh and list-lock coverage.
- Added best-effort active map cleanup after successful PE cancel, suspend, and finalize so the `TR <transaction> - Review` group is removed from Contents before the workflow form resets back to the transaction list.
- Kept cleanup after lifecycle success only; failed suspend/finalize/cancel attempts preserve the review map context for recovery.
- Added focused regression coverage that all three PE exit paths route through the shared transaction review map cleanup helper.

### File List

- `_bmad-output/implementation-artifacts/2-17-stabilize-transaction-return-and-refresh-after-workflow-exit.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ParcelWorkflowDockpaneExitCleanupTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for reliable return-to-list and refresh stabilization after workflow cancel, suspend, and complete actions. | Codex |
| 2026-06-17 | 1.0 | Implemented workflow-exit handoff to the Transaction List, refresh/suppression handling for completed transactions, lifecycle-gate tightening, and focused tests. | Codex |
| 2026-07-21 | 1.1 | Extended successful PE workflow exits to clean the active `TR <transaction> - Review` map group from ArcGIS Pro Contents on cancel, suspend, and finalize. | Codex |
