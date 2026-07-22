---
baseline_commit: handoff-2026-07-14
---

# Story 8.4E: Add Compare Task Lifecycle Actions And Reopen Active Workspace

Status: review

## Story

As a cadastral examiner in Compare,  
I want clear task lifecycle actions inside the Compare workspace,  
so that I can save evidence notes, suspend work for later, complete finalized work, or close the window without accidentally leaving the transaction in an unclear state.

## Business Context

Compare is now a real evidence reconciliation workspace. The examiner can load the transaction PDF, load transaction-scoped working-review geometry into ArcGIS Pro, query Legal/Fiscal evidence, curate results, and save Compare decisions. Field testing on `TR100000668` exposed a lifecycle gap: closing the Compare window only closes the WPF form. It does not suspend, complete, release, or refresh the task. After the window is closed, users can be left with an active transaction in the Transaction Panel but no obvious way to reopen Compare without trying to start/claim the task again.

This story adds explicit Compare-stage lifecycle actions and a reopen path for active Compare tasks. It must reuse the existing task lifecycle services and Transaction Panel state gates rather than creating a second lifecycle state machine.

## Dependency Notes

- **Story 2.5: Control Active Transaction Lifecycle and Completion Gate**
  - Reuse `InnolaTransactionLifecycleCoordinator.SaveAndCloseAsync(...)`, `CompleteAsync(...)`, manifest lifecycle updates, resume package upload, lifecycle audit records, and sanitized failure handling.
  - Do not add a parallel suspend/complete implementation inside Compare.

- **Story 2.7: Enforce Transaction Execution State and Panel Locking**
  - Preserve Transaction Panel active-lock behavior.
  - When Compare suspends or completes a task, the Transaction Panel must unlock, clear loaded state, update row context, and refresh/suppress rows using the existing `TransactionPanelState` patterns.

- **Story 8.1: Add Compare Stage Gating and Transaction Launch**
  - Reuse the Compare launch seam and stage routing.
  - Add a reopen path that can open the active Compare workspace without re-running claim/start against Innola.

- **Story 8.5: Persist Compare Decision and Unlock Commit Stage**
  - Reuse Compare draft and decision artifacts.
  - `Complete task` is enabled only when Compare is finalized/approved and task completion readiness passes.
  - `Save draft` must continue to write Compare evidence/notes only; it must not release the task.

## Acceptance Criteria

1. Given Compare is open for an active transaction, when the user clicks `Save draft`, then the add-in saves Compare evidence, notes, query history, included/excluded Enterprise evidence, and decision draft artifacts only; the Innola task remains active and the window remains open.
2. Given Compare is open for an active transaction, when the user clicks `Suspend task`, then the add-in saves the Compare draft, uploads the resume package, calls the existing task save-and-close lifecycle path, clears loaded/active transaction state, refreshes or restores the Transaction Panel list, and closes the Compare window.
3. Given `Suspend task` fails because upload, save-progress, auth, timeout, or lifecycle service fails, then the Compare window remains open, the active transaction state is preserved, a sanitized retryable message is shown, and no token/raw response is written to UI or artifacts.
4. Given Compare is finalized and completion readiness passes, when the user clicks `Complete task`, then the add-in saves the final Compare decision, calls the existing completion lifecycle path, clears loaded/active transaction state, refreshes the Transaction Panel list, suppresses stale completed rows, and closes the Compare window.
5. Given Compare is not finalized, has unresolved blockers, missing required evidence, or completion readiness fails, when the user views `Complete task`, then the action is disabled or returns a clear blocked message and does not call the lifecycle completion service.
6. Given completion fails after being attempted, when the user returns to Compare, then previous valid active transaction state and Compare artifacts are preserved, and the user sees a sanitized retryable message.
7. Given the user clicks `Close window`, then the Compare window closes and the transaction-scoped Compare map group is removed; no task save, task suspend, completion, cancellation, row suppression, or lifecycle mutation occurs.
8. Given a Compare task remains active after `Close window`, when the Transaction Panel is visible, then it exposes a `Reopen Compare` action for the active Compare task.
9. Given the user clicks `Reopen Compare` for the active Compare task, then the Compare workspace opens using the already selected/loaded active transaction and does not call claim/start again.
10. Given a transaction is in Compute stage or no active transaction exists, then `Reopen Compare` is not available.
11. Given Compare suspends, completes, or close-window-only exits, then Transaction Panel command states and list-lock state are updated consistently with Stories 2.5 and 2.7, and stale `Compare Review - <transaction>` map groups are removed from the active map.
12. Given automated tests run, then they cover Save Draft, Suspend success/failure, Complete success/blocked/failure, Close window only, Reopen Compare without claim, Transaction Panel refresh/suppression behavior, and sanitized failure messages.
13. Given the user clicks `Save`, `Suspend`, or `Finalize`, then the add-in asks for confirmation before mutating saved/task state.
14. Given Suspend, Finalize, or Close window proceeds after required confirmation, then the Compare form state is cleared and every loaded Compare map group for that transaction is removed from the active map.

## Tasks / Subtasks

- [x] Add Compare lifecycle command surface. (AC: 1-7)
  - [x] Rename the current Compare `Save progress` button/copy to `Save draft`.
  - [x] Add `Suspend task` command to the Compare workspace.
  - [x] Add `Complete task` command to the Compare workspace.
  - [x] Rename the window-only close affordance to `Close window`.
  - [x] Keep `Block` and the final approving action intact; the approving action is now labeled `Finalize`.

- [x] Add a Compare-to-TransactionPanel lifecycle bridge. (AC: 2-6, 11)
  - [x] Add a small interface or callback seam so `CompareWorkspaceViewModel` can request task suspend/complete without directly reaching into WPF dockpane internals.
  - [x] Reuse `InnolaTransactionLifecycleCoordinator.SaveAndCloseAsync(...)` for suspend.
  - [x] Reuse `InnolaTransactionLifecycleCoordinator.CompleteAsync(...)` for complete.
  - [x] Reuse or extend `TransactionPanelState.HandleWorkflowExitAsync(...)` so the list unlocks and refreshes after suspend/complete.
  - [x] Preserve active state on lifecycle failure.

- [x] Add active Compare reopen behavior. (AC: 8-10)
  - [x] Add a Transaction Panel command such as `ReopenCompareCommand`.
  - [x] Gate it to active Compare transactions only.
  - [x] Reuse `ShellState.OpenCompareWorkspace(transactionNumber)` or the existing injected compare launcher.
  - [x] Ensure reopen does not call `StartSelectedTransactionAsync`, `StartOrClaimAsync`, or Innola claim/start.
  - [x] Add status copy when reopen is unavailable because the active task is Compute or no task is active.

- [x] Wire Compare window close/cleanup behavior. (AC: 2-7, 11)
  - [x] On suspend success, close the Compare window after cleanup/refresh is complete.
  - [x] On complete success, close the Compare window after cleanup/refresh is complete.
  - [x] On `Close window`, remove the transaction-scoped Compare map group, close only the window, and leave active task state unchanged.
  - [x] Avoid duplicate cleanup when the user closes the window after suspend/complete has already closed it.
  - [x] Ensure WebView2/PDF resources are still disposed through normal window close.
  - [x] Confirm Save, Suspend, and Finalize before saving or changing task state.
  - [x] Remove all matching transaction-scoped `Compare Review - <transaction>` map groups after successful Suspend/Finalize and window-only Close.
  - [x] Clear Compare form state after successful Suspend/Finalize/Close cleanup.

- [x] Preserve Compare decision semantics. (AC: 1, 4, 5)
  - [x] `Save draft` writes only `compare_review_draft.json`.
  - [x] `Complete task` requires a finalized/current Compare decision from Story 8.5.
  - [x] If needed, have `Complete task` save/confirm a finalized decision before lifecycle complete, but do not invent commit-stage promotion.
  - [x] Do not let `Block` call task completion.

- [x] Add tests. (AC: 1-12)
  - [x] Add Compare ViewModel tests for Save Draft not calling lifecycle.
  - [x] Add suspend success/failure tests with fake lifecycle bridge.
  - [x] Add complete blocked/success/failure tests with fake readiness/lifecycle bridge.
  - [x] Add Transaction Panel tests for `Reopen Compare` availability and no claim/start call.
  - [x] Add XAML smoke tests for `Save draft`, `Suspend task`, `Complete task`, and `Close window` controls.
  - [x] Add regression coverage for `TR100000668` style flow: close window, reopen active Compare without re-claim, then suspend.

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`

Current behavior to preserve:

- `CompareWorkspaceViewModel.SaveProgress()` currently saves Compare draft artifacts only.
- `CompareWorkspaceWindow.CloseButton_Click(...)` currently calls `Close()` only.
- `TransactionPanelState.SaveCurrentTransactionAsync(...)` already uses `SaveAndCloseAsync(...)`, clears loaded transaction state, restores the selected row, and marks the transaction as suspended.
- `TransactionPanelState.CompleteCurrentTransactionAsync(...)` already calls the completion lifecycle path, clears selection, refreshes rows, and suppresses stale completed rows.
- `ShellState.OpenCompareWorkspace(...)` already builds the Compare ViewModel and window for the selected Compare transaction.

Recommended implementation shape:

```csharp
public interface ICompareTaskLifecycleService
{
    Task<CompareTaskLifecycleResult> SuspendAsync(string transactionNumber, CancellationToken cancellationToken = default);
    Task<CompareTaskLifecycleResult> CompleteAsync(string transactionNumber, CancellationToken cancellationToken = default);
}
```

The concrete implementation can wrap existing shell services:

- save Compare draft first
- call `ShellState.LifecycleCoordinator.SaveAndCloseAsync(...)` or `CompleteAsync(...)`
- call/notify `TransactionPanelState.HandleWorkflowExitAsync(...)`
- return a sanitized result to the Compare ViewModel

If direct `ShellState` usage would make testing hard, inject delegates from `ShellState.OpenCompareWorkspace(...)` when constructing `CompareWorkspaceViewModel`.

## UX Notes

Sally/Mary guidance:

- Use distinct labels so users understand the difference:
  - `Save draft`: Compare evidence only.
  - `Suspend task`: save package and release task for later.
  - `Complete task`: finish the task after approval/readiness.
  - `Close window`: hide/close the sidecar only.
- The footer should not imply that closing the window saves or releases work.
- If space is tight, place `Save draft`, `Suspend task`, and `Complete task` together in the decision panel; keep `Close window` in the bottom-right window controls.
- Show brief status after blocked lifecycle actions, not modal explanations.
- On active Compare tasks, Transaction Panel should show a visible `Reopen Compare` affordance near Start/Stop/Complete rather than forcing the user to guess that Start is disabled.

## Security And Reliability Requirements

- Never write access tokens, passwords, signed URLs, raw request/response bodies, or full exception dumps to status text, draft/decision files, manifest, lifecycle audit, or tests.
- Preserve active transaction state after failed suspend or complete.
- Do not call claim/start when reopening an already active Compare task.
- Do not call complete unless Compare decision readiness and lifecycle readiness pass.
- Keep mock mode deterministic and network-free.
- Keep ArcGIS Pro 3.6 / `net8.0-windows` compatibility.

## Testing Notes

Run after implementation:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "transaction panel"
```

Manual validation target:

- Open Compare for `TR100000668`.
- Click `Cancel`.
- Confirm Transaction Panel still shows the active Compare task and exposes `Reopen Compare`.
- Click `Reopen Compare`; confirm Compare opens without a new claim/start attempt.
- Click `Save draft`; confirm the task remains active.
- Click `Suspend task`; confirm resume package upload/save occurs, Compare closes, Transaction Panel unlocks/refreshes, and the task can be resumed later.
- For a finalized Compare decision, click `Complete task`; confirm Compare closes, Transaction Panel refreshes, and stale completed rows are suppressed.

## Investigation Reference

- `_bmad-output/implementation-artifacts/investigations/tr100000668-compare-reopen-task-lifecycle-investigation.md`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-16 | 1.0 | Initial story for Compare task lifecycle actions, close-window semantics, and active Compare reopen path. | Mary |
| 2026-07-16 | 1.1 | Implemented Compare lifecycle commands, Transaction Panel reopen bridge, and regression tests. | Amelia |
| 2026-07-16 | 1.2 | Patched review finding: Complete task now revalidates current Compare readiness after approval. | Amelia |
| 2026-07-17 | 1.3 | Aligned lifecycle copy with Story 8.5 Finalize terminology and removal of Return to Compute from Compare. | Codex |
| 2026-07-17 | 1.4 | Added confirmation prompts and Compare form/map cleanup after confirmed Suspend/Finalize. | Codex |

## Dev Agent Record

### Implementation Plan

- Add a small Compare lifecycle bridge contract so the Compare ViewModel can request suspend/complete without owning transaction state.
- Keep Transaction Panel as the owner of lifecycle state by routing Compare suspend/complete through the existing coordinator-backed panel paths.
- Add a gated `ReopenCompareCommand` that launches Compare for the active transaction without calling claim/start again.
- Update Compare copy and tests to make the difference between draft save, task suspend/complete, and window close explicit.

### Debug Log

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare workspace"` passed 28 tests.
- First parallel `transaction panel` slice collided with a test DLL write lock while the compare slice was building/running; reran serially.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "transaction panel"` passed 28 tests.
- Full harness initially timed out at 3 minutes; reran with a longer timeout.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 435 tests.
- Review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare workspace"` passed 29 tests.
- Review patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed after rerunning serially; the first parallel build attempt hit a transient compiler output lock.
- Button contract patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- compare` passed 110 tests.
- Button contract patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 476 tests.
- Button contract patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` passed with 0 warnings/errors.

### Completion Notes

- Added `ICompareTaskLifecycleService` and `CompareTaskLifecycleResult`.
- Compare now exposes `Save`, `Suspend`, `Finalize`, and `Cancel` with distinct behavior.
- `Save` writes the current Compare status and regenerates the PDF report without releasing or completing the transaction task.
- `Suspend` saves current status through the lifecycle bridge, uploads the resume/status package to the transaction, then clears the Compare form and map content.
- `Finalize` saves current status, regenerates and uploads the PDF report to the transaction, completes the lifecycle task, then clears the Compare form and map content.
- `Cancel` closes the Compare workspace without saving and clears the Compare form and map content.
- Successful suspend/complete asks the window to close after Transaction Panel cleanup; failures keep Compare open and preserve active task state.
- Transaction Panel now exposes gated `ReopenCompareCommand` for active Compare-stage tasks and passes a lifecycle bridge to Compare launches.
- Reopen Compare uses the already active transaction and does not call claim/start again.
- Review patch tightened `Complete task` so a previously finalized Compare cannot complete after new unresolved blockers are introduced.
- Added close-path cleanup so `Close window`, window X, Suspend, and Finalize all remove loaded Compare map groups for the transaction without mutating task lifecycle on plain close.

### File List

- `_bmad-output/implementation-artifacts/8-4e-add-compare-task-lifecycle-actions-and-reopen-active-workspace.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ICompareTaskLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
