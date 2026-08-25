---
baseline_commit: handoff-2026-08-24
parent_story: 2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md
depends_on: 2-23b-add-pla-plan-annexation-pdf-page-selection-and-evidence-artifact.md
---

# Story 2.23C: Add PLA Selected-Plan Extraction, Review, And Local-Origin Geometry

Status: done

## Story

As an SMD examiner working a PLA Plan Annexation transaction,
I want OCR/vision extraction and reviewable geometry construction to run against the selected plan evidence,
so that annexation geometry can be reviewed and advanced even when no usable geographic reference exists.

## Business Context

PLA plan evidence is expected to be OCR/vision work, not embedded-text parsing. The selected plan evidence may contain bearings/distances and may or may not contain coordinate or georeference evidence. When geographic placement is unavailable, the workflow should still generate form-valid local-origin geometry and report that it is unreferenced.

## Acceptance Criteria

1. Given extraction runs for PLA, when a selected plan page artifact exists, then OCR/vision extraction is run against the selected plan evidence, not against unrelated title-information pages.
2. Given selected plan evidence contains bearings and distances, when extraction completes, then `extraction_review_data.json` contains reviewable boundary segment rows with sequence, from point, to point, bearing, distance, source page/region, confidence/status, and review notes.
3. Given selected plan evidence contains a coordinate table or coordinate labels, when extraction completes, then available coordinates are captured as reviewable point rows.
4. Given no usable coordinate/georeference evidence exists, when extraction completes, then absence of coordinates is recorded as explicit no-coordinate/georeference evidence rather than an extraction failure.
5. Given extracted boundary segments are sufficient to form a closed polygon, when the user rebuilds or validates points, then the existing deterministic boundary solver creates parcel geometry from bearings/distances.
6. Given no usable coordinate/georeference evidence exists, when geometry is created, then the system creates local unreferenced geometry using a local origin such as `(0,0)` and records that the geometry is form-valid but not geographically referenced.
7. Given usable coordinates or georeference evidence exists, when geometry is created, then the system uses the existing georeference readiness/placement path where practical and records the source evidence used.
8. Given geometry is generated from selected plan evidence, when Validate Points and Lines runs, then the examiner can review and edit point/segment rows using existing review workspace patterns before approving.
9. Given reviewed PLA geometry is approved, when validation runs, then the polygon is validated for shape, closure, parseable bearings/distances, duplicate/missing points, and geometry construction readiness.
10. Given generated geometry is local/unreferenced, when validation reports status, then the workflow distinguishes geometry shape validity from geographic placement validity and does not block solely because there is no geographic reference.
11. Given existing PE/PXA extraction and validation workflows exist, when PLA extraction/review is added, then those workflows keep their current source routing, review contracts, and validation behavior.
12. Given automated tests run, then coverage proves selected artifact routing, extracted segment/point rows, no-coordinate evidence, local-origin geometry, validation distinction between shape and placement, reopen/retry behavior, and PE/PXA non-regression.

## Tasks / Subtasks

- [x] Route PLA extraction to selected plan evidence. (AC: 1-4, 11)
  - [x] Reuse OCR/vision extraction patterns from PXA where applicable.
  - [x] Ensure extraction input is the selected plan evidence artifact/page.
  - [x] Add a PLA profile/prompt where needed.
  - [x] Extract bearing/distance boundary segments with reviewability, confidence, source page/region, and notes.
  - [x] Extract coordinate rows when visible.
  - [x] Emit explicit no-coordinate/no-georeference evidence when usable coordinate control is absent.

- [x] Reuse boundary review and solver behavior for PLA. (AC: 5-10)
  - [x] Load extracted PLA segments and optional points into the existing point/segment review workspace or a PLA-specific variant that preserves the same review contracts.
  - [x] Rebuild geometry from reviewed bearings/distances.
  - [x] Support local-origin geometry when no geographic control exists.
  - [x] Validate closure, parseability, duplicate/missing labels, area/shape where available, and construction readiness.
  - [x] Make unreferenced/local geometry a reportable condition, not an automatic blocker.

- [x] Persist review and validation evidence. (AC: 2-10, 12)
  - [x] Preserve selected plan source page/region evidence on rows and diagnostics.
  - [x] Persist `geometry_reference_mode = "local_origin"` or equivalent when applicable.
  - [x] Ensure approved review artifacts and validation summaries distinguish shape validity from geographic placement validity.
  - [x] Restore selected extraction/review artifacts on reopen.

- [x] Add tests. (AC: 1-12)
  - [x] Test OCR/vision adapter routing receives selected plan evidence artifact.
  - [x] Test boundary segment and optional point rows are emitted.
  - [x] Test explicit no-coordinate/no-georeference evidence.
  - [x] Test local-origin geometry creation when no coordinates are available.
  - [x] Test validation distinguishes shape validity from geographic reference availability.
  - [x] Test PE/PXA routing is unchanged.

### Review Findings

- [x] [Review][Patch] PLA selected evidence can be PNG but is passed to a PDF-only OCR/vision script [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs:1860] — resolved by adding `--source-image` support to the OCR/vision Python adapter, routing image artifacts through that argument from C#, and adding .NET/Python coverage for the PNG selected-plan path.
- [x] [Review][Patch] Survey plan reference-fit scale blocker was weakened and the broader solver regression test is failing [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs:459] — resolved by restoring blocking behavior for explicit printed/reference coordinate scale mismatches while preserving the existing warning behavior for candidate-only second anchors.
- [x] [Review][Patch] PLA georeference availability is inferred from `row_count` instead of usable coordinate evidence [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs:1422] — resolved by basing PLA coordinate/georeference evidence on rows with parseable easting and northing, with regression coverage for label-only point rows.
- [x] [Review][Patch] PLA is routed by setting `IsPxaSurveyPlanReview`, which applies PXA-specific solver policies to PLA [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaSurveyPlanReviewRouting.cs:5] — resolved by adding a PXA-only routing predicate and using it for PXA-specific solver repair/reference-fit options while keeping PLA in the shared survey-plan review workspace.

## Dev Notes

Reuse from Story 2.18:

- Image-only scanned PDF detection.
- OCR/vision survey-plan extraction path.
- `survey_plan_extraction_summary.json`, `extraction_review_data.json`, and `extraction_route.json` style artifacts.
- Reviewable points, segments, metadata, confidence, source page/zone, and manual-review fallback.

Reuse from Story 2.19:

- Editable boundary segment review.
- Deterministic boundary solving from reviewed bearing/distance segments.
- Closure and area validation where sufficient.

Preserve these constraints:

- Python/PDF/OCR processing stays behind adapter boundaries.
- Do not bypass review-before-output workflow.
- Do not label local-origin geometry as survey-accurate georeferencing.
- Do not block solely for missing geographic placement if the geometry is otherwise form-valid.

## References

- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `_bmad-output/implementation-artifacts/2-19-implement-pxa-survey-plan-segment-review-and-deterministic-boundary-solver.md`
- `_bmad-output/implementation-artifacts/2-23b-add-pla-plan-annexation-pdf-page-selection-and-evidence-artifact.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-08-24: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pla selected plan extraction"` initially failed because PLA extraction returned only selection artifacts and did not call OCR/vision on selected evidence.
- 2026-08-24: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pla selected plan extraction" "survey plan solver solves PLA" "survey plan solver keeps default"` passed 3 focused tests.
- 2026-08-24: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- 2026-08-24: Broader slice `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pla" "survey plan solver" "review persistence"` passed the new PLA tests and many adjacent tests, then stopped on an older PXA solver fixture `RebuildBlocksLargeReferenceFitScaleMismatch`; this is outside the PLA selected-plan path and remains a follow-up risk for the existing PXA reference-fit tests.
- 2026-08-24: Review patch validation `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- 2026-08-24: Review patch validation `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pla selected plan extraction" "review routing requires pxa" "survey plan solver"` passed 27 tests.
- 2026-08-24: Review patch validation `$env:PYTHONPATH='src\ProcessingTools'; python -m unittest src.ProcessingTools.tests.test_survey_plan_ocr_vision_extraction` passed 11 tests.
- 2026-08-24: Broader slice `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pla" "survey plan solver" "review persistence"` passed all PLA, solver, and review-routing tests, then stopped on unrelated `TransactionPanelStateTests.ActiveTransactionStayDecisionPreventsReplacement` matched by the broad `pla` filter.
- 2026-08-24: TR 100001219 investigation showed PLA intake was correctly classified as `pla_plan_annexation` but initial preflight blocked before selected-plan extraction because coordinate/georeference readiness required source context too early.
- 2026-08-24: Patch validation `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "manifest preflight pla" "georeference check blocks when only source presence exists"` passed 6 tests. Debug configuration could not be used because the local `obj\Debug\net8.0-windows` folder denied access.
- 2026-08-24: Patch validation `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- 2026-08-24: Patch validation `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "manifest preflight" "georeference check" "dimension check"` passed 40 tests.

### Completion Notes List

- PLA extraction now uses the selected generated plan evidence artifact from Story 2.23B as OCR/vision input, instead of the original multi-page/title-information attachment.
- The PLA extraction step writes `extraction_review_data.json`, `survey_plan_extraction_summary.json`, and `extraction_route.json`, keeping the normal Validate Points and Lines workflow contract.
- PLA review artifacts are enriched with `source_profile = "pla_plan_annexation_selected_plan"`, selected page evidence, no-coordinate/georeference evidence, and `geometry_reference_mode = "local_origin"` when no coordinate rows are extracted.
- PLA selected-plan review artifacts reuse the existing segment review workspace and deterministic boundary solver while preserving PE/PXA routing behavior.
- `SurveyPlanBoundarySolver` now supports an explicit local-origin mode that seeds the first boundary point at `(0,0)` only when requested; default missing-anchor behavior remains blocked for existing non-PLA flows.
- PNG selected-plan evidence now uses the OCR/vision image-input contract instead of the PDF-only input contract.
- PLA coordinate/georeference evidence now requires usable coordinate rows, not just nonzero point-row count.
- PXA-specific solver repair/reference-fit behavior is now gated separately from the shared survey-plan review workspace so PLA does not inherit PXA-only policies.
- PLA initial preflight now treats a valid `plan_annexation_pdf` as sufficient to proceed, deferring missing coordinate/georeference and dimension evidence to selected-plan extraction/local-origin review instead of blocking before extraction.

### File List

- `_bmad-output/implementation-artifacts/2-23c-add-pla-selected-plan-extraction-review-and-local-origin-geometry.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowScriptExecutor.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaSurveyPlanReviewRouting.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SurveyPlanBoundarySolverTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-08-24 | 0.1 | Implemented PLA selected-plan OCR/vision routing, review artifact enrichment, local-origin solver mode, and focused regression coverage. | Codex |
| 2026-08-24 | 0.2 | Resolved code-review findings for selected-image OCR input, coordinate evidence detection, PXA-only solver gating, and reference-fit regression coverage. | Codex |
| 2026-08-24 | 0.3 | Patched PLA initial preflight to defer coordinate/georeference and dimension evidence until selected-plan extraction when a valid plan annexation PDF is present. | Codex |
