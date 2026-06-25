---
baseline_commit: handoff-2026-06-24
---

# Story 5.22: Group Transaction Review Layers And Ground COGO Diagnostics In Final Feature Class State

Status: review

## Story

As a cadastral examiner reviewing generated spatial outputs in ArcGIS Pro,  
I want each transaction's review layers grouped together and the Final Review diagnostics to reflect the actual written feature classes,  
so that the map stays organized and I can trust that COGO-bearing and distance messages describe the real geodatabase output rather than only an intermediate payload.

## Acceptance Criteria

1. Given Create Spatial Units succeeds for any output mode that loads map layers, when the add-in adds review layers to the active map, then those layers are placed inside a group layer named with the transaction number such as `TR 100000236 - Review`.
2. Given a transaction is rerun or its outputs are reloaded, when the group layer already exists for that transaction, then the add-in reuses or refreshes that transaction group instead of scattering duplicate root layers across the Contents pane.
3. Given the Python output adapter writes `parcel_points`, `parcel_lines`, and `parcel_polygons`, when output creation finishes, then the workflow records a post-write schema diagnostic that confirms whether configured COGO fields actually exist in the final feature classes.
4. Given `parcel_lines` is expected to carry optional COGO enrichment, when post-write diagnostics run, then the workflow records whether `bearing_txt`, `distance_txt`, `length_txt`, and `distance_m` exist and how many rows in the final feature class contain values.
5. Given the Final Review card shows COGO diagnostics, when the user inspects that status, then the message is driven by the actual feature class state after write and not only by the pre-write or GeoJSON payload summary.
6. Given the GeoJSON payload and the feature class schema diverge, when diagnostics are shown, then the workflow surfaces a reviewer-friendly warning that output payload enrichment and final geodatabase schema are out of sync.
7. Given Parcel Fabric mode is active, when diagnostics are shown, then the workflow clearly distinguishes between root output feature classes such as `parcel_lines` and review/fabric review layers such as `compute_review_Lines`.
8. Given this story is complete, when a user says "bearing text populated yes but I do not see the field," then the add-in provides enough grouped-layer and schema evidence to explain whether the issue is generation, write-through, or map refresh.

## Tasks / Subtasks

- [x] Add transaction-scoped layer grouping to output map integration. (AC: 1-2, 7)
  - [x] Introduce a predictable group-layer naming convention based on transaction number and review purpose.
  - [x] Place all loaded output review layers into that group for both non-fabric and parcel-fabric review paths.
  - [x] Refresh or replace prior layers within the transaction group on rerun instead of adding duplicate root layers.

- [x] Add post-write schema diagnostics to the output flow. (AC: 3-4, 6-8)
  - [x] Inspect the final written feature classes after ArcPy output generation completes.
  - [x] Record whether the expected optional COGO fields exist on `parcel_lines`.
  - [x] Record populated-row counts for those fields from the actual feature class, not just the in-memory segment payload.

- [x] Update Final Review diagnostics to use feature-class-grounded evidence. (AC: 5-8)
  - [x] Distinguish payload enrichment from feature class enrichment in the summary model.
  - [x] Show a compact reviewer-facing diagnostic line derived from the final feature class inspection.
  - [x] Surface a warning when payload says populated but the final feature class schema or row population does not match.

- [x] Clarify parcel fabric versus root output reporting. (AC: 6-7)
  - [x] Explicitly report whether the loaded map surface is root non-fabric outputs, root feature classes plus fabric review layers, or fabric review layers only.
  - [x] Keep reviewer messaging plain enough to explain why `extracted_geometry.geojson` and the visible map layer may differ.

- [x] Add focused verification coverage. (AC: 1-8)
  - [x] Cover transaction group-layer naming and rerun behavior.
  - [x] Cover diagnostic output when root feature classes contain the expected fields.
  - [x] Cover diagnostic output when payload enrichment succeeds but final feature class schema or row population does not.

## Dev Notes

### Why This Story Exists

- Story 5.20 improved non-fabric map styling and labeling behavior.
- Story 5.21 added optional COGO attributes and label inputs to the output generation path.
- Recent testing showed a trust gap: `output_summary.json` and `extracted_geometry.geojson` can report populated COGO values while the user still does not see those fields in the ArcGIS Pro layer schema or Fields view.
- The Contents pane also becomes cluttered because review layers are currently added at the root instead of under a transaction group.

This story closes both usability gaps:

1. **layer organization**
2. **diagnostic truthfulness**

### Scope Boundary

This story should improve:

- output layer grouping in the Contents pane
- post-write schema inspection
- Final Review diagnostics credibility
- reviewer understanding of payload vs written feature class state

It should **not** implement:

- a new extraction method
- new COGO math rules
- enterprise publication changes
- a redesign of the map review workflow stages

### Expected Diagnostic Contract

For the root `parcel_lines` feature class, diagnostics should confirm:

- whether field exists:
  - `bearing_txt`
  - `distance_txt`
  - `length_txt`
  - `distance_m`
- how many rows have non-empty values in:
  - `bearing_txt`
  - `distance_txt`
  - `length_txt`
- how many rows have non-null values in:
  - `distance_m`

Optional extension:

- include whether labels were actually applied to the current loaded line layer

### Suggested Reviewer-Facing Message Shape

Keep the Final Review card compact and direct, for example:

- `COGO diagnostics: root parcel_lines fields present (bearing_txt, distance_txt, length_txt, distance_m); populated rows bearing 76, distance 76; map load fabric review.`

When mismatched:

- `COGO diagnostics: payload reported bearing/distance values, but root parcel_lines is missing bearing_txt and distance_txt. Reload layers or review output schema.`

### Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/OutputMapReviewStylingTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

### Design Direction

- Group layers under the transaction so the reviewer can collapse or remove one case cleanly.
- Prefer diagnostics grounded in the actual feature class over optimistic pre-write payload summaries.
- Keep the GeoJSON summary because it remains useful for debugging the adapter itself, but do not present it as the final reviewer truth source when the question is "what fields do I have in ArcGIS Pro right now?"

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-24 | 0.1 | Drafted enhancement story for transaction-scoped review layer grouping and feature-class-grounded COGO diagnostics. | Codex |
| 2026-06-24 | 1.0 | Implemented transaction review layer grouping, post-write line-schema diagnostics, and Final Review messaging grounded in written feature-class state. | Codex |

## Dev Agent Record

### Completion Notes

- Added transaction-scoped review group layer naming (`TR ###### - Review`) and changed output layer loading to refresh matching layers into that group instead of leaving duplicate root layers in the map.
- Added post-write line feature-class diagnostics in the Python output adapter for both real ArcPy outputs and JSON fallback outputs, covering `bearing_txt`, `distance_txt`, `length_txt`, and `distance_m`.
- Extended `output_summary.json` and the C# summary model to carry root/review line diagnostics plus payload-versus-feature-class mismatch warnings.
- Updated Final Review and output-preview diagnostic text to report root feature-class field presence and populated-row counts instead of only the pre-write payload counts.
- Added focused test coverage for transaction group naming and feature-class-grounded diagnostics, and updated the local test harness to include those checks.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.sln`
- `python -m unittest tests.test_output_adapter` (run from `src\\ProcessingTools`)

### File List

- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/OutputMapReviewStylingTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
