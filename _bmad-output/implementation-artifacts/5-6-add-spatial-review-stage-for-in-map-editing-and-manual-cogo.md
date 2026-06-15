---
baseline_commit: handoff-2026-06-14
---

# Story 5.6: Add Spatial Review Stage For In-Map Editing And Manual COGO

Status: ready-for-review

## Story

As a cadastral plan examiner,  
I want a dedicated Spatial Review stage after output generation,  
so that I can inspect generated parcel geometry in the ArcGIS Pro map, use native editing/snapping/COGO tools when needed, and only approve the transaction after spatial review is complete.

## Acceptance Criteria

1. Given output generation succeeds and transaction-local geometry artifacts exist, when workflow gating is recalculated, then the next active stage becomes `Spatial Review` rather than moving directly from Outputs to final completion.
2. Given the Spatial Review stage is active, when the dock pane is shown, then it clearly instructs the user to review and edit parcel geometry in the ArcGIS Pro map rather than trying to duplicate heavy geometry editing inside the dock pane.
3. Given the case includes extracted geometry that is insufficient or partially wrong, when the user enters Spatial Review, then the workflow supports map-based manual correction using standard ArcGIS Pro editing, snapping, and COGO-capable tools outside the dock pane.
4. Given the generated outputs can be loaded into the map, when Spatial Review begins, then the add-in loads or reuses the relevant output layers in the active map and gives clear status if no active map is available.
5. Given the user completes map-based spatial review, when they confirm review completion in the dock pane, then the workflow records a spatial-review approval marker and enables `Ready to Complete`.
6. Given the user suspends and later reopens the case, when workflow state is restored, then the Spatial Review stage reopens correctly and preserves whether spatial review is still pending or already approved.
7. Given the user edits geometry in-map after an earlier spatial review approval, when the case is resumed or outputs are refreshed, then the workflow has a deterministic rule for invalidating or preserving spatial review approval based on the implemented artifact/version strategy.
8. Given this story is complete, then focused tests cover stage progression from Outputs to Spatial Review, readiness messaging, suspend/reopen behavior, and approval gating into `Ready to Complete`.

## Tasks / Subtasks

- [x] Extend the workflow state machine for Spatial Review. (AC: 1, 5-8)
  - [x] Add explicit workflow states for spatial review pending/approved as needed.
  - [x] Update workspace progression, badges, and stage focus logic.
  - [x] Ensure `Ready to Complete` depends on Spatial Review completion, not just output generation.

- [x] Add Spatial Review stage UI and guidance. (AC: 2-4)
  - [x] Add a new dock-pane stage section between Outputs and Ready to Complete.
  - [x] Keep the UI guidance focused on map review, not duplicate geometry-edit controls.
  - [x] Surface a clear status message when output layers cannot be loaded automatically because no active map is available.

- [x] Wire ArcGIS Pro map handoff behavior. (AC: 3-4)
  - [x] Reuse or extend current output map integration so the relevant parcel layers are available when Spatial Review starts.
  - [x] Support manual re-load/focus actions where automatic map load does not occur.
  - [x] Preserve the map as the primary spatial editing surface.

- [x] Add workflow approval for Spatial Review completion. (AC: 5-7)
  - [x] Add an explicit dock-pane action to mark spatial review complete.
  - [x] Persist the result as a case artifact or manifest-backed state marker.
  - [x] Define invalidation behavior if geometry-affecting artifacts change later.

- [x] Preserve suspend/resume behavior. (AC: 6-8)
  - [x] Ensure suspended cases reopen into Spatial Review when that is the last incomplete stage.
  - [x] Preserve already-approved spatial review state where still valid.

- [x] Add focused tests. (AC: 8)
  - [x] Test Outputs -> Spatial Review progression.
  - [x] Test Ready to Complete stays blocked until Spatial Review is approved.
  - [x] Test reopen/resume state for pending and approved spatial review.
  - [x] Test no-active-map messaging remains non-blocking but clear.

## Dev Notes

### Why This Story Exists

- The current workflow moves too quickly from output generation to final completion.
- In real cadastral plan examination, users often need to inspect geometry spatially and sometimes correct it manually before approving the transaction.
- ArcGIS Pro already provides the right editing surface; the dock pane should guide and gate, not replace ArcGIS editing tools.

### Architectural Direction

- Keep the transaction `.gdb` as the working spatial review contract.
- Let ArcGIS Pro own geometry editing, snapping, and COGO-capable tools.
- Let the dock pane own workflow state, readiness, guidance, and approval gating.

### Scope Boundaries

- Do not build a full custom map editor in the dock pane.
- Do not require Parcel Fabric in this story.
- Do not implement enterprise sync here; this story only prepares a reviewed local geometry result for final completion.

### Suggested Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/*`

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `docs/project/PROCESSING_ALIGNMENT.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes

- Output generation now transitions into `Spatial Review` rather than final completion.
- Spatial review approval persists as `working/spatial_review_approval.json` and is invalidated deterministically when output artifacts change.
- Reopen logic restores pending/approved spatial review state and gracefully downgrades stale approvals back to pending review.
- Dock-pane workflow now exposes a dedicated Spatial Review stage with map-loading guidance and a `Mark reviewed` action before `Ready to Complete`.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Change Log

- 2026-06-14: Implemented Story 5.6 workflow state, spatial review approval persistence, dock-pane stage UI, reopen behavior, and focused tests.
