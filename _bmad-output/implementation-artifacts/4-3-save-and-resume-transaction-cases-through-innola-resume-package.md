---
baseline_commit: handoff-2026-06-12
---

# Story 4.3: Save And Resume Transaction Cases Through Innola Resume Package

Status: updated

## Story

As a cadastral technical staff user,
I want to suspend or complete the current transaction case with a package attached back to Innola,
so that I can pause work safely, resume later at the same workflow stage, or finalize the case with a completed package and clear lifecycle state.

## Acceptance Criteria

1. Given a transaction has an active local Case Folder and is not yet completed, when the user chooses `Suspend`, then the add-in persists the current workflow state, packages the resumable case contents into a zip file, attaches it to Innola, closes the workflow form, and keeps the transaction incomplete.
2. Given the resumable package is created, when the add-in syncs it back to Innola, then the package is attached to the current transaction using a deterministic Sidwell resume-package naming convention and metadata that identifies transaction number, workflow stage, saved timestamp, operator, and add-in version.
3. Given a transaction already has a saved Sidwell resume package attached, when the user loads that transaction again from the Transaction Panel, then the add-in detects the package and restores the local Case Folder from it instead of treating the load as a brand-new case.
4. Given a saved transaction is restored, when the Parcel Workflow opens, then the workflow resumes at the previously saved stage with the last persisted artifacts, review edits, approval/validation state, and available source/output files restored from the package.
5. Given the user suspends work and later returns, when the transaction is reopened, then the saved package remains the authoritative resumable snapshot and the user does not need to rerun already completed stages unless the saved package is missing, stale, or corrupt.
6. Given the add-in resumes a saved package, when the transaction metadata or package identity does not match the selected transaction, then restore is blocked and the user receives a clear message rather than silently opening the wrong case.
7. Given the resume package is missing, unreadable, corrupt, or fails upload/download, when suspend or reopen is attempted, then the user receives a deterministic error/fallback state and no existing saved package is overwritten with invalid content.
8. Given a transaction is suspended for later resume, when the user returns to the Transaction Panel, then the current transaction is released from the active lock so another transaction can be selected and worked without completing the first one.
9. Given this story is complete, then `Cancel` is a separate lifecycle action from `Suspend`; `Cancel` discards the current local UI session, while `Suspend` creates or refreshes the Innola resume package after user confirmation.
10. Given the user chooses `Approve`, when the current workflow is ready for completion, then the add-in saves the final state, uploads a final package to Innola, marks the transaction completed, and closes the workflow form.
11. Given the Parcel Workflow header is shown for a loaded transaction, then the state badge communicates `New Case` when no resume package was restored and `Existing Case` when the session was reopened from a saved Innola package.
12. Given this story is complete, then focused tests cover package creation, Innola attachment sync, saved-state detection on reload, stage restoration, stale/corrupt package handling, new-vs-existing case labeling, and no regression to current suspend/cancel/claim/complete lifecycle gating.

## Tasks / Subtasks

- [ ] Add a resumable case-package contract and naming convention. (AC: 1-3, 6-7, 9-10)
  - [ ] Define the Sidwell resume zip file naming pattern for Innola attachments.
  - [ ] Define a small resume manifest inside the package with transaction identity, saved workflow stage, timestamp, operator, add-in version, and integrity/hash fields.
  - [ ] Ensure transaction number / task identity mismatches block restore cleanly.

- [ ] Add `Suspend` as a first-class lifecycle action. (AC: 1, 5, 8-9)
  - [ ] Introduce a dedicated command separate from `Cancel` and `Approve`.
  - [ ] Persist the current local workflow state before packaging.
  - [ ] Release the active transaction lock after successful suspend so another transaction can be started.

- [ ] Implement case-folder packaging for resume. (AC: 1-2, 5, 7, 10)
  - [ ] Select which case-folder contents are required in the resume package: manifest, source references/copied files, working artifacts, approval/validation artifacts, logs, and output previews where applicable.
  - [ ] Exclude transient/bad artifacts that should be regenerated rather than resumed.
  - [ ] Prevent invalid or partial packages from overwriting the last good saved package.

- [ ] Implement Innola attachment upload/download support for resume packages. (AC: 2-3, 5, 7, 10)
  - [ ] Add attachment-upload capability for the resume package to the current transaction.
  - [ ] Detect whether a current transaction already contains a Sidwell resume package attachment.
  - [ ] Download the package when present and selected for restore.

- [ ] Implement restore/reopen flow from transaction load. (AC: 3-7, 10)
  - [ ] On transaction load, check Innola attachment metadata for the Sidwell resume package before creating a fresh case.
  - [ ] Restore the local Case Folder from the saved package when available and valid.
  - [ ] Reopen `WorkflowSession` from restored manifest/artifacts so the active stage and artifacts match the saved snapshot.

- [ ] Preserve lifecycle semantics and UX clarity. (AC: 5, 8-11)
  - [ ] Keep `Cancel` as local abandon/reset only, with confirmation.
  - [ ] Show clear user-facing status that distinguishes `Suspended`, `Cancelled locally`, `Restored from saved package`, `Opened as new case`, and `Completed`.
  - [ ] Replace the internal `Local v1` badge with a case-state badge that shows `New Case` or `Existing Case`.
  - [ ] Ensure transaction panel locking/unlocking still follows the active-transaction rules established in Epic 2.

- [ ] Add focused tests and validation. (AC: 12)
  - [ ] Test successful package creation from an in-progress case.
  - [ ] Test reload detection and restore to the correct saved stage.
  - [ ] Test corrupt/missing/mismatched package rejection.
  - [ ] Test active-lock release after suspend.
  - [ ] Test `New Case` vs `Existing Case` badge semantics on first load and resumed load.
  - [ ] Test no regression to existing suspend, cancel, and completion gate behavior.

## Dev Notes

### Why This Story Exists

- The current lifecycle supports local `Save progress` and local `Cancel`, but not a transaction-backed suspend/resume flow with explicit user intent.
- The user needs to stop work on one transaction, move to another, and later resume the first without rebuilding the local case manually.
- The Innola transaction should become the source of truth for resumable case state, not just the local workstation.

### Current Implementation Reality

Today the system already has several pieces we can build on:

- `WorkflowSession` already persists workflow state through the local Case Folder and can reopen from `manifest.json` plus artifacts.
- `InnolaTransactionLifecycleCoordinator` already supports `SaveProgressAsync`, `CancelActiveProcess`, and `CompleteAsync`.
- `TransactionPanelState` already releases the currently loaded transaction after local save progress so users can work another transaction.
- The transaction-load path already downloads transaction attachments into the Case Folder during new-case setup.

What is missing is the server-backed resume artifact and final-action clarity:

- no zip/export of the local case state,
- no upload of a resume artifact back to Innola,
- no attachment-based restore detection on reload,
- no dedicated `Suspend` UX separate from `Cancel`,
- and no explicit completed-package behavior behind `Approve`.

### Scope Intent

This story should add a resumable transaction package flow that:

1. saves the current case state,
2. zips the resumable case contents,
3. uploads that package to Innola,
4. later detects and restores it when the same transaction is selected again,
5. distinguishes resumed cases from new cases in the UI,
6. and preserves a clean split between suspend and complete.

This story should **not**:

- complete the Innola task,
- blur `Cancel` and `Save and Close`,
- change validation/output business rules beyond preserving them through restore,
- or introduce final sync/readiness completion logic.

### Recommended UX Semantics

Use distinct lifecycle actions:

- `Save` or `Save Progress`: save current local work state while remaining in the current active session
- `Suspend`: create/update the Innola resume package, confirm with the user, and release the transaction
- `Cancel`: abandon the active local UI session without creating a new resume package
- `Approve`: save final state, upload final package, and complete the transaction once readiness passes

Do not overload `Cancel` to mean suspend-save; that would make lifecycle intent ambiguous.

Use a case-state badge in the workflow header:

- `New Case`: transaction opened without a restored resume package
- `Existing Case`: transaction restored from a saved Innola resume package

### Resume Package Contract Guidance

The package should be deterministic and safe to inspect:

- zip name pattern such as `sidwell-case-state-{transaction_number}.zip`
- internal resume manifest with:
  - transaction number
  - task id / transaction id
  - saved workflow state
  - saved timestamp
  - operator id/display
  - add-in version
  - package schema version
  - integrity/hash metadata

The restore flow must verify that the selected transaction matches the package identity before reuse.

### Suggested Technical Direction

Likely implementation shape:

1. add a case-package service under the Case Folder / Innola boundary,
2. generate the zip from the current case folder after local state is flushed,
3. upload the zip through a new Innola attachment service,
4. extend transaction-load flow to detect/download/restore the package before fresh-case creation,
5. reopen the restored folder through the existing `WorkflowSession.ReopenCaseFolder(...)` path,
6. expose a restored/new-case flag through session/view model state so the UI badge is meaningful,
7. keep `Approve` distinct from `Suspend` in lifecycle and attachment semantics.

### Code Areas That Matter

Read and preserve behavior in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`

### Preservation Rules

- Do not break current local reopen/resume behavior from case folder artifacts.
- Do not regress transaction locking rules: only one active transaction at a time.
- Do not clear the last good saved package if a new package build/upload fails.
- Do not trust attachment contents unless transaction identity matches.

### Testing Guidance

Prefer focused tests at these boundaries:

- package creation and integrity,
- upload/download contract behavior,
- saved package detection on transaction reload,
- restore to `review_pending`, `review_approved`, `validation_blocked`, and `validation_passed` states,
- `New Case` vs `Existing Case` badge behavior,
- and lifecycle gating after `Suspend` vs `Cancel` vs `Approve`.

### References

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/2-5-control-active-transaction-lifecycle-and-completion-gate.md`
- `_bmad-output/implementation-artifacts/2-7-enforce-transaction-execution-state-and-panel-locking.md`
- `_bmad-output/implementation-artifacts/2-12-execute-draft-extraction-and-review-artifact-generation.md`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`
- `_bmad-output/implementation-artifacts/4-1-run-validation-on-approved-review-data.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Completion Notes List

- Updated Story 4.3 to reflect the preferred lifecycle semantics: `Cancel`, `Suspend`, and `Approve`.
- Added explicit `New Case` vs `Existing Case` badge semantics to replace the placeholder `Local v1` label.
- Kept the story anchored in current local reopen/lifecycle behavior so implementation can extend, not replace, existing case-folder recovery.

### File List

- `_bmad-output/implementation-artifacts/4-3-save-and-resume-transaction-cases-through-innola-resume-package.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for transaction-backed save/resume through an Innola resume package. | Codex |
| 2026-06-12 | 0.2 | Updated lifecycle semantics to Cancel / Suspend / Approve and added New Case / Existing Case badge behavior. | Codex |
