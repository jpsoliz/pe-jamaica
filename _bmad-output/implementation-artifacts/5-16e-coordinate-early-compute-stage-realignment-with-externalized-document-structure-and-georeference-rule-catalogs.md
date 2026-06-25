---
baseline_commit: handoff-2026-06-24
---

# Story 5.16E: Coordinate Early Compute Stage Realignment With Externalized Document, Structure, And Georeference Rule Catalogs

Status: implemented

## Story

As a cadastral examiner and solution administrator,  
I want the early compute stages and their underlying rules to be refactored together,  
so that the workflow shell, operator guidance, and rule behavior all align around `Supporting Document Check`, `Structure Check`, and `Georeference Check` before `Validate Points`.

## Acceptance Criteria

1. Given the compute workflow shell is displayed, when the operator sees the early-stage sequence, then the first stages are shown in this approved order:
   - `Supporting Document Check`
   - `Structure Check`
   - `Georeference Check`
   - `Validate Points`
   - `Create Spatial Units`
   - `Final Review`
   - `Finalize`

2. Given the current shell uses `Attachments` and `Data Extraction`, when the coordinated refactor is complete, then:
   - `Attachments` is replaced by `Supporting Document Check`
   - `Data Extraction` is no longer the single operator-facing stage name
   - extraction-era logic is redistributed behind `Structure Check` and `Georeference Check`
   - `Validate Points` remains the dedicated examiner review stage rather than being merged into automated georeference checks.

3. Given different transaction types require different submitted files, when `Supporting Document Check` runs, then its rules are loaded from configuration and can define required, optional, and disallowed source roles by transaction type and workflow stage.

4. Given source files may arrive as computation PDFs, plan/map PDFs, raster images, DWG, TXT, or CSV, when `Structure Check` runs, then its rules are loaded from configuration and can define source-type-specific readiness expectations such as:
   - required file extensions or content types
   - expected source role
   - whether embedded text is preferred
   - whether OCR fallback is allowed
   - whether DWG layer validation is required
   - whether tabular coordinate columns are required.

5. Given coordinate information is provided differently by source type, when `Georeference Check` runs, then its rules are loaded from configuration and can define source-type-specific location rules such as:
   - minimum coordinate presence requirements
   - valid coordinate source role(s)
   - Jamaica bounds/tolerance expectations
   - whether control-point minimums are required
   - whether georeference may come from TXT/CSV instead of plan imagery.

6. Given the transaction may contain multiple attachments, when operator-facing guidance is shown, then the stage copy clearly reflects the current supported source roles:
   - computation sheet PDF used to extract points
   - plan/map PDF or image used as printed map reference
   - DWG file used for structural validation and later local `.gdb` import where applicable.

7. Given some rule failures should stop the workflow while others should guide the examiner, when the externalized rule catalog is used, then each rule declares severity at minimum as:
   - `blocker`
   - `warning`
   - `info`
   and the early-stage summaries surface them consistently.

8. Given the add-in already supports configuration-backed preflight rules, when this story is implemented, then the new early-stage rule catalog follows the same broad design principles:
   - readable/editable in project settings files
   - resilient to partial-invalid configuration
   - safe fallback behavior when a custom catalog is missing or malformed
   - clear operator/admin warnings when fallback behavior is being used.

9. Given `Create Spatial Units` already creates local review geometry only, when the refactor is complete, then no user-facing copy suggests that this stage commits geometry to the authoritative cadastre or performs final Parcel Fabric Maintenance.

10. Given `Final Review` is the stage after local spatial creation, when help text and status messages are updated, then the operator understands that `Final Review` confirms the local spatial result is ready for final submission / downstream handoff rather than directly updating the final cadastre.

11. Given this story is complete, when later extraction, DWG, comparison, and multi-cadastre stories are implemented, then they can plug into a stable early-stage rule contract and vocabulary instead of adding more one-off branching and mixed labels.

## Tasks / Subtasks

- [x] Realign the early compute-stage vocabulary in the shell. (AC: 1-2, 6, 9-10)
  - [x] Replace `Attachments` with `Supporting Document Check` in lifecycle chips, active-stage text, and related shell labels.
  - [x] Replace the single business-stage label `Data Extraction` with the split operator-facing wording for `Structure Check` and `Georeference Check`.
  - [x] Keep `Validate Points`, `Create Spatial Units`, `Final Review`, and `Finalize` aligned to the current operator journey.

- [x] Define and add the external rule catalog contract. (AC: 3-5, 7-8)
  - [x] Create the schema for grouped rule definitions under:
    - `supporting_document`
    - `structure`
    - `georeference`
  - [x] Define severity, enablement, and operator-facing metadata.
  - [x] Define safe fallback behavior for missing or invalid custom catalogs.

- [x] Externalize supporting document rules by transaction type. (AC: 3, 7-8)
  - [x] Allow transaction-type and compute-stage-specific required/optional/disallowed source-role rules.
  - [x] Support at least the current compute transaction set used by the add-in.
  - [x] Surface blocker/warning/info results back into the early-stage summaries.

- [x] Externalize structure rules by source type. (AC: 4, 6-8)
  - [x] Support structure rules for computation-sheet PDF, plan/map PDF or image, DWG, TXT, and CSV.
  - [x] Define whether embedded text, OCR fallback, DWG validation, or tabular-column checks apply.
  - [x] Map results cleanly into the `Structure Check` stage without changing the approved business wording.

- [x] Externalize georeference rules by source type. (AC: 5, 7-8)
  - [x] Define coordinate-presence and control-point rules.
  - [x] Define Jamaica bounds/tolerance rules.
  - [x] Define how text/CSV coordinate sources can satisfy georeference readiness when plan imagery does not carry visible coordinates.

- [x] Integrate the new rule catalog into the current workflow services and shell copy. (AC: 2-11)
  - [x] Add loader/service support parallel to the existing preflight-rule pattern.
  - [x] Ensure partial-invalid catalogs degrade safely with warnings.
  - [x] Preserve existing compute behavior until explicit rules are enabled or resolved through the new catalog.
  - [x] Update warnings, banners, status messages, and helper copy so the UI terminology matches the rule ownership.

## Dev Notes

### Why This Story Exists

- The SDS memorandum separates the early compute flow into distinct business checks:
  - supporting document completeness
  - structure readiness
  - georeference readiness
- Story `5.16C` isolated the **naming alignment**.
- Story `5.16D` isolated the **rule contract**.
- In practice, these two changes should land together so the user sees the new stage names only when the underlying rule ownership is ready to support them.

### Coordinated Refactor Principle

This story intentionally combines:

- operator-facing stage renaming
- rule-catalog externalization
- guidance/status-message alignment

That avoids a temporary mismatch where:

- the UI says one thing
- the logic still behaves like the older compressed stage model

### Recommended Catalog Location

Use a dedicated JSON file parallel to existing settings-based rule catalogs, for example:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/DataExtractionRules.json`

Recommended top-level shape:

```json
{
  "schema_version": "1.0.0",
  "rules": [
    {
      "id": "required_computation_pdf_for_plan_exam",
      "group": "supporting_document",
      "title": "Computation sheet required",
      "description": "Plan Examination compute cases must include a computation sheet PDF.",
      "severity": "blocker",
      "enabled": true,
      "transaction_types": ["Plan Examination"],
      "workflow_stages": ["Assign Computation Task"],
      "source_roles": ["computation_sheet"],
      "file_types": [".pdf"]
    }
  ]
}
```

### Recommended Source Roles

Suggested minimum source-role vocabulary:

- `computation_sheet`
- `plan_map_reference`
- `dwg_source`
- `coordinate_text_source`
- `supporting_document`

### Recommended Business Meaning

#### Supporting Document Check

Confirms:

- the transaction attachments were received
- the required documents for the transaction/submission type are present

#### Structure Check

Confirms:

- the source files are structurally usable for downstream processing
- computation PDFs, plan/map references, DWG, and table sources each satisfy their own readiness expectations

#### Georeference Check

Confirms:

- enough coordinate context exists to proceed
- valid source roles can satisfy coordinate readiness
- the source data falls within Jamaica tolerance expectations

#### Validate Points

Remains the human review stage for:

- parcel-by-parcel inspection
- correction of extracted points
- confirmation of the saved point set before spatial creation

### Scope Boundaries

This story does **not**:

- redesign the point validation tool
- implement downstream comparison workflow behavior in full
- define final authoritative cadastre sync rules
- replace the existing document-type catalog used for extraction routing

It does make the early-stage workflow and rules formal enough that those later stories can hook in cleanly.

### Relationship To Existing Patterns

Reuse the current project style already established for:

- `PreflightRules.json`
- rule-catalog loaders
- safe fallback behavior
- settings-surface validation and summary warnings

The goal is consistency with the existing brownfield architecture.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

## References

- `_bmad-output/implementation-artifacts/5-16c-realign-early-compute-stages-around-document-structure-and-georeference-checks.md`
- `_bmad-output/implementation-artifacts/5-16d-externalize-data-extraction-rules-by-transaction-and-source-type.md`
- `_bmad-output/implementation-artifacts/5-16a-realign-compute-workflow-vocabulary-around-data-extraction-and-points-validation.md`
- `_bmad-output/implementation-artifacts/4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md`
- `_bmad-output/implementation-artifacts/2-8-validate-dwg-readiness-when-present.md`
- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `C:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\Doc\MEMORANDUM - Sidwell SDS Update Requirements.docx`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-24 | 0.1 | Drafted the coordinated refactor story that merges early-stage vocabulary realignment with the externalized supporting-document, structure, and georeference rule catalog. | Codex |
| 2026-06-24 | 1.0 | Implemented grouped early-stage rule metadata, georeference readiness checks, aligned workflow labels, grouped dockpane presentation, and updated test coverage/build compatibility. | Codex |
