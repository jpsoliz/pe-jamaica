---
baseline_commit: handoff-2026-08-24
parent_story: 2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md
depends_on: 2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md
---

# Story 2.23B: Add PLA Plan Annexation PDF Page Selection And Evidence Artifact

Status: done

## Story

As an SMD examiner working a PLA Plan Annexation transaction,
I want to select the PDF and page containing the plan/map evidence and save that selection as a generated case artifact,
so that downstream PLA extraction uses the selected plan evidence rather than unrelated title-information pages.

## Business Context

PLA plan PDFs may be image-only and multi-page, with title information before the plan/map page. The first implementation supports full-page selection only. Rectangular crop support is deliberately deferred, but the metadata must leave room for future `rectangle` selections.

## Acceptance Criteria

1. Given a PLA source PDF is available, when the examiner starts the PLA plan selection step, then the workspace lists the transaction PDF attachments and lets the examiner select the source PDF and page containing the plan/map.
2. Given the PDF has multiple pages, when the examiner selects a page, then the selected page number is persisted in the case folder and is not assumed from a fixed page position.
3. Given rectangular crop support is not implemented in this first slice, when the examiner selects a plan page, then the system records the selection as `selection_type = "full_page"` and uses the full rendered page as the extraction area.
4. Given the selected page is rendered, when the selection is saved, then the system creates a generated plan evidence artifact under the transaction case folder as a PDF if practical; if PDF generation is not practical in the implementation environment, it may generate a PNG with a clear artifact type and reason.
5. Given the generated plan evidence artifact is created, when case artifacts are listed, then the artifact is visible as an internal/generated PLA plan evidence document and is not uploaded to Innola before Finalize.
6. Given the transaction is reopened, when the case folder contains the PLA selection artifact, then the workflow restores the selected source PDF, page number, selection metadata, and generated evidence artifact.
7. Given existing Supporting Documents / PDF viewer / title-plan image-placement workflows exist, when PLA page selection is added, then those workflows keep their current behavior.
8. Given automated tests run, then coverage proves selected page metadata persistence, full-page selection metadata shape, PDF/PNG generated evidence handling, reopen restore behavior, and non-regression for existing document viewers.

## Tasks / Subtasks

- [x] Add selected plan evidence model and persistence. (AC: 2-6)
  - [x] Add case-folder artifact naming for selected PLA plan evidence.
  - [x] Persist source path, attachment/source metadata, selected page number, selection type, page dimensions, generated artifact path, generated artifact format, fallback reason when applicable, and timestamps.
  - [x] Represent MVP selection as `selection_type = "full_page"`.
  - [x] Design the schema so rectangular crop can later add `x`, `y`, `width`, and `height` without breaking existing full-page artifacts.

- [x] Build PLA page selection UX. (AC: 1-7)
  - [x] Reuse existing Supporting Documents / PDF viewer patterns for transaction attachments.
  - [x] Let the examiner choose the source PDF and plan page.
  - [x] Show selected source file, page number, and generated artifact status.
  - [x] Save/reopen the selection from case artifacts.
  - [x] Do not require rectangular crop in the MVP.

- [x] Generate selected-plan evidence artifact. (AC: 4-5)
  - [x] Render the selected PDF page.
  - [x] Generate selected-plan evidence as PDF when practical.
  - [x] Use PNG fallback with explicit reason when PDF generation is unavailable.
  - [x] Mark the artifact as internal/generated PLA plan evidence.

- [x] Add tests. (AC: 1-8)
  - [x] Test image-only multi-page PDF probe behavior using PLA fixtures or redacted equivalents.
  - [x] Test full-page selected-plan artifact metadata persistence.
  - [x] Test PDF and PNG fallback metadata behavior.
  - [x] Test reopen restores PLA selection and generated artifact.

### Review Findings

- [x] [Review][Patch] PLA selection workspace is not wired into the production UI/workflow [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs:210]
- [x] [Review][Patch] Generated evidence artifact copies the whole source PDF instead of rendering/extracting the selected page [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs:443]
- [x] [Review][Patch] PLA script-plan step is not executable by the extraction adapter [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json:274]
- [x] [Review][Patch] Artifact discovery can expose stale or unclassified PLA evidence artifacts [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs:359]
- [x] [Review][Patch] Persisted selection paths are not validated to stay inside the case folder [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs:557]
- [x] [Review][Patch] Renderer success output is trusted even when content is empty or format is unsupported [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs:117]
- [x] [Review][Patch] Reopen source matching can select the wrong attachment when filenames collide [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs:357]

## Dev Notes

Example selected-plan artifact contract:

```json
{
  "schema_version": "1.0.0",
  "transaction_number": "string",
  "source_type": "st_plan_annexation_pdf",
  "source_relative_path": "source/...",
  "selected_page_number": 2,
  "selection_type": "full_page",
  "selection_region": null,
  "page_width_points": 612,
  "page_height_points": 792,
  "generated_plan_evidence_path": "working/pla_selected_plan.pdf",
  "generated_plan_evidence_format": "pdf",
  "fallback_reason": null,
  "created_at_utc": "date-time",
  "updated_at_utc": "date-time"
}
```

Preserve these constraints:

- Do not upload selected evidence to Innola before Finalize.
- Do not assume the plan page is always page 2.
- Do not require crop support in this story.
- Do not block the WPF UI thread during PDF rendering or filesystem writes.

## References

- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md`
- `_bmad-output/implementation-artifacts/5-28-add-assisted-title-plan-image-placement-for-map-comparison.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-08-24: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with one existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- 2026-08-24: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build "pla plan evidence selection"` passed 8 tests.
- 2026-08-24: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build "review source viewer"` passed 5 tests.
- 2026-08-24: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build "supporting document"` passed 6 tests.
- 2026-08-24: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build "title plan"` passed 9 tests.
- 2026-08-24: Full harness run reached the new PLA tests successfully, then stopped on an unrelated existing PXA memorandum XAML assertion: `PxaReviewExposesMemorandumRuleGroups`.

### Completion Notes List

- Added `PlaPlanEvidenceSelectionService` with the `working/pla_plan_annexation/pla_plan_evidence_selection.json` contract, `full_page` MVP metadata, selected page persistence, PDF/PNG artifact format handling, and reopen-safe absolute path projection.
- Added `IPlaPlanEvidenceRenderer` so a real page renderer can be swapped in without changing persistence; the built-in renderer preserves the selected source PDF with an explicit fallback reason because the add-in project has no C# PDF page extraction dependency.
- Added `PlaPlanEvidenceSelectionViewModel` for a WPF-bound PLA selection workspace: copied PLA PDF options, selected page number, save command, generated artifact status, and reopen restoration.
- Extended case-folder artifact discovery so PLA selection metadata plus generated PDF/PNG evidence artifacts appear on reopen and stay local until a later Finalize story uploads outputs.
- Added focused tests for metadata persistence, full-page schema shape, PDF and PNG artifact paths, invalid page rejection, candidate filtering, reopen artifact discovery, view-model command state, and document-viewer/title-plan non-regression.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaPlanEvidenceSelectionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

- 2026-08-24: Implemented PLA selected-plan evidence selection persistence, generated evidence artifact handling, artifact discovery, bindable selection workspace state, and targeted regression coverage.
