---
baseline_commit: handoff-2026-07-01
---

# Story 7.9: Record Compute Final Review Disposition And Close Out Enterprise Working Review

Status: ready-for-dev

## Story

As a cadastral examiner completing Compute review,  
I want to record whether the submitted document geometry is approved, rejected, or postponed,  
so that the temporary Enterprise working layers preserve the transaction review outcome without promoting the geometry into CADMAP, CADINDEX, or any authoritative cadastral store.

## Business Context

Compute is the document-geometry review stage. Its job is to inspect the geometry derived from submitted transaction documents (`.pdf`, `.csv`, `.txt`, `.dwg`, and related attachments), compare the temporary parcel geometry against official reference layers such as CADMAP and CADINDEX, and record the examiner's review outcome.

The current UI and lifecycle code still assume a single final `Approve` action that uploads a completed package and marks the task complete. That is too narrow. Final Review must support three Compute dispositions:

- `approved`: submitted geometry passed Compute review.
- `rejected`: submitted geometry failed Compute review and should be routed/closed with a reason.
- `postponed`: submitted geometry needs follow-up or cannot be decided yet.

All three dispositions must write back to the Enterprise working points, lines, polygons, and case index when `review_workspace_mode = enterprise_working_layers`. None of these dispositions performs authoritative cadastral promotion.

The Compute closeout must also create or update the Spatial Unit record expected by the Innola framework. The Spatial Unit must be created through the Innola API, linked to the active transaction, and reference the temporary reviewed geometry/workflow artifacts. The zipped working package must also be attached to the transaction for all Compute dispositions, following the same recoverable package pattern used by Suspend/Save and Close.

## Acceptance Criteria

1. Given a transaction is in `SpatialReviewApproved` / Finalize-ready state, when the examiner opens the Finalize area, then the UI presents explicit Compute closeout actions for `Approve`, `Reject`, and `Postpone` instead of implying that every closeout is final approval.
2. Given the examiner selects `Approve`, when current Enterprise working publish evidence is available and current, then the add-in records `review_decision = approved` on the transaction-scoped working points, lines, polygons, and case-index row.
3. Given the examiner selects `Reject`, when they provide a rejection reason/comment, then the add-in records `review_decision = rejected` and preserves the temporary working geometry for traceability and follow-up.
4. Given the examiner selects `Postpone`, when they provide a reason/comment, then the add-in records `review_decision = postponed` and preserves the temporary working geometry for later review.
5. Given Enterprise disposition writeback fails because of schema, auth, service, or network issues, when the examiner attempts closeout, then no successful Compute disposition is recorded locally or in Innola, the local case artifacts remain intact, and the UI shows a clear non-secret error.
6. Given `review_workspace_mode = enterprise_working_layers`, when closeout readiness is evaluated, then readiness requires current `enterprise_working_publish.json` / `output_summary.json` evidence and successful disposition writeback for the current transaction.
7. Given the transaction is closed after any valid Compute disposition, when the completed/resume package is uploaded to Innola, then the package and manifest include the disposition, operator, timestamp, comment, and Enterprise publish references.
8. Given any valid Compute disposition is being closed out, when the Innola Spatial Unit API contract is available, then the add-in creates or updates the transaction-linked Spatial Unit record through the API and stores the returned Spatial Unit identifier in local artifacts and working-layer/case-index metadata.
9. Given the Spatial Unit API call fails or the required endpoint/configuration is unavailable, when the examiner attempts closeout, then the transaction is not marked successfully closed, the working package is not falsely treated as final, and a clear diagnostic explains that Innola Spatial Unit creation is blocked.
10. Given the Compute disposition is approved, rejected, or postponed, when closeout succeeds, then a zipped working package containing the local case working information is attached to the Innola transaction using the same recoverable package pattern as Suspend/Save and Close.
11. Given the examiner rejects or postpones a transaction, when the transaction returns to the list, then the add-in refreshes the list without suppressing unrelated transactions and does not present the case as an authoritative cadastral success.
12. Given the workflow restores a previously decided transaction from local or Enterprise state, when the case is reopened, then the UI can display the existing Compute disposition, package attachment state, and Spatial Unit reference without requiring another decision unless geometry/output evidence changed.
13. Given Story 7.4 remains deferred, when this story is complete, then no code writes to CADMAP, CADINDEX, Enterprise Parcel Fabric authoritative targets, or any sync-ready promotion package.

## Tasks / Subtasks

- [ ] Model Compute disposition state. (AC: 1-13)
  - [ ] Add a small disposition contract with allowed values: `pending`, `approved`, `rejected`, `postponed`.
  - [ ] Persist disposition metadata locally, recommended artifact: `working/compute_review_disposition.json`.
  - [ ] Include transaction number/id, operator, UTC timestamp, decision, comment, output summary reference, enterprise publish reference, and publish run id.
  - [ ] Include Innola Spatial Unit API status/id when available.
  - [ ] Include working package attachment file name/source type/upload status when available.
  - [ ] Include disposition metadata in manifest lifecycle/audit where appropriate without overloading authoritative completion fields.

- [ ] Add Enterprise disposition writeback. (AC: 2-6)
  - [ ] Add a service or method near `JsonEnterpriseWorkingLayerPublishService` that updates existing transaction-scoped working rows without republishing geometry.
  - [ ] Update working points, lines, polygons, and case index with:
    - `review_decision`
    - `review_decision_by`
    - `review_decision_utc`
    - `review_comment`
    - `official_comparison_status`
    - `official_reference_ids`
    - `review_state = final_review_decided`
    - `case_status = review_closed` for approved/rejected; choose an explicit non-final status for postponed if product direction requires it, otherwise keep `review_closed` plus `review_decision = postponed`.
  - [ ] Use configured `transaction_scope_field` and active transaction number for scoped `updateFeatures`/offline store updates.
  - [ ] Treat ArcGIS REST top-level errors, `success: false`, failed update results, auth failures, and schema-missing responses as blockers.
  - [ ] Do not log portal tokens, signed URLs, raw credential material, or full sensitive service responses.

- [ ] Replace single final approval UI with Compute disposition actions. (AC: 1, 3-5, 11)
  - [ ] Update `ParcelWorkflowDockpane.xaml` bottom action area and/or Finalize card to expose `Approve`, `Reject`, and `Postpone`.
  - [ ] Require a comment/reason for `Reject` and `Postpone`; approval comment can be optional.
  - [ ] Update confirmation copy so `Approve` means "submitted geometry passed Compute review", not authoritative cadastral promotion.
  - [ ] Update badge/help text currently saying "Complete", "Approve Transaction", and "final transaction completion" to use Compute closeout language.
  - [ ] Keep `Suspend` behavior unchanged.

- [ ] Update workflow/session closeout behavior. (AC: 5-10)
  - [ ] Add workflow-session methods for recording each disposition after spatial review approval.
  - [ ] Replace or wrap `CompleteTransactionAsync` so it accepts a disposition and comment.
  - [ ] Ensure Enterprise final-stage publish still runs when `publish_timing = on_complete`, then disposition writeback occurs only after publish success.
  - [ ] Block closeout if spatial review approval is stale, output summary is missing, Enterprise publish evidence is missing/stale, or disposition writeback fails.
  - [ ] Invalidate the disposition if Create Spatial Units is regenerated after the decision.

- [ ] Update Innola lifecycle closeout semantics. (AC: 7-10)
  - [ ] Replace the default `DefaultTransactionCompletionReadinessService` blocker with readiness that checks current Enterprise publish and disposition artifacts for Enterprise working-layer mode.
  - [ ] Preserve existing owner/session gating in `InnolaTransactionLifecycleCoordinator`.
  - [ ] Upload the package after the disposition is successfully recorded.
  - [ ] Decide and document whether rejected/postponed use the existing Innola `complete` transition or need a different lifecycle transition/source type; do not silently use approval wording for rejected/postponed outcomes.
  - [ ] Record clear audit actions such as `compute_review_approved`, `compute_review_rejected`, and `compute_review_postponed`.

- [ ] Create/update Innola Spatial Unit on Compute closeout. (AC: 8-9, 12)
  - [ ] Review the Innola API/service contract for creating Spatial Units and linking them to a transaction.
  - [ ] Use the supplied Postman flow as the implementation baseline:
    - `POST /api/v4/rest/administrative/ladm-objects/create/multi?transactionId={transactionId}` or documented equivalent `POST /api/v4/rest/data/objects/create/multi`.
    - Request body is an array of placeholder objects, one per spatial unit, for example `{ "@c": "SpatialUnitExt", "id": null }`.
    - API initializes defaults, ids/uids, address child objects, and transaction link scaffolding.
    - Save the populated objects through `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=spatialunit&transactionId={transactionId}` or documented equivalent `POST /api/v4/rest/data/objects/transaction`.
  - [ ] Add a typed service seam for Spatial Unit creation/update rather than embedding endpoint calls directly in the ViewModel.
  - [ ] Send the minimum required payload from the Compute artifacts: transaction id/number, disposition, operator, timestamps, working layer references, output summary reference, package references, and one SpatialUnitExt row per computed parcel/polygon.
  - [ ] Populate known `SpatialUnitExt` fields from Compute output when available: `type = spatial_unit_type_land`, `status = reg_status_pending`, `idMarkupType = spatialunit`, `lot`, `suid`/`ladmId` if determinable, area fields, plan/examination number fields, address fields when known, and transaction link metadata.
  - [ ] Preserve API-generated `id`, `uid`, nested `address`, and `link` objects from the create/default response when saving.
  - [ ] Make route names configurable or isolated behind the service seam until the live Innola contract confirms whether this deployment should use `administrative/ladm-objects` or `data/objects`.
  - [ ] Persist the returned Spatial Unit id/reference in the local disposition artifact, manifest lifecycle/audit, and Enterprise case index when available.
  - [ ] Block successful closeout when the Spatial Unit API call fails, unless the product owner explicitly defines an offline/deferred retry mode.
  - [ ] Add non-secret diagnostics for missing endpoint configuration, unauthorized responses, validation errors, and network failures.

- [ ] Attach zipped working package for every Compute disposition. (AC: 7, 10-12)
  - [ ] Reuse `CaseResumePackageService` or extract a shared package builder so the closeout package includes the same recoverable working information as Suspend/Save and Close.
  - [ ] Ensure approved, rejected, and postponed closeouts all attach a package to the active transaction.
  - [ ] Use a clear attachment naming/source-type convention, for example extending `InnolaResumePackageConventions` with a Compute working package source type and file name builder.
  - [ ] Include `compute_review_disposition.json`, `enterprise_working_publish.json`, `output_summary.json`, manifest, logs, generated working outputs, and relevant local working artifacts in the zip.
  - [ ] Record package upload result in manifest/audit/disposition artifact.
  - [ ] Do not mark closeout successful when package upload fails.

- [ ] Update restore/resume behavior. (AC: 12)
  - [ ] Extend enterprise/local restore to read disposition fields when present.
  - [ ] Surface decided state in the UI without reusing "authoritative completed" language.
  - [ ] If local artifact and Enterprise disposition disagree, prefer current output/publish evidence and show a recoverability warning.

- [ ] Add tests. (AC: 1-13)
  - [ ] Unit-test disposition artifact serialization and stale-output invalidation.
  - [ ] Unit-test Enterprise disposition writeback success and failed ArcGIS update responses.
  - [ ] Unit-test closeout blocking when `enterprise_working_publish.json` is missing/stale.
  - [ ] Unit-test approve/reject/postpone command enablement and comment requirements where feasible.
  - [ ] Unit-test lifecycle package upload after successful disposition and no upload after failed disposition.
  - [ ] Unit-test Spatial Unit API success/failure behavior and returned id persistence.
  - [ ] Unit-test working package attachment for approve/reject/postpone.
  - [ ] Unit-test restore of existing disposition state.

## Developer Context

### Current Code Notes

- `ParcelWorkflowDockpane.xaml` currently has a bottom `Approve` button bound to `CompleteTransactionCommand`. This assumes a single approval path and should be split or reworded.
- `ParcelWorkflowDockpaneViewModel.CompleteTransactionAsync()` currently:
  - confirms "Approve this transaction..."
  - calls `workflowSession.PublishEnterpriseWorkingReviewAsync(Environment.UserName)`
  - calls `ShellState.LifecycleCoordinator.CompleteAsync()`
  - returns to the transaction list on success.
- `ParcelWorkflowDockpaneViewModel.ApproveSpatialReview()` currently saves spatial review approval and unlocks the Finalize-ready state. This should remain the "reviewed in map" gate, not become the final Compute disposition.
- `WorkflowSession.ApproveSpatialReview()` writes `spatial_review_approval.json` and sets `WorkflowState.SpatialReviewApproved`.
- `WorkflowSession.PublishEnterpriseWorkingReviewAsync()` only publishes/re-publishes geometry when final-stage publishing is configured. Disposition writeback should be a separate operation after publish evidence is current.
- `JsonEnterpriseWorkingLayerPublishService` already knows how to write transaction-scoped Enterprise working layer rows and case-index metadata. Story 7.9 should reuse its settings, auth, REST error-guard patterns, and transaction-scope behavior instead of creating a parallel portal client from scratch.
- `DefaultTransactionCompletionReadinessService` currently blocks completion by default with `sync_readiness_not_met`. Replace or extend this with real readiness for Enterprise working-layer Compute closeout.
- `InnolaTransactionLifecycleCoordinator.CompleteAsync()` handles owner checks, readiness checks, package upload, lifecycle `complete`, manifest update, and audit. Preserve those responsibilities, but do not let it run before disposition writeback and Innola Spatial Unit creation succeed.
- `InnolaTransactionLifecycleCoordinator.SaveAndCloseAsync()` already uploads a recoverable resume package before saving progress. The Compute closeout package should follow this recoverable-package pattern, not merely upload a thin status marker.
- `InnolaResumePackageConventions` currently has `ResumeSourceType`, `CompletedSourceType`, `BuildResumeAttachmentFileName`, and `BuildCompletedAttachmentFileName`. Story 7.9 should decide whether the Compute working package reuses completed package conventions or adds explicit Compute disposition package naming/source types.
- `IInnolaTransactionDetailService.UploadAttachmentAsync(...)` is the existing attachment upload seam used by lifecycle package upload. Reuse or wrap it for the Compute working package attachment.
- The Innola Spatial Unit creation endpoint/service contract is not yet confirmed in this repo. Treat endpoint discovery/review as a required first task and avoid hard-coding speculative URLs without tests.
- Supplied Postman evidence: `Sidwell Plan Exam Scenario.postman_collection.json`, items `4 - Create Spatial Units` and `5 - Save Spatial Units`. Item 4 creates default `SpatialUnitExt` objects. Item 5 saves populated `SpatialUnitExt` objects linked to the transaction with `objectType = spatialunit`.
- The user-provided REST documentation links mention Data API routes `POST /api/v4/rest/data/objects/create/multi` and `POST /api/v4/rest/data/objects/transaction`, while the Postman collection uses `POST /api/v4/rest/administrative/ladm-objects/create/multi` and `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=spatialunit`. Implementation must verify the live route and keep this behind an adapter/service seam.
- `JsonEnterpriseWorkingStateRestoreService` and Story 7.3 provide restore patterns. Extend restore behavior to include disposition state.

### Required Files To Inspect Before Dev

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/SpatialReview/SpatialReviewApprovalPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaResumePackageConventions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`

### Dependencies

- Story 7.7 defines the Enterprise working-layer runtime contract and disposition semantics.
- Story 7.8 must provision/validate the disposition fields before this story can be tested live against Enterprise.
- Story 7.3 restore behavior should be extended to include disposition state.
- Story 7.4 remains future/deferred and must not be implemented here.

### Non-Goals

- Do not update CADMAP, CADINDEX, Enterprise Parcel Fabric, or authoritative cadastral data.
- Do not create a sync-ready authoritative package; that remains Story 7.4.
- Do not silently treat rejected or postponed decisions as approvals.
- Do not mark Innola Spatial Unit creation as successful without an API-confirmed result or an explicitly designed deferred retry artifact.
- Do not skip the working package attachment for rejected/postponed outcomes.
- Do not store portal credentials or tokens in settings, logs, diagnostics, or artifacts.
- Do not delete working geometry after rejection/postponement.

## Testing Requirements

Minimum verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- Focused tests for workflow/session, Enterprise working output, restore, and Innola lifecycle readiness.

Manual/live smoke testing:

- Requires a configured Enterprise working service with Story 7.8 disposition fields.
- Use a non-production transaction/test scope.
- Confirm `approved`, `rejected`, and `postponed` each update case index and geometry rows.
- Confirm rejected/postponed records remain visible in the working layers.
- Confirm the Innola transaction receives the zipped working package attachment for approved, rejected, and postponed closeouts.
- Confirm the Innola Spatial Unit reference is created/updated and linked to the transaction, or closeout blocks with clear diagnostics if the API is unavailable.
- Confirm the Spatial Unit service first initializes default `SpatialUnitExt` rows, then saves populated rows linked to the transaction using the selected live route.

## References

- `_bmad-output/implementation-artifacts/7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md`
- `_bmad-output/implementation-artifacts/7-8-add-enterprise-working-layer-admin-provisioning-and-maintenance-settings-tab.md`
- `_bmad-output/implementation-artifacts/7-3-restore-transaction-working-state-from-enterprise-review-layers.md`
- `_bmad-output/implementation-artifacts/7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes

Story created from the July 1 review that clarified Compute as a temporary document-geometry review stage with approve/reject/postpone outcomes.

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-01 | 0.1 | Created story for Final Review disposition UI, Enterprise working-layer writeback, and Compute closeout behavior. | Mary / Codex |
| 2026-07-01 | 0.2 | Added Innola Spatial Unit API creation/update and zipped working package attachment requirements for every Compute disposition. | Mary / Codex |
| 2026-07-01 | 0.3 | Added concrete Postman API flow for SpatialUnitExt create/default and save-to-transaction behavior, with route verification guardrail. | Amelia / Codex |
