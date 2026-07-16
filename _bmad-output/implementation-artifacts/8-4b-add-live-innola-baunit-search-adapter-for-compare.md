---
baseline_commit: handoff-2026-07-14
---

# Story 8.4B: Add Live Innola BA Unit Search Adapter For Compare

Status: review

## Story

As a cadastral examiner in Compare,  
I want the manual evidence search fields to call the Innola BA Unit search service,  
so that Volume/Folio and later PID, Land Val No., and Name searches return real legal cadaster evidence for ownership reconciliation.

## Business Context

Stories 8.4 and 8.4A created the Compare evidence model, query service seam, manual search UI, query history, and valuable-evidence curation. The remaining gap is the live Innola search adapter. The first confirmed request shape is a BA Unit search by Volume/Folio from the Innola web client network trace.

This story should add the live adapter conservatively: start with the confirmed Volume/Folio request, keep the existing mock/test service behavior, and leave PID, Land Val No., and Name live calls behind explicit unsupported diagnostics until their exact Innola request payloads and response fields are confirmed.

## Confirmed Innola Request Evidence

The browser network trace for Volume/Folio search shows a `search/` request with this payload shape:

```json
{
  "searchKind": "baunit",
  "params": {
    "statusLatest": true,
    "type": "bu_type_land",
    "status": "reg_status_current",
    "volume": 1549,
    "folio": 583
  },
  "page": 1,
  "start": 0,
  "limit": 25
}
```

The trace also shows request metadata including `info.datamap = "BaUnitSearchDM"`. The full response contract is not confirmed yet, so response mapping must be tolerant and covered by tests with representative fixtures once a successful response is captured.

## Architecture Decision

- Add an Innola-specific adapter behind the existing `ILegalCadasterQueryService`.
- Do not call Innola directly from `CompareWorkspaceViewModel`.
- Use existing Innola HTTP helpers for base URL normalization, auth headers, JSON parsing, timeout behavior, and diagnostics redaction.
- Keep feature-service/ArcGIS geometry loading separate from Innola BA Unit legal evidence search.
- Keep credentials and tokens out of `WorkflowSettings.json`.

## Acceptance Criteria

1. Given `compare_legal_cadaster` is configured for the Innola BA Unit adapter, when the user runs a Volume/Folio search, then the add-in posts to the Innola REST search endpoint using the confirmed `searchKind = baunit` payload.
2. Given the transaction session has an access token, when the adapter sends the request, then it applies the existing Innola `Access-Token` header behavior without writing the token to diagnostics or artifacts.
3. Given Volume/Folio values are typed as text in the Compare UI, when the adapter builds the request, then numeric values are sent as JSON numbers and invalid numeric input is rejected before the service call.
4. Given Innola returns records, when the adapter maps the response, then Compare receives normalized legal evidence records with source label, query key, queried timestamp, and any available owner, parcel, volume/folio, land valuation number, parish, and record identifiers.
5. Given Innola returns no records, when the adapter completes, then Compare shows a `No record returned` query result without clearing existing valuable evidence.
6. Given Innola returns auth, timeout, invalid config, network, or service errors, when the adapter completes, then Compare shows a retryable non-secret diagnostic and preserves previous query history/evidence.
7. Given PID, Land Val No., or Name live request payloads are not yet confirmed, when those modes run against the live adapter, then the adapter returns a clear unsupported diagnostic rather than guessing a payload.
8. Given mock mode is enabled, when Compare searches evidence, then existing mock query behavior continues to work without requiring live Innola access.
9. Given automated tests run, then they verify endpoint construction, `Access-Token` application, exact Volume/Folio payload construction, no-record handling, error redaction, and normalized mapping from a fixture response.
10. Given the full Innola response fixture is later added, when tests run, then response field mapping is locked with regression coverage before enabling additional query modes.

## Tasks / Subtasks

- [x] Add live Innola BA Unit adapter. (AC: 1-8)
  - [x] Create `InnolaBaUnitLegalCadasterQueryService` or equivalent behind `ILegalCadasterQueryService`.
  - [x] Route only `QueryByVolumeFolioAsync` to the confirmed live request.
  - [x] Return unsupported diagnostics for PID, Land Val No., and Name until payloads are confirmed.

- [x] Add configuration support. (AC: 1, 8)
  - [x] Extend legal cadaster settings with adapter type, search path, datamap, search kind, BA Unit type/status, latest-status flag, page, start, and limit.
  - [x] Default the Innola search path to `/api/v4/rest/search/` or `search/` resolved against the configured Innola REST root.
  - [x] Keep mock mode and disabled live mode behavior unchanged.

- [x] Build request and auth handling. (AC: 1-3, 6, 9)
  - [x] Serialize the confirmed payload shape exactly for Volume/Folio.
  - [x] Include `info.datamap = "BaUnitSearchDM"` only if confirmed required by the endpoint or existing Innola client convention.
  - [x] Use existing `InnolaHttp.ApplyAuthHeaders`.
  - [x] Redact tokens, authorization headers, cookies, passwords, and raw unauthorized responses from diagnostics.

- [x] Add tolerant response mapping. (AC: 4-6, 10)
  - [x] Map known top-level collections such as `records`, `rows`, `data`, `items`, `value`, or `result`.
  - [x] Extract known fields only when present: owner/name, parcel/PID, volume, folio, title/record id, land valuation number, parish, status, and source role.
  - [x] Preserve source label/query key without persisting sensitive raw payloads.

- [x] Add tests. (AC: 1-10)
  - [x] Verify exact Volume/Folio JSON payload.
  - [x] Verify request URL resolution.
  - [x] Verify `Access-Token` header behavior.
  - [x] Verify no-record result.
  - [x] Verify unsupported PID/Land Val/Name diagnostics in live mode.
  - [x] Verify diagnostic redaction.
  - [x] Verify mapping with a representative successful response fixture.

### Review Findings

- [x] [Review][Patch] Validate manual Volume/Folio numeric input before calling the legal cadaster service [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs:694]
- [x] [Review][Patch] Apply `compare_cadaster_query_timeout_seconds` to live Innola BA Unit requests [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs:154]
- [x] [Review][Patch] Map single-record objects under known response containers such as `result` [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs:361]

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaHttp.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

Existing `ILegalCadasterQueryService` methods already match the Compare UI:

- `QueryByParcelIdAsync`
- `QueryByVolumeFolioAsync`
- `QueryByLandValuationNumberAsync`
- `QueryByNameAsync`

Recommended initial settings shape:

```json
"compare_legal_cadaster": {
  "enabled": true,
  "source_name": "Innola BA Unit",
  "adapter": "innola_baunit_search",
  "service_url": "search/",
  "datamap": "BaUnitSearchDM",
  "search_kind": "baunit",
  "baunit_type": "bu_type_land",
  "baunit_status": "reg_status_current",
  "status_latest": true,
  "page": 1,
  "start": 0,
  "limit": 25
}
```

If the app already has an Innola REST root setting, resolve `service_url` relative to that root instead of requiring a full URL in `WorkflowSettings.json`.

## Open Questions

- Confirm the full request URL from the DevTools Headers tab.
- Confirm whether `info.datamap = "BaUnitSearchDM"` is required for `/search/`.
- Capture one successful response body for Volume/Folio so owner, BA Unit, party, and parcel fields can be mapped accurately.
- Confirm the payloads for PID, Land Val No., and Name + Parish search before implementing those live modes.

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-15 | 1.0 | Initial story for live Innola BA Unit search adapter in Compare. | Mary / Winston |
| 2026-07-15 | 1.1 | Implemented live Innola BA Unit Volume/Folio adapter, settings, ShellState token hookup, and regression tests. | Amelia |
| 2026-07-15 | 1.2 | Patched code review findings for numeric validation, configured timeout use, and single-record response mapping. | Amelia |

## Dev Agent Record

### Implementation Plan

1. Add red tests for Innola BA Unit payload, endpoint/auth handling, response mapping, no-record behavior, unsupported modes, redaction, and factory wiring.
2. Extend Compare legal cadaster settings for the Innola BA Unit adapter while preserving mock mode and unsupported generic live adapters.
3. Implement `InnolaBaUnitLegalCadasterQueryService` behind `ILegalCadasterQueryService`, limited to confirmed Volume/Folio live calls.
4. Wire Compare launch through `ShellState` with the active Innola session token and shared HTTP client.
5. Enable the default legal cadaster Compare configuration for `innola_baunit_search`.

### Debug Log

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare innola baunit"` initially failed because the adapter/settings fields did not exist.
- After implementation, focused BA Unit tests passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"` passed 54 compare-filtered tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings/errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 413 tests.
- Review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"` passed 57 compare-filtered tests.
- Review patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings/errors.
- Review patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 416 tests.

### Completion Notes

- Added a live Innola BA Unit legal cadaster adapter that posts the confirmed Volume/Folio payload to `search/` under the active Innola REST root.
- The adapter sends numeric `volume` and `folio`, applies the existing `Access-Token` header helper, returns reviewable no-record results, and redacts failure diagnostics.
- PID, Land Val No., and Name + Parish live modes intentionally return unsupported diagnostics until their Innola payloads are confirmed.
- Added tolerant response mapping for common record collection names and likely owner/parcel/volume/folio/title/land valuation/parish/role fields.
- Updated Compare launch wiring so the live adapter can access the current in-memory Innola session without persisting secrets.
- Resolved all 3 code review findings: manual Volume/Folio numeric validation now blocks service calls, live BA Unit searches use a timeout-linked cancellation token from `compare_cadaster_query_timeout_seconds`, and single-record objects under known response containers now map correctly.

### File List

- `_bmad-output/implementation-artifacts/8-4b-add-live-innola-baunit-search-adapter-for-compare.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareCadasterQueryServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
