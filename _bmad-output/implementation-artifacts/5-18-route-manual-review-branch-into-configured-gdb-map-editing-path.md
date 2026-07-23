---
baseline_commit: handoff-2026-06-17
---

# Story 5.18: Route Manual Review Branch Into Configured GDB And Map Editing Path

Status: review

> 2026-07-22 product update: this story documents the earlier spatial-editing manual branch. The current Manual Mode enhancement supersedes that immediate routing behavior for point review: Manual Mode remains in Points Validation Tool, allows point add/remove/edit from partial or blank extraction review data, and reaches map/GDB output only after the review is saved and approved.

## Story

As a cadastral examiner who decides automated point review is not good enough,  
I want the manual review branch to open the configured spatial editing path and persist my reviewed work into the case outputs,  
so that I can continue the case with regular ArcGIS Pro editing tools and still produce the correct transaction output package.

## Acceptance Criteria

1. Given the examiner chooses the manual review branch from Point Review, when the branch is confirmed, then the add-in routes into the configured spatial mode rather than leaving the user in an ambiguous in-between state.
2. Given the system already supports configurable output/review modes, when the manual branch launches, then it uses the configured setting for:
   - normal local `.gdb`
   - local `.gdb` with parcel fabric
   - enterprise-backed working layer mode
3. Given the manual branch opens, when the user enters the editing path, then the transaction PDFs remain the authoritative source reference for the case and stay available to open/reveal alongside editing.
4. Given the configured mode is normal local `.gdb`, when manual branch setup runs, then the working point/line/polygon layers are created or prepared in the case output/work area and loaded into ArcGIS Pro for editing.
5. Given the configured mode is parcel fabric, when manual branch setup runs, then the add-in prepares the parcel fabric-oriented workspace, loads the relevant parcel layers, and frames/zooms the case context for editing.
6. Given the configured mode is enterprise-backed working layer review, when manual branch setup runs, then the add-in prepares the working-layer editing path without implying final authoritative sync has already happened.
7. Given the examiner edits manually in the selected map/GDB path, when progress is saved, then the add-in records that the manual branch owns the review result rather than the extracted-review approval path.
8. Given manual editing is completed successfully, when the workflow returns to the main shell, then downstream `Create Spatial Outputs`, `Map Review`, and `Finalize` states reflect the manual branch as the source of reviewed geometry.
9. Given the manual branch is active, when the user has not completed or cancelled the current edit operation, then the workflow prevents conflicting state changes that would corrupt parcel context or active parcel ownership.
10. Given this story is complete, then a manual-review case can move from decision gate -> configured spatial editing path -> saved case outputs without pretending extracted review was approved.

## Tasks / Subtasks

- [x] Wire manual branch launch into configured spatial mode. (AC: 1-3)
  - [x] Read the active review/output mode from settings.
  - [x] Route manual review into the matching local, parcel fabric, or enterprise working path.
  - [x] Preserve transaction PDF source context as reference material.

- [x] Prepare editing surfaces for each supported mode. (AC: 4-6)
  - [x] Define normal local `.gdb` manual-edit initialization.
  - [x] Define parcel-fabric manual-edit initialization.
  - [x] Define enterprise working-layer initialization without final sync semantics.

- [x] Persist manual-branch ownership and return state. (AC: 7-10)
  - [x] Record manual-branch state and reviewed-output ownership in workflow/audit artifacts.
  - [x] Return to the shell with downstream steps aligned to the manual path.
  - [x] Keep extracted-review approval semantics separate from manual edit completion.

- [x] Add focused verification coverage. (AC: 10)
  - [x] Cover manual launch in each configured mode where feasible.
  - [x] Cover save/resume behavior for manual branch cases.
  - [x] Cover return-to-shell state after manual editing path completion.

## Dev Notes

### Why This Story Exists

- The manual fallback branch now exists conceptually, but it still needs a concrete destination.
- The destination should not be hardcoded; it should respect the solution’s spatial configuration choices already present in settings.

### Architectural Direction

- Use the existing configuration for review/output mode rather than inventing a fourth path.
- Treat manual editing as a legitimate reviewed-data branch, not as a broken-extraction exception.
- Keep the source-of-truth distinction explicit:
  - extracted-review approval path
  - manual spatial-edit path

### Scope Boundaries

- This story does not define the extraction decision gate itself.
- This story does not redesign the Jamaica COGO Tool review surface.
- This story does not implement final enterprise authoritative sync beyond the currently configured working-layer review behavior.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/*`

## References

- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-7-evaluate-parcel-fabric-review-workspace-pilot.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/5-17-add-manual-cogo-fallback-branch-from-point-review.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false`
- `dotnet run --no-build --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Manual COGO Review now prepares the configured spatial workspace immediately instead of leaving the case in a holding state.
- The output adapter now accepts a manual review ownership route and can prepare empty or partial edit workspaces without requiring `approved_review.json`.
- Manual branch cases persist `review_result_owner = manual_spatial_review` in `output_summary.json`, which keeps downstream map review semantics distinct from extracted-review approval.
- Enterprise-backed review mode stays non-authoritative on the manual branch; the local/map editing path is prepared without implying final shared sync.
- Superseding note: current Manual Mode behavior does not prepare spatial output immediately. It keeps review editable in Points Validation Tool and requires save/approval before Create Spatial Units.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/ReviewResultOwnership.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for routing the manual review branch into the configured local GDB, parcel fabric, or enterprise working-layer editing path. | Codex |
| 2026-06-17 | 1.0 | Implemented manual branch workspace preparation, manual ownership persistence, and focused workflow/session coverage. | Codex |
| 2026-07-22 | 1.1 | Added superseding Manual Mode note: manual point review now stays in Points Validation Tool until saved and approved. | Codex |
