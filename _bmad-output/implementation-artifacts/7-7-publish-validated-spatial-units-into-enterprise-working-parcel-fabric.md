---
baseline_commit: handoff-2026-06-23
---

# Story 7.7: Publish Validated Spatial Units Into Enterprise Working Parcel Fabric

Status: ready for review

## Story

As a cadastral examiner working in a distributed environment,  
I want validated parcel geometry published into a shared working Parcel Fabric and opened in ArcGIS Pro for map-based review,  
so that I can use parcel-aware editing, COGO, snapping, and review tools on the transaction after point validation is complete.

## Acceptance Criteria

1. Given a transaction has completed `Validate Points` and the configured workspace mode is `enterprise_parcel_fabric`, when `Create Spatial Units` runs, then the add-in publishes the validated points, lines, and parcel grouping into the configured working Parcel Fabric using the standardized contract and transaction scoping rules.
2. Given Parcel Fabric publication requires record and parcel-type context, when the working-fabric branch runs, then it creates or resolves the working record, targets the configured parcel type, copies or imports the validated geometry into the required structures, and triggers the required build or equivalent parcel-generation step.
3. Given the working-fabric path is meant for review, not final promotion, when publication succeeds, then the add-in loads the working Parcel Fabric map context, zooms to the transaction extent, and keeps the local transaction artifacts available for audit, resume, and fallback.
4. Given reviewer usability still depends on readable labels and traceability, when the map context is loaded, then the add-in can optionally load the local labeled review overlays above the working fabric so point ids, line COGO labels, and parcel review context remain visible.
5. Given publication or build can fail for service, schema, edit-session, or Parcel Fabric reasons, when failure occurs, then the workflow preserves local artifacts, records a clear diagnostic message, and does not falsely mark the transaction as ready for final promotion.
6. Given the working-fabric review is later approved, when downstream promotion stories run, then they can treat the working Parcel Fabric as the collaborative review surface while still applying separate final authoritative completion rules.

## Tasks / Subtasks

- [x] Add the Enterprise Parcel Fabric runtime branch to `Create Spatial Units`. (AC: 1-3, 5)
  - [x] Resolve the new `enterprise_parcel_fabric` mode in settings/runtime selection.
  - [x] Keep the existing `normal`, `parcel_fabric_local`, and `enterprise_working_layers` branches intact.
  - [x] Ensure the local validated review snapshot remains the source input for publication.

- [x] Implement transaction-scoped working record and parcel-type handling. (AC: 2, 5)
  - [x] Create or resolve the working record name from the configured pattern.
  - [x] Apply transaction identity and review-state metadata needed for later restore/final-review steps.
  - [x] Resolve the configured parcel type for compute-review publication.

- [x] Publish validated geometry into the working Parcel Fabric. (AC: 1-2, 5)
  - [x] Map validated point/line/parcel grouping data into the fabric import/copy flow.
  - [x] Trigger the required parcel build or equivalent operation.
  - [x] Record local audit/log artifacts describing what was published and built.

- [x] Load the ArcGIS Pro review context for the examiner. (AC: 3-4)
  - [x] Add the working Parcel Fabric layer to the active map and zoom to the transaction extent.
  - [x] Optionally load the local labeled review overlays above the fabric layer.
  - [x] Show clear final-review guidance describing that the map is now the editing surface.

- [x] Handle failures and preserve recovery paths. (AC: 5-6)
  - [x] Keep local artifacts intact on publish/build failure.
  - [x] Record clear status messages and output summaries for partial success vs total failure.
  - [x] Avoid incorrectly unlocking final promotion/finalize when the working-fabric step has not truly succeeded.

## Dev Notes

### Why This Story Exists

- Story 5.8 gave us a **local Parcel Fabric** branch inside the transaction `.gdb`.
- Story 7.2 gave us **Enterprise working-layer** publication for generic shared feature layers.
- The next missing step is the **shared Enterprise Parcel Fabric** path for distributed parcel-aware review.

This story should be treated as the **working review** branch, not the final promotion branch.

### Recommended Runtime Position

This branch belongs in the current workflow at:

- `Attachments`
- `Data Extraction`
- `Validate Points`
- `Create Spatial Units` -> publish into working Parcel Fabric
- `Final Review` -> examiner edits/inspects in ArcGIS Pro
- `Finalize` -> later completion/promotion behavior

That keeps the Points Validation Tool as the last document-centric stage and moves spatial editing fully into ArcGIS Pro.

### Recommended Source of Truth

Do not publish directly from ad hoc in-memory UI state.

Use:

- saved validated review data (`approved_review.json` / validated review snapshot)
- local output adapter inputs
- local process log / output summary

The Enterprise Parcel Fabric should be treated as:

- a collaborative review surface
- not the only recoverability mechanism
- not the authoritative completion target yet

### Recommended Publish Flow

High-level flow:

1. Validate runtime mode = `enterprise_parcel_fabric`
2. Resolve settings / service targets
3. Read validated review snapshot
4. Create or resolve working record
5. Resolve target parcel type
6. Create/import/copy required points and lines
7. Build parcels
8. Write local output summary / diagnostics
9. Load the map review context
10. Move workflow into `Final Review` / map-review pending state

### Recommended Map Loading Behavior

To stay aligned with current add-in behavior:

- load the working Parcel Fabric base layer
- optionally load local `parcel_polygons`, `parcel_lines`, `parcel_points` overlays above it
- preserve readable labels for:
  - point ids
  - bearing/distance line labels
  - parcel names/numbers

This mirrors the current local Parcel Fabric usability fix already added in the repo and avoids depending on native fabric child-layer labeling alone.

### Failure Handling

Failures should preserve:

- local validated review artifacts
- local geometry outputs where already created
- clear status text in the Parcel Workflow pane
- detailed diagnostics in `process.log` / output summaries

Failures should not:

- silently fall back to some other mode
- mark `Create Spatial Units` as succeeded
- unlock final promotion or finalize

### Suggested Settings / Status Effects

When this story is later implemented, expect:

- `review_workspace_mode = enterprise_parcel_fabric`
- output summary to record:
  - working fabric target
  - working record name
  - parcel type name
  - publish/build status
  - overlay-load status

Suggested workflow messages:

- success: "Working Parcel Fabric review context is ready. Continue in Final Review using ArcGIS Pro map tools."
- failure: "Working Parcel Fabric publication failed. Local validated artifacts were preserved; review logs before retrying."

### Dependencies / Sequencing

Depends on:

- Story 7.6 contract/configuration
- Story 5.8 local Parcel Fabric runtime patterns
- Story 7.2 enterprise publish/service patterns
- current `Create Spatial Units` and `Final Review` stage flow

Should precede:

- any story that treats Enterprise Parcel Fabric as the primary collaborative review surface
- any story that promotes working fabric changes into authoritative cadastral systems

### Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/`

### References

- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/7-2-publish-approved-review-geometry-to-enterprise-working-layers.md`
- `_bmad-output/implementation-artifacts/7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `https://developers.arcgis.com/rest/services-reference/enterprise/parcel-fabric-service/`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-23 | 0.1 | Drafted the Enterprise working Parcel Fabric publication story aligned to current `Create Spatial Units -> Final Review` workflow behavior. | Codex |
| 2026-06-23 | 1.0 | Implemented the enterprise Parcel Fabric publish branch, runtime settings seam, map-load target selection, and regression coverage for output-stage and final-review publication flows. | Codex |
