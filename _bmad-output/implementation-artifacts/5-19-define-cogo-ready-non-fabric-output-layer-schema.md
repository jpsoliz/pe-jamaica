---
baseline_commit: handoff-2026-06-22
---

# Story 5.19: Define COGO-Ready Non-Fabric Output Layer Schema

Status: drafted

## Story

As a cadastral examiner using the compute workflow,  
I want the standard non-fabric output mode to generate a COGO-ready layer set with a stable schema,  
so that I can review, label, edit, and hand off parcel geometry in ArcGIS Pro even when Parcel Fabric mode is not being used.

## Acceptance Criteria

1. Given the workflow is configured for the standard non-fabric output mode, when Create Spatial Units runs, then the transaction output geodatabase creates one authoritative COGO-ready layer set with fixed layer names and field names.
2. Given the non-fabric output mode is meant to support parcel editing in ArcGIS Pro, when the layer contract is defined, then points, lines, and polygons include the fields needed for parcel grouping, sequence, bearings, distances, source traceability, and examiner review status.
3. Given parcel lines are the primary COGO carrier in non-fabric mode, when the schema is defined, then the line feature class includes explicit fields for from-point, to-point, segment order, bearing text, distance, curve values where present, and parcel/traverse grouping.
4. Given parcel points are used for labels, snapping, and manual adjustment, when the schema is defined, then the point feature class includes stable point identifiers, parcel grouping, point order, coordinate fields, and review/edit flags.
5. Given parcel polygons are the parcel review surface in non-fabric mode, when the schema is defined, then the polygon feature class includes parcel identity, parcel grouping, status, area/perimeter support fields, and source traceability fields.
6. Given the output layer set should remain usable without Parcel Fabric, when the schema is implemented, then it supports standard ArcGIS Pro editing tools, labels, snapping, and optional COGO-style line labeling without depending on parcel records or fabric-only datasets.
7. Given the schema becomes the standard contract, when output generation, map loading, and downstream review tooling are updated later, then they use this field set consistently instead of adding ad hoc fields per transaction type.
8. Given this story is complete, when later stories implement or refine non-fabric output creation, then the schema document clearly distinguishes required fields, optional fields, labeling fields, and compatibility notes with the current output adapter.

## Tasks / Subtasks

- [ ] Define the authoritative non-fabric layer set. (AC: 1, 6-8)
  - [ ] Standardize the layer names for points, lines, polygons, and any optional issue/annotation support layers.
  - [ ] Decide which existing output names remain for backward compatibility and which need normalization.
  - [ ] Document how this non-fabric contract differs from Parcel Fabric mode.

- [ ] Define the shared case/workflow metadata fields. (AC: 2, 7-8)
  - [ ] Identify the base fields that should appear on all output layers.
  - [ ] Include transaction identity, workflow branch/stage, review state, and provenance/source fields.
  - [ ] Keep field names and text lengths practical for file geodatabase use.

- [ ] Define the point schema. (AC: 2, 4, 6)
  - [ ] Specify identifier, order, parcel-group, coordinate, status, and edit/provenance fields.
  - [ ] Define which fields drive labels and which fields are only operational metadata.

- [ ] Define the line schema. (AC: 2-3, 6)
  - [ ] Specify from/to point ids, segment order, parcel/traverse grouping, bearing, distance, radius, arc length, and line-type fields.
  - [ ] Identify which line fields should support COGO-style labeling and review.

- [ ] Define the polygon schema. (AC: 2, 5-6)
  - [ ] Specify parcel identity, grouping, area/perimeter support fields, review status, and provenance fields.
  - [ ] Clarify how multi-parcel source documents map into polygon records.

- [ ] Define labeling, editability, and compatibility rules. (AC: 6-8)
  - [ ] Document recommended point and line label expressions for ArcGIS Pro.
  - [ ] Document how standard snapping/editing should work in non-fabric mode.
  - [ ] Note the current output adapter gaps versus the target contract.

## Dev Notes

### Why This Story Exists

- The current `normal` output mode creates usable geometry, but the schema is still lightweight and not yet fully COGO-ready.
- The user wants a standard non-fabric path that still carries parcel editing value: point ids, ordered lines, distances, bearings, and clean reviewable parcel grouping.
- This story should define the contract first so later code work does not keep expanding the schema in ad hoc ways.

### Recommended Authoritative Layer Set

Use these four layer roles for the standard non-fabric output workspace:

#### Required

- `parcel_points`
- `parcel_lines`
- `parcel_polygons`

#### Optional

- `parcel_issues`

The current adapter already uses:

- `parcel_points`
- `parcel_lines`
- `parcel_polygon`

Recommendation:

- normalize the polygon layer name to `parcel_polygons`
- preserve a temporary compatibility path only if needed during migration

### Recommended Shared Base Fields

These fields should exist on **all** non-fabric output layers:

| Field | Type | Notes |
|---|---|---|
| `transaction_number` | Text(64) | Human-visible transaction number |
| `transaction_id` | Text(64) | Stable transaction/system id when available |
| `workflow_name` | Text(64) | Example: `parcel_workflow_compute` |
| `workflow_stage` | Text(64) | Example: `spatial_units_created` |
| `transaction_type` | Text(128) | Display transaction type/stage |
| `review_state` | Text(64) | Example: `draft`, `validated`, `approved`, `manual_edit` |
| `source_mode` | Text(64) | Example: `pdf_text_parse`, `ocr_parse`, `manual_review`, `csv_points` |
| `source_doc` | Text(256) | Primary evidence/source file name |
| `doc_type_id` | Text(64) | Document-type catalog id when available |
| `parcel_group_id` | Text(64) | Stable group/traverse/parcel key used during review |
| `row_id` | Text(64) | Source review row id when available |
| `is_manual` | Short Integer | `1` if manually added/edited |
| `is_edited` | Short Integer | `1` if changed after extraction |
| `status_txt` | Text(64) | Display/review status |
| `source_txt` | Text(1024) | Condensed source evidence / traceability |

### Recommended Point Feature Class Schema

Feature class name: `parcel_points`

Geometry:

- `POINT`
- projected coordinate system from configured workflow spatial reference

Fields:

| Field | Type | Required | Notes |
|---|---|---:|---|
| `point_id` | Text(64) | Yes | Visible point label and stable point key |
| `parcel_id` | Text(64) | Yes | Final parcel identifier for this point |
| `parcel_group_id` | Text(64) | Yes | Group key carried from extraction/review |
| `point_order` | Long | Yes | Ordered point sequence within parcel |
| `easting` | Double | Yes | Stored numeric X value |
| `northing` | Double | Yes | Stored numeric Y value |
| `point_role` | Text(32) | No | Corner, tie, control, start, close |
| `from_segment` | Long | No | Prior segment number if relevant |
| `status_txt` | Text(64) | Yes | Review status |
| `length_txt` | Text(64) | No | Preserved display length from source |
| `source_doc` | Text(256) | Yes | Primary source file |
| `doc_type_id` | Text(64) | No | Document type routing id |
| `row_id` | Text(64) | No | Extraction/review row link |
| `is_manual` | Short Integer | Yes | Manual add/edit flag |
| `is_edited` | Short Integer | Yes | User-edited flag |
| `source_txt` | Text(1024) | No | Evidence/reference text |

Recommended point label:

- label field: `point_id`

### Recommended Line Feature Class Schema

Feature class name: `parcel_lines`

Geometry:

- `POLYLINE`

Fields:

| Field | Type | Required | Notes |
|---|---|---:|---|
| `line_id` | Text(64) | Yes | Stable segment/line id |
| `parcel_id` | Text(64) | Yes | Final parcel identifier |
| `parcel_group_id` | Text(64) | Yes | Group key carried from review |
| `traverse_id` | Text(64) | No | Traverse/group id when distinct from parcel id |
| `segment_order` | Long | Yes | Ordered segment sequence within parcel |
| `from_point_id` | Text(64) | Yes | Start point id |
| `to_point_id` | Text(64) | Yes | End point id |
| `line_type` | Text(32) | Yes | `line`, `curve`, `tie`, `closure` |
| `bearing_txt` | Text(64) | No | Source/display bearing |
| `distance_m` | Double | No | Numeric line distance in meters |
| `distance_txt` | Text(64) | No | Preserved source/display distance |
| `radius_m` | Double | No | Curve radius |
| `arc_length_m` | Double | No | Curve arc length |
| `delta_angle_txt` | Text(64) | No | Optional delta/curve text |
| `chord_bearing_txt` | Text(64) | No | Optional curve chord bearing |
| `chord_distance_m` | Double | No | Optional curve chord distance |
| `is_boundary_break` | Short Integer | No | Boundary split hint from extraction |
| `status_txt` | Text(64) | Yes | Review status |
| `source_doc` | Text(256) | Yes | Primary source file |
| `doc_type_id` | Text(64) | No | Document type routing id |
| `row_id` | Text(64) | No | Extraction/review row link |
| `is_manual` | Short Integer | Yes | Manual add/edit flag |
| `is_edited` | Short Integer | Yes | User-edited flag |
| `source_txt` | Text(1024) | No | Evidence/reference text |

Recommended line label expression:

- default review label: `bearing_txt + ' ' + distance_txt`
- fallback if numeric-only workflows are needed: format from `distance_m`

This is the core of “COGO-ready non-fabric”: the lines carry the bearing/distance contract even if Parcel Fabric is not present.

### Recommended Polygon Feature Class Schema

Feature class name: `parcel_polygons`

Geometry:

- `POLYGON`

Fields:

| Field | Type | Required | Notes |
|---|---|---:|---|
| `parcel_id` | Text(64) | Yes | Final parcel identifier |
| `parcel_name` | Text(128) | Yes | Display parcel name/number |
| `parcel_group_id` | Text(64) | Yes | Group key from extraction/review |
| `polygon_order` | Long | No | Useful when multiple polygons exist |
| `point_count` | Long | No | Number of points used to build polygon |
| `perimeter_m` | Double | No | Derived perimeter value |
| `area_sq_m` | Double | No | Derived area value |
| `closure_status` | Text(64) | No | `closed`, `open`, `warning` |
| `status_txt` | Text(64) | Yes | Review/output status |
| `source_doc` | Text(256) | Yes | Primary source file |
| `doc_type_id` | Text(64) | No | Document type routing id |
| `is_manual` | Short Integer | Yes | Manual add/edit flag |
| `is_edited` | Short Integer | Yes | User-edited flag |
| `source_txt` | Text(1024) | No | Evidence/reference text |

### Optional Issues Layer

Feature class name: `parcel_issues`

Purpose:

- keep unresolved geometry/review issues visible in the map even in non-fabric mode

Suggested fields:

| Field | Type | Notes |
|---|---|---|
| `issue_id` | Text(64) | Stable issue identifier |
| `parcel_id` | Text(64) | Parcel tied to issue |
| `issue_code` | Text(64) | Duplicate point, closure break, missing bearing, etc. |
| `severity` | Text(32) | `error`, `warning`, `info` |
| `issue_txt` | Text(1024) | Human-readable description |
| `resolved_flag` | Short Integer | `1` resolved, `0` unresolved |

### Non-Fabric COGO-Ready Behavior

This layer set should support:

- point labeling by `point_id`
- line labeling by bearing/distance
- standard ArcGIS Pro snapping
- manual point/line geometry edits
- parcel-by-parcel map review
- downstream generation of reviewer-facing parcel geometry without requiring parcel records

This layer set does **not** provide:

- Parcel Fabric records
- active-record behavior
- Build Parcels tools
- parcel-type rules
- fabric topology workflows

### Recommended Compatibility Notes Against Current Adapter

The current `normal` output path already provides a partial base:

- `parcel_points`
  - `point_id`
  - `parcel_grp`
  - `status_txt`
  - `length_txt`
  - `source_txt`
  - `row_id`

- `parcel_lines`
  - `start_pt`
  - `end_pt`
  - `parcel_grp`
  - `length_txt`
  - `seg_index`

- `parcel_polygon`
  - `name`
  - `parcel_grp`

Main gaps to close in future implementation:

1. standardize polygon naming to `parcel_polygons`
2. add numeric `easting` / `northing` fields to points
3. add `parcel_id` / final parcel identity fields across all layers
4. add bearing/distance/radius/arc fields to lines
5. add workflow/source/document routing metadata consistently
6. add optional issues layer for map-visible blockers

### Suggested Areas

- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/`

### References

- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/5-18-route-manual-review-branch-into-configured-gdb-map-editing-path.md`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-22 | 0.1 | Drafted the authoritative COGO-ready non-fabric output schema contract for future implementation. | Codex |
