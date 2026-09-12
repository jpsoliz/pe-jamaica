# PE/PXA Plan Examination Refactor Analysis

Last updated: 2026-09-12
Owner: Mary / Business Analysis
Review partners: Winston / Architecture, Sally / UX, Amelia / Implementation
Status: Analysis draft for review before PRD, architecture, UX, epics, or stories

## Confirmed Direction

- User-facing transaction code: `PE`
- User-facing workflow name: `Plan Examination`
- Long-term direction: PXA disappears as a separate user-visible workflow after the new unified `PE / Plan Examination` workflow is in place.
- Transition rule: do not lose existing PXA extraction, review, memorandum, and single-parcel behavior. Preserve it as profile/routing capability until it is absorbed and verified inside the unified PE workflow.
- Business goal: reduce duplicate process, make support easier, make extraction and review consistent, and prevent PE/PXA divergence.

## Purpose

This document is the planning home for the PE/PXA refactor. It consolidates the working lessons from the PE and PXA transaction work into one Plan Examination analysis so the team can decide the target process before writing implementation stories.

The rough input note remains beside this document:

- `_bmad-output/planning-artifacts/plan-examination-general/General Plan Examination story.txt`

This Markdown file should become the source-of-truth analysis artifact. The `.txt` file is retained as source feedback/history.

## Goal

Unify the current PE and PXA behaviors into one `PE / Plan Examination` workflow while preserving the transaction-specific experiences that proved necessary during real case work.

The target should not be "delete PXA logic." The target should be:

- One `PE / Plan Examination` process model.
- Profile-driven source handling for survey sheet/computation plan PDF, survey plan PDF, DWG, optional coordinate table files, and related variants.
- One workflow that supports both single-parcel and multi-parcel submitted documents.
- One review artifact contract for points, boundary segments, parcel groups, metadata, validation findings, and examiner overrides.
- One restart/recovery contract for case folders.
- One UX model where transaction-wide evidence and parcel-specific geometry are clearly separated.
- Regression coverage using the real problem cases that drove the fixes.

Core business concept:

- PE and PXA perform the same examination job.
- The main differences are the presented source-document type and whether the document contains one parcel or multiple parcels.
- The unified workflow must recognize the presented information type and parcel count, then route extraction/review accordingly.
- PXA may remain in code, configuration, fixtures, and migration language during the transition, but should not remain the final user-facing workflow split.

## Why This Refactor Exists

The workflow is currently operational only because multiple targeted fixes were layered in under pressure. Those fixes were useful, but the behavior is now distributed across transaction profiles, OCR routes, stage gates, review persistence, map loading, Innola writeback, and case-folder restart behavior.

Main risk: PE and PXA can drift as separate products even though the user experience and data contract should be one coherent `PE / Plan Examination` workflow.

## Evidence Inputs

Primary planning references:

- `_bmad-output/planning-artifacts/plan-examination-general/General Plan Examination story.txt`
- `docs/project/EXTRACTION_PROMPTS_AND_RULES.md`
- `docs/project/COMPUTE_STAGE_STEPS_AND_RULES.md`
- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/pe-pxa-current-review-2026-07-29.md`

Key implementation story references:

- `2-12-execute-draft-extraction-and-review-artifact-generation.md`
- `2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md`
- `2-16b-add-embedded-pdf-text-first-structured-computation-extraction.md`
- `2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `2-18a-add-transaction-type-workflow-profiles-for-pe-and-pxa-source-requirements.md`
- `2-19-implement-pxa-survey-plan-segment-review-and-deterministic-boundary-solver.md`
- `2-20-add-pxa-survey-plan-metadata-review-model-and-ux.md`
- `4-2a-redesign-parcel-workflow-into-stage-focused-workspace.md`
- `4-6-add-extraction-result-decision-gate-for-rerun-vs-manual-review.md`
- `4-8-split-structure-check-and-dimension-check-into-separate-actions-and-result-summaries.md`
- `4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md`
- `4-10-add-configurable-compute-rule-catalog-by-stage.md`
- `4-11-add-pxa-memorandum-detection-and-review-rules.md`
- `4-12-improve-pe-pxa-memorandum-extraction-semantic-review-rules.md`
- `4-13-add-parish-boundary-and-document-consistency-validation-rules.md`
- `5-14-replace-embedded-pdf-browser-with-unified-rendered-document-viewer-for-pdf-and-raster-verification.md`
- `5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`
- `5-15a-add-modal-point-editor-for-parcel-scoped-point-add-edit-in-points-validation-tool.md`
- `5-16-align-compute-workflow-stage-copy-and-jamaica-cogo-handoff.md`
- `5-16a-realign-compute-workflow-vocabulary-around-data-extraction-and-points-validation.md`
- `5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md`
- `5-16c-realign-early-compute-stages-around-document-structure-and-georeference-checks.md`
- `5-16d-externalize-data-extraction-rules-by-transaction-and-source-type.md`
- `5-16e-coordinate-early-compute-stage-realignment-with-externalized-document-structure-and-georeference-rule-catalogs.md`
- `5-16f-configure-supporting-document-source-types-and-attachment-role-rules-for-compute-intake.md`
- `5-17-add-manual-cogo-fallback-branch-from-point-review.md`
- `5-18-route-manual-review-branch-into-configured-gdb-map-editing-path.md`
- `5-19-define-cogo-ready-non-fabric-output-layer-schema.md`
- `5-20-configure-cogo-style-map-symbology-labeling-and-editing-experience-for-non-fabric-spatial-outputs.md`
- `5-21-add-optional-cogo-attributes-and-labels-to-non-fabric-spatial-output-layers.md`
- `5-22-group-transaction-review-layers-and-ground-cogo-diagnostics-in-final-feature-class-state.md`
- `5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md`
- `5-23a-add-orientation-detection-bearing-consistency-and-optional-orientation-normalization.md`
- `5-24-add-whole-review-parcel-context-and-active-parcel-diagnostics-to-points-validation-preview.md`
- `5-25-externalize-parcel-construction-readiness-rules-for-gaps-shared-edges-and-boundary-completeness.md`
- `5-25a-expose-parcel-construction-readiness-rules-in-settings-workspace.md`
- `5-25b-polish-points-validation-tool-visual-hierarchy-and-preview-readability.md`
- `5-25c-refine-points-validation-preview-modes-and-rule-status-readability.md`
- `5-25d-apply-points-validation-tool-icon-and-action-deduplication-polish.md`
- `5-25e-integrate-calcite-esri-svg-icons-into-points-validation-tool.md`
- `7-1-define-enterprise-working-review-layer-schema-and-configuration.md`
- `7-2-publish-approved-review-geometry-to-enterprise-working-layers.md`
- `7-3-restore-transaction-working-state-from-enterprise-review-layers.md`
- `7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md`
- `7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `7-11-write-innola-plan-check-list-on-compute-finalize.md`

Recent investigation and defect references:

- `investigations/pe-100001027-extraction-process-investigation.md`
- `investigations/tr100000622-plan-missing-finalize-investigation.md`
- `investigations/tr100000623-plan-route-finalize-investigation.md`
- `investigations/tr100000839-spatial-unit-save-investigation.md`
- `investigations/arcgispro-load-point-crash-investigation.md`

Recent case examples to preserve as regression fixtures:

- `100001027`: PE extraction produced no useful result during the survey-plan review push.
- `100000896`: point rows existed but were not populating the grid as expected.
- `100000622`: finalize/writeback path exposed Plan lookup identifier mismatch after returned-to-Compute flow.
- `100001005`: client case where deleting the visible case folder did not clear the existing-case behavior as expected.
- `100000623`: linked PE context used by RT and spatial-unit fallback validation.
- `100000626`: linked PE output used by RT map loading validation.

Additional fixture discovery is required. The case-folder root contains more PE/PXA examples and should be inventoried before final PRD acceptance criteria are frozen.

## Current Process Understanding

The desired Plan Examination stage order is:

1. Supporting Document Check
2. Structure Check
3. Data Extraction
4. Georeference Check
5. Dimension Check
6. Validate Points and Lines
7. Create Spatial Units
8. Final Review
9. Finalize

These stages are non-negotiable as audit checkpoints, but the user flow should be simplified. The examiner should be able to run the normal path with only a few clear actions while still having access to stage details, errors, warnings, and reprocess controls when needed.

Product direction:

- Keep the stages visible as status and diagnostics.
- Avoid forcing the examiner to manually execute every stage one by one in the happy path.
- Use three macro workflow actions:
  - `Process Plan`: Supporting Document Check, Structure Check, Data Extraction, Georeference Check, and Dimension Check.
  - `Validate Points And Create Spatial Units`: Validate Points and Lines, then enable an explicit `Create Spatial Units` action after validation passes.
  - `Final Review And Finalize`: Final Review, then Finalize.
- Provide controlled reprocess actions by stage:
  - restart whole transaction
  - rerun extraction only
  - clear/rebuild validation only
  - recreate spatial units only
  - retry finalize/writeback only
- Make restart/reprocess behavior dependent on the current stage and existing artifacts.

Each stage must define:

- Inputs.
- Outputs.
- Authoritative case-folder artifacts.
- Diagnostics artifacts.
- Pass/fail/warning rules.
- Retry/reprocess behavior.
- Restart behavior.
- User-visible status.
- Whether the result is transaction-wide or parcel-specific.

## Current PE/PXA Split

PE is currently computation-sheet driven:

- Main route: `extract_points_from_computation_pdf`
- Main source role: `computation_sheet`
- Supporting source role: `plan_map_reference`
- Review artifact: `working/extraction_review_data.json`

PXA is currently survey-plan PDF driven:

- Main route: `extract_single_parcel_survey_plan_pdf`
- Main source role: `survey_plan_pdf`
- OCR/vision script: `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- Review artifacts include `working/survey_plan_extraction_summary.json`, `working/extraction_review_data.json`, and `working/extraction_route.json`

Refactor direction: PE and PXA should become profiles under one Plan Examination workflow, not two disconnected workflow products.

Unified source-document support:

- Mandatory Survey sheet PDF / computation plan PDF: `st_surveysheet`.
- Mandatory Survey plan PDF: `st_surveyplan`.
- Optional DWG file: `st_autocad_file`.
- Optional coordinate table file: `st_survey_points`.

Unified parcel support:

- Single-parcel document.
- Multi-parcel document.
- Parcel count and parcel grouping should be detected from the document/artifact evidence and corrected by the examiner when needed.

## Data Contract Target

The unified review artifact must explicitly model:

- `rows`: point rows with point id, easting, northing, source, confidence, status, and review fields.
- `segments`: boundary segments with from point, to point, bearing, distance, source, confidence, status, and review fields.
- `parcel_groups`: parcel identity, parcel name, active parcel state, and membership.
- `lots`: lot/parcel metadata where available.
- `document_metadata`: parish, property name, surveyor, survey method, date, volume/folio, memorandum data, source evidence.
- `validation_findings`: stage, rule id, outcome, workflow effect, message, correction, evidence, timestamp.
- `review_overrides`: examiner edits for point, segment, metadata, and parcel grouping values.
- `source_trace`: source document, source page/zone, OCR route, parser route, prompt/profile version where applicable.

Required normalization rules:

- Segment `parcel_group_id` must never be silently blank if both endpoint point rows belong to the same parcel.
- Grid loading must explain whether no rows means no extraction, invalid schema, filtered-out by parcel, stale artifact, or actual empty result.
- Review artifact loading must support legacy artifacts but normalize them into the current contract before UI binding.
- Generated temporary point labels must be clearly marked as review-required, not treated as source truth.
- OCR/vision output and embedded text output must preserve visible evidence, especially memorandum fields and semantic states.

## Case Folder And Restart Contract

This must be one of the first refactor outcomes.

The document must define:

- Dev case-folder root.
- Installer/production case-folder root.
- How the root is resolved from settings.
- Which folder is transaction-scoped.
- Which files are authoritative.
- Which files are cache or diagnostics.
- Which artifact controls the "Existing Case" badge.
- Which artifacts must be deleted to restart one transaction.
- Which artifacts must not be deleted unless the examiner wants to lose source provenance or approved review history.

Minimum authoritative artifacts to classify:

- `manifest.json`
- `working/extraction_review_data.json`
- `working/approved_review.json`
- `working/extraction_route.json`
- `working/*_check_summary.json`
- `working/spatial_unit_api_response.json`
- `output/output_summary.json`
- `output/*_parcel_output.gdb`
- lifecycle/audit artifacts

Open analysis question:

- Does "restart the process" mean restart from source intake, restart from extraction, restart from validation, or restart from spatial-unit creation? The UI and support documentation need separate commands/recipes for each.

## UX Target

Sally's UX position:

The review workspace should separate transaction-wide evidence from active-parcel geometry.

Transaction-wide tabs:

- Supporting Documents
- General Information
- Owners / Occupiers / Parties
- Memorandum
- Stage Findings / Diagnostics
- Final Review

Parcel-scoped tabs:

- Points
- Boundary Segments
- Parcel Preview / Map Evidence

Parcel-scoped behavior:

- A parcel selector controls the Points and Boundary Segments tabs.
- Add/Edit/Delete/Rebuild actions apply only to the active parcel geometry.
- If points or segments exist for another parcel, the UI must say that instead of showing an empty grid with no explanation.
- Partial extraction messages should be plain-count based, for example: `8 points found, 0 segments found, parcel cannot be built yet.`
- For multi-parcel documents, the parcel selector should prefer document-derived identity in this order: parcel name, lot number, then generated label such as `parcel-001`.
- The active parcel preview must show the selected parcel distinctly and avoid implying that hidden parcels are missing.
- Validation messages must say whether they apply to the full transaction or the selected parcel.

## Architecture Target

Winston's architecture position:

The refactor should reduce scattered profile logic without flattening every source into the same parser.

Target boundaries:

- Transaction profile resolver decides the Plan Examination profile.
- Source role catalog decides required and optional documents.
- Extraction route catalog decides script/provider by profile and source role.
- Python extraction emits a stable review contract.
- C# review persistence normalizes legacy artifacts into the stable contract.
- Stage services read the stable contract and write stage findings.
- Output/spatial-unit services consume approved review only.
- Innola writeback services remain separate from extraction and review UX.

Architecture risks:

- Prompt text is still embedded in Python for survey-plan OCR/vision.
- PE/PXA terminology remains visible in class/service names and may obscure the desired Plan Examination model.
- Runtime behavior can depend on hidden local case-folder state.
- Diagnostics are not yet good enough when a grid is empty.
- Dev and installer settings can point to different case-folder roots and make support diagnosis confusing.

## Implementation Target

Amelia's implementation position:

Do not start with a broad rename. Start with contract and behavior stabilization.

Implementation sequencing should be:

1. Lock the existing behavior with regression fixtures from the real cases.
2. Define the unified review artifact schema and normalization adapter.
3. Centralize PE/PXA profile and extraction-route decisions.
4. Rework the review UX around transaction-wide vs parcel-scoped state.
5. Improve restart and diagnostics.
6. Only then consider renaming PXA-specific classes behind compatibility wrappers.

Required test strategy:

- Service-level tests for profile resolution.
- JSON contract tests for legacy artifact normalization.
- UI/view-model tests for active parcel filtering and empty-state diagnostics.
- Python tests for OCR route behavior.
- Case fixture tests for `100001027`, `100000896`, `100000622`, and `100001005`.
- Finalize/writeback tests for transaction id vs transaction number fallback.

## Proposed Epics

### Epic 1: Formalize Plan Examination Case Folder And Restart Contract

Outcome: Support, dev, and installed users can identify the correct case folder and safely restart a transaction from the intended stage.

Candidate stories:

- Document dev/prod case-folder root resolution.
- Classify authoritative vs diagnostic artifacts.
- Add visible restart diagnostics for existing-case detection.
- Add support recipes for restart from intake, extraction, validation, spatial-unit creation, and finalize.

### Epic 2: Stabilize Unified Review Artifact Contract

Outcome: PE and PXA routes emit/load one normalized review model.

Candidate stories:

- Define schema version for Plan Examination review artifacts.
- Add legacy normalization for PE computation-sheet artifacts.
- Add legacy normalization for PXA survey-plan artifacts.
- Add segment parcel inference as a formal normalization rule.
- Add empty-grid reason diagnostics.

### Epic 3: Centralize Extraction Routing And Source Profiles

Outcome: Computation sheets and survey-plan PDFs route through one Plan Examination profile system.

Candidate stories:

- Consolidate profile resolver terms around Plan Examination.
- Externalize prompt/version metadata for OCR/vision route.
- Record route, provider, source role, profile, and prompt version in artifacts.
- Add scanned PDF route diagnostics when no points or segments are extracted.

### Epic 4: Redesign Review UX Around Transaction-Wide And Parcel-Scoped Work

Outcome: The examiner can reliably review each parcel while preserving whole-document context.

Candidate stories:

- Add parcel selector as the controlling context for Points and Boundary Segments.
- Keep General Info, Parties, Memorandum, and Findings transaction-wide.
- Add active-parcel empty states and filtered-out counts.
- Scope Add/Edit/Delete/Rebuild to active parcel geometry.
- Make validation messages clearly parcel-scoped or transaction-scoped.

### Epic 5: Stabilize Spatial Unit Creation, Output, And Finalize

Outcome: Approved review data consistently creates spatial output and writes back to Innola without identifier confusion.

Candidate stories:

- Consume only approved normalized review data for spatial-unit creation.
- Preserve transaction id vs transaction number fallback where Innola endpoints require it.
- Add output GDB and Enterprise working-layer consistency diagnostics.
- Include final review evidence and stage findings in reports.

### Epic 6: Real-Case Regression Fixture Suite

Outcome: The cases that caused support pain become permanent fixtures.

Candidate stories:

- Inventory the available local case folders and classify usable PE/PXA examples by document type, parcel count, extraction route, and failure mode.
- Add fixture for `100001027` no-result extraction route.
- Add fixture for `100000896` point/grid population.
- Add fixture for `100000622` returned-to-Compute finalize lookup.
- Add fixture for `100001005` existing-case restart behavior.
- Add fixture for multi-parcel active-parcel filtering.

## Draft Acceptance Criteria Examples

- Given transaction `100000896`, when the Points Validation Tool opens, then the expected point rows and boundary segments are visible for the active parcel.
- Given a segment has no `parcel_group_id` and both endpoints belong to the same parcel, when review data is loaded, then the segment inherits that parcel group and appears in the active parcel Boundary Segments grid.
- Given extraction produced zero rows, when the review UI opens, then the status explains whether the cause is no extraction, invalid schema, filtered-out parcel, or stale artifact.
- Given transaction `100001005`, when the correct transaction case folder is deleted and the transaction is reopened, then no stale Existing Case badge is shown and the workflow starts from the configured restart point.
- Given a scanned survey-plan PDF, when OCR/vision extraction runs, then the route, provider, profile, prompt version, source document, and result counts are persisted.
- Given PE and PXA source types, when profile resolution runs, then both route through Plan Examination with profile-specific extraction routes.
- Given a user edits points or boundary segments, when a parcel is active, then Add/Edit/Delete/Rebuild affects only that active parcel.

## Open Questions Before PRD

- Which case-folder root is the production installer expected to use in all environments?
- Which real cases can be safely checked into fixtures without sensitive data?
- Which fields are mandatory for spatial-unit creation across all profiles?
- Which diagnostics must appear in the UI versus only in case-folder artifacts?
- At what point should PXA labels disappear from the UI: immediately in the refactor, after fixture parity is proven, or after production migration?

## Recommended Next Step

Mary should turn this analysis into a PRD/addendum for Plan Examination refactor. Winston should then produce architecture decisions for profile resolution, artifact normalization, and stage service boundaries. Sally should produce the review UX specification for transaction-wide versus parcel-scoped tabs. Amelia should not implement broad code changes until those three artifacts agree on the contract and acceptance criteria.
