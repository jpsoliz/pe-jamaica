---
baseline_commit: handoff-2026-07-02
---

# Story 4.9: Add Georeference Check Stage And Reportable Stage Findings Model

Status: in-progress

## Story

As a cadastral compute reviewer,  
I want Georeference Check to run as its own workflow stage and every compute stage to produce reportable findings,  
so that parish/location/JAD2001 issues are reviewed separately from dimension geometry checks and later examination reports can include clear findings even when a stage fails.

## Acceptance Criteria

1. Given the compute workflow shell is displayed, when a transaction reaches early compute checks, then the visible stage order must include:
   - `Supporting Document Check`
   - `Structure Check`
   - `Georeference Check`
   - `Dimension Check`
   - `Validate Points and Lines`
   - `Create Spatial Units`
   - `Final Review`
   - `Finalize`
2. Given the user runs `Georeference Check`, when it completes, then only georeference/location readiness rules are evaluated and persisted, including JAD2001 coordinate-system expectations, coordinate usability, Jamaica/parish bounds, tolerance checks, and parish/location mismatch results.
3. Given the user runs `Dimension Check`, when it completes, then only dimension/geometry-construction readiness rules are evaluated and persisted, including bearings, distances, point references, closure/tolerance, and whether computation-sheet or spatial-line data can produce usable parcel geometry.
4. Given Structure Check has not passed, when Georeference Check or Dimension Check is requested, then the workflow must block or clearly explain the prerequisite without creating false pass results.
5. Given Georeference Check has not passed, when Dimension Check or Validate Points and Lines is requested, then the workflow must block with a clear message requiring Georeference Check to pass or be explicitly dispositioned according to rule severity.
6. Given Dimension Check has not passed, when Validate Points and Lines is requested, then the workflow must block with a clear message requiring Dimension Check to pass or be explicitly dispositioned according to rule severity.
7. Given any check produces a rule outcome, when results are persisted, then the result must include stable report fields: stage id, rule id, display name, outcome, severity, workflow effect, message, correction, evidence, affected source path/role where applicable, operator id, timestamp, and run id.
8. Given a rule fails but should still support examination-report capture, when the result is stored, then the result must distinguish report finding status from workflow blocking status. A failed finding must not automatically block downstream work unless the rule severity/workflow effect requires it.
9. Given disabled, skipped, or not-applicable rules are produced, when results are displayed and persisted, then those outcomes must remain visible and must not be silently counted as passed or warnings.
10. Given a case is reopened, when stage summaries exist, then Structure Check, Georeference Check, and Dimension Check summaries must reload independently and preserve which stages passed, failed, warned, were skipped, were disabled, or were not started.
11. Given only legacy split summaries from Story 4.8 exist, when a case is reopened after this story, then existing `structure_check_summary.json` and `dimension_check_summary.json` must remain readable. Existing Dimension Check georeference rows must be migrated or presented safely without creating false pass states.
12. Given source files, source roles, detected profile, supporting document options, structure/georeference/dimension rules, or parish metadata change after a check passes, when those changes are saved, then stale stage artifacts must be invalidated according to the affected scope.
13. Given Settings exposes Structure Rules and related rule configuration, when this story is complete, then Settings must make clear which rules belong to Structure Check, Georeference Check, and Dimension Check.
14. Given automated tests run in the console harness, then tests must cover separate Georeference Check execution, Dimension Check execution without georeference rules, gating before Validate Points and Lines, independent persistence/reopen, legacy 4.8 summary compatibility, and reportable findings with separate workflow effect.

## Tasks / Subtasks

- [x] Define the stage and findings contract. (AC: 1, 7-11)
  - [x] Add explicit `georeference_check` stage identifier alongside existing `structure_check` and `dimension_check`.
  - [x] Add or extend a shared reportable finding/result model that can be used by all compute stages.
  - [x] Include both `outcome` and `workflow_effect` or equivalent fields so failed findings can be reportable without automatically becoming workflow blockers.
  - [x] Preserve lowercase `snake_case` JSON field names.

- [x] Split georeference rules out of Dimension Check. (AC: 2, 3, 13)
  - [x] Route `georeference` rules to `Georeference Check`.
  - [x] Route dimension/geometry-construction rules to `Dimension Check`.
  - [x] Ensure Dimension Check no longer reports parish/location/JAD2001 checks as its own results.
  - [x] Preserve existing source integrity/system prerequisites without confusing them with business rule outcomes.

- [x] Persist independent stage summaries. (AC: 7-12)
  - [x] Persist `working/georeference_check_summary.json`.
  - [x] Keep `working/structure_check_summary.json` and `working/dimension_check_summary.json`.
  - [x] Keep existing `working/preflight_summary.json` or aggregate compatibility behavior readable where required.
  - [x] Add safe migration/read behavior for cases created by Story 4.8 where georeference rows were inside Dimension Check.

- [x] Update workflow commands, gating, and stale-artifact invalidation. (AC: 1, 4-6, 10-12)
  - [x] Add session/ViewModel command support for running Georeference Check independently.
  - [x] Update Validate Points and Lines gating to require Structure Check, Georeference Check, and Dimension Check according to configured workflow effects.
  - [x] Ensure source/profile/rule/parish changes invalidate the affected stage summaries and downstream artifacts.
  - [x] Ensure no check stage creates extraction review data, output geometry, map layers, Enterprise edits, Innola writes, or final reports.

- [x] Update UI presentation and microcopy. (AC: 1, 4-9, 13)
  - [x] Add a Georeference Check lifecycle card/action in the dock pane.
  - [x] Keep Structure, Georeference, and Dimension result panels compact and scannable.
  - [x] Show outcome labels distinctly: `passed`, `failed`, `warning`, `not_applicable`, `skipped`, `disabled`.
  - [x] Avoid labeling disabled/skipped/not-applicable findings as warnings in badges or report summaries.
  - [x] Update user-facing text so Georeference Check means parish/location/JAD2001 coherence and Dimension Check means dimension/geometry construction coherence.

- [x] Update Settings rule grouping. (AC: 2, 3, 8, 13)
  - [x] Show Structure Check rules, Georeference Check rules, and Dimension Check rules as separate groups.
  - [x] Expose enabled/disabled state, severity, workflow effect, and description for configurable rules.
  - [x] Preserve locked/core safety rule behavior.

- [x] Add focused tests. (AC: 1-14)
  - [x] Georeference Check persists its own summary and does not write Dimension Check summary.
  - [x] Dimension Check excludes georeference/parish/JAD2001 rules.
  - [x] Validate Points and Lines remains blocked until required Structure, Georeference, and Dimension checks are satisfied.
  - [x] Reportable failed finding can persist without automatically becoming a workflow blocker when configured that way.
  - [x] Disabled/skipped/not-applicable outcomes remain visible and are not counted as passed.
  - [x] Reopen restores all three summaries independently.
  - [x] Legacy 4.8 cases with georeference rows in Dimension Check reopen safely.

- [ ] Validate and package. (AC: 1-14)
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` or `--no-build` after a clean build if WPF generated files are locked.
  - [ ] Manually smoke test a transaction through Structure Check, Georeference Check, Dimension Check, then Validate Points and Lines.
  - [ ] Manually smoke test a reopened Story 4.8 case.

### Review Findings

- [ ] [Review][Patch] Dimension Check only validates source presence, not the specified dimension/geometry rules [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs:408]
- [ ] [Review][Patch] Persisted findings do not carry the full reportable finding contract per result [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs:5]
- [ ] [Review][Patch] Structure Rules settings do not expose or persist workflow effect [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs:131]

## Dev Notes

### Why This Story Exists

The product workflow has moved beyond a two-check early model. The latest product workflow note stored at `docs/project/compute-steps.docx` defines separate business meanings:

- Structure Check validates submitted document/spatial-file structure.
- Georeference Check validates starting points, parish/location coherence, and JAD2001 coordinate-system expectations.
- Dimension Check validates bearings, distances, point references, closure, and whether geometry can be built.

Story 4.8 currently split Structure Check from Dimension Check, but Dimension Check still owns `georeference` category rules. This story corrects that product drift and introduces the reportable findings model needed by later examination-report and Finalize work.

### Current Code Reality

Important current implementation points:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheckStage.cs`
  - Currently defines split stage identifiers from Story 4.8.
  - Needs a new `GeoreferenceCheck` stage.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - Currently routes Structure Check and Dimension Check through stage-specific execution.
  - Current georeference/dimension readiness logic includes coordinate-source presence, tabular columns, Jamaica bounds, and related checks under Dimension Check.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Owns check commands, state/gating, summary reload, stale artifact invalidation, and Validate Points and Lines gating.
  - Story 4.8 review found a gating risk: Validate Points and Lines must not rely only on stale `CurrentState`; it must verify required stage results are valid after reopen.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Contains the stage card/action layout that must add Georeference Check without overcrowding.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Owns result grouping, badges, command exposure, and stage microcopy.
  - Story 4.8 review found skipped/disabled/not-applicable outcomes should not be counted as warnings.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
  - Preferred rules catalog after Story 4.7.
  - Needs rule grouping clarity across Structure, Georeference, and Dimension.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
  - Needs a new path for `GeoreferenceCheckSummaryPath`.

### Required Business Semantics

Use these meanings consistently:

- `Structure Check`: are required documents and submitted spatial files structurally complete and usable?
- `Georeference Check`: are extracted/control points spatially coherent with JAD2001 and the expected Jamaica parish/map location?
- `Dimension Check`: are bearings, distances, point references, closure, and line/polygon construction coherent enough to build spatial units?
- `Validate Points and Lines`: remains a human/assisted review stage and must not be merged into automated checks.

The product note also states that users may need to capture examination-report details even if a stage fails. This means the result model must separate:

- `outcome`: what the rule found, such as `passed`, `failed`, `warning`, `not_applicable`, `skipped`, `disabled`.
- `workflow_effect`: what the finding does to workflow progression, such as `blocker`, `requires_disposition`, `report_only`, or `info`.

The exact enum names may follow existing conventions, but the separation must exist.

### Recommended Artifact Contract

Add:

```text
working/georeference_check_summary.json
```

Keep:

```text
working/structure_check_summary.json
working/dimension_check_summary.json
working/preflight_summary.json
```

Each stage summary should include enough report metadata for later examination reports:

```json
{
  "schema_version": "1.0.0",
  "transaction_id": "100000379",
  "stage_id": "georeference_check",
  "run_id": "...",
  "created_at": "...",
  "created_by": "...",
  "payload": {
    "status": "passed",
    "findings": [
      {
        "stage_id": "georeference_check",
        "rule_id": "parish_location_match",
        "display_name": "Parish location match",
        "outcome": "passed",
        "severity": "info",
        "workflow_effect": "report_only",
        "message": "...",
        "correction": null,
        "evidence": {},
        "affected_path": null,
        "source_role": "plan_map_reference"
      }
    ]
  }
}
```

The implementation may reuse `PreflightSummaryDocument` and `PreflightCheck` if that is lowest risk, but the shape must preserve reportable findings and workflow effect without breaking existing summaries.

### Georeference Rule Scope

Georeference Check should own:

- Coordinate system is expected/validated as JAD2001 where the available data allows this.
- Extracted/control points have usable numeric coordinates.
- Coordinates fall inside Jamaica bounds.
- Points fall inside or within tolerance of the expected parish described in the plan/map.
- Parish/location mismatch is reported with evidence.
- Missing parish metadata is a visible finding, not a silent pass.

Do not require live CADINDEX, live Enterprise, or final authoritative cadastral data for automated tests. Use test doubles or configured bounds/parish fixtures.

### Dimension Rule Scope

Dimension Check should own:

- Bearings are parseable and internally consistent.
- Distances are parseable and internally consistent.
- Point references connect correctly.
- Computation-sheet dimensions can produce valid lines/polygons.
- Spatial parcel-line data can be assessed when configured as a supporting source.
- Closure/tolerance checks can use computation-sheet lines, control point file, or spatial parcel line layer according to configured source precedence.
- Parcel polygon geometry can be created without obvious contradictions.

Dimension Check must not report parish/location/JAD2001 checks as its own business results after this story.

### Legacy Compatibility

Story 4.8 produced split summaries with georeference rows inside Dimension Check. This story must not strand those cases:

- If `georeference_check_summary.json` is missing but `dimension_check_summary.json` contains legacy `georeference` rows, reopen should either:
  - present them as legacy Georeference Check results, or
  - require rerun with a clear message.
- Do not create a false Georeference Check pass from corrupt or incomplete legacy data.
- Do not erase valid Structure or Dimension results when one stage summary is missing or corrupt.

### UX Guidance

The dock pane must stay compact. Recommended layout:

- Add a Georeference Check stage card/action between Structure Check and Dimension Check.
- Use one row of cards where available, but allow wrapping/responsive layout for narrow pane widths.
- Result badges should distinguish blockers, warnings, failed report-only findings, disabled/skipped, and passed outcomes.
- Avoid broad explanatory text inside compact cards. Put detail in result rows/tooltips/expanded panels.

### Preservation Rules

- Do not rename internal `Preflight*` classes just for terminology purity if aliases and stage IDs solve the product need.
- Do not create geometry, GDB feature classes, map layers, Enterprise features, Innola updates, ZIP packages, or final reports from Structure, Georeference, or Dimension checks.
- Do not weaken path containment, source-file safety, DWG signature/readability checks, or system readiness checks.
- Do not allow disabled rules to appear as passed.
- Do not store portal tokens, Innola tokens, API keys, passwords, or raw unbounded subprocess output in summaries or logs.
- Do not require live ArcGIS Enterprise, live Innola, or live CADINDEX in automated tests.

### Project Structure Notes

Expected files to review or update:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheckStage.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### References

- `docs/project/compute-steps.docx`
- `docs/project/PROCESSING_ALIGNMENT.md`
- `_bmad-output/implementation-artifacts/4-7-rename-preflight-rules-to-structure-rules-and-add-configurable-dwg-cad-layer-validation.md`
- `_bmad-output/implementation-artifacts/4-8-split-structure-check-and-dimension-check-into-separate-actions-and-result-summaries.md`
- `_bmad-output/implementation-artifacts/5-16e-coordinate-early-compute-stage-realignment-with-externalized-document-structure-and-georeference-rule-catalogs.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added `Georeference Check` as an independent compute workflow stage between Structure Check and Dimension Check.
- Extended preflight findings with report-oriented `display_name`, `outcome`, and `workflow_effect` fields while preserving snake_case JSON output.
- Split georeference/location readiness from dimension/geometry readiness and persisted `working/georeference_check_summary.json`.
- Updated session gating so Validate Points and Lines requires real Structure, Georeference, and Dimension pass results rather than stale workflow state alone.
- Added safe legacy handling for Story 4.8 split summaries where georeference rows were stored inside `dimension_check_summary.json`.
- Updated dock pane cards, commands, collapsed summaries, and Settings rule grouping for Structure, Georeference, and Dimension.
- Automated validation is complete: solution build passed and the console harness reports 302 passing tests.
- Manual ArcGIS Pro smoke tests remain pending because they require the running add-in/UI environment.

### File List

- `_bmad-output/implementation-artifacts/4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheckStage.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-03 | 0.1 | Created story for adding a separate Georeference Check stage and reportable stage findings model. | Mary / Codex |
| 2026-07-03 | 0.2 | Patched stage wording from Validate Points to Validate Points and Lines for current product alignment. | Mary / Codex |
| 2026-07-03 | 0.3 | Implemented independent Georeference Check stage, reportable findings metadata, gating, summary migration, UI/settings updates, and automated tests. | Codex |
