---
baseline_commit: handoff-2026-08-20
---

# Story 4.10: Add Configurable Compute Rule Catalog By Stage

Status: review

## Story

As a cadastral workflow administrator,  
I want every Compute rule assigned to a clear workflow stage in Settings/configuration,  
so that examiners and administrators can understand what is reviewed at each step, enable or disable configurable rules, set rule severity, add supported rules, and see the same rule outcomes on screen and in the examination report.

## Acceptance Criteria

1. Given the Settings workspace is opened, when the administrator reviews Compute rules, then rules are grouped by the configured Compute stage:
   - `Supporting Document Check`
   - `Structure Check`
   - `Data Extraction`
   - `Georeference Check`
   - `Dimension Check`
   - `Validate Points and Lines`
   - `Create Spatial Units`
   - `Final Review`
   - `Finalize`
2. Given a rule row is shown in Settings, then it displays rule id, display name, stage, category, enabled state, severity, workflow effect, locked/core state, description, source/file/profile scope summary, and report visibility.
3. Given an unlocked rule exists, when the administrator edits it, then enabled/disabled and severity remain editable using the existing supported severities: `warning`, `blocker`, and `configured`.
4. Given a locked/core rule exists, when the administrator edits rules in Settings, then locked rules cannot be disabled, severity-changed, deleted, or moved to an incompatible stage.
5. Given an unlocked rule is moved to another allowed stage by changing `stage_id`, when the settings are saved and the relevant stage runs, then the rule appears and executes under the new stage without hardcoded routing overriding the configuration.
6. Given an administrator adds a new rule, when the rule is saved, then it must use a supported `evaluator_key`/template; arbitrary unsupported rule logic cannot be saved from Settings.
7. Given a rule has an unknown or stage-incompatible `evaluator_key`, when rules are loaded or saved, then the catalog reports a clear validation error and falls back safely without creating false passes.
8. Given a stage runs, when rule results are persisted, then each finding includes stage id, rule id, display name, category, outcome, severity, workflow effect, message, correction, evidence, affected source path/role where applicable, operator id, timestamp, and run id.
9. Given a rule is disabled or skipped, when results are shown on screen or included in reports, then it appears as `disabled` or `skipped`, never as `passed`.
10. Given a failed finding is `report_only` or `requires_disposition`, when the workflow gate is evaluated, then outcome and workflow effect are evaluated separately so report-only findings do not automatically block progression.
11. Given the Compute examination report is generated, when it summarizes stage findings, then it uses the same persisted rule outcomes shown in the screen stage details.
12. Given existing deployments contain `StructureRules.json` or legacy `PreflightRules.json`, when this story loads rules, then existing rules migrate or default into staged rule records without losing enabled/severity settings for unlocked rules.
13. Given Story 4.9 review findings are unresolved, when Amelia starts implementation, then she first reviews and resolves or explicitly incorporates those findings into this story's implementation plan.
14. Given automated tests run, then coverage proves stage grouping, enable/disable, severity persistence, locked-rule protections, moving an allowed rule between stages, supported-rule creation, invalid evaluator fallback, screen/report contract shape, and workflow-effect gating.

## Tasks / Subtasks

- [x] Resolve and incorporate Story 4.9 review findings before changing the rule catalog. (AC: 8, 10, 13)
  - [x] Patch Dimension Check so it validates the specified dimension/geometry rules, not only source presence.
  - [x] Ensure persisted findings carry the full reportable finding contract per result.
  - [x] Ensure Structure Rules settings expose and persist workflow effect.
  - [x] Document any 4.9 finding that is intentionally deferred with rationale in this story's Dev Agent Record.

- [x] Extend the rule catalog contract for staged Compute rules. (AC: 1-8, 10, 12)
  - [x] Add `stage_id` to rule definitions using lowercase snake_case stage ids.
  - [x] Add `workflow_effect` with supported values such as `blocker`, `requires_disposition`, `report_only`, and `info`.
  - [x] Add `evaluator_key` so new rules can be selected from supported evaluator templates rather than arbitrary text logic.
  - [x] Add report visibility metadata such as `report_visible` or an equivalent report section field.
  - [x] Preserve existing `group`/`category` behavior as compatibility metadata where needed, but do not rely on `group` alone for stage routing.

- [ ] Add supported rule/evaluator templates for administrator-added rules. (AC: 6-7, 14)
  - [x] Support at minimum the existing evaluator families already present in code: source/profile presence, source/file integrity, workflow rule/script plan, DWG readiness/layer category, coordinate/georeference readiness, dimension source/readiness, closure/readiness, orientation/bearing consistency, and report-only checklist findings where implementation support exists.
  - [x] Block saves for unknown evaluator keys or evaluator/stage combinations that cannot execute.
  - [x] Show clear validation messages instead of silently dropping invalid rules.

- [ ] Update Settings UI and persistence. (AC: 1-7, 12, 14)
  - [x] Show Compute rules grouped by stage instead of only by current `group`.
  - [x] Show rule id, display name, stage, category, enabled, severity, workflow effect, locked state, description, scope, and report visibility.
  - [x] Preserve current enabled/disabled and severity editing behavior for unlocked rules.
  - [x] Add a controlled stage selector for unlocked movable rules.
  - [ ] Add a controlled "add rule" path that starts from supported evaluator templates.
  - [x] Save edits without losing unrelated JSON fields or unsupported-but-preserved rule parameters.

- [x] Route stage execution from configured rule stage assignments. (AC: 5, 7-10, 14)
  - [x] Replace hardcoded stage membership where practical with catalog-driven rule resolution by `stage_id`.
  - [x] Keep evaluator implementation code explicit and testable; configuration chooses which supported evaluator runs where, not arbitrary code.
  - [x] Ensure a moved rule appears in the target stage's screen details and no longer appears in the source stage.
  - [x] Keep system prerequisites clearly labeled and avoid hiding system failures inside business-rule results.

- [x] Align screen results and examination report output. (AC: 8-11, 14)
  - [x] Ensure stage cards/details use persisted findings rather than recomputing display-only rule state.
  - [x] Ensure disabled/skipped/not-applicable findings are visible where configured for audit.
  - [x] Ensure report generation consumes the same persisted findings and distinguishes workflow blockers from report-only findings.

- [ ] Add focused verification coverage. (AC: 1-14)
  - [x] Settings loads existing `StructureRules.json` and applies staged defaults.
  - [x] Legacy `PreflightRules.json` fallback remains readable.
  - [x] Unlocked rule enable/disable and severity round-trip.
  - [x] Locked rule cannot be disabled, severity-changed, deleted, or moved incompatibly.
  - [x] Rule moved from Dimension Check to Georeference Check appears and runs only in Georeference Check.
  - [ ] New supported template rule can be added and saved.
  - [x] Unknown evaluator key produces validation/fallback without false pass.
  - [x] Failed `report_only` finding appears in report but does not block workflow.
  - [x] Disabled/skipped finding is not counted as passed.
  - [x] Existing Story 4.9 stage summary/reopen tests continue to pass.

## Dev Notes

### Why This Story Exists

The Compute process now has clearly identified stages, but the current rule model is only partially configurable. Administrators can enable/disable some unlocked rules and change severity, but rules are still largely grouped by `group` and routed by hardcoded stage-specific code paths.

The product target is a clearer process:

```text
Supporting Document Check -> Structure Check -> Data Extraction -> Georeference Check -> Dimension Check -> Validate Points and Lines -> Create Spatial Units -> Final Review -> Finalize
```

Each stage should communicate exactly what is reviewed and how. Settings/configuration should make the rule catalog visible and manageable by stage.

### Current Code Reality

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
  - Current model includes `rule_id`, `group`, `category`, `display_name`, `description`, `enabled`, `severity`, `locked`, source/file/profile filters, and rule-specific booleans.
  - It does not currently expose a durable `stage_id`, `evaluator_key`, report visibility, or editable workflow effect in the rule definition.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
  - `EditablePreflightRule` exposes enabled/severity/locked and display grouping through `SectionName`.
  - It does not expose editable stage assignment, evaluator key, workflow effect, or report visibility.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
  - Saves unlocked `enabled` and `severity`; preserves locked-rule protections.
  - Must be extended without losing unrelated JSON fields.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - Stage execution still calls specific evaluator methods and rule ids for Structure, Georeference, and Dimension.
  - This story should move toward catalog-driven stage selection while keeping evaluator code explicit.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
  - Already contains `WorkflowEffect`, but Story 4.9 review says persisted findings do not yet carry the full reportable finding contract per result.

### Required Rule Shape

Use this conceptual shape. The implementation may add compatibility fields, but these concepts must be present:

```json
{
  "rule_id": "dimension_bearing_consistency",
  "display_name": "Bearing consistency",
  "stage_id": "dimension_check",
  "category": "dimension",
  "evaluator_key": "bearing_coordinate_consistency",
  "enabled": true,
  "severity": "warning",
  "workflow_effect": "requires_disposition",
  "locked": false,
  "report_visible": true,
  "applies_to": {
    "transaction_types": ["Plan Examination"],
    "source_roles": ["computation_sheet", "survey_plan_pdf"]
  },
  "parameters": {
    "max_bearing_delta_degrees": 5.0
  }
}
```

Supported severities remain:

```text
warning
blocker
configured
```

Recommended workflow effects:

```text
blocker
requires_disposition
report_only
info
```

Supported stage ids:

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

### Scope Boundary

This story should add rule governance and supported-rule creation. It should not add arbitrary scripting, user-authored expressions, or free-form code execution from Settings.

"Add new rule" means: choose a supported evaluator/template, configure its safe parameters, scope it to stages/source roles/profiles, and save it to the catalog.

### UX Requirements

Settings should remain compact and operational:

- Group rules by stage using clear headings.
- Keep locked/core status obvious.
- Use checkboxes/toggles for enabled state.
- Use controlled dropdowns for severity, workflow effect, stage, and evaluator template.
- Use compact summaries for scope and parameters; keep detailed JSON or advanced fields available where existing Settings already uses JSON editing.
- Do not turn Settings into a large admin console or require users to understand code internals.

### Preservation Rules

- Do not weaken locked/core safety rules.
- Do not let disabled rules appear as passed.
- Do not create geometry, GDB feature classes, map layers, Enterprise features, Innola updates, packages, or final reports from Structure, Georeference, or Dimension check execution.
- Do not bypass review-before-output gates.
- Do not require live ArcGIS Enterprise, live Innola, or live CADINDEX in automated tests.
- Do not store tokens, passwords, API keys, or raw unbounded subprocess output in settings, summaries, or reports.
- Do not remove `PreflightRules.json` compatibility unless a separate migration story explicitly does so.

### References

- `docs/project/COMPUTE_STAGE_STEPS_AND_RULES.md`
- `_bmad-output/implementation-artifacts/4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md`
- `_bmad-output/implementation-artifacts/4-8-split-structure-check-and-dimension-check-into-separate-actions-and-result-summaries.md`
- `_bmad-output/implementation-artifacts/4-7-rename-preflight-rules-to-structure-rules-and-add-configurable-dwg-cad-layer-validation.md`
- `_bmad-output/project-context.md`
- `_bmad-output/planning-artifacts/architecture.md`

## Dev Agent Record

### Agent Model Used

Codex / GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet test src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-restore`

### Completion Notes List

- Added staged rule metadata to `PreflightRuleDefinition`: `stage_id`, `workflow_effect`, `evaluator_key`, and `report_visible`, with supported-value normalizers.
- Added staged defaults/migration inference in `PreflightRuleCatalogLoader` so existing `StructureRules.json`/legacy `PreflightRules.json` entries load into staged rule records when new fields are absent.
- Settings now groups rules by configured stage and displays rule id, evaluator, category/group, scope, workflow effect, locked state, report visibility, enabled state, severity, and description.
- Unlocked rules persist enabled/severity/stage/workflow-effect/report visibility; locked rules preserve protected settings.
- Preflight persisted checks now carry stage id, evaluator key, report visibility, display name, outcome, severity, workflow effect, category, message, correction, evidence, affected path/role, and the surrounding summary continues to carry operator/timestamp/run id.
- Stage dispatch now uses configured `stage_id` guards and includes a regression test proving a moved Dimension rule runs in Georeference Check and not Dimension Check.
- Story 4.9 review findings were incorporated for reportable persisted findings and workflow effect persistence. The broader controlled "add rule from Settings UI" path remains a follow-up gap for Amelia review.
- Verification passed. Solution build has one existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-08-20 | 0.1 | Created story for configurable staged Compute rule catalog, Settings governance, supported-rule creation, and screen/report rule outcome alignment. | Mary / Codex |
| 2026-08-20 | 0.2 | Implemented staged rule metadata, Settings display/persistence, stage-routed execution guards, persisted finding metadata, and focused regression tests. | Amelia / Codex |
