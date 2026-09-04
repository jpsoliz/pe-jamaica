# Investigation: TR100000623 Plan Examination Writeback Route Failure

## Hand-off Brief

1. **What happened.** Confirmed: transaction `100000623` reached Finalize, saved the Spatial Unit, attached the Compute report, then failed during Innola Plan Examination writeback.
2. **Where the failure occurs.** Confirmed: failure occurs during Plan GET, before `plan_examination_api_request.json` is written, before Neighbor object creation, and before Plan save.
3. **Fix direction.** Implemented: try the newer `data/objects` Plan route first, then fall back to the original `administrative/ladm-objects` Plan route and save through the route that returned the Plan.

## Case Info

| Field | Value |
| --- | --- |
| Transaction number | `100000623` |
| Innola transaction id | `019fe6d2-7c02-7bf5-95f3-08d2e47635b0` |
| Task id | `23655889-a7f0-11f1-8772-96a4173ae162` |
| Date opened | 2026-09-03 |
| Status | Patched; awaiting live retry |
| Evidence sources | Local case folder `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000623`, Story 7.11, source code, focused tests |

## Evidence

| Source | Finding |
| --- | --- |
| `working/plan_examination_api_failure.json` | Add-in version with lookup-key diagnostics ran and reported both UUID and transaction-number lookups failing on Plan GET. |
| `working/workflow_lifecycle_audit.json` | Spatial Unit save, SUID reference save, report generation, and report attachment completed before Plan Examination writeback failed. |
| `working/spatial_unit_api_response.json` | Spatial Unit was saved with id `01a069b1-abd9-7d1c-ba59-2f7ece5342a6` and SUID `S100284151`. |
| `working/extraction_review_data.json` | Neighbor rows are present and structurally valid; the observed failure happens before those rows are sent. |

## Reviewed Neighbor Rows

| Name | Role | Volume | Folio | Lot | Address | LandVal No. | Exam No. |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Baron Drummond | Neighbor | 1234 | 123 | 10-2 | Rua du Laneg | 123-456-789 | |
| Leroy Grant et al | Neighbor | 4321 | 321 | 11 | Same rue | 987 | |

## Confirmed Findings

### Finding 1: The failure is not caused by Neighbor values

`plan_examination_api_failure.json` is written before any Plan Examination save request artifact exists. That means the add-in never reached Neighbor creation, Neighbor field mapping, or Plan PUT/POST for this retry.

### Finding 2: The newer data-object Plan route fails for both identifiers

The failure artifact records:

- UUID lookup: `019fe6d2-7c02-7bf5-95f3-08d2e47635b0`
- Transaction-number lookup: `100000623`
- Error: `500 InternalServerError` from Plan GET

This means the earlier UUID-to-transaction-number fallback is working, but Innola still rejects the newer data-object Plan route for this transaction state.

### Finding 3: The original administrative Plan route remains a valid compatibility path

Story 7.11 and the Postman fixture already document the original Plan Check contract:

- `GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`
- `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}`

Spatial Unit writeback also continues to succeed through the administrative LADM-object API family, so adding this fallback is consistent with the local integration pattern.

## Fix Applied

- Updated `InnolaPlanCheckService` so Plan lookup tries:
  1. UUID on `data/objects`
  2. displayed transaction number on `data/objects`
  3. displayed transaction number on `administrative/ladm-objects`
- Saves the mutated Plan through the same route that returned the Plan:
  - `PUT` for `data/objects`
  - `POST` for `administrative/ladm-objects`
- Extended failure diagnostics so a future failure names both lookup keys and both route families.
- Added regression coverage for the exact `100000623` shape: UUID data route empty, transaction-number data route HTTP 500, administrative route succeeds, then save uses administrative `POST`.

## Verification

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj --no-restore -v:q -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:GenerateTargetPlatformAttribute=false --no-incremental` passed with existing ArcGIS platform analyzer warnings.
- Focused test run passed 16 tests, including `innola plan check service falls back to administrative plan route when data route fails`.
- Add-in package rebuilt and registered as version `1.1.367`.

## Next Live Check

Restart ArcGIS Pro or otherwise ensure the add-in reloads version `1.1.367`, reopen transaction `100000623`, and press Finalize again.

If it still fails, inspect the new `working/plan_examination_api_failure.json`. A failure mentioning `Administrative route fallback` means Innola is rejecting both documented Plan route families for that transaction and the next evidence needed is the Innola server response/backend log for transaction `100000623`.

## Follow-up: 2026-09-03 #2

### New Evidence

- Confirmed: latest retry wrote `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000623\working\plan_examination_api_failure.json` at `2026-09-04T00:26:27.9859789Z`.
- Confirmed: the failure message now includes both route families: `DataObjects` and `AdministrativeLadmObjects`.
- Confirmed: UUID lookup `019fe6d2-7c02-7bf5-95f3-08d2e47635b0` returned no Plan objects.
- Confirmed: displayed transaction-number lookup `100000623` returned `500 InternalServerError` on `data/objects`.
- Confirmed: displayed transaction-number lookup `100000623` returned `500 InternalServerError` on `administrative/ladm-objects`.
- Confirmed: no `plan_examination_api_request.json` or `plan_examination_api_response.json` exists, so the add-in did not reach Plan save or Neighbor save.
- Confirmed: `compute_review_disposition.json` remains `plan_check_api_status: null` and `working_package_upload_status: pending`.

### Finding

The latest user-visible message `Could not complete transaction. Try again.` is generic shell feedback. The durable case evidence still points to Plan Examination writeback as the blocker, specifically Plan GET failure before package upload or Innola task completion.

### Conclusion

Confidence: High.

The add-in now exercises the implemented fallback path, but Innola rejects both documented Plan lookup routes for transaction `100000623`. This is no longer a Neighbor value problem and no longer only a UUID-versus-transaction-number issue. The remaining blocker is the Innola Plan object/API state for this transaction.

### Needed To Unblock

- Innola backend/API log for transaction `100000623` at approximately `2026-09-04T00:26:27Z`, covering these requests:
  - `GET /api/v4/rest/data/objects?typeKeyId=plan&transactionId=100000623`
  - `GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId=100000623`
- Or a browser Network capture from the Innola Plan Examination page showing the successful request used to populate the Plan Check/Neighbors tabs for transaction `100000623`.

If Innola cannot return a Plan object for either route, the application should not finalize silently because Story 7.11 requires the Plan Examination values and reviewed Neighbors to be written before task completion.

## Follow-up: 2026-09-03 #3

### New Evidence Review

- Confirmed: Spatial Unit save succeeds for transaction `100000623` using the Innola UUID `019fe6d2-7c02-7bf5-95f3-08d2e47635b0` on the administrative API family.
- Confirmed: the previous Plan fallback tried administrative lookup only with displayed transaction number `100000623`.
- Deduced: certificate/session plumbing is not the likely blocker, because Spatial Unit and Plan Check share the same configured Innola HTTP client and certificate path.

### Patch Applied

- Added an additional Plan lookup attempt: `GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={uuid}`.
- New lookup order:
  1. UUID on `data/objects`
  2. UUID on `administrative/ladm-objects`
  3. displayed transaction number on `data/objects`
  4. displayed transaction number on `administrative/ladm-objects`
- Save still uses the route and lookup id that returned the Plan.

### Verification

- Add-in build passed with existing ArcGIS analyzer warnings.
- Focused Plan Examination test run passed 16 tests.
- Add-in package rebuilt and registered as version `1.1.369`.

### Next Live Check

Retry Finalize after ArcGIS Pro reloads add-in version `1.1.369`. If the UUID administrative route returns the Plan, the workflow should proceed to Plan save and Neighbor writeback. If it fails again, inspect the failure artifact; it should now show whether all four lookup combinations failed.

## Follow-up: 2026-09-03 #4

### New Evidence

- Confirmed: latest retry reached Neighbor handling and failed at `2026-09-04T00:44:12.2592422Z` with `error_message` = `Neighbor create-template POST failed: BadRequest`.
- Confirmed: this means the Plan lookup route ladder progressed past the previous Plan GET blocker.
- Confirmed: failure still happened before `plan_examination_api_request.json` and before final Plan save.

### Architecture Decision

Treat `POST /api/v4/rest/data/objects/create` as opportunistic for Neighbor defaults, not mandatory for Plan Examination writeback. Story 7.11 AC25 says the add-in may call it when needed; it does not require Finalize to fail when the endpoint rejects a default-object request.

### Patch Applied

- `CreateDefaultNeighborAsync` still calls the create-template endpoint first.
- If the endpoint returns non-success after auth/cookie retry handling, the add-in now creates a local nested `Neighbor` object with:
  - `@c = Neighbor`
  - `neighborType = neighbor_type_owner`
  - editable fields populated later from reviewed rows
  - `allowRead = true`
  - `allowWrite = true`
- Successful but malformed create-template responses still fail, because that indicates a contract mismatch rather than an endpoint-level rejection.

### Verification

- Build passed with existing ArcGIS analyzer warnings.
- Focused Plan Examination run passed 17 tests, including `innola plan check service falls back to local neighbor when template create returns bad request`.
- Add-in package rebuilt and registered as version `1.1.371`.

### Next Live Check

Retry Finalize with add-in `1.1.371`. If the embedded local Neighbor is accepted, the next expected artifacts are `plan_examination_api_request.json` and `plan_examination_api_response.json`. If Innola rejects the final Plan save, the next failure artifact should move from create-template failure to Plan save failure, which will identify the remaining payload contract issue.
