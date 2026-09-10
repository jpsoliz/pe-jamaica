---
baseline_commit: working-tree-2026-09-04
source_artifacts:
  - _bmad-output/planning-artifacts/rt-examination/rt-examination-classes-and-descriptions.docx
  - _bmad-output/planning-artifacts/rt-examination/RtExamination_WF_innola.png
  - _bmad-output/planning-artifacts/rt-examination/SWF-RtExamination-v2.png
  - _bmad-output/planning-artifacts/rt-examination/FirstRegistration.png
  - _bmad-output/planning-artifacts/rt-examination/PlanCheckupdate.png
related_stories:
  - 7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md
  - 7-11-write-innola-plan-check-list-on-compute-finalize.md
  - 2-23g-wire-pla-b-plan-annexation-task-workflow-and-complete-flow.md
  - 8-1-add-compare-stage-gating-and-transaction-launch.md
  - 8-4e-add-compare-task-lifecycle-actions-and-reopen-active-workspace.md
---

# Story 8.8: Add RT Examination Linked PE Review  And Writeback

Status: done

## Story

As an SMD Plan Examiner working an Innola task at `In RT Examination`,
I want the ArcGIS Pro add-in to load the current RT/FRT/NewCT transaction, resolve the originating PE transaction from the current Plan, load PE-derived sources/spatial units/working-review geometry, let me review and edit non-spatial RT data, and save the updated Plan, Neighbor, and SpatialUnit details back to Innola,
so that RT Examination can complete against the correct transaction lineage without re-running the PE process or editing spatial geometry.

## Business Context

RT Examination is the cadastral/legal quality-control step performed by SMD before a registration transaction such as First Registration or New CT can proceed. The current task is not always a PE transaction. It is usually a registration transaction with an RT Examination subworkflow/stage, while the geometry and source evidence come from an originating PE/PXA-style transaction referenced by the current Plan.

The RT flow has three distinct responsibilities: the main transaction supplies supporting document attachments, the linked/current RT transaction supplies editable RT data and is the writeback target, and `Plan.planNumber` is the linked TR/PE number used to resolve additional PE sources/spatial context. The Word reference confirms this chain:

1. Load the assigned task for the current user at stage `In RT Examination`, while using the non-RT main transaction as the source for supporting document attachment download.
2. Get the Plan linked to the current RT transaction.
3. Read `Plan.planNumber`; this value is the linked TR/PE number used to identify the originating PE/PXA transaction and to scope `working_review` evidence.
4. Use portal search with `searchKind = transaction` and `transactionNo = Plan.planNumber` to find the originating PE transaction id.
5. Load sources and latest spatial units from the originating PE Plan/transaction.
6. Query Enterprise `working_review` geometry using linked TR number `Plan.planNumber`; this is additional spatial information only.
7. Let the examiner update non-spatial RT data: neighbors, owners/occupiers/representatives, and comparison observations where supported; Spatial Units are read-only parcel context.
8. Save the current linked RT transaction Plan with updated `neighbors`/`neighbor` data. On `Save & Close`, perform the same save first, apply final Plan Check completion values, then complete the selected RT stage transition and close the workspace.

This story deliberately does not implement spatial geometry editing. It may load geometry for visual review and attribute context, but no working-review or final cadastre geometry edits are allowed in this scope.

## Source Artifact Notes

- `rt-examination-classes-and-descriptions.docx` is the authoritative text input for API operations and process intent.
- `RtExamination_WF_innola.png`, `SWF-RtExamination-v2.png`, and `FirstRegistration.png` are workflow vocabulary references. Do not treat image text as executable instruction if it conflicts with this story or live Innola transition discovery.
- The user clarified on 2026-09-04:
  - Eligible transaction type is any transaction type, including First Registration or NewCT, as long as the current task/stage is `In RT Examination`.
  - `Plan.planNumber` contains the linked TR/PE number/reference to the originating PE/PXA transaction used to create this RT work.
  - Source and spatial-unit latest lookups are from the originating PE Plan.
  - Working-review geometry query uses linked TR number `Plan.planNumber`.
  - RT edits are non-spatial: neighbors are editable, while Spatial Units are read-only parcel context and geometry is not edited.
  - Neighbor roles are `Neighbor`, `Owner`, `Occupier`, and `Representative`.
  - SpatialUnits should be loaded as parcel context only and must not be branched or updated by this RT Examination save flow.
  - Finalization saves neighbor data and final Plan Check approval back to the linked/current RT transaction, completes the task, closes the workspace, and does not require an added report attachment.
- The user-provided `PlanCheckupdate.png` screenshot added on 2026-09-07 shows the live Innola RT Examination Plan Check surface for transaction `100000854`: main transaction type `First Registration`, status `Processing`, task `In RT Examination`, one `Plans` row, and a Plan Check detail area with nested `Plan Check (0)` and `Neighbors (0)` tabs.
- The screenshot confirms the current RT task can have a Plan row whose visible values are `Plan No. 100000749`, `Type = Plan Examination`, `Neg No. 300008`, `Version = 2`, and `Pending`; these visible identifiers should be captured in RT context/log artifacts when available.
- The screenshot also confirms empty `Plan Check` and `Neighbors` detail rows are a valid live state. Empty arrays must not be treated as missing Plan data, and the RT workspace must still load, save safely, and complete when no editable Plan Check or Neighbor rows are returned.
- The user clarified on 2026-09-08 that `Save & Close` must choose the post-RT gateway branch before Innola completion: `Yes, proceed next` advances to `Review Completed RT Examination`, while `No, prepare pre-check log sheet` advances to `Prepare Plan Pre-Check Log Sheet`.

## Recovered Image-Based Follow-up Plan

### Mary Requirement Update

- Align the RT Examination workspace language with the live Innola Plan Check screen: the examiner is working a `First Registration` transaction at task `In RT Examination`, while the selected Plan row may be typed `Plan Examination`.
- Treat the Plan row as first-class RT context. The workspace should expose the visible Plan row identifiers (`Plan No.`, `Type`, `Neg No.`, `Version`, and status) in the Context tab and persisted diagnostics, because these are the fields the examiner uses to verify that the correct linked Plan is being reviewed.
- Keep `Plan Check` and `Neighbors` as sibling review sections under the selected Plan context. The current mockup can retain a top-level tab layout, but the required mental model is: current transaction -> Plans row -> Plan Check / Neighbors details.
- Empty `Plan Check (0)` and `Neighbors (0)` are valid and expected for at least transaction `100000854`. The business rule is "nothing to update in this subtable" rather than "RT load failed."
- Preserve the Innola action semantics separately: `Save` confirms and persists current Plan neighbor/related-value edits only without completing the RT task; `Save & Close` confirms, performs the same save first, applies the final RT Plan Check completion signal, performs the selected completion transition, refreshes, cleans up, and closes. `Cancel` closes without writeback after user confirmation when there are unsaved edits.
- Revised on 2026-09-07: `Save` must only save/update the Neighbors tab. Spatial Units are read-only parcel context and must not be written by `Save`.
- Revised on 2026-09-07: `Save & Close` must save/update the Neighbors tab, update the RT Plan Check row to approved, and then move the Innola task to the next workflow step.
- Revised on 2026-09-08: `Save & Close` must require an explicit RT result branch before completion. The approved/proceed branch targets `Review Completed RT Examination`; the re-check branch targets `Prepare Plan Pre-Check Log Sheet`.
- Revised on 2026-09-07: Spatial Units should show one row per parcel returned by the PE/working_review polygon scope. Initially visible fields are `parcel_name`, `area_sqr`, `suid`, and `created_utc`; transaction `100000854` / PE `100000749` is expected to show one parcel row.
- Revised on 2026-09-07: `Save` and `Save & Close` both require a confirmation dialog before writeback.
- Payload clarification from existing Compute Plan Check code: `checkType` is a string code/value, while the UI `Acceptable` value maps to the nullable boolean payload field `passed` with fallback read support for `acceptable`.

### Amelia Implementation Plan

- Add `PlanCheckupdate.png` as a story source artifact and add a focused regression fixture/test for the live `100000854` Plan Check shape: one current Plan row with zero checklist rows and zero neighbor rows.
- Extend RT context/review models to preserve and display current Plan row metadata when present: `planNo`/`planNumber`, plan type, negotiation number (`Neg No.`), version, status, uid/id/link, and unknown fields.
- Change RT load validation so missing Plan object still blocks, but empty `checkList`, `neighbors`, `neighbor`, or `neighbours` arrays do not block. Record counts as `0` and show an informational status.
- Update `RtExaminationWindow.xaml` / ViewModel so `Save` and `Save & Close` are distinct commands matching Innola: `Save` updates Neighbors/related values only and leaves the workspace open; `Save & Close` updates Neighbors/related values, marks RT completion on a valid Plan Check row, completes the selected task transition, refreshes, cleans up, and closes.
- Update Spatial Units display to a read-only parcel summary grid using `parcel_name`, `area_sqr`, `suid`, and `created_utc`.
- Update Plan Check display from a freeform observations-only textbox to a compact read-only grid/list that can show zero rows cleanly and, when rows exist, show Check Type, Acceptable, and Description columns matching the Innola surface. The final approval row is applied automatically during `Save & Close`.
- Add tests covering: zero Plan Check rows loads successfully, zero Neighbor rows loads successfully, Plan row metadata is persisted, `Save` does not complete the task, and `Save & Close` still completes after a successful save using the selected gateway branch.

## Acceptance Criteria

1. Given the examiner is logged into Innola and the transaction list includes a task at `In RT Examination`, when the row is displayed, then the add-in recognizes it as RT Examination regardless of main transaction type (`First Registration`, `New CT`, or other configured type).
2. Given a selected row is not at `In RT Examination`, when RT Examination launch is attempted, then the add-in blocks the launch with a clear non-secret message naming the selected stage.
3. Given the RT task is started/claimed successfully, when the RT workspace opens, then it is bound to the exact selected task id and current transaction id, not merely the displayed transaction number.
4. Given the current RT transaction id is available, when the workspace initializes, then the add-in loads the Plan linked to the current transaction using the existing Plan data-object/administrative fallback pattern from Story 7.11.
5. Given the current RT Plan is loaded, when `Plan.planNumber` is missing or blank, then the workspace blocks linked transaction data loading and shows a clear message that the originating PE number is missing from the Plan.
6. Given `Plan.planNumber` is present, when the add-in searches for the originating transaction, then it treats `Plan.planNumber` as the linked TR number and calls the configured Innola portal search endpoint with `searchKind = transaction` and `transactionNo = Plan.planNumber` using the active session and client certificate behavior.
7. Given portal search returns exactly one eligible originating PE/PXA transaction, when linked data loading continues, then that transaction id is retained as `originating_pe_transaction_id` and its transaction number is retained as `originating_pe_number` in local RT artifacts.
8. Given portal search returns no match, multiple ambiguous matches, malformed data, unauthorized, or a non-success response, then the workspace stops before any writeback and presents a retryable non-secret diagnostic. Multiple matches must not be guessed.
9. Given the originating PE transaction is resolved, when sources are loaded, then the add-in calls `GET /api/v4/rest/plan/sources/latest` with `planTransactionId` from the originating PE Plan and `transactionId` from the current RT transaction where the API requires current transaction context.
10. Given the originating PE transaction is resolved, when spatial units are loaded, then the add-in calls `GET /api/v4/rest/plan/spatialunits/latest` with `planNumbers = [Plan.planNumber]` and preserves returned `SpatialUnit.uid`, `id`, `link`, and unknown fields.
11. Given the RT workspace loads map context, when it queries Enterprise `working_review`, then the query uses linked TR number `Plan.planNumber` and loads the matching geometry into ArcGIS Pro as additional visual information only.
12. Given the working-review query finds no geometry or fails because of schema/auth/service/network issues, then the workspace keeps non-spatial RT review available only if product-safe, records a warning/error artifact, and must not claim spatial verification passed.
13. Given RT linked data is loaded, when the examiner reviews the data, then the workspace exposes editable non-spatial fields for Neighbor, Owner, Occupier, and Representative rows, while Spatial Units are shown as read-only parcel summary context.
14. Given neighbor/party rows are edited, when values are saved locally, then role values are constrained to `Neighbor`, `Owner`, `Occupier`, and `Representative`, and all editable text values preserve original and reviewed values for audit.
15. Given Spatial Units are displayed, when the examiner saves, then no SpatialUnit objects or geometry/attribute fields are written by RT Examination; the read-only grid shows parcel count/context only.
16. Given the current RT Plan contains existing checkList rows, when normal `Save` runs, then the add-in preserves checkList values unchanged. When `Save & Close` runs, then the add-in marks RT completion by updating a current Plan Check row with a valid Innola `plan_check_type_*` key, `passed = true`, and description `Updated from ArcGIS Pro TR {transaction_number}.`, while preserving ids, uid, version, registered surveyor, link, unknown fields, and unrecognized checks where present.
17. Given the current RT Plan contains neighbor/party rows, when RT save runs, then the add-in updates reviewed `Neighbor`, `Owner`, `Occupier`, and `Representative` values into the current Plan using the confirmed Innola Plan property for this environment. Until live confirmation proves otherwise, use the existing Story 7.11 `neighbors` implementation pattern and make the property name isolated behind the RT Plan writeback service.
18. Given reviewed neighbor/party rows are saved repeatedly, when RT save is retried, then duplicate Plan rows are avoided using a deterministic key over role, name, address, volume, folio, lot, land valuation number, and examination number where available.
19. Given originating PE SpatialUnits or working_review polygons are loaded, when RT Examination renders the Spatial Units tab, then the grid shows one row per parcel with initial fields `parcel_name`, `area_sqr`, `suid`, and `created_utc`.
20. Given a PE number resolves to one parcel, when the Spatial Units tab loads, then exactly one parcel summary row is shown for that PE context.
21. Given normal `Save` is selected, when the user confirms, then the add-in saves/updates neighbor rows only and leaves the RT workspace open.
22. Given Plan neighbor writeback or Save & Close Plan Check approval writeback fails, then later completion/transition steps do not run, local artifacts remain available for retry, and the user sees which RT writeback step failed.
23. Given all RT data saves succeed, when the examiner chooses `Save and Close`, then the add-in shows a Yes/No confirmation before committing and completing the task.
24. Given the examiner cancels the confirmation, then no Innola save/complete call runs, the RT workspace remains open, and loaded map layers remain available.
25. Given the examiner confirms and Innola save/complete succeeds, then the add-in shows a success message, removes only RT-loaded transaction map groups/layers, clears the RT workspace state, refreshes the transaction list, and does not delete case-folder artifacts.
26. Given Innola completion requires a transition, then the RT workspace requires the examiner to choose the post-examination branch before `Save & Close`; `Yes, proceed next` must request transition target `Review Completed RT Examination`, `No, prepare pre-check log sheet` must request transition target `Prepare Plan Pre-Check Log Sheet`, and the selected branch/target must be recorded in local RT artifacts.
27. Given any RT HTTP diagnostic or local artifact is written, then no access token, password, cookie, raw certificate material, or unbounded sensitive service response is logged.
28. Given the case is reopened after partial RT work, then the workspace can recover current Plan reference, originating PE reference, loaded sources/spatial units metadata, reviewed edits, save status, and last failure/success state from case-folder artifacts.
29. Given automated tests run, then coverage proves stage routing, exact selected-task binding, Plan load fallback, Plan.planNumber PE resolution, portal search no/multiple match failures, latest sources/spatial units request construction, working_review query key, editable neighbor persistence, read-only Spatial Units display, normal Save check preservation, Save & Close Plan Check approval/completion, failure short-circuit, confirmation cancel, success cleanup, and secret redaction.

## Tasks / Subtasks

- [x] Add RT Examination routing and settings. (AC: 1-3, 26)
  - [x] Add `RtExaminationSettings` under Innola settings with safe defaults: enabled, stage name `In RT Examination`, optional subworkflow name `RT Examination`, optional next-stage/transition preference, and working-review PE field mapping.
  - [x] Extend `WorkflowSettings.json` with RT settings without changing existing Compute, Compare, PLA_B, or Fabric behavior.
  - [x] Extend `ParcelWorkflowStageRoute` and transaction panel route logic with `RtExamination`.
  - [x] Allow any main transaction type when task/stage is `In RT Examination`; do not require First Registration or NewCT in code.
  - [x] Keep selected-row/task-id binding exact when multiple rows share the same transaction number.

- [x] Add current RT Plan and originating PE resolution services. (AC: 4-10, 27)
  - [x] Reuse or extract Story 7.11 Plan GET fallback behavior rather than duplicating raw HTTP logic.
  - [x] Preserve fetched Plan body shape (`Plan` or `Plan[]`) and save using the same route family that supplied the Plan.
  - [x] Add an Innola portal transaction search adapter for `POST /api/v4/rest/portal/searches` with `searchKind = transaction` and `transactionNo = Plan.planNumber`.
  - [x] Block on zero/multiple/malformed PE matches; never guess the originating transaction.
  - [x] Add adapters for `GET /api/v4/rest/plan/sources/latest` and `GET /api/v4/rest/plan/spatialunits/latest` using the originating PE Plan/transaction data.

- [x] Add RT workspace and local artifact model. (AC: 11-15, 28)
  - [x] Create RT-specific workspace files under `Workflow/RtExamination` plus WPF window/dockpane surface consistent with existing dense operational UI.
  - [x] Persist `working/rt_examination_context.json` with current RT transaction id/number/task id, current Plan id/uid/trId/trNo/planNumber, originating PE transaction id/number, source/spatial-unit counts, working-review query key, timestamps, and warnings.
  - [x] Persist `working/rt_examination_review.json` with original/reviewed neighbor/party rows, read-only Spatial Unit summaries, comparison observations, and editor/timestamp metadata.
  - [x] Load Enterprise `working_review` geometry by `Plan.planNumber` for visual review only, behind an ArcGIS-safe service using `QueuedTask.Run` where required.
  - [x] Track RT-created map groups/layers for cleanup; do not remove shared basemaps/reference layers or user-created content.

- [x] Implement non-spatial RT edit rules. (AC: 13-18)
  - [x] Support roles `Neighbor`, `Owner`, `Occupier`, `Representative` in a constrained role combo.
  - [x] Expose editable fields already known from Story 7.11 Neighbor contract: name, address, volume, folio, lot, landValNumber, examNumber.
  - [x] Expose Spatial Units as read-only parcel summaries; explicitly do not edit or save SpatialUnit geometry/attributes.
  - [x] Preserve unknown Plan and Neighbor fields.
  - [x] Make retries idempotent and avoid duplicate party rows.

- [x] Implement RT save/writeback orchestration. (AC: 16-27)
  - [x] Add `IInnolaRtExaminationService` or equivalent façade that orchestrates Plan neighbor writeback, Save & Close Plan Check approval, and lifecycle completion.
  - [x] Save Plan neighbor/party data to the current RT transaction, not the originating PE transaction.
  - [x] Preserve Plan `checkList` during normal `Save`; apply final Plan Check completion values during `Save & Close` without replacing Innola's `plan_check_type_*` enum key with a literal status.
  - [x] Do not create or update spatial geometry or SpatialUnit attributes.
  - [x] Stop immediately on the first writeback failure and record `working/rt_examination_api_failure.json`.
  - [x] On success, record `working/rt_examination_api_request.json`, `working/rt_examination_api_response.json`, and lifecycle audit entries.
  - [x] Show final confirmation before save/complete; after success, show completion message, cleanup RT-loaded layers, close workspace, and refresh transaction list.
  - [x] Add explicit post-RT gateway choices to the form so `Save & Close` sends either `Review Completed RT Examination` or `Prepare Plan Pre-Check Log Sheet` instead of relying on a default transition.
  - [x] Do not attach a new RT report in this story.

- [x] Add focused tests. (AC: 1-29)
  - [x] Settings parser/default tests for RT stage and transition settings.
  - [x] Transaction panel routing tests for First Registration, NewCT, and arbitrary transaction type at `In RT Examination`.
  - [x] Exact selected task id tests for duplicate transaction-number rows.
  - [x] Mock HTTP tests for current Plan lookup, Plan.planNumber extraction, portal search PE resolution, no-match/multi-match failures, `plan/sources/latest`, and `plan/spatialunits/latest`.
  - [x] Service tests for Plan body-shape preservation, neighbor/party duplicate prevention, allowed role values, read-only Spatial Units, and Save & Close Plan Check approval.
  - [x] SpatialUnit summary tests for parcel rows from linked PE/working_review data without SpatialUnit writeback.
  - [x] ViewModel tests for editable non-spatial values, confirmation cancel, success message, cleanup, close, and retry-safe failure behavior.
  - [x] Secret-redaction tests for all failure artifacts/diagnostics.

## Developer Context

### Reuse First

- Reuse Story 7.11 `InnolaPlanCheckService` behavior for Plan route compatibility, full JSON preservation, Plan `checkList`, and `neighbors` writeback. Do not fork a second incompatible Plan serializer.
- Reuse Story 7.9 `InnolaSpatialUnitService` patterns only for safe SpatialUnit/latest read behavior, JSON preservation in artifacts, auth, client certificate, and failure diagnostics; RT Examination must not branch or save SpatialUnit objects.
- Reuse `InnolaTransactionLifecycleService` transition discovery/completion through `InnolaTransactionLifecycleRequest.DesiredTransitionName`; do not POST workflow completion directly from a ViewModel.
- Reuse `TransactionPanelState` and `ParcelWorkflowStageRouter` stage-routing patterns from Compare/PLA_B/Fabric instead of creating a separate transaction list.
- Reuse ArcGIS map/layer service seams and `QueuedTask.Run` patterns. ViewModels must not manipulate ArcGIS map state directly.

### UX Mockup

RT Examination should open as a dedicated ArcGIS Pro `ProWindow`, closer to `CompareWorkspaceWindow` than to the smaller Fabric Maintenance confirmation window. The examiner needs one work surface for linked PE context, non-spatial edits, save, completion, and cleanup.

```text
RT Examination

Transaction No: 100000xxx        Stage: In RT Examination        Status: Ready / Dirty / Saving
Current Type: First Registration  PE Plan No: 100000yyy           Originating PE: Resolved / Not resolved

[Load Linked TR Data] [Save and Close] [Suspend] [Cancel]

Tabs:
  Context | Neighbors / Parties | Spatial Units | Plan Check | Sources / Map Evidence
```

The first screen should land on `Context` after launch. If linked transaction data has not loaded yet, show the blocking reason and keep `Save and Close` disabled. Once loaded, the status row should show current RT Plan, originating PE transaction, latest source count, latest spatial-unit count, and working-review geometry load status.

`Neighbors / Parties` should be the primary editing tab:

```text
Role            Name        Address      Volume   Folio   Lot   LandVal No.   Exam No.
Neighbor        ...
Owner           ...
Occupier        ...
Representative  ...
```

Use a constrained combo for `Role` with `Neighbor`, `Owner`, `Occupier`, and `Representative`. All listed fields are editable. Preserve unknown fields from the fetched Plan object and avoid adding duplicate rows on retry/reload.

`Spatial Units` should expose read-only parcel summary rows. Initially show `parcel_name`, `area_sqr`, `suid`, and `created_utc`; geometry fields, coordinate arrays, point references, boundary fields, and other SpatialUnit attributes must not be editable or written by RT Examination.

`Plan Check` should show supported checklist rows and observations with compact read-only controls. Normal `Save` preserves Plan Check values unchanged. `Save & Close` applies the final completion signal automatically by preserving/using a valid Innola `plan_check_type_*` key, setting `passed = true`, and writing description `Updated from ArcGIS Pro TR {transaction_number}.`; it should not create or attach a report in this story.

`Sources / Map Evidence` should show the linked transaction/originating PE sources, working-review query key (`Plan.planNumber`), loaded map group/layer names, and warnings. This is additional information only; no embedded map preview is needed because ArcGIS Pro's active map is the companion surface.

Completion behavior:

- `Save` confirms and writes Plan neighbor/related values only to the linked/current RT transaction, then leaves the RT workspace open.
- `Save and Close` confirms, performs the same Plan neighbor/related-value save, applies the final Plan Check completion signal to the linked/current RT transaction, completes the RT task using the selected RT Result transition, refreshes the transaction list, cleans RT-loaded map layers, and closes the workspace.
- On success, show a completion message, remove only RT-loaded map layers/groups, clear RT workspace state, refresh the transaction list, and close the window.
- On failure, keep the workspace open, preserve edits, write safe diagnostics, and leave layers loaded for retry.
### API Contract From RT Reference

```http
GET /api/v4/rest/workflow/my-tasks
POST /api/v4/rest/application/my-tasks/search
GET /api/v4/rest/workflow/tasks/{taskId}
GET /api/v4/rest/data/objects?typeKeyId=plan&transactionId={currentRtTransactionId}
POST /api/v4/rest/portal/searches
GET /api/v4/rest/plan/sources/latest?planTransactionId={originatingPePlanTrId}&transactionId={currentRtTransactionId}
GET /api/v4/rest/plan/spatialunits/latest?planNumbers=[{planNumber}]
PUT /api/v4/rest/data/objects?typeKeyId=plan&transactionId={currentRtTransactionId}
GET /api/v4/rest/workflow/tasks/{taskId}/transitions
POST /api/v4/rest/validation/tasks/{taskId}/transition-check
POST /api/v4/rest/workflow/tasks/{taskId}/complete?transition={key}
```

The current linked RT transaction is the writeback target. The main transaction row with the same normalized transaction number supplies the supporting documents copied into the Case Folder. Linked RT/PE object data remains separate: the current RT transaction and its Plan provide neighbors/additional RT review data; `Plan.planNumber` is the linked TR/PE number for originating PE lookup, latest sources/spatial units, and `working_review` additional map evidence.

### Data Boundaries

- Editable: non-spatial neighbor/party data and comparison observations where supported.
- Automatically updated on `Save & Close`: one current RT Plan Check row is selected/created, its valid `plan_check_type_*` key is preserved or defaulted to `plan_check_type_general`, and `passed` plus `description` are updated.
- Read-only: Spatial Unit parcel summary fields `parcel_name`, `area_sqr`, `suid`, and `created_utc`.
- Not editable: spatial geometry, coordinate arrays, ArcGIS final cadastre geometry, Enterprise authoritative layers, CADMAP, CADINDEX, and Parcel Fabric authoritative targets.
- `Plan.planNumber` is the linked TR/PE number and the key for `working_review` geometry lookup.
- `Plan.trId` used for `plan/sources/latest` must come from the originating PE Plan once that Plan is resolved.
- If the live API reveals the current RT Plan already carries enough originating PE metadata to avoid an additional PE Plan lookup, keep the adapter flexible but preserve the audit fields that show which source produced the result.

### Suggested Files To Inspect Before Dev

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/FabricMaintenance/FabricMaintenancePromotionServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaPlanCheckServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Implementation Guardrails

- Keep all HTTP calls behind Innola services with `InnolaHttp.BuildUri`, `InnolaHttp.ApplyAuthHeaders`, `InnolaApiResilience`, and configured certificate behavior.
- Preserve all unknown JSON fields in Plan, Neighbor, and SpatialUnit objects.
- Write local artifacts before/after remote writes so failures are diagnosable and retryable.
- Do not mark local transaction complete until all configured RT save steps succeed.
- Do not upload or attach an RT report in this story.
- If the exact next RT transition label is not known from settings or diagrams, rely on transition discovery and record the selected transition; do not hardcode a guessed stage label.
- Use `StringComparer.OrdinalIgnoreCase` for stage, role, source type, and transaction type matching.
- Keep WPF operational and dense. Avoid marketing/hero layout. Use grids, tabs, combo boxes, and clear status text consistent with existing Compare/Fabric surfaces.
- Add tests in the existing executable harness. Do not introduce xUnit/NUnit.

## Testing Notes

Run focused tests first:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"
```

Then run related regression slices:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "innola plan check" "spatial unit" "transaction panel"
```

Package only after focused and related tests pass:

```powershell
tools/package_addin.ps1 -Configuration Release
```

## Open Questions

- Exact next-stage label after `In RT Examination` should be validated from the added `FirstRegistration.png` and, more importantly, from live `GET /workflow/tasks/{taskId}/transitions`. This is not blocking if the implementation records transition metadata and avoids hardcoding a guessed label.
- Confirm whether the live Plan property is spelled `neighbors`, `neighbor`, or `neighbours` for RT. Story 7.11 uses `neighbors`; this story requires the property name to be isolated so a live correction is low risk.
- Confirm whether live Innola requires an approved Plan Check dictionary code other than the string value `approved`; current repo code treats `checkType` as string and acceptable as boolean payload field `passed`.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Story Creation Notes

- Created from user-provided RT Examination Word/API reference and workflow images in `_bmad-output/planning-artifacts/rt-examination`.
- Mary/Winston review conclusion: implement as one end-to-end story but keep routing, linked PE lookup, editable review state, Plan writeback, read-only Spatial Unit context, and lifecycle completion behind separate services.
- Story is assigned to Epic 8 because RT Examination is comparison/reconciliation work over linked PE data and should reuse Compare transaction routing/lifecycle patterns.

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination" "transaction panel"` - RT and transaction-panel tests progressed through the new RT APP-profile regression; run later stopped on an existing toolbar command-state assertion unrelated to the RT load patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests, including RT selected stage with live detail case/profile `APP`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000854` inspection - fresh case manifest exists but has no `innola_transaction`, no `workflow_profile`, no detected profile, no source files, and no working RT artifacts. This confirms the no-form symptom happens before RT workspace context is successfully persisted.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests after TR `100000854` no-form diagnostics patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors after TR `100000854` no-form diagnostics patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment upload"` - PASS 9 tests after shared attachment filename normalization.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.386` after attachment filename normalization.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment"` - PASS 21 tests, including download `documentName` leaf-name normalization and upload leaf-name normalization.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "transaction load"` - PASS 21 tests, including absolute/path-shaped attachment metadata copied inside the Case Folder by leaf name and traversal still blocked.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.388` after TR `100000854` attachment download/local-copy filename normalization.

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "transaction panel rt examination stage starts"` - PASS 1 test after RT start now loads support documents from the non-RT main transaction row, then restores the selected RT task for lifecycle/workspace launch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment" "transaction load"` - PASS 36 tests after RT source-row routing patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 8 tests, including new main-vs-RT transaction split regression.
- Parallel `dotnet run` of RT plus attachment/load slices produced transient WPF `*_wpftmp.csproj` generated XAML symbol errors; rerunning the same RT slice sequentially passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 9 tests after `Save and Close` became the terminal save/complete/close action.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment" "transaction load"` - PASS 36 tests after confirming attachment filename and transaction-load regressions.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false` - Build succeeded with existing platform analyzer warnings and 0 errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "in-progress second row"` - PASS 1 test proving the selected second-row `100000854 - In RT Examination` case downloads attachments from the main non-RT row, reopens the already-in-progress RT task without a second claim, opens the RT UX, and opens Supporting Documents.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "transaction panel rt examination"` - PASS 3 tests after second-row in-progress reopen patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment" "transaction load"` - PASS 36 tests after second-row in-progress reopen patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false` - Build succeeded with existing platform analyzer warnings and 0 errors.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.393`.

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "scanning source attachment download strips path" "in-progress second row"` - PASS 2 tests proving the selected second-row RT reopen still loads main-row documents and the `scanning/source/{id}/body` fallback sends only a leaf `documentName`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "transaction panel rt examination" "attachment" "transaction load"` - PASS 40 tests with TEMP/TMP redirected to workspace after the first run hit an AppData temp ACL issue in a resume-package test.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false` - Build succeeded with existing ArcGIS platform analyzer warnings and 0 errors.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.395`.



- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "in-progress second row"` - PASS 1 test after adding the same-active RT row reopen path; OpenTask can now re-run main-document load and reopen RT UX/Supporting Documents instead of being disabled.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "transaction panel rt examination" "attachment" "transaction load"` - PASS 40 tests after the no-op patch.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.402`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "rt examination"` - PASS 12 tests after RT save was corrected to write Neighbor rows to the current RT Plan object while leaving the linked/originating transaction untouched.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.455`.
- Live TR `100000854` retry - Neighbor create-template request `{"@c":"Neighbor","id":null}` succeeded, then Plan save failed with `400 Failed to read request` because the outbound Plan graph contained duplicate transient `@id` aliases.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "rt examination"` - PASS 12 tests after outbound Plan save payload alias normalization.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.458`.
- Live TR `100000854` retry - RT Examination writeback succeeded after transient `@id` alias normalization.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "rt examination"` - PASS 13 tests after synchronizing the RT window chrome close button with the ViewModel `CancelCommand`.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.460`.
- Code review pass on 2026-09-08 found one close-button edge case: a sticky chrome-close guard could suppress later cancel prompts after the user declined the first prompt. The guard was removed and the RT focused suite remained green.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.461` with the code-review close-button fix included.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "rt examination" "remember me" "lifecycle complete"` - PASS 28 tests after adding RT gateway branch selection, desired-transition alias matching, fail-closed transition behavior, and username-only login preference storage.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false` - Build succeeded with existing ArcGIS platform analyzer warnings and 0 errors after RT branch and login preference patches.



### Completion Notes

- Added RT Examination stage routing that is stage-driven and not limited by main transaction type.
- Added RT Examination ProWindow with Context, Neighbors / Parties, Spatial Units, Plan Check, and Sources / Map Evidence tabs.
- Added Innola RT service for current Plan lookup with fallback, `Plan.planNumber` PE resolution, source/spatial-unit latest reads, neighbor/party writeback, read-only Spatial Unit context, Save & Close Plan Check approval, lifecycle completion, safe diagnostics, and artifact persistence.
- Wired `working_review` geometry loading through the existing ArcGIS Compare map integration seam using `rt_examination.working_review_pe_number_field = Plan.planNumber`, with loaded map group cleanup after successful completion.
- Added focused tests for RT settings, stage routing, allowed roles, geometry edit exclusion, deterministic party dedupe, XAML surface, and transaction-panel RT launch for First Registration-style rows.
- Patched transaction loading for case `100000854` shape: when the selected row is exactly `In RT Examination`, the loader no longer rejects live detail metadata reporting `APP`; it creates an RT review profile and permits an attachment-free current RT case so linked PE data can load from the RT workspace.
- Preserved exact selected task id/transaction number/process-step matching so the duplicate `100000854 - Assign Legal Officer` row is not treated as RT.
- Patched the RT workspace launcher to avoid silent returns: if selected transaction state is missing, transaction numbers do not match, or the loaded case folder is absent, the add-in now shows a specific `RT Examination - {transactionNumber}` warning instead of doing nothing.
- Added guarded RT load/save exception handling so linked PE/API/file failures surface in the RT window status text instead of closing or hiding the reason.
- Patched shared Innola attachment upload to strip local folder paths from multipart file names. This fixes the RT/Fabric-style failure `attachment file name must not contain a path` while preserving downloads into the current/main transaction case folder.
- Patched shared Innola attachment download and RT/current transaction case-folder copy to strip absolute/path-shaped metadata to the leaf filename. This addresses TR `100000854` no-form progress where the case manifest remained at intake because source attachment download/copy failed before RT workspace creation.
- Preserved path traversal blocking for unsafe relative attachment names such as `..\escape.pdf`.
- Validated and patched the corrected two-context RT contract: supporting documents are downloaded from the non-RT main transaction row, while selected RT transaction state remains active for lifecycle, workspace launch, neighbors/additional data, and writeback.
- Updated RT Examination UX behavior so `Save and Close` is the final save/complete action: it writes back to the linked/current RT transaction, completes the task, refreshes transactions, cleans RT-loaded map layers, and closes the workspace.
- Clarified `Plan.planNumber` as the linked TR/PE number for originating transaction lookup and `working_review` additional spatial evidence.
- Added a `100000854` regression proving the main transaction attachment is copied by leaf filename into the Case Folder and the manifest lifecycle still points at the selected RT task.
- Patched the selected-second-row live case: when `100000854 - In RT Examination` is already `In Progress` for the current user/group, RT start downloads support documents from the non-RT main transaction row, restores the selected RT row as the active task, opens the RT workspace, and opens Supporting Documents without trying to claim the already-started task again.

- Patched the remaining live attachment route: `scanSourceId` downloads through `/api/rest/scanning/source/{id}/body` now include sanitized leaf-only `documentName`, preventing Innola from seeing a local case-folder path such as `C:\Users\...\ParcelWorkflowCases\100000854\...`.
- Added early RT start trace logging to the case folder at `working\rt_examination_start_trace.json`, so the selected RT row, main supporting-document row, expected document/data sources, and lookup endpoints are written before attachment download can fail.
- Added linked transaction/object-field logging to `working\rt_examination_linked_transaction_log.json` after the RT workspace load resolves the current Plan, `Plan.planNumber`, originating PE transaction, latest sources, latest spatial units, and `working_review` query field/value.



- Patched the no-op path Winston identified: when the selected second-row RT task is already the active transaction, `StartTransactionCommand` is enabled only for that same RT task and reopens the RT workflow instead of returning before trace/file creation.
- Patched RT neighbor writeback for the live main/current Plan object contract: the selected/current RT Plan is the only write target, empty current `neighbors` arrays are populated with Innola-created Neighbor child objects, and the linked/originating PE transaction remains read-only source context.
- Patched RT administrative Plan save payloads to preserve the response body shape and normalize transient `@id` aliases across the outbound object graph. This fixed the live `400 Failed to read request` writeback failure on TR `100000854`.
- Patched RT window chrome close behavior so the `X` button routes through the same confirmation, cleanup, transaction refresh, and close path as the Cancel/Close command.
- Patched RT `Save & Close` to require a selected RT result branch and pass the selected target stage into Innola lifecycle completion: `Review Completed RT Examination` for proceed-next, or `Prepare Plan Pre-Check Log Sheet` for pre-check log sheet preparation. The selected label, gateway outcome, and target stage are written to RT local artifacts.
- Patched live RT `100000983` writeback diagnosis: Innola rejected the Plan save before workflow transition because the payload used literal `checkType = approved`. `Save & Close` now preserves valid `plan_check_type_*` values, prefers `plan_check_type_general`, and only then sets `passed = true` plus the ArcGIS Pro completion description before invoking the selected combo transition.

## Code Review

- Review date: 2026-09-08.
- Scope: Story 8.8 RT Examination writeback, Spatial Units read-only display, Plan Check Save & Close approval, window close synchronization, tests, and package version updates through add-in `1.1.461`.
- Finding fixed: RT window chrome close used a sticky guard that could suppress later close/cancel attempts after a declined confirmation. The guard was removed; `CanClose`/`IsBusy` now gates repeated close attempts.
- Remaining findings: none in the focused RT Examination scope.
- Verification after review: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "rt examination"` passed 13 tests.


- Patched the no-op path Winston identified: when the selected second-row RT task is already the active transaction, `StartTransactionCommand` is enabled only for that same RT task and reopens the RT workflow instead of returning before trace/file creation.

### File List

- `_bmad-output/implementation-artifacts/8-8-add-rt-examination-linked-pe-review-and-writeback.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleRequest.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RtExaminationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RtExaminationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/RtExamination/RtExaminationModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/RtExamination/InnolaRtExaminationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/RtExaminationTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaAuthServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-09-04 | 1.0 | Created RT Examination story covering stage routing, linked PE load/review, non-spatial edits, Plan/neighbors/SpatialUnit save, lifecycle completion, and cleanup. | Mary / Winston / Codex |
| 2026-09-04 | 1.1 | Implemented RT Examination routing, workspace, Innola linked-PE load/writeback, working_review map load/cleanup seam, and focused regression tests. | Amelia / Codex |
| 2026-09-04 | 1.2 | Fixed path-shaped Innola attachment metadata handling for TR `100000854` download and current case-folder copy; preserved traversal blocking. | Amelia / Codex |
| 2026-09-06 | 1.3 | Corrected RT start split so support documents load from the main transaction row while RT lifecycle/workspace/data stays bound to the selected RT task. | Mary / Amelia / Codex |
| 2026-09-06 | 1.4 | Clarified linked TR/Plan.planNumber contract and made Save and Close the terminal RT save/complete action. | Mary / Amelia / Codex |
| 2026-09-06 | 1.5 | Fixed selected second-row `In RT Examination` reopen for already-in-progress TR `100000854` and packaged add-in version `1.1.393`. | Amelia / Codex |
| 2026-09-06 | 1.6 | Fixed remaining scanning-source download path leak for live TR `100000854`, added case-folder RT start trace plus linked transaction/object field logs, and packaged add-in version `1.1.395`. | Amelia / Codex |
| 2026-09-06 | 1.7 | Fixed same-active RT row OpenTask no-op so already-active TR `100000854` can reopen RT UX/supporting documents and regenerate start trace; packaged add-in version `1.1.397`. | Winston / Amelia / Codex |
| 2026-09-06 | 1.8 | Fixed the transaction list being disabled while the main task was active, preventing selection of the same-number `In RT Examination` row; packaged add-in version `1.1.402`. | Winston / Amelia / Codex |
| 2026-09-08 | 1.9 | Completed live RT Examination writeback: current RT Plan is the write target, Neighbor rows are created/appended safely, Spatial Units are read-only, Plan Check approval completes on Save & Close, transient `@id` aliases are normalized for Innola, the window `X` routes through Cancel, focused review passed, and add-in version `1.1.461` was packaged. | Mary / Amelia / Codex |
| 2026-09-08 | 2.0 | Added explicit RT Save & Close gateway branch selection so Innola completion requests either `Review Completed RT Examination` or `Prepare Plan Pre-Check Log Sheet`, with branch evidence recorded in RT artifacts. | JotaPe / Codex |
| 2026-09-08 | 2.1 | Validated live RT `100000983` writeback failure and corrected PlanCheck completion payload to keep Innola `plan_check_type_*` enum values instead of sending literal `approved`; clarified Save versus Save & Close semantics. | JotaPe / Codex |


