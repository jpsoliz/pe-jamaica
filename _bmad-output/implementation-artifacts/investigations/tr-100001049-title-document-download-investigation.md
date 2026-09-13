# TR 100001049 Title Document Download Investigation

## Hand-off Brief
Compare `Search Title Image` no longer fails at title-source search for TR 100001049; Innola returns current title source records for the selected Volume/Folio rows. The remaining failure is the document-body download, where Innola returns HTTP 500 with an S3 `The specified key does not exist` detail after the add-in requests the returned `bodyId`. The patch now keeps the local filename only for disk output, sends Innola the original source/body document name when available, and falls back to `sourceId` download when the `bodyId` path repeatedly fails with the S3 missing-key response.

## Case Info
- Transaction: 100001049
- Case folder: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001049`
- Evidence: `working\compare_title_source_trace.json`
- Investigation date: 2026-09-13
- Status: Patched with sourceId fallback, awaiting live retest

## Confirmed Evidence
- `compare_title_source_trace.json` records successful searches for `1298/168`, `1298/166`, and `1298/131`.
- Each search returned one current title source record.
- Each download failed with `500 InternalServerError`.
- The server response body includes `The specified key does not exist. (Service: S3, Status Code: 404...)`.
- No `Repo_1298_*.pdf` files were written into the case `source` folder after the failed downloads.

## Source Trace
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Compare\CompareComputedParticipantTitleService.cs` reads the selected source object through `/api/v4/rest/portal/ladm-objects/{sourceId}?typeKeyId=source`.
- The same service resolves `bodyId` and calls `/api/v4/rest/source/download`.
- The working transaction attachment downloader in `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Innola\InnolaTransactionDetailService.cs` supports `bodyId`, `sourceId`, and `sourceUid` through `/api/v4/rest/source/download`.
- Compare title download previously stopped after the `bodyId` route failed.
- Before the first patch, Compare title download sent `documentName=Repo_[Volume]_[Folio].pdf`, which is a local output naming convention, not Innola's original source document name.

## Final Conclusion
Confidence: Medium-high. The live server proves the source search and object selection are working, and the failure is specifically S3 object resolution during `bodyId` download. Since the source search returns a valid source object, the add-in should try the supported `sourceId` download route before giving up.

## Fix Direction
- Keep local downloaded CT naming as `Repo_[Volume]_[Folio].pdf`.
- Use source/body metadata to populate the Innola `documentName` query parameter.
- Continue using `bodyId` and `attachment=false`, matching the proven attachment download contract.
- If `bodyId` download still fails after retry with the Innola/S3 missing-key response, retry `/api/v4/rest/source/download` using `sourceId`.
- Add `fileBodyId` and `fileBody` as accepted body-id shapes for title source objects.

## Verification
- `dotnet exec src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compare title source"` - PASS 11 tests, including `compare title source download falls back to source id`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` - PASS.
- `git diff --check` - PASS, with existing CRLF conversion warnings only.
