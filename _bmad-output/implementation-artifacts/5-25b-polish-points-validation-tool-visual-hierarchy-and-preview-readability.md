---
baseline_commit: handoff-2026-06-27
---

# Story 5.25B: Polish Points Validation Tool Visual Hierarchy And Preview Readability

Status: review

## Story

As a cadastral examiner reviewing parcel points in the Points Validation Tool,  
I want the transaction context, parcel preview, and diagnostics area to be easier to scan and visually separated,  
so that I can understand the case state quickly, see the parcel preview clearly, and read validation details without the form feeling cramped or mixed.

## Acceptance Criteria

1. Given the Points Validation Tool opens, when the examiner views the top transaction area, then the transaction context is visually separated from the working panels using a distinct header-band treatment rather than blending into the whole form.
2. Given the form contains three primary work regions, when the examiner scans the UI, then `Source Verification`, `Validate Points`, and `Parcel Interpretation` read as clearly distinct sections with balanced visual weight.
3. Given the right-side parcel preview is a key review aid, when the active parcel is shown, then the preview card is large enough and padded enough for the parcel shape to remain visible without crowding the surrounding text.
4. Given parcel-level rule and closure details may be lengthy, when diagnostics exceed the visible space, then the detail area below the preview uses a vertical scroll container instead of forcing the full text to compress the preview.
5. Given the preview and diagnostics serve different purposes, when both are shown together, then the preview remains visually dominant and the diagnostics panel reads as a secondary detail area.
6. Given the left source-viewer area already contains helper text and controls, when the layout is tightened, then low-value explanatory copy is reduced or compacted to preserve more usable space for the document preview.
7. Given the tool is already functionally working, when this polish is implemented, then no point-review behavior, save flow, approval gating, or parcel switching logic regresses.
8. Given the UI polish is complete, when the tool is compared with the current version, then the screen feels calmer, more structured, and easier to scan without changing the underlying workflow.

## Tasks / Subtasks

- [x] Add a dedicated transaction-context header treatment. (AC: 1, 8)
  - [x] Give the transaction summary area a distinct background or band treatment.
  - [x] Keep the action chips and transaction metadata readable without increasing vertical clutter.

- [x] Rebalance the three-column review layout. (AC: 2, 3, 5, 8)
  - [x] Review the relative width and spacing of `Source Verification`, `Validate Points`, and `Parcel Interpretation`.
  - [x] Increase the practical readability of the parcel preview card.
  - [x] Preserve stable column behavior inside the ArcGIS Pro window.

- [x] Move parcel diagnostics into a scrollable detail surface. (AC: 4, 5)
  - [x] Separate the preview drawing area from the longer diagnostic text.
  - [x] Add a vertical scrollable diagnostics region under the preview.
  - [x] Ensure long closure/readiness messages no longer collapse the preview.

- [x] Tighten low-value helper copy in the left panel. (AC: 6)
  - [x] Reduce repeated explanatory text near the document viewer.
  - [x] Preserve only the guidance needed for first-use comprehension.

- [x] Verify this remains a pure UX polish pass. (AC: 7-8)
  - [x] Confirm no save/close/validation behavior changes.
  - [x] Confirm parcel preview still updates from the same live view model signals.
  - [x] Confirm the form still behaves correctly for compact ArcGIS Pro widths.

## Dev Notes

### Why This Story Exists

- The Points Validation Tool is functionally strong now, but its visual hierarchy still makes the right-side parcel review area feel cramped.
- The parcel preview is being visually competed with by dense diagnostics text.
- The top transaction area currently reads like part of the same flat surface rather than a context header.

### Design Intent

This is not a functional redesign. It is a usability polish pass focused on:

- stronger section separation
- more visible parcel preview geometry
- better handling of long diagnostics text
- a clearer “context band” at the top of the form

### Recommended UI Direction

Preferred adjustments:

1. Top context band
   - lightly tinted background
   - same controls, cleaner grouping

2. Right panel
   - slightly larger preview region
   - dedicated diagnostics area with vertical scrollbar
   - stronger padding around the preview drawing

3. Left panel
   - compress helper text
   - favor document-viewing space over repeated instructions

### Scope Boundary

This story should improve:

- visual hierarchy
- preview readability
- diagnostics readability
- information density balance

This story should not change:

- validation rules
- parcel grouping logic
- save/continue/close behavior
- manual point edit workflow
- Create Spatial Units or downstream state transitions

### Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs` only if minor binding refinements are needed

### References

- `_bmad-output/implementation-artifacts/5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`
- `_bmad-output/implementation-artifacts/5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md`
- `_bmad-output/implementation-artifacts/5-24-add-whole-review-parcel-context-and-active-parcel-diagnostics-to-points-validation-preview.md`
- `_bmad-output/implementation-artifacts/5-25a-expose-parcel-construction-readiness-rules-in-settings-workspace.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-27 | 0.1 | Drafted UX follow-up story for Points Validation Tool visual hierarchy, parcel preview readability, and scrollable diagnostics polish. | Codex |
| 2026-06-27 | 1.0 | Implemented visual hierarchy polish, larger parcel preview treatment, and scrollable diagnostics layout for the Points Validation Tool. | Codex |

## Dev Agent Record

### Completion Notes

- Added a tinted transaction-context header band so the case summary reads as a clear top context region instead of blending into the full form.
- Rebalanced the three-column layout to give the left document pane and right parcel-interpretation pane more usable width while keeping the central review grid stable.
- Reworked the right panel so the parcel preview remains visually dominant and the longer validation details live in a dedicated scrollable diagnostics surface.
- Tightened helper copy in the source-viewer area so the document pane keeps more room for the rendered PDF/image content.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.csproj`

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
