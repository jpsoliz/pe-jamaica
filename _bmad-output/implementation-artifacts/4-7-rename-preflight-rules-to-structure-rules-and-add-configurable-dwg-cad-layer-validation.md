---
baseline_commit: handoff-2026-07-02
---

# Story 4.7: Rename Preflight Rules To Structure Rules And Add Configurable DWG CAD Layer Validation

Status: in-progress

## Story

As a cadastral workflow administrator and compute reviewer,  
I want the Structure Check stage to be driven by visible, configurable Structure Rules, including DWG CAD layer requirements,  
so that mandatory document structure and DWG contents can be validated consistently and reported clearly before downstream dimension review, point validation, and spatial unit creation.

## Acceptance Criteria

1. Given the workflow UI and Settings workspace currently expose `Preflight Rules`, when this story is complete, then user-facing labels must use `Structure Rules` or `Structure Check Rules` for document/source structure validation.
2. Given existing installations may still contain `PreflightRules.json`, when the add-in loads structure rules, then it must preserve backward compatibility by loading the existing file as an alias/fallback while introducing `StructureRules.json` as the preferred rules file.
3. Given the Settings workspace is opened, when the user reviews rule configuration, then structure/document rules must be organized separately from system/environment checks.
4. Given a DWG source is present and DWG layer validation rules are enabled, when Structure Check runs, then the add-in must inspect the DWG and report whether required point, line/polyline, and annotation/text CAD layer categories are present.
5. Given required DWG CAD layer names are configured, when the DWG probe discovers CAD layers, then matching must be configurable by rule rather than hardcoded in C# or Python.
6. Given an expected DWG layer category is missing, when Structure Check completes, then the outcome must be `failed`/blocker or warning according to rule severity and must include correction guidance.
7. Given an expected DWG layer category is found, when Structure Check completes, then the outcome must be recorded as `passed` with the matched layer evidence.
8. Given no DWG is required for the workflow profile, when Structure Check runs, then DWG layer validation must report `not_applicable` or remain absent without blocking the transaction.
9. Given a configurable structure rule is disabled, when Structure Check runs, then the result must record `skipped`/disabled rather than silently pretending the rule passed.
10. Given rule outcomes are produced, when summaries and UI are rendered, then results must be available for reporting using stable rule IDs, display names, severity, outcome, message, source role/path, and discovered evidence.
11. Given this story is complete, then existing Structure Check behavior must remain compatible: source integrity, required role validation, workflow rule resolution, DWG signature validation, DWG readability probe, and safe fallback behavior must not regress.

## Tasks / Subtasks

- [x] Introduce the Structure Rules naming contract. (AC: 1, 2, 11)
  - [x] Add `Settings/StructureRules.json` as the preferred file name.
  - [x] Preserve `Settings/PreflightRules.json` as a backward-compatible alias/fallback.
  - [x] Update user-facing labels in Settings and workflow summaries from `Preflight Rules` to `Structure Rules` where the rules apply to Structure Check.
  - [x] Avoid broad class/file renames unless they are low risk; compatibility and product language matter more than churn.

- [x] Reorganize rule categories in Settings. (AC: 3, 9, 10)
  - [x] Display document/source structure rules under `Structure Rules`.
  - [x] Display ArcGIS/Python/workspace readiness checks separately as `System Checks` or equivalent.
  - [x] Preserve locked/core rule visibility and existing enabled/severity edit behavior.
  - [x] Keep Settings UI compact and consistent with the existing Settings workspace.

- [x] Extend the rule definition model for configurable DWG CAD layer categories. (AC: 4, 5, 6, 7, 8, 9)
  - [x] Add a rule such as `dwg_required_cad_layers`.
  - [x] Support configured CAD layer categories, initially:
    - [x] points
    - [x] lines / polylines
    - [x] annotation / text
  - [x] Allow each category to define accepted layer name aliases.
  - [x] Allow category-level severity or rule-level severity, using existing severity behavior where possible.
  - [x] Keep file naming and JSON properties in lowercase `snake_case`.

- [x] Enhance DWG probing to return discovered CAD layer evidence. (AC: 4, 5, 7, 10, 11)
  - [x] Extend `ArcPyDwgReferenceReadinessInspector` or adjacent service to enumerate CAD layer names where ArcPy exposes them.
  - [x] Continue proving DWG readability and sublayer existence as today.
  - [x] Do not import CAD into a GDB during Structure Check.
  - [x] Do not create output geometry, feature classes, annotations, Enterprise edits, or downstream artifacts during Structure Check.
  - [x] Sanitize probe failures and avoid logging raw subprocess output beyond existing safe patterns.

- [x] Apply required-layer validation in Structure Check. (AC: 4-10)
  - [x] Compare discovered DWG layer names against configured category aliases.
  - [x] Produce one stable result per configured category.
  - [x] Use outcomes: `passed`, `failed`, `warning`, `not_applicable`, `skipped`.
  - [x] Include matched layer names and discovered layer lists in bounded evidence fields for reporting.
  - [x] Preserve existing `PreflightCheck` / summary compatibility, adding backward-compatible detail fields only if needed.

- [x] Add reporting/UI visibility for rule outcomes. (AC: 6-10)
  - [x] Ensure Structure Check result rows clearly identify each DWG layer rule outcome.
  - [x] Ensure disabled/skipped rules are visible.
  - [x] Ensure missing required layers include clear correction text.
  - [x] Ensure the persisted summary can be used later for audit/reporting without reading logs.

- [x] Add focused tests. (AC: 1-11)
  - [x] Existing `PreflightRules.json` still loads as a compatibility fallback.
  - [x] Preferred `StructureRules.json` loads when present.
  - [x] Settings displays structure rules separately from system checks.
  - [x] DWG with matching point/line/annotation CAD layers passes configured required-layer checks.
  - [x] DWG missing a required CAD layer produces the configured blocker/warning.
  - [x] Disabled DWG layer rule records skipped/disabled outcome.
  - [x] Scenario without DWG requirement does not block on DWG layer rules.
  - [x] Existing DWG signature/readability tests continue to pass.

- [ ] Validate and package. (AC: 1-11)
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` or `--no-build` after a clean build if WPF generated files are locked.
  - [ ] Manually smoke test Settings: confirm labels show Structure Rules and system checks remain understandable.
  - [ ] Manually smoke test a Scenario B transaction with DWG: confirm required CAD layer outcomes appear in Structure Check.

## Dev Notes

### Why This Story Exists

The current workflow language has evolved. What users see as `Structure Check` still relies on an older `Preflight Rules` concept. That was acceptable while the checks were broad readiness gates, but the stage now needs to own mandatory document structure validation, especially DWG internals.

The user explicitly wants:

- Structure Check based on rules.
- Preflight Rules renamed/reframed as Structure Rules where they govern Structure Check.
- Settings reorganized so these rules are understandable.
- DWG layer checks for point, line, and annotation/text content.
- Passed/failed outcomes persisted for reporting.
- Rules enable/disable behavior preserved.

### Current Code Reality

Existing rule and DWG readiness implementation:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
  - Current external rule catalog.
  - Includes `dwg_signature_check` and `dwg_readiness_probe`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
  - Loads `PreflightRules.json`.
  - Provides safe defaults and fallback warnings.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
  - Current rule model includes `rule_id`, `group`, `category`, `display_name`, `description`, `enabled`, `severity`, `locked`, source/file filters, and several rule-specific booleans.
  - Does not currently include configurable CAD layer category aliases.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - Owns source/manifest checks and DWG validation inside Structure Check behavior.
  - Currently emits DWG checks for source present, non-empty/readable, signature, and sublayer readiness.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ArcPyDwgReferenceReadinessInspector.cs`
  - Runs a bounded ArcPy probe.
  - Currently only proves `Describe(path).children` exists.
  - Does not return named CAD layer evidence.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
  - Loads/saves Settings workspace fields, including rule rows from the catalog.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml(.cs)`
  - Contains current Settings UI labels and rule display behavior.

### Existing Related Stories

- Story 2.8, `2-8-validate-dwg-readiness-when-present.md`
  - Established DWG readiness as preflight/structure verification only.
  - Explicitly required DWG inspection not to create extraction, GDB, or output artifacts.
  - Suggested layer availability check IDs but implementation currently stops at readable sublayers.
- Story 4.5, `4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md`
  - Externalized `PreflightRules.json`.
  - Added visible rule catalog in Settings.
  - Preserved locked/core rule protections.

This story is an extension/refinement of both: it should not rewrite the system from scratch.

### Recommended Rule Contract

Add a new preferred file:

```text
src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json
```

Keep this as fallback/alias:

```text
src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json
```

Suggested rule entry:

```json
{
  "rule_id": "dwg_required_cad_layers",
  "group": "structure",
  "category": "dwg",
  "display_name": "Required DWG CAD layers",
  "description": "Validates that DWG sources include expected CAD layer categories for points, lines, and annotation.",
  "enabled": true,
  "severity": "blocker",
  "locked": false,
  "source_roles": ["dwg_source"],
  "file_types": [".dwg"],
  "required_cad_layers": {
    "points": ["POINTS", "SURVEY_POINTS", "PNT"],
    "lines": ["LINES", "BOUNDARY", "LINEWORK"],
    "annotation": ["TEXT", "ANNOTATION", "ANNO"]
  }
}
```

The implementation may choose a more strongly typed JSON shape if easier, but must preserve the user intent: configurable accepted layer names by category.

### Result / Reporting Contract

Each rule outcome should be stable and reportable. Recommended outcome values:

```text
passed
failed
warning
not_applicable
skipped
```

Each persisted result should be able to communicate:

- `rule_id`
- `display_name`
- `category`
- `severity`
- `outcome`
- `message`
- `affected_path`
- `source_role`
- `correction`
- bounded `evidence`, such as:
  - discovered CAD layer names
  - matched layer names
  - missing categories

Prefer extending existing `PreflightCheck` only in a backward-compatible way. If adding an `Outcome` or `Evidence` field is too disruptive, encode outcome through existing blocker/warning/passed helpers and add bounded evidence in message/correction. The long-term preferred shape is structured outcome/evidence.

### DWG Probe Guidance

The current ArcPy probe:

```python
desc = arcpy.Describe(path)
children = list(getattr(desc, "children", None) or [])
```

This can be extended to inspect child names and, where available, child type/shape information. The implementation should account for ArcPy CAD behavior varying by DWG and environment.

Potential evidence fields:

- child dataset names from `desc.children`
- CAD feature class names such as Point, Polyline, Polygon, Annotation if exposed
- layer field values if safely enumerable without importing the DWG

If ArcPy cannot expose exact CAD layer names without importing, the dev agent should document that limitation and still implement the best available check using child feature class names/types. Do not silently claim full CAD layer-name validation if only feature class type validation is possible.

### Settings UX Guidance

Settings should be understandable to non-developer admins:

- Rename tab/section label from `Preflight Rules` to `Structure Rules` for document/source rules.
- Separate `System Checks` from `Structure Rules`.
- Show rule name, enabled state, severity, locked state, and description.
- For DWG CAD layer rule, show the configured layer aliases in a readable form.
- Keep this within the existing Settings workspace style; no complex admin console is required.

### Preservation Rules

- Do not break existing `PreflightRules.json` deployments.
- Do not let configuration disable locked/core safety rules.
- Do not weaken path containment, file existence/readability, supported extension, or required source-role checks.
- Do not move system environment checks into the Structure Check outcome unless they already participate there.
- Do not run extraction, validation, output generation, map loading, Enterprise publish, or Innola writeback from Structure Check.
- Do not require live ArcGIS Enterprise, live Innola, or live CADINDEX in automated tests.
- Do not log secrets, tokens, raw command lines with credentials, or unbounded ArcPy output.

### Testing Notes

Follow the existing console harness style under:

```text
src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests
```

Likely test files:

- `Preflight/PreflightRuleCatalogLoaderTests.cs`
- `Preflight/ManifestPreflightServiceTests.cs`
- `Settings/SettingsWorkspaceServiceTests.cs`
- `Preflight/ProcessingEnvironmentPreflightServiceTests.cs` only if system-check grouping is touched
- `Program.cs` to register new tests

Use fake DWG readiness/probe services for most C# tests. Avoid requiring real ArcPy except for manual smoke testing.

### Open Implementation Questions For Dev

1. Can ArcPy enumerate true CAD layer names directly from the DWG without importing to a geodatabase in the deployed environment?
2. If true CAD layer names are unavailable, should the first implementation validate CAD feature class categories only, then record a warning that exact layer names could not be enumerated?
3. Should `StructureRules.json` fully replace `PreflightRules.json` in source control immediately, or should both files be shipped for one transition release?

Recommended answer for #3: ship both for one transition release or load preferred `StructureRules.json` with `PreflightRules.json` fallback, while Settings writes the preferred file going forward.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` - passed
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 290 tests

### Completion Notes List

- Implemented preferred `StructureRules.json` catalog loading with legacy `PreflightRules.json` fallback.
- Added configurable `dwg_required_cad_layers` aliases for points, lines/polylines, and annotation/text.
- Extended DWG readiness probing to persist bounded discovered CAD layer evidence where ArcPy exposes names/types.
- Added per-category Structure Check results with stable check IDs, outcome values, correction guidance, and structured evidence.
- Updated Settings labels to `Structure Rules` and exposed `System Checks` grouping plus CAD alias summaries.
- Manual ArcGIS Pro smoke checks remain open because they require an interactive Pro session.

### File List

- `_bmad-output/implementation-artifacts/4-7-rename-preflight-rules-to-structure-rules-and-add-configurable-dwg-cad-layer-validation.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ArcPyDwgReferenceReadinessInspector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/DwgReferenceReadinessInspector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-02 | 0.1 | Created story for renaming Preflight Rules to Structure Rules and adding configurable DWG CAD layer validation. | Mary / Winston / Codex |
| 2026-07-02 | 0.2 | Implemented Structure Rules catalog compatibility, configurable DWG CAD layer validation, Settings labeling/grouping, and automated tests. | Codex |
