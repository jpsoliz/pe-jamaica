---
baseline_commit: handoff-2026-06-23
course_correction_date: 2026-06-30
---

# Story 7.7: Publish Validated Spatial Units Into Enterprise Generic Working Layers

Status: ready-for-dev

## Story

As a cadastral examiner working in a distributed environment,  
I want validated parcel geometry published into shared Enterprise working layers,  
so that points, lines, parcels, transaction status, and the examiner's Compute review decision can be reviewed in ArcGIS Enterprise without treating the working layer as the final authoritative cadastral system.

## Course Correction

This story was previously drafted around `enterprise_parcel_fabric`. The current product decision is to proceed with **Enterprise generic working layers** first:

- working points
- working lines
- working polygons/parcels
- working case index
- optional working issues/review notes

Enterprise Parcel Fabric remains a future/deferred path. The active implementation target for this story is `review_workspace_mode = enterprise_working_layers`.

## Latest Review Adjustment

Compute is a document-geometry review stage, not final cadastral production.

The goal of Compute is to review the submitted transaction documents (`.pdf`, `.csv`, `.txt`, `.dwg`, and related attachments), validate whether the derived geometry is correct, and publish a temporary transaction-scoped working parcel into Enterprise working layers for comparison against official reference data such as CADMAP and CADINDEX.

After `Final Review`, the examiner must be able to record a Compute review disposition:

- `approved`
- `rejected`
- `postponed`

All dispositions must update the Enterprise working points, lines, polygons, and case index with the decision metadata. Approval means the submitted geometry passed Compute review; it does **not** mean the geometry has been promoted into the authoritative cadastre.

## Acceptance Criteria

1. Given a transaction has completed `Validate Points` and `Create Spatial Units`, when `review_workspace_mode = enterprise_working_layers` and publish timing is satisfied, then the add-in publishes the validated spatial units into configured Enterprise working point, line, polygon, and case-index targets.
2. Given Enterprise working features already exist for the same transaction scope, when publish runs again, then the add-in replaces or updates only the active transaction-scoped working rows instead of duplicating geometry.
3. Given Innola transaction context is available, when working features are written, then every point, line, polygon, and case-index row includes transaction metadata needed to link the Enterprise geometry back to Innola.
4. Given the case-index target is configured, when publish succeeds, then the add-in writes or updates one case-index row for the transaction with owner, status, stage, publish timestamp, output run id, and review readiness.
5. Given reviewer usability depends on labels and traceability, when the working layer map context is loaded, then the add-in can load the Enterprise working layers and preserve point ids, bearing/distance line labels, parcel labels, and transaction-scoped filtering.
6. Given Enterprise publish can fail because of service, auth, schema, or network issues, when failure occurs, then local artifacts remain intact, the workflow records a clear diagnostic artifact, and Final Review disposition actions are not allowed to record a successful Enterprise decision from a failed publish.
7. Given the examiner completes `Final Review`, when they choose `Approve`, `Reject`, or `Postpone`, then the add-in records the selected Compute review disposition in the Enterprise working layers and case index with operator, timestamp, comment/reason when supplied, and current output/publish references.
8. Given Final Review disposition is recorded, when completion/readiness is evaluated, then readiness confirms the latest transaction-scoped working publish exists, is linked to the current output summary, and carries the current review disposition.
9. Given the examiner rejects or postpones the transaction, when the decision is recorded, then the temporary geometry remains available in Enterprise working layers for traceability, comparison, and follow-up rather than being deleted or treated as a successful cadastral result.
10. Given authoritative promotion remains separate, when this story is complete, then the working layer is clearly described as temporary/review state, not the final authoritative cadastral layer.

## Tasks / Subtasks

- [x] Retarget Story 7.7 runtime mode to generic Enterprise working layers. (AC: 1, 8)
  - [x] Use `enterprise_working_layers` as the active mode for this story.
  - [x] Keep `enterprise_parcel_fabric` code/configuration separate and deferred.
  - [x] Ensure current local modes remain unaffected.

- [x] Define and enforce required working targets. (AC: 1, 4, 6)
  - [x] Require configured targets for points, lines, polygons, and case index.
  - [x] Keep issues/review notes optional but supported when configured.
  - [x] Surface missing target/schema messages in Settings and workflow status.

- [x] Publish validated spatial unit geometry into transaction-scoped Enterprise working layers. (AC: 1-3, 6)
  - [x] Publish point rows from the deduped output `parcel_points` representation.
  - [x] Publish line rows from the deduped output `parcel_lines` representation with COGO label fields.
  - [x] Publish polygon rows from `parcel_polygons`.
  - [x] Use `transaction_number` as the default replace scope and preserve `transaction_id` as the system identifier.
  - [x] Replace active transaction rows on retry/reprocess.

- [x] Write and update the working case index. (AC: 3-4, 7)
  - [x] Write one row per transaction.
  - [x] Include Innola task/user/group context, workflow stage, review state, status, output run id, and last publish metadata.
  - [x] Record whether the working geometry is current with the latest output summary.

- [x] Load the Enterprise working review context into ArcGIS Pro. (AC: 5)
  - [x] Add working polygons, lines, and points to the active map.
  - [x] Apply transaction-scoped filtering or definition queries.
  - [x] Apply point, line, and polygon labeling consistent with local review layers.
  - [x] Zoom to the transaction extent.

- [x] Gate Finalize readiness on working publish evidence. (AC: 6-8)
  - [x] Preserve local output and review artifacts as source of truth.
  - [x] Add readiness evidence from `enterprise_working_publish.json` / output summary.
  - [x] Block Finalize when Enterprise working publish failed, is stale, or is missing for Enterprise working-layer mode.
  - [x] Keep authoritative promotion out of this story.

- [ ] Add Final Review disposition recording for Compute. (AC: 7-10)
  - [ ] Replace the single final completion assumption with explicit `Approve`, `Reject`, and `Postpone` disposition paths.
  - [ ] Require current Enterprise working publish evidence before any disposition is written.
  - [ ] Record disposition metadata to points, lines, polygons, and case index.
  - [ ] Preserve the temporary working geometry for rejected and postponed cases.
  - [ ] Keep authoritative CADMAP/CADINDEX edits and cadastral promotion out of this story.

- [ ] Update transaction completion/readiness behavior for Compute decisions. (AC: 7-10)
  - [ ] Treat `approved`, `rejected`, and `postponed` as valid Compute outcomes, each with its own status text and audit trail.
  - [ ] Allow Innola/task closeout behavior to be driven by the recorded Compute disposition rather than assuming every Final Review action is an approval.
  - [ ] Block completion when Enterprise disposition writeback fails, is stale, or is missing.
  - [ ] Ensure the UI copy clearly says Compute approval is a document-geometry review outcome, not authoritative cadastral promotion.

- [x] Add tests and diagnostics. (AC: 1-8)
  - [x] Test required target validation for points, lines, polygons, and case index.
  - [x] Test transaction-scoped replace semantics.
  - [x] Test metadata fields on all working layer rows.
  - [x] Test case-index row creation/update.
  - [x] Test failure containment and Finalize readiness blocking.
  - [x] Test map-layer path selection and transaction-scoped review loading where feasible.

### Review Findings

- [x] [Review][Patch] Live Enterprise publish does not build geometry from normal ArcGIS output feature classes; `ReadFeatureRows` only reads JSON files and `BuildArcGisFeatures` only sends geometry when a source row already contains a `geometry` property, so real file geodatabase output can publish empty or attribute-only features instead of working points/lines/polygons. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs:737]
- [x] [Review][Patch] Scoped delete failures are not checked for ArcGIS REST responses; `EnsureArcGisSuccess` inspects top-level errors and failed `addResults`, but misses failed `deleteResults`, so retry/reprocess can leave old transaction rows active before adding replacement rows. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs:477]

## Required Enterprise Working Layers

### 1. Working Points

Purpose: transaction-scoped parcel corner/control points for review.

Required fields:

| Field | Type | Notes |
|---|---|---|
| `transaction_number` | Text(64) | Default transaction scope and human-readable key |
| `transaction_id` | Text(64) | System identifier from Innola/case manifest |
| `task_id` | Text(64) | Innola task id when available |
| `workflow_stage` | Text(64) | Example: `spatial_units_created`, `final_review_pending` |
| `review_state` | Text(64) | Example: `published_to_working`, `final_review_decided` |
| `case_status` | Text(64) | Example: `active`, `review_pending`, `review_closed` |
| `created_by` | Text(128) | Initial publisher/operator |
| `created_utc` | Date | First publish timestamp |
| `last_saved_by` | Text(128) | Last publisher/operator |
| `last_saved_utc` | Date | Last publish timestamp |
| `run_id` | Text(64) | Output run id |
| `point_id` | Text(64) | Visible point identifier |
| `parcel_group_id` | Text(64) | Parcel/traverse grouping |
| `parcel_name` | Text(128) | Parcel label context |
| `point_role` | Text(64) | Optional corner/tie/control role |
| `status_txt` | Text(64) | Point review status |
| `source_txt` | Text(1024) | Condensed source/evidence text |
| `row_id` | Text(64) | Link to review row when available |
| `is_active` | Short | Active transaction row flag |
| `edit_generation` | Long | Incremented on republish |

Shared final-review disposition fields required across working points, lines, polygons, and case index after this amendment:

| Field | Type | Notes |
|---|---|---|
| `review_decision` | Text(32) | `pending`, `approved`, `rejected`, or `postponed` |
| `review_decision_by` | Text(128) | Examiner/operator who recorded the disposition |
| `review_decision_utc` | Date | Disposition timestamp |
| `review_comment` | Text(1024) | Required or strongly recommended for reject/postpone |
| `official_comparison_status` | Text(64) | Optional comparison state against CADMAP/CADINDEX, for example `not_checked`, `matches_reference`, `conflict_found`, `needs_research` |
| `official_reference_ids` | Text(512) | Optional CADMAP/CADINDEX identifiers or compact reference notes |

### 2. Working Lines

Purpose: transaction-scoped parcel boundaries and COGO review lines.

Required fields:

| Field | Type | Notes |
|---|---|---|
| shared transaction fields | mixed | Same shared fields as points |
| `line_id` | Text(64) | Stable line identifier |
| `parcel_group_id` | Text(64) | Parcel/traverse grouping |
| `parcel_name` | Text(128) | Parcel label context |
| `start_pt` | Text(64) | Start point id |
| `end_pt` | Text(64) | End point id |
| `bearing_txt` | Text(64) | Display/source bearing |
| `distance_txt` | Text(64) | Display/source distance |
| `length_txt` | Text(128) | Display/source length |
| `line_type` | Text(32) | line/closure/curve |
| `seg_index` | Long | Segment index/order |
| `source_txt` | Text(1024) | Condensed source/evidence text |

### 3. Working Polygons / Parcels

Purpose: transaction-scoped parcel areas created by `Create Spatial Units`.

Required fields:

| Field | Type | Notes |
|---|---|---|
| shared transaction fields | mixed | Same shared fields as points |
| `parcel_group_id` | Text(64) | Parcel/traverse grouping |
| `parcel_name` | Text(128) | Display parcel name/number |
| `parcel_type` | Text(64) | Optional classification |
| `validation_status` | Text(64) | Latest validation status |
| `closure_status` | Text(64) | Latest closure/readiness status |
| `area_sq_m` | Double | Output area where available |
| `perimeter_m` | Double | Output perimeter where available |
| `review_note` | Text(512) | Optional reviewer note |
| `source_txt` | Text(1024) | Condensed source/evidence text |

### 4. Working Case Index

Purpose: one transaction row for quick lookup, resume, Compute closeout readiness, and audit.

Required fields:

| Field | Type | Notes |
|---|---|---|
| `case_id` | Text(64) | Case/workspace identifier |
| `transaction_number` | Text(64) | Primary working scope |
| `transaction_id` | Text(64) | System identifier |
| `task_id` | Text(64) | Innola task id |
| `workflow_name` | Text(64) | Example: `parcel_workflow_compute` |
| `workflow_stage` | Text(64) | Current stage |
| `case_status` | Text(64) | active/review_pending/review_closed/failed |
| `review_state` | Text(64) | working publish/review state |
| `review_decision` | Text(32) | pending/approved/rejected/postponed |
| `review_decision_by` | Text(128) | Examiner/operator who recorded the decision |
| `review_decision_utc` | Date | Decision timestamp |
| `review_comment` | Text(1024) | Decision reason/comment |
| `official_comparison_status` | Text(64) | CADMAP/CADINDEX comparison status when available |
| `official_reference_ids` | Text(512) | CADMAP/CADINDEX identifiers or compact reference notes |
| `assigned_user` | Text(128) | Innola assigned user |
| `assigned_group` | Text(128) | Innola assigned group |
| `created_by` | Text(128) | Initial publisher/operator |
| `created_utc` | Date | First publish timestamp |
| `last_saved_by` | Text(128) | Last publisher/operator |
| `last_saved_utc` | Date | Last publish timestamp |
| `run_id` | Text(64) | Output run id |
| `output_summary_ref` | Text(256) | Local output summary reference |
| `working_publish_ref` | Text(256) | Local publish diagnostic reference |
| `recoverability_state` | Text(64) | clean/warning/partial_restore |
| `is_active` | Short | Active row flag |
| `edit_generation` | Long | Incremented on republish |

### 5. Working Issues / Review Notes

Optional but recommended for validation blockers, waivers, manual review notes, and examiner comments.

## Recommended Settings Contract

Use the existing `enterprise_working_review` block, with `case_index` elevated from optional to required for this story:

```json
{
  "review_workspace_mode": "enterprise_working_layers",
  "enterprise_working_review": {
    "enabled": true,
    "service_root": "",
    "workspace_name": "sidwell_working_review",
    "publish_behavior": "replace_transaction_scope",
    "publish_timing": "on_outputs",
    "restore_behavior": "prefer_local_then_enterprise",
    "allow_cross_machine_restore": true,
    "transaction_scope_field": "transaction_number",
    "layers": {
      "points": "",
      "lines": "",
      "polygons": "",
      "issues": "",
      "case_index": ""
    }
  }
}
```

## Recommended Runtime Position

This branch belongs at:

1. `Attachments`
2. `Data Extraction`
3. `Validate Points`
4. `Create Spatial Units`
   - local output remains the source of truth
   - publish points/lines/polygons/case index into Enterprise working layers
   - load Enterprise working layers into ArcGIS Pro
5. `Final Review`
   - examiner reviews the Enterprise working layer state
   - examiner compares temporary transaction geometry against submitted documents and official reference layers such as CADMAP/CADINDEX
   - examiner records a Compute disposition: approve, reject, or postpone
6. `Finalize`
   - closeout is allowed only if Enterprise working publish evidence is current and the chosen Compute disposition was written to the working layers/case index
   - closeout does not promote the geometry into CADMAP, CADINDEX, or any authoritative cadastral store

## Source Of Truth

Do not publish directly from ad hoc in-memory UI state.

Use:

- `approved_review.json`
- generated local `parcel_points`, `parcel_lines`, `parcel_polygons`
- `output_summary.json`
- `enterprise_working_publish.json`

The Enterprise working layers are:

- collaborative review state
- temporary / transaction-scoped
- replaceable on reprocess
- the Compute review disposition record for approved, rejected, and postponed transaction geometry
- not the authoritative cadastral layer

## Current Code Notes

- Story 7.2 introduced `JsonEnterpriseWorkingLayerPublishService`, but its name and behavior must be reviewed before production use. If it is still file/JSON-backed, this story should introduce or wrap a live Enterprise feature-layer writer.
- The existing `enterprise_working_review.layers.case_index` setting exists in the configuration contract, but this story makes it required for Finalize readiness.
- The deduped output rows from the spatial output adapter should be used for Enterprise working geometry so shared parcel edges and duplicate shared points do not produce duplicate labels.
- The current `DefaultTransactionCompletionReadinessService` blocks completion by default. This story amendment requires replacing that placeholder with readiness logic that checks current Enterprise publish evidence and successful disposition writeback for the active transaction.

## Failure Handling

Failures must preserve:

- local validated review artifacts
- local geometry outputs
- current transaction lifecycle state
- clear status text in the Parcel Workflow pane
- detailed diagnostics in `enterprise_working_publish.json` and output summary

Failures must not:

- silently fall back to local-only mode
- mark Enterprise working publish as successful
- unlock Finalize/closeout readiness
- record an approved/rejected/postponed disposition without Enterprise writeback evidence
- write secrets, tokens, raw service responses, or signed URLs into artifacts

## Dependencies / Sequencing

Depends on:

- Story 7.1: Enterprise working review schema/configuration
- Story 7.2: initial Enterprise working-layer publication facade
- Story 7.3: restore transaction working state from Enterprise review layers
- current `Create Spatial Units` and `Final Review` stage flow

Precedes:

- Story 7.4: promote working review geometry to sync-ready authoritative package
- Follow-up story: record and route Compute Final Review dispositions in the UI and Innola lifecycle, if split from this amendment
- any future Enterprise Parcel Fabric authoritative or working-fabric path

## Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/`

## References

- `_bmad-output/implementation-artifacts/7-1-define-enterprise-working-review-layer-schema-and-configuration.md`
- `_bmad-output/implementation-artifacts/7-2-publish-approved-review-geometry-to-enterprise-working-layers.md`
- `_bmad-output/implementation-artifacts/7-3-restore-transaction-working-state-from-enterprise-review-layers.md`
- `_bmad-output/implementation-artifacts/7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md`
- `_bmad-output/implementation-artifacts/5-22-group-transaction-review-layers-and-ground-cogo-diagnostics-in-final-feature-class-state.md`

## Dev Agent Record

### Completion Notes

- Retargeted the runtime publish path to generic Enterprise working layers while leaving Enterprise Parcel Fabric behavior separate.
- Required `case_index` alongside points, lines, and polygons for Enterprise working-layer publish.
- Updated the working-layer publisher to use deduped output feature rows from `parcel_points`, `parcel_lines`, and `parcel_polygons`, enrich each row with Innola/workflow metadata, and publish one transaction-scoped case-index row.
- Preserved transaction-scoped replace semantics for offline JSON targets and added an ArcGIS REST `deleteFeatures`/`addFeatures` branch for `http`/`https` FeatureServer targets using `ARCGIS_PORTAL_TOKEN` when present.
- Updated map layer path selection so Enterprise working mode loads published working polygons, lines, and points rather than local-only review paths.
- Verified with `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`, `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`, and `python -m unittest discover -s tests` from `src\ProcessingTools`.

### File List

- `_bmad-output/implementation-artifacts/7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/OutputMapReviewStylingTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-23 | 0.1 | Drafted the Enterprise working Parcel Fabric publication story aligned to current `Create Spatial Units -> Final Review` workflow behavior. | Codex |
| 2026-06-23 | 1.0 | Implemented the enterprise Parcel Fabric publish branch, runtime settings seam, map-load target selection, and regression coverage for output-stage and final-review publication flows. | Codex |
| 2026-06-30 | 2.0 | Course-corrected Story 7.7 to target Enterprise generic working layers with points, lines, polygons, and case index as the active review workspace path. | Mary / Codex |
| 2026-06-30 | 3.0 | Implemented generic Enterprise working-layer publish, case-index publish, metadata enrichment, URL-aware ArcGIS REST publish branch, and map-load target selection. | Amelia / Codex |
| 2026-07-01 | 4.0 | Reframed Final Review/Finalize for Compute as a temporary document-geometry review disposition flow with approved, rejected, and postponed outcomes written back to Enterprise working layers. | Mary / Codex |
