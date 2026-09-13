---
baseline_commit: wipRC9-current-testing
---

# Story 12.3: Add Computed Participants And Title Image Download To Compare

Status: done

## Story

As a cadastral examiner working a Compare transaction,
I want to see the participant/neighbor records produced by Compute and download the matching current title image by Volume/Folio,
so that I can review the submitted survey evidence and the registered title document in one Compare workspace without manually leaving ArcGIS Pro.

## Business Context

Compare currently supports manual ownership evidence search, retained evidence, notes, overlap review, and final task actions. The next presentation/testing gap is that the form does not show the Compute participant/neighbor list that was already sent back to Innola from Plan Examination. That list is the correct row-level launch point for a title-image search.

This story adds a local-only title document lookup/download path for computed participants. It must not register the downloaded document back to Innola and must not expose the same action from generic ownership search results.

## UX Decisions Locked

- Keep one Compare form.
- Add a `Computed Participants` tab or equivalent section in the Compare workspace.
- The tab shows the participant/neighbor rows from Compute/PE review artifacts.
- `Search Title Image` is available only from selected `Computed Participants` rows.
- Do not add `Search Title Image` to Ownership Search Results or Related Party Matches.
- When multiple current title source records are returned, show a small selection dialog before download.
- Downloaded file name must be `Repo_[Volume]_[Folio].pdf`; example: `Repo_2000_1.pdf`.
- Save downloaded title documents only into the local case-folder `source` directory and refresh the Compare document/PDF selector.
- Do not register the downloaded title document back to Innola.

## Acceptance Criteria

1. Given a Compare transaction opens with a case folder that contains Compute/PE review evidence, when the workspace loads, then the Compare form shows a `Computed Participants` list populated from the Compute participants/owners/neighbors artifact used for Innola writeback.
2. Given a participant row contains `Volume` and `Folio`, when the row is selected, then `Search Title Image` is enabled for that row.
3. Given a participant row does not contain both `Volume` and `Folio`, when the row is selected, then `Search Title Image` is disabled or shows a field-specific message without calling Innola.
4. Given `Search Title Image` is launched, when the Innola source search runs, then the request uses `POST /api/v4/rest/search/` with `searchKind = source`, `statusLatest = true`, `status = reg_status_current`, `documentTypes = ["st_title"]`, and `referenceNo = "{Volume}/{Folio}"`.
5. Given source search returns no current title sources, when the call completes, then the Compare UI shows a non-blocking no-record message and preserves existing documents, participants, notes, and evidence.
6. Given source search returns one current title source, when the source object has a downloadable `body.id`, then the add-in downloads the source body and writes it to the case `source` folder as `Repo_[Volume]_[Folio].pdf`.
7. Given source search returns more than one current title source, when the call completes, then the add-in shows a compact selection dialog and downloads only the source chosen by the examiner.
8. Given the selected source object cannot be read or has no usable `body.id`, when download is attempted, then the UI shows a safe diagnostic and no empty/broken source file is added.
9. Given download succeeds, when the Compare workspace remains open, then the downloaded title appears in the Compare PDF/document selector without reopening the transaction.
10. Given a downloaded file name already exists in the source folder, when another title download uses the same Volume/Folio, then the implementation must avoid silent overwrite by using the existing duplicate-file naming convention or a deterministic suffix.
11. Given the downloaded title is local-only, when save/suspend/finalize is run, then the title file is not registered back to Innola as a transaction source unless a later story explicitly adds that behavior.
12. Given an Innola session has expired, when source search/object read/body download starts, then each Innola request obtains a current session through `InnolaSessionManager.EnsureCurrentSessionAsync` or an existing wrapper that calls it.
13. Given auth/network/service errors occur, when the feature fails, then tokens, passwords, cookies, authorization headers, and certificate material are not shown or written to artifacts.
14. Given automated tests run, then coverage proves participant loading, Volume/Folio gating, exact search payload, multiple-result selection, local file write/manifest refresh, no Innola re-registration, auth failure diagnostics, and Compare document selector refresh.

## Tasks / Subtasks

- [x] Add Computed Participants model/projection for Compare. (AC: 1-3)
  - [x] Reuse existing Compute review artifact models where possible; do not create a second participant contract if `ExtractionReviewDocument.AdjacentOwners` or related reviewed participant rows already satisfy the data shape.
  - [x] Include at minimum `Name`, `Role`, `Lot Number`, `Address`, `LandVal No.`, `Exam No`, `Volume`, `Folio`, `Source`, and row status/diagnostic if available.
  - [x] Keep generic ownership search results separate from computed participants.

- [x] Add Compare UX for participant rows and row action. (AC: 1-3, 7, 9)
  - [x] Add a tab or compact section in `CompareWorkspaceWindow.xaml` for `Computed Participants`.
  - [x] Keep long grids bounded with scrollbars so search results cannot consume the whole form.
  - [x] Add row-scoped `Search Title Image` command that only binds to computed participant rows.
  - [x] Add a compact title-source selection dialog/window for multiple source results.

- [x] Add Innola title source lookup/download service. (AC: 4-8, 10, 12-13)
  - [x] Implement a service behind a mockable interface; do not call HTTP directly from the view model.
  - [x] Search current title sources using:

    ```json
    {
      "searchKind": "source",
      "params": {
        "statusLatest": true,
        "status": "reg_status_current",
        "documentTypes": ["st_title"],
        "referenceNo": "2000/1"
      },
      "page": 1,
      "start": 0,
      "limit": 25,
      "orderBy": "id",
      "orderAsc": true
    }
    ```

  - [x] For the selected source id, read the source object with `GET /api/v4/rest/portal/ladm-objects/{sourceId}?typeKeyId=source`.
  - [x] Download the document body with `GET /api/v4/rest/source/download?noredirect=1&bodyId={bodyId}`.
  - [x] Apply existing Innola auth/session helpers and redaction patterns.

- [x] Save downloaded title into the case folder and refresh documents. (AC: 6, 9-11)
  - [x] Write only under `CaseFolderLayout.SourceDirectory`.
  - [x] Use file name `Repo_[Volume]_[Folio].pdf`, sanitizing path-invalid characters and avoiding traversal.
  - [x] Update/reopen the case manifest or reuse source-file copy/attachment writer conventions so `CaseFolderStore.ReopenCaseFolder()` and Compare document lists see the file.
  - [x] Refresh `Documents`, `PdfDocuments`, `SelectedDocument`, and viewer state after successful download.
  - [x] Do not call Innola attachment registration/upload APIs for this local title file.

- [x] Add tests. (AC: 1-14)
  - [x] View-model tests for participant list load and command gating.
  - [x] Service tests for exact search payload and URL construction.
  - [x] Service tests for no-record, auth failure, missing `body.id`, and redacted diagnostics.
  - [x] Dialog/selection seam tests for multiple search results.
  - [x] Case-folder tests proving local `Repo_[Volume]_[Folio].pdf` is added without overwrite/traversal and appears after reopen.
  - [x] Compare workspace tests proving Ownership Search Results do not expose `Search Title Image`.

### Review Findings

- [x] [Review][Patch] Title source service does not use the 12-2 safe auth-refresh retry after a sent request gets 401/403. AC12 and the Story 12.2 contract require Innola reads/downloads to use the shared session-ensure path and one controlled refresh/retry for safe reads after auth failure, but `SendSearchRequestAsync` calls `InnolaApiResilience.SendAsync` directly and then either falls back to cookie-only auth or fails. If the Access-token expires between the preflight ensure and the POST `/search/`, the examiner can still see the same connection/search failure instead of a restored retry. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs:163]
- [x] [Review][Patch] Title source object read and body download bypass `InnolaApiResilience`, so transient failures and post-send auth failures are not handled consistently. AC12 covers source search, object read, and body download, but `ReadSourceObjectAsync` and `DownloadAsync` send raw `HttpClient.SendAsync` requests after a single ensure. A stale token or transient 5xx/timeout on either GET can fail the title-image workflow even though the existing shared resilience helper could retry safe reads without remote mutation risk. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs:321]

## Dev Notes

### Existing Implementation To Reuse

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
  - Owns Compare tabs/state, document selector collections, manual ownership search, valuable evidence, notes, save/suspend/finalize commands.
  - `ApplyLoadState()` already reopens the case folder and populates `Documents`/`PdfDocuments` from `CaseFolderStore.ReopenCaseFolder()`.
  - Extend this rather than creating a second Compare window.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
  - Current form has top buttons, ownership search, result grids, notes, and final actions. Keep the layout dense and bounded; avoid letting long result grids expand without `MaxHeight`/scrolling.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
  - Existing query/result models cover manual ownership search; add separate computed-participant/title-source models if needed.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
  - Compare draft persists notes, query history, valuable evidence, and enterprise evidence. Persist selected/downloaded title references only if useful for restore; do not persist secrets or raw title document bytes.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
  - Compute/PXA review data includes adjacent owner/participant-style rows used later by `InnolaPlanCheckService` for neighbor writeback.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
  - Review how neighbor rows are selected and sent to Innola; the Compare participant list should be based on the same reviewed row intent.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
  - Reopen behavior discovers source files and exposes them to document lists.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
  - Existing source download patterns and `source/download` request construction are relevant. Reuse helpers where possible, but this story searches by source `referenceNo`, not by transaction attachment.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`
  - All Innola reads/downloads must use the shared session-ensure path from Story 12.2.

### API Contract

The user-confirmed API flow is:

1. Search title sources:
   - Method: `POST`
   - Path: `/api/v4/rest/search/`
   - Payload: `searchKind = source`, current status, document type `st_title`, `referenceNo = Volume/Folio`.
2. Retrieve selected source object:
   - Method: `GET`
   - Path: `/api/v4/rest/portal/ladm-objects/{sourceId}?typeKeyId=source`.
3. Download body:
   - Method: `GET`
   - Path: `/api/v4/rest/source/download?noredirect=1&bodyId={bodyId}`.

Response mapping must be tolerant because production source result envelopes may use `data`, `records`, `items`, `result`, or nested source/body objects, as in prior Innola adapters.

### UX Guardrails

- Computed participants are document-derived transaction context; Ownership Search Results are generic search output. Keep them visually and functionally separate.
- Put `Search Title Image` beside/above the computed participants grid or as a row action, not in the generic search result grid.
- Multiple title results require human selection. Do not silently pick the first record.
- Download status must be visible and non-blocking: success, no record, multiple selection cancelled, missing body, auth failure, and network failure.
- After download, the examiner should be able to select/view the PDF immediately through the existing Compare document selector.

### Security And Recovery Guardrails

- Do not persist tokens, passwords, cookies, authorization headers, certificate details, or raw auth failure bodies.
- Do not write outside the case folder source directory.
- Do not overwrite existing source files silently.
- Do not register/upload the title document to Innola.
- If the session cannot be restored, stop before remote mutation/download and show `Innola connection could not be restored. Please log in again and retry.` or the current shared login-required copy.

### Project Structure Notes

- Put new Compare-specific models/services under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare` unless a service is clearly generic Innola infrastructure.
- Put any source-title HTTP helper under `Innola` only if it is reusable by non-Compare workflows; otherwise keep a Compare service that uses `InnolaHttp` helpers.
- Put tests under:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola` only for generic Innola transport/session behavior.

### Testing Notes

Run at minimum:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
```

If the full harness is run outside ArcGIS Pro, it may still stop at the known `ArcGIS.Desktop.Mapping` boundary. Relevant Compare/Innola tests must pass before that point.

## References

- `_bmad-output/implementation-artifacts/8-3-build-compare-workspace-evidence-reconciliation-ux.md`
- `_bmad-output/implementation-artifacts/8-4a-add-manual-compare-evidence-search-and-result-curation-ui.md`
- `_bmad-output/implementation-artifacts/8-4b-add-live-innola-baunit-search-adapter-for-compare.md`
- `_bmad-output/implementation-artifacts/12-2-centralize-innola-session-refresh-for-workflow-finalize.md`
- `_bmad-output/project-context.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareEvidenceModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSessionManager.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` - passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll compare` - 140 Compare tests passed.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll` - broad regression progressed through the pure tests, then stopped at the known external ArcGIS Pro SDK assembly boundary: `ArcGIS.Desktop.Mapping, Version=13.6.0.0`.
- `tools/package_addin.ps1 -Configuration Release` - passed; produced add-in package version `1.1.544`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` - review patch verification passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet exec src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compare title source"` - 9 title-source tests passed, including auth-refresh search/object/download coverage.
- `dotnet exec src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "compare"` - 148 Compare tests passed.
- `git diff --check` - passed with line-ending warnings only.
- `tools/package_addin.ps1 -Configuration Release` - passed; produced add-in package version `1.1.575`.

### Completion Notes List

- Added a computed participant projection in Compare using existing `ExtractionReviewDocument` party, representative, and adjacent-owner rows.
- Added a bounded `Computed Participants` Compare section with row selection, status feedback, and a `Search Title Image` action only on that computed participant surface.
- Added a compact source-selection window for multiple title-source results.
- Added an Innola title source service that uses the locked search/object/download routes, ensures the Innola session before remote requests, redacts diagnostics, writes local-only title PDFs into the case `source` folder, updates the manifest, and refreshes the Compare PDF selector.
- Preserved the local-only boundary: Save/Suspend/Finalize do not register downloaded title PDFs back to Innola.
- Patched review findings so title source search, source-object read, and document download use the shared safe auth-refresh retry path. Production wiring now starts with the active session and forces refresh only for auth-retry operations.

### File List

- `_bmad-output/implementation-artifacts/12-3-add-computed-participants-and-title-image-download-to-compare.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/project-context.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareTitleSourceSelectionWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareTitleSourceSelectionWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareTitleSourceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-09-12 | 1.0 | Created story for Compare computed participants and local-only title image search/download by Volume/Folio. | Mary / Sally / Codex |
| 2026-09-12 | 1.1 | Implemented computed participants, local-only title lookup/download, selector refresh, tests, and release package `1.1.544`. | Amelia / Codex |
