---
baseline_commit: handoff-2026-06-14
---

# Story 2.15A: Route Supported Transactions Into Compute Workflow And Align Compute UI Labels

Status: ready-for-review

## Story

As a cadastral plan examination user,  
I want only supported compute-stage transactions to launch the current compute workflow,  
so that the add-in opens the correct workflow experience for Compute Survey Plan work and presents labels that match the transaction metadata shown to staff.

## Acceptance Criteria

1. Given the add-in loads local configuration, when Innola workflow-stage settings are resolved, then the allowed compute-stage names are read from configuration rather than hardcoded only in C#.
2. Given a transaction is selected in the Transaction Panel, when the user chooses Load, Start, or double-clicks the transaction, then the add-in validates both the supported transaction type and the supported compute-stage metadata before launching the current workflow.
3. Given the selected transaction is a supported transaction type but not a supported compute-stage transaction, when the user attempts to load it, then the add-in blocks launch, shows a clear user message, and keeps the user in the Transaction Panel.
4. Given the selected transaction is supported for the compute workflow, when the workflow opens, then the dock pane title and messaging identify it as `Parcel Workflow [Compute]`.
5. Given the compute workflow dock pane is displayed, when transaction metadata is available, then the header shows `Transaction Number: {number}` and `Transaction Type: {transaction stage or resolved task/type label}` rather than generic or shortened labels.
6. Given output layers are loaded into ArcGIS Pro, when `parcel_points` is added to the map, then the layer is labeled using the `point_id` field to improve spatial review readability.
7. Given the Configuration panel is opened, when the user reviews local settings, then it shows the configured compute workflow stage allowlist alongside the existing supported transaction type settings.
8. Given this story is complete, then focused tests cover compute-stage allow/block behavior, configuration fallback behavior, and the compute workflow label changes.

## Tasks / Subtasks

- [x] Add configurable compute workflow stage gating. (AC: 1-3, 7)
  - [x] Extend `WorkflowSettings.json` with a `compute_workflow_stages` list.
  - [x] Load the configured values into Innola transaction settings and safe defaults.
  - [x] Apply the allowlist during transaction load/start decisions in the Transaction Panel.

- [x] Keep unsupported stages out of the compute workflow. (AC: 2-3)
  - [x] Reuse the normalized transaction metadata already resolved from Innola rows/details where possible.
  - [x] Block unsupported stage launches before case loading or dock-pane workflow activation begins.
  - [x] Preserve user context in the Transaction Panel when the selected transaction is not valid for compute.

- [x] Align compute workflow naming and header text. (AC: 4-5)
  - [x] Rename the current workflow command, caption, and visible pane text to `Parcel Workflow [Compute]`.
  - [x] Update the dock-pane header to use `Transaction Number` and `Transaction Type`.
  - [x] Prefer the transaction stage/task label shown by Innola when available for the transaction type display.

- [x] Improve map readability for output review. (AC: 6)
  - [x] Update output map integration so `parcel_points` is labeled from `point_id`.
  - [x] Preserve existing output layer loading behavior for points, lines, and polygons.

- [x] Add focused tests. (AC: 8)
  - [x] Test compute-stage allow passes when transaction type and stage are both supported.
  - [x] Test supported type plus unsupported stage blocks before workflow launch.
  - [x] Test configuration/default handling for compute workflow stages.
  - [x] Test UI-facing configuration/state outputs reflect the compute workflow settings.

## Dev Notes

### Why This Story Exists

- The current compute experience should not launch for every transaction that happens to share a broad transaction family.
- Plan Examination and Cadastral Plan Examination transactions can carry multiple business stages over time, and only the compute-oriented stages should open this workflow.
- The UI also needed to reflect that this workflow is specifically the compute branch, not the future compare branch.

### Architectural Direction

- Keep transaction launch gating configuration-driven.
- Treat this workflow as the compute-specific lane of a broader transaction lifecycle.
- Preserve room for future workflow branching so compare-stage transactions can open a different dock pane later without overloading the compute experience.

### Scope Boundaries

- This story does not build the compare workflow.
- This story does not change the core extraction/preflight/output lifecycle beyond launch gating and display alignment.
- This story does not change enterprise sync behavior.

### Suggested Files Extended

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/AboutWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`

### References

- `_bmad-output/implementation-artifacts/2-15-gate-supported-transaction-types-before-workflow-load.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes

- Added configuration-driven compute workflow stage gating on top of the supported transaction type gate.
- Renamed the current workflow surface to `Parcel Workflow [Compute]` and aligned the dock-pane header labels with transaction metadata.
- Updated output map integration so `parcel_points` layers are labeled using `point_id`.
- Preserved the current compute path while preparing the architecture for a future compare-stage workflow branch.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/AboutWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Change Log

- 2026-06-14: Captured compute-stage transaction routing, compute workflow renaming, header label alignment, and point labeling as Story 2.15A.
