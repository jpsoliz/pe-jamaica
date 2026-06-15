---
baseline_commit: handoff-2026-06-14
---

# Story 4.5: Externalize Configurable Preflight Rules And Expose Them In Configuration Panel

Status: review

## Story

As a cadastral technical staff lead or configuration administrator,  
I want configurable preflight rules to be externalized from hardcoded logic and visible in the Configuration panel,  
so that we can tune operational readiness checks without recompiling the add-in while keeping core safety rules protected.

## Acceptance Criteria

1. Given the Parcel Workflow add-in loads local configuration, when preflight rule settings are resolved, then configurable preflight rule metadata is loaded from a versioned external rules file rather than being embedded only in hardcoded C# logic.
2. Given preflight rule settings are externalized, when the add-in builds the Preflight run, then core safety rules remain enforced and cannot be disabled from configuration.
3. Given configurable operational rules exist, when the rules file marks one as disabled, then Preflight skips that rule and records that it was disabled rather than silently pretending it passed.
4. Given configurable operational rules exist, when the rules file marks one as enabled with a severity policy, then Preflight applies that rule using the configured enabled state and configured severity behavior where supported.
5. Given the Configuration panel is opened, when the user reviews local settings, then the panel shows a `Preflight Rules` section with each configurable rule’s name, category, enabled/disabled status, severity, and short description.
6. Given a rule is classified as core safety, when it is shown in the Configuration panel, then it is visible but clearly marked as locked/non-editable.
7. Given no preflight rules file exists, is corrupt, or is partially invalid, when the add-in loads configuration, then the add-in falls back deterministically to safe defaults and shows a clear configuration warning rather than failing the add-in.
8. Given the rules file changes, when ArcGIS Pro is restarted, then the Configuration panel and Preflight engine reflect the updated rule settings without requiring recompilation.
9. Given this story is complete, then at minimum the following rule families are represented in the externalized model: Python package checks, unknown ArcGIS Pro version behavior, optional DWG readiness probe behavior, and any future operational preflight rules that do not weaken core path/file/workspace safety.
10. Given this story is complete, then focused tests cover default loading, missing/corrupt file fallback, enabled/disabled application, locked core-rule visibility, and Configuration panel rendering of the externalized rule set.

## Tasks / Subtasks

- [x] Define the external preflight rule contract. (AC: 1-4, 7-9)
  - [x] Create a versioned `PreflightRules.json` contract under the add-in settings area.
  - [x] Define fields for `rule_id`, `category`, `display_name`, `description`, `enabled`, `severity`, `locked`, and any rule-specific configuration payload needed for current operational rules.
  - [x] Keep file and field naming in lowercase `snake_case` where the project conventions require it.

- [x] Separate core safety rules from configurable operational rules. (AC: 2-4, 6, 9)
  - [x] Identify which current Preflight checks must remain hard-enforced and non-disableable.
  - [x] Identify which current Preflight checks can be safely driven by configuration.
  - [x] Ensure configurable rules cannot disable path containment, file existence/readability, required source-role presence, script-plan freshness, or writable case-folder protections.

- [x] Add a preflight rule settings loader with safe fallback behavior. (AC: 1, 3-4, 7-8)
  - [x] Load externalized preflight rule settings from disk at add-in startup/configuration read time.
  - [x] Apply deterministic defaults when the rules file is missing or invalid.
  - [x] Surface a non-crashing configuration warning when fallback defaults are used.

- [x] Apply configurable rules in the Preflight engine. (AC: 1-4, 9-10)
  - [x] Update `ProcessingEnvironmentPreflightService` to use externalized enabled/severity settings for operational checks it currently owns.
  - [x] Where appropriate, update `ManifestPreflightService` to consult externalized operational rule toggles without weakening core validation behavior.
  - [x] Record disabled-rule status clearly in the preflight results or diagnostics so users know a rule was intentionally skipped.

- [x] Expose preflight rules in the Configuration panel. (AC: 5-8, 10)
  - [x] Extend the current Configuration window to show a `Preflight Rules` section below the existing settings summary.
  - [x] Display configurable rules with their category, enabled state, severity, and description.
  - [x] Display locked/core rules as visible but read-only.
  - [x] Keep this story read-focused unless a lightweight local edit flow is already natural; if direct editing is not implemented, clearly indicate that changes are made in the rules/settings file and reloaded on restart.

- [x] Seed the first configurable rule set. (AC: 4, 8-9)
  - [x] Externalize Python package checks (`required_python_packages`, `optional_python_packages`, `arcpy_required`) into a visible configuration-backed rule model.
  - [x] Externalize unknown ArcGIS Pro version warning vs blocker behavior.
  - [x] Externalize optional DWG readiness probe behavior where safe to do so.

- [x] Add focused tests and validation. (AC: 7-10)
  - [x] Test loading default preflight rule settings.
  - [x] Test missing/corrupt rules file fallback behavior.
  - [x] Test enabled vs disabled operational rule application.
  - [x] Test locked core-rule visibility in the Configuration panel model.
  - [x] Test that core safety rules remain enforced even when configuration attempts to disable them.

## Dev Notes

### Why This Story Exists

- The current Preflight implementation mixes two kinds of checks:
  - hardcoded manifest/business safety checks
  - operational/environment readiness checks that are good candidates for configuration
- Users and admins need visibility into which operational readiness checks are active without recompiling the add-in.
- The Configuration panel already surfaces local settings, but it does not yet explain or summarize the active preflight rule posture.

### Current Code Reality

There are two main preflight engines today:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - owns source-role presence, copied-file safety, supported extension checks, script-plan freshness, path containment, and DWG input validation wiring
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs`
  - owns ArcGIS Pro lane/version compatibility, workspace write access, Python executable readiness, Python package checks, and timeout/error handling

Current local configuration surfaces:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`

At present:

- some operational behavior is already configurable indirectly through settings
- preflight rules themselves are not represented as a first-class externally visible rule catalog
- the Configuration panel shows only a compact settings summary, not rule-by-rule preflight state

### Recommended Rule Split

Treat rules in two tiers:

1. **Core safety rules** — always on, locked
   - path containment
   - copied file exists/readable
   - supported extensions
   - required source roles by detected scenario
   - case-folder writable/readable locations
   - script-plan freshness

2. **Operational/configurable rules** — externalized and visible
   - Python package presence checks
   - ArcPy-required behavior
   - unknown ArcGIS version warning vs blocker
   - optional DWG readiness probe execution
   - future optional environment or advisory checks

Do not allow configuration to weaken the first tier.

### Configuration UX Guidance

The current Configuration window is summary-oriented and compact. Keep that feel.

Recommended presentation:

- new section title: `Preflight Rules`
- each row should show:
  - rule name
  - category
  - enabled/disabled
  - severity
  - short description
  - locked status where applicable

If direct editing is not implemented in this story, that is acceptable. In that case:

- show the rules clearly
- show the active rules file path
- keep the existing “edit file then restart ArcGIS Pro” pattern

This story should not turn the configuration dialog into a complex admin console unless the implementation remains small and consistent with the existing WPF window.

### Suggested Technical Direction

Use a dedicated rules/settings file rather than overloading unrelated workflow settings keys.

Suggested shape:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `PreflightRuleDefinition` / `PreflightRuleSettings` model in C#
- `PreflightRuleCatalogLoader` service with safe fallback defaults

Prefer:

- hardcoded defaults in C# for resilience
- external file override for operational rules
- explicit “locked” metadata in the rule catalog so the UI and engine agree

### Preservation Rules

- Do not regress current Preflight summary structure or existing blocker/warning/passed result grouping.
- Do not break Story 2.6 environment readiness behavior while externalizing it.
- Do not let config disable source containment, file existence, or writable-folder protections.
- Do not require live editing of rules from the UI in this first pass if file-backed restart-based configuration is simpler and safer.

### Testing Guidance

Prefer focused tests around:

- rule catalog load/fallback
- rule enable/disable behavior in `ProcessingEnvironmentPreflightService`
- locked core rule enforcement
- configuration-window view model or code-behind rendering of rule rows
- graceful behavior when config is missing or malformed

### Story Positioning Note

This story is a corrected-course replacement for the previous backlog item occupying `4.5` in sprint tracking. The sprint record should reflect this story as the current `4.5` source of truth for Epic 4 planning.

### References

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/implementation-artifacts/2-1-run-manifest-preflight.md`
- `_bmad-output/implementation-artifacts/2-6-validate-arcgis-pro-and-python-processing-environment.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Completion Notes List

- Added a versioned `Settings/PreflightRules.json` contract plus a `PreflightRuleCatalogLoader` that resolves file-backed rule overrides and falls back deterministically to safe defaults with a visible warning.
- Wired configurable operational rule behavior into `ProcessingEnvironmentPreflightService` and `ManifestPreflightService`, including explicit `disabled` result records for skipped package-probe and DWG readiness rules.
- Extended the Configuration window to show the active rules file path, fallback warning state, and a read-only `Preflight Rules` catalog with locked/core visibility.
- Validated the change with `dotnet build` on the add-in project and a full `dotnet run` pass of the local test harness (`187` passing tests).
- Tightened the loader so the external rules file is now the authoritative catalog shape, while C# defaults are used only as deterministic safety fallback.
- Added explicit partial-invalid validation warnings for missing metadata, missing required rules, duplicate rule IDs, bad severities, and attempts to weaken locked rules.
- Revalidated the tightened implementation with `dotnet build` and the full test harness (`189` passing tests).

### File List

- `_bmad-output/implementation-artifacts/4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleDefinition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ProcessingEnvironmentPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-14 | 0.1 | Created corrected-course Story 4.5 for externalizing configurable preflight rules and surfacing them in the Configuration panel. | Codex |
| 2026-06-14 | 0.2 | Implemented external preflight rule catalog loading, configurable operational rule application, Configuration window visibility, and focused test coverage. | Codex |
| 2026-06-14 | 0.3 | Tightened the rule catalog contract so the external file is authoritative and partially invalid catalogs now trigger safe fallback warnings. | Codex |
