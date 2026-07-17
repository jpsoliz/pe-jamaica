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

This story should add the live adapter conservatively: start with the confirmed Volume/Folio request, keep the existing mock/test service behavior, and enable PID, Land Val No., and owner-name searches through the configured live adapter. When the configured adapter is `innola_owner_search`, requests must use the Postman-confirmed `SearchRequest` envelope against `portal/searches`. Owner-name search is name-only and must support `%` wildcard matching.

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
7. Given PID, Land Val No., or owner-name search values are provided, when those modes run against the live adapter, then the adapter posts the configured live search payload and maps normalized legal evidence results.
8. Given mock mode is enabled, when Compare searches evidence, then existing mock query behavior continues to work without requiring live Innola access.
9. Given automated tests run, then they verify endpoint construction, `Access-Token` application, exact Volume/Folio payload construction, no-record handling, error redaction, and normalized mapping from a fixture response.
10. Given the full Innola response fixture is later added, when tests run, then response field mapping is locked with regression coverage before enabling additional query modes.

## Amendment: Party-Shaped Innola Results

Live Innola searches can return two result shapes through the same Postman-confirmed search contract:

- Property / BA Unit rows with parcel evidence fields such as Volume/Folio, PID, LandVal No., owner, parish, tenure, type, and registration date.
- Party-shaped rows with fields such as `type = party_type_individual`, `fullname`, `prid`, `fulladdress`, `taxnumber`, and `status`.

The Compare workspace must preserve the existing Postman payload contract while presenting these shapes separately. Party-shaped rows must not be rendered as property / BA Unit rows because that creates blank or misleading legal evidence in the main Search Results grid. They still represent useful evidence and must be reviewable, keepable, and reportable as party matches.

### Amendment Acceptance Criteria

11. Given PID, Land Val No., or owner-name search returns property / BA Unit rows, when Compare maps the response, then those rows continue to appear in the existing Search Results grid with Volume/Folio, Type, Tenure, PID, LandVal No., Owner, Parish, Date Registered, and Keep action.
12. Given PID, Land Val No., or owner-name search returns rows where the mapped type starts with `party_type_`, when Compare maps the response, then those rows do not appear in the property / BA Unit Search Results grid unless they also contain real property evidence fields.
13. Given a `party_type_*` row contains party evidence such as `fullname`, `prid`, `fulladdress`, `taxnumber`, or `status`, when Compare maps the response, then the row appears in a separate Related Party Matches result area.
14. Given Related Party Matches are shown, when the user selects Keep, then the evidence is added to Valuable Evidence with a clear party-oriented source/summary such as party name, PRID, address, tax number, and status where available.
15. Given the user saves, suspends, or finalizes Compare, when kept party evidence exists, then that evidence is persisted with other Valuable Evidence and is included in the future Compare review report.
16. Given the adapter builds Innola requests for this amendment, when the request is sent, then the payload contract remains the Postman-confirmed contract for the selected mode; no endpoint, search kind, field name, pagination, wildcard, auth, or envelope change is allowed as part of party result display.
17. Given automated tests run, then regression coverage proves `party_type_individual` rows with only party identifiers are excluded from the property grid, included in Related Party Matches, and can be kept as Valuable Evidence.

## Tasks / Subtasks

- [x] Add live Innola BA Unit adapter. (AC: 1-8)
  - [x] Create `InnolaBaUnitLegalCadasterQueryService` or equivalent behind `ILegalCadasterQueryService`.
  - [x] Route `QueryByVolumeFolioAsync` to the confirmed live request.
  - [x] Route PID, Land Val No., and owner-name searches through the configured live adapter once payload variants are confirmed.

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
  - [x] Verify PID, Land Val No., and owner-name payloads in live mode, including the Postman owner-search envelope.
  - [x] Verify owner-name search adds `%` wildcards and preserves user-supplied wildcard values.
  - [x] Verify diagnostic redaction.
  - [x] Verify mapping with a representative successful response fixture.

- [x] Add party-shaped result classification without changing Innola payloads. (AC: 11-17)
  - [x] Keep the existing property / BA Unit mapping and filtering behavior for the Search Results grid.
  - [x] Add a party result model for `party_type_*` rows with fields for party name/full name, PRID, full address, tax number, status, and raw type label.
  - [x] Split mapped responses into property results and related party matches after receiving the existing Innola response.
  - [x] Do not show party-shaped rows in the property / BA Unit grid unless property evidence fields are also present.
  - [x] Add a Related Party Matches UI area with a Keep action.
  - [x] Ensure kept party evidence writes into Valuable Evidence with a party-specific summary and role selection.
  - [x] Include kept party evidence in the Compare report/export contract when the report story is implemented.
  - [x] Add regression tests for party-only rows, mixed property/party rows, Keep behavior, and payload-contract preservation.

### Review Findings

- [x] [Review][Patch] Validate manual Volume/Folio numeric input before calling the legal cadaster service [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs:694]
- [x] [Review][Patch] Apply `compare_cadaster_query_timeout_seconds` to live Innola BA Unit requests [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs:154]
- [x] [Review][Patch] Map single-record objects under known response containers such as `result` [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs:361]

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaHttp.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

Existing `ILegalCadasterQueryService` methods already match the Compare UI:

- `QueryByParcelIdAsync`
- `QueryByVolumeFolioAsync`
- `QueryByLandValuationNumberAsync`
- `QueryByNameAsync`

Party-shaped response handling must be a mapping/display concern only. Do not change the live Innola payload contract to solve party display:

- Preserve the Postman-confirmed `SearchRequest` envelope for `innola_owner_search`.
- Preserve configured field names for PID, Land Val No., and owner-name searches.
- Preserve pagination behavior and request paths.
- Preserve existing auth header behavior.
- Use `compare_legal_query_raw_debug.json` only for diagnostics and fixtures, not as a UI data source.

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
- Confirm whether additional Innola field aliases are needed for PID, Land Val No., or owner-name search after more production examples are captured.
- Confirm whether a follow-up enrichment API exists to resolve party `prid` rows back to linked BA Unit/property rows. If so, create a separate story; do not mix enrichment with this display-only amendment.

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
| 2026-07-17 | 1.3 | Enabled PID, Land Val No., and owner-name BA Unit search payloads; removed parish from owner search. | Amelia / Mary |
| 2026-07-17 | 1.4 | Restored the Postman-confirmed owner-search transport as the default Compare legal search path and stored the Postman collection fixture for regression reference. | Amelia |
| 2026-07-17 | 1.5 | Pinned Owner search to a single uppercase wildcard `owner` variable and added multi-row owner result mapping coverage. | Amelia |
| 2026-07-17 | 1.6 | Added amendment for party-shaped Innola results: keep payload contract unchanged, separate party matches from property results, and allow party evidence to be kept and reported. | Mary / Amelia |

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
- Owner-search Postman patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- compare` passed after storing the reference collection and pinning the Postman request envelope.
- Owner variable/result patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- compare` passed 92 compare-filtered tests.
- Owner variable/result patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed.
- Party-shaped result patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- compare` passed 102 compare-filtered tests.
- Party-shaped result patch: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings/errors.
- Party-shaped result patch: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 461 tests.

### Completion Notes

- Added a live Innola BA Unit legal cadaster adapter that posts the confirmed Volume/Folio payload to `search/` under the active Innola REST root.
- The adapter sends numeric `volume` and `folio`, applies the existing `Access-Token` header helper, returns reviewable no-record results, and redacts failure diagnostics.
- PID, Land Val No., and owner-name live modes now post through the configured Innola search adapter; owner-name search no longer requires parish and wraps names in `%` wildcards unless the user already provided `%`.
- Default `WorkflowSettings.json` uses the Postman-confirmed owner-search transport: `adapter = innola_owner_search`, `service_url = portal/searches`, and `search_kind = owner`.
- The Postman collection `Sidwell Plan Exam Scenario.postman_collection2.json` is stored in Compare test fixtures so request envelope changes can be checked against a real reference.
- Owner-name searches now send one Postman-style parameter, `owner`, normalized to an uppercase wildcard such as `%TRACEY%`; multi-row owner responses map into the results grid with Volume/Folio, Type, Tenure, PID, LandVal No., Owner, Parish, and Date Registered.
- Party-shaped Innola rows are now split into Related Party Matches instead of appearing as blank or misleading property Search Results rows.
- Related Party Matches can be kept as Valuable Evidence with a party-oriented summary, and kept party evidence is restored from Compare draft persistence with other retained evidence.
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
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareCadasterQueryServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
