---
baseline_commit: d2cb1378c6938f073be54fa4169af908241c32aa
---

# Story 11.1: Inventory And Decision Lock For Plan Examination Unification

Status: review

## Story

As a Plan Examination product and technical team member,
I want the PE/PXA pipeline inventory reviewed, decisioned, and locked,
so that the refactor starts from verified reuse/adapt/retire decisions instead of rewriting working extraction, validation, output, and writeback behavior.

## Business Context

The target user-facing workflow is `PE / Plan Examination`.

PXA should eventually disappear from the user UI, but current PXA behavior must not be lost while the unified workflow is being built. The unified Plan Examination workflow must support both single-parcel and multi-parcel documents by recognizing the type of information present in the source files instead of splitting behavior by PE versus PXA labels.

Core source documents for the unified workflow are PDFs:

- Mandatory `st_surveyplan`.
- Mandatory `st_surveysheet`.
- Optional `st_autocad_file`.
- Optional `st_survey_points`.

This first story is a decision-lock story, not a broad code refactor. It exists to make sure Mary, Winston, Sally, and Amelia agree which existing scripts, services, procedures, and UX surfaces are valid to reuse, which must be adapted, and which must be retired or recreated before implementation proceeds.

## Acceptance Criteria

1. Given the current Plan Examination planning documents exist, when this story is complete, then `plan-examination-pipeline-inventory.md` contains a final decision for every listed script, service, and procedure using one of these statuses: `Reuse as-is`, `Reuse with small changes`, `Adapt into unified Plan Examination`, `Retire after compatibility period`, or `Recreate`.
2. Given `src/ProcessingTools/adapters/extraction_adapter.py` is currently a placeholder, when decisions are locked, then it has an explicit retire-or-recreate decision and is not treated as the active production extraction route.
3. Given `src/ProcessingTools/adapters/pdf_text_structured_extraction.py` handles deterministic embedded-text PDF extraction, when decisions are locked, then its ownership is clarified for `st_surveysheet` and any embedded-text `st_surveyplan` PDF route.
4. Given `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py` currently contains PXA-oriented scanned/vision extraction behavior, when decisions are locked, then the team has decided whether to keep the filename as a compatibility wrapper or introduce a generalized Plan Examination route name, and the decision explicitly preserves current PXA behavior while enabling multi-parcel extraction.
5. Given `CreateParcelDraftExtractionAdapter.cs` currently owns too much route orchestration, when decisions are locked, then the inventory states how route planning and route execution should be separated in later stories without rewriting extraction code in this story.
6. Given validation, output generation, Enterprise `working_review` publish, and Innola finalize already exist, when decisions are locked, then the story confirms extraction and analysis remain separate and that the current downstream sequence is preserved: approved review data -> Create Spatial Units local/map output -> Final Review -> Finalize publish/writeback.
7. Given Sally's UX direction has two main surfaces, when decisions are locked, then the process confirms the ArcGIS Pro dockpane remains the workflow/process surface and the Points Validation workspace remains the detailed review/correction surface.
8. Given the new UX must support multiple parcels, when decisions are locked, then the review contract confirms transaction/document-wide tabs remain generic while Points and Boundary Segments are filtered by active parcel.
9. Given real transaction cases have been used during PE/PXA troubleshooting, when decisions are locked, then the inventory identifies which cases can be used as acceptance fixtures, sanitized regression fixtures, or investigation-only evidence, including at minimum `100001027`, `100000896`, `100000622`, `100001005`, `100000623`, and `100000626`.
10. Given this is a documentation and decision story, when implementation is complete, then no production source files are changed unless explicitly approved for this story.
11. Given the locked decisions will drive later work, when this story is complete, then the prerequisites or open questions for stories `11-2` and `11-3` are updated in the story slicing plan or pipeline inventory.

## Tasks / Subtasks

- [x] Review `plan-examination-pipeline-inventory.md` and convert draft recommendations into explicit final decisions for every row. (AC: 1)
- [x] Lock the decision for `extraction_adapter.py`: retire, recreate, or keep only as compatibility shim. (AC: 2)
- [x] Lock the deterministic embedded-text PDF route ownership for `pdf_text_structured_extraction.py`. (AC: 3)
- [x] Lock the scanned PDF OCR/vision route direction for `survey_plan_ocr_vision_extraction.py`, including compatibility naming and multi-parcel expectations. (AC: 4)
- [x] Clarify the future boundary between route planning and route execution in `CreateParcelDraftExtractionAdapter.cs`. (AC: 5)
- [x] Confirm the separation between extraction, post-extraction analysis, review approval, output generation, and final writeback. (AC: 6)
- [x] Confirm the two-surface UX and active-parcel filtering rules from Sally's UX artifact. (AC: 7, 8)
- [x] Build the case/fixture decision table for known PE/PXA examples. (AC: 9)
- [x] Update the planning artifacts with any decisions or open questions that block `11-2` or `11-3`. (AC: 11)
- [x] Verify this story did not change production code unless the team explicitly approved it. (AC: 10)

## Dev Notes

This story should be executed as a planning and architecture decision pass. Do not start the PE/PXA refactor here.

Primary artifact to update:

- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md`

Supporting artifacts:

- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-story-slicing-plan.md`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-process-design.md`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-unified-review-ux.md`
- `_bmad-output/planning-artifacts/plan-examination-general/pe-pxa-general-plan-examination-refactor-analysis.md`

Important implementation context already agreed:

- `Validate Points and Lines` loads and reviews `working/extraction_review_data.json`, lets the examiner correct extracted data, and writes `working/approved_review.json` after approval.
- `Create Spatial Units` validates approved review data and creates local/map-ready output geometry.
- `Final Review` remains the visual approval gate.
- `Finalize` publishes approved geometry to Enterprise `working_review` when configured for finalize timing, records disposition, saves Innola SpatialUnit evidence, uploads the package, and completes the Innola transaction.
- The refactor must preserve current PXA single-parcel behavior while extending the unified PE workflow to multi-parcel documents.
- The final UX keeps two surfaces: ArcGIS Pro dockpane for process flow and Points Validation workspace for detailed source/point/boundary review.

Key code areas to classify, but not refactor in this story:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowScriptExecutor.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ProcessingTools/adapters/extraction_adapter.py`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspace.xaml`

## Test Guidance

This is expected to be a docs-only story.

- Use `rg` or direct review to confirm every inventory row has one of the approved decision statuses.
- Confirm `11-2` and `11-3` dependencies are not left vague.
- Confirm no production source files changed unless explicitly approved.
- If any production source or project configuration file is changed unexpectedly, run the relevant build/test command before marking the story ready for review.

## References

- Planning analysis: `_bmad-output/planning-artifacts/plan-examination-general/pe-pxa-general-plan-examination-refactor-analysis.md`
- Process design: `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-process-design.md`
- UX design: `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-unified-review-ux.md`
- Architecture inventory: `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md`
- Story slicing plan: `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-story-slicing-plan.md`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-09-12 | 0.1 | Initial story draft for PE/PXA Plan Examination inventory and decision lock. | Mary |
| 2026-09-12 | 0.2 | Executed story as docs-only decision lock; updated pipeline inventory and story slicing plan. | Amelia |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `git rev-parse HEAD`
- `rg "Reuse as-is|Reuse with small changes|Adapt into unified Plan Examination|Retire after compatibility period|Recreate" _bmad-output/planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md`
- `rg "11-2|11-3|Story 11-1 Execution Status|Locked Decision Matrix" _bmad-output/planning-artifacts/plan-examination-general`

### Completion Notes

- Added a locked decision matrix to the Plan Examination pipeline inventory.
- Locked `extraction_adapter.py` as non-production placeholder behavior to retire after compatibility period.
- Locked `pdf_text_structured_extraction.py` as the deterministic embedded-text PDF route for `st_surveysheet` and eligible embedded-text `st_surveyplan` inputs.
- Locked `survey_plan_ocr_vision_extraction.py` as the PXA-compatible scanned/vision implementation to adapt into a generalized multi-parcel Plan Examination route.
- Clarified future `CreateParcelDraftExtractionAdapter.cs` split: route planner, route executor, and artifact normalizer.
- Confirmed extraction, post-extraction analysis, review approval, output generation, Final Review, Enterprise `working_review`, and Innola finalize remain separate.
- Confirmed the two-surface UX and active-parcel filtering rules.
- Classified known cases as acceptance fixture, sanitized regression fixture, support/recovery fixture, or investigation-only evidence.
- No production source files were changed.

### File List

- `_bmad-output/implementation-artifacts/11-1-inventory-and-decision-lock-for-plan-examination-unification.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-story-slicing-plan.md`
