---
baseline_commit: handoff-2026-07-07
---

# Story 7.11: Write Innola Plan Examination Details On Compute Finalize

Status: done

## Story

As a cadastral examiner completing Compute review,
I want the Finalize process to update the Innola Plan Examination values for the transaction,
so that the Innola framework Plan Examination screen reflects the same Closure, Area, Plotting, Details, Neighbor, and related review outcomes that were produced by the ArcGIS Pro Compute workflow.

## Business Context

The Compute workflow now creates Enterprise working-layer rows and Innola Spatial Unit records during Finalize. The remaining Innola closeout gap is the Plan Check table visible in the Innola transaction detail UI.

The user confirmed the target UI on transaction `100000379`, where the Plan Check table contains rows such as `Closure`, `Area`, `Comparison Plan & Data Sheet`, `Plotting`, `Notices`, `Details (Tape Check)`, `General (other info)`, and `Adjoining Plans (Comparator's Report)`. These values currently remain `N/A` even when the ArcGIS Pro workflow has produced stage findings and accepted geometry.

The Postman evidence in `C:\Users\js91482\Downloads\Sidwell Plan Exam Scenario.postman_collection.json` confirms the plan-check API pattern:

- `GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`
- `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`
- The returned/saved object is a full `Plan` object array.
- Each `Plan` contains `checkList[]` rows of class `PlanCheck`.
- Each `PlanCheck` row contains `checkType`, `passed`, and `description`.

A read-only live probe with the configured client certificate reached Innola but returned `401 Unauthorized`, which is expected without the active Innola `Access-Token`. Therefore this story must be implemented inside the add-in using the active `InnolaSession`, not as an external unauthenticated script.

This story is a closeout extension to Story 7.9. It must run after the Compute examination report can be generated from saved stage findings and before final Innola task completion. It must not write to authoritative GIS layers, CADMAP, CADINDEX, or Enterprise Parcel Fabric.

### Amendment: Neighbor Rows In Plan Examination

The 2026-09-03 transaction `100000622` review confirmed that the Innola Plan Examination `Neighbors` tab uses objects of class `Neighbor`. A captured create-template request returned a blank Neighbor object from:

```http
POST /api/v4/rest/data/objects/create
```

Observed returned object shape:

```json
{
  "@c": "Neighbor",
  "@id": "obj:1",
  "versionRev": 0,
  "id": "01a0692d-b209-7975-9eca-5c903b962f77",
  "neighborType": "neighbor_type_owner",
  "name": null,
  "address": null,
  "volume": null,
  "folio": null,
  "lot": null,
  "landValNumber": null,
  "examNumber": null,
  "allowRead": true,
  "allowWrite": true
}
```

The same API notes indicate Plan Examination save uses the newer data-object route:

- `GET /api/v4/rest/data/objects?typeKeyId=plan&transactionId={transactionId}`
- `PUT /api/v4/rest/data/objects?typeKeyId=plan&transactionId={transactionId}`
- Body: `Plan` single object or `Plan[]`
- Plan payload should include populated `checkList` and `neighbors`.

Review decision on 2026-09-03: proceed with `neighbors[]` on the Plan payload and treat `/api/v4/rest/data/objects/create` as a Neighbor template/default-object endpoint. The implementation preserves the fetched Plan save body shape as `Plan` or `Plan[]` while this contract is validated in live testing.

### Amendment: Plan Route Compatibility For Returned Compute Tasks

The 2026-09-03 transaction `100000623` retry showed that the data-object Plan route can fail with HTTP 500 for both the Innola UUID transaction id and the displayed transaction number. The failure happened during Plan GET before `plan_examination_api_request.json` was written, so the evidence does not support Neighbor data as the cause.

Implementation decision on 2026-09-03: keep the newer data-object route as the primary Plan Examination path, but fall back to the original Story 7.11 administrative Plan route when the data-object route returns no Plan or fails. Save must use the same route family that returned the Plan: `PUT` for `data/objects`, `POST` for `administrative/ladm-objects`.

Follow-up decision on 2026-09-03: the administrative fallback must try both the Innola UUID transaction id and the displayed transaction number. Spatial Unit save succeeds through the administrative API using the UUID, so Plan lookup must not skip that combination.

Follow-up decision on 2026-09-03: the Neighbor create-template endpoint is opportunistic only. If `POST /api/v4/rest/data/objects/create` rejects the minimal `Neighbor` template request, the add-in builds a local nested `Neighbor` object from the confirmed field contract and lets the final Plan save validate the embedded `neighbors[]` payload.
eighbors[] payload.

## Acceptance Criteria

1. Given an active Innola session and a transaction with `TransactionId`, when Finalize reaches Innola Plan Check writeback, then the add-in fetches the transaction Plan object using `GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`.
2. Given the Plan API returns one or more Plan objects, when a Plan object contains `checkList[]`, then the add-in updates only recognized `PlanCheck` rows and preserves all existing API-generated Plan fields, ids, uid, link, version, registered surveyor, and unknown fields.
3. Given the Plan API returns no Plan object, an empty checklist, malformed JSON, or no matching transaction Plan, when Finalize runs, then Finalize stops before package upload and task completion with a clear non-secret diagnostic.
4. Given a Plan Check row has `checkType = plan_check_type_closure`, when Dimension/Validate Points findings indicate closure passed without blockers, then `passed = true` and `description` summarizes the closure outcome; when blockers remain, `passed = false` and the description summarizes the blocker.
5. Given a Plan Check row has `checkType = plan_check_type_area`, when output polygons/area summaries are available and no area blocker exists, then `passed = true` and `description` summarizes the area source/count/total where available; when area evidence is missing or blocked, `passed = false` with a diagnostic description.
6. Given a Plan Check row has `checkType = plan_check_type_compplan_datasheet`, when Supporting Document and Structure Check confirm the computation sheet is present and used as the primary source, then `passed = true`; otherwise `passed = false` with the missing/source-role explanation.
7. Given a Plan Check row has `checkType = plan_check_type_plotting`, when Create Spatial Units and Final Review are complete and Enterprise working geometry publish/disposition succeeded, then `passed = true`; otherwise `passed = false` with the missing stage explanation.
8. Given a Plan Check row has `checkType = plan_check_type_notices`, when no notices-specific rules exist, then the row is preserved as `passed = null` or left unchanged according to API behavior, and the description says no automated notice check was performed only if product chooses to report that explicitly.
9. Given a Plan Check row has `checkType = plan_check_type_details`, when bearing, distance, point-reference, closure, and dimension findings are acceptable, then `passed = true`; otherwise `passed = false` with the relevant dimension/detail finding summary.
10. Given a Plan Check row has `checkType = plan_check_type_general`, when Final Review is approved and closeout prerequisites have passed, then `passed = true` with a description referencing the Compute examination report; otherwise `passed = false`.
11. Given a Plan Check row has `checkType = plan_check_type_adjoining`, when official comparison/CADMAP-CADINDEX comparison evidence exists and passes, then `passed = true`; when the workflow has not implemented an adjoining/comparator report rule, then preserve the row as N/A/unchanged and make this limitation visible in local diagnostics.
12. Given Plan Check values are updated, when the add-in saves them, then it posts the full updated Plan object array back to `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}` using the active Innola session access token and client certificate behavior already used by the app.
13. Given the Plan Check POST succeeds, when local artifacts are written, then `working/plan_check_api_request.json` and `working/plan_check_api_response.json` are created with non-secret request/response evidence.
14. Given the Plan Check POST fails because of authorization, validation, network, or business-rule errors, when Finalize runs, then no later closeout step runs, the transaction is not marked complete, the working package is not uploaded as final, and a non-secret diagnostic identifies Plan Check writeback as the blocker.
15. Given Plan Check writeback succeeds, when Finalize continues, then the closeout order is: Enterprise publish/disposition, Innola Spatial Unit create/update, Enterprise SUID references, Compute examination report, Innola Plan Check writeback, working package zip/upload, Innola task complete.
16. Given the case is reopened after a successful Plan Check writeback, when local artifacts are inspected, then the latest Plan Check writeback status, updated check types, transaction id, operator, timestamp, and report reference are recoverable.
17. Given diagnostics or report artifacts are written, then no Innola access token, portal token, password, raw certificate material, or full sensitive service response is logged.
18. Given automated tests run, then the Plan Check service is covered for GET/POST success, missing Plan, missing checklist, per-check mapping, preservation of unknown Plan fields, failure short-circuit behavior, and non-secret diagnostics.
19. Given reviewed adjacent owner/neighbor data exists, when Finalize reaches Innola Plan Examination writeback, then the add-in reads the saved reviewed extraction artifact and selects only adjacent owner rows whose reviewed `Role` is `Neighbor`, using case-insensitive role matching.
20. Given selected Neighbor rows exist, when the add-in builds Innola Neighbor objects, then it maps reviewed fields to the Innola Neighbor object fields: `Name -> name`, `Address -> address`, `Volume -> volume`, `Folio -> folio`, `Lot Number -> lot`, `LandVal No. -> landValNumber`, and `Exam No -> examNumber`.
21. Given the Innola Neighbor object requires a type, when the source row role is `Neighbor`, then `neighborType` defaults to `neighbor_type_owner` unless Innola confirms a more precise controlled value for adjacent-owner versus neighbor rows.
22. Given the Plan object already contains `neighbors[]`, when Neighbor writeback runs, then the add-in preserves existing unknown Neighbor fields and avoids creating duplicates for the same reviewed neighbor according to a deterministic match key using available name, volume, folio, lot, land valuation number, and examination number.
23. Given the Plan object has no `neighbors[]`, when Neighbor writeback runs and the API contract confirms that `neighbors` is the correct property, then the add-in creates the `neighbors` array and appends populated Neighbor objects from reviewed rows.
24. Given the Plan Examination payload is saved, when the API contract is confirmed, then the add-in uses the active Innola session against `PUT /api/v4/rest/data/objects?typeKeyId=plan&transactionId={transactionId}` and preserves all existing Plan fields, ids, uid, link, version, registered surveyor, `checkList`, existing neighbors, and unknown fields.
25. Given the Neighbor create-template endpoint is needed for valid object defaults, when Finalize prepares new Neighbor rows, then the add-in may call `POST /api/v4/rest/data/objects/create` to obtain a default `Neighbor` object before populating fields.
26. Given Neighbor writeback succeeds, when local artifacts are written, then `working/plan_examination_api_request.json` and `working/plan_examination_api_response.json` include non-secret Plan Check and Neighbor writeback evidence.
27. Given Neighbor writeback fails because of authorization, validation, network, malformed contract, or business-rule errors, when Finalize runs, then no later closeout step runs, the transaction is not marked complete, the working package is not uploaded as final, and a non-secret diagnostic identifies Plan Examination Neighbor writeback as the blocker.
28. Given automated tests run after this amendment, then coverage proves data-object route construction, optional Neighbor template creation, reviewed-neighbor filtering, field mapping, duplicate prevention, existing Plan/neighbor preservation, request/response evidence, and lifecycle short-circuit behavior.

## Tasks / Subtasks

- [x] Add an Innola Plan Check service seam. (AC: 1-3, 12-14, 17)
  - [x] Add `IInnolaPlanCheckService` and `InnolaPlanCheckService` near the existing Innola API services.
  - [x] Use the existing `InnolaHttp.BuildUri(...)` and `InnolaHttp.ApplyAuthHeaders(...)` helpers.
  - [x] Keep the endpoint isolated behind the service:
    - `GET {V4RestPath}administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`
    - `POST {V4RestPath}administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`
  - [x] Preserve full `JsonObject` Plan payloads instead of replacing them with narrow DTOs.
  - [x] Treat missing transaction id, unauthorized responses, non-success responses, invalid JSON, empty Plan arrays, and missing `checkList` as explicit failures.

- [x] Map Compute evidence to Innola Plan Check rows. (AC: 4-11)
  - [x] Read saved case artifacts, not recomputed stage outcomes.
  - [x] Use `output/reports/compute_examination_report.json` as the preferred summary input once it exists.
  - [x] Fall back only to current saved stage artifacts when needed:
    - `working/structure_check_summary.json`
    - `working/georeference_check_summary.json`
    - `working/dimension_check_summary.json`
    - `working/validation_summary.json`
    - `output/output_summary.json`
    - `output/enterprise_working_publish.json`
    - `working/enterprise_working_disposition.json`
  - [x] Implement stable mapping for:
    - `plan_check_type_closure`
    - `plan_check_type_area`
    - `plan_check_type_compplan_datasheet`
    - `plan_check_type_plotting`
    - `plan_check_type_notices`
    - `plan_check_type_details`
    - `plan_check_type_general`
    - `plan_check_type_adjoining`
  - [x] Keep unsupported/not-yet-automated checks as N/A/unchanged unless product explicitly chooses to set `passed = false`.
  - [x] Keep descriptions concise enough for the Innola Plan Check table but useful for examiner audit.

- [x] Persist local evidence artifacts. (AC: 13, 16-17)
  - [x] Write `working/plan_check_api_request.json` before POST.
  - [x] Write `working/plan_check_api_response.json` after POST.
  - [x] Include transaction id/number, task id, operator, UTC timestamp, updated check types, old/new values when safe, and report reference.
  - [x] Redact tokens, passwords, certificate material, and unbounded raw response bodies.
  - [x] Include Plan Check status in `workflow_lifecycle_audit.json` and the disposition artifact where appropriate.

- [x] Insert Plan Check writeback into Finalize ordering. (AC: 14-15)
  - [x] Update `InnolaTransactionLifecycleCoordinator.CompleteAsync(...)` so Plan Check writeback runs after Compute examination report generation and before working package upload.
  - [x] Stop Finalize before package upload and task completion when Plan Check writeback fails.
  - [x] Ensure retry behavior does not recreate Spatial Units when Spatial Unit API status is already saved.
  - [x] Ensure retry can attempt Plan Check writeback again using existing local disposition/spatial-unit evidence.

- [x] Add settings/configuration only where necessary. (AC: 1, 12)
  - [x] Prefer hard-isolated constants in the service seam for `typeKeyId=plan` unless the project needs runtime settings.
  - [x] If settings are added, expose them in `WorkflowSettings.json` and Settings Workspace without storing credentials.

- [x] Add automated tests. (AC: 1-18)
  - [x] Unit-test GET/POST path and query construction.
  - [x] Unit-test auth header use through `InnolaHttp.ApplyAuthHeaders`.
  - [x] Unit-test preservation of unknown fields and nested Plan/link/surveyor fields.
  - [x] Unit-test each known Plan Check type mapping.
  - [x] Unit-test missing Plan/checkList failures.
  - [x] Unit-test non-success POST failure stops lifecycle before package upload and task completion.
  - [x] Unit-test successful Plan Check writeback appears in lifecycle audit/disposition artifacts.
  - [x] Unit-test diagnostics do not contain access tokens or sensitive raw payloads.

- [x] Amend Plan Examination API route for data objects. (AC: 1-3, 12, 19, 24, 27)
  - [x] Confirm whether the Plan GET/PUT route should replace or coexist with the older `administrative/ladm-objects` route.
  - [x] Fetch Plan using `GET /api/v4/rest/data/objects?typeKeyId=plan&transactionId={transactionId}` when the data-object contract is active.
  - [x] Save Plan using `PUT /api/v4/rest/data/objects?typeKeyId=plan&transactionId={transactionId}`.
  - [x] Support the confirmed response/save body shape: `Plan` single object or `Plan[]`.
  - [x] Fall back to `GET/POST /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}` when the data-object Plan route fails or returns no Plan for returned Compute tasks.
  - [x] Try the administrative Plan route with the Innola UUID before falling back to the displayed transaction number, matching the successful Spatial Unit administrative route pattern.

- [x] Populate Innola Plan `neighbors[]` from reviewed Neighbor rows. (AC: 19-25)
  - [x] Read saved reviewed extraction data from `working/extraction_review_data.json`.
  - [x] Select only adjacent owner rows whose reviewed role is `Neighbor`.
  - [x] Map `name`, `address`, `volume`, `folio`, `lot`, `landValNumber`, and `examNumber` from the reviewed row values.
  - [x] Default `neighborType` to `neighbor_type_owner` until Innola confirms additional controlled values.
  - [x] Preserve existing neighbor objects and avoid duplicate rows on retry/finalize rerun.
  - [x] Use `POST /api/v4/rest/data/objects/create` for default Neighbor object creation if required by the live API contract.
  - [x] Fall back to a locally constructed nested Neighbor object when the optional create-template endpoint rejects the request before Plan save.

- [x] Persist Plan Examination writeback diagnostics. (AC: 26-27)
  - [x] Replace or supplement Plan Check evidence files with Plan Examination evidence files covering both `checkList` and `neighbors`.
  - [x] Record request/response/failure artifacts without access tokens, passwords, cookies, certificate material, or unbounded raw service responses.
  - [x] Record Plan Examination writeback status in disposition and lifecycle audit state.

- [x] Extend lifecycle and tests for Neighbor writeback. (AC: 19-28)
  - [x] Run Plan Examination writeback before package upload and task completion.
  - [x] Stop Finalize when Neighbor writeback fails.
  - [x] Add tests for data-object route construction, Neighbor template creation, reviewed-neighbor filtering, field mapping, duplicate prevention, payload preservation, diagnostics, and lifecycle short-circuit behavior.

### Review Findings

- [x] [Review][Patch] Plan Check mapping treats pending/not-started stages as passed [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:267] — `StageDecision` only fails on explicitly blocking words, so rows like Closure/Details can be marked passed when a required stage exists but has status `pending`, `not_started`, `not started`, `skipped`, or another non-complete state. This violates AC 4 and AC 9, which require positive acceptable evidence before setting `passed = true`.
- [x] [Review][Patch] Unsupported Adjoining/Notices automation is not visible in local diagnostics [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:255] — unsupported check types are skipped entirely, and request/response evidence only lists updated rows. AC 11 requires the not-yet-implemented adjoining/comparator limitation to be visible in local diagnostics while preserving the row as N/A/unchanged.

- [x] [Review][Decision] Confirm whether missing `neighbors[]` may be created on the Plan payload - AC 23 allows creating `neighbors` only when the API contract confirms that property; the story still records a remaining blocker to capture a Plan GET/PUT payload showing exactly where `neighbors[]` lives. Current code creates `plan["neighbors"]` unconditionally when missing at `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:359`.
- [x] [Review][Decision] Confirm `/api/v4/rest/data/objects/create` semantics before relying on it during Finalize - new Neighbor objects are created before the final Plan `PUT`; if the endpoint persists objects rather than returning a detached template, failed Plan saves or retries can leave orphan/duplicate Neighbor objects. Current create-before-save path is `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:375` and final save is `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:94`.
- [x] [Review][Patch] Preserve fetched Plan save body shape [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:158] - `ResolveArray` accepts a single Plan object, but `SavePlansAsync` always PUTs `Plan[]`. If Innola expects the same single-object shape it returned, the writeback can fail after local mutation and Neighbor template calls.
- [x] [Review][Patch] Reject malformed Neighbor create-template responses [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:428] - `CreateDefaultNeighborAsync` silently fabricates a local Neighbor object when the create endpoint returns the wrong JSON shape, contrary to AC 27's malformed-contract failure requirement.
- [x] [Review][Patch] Handle `data` array create-template envelopes [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:461] - `ResolveObject` handles raw objects, raw arrays, and `data` objects, but not `{ "data": [ ... ] }`, so it can populate and save the wrapper object as a Neighbor.
- [x] [Review][Patch] Trim reviewed roles before filtering Neighbor rows [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:343] - role matching is case-insensitive but exact, so reviewed rows such as `" Neighbor "` are skipped.
- [x] [Review][Patch] Make Neighbor matching/enrichment idempotent for partial existing rows [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:483] - matching on all fields misses existing Innola rows that lack the newly captured lot/LandVal/exam values, and `PopulateNeighbor` can overwrite existing values with null when a reviewed row is partial.
- [x] [Review][Patch] Resolve multiple returned Plan objects deterministically [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:353] - checklist updates iterate all Plans, but Neighbor writeback uses only `plans.FirstOrDefault()`, which can attach neighbors to the wrong Plan when the transaction returns more than one Plan object.
- [x] [Review][Patch] Report Neighbor writeback failures as Plan Examination Neighbor blockers [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:106] - Neighbor exceptions are currently sanitized as generic Plan Check writeback failures, so operators do not get the AC 27 diagnostic that identifies malformed/missing Neighbor contract data.
- [x] [Review][Patch] Add Neighbor failure and response-evidence coverage [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaPlanCheckServiceTests.cs:89] - tests cover the happy path but not Neighbor create/save failure diagnostics or response evidence contents, while AC 28 requires failure short-circuit and request/response evidence coverage for the amendment.

## Developer Context

### Confirmed API Contract From Postman

Source file:

- `C:\Users\js91482\Downloads\Sidwell Plan Exam Scenario.postman_collection.json`

Relevant items:

- `2 - Get Plan`
- `3 - Update Plan Check List`

Observed GET:

```http
GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}
```

Observed POST:

```http
POST /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}
Content-Type: application/json

[ { full Plan object with checkList[] } ]
```

Observed checklist rows:

```json
{
  "@c": "PlanCheck",
  "id": "...",
  "checkType": "plan_check_type_closure",
  "passed": true,
  "description": null,
  "allowRead": true,
  "allowWrite": true
}
```

Observed check types:

- `plan_check_type_closure`
- `plan_check_type_area`
- `plan_check_type_compplan_datasheet`
- `plan_check_type_plotting`
- `plan_check_type_notices`
- `plan_check_type_details`
- `plan_check_type_general`
- `plan_check_type_adjoining`

### Confirmed Neighbor Object Evidence

Source: user-captured browser network trace from Innola test environment for transaction `100000622` review.

Observed create-template request:

```http
POST https://eltrs-test.innola-solutions.com/api/v4/rest/data/objects/create
Status: 200 OK
```

Observed Neighbor object fields:

| Innola Field | Observed Value / Purpose |
|---|---|
| `@c` | `Neighbor` |
| `id` | generated object id |
| `neighborType` | `neighbor_type_owner` |
| `name` | neighbor name |
| `address` | neighbor address |
| `volume` | title volume |
| `folio` | title folio |
| `lot` | lot number |
| `landValNumber` | LandVal number |
| `examNumber` | examination number |
| `allowRead` / `allowWrite` | API permissions |

Implementation decision: proceed with the captured Neighbor object evidence, create or update Plan `neighbors[]`, and preserve the fetched Plan save body shape as `Plan` or `Plan[]`. Live validation against transaction `100000622` remains the final environment check.

### Live Access Finding

A live read-only certified request to:

```text
https://eltrs-dev.innola-solutions.com/api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId=019effe5-0a96-7184-88d0-18008e43c46d
```

returned `401 Unauthorized` without an active Innola access token. The configured client certificate is present and valid:

- Subject: `CN=Jamaica eTitles Project Team, O=Innola Solutions, L=Kyiv, C=UA`
- Thumbprint: `DEA7F06F3917AA2E6AF817FF5B20587C28EB6215`
- Expires: `2026-10-26`

Implementation must therefore use the authenticated in-app `InnolaSession`, not an external unauthenticated command.

### Current Code Reality

Existing Innola services and patterns:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaHttp.cs`
  - `BuildUri(...)`
  - `ApplyAuthHeaders(...)`
  - `SafeRetryMessage(...)`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
  - Existing `administrative/ladm-objects/create/multi` and `administrative/ladm-objects?typeKeyId=spatialunit` pattern.
  - Good example for writing local request/response evidence.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
  - Owns Finalize closeout ordering.
  - Currently handles Enterprise disposition, Spatial Unit save, SUID writeback, Compute report generation, package upload, and task completion.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
  - Produces `output/reports/compute_examination_report.json`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
  - Stores closeout state and report/package references.

### Required Files To Inspect Before Dev

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaHttp.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`

## Dependencies

- Story 7.9 Finalize closeout ordering and Compute examination report generation.
- Story 4.9 reportable stage findings model.
- Existing authenticated Innola session from Stories 2.8a / 7.9.
- Existing Spatial Unit and working package closeout artifacts.

## Non-Goals

- Do not write to CADMAP, CADINDEX, Enterprise Parcel Fabric, or authoritative GIS layers.
- Do not create new Spatial Units in this story; reuse Story 7.9 behavior.
- Do not change Enterprise working-layer geometry publishing.
- Do not invent Plan Check types outside the Innola API response.
- Do not overwrite unknown Plan fields or narrow the Plan payload to a lossy DTO.
- Do not log tokens, passwords, certificate material, or sensitive full service responses.
- Do not treat a checklist item as passed if the supporting evidence is missing.

## Testing Requirements

Minimum verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false`
- Custom test runner if stable in the local environment:
  - `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.exe`

Focused test areas:

- Plan Check API route construction.
- Plan payload preservation.
- Plan Check mapping from saved report/stage artifacts.
- Lifecycle short-circuit before package upload/task completion on Plan Check failure.
- Evidence artifact creation.
- Secret redaction.

Manual/live smoke testing:

- Use a test Innola transaction such as `100000379` or a newer non-production transaction.
- Log in through the add-in so an active `Access-Token` is available.
- Run Finalize.
- Confirm Innola Plan Check table values change from `N/A` to the expected accepted/failed/N/A states.
- Confirm `working/plan_check_api_request.json` and `working/plan_check_api_response.json` exist.
- Confirm package upload and task completion run only after Plan Check writeback succeeds.

## References

- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `C:\Users\js91482\Downloads\Sidwell Plan Exam Scenario.postman_collection.json`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false` - passed.
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.exe "plan check" "generates report"` - passed 5 focused tests.
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.exe "plan check"` - passed 5 focused tests after review patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj --no-restore -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:GenerateTargetPlatformAttribute=false --no-incremental` - passed with existing ArcGIS platform analyzer warnings.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-restore --no-launch-profile -p:BuildProjectReferences=false -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:GenerateTargetPlatformAttribute=false -- "plan check service" "plan examination writeback failure" "generates report before package upload"` - passed 8 focused tests.
- Full executable harness ran through the Story 7.11 focused area and later stopped at existing local ArcGIS dependency boundary: missing `ArcGIS.Desktop.Mapping, Version=13.6.0.0` in `SpatialOverlapReviewPersistenceServiceTests.OverlapReviewServiceBlocksWhenNoTargetsAreConfigured`.

### Completion Notes

- Added authenticated Innola Plan Check GET/POST service that preserves full Plan JSON payloads and updates only recognized `checkList` rows.
- Mapped report/output evidence to Closure, Area, Comparison Plan/Data Sheet, Plotting, Details, and General rows; Notices and Adjoining remain unchanged when no automated rule exists.
- Inserted Plan Check writeback after Compute examination report generation and before working package upload/task complete.
- Added local non-secret request/response/failure evidence and disposition/audit status for retry/reopen visibility.
- Amended the writeback to use the confirmed Plan Examination data-object route: `GET/PUT /api/v4/rest/data/objects?typeKeyId=plan&transactionId={transactionId}`.
- Added reviewed Neighbor writeback from `working/extraction_review_data.json`, mapping name/address/volume/folio/lot/landValNumber/examNumber, defaulting `neighborType`, preserving unknown fields, and avoiding duplicate rows on rerun.
- Added Plan Examination request/response/failure diagnostics alongside the existing Plan Check evidence files, without logging secrets.
- Patched code-review findings after user decision option 1: preserved fetched Plan save shape, hardened Neighbor template validation/envelope parsing, trimmed Neighbor roles, avoided partial-row null overwrites, selected the correct Plan for multi-Plan responses, and added focused regression coverage.
- Patched live transaction `100000622` Finalize retry failure by falling back from the selected Innola UUID transaction id to the displayed transaction number when Plan lookup returns no Plan objects; save uses the lookup id that returned the Plan.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaPlanCheckServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-07 | 0.1 | Created story for Innola Plan Check list writeback during Compute Finalize using the discovered Plan API contract. | Amelia / Codex |
| 2026-07-07 | 1.0 | Implemented Plan Check service, finalize ordering hook, evidence artifacts, disposition/audit status, and focused tests. | Amelia / Codex |
| 2026-07-07 | 1.1 | Patched code review findings for positive stage acceptance and unsupported Plan Check diagnostics. | Amelia / Codex |
| 2026-09-03 | 1.2 | Amended story scope from Plan Check only to Plan Examination writeback, adding reviewed Neighbor row API evidence, field mapping, diagnostics, and lifecycle criteria. | Mary / Amelia / Codex |
| 2026-09-03 | 1.3 | Implemented Plan Examination data-object GET/PUT route, reviewed Neighbor writeback, Plan Examination diagnostics, and focused lifecycle/service tests. | Amelia / Codex |
| 2026-09-03 | 1.4 | Patched Story 7.11 code-review findings for Neighbor writeback hardening and marked review complete after focused tests passed. | Codex |
| 2026-09-03 | 1.5 | Added Plan lookup fallback from Innola UUID transaction id to displayed transaction number after live transaction 100000622 returned no Plan object on Finalize retry. | Codex |
| 2026-09-03 | 1.6 | Extended Plan lookup fallback to handle live HTTP 500 from UUID Plan GET before retrying displayed transaction number 100000622. | Codex |
| 2026-09-03 | 1.7 | Added lookup-key-specific Plan GET diagnostics and test coverage for both UUID and transaction-number lookups failing. | Amelia / Codex |
| 2026-09-03 | 1.8 | Added administrative Plan route fallback for transaction 100000623 after the data-object Plan route returned HTTP 500 for both lookup keys. | Amelia / Codex |
| 2026-09-03 | 1.9 | Added UUID administrative Plan lookup before transaction-number fallbacks after reviewing that Spatial Unit succeeds through administrative APIs with the UUID. | Amelia / Codex |
| 2026-09-03 | 1.10 | Added local nested Neighbor fallback after transaction 100000623 reached Neighbor create-template and Innola returned 400 BadRequest. | Amelia / Winston / Codex |
