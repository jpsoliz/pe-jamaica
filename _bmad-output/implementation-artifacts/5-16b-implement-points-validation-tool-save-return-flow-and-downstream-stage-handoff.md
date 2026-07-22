---
baseline_commit: handoff-2026-06-17
---

# Story 5.16B: Implement Points And Lines Validation Tool Save/Return Flow And Downstream Stage Handoff

Status: review

## Story

As a cadastral examiner validating extracted parcel points and parcel lines,  
I want the `Points and Lines Validation Tool` to save edited point/line review data and return me to the workflow at the correct next stage,  
so that point/line validation and spatial creation are clearly separated and I do not lose work or get confused about what happens next.

## Acceptance Criteria

1. Given the `Points and Lines Validation Tool` is open, when the user has not changed any point or line data, then the `Save` button is disabled and `Close` exits without modifying the persisted review dataset.
2. Given the user edits, adds, or removes point or line data, when unsaved changes exist, then the `Save` button becomes enabled and the tool clearly indicates that the working review set has changed.
3. Given the user presses `Save`, when the save succeeds, then the updated point/line review dataset is persisted to the case working files used by downstream spatial creation.
4. Given the user saves changed point or line data, when the save completes, then the tool asks whether the examiner wants to continue with the saved validated points and lines into the next workflow stage.
5. Given the examiner confirms that they want to continue, when the tool closes and returns control to `Parcel Workflow [Compute]`, then the next visible actionable stage becomes `Create Spatial Units`.
6. Given the examiner saves but chooses not to continue yet, when the tool closes or remains open, then the saved validated points and lines remain available without automatically starting spatial creation.
7. Given the examiner closes the tool after save or without changes, when control returns to the workflow shell, then the shell no longer presents the validation tool as the active embedded review surface and instead communicates the current downstream readiness state.
8. Given `Create Spatial Units` runs after validated points and lines are saved, when spatial creation succeeds, then parcel fabric or other configured spatial units are created from the saved validated review data rather than directly from raw extraction output.
9. Given spatial creation is complete, when the workflow advances, then the next stage becomes `Final Review`, where the examiner can `Approve`, `Reject`, or `Postpone`.
10. Given the examiner approves in `Final Review`, when the workflow proceeds, then `Finalize` becomes the last step that commits the case result back into the Innola transaction process.
11. Given validation-tool save/close flows encounter errors, when save fails or transition state is inconsistent, then the user receives a clear non-destructive message and no downstream spatial creation is implied until saved validated data exists.

## Tasks / Subtasks

- [x] Add save-state behavior to the Points Validation Tool. (AC: 1-4, 11)
  - [x] Track whether the point dataset is dirty.
  - [x] Enable `Save` only when there are unsaved point changes.
  - [x] Persist updated validated-point data back into the case working files.
  - [x] Show clear success/failure messaging around save operations.
  - [ ] Product alignment patch: extend dirty-state/save language and persistence expectations to parcel lines and proposed polygon/construction data where the review contract carries them.

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
  - [ ] Add or update tests so edited line data participates in dirty-state detection, save persistence, and `Create Spatial Units` handoff.

## Dev Notes

### Why This Story Exists

- The dedicated review tool now owns parcel-by-parcel point and line validation, but its close behavior still needs to map back into the main compute workflow cleanly.
- Saving point/line changes should not silently trigger parcel creation, but it should support an intentional handoff into the next stage.

### Product Alignment Update - 2026-07-03

The latest compute workflow notes in `docs/project/compute-steps.docx` rename/expand this stage from point-only validation to point-and-line validation. The review artifact should be treated as carrying:

- extracted/reviewed parcel points
- extracted/reviewed parcel lines
- proposed polygon or parcel construction data where available
- parcel point validation status
- parcel line validation status

Existing `extraction_review_data.json` compatibility must be preserved. If current contracts only have point rows, the implementation should extend them backward-compatibly rather than replacing the artifact or breaking older cases.

### Process Rule

- `Save` in the validation tool persists validated point and line data.
- `Create Spatial Units` is the stage that builds parcels/fabric from those validated points and lines.
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
| 2026-07-03 | 0.3 | Patched story scope to expand validation/save/handoff from points-only to points-and-lines review data. | Mary / Codex |
| 2026-07-21 | 0.4 | Hardened Points Validation Tool footer actions and close/save messaging so disabled actions are hidden, Close remains pressable, and save availability is explained when closing with unsaved changes. | Codex |

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
- Product alignment patch added the future requirement that line edits and line validation status participate in dirty-state save and downstream handoff; implementation may need a follow-up dev patch if current code only tracks point rows.
- Follow-up hardening made footer actions reflect pressable state: `Validation Complete` is only shown when validation can complete, `Save` is only shown when dirty review changes can be saved, and `Close` remains visible as the escape path.
- Added close-prompt messaging that tells the examiner whether `Save` is available before closing with unsaved point changes; if save is unavailable or fails, the tool now explains why it stays open instead of appearing unresponsive.
- Explicitly notified `HasUnsavedReviewChanges` and `CanSaveReviewChangesFromWorkspace` from the parent dockpane so adding a point refreshes the validation window's Save/Close state immediately.
- Regression coverage now includes footer action visibility, Close pressability, save-state notification, and close/save failure messaging.

## File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PointsValidationWorkspaceMessages.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PointsValidationWorkspaceMessagesTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `_bmad-output/implementation-artifacts/5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
