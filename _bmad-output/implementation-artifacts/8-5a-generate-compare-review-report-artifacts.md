---
baseline_commit: handoff-2026-07-14
---

# Story 8.5A: Generate Compare Review Report Artifacts

Status: in progress

## Story

As a cadastral examiner,  
I want Compare to generate a review report artifact,  
so that the final ownership/evidence reconciliation decision can be audited, resumed, and shared like the Compute examination report.

## Business Context

Compute already produces durable report artifacts under `output/reports`. Compare currently persists structured decision evidence in `working/compare_review_draft.json` and `working/compare_review_decision.json`, but there is no examiner-facing Compare report package. Compare needs a report artifact that summarizes the transaction, reviewer, source documents, retained valuable evidence, Enterprise Legal/Fiscal spatial context, discrepancies, decision notes, and final decision.

This story does not change Commit promotion. It adds report generation for the Compare stage and references that report from the existing Compare decision evidence refs.

## Acceptance Criteria

1. Given Compare has a Case Folder, when the user saves draft, blocks, or finalizes, then the add-in can generate `output/reports/compare_review_report.json`.
2. Given PDF report generation is enabled, when the report is generated, then the add-in also writes `output/reports/compare_review_report.pdf` with the header, a numbered `Valuable Evidence` section, and `Notes`.
3. Given the report is generated, then it includes transaction id/number, task id, reviewer id/display name, generated timestamp, decision status, readiness status, and Decision Notes.
4. Given source documents were loaded, then the report includes the selected PDF/source document names and source document count.
5. Given Innola search results were retained as valuable evidence, then the report includes each retained item with role tag, display summary, query key, source type, and captured timestamp.
6. Given Enterprise Legal/Fiscal evidence rows exist, then the report includes included/excluded rows, source kind, relationship, display name, summary, and source label.
7. Given discrepancies exist, then the report includes discrepancy title, source, status, resolved flag, and blocking flag.
8. Given a Compare decision is finalized, then `compare_review_decision.json` includes an evidence ref to the generated report JSON and PDF when available.
9. Given Finalize is pressed and the Compare PDF report exists, then the add-in attaches `compare_review_report.pdf` to the Innola transaction using document/source type `st_compare_report` before completing the task.
10. Given Finalize is pressed and no Compare PDF report exists yet, then the add-in generates the report first and asks the user to confirm task conclusion before completion.
11. Given report generation or attachment fails, then the Compare decision/draft save still completes where possible, a sanitized warning is shown, and no raw tokens or credentials are written to UI or artifacts.
12. Given automated tests run, then report JSON content, PDF creation, decision evidence refs, Finalize attachment behavior, and failure handling are covered.

## Tasks / Subtasks

- [x] Add Compare report document model. (AC: 1, 3-7)
  - [x] Define a schema versioned `CompareReviewReportDocument`.
  - [x] Include transaction, reviewer, decision, notes, valuable evidence, Enterprise evidence, discrepancies, and artifact refs.
  - [x] Keep values redacted consistently with Compare decision persistence.

- [x] Add Compare report generation service. (AC: 1-2, 10)
  - [x] Add `CompareReviewReportService`.
  - [x] Write JSON to `layout.ReportsDirectory`.
  - [x] Reuse the existing simple PDF report writer pattern from Compute where practical.
  - [x] Keep the PDF examiner-facing: header, numbered Valuable Evidence, and Notes.
  - [x] Return success/failure with sanitized messages.

- [x] Wire report generation into Compare saves. (AC: 1, 8-11)
  - [x] Generate JSON report on Save, Suspend pre-save, Block, and Finalize pre-save.
  - [x] Add report refs to `compare_review_decision.json` evidence refs when available.
  - [x] Preserve existing draft/decision saves when report generation fails.
  - [x] Generate a report before Finalize confirmation when no PDF report exists yet.

- [x] Attach Compare PDF report on Finalize. (AC: 9-11)
  - [x] Add a Compare report attachment service using Innola attachment upload.
  - [x] Use source type `st_compare_report`.
  - [x] Verify the PDF exists before task completion.

- [~] Add tests. (AC: 1-12)
  - [x] Report JSON includes core transaction fields and Decision Notes.
  - [x] Report PDF is created on Save and includes numbered Valuable Evidence plus Notes.
  - [ ] Report includes valuable evidence and Enterprise evidence rows.
  - [ ] Report includes discrepancies and Decision Notes.
  - [ ] Finalized decision references report artifacts.
  - [x] Finalize attaches the generated PDF before completing task.
  - [ ] Report failure is non-fatal and sanitized.

## Developer Notes

Relevant existing analogs:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDecision.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`

Recommended artifact names:

- `output/reports/compare_review_report.json`
- `output/reports/compare_review_report.pdf`

Innola Finalize attachment:

- File: `output/reports/compare_review_report.pdf`
- Document/source type: `st_compare_report`
- The PDF is regenerated and overwritten before attachment.

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- compare
```

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-17 | 0.1 | Created story for Compare review JSON/PDF report artifacts. | Codex |
| 2026-07-17 | 0.2 | Implemented initial JSON report generation on Compare save and completion status message. | Codex |
| 2026-07-17 | 0.3 | Added PDF report generation on Save and Finalize attachment as `st_compare_report`. | Codex |
| 2026-07-17 | 0.4 | Refined PDF report body to numbered Valuable Evidence and Notes, and clarified Save confirmation message. | Codex |
| 2026-07-17 | 0.5 | Added Finalize pre-check to generate missing report before user confirmation. | Codex |
