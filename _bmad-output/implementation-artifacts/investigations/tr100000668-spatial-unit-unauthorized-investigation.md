# Investigation: TR100000668 Spatial Unit Unauthorized

## Hand-off Brief

1. **What happened.** TR100000668 generated and published the review geometry, then failed while creating Innola Spatial Units because the Innola default Spatial Unit API returned `Unauthorized`.
2. **Where the case stands.** Confirmed root cause is an Innola API authorization failure during the default-object creation call; local/Enterprise geometry creation itself succeeded.
3. **What's needed next.** Refresh or fix the Innola session/API authorization used by `InnolaSpatialUnitService`, then retry Finalize/Create Spatial Unit for the same case.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000668 |
| Date opened | 2026-07-13 |
| Status | Concluded |
| System | Windows, ArcGIS Pro add-in, PXA single parcel survey plan workflow |
| Evidence sources | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668`, repo source |

## Problem Statement

User reported the UI footer message `Could not create Spatial Unit. Try again.` for transaction TR100000668 after Create Spatial Units had produced output artifacts.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| TR100000668 case folder | Available | Contains manifest, lifecycle audit, Spatial Unit request/failure traces, output summary, and Enterprise publish evidence. |
| Repo source | Available | Lifecycle coordinator and Spatial Unit service code identify the failure path. |
| Live Innola API/session state | Missing | Not queried directly; case evidence records the API response outcome as `Unauthorized`. |

## Confirmed Findings

### Finding 1: Review geometry publish succeeded before the failure

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\output\enterprise_working_publish.json:2`, `:19`, `:25`, `:31`, `:37`, `:43`, `:55`

**Detail:** Enterprise publish status was `published`, with 12 points, 12 lines, 1 polygon, 1 case index row, and no errors.

### Finding 2: Spatial Unit creation was attempted for one Spatial Unit

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\working\spatial_unit_api_request.json:8`

**Detail:** The trace requested one Spatial Unit, matching one output polygon.

### Finding 3: Innola default Spatial Unit creation returned Unauthorized

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\working\spatial_unit_api_failure.json:7`, `:8`

**Detail:** The durable failure trace records `HttpRequestException` with message `Spatial Unit default creation failed: Unauthorized`.

### Finding 4: The UI message is the expected fallback for this failure path

**Evidence:** `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Innola\InnolaTransactionLifecycleCoordinator.cs:299`, `:307`, `:310`; `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Innola\InnolaSpatialUnitService.cs:97`, `:126`

**Detail:** Finalize calls `CreateOrUpdateAsync`; on failure it displays `Could not create Spatial Unit. Try again.`. The service throws `HttpRequestException` when the default creation response is not successful.

## Deduced Conclusions

### Deduction 1: This is not a geometry generation failure

**Based on:** Findings 1 and 2.

**Reasoning:** Output artifacts and Enterprise working layers were created before the Spatial Unit API call. The failure occurred after readiness and publish, during Innola Spatial Unit API creation.

**Conclusion:** Re-running extraction or point validation is not the first fix path.

### Deduction 2: Retry should focus on authorization/session state

**Based on:** Findings 3 and 4.

**Reasoning:** The failed HTTP operation was the default Spatial Unit creation endpoint; the API rejected it as unauthorized.

**Conclusion:** Refresh/login/reclaim session or fix token/scope/permission before retrying Finalize/Create Spatial Unit.

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `InnolaSpatialUnitService.CreateDefaultSpatialUnitsAsync` throws `HttpRequestException` on non-success response |
| Trigger | Finalize/Create Spatial Unit runs after compute review approval |
| Condition | Innola API returns `Unauthorized` for default Spatial Unit creation |
| Related files | `InnolaTransactionLifecycleCoordinator.cs`, `InnolaSpatialUnitService.cs` |

## Conclusion

**Confidence:** High

TR100000668 failed because the Innola Spatial Unit default-creation API returned `Unauthorized`. The local parcel output and Enterprise working publish succeeded; the failure is specifically the Innola Spatial Unit API authorization/session leg.

## Recommended Next Steps

### Diagnostic

Refresh the Innola session used by ArcGIS Pro, then retry Finalize/Create Spatial Unit. If it fails again, inspect `working\spatial_unit_api_failure.json` first; it should confirm whether the unauthorized response persists or changes to a downstream save/payload problem.

### Fix direction

If this recurs after a fresh login, review the authorization headers/session token used by `InnolaSpatialUnitService` and confirm the current user/session has permission for `v4/rest/administrative/ladm-objects/create/multi`.

## Reproduction Plan

Open TR100000668 with a valid Innola session and retry Finalize/Create Spatial Unit. Expected successful evidence: `working\spatial_unit_api_response.json`, lifecycle event `compute_spatial_unit_saved`, and manifest `spatial_unit_api_status = saved`.

## Follow-up: 2026-07-13

### New Evidence

- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\output\reports` is empty.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\working\compute_review_disposition.json:20` has `compute_examination_report_ref = null`.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000668\working\workflow_lifecycle_audit.json:116` through `:133` shows the second retry also stopped at `compute_spatial_unit_save_failed`.
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Innola\InnolaTransactionLifecycleCoordinator.cs:299` through `:318` returns immediately when Spatial Unit creation fails.
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Innola\InnolaTransactionLifecycleCoordinator.cs:407` through `:435` shows the Compute examination report is generated only after the Spatial Unit save and polygon SUID writeback block completes.
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Workflow\Reports\ComputeExaminationReportService.cs:45` and `:118` through `:122` show the expected report files are `output\reports\compute_examination_report.json` and `output\reports\compute_examination_report.pdf`.

### Additional Findings

The "report with all the work" is the Compute examination report step. That step is downstream of Innola Spatial Unit creation, so it was not reached for TR100000668.

### Updated Conclusion

The missing report is expected from the current control flow. The transaction failed before report generation because Innola Spatial Unit default creation returned `Unauthorized`.
