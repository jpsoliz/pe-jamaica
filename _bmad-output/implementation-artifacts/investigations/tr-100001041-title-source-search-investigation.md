# TR 100001041 Title Source Search Investigation

## Hand-off Brief
The Compare `Search Title Image` button failed for TR 100001041 because Innola returned `401 Unauthorized` from `POST /api/v4/rest/search/` even after session refresh and cookie-only retry. The regular ownership search in the same form uses `POST /api/v4/rest/portal/searches`, which is the endpoint family already configured and proven for Compare searches. The fix keeps the original source-search payload, but falls back to `portal/searches` when the primary source endpoint rejects authorization.

## Case Info
- Transaction: 100001041
- Case folder: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001041`
- Evidence: `working/compare_title_source_trace.json`
- Investigation date: 2026-09-13
- Status: Concluded

## Confirmed Evidence
- `compare_title_source_trace.json` records `reference_no: 1149/558`.
- The first source search failed with `401 Unauthorized`.
- Cookie-only retry also failed with `401 Unauthorized`.
- The server response was `Full authentication is required to access this resource`.
- The regular ownership search path is configured as `portal/searches` in `Settings/WorkflowSettings.json`.

## Source Trace
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs` previously sent title source search only to `/api/v4/rest/search/`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs` uses configured Compare ownership endpoint `portal/searches`.

## Final Conclusion
Confidence: High for the observed failure mechanism, medium for server-side reason. The client had an active session and cookie, but `/search/` rejected this request with 401. The best client-side correction is endpoint fallback to `portal/searches`, matching the working Compare search route.

## Fix Direction
- Keep primary `POST /api/v4/rest/search/` for compatibility with the documented source-search sample.
- On auth rejection from that endpoint, retry the same `searchKind: source` / `documentTypes: st_title` payload through `POST /api/v4/rest/portal/searches`.
- Preserve safe diagnostics if both endpoints fail.

## Verification
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`
- `dotnet exec src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compare title source"`
