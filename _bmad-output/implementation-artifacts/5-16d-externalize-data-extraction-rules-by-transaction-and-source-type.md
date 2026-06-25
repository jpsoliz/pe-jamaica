---
baseline_commit: handoff-2026-06-24
---

# Story 5.16D: Externalize Data Extraction Rules By Transaction And Source Type

Status: superseded

> Superseded by `5.16E - Coordinate early compute stage realignment with externalized document/structure/georeference rule catalogs` so the rule contract and the UI/workflow wording are implemented together.

## Story

As a solution administrator and cadastral examiner,  
I want the early compute-stage rules externalized by transaction type and source type,  
so that `Supporting Document Check`, `Structure Check`, and `Georeference Check` can be configured, explained, and evolved without hardcoding every rule path into the workflow shell.

## Acceptance Criteria

1. Given the compute workflow now distinguishes `Supporting Document Check`, `Structure Check`, and `Georeference Check`, when the rule model is externalized, then each check can load rule definitions from configuration rather than relying only on hardcoded logic.

2. Given different transaction types require different submitted files, when `Supporting Document Check` runs, then the configured rule catalog can define required, optional, and disallowed source roles by transaction type and workflow stage, including at minimum:
   - `Plan Examination`
   - `Cadastral Plan Examination`
   - current compute workflow stages such as `Assign Computation Task`, `Compute Survey Plan`, and related supported compute-stage variants.

3. Given source files may arrive as computation PDFs, plan/map PDFs, raster images, DWG, TXT, or CSV, when `Structure Check` runs, then the configured rule catalog can define source-type-specific readiness expectations such as:
   - required file extensions or content types
   - expected source role (`computation_sheet`, `plan_map_reference`, `dwg_source`, `coordinate_text_source`)
   - whether embedded text is preferred
   - whether OCR fallback is allowed
   - whether DWG layer validation is required
   - whether tabular coordinate columns are required.

4. Given coordinate information is provided differently by source type, when `Georeference Check` runs, then the configured rule catalog can define source-type-specific location rules such as:
   - minimum coordinate presence requirements
   - valid coordinate source role(s)
   - Jamaica bounds/tolerance expectations
   - whether two-point control is required
   - whether georeference can come from text/CSV rather than the plan image itself.

5. Given some rule failures should stop the workflow while others should guide the examiner, when the externalized rule catalog is used, then each rule can declare severity and intended behavior at minimum as:
   - `blocker`
   - `warning`
   - `info`
   and the workflow surfaces these consistently in the corresponding early-stage summaries.

6. Given the add-in already supports configuration-backed preflight rules, when this story is implemented, then the new data-extraction rule catalog follows the same broad design principles:
   - readable/editable in project settings files
   - resilient to partial-invalid configuration
   - safe fallback behavior when a custom catalog is missing or malformed
   - clear operator/admin warnings when fallback behavior is being used.

7. Given business rules must remain understandable to non-developers, when the rule catalog is authored, then each rule definition includes operator-facing metadata at minimum:
   - stable rule id
   - check group (`supporting_document`, `structure`, `georeference`)
   - title
   - description
   - severity
   - enabled flag
   - transaction/source applicability metadata.

8. Given this story is complete, when later extraction, DWG, document-type, or comparison stories are implemented, then they can plug into a stable rule contract rather than adding more one-off branching into the workflow session or shell copy.

## Tasks / Subtasks

- [ ] Define the external data-extraction rule catalog contract. (AC: 1, 5-7)
  - [ ] Create the schema for grouped rule definitions under:
    - `supporting_document`
    - `structure`
    - `georeference`
  - [ ] Define severity, enablement, and operator-facing metadata.
  - [ ] Define safe fallback behavior for missing or invalid custom catalogs.

- [ ] Externalize supporting document rules by transaction type. (AC: 2, 5, 7)
  - [ ] Allow transaction-type and compute-stage-specific required/optional/disallowed source-role rules.
  - [ ] Support at least the current compute transaction set used by the add-in.
  - [ ] Surface blocker/warning results back into the early-stage workflow summaries.

- [ ] Externalize structure rules by source type. (AC: 3, 5, 7)
  - [ ] Support structure rules for computation-sheet PDF, plan/map PDF or image, DWG, TXT, and CSV.
  - [ ] Define whether embedded text, OCR fallback, DWG validation, or tabular-column checks apply.
  - [ ] Map results cleanly into the `Structure Check` stage without changing the user-facing stage concept.

- [ ] Externalize georeference rules by source type. (AC: 4, 5, 7)
  - [ ] Define coordinate-presence and control-point rules.
  - [ ] Define Jamaica bounds/tolerance rules.
  - [ ] Define how text/CSV coordinate sources can satisfy georeference readiness when plan imagery does not carry visible coordinates.

- [ ] Integrate the rule catalog with current workflow services. (AC: 1, 5-6, 8)
  - [ ] Add loader/service support parallel to the existing preflight-rule pattern.
  - [ ] Ensure partial-invalid catalogs degrade safely with warnings.
  - [ ] Preserve existing compute behavior until explicit rules are enabled or resolved through the new catalog.

## Dev Notes

### Why This Story Exists

- Story 5.16C aligns the **names and operator meaning** of the early compute stages.
- This story defines the **actual rule contract** behind those stages.
- The SDS memorandum expects the workflow to distinguish:
  - supporting document requirements
  - structure readiness
  - georeference readiness
- Those distinctions should be visible in configuration and not buried entirely in code.

### Recommended Catalog Shape

The cleanest approach is a dedicated JSON file parallel to existing rule settings, for example:

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

To keep the rule model stable, define source-role vocabulary explicitly. Suggested minimum roles:

- `computation_sheet`
- `plan_map_reference`
- `dwg_source`
- `coordinate_text_source`
- `supporting_document`

### Recommended Rule Dimensions

#### Supporting Document rules

Should answer:

- what files are required for a given transaction type?
- what files are optional?
- what files are not valid for this path?

#### Structure rules

Should answer:

- is this source structurally usable?
- what validation path applies by source type?
- is OCR allowed?
- is embedded text required/preferred?
- are DWG layer checks required?
- are CSV/TXT column checks required?

#### Georeference rules

Should answer:

- is there enough coordinate context to proceed?
- what source can satisfy that requirement?
- do the coordinates appear to fall within Jamaica?
- do we require minimum control-point presence?

### Scope Boundaries

This story does **not**:

- redesign the point validation tool
- implement every future comparison rule
- define final authoritative cadastre sync rules
- replace the existing document-type catalog used for extraction routing

Instead, it gives the early compute checks a formal contract that future logic can plug into.

### Relationship To Existing Patterns

Reuse the current project style already established for:

- `PreflightRules.json`
- rule-catalog loaders
- safe fallback behavior
- settings-surface validation and summary warnings

The goal is consistency, not a brand-new rule-engine pattern.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`

## References

- `_bmad-output/implementation-artifacts/5-16c-realign-early-compute-stages-around-document-structure-and-georeference-checks.md`
- `_bmad-output/implementation-artifacts/4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md`
- `_bmad-output/implementation-artifacts/2-8-validate-dwg-readiness-when-present.md`
- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `C:\JPFiles\Dropbox\Sidwell\Projects\Jamaica\Doc\MEMORANDUM - Sidwell SDS Update Requirements.docx`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-24 | 0.1 | Drafted the companion rules story for supporting document, structure, and georeference checks by transaction type and source type. | Codex |
