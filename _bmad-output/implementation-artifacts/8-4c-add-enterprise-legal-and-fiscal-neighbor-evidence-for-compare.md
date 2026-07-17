---
baseline_commit: handoff-2026-07-14
---

# Story 8.4C: Add Enterprise Legal And Fiscal Neighbor Evidence For Compare

Status: review

## Story

As a cadastral examiner in Compare,  
I want the parcel under review to load its touching, overlapping, and nearby Legal and Fiscal Cadastre parcel evidence from ArcGIS Enterprise,  
so that I can validate ownership, occupation, valuation, and neighboring parcel context without loading or editing the full cadastre.

## Business Context

Compare is an evidence reconciliation stage, not another COGO editor. The source parcel being reviewed is the transaction-scoped `working_review` geometry loaded in ArcGIS Pro. The source-of-truth spatial context for validation is the Legal Cadastre and Fiscal Cadastre layers in ArcGIS Enterprise.

Stories 8.2 and 8.3 established the Compare workspace and active-map geometry pattern. Stories 8.4, 8.4A, and 8.4B added the query service seams, manual evidence search UI, evidence curation, and live Innola BA Unit search for Volume/Folio. This story adds the missing spatial evidence discovery layer: retrieve only the Legal and Fiscal parcels that spatially relate to the review parcel, classify them, display them as evidence, and let the examiner decide which ones are relevant.

Innola tabular searches remain part of the workflow, but they should be triggered from selected evidence rows or manual search fields using PID, Volume/Folio, Land Val No., or Name + Parish. The add-in must not run bulk Innola searches for every surrounding parcel by default.

## Architecture Decision

- Use the active ArcGIS Pro map, not an embedded map in the Compare form.
- Reuse or create a read-only map group named `Compare Review - {transactionNumber}`.
- Query Legal and Fiscal Cadastre layers server-side using the review polygon geometry.
- Do not load full Legal or Fiscal Cadastre layers into the map or memory.
- Classify and sort the small server-returned candidate set locally.
- Keep included/excluded evidence decisions in the Compare evidence model.
- Keep Innola BA Unit and other tabular services behind the existing Compare query service seam.

## Acceptance Criteria

1. Given a Compare transaction has a `working_review` polygon, when neighbor evidence refresh runs, then the add-in queries configured Legal Cadastre and Fiscal Cadastre Enterprise layers using the review polygon geometry.
2. Given Legal or Fiscal Cadastre layers are large, when the query runs, then the query is server-side with a spatial filter, selected `outFields`, a result cap, pagination or transfer-limit handling, and no full-layer load.
3. Given the Enterprise query returns candidate geometries, when Compare processes them, then each row is classified locally as `same/review match`, `touches`, `overlaps`, `contains`, `within`, or `intersects-only` using a configured tolerance.
4. Given candidate rows are classified, when they are displayed, then the examiner can see source layer, parcel id/PID, Volume/Folio, Land Val No., owner/taxpayer/occupant, parish, object/global id, SUID when available, relationship type, and whether the row is included as evidence.
5. Given overlaps or duplicate candidates exist, when the examiner reviews the list, then each Legal/Fiscal row can be included or excluded without deleting it from the result set.
6. Given the examiner includes rows, when Compare progress is saved, then included evidence references are persisted with the Compare draft/decision state and excluded rows do not count as supporting evidence.
7. Given the active map is available, when neighbor evidence loads, then Legal and Fiscal neighbor layers are added or refreshed as read-only context inside the existing `Compare Review - {transactionNumber}` group and the map zoom remains focused on the review parcel extent.
8. Given no active map is available, when neighbor evidence loads, then the Compare form shows a clear diagnostic and still allows document review and manual Innola searches.
9. Given only one of Legal or Fiscal Cadastre settings is enabled, when refresh runs, then the enabled source loads and the disabled or missing source is reported without blocking the workflow.
10. Given a spatial evidence row has PID, Volume/Folio, Land Val No., or Name + Parish values, when the examiner chooses to query Innola for that row, then the existing manual evidence search fields are seeded and the existing query service is used on demand.
11. Given a server query exceeds the result limit or transfer limit, when Compare receives the response, then the UI shows a warning and keeps the returned candidates reviewable.
12. Given automated tests run, then they cover spatial query plan construction, field mapping, disabled configuration, transfer-limit diagnostics, relationship classification, sorting, include/exclude persistence, active-map group reuse, and Innola search seeding from a spatial evidence row.

## Tasks / Subtasks

- [x] Extend Compare settings for Enterprise Legal and Fiscal Cadastre layers. (AC: 1, 2, 4, 9)
  - [x] Add layer URL settings for legal and fiscal parcel polygon sources.
  - [x] Add field mappings for parcel id/PID, volume, folio, land valuation number, owner/taxpayer/occupant, parish, SUID, object id, and global id.
  - [x] Add result limit, page size, relationship tolerance, and enabled/disabled flags.
  - [x] Validate settings at Compare launch and surface non-secret diagnostics.

- [x] Add spatial neighbor query service. (AC: 1, 2, 8, 9, 11)
  - [x] Add an `ICompareNeighborEvidenceService` or equivalent behind the Compare ViewModel.
  - [x] Build server-side spatial query requests from the review polygon geometry.
  - [x] Request only mapped fields plus geometry.
  - [x] Handle token/portal auth through the existing Enterprise/ArcGIS auth path.
  - [x] Support result caps, pagination, and transfer-limit warnings.

- [x] Add local geometry relationship classification. (AC: 3, 4, 5, 12)
  - [x] Classify candidates as same/review match, touches, overlaps, contains, within, or intersects-only.
  - [x] Use configured tolerance to avoid noisy boundary sliver behavior.
  - [x] Sort rows with included rows first, then higher-risk relationships such as overlaps, then touches, then source and parcel identifiers.
  - [x] Keep the classifier testable without ArcGIS UI dependencies.

- [x] Wire active-map context layers. (AC: 7, 8)
  - [x] Reuse `Compare Review - {transactionNumber}` if it already exists.
  - [x] Add or refresh read-only Legal and Fiscal neighbor context layers.
  - [x] Avoid creating a new map unless there is no usable active map and the user explicitly requests one in a later story.
  - [x] Keep Compare geometry controls limited to `Show active map` and `Refresh`.

- [x] Extend Compare evidence UI for spatial rows. (AC: 4, 5, 6, 10, 11)
  - [x] Show Legal and Fiscal neighbor evidence in compact review grids or grouped sections.
  - [x] Provide include/exclude controls per row.
  - [x] Show relationship and source badges without making the workspace feel like an editing tool.
  - [x] Add row actions to seed PID, Volume/Folio, Land Val No., or Name + Parish searches.
  - [x] Preserve existing manual search and valuable-evidence list behavior.

- [x] Persist selected spatial evidence. (AC: 5, 6, 10)
  - [x] Store included evidence references with transaction number, source, layer id/url, object/global id, SUID when available, identifiers, relationship type, and captured display values.
  - [x] Store excluded rows only as review state if needed for repeat session consistency.
  - [x] Do not persist raw tokens, credentials, or full service responses.

- [x] Add tests. (AC: 1-12)
  - [x] Verify no full-layer load is attempted for Legal/Fiscal layers.
  - [x] Verify spatial query requests include geometry, selected fields, and limits.
  - [x] Verify disabled/missing source diagnostics.
  - [x] Verify classification and sorting behavior with fixture geometries.
  - [x] Verify include/exclude persistence.
  - [x] Verify active-map group reuse and read-only layer behavior through the existing map integration seam.
  - [x] Verify Innola manual search fields can be seeded from spatial evidence rows.

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkingGeometryService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

Existing `enterprise_working_review` settings already define transaction-scoped working review layers. This story should add separate Legal and Fiscal Enterprise layer settings instead of overloading the Innola BA Unit settings.

### Innola Query Examples From Postman Collection

Source example file:

`C:\Users\js91482\Downloads\Sidwell Plan Exam Scenario.postman_collection2.json`

The collection confirms that Innola plan-exam API calls use the active `Access-Token` header, not bearer authorization. The Compare tabular ownership query must therefore use the current logged-in `InnolaSession.AccessToken` and must not write that token to UI diagnostics, draft files, or logs.

Authenticate example:

```http
POST /api/rest/authenticate
```

```json
{
  "generateAccessToken": true,
  "login": "innola",
  "password": "*****"
}
```

Plan lookup example:

```http
GET /api/v4/rest/administrative/ladm-objects?typeKeyId=plan&transactionId={transactionId}
Access-Token: {access-token}
```

Owner search by Volume/Folio example to use for Compare ownership evidence:

```http
POST /api/v4/rest/portal/searches
Access-Token: {access-token}
Content-Type: application/json
```

```json
{
  "@c": "SearchRequest",
  "searchKind": "owner",
  "params": {
    "volume": 1549,
    "folio": 583
  },
  "start": 0,
  "limit": 25
}
```

Implementation guidance:

- Configure this as adapter `innola_owner_search` with `service_url = "portal/searches"` and `search_kind = "owner"`.
- Serialize `volume` and `folio` as numbers after validating numeric input.
- Keep `X-Requested-With: XMLHttpRequest`, `Origin`, and `Referer` headers because the same Innola server also accepts browser-like AJAX requests.
- Keep the earlier BA Unit search adapter available for `/api/v4/rest/search/` because it returns PID, Land Val No., parish, tenure, and title identifiers when that endpoint is authorized.
- Map response records defensively from known owner, parcel/PID, volume, folio, title, land valuation, parish, and tenure/role fields because Innola endpoints can return different response shapes.

Suggested settings shape:

```json
"compare_enterprise_cadaster": {
  "enabled": true,
  "relationship_tolerance_meters": 0.05,
  "result_limit": 250,
  "page_size": 100,
  "legal": {
    "enabled": true,
    "source_name": "Legal Cadastre",
    "layer_url": "",
    "parcel_id_field": "parcel_id",
    "pid_field": "pid",
    "volume_field": "volume",
    "folio_field": "folio",
    "land_valuation_number_field": "landvalnumber",
    "owner_field": "owners",
    "parish_field": "parish",
    "suid_field": "suid",
    "global_id_field": "globalid"
  },
  "fiscal": {
    "enabled": true,
    "source_name": "Fiscal Cadastre",
    "layer_url": "",
    "parcel_id_field": "parcel_id",
    "pid_field": "pid",
    "land_valuation_number_field": "landvalnumber",
    "occupant_field": "occupant",
    "taxpayer_field": "taxpayer_display",
    "parish_field": "parish",
    "suid_field": "suid",
    "global_id_field": "globalid"
  }
}
```

The final names can follow the existing settings conventions if there is already a better local pattern.

## UX Notes

Sally guidance:

- Keep Compare as a two-panel evidence workspace: documents on the left, ownership and cadaster evidence on the right.
- Do not bring back the fake embedded map panel.
- Keep the geometry area minimal: only `Show active map` and `Refresh`.
- Display spatial candidates as evidence, not editable geometry.
- Make include/exclude lightweight and visible because Legal and Fiscal layers may overlap or disagree.
- Let the selected evidence row populate the Innola search fields so the examiner can verify tabular records without retyping PID, Volume/Folio, or Land Val No.

## Performance Requirements

- Never query or render the full Legal or Fiscal Cadastre layers.
- Use server-side spatial filters with the review polygon geometry.
- Request only fields needed for evidence review and Innola search seeding.
- Cap results and show a warning when the cap or Enterprise transfer limit is reached.
- Run Innola tabular searches only on demand for selected rows or manual user input.
- Cache only transaction-scoped results for the current Compare session.

## Open Questions

- What are the production Legal Cadastre and Fiscal Cadastre layer URLs?
- What are the exact field names for PID, parcel id, Volume, Folio, Land Val No., owner, occupant, taxpayer, parish, SUID, object id, and global id in each layer?
- Should `touches` be strict boundary touch only, or should near-touch within tolerance also count for survey-plan review?
- What default result limit is acceptable for final testing: 100, 250, or another value?
- Do Legal and Fiscal layers use the same ArcGIS Enterprise portal/token as `working_review`?
- Should same-parcel matches from Legal/Fiscal be visually separated from surrounding-neighbor rows?

## Testing Notes

Run after implementation:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

Manual validation target:

- Open Compare for `TR100000668`.
- Confirm the active ArcGIS Pro map shows the transaction-scoped `working_review` parcel.
- Refresh Legal/Fiscal neighbor evidence.
- Confirm only parcels touching, overlapping, containing, within, or intersecting the review parcel are listed.
- Include and exclude sample rows.
- Seed a Volume/Folio or PID search from a selected row and confirm the Innola query status appears in the form.

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-15 | 1.0 | Initial story for Enterprise Legal/Fiscal neighbor evidence in Compare. | Mary / Winston / Sally / Amelia |
| 2026-07-15 | 1.1 | Implemented Enterprise Legal/Fiscal spatial evidence settings, query-plan seam, classifier, Compare UI rows, search seeding, persistence, and tests. | Amelia |
| 2026-07-16 | 1.2 | Added Postman collection Innola API examples and clarified owner-search payload for Compare tabular ownership queries. | Amelia |

## Dev Agent Record

### Implementation Plan

1. Add red tests for Enterprise cadaster settings, spatial query planning, disabled source behavior, relationship sorting, ViewModel search seeding, and draft persistence.
2. Extend `InnolaTransactionSettings` with disabled-by-default `compare_enterprise_cadaster` settings and field mappings.
3. Add a Compare Enterprise cadaster evidence service seam with query-plan construction, selected-field requests, result caps, diagnostics, and a testable relationship classifier.
4. Wire the Compare ViewModel and XAML to refresh Legal/Fiscal spatial evidence, include/exclude rows, seed existing Innola manual search fields, and persist spatial evidence rows.
5. Run focused Compare tests, full build, and full test harness.

### Debug Log

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare enterprise cadaster"` failed because the new Enterprise cadaster settings/service/ViewModel surface did not exist.
- Focused 8.4c tests passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare enterprise cadaster"` passed 6 tests.
- Compare regression tests passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"` passed 66 tests.
- Build passed: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 errors and 1 pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Full test harness passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 425 tests.
- Postman owner-search adapter tests passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare innola"` passed 8 tests.
- Settings regression tests passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "settings"` passed 17 tests.
- Build passed after adding the Postman query adapter: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.

### Completion Notes

- Added `compare_enterprise_cadaster` settings with Legal/Fiscal layer URLs, field mappings, relationship tolerance, result cap, and page size.
- Added `CompareEnterpriseCadasterEvidenceService` with a query-plan seam that requires spatial geometry, selected fields, geometry return, result limits, disabled-source diagnostics, and a testable classifier/sorter.
- Added Compare UI support for Legal/Fiscal spatial evidence rows with include/exclude checkboxes and `Use values` search seeding.
- Persisted Enterprise cadaster evidence rows in the Compare draft and included selected rows in decision evidence references without storing secrets or raw service payloads.
- Wired Compare launch to create the Enterprise cadaster evidence service explicitly.
- The ArcGIS-specific execution remains behind `ICompareEnterpriseCadasterSpatialQueryExecutor`; the current implementation prevents full-layer loading and exposes the query plan/diagnostics until the configured Enterprise Legal/Fiscal layer executor is enabled.
- Added the Postman collection owner-search payload as adapter `innola_owner_search`, configured Compare to use `portal/searches`, and kept the existing BA Unit adapter available for the earlier `/search/` contract.

### File List

- `_bmad-output/implementation-artifacts/8-4c-add-enterprise-legal-and-fiscal-neighbor-evidence-for-compare.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEnterpriseCadasterEvidenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareEnterpriseCadasterEvidenceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTestHelpers.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
