---
baseline_commit: handoff-2026-07-01
---

# Story 7.9: Record Compute Final Review Disposition And Close Out Enterprise Working Review

Status: review

## Story

As a cadastral examiner completing Compute review,  
I want to finalize the submitted document geometry after Final Review has passed,  
so that the temporary Enterprise working layers preserve the transaction review outcome without promoting the geometry into CADMAP, CADINDEX, or any authoritative cadastral store.

## Business Context

Compute is the document-geometry review stage. Its job is to inspect the geometry derived from submitted transaction documents (`.pdf`, `.csv`, `.txt`, `.dwg`, and related attachments), compare the temporary parcel geometry against official reference layers such as CADMAP and CADINDEX, and record the examiner's review outcome.

The general workflow now keeps rejection/postponement outside this Compute closeout action. The Finalize stage should expose one closeout action named `Finalize`, which records that the submitted geometry passed Compute review and then completes the Innola task through the normal workflow lifecycle.

The approved/finalized disposition must write back to the Enterprise working points, lines, polygons, and case index when `review_workspace_mode = enterprise_working_layers`. This disposition does not perform authoritative cadastral promotion.

The Compute closeout must also create or update the Spatial Unit record expected by the Innola framework. The Spatial Unit must be created through the Innola API, linked to the active transaction, and reference the temporary reviewed geometry/workflow artifacts. The zipped working package must also be attached to the transaction for the Finalize closeout, following the same recoverable package pattern used by Suspend/Save and Close.

Finalize must also generate a Compute examination report from the reportable stage findings created throughout the workflow. At minimum, the report must summarize Supporting Document Check, Structure Check, Georeference Check, Dimension Check, Validate Points and Lines, Create Spatial Units, Final Review, Enterprise publish/disposition, Spatial Unit API status, and package attachment status. The generated report must be included in the zipped working package and referenced in local closeout artifacts.

The Finalize action area should make the operator intent obvious: `Cancel` and `Suspend` are session/lifecycle controls and should sit together on the left; the single Compute closeout control should sit on the right as `Finalize`. Do not show disabled `Reject` or `Postpone` placeholders in this workflow.

## Acceptance Criteria

1. Given a transaction is in `SpatialReviewApproved` / Finalize-ready state, when the examiner opens the Finalize area, then the UI presents a single Compute closeout action named `Finalize`; it does not show `Reject` or `Postpone` controls.
2. Given the examiner selects `Finalize`, when current Enterprise working publish evidence is available and current, then the add-in records `review_decision = approved` on the transaction-scoped working points, lines, polygons, and case-index row.
3. Given the general workflow determines a transaction should be rejected or postponed, when that path is needed, then it is handled outside this Finalize closeout UI and this story does not expose disabled placeholder buttons for those actions.
4. Given Finalize records an approved Compute result, when the Enterprise working rows are updated, then the temporary working geometry remains available for traceability and does not imply authoritative cadastral promotion.
5. Given Enterprise disposition writeback fails because of schema, auth, service, or network issues, when the examiner attempts closeout, then no successful Compute disposition is recorded locally or in Innola, the local case artifacts remain intact, and the UI shows a clear non-secret error.
6. Given `review_workspace_mode = enterprise_working_layers`, when closeout readiness is evaluated, then readiness requires current `enterprise_working_publish.json` / `output_summary.json` evidence and successful disposition writeback for the current transaction.
7. Given the transaction is closed after Finalize, when the completed/resume package is uploaded to Innola, then the package and manifest include the disposition, operator, timestamp, comment if supplied, and Enterprise publish references.
8. Given Finalize is being closed out, when the Innola Spatial Unit API contract is available, then the add-in creates or updates the transaction-linked Spatial Unit record through the API and stores the returned Spatial Unit identifier in local artifacts and working-layer/case-index metadata.
9. Given the Spatial Unit API call fails or the required endpoint/configuration is unavailable, when the examiner attempts closeout, then the transaction is not marked successfully closed, the working package is not falsely treated as final, and a clear diagnostic explains that Innola Spatial Unit creation is blocked.
10. Given Finalize succeeds, then a zipped working package containing the local case working information is attached to the Innola transaction using the same recoverable package pattern as Suspend/Save and Close.
11. Given Finalize succeeds and the transaction returns to the list, then the add-in refreshes the list without suppressing unrelated transactions except the completed transaction and does not present the case as an authoritative cadastral promotion.
12. Given the workflow restores a previously decided transaction from local or Enterprise state, when the case is reopened, then the UI can display the existing Compute disposition, package attachment state, and Spatial Unit reference without requiring another decision unless geometry/output evidence changed.
13. Given Story 7.4 remains deferred, when this story is complete, then no code writes to CADMAP, CADINDEX, Enterprise Parcel Fabric authoritative targets, or any sync-ready promotion package.
14. Given the Finalize action bar is displayed, when session and closeout controls are rendered, then `Cancel` and `Suspend` are grouped on the left and `Finalize` is grouped on the right so Suspend is not visually treated as the Compute closeout action.
15. Given the examiner selects `Finalize`, when closeout succeeds, then the operation sequence is: ensure/copy current reviewed geometry to Enterprise working layers, record `review_decision = approved`, create/update Innola Spatial Unit records linked to the transaction, generate the Compute examination report from stage findings, zip the produced/working files including the report, attach that zip to the transaction, and only then complete/close the Innola task.
16. Given any step in the Finalize closeout sequence fails, when failure is detected, then later steps are not executed, the transaction is not marked complete, and diagnostics identify which step blocked closeout without losing the local produced files.
17. Given reportable stage findings exist, when Finalize generates the Compute examination report, then the report includes stage-by-stage findings, outcomes, workflow effects, operator/timestamp metadata, source/artifact references, and any failed/warning/skipped/disabled/not-applicable results needed for examiner reporting.
18. Given report generation fails or required stage findings are missing/corrupt, when the examiner attempts Finalize, then the transaction is not marked successfully closed, the working package is not uploaded as final, and the UI shows a clear non-secret diagnostic identifying report generation as the blocker.
19. Given the Innola Spatial Unit API returns a saved Spatial Unit id, when Enterprise working-layer mode is active, then Finalize writes that id/status to the transaction-scoped `working_case_index` row using `spatial_unit_id` and `spatial_unit_api_status`; if those fields are missing from Enterprise, Finalize records a clear Enterprise case-index schema diagnostic and does not falsely report the case-index reference as saved.

## Tasks / Subtasks

- [x] Model Compute disposition state. (AC: 1-18)
  - [x] Add a small disposition contract with allowed values: `pending`, `approved`, `rejected`, `postponed`.
  - [x] Persist disposition metadata locally, recommended artifact: `working/compute_review_disposition.json`.
  - [x] Include transaction number/id, operator, UTC timestamp, decision, comment, output summary reference, enterprise publish reference, and publish run id.
  - [x] Include Innola Spatial Unit API status/id when available.
  - [x] Include working package attachment file name/source type/upload status when available.
  - [x] Include disposition metadata in manifest lifecycle/audit where appropriate without overloading authoritative completion fields.

- [x] Add Enterprise disposition writeback. (AC: 2-6)
  - [x] Add a service or method near `JsonEnterpriseWorkingLayerPublishService` that updates existing transaction-scoped working rows without republishing geometry.
  - [x] Update working points, lines, polygons, and case index with:
    - `review_decision`
    - `review_decision_by`
    - `review_decision_utc`
    - `review_comment`
    - `official_comparison_status`
    - `official_reference_ids`
    - `review_state = final_review_decided`
    - `case_status = review_closed` for approved/rejected; choose an explicit non-final status for postponed if product direction requires it, otherwise keep `review_closed` plus `review_decision = postponed`.
  - [x] Use configured `transaction_scope_field` and active transaction number for scoped `updateFeatures`/offline store updates.
  - [x] Treat ArcGIS REST top-level errors, `success: false`, failed update results, auth failures, and schema-missing responses as blockers.
  - [x] Do not log portal tokens, signed URLs, raw credential material, or full sensitive service responses.

- [x] Add Enterprise case-index Spatial Unit reference writeback. (AC: 8-9, 12, 15-16, 19)
  - [x] After the Innola Spatial Unit API save succeeds, update the transaction-scoped `working_case_index` row with `spatial_unit_id` and `spatial_unit_api_status`.
  - [x] Treat missing `spatial_unit_id` or `spatial_unit_api_status` fields as an Enterprise schema/remediation issue, not as a successful reference writeback.
  - [x] Persist a local evidence artifact for successful case-index Spatial Unit reference writeback.
  - [x] Record a warning/non-secret diagnostic when the Spatial Unit is saved through Innola but the Enterprise case-index reference writeback cannot be completed.

- [x] Replace single final approval UI with Finalize closeout action. (AC: 1, 3-5, 11, 14)
  - [x] Update `ParcelWorkflowDockpane.xaml` bottom action area and/or Finalize card to expose `Finalize` as the single closeout action.
  - [x] Move `Suspend` beside `Cancel` on the left side of the bottom action area.
  - [x] Place `Finalize` on the right side of the bottom action area as the primary Compute closeout control.
  - [x] Remove disabled `Reject` and `Postpone` placeholders from this workflow.
  - [x] Update confirmation copy so `Finalize` means "submitted geometry passed Compute review", not authoritative cadastral promotion.
  - [x] Update badge/help text currently saying "Complete", "Approve Transaction", and "final transaction completion" to use Compute closeout language.
  - [x] Keep `Suspend` behavior unchanged.

- [x] Update workflow/session closeout behavior. (AC: 5-10, 15-16)
  - [x] Add workflow-session methods for recording each disposition after spatial review approval.
  - [x] Replace or wrap `CompleteTransactionAsync` so it accepts a disposition and comment.
  - [x] Ensure Enterprise final-stage publish still runs when `publish_timing = on_complete`, then disposition writeback occurs only after publish success.
  - [x] For `Finalize`, enforce the closeout order: current Enterprise geometry publish/evidence -> approved disposition writeback -> Innola Spatial Unit create/update -> working package zip/upload -> Innola task complete/close.
  - [x] Product alignment patch: insert Compute examination report generation after Spatial Unit creation/update and before working package zip/upload.
  - [x] Ensure no later closeout step runs when an earlier step fails; for example, do not create/attach the package or complete Innola if Spatial Unit creation fails.
  - [x] Block closeout if spatial review approval is stale, output summary is missing, Enterprise publish evidence is missing/stale, or disposition writeback fails.
  - [x] Invalidate the disposition if Create Spatial Units is regenerated after the decision.

- [x] Update Innola lifecycle closeout semantics. (AC: 7-10)
  - [x] Replace the default `DefaultTransactionCompletionReadinessService` blocker with readiness that checks current Enterprise publish and disposition artifacts for Enterprise working-layer mode.
  - [x] Preserve existing owner/session gating in `InnolaTransactionLifecycleCoordinator`.
  - [x] Upload the package after the disposition is successfully recorded.
  - [x] For approved closeout, run Innola task completion only after Enterprise disposition, Spatial Unit creation/update, and working package attachment have all succeeded.
  - [x] Remove rejected/postponed closeout behavior from this story; those outcomes are handled by the general workflow process, not by the Finalize action.
  - [x] Record clear audit actions such as `compute_review_approved`, `compute_review_rejected`, and `compute_review_postponed`.

- [x] Create/update Innola Spatial Unit on Compute closeout. (AC: 8-9, 12)
  - [x] Review the Innola API/service contract for creating Spatial Units and linking them to a transaction.
  - [x] Use the supplied Postman flow as the implementation baseline:
    - `POST /api/v4/rest/administrative/ladm-objects/create/multi?transactionId={transactionId}` or documented equivalent `POST /api/v4/rest/data/objects/create/multi`.
    - Request body is an array of placeholder objects, one per spatial unit, for example `{ "@c": "SpatialUnitExt", "id": null }`.
    - API initializes defaults, ids/uids, address child objects, and transaction link scaffolding.
    - Save the populated objects through `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=spatialunit&transactionId={transactionId}` or documented equivalent `POST /api/v4/rest/data/objects/transaction`.
  - [x] Add a typed service seam for Spatial Unit creation/update rather than embedding endpoint calls directly in the ViewModel.
  - [x] Send the minimum required payload from the Compute artifacts: transaction id/number, disposition, operator, timestamps, working layer references, output summary reference, package references, and one SpatialUnitExt row per computed parcel/polygon.
  - [x] Populate known `SpatialUnitExt` fields from Compute output when available: `type = spatial_unit_type_land`, `status = reg_status_pending`, `idMarkupType = spatialunit`, `lot`, `suid`/`ladmId` if determinable, area fields, plan/examination number fields, address fields when known, and transaction link metadata.
  - [x] Preserve API-generated `id`, `uid`, nested `address`, and `link` objects from the create/default response when saving.
  - [x] Make route names configurable or isolated behind the service seam until the live Innola contract confirms whether this deployment should use `administrative/ladm-objects` or `data/objects`.
  - [x] Persist the returned Spatial Unit id/reference in the local disposition artifact, manifest lifecycle/audit, and Enterprise case index when available.
  - [x] Require the Enterprise `working_case_index` schema to expose `spatial_unit_id` and `spatial_unit_api_status` before treating the case-index reference writeback as successful.
  - [x] Block successful closeout when the Spatial Unit API call fails, unless the product owner explicitly defines an offline/deferred retry mode.
  - [x] Add non-secret diagnostics for missing endpoint configuration, unauthorized responses, validation errors, and network failures.

- [x] Generate Compute examination report from stage findings. (AC: 15, 17-18)
  - [x] Add or reuse a reporting service that reads stage findings from case artifacts without recomputing stage outcomes.
  - [x] Include findings from:
    - Supporting Document Check
    - Structure Check
    - Georeference Check
    - Dimension Check
    - Validate Points and Lines
    - Create Spatial Units
    - Final Review
    - Enterprise working-layer publish/disposition
    - Innola Spatial Unit creation/update
    - working package attachment
  - [x] Include `outcome`, `severity`, `workflow_effect`, message, correction, evidence, affected source/artifact references, operator, timestamp, and run id where available.
  - [x] Persist report artifacts under the case output/report area, for example `output/reports/compute_examination_report.html`, `.pdf` if supported, and/or `.json`.
  - [x] Store report path/reference in `compute_review_disposition.json`, manifest lifecycle/audit, and package metadata.
  - [x] Block Finalize if report generation fails or required stage-finding artifacts are missing/corrupt, unless product explicitly defines a deferred report mode.
  - [x] Do not include secrets, portal tokens, Innola tokens, raw signed URLs, or unbounded service responses in the report.

- [x] Attach zipped working package for every Compute disposition. (AC: 7, 10-12, 15, 17)
  - [x] Reuse `CaseResumePackageService` or extract a shared package builder so the closeout package includes the same recoverable working information as Suspend/Save and Close.
  - [x] Ensure Finalize closeout attaches a package to the active transaction.
  - [x] Use a clear attachment naming/source-type convention, for example extending `InnolaResumePackageConventions` with a Compute working package source type and file name builder.
  - [x] Include `compute_review_disposition.json`, `enterprise_working_publish.json`, `output_summary.json`, generated Compute examination report artifacts, manifest, logs, generated working outputs, and relevant local working artifacts in the zip.
  - [x] Treat "produced files" as the full case working/outputs package needed to reconstruct the review, including the local generated GDB, extracted geometry/summary artifacts, stage findings/summaries, report artifacts, disposition artifact, Enterprise publish summary, and manifest/audit files.
  - [x] Record package upload result in manifest/audit/disposition artifact.
  - [x] Do not mark closeout successful when package upload fails.

- [x] Update restore/resume behavior. (AC: 12)
  - [x] Extend enterprise/local restore to read disposition fields when present.
  - [x] Surface decided state in the UI without reusing "authoritative completed" language.
  - [x] If local artifact and Enterprise disposition disagree, prefer current output/publish evidence and show a recoverability warning.

- [x] Add tests. (AC: 1-18)
  - [x] Unit-test disposition artifact serialization and stale-output invalidation.
  - [x] Unit-test Enterprise disposition writeback success and failed ArcGIS update responses.
  - [x] Unit-test closeout blocking when `enterprise_working_publish.json` is missing/stale.
  - [x] Unit-test Finalize command enablement where feasible.
  - [x] Unit-test Finalize action layout intent where feasible: `Cancel`/`Suspend` are separate from the `Finalize` closeout control.
  - [x] Unit-test approved closeout ordering: Enterprise publish/evidence, disposition writeback, Spatial Unit API, package attachment, then Innola completion.
  - [x] Unit-test approved closeout stops before Innola completion when Spatial Unit creation or package upload fails.
  - [x] Unit-test lifecycle package upload after successful disposition and no upload after failed disposition.
  - [x] Unit-test Spatial Unit API success/failure behavior and returned id persistence.
  - [x] Unit-test report generation from synthetic stage findings.
  - [x] Unit-test Finalize blocks before zip/upload and Innola completion when report generation fails.
  - [x] Unit-test working package includes generated report artifacts.
  - [x] Unit-test working package attachment for Finalize.
  - [x] Unit-test restore of existing disposition state.

### Review Findings

- [x] [Review][Patch] Compute examination report is generated before working package metadata is populated [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:296] — resolved by recording planned package metadata before report generation and asserting the packaged report contains file name, source type, and pending upload status.

## Developer Context

### Current Code Notes

- `ParcelWorkflowDockpane.xaml` previously had a bottom `Approve` button bound to `CompleteTransactionCommand`. This should be reworded to `Finalize`.
- The bottom action area should render `Cancel`/`Suspend` together on the left and the single `Finalize` closeout action on the right.
- `ParcelWorkflowDockpaneViewModel.CompleteTransactionAsync()` currently:
  - confirms "Finalize this Compute review..."
  - calls `workflowSession.PublishEnterpriseWorkingReviewAsync(Environment.UserName)`
  - calls `ShellState.LifecycleCoordinator.CompleteAsync()`
  - returns to the transaction list on success.
- The current `CompleteTransactionAsync()` order is incomplete for approved Compute closeout. The required order is Enterprise working geometry publish/evidence, approved disposition writeback, Innola Spatial Unit create/update, Compute examination report generation, zipped working package attachment, then Innola task completion.
- `ParcelWorkflowDockpaneViewModel.ApproveSpatialReview()` currently saves spatial review approval and unlocks the Finalize-ready state. This should remain the "reviewed in map" gate, not become the final Compute disposition.
- `WorkflowSession.ApproveSpatialReview()` writes `spatial_review_approval.json` and sets `WorkflowState.SpatialReviewApproved`.
- `WorkflowSession.PublishEnterpriseWorkingReviewAsync()` only publishes/re-publishes geometry when final-stage publishing is configured. Disposition writeback should be a separate operation after publish evidence is current.
- `JsonEnterpriseWorkingLayerPublishService` already knows how to write transaction-scoped Enterprise working layer rows and case-index metadata. Story 7.9 should reuse its settings, auth, REST error-guard patterns, and transaction-scope behavior instead of creating a parallel portal client from scratch.
- `DefaultTransactionCompletionReadinessService` currently blocks completion by default with `sync_readiness_not_met`. Replace or extend this with real readiness for Enterprise working-layer Compute closeout.
- `InnolaTransactionLifecycleCoordinator.CompleteAsync()` handles owner checks, readiness checks, package upload, lifecycle `complete`, manifest update, and audit. Preserve those responsibilities, but do not let it run before disposition writeback and Innola Spatial Unit creation succeed.
- `InnolaTransactionLifecycleCoordinator.SaveAndCloseAsync()` already uploads a recoverable resume package before saving progress. The Compute closeout package should follow this recoverable-package pattern, not merely upload a thin status marker.
- `InnolaResumePackageConventions` currently has `ResumeSourceType`, `CompletedSourceType`, `BuildResumeAttachmentFileName`, and `BuildCompletedAttachmentFileName`. Story 7.9 should decide whether the Compute working package reuses completed package conventions or adds explicit Compute disposition package naming/source types.
- `IInnolaTransactionDetailService.UploadAttachmentAsync(...)` is the existing attachment upload seam used by lifecycle package upload. Reuse or wrap it for the Compute working package attachment.
- Stage findings are formalized by Story 4.9. Until that is implemented, report generation should consume the best available current artifacts (`structure_check_summary.json`, `georeference_check_summary.json` when available, `dimension_check_summary.json`, validation/output summaries, disposition/publish artifacts) and fail clearly if required report inputs are absent.
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
- Do not expose Reject/Postpone controls in this Finalize UI; those outcomes belong to the general workflow process.
- Do not complete the Innola task before the Compute examination report is generated and included in the working package.
- Do not fabricate report findings by rerunning stages during Finalize. Finalize reports the saved stage evidence from the case folder.
- Do not mark Innola Spatial Unit creation as successful without an API-confirmed result or an explicitly designed deferred retry artifact.
- Do not store portal credentials or tokens in settings, logs, diagnostics, or artifacts.
- Do not delete working geometry after rejection/postponement.

## Testing Requirements

Minimum verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- Focused tests for workflow/session, Enterprise working output, restore, and Innola lifecycle readiness.
- Focused tests for approved closeout ordering and failure short-circuit behavior before Innola completion.
- Focused tests proving successful Spatial Unit creation writes `spatial_unit_id` and `spatial_unit_api_status` to `working_case_index` when Enterprise working-layer mode is active.
- Focused tests proving Enterprise case-index Spatial Unit reference writeback reports a clear schema/remediation diagnostic when those fields are missing.

Manual/live smoke testing:

- Requires a configured Enterprise working service with Story 7.8 disposition fields and `working_case_index` Spatial Unit reference fields.
- Use a non-production transaction/test scope.
- Confirm Finalize updates case index and geometry rows with the approved Compute disposition.
- Confirm the Innola transaction receives the zipped working package attachment for Finalize closeout.
- Confirm the generated Compute examination report is present in the case reports folder and included in the zipped working package.
- Confirm the Innola Spatial Unit reference is created/updated and linked to the transaction, or closeout blocks with clear diagnostics if the API is unavailable.
- Confirm the `working_case_index` row for the transaction receives `spatial_unit_id` and `spatial_unit_api_status` after the Spatial Unit API save succeeds.
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

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` - passed, 0 warnings/errors.
- `dotnet run --no-build --project .\src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 306 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` - passed, 0 warnings/errors after review patch.
- `dotnet run --no-build --project .\src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 306 tests after review patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` - passed, 0 warnings/errors.
- `dotnet run --no-build --project .\src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 305 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` - passed, 0 warnings/errors.
- `dotnet run --no-build --project .\src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 310 tests.

### Completion Notes

Story created from the July 1 review that clarified Compute as a temporary document-geometry review stage with a controlled Enterprise working-layer closeout.
- Implemented the first approved Compute disposition slice: `ComputeReviewDecision`, `compute_review_disposition.json` persistence, and `WorkflowSession.RecordComputeDispositionAsync(...)`.
- Added Enterprise working-layer disposition writeback for transaction-scoped local JSON working stores, updating points, lines, polygons, and case index with `review_decision`, decision operator/time/comment, `review_state = final_review_decided`, and closeout `case_status`.
- Updated the Finalize action bar layout so `Cancel` and `Suspend` are grouped on the left, and `Finalize` is the only closeout action on the right. Reject/Postpone placeholders were removed based on the general workflow process.
- Updated Finalize wording and help text to describe Compute review closeout, not authoritative cadastral promotion.
- Updated approved closeout command order to run Enterprise publish, then approved disposition writeback, then Innola lifecycle completion/package upload.
- Updated Enterprise working-layer readiness so Innola completion can proceed only after `enterprise_working_publish.json` and `working/compute_review_disposition.json` exist.
- Added Innola Spatial Unit API adapter using the supplied Postman-style flow: create default `SpatialUnitExt` objects, preserve generated ids/address/link scaffolding, populate Compute review metadata, save through the transaction-linked `spatialunit` route, and return the saved Spatial Unit id.
- Wired approved Compute closeout so Spatial Unit creation runs after disposition writeback/readiness and before package upload and Innola task completion. Spatial Unit failure now blocks package upload and task completion.
- Persisted successful Spatial Unit API status/id back to `working/compute_review_disposition.json` and lifecycle audit.
- Finished the approved closeout order through package upload and Innola task completion. The coordinator now records working package filename/source/upload status in `compute_review_disposition.json`, includes pending package metadata before zipping, records uploaded/failed status after the upload attempt, audits package upload success/failure, and blocks Innola task completion when package upload fails.
- Remaining 7.9 work: ArcGIS REST `updateFeatures` disposition writeback, Enterprise case-index Spatial Unit id writeback, restore of decided state, and Finalize closeout tests.
- Product alignment patch added Compute examination report generation from saved stage findings as a required closeout step before working package zip/upload and Innola task completion.
- Added `ComputeExaminationReportService`, which builds `output/reports/compute_examination_report.json` from persisted stage findings and closeout artifacts without recomputing stage results.
- Wired Finalize closeout so report generation runs after Spatial Unit save and before package upload; report failure blocks package upload and Innola task completion.
- Persisted the report reference to `working/compute_review_disposition.json`, records audit success/failure, and includes generated report artifacts in the completed working package zip.
- Normalized resume/closeout package zip entry names to forward-slash paths so report artifacts are portable and inspectable.
- Added explicit `compute_review_{decision}` workflow audit actions when Compute disposition is recorded.
- Confirmed Spatial Unit save payload population covers the baseline `SpatialUnitExt` defaults and Compute review metadata currently available before package upload.
- Updated `CompleteTransactionAsync` to accept a Compute disposition/comment while preserving the current Finalize UI default of approved/no comment.
- Regenerating Create Spatial Units now clears stale spatial-review approval, Compute disposition, Enterprise disposition, and generated Compute examination report artifacts so old Finalize evidence cannot ride forward with new geometry.
- Resolved 7.9 review patch: planned working-package metadata is now persisted before report generation, so the report included in the uploaded package carries package file name, source type, and pending upload status.
- Finished ArcGIS REST disposition writeback for Enterprise working layers with schema validation, token/auth failure handling, top-level error handling, and failed `updateResults` detection without logging tokens or raw service responses.
- Expanded the Spatial Unit save payload with working layer references, output summary refs, report refs, package refs, and one `SpatialUnitExt` default/save row per computed parcel/polygon.
- Persisted Spatial Unit id/status into disposition, manifest lifecycle, audit, and Enterprise case index metadata when available.
- Strengthened Enterprise closeout readiness so Finalize requires approved disposition evidence, matching output summary/publish run ids, matching transaction metadata, and successful nonempty Enterprise publish evidence.
- Expanded completed working package contents to include full output artifacts, including generated GDB contents and extracted geometry artifacts, while preserving lightweight Suspend/Save-and-Close package behavior.
- Restore now exposes Compute disposition, Enterprise disposition, and Compute examination report artifacts and warns when restored disposition evidence disagrees with the current output summary run.
- Patched Enterprise working-layer publish metadata so feature rows and case-index rows store the Innola GUID in `transaction_id` and the readable transaction number in `transaction_number`, matching the disposition query scope used by Finalize.

### File List

- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseResumePackageService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDispositionPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IEnterpriseWorkingDispositionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ComputeExaminationReportServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-01 | 0.1 | Created story for Final Review disposition UI, Enterprise working-layer writeback, and Compute closeout behavior. | Mary / Codex |
| 2026-07-01 | 0.2 | Added Innola Spatial Unit API creation/update and zipped working package attachment requirements for every Compute disposition. | Mary / Codex |
| 2026-07-01 | 0.3 | Added concrete Postman API flow for SpatialUnitExt create/default and save-to-transaction behavior, with route verification guardrail. | Amelia / Codex |
| 2026-07-01 | 0.4 | Clarified Finalize button layout and approved closeout sequence: Enterprise working copy/evidence, disposition writeback, Spatial Unit creation, zipped working package attachment, then Innola task completion. | Mary / Codex |
| 2026-07-01 | 0.5 | Implemented approved Compute disposition artifact/writeback slice, Finalize action layout update, and Enterprise readiness gate for publish + disposition evidence. | Amelia / Codex |
| 2026-07-01 | 0.6 | Implemented Spatial Unit API adapter and lifecycle closeout gate so approved closeout creates/saves Spatial Units before package upload and Innola task completion. | Amelia / Codex |
| 2026-07-01 | 0.7 | Finished approved closeout package upload ordering and metadata persistence: pending before zip, uploaded/failed after upload, and no Innola complete on package failure. | Amelia / Codex |
| 2026-07-02 | 0.8 | Removed Reject/Postpone controls from the Finalize UI contract and renamed the closeout action from Approve to Finalize to align with the general workflow process. | Mary / Amelia / Codex |
| 2026-07-03 | 0.9 | Added Compute examination report generation from stage findings as a required Finalize closeout step and package artifact. | Mary / Codex |
| 2026-07-03 | 1.0 | Added Finalize disposition/comment seam and stale disposition invalidation when Create Spatial Units is regenerated. | Amelia / Codex |
| 2026-07-03 | 1.1 | Patched review finding so Compute examination report receives planned working-package metadata before package zip/upload. | Amelia / Codex |
| 2026-07-03 | 1.2 | Finished remaining 7.9 closeout: REST disposition guards, Spatial Unit refs in case index/manifest, full output package contents, stronger readiness, and disposition restore artifacts. | Amelia / Codex |
| 2026-07-06 | 1.3 | Patched story to require explicit Enterprise `working_case_index` fields `spatial_unit_id` and `spatial_unit_api_status` for Spatial Unit closeout reference writeback. | Mary / Codex |
| 2026-07-21 | 1.4 | Patched Enterprise working-layer transaction metadata so Finalize disposition writeback can find rows by the configured `transaction_id` scope. | Codex |
