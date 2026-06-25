---
baseline_commit: handoff-2026-06-24
---

# Story 5.16C: Realign Early Compute Stages Around Supporting Document, Structure, And Georeference Checks

Status: superseded

> Superseded by `5.16E - Coordinate early compute stage realignment with externalized document/structure/georeference rule catalogs` so stage naming and rule behavior can shift together in one refactor.

## Story

As a cadastral examiner using `Parcel Workflow [Compute]`,  
I want the early compute stages to reflect the real business checks described in the SDS memorandum,  
so that the workflow clearly distinguishes document completeness, source structure readiness, and georeference readiness before parcel-point validation begins.

## Acceptance Criteria

1. Given the compute workflow shell is displayed, when the operator sees the early-stage sequence, then the first stages are shown in this approved order:
   - `Supporting Document Check`
   - `Structure Check`
   - `Georeference Check`
   - `Validate Points`
   - `Create Spatial Units`
   - `Final Review`
   - `Finalize` remains the downstream closeout stage when shown by the shell.

2. Given the current shell uses `Attachments` and `Data Extraction`, when the renamed stages are applied, then their business meaning is rebalanced as follows:
   - `Attachments` -> `Supporting Document Check`
   - `Data Extraction` no longer stands alone as the user-facing business stage name
   - `Structure Check` becomes the operator-facing stage that covers extraction-readiness and source-structure checks
   - `Georeference Check` becomes the operator-facing stage that covers coordinate and location readiness before point validation.

3. Given a transaction can contain multiple attachment types, when `Structure Check` guidance is shown, then it reflects the supported source roles:
   - computation sheet PDF used to extract points
   - plan/map PDF or image used as printed map reference
   - DWG file used for structural validation and later local `.gdb` import where applicable.

4. Given the product already uses a dedicated `Validate Points` step, when the new alignment is applied, then `Georeference Check` is treated as a system/business gate and `Validate Points` remains the examiner review-and-correction stage rather than merging the two into one label.

5. Given `Create Spatial Units` already creates local review geometry only, when early-stage wording is revised, then no user-facing copy suggests that this stage commits geometry into the authoritative cadastre or performs Parcel Fabric Maintenance in the enterprise sense.

6. Given `Final Review` is the stage after local spatial creation, when help text and status messages are updated, then the operator understands that `Final Review` confirms the local spatial result is ready for final submission / downstream handoff rather than directly updating the final cadastre.

7. Given preflight/configuration rules already exist in the add-in, when this alignment story is complete, then the early-stage wording clearly prepares the way for a later story that formalizes the rule catalog behind:
   - supporting document rules
   - structure rules
   - georeference rules
   without hardcoding that future rule detail into this naming/alignment story.

## Tasks / Subtasks

- [ ] Realign the early compute-stage vocabulary in the shell. (AC: 1-2, 6)
  - [ ] Replace `Attachments` with `Supporting Document Check` in lifecycle chips, active-stage text, and related shell labels.
  - [ ] Replace `Data Extraction` as the single early business-stage name with the split business wording for `Structure Check` and `Georeference Check`.
  - [ ] Preserve `Finalize` as the downstream workflow closeout stage.

- [ ] Update step descriptions, help text, and status guidance. (AC: 3-6)
  - [ ] Clarify the role of computation sheet, plan/map PDF or image, and DWG inputs in the operator-facing copy.
  - [ ] Keep `Georeference Check` distinct from `Validate Points`.
  - [ ] Ensure `Create Spatial Units` and `Final Review` are described as local-review and submission-readiness stages, not authoritative cadastre commit stages.

- [ ] Align supporting workflow copy and stage summaries. (AC: 2, 6-7)
  - [ ] Update any early-stage warnings, banners, or footer messages that still imply the older `Attachments/Data Extraction` simplification.
  - [ ] Leave detailed rule formalization to a follow-up rule-catalog story rather than overloading this alignment change.

## Dev Notes

### Why This Story Exists

- The SDS memorandum separates the early compute flow into distinct business checks:
  - supporting document completeness
  - structure readiness
  - georeference readiness
- The current add-in shell already improved stage naming, but it still compresses too much meaning into `Attachments` and `Data Extraction`.
- The team wants the operator-facing workflow to match the submitted requirements document without changing the core implementation concept.

### Alignment Recommendation

This story is an **operator-facing workflow-alignment story**, not a deep logic rewrite.

Its purpose is to:

- make the early compute stages read like the SDS memorandum
- preserve the dedicated `Validate Points` stage
- avoid implying that `Create Spatial Units` performs final cadastre maintenance
- prepare for a later story that formalizes the actual rule catalogs

### Recommended Business Meaning

#### Supporting Document Check

Confirms that:

- the transaction attachments were received
- the required documents for the transaction/submission type are present

#### Structure Check

Confirms that the source files are structurally usable for downstream processing, including:

- computation sheet PDF usable for point extraction
- plan/map PDF or image usable for document review/reference
- DWG usable for structure validation and later spatial import where applicable

#### Georeference Check

Confirms that:

- coordinate information is present in the expected source
- the data can be interpreted spatially for Jamaica
- there is enough location readiness to continue into point validation

#### Validate Points

Remains the human review stage for:

- parcel-by-parcel inspection
- correction of extracted points
- confirmation of the saved point set before spatial creation

### Scope Boundaries

This story does **not**:

- redesign the extraction engine
- implement the deeper supporting-document / structure / georeference rules
- change the meaning of `Create Spatial Units` into authoritative Parcel Fabric Maintenance
- implement downstream multi-cadastre promotion

### Expected Follow-Up

This story should be followed by a rules-focused story that externalizes:

- supporting document requirements by transaction type
- structure validation rules by source type
- georeference validation rules by source type

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionDecisionGateService.cs`

## References

- `_bmad-output/implementation-artifacts/5-16a-realign-compute-workflow-vocabulary-around-data-extraction-and-points-validation.md`
- `_bmad-output/implementation-artifacts/5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md`
- `_bmad-output/implementation-artifacts/4-6-add-extraction-result-decision-gate-for-rerun-vs-manual-review.md`
- `C:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\Doc\MEMORANDUM - Sidwell SDS Update Requirements.docx`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-24 | 0.1 | Drafted the early compute-stage alignment story to reflect Supporting Document Check, Structure Check, and Georeference Check before Validate Points. | Codex |
