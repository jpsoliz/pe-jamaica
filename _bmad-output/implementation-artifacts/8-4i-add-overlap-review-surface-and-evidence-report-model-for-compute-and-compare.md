---
baseline_commit: handoff-2026-08-17
---

# Story 8.4I: Add Overlap Review Surface And Evidence Report Model For Compute And Compare

Status: review

## Story

As a cadastral examiner reviewing overlap findings,  
I want a dedicated overlap review surface and report model that present overlap images and tabular evidence together,  
so that I can understand each overlap clearly and preserve the review outcome as a usable artifact.

## Scope

This story assumes the overlap engine from Story 8.4H already exists. It adds the examiner-facing review surface and report artifact structure. It may display owner enrichment fields if present, but it does not implement Innola enrichment itself.

## Acceptance Criteria

1. Add a dedicated overlap review surface for Compute and Compare results, separate from the default ArcGIS Pro popup.
2. The surface renders per-layer and per-feature overlap evidence from the saved overlap review artifact.
3. For each overlap row, the surface shows at minimum:
   - overlap source/layer
   - overlap id
   - review parcel id
   - overlap area
   - overlap percentage
   - identifier fields captured from the overlap feature
   - enrichment status placeholder/result field
4. If overlap exists, the review surface can display or reference one or more map snapshots tied to the overlap rows.
5. Each image and table row must be linked by a stable overlap id or group id.
6. If no overlap exists, the review surface shows a clear `No overlaps found across configured layers` result and does not require an image.
7. The report model supports both Compute and Compare without duplicating structure.
8. The review surface distinguishes clearly between:
   - spatial overlap result
   - identifier capture
   - owner enrichment status
9. The user can rerun overlap review and refresh the review surface from the updated artifact.
10. Automated tests cover rendering states for overlap found, no overlap, multi-row overlap evidence, and image-reference binding.

## UX Notes

- Keep the ArcGIS Pro popup lightweight.
- Use a dedicated review pane/window/dockpane for evidence.
- The user should be able to understand quickly:
  - which layer caused the overlap
  - how big it is
  - whether enrichment has been completed

## Files Likely To Change

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/*`
- new overlap review UI files
- tests under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`

## Dev Agent Record

- 2026-08-17: Implemented the dedicated overlap review window/viewmodel and wired refresh/open commands into both Compute and Compare review flows.
- 2026-08-17: Extended the persisted review document model with stable overlap ids/group ids and optional snapshot references so table rows and evidence can stay linked.
- 2026-08-17: Added XAML-level coverage for the new overlap review surface and Compute/Compare launch points.

## File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/SpatialOverlapReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/ArcGisSpatialOverlapReviewService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/SpatialOverlapReviewViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/SpatialOverlapReviewWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/SpatialOverlapReviewWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SpatialOverlapReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SpatialOverlapReviewWindowXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-17 | 0.1 | Split overlap review/report surface work out of Story 8.4G as the second implementation step. | Mary / Sally / Amelia / Codex |
| 2026-08-17 | 0.2 | Added dedicated overlap review surface, stable overlap ids/snapshot refs, Compute/Compare launch wiring, and XAML coverage for the new review flow. | Amelia / Codex |
