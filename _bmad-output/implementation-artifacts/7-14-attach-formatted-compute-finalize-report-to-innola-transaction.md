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
   - General Info
   - Owner / Neighbor Found
   - Boundary Segments
   - Points
6. Given the General Info section is rendered, then it uses a two-column Field/Value format where field labels are bold and values are regular weight.
7. Given General Info evidence exists, then the report includes at least transaction number, transaction type/task, applicant when available, owner/responsible when available, parish when available, source document name, generated timestamp, operator when available, and Volume/Folio found in the document.
8. Given Volume/Folio was extracted or manually reviewed, then the report labels it as `Volume / Folio` and includes the value found in the document. If it is unavailable, the report explicitly says `Not found`.
9. Given Owner / Neighbor evidence exists, then the report includes owner, occupant, adjoining owner, neighbor, and related names found during Compute review where available. If the role is unclear, the report labels the person/entity as `Participant`. If no evidence exists, the section says `No owner, neighbor, possessor, or participant evidence recorded.`
10. Given Boundary Segments are rendered, then the report includes only segments marked for use in point/parcel generation.
11. Given Boundary Segments are rendered, then the table columns are `Seq`, `From`, `To`, `Bearing`, `Distance`, and `Notes`.
12. Given Points are rendered, then the table columns are `Point`, `Easting`, `Northing`, and `Sequence`.
13. Given coordinate values are rendered, then the report states that coordinates are in JAD2001 / EPSG:3448 metres.
14. Given the PDF is generated, then it is formatted with a title, section headings, bold field labels, bold table headers, and readable table rows; it must not be a plain unstructured text dump.
15. Given the PDF is attached, then the uploaded source registration preserves `st_compute_report` and must not be rewritten to the resume package or completed package source type.
16. Given a previous `st_compute_report` attachment exists for the same transaction, when Finalize uploads the new Compute report, then the previous report is replaced/overwritten so Innola retains only the current Compute Finalize report for that transaction.
17. Given the attachment succeeds, then local evidence records the report path, PDF path, source type, upload status, transaction id/number, operator, and timestamp without logging tokens, passwords, certificate material, or API keys.
18. Given the attachment succeeds and later closeout steps succeed, then the user sees the normal successful Finalize completion behavior and the transaction can move to the next Innola workflow stage.
19. Given automated tests run, then coverage proves report content, used-segment filtering, point table content, PDF creation, `st_compute_report` upload/replacement, failure short-circuit behavior, and sanitized diagnostics.

## Tasks / Subtasks

- [x] Extend the Compute report document model. (AC: 5-13)
  - [x] Add structured report data for General Info, Owner / Neighbor Found, Boundary Segments, and Points.
  - [x] Read values from saved case artifacts and reviewed data, not a fresh extraction run.
  - [x] Include Volume/Folio evidence from reviewed general info or extraction metadata.
  - [x] Include owner/occupant/neighbor/adjoining owner evidence where available.
  - [x] Include only segments with `Use for points`/generation enabled.
  - [x] Include the final reviewed points and sequence order.

- [x] Improve Compute PDF rendering. (AC: 6, 11-14)
  - [x] Reuse or extend `ComputeExaminationReportService` rather than creating an unrelated report path.
  - [x] Extend the current PDF writer or add a small internal rendering layer that supports bold headings, bold labels, and table headers.
  - [x] Keep the report layout deterministic for tests.
  - [x] Preserve the existing JSON report artifact for Plan Check and audit consumers.

- [x] Attach the Compute PDF report to Innola on Finalize. (AC: 1-4, 15-18)
  - [x] Add a Compute report attachment service or reuse a generic attachment upload service.
  - [x] Use source type `st_compute_report`.
  - [x] Replace/overwrite any previous `st_compute_report` attachment for the same transaction.
  - [x] Wire attachment into the Compute Finalize closeout flow after the report is generated and before task completion.
  - [x] Stop Finalize on report upload failure and show a retryable, non-secret message.
  - [x] Keep Compare report attachment behavior unchanged.

- [x] Persist sanitized diagnostics. (AC: 17)
  - [x] Record local report path, PDF path, source type, file size, upload status, transaction id/number, operator, and timestamps.
  - [x] Redact credentials, tokens, certificate material, and API keys from logs and artifacts.

- [x] Add regression tests. (AC: 19)
  - [x] Report JSON contains General Info including Volume/Folio.
  - [x] PDF is created and contains the expected section headings/table text.
  - [x] Boundary Segments include only used segments.
  - [x] Points table includes point label, easting, northing, and sequence.
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

## Dev Agent Record

Implementation summary:

- Extended Compute report generation to render structured General Info, Owner / Neighbor Found, Boundary Segments, and Points sections into JSON and formatted PDF artifacts.
- Added bold PDF headings, field labels, and table headers while preserving deterministic internal PDF generation.
- Added Compute report attachment upload on Finalize using `st_compute_report`, before Plan Check and before transaction completion.
- Added replacement behavior for existing `st_compute_report` sources so the latest Compute Finalize report is retained.
- Added sanitized local attachment evidence in `working/compute_report_attachment.json`.
- Added tests for report content, used-segment filtering, PDF bold text support, upload replacement, lifecycle success, and upload-failure short-circuit behavior.

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
