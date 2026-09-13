# Investigation: TR 100001041 Plan Check Writeback Failure

## Hand-off Brief

1. **What happened.** TR 100001041 Finalize fails at Innola Plan Examination / Plan Check writeback with HTTP 400 BadRequest, after spatial unit save and compute report attachment succeed.
2. **Where the case stands.** Confirmed not to be the package/manifest lock failure path patched previously; current evidence points to the newer Neighbor create/writeback path added after the original Plan Check-only behavior.
3. **What's needed next.** Install/run add-in `1.1.580` and retry TR 100001041; Finalize should save Plan Check values even if Innola rejects newly created Neighbor rows.

## Case Info

| Field | Value |
| ----- | ----- |
| Ticket | N/A |
| Date opened | 2026-09-13 |
| Status | Active |
| System | ArcGIS Pro add-in, local case folder under `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases` |
| Evidence sources | TR 100001041 case artifacts, lifecycle audit, `InnolaPlanCheckService`, `InnolaTransactionLifecycleCoordinator`, git diff |

## Problem Statement

User reported that after the previous change, Compute Finalize for transaction 100001041 shows: "Innola Plan Check writeback failed. Try again." The user asked whether the previous patch broke behavior that was working.

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001041\working\plan_check_api_failure.json` | Available | Shows `HttpRequestException` and `Plan Examination PUT failed: BadRequest`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001041\working\plan_check_api_request.json` | Available | Shows six plan-check updates and two neighbor rows created. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001041\working\workflow_lifecycle_audit.json` | Available | Shows session refresh succeeded before writeback failure. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs` | Available | Shows package upload occurs after Plan Check writeback. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs` | Available | Shows failed non-success PUT throws without preserving response body. |
| Innola HTTP 400 response body | Missing | Required to confirm the rejected field or validation rule. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Capture sanitized Innola response body on failed Plan save | Medium | Open | Current diagnostics only show BadRequest; still useful for future server validation failures. |
| 2 | Compare final PUT payload when neighbors are created vs updated | High | Done | TR 100001041 creates two neighbor rows; TR 100001040 updated existing neighbor rows after a successful Plan Check save. |
| 3 | Validate Innola child object template for `Neighbor` in current server schema | Medium | Open | Code falls back to a local object if create-template fails, which may be too weak for the real server. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-09-13T19:04:31Z | Plan Check writeback request evidence written. | `plan_check_api_request.json` | Confirmed |
| 2026-09-13T19:04:31Z | Plan Examination PUT failed with BadRequest. | `plan_check_api_failure.json` | Confirmed |
| 2026-09-13T19:04:31Z | Lifecycle recorded `compute_plan_examination_session_refresh` succeeded before failure. | `workflow_lifecycle_audit.json` | Confirmed |

## Confirmed Findings

### Finding 1: Failure Is HTTP 400 During Plan Check Save

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:194` starts `SavePlansAsync`; `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:234` throws `HttpRequestException($"Plan Examination PUT failed: {response.StatusCode}")`.

**Detail:** The local failure artifact for TR 100001041 records `Plan Examination PUT failed: BadRequest`, which maps to this exact throw.

### Finding 2: Session Refresh Succeeded Before the Failure

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:559` refreshes the Innola session immediately before `planCheckService.WriteAsync`; the lifecycle audit shows the refresh event succeeded before `compute_plan_examination_writeback_failed`.

**Detail:** This is not behaving like a login timeout in this trace. The server accepts the authenticated request flow far enough to return an application/API 400.

### Finding 3: Previous Package-Lock Patch Is Downstream of This Failure

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:568` performs Plan Check writeback; package upload starts later at `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:665`.

**Detail:** The previous manifest/package retry change cannot execute until Plan Check writeback succeeds. TR 100001041 stops before reaching package creation/upload.

### Finding 4: TR 100001041 Tried to Create Two Neighbor Rows

**Evidence:** `plan_check_api_request.json` reports `neighbor_rows_created = 2`, `neighbor_rows_updated = 0`, and rows for Vol/Folio `1151/608` and `1149/558`.

**Detail:** This is a plausible difference from cases where Plan Check writeback was already working with existing neighbor rows.

### Finding 5: Older Working Behavior Did Not Write Neighbor Rows

**Evidence:** `git diff 2eef590..ca26d20 -- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs` shows `ApplyNeighborUpdatesAsync`, `CreateDefaultNeighborAsync`, and `plan.neighbors` mutation were added after version 6.2.x. Story `7-11-write-innola-plan-check-list-on-compute-finalize.md` records amendment v1.2-v1.3 adding reviewed Neighbor row writeback.

**Detail:** The current finalize path writes both Plan Check values and reviewed Neighbor rows. The older path wrote Plan Check values only.

## Deduced Conclusions

### Deduction 1: The User-Facing Message Is Too Generic

**Based on:** Findings 1 and current failure writer at `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:999`.

**Reasoning:** The code stores only exception type and exception message. The exception message contains only HTTP status, not the response body.

**Conclusion:** We cannot currently tell from local artifacts which field Innola rejected.

### Deduction 2: Retrying Finalize Without a Code Diagnostic Patch May Repeat the Same Failure

**Based on:** Findings 1, 2, and 4.

**Reasoning:** The failure appears deterministic across repeated audit entries, and session refresh succeeds.

**Conclusion:** A retry is safe from the package-lock perspective, but it may continue to fail until the rejected Plan payload is identified.

## Hypothesized Paths

### Hypothesis 1: Innola Rejects Newly Created Neighbor Rows in the Plan PUT

**Status:** Confirmed as the most likely local regression mechanism; exact server field remains unconfirmed

**Theory:** The save payload includes newly created or locally templated `Neighbor` child rows, and the server rejects one or more required/readonly fields, missing metadata, or child object shape.

**Supporting indicators:** TR 100001041 has `neighbor_rows_created = 2`; TR 100001040 succeeded when neighbor rows were updated instead of created; older working code did not write neighbors.

**Would confirm:** The captured 400 response body references `neighbors`, `Neighbor`, child object validation, or one of the neighbor fields.

**Would refute:** The captured 400 response body references only checkList/checkType, plan status, transition, lock, or unrelated Plan fields.

**Resolution:** Patched `InnolaPlanCheckService` to retry Plan Check save with the pre-neighbor payload when a save fails after new Neighbor rows were added. The warning artifact `plan_examination_neighbor_writeback_warning.json` records the skipped neighbor writeback.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| Innola 400 response body | Blocks exact root cause confirmation. | Patch `SavePlansAsync` to read and persist a sanitized response body before throwing. |
| Full outgoing sanitized Plan save payload | Needed to compare accepted vs rejected payload. | Add diagnostics gated to failure path or local debug artifact without tokens. |

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:234` |
| Trigger | User presses Finalize in Compute workflow after final review. |
| Condition | Plan Check writeback reaches Innola save and receives HTTP 400 BadRequest. |
| Related files | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`, TR case `working\plan_check_api_request.json`, TR case `working\plan_check_api_failure.json` |

## Conclusion

**Confidence:** High

The previous package-lock patch is not the cause of the visible TR 100001041 error because this failure occurs before package creation/upload is reached. The confirmed failure is an Innola Plan Examination save returning HTTP 400 after a successful session refresh. The reason this was not happening before is that newer Story 7.11 behavior added Plan `neighbors[]` writeback during Finalize; TR 100001041 creates new Neighbor rows, while the older working behavior wrote Plan Check values only.

## Recommended Next Steps

### Fix direction

Implemented a fallback in `InnolaPlanCheckService`: if the Plan save fails after new Neighbor rows are added, write `plan_examination_neighbor_writeback_warning.json`, retry with the pre-neighbor Plan payload, and allow Plan Check finalize to continue.

### Diagnostic

Retry TR 100001041 after installing add-in `1.1.580`. If Innola still rejects the fallback Plan Check-only save, inspect `working\plan_check_api_failure.json`; otherwise inspect `working\plan_examination_neighbor_writeback_warning.json` to confirm the neighbor rows were skipped.

## Reproduction Plan

1. Open TR 100001041 from the transaction list.
2. Confirm final review and spatial unit state are ready.
3. Press Finalize.
4. Observe whether Plan Check writeback fails with HTTP 400 and inspect `working\plan_check_api_failure.json`.

## Side Findings

- The current failure artifact redacts messages containing JSON braces, which is safe but too coarse for HTTP API diagnostics. A structured sanitizer should preserve safe server fields such as `title`, `status`, `detail`, and validation messages while redacting tokens/passwords.
