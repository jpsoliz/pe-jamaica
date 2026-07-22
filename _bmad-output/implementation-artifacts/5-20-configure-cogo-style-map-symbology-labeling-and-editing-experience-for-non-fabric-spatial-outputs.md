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
3. Given `parcel_lines` are loaded for review, when the map integration applies the layer definition, then lines display with parcel-review symbology and labels derived from the segment length only, preferring `length_txt` and falling back to `distance_txt` / `distance_m`.
4. Given `parcel_polygons` are loaded for review, when the map integration applies the layer definition, then polygons render with transparent parcel fill, readable parcel boundary emphasis, and parcel name/identifier support where appropriate.
5. Given multiple output layers are loaded together, when the map setup completes, then the drawing order keeps polygons below lines and lines below points so labels and geometry remain readable.
6. Given the non-fabric output path is meant for map-based correction, when the review context is prepared, then the user is placed into a map experience that supports standard ArcGIS Pro snapping, feature selection, attribute editing, and geometry correction without requiring Parcel Fabric.
7. Given the user is in Map Review with non-fabric outputs, when the stage loads, then the add-in shows clear status guidance that this is a COGO-ready non-fabric review surface and not a true Parcel Fabric workspace.
8. Given a layer is missing required fields for the configured label/symbology behavior, when the map integration runs, then the add-in degrades gracefully, avoids crashing, and surfaces a useful warning instead of a silent failure.
9. Given optional structured survey points are checked in Supporting Document Check, when Create Spatial Units succeeds, then the point file is imported into the output GDB as a supplemental/supporting layer such as `survey_point_layer`, added to the transaction review group as supporting context, and hidden by default so it does not compete with the primary computed parcel layers.
10. Given optional AutoCAD survey source is checked in Supporting Document Check, when Create Spatial Units succeeds, then only the configured/allowed DWG-derived content is imported into the output GDB as supplemental/supporting CAD reference layers or datasets such as `survey_cad_reference`, added to the transaction review group as supporting context, and hidden by default so it can be turned on manually only when needed.
11. Given a transaction review group is created in the active map, when primary and supplemental outputs are loaded, then the group contains clear subgroups such as `Computed Parcel Review` for `parcel_points`, `parcel_lines`, and `parcel_polygons`, and `Supporting Sources` for checked survey point and DWG-derived reference layers.
12. Given optional supporting layers are hidden by default, when an examiner manually turns them on, then they display as reference/support context only and do not replace or alter the primary `parcel_points`, `parcel_lines`, or `parcel_polygons` review layers.
13. Given this story is complete, when an examiner runs the standard non-fabric path, then the resulting map review experience visibly shows coordinates, point ids, parcel geometry, and COGO-style line labeling well enough to support manual correction before final review while keeping optional supporting imports available, organized, and unobtrusive.

## Tasks / Subtasks

- [x] Define the standard non-fabric review cartography contract. (AC: 1-5, 7-13)
  - [x] Standardize the expected layer order for `parcel_polygons`, `parcel_lines`, and `parcel_points`.
  - [x] Define the default symbology goals for each layer role.
  - [x] Define which fields drive labels in the non-fabric review experience.
  - [x] Define supplemental supporting layer behavior for checked survey point and DWG imports: imported into the GDB, added under a supporting subgroup in the map/review group, hidden by default, and never treated as primary parcel output.

- [x] Implement point layer review styling. (AC: 2, 5, 8, 12)
  - [x] Apply a consistent point symbol to `parcel_points`.
  - [x] Enable point labels from `point_id`.
  - [x] Keep label configuration resilient when the field contract is partially missing.

- [x] Implement line layer COGO-style styling and labels. (AC: 3, 5, 8, 12)
  - [x] Apply a review-oriented line symbol to `parcel_lines`.
  - [x] Build a label expression using only line length/distance fields.
  - [x] Support graceful fallback when text length/distance fields are partially available.

- [x] Implement polygon review styling. (AC: 4-5, 8, 12)
  - [x] Apply a light-fill, strong-outline parcel review style to `parcel_polygons`.
  - [x] Optionally label polygons by `parcel_name` or `parcel_id` when useful and readable.

- [x] Improve non-fabric map review experience and messaging. (AC: 6-8)
  - [x] Ensure output-loading status text clearly distinguishes non-fabric review from Parcel Fabric mode.
  - [x] Add clear fallback messaging when labels or styling cannot be fully applied.
  - [x] Confirm Map Review messaging aligns with current stage vocabulary.

- [x] Implement supplemental supporting import visibility behavior. (AC: 9-12)
  - [x] Ensure `survey_point_layer`/structured survey point output is included in the output GDB when the Supporting Document Check option is checked.
  - [x] Ensure configured/allowed `survey_cad_reference`/DWG-derived output is included in the output GDB when the Supporting Document Check option is checked, without importing unwanted DWG layers.
  - [x] Ensure the transaction review group includes a primary computed parcel subgroup and a supporting sources subgroup when supplemental outputs exist.
  - [x] Ensure these supplemental outputs are added under the supporting sources subgroup hidden by default.
  - [x] Ensure hidden supplemental layers are clearly named as supporting/reference layers and do not replace the primary parcel point/line/polygon layers.

- [x] Add focused tests for map integration behavior. (AC: 1-13)
  - [x] Cover label configuration for points.
  - [x] Cover line label expression setup.
  - [x] Cover drawing-order and graceful-degradation behavior where feasible in existing test seams.
  - [x] Cover checked optional survey points and DWG imports being map-loadable under a supporting subgroup but hidden by default.

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

- symbol: clear, small review point marker with white fill and black outline
- label: `point_id`
- purpose: locate and verify vertices quickly

#### Lines

- symbol: parcel boundary review line
- label: length only, using `length_txt` first and `distance_txt` as fallback
- fallback: use `distance_m` when `distance_txt` is empty
- purpose: give the examiner a COGO-like read of each boundary segment

#### Polygons

- symbol: transparent parcel fill with stronger boundary outline
- optional label: `parcel_name` or `parcel_id`
- purpose: make parcel grouping obvious while keeping lines/points readable

#### Supplemental Supporting Layers

Checked optional supporting documents are not primary parcel outputs.

- Structured survey points:
  - source role: `coordinate_text_source`
  - expected output: `survey_point_layer` or equivalent supplemental point layer in the output GDB
  - map behavior: add to the review group hidden by default
  - purpose: reference/compare submitted point file values without allowing it to drive the primary computed parcel geometry
- AutoCAD survey source:
  - source role: `dwg_source`
  - expected output: `survey_cad_reference` or equivalent supplemental CAD reference dataset/layer in the output GDB
  - map behavior: add to the review group hidden by default
  - purpose: reference submitted CAD content when the examiner explicitly turns it on

The primary visible review layers remain:

1. `parcel_polygons`
2. `parcel_lines`
3. `parcel_points`

Recommended transaction group organization:

1. `TR <transaction number> - Review`
2. `Computed Parcel Review`
   - `parcel_polygons`
   - `parcel_lines`
   - `parcel_points`
3. `Supporting Sources`
   - `survey_point_layer`
   - `survey_cad_reference` or allowed DWG-derived child layers

Supplemental supporting layers should be loaded under `Supporting Sources`, hidden by default, and should not obscure parcel labels when the map opens. DWG imports may contain many internal CAD layers; only configured/allowed DWG-derived outputs should be imported into the GDB or exposed in the map. Do not make the DWG reference subgroup visible by default.

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

- if `length_txt` exists, label with `length_txt`
- if `length_txt` is missing and `distance_txt` exists, label with `distance_txt`
- if text length/distance fields are missing but `distance_m` exists, label with numeric distance
- if no length/distance fields are present, leave labels off and warn softly

Example intent:

- `55.00`
- `135.01`

### Suggested Resilience Rules

- If `parcel_points` exists but `point_id` is missing, do not fail layer loading; skip point labels and show a warning.
- If `parcel_lines` exists but COGO text fields are missing, keep the line layer visible and use fallback labeling or no labels.
- If `parcel_polygons` exists but parcel name fields are missing, keep polygons styled without labels.
- If checked supplemental survey point or DWG imports are created but cannot be added to the map, keep the primary parcel layers usable and show a non-blocking warning.
- If supplemental survey point or DWG imports are added to the map, set initial visibility to off/hidden by default.
- If a DWG contains many source layers, import only the layers allowed by configuration/script rules and place the resulting reference layers under the supporting sources subgroup.
- If subgroup creation fails, keep the primary computed parcel layers usable and show a non-blocking warning rather than failing the Create Spatial Units result.

### User-Facing Message Direction

The status/messaging should say plainly that:

- the standard output is a COGO-ready review workspace
- it supports map-based editing and manual correction
- Parcel Fabric tools are only available in Parcel Fabric mode

That keeps expectations accurate while still making the standard mode feel intentional and capable.

### Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
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
| 2026-07-02 | 1.1 | Added supplemental supporting layer contract: checked survey point and DWG imports must be imported into the GDB and map-loadable but hidden by default. | Codex |
| 2026-07-02 | 1.2 | Added transaction review subgroup contract: primary computed parcel layers stay separate from hidden supporting survey point and DWG-derived reference layers. | Codex |
| 2026-07-02 | 1.3 | Implemented supporting layer subgroup routing and hidden-by-default map loading behavior with focused tests. | Codex |
| 2026-07-02 | 1.4 | Patched review findings: supporting map-load failures are non-blocking and DWG imports can enforce configured CAD layer allowlists. | Codex |
| 2026-07-02 | 1.5 | Patched parcel-fabric map path resolution so checked supporting survey point and DWG layers remain map-loadable under the hidden supporting sources subgroup. | Codex |
| 2026-07-21 | 1.6 | Updated review cartography contract for PE Compute testing: transparent parcel fill, length-only line labels, and white-fill/black-outline points. | Mary / Codex |
| 2026-07-21 | 1.7 | Implemented PE computed parcel review symbol polish: `parcel_points` black outline with white fill and `parcel_polygons` 60% transparency while preserving line labels. | Codex |
| 2026-07-21 | 1.8 | Patched PE computed parcel review styling to use explicit ArcGIS layer transparency for polygons and corrected point marker layer ordering so the white fill remains visible. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /nodeReuse:false`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Extended the existing output map integration seam so non-fabric review styling is applied automatically when spatial outputs are added to the active map.
- Standardized non-fabric layer ordering to polygons, then lines, then points, matching the review-oriented COGO display contract from the story.
- Added reviewer-friendly point, line, and polygon renderers plus resilient labeling that prefers `point_id`, length/distance fields, `parcel_name`, and `parcel_id`.
- Updated PE Compute review styling so parcel points use white fill with black outline, parcel polygons use transparent fill, and line labels show only length.
- Implemented the requested Create Spatial Units map styling refinement: `parcel_points` render as a black outer marker with a white inner fill, `parcel_polygons` now set ArcGIS layer transparency to 60% with an opaque fill symbol so the Contents pane and map display align, and `parcel_lines` styling and labels remain unchanged.
- Added graceful warning behavior so missing label fields or styling issues degrade safely instead of breaking map loading.
- Updated success messaging to clearly position the standard path as a COGO-ready non-fabric review surface, distinct from Parcel Fabric mode.
- Added focused tests for layer ordering and non-fabric messaging.
- Added supporting-source routing so checked `survey_point_layer` and `survey_cad_reference` outputs load under `Supporting Sources`, hidden by default, while primary parcel layers load under `Computed Parcel Review`.
- Added focused test coverage for supporting source grouping and hidden-by-default behavior; full harness passes with 285 tests.
- Patched supporting layer load resilience so subgroup/layer creation failures are reported as warnings while primary computed parcel layers continue loading.
- Added `spatial_output_dwg_allowed_layers` execution setting and `--dwg-allowed-layers` adapter argument so DWG imports can remove CAD rows outside the configured CAD layer allowlist.
- Added Python unit coverage for DWG allowlist parsing/matching; targeted adapter tests pass with 11 tests.
- Confirmed TR `100000379` generated `survey_point_layer` and `survey_cad_reference` in `output_summary.json`, then patched parcel-fabric path resolution so those supplemental paths are not dropped before map loading; full add-in harness passes with 286 tests.
- Added regression coverage for the computed parcel review point halo and polygon transparency constants; full add-in harness passes with 474 tests after the symbol update.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/OutputMapReviewStylingTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowExecutionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `_bmad-output/implementation-artifacts/5-20-configure-cogo-style-map-symbology-labeling-and-editing-experience-for-non-fabric-spatial-outputs.md`
