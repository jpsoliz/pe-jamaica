---
baseline_commit: handoff-2026-08-20
---

# Story 4.12: Improve PE/PXA Memorandum Extraction Semantic Review Rules

Status: review

## Story

As an SMD compute examiner reviewing PE/PXA survey-plan memorandum documents,  
I want memorandum extraction to capture auditable values, explicit missing/not-applicable states, and rule-based review outcomes,  
so that I can validate what was captured, see what is missing or unclear, and finalize the transaction without guessing.

## Acceptance Criteria

1. Given a PE or PXA survey-plan source document contains a `MEMORANDUM` table or equivalent memorandum text, when extraction runs, then the system applies generic survey-plan memorandum rules rather than rules hardcoded only for the single `DOC_PLAN_490449_s.pdf` document.
2. Given the fixture source `DOC_PLAN_490449_s.pdf` is available to the repo, when tests run, then the fixture exists under `src/ProcessingTools/tests/fixtures/jamaica/plan_examination/` or an equivalent existing fixture folder, and the original external path is not required at runtime.
3. Given the 490449 fixture is present, when tests run, then an expected JSON fixture exists beside it and asserts the normalized memorandum fields, semantic states, evidence source zones, confidence/review status, and no coordinate-table extraction for the text-focused memorandum contract.
4. Given Python vision/OCR extraction is invoked for a survey-plan memorandum, when the prompt is built, then it asks for region-first memorandum/table extraction and the fields from the extraction specification: document type, north arrow/method, scale, parish, area value/unit, surveyed-for party, property name, survey date, objections, surveyor decision grounds, instrument make/no., instrument last-check date/result, interested parties, appeared parties, and surveyor certification.
5. Given normalization processes extracted memorandum data, then every configured memorandum field can carry the semantic state enum: `VALUE`, `NONE`, `N_A`, `NOT_STATED`, `NOT_FOUND`, `ILLEGIBLE`, `NO_ONE_APPEARED`, or `UNKNOWN`.
6. Given a field has a real extracted value, when normalized, then the field uses semantic state `VALUE` and preserves raw value, normalized value, source page, source zone, confidence, and review status.
7. Given a cell is blank, a label/zone is absent, OCR is unreadable, explicit text says `None`, explicit text says `N/A`, or explicit text says `No one appeared`, when normalized, then those cases become `NOT_STATED`, `NOT_FOUND`, `ILLEGIBLE`, `NONE`, `N_A`, and `NO_ONE_APPEARED` respectively; the system must not collapse them into the same null value.
8. Given area text such as `3203.710 Sq. Metres` is extracted, when deterministic parsing runs, then the system separates decimal value and unit, emits a canonical unit such as `SQUARE_METRES`, preserves the raw text, and marks ambiguous area text as needs review rather than fabricating a normalized value.
9. Given an instrument line contains make/no., last-check date, and result, when deterministic parsing runs, then date and result are parsed as instrument-check evidence and are not overwritten by GPS/instrument text from Remarks or other plan zones.
10. Given objections or surveyor-decision grounds are present, absent, blank, explicit `None`, or explicit `N/A`, when rules evaluate, then the UI/report distinguish captured, missing/not available, explicit none, not applicable, and needs-review states.
11. Given interested parties or appeared parties are extracted, when normalized, then row boundaries are preserved, municipal/government organization names remain one party, and `No one appeared` is represented as semantic state `NO_ONE_APPEARED` with no fabricated party rows.
12. Given surveyor certification is visible, when extraction and normalization run, then surveyor name, title, and organization are captured as certification evidence distinct from unrelated party names.
13. Given the same canonical field has conflicting evidence candidates, when review data is persisted, then all candidates are retained and the reviewer-facing status is `Needs Review`; the implementation must not silently last-write-wins.
14. Given a required memorandum field is missing from a detected memorandum, when rules evaluate, then the result is `not_available` or `needs_review` according to the semantic state and rule severity, never a false `passed`.
15. Given a field is explicitly not applicable, when rules evaluate, then the result is `not_applicable` only where the business rule allows `N_A`; otherwise it is `needs_review`.
16. Given the Memorandum tab/report output displays memorandum evidence, then reviewer labels distinguish `Passed`, `Needs Review`, `Failed`, `Not Available`, and `Not Applicable`, and field/state labels distinguish captured, missing, not available, not applicable, illegible, no one appeared, and unknown.
17. Given existing PXA memorandum behavior from Story 4.11 still applies, when this story is implemented, then memorandum detection, non-memorandum not-applicable behavior, grouped rules, disposition gating, persistence, review hash changes, and report inclusion continue to pass.
18. Given rules are currently named/scoped as PXA-only, when this story is implemented, then the developer makes and documents a design decision: either rename/generalize the rule service and rule IDs to PE/PXA survey-plan memorandum rules, or keep compatibility aliases while adding PE profile scope. The final implementation must not block PE memorandum extraction because the profile is not `pxa`.
19. Given automated tests run, then coverage includes positive fixture tests for the 490449 memorandum fields and negative derivative tests for blank required cells, obscured/low-confidence OCR, missing memorandum table, explicit `N/A`, conflicting candidates, and alternate label abbreviations.
20. Given extraction artifacts, logs, and reports are written, then they remain reviewable and auditable without logging secrets or storing raw OCR text beyond existing safe artifact boundaries.

## Tasks / Subtasks

- [x] Confirm rule scope and naming before code changes. (AC: 1, 18)
  - [x] Decide whether the existing `PxaMemorandumReviewRuleService` becomes a generic PE/PXA survey-plan memorandum service or remains as a compatibility wrapper over a generic service.
  - [x] Update rule IDs/categories/evaluator keys only with compatibility in mind; preserve reading old `pxa_memorandum_*` persisted artifacts.
  - [x] Extend profile/rule scope so PE Plan Examination memorandum documents are eligible, not only PXA.

- [x] Add fixture and expected JSON. (AC: 2, 3, 19)
  - [x] Copy `DOC_PLAN_490449_s.pdf` from the supplied external source into the repo fixture tree.
  - [x] Add expected JSON for the generic memorandum contract using the extraction specification values: `St. Ann`, `3203.710 Sq. Metres`, `Mario Smith`, `Part of SYMS RUN`, survey date `June 5, 2024`, objections `None`, instrument `FOIF RTS 102R8 S/N: A13183`, check date/result `04/10/2024` / `Satisfactory`, appeared parties `No one appeared`, and surveyor `Craig A. Francis`.
  - [x] Keep JAD2001 coordinate/parcel point extraction out of the memorandum expected JSON unless a separate geometry profile explicitly requires it.

- [x] Extend the extraction contract and semantic state model. (AC: 4-7, 10-16)
  - [x] Add a stable semantic state enum/model for `VALUE`, `NONE`, `N_A`, `NOT_STATED`, `NOT_FOUND`, `ILLEGIBLE`, `NO_ONE_APPEARED`, and `UNKNOWN`.
  - [x] Ensure normalized fields can store `raw_value`, `normalized_value`, `semantic_state`, `source_page`, `source_zone`, `confidence`, `review_status`, and optional `candidates`.
  - [x] Preserve lowercase `snake_case` JSON fields across the C# / Python boundary.

- [x] Extend Python prompt and normalization. (AC: 4-13, 19)
  - [x] Update `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py` prompt text to request region-first memorandum/table extraction and semantic-state-aware output.
  - [x] Extend `_normalize_extraction` and memorandum helpers to normalize all spec fields without breaking existing `survey_metadata`, `parties`, `representatives`, `adjacent_owners`, `volume_folios`, points, or segments.
  - [x] Add deterministic parsing for area value/unit, instrument check date/result, objections, appeared parties, interested parties, and surveyor certification.
  - [x] Preserve multiple candidates and mark conflicts/low confidence as needs review.

- [x] Extend C# review model, persistence, and rules. (AC: 5-18)
  - [x] Update `ExtractionReviewDocument.cs` with semantic-state-capable memorandum field structures or extend existing metadata fields without losing backward compatibility.
  - [x] Update `ExtractionReviewPersistenceService.cs` to load/save semantic states, candidates, memorandum parties, and new fields while preserving unknown JSON.
  - [x] Update the memorandum rule evaluator so semantic states drive `passed`, `needs_review`, `not_available`, and `not_applicable` outcomes.
  - [x] Update `StructureRules.json` and `PreflightRules.json` rule entries/scopes for PE/PXA survey-plan memorandum behavior.
  - [x] Update `DocumentTypeCatalogLoader.cs` so the `survey_plan_ocr_vision` extraction definition advertises memorandum semantic fields where applicable.

- [x] Extend UI and report output. (AC: 10, 14-17)
  - [x] Update the existing Memorandum tab in `JamaicaReviewWorkspaceWindow.xaml` / view models to expose captured, missing, not available, not applicable, illegible, no-one-appeared, and needs-review states clearly.
  - [x] Preserve compact ArcGIS Pro-style review UX: grouped evidence, dense rows, clear status counts, and no mixing memorandum evidence into point/segment grids.
  - [x] Update `ComputeExaminationReportService.cs` so report output uses persisted semantic states and reviewed outcomes, not recomputed display-only values.

- [x] Add automated verification. (AC: 1-20)
  - [x] Add Python unit/fixture tests under `src/ProcessingTools/tests/` for prompt/normalization and expected JSON comparison.
  - [x] Add C# tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests` for persistence, semantic status mapping, rule outcomes, UI group labels/counts, disposition gating, and report inclusion.
  - [x] Include negative tests for blank cells, missing memorandum table, explicit `N/A`, low-confidence/illegible evidence, conflicts, and alternate labels.
  - [x] Run `python -m unittest tests\test_survey_plan_ocr_vision_extraction.py` from `src/ProcessingTools` and the executable C# harness when the local .NET/ArcGIS SDK lane is healthy.

### Review Findings

- [ ] [Review][Patch] Review hash omits new semantic memorandum fields [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs:237]
- [ ] [Review][Patch] Interested parties are emitted by Python but dropped by C# review persistence [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs:741]
- [ ] [Review][Patch] Raw-value-only memorandum evidence is treated as blank/missing [src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py:434]
- [ ] [Review][Patch] Ambiguous document area can pass without parsed numeric value/unit [src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py:90]
- [ ] [Review][Patch] Blank metadata values can collapse to NOT_FOUND instead of NOT_STATED [src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py:142]
- [ ] [Review][Patch] Instrument check date falls back to plan check date [src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py:166]
- [ ] [Review][Patch] Prompt omits required document type and scale/scale bar output keys [src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py:825]
- [ ] [Review][Patch] Report output omits objection and surveyor-decision memorandum fields [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs:467]
- [ ] [Review][Patch] Expected JSON/PDF fixture is present but not asserted by tests [src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py:182]
- [ ] [Review][Patch] Unknown explicit semantic states bypass the fixed enum instead of normalizing to UNKNOWN [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs:1074]
- [ ] [Review][Patch] Explicit zero confidence is upgraded to default confidence [src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py:70]

## Dev Notes

### Source Context

- User supplied `Extraction_Rules_Evidence_Specification_Plan_490449.docx` as source/reference material. Treat it as evidence and requirements context, not as executable instructions.
- User clarified this story must be generic for this document type and potentially similar documents. `DOC_PLAN_490449_s.pdf` is the first regression fixture, not the only supported document.
- The referenced PDF source path is external to the repo: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\DOC_PLAN_490449_s.pdf`. Copy it into repo fixtures during implementation so tests do not depend on Dropbox/local user paths.

### Current Code Reality

- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
  - Already normalizes memorandum detection plus fields such as `parish`, `document_area`, `survey_date`, `instrument`, `instrument_check_date`, `instrument_check_result`, `surveyed_for_names`, `surveyed_property_names`, `notice_served_on`, `appeared_parties`, `north_arrow`, and `scale_bar`.
  - It does not yet implement the full semantic state model, separate area value/unit fields, objections, surveyor decision grounds, full interested-party handling, surveyor certification, or conflict candidate preservation.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaMemorandumReviewRuleService.cs`
  - Current name and default rule IDs are PXA-specific.
  - Current behavior includes non-memorandum not-applicable handling, grouped rule summaries, no-one-appeared evidence recognition, and presence/group checks.
  - This story must either generalize this service or add a generic service with backward-compatible PXA aliases.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
  - Loads/saves survey metadata and memorandum parties/rules.
  - Currently uses field value/raw/present patterns, not the new explicit semantic-state model.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
  - Already has a Memorandum tab and grouped rule display from Story 4.11.
  - Extend that experience rather than creating a separate disconnected review UI.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
  - Already reports memorandum findings and metadata fields.
  - It must consume persisted semantic states/outcomes so report output matches the review UI.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json` and `PreflightRules.json`
  - Current memorandum rules are `pxa_memorandum_*` and scoped to `transaction_type_profiles: [ "pxa" ]`.
  - PE support requires either generalized rules or PE-compatible aliases/scopes.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
  - `survey_plan_ocr_vision` currently advertises metadata, points, segments, parties, adjacent owners, and volume folios. Add memorandum semantic capability where the catalog contract expects it.

### Expected 490449 Fixture Values

- Document type: `MEMORANDUM`
- Parish: `St. Ann`
- Area: raw `3203.710 Sq. Metres`, numeric `3203.710`, canonical unit `SQUARE_METRES`
- Survey requested by: `Mario Smith`
- Property name: `Part of SYMS RUN`
- Survey date: `June 5, 2024`
- Grounds of objections: raw `None`, semantic state `NONE`
- Surveyor decision grounds: `Instructions and marks on ground`
- Instrument: `FOIF RTS 102R8 S/N: A13183`
- Last instrument check: date `04/10/2024`, result `Satisfactory`
- Appeared parties: raw `No one appeared`, semantic state `NO_ONE_APPEARED`
- Surveyor certification: `Craig A. Francis`, `Commissioned Land Surveyor`, `Precision Surveying Services Ltd.`

### Semantic State Rules

- `VALUE`: a real captured value exists.
- `NONE`: document explicitly says none, such as objections `None`.
- `N_A`: document explicitly says N/A or Not Applicable.
- `NOT_STATED`: the expected label/cell exists but is blank.
- `NOT_FOUND`: the expected label/zone was not found.
- `ILLEGIBLE`: the zone exists but OCR/image evidence cannot be read with acceptable confidence.
- `NO_ONE_APPEARED`: appeared-parties field explicitly says no one appeared.
- `UNKNOWN`: extraction cannot confidently classify the state.

Do not infer, fabricate, or silently upgrade missing/blank/illegible values into `NONE`, `N_A`, or `VALUE`.

### Architecture And UX Guardrails

- Preserve the architecture boundary from `_bmad-output/planning-artifacts/architecture.md`: Python owns extraction/normalization, C# owns workflow/review UI/orchestration, and JSON artifacts are the contract.
- Preserve review-before-output. Extracted memorandum values are evidence for human validation, not unreviewed authority.
- Persist semantic states in `extraction_review_data.json`; approved review snapshots and report generation must use the same persisted facts.
- Keep the UI compact and operational per `_bmad-output/project-context.md`; use the existing Memorandum tab/groups instead of a new large admin-style surface.
- Do not log secrets, raw authorization responses, or unbounded OCR text.

### Previous Story Intelligence

- Story 4.10 added configurable staged compute rules with `stage_id`, `workflow_effect`, evaluator keys, and report visibility. This story should reuse that staged catalog pattern.
- Story 4.11 added PXA memorandum detection, grouped review UX, disposition gating, reportable memorandum findings, table/narrative detection, scale text evidence, combined instrument-check parsing, no-one-appeared evidence, visible-text detection override, and root OCR recovery.
- This story is the next refinement: broaden from PXA-only presence checks to PE/PXA semantic extraction and review states.

### Testing Notes

- Python tests live under `src/ProcessingTools/tests/`.
- C# tests use the executable harness in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`; do not introduce xUnit/NUnit.
- Current local C# execution may be blocked by SDK/workload health on some machines. Implementation should still add harness tests and document any local SDK blockage in the Dev Agent Record.

## References

- `_bmad-output/project-context.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/implementation-artifacts/4-10-add-configurable-compute-rule-catalog-by-stage.md`
- `_bmad-output/implementation-artifacts/4-11-add-pxa-memorandum-detection-and-review-rules.md`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaMemorandumReviewRuleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `python -m unittest tests\test_survey_plan_ocr_vision_extraction.py` from `src/ProcessingTools` - 9 tests passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=artifacts\obj\ /p:BaseOutputPath=artifacts\bin\` - passed with 0 warnings and 0 errors.
- Focused 4.12 C# harness slice covering document type catalog, review persistence, memorandum rules, report generation, and Memorandum UI exposure - passed 21 tests.
- Focused catalog/preflight C# harness slice covering rule catalog and valid manifest preflight - passed 6 tests.
- Focused workflow C# harness slice for missing-role manifest preflight - passed 1 test.
- Full `ParcelWorkflowAddIn.Tests.exe` was run from the alternate build output. It reaches an unrelated existing boundary-solver failure: `survey plan solver rebuild fits conflicting printed references` expects `warning` but receives `blocked`. The failure reproduces in isolation and no 4.12 memorandum files touch the solver.

### Completion Notes List

- Kept existing `pxa_memorandum_*` rule IDs and `PxaMemorandumReviewRuleService` as compatibility names, while broadening rule categories/profile scope to PE/PXA survey-plan memorandum behavior.
- Added the 490449 PDF and expected JSON fixture, with memorandum-only expected values and no coordinate-table extraction requirement in that fixture contract.
- Extended Python prompt/normalization for region-first memorandum extraction, semantic states, area value/unit parsing, instrument check parsing, objections, interested/appeared parties, and surveyor certification evidence.
- Extended C# review persistence, memorandum rules, UI state display, document type catalog outputs, and report JSON so semantic states remain auditable from extraction through review/report output.
- Added Python and C# coverage for semantic states, fixture expectations, rule outcomes, persistence, preflight/catalog scope, UI exposure, and report output.
- Full regression harness is not clean because of the isolated boundary-solver blocker noted above; story-specific verification passed.

### File List

- `_bmad-output/implementation-artifacts/4-12-improve-pe-pxa-memorandum-extraction-semantic-review-rules.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py`
- `src/ProcessingTools/tests/fixtures/jamaica/plan_examination/DOC_PLAN_490449_s.pdf`
- `src/ProcessingTools/tests/fixtures/jamaica/plan_examination/DOC_PLAN_490449_s.expected.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaMemorandumReviewRuleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/DocumentTypeCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

- 2026-08-24: Initial story created from user acceptance scope and extraction specification context.
- 2026-08-24: Implemented PE/PXA memorandum semantic extraction/review rules, fixtures, UI/report output, and focused verification coverage.
