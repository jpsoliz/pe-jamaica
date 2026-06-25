---
baseline_commit: handoff-2026-06-24
---

# Story 5.24: Add Whole-Review Parcel Context And Active-Parcel Diagnostics To Points Validation Preview

Status: review

## Story

As a cadastral examiner validating extracted points parcel by parcel,  
I want the preview panel to show the active parcel in the context of the whole review and display parcel-level diagnostics,  
so that I can understand where the current parcel sits within the transaction and whether closure or row issues still need attention.

## Acceptance Criteria

1. Given the Points Validation Tool loads review data with multiple parcel groups, when the preview panel renders, then it shows faint outlines for all parcel groups in the current review and highlights the active parcel more prominently.
2. Given the active parcel changes, when the examiner switches parcels, then the preview refreshes to highlight the newly active parcel and keep the selected point marker aligned to that parcel.
3. Given closure validation results exist for the active parcel, when the parcel is shown in the preview panel, then a compact diagnostic summary shows closure status, applied tolerance profile, and key parcel counts.
4. Given the active parcel has warnings or blockers, when the preview panel is shown, then the reviewer can see those parcel-specific issues without depending only on the row grid or footer text.
5. Given only one parcel exists in the review, when the preview renders, then it still shows the parcel cleanly without duplicate or unnecessary context chrome.
6. Given parcel geometry cannot be built from valid coordinates, when the preview panel renders, then it degrades gracefully without breaking the workspace.

## Tasks / Subtasks

- [x] Add a follow-up story artifact for preview-context enhancement. (AC: 1-6)
- [x] Extend the preview view model to build whole-review parcel outlines and an active-parcel highlight. (AC: 1-2, 5-6)
- [x] Add active-parcel diagnostic text for closure profile/status and parcel row counts. (AC: 3-4)
- [x] Update the Points Validation Tool preview XAML to render whole-review parcel context and parcel diagnostics. (AC: 1-6)
- [x] Verify the add-in still builds cleanly after the preview enhancement. (AC: 6)

## Dev Notes

### Design Direction

- Keep the preview lightweight and in-process.
- Do not turn the preview panel into a second full GIS map.
- Show all parcel groups faintly, with the active parcel visually dominant.
- Keep selected-point emphasis and parcel-level diagnostics tied to the active parcel.

### Scope Boundary

This story improves:

- parcel context visibility in the preview panel
- active parcel diagnostics in the Points Validation Tool

This story does not add:

- full embedded ArcGIS map editing
- additional map-review workflows
- authoritative parcel-fabric visualization changes

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-24 | 0.1 | Drafted follow-up story for whole-review parcel preview context and active-parcel diagnostics. | Codex |
| 2026-06-24 | 1.0 | Implemented whole-review parcel preview context, active parcel highlighting, and parcel-level diagnostics in Points Validation Tool. | Codex |

## Dev Agent Record

### Completion Notes

- Added whole-review parcel context rendering so the preview can show all parcel groups faintly while keeping the active parcel highlighted.
- Added active-parcel diagnostic messaging with closure status/profile plus parcel row counts and parcel-specific issue summary.
- Updated the preview XAML to render multiple parcel outlines and a dedicated active-parcel diagnostics block.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.csproj`

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
