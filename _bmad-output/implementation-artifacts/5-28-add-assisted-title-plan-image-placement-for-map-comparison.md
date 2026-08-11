---
baseline_commit: handoff-2026-08-10
---

# Story 5.28: Add Assisted Title Plan Image Placement For Map Comparison

Status: in-progress

## Story

As a cadastral examiner comparing an old title plan against the current cadastral map,
I want to place a scanned title-plan page from the active transaction attachments onto the ArcGIS Pro map using controlled image-placement tools,
so that I can visually compare the old registered plan evidence with current parcel, imagery, and reference layers without reconstructing COGO geometry.

## Business Context

The current manual process uses MicroSurvey to add the scanned title-plan image and position it as well as possible over the map. The product needs the same comparison capability inside the ArcGIS Pro add-in, but with stronger control, repeatability, and audit evidence.

This story is not about extracting parcel coordinates or building COGO geometry. The primary job is scanned-image map comparison:

- identify the relevant title-plan page from transaction attachments,
- place that image over the configured working map,
- let the examiner align it to current map evidence using matching control points and manual adjustment tools,
- preserve placement metadata and restore the overlay later.

Example UAT documents reviewed:

- `c:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\UAT_Training\1000-55.pdf`
- `c:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\UAT_Training\1150-100.pdf`
- `c:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\UAT_Training\1200-66.pdf`

Findings from those examples:

- The relevant plan pages are scanned raster images, not text PDFs.
- Page location is not always fixed: `1200-66.pdf` has a caveat/notice on page 2 and the plan/map on page 3.
- Some plan pages provide only bearings/distances, scale, north arrow, roads, lots, adjacent owners, and visual map context; they do not provide enough coordinate control for direct coordinate placement.
- The required workflow is therefore assisted image placement, not COGO reconstruction.

Latest product review correction:

- The title-plan PDF generally does not print a coordinate system or usable JAD2001 coordinate values.
- The placed image is reference evidence only. The workflow must not imply survey accuracy or require source-document coordinates.
- The primary placement interaction should be matching visual control points: pick two or more points on the captured plan image and pick the corresponding points directly on the ArcGIS Pro map.
- Manual coordinate entry may exist only as an optional fallback/debug path. It must not be the examiner's required path for this story.

## Acceptance Criteria

1. Given a transaction is loaded and its attachments have been copied into the Case Folder, when the examiner opens the image-placement workflow, then the workflow lists PDF and raster image attachments from the active transaction source folder with attachment provenance where available.
2. Given the examiner is in the Transaction List, when a transaction is loaded, then the title-plan image placement action is available with the map-review actions near `M-Geo`, using a compact grouped toolbar/dropdown pattern that remains stable when the dockpane is resized.
3. Given no transaction is loaded or no Case Folder/source attachments are available, when the Transaction List toolbar renders, then the title-plan image placement button is disabled with a clear tooltip.
4. Given a source PDF has multiple pages, when the examiner selects the PDF, then the workflow lets the examiner choose the page to use for map comparison rather than assuming page 2.
5. Given the selected page is scanned raster content, when the page is prepared for placement, then the workflow renders or extracts a stable page image for control-point picking and overlay generation.
6. Given the examiner selects the title-plan page image, when the map placement workflow starts, then the current working map must be JAD2001 / EPSG:3448 or the workflow must block with a clear message.
7. Given the plan has no usable printed coordinates or printed coordinate system, when the examiner starts title-plan placement, then the workflow must still proceed as a reference-image comparison workflow and must not require COGO or printed coordinate entry.
8. Given the examiner identifies a feature on the captured plan image, when the examiner chooses the matching map-point action, then the next ArcGIS Pro map click captures the map coordinate for that control pair.
9. Given two plan/map control-point pairs are supplied by image clicks and map clicks, when the examiner previews placement, then the workflow creates a similarity transform preview with translation, rotation, and uniform scale.
10. Given three or more control-point pairs are supplied, when the examiner previews placement, then the workflow can use an affine transform option that supports translation, rotation, non-uniform scale, and skew.
11. Given more than the minimum number of control points are supplied, when placement diagnostics are calculated, then the workflow reports residual error per control point and overall RMS error in map units.
12. Given the overlay is active or being prepared, when the examiner changes transparency in the form, then the title-plan image overlay uses the selected transparency value and persists that value with the artifact.
13. Given the examiner picked the wrong plan/map point, when the examiner clears or removes that point/pair, then the workflow removes it from the placement calculation and disables overlay creation until the remaining control points are valid.
14. Given a title-plan comparison overlay already exists, when the examiner chooses remove/retry from the placement form, then the workflow removes the transaction-specific title-plan overlay from the active map and lets the examiner place it again.
15. Given the overlay is active, when the examiner needs fine adjustment, then the workflow provides manual nudge controls for move, rotate, and scale that preserve the control-point audit trail.
16. Given the examiner accepts placement, when the workflow saves, then the georeferenced overlay image, world/projection metadata or equivalent ArcGIS raster placement artifact, control-point pairs, transformation type, manual adjustments, residuals, selected source file, selected page number, transparency, and transaction number are persisted under the active transaction Case Folder.
17. Given a placement artifact exists for the active transaction, when the transaction is reopened or the placement workflow is launched again, then the workflow offers to restore the saved overlay before asking the examiner to place the image again.
18. Given an overlay is added to the map, when it is displayed, then it is placed in a transaction-specific group, uses a clear title-plan comparison name, defaults to a configurable transparency, and does not modify source PDFs, source images, authoritative cadastre layers, or extracted parcel geometry.
19. Given the placement residual exceeds a configured threshold, when the examiner attempts to save or use the overlay, then the workflow warns that the placement is approximate and records that warning in the artifact.
20. Given the examiner cancels, suspends, finalizes, or clears the active transaction, when transaction cleanup runs, then temporary title-plan comparison overlays are removed or hidden using the same transaction-scoped cleanup pattern as M-Geo overlays.
21. Given attachment rendering or image placement fails, when the workflow cannot prepare an overlay, then it shows an actionable error and leaves ArcGIS Pro stable.
22. Given existing M-Geo, SD, CMP/CMD, Compute, and Compare workflows exist, when this feature is added, then those existing launchers and saved M-Geo overlay restore behavior are not regressed.

## Out Of Scope

- COGO reconstruction from bearings/distances.
- Automatic parcel corner extraction from the scanned plan.
- Automatic legal/cadastre matching from volume/folio or owner labels.
- Authoritative geometry creation or replacement.
- Editing current cadastre, parcel fabric, or Enterprise working layers.
- Uploading the placed overlay back to Innola unless a later story explicitly adds that step.

## Tasks / Subtasks

- [x] Add the Transaction List launcher. (AC: 2-3, 22)
  - [x] Add a dedicated title-plan image placement button near the existing `SD`, `M-Geo`, and `CMP/CMD` toolbar actions.
  - [x] Refine the Transaction List toolbar so secondary actions are grouped into compact `Documents`, `Map Tools`, and `Compare` dropdowns with icon-backed menu items.
  - [x] Gate the button on loaded transaction, Case Folder availability, and source attachment availability.
  - [x] Add disabled-state tooltip text that explains what is missing.
  - [x] Preserve existing `SD`, `M-Geo`, and `CMP/CMD` launcher behavior.

- [ ] Extend the transaction attachment/page selection workflow. (AC: 1, 4-5)
  - [x] Reuse the existing Supporting Documents / M-Geo source-document list pattern for active transaction attachments.
  - [x] Show PDFs and supported raster images only.
  - [x] Add explicit page selection for PDFs, including cases where the comparison plan is not page 2.
  - [ ] Render or extract the selected page to a stable image used for point picking and overlay generation.

- [ ] Build the title-plan image placement workspace. (AC: 6-15)
  - [x] Require the active working map to be JAD2001 / EPSG:3448.
  - [x] Let the examiner pick control points on the captured plan image.
  - [x] Add `Pick map point 1` / `Pick map point 2` actions that capture the next ArcGIS Pro map click as the matching map control point.
  - [x] Treat manual JAD2001 coordinate entry as optional fallback/debug support only, not the primary examiner workflow.
  - [x] Display the selected plan/map control pairs in the form.
  - [x] Add clear/reset controls for a picked point and for all control pairs.
  - [x] Support two-point similarity placement.
  - [ ] Support three-or-more-point affine placement.
  - [ ] Display residual diagnostics when more than the minimum control points are available.
  - [x] Add transparency control in the form, defaulting to the existing overlay transparency.
  - [ ] Add manual nudge controls for move, rotate, and scale.

- [ ] Generate and manage the map overlay. (AC: 12, 14, 16-20, 22)
  - [x] Create a georeferenced plan image overlay in the active working map.
  - [x] Group overlay layers by transaction number and distinguish them from M-Geo coordinate-control overlays.
  - [x] Persist overlay image, placement metadata, plan/map control-point pairs, transformation type, residuals, selected source file, selected page, transparency, and warnings in the Case Folder.
  - [x] Apply the selected transparency to the ArcGIS Pro overlay layer.
  - [x] Restore an accepted overlay when the transaction is reopened or the placement workflow is launched again.
  - [x] Add a form action to remove the transaction-specific title-plan overlay from the active map.
  - [x] When recreating the overlay, replace the prior title-plan comparison overlay instead of stacking duplicates.
  - [x] Clean up temporary overlays during transaction exit actions.

- [ ] Add resilience and non-regression coverage. (AC: 18, 21-22)
  - [x] Ensure source PDFs/images are never modified.
  - [x] Ensure authoritative/current map layers are not modified.
  - [ ] Handle missing page images, unreadable PDFs, unsupported attachments, no active map, wrong map SR, and failed overlay creation.
  - [x] Confirm existing M-Geo restore-first behavior still works.
  - [x] Confirm SD/CMP/Compute/Compare launch behavior is unchanged.

- [ ] Verify with UAT examples. (AC: 1-22)
  - [ ] Verify `1000-55.pdf` can select the title-plan page and place the scanned parcel sketch by map control points.
  - [ ] Verify `1150-100.pdf` can place the scanned plan image without requiring printed coordinates.
  - [ ] Verify `1200-66.pdf` does not assume page 2 and can select the plan page for placement.
  - [ ] Verify saved overlays restore after closing/reopening the transaction.

## Developer Notes

### Relationship To M-Geo

Story 5.27 created the M-Geo launcher and overlay workflow for georeferencing source plan imagery using printed JAD2001 coordinates. This story should reuse as much of that infrastructure as practical, but it must not force the examiner into coordinate entry when the source document has no coordinates.

The distinction:

- M-Geo: coordinate-control placement when printed JAD2001 reference coordinates exist.
- Title Plan Image Placement: reference-only visual comparison placement from matched plan/map control points when source-document coordinates are absent or not useful.

For Title Plan Image Placement, the map coordinates must be captured from ArcGIS Pro map clicks as the primary path. Typed coordinates are not an acceptable primary workflow because the examiner is aligning visual evidence, not entering printed coordinate control.

If implemented in the same UI, the workflow should expose clear modes such as:

- `Coordinate Control` for existing M-Geo behavior.
- `Image Comparison` for this story.

### Relevant Existing Files And Patterns

Likely files to inspect and preserve:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceOverlayService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SupportingDocumentsWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/MapGeoreferenceOverlayTests.cs`

The Transaction List toolbar already contains `SD`, `M-Geo`, and `CMP/CMD` style launch actions. Add this feature as a peer action in that same toolbar area rather than burying it inside the Compute workspace.

Keep map operations inside established ArcGIS Pro `QueuedTask`/map-service boundaries. View models should coordinate state and commands but should not directly mutate map layers when a service pattern already exists.

### Attachment And Case Folder Requirements

The source image must come from the loaded transaction attachments copied into the Case Folder `source` area. Do not ask the user to browse arbitrary external files for the primary workflow. Manual browse may be a later fallback, but the acceptance path is transaction-attached evidence.

Persisted metadata should be transaction-scoped and include at least:

- transaction number
- source relative path
- original attachment id/source role/category if available from manifest provenance
- selected PDF page number or image identifier
- generated page image path
- map spatial reference WKID/latest WKID
- transformation type
- control-point pairs: image pixel x/y and map x/y captured from ArcGIS Pro map clicks
- manual nudge values
- residuals/RMS error
- transparency
- warning list
- created/updated timestamp

### Transformation Guidance

Preferred implementation sequence:

1. Start with two-point similarity transform for MVP.
2. Add affine transform when at least three control pairs are available.
3. Add residual reporting before any higher-order transform.
4. Avoid polynomial/rubber-sheet transformation in this story unless an existing ArcGIS API makes it low-risk. Higher-order warping can visually fit bad scans but may distort evidence and should require a later explicit product decision.

### UX Guardrails

- Do not label the placed image as authoritative geometry.
- Do not imply that a low-control placement is survey-accurate.
- Do not require or imply that the PDF itself has a coordinate system.
- Make the transformation mode and residual/warning status visible.
- Make the selected source file and page visible.
- Keep transparency, remove/retry, and nudge controls close to the map/overlay controls.
- Preserve the examiner's ability to compare against imagery, cadastral parcels, roads, and reference layers.

### Testing Requirements

Add focused unit/source tests where possible for:

- PDF/image attachment filtering.
- selected page metadata persistence.
- transformation metadata serialization.
- map-click control point capture routing.
- two-point similarity transform numeric behavior.
- affine transform input validation.
- residual warning threshold behavior.
- transparency persistence and layer application.
- remove/retry overlay cleanup.
- overlay artifact restore routing.
- transaction cleanup routing for title-plan comparison overlays.

Add source-level tests for UI wiring if direct ArcGIS Pro integration cannot be tested headlessly.

Manual ArcGIS Pro smoke tests are required for:

- selecting a PDF attachment and page,
- placing at least two plan/map control-point pairs using image clicks and ArcGIS Pro map clicks,
- adjusting transparency,
- removing and recreating a mistaken overlay,
- saving and restoring the overlay,
- cleanup on transaction exit.

## References

- Story 5.27: Add Map Georeference Review Launcher And Workflow
- Story 5.26: Configure Default Working Map And Reference Layers
- Story 9.2: Add Supporting Documents Full Panel Viewer
- User review examples: `1000-55.pdf`, `1150-100.pdf`, `1200-66.pdf`
- User clarification: main goal is comparing the scanned map image with the current map, similar to MicroSurvey image positioning, not COGO extraction.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Completion Notes List

- Story created from Mary/Winston review of scanned UAT examples and user clarification.
- Scope explicitly excludes COGO reconstruction and focuses on transaction-attached scanned plan image placement.
- Story is ready for implementation after developer review of existing M-Geo and supporting-document viewer patterns.
- Implemented first MVP slice: Transaction List `TP` launcher, title-plan image-comparison mode in the M-Geo window, two-point similarity overlay creation, separate title-plan overlay folder/layer/group naming, selected source/page metadata persistence, restore routing, and cleanup routing.
- Left story in-progress because full PDF page extraction, affine placement, residual diagnostics, manual nudge controls, and manual UAT smoke verification remain open.
- Verification: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release --no-restore` passed. Release test harness passed all new 5-28 tests, then stopped at the pre-existing `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates` failure.
- Code review patch corrected transaction-safe window reuse, PDF page navigation metadata, title-plan SR blocking, failed-overlay artifact cleanup, and the story checkbox that had over-claimed map control-point picking.
- Latest product review clarified that the current manual-coordinate approach is not acceptable for TP because title-plan PDFs usually do not print coordinate systems or usable coordinates. The remaining implementation must pivot to reference-only placement by captured plan-image points matched to ArcGIS Pro map-click points, with form-level transparency and remove/retry controls.
- Implemented the TP transparency and remove/retry patch: the form now exposes a transparency slider, persists `TransparencyPercent` in overlay metadata, applies the selected ArcGIS layer transparency, displays control-pair summaries, and provides clear-point / clear-all / remove comparison overlay actions.
- Implemented native ArcGIS Pro map-click capture for TP using `TitlePlanMapPointTool`, registered it in `Config.daml`, and added the `ArcGIS.Desktop.Extensions` reference required for `ArcGIS.Desktop.Mapping.MapTool`.
- Verification: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release --no-restore` passed. Release test harness passed the updated 5-28 checks, then stopped at the pre-existing `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates` failure.
- Packaging verification: `tools/package_addin.ps1 -Configuration Release` produced and registered `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`; the package `Config.daml` contains `ParcelWorkflow_TitlePlanMapPointTool` with `className="TitlePlanMapPointTool"`.
- UX refinement after Sally review: grouped the Transaction List secondary toolbar actions into compact `Documents`, `Map Tools`, and `Compare` menus. Icons remain visible at the group and item level, and `M-Geo`/`TP` are now siblings under `Map Tools` instead of separate right-side buttons.
- Sally UX cleanup for the TP form: shortened the window title and point/action labels, changed clipped horizontal controls to wrapping button rows, added a resizable splitter between the document viewer and control panel, and kept the right panel constrained but adjustable.
- Packaged and registered Release add-in version 1.1.131 after TP form UX cleanup.

### File List

- `_bmad-output/implementation-artifacts/5-28-add-assisted-title-plan-image-placement-for-map-comparison.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deployment/target-computer-tools/package/deployment_manifest.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceOverlayService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TitlePlanMapPointTool.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/MapGeoreferenceOverlayTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-10 | 0.1 | Created story for transaction-attachment based scanned title-plan image placement for map comparison, distinct from COGO reconstruction and coordinate-control M-Geo. | Mary / Winston / Codex |
| 2026-08-10 | 0.2 | Added Transaction List toolbar launcher requirement near `SD`, `M-Geo`, and `CMP/CMD`, including enablement and tooltip behavior. | Codex |
| 2026-08-10 | 0.3 | Implemented first MVP slice for TP launcher, image-comparison mode, two-point overlay creation, separate title-plan overlay persistence/restore/cleanup, and source-level tests. | Codex |
| 2026-08-10 | 0.4 | Patched code review findings for transaction-safe window reuse, selected-page navigation, failed overlay artifact cleanup, title-plan SR blocking, and corrected task status. | Codex |
| 2026-08-10 | 0.5 | Updated story after product review: TP is reference-only visual placement, must use plan-image clicks plus ArcGIS Pro map-click control points, and must add transparency plus remove/retry controls. | Codex |
| 2026-08-10 | 0.6 | Implemented transparency persistence/application plus clear/remove/retry controls; left native map-click capture open due unavailable public `MapTool` type in current add-in references. | Codex |
| 2026-08-10 | 0.7 | Added `ArcGIS.Desktop.Extensions` reference, implemented `TitlePlanMapPointTool`, registered it in DAML, and wired TP map-point commands to capture map-click coordinates. | Codex |
| 2026-08-10 | 0.8 | Refined Transaction List toolbar UX into grouped `Documents`, `Map Tools`, and `Compare` menus while preserving icons and TP/M-Geo proximity. | Sally / Codex |
| 2026-08-10 | 0.9 | Cleaned TP placement form title/labels, button wrapping, and added a resizable document/control panel splitter. | Sally / Codex |
| 2026-08-10 | 1.0 | Packaged and registered Release add-in version 1.1.131 after TP form UX cleanup. | Sally / Codex |
