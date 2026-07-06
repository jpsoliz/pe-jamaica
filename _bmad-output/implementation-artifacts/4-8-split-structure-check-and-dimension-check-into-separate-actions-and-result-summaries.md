---
baseline_commit: handoff-2026-07-02
---

# Story 4.8: Split Structure Check And Dimension Check Into Separate Actions And Result Summaries

Status: in-progress

## Story

As a cadastral compute reviewer,  
I want Structure Check and Dimension Check to run as separate workflow actions with separate result summaries,  
so that document/source structure issues can be reviewed independently from dimension readiness before Validate Points begins.

## Acceptance Criteria

1. Given a transaction is ready for early compute checks, when the workflow panel is shown, then `Structure Check` and `Dimension Check` must each have their own action/run affordance instead of one combined run path.
2. Given the user runs `Structure Check`, when it completes, then only document/source structure rules are evaluated and persisted, including mandatory source/document presence, source integrity, workflow rule/script plan resolution, DWG signature/readability, and configured DWG CAD layer rules.
3. Given the user runs `Dimension Check`, when it completes, then dimension/geometry-construction readiness rules are evaluated separately, including bearings, distances, point references, closure/tolerance, and any future dimension-specific rules. Coordinate-source presence, Jamaica/parish bounds, JAD2001, and parish/location mismatch checks are transitional 4.8 behavior and must move to `Georeference Check` under Story 4.9.
4. Given Structure Check has blockers, when Dimension Check has not run or would otherwise be eligible, then downstream `Validate Points` must remain blocked and the UI must explain that Structure Check must pass first.
5. Given Structure Check passes and Dimension Check has not run, when the user attempts to start `Validate Points`, then the workflow must block with a message requiring Dimension Check to pass.
6. Given both Structure Check and Dimension Check pass, when the user starts `Validate Points`, then the existing downstream behavior remains eligible and no extraction/output geometry is produced by either check stage.
7. Given either check produces rule outcomes, when the stage result is rendered, then the UI must show rule-level outcome rows with stable IDs and statuses: `passed`, `failed`, `warning`, `not_applicable`, `skipped`, and `disabled`.
8. Given a rule is disabled, when its stage runs, then the result must show `disabled` or `skipped` visibly and must not silently count as passed.
9. Given a rule does not apply to the transaction/source profile, when its stage runs, then the result must be `not_applicable` or absent by explicit design, without blocking the transaction.
10. Given results are persisted, when a case is reopened, then Structure Check and Dimension Check summaries must reload independently and the workflow must preserve which stage passed, blocked, or has not started.
11. Given existing cases may already have `preflight_summary.json`, when reopened after this change, then legacy combined summaries must remain readable and must map safely into the new split-stage model without data loss or false pass states.
12. Given source files, source roles, detected profile, supporting document options, or structure/dimension rules change after either check passes, when those changes are saved, then stale Structure Check and Dimension Check artifacts must be invalidated according to the affected scope.
13. Given the Settings workspace now exposes `Structure Rules`, when the rules are split by stage, then Settings must make clear which rules belong to Structure Check versus Dimension Check without reintroducing `Preflight Rules` user-facing wording. Story 4.9 extends this grouping to include `Georeference Check`.
14. Given tests run in the console harness, then automated tests must cover split execution, gating, persistence/reopen, disabled/skipped outcomes, legacy summary compatibility, and no downstream artifact creation during either check.

## Tasks / Subtasks

- [x] Define the split-stage contract. (AC: 1-3, 7-11)
  - [x] Define explicit stage identifiers such as `structure_check` and `dimension_check`.
  - [x] Decide whether summaries are separate files, one envelope with separate payloads, or both for transition compatibility.
  - [x] Preserve backward compatibility with existing `preflight_summary.json`.
  - [x] Document the migration/read behavior for legacy combined summaries.

- [x] Split workflow actions and state/gating. (AC: 1, 4-6, 10-12)
  - [x] Add separate session methods or commands for running Structure Check and Dimension Check.
  - [x] Update workflow state/gating so `Validate Points` requires both checks to pass.
  - [x] Preserve existing recoverability and active-run protection behavior.
  - [x] Ensure reruns and intake/source changes invalidate the correct downstream artifacts.
  - [x] Avoid creating extraction review data, validation artifacts, output layers, map layers, Enterprise edits, or Innola writeback from either check.

- [x] Split rule evaluation by stage. (AC: 2, 3, 7-9, 13)
  - [x] Route `supporting_document`, `structure`, `workflow_rule`, and `dwg` rules to Structure Check.
  - [x] Route `georeference`/dimension rules to Dimension Check.
  - [ ] Story 4.9 follow-up: move `georeference` rules out of Dimension Check into a dedicated Georeference Check stage.
  - [x] Preserve `system` checks without confusing them with Structure Check business results; decide whether they remain prerequisites, a separate system group, or shared preconditions.
  - [x] Keep rules enabled/disabled and severity behavior consistent with Story 4.7.

- [x] Persist independent result summaries. (AC: 7-12)
  - [x] Persist Structure Check outcome rows with blockers, warnings, passed checks, skipped/disabled/not-applicable where applicable.
  - [x] Persist Dimension Check outcome rows independently.
  - [x] Keep stable rule IDs, stage IDs, severity, outcome, message, affected path, source role, correction, and evidence fields.
  - [x] Keep legacy combined `preflight_summary.json` readable for reopen and audit.

- [x] Update UI presentation. (AC: 1, 4-9, 13)
  - [x] Show separate action buttons/run controls for Structure Check and Dimension Check.
  - [x] Show separate stage status badges and result panels.
  - [x] Show rule rows with outcome labels: `passed`, `failed`, `warning`, `not_applicable`, `skipped`, `disabled`.
  - [x] Update microcopy that currently says `Structure Check and Dimension Check are running/passed`.
  - [x] Keep compact ArcGIS Pro dock-pane layout and avoid overcrowding the stage cards.

- [x] Update reopen/recovery behavior. (AC: 10-12)
  - [x] Reopen cases with both new split summaries.
  - [x] Reopen cases with only legacy `preflight_summary.json`.
  - [x] Ensure stale or corrupt one-stage summary does not erase valid results from the other stage.
  - [x] Ensure source/profile changes clear or downgrade both checks when appropriate.

- [x] Add focused tests. (AC: 1-14)
  - [x] Structure Check can pass independently while Dimension Check is not started.
  - [x] Dimension Check cannot unlock Validate Points if Structure Check is blocked.
  - [x] Validate Points is blocked until both checks pass.
  - [x] Structure Check summary persists and reopens independently.
  - [x] Dimension Check summary persists and reopens independently.
  - [x] Legacy `preflight_summary.json` reopens safely.
  - [x] Disabled/skipped/not-applicable outcomes render and persist.
  - [x] Existing DWG signature/readability/CAD layer tests still pass.
  - [x] Existing coordinate/dimension tests still pass.
  - [x] Neither check creates downstream extraction, validation, output, map, Enterprise, or Innola artifacts.

- [ ] Validate and package. (AC: 1-14)
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` or `--no-build` after a clean build if WPF generated files are locked.
  - [ ] Manually smoke test a transaction through Structure Check only, then Dimension Check, then Validate Points.
  - [ ] Manually smoke test a reopened legacy case that only has `preflight_summary.json`.

## Product Alignment Update - 2026-07-03

This story remains the historical implementation split from one combined early-check path into two actions. After review of `docs/project/compute-steps.docx`, the product model now requires three early business checks:

- `Structure Check`: submitted document/spatial-file structure.
- `Georeference Check`: JAD2001, coordinate usability, Jamaica/parish location, and parish/location mismatch.
- `Dimension Check`: bearings, distances, point references, closure/tolerance, and geometry-construction readiness.

Story 4.9 owns the new Georeference Check stage and the shared reportable-finding model. Developers must not treat this Story 4.8 scope as the final home for parish/location/JAD2001 rules.

## Dev Notes

### Why This Story Exists

The product language at the time of this story treated `Structure Check` and `Dimension Check` as distinct reviewer concepts:

- Structure Check answers: are the submitted documents and source files structurally correct and complete?
- Dimension Check answered: are the dimensions, coordinates, and coordinate-supporting sources usable for point validation?

After the 2026-07-03 product workflow update, coordinate/parish/JAD2001 responsibilities move to Story 4.9 `Georeference Check`, while Dimension Check keeps dimension/geometry-construction readiness.

The current code path still executes them together through `WorkflowSession.RunManifestPreflightAsync`, writes one `preflight_summary.json`, and moves the workflow directly to `PreflightPassed` when the combined summary has no blockers. That makes it hard for the operator to understand whether a document/DWG problem or a dimension/coordinate problem is blocking the workflow.

### Current Code Reality

Important current implementation points:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - `RunManifestPreflight` / `RunManifestPreflightAsync` is the combined early-check entrypoint.
  - Current status text says `Structure Check and Dimension Check are running...`.
  - A successful combined run sets `WorkflowState.PreflightPassed`.
  - `RunDraftExtractionInternalAsync` currently blocks with `Run Structure Check and Dimension Check successfully before starting Validate Points.`
  - `InvalidatePreflight` clears `layout.PreflightSummaryPath` and downstream artifacts.
  - `LoadPreflightResults` reloads one `preflight_summary.json`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
  - Current states are generic `PreflightRunning`, `PreflightBlocked`, `PreflightPassed`.
  - The implementation may add new states or keep existing states plus explicit split-stage flags/artifacts. Do not add state churn unless it improves clarity and recovery.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
  - Current `Preflight` workspace covers early checks.
  - `PreflightPassed` currently advances to extraction review.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - Owns current source/manifest, DWG, workflow rule, and georeference/dimension checks.
  - Story 4.7 added Structure Rules and `dwg_required_cad_layers`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs`
  - Current payload has `status`, `blockers`, `warnings`, and `passed_checks`.
  - Story 4.7 added backward-compatible `PreflightCheck.Outcome` and `PreflightCheck.Evidence`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
  - Preferred rules catalog after Story 4.7.
  - Rules already include groups/categories that can be used for stage partitioning.

### Recommended Implementation Shape

Prefer a minimal, recoverable split:

1. Keep existing `preflight_summary.json` readable as a legacy combined summary.
2. Add new persisted artifacts, for example:

```text
working/structure_check_summary.json
working/dimension_check_summary.json
```

3. Add a shared summary model or wrapper that includes:

```json
{
  "schema_version": "1.0.0",
  "transaction_id": "100000379",
  "stage_id": "structure_check",
  "run_id": "...",
  "created_at": "...",
  "created_by": "...",
  "source_manifest_hash": "...",
  "payload": {
    "status": "passed",
    "blockers": [],
    "warnings": [],
    "passed_checks": [],
    "other_checks": []
  }
}
```

The exact field names can reuse `PreflightSummaryDocument` if that is less risky. If a new payload bucket is needed for `skipped`, `disabled`, or `not_applicable`, keep it backward-compatible and avoid breaking old summaries.

4. Split evaluation in code by rule group/category rather than duplicating all checks:

- Structure Check:
  - `supporting_document`
  - `structure`
  - `workflow_rule`
  - `dwg`
  - source integrity/path/readability
  - required source roles/document completeness
- Georeference Check (Story 4.9 follow-up):
  - `georeference`
  - coordinate-source presence
  - tabular coordinate columns
  - JAD2001 expectation/readiness where data allows
  - Jamaica/parish coordinate bounds and parish/location mismatch
- Dimension Check:
  - bearings
  - distances
  - point references
  - closure/tolerance
  - geometry-construction readiness
  - future dimension-specific rules
- System Checks:
  - `system`, `python`, `arcgis_pro`, `write_access`
  - Do not hide them. The implementation should decide whether they run as shared prerequisites before each stage or remain in a compact `System Checks` group. They should not be mixed into Structure Check business outcomes unless clearly labeled.

### UX Guidance

The current stage-focused workspace should remain compact:

- The early workflow section should show two cards/actions:
  - `Structure Check`
  - `Dimension Check`
- Each card should show:
  - not started / running / blocked / passed / warning
  - blocker count
  - warning count
  - last run timestamp if available
- Result details should list rule rows with:
  - display name
  - outcome
  - severity
  - short message
  - correction/evidence where useful

Avoid returning to the older generic `Preflight` wording in user-facing UI. Internal class names may remain `Preflight*` where low risk.

### Gating Rules

Downstream eligibility should be explicit:

- Story 4.8 gate: `Validate Points` requires Structure Check passed and Dimension Check passed.
- Story 4.9 target gate: `Validate Points` requires Structure Check, Georeference Check, and Dimension Check according to configured workflow effects.
- If Structure Check is blocked, Dimension Check may be disabled or allowed only as diagnostic; choose one behavior and test it.
- Recommended first implementation: require Structure Check to pass before Dimension Check can run, because Dimension Check relies on validated source/document structure.
- Any source intake/profile/rules edit after either check passes should invalidate both checks unless the implementation can confidently scope the invalidation.

### Backward Compatibility

Do not strand existing cases:

- If only legacy `preflight_summary.json` exists and it is `passed`, the reopen path may treat both Structure Check and Dimension Check as passed with a legacy indicator, or require rerun with a clear message. Recommended: treat as legacy pass for continuity, but show that split summaries are not yet available.
- If legacy summary is blocked, preserve the blockers and route the case back to early checks.
- Never create a false Dimension Check pass from a corrupt or incomplete legacy file.
- Story 4.9 must also handle Story 4.8 split summaries where georeference rows were stored inside `dimension_check_summary.json`; do not create a false Georeference Check pass from corrupt or incomplete legacy data.

### Related Stories

- Story 4.7: `4-7-rename-preflight-rules-to-structure-rules-and-add-configurable-dwg-cad-layer-validation.md`
  - Adds Structure Rules, DWG CAD layer validation, and outcome/evidence fields.
- Story 4.5: `4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md`
  - Externalized the original rule catalog and Settings behavior.
- Story 2.8: `2-8-validate-dwg-readiness-when-present.md`
  - Established DWG readiness as verification-only.
- Story 5.16 series:
  - Established the current compute-stage vocabulary around Supporting Document Check, Structure Check, Dimension Check, Validate Points, Create Spatial Units, Final Review, and Finalize.

### Preservation Rules

- Do not create geometry, GDB feature classes, output summaries, map layers, Enterprise features, or Innola updates from either check.
- Do not remove the ability to reopen old `preflight_summary.json` cases.
- Do not weaken source containment, safe path checks, required source role checks, DWG signature validation, or system environment readiness checks.
- Do not let disabled rules appear as passed.
- Do not log secrets or raw unbounded subprocess output.
- Do not rename internal `Preflight*` classes just for language purity if aliases and user-facing wording solve the product need.

### Testing Notes

Follow the console harness pattern under:

```text
src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests
```

Likely test files:

- `Workflow/WorkflowSessionTests.cs`
- `Workflow/WorkflowWorkspacePlannerTests.cs`
- `Workflow/WorkflowStateExtensionsTests.cs`
- `Preflight/ManifestPreflightServiceTests.cs`
- `Preflight/PreflightRuleCatalogLoaderTests.cs`
- `Program.cs`

Expected test focus:

- split stage commands
- gating before Validate Points
- independent persisted summaries
- reopen behavior
- legacy combined summary compatibility
- disabled/skipped/not-applicable outcome visibility
- no downstream artifact creation

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` - passed
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` - passed, 297 tests

### Completion Notes List

- Added explicit `structure_check` and `dimension_check` stage identifiers and separate summary artifacts under `working/`.
- Split service/session execution so Structure Check evaluates document/source/DWG/workflow/system readiness, while Dimension Check evaluates georeference/dimension readiness.
- Kept legacy `preflight_summary.json` readable and written as an aggregate compatibility artifact.
- Updated workflow gating so Validate Points requires both Structure Check and Dimension Check to pass.
- Added a separate Dimension Check command/button in the dock pane.
- Manual ArcGIS Pro smoke checks remain open because they require interactive Pro verification.
- Product alignment patch marks georeference-in-Dimension behavior as transitional. Story 4.9 owns extraction of Georeference Check and reportable findings.

### File List

- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheckStage.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-02 | 0.1 | Created story for splitting Structure Check and Dimension Check into separate actions, summaries, and gates. | Mary / Codex |
| 2026-07-02 | 0.2 | Implemented split Structure/Dimension check execution, persistence, UI command split, gating, and automated coverage. | Amelia / Codex |
| 2026-07-03 | 0.3 | Patched story to align with new Georeference Check stage and mark georeference-in-Dimension behavior as transitional for Story 4.9. | Mary / Codex |
