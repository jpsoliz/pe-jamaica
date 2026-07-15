---
baseline_commit: handoff-2026-07-14
---

# Story 8.1: Add Compare Stage Gating And Transaction Launch

Status: review

## Story

As a cadastral examiner,  
I want transactions in the Innola transaction list to open the correct workflow stage based on their current task,  
so that Compute tasks launch Compute tools, Compare tasks launch the Compare workspace, and users cannot accidentally work in the wrong stage.

## Business Context

The current transaction shell is Compute-centric. `TransactionPanelState` gates supported rows through `ValidateComputeWorkflowStage(...)`, opens the Parcel Workflow dockpane, and messages unsupported rows as `Parcel Workflow [Compute]`. The production workflow now has three stages:

1. Compute: spatial data extraction, validation, Spatial Unit creation, and working-review publish.
2. Compare: ownership, boundary-rights, legal cadaster, and fiscal neighbor evidence reconciliation.
3. Commit: promotion to the final authoritative layer after Compare approval.

This story creates the stage-routing foundation. It should not implement the full Compare workspace yet; it should make the shell aware that Compare is a valid stage and route Compare transactions to a Compare launch seam.

## Acceptance Criteria

1. Given `WorkflowSettings.json` includes configured Compute and Compare stage names, when the transaction list loads, then the add-in can distinguish Compute-eligible tasks from Compare-eligible tasks using the selected transaction row task name.
2. Given a selected transaction is in a configured Compute stage, when the user loads or starts it, then current Compute behavior is preserved.
3. Given a selected transaction is in a configured Compare stage, when the user loads or starts it, then the transaction is allowed through stage validation and routes to a Compare workspace launch seam.
4. Given a selected transaction is in neither Compute nor Compare, when the user attempts to load/start it, then the UI blocks the action with a clear message naming the current task and supported tasks.
5. Given transaction type validation runs, when PE and PXA transactions are selected, then existing supported transaction type behavior remains intact.
6. Given the user starts a Compare transaction, when Innola ownership/start succeeds, then the transaction is marked active for the logged-in user before the Compare workspace opens.
7. Given ownership/start fails, when Compare launch is attempted, then the previous transaction/session state is preserved and no Compare workspace opens.
8. Given the transaction panel is locked by an active transaction, when filters/search/sort/refresh are used, then existing active-transaction guard behavior remains unchanged.
9. Given automated tests run, then Compute routing, Compare routing, unsupported-stage blocking, and ownership-failure preservation are covered.

## Tasks / Subtasks

- [x] Add stage configuration support. (AC: 1, 4)
  - [x] Add `CompareWorkflowStages` to `InnolaTransactionSettings`.
  - [x] Add default Compare task names only if product has a safe default; otherwise require config and report a warning.
  - [x] Add `compare_workflow_stages` to `WorkflowSettings.json`.
  - [x] Expose Compare stages through `ShellState`.

- [x] Refactor stage validation. (AC: 1-4)
  - [x] Replace `ValidateComputeWorkflowStage(...)` with a stage-aware validator that can return `Compute`, `Compare`, or unsupported.
  - [x] Preserve existing Compute error messages where the selected task is not a Compute task and no Compare route applies.
  - [x] Add Compare-specific message copy such as `Parcel Workflow [Compare]`.

- [x] Add Compare launch seam. (AC: 3, 6-7)
  - [x] Add an interface or delegate for `OpenCompareWorkspace(...)`.
  - [x] In this story, the launch seam may open a placeholder or record a clear `Compare workspace not implemented yet` status if later stories have not landed.
  - [x] Ensure Compare launch happens only after transaction load and lifecycle start/claim succeeds.

- [x] Preserve Compute launch behavior. (AC: 2, 5, 8)
  - [x] Keep `OpenParcelWorkflowDockpane(...)` behavior for Compute.
  - [x] Do not regress `CanLoadSelectedTransaction`, `CanStartTransaction`, `CanStopTask`, `CanCompleteTask`, and active transaction switching.

- [x] Add tests. (AC: 1-9)
  - [x] Add settings parser tests for `compare_workflow_stages`.
  - [x] Add transaction panel tests for Compute-stage route.
  - [x] Add transaction panel tests for Compare-stage route.
  - [x] Add unsupported task tests.
  - [x] Add ownership/start failure test confirming Compare does not open.

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`

Current hotspots:

- `TransactionPanelState` currently stores `computeWorkflowStages` and validates through `ValidateComputeWorkflowStage(...)`.
- `StartSelectedTransactionAsync(...)` currently loads, claims/starts, then opens the Compute dockpane.
- `InnolaTransactionSettings` currently has `ComputeWorkflowStages` only.

Recommended shape:

```csharp
public enum ParcelWorkflowStageRoute
{
    Unsupported,
    Compute,
    Compare
}
```

Keep the route calculation independent from UI launch so later stories can add a real Compare workspace without rewriting stage validation again.

## UX References

- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/compare-workspace-evidence-reconciliation.html`

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Open Questions

- Confirm the exact Innola task names for Compare and Commit before final production config.

## Dev Agent Record

### Implementation Plan

- Add Compare workflow-stage parsing beside existing Compute stage parsing, with missing Compare config warning instead of a guessed safe default.
- Route transaction panel load/start through a stage-aware resolver returning `Compute`, `Compare`, or `Unsupported`.
- Preserve the existing Compute dockpane launch path and add an injectable Compare workspace launcher seam for future Story 8.3 implementation.
- Cover settings parsing, Compare load/start routing, unsupported Compute-only task blocking, and ownership failure/no-launch behavior in the test harness.

### Debug Log

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- First full test run exposed a test setup issue for ownership failure because `task-owned-by-other` prevented transaction detail load before claim.
- Second full test run showed failed claim preserves `Loaded` lifecycle state, matching existing coordinator behavior and story requirement to preserve prior state.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed: 359 tests.

### Completion Notes

- Implemented `compare_workflow_stages` configuration and ShellState exposure.
- Added `ParcelWorkflowStageRoute` and replaced Compute-only validation with stage-aware routing.
- Added Compare launch seam via injectable launcher delegate; default behavior records a clear placeholder status until the real workspace lands.
- Preserved Compute launch behavior for configured Compute tasks.
- Added settings and transaction panel tests for Compare route and failure handling.

### File List

- `_bmad-output/implementation-artifacts/8-1-add-compare-stage-gating-and-transaction-launch.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-14 | 1.0 | Added Compare stage configuration, transaction panel route resolution, Compare launch seam, and regression coverage. | Amelia / Codex |
