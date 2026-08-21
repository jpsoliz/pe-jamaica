# Compute Stage Steps And Rules

**Last Updated:** 2026-08-20  
**Owner:** Sidwell / NLA Compute Workflow Team  
**Status:** Draft product and implementation alignment document

## Purpose

Define a clear, configurable Compute workflow where each stage has explicit rules, rule outcomes are visible to the examiner, and the same outcomes can be included in the final examination report.

This document is the product-level contract for organizing Compute stages and rules. Implementation stories should use it to keep Settings, runtime configuration, screen behavior, and report output aligned.

## Core Principles

- Compute must be a staged process, not one large hidden validation run.
- Each stage must have a clear purpose and a visible pass, fail, warning, skipped, disabled, or not-applicable result.
- Rules must be cataloged in configuration and surfaced in Settings so an administrator can understand which rules apply to each stage.
- A rule must be movable from one stage to another by configuration when the business process changes, without requiring scattered code changes.
- Screen results and report results must come from the same persisted rule outcomes.
- Extraction is draft data capture. It must not be treated as final geometry or final approval.
- Spatial units must not be created until extracted or entered data has been reviewed and approved according to the configured gates.

## Recommended Compute Stage Order

The Compute workflow should use this order:

1. Supporting Document Check
2. Structure Check
3. Data Extraction
4. Georeference Check
5. Dimension Check
6. Validate Points and Lines
7. Create Spatial Units
8. Final Review
9. Finalize

## Step And Rule Catalog

| Step | Purpose | Rule families | Main artifacts |
|---|---|---|---|
| Supporting Document Check | Confirm the transaction has the expected source documents copied into the Case Folder. | detected profile presence, detected profile completeness, required source roles, source file inventory | `manifest.json`, copied source files |
| Structure Check | Confirm submitted documents and spatial files are structurally usable before extraction and deeper review. | source file integrity, workflow rule resolution, script plan currency, DWG signature, DWG readiness probe, required CAD/spatial layers, system prerequisites | `working/structure_check_summary.json` |
| Data Extraction | Produce draft review data from computation sheets, plan maps, DWG context, coordinate files, or survey plan PDFs. | workflow routing rules, extraction provider rules, source profile rules, script plan rules | `working/extraction_review_data.json`, `working/extraction_points.json`, `working/plan_ocr.json`, `working/dwg_context.json`, `working/survey_plan_extraction_summary.json` |
| Georeference Check | Confirm spatial/geographic coherence of extracted or reviewed evidence. | georeference source presence, coordinate columns, JAD2001 expectation, Jamaica coordinate bounds, parish/location evidence, parish/location mismatch | `working/georeference_check_summary.json` |
| Dimension Check | Confirm dimensions can produce coherent parcel geometry. | dimension source presence, bearing parseability, distance parseability, point references, closure/tolerance, geometry-construction readiness | `working/dimension_check_summary.json` |
| Validate Points and Lines | Let the examiner review, correct, approve, or manually enter points and lines. | review rows present/resolved, coordinates present/numeric, unique point identifiers, parcel construction readiness, closure profiles, orientation detection, bearing-coordinate consistency | `working/approved_review.json`, validation summary artifacts |
| Create Spatial Units | Build spatial output from approved reviewed data. | approved review current, validation passed or dispositioned, output schema rules, orientation normalization policy | output GDB/GeoJSON/spatial unit artifacts |
| Final Review | Summarize all Compute evidence and require examiner disposition before closeout. | stage completion gates, blocker/warning summaries, output readiness, report completeness, spatial unit readiness | compute examination report, output summary |
| Finalize | Attach/report results and complete the transaction workflow. | Innola completion readiness, report attachment, package upload/writeback readiness, final lifecycle gate | Innola checklist/report attachment, lifecycle audit |

## Stage Details

### 1. Supporting Document Check

This stage answers: do we have the expected source package for this transaction?

Rules should verify:

- transaction/source profile was detected
- profile is complete
- required source roles exist
- source files were copied into the Case Folder
- unsupported or missing files are visible to the examiner

This stage should not run extraction, validation, geometry generation, map loading, Enterprise writes, or Innola completion.

### 2. Structure Check

This stage answers: are the submitted files structurally usable?

Rules should verify:

- copied source paths exist and remain inside the Case Folder
- file types are supported
- source files are readable
- workflow rule and script plan are resolved and current
- DWG files, when present or required, have valid signatures and can be inspected
- configured CAD/spatial layer categories are present when required

Examples of CAD/spatial layer categories:

- points
- lines / polylines
- parcel polygons
- annotation / text
- north arrow
- registered adjoining parcel ownership details
- non-registered adjoining parcel occupier details
- streets
- water bodies

Structure Check should be the first business gate before extraction. If this stage fails, the user should fix the source package before proceeding.

### 3. Data Extraction

This stage answers: what draft point, line, OCR, and source evidence can be captured from the submitted package?

Extraction should be controlled by `WorkflowRules.json` or equivalent routing configuration. The script plan should specify:

- required input source roles
- allowed file types
- adapter or script name
- provider mode, such as local, OpenAI-assisted, or hybrid
- output artifacts
- timeout and credential profile rules

Current examples:

- `extract_points_from_computation`
- `ocr_plan_map_reference`
- `inspect_dwg_reference`
- `extract_single_parcel_survey_plan_pdf`

Extraction output is draft evidence for review. It is not final approval and must not create final spatial units by itself.

### 4. Georeference Check

This stage answers: is the extracted/reviewed spatial evidence geographically coherent?

Rules should verify:

- at least one usable georeference source exists
- tabular coordinate files expose Easting/Northing-style columns
- coordinate samples fall within Jamaica working bounds
- JAD2001 evidence is present or flagged when missing
- parish/location metadata agrees with spatial evidence where available
- missing parish/location evidence is a visible finding, not a silent pass

Georeference Check should normally run after Data Extraction because PDF/image evidence, survey plan metadata, coordinate tables, and reviewed extraction artifacts may be needed.

### 5. Dimension Check

This stage answers: are the parcel dimensions coherent enough to support point validation and spatial unit creation?

Rules should verify:

- a computation sheet, survey plan PDF, coordinate text source, or configured spatial line source exists
- bearings are present and parseable where required
- distances are present and parseable where required
- point references connect correctly
- extracted or reviewed segments can form a usable chain
- closure/tolerance evidence is acceptable
- geometry-construction readiness is sufficient for Validate Points and Lines

Dimension Check should not own parish/location/JAD2001 rules. Those belong to Georeference Check.

### 6. Validate Points And Lines

This stage answers: has the examiner reviewed and accepted the point/line truth used to build spatial units?

Rules should verify:

- review rows exist
- unresolved rows are cleared or dispositioned
- required coordinates are present
- coordinate values are numeric
- point identifiers are unique
- parcel construction readiness is acceptable
- closure tolerance profiles pass or are dispositioned
- parcel orientation is computed from the full closed ring
- source bearings are compared against coordinate-derived azimuths

This is the main human review stage. Automated extraction and rules support the examiner, but they do not replace review.

### 7. Create Spatial Units

This stage answers: can approved review data be converted into spatial output?

Rules should verify:

- approved review data is current
- validation results are current
- required blockers are cleared or dispositioned
- output schema requirements are satisfied
- optional orientation normalization is applied only to generated output geometry, not to saved reviewer-entered order

This stage may generate output geometry. Earlier checks must not.

### 8. Final Review

This stage answers: is the transaction ready for closeout?

Rules should verify:

- required stage summaries exist and are current
- blocker findings are resolved or dispositioned
- warning/report-only findings are visible
- output artifacts exist
- spatial unit readiness is clear
- the examination report includes the rule outcomes needed for audit

### 9. Finalize

This stage answers: can the transaction be closed in Innola and the required evidence attached?

Rules should verify:

- final report is generated and attached where required
- output package or spatial unit writeback readiness is satisfied
- Innola completion gate is satisfied
- lifecycle audit is written
- completion failures are visible and recoverable

## Rule Catalog And Settings Requirements

Yes, Compute rules should be cataloged in Settings/configuration.

The Settings workspace should let an administrator see and manage:

- rule id
- display name
- stage assignment
- category
- enabled/disabled state
- severity
- workflow effect
- locked/core status
- source role filters
- file type filters
- transaction/profile filters
- correction guidance
- report visibility
- rule-specific parameters, such as tolerances, layer aliases, expected coordinate system, or orientation policy

The configuration model should support moving a rule between stages by changing its `stage_id` or equivalent field. For example, if a rule currently runs in Dimension Check but should belong to Georeference Check, Settings/config should reflect that move and the UI/report should follow it.

Recommended stage ids:

```text
supporting_document_check
structure_check
data_extraction
georeference_check
dimension_check
validate_points_and_lines
create_spatial_units
final_review
finalize
```

Recommended outcome values:

```text
passed
failed
warning
not_applicable
skipped
disabled
```

Recommended workflow effects:

```text
blocker
requires_disposition
report_only
info
```

## Screen And Report Requirements

Every rule result should be persisted once and reused by both the UI and reports.

Each persisted result should include:

- transaction id
- stage id
- rule id
- display name
- category
- outcome
- severity
- workflow effect
- message
- correction
- affected source path
- source role
- evidence
- operator id
- timestamp
- run id

Screen behavior:

- Each stage card should show current status, blocker count, warning count, and last run time.
- Each stage detail panel should show rule rows with outcome, severity, message, correction, and evidence where useful.
- Disabled, skipped, and not-applicable rules must remain visible where configured for audit.
- Failed report-only findings must be visually distinct from workflow blockers.

Report behavior:

- The examination report should include a stage summary.
- Passed, failed, warning, skipped, disabled, and not-applicable outcomes should be reportable.
- The report should distinguish workflow-blocking failures from report-only findings.
- The report should identify which rules were active for the transaction/profile.

## Configuration Shape Recommendation

The existing `StructureRules.json`, `WorkflowRules.json`, and `rules.yaml` are useful foundations, but the long-term Compute model should converge on a clear staged rule catalog.

Recommended conceptual shape:

```json
{
  "rule_id": "dimension_bearing_consistency",
  "display_name": "Bearing consistency",
  "stage_id": "dimension_check",
  "category": "dimension",
  "enabled": true,
  "severity": "warning",
  "workflow_effect": "requires_disposition",
  "locked": false,
  "applies_to": {
    "transaction_types": ["Plan Examination"],
    "source_roles": ["computation_sheet", "survey_plan_pdf"]
  },
  "parameters": {
    "max_bearing_delta_degrees": 5.0
  },
  "report": {
    "include": true,
    "section": "Dimension Check"
  }
}
```

The implementation may keep multiple physical files if that is safer, but each rule still needs a clear stage assignment and report behavior.

## Current Alignment Notes

Current implementation already has parts of this model:

- `StructureRules.json` catalogs many early-stage rules.
- `WorkflowRules.json` routes source profiles to extraction script plans.
- `rules.yaml` catalogs validation, closure, readiness, orientation, and bearing-consistency profiles.
- Stage summaries are already being persisted for Structure, Georeference, and Dimension checks.
- Story 5.23A added orientation and bearing-coordinate consistency to the validation/output contract.

Open alignment issue:

- The visible product process should explicitly include Data Extraction between Structure Check and Georeference/Dimension checks.
- Rules should be consistently stage-assigned in Settings/config so business changes can move a rule from one stage to another without changing scattered code.
- Amelia should review whether current code still hard-routes any rule to a stage without honoring a configurable stage assignment.

## Decision Target

The desired end state is:

1. A Compute administrator can open Settings and see every Compute rule by stage.
2. A rule can be enabled, disabled, severity-adjusted, or moved to another stage where allowed.
3. The examiner can see rule outcomes on screen at the stage where they ran.
4. The final report uses the same persisted outcomes.
5. The workflow gate uses `workflow_effect`, not only `outcome`, so report-only findings can be captured without blocking progression.
