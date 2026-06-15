---
baseline_commit: handoff-2026-06-14
---

# Story 5.8: Implement True Local Parcel Fabric Output Mode

Status: review

## Story

As a cadastral examiner using the Sidwell Co ArcGIS Pro add-in,  
I want the approved transaction output to generate a true local Parcel Fabric review workspace inside the transaction geodatabase,  
so that I can review and refine parcel geometry with ArcGIS Pro parcel tools, records, topology-aware parcel behavior, and map-centric editing before downstream sync.

## Acceptance Criteria

1. Given `review_workspace_mode` is configured as `parcel_fabric`, when output generation is started after validation has passed, then the workflow creates a true local Parcel Fabric-enabled review workspace inside the transaction `.gdb` instead of only creating plain pilot feature classes.
2. Given Parcel Fabric mode is active, when the output adapter runs, then Parcel Fabric creation is performed through supported ArcGIS geoprocessing and/or ArcPy parcel-fabric creation tools appropriate for ArcGIS Pro 3.6/3.7 local geodatabases.
3. Given the Parcel Fabric review workspace is being created, when initialization succeeds, then the transaction `.gdb` contains the required parcel fabric structure, including the fabric dataset/layer context needed for parcel editing rather than only loose point/line/polygon feature classes.
4. Given a transaction reaches output generation in Parcel Fabric mode, when the workflow prepares the review context, then it creates or sets a parcel record and active record context suitable for examiner review operations in the local fabric workspace.
5. Given approved review data has already produced normalized points, lines, and polygon candidates, when Parcel Fabric mode runs, then the workflow imports or copies the generated parcel lines and points into the correct parcel fabric structures or parcel types needed to build parcels.
6. Given parcel lines and points have been imported into the Parcel Fabric review workspace, when the build step completes, then parcel polygons are built in the fabric and are spatially reviewable in ArcGIS Pro.
7. Given local Parcel Fabric output generation succeeds, when the add-in loads the review context into the active map, then it adds the parcel fabric layer or parcel-aware review layers, zooms to the resulting parcel extent, and leaves the examiner in a usable review/editing state.
8. Given Parcel Fabric mode is selected, when output generation fails at fabric creation, record setup, import, or build, then the workflow writes a clear failure message, preserves logs/artifacts for diagnosis, and does not silently fall back to plain feature-class mode.
9. Given the app still supports the existing standard output flow, when `review_workspace_mode` is `normal`, then the current standard `.gdb` output path remains unchanged and backward compatible.
10. Given the fabric review workspace is intended for local examination, when the story is complete, then documentation clearly distinguishes this local Parcel Fabric review mode from future enterprise sync/system-of-record fabric behavior.

## Tasks / Subtasks

- [x] Define the true local Parcel Fabric output contract. (AC: 1-3, 10)
  - [x] Specify the exact local geodatabase/fabric structure to create for a transaction case.
  - [x] Document which ArcGIS Pro geoprocessing or ArcPy parcel-fabric tools will be used for fabric creation in ArcGIS Pro 3.6/3.7.
  - [x] Define the required output summary fields for true Parcel Fabric mode so downstream UI/reporting can distinguish it from the current pilot dataset.

- [x] Implement geoprocessing-based Parcel Fabric creation. (AC: 1-3, 8-9)
  - [x] Update the Python output adapter to create a real Parcel Fabric review workspace rather than only the current `parcel_fabric_review` pilot dataset.
  - [x] Ensure the adapter logs every major step: fabric creation, record setup, line/point import, parcel build, and load/zoom handoff.
  - [x] Preserve the existing `normal` mode path unchanged.

- [x] Add record and active-record setup. (AC: 4, 8)
  - [x] Define how the transaction number, transaction type, and output run map to the local parcel record created for review.
  - [x] Create/set the active record context needed for parcel editing/build tools to behave correctly during review.

- [x] Import approved geometry into parcel types. (AC: 5-6, 8)
  - [x] Map generated review/output geometry into the parcel fabric line/point structures required by the chosen parcel type.
  - [x] Build parcel polygons from the imported geometry.
  - [x] Record feature counts, dataset paths, and build results in the output summary.

- [x] Load and zoom the Parcel Fabric review context in ArcGIS Pro. (AC: 7-8)
  - [x] Update map integration so the add-in loads the parcel fabric layer or the correct parcel-aware review layer set.
  - [x] Zoom to the parcel extent after successful output generation.
  - [x] Provide a clear user-facing status message that the review workspace is ready for parcel editing tools.

- [x] Harden diagnostics, fallback behavior, and documentation. (AC: 8-10)
  - [x] Ensure output/log artifacts clearly show whether the run was `normal`, `parcel_fabric_pilot`, or `parcel_fabric_true`.
  - [x] Fail explicitly when ArcGIS licensing/tooling prerequisites for fabric creation are missing.
  - [x] Document how this local fabric review workspace relates to future enterprise sync without implying that enterprise parcel fabric sync is already implemented.

## Dev Notes

### Why This Story Exists

- Story 5.7 established an optional Parcel Fabric pilot mode, but the current implementation is intentionally only a parcel-oriented review dataset and not a true Esri Parcel Fabric.
- The team now wants a dedicated implementation story for the real local Parcel Fabric path so examiners can leverage parcel editing tools in a stronger, map-first review workflow.
- This story should convert the current pilot concept into a true local review-mode implementation without breaking the standard output path.

### Current State To Preserve

- `review_workspace_mode = normal` already works and must remain the safe default.
- `review_workspace_mode = parcel_fabric` currently produces a pilot review dataset, not a real Parcel Fabric.
- Output generation, output summary, stage progression, and map loading already exist and should be extended rather than reinvented.

### Developer Guardrails

- Do not silently relabel the current pilot dataset as a true Parcel Fabric. The implementation must create the real fabric-backed review context or clearly fail.
- Do not break the existing standard output mode while adding the true Parcel Fabric path.
- Keep the local transaction `.gdb` as the case-scoped workspace. This story is about a local review workspace, not enterprise registration or system-of-record migration.
- Preserve the case-folder artifact and logging model already used by outputs so recoverability and diagnostics remain consistent.

### Suggested Technical Focus

Focus the implementation around these concrete steps:

1. create/open the transaction-local `.gdb`
2. create the parcel fabric structure using ArcGIS-supported tooling
3. create or assign the parcel record / active record context
4. import approved review output points and lines into the fabric structures / parcel type
5. build parcels
6. add the parcel fabric review layer(s) to the active map
7. zoom to the parcel review extent
8. write deterministic output/log metadata

### Recommended Output Summary Extensions

The final implementation should likely record fields along these lines in `output_summary.json`:

- `review_workspace_mode`
- `parcel_fabric_mode`
- `parcel_fabric_dataset_path`
- `parcel_fabric_layer_name`
- `parcel_record_name`
- `parcel_record_id` if available
- `parcel_type`
- `built_parcel_count`
- `built_line_count`
- `built_point_count`
- `map_load_status`

### Failure Handling Expectations

If true fabric creation fails, the workflow should:

- leave the existing standard output mode untouched
- write the Python/stdout/stderr execution details to `output/logs/process.log`
- show a clear user-facing failure message describing which step failed
- avoid pretending the transaction is review-ready in parcel mode

### References

- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-7-evaluate-parcel-fabric-review-workspace-pilot.md`
- `_bmad-output/planning-artifacts/research/parcel-fabric-review-workspace-pilot-2026-06-14.md`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Parcel-Fabric.html`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `$env:PYTHONPATH='src\\ProcessingTools'; python -m unittest discover -s src\\ProcessingTools\\tests -p test_output_adapter.py`
- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.sln /nodeReuse:false`
- `dotnet run --project src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.Tests\\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes

- Replaced the prior Parcel Fabric pilot-only adapter branch with a true local Parcel Fabric output path when `review_workspace_mode` is `parcel_fabric`.
- Implemented a geoprocessing/ArcPy flow that creates a local feature dataset, creates the parcel fabric, adds a parcel type, appends the approved polygon candidate, creates a transaction-scoped parcel record, builds the parcel fabric, imports approved points, and validates the resulting fabric.
- Extended `output_summary.json` so the add-in can distinguish `parcel_fabric_true` output metadata from the prior pilot-style branch, including parcel fabric dataset/layer paths, parcel record name, parcel type, and built feature counts.
- Updated map-loading and output-preview messaging so the UI now describes a true Parcel Fabric review workspace instead of always saying `pilot`.
- Added filesystem-fallback test-mode behavior that simulates the true Parcel Fabric workspace contract for automated tests.

### File List

- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

### Change Log

- 2026-06-14: Implemented the true local Parcel Fabric output mode, extended output summary metadata, updated map/status messaging, and added test coverage for the new fabric contract.
