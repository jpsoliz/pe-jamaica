---
baseline_commit: handoff-2026-08-05
---

# Story 7.14: Attach Formatted Compute Finalize Report to Innola Transaction

Status: review

## Story

As a cadastral examiner finalizing a Compute transaction,  
I want a formatted Compute report PDF saved locally and attached to the Innola transaction,  
so that the transaction record contains the complete reviewed evidence used to move the workflow forward.

## Business Context

Compute Finalize is the official handoff from ArcGIS Pro back to Innola. The add-in already creates local Compute examination report artifacts and writes Plan Check evidence, but the transaction also needs an examiner-facing PDF attached directly to Innola under the document/source type `st_compute_report`.

This report must be suitable for audit and supervisor review. It should summarize the final reviewed data, not raw extraction noise, and it must be generated from saved case artifacts so reopening or retrying Finalize uses the same evidence.

This is the Compute equivalent of the Compare report attachment behavior already tracked in Story 8.5A, but with Compute-specific sections and the `st_compute_report` source type.

## Acceptance Criteria

1. Given a Compute transaction reaches Finalize, when Finalize starts report closeout, then the add-in generates a PDF report under the case repository reports folder before completing the transaction.
2. Given the PDF report is generated, then the add-in attaches/uploads the PDF to the active Innola transaction using document/source type `st_compute_report`.
3. Given report generation or upload fails, then Finalize stops before transaction completion, shows a clear non-secret message, and keeps the local case state available for retry.
4. Given Finalize is retried after a previous partial attempt, then report generation overwrites or safely replaces the local current report and does not create confusing duplicate local report artifacts.
5. Given the report is opened, then it contains these sections in order:
   - Transaction Info
   - General Info
   - Owners / Neighbors / Participants
   - Boundary Segments
   - Points
6. Given the Transaction Info section is rendered, then it includes the latest loaded transaction metadata available from Innola/session state, including transaction number/id, transaction type, task/stage name, status, received/created date when available, assigned user/group when available, applicant, owner/responsible party, operator, source system/server, and generated timestamp.
7. Given the General Info section is rendered, then it uses a two-column Field/Value format where field labels are bold and values are regular weight.
8. Given General Info evidence exists, then the report includes the latest reviewed values shown in the PXA/PE review tabs where available, including coordinate system, document area, file reference, north arrow, parish, plan check date, survey date, survey instrument, surveyed by / surveyor, registration details, and source document name.
9. Given Volume/Folio was extracted or manually reviewed, then the General Info section includes a `Volume / Folio` table with `Volume`, `Folio`, `Raw Text`, `Source`, and `Status`. If no reviewed Volume/Folio evidence exists, the report explicitly says `Not found`.
10. Given Owners / Neighbors / Participants evidence exists, then the report includes all reviewed people/entity rows from Compute review, including party/owner, representative, occupant, adjoining owner, neighbor, and related names where available.
11. Given a person/entity role is unclear, when the report is rendered, then the role is labeled as `Participant` instead of guessing owner, neighbor, or possessor.
12. Given adjacent owner / neighbor rows exist, then the report includes an `Adjacent Owners / Neighbors` table with `Name`, `Role`, `From`, `To`, `Vol.`, `Folio`, and `Status` where available.
13. Given no owner, neighbor, possessor, representative, or participant evidence exists, then the section says `No owner, neighbor, possessor, representative, or participant evidence recorded.`
14. Given Boundary Segments are rendered, then the report includes only the latest reviewed segments marked for use in point/parcel generation after examiner edits, additions, exclusions, and rebuild actions.
15. Given Boundary Segments are rendered, then the table columns are `Seq`, `From`, `To`, `Bearing`, `Distance`, and `Notes`.
16. Given Points are rendered, then the table includes the latest reviewed point rows after examiner edits, additions, deletions, and rebuild actions.
17. Given Points are rendered, then the table columns are `Point`, `Easting`, `Northing`, and `Sequence`.
18. Given coordinate values are rendered, then the report states that coordinates are in JAD2001 / EPSG:3448 metres.
19. Given the PDF is generated, then it is formatted with a title, section headings, bold field labels, bold table headers, and readable table rows; it must not be a plain unstructured text dump.
20. Given the PDF is attached, then the uploaded source registration preserves `st_compute_report` and must not be rewritten to the resume package or completed package source type.
21. Given a previous `st_compute_report` attachment exists for the same transaction, when Finalize uploads the new Compute report, then the previous report is replaced/overwritten so Innola retains only the current Compute Finalize report for that transaction.
22. Given the attachment succeeds, then local evidence records the report path, PDF path, source type, upload status, transaction id/number, operator, and timestamp without logging tokens, passwords, certificate material, or API keys.
23. Given the attachment succeeds and later closeout steps succeed, then the user sees the normal successful Finalize completion behavior and the transaction can move to the next Innola workflow stage.
24. Given automated tests run, then coverage proves transaction metadata content, reviewed General Info content, Volume/Folio table content, participants/adjacent-neighbor content, used-segment filtering, latest point table content, PDF creation, `st_compute_report` upload/replacement, failure short-circuit behavior, and sanitized diagnostics.

## Tasks / Subtasks

- [x] Extend the Compute report document model. (AC: 5-18)
  - [x] Add structured report data for Transaction Info, General Info, Owners / Neighbors / Participants, Boundary Segments, and Points.
  - [x] Read values from saved case artifacts and reviewed data, not a fresh extraction run.
  - [x] Include transaction number/id, transaction type, task/stage, status, dates, assigned user/group, applicant, owner/responsible, operator, server/source, and generated timestamp where available.
  - [x] Include reviewed General Info rows from the PXA/PE review data: coordinate system, document area, file reference, north arrow, parish, plan check date, survey date, survey instrument, surveyed by / surveyor, registration details, and source document name.
  - [x] Include Volume/Folio evidence from reviewed general info or extraction metadata.
  - [x] Include owner/occupant/representative/neighbor/adjoining owner evidence where available, using `Participant` when the role is unclear.
  - [x] Include only the latest reviewed segments with `Use for points`/generation enabled.
  - [x] Include the latest reviewed points and sequence order after examiner edits, additions, deletions, and rebuild actions.

- [x] Improve Compute PDF rendering. (AC: 7, 9, 12, 15, 17-19)
  - [x] Reuse or extend `ComputeExaminationReportService` rather than creating an unrelated report path.
  - [x] Extend the current PDF writer or add a small internal rendering layer that supports bold headings, bold labels, and table headers.
  - [x] Render Transaction Info and General Info as Field/Value rows.
  - [x] Render Volume/Folio, participants, adjacent owners/neighbors, boundary segments, and points as readable tables.
  - [x] Keep the report layout deterministic for tests.
  - [x] Preserve the existing JSON report artifact for Plan Check and audit consumers.

- [x] Attach the Compute PDF report to Innola on Finalize. (AC: 1-4, 20-23)
  - [x] Add a Compute report attachment service or reuse a generic attachment upload service.
  - [x] Use source type `st_compute_report`.
  - [x] Replace/overwrite any previous `st_compute_report` attachment for the same transaction.
  - [x] Wire attachment into the Compute Finalize closeout flow after the report is generated and before task completion.
  - [x] Stop Finalize on report upload failure and show a retryable, non-secret message.
  - [x] Keep Compare report attachment behavior unchanged.

- [x] Persist sanitized diagnostics. (AC: 22)
  - [x] Record local report path, PDF path, source type, file size, upload status, transaction id/number, operator, and timestamps.
  - [x] Redact credentials, tokens, certificate material, and API keys from logs and artifacts.

- [x] Add regression tests. (AC: 24)
  - [x] Report JSON contains Transaction Info metadata.
  - [x] Report JSON contains reviewed General Info including Volume/Folio.
  - [x] Report JSON contains participants and adjacent owners/neighbors with unclear roles normalized to `Participant`.
  - [x] PDF is created and contains the expected section headings/table text.
  - [x] Boundary Segments include only used segments.
  - [x] Points table includes the latest reviewed point label, easting, northing, and sequence.
  - [x] Finalize uploads the generated PDF as `st_compute_report`.
  - [x] Finalize replaces/overwrites an existing `st_compute_report` attachment for the same transaction.
  - [x] Upload failure blocks task completion and preserves retryable local state.

## Developer Notes

Relevant existing implementation:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReportAttachmentService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`

Existing local report artifacts:

- `output/reports/compute_examination_report.json`
- `output/reports/compute_examination_report.pdf`

Recommended Innola attachment:

- File: `output/reports/compute_examination_report.pdf`
- Document/source type: `st_compute_report`

Report data source guidance:

- Prefer saved reviewed artifacts such as `working/approved_review.json`, `working/extraction_review_data.json`, `manifest.json`, `output/output_summary.json`, and closeout/disposition artifacts.
- Do not recompute extraction during Finalize.
- The report must reflect the latest examiner-reviewed state at Finalize time. If the examiner edited General Info, Participants, Boundary Segments, or Points in the review UI, the PDF and JSON report must use those saved reviewed values, not the original raw extraction values.
- Transaction Info should come from the loaded Innola transaction/session metadata when available, with graceful `Not provided` values for optional fields that Innola did not return.
- General Info should mirror the reviewed fields visible in the Compute/PXA review tab: coordinate system, document area, file reference, north arrow, parish, plan check date, survey date, survey instrument, surveyed by / surveyor, registration details, source document, and reviewed Volume/Folio rows.
- Owners / Neighbors / Participants should combine reviewed party/owner/representative rows and adjacent owner/neighbor rows. Keep the two concepts readable in the PDF, but use the same section so the report does not lose ambiguous participant evidence.
- If the exact owner/neighbor role is unclear, render it as `Participant`; do not guess whether the person/entity is an owner, neighbor, or possessor.
- If the exact owner/neighbor evidence source is missing, render the section with a clear `No owner, neighbor, possessor, or participant evidence recorded.` message rather than failing Finalize.
- Keep `st_compare_report` isolated to Compare. Compute Finalize must use `st_compute_report`.
- Compute Finalize should replace any prior `st_compute_report` attachment for the same transaction. Innola should show the current report, not a history of stale Compute reports.

Formatting answer:

- The PDF can and should be formatted. Bold field labels, section headings, table headers, and simple table borders are in scope. The report should not be limited to plain text.

## Open Questions

1. Resolved: unclear owner/neighbor/possessor names are reported as `Participant`.
2. Resolved: the uploaded PDF replaces/overwrites any prior `st_compute_report` attachment for the same transaction.

## Testing Notes

Recommended commands:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- workflow
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- innola
```

Executed:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "innola attachment upload replaces"
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compute examination report"
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
```

Notes:

- The story-specific tests passed and the solution build succeeded.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- innola` now passes the new compute-report lifecycle tests, but still stops later in the unrelated Spatial Unit endpoint-login test: `Rejected Spatial Unit API login should fail`.
- 2026-08-05 code patch re-ran `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compute examination report"` and `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`; both passed.

## Dev Agent Record

Implementation summary:

- Extended Compute report generation to render structured Transaction Info, General Info, Owners / Neighbors / Participants, Boundary Segments, and Points sections into JSON and formatted PDF artifacts.
- Added bold PDF headings, field labels, and table headers while preserving deterministic internal PDF generation.
- Added Compute report attachment upload on Finalize using `st_compute_report`, before Plan Check and before transaction completion.
- Added replacement behavior for existing `st_compute_report` sources so the latest Compute Finalize report is retained.
- Added sanitized local attachment evidence in `working/compute_report_attachment.json`.
- Added tests for report content, used-segment filtering, PDF bold text support, upload replacement, lifecycle success, and upload-failure short-circuit behavior.
- Patched Compute report generation to read the full reviewed `extraction_review_data.json` artifact for Transaction Info, General Info, Volume/Folio, participants, adjacent owners/neighbors, used boundary segments, and latest point rows.
- Expanded the report regression fixture so it proves the report uses reviewed tab data rather than only the approval summary.

File List:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeReportAttachmentService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Disposition/ComputeReviewDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ComputeExaminationReportServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-05 | 0.1 | Created story for formatted Compute Finalize PDF report attachment to Innola as `st_compute_report`. | Mary / Codex |
| 2026-08-05 | 0.2 | Clarified participant labeling for ambiguous parties and required replacement of prior `st_compute_report` attachments. | Mary / Codex |
| 2026-08-05 | 1.0 | Implemented formatted Compute PDF generation, `st_compute_report` upload/replacement on Finalize, diagnostics, and regression coverage. | Amelia / Codex |
| 2026-08-05 | 1.1 | Expanded report scope to include Transaction Info, reviewed General Info tab data, participants/adjacent neighbors, and latest reviewed segment/point edits at Finalize. | Mary / Codex |
| 2026-08-05 | 1.2 | Patched code and tests so Compute Finalize report generation consumes the reviewed extraction artifact for all Story 7-14 report sections. | Amelia / Codex |
