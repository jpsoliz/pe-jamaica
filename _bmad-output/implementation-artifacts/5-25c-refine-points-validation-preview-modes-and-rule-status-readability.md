---
baseline_commit: handoff-2026-06-27
---

# Story 5.25C: Refine Points Validation Preview Modes And Rule-Status Readability

Status: review

## Story

As a cadastral examiner validating parcel points in the Points Validation Tool,  
I want the parcel preview to default to the active parcel while still letting me toggle all-parcel context, and I want validation results grouped by status instead of mixed together,  
so that I can read the current parcel clearly, understand whether a rule passed or failed, and decide what still needs correction before Create Spatial Units.

## Acceptance Criteria

1. Given the Points Validation Tool opens, when the parcel preview first renders, then it shows only the active parcel by default rather than a mixed whole-review overlay.
2. Given parcel context can still be useful, when the examiner enables an `All parcels` toggle, then the preview shows the active parcel prominently and all other parcels as faint background context.
3. Given the examiner disables `All parcels`, when the preview redraws, then the preview returns to a clean active-parcel-only view without stale context geometry.
4. Given the right-side parcel section contains active parcel name, summary, issues, preview, and diagnostics, when the UI renders, then those elements use separate layout rows so no title or status text is clipped or visually overlapped by the combo box or neighboring content.
5. Given validation details currently combine passing and failing results, when parcel diagnostics are shown, then blockers, warnings, and passed checks are visually distinguished instead of appearing as one undifferentiated list.
6. Given parcel closure or readiness blockers exist, when the parcel is selected, then the blocking issues are immediately visible without requiring the examiner to parse through passed-rule text first.
7. Given passed checks are still useful for auditability, when the examiner wants more detail, then passed validations can be shown in a lower-emphasis section or collapsed/secondary presentation.
8. Given no preview-context overlay is needed for every case, when the preview is in default mode, then the parcel shape is easier to read than in the current mixed overlay rendering.
9. Given the Points Validation Tool already supports parcel switching, when the active parcel changes, then the preview mode and rule-status grouping refresh correctly for the newly selected parcel.
10. Given this story is implemented as a refinement, when complete, then no point-edit save, validation-complete gating, parcel switching logic, or preview-selection marker behavior regresses.

## Tasks / Subtasks

- [x] Add parcel preview mode control to the right panel. (AC: 1-3, 8-9)
  - [x] Add an `All parcels` toggle in the Parcel Interpretation area.
  - [x] Default the preview mode to active parcel only.
  - [x] Preserve active parcel emphasis when all-parcel context is enabled.

- [x] Refine preview rendering behavior for active-only vs all-parcels mode. (AC: 1-3, 8-9)
  - [x] Render only the active parcel in default mode.
  - [x] Render faint context geometry only when the toggle is enabled.
  - [x] Keep selected-point marker placement correct in both modes.

- [x] Fix right-panel visual hierarchy and row layout. (AC: 4, 8)
  - [x] Give parcel title, summary, issues, preview, and diagnostics distinct layout rows.
  - [x] Prevent parcel name/status text from clipping or colliding with the combo box area.
  - [x] Preserve stable layout inside a docked ArcGIS Pro width.

- [x] Reformat validation details by rule status. (AC: 5-7, 9)
  - [x] Separate blocker, warning, and pass results in the parcel diagnostics output.
  - [x] Show blocker and warning findings first and with stronger emphasis.
  - [x] Move passed checks to a lower-emphasis section or secondary block.

- [x] Verify this remains a refinement story, not a functional rewrite. (AC: 9-10)
  - [x] Confirm parcel switching still updates the preview and diagnostics.
  - [x] Confirm point editing and save behavior are unchanged.
  - [x] Confirm validation-complete gating still depends on the same underlying rule truth.

## Dev Notes

### Why This Story Exists

- The current Points Validation Tool preview can feel visually mixed because whole-review geometry appears even when the examiner only needs the active parcel.
- The parcel interpretation header area can clip or crowd text when the selected parcel name is long.
- Validation details currently mix failed and passed rule messages in one text block, which makes it harder to see what actually blocks completion.

### Design Intent

This is a refinement story focused on:

- active-parcel-first readability
- optional surrounding-parcel context
- clearer status communication for validation rules
- a cleaner right-panel hierarchy

### Recommended UI Direction

Preferred behavior:

1. Preview mode
   - default = active parcel only
   - optional toggle = `All parcels`

2. All-parcel mode
   - active parcel in strong stroke/fill
   - other parcels as faint outlines only

3. Diagnostics
   - `Blocked`
   - `Warnings`
   - `Passed checks`

### Scope Boundary

This story should improve:

- parcel preview readability
- right-panel structure
- validation diagnostics readability

This story should not change:

- validation rule logic itself
- closure thresholds
- save/close/continue behavior
- manual point editing mechanics
- downstream Create Spatial Units behavior

### Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`

### Alignment Notes

- This story builds directly on `5.24` and `5.25B`.
- The all-parcel context should remain available, but it should no longer be the default presentation.
- Rule-status grouping should reflect the same saved validation truth already produced by closure and readiness validation, not a separate interpretation layer.

## References

- `_bmad-output/implementation-artifacts/5-24-add-whole-review-parcel-context-and-active-parcel-diagnostics-to-points-validation-preview.md`
- `_bmad-output/implementation-artifacts/5-25b-polish-points-validation-tool-visual-hierarchy-and-preview-readability.md`
- `_bmad-output/implementation-artifacts/5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md`
- `_bmad-output/implementation-artifacts/5-25-externalize-parcel-construction-readiness-rules-for-gaps-shared-edges-and-boundary-completeness.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-27 | 0.1 | Drafted follow-up story for active-parcel-first preview mode, optional all-parcels context toggle, and clearer validation status readability in Points Validation Tool. | Codex |
| 2026-06-27 | 1.0 | Implemented active-parcel-first preview mode, optional all-parcels context toggle, and grouped rule-status diagnostics in Points Validation Tool. | Codex |

## Dev Agent Record

### Completion Notes

- Added a parcel preview mode toggle so the right-panel preview now defaults to the active parcel only, with optional surrounding parcel context when needed.
- Refined preview rendering so non-active parcels render only as faint background outlines while the active parcel stays visually dominant.
- Fixed the right-panel layout hierarchy so parcel title, summary, context toggle, issues, preview, and diagnostics occupy separate rows and no longer crowd each other.
- Reworked validation diagnostics into grouped `Blocked`, `Warnings`, and `Passed checks` sections so reviewers can see the actionable issues first without losing audit detail.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.csproj`

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
