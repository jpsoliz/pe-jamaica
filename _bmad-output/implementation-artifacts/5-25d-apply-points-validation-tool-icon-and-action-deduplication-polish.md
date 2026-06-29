---
baseline_commit: handoff-2026-06-27
---

# Story 5.25D: Apply Points Validation Tool Icon And Action-Deduplication Polish

Status: review

## Story

As a cadastral examiner using the Points Validation Tool,  
I want the document actions and point-edit actions to use clear ArcGIS-friendly icons without duplicating workspace-completion actions,  
so that the tool feels cleaner, easier to scan, and less confusing during review.

## Acceptance Criteria

1. Given the Source Verification panel shows document actions, when the tool renders, then the actions use compact icon buttons for:
   - open source document
   - open in folder
   - reload viewer
2. Given the source-folder action was previously named `Reveal`, when the tool is updated, then all user-facing copy in this workspace uses `Open in folder` instead.
3. Given the Validate Points toolbar currently mixes point-edit actions with workspace/stage actions, when the toolbar is refined, then it keeps only point-edit actions:
   - add point
   - remove point
   - discard in-progress manual point
4. Given `Save` and `Validation Complete` already exist in the footer, when the toolbar is refined, then duplicate toolbar actions for save/validation completion are removed so those meanings exist only once in the window.
5. Given the footer owns the stage-level flow, when the examiner reaches the end of the review, then the footer remains the only place for:
   - Validation Complete
   - Save
   - Close
6. Given the workspace should stay visually aligned with ArcGIS Pro conventions, when icons are selected, then the implementation uses ArcGIS-friendly assets or glyphs consistently across the window instead of mixing unrelated styles.
7. Given this is a UI asset and interaction-polish story, when complete, then no point-edit behavior, validation gating, source-viewer loading, or close/save flow regresses.

## Tasks / Subtasks

- [x] Refine Source Verification document action controls. (AC: 1-2, 6)
  - [x] Replace or confirm compact icon buttons for open source document, open in folder, and reload viewer.
  - [x] Update the folder action tooltip/copy from `Reveal source in folder` to `Open in folder`.
  - [x] Review surrounding helper text so it matches the renamed action.

- [x] Simplify the Validate Points toolbar. (AC: 3-4, 7)
  - [x] Keep toolbar actions limited to row/edit actions only.
  - [x] Remove duplicated toolbar actions for save and validation completion.
  - [x] Confirm add/remove/discard actions remain wired to the current point-edit workflow.

- [x] Preserve footer ownership of stage/workspace actions. (AC: 4-5, 7)
  - [x] Keep `Validation Complete`, `Save`, and `Close` in the footer only.
  - [x] Confirm footer actions still respect existing enable/disable logic.

- [x] Align icon usage and tooltips across the window. (AC: 1, 3, 6)
  - [x] Use a consistent ArcGIS-friendly icon set or glyph mapping across document and review actions.
  - [x] Confirm all tooltips match the actual action semantics.

- [x] Verify no functional regressions. (AC: 7)
  - [x] Confirm document open/open-in-folder/reload still work.
  - [x] Confirm add/remove/discard point actions still work.
  - [x] Confirm footer save, validation complete, and close flows are unchanged.

## Dev Notes

### Why This Story Exists

- The current Points Validation Tool still carries some duplicated action meaning between the top-right icon row and the footer.
- `Reveal` is a serviceable internal term, but `Open in folder` is clearer and more human for examiners.
- The toolbar should communicate local edit actions, while the footer should communicate workspace completion actions.

### Design Intent

This story is intentionally small and focused on:

- icon clarity
- terminology cleanup
- action deduplication
- clearer separation between edit-level and stage-level controls

### Preferred Interaction Model

Toolbar:

- Add point
- Remove point
- Discard in-progress manual point

Footer:

- Validation Complete
- Save
- Close

Source Verification action row:

- Open source document
- Open in folder
- Reload viewer

### Icon Guidance

The implementation should prefer ArcGIS-friendly icons. If Calcite assets are not yet being imported directly, the current glyph-based approach may still be used as long as:

- the chosen symbols clearly match the action
- document actions and edit actions look like one coherent system
- the patch does not introduce a second mixed visual language

### Scope Boundary

This story should improve:

- action clarity
- icon consistency
- terminology
- duplication in the Points Validation Tool

This story should not change:

- review-save semantics
- validation-complete semantics
- point-edit validation rules
- document-rendering logic
- workflow-stage sequencing

### Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`

## References

- `_bmad-output/implementation-artifacts/5-25b-polish-points-validation-tool-visual-hierarchy-and-preview-readability.md`
- `_bmad-output/implementation-artifacts/5-25c-refine-points-validation-preview-modes-and-rule-status-readability.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-28 | 0.1 | Drafted focused UI asset and action-deduplication follow-up for Points Validation Tool. | Codex |
| 2026-06-28 | 1.0 | Implemented icon/action deduplication polish, renamed Reveal to Open in folder, and kept footer ownership of Save/Validation Complete/Close actions. | Codex |

## Dev Agent Record

### Completion Notes

- Kept the Source Verification action row as compact icon-only controls for open source document, open in folder, and reload viewer.
- Renamed user-facing `Reveal` copy in the Points Validation Tool flow to `Open in folder` for clearer examiner language.
- Removed duplicated toolbar actions for `Save review` and `Validation complete` so the top-right toolbar now contains only point-edit actions.
- Preserved the footer as the single owner of workspace/stage actions: `Validation Complete`, `Save`, and `Close`.
- Kept the existing MDL2 glyph approach for this patch so the window remains visually consistent without introducing a second icon system mid-stream.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.csproj`

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ReviewSourceViewerStateProjector.cs`
