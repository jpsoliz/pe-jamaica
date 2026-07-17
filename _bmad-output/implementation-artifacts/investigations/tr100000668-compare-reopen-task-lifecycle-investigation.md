# Investigation: TR100000668 Compare Reopen Task Lifecycle

## Hand-Off Brief

TR100000668 can load Compare once, but closing the Compare window does not release or suspend the Innola task. The local case folder is readable and contains a Compare draft; later attempts to start/claim the task failed with Innola `Unauthorized`. The immediate product gap is that Compare has window-level close/save controls but no clear task-level Suspend/Complete/reopen path inside the Compare experience.

## Case Info

| Field | Value |
| --- | --- |
| Transaction | TR100000668 / 100000668 |
| Case folder | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668` |
| Date | 2026-07-16 |
| Status | Active |

## Problem Statement

User reports that after closing the Compare form for TR100000668, loading it again is not successful. The user also needs a workflow model that supports starting tasks, reviewing evidence, and then completing or suspending the task.

## Confirmed Evidence

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs:81` has `CloseButton_Click` implemented as a plain window close. It does not call task save, suspend, complete, or panel workflow exit.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs:1103` through `:1129` implements Compare `SaveProgress` as draft persistence only.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:399` through `:410` disables load/start while `session.HasActiveTransaction` is true, while `CanStopTask` depends on task-level save-progress permission.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:761` through `:779` has the task-level suspend path: `SaveCurrentTransactionAsync` calls `SaveAndCloseAsync`, preserves the selected transaction, and marks it suspended.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:148` through `:185` uploads the resume package, calls save progress, and clears the loaded transaction.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\working\compare_review_draft.json` exists and contains `decision_state = returned_to_compute`, so the Compare draft itself is available.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\working\workflow_lifecycle_audit.json` shows a successful claim at `2026-07-16T14:49:52.3653542Z`, then failed claim retries at `14:55:14Z`, `14:59:08Z`, and `15:05:03Z` with `error_category = Unauthorized`.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\manifest.json` payload has `innola_lifecycle.status = error` and `last_error_category = Unauthorized`.

## Deduced Conclusion

The second-load failure is not explained by a missing local Compare draft or missing source PDF. The evidence points to two combined issues:

1. Compare window close is UI-only and leaves task lifecycle responsibility in the Transaction Panel.
2. Later re-start attempts reached the Innola claim/start operation and failed with `Unauthorized`.

This means the UX currently lets the user close the evidence workspace without choosing whether the task remains active, is suspended for later, or is completed.

## Product Options

1. Keep `Close` as hide-only, add `Reopen Compare` for active Compare tasks in the Transaction Panel.
2. Change Compare `Close` into an explicit dialog: `Keep task active`, `Suspend task`, `Cancel local work`, `Close window only`.
3. Add Compare footer task actions: `Save draft`, `Suspend task`, `Complete task`, `Close`.
4. Add a stage-aware active-task dock/button that always opens the correct active workspace: Compute or Compare.

## Recommended Direction

Implement option 3 plus option 4:

- In the Compare form, rename current `Save progress` to `Save draft`.
- Add `Suspend task` that calls the same `SaveAndCloseAsync` lifecycle path as the Transaction Panel.
- Add `Complete task` gated by Compare decision readiness.
- Keep `Close` as window-only, but make the text clear.
- Add a Transaction Panel command to reopen the active Compare workspace when the task is already in progress.

## Missing Evidence

- Exact UI action used after closing Compare: Start button, double-click row, ribbon button, or Transaction Panel reload.
- Current Innola session token validity at the moment the failed `Unauthorized` claim occurred.

## Verification Plan

1. Start TR100000668 or another Compare task.
2. Close the Compare window with `Close`.
3. Confirm Transaction Panel shows active task and exposes `Reopen Compare` plus `Suspend task`.
4. Click `Reopen Compare`; the Compare window should reopen without re-claiming the task.
5. Click `Suspend task`; the resume package should upload, the active task should clear, and the row should remain selectable.
