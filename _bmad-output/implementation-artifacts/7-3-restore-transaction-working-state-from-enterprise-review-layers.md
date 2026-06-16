---
baseline_commit: handoff-2026-06-15
---

# Story 7.3: Restore Transaction Working State From Enterprise Review Layers

Status: review

## Story

As a cadastral examiner,  
I want a reopened transaction to restore its distributed working-state context,  
so that I can continue review from the latest saved stage even when the work has moved across sessions or machines.

## Acceptance Criteria

1. Given a transaction has previously published geometry into Enterprise working layers, when the user reopens that transaction, then the add-in can detect whether distributed working-state geometry exists for the transaction.
2. Given Enterprise working-state geometry exists, when the user restores the transaction, then the add-in can rehydrate the map context from the Enterprise working workspace without losing the local Case Folder artifacts.
3. Given a restored transaction may have mixed local and Enterprise recoverability, when the workflow opens, then it clearly indicates whether the case was restored from local-only state, Enterprise working state, or both.
4. Given Enterprise features are missing, partial, or stale, when reopen occurs, then the add-in reports recoverability issues explicitly rather than silently pretending the transaction restored cleanly.
5. Given transaction ownership and stage-lock rules already exist, when a distributed restore happens, then those rules still prevent conflicting parallel execution.

## Tasks / Subtasks

- [x] Detect Enterprise working-state presence during reopen. (AC: 1, 3-4)
  - [x] Query for matching transaction geometry/state in configured working layers.
  - [x] Reconcile Enterprise findings with the local Case Folder resume state.

- [x] Restore map context safely. (AC: 2-4)
  - [x] Reload working layers or transaction-scoped selections into the map.
  - [x] Surface partial restore warnings when expected features are missing.

- [x] Preserve lifecycle and locking behavior. (AC: 5)
  - [x] Ensure restore does not bypass active transaction ownership checks.
  - [x] Keep stage progression rules consistent after Enterprise rehydration.

## Dev Notes

### Architectural Direction

- Reopen should merge local artifact truth with Enterprise spatial-state visibility.
- Enterprise restore improves continuity but must not replace local audit/recovery artifacts.

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/implementation-artifacts/4-3-save-and-resume-transaction-cases-through-innola-resume-package.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet run --project src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.Tests\\ParcelWorkflowAddIn.Tests.csproj`
- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.sln /nodeReuse:false /p:UseSharedCompilation=false`

### Completion Notes

- Added enterprise working-state restore orchestration so reopen can inspect configured enterprise working-layer stores and detect transaction-scoped distributed state.
- Added a restore snapshot artifact (`enterprise_working_restore.json`) so reopened cases keep an explicit local record of the enterprise state that was detected.
- Reconciled enterprise findings with local case-folder truth: local output summaries remain preferred when present, and enterprise state can synthesize a safe fallback output summary when the local summary is missing.
- Preserved lifecycle safety by reopening enterprise-only restores into a reviewable state when prior spatial approval can no longer be fully verified from local artifacts.
- Added explicit partial-restore warnings when configured enterprise layer roles are missing or incomplete during reopen.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IEnterpriseWorkingStateRestoreService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
