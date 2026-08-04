---
baseline_commit: handoff-2026-08-04
---

# Story 5.27: Add Map Georeference Review Launcher And Workflow

Status: in-progress

## Story

As a cadastral examiner reviewing a Compute transaction,
I want an M-Geo action that opens a map georeference workflow using the transaction source PDF and printed JAD2001 reference points,
so that I can verify whether the plan geometry aligns with the cadastral map before final spatial review.

## Business Context

Recent Compute reviews showed parcels whose generated geometry was dimensionally valid but visually offset from imagery and cadastral reference layers. The examiner needs a direct way to compare the source plan against the map using the printed reference coordinates, without leaving the ArcGIS Pro workflow or changing authoritative data.

This workflow must support Jamaica survey plan review in JAD2001 only. The source PDF may contain printed coordinates and bearing/distance information. The georeference review should help determine whether the source plan image, extracted geometry, and map position are consistent before the case proceeds.

## Acceptance Criteria

1. The Transactions List toolbar includes an `M-Geo` button near the `SD` and `CMP` actions.
2. `M-Geo` is enabled only when a transaction is loaded and a case folder is available.
3. Pressing `M-Geo` opens a map georeference review window for the loaded transaction.
4. The review window lists readable source documents from the transaction source folder; supported initial formats are PDF and image files.
5. Archive files such as `.zip` and `.rar` are not shown as readable documents.
6. The selected source document is visible in the review workflow using the same stable PDF/image viewer approach used by the validation tool or supporting documents window.
7. The workflow lets the examiner use two printed JAD2001 reference coordinates from the document and corresponding map/control points to assess placement.
8. All geometry, control points, map operations, and generated overlays use JAD2001 / EPSG:3448. No other coordinate system is allowed for workflow output.
9. The workflow can create a temporary georeferenced plan overlay on the map with 70 percent transparency for visual inspection.
10. The temporary overlay is grouped and named with the transaction number.
11. The workflow reports residual error, rotation, scale, and coordinate-system warnings in plain language.
12. If the overlay cannot be created, the user sees an actionable message and the workflow does not crash ArcGIS Pro.
13. The source PDF and existing authoritative layers are never modified by this workflow.
14. Transaction cleanup actions such as Cancel, Suspend, Finalize, or closing the active transaction remove or hide temporary M-Geo review layers.
15. Existing `SD`, `CMP`, Compute, and Compare launch behavior is not regressed.

## Tasks / Subtasks

- [x] Complete the Transactions List launcher
  - [x] Add `M-Geo` button near `SD` and `CMP`.
  - [x] Gate `M-Geo` on loaded transaction/case availability.
  - [x] Replace the current supporting-documents launcher fallback with the dedicated M-Geo workflow window.
- [x] Build the M-Geo review workflow
  - [x] Reuse the stable source-document viewer pattern from the Points Validation Tool / Supporting Documents window.
  - [x] Add source document selection for readable transaction documents.
  - [x] Capture or select two document reference points and their JAD2001 coordinates.
  - [x] Capture or select the matching map/control points.
- [x] Implement JAD2001 georeference processing
  - [x] Enforce EPSG:3448 for all generated geometry and overlays.
  - [x] Compute two-point transform diagnostics: residual error, rotation, scale, and offset.
  - [x] Generate a temporary plan overlay suitable for ArcGIS Pro map display.
  - [x] Apply 70 percent transparency to the plan overlay.
- [x] Integrate with map lifecycle
  - [x] Add overlay layers to the configured working map group for the active transaction.
  - [x] Remove or hide M-Geo layers during transaction cleanup.
  - [x] Handle missing map, missing document, missing coordinates, and projection mismatch with clear user messages.
- [ ] Verify behavior
  - [ ] Manual smoke test with TR100000861 and TR100000862.
  - [ ] Confirm the map remains in JAD2001 after launching M-Geo.
  - [ ] Confirm the workflow does not mutate source PDFs, case artifacts, or authoritative layers.
  - [ ] Confirm ArcGIS Pro does not crash when opening, closing, or re-opening the M-Geo workflow.

### Review Findings

- [x] [Review][Patch] Guard coordinate parsing against non-finite values that can hang the UI [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs:231]
- [x] [Review][Patch] Unsubscribe the M-Geo view model from supporting-document property changes when the window closes [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs:27]
- [x] [Review][Patch] Dispose or detach the M-Geo WebView2 instance on window close to avoid repeated viewer resource leaks [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs:70]
- [x] [Review][Patch] Revalidate the selected document/navigation key after async WebView2 initialization before navigating [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs:128]
- [x] [Review][Patch] Avoid sharing the live Supporting Documents window view model with M-Geo because selection/status changes leak across windows [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs:40]
- [x] [Review][Patch] Restrict M-Geo readable documents to PDF and image formats as specified [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml:58]
- [x] [Review][Patch] Add residual error and coordinate-system warning diagnostics, not just distance/scale/rotation/offset [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs:186]
- [x] [Review][Patch] Do not mark M-Geo layer cleanup complete until temporary overlay layers exist and are removed or hidden during cleanup [_bmad-output/implementation-artifacts/5-27-add-map-georeference-review-launcher-and-workflow.md:57]
- [x] [Review][Patch] Implement the temporary georeferenced map overlay, 70 percent transparency, transaction grouping, JAD2001 map validation, and overlay failure messages required by AC 8-10 and AC 12 [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs:42]
- [x] [Review][Patch] Hide the live WebView2 PDF host while the captured-image picker is active so PDF point clicks are handled by the WPF image control [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs:86]
- [x] [Review][Patch] Simplify M-Geo coordinate entry so the overlay uses picked PDF points plus one matching JAD2001 coordinate set instead of duplicate document/map coordinate fields [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml:202]
- [x] [Review][Patch] Remove transaction M-Geo overlay groups during Cancel/Suspend/Finalize cleanup even when the M-Geo window is already closed [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3393]

## Developer Notes

The initial toolbar hook has already been added in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`

The current hook opens the Supporting Documents window as a temporary entry point. The dedicated M-Geo workflow should replace that fallback once the georeference window/service exists.

Relevant existing patterns:

- Source document loading and PDF rendering: `SupportingDocumentsWindow.*`
- Stable embedded PDF viewer behavior: Points Validation Tool / `JamaicaReviewWorkspaceWindow.*`
- Working map creation and configured layers: `Workflow/Maps/*`
- Transaction cleanup and loaded-case state: `TransactionPanelState.cs`

ArcGIS Pro map and layer changes must be performed through the established map service / `QueuedTask` pattern. View models should not directly manipulate map layers.

JAD2001 / EPSG:3448 is mandatory for this workflow. Any source or map operation that would generate Web Mercator or WGS84 output must be rejected or reprojected into JAD2001 before display or persistence.

## References

- Story 5.26: Configure Default Working Map And Reference Layers
- Story 9.2: Add Supporting Documents Full Panel Viewer
- User request: add `M-Geo` button near `SD` and `CMP`, enabled only when a transaction is loaded
- User review cases: TR100000861 and TR100000862

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-04 | 0.1 | Initial story created for M-Geo launcher and georeference workflow tracking. | Codex |
| 2026-08-04 | 0.2 | Implemented first M-Geo window slice: dedicated launcher target, source document viewer reuse, manual JAD2001 two-point diagnostics, and cleanup hook. | Codex |
| 2026-08-04 | 0.3 | Patched review findings for M-Geo window lifecycle, WebView cleanup, stale navigation guard, coordinate parsing, PDF/image-only document selection, and diagnostics wording. | Codex |
| 2026-08-04 | 0.4 | Added PDF view capture, document point picking, temporary JAD2001 world-file overlay creation, 70 percent map transparency, transaction grouping, and cleanup removal. | Codex |
| 2026-08-04 | 0.5 | Patched captured PDF point picking so WebView2 no longer intercepts clicks intended for the WPF capture image. | Codex |
| 2026-08-04 | 0.6 | Simplified M-Geo inputs to picked PDF points plus matching JAD2001 coordinates and replaced misleading duplicated coordinate diagnostics. | Codex |
| 2026-08-04 | 0.7 | Patched transaction exit cleanup so Cancel, Suspend, and Finalize remove the transaction M-Geo overlay group by transaction number. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Debug` passed with 0 warnings and 0 errors.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Debug` passed after M-Geo overlay implementation with 0 warnings and 0 errors.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Debug` passed after hiding the WebView2 host during captured-image picking with 0 warnings and 0 errors.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Debug` passed after simplifying M-Geo coordinate entry with 0 warnings and 0 errors.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Debug` passed after transaction-scoped M-Geo overlay cleanup with 0 warnings and 0 errors.

### Completion Notes List

- Initial `M-Geo` launcher wiring was added before this tracking story was created.
- Added a dedicated M-Geo WPF window instead of opening the Supporting Documents fallback.
- Reused the stable supporting-documents source list and PDF/image viewer pattern in the M-Geo window.
- Added manual JAD2001 reference/control coordinate entry and two-point fit diagnostics for distance, scale, rotation, and offset.
- Added transaction cleanup hooks so the M-Geo window closes when the active transaction is cleared.
- Patched M-Geo window resource cleanup, independent document selection state, stale WebView navigation protection, non-finite coordinate rejection, and PDF/image-only source selection.
- Added visible-PDF capture for point picking, image-document point picking, temporary JAD2001 PNG/world-file overlay generation, 70 percent overlay transparency, transaction-specific map grouping, and cleanup removal when the loaded transaction is cleared.
- Patched captured PDF point picking by collapsing the live WebView2 PDF host once a capture is available; this avoids WebView2 swallowing clicks that should select document reference points.
- Simplified M-Geo so the examiner picks two points on the PDF capture and enters the matching JAD2001 coordinates once; diagnostics now report pixel distance, JAD2001 distance, scale, and rotation for the actual overlay transform.
- Patched transaction cleanup so the M-Geo overlay group is removed from the active map by transaction number even if the M-Geo review window is not open.
- Manual ArcGIS Pro smoke testing with TR100000861/TR100000862 remains pending outside this build-only validation pass.

### File List

- `_bmad-output/implementation-artifacts/5-27-add-map-georeference-review-launcher-and-workflow.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceOverlayService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
