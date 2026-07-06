# Investigation: TR100000379 Spatial Unit Finalize

## Hand-off Brief

1. **What happened.** TR100000379 reached compute disposition and completion-readiness persistence, but no Spatial Unit save/success/failure event was persisted before the reported ArcGIS Pro crash.
2. **Where the case stands.** The evidence places the failure after `transaction_completion_readiness_checked` and before `compute_spatial_unit_saved`/`compute_spatial_unit_save_failed`.
3. **What's needed next.** Add durable Spatial Unit API start/response/failure trace before retrying Finalize; attach debugger if the crash still happens before trace completion.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000379 |
| Date opened | 2026-07-06 |
| Status | Active |
| System | Windows, ArcGIS Pro add-in, Enterprise working layers mode |
| Evidence sources | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379`, repo source, test suite |

## Problem Statement

User reports TR100000379 did not create Innola Spatial Units and `working_polygons.review_comment` was not populated with generated SUID values. ArcGIS Pro crashed again during the Enterprise/Finalize process. User asks whether adding debugger instrumentation for the next execution is advisable.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| TR100000379 case folder | Available | Disposition, lifecycle audit, output summary, Enterprise publish evidence inspected. |
| Enterprise working layer state | Partial | Publish evidence confirms polygons were published; live `review_comment` values not queried in this investigation. |
| Repo source | Available | Finalize and Spatial Unit writeback code is available locally. |
| ArcGIS Pro crash dump/log | Missing | Not yet provided or located. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Inspect TR100000379 `working` and `output` artifacts | High | Done | No Spatial Unit save/writeback evidence files are present. |
| 2 | Inspect latest lifecycle audit and manifest | High | Done | Last persisted lifecycle event is readiness checked. |
| 3 | Add targeted debug/audit instrumentation if persistence gap remains | High | Open | Needed if crash occurs before durable evidence is written. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-07-06 | User reports ArcGIS Pro crashed during TR100000379 Finalize and polygon `review_comment` is not populated. | User message | Confirmed |
| 2026-07-06 19:15:53Z | Enterprise working publish succeeded with 46 points, 55 lines, 10 polygons, 1 case_index row, and 0 errors. | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\output\enterprise_working_publish.json:2`, `:19`, `:31`, `:37`, `:55` | Confirmed |
| 2026-07-06 19:16:03Z | Compute review disposition was recorded and completion readiness passed. | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\working\workflow_lifecycle_audit.json:28`, `:38` | Confirmed |

## Confirmed Findings

### Finding 1: Enterprise working publish succeeded before Finalize

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\output\enterprise_working_publish.json:2`, `:19`, `:31`, `:37`, `:55`

**Detail:** The publish artifact reports status `published`, 46 points, 55 lines, 10 polygons, 1 case_index row, and no errors.

### Finding 2: Spatial Unit save did not persist any terminal state

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\working\compute_review_disposition.json:15`, `:16`

**Detail:** The disposition still has `spatial_unit_api_status` = `not_started` and `spatial_unit_id` = `null`.

### Finding 3: Lifecycle audit stops before Spatial Unit save

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\working\workflow_lifecycle_audit.json:28`, `:38`

**Detail:** The audit contains `compute_review_approved` and `transaction_completion_readiness_checked`, but no `compute_spatial_unit_*` event.

### Finding 4: Earlier local parcel-fabric output attempts failed on edit-session requirements

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000379\output\logs\process.log:12`, `:25`, `:38`

**Detail:** Three earlier parcel-fabric-mode attempts failed with `Objects in this class cannot be updated outside an edit session [compute_review_Lines]`. The later Enterprise working-layer output attempt exited successfully.

## Deduced Conclusions

### Deduction 1: The reported missing `review_comment` is expected from the persisted state

**Based on:** Findings 2 and 3.

**Reasoning:** Polygon `review_comment` SUID writeback is downstream of a successful Spatial Unit API save. The local artifacts show the Spatial Unit save did not persist as started/saved/failed.

**Conclusion:** `working_polygons.review_comment` would remain unchanged for this run.

### Deduction 2: A debugger alone may not be sufficient

**Based on:** Finding 3.

**Reasoning:** The process appears to terminate or crash before the code records a durable Spatial Unit event. If the debugger is not attached at the exact moment, the evidence gap remains.

**Conclusion:** Add file-based trace around Spatial Unit start/request/response/failure before the next run, and optionally attach debugger too.

## Hypothesized Paths

### Hypothesis 1: Finalize crashed before Spatial Unit API save completed

**Status:** Confirmed

**Theory:** No Spatial Unit evidence exists because the process crashed before or during the Innola API call.

**Supporting indicators:** User observes no Spatial Unit creation.

**Would confirm:** Missing `spatial_unit_api_status`/`spatial_unit_id` in disposition, manifest, and lifecycle audit.

**Would refute:** Disposition/audit shows Spatial Unit saved with id before a later writeback/upload failure.

**Resolution:** Confirmed by disposition and lifecycle audit: Spatial Unit status remained `not_started` and no `compute_spatial_unit_*` lifecycle event exists.

### Hypothesis 2: Spatial Units were created but polygon SUID writeback did not run or did not receive SUID values

**Status:** Open

**Theory:** API returned object IDs but no `suid`, or Finalize crashed after save and before polygon writeback.

**Supporting indicators:** User reports `review_comment` missing.

**Would confirm:** Disposition/audit has Spatial Unit saved but no `compute_spatial_unit_polygon_suid_reference_saved` event.

**Would refute:** Evidence file exists and Enterprise query shows `review_comment` populated.

**Resolution:** Pending.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| ArcGIS Pro crash detail | Needed to know whether crash is add-in exception, ArcGIS COM/runtime, or process termination | Capture debugger/trace next run or inspect Windows Event Viewer/ArcGIS logs. |
| Live Enterprise polygon row state | Needed to confirm user-observed missing `review_comment` against service data | Query `working_polygons` by transaction number. |
| Innola API response body | Needed to confirm whether `suid` is returned per Spatial Unit | Add temporary response evidence/debug logging or capture with debugger. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | Gap between `transaction_completion_readiness_checked` and `spatialUnitService.CreateOrUpdateAsync` return |
| Trigger | User clicks Finalize for a transaction in ready-to-complete state |
| Condition | Enterprise working layers mode; Spatial Unit API and Enterprise writebacks run during closeout |
| Related files | `InnolaTransactionLifecycleCoordinator.cs`, `InnolaSpatialUnitService.cs`, `JsonEnterpriseWorkingDispositionService.cs` |

## Conclusion

**Confidence:** Medium

The persisted evidence confirms Finalize did not complete the Spatial Unit API step for TR100000379. The exact crash mechanism remains unknown because no durable event is written immediately before the API call and no crash dump/log has been inspected.

## Recommended Next Steps

### Fix direction

Add durable instrumentation before the Spatial Unit call, not only debugger attachment:

- record `compute_spatial_unit_save_started` before `CreateOrUpdateAsync`;
- write sanitized request count/transaction context;
- write raw/sanitized API response metadata to `working/spatial_unit_api_response.json`;
- record polygon SUID writeback start/result separately.

### Diagnostic

Attach Visual Studio to ArcGISPro.exe for the next run if possible, but keep file-based trace because Pro process crashes can bypass normal managed exception handling.

## Reproduction Plan

Rebuild/redeploy add-in, reopen TR100000379, retry Finalize with instrumentation enabled, then inspect case folder audit files and Enterprise `working_polygons.review_comment`.

## Side Findings

Pending.

## Follow-up: 2026-07-06

### New Evidence

### Additional Findings

### Updated Hypotheses

### Backlog Changes

### Updated Conclusion
