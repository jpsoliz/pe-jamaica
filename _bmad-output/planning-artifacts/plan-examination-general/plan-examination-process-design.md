# Plan Examination Process Design

Last updated: 2026-09-12
Owner: Mary / Business Analysis
Review partners: Sally / UX, Winston / Architecture, Amelia / Implementation
Status: Draft process design before PRD and stories

## Purpose

Define the target `PE / Plan Examination` workflow after unifying current PE and PXA behavior.

This document focuses on examiner flow, stage grouping, visible recovery actions, and the process contract. It should be reviewed before creating PRD addenda, architecture decisions, UX specs, epics, or implementation stories.

Related analysis:

- `_bmad-output/planning-artifacts/plan-examination-general/pe-pxa-general-plan-examination-refactor-analysis.md`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-unified-review-ux.md`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md`
- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-story-slicing-plan.md`
- `_bmad-output/planning-artifacts/plan-examination-general/General Plan Examination story.txt`

## Confirmed Product Direction

- Transaction code: `PE`
- User-facing workflow name: `Plan Examination`
- PXA should eventually disappear as a separate user-visible workflow.
- Existing PXA work must not be lost; it should become profile/routing behavior inside unified Plan Examination until parity is proven.
- The unified workflow must support single-parcel and multi-parcel documents.
- The unified workflow must support different source-document types without creating separate user-facing processes.

## Supported Source Inputs

Mandatory source families:

- Survey plan PDF: `st_surveyplan`
- Survey sheet PDF / computation plan PDF: `st_surveysheet`


Optional source family:

- DWG file: `st_autocad_file`
- Coordinate table file: `st_survey_points`

The workflow should detect the source type and route extraction/review by profile. The examiner should not need to know whether legacy code calls the route PE or PXA.

## Macro Workflow

The detailed stages remain audit checkpoints, but the examiner-facing happy path should use three macro actions.

### 1. Process Plan

Runs:

1. Supporting Document Check
2. Structure Check
3. Data Extraction
4. Georeference Check
5. Dimension Check

Intent:

- Confirm the transaction has usable source documents.
- Determine the document/profile route.
- Extract draft evidence.
- Check whether the extracted evidence is spatially and dimensionally coherent enough for review.

Expected output:

- Source inventory.
- Extraction route evidence.
- Draft review artifact.
- Stage summaries.
- Clear counts for points, segments, parcels, metadata, warnings, and blockers.

Failure behavior:

- Stop at the first blocking condition that prevents meaningful downstream work.
- Preserve all successful stage artifacts.
- Show a concise examiner message and detailed diagnostics.
- Offer visible reprocess/restart actions.

### 2. Validate Points And Create Spatial Units

Runs:

1. Validate Points and Lines
2. Create Spatial Units, only after explicit user action

Intent:

- Let the examiner review and correct the point/boundary truth.
- Support parcel-scoped review for one or multiple parcels.
- Build spatial units only from approved reviewed data after point validation, rule checks, closure values, and required geometry checks pass.
- Require the user to press a `Create Spatial Units` action after validation passes; this step must not happen automatically.

Expected output:

- Approved review artifact.
- Parcel construction validation.
- Spatial-unit creation request/response evidence.
- Output GDB/GeoJSON/report artifacts as configured.

Failure behavior:

- If validation fails, do not create spatial units.
- If validation passes, enable the `Create Spatial Units` action and show the validation evidence that made it eligible.
- If spatial-unit creation fails, preserve approved review data and validation output.
- Show whether failure is caused by missing points, missing segments, parcel grouping, closure/tolerance, schema, Innola API, or map/output generation.

### 3. Final Review And Finalize

Runs:

1. Final Review
2. Finalize

Intent:

- Give the examiner one final transaction-wide review of findings, outputs, warnings, and blockers.
- Attach/write back required results.
- Complete the Innola workflow only after gates pass.

Expected output:

- Final report.
- Lifecycle audit.
- Innola attachment/writeback evidence.
- Final workflow transition result.

Failure behavior:

- Preserve final report/output artifacts if created.
- Allow retry finalize/writeback without rerunning extraction or validation.
- Show clear distinction between report generation failure, attachment failure, Innola Plan writeback failure, spatial-unit failure, and lifecycle transition failure.

## Stage Visibility

The UI should show the three macro actions as the main workflow.

Under each macro action, detailed stages should remain visible with:

- status
- blocker count
- warning count
- last run timestamp
- main output artifact
- reprocess action where allowed

The examiner should not need nine separate clicks in the normal path, but support and power users must be able to inspect each stage.

## Partial Extraction Messaging

Partial extraction is expected and must be communicated as a usable diagnostic state, not as a generic failure.

Preferred message style:

`8 points found, 0 segments found, parcel cannot be built yet.`

Message requirements:

- Always show counts where available.
- Say what is missing.
- Say whether review can continue.
- Say which action is recommended next.
- Distinguish no extraction from filtered-out-by-parcel, stale artifact, invalid schema, or unsupported document.

Examples:

- `8 points found, 0 segments found, parcel cannot be built yet. Review extraction source or add boundary segments manually.`
- `12 points and 12 segments found across 2 parcels. Select a parcel to validate its boundary.`
- `0 points and 0 segments found. The selected source did not produce usable geometry. Rerun extraction or use Manual Mode.`
- `Segments exist, but none belong to the active parcel. Select another parcel or repair parcel grouping.`

## Parcel Selector

The parcel selector is required for multi-parcel support and should still behave consistently for single-parcel documents.

Display priority:

1. Parcel name from the document, when available.
2. Lot number, when available.
3. Generated label such as `parcel-001`.

The selector should show enough context to avoid ambiguity:

- display label
- point count
- segment count
- validation status
- missing-data indicator when applicable

Examples:

- `Lot 4 - 12 points / 12 segments`
- `Rose Hill - 9 points / 8 segments`
- `parcel-001 - 8 points / 0 segments`

## Transaction-Wide Versus Parcel-Scoped UX

Transaction-wide areas:

- Supporting Documents
- General Information
- Owners / Occupiers / Parties
- Memorandum
- Stage Findings / Diagnostics
- Final Review

Parcel-scoped areas:

- Points
- Boundary Segments
- Parcel Preview / Map Evidence

Rules:

- The parcel selector filters Points and Boundary Segments.
- Add/Edit/Delete/Rebuild applies only to the active parcel.
- Stage findings must say whether they apply to the whole transaction or the active parcel.
- The UI must never show an empty point/segment grid without explaining why it is empty.

## Visible Reprocess And Restart

Restart and reprocess controls should be visible, not hidden only in a support tool.

Required actions:

- Restart whole transaction.
- Rerun extraction only.
- Clear/rebuild validation only.
- Recreate spatial units only.
- Retry finalize/writeback only.

Each action must show:

- what will be deleted or regenerated
- what will be preserved
- which artifacts are affected
- whether the action can be undone

Design principle:

Restart should be stage-aware. A user who only needs to retry finalize should not have to delete extraction/review artifacts. A user who only needs to rerun OCR should not lose approved source intake unless explicitly requested.

## Artifact Expectations

The process should classify artifacts as:

- source/provenance
- authoritative review state
- generated output
- stage summary
- diagnostic/cache
- lifecycle/writeback evidence

The existing-case badge and restart decisions must be based on explicit artifact state, not implicit folder existence alone.

Minimum artifacts to classify in follow-up:

- `manifest.json`
- `working/extraction_route.json`
- `working/extraction_review_data.json`
- `working/approved_review.json`
- `working/*_check_summary.json`
- `working/spatial_unit_api_response.json`
- `output/output_summary.json`
- `output/*_parcel_output.gdb`
- lifecycle/audit artifacts

## Review Notes By Role

### Mary

Mary should turn this design into product requirements and define which behaviors are must-have versus follow-up.

Open Mary questions:

- What is the minimum viable first release of unified Plan Examination?
- Which source input combinations must be supported in release one?
- Which existing cases are acceptance fixtures versus exploratory evidence?

### Sally

Sally should design the macro-action workflow, parcel selector, partial-extraction states, and visible restart controls.

UX draft:

- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-unified-review-ux.md`

Open Sally questions:

- Where should the three macro actions live in the existing dockpane?
- Should stage detail be always expanded, collapsible, or tabbed?
- How should the UI present transaction-wide findings beside parcel-scoped findings?

### Winston

Winston should define architecture boundaries for profile resolution, stage orchestration, artifact normalization, and restart contracts.

Architecture inventory:

- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md`

Open Winston questions:

- What orchestrates macro actions across existing stage services?
- What is the stable unified review schema boundary?
- How do legacy PE/PXA artifacts normalize without breaking current cases?

### Amelia

Amelia should turn the approved process design into stories only after PRD and architecture agree.

Open Amelia questions:

- Which real case fixtures can be automated first?
- Which current services are safe to adapt before a larger rename?
- Which tests must exist before replacing user-visible PXA behavior?

## Acceptance Criteria Seeds

- Given the examiner opens a PE transaction, when they choose `Process Plan`, then Supporting Document Check, Structure Check, Data Extraction, Georeference Check, and Dimension Check run as one macro action while preserving individual stage results.
- Given extraction finds points but no segments, when the macro action completes, then the UI shows a count-based partial extraction message and recommends a next action.
- Given a multi-parcel document, when extraction or manual review identifies multiple parcels, then the parcel selector lists each parcel using document name, lot number, or generated label in that priority order.
- Given a parcel is active, when the examiner edits points or boundary segments, then only that active parcel changes.
- Given a user chooses `Rerun extraction only`, then source intake artifacts are preserved and extraction/review draft artifacts are regenerated according to the stage restart contract.
- Given a user chooses `Retry finalize/writeback only`, then extraction, validation, and spatial-unit artifacts are preserved.

## Next Step

Use this process design as input to the PRD addendum. Do not begin implementation until the macro workflow, partial-extraction behavior, parcel selector rules, and restart contract are accepted.
