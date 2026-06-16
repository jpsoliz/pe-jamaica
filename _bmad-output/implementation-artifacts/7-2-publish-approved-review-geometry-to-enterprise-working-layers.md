---
baseline_commit: handoff-2026-06-15
---

# Story 7.2: Publish Approved Review Geometry To Enterprise Working Layers

Status: review

## Story

As a cadastral examiner,  
I want approved review geometry published into Enterprise working layers,  
so that I and other authorized reviewers can inspect the completed transaction state in a shared spatial workspace without exposing partial in-progress edits.

## Acceptance Criteria

1. Given review data has been approved for a loaded transaction, when the configured publish timing is `on_complete` and the user performs final Approve, then the add-in writes the current points, lines, and polygons into the configured working layers using the transaction identity as the key.
2. Given a deployment explicitly selects `on_outputs`, when output generation completes, then the add-in may publish the same approved transaction-scoped geometry earlier while preserving the same replace/update rules.
3. Given working features already exist for the same active transaction, when a new publish occurs, then the add-in updates or replaces those features according to the configured ownership and overwrite rules rather than duplicating them blindly.
4. Given Enterprise working features are written, when the publish succeeds, then the working features include transaction metadata, workflow name, workflow stage, assigned user, and last-saved timestamp.
5. Given the Enterprise publish fails, when the add-in reports the error, then local Case Folder artifacts remain intact and the workflow does not lose approved review data or local output artifacts.
6. Given the user needs operational confidence, when the publish completes, then the add-in shows what was published and what remains local-only.

## Tasks / Subtasks

- [x] Implement working-layer publish orchestration. (AC: 1-3)
  - [x] Resolve target working layers from configuration.
  - [x] Map approved review outputs into enterprise points/lines/polygons features.
  - [x] Persist shared transaction/stage metadata on published features.

- [x] Add replace/update rules for active transaction ownership. (AC: 2)
  - [x] Define idempotent publish behavior for the same transaction.
  - [x] Prevent duplicate working geometry when a case is re-published.

- [x] Preserve local resilience and user feedback. (AC: 4-5)
  - [x] Keep local review/output artifacts untouched on Enterprise failure.
  - [x] Add clear publish success/failure status messaging and audit hooks.

## Dev Agent Record

### Completion Notes

- Added `IEnterpriseWorkingLayerPublishService` plus `JsonEnterpriseWorkingLayerPublishService` to publish transaction-scoped enterprise working-layer snapshots for points, lines, and polygons.
- Implemented explicit publish timing support so Enterprise publish defaults to `on_complete` and can optionally run at `on_outputs` for deployments that want earlier shared visibility.
- Wired Enterprise publish into final completion flow by default, so local outputs remain private during in-progress editing and enterprise publish failures do not roll back local case state.
- Extended `output_summary.json` with `enterprise_working_publish` metadata and added a dedicated `enterprise_working_publish.json` artifact for operator visibility.
- Preserved transaction-scoped replace behavior on re-publish and added tests covering success, replace semantics, and failure containment.
- Also patched 7.1A runtime normalization so `enterprise_working_layers` survives workflow execution settings loading.

## Dev Notes

### Architectural Direction

- Enterprise working layers are the shared visibility surface for completed review state.
- Local artifacts remain the execution and recovery backbone.
- Publish must be idempotent per transaction and safe to retry.
- The default visibility boundary is local-first: Enterprise publish occurs on final completion unless configuration explicitly opts into earlier publish timing.

### Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/`
- `src/ProcessingTools/adapters/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
