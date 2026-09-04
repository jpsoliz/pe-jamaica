# Investigation: TR100000622 Plan Missing On Finalize Retry

## Hand-off Brief

1. **What happened.** Confirmed: transaction `100000622` reached Finalize after being returned to Compute, attached the Compute PDF report, then failed Plan Examination writeback with `plan_missing`.
2. **Where the case stands.** Confirmed: the failed writeback used Innola transaction UUID `019fe6d0-0c82-7009-b92e-10248f82a968`, while the user-captured working API reference uses `transactionId=100000622`.
3. **What's needed next.** Patch and validate fallback Plan lookup: try the selected UUID first, then use the displayed transaction number when the UUID lookup returns no Plan objects.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | N/A |
| Date opened | 2026-09-03 |
| Status | Concluded |
| System | Windows, ArcGIS Pro add-in, Innola test environment |
| Evidence sources | User screenshot, local case folder `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000622`, source code, focused tests |

## Problem Statement

User reported that after returning parcel transaction `100000622` to the Compute process and pressing Finalize again, the add-in displayed: `Innola Plan Check writeback failed because no Plan object was returned.`

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| User screenshot | Available | Shows active transaction number `100000622`, task `Compute Survey Plan`, and Finalize error message. |
| Local failure evidence | Available | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000622\working\plan_examination_api_failure.json` records `plan_missing` at `2026-09-03T22:39:46.1051004Z`. |
| Local lifecycle audit | Available | Audit tail shows report attachment succeeded, then Plan Examination writeback started and failed. |
| Manifest | Available | Manifest has display `transaction_id` as `100000622`, but nested Innola transaction id as UUID `019fe6d0-0c82-7009-b92e-10248f82a968`. |
| Source code | Available | `InnolaPlanCheckService` previously used only `transaction.TransactionId` for Plan lookup. |
| Live HTTP response body | Missing | Failure artifact records category/message, not the raw/sanitized Plan GET response body. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-09-03T14:45:15Z | Earlier Plan Check evidence recorded `saved_plan_count = 1`. | `working/plan_check_api_response.json` | Confirmed |
| 2026-09-03T22:39:04Z | Transaction was loaded as task `Compute Survey Plan` with UUID `019fe6d0-0c82-7009-b92e-10248f82a968` and number `100000622`. | `manifest.json` | Confirmed |
| 2026-09-03T22:39:45Z | Compute PDF report attached successfully. | `working/workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-03T22:39:46Z | Plan Examination writeback failed with `plan_missing`. | `working/plan_examination_api_failure.json` | Confirmed |

## Confirmed Findings

### Finding 1: Finalize failed after Plan lookup returned zero parsed Plan objects

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000622\working\plan_examination_api_failure.json` records `error_category: plan_missing` and `error_message: Plan API returned no Plan objects.`

**Detail:** The code emits the UI message only when `FetchPlansAsync(...).Plans.Count == 0` before any checklist or Neighbor mutation runs.

### Finding 2: The retry context mixed UUID and display transaction number identifiers

**Evidence:** `manifest.json` records top-level `transaction_id: 100000622`; nested `payload.innola_transaction.transaction_id: 019fe6d0-0c82-7009-b92e-10248f82a968`; nested `transaction_number: 100000622`.

**Detail:** The user-provided API reference for Plan lookup used `transactionId=100000622`, while the failed artifact shows the add-in used the UUID as the active Innola transaction id.

## Deduced Conclusions

### Deduction 1: A transaction-number fallback is needed for Plan Examination lookup after returned-to-Compute reloads

**Based on:** Findings 1 and 2, plus source code path in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`.

**Reasoning:** If the UUID lookup returns an empty Plan list but the Plan Examination page/API reference resolves by display transaction number, the current single-key lookup blocks Finalize even when the transaction has a Plan in Innola.

**Conclusion:** The service should first try `TransactionId`; if it gets a successful but empty Plan collection and `TransactionNumber` differs, retry using `TransactionNumber` and save with whichever lookup id returned the Plan.

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `InnolaPlanCheckService.WriteAsync`, `plans.Count == 0` branch |
| Trigger | User presses Finalize after the returned Compute task is ready to complete |
| Condition | Plan lookup through `/api/v4/rest/data/objects?typeKeyId=plan&transactionId={id}` returns no parsed Plan objects for the selected UUID |
| Related files | `InnolaPlanCheckService.cs`, `InnolaTransactionService.cs`, `InnolaTransactionDetailService.cs`, `InnolaPlanCheckServiceTests.cs` |

## Conclusion

**Confidence:** Medium-High

The root cause is very likely identifier selection for the Plan data-object lookup after the transaction was returned to Compute. The local failure does not include the raw GET body, so it cannot prove the server would return a Plan for `transactionId=100000622`; however, the user-captured API note and the local UUID/number split make the fallback the correct low-risk fix.

## Recommended Next Steps

### Fix Direction

Implemented: Plan lookup now falls back from the selected Innola UUID to the displayed transaction number when the UUID lookup succeeds but returns no Plan objects. The final `PUT` uses the same lookup id that returned the Plan.

### Diagnostic

After packaging/installing the add-in, retry Finalize for `100000622`. If it still fails, capture the GET response for both lookup ids: `transactionId=019fe6d0-0c82-7009-b92e-10248f82a968` and `transactionId=100000622`.

## Reproduction Plan

1. Mock Plan GET by UUID as `[]`.
2. Mock Plan GET by transaction number as a valid `Plan[]` with `checkList`.
3. Finalize writeback should succeed and PUT back to `transactionId=100000622`.

## Side Findings

- The failure artifact still uses legacy schema name `plan_check_api_failure_v1` even when written to `plan_examination_api_failure.json`; this is cosmetic but may be worth normalizing later.
## Follow-up: 2026-09-03 #2

### New Evidence
- Confirmed: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000622\working\plan_examination_api_failure.json` was rewritten at `2026-09-03T22:56:21.6032667Z` with `error_category` = `HttpRequestException` and `error_message` = `Plan Check GET failed: InternalServerError`.
- Confirmed: `workflow_lifecycle_audit.json` shows the failure occurred after `compute_report_attached` and at `compute_plan_examination_writeback_failed`, before request/response evidence for a new Plan Examination save was written.
- Confirmed: `extraction_review_data.json` contains two `adjacent_owners` rows with role `Neighbor`: `Reander Wilson (Occ.)` and `The Commissioner of Lands (R.O.)`, each with volume `1448` and folio `938`; address, lot, land valuation number, and examination number are null.

### Finding
- Confirmed: this failure is not caused by neighbor values. The Innola API failed during the Plan GET before the add-in created or saved Neighbor rows.
- Deduced: the first Story 7-11 fallback only handled a successful UUID GET that returned zero Plan objects. Live retry showed a stricter Innola behavior: UUID Plan lookup can return HTTP 500, so the transaction-number fallback must also cover HTTP GET failure when transaction number differs from transaction id.

### Fix Applied
- Updated `InnolaPlanCheckService.FetchPlansAsync` so when a displayed transaction number is available and differs from the selected Innola UUID, the primary UUID lookup uses one attempt and falls back to the transaction number on `HttpRequestException`.
- Added regression coverage for `transactionId=019fe6d0-0c82-7009-b92e-10248f82a968` returning HTTP 500 followed by successful lookup/save with `transactionId=100000622`.

### Verification
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj --no-restore -v:q ...` passed with existing CA1416 warnings.
- Focused test run passed 14 tests, including `innola plan check service falls back to transaction number for UUID plan lookup server error`.
- Add-in package rebuilt and registered as version `1.1.362`.

### Status
Concluded with high confidence for the observed failure message. Next live retry should reach Plan save/Neighbor writeback; if it fails there, inspect the newly written Plan Examination request/failure artifact.

## Follow-up: 2026-09-03 #3

### New Evidence
- Confirmed: Latest live retry at `2026-09-03T23:32:24.8448335Z` still wrote `plan_examination_api_failure.json` with `error_category` = `HttpRequestException` and `error_message` = `Plan Check GET failed: InternalServerError`.
- Confirmed: No new `plan_examination_api_request.json` was written before the failure, so the failure still occurs during Plan fetch, before checklist mutation, Neighbor template creation, Neighbor value population, or Plan PUT.
- Confirmed: Reviewed neighbor rows for `100000622` are not malformed: two role=`Neighbor` rows, `Reander Wilson (Occ.)` and `The Commissioner of Lands (R.O.)`, with volume/folio `1448/938`; editable optional fields are null.

### Finding
- Confirmed: Current evidence does not support Neighbor data as the cause. The failure is in the transaction Plan GET phase.
- Deduced: The existing diagnostic was too generic to distinguish old add-in still loaded vs. fallback also failing. The next build must write lookup-key-specific diagnostics.

### Fix Applied
- Updated Plan GET failure messages to include the lookup key and HTTP status.
- Updated fallback logic so if UUID lookup fails and transaction-number fallback also fails, failure evidence states both lookup keys: UUID and displayed transaction number.
- Added internal test `FailureEvidenceNamesBothLookupKeysWhenUuidAndTransactionNumberPlanLookupsFail`.

### Verification
- Focused Plan Check/Plan Examination test run passed 15 tests.
- Add-in package rebuilt and registered as version `1.1.365`.

### Status
Concluded current code-side observability gap. Next live retry with `1.1.365` will identify whether ArcGIS Pro was still running an old add-in or whether Innola returns HTTP 500 for both Plan lookup keys.
