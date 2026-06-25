---
baseline_commit: handoff-2026-06-24
---

# Story 5.16F: Configure Supporting Document Source Types And Attachment Role Rules For Compute Intake

Status: implemented

## Story

As a cadastral examiner and solution administrator,  
I want the compute intake stage to classify transaction attachments by configured source type and enforce role-based supporting-document rules,  
so that the workflow can distinguish required survey inputs, optional direct-point imports, DWG references, and internal resume packages before structure and georeference checks run.

## Acceptance Criteria

1. Given the compute workflow begins at `Supporting Document Check`, when attachments are loaded from the Innola transaction, then each attachment is classified against a configured supported source-type catalog rather than only filename heuristics.

2. Given the supported compute source types are now formalized, when configuration is loaded, then it supports at minimum these business source types:
   - `st_surveyplan`
   - `st_surveysheet`
   - `st_survey_points`
   - `st_autocad_file`
   - `st_survey_zip`

3. Given a transaction is a compute-type transaction, when `Supporting Document Check` runs, then the stage treats the source types as follows:
   - `st_surveysheet` = required primary source for point extraction
   - `st_surveyplan` = required plan/map reference source
   - `st_autocad_file` = required CAD/supporting spatial reference source
   - `st_survey_points` = optional direct point-import source
   - `st_survey_zip` = internal system package and excluded from business completeness checks

4. Given one or more required source types are missing, when `Supporting Document Check` completes, then the workflow surfaces a blocker that clearly identifies which required source role(s) are missing and does not continue automatically into later automated stages.

5. Given `st_survey_points` is present as `.txt` or `.csv`, when the attachment rules are resolved, then the workflow records it as an optional structured coordinate source that can be imported on demand without replacing the required `st_surveysheet` business role.

6. Given `st_autocad_file` is present as `.dwg`, when the source-type rules are resolved, then the workflow records it as a CAD source intended for later import/reference behavior and not as the main AI extraction source.

7. Given `st_survey_zip` is present, when attachments are classified, then it is marked as an internal workflow/resume package and is:
   - excluded from supporting-document completeness
   - excluded from structure and georeference business readiness counts unless a dedicated internal workflow path explicitly uses it
   - still available for resume/suspend handling where needed.

8. Given the transaction may include multiple files of similar extension, when classification occurs, then the workflow persists both:
   - the configured source type
   - the resolved workflow source role
   for each attachment so downstream `Structure Check`, `Georeference Check`, and extraction routing do not have to infer the role again.

9. Given the operator is reviewing the early compute stages, when stage summaries and helper text are shown, then the wording reflects the approved meaning:
   - `Supporting Document Check` = confirms required submitted source types are present
   - `Structure Check` = confirms the usable extraction/input structure of those sources
   - `Georeference Check` = confirms coordinate/location readiness

10. Given this story is implemented, when later extraction-routing and source-type-specific rule stories run, then they can plug into the same attachment-role contract instead of reintroducing hardcoded assumptions about PDF, DWG, TXT/CSV, or ZIP behavior.

## Tasks / Subtasks

- [x] Add configured compute source types to the supporting-document contract. (AC: 1-3, 8-10)
  - [x] Extend the attachment/source-type configuration model to include:
    - `st_surveyplan`
    - `st_surveysheet`
    - `st_survey_points`
    - `st_autocad_file`
    - `st_survey_zip`
  - [x] Define the expected file extension/content expectations for each source type.
  - [x] Define the mapped workflow source role for each source type.

- [x] Externalize compute intake completeness rules by transaction type and source role. (AC: 3-4, 7, 9)
  - [x] Mark `st_surveysheet`, `st_surveyplan`, and `st_autocad_file` as required for the current compute workflow.
  - [x] Mark `st_survey_points` as optional.
  - [x] Mark `st_survey_zip` as internal/excluded from business completeness.
  - [x] Surface missing-role blocker messages in `Supporting Document Check`.

- [x] Persist attachment classification for downstream stages. (AC: 5-8, 10)
  - [x] Store the configured source type and resolved source role per attachment in the workflow/session artifact state.
  - [x] Ensure downstream stages can read attachment-role decisions without repeating role inference.
  - [x] Preserve this metadata through reopen/resume where applicable.

- [x] Update early-stage operator messaging to match the source-role model. (AC: 4-5, 7, 9)
  - [x] Clarify that the survey sheet is the main extraction source.
  - [x] Clarify that the plan/map file is the printed spatial reference.
  - [x] Clarify that DWG is a CAD support/import source.
  - [x] Clarify that TXT/CSV points are optional structured imports.
  - [x] Clarify that ZIP is internal workflow packaging, not a submitted survey source.

- [x] Add focused tests for source-type and completeness behavior. (AC: 1-10)
  - [x] Required-role pass case: `st_surveysheet` + `st_surveyplan` + `st_autocad_file`.
  - [x] Missing survey sheet blocker case.
  - [x] Missing survey plan blocker case.
  - [x] Missing DWG blocker case.
  - [x] Optional structured points present but not required case.
  - [x] ZIP present but excluded from completeness case.
  - [x] Multiple same-extension attachments still classify to the right business role case.

## Dev Notes

### Why This Story Exists

- The SDS-aligned compute flow now starts with `Supporting Document Check`.
- That stage needs a stable attachment-role contract before `Structure Check` and `Georeference Check` can behave predictably.
- Today the system already knows about attachments, but it still needs a more formal business meaning for:
  - extraction source
  - map/reference source
  - CAD source
  - direct coordinate source
  - internal resume package

### Approved Source-Type Meanings

#### `st_surveyplan`

- Business meaning: printed survey plan or map reference
- Typical formats: `.pdf`, `.tif`, `.tiff`, image-based plan sources
- Workflow role: `plan_map_reference`
- Main usage:
  - visual reference
  - map/plan verification
  - georeference context

#### `st_surveysheet`

- Business meaning: survey/computation sheet used to extract parcel points
- Typical formats: primarily `.pdf`, possibly image-backed variants later
- Workflow role: `computation_sheet`
- Main usage:
  - primary extraction source
  - AI/text/OCR parsing source
  - source of point, segment, and parcel interpretation

#### `st_survey_points`

- Business meaning: structured point file captured externally
- Typical formats: `.txt`, `.csv`
- Workflow role: `coordinate_text_source`
- Main usage:
  - optional direct import path
  - may support later structure/georeference checks
  - does not replace the business requirement for the survey sheet in the current compute design

#### `st_autocad_file`

- Business meaning: CAD reference drawing
- Typical formats: `.dwg`
- Workflow role: `dwg_source`
- Main usage:
  - structural validation
  - later local `.gdb`/map import
  - reference/supporting geometry, not primary AI extraction

#### `st_survey_zip`

- Business meaning: internal package for workflow persistence/resume
- Typical formats: `.zip`
- Workflow role: `workflow_resume_package`
- Main usage:
  - suspend/resume support
  - internal lifecycle packaging
- Explicitly not a supporting business source for compute completeness

### Recommended Rule Storage

This story should extend the externalized rule/catalog pattern already introduced around:

- `Supporting Document Check`
- `Structure Check`
- `Georeference Check`

Rather than hardcoding these source types in the dockpane or extraction adapter, store them in the same broad settings/rule family used by story `5.16E`.

Suggested direction:

- source-type registry section in settings
- supporting-document required/optional/excluded rules by transaction type
- source-role metadata persisted into the workflow session

### Recommended Workflow Meaning

#### Supporting Document Check

Should answer:

- Did the submitted transaction include the minimum required survey sources?
- Which attachment fulfills each business role?
- Are any files present only for optional or internal purposes?

It should not yet answer:

- whether extraction succeeded
- whether coordinate values are valid
- whether map geometry closes

Those belong later to:

- `Structure Check`
- `Georeference Check`
- `Validate Points`

### Scope Boundaries

This story does **not**:

- redesign the Points Validation Tool
- change how extraction itself works by source type
- implement DWG import into the map or `.gdb`
- change resume package upload behavior

This story does:

- formalize attachment classification
- formalize required vs optional vs internal source roles
- provide the early-stage contract for later source-specific logic

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/DataExtractionRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`

## References

- `_bmad-output/implementation-artifacts/5-16e-coordinate-early-compute-stage-realignment-with-externalized-document-structure-and-georeference-rule-catalogs.md`
- `_bmad-output/implementation-artifacts/2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md`
- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `C:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\Doc\MEMORANDUM - Sidwell SDS Update Requirements.docx`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-25 | 0.1 | Drafted the source-type and attachment-role story to formalize compute intake around required survey sheet, survey plan, DWG, optional structured points, and internal ZIP workflow packages. | Codex |
| 2026-06-25 | 1.0 | Implemented the configured compute source-type registry, canonical source-role persistence, supporting-document completeness wiring, settings-surface editing, workflow-rule normalization, and build/test verification. | Codex |
