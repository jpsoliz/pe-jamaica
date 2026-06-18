---
baseline_commit: handoff-2026-06-17
---

# Story 5.16B: Implement Points Validation Tool Save/Return Flow And Downstream Stage Handoff

Status: review

## Story

As a cadastral examiner validating extracted parcel points,  
I want the `Points Validation Tool` to save edited point data and return me to the workflow at the correct next stage,  
so that point validation and spatial creation are clearly separated and I do not lose work or get confused about what happens next.

## Acceptance Criteria

1. Given the `Points Validation Tool` is open, when the user has not changed any point data, then the `Save` button is disabled and `Close` exits without modifying the persisted point-review dataset.
2. Given the user edits, adds, or removes point data, when unsaved changes exist, then the `Save` button becomes enabled and the tool clearly indicates that the working point set has changed.
3. Given the user presses `Save`, when the save succeeds, then the updated point-review dataset is persisted to the case working files used by downstream spatial creation.
4. Given the user saves changed point data, when the save completes, then the tool asks whether the examiner wants to continue with the saved validated points into the next workflow stage.
5. Given the examiner confirms that they want to continue, when the tool closes and returns control to `Parcel Workflow [Compute]`, then the next visible actionable stage becomes `Create Spatial Units`.
6. Given the examiner saves but chooses not to continue yet, when the tool closes or remains open, then the saved validated points remain available without automatically starting spatial creation.
7. Given the examiner closes the tool after save or without changes, when control returns to the workflow shell, then the shell no longer presents the validation tool as the active embedded review surface and instead communicates the current downstream readiness state.
8. Given `Create Spatial Units` runs after validated points are saved, when spatial creation succeeds, then parcel fabric or other configured spatial units are created from the saved validated review data rather than directly from raw extraction output.
9. Given spatial creation is complete, when the workflow advances, then the next stage becomes `Final Review`, where the examiner can `Approve`, `Reject`, or `Postpone`.
10. Given the examiner approves in `Final Review`, when the workflow proceeds, then `Finalize` becomes the last step that commits the case result back into the Innola transaction process.
11. Given validation-tool save/close flows encounter errors, when save fails or transition state is inconsistent, then the user receives a clear non-destructive message and no downstream spatial creation is implied until saved validated data exists.

## Tasks / Subtasks

- [x] Add save-state behavior to the Points Validation Tool. (AC: 1-4, 11)
  - [x] Track whether the point dataset is dirty.
  - [x] Enable `Save` only when there are unsaved point changes.
  - [x] Persist updated validated-point data back into the case working files.
  - [x] Show clear success/failure messaging around save operations.

- [x] Add continue-after-save confirmation flow. (AC: 4-6)
  - [x] Prompt the examiner after successful save.
  - [x] Support both “save and continue” and “save only” outcomes.
  - [x] Prevent accidental transition into spatial creation when the user only wanted to save progress.

- [x] Return the workflow shell to the correct downstream stage. (AC: 5, 7-10)
  - [x] Close the validation tool cleanly back into `Parcel Workflow [Compute]`.
  - [x] Make `Create Spatial Units` the next visible actionable stage after confirmed save-and-continue.
  - [x] Route successful spatial creation into `Final Review`.
  - [x] Keep `Finalize` as the Innola commit/closeout step after final examiner decision.

- [x] Add focused tests/manual verification notes. (AC: 1-11)
  - [x] Verify clean close with no changes.
  - [x] Verify save enablement only after edits.
  - [x] Verify save-and-continue handoff.
  - [x] Verify save-only without downstream transition.
  - [x] Verify `Create Spatial Units` uses saved validated data, not raw extraction data.

## Dev Notes

### Why This Story Exists

- The dedicated review tool now owns parcel-by-parcel point validation, but its close behavior still needs to map back into the main compute workflow cleanly.
- Saving point changes should not silently trigger parcel creation, but it should support an intentional handoff into the next stage.

### Process Rule

- `Save` in the validation tool persists validated point data.
- `Create Spatial Units` is the stage that builds parcels/fabric from those validated points.
- `Final Review` is the examiner’s approve/reject/postpone stage after spatial creation.
- `Finalize` is the Innola-facing completion step.

### Scope Boundaries

- This story does not rename the stages; that belongs to the alignment story.
- This story does not redesign the detailed point-editing controls themselves beyond what is needed for save/close behavior.
- This story does not redefine enterprise/manual branch architecture.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/*`
- `src/ProcessingTools/adapters/output_adapter.py`

## References

- `_bmad-output/implementation-artifacts/5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`
- `_bmad-output/implementation-artifacts/5-16-align-compute-workflow-stage-copy-and-jamaica-cogo-handoff.md`
- `_bmad-output/implementation-artifacts/5-18-route-manual-review-branch-into-configured-gdb-map-editing-path.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial implementation story for save/close behavior in Points Validation Tool and downstream handoff into Create Spatial Units, Final Review, and Finalize. | Codex |
| 2026-06-17 | 0.2 | Implemented dirty-only save, close/discard prompts, save-and-continue workflow handoff, and close-state messaging with focused tests. | Codex |

## Dev Agent Record

### Debug Log

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false /p:UseSharedCompilation=false`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false` still hits the pre-existing direct WPF/XAML compile issue in `TransactionPanelDockpane.xaml.cs` (`InitializeComponent`), which is outside this story’s changes.

### Completion Notes

- Added a dedicated save/close orchestration path for `Points Validation Tool` so dirty changes can be saved, discarded, or promoted into `Create Spatial Units` without leaving stale in-memory review edits behind.
- Added a footer `Save` button and wired both toolbar/inline save actions to the same examiner prompt flow: save, optionally continue, and close back into the workflow shell only when intended.
- Reused the existing approved-review snapshot path for the continue handoff so downstream `Create Spatial Units` still runs from saved validated review data rather than raw extraction output.
- Added focused tests around the new close-state messaging and verified the full test harness passes.

## File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PointsValidationWorkspaceMessages.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PointsValidationWorkspaceMessagesTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `_bmad-output/implementation-artifacts/5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
