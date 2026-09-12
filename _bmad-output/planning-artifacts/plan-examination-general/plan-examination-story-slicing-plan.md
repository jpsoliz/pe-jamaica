# Plan Examination Story Slicing Plan

Last updated: 2026-09-12
Owner: Mary / Business Analysis
Review partners: Sally / UX, Winston / Architecture, Amelia / Implementation
Status: Draft story split guidance before formal PRD/epics

## Purpose

Guide the PE/PXA unification work into safe stories instead of one large implementation story.

Current planning inputs are enough to define the direction and first slices:

- `plan-examination-process-design.md`
- `plan-examination-unified-review-ux.md`
- `plan-examination-pipeline-inventory.md`
- `pe-pxa-general-plan-examination-refactor-analysis.md`

They are not yet enough for one full implementation story that rewrites the complete PE/PXA pipeline. That would mix source routing, PDF extraction, OCR/vision, review UX, validation, output creation, Enterprise publish, restart/recovery, and regression fixtures.

## Mary Recommendation

Split this into one epic with several stories.

Do not start with a broad rename from PXA to PE. Start with behavior preservation, contracts, and diagnostics. The target is a unified `PE / Plan Examination` workflow, but the implementation should move in controlled slices.

Use a new planning-level numbering namespace:

- Epic code: `PEU`
- Epic name: `Plan Examination Unification`
- Story format: `PEU-1.1`, `PEU-1.2`, etc.
- Official implementation epic number: `11`
- Official implementation story format: `11-1`, `11-2`, etc.

Reason:

- Existing implementation artifacts already use numeric epic/story filenames through Epic 9.
- Sprint status already contains `10-1-create-user-training-and-test-guide-for-parcel-workflow-extension`.
- `11-*` is the clean next official implementation sequence.
- `PEU` remains useful as the planning label, while `11-*` should be used for implementation artifact filenames and sprint tracking.

## Proposed Epic

### PEU-1: Unified Plan Examination For PDF-Based PE/PXA Review

Epic outcome:

Examiners use one `PE / Plan Examination` workflow for survey plan and survey sheet PDFs, supporting single-parcel and multi-parcel review, while preserving current PXA capabilities and PE multi-parcel corrections.

## Story Split

### PEU-1.1 / 11-1: Inventory And Decision Lock

Goal:

Confirm which existing scripts, services, and procedures are reused, adapted, recreated, or retired.

Why first:

The current pipeline has real behavior spread across C#, Python scripts, placeholders, legacy fallback, and case artifacts. Amelia should not refactor until the team agrees what each piece owns.

Primary inputs:

- `plan-examination-pipeline-inventory.md`

Acceptance criteria:

- Each inventory row has a decision: reuse as-is, reuse with small changes, adapt, retire, or recreate.
- `extraction_adapter.py` placeholder has an explicit decision.
- `survey_plan_ocr_vision_extraction.py` has an explicit decision for compatibility name versus new generalized route.
- The team agrees extraction and analysis remain separate.

### PEU-1.2 / 11-2: Unified PDF Extraction Contract

Goal:

Define the canonical Plan Examination review artifact for PDF extraction results.

Why second:

Both embedded-text extraction and scanned OCR/vision extraction need one output model before UX and validation can be made stable.

Acceptance criteria:

- Contract models transaction-wide metadata, source trace, memorandum, parties, volume/folio, parcel groups, points, boundary segments, confidence, and review status.
- Contract supports `st_surveyplan` and `st_surveysheet` as mandatory PDF evidence.
- Contract supports single parcel and multiple parcels.
- Legacy PE/PXA artifacts can be normalized into the contract.

### PEU-1.3 / 11-3: Route Planner For PDF Extraction

Goal:

Centralize route selection for Plan Examination PDFs.

Why third:

Today route decisions are too distributed. We need predictable behavior before improving extraction quality.

Acceptance criteria:

- Embedded text PDF route is attempted first when useful text exists.
- Scanned/image OCR/vision route is attempted when embedded text is absent or too weak.
- Legacy fallback remains available only when configured and safe.
- Manual Mode is selected when automation is unsafe or insufficient.
- `extraction_route.json` explains route, source files, provider, confidence, counts, and fallback reason.

### PEU-1.4 / 11-4: Multi-Parcel Scanned PDF Extraction

Goal:

Adapt the current PXA scanned survey-plan OCR/vision route so it can extract multiple parcels from Plan Examination PDFs.

Why fourth:

This is the highest business value but also the riskiest extraction change. It should depend on the contract and route planner.

Acceptance criteria:

- OCR/vision extraction can return multiple parcel groups.
- Points and boundary segments include parcel identity.
- Partial extraction counts are produced per parcel.
- Source page/zone evidence is retained.
- Case `100001027` becomes a regression fixture or fixture-derived test.

### PEU-1.5 / 11-5: Points Validation UX Binding

Goal:

Bind the existing Points Validation workspace to the unified contract and final two-surface UX.

Why fifth:

The UX should consume a stable contract, not compensate for inconsistent extraction outputs.

Acceptance criteria:

- Dockpane remains process flow.
- Points Validation remains detailed review workspace.
- Transaction Review tabs are not parcel-filtered.
- Points and Boundary Segments are filtered by active parcel.
- Full findings live in a Findings tab.
- Active parcel blockers are summarized near the parcel selector.
- Large extracted row sets use scrollable grids with sticky headers.

### PEU-1.6 / 11-6: Post-Extraction Analysis And Diagnostics

Goal:

Make extraction quality and validation outcomes clear before Create Spatial Units.

Why sixth:

The user pain has often been "nothing loaded" or "no results" without knowing if the cause is extraction, schema, filtering, or validation.

Acceptance criteria:

- The UI distinguishes no extraction, partial extraction, filtered-out-by-parcel, invalid schema, stale artifact, and unsupported document.
- Extraction quality summary shows counts for points, segments, parcels, metadata, warnings, and blockers.
- Validation remains based on approved reviewed data.
- Validation findings show transaction/document/parcel/segment scope.

### PEU-1.7 / 11-7: Create Spatial Units / Final Review / Finalize Preservation

Goal:

Preserve the current downstream contract while the front half is refactored.

Why seventh:

We already agreed the current sequence is correct: local output first, final review second, Enterprise `working_review` publish during Finalize unless configured otherwise.

Acceptance criteria:

- `Create Spatial Units` consumes only approved normalized review data.
- It creates local/map-ready output geometry.
- `Final Review` remains the visual approval gate.
- `Finalize` publishes approved geometry to Enterprise `working_review`, records disposition, saves SpatialUnit evidence, uploads package, and closes Innola.
- Restart/retry does not require rerunning extraction when only finalize/writeback failed.

## Minimum First Story

Mary recommends the first actionable story should be:

`PEU-1.1: Inventory And Decision Lock`

This story is small but important. It prevents the team from accidentally rewriting valid production behavior or building new UX over unstable extraction contracts.

Official implementation artifact name should start as:

`11-1-inventory-and-decision-lock-for-plan-examination-unification.md`

## Story 11-1 Execution Status

`11-1-inventory-and-decision-lock-for-plan-examination-unification.md` has been created and executed as the decision-lock story.

Locked outputs are recorded in:

- `plan-examination-pipeline-inventory.md`

Key decisions:

- `WorkflowSession.cs`, validation, output, Enterprise working review, and Innola finalize behavior are preserved.
- `CreateParcelDraftExtractionAdapter.cs` is adapted later by splitting route planning from route execution.
- `pdf_text_structured_extraction.py` remains the deterministic embedded-text PDF route.
- `survey_plan_ocr_vision_extraction.py` remains PXA-compatible while being adapted into a generalized multi-parcel Plan Examination vision route.
- `extraction_adapter.py` is not production extraction behavior and should be retired or replaced by a compatibility shim only if needed.
- UX remains two-surface: ArcGIS Pro dockpane for process flow and Points Validation workspace for detailed review.
- Points and Boundary Segments are active-parcel scoped; general tabs remain transaction/document scoped.

## What Is Still Needed Before Full Epic Creation

Before running the formal BMad epic/story workflow, confirm:

- Which documents are the official inputs: process design, UX spec, pipeline inventory, and refactor analysis.
- Whether we need a short PRD addendum before epics.
- Which case folders can be used as acceptance fixtures.
- Whether the complete `11-*` sequence should be added to sprint tracking immediately or created one story at a time after each upstream decision is accepted.

## Suggested Process From Here

1. Mary: confirm this `PEU-1` story map and produce a short PRD addendum / requirements list.
2. Winston: turn the pipeline inventory into architecture decisions for contract, route planner, and data boundaries.
3. Sally: finalize the two-surface UX enough for implementation.
4. Amelia: implement `PEU-1.1`, then `PEU-1.2`, with regression fixtures before extraction changes.

## How To Start

Start with a short PRD addendum, not code.

Recommended first planning artifact:

`plan-examination-prd-addendum.md`

Minimum contents:

- Goal and non-goals.
- Required source types: `st_surveyplan`, `st_surveysheet`, optional `st_autocad_file`, optional `st_survey_points`.
- Confirmed two-surface UX: dockpane process flow plus Points Validation workspace.
- Confirmed downstream workflow contract: Create Spatial Units creates local/map-ready output; Final Review approves; Finalize publishes to Enterprise `working_review` and closes Innola.
- Epic `PEU-1` with stories `PEU-1.1` through `PEU-1.7`.
- Acceptance fixtures: `100001027`, `100000896`, `100000622`, `100001005`, and any additional approved cases.

After the PRD addendum is accepted:

1. Create Winston architecture decision for `PEU-1.2` and `PEU-1.3`.
2. Create implementation story `11-2` / `PEU-1.2` for the unified PDF extraction contract.
3. Use `11-2` to define the canonical review artifact before touching extraction route code.

## Mary Decision

We have enough information to start story shaping.

We do not have enough information to safely create one implementation story for the whole PE/PXA refactor.

Split the work.
