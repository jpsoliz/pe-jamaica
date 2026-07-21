---
baseline_commit: handoff-2026-06-23
---

# Story 5.21: Add Optional COGO Attributes And Labels To Non-Fabric Spatial Output Layers

Status: review

## Story

As a cadastral examiner using the standard non-fabric output path,  
I want optional COGO-bearing, distance, and labeling support added to the generated review layers,  
so that the non-fabric map can show parcel-review information closer to Parcel Fabric behavior without forcing every deployment to use those fields and labels.

## Acceptance Criteria

1. Given the workflow is configured for the standard non-fabric output mode, when Create Spatial Units runs with optional COGO attributes enabled, then the generated `parcel_lines` and related non-fabric layers include the configured COGO-supporting fields needed for review labels and editing context.
2. Given extracted or validated review data already contains bearing, length, distance, or curve values, when the non-fabric output layers are created, then those values are preserved into the configured COGO-supporting fields instead of being discarded.
3. Given source extraction does not provide every desired COGO display field, when non-fabric outputs are generated with COGO attributes enabled, then the workflow computes reasonable fallback values from the reviewed geometry where possible and leaves unsupported values empty rather than failing output creation.
4. Given optional COGO labels are enabled for non-fabric outputs, when the output layers are loaded into ArcGIS Pro, then point labels, line labels, and parcel labels use the configured COGO-ready fields and degrade safely if some fields are absent.
5. Given optional COGO attributes are disabled in settings, when the standard non-fabric path runs, then the current simpler layer behavior remains supported and the workflow does not force new fields, labels, or failures into deployments that do not want them.
6. Given a reviewer uses the non-fabric map review path, when inspecting generated points and lines, then the visible map experience can show point ids, parcel identity, line bearing text, and line distance text closely enough to support parcel correction before Final Review.
7. Given this story is complete, when later stories extend Create Spatial Units, Map Review, or enterprise publishing, then they can rely on a stable optional COGO-ready field contract for the non-fabric branch instead of adding more ad hoc line attributes.

## Tasks / Subtasks

- [x] Add configuration for optional non-fabric COGO enrichment. (AC: 1, 4-5, 7)
  - [x] Define settings keys for enabling COGO-supporting attributes on standard non-fabric outputs.
  - [x] Define settings keys for enabling COGO-style labels independently from field creation.
  - [x] Document safe defaults so existing deployments keep working when the new settings are absent.

- [x] Extend the non-fabric output contract to carry optional COGO fields. (AC: 1-3, 7)
  - [x] Confirm which fields from Story 5.19 are required versus optional for the non-fabric branch.
  - [x] Standardize the line-level COGO fields used for display and editing support.
  - [x] Keep backward compatibility with existing `parcel_points`, `parcel_lines`, and `parcel_polygons` generation.

- [x] Preserve source COGO values from approved review data when available. (AC: 2-3, 6)
  - [x] Map extracted source values such as `bearing_txt`, `distance_txt`, `distance_m`, `radius_m`, and `arc_length_m` into non-fabric outputs.
  - [x] Preserve source values before attempting computed fallbacks.
  - [x] Ensure missing source values do not break non-fabric output creation.

- [x] Add computed fallback COGO enrichment for non-fabric lines. (AC: 3, 6-7)
  - [x] Compute fallback distance values from reviewed point geometry where source distance is absent.
  - [x] Compute fallback azimuth or bearing support fields where practical for straight segments.
  - [x] Leave unsupported curve-specific values empty unless source review data already provides them.

- [x] Update ArcGIS Pro non-fabric labeling to use the optional field contract. (AC: 4, 6-7)
  - [x] Keep point labels driven by `point_id`.
  - [x] Drive line labels from configured length/distance fields when enabled.
  - [x] Keep polygon labels parcel-oriented and readable without overwhelming the map.

- [x] Add focused verification coverage. (AC: 1-7)
  - [x] Cover output generation when optional COGO enrichment is enabled.
  - [x] Cover output generation when optional COGO enrichment is disabled.
  - [x] Cover graceful fallback behavior when source review data has only partial COGO values.

## Dev Agent Record

### Completion Notes

- Added optional non-fabric COGO settings to workflow execution loading with safe defaults: attributes off, labels off, source mode `source_then_computed`.
- Wired output adapter execution to pass the new toggles and source-mode selection into `output_adapter.py`.
- Updated non-fabric output generation to preserve source bearing/distance values first, compute safe straight-line fallback distance and azimuth support when enabled, and strip optional COGO fields from the standard non-fabric branch when disabled.
- Added output summary payload flags so ArcGIS Pro map loading can decide whether non-fabric labels should be applied.
- Updated non-fabric map styling so labels remain renderer-safe and are only applied for the standard branch when the new label toggle is enabled.
- Exposed the new non-fabric COGO settings in the editable `Settings` workspace under `Spatial Workspace`, including independent attribute and label toggles plus source-mode selection.
- Verified with `python -m unittest tests.test_output_adapter` and `dotnet run --project src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.Tests\\ParcelWorkflowAddIn.Tests.csproj` (PASS 256 tests).

## File List

- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowExecutionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Dev Notes

### Why This Story Exists

- Story 5.19 defined the target non-fabric schema contract.
- Story 5.20 improved non-fabric symbology and labeling behavior.
- The user now wants the standard non-fabric path to carry more of the parcel-review experience: visible point ids and line length/distance labeling, but only when desired.

This story is the bridge between:

- **schema definition** and
- **optional data population + label-ready usage**

for the standard non-fabric branch.

### Scope Boundary

This story applies only to the **standard non-fabric output mode**.

It should improve:

- field population
- optional COGO text/numeric attributes
- safe computed fallbacks
- optional labeling inputs for map review

It should **not** implement:

- true Parcel Fabric records or build workflows
- enterprise Parcel Fabric publication
- parcel fabric topology behavior
- a new extraction method
- final downstream authoritative promotion

### Recommended Settings Keys

Add these settings to the workflow settings contract for the standard non-fabric branch:

- `spatial_output_add_cogo_attributes`
- `spatial_output_add_cogo_labels`
- `spatial_output_cogo_source_mode`

Recommended meanings:

- `spatial_output_add_cogo_attributes`
  - `true`: create and populate optional COGO-support fields on non-fabric outputs
  - `false`: keep the current lighter non-fabric output behavior

- `spatial_output_add_cogo_labels`
  - `true`: prefer COGO-style point/line/parcel labeling in map review
  - `false`: keep simpler labels or no labels as currently configured

- `spatial_output_cogo_source_mode`
  - `prefer_source`
  - `prefer_computed`
  - `source_then_computed` (recommended default)

### Recommended Non-Fabric COGO-Support Fields

This story should align to Story 5.19 instead of inventing a parallel contract.

Priority line fields for this implementation:

- `from_point_id`
- `to_point_id`
- `segment_order`
- `bearing_txt`
- `distance_txt`
- `distance_m`
- `radius_m`
- `arc_length_m`
- `line_type`
- `parcel_id`
- `parcel_group_id`
- `status_txt`

Helpful optional computed fields if practical:

- `azimuth_deg`
- `is_computed_cogo`

Priority point and polygon label-support fields:

- `point_id`
- `parcel_id`
- `parcel_name`

### Data Precedence Rule

When both source-derived and computed values are possible:

1. preserve reviewed/source values first
2. compute fallbacks only for missing values
3. never overwrite a reviewed source bearing/distance with a computed value silently

That keeps the workflow faithful to the extracted and user-reviewed evidence.

### Expected Behavior For Straight vs Curve Segments

#### Straight segments

Should support:

- source `bearing_txt`
- source `distance_txt`
- fallback numeric `distance_m`
- optional computed azimuth/bearing support when source bearing is absent

#### Curve segments

Should support source-preserved values where available:

- `radius_m`
- `arc_length_m`
- `delta_angle_txt`
- `chord_bearing_txt`
- `chord_distance_m`

If those values are not present in the approved review data, this story should not try to invent curve math beyond safe straight-line fallbacks.

### Map Review Intent

This story is not trying to make the non-fabric branch identical to Parcel Fabric.

It is trying to make the non-fabric branch:

- more readable
- more COGO-aware
- more useful for map review and correction

while remaining:

- optional
- configuration-driven
- backward compatible

### Suggested Implementation Areas

- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/OutputMapReviewStylingTests.cs`

### References

- `_bmad-output/implementation-artifacts/5-19-define-cogo-ready-non-fabric-output-layer-schema.md`
- `_bmad-output/implementation-artifacts/5-20-configure-cogo-style-map-symbology-labeling-and-editing-experience-for-non-fabric-spatial-outputs.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-23 | 0.1 | Drafted optional COGO attribute and label enrichment story for non-fabric spatial outputs. | Codex |
| 2026-06-23 | 1.0 | Implemented optional non-fabric COGO enrichment, summary flags, map-label gating, and focused verification coverage. | Codex |
| 2026-06-23 | 1.1 | Exposed the non-fabric COGO enrichment settings in the Settings workspace and added settings round-trip coverage. | Codex |
| 2026-07-21 | 1.2 | Clarified that review line labels should use length/distance only while retaining COGO-support fields for diagnostics and data. | Mary / Codex |
