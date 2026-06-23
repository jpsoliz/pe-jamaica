---
baseline_commit: handoff-2026-06-22
---

# Story 5.20: Configure COGO-Style Map Symbology, Labeling, And Editing Experience For Non-Fabric Spatial Outputs

Status: review

## Story

As a cadastral examiner reviewing standard non-fabric output layers,  
I want ArcGIS Pro to load those layers with COGO-style labels, parcel-friendly symbology, and the right editing context,  
so that I can inspect and correct parcel geometry in a map experience that feels close to parcel review work even when Parcel Fabric is not being used.

## Acceptance Criteria

1. Given the workflow is using the standard non-fabric output mode, when Create Spatial Units succeeds and layers are loaded into the active map, then the generated `parcel_points`, `parcel_lines`, and `parcel_polygons` layers are added with a consistent review-ready drawing order and visible symbology.
2. Given `parcel_points` are loaded for review, when the map integration applies the layer definition, then points display with clear point symbols and labels driven by `point_id`.
3. Given `parcel_lines` are loaded for review, when the map integration applies the layer definition, then lines display with parcel-review symbology and labels derived from the line bearing and distance fields in a COGO-style format.
4. Given `parcel_polygons` are loaded for review, when the map integration applies the layer definition, then polygons render as lightly filled parcel areas with readable parcel boundary emphasis and parcel name/identifier support where appropriate.
5. Given multiple output layers are loaded together, when the map setup completes, then the drawing order keeps polygons below lines and lines below points so labels and geometry remain readable.
6. Given the non-fabric output path is meant for map-based correction, when the review context is prepared, then the user is placed into a map experience that supports standard ArcGIS Pro snapping, feature selection, attribute editing, and geometry correction without requiring Parcel Fabric.
7. Given the user is in Map Review with non-fabric outputs, when the stage loads, then the add-in shows clear status guidance that this is a COGO-ready non-fabric review surface and not a true Parcel Fabric workspace.
8. Given a layer is missing required fields for the configured label/symbology behavior, when the map integration runs, then the add-in degrades gracefully, avoids crashing, and surfaces a useful warning instead of a silent failure.
9. Given this story is complete, when an examiner runs the standard non-fabric path, then the resulting map review experience visibly shows coordinates, point ids, parcel geometry, and COGO-style line labeling well enough to support manual correction before final review.

## Tasks / Subtasks

- [x] Define the standard non-fabric review cartography contract. (AC: 1-5, 7-9)
  - [x] Standardize the expected layer order for `parcel_polygons`, `parcel_lines`, and `parcel_points`.
  - [x] Define the default symbology goals for each layer role.
  - [x] Define which fields drive labels in the non-fabric review experience.

- [x] Implement point layer review styling. (AC: 2, 5, 8-9)
  - [x] Apply a consistent point symbol to `parcel_points`.
  - [x] Enable point labels from `point_id`.
  - [x] Keep label configuration resilient when the field contract is partially missing.

- [x] Implement line layer COGO-style styling and labels. (AC: 3, 5, 8-9)
  - [x] Apply a review-oriented line symbol to `parcel_lines`.
  - [x] Build a label expression using line bearing and distance fields.
  - [x] Support graceful fallback when only text distance or partial bearing fields are available.

- [x] Implement polygon review styling. (AC: 4-5, 8-9)
  - [x] Apply a light-fill, strong-outline parcel review style to `parcel_polygons`.
  - [x] Optionally label polygons by `parcel_name` or `parcel_id` when useful and readable.

- [x] Improve non-fabric map review experience and messaging. (AC: 6-8)
  - [x] Ensure output-loading status text clearly distinguishes non-fabric review from Parcel Fabric mode.
  - [x] Add clear fallback messaging when labels or styling cannot be fully applied.
  - [x] Confirm Map Review messaging aligns with current stage vocabulary.

- [x] Add focused tests for map integration behavior. (AC: 1-9)
  - [x] Cover label configuration for points.
  - [x] Cover line label expression setup.
  - [x] Cover drawing-order and graceful-degradation behavior where feasible in existing test seams.

## Dev Notes

### Why This Story Exists

- The non-fabric path now has a stronger COGO-ready schema, but the map experience still needs to feel intentionally prepared for parcel review.
- The user explicitly wants the generated non-fabric data to behave more like a parcel review workspace: visible point ids, distances, bearings, and readable parcel geometry.
- This story is the bridge between “the right attributes exist” and “the examiner can comfortably work with them in ArcGIS Pro.”

### Scope Boundary

This story is for the **standard non-fabric output mode** only.

It should improve:

- map symbology
- labels
- drawing order
- review messaging
- standard editing readiness

It should **not** implement:

- true Parcel Fabric behavior
- parcel records / active record setup
- build parcels
- fabric topology tools
- enterprise sync

### Recommended Non-Fabric Review Presentation

#### Layer order

1. `parcel_polygons`
2. `parcel_lines`
3. `parcel_points`

#### Points

- symbol: clear, small review point marker
- label: `point_id`
- purpose: locate and verify vertices quickly

#### Lines

- symbol: parcel boundary review line
- label: `bearing_txt + ' ' + distance_txt`
- fallback: use `distance_m` when `distance_txt` is empty
- purpose: give the examiner a COGO-like read of each boundary segment

#### Polygons

- symbol: light parcel fill with stronger boundary outline
- optional label: `parcel_name` or `parcel_id`
- purpose: make parcel grouping obvious while keeping lines/points readable

### Technical Direction

The most likely implementation seam is:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`

That service already:

- loads output layers
- zooms to them
- applies point labeling

This story should extend that seam rather than introducing a separate map loader.

### Suggested Labeling Rules

#### Points

- Arcade: `$feature.point_id`

#### Lines

Preferred:

- if both `bearing_txt` and `length_txt` exist, label with both
- if `bearing_txt` is missing but `distance_m` exists, label with numeric distance
- if neither is present, leave labels off and warn softly

Example intent:

- `N89°34'10"W 55.00`
- `S00°12'38"E 135.01`

### Suggested Resilience Rules

- If `parcel_points` exists but `point_id` is missing, do not fail layer loading; skip point labels and show a warning.
- If `parcel_lines` exists but COGO text fields are missing, keep the line layer visible and use fallback labeling or no labels.
- If `parcel_polygons` exists but parcel name fields are missing, keep polygons styled without labels.

### User-Facing Message Direction

The status/messaging should say plainly that:

- the standard output is a COGO-ready review workspace
- it supports map-based editing and manual correction
- Parcel Fabric tools are only available in Parcel Fabric mode

That keeps expectations accurate while still making the standard mode feel intentional and capable.

### Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/`

### References

- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/5-19-define-cogo-ready-non-fabric-output-layer-schema.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-22 | 0.1 | Drafted story for non-fabric COGO-style map symbology, labeling, and editing readiness. | Codex |
| 2026-06-22 | 1.0 | Implemented non-fabric review styling, resilient labeling, reviewer-facing messaging, and focused output-map tests. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /nodeReuse:false`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Extended the existing output map integration seam so non-fabric review styling is applied automatically when spatial outputs are added to the active map.
- Standardized non-fabric layer ordering to polygons, then lines, then points, matching the review-oriented COGO display contract from the story.
- Added reviewer-friendly point, line, and polygon renderers plus resilient labeling that prefers `point_id`, `bearing_txt`, `length_txt`, `distance_m`, `parcel_name`, and `parcel_id`.
- Added graceful warning behavior so missing label fields or styling issues degrade safely instead of breaking map loading.
- Updated success messaging to clearly position the standard path as a COGO-ready non-fabric review surface, distinct from Parcel Fabric mode.
- Added focused tests for layer ordering and non-fabric messaging; full harness passes with 242 tests.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/OutputMapReviewStylingTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `_bmad-output/implementation-artifacts/5-20-configure-cogo-style-map-symbology-labeling-and-editing-experience-for-non-fabric-spatial-outputs.md`
