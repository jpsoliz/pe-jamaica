---
baseline_commit: 707971756fad795cc2bd8d45dc38b9892e7d7b93
---

# Story 7.5: Refactor Configuration Into Editable Settings Workspace With Functional Tabs

Status: review

## Story

As a solution administrator or technical lead,  
I want the current Configuration dialog refactored into an editable Settings workspace with functional tabs,  
so that deployment, AI, Innola, preflight, and spatial workspace settings can be reviewed and maintained in one structured place without editing raw JSON for every change.

## Acceptance Criteria

1. Given the add-in currently exposes a read-only `Configuration` dialog, when the new workspace is implemented, then the ribbon command, window title, tooltips, and visible microcopy use `Settings` instead of `Configuration`.
2. Given the Settings workspace is opened, when the user navigates the UI, then it is organized into functional tabs at minimum named `General`, `AI Toolset`, `Innola Integration`, `Preflight Rules`, and `Spatial Workspace`.
3. Given the `General` tab is opened, when the user reviews or edits local execution settings, then the tab surfaces editable folder and toolchain values such as case-folder root, Python executable, adapter paths where appropriate, and other non-secret local runtime settings already carried in `WorkflowSettings.json`.
4. Given the `AI Toolset` tab is opened, when the user reviews extraction and OCR settings, then the tab surfaces editable AI-related settings such as OCR engine, OpenAI enabled state, model, and API-key source while masking secrets and preferring environment-variable or managed-secret references over plaintext display.
5. Given the `Innola Integration` tab is opened, when the user reviews transaction and service settings, then the tab surfaces editable Innola server, mode, process step, supported transaction types, compute workflow stages, client-certificate options, and attachment/upload behavior settings that are already part of the current local configuration contract.
6. Given the `Preflight Rules` tab is opened, when the user reviews rule configuration, then the tab presents the external preflight rule catalog in a focused editing surface where locked safety rules remain visible but non-editable and configurable operational rules expose enabled/disabled state, severity, and description.
7. Given the `Spatial Workspace` tab is opened, when the user reviews spatial-review and downstream sync settings, then the tab surfaces the review workspace mode, Enterprise working-layer targets, output template settings, and a dedicated `GSI Sync Target` section with editable GSI server, user, and password handling fields.
8. Given the `Spatial Workspace` tab contains secret-bearing fields, when GSI credentials or other sensitive values are shown, then passwords are masked in the UI and the workspace clearly distinguishes between direct secret entry, environment-variable reference, and future managed-credential modes if more than one is supported.
9. Given the user edits one or more supported values and saves, when validation passes, then the workspace writes the updated configuration back to the correct local settings source without requiring recompilation and preserves unrelated existing settings.
10. Given edited values are invalid, incomplete, or only partially supported, when the user attempts to save, then the workspace shows clear non-destructive validation messages tied to the relevant tab/field and does not corrupt the last known-good configuration.
11. Given some settings only take effect on restart or on the next loaded transaction, when the user saves those values, then the workspace clearly labels that scope so administrators know whether a restart is required.
12. Given this story is complete, then focused tests cover tab rendering, settings load/save round-trips, secret masking behavior, validation failures, and preservation of existing configuration semantics already used by preflight, workflow execution, and Innola integration services.

## Tasks / Subtasks

- [x] Rename and reframe the current configuration entrypoint as Settings. (AC: 1)
  - [x] Update `Config.daml` captions, tooltips, and any visible command labels from `Configuration` to `Settings`.
  - [x] Update the window title and section copy to match the new Settings language consistently.

- [x] Introduce a tabbed Settings workspace shell. (AC: 2, 11)
  - [x] Replace the current single-scroll summary layout with a compact tabbed WPF layout suitable for ArcGIS Pro.
  - [x] Add tabs for `General`, `AI Toolset`, `Innola Integration`, `Preflight Rules`, and `Spatial Workspace`.
  - [x] Add visible save/cancel/reload behavior and clearly mark values that require restart or next-session reload.

- [x] Add editable General and AI tabs. (AC: 3-4, 9-10, 12)
  - [x] Bind editable non-secret fields from `WorkflowSettings.json` into the Settings UI.
  - [x] Add validation and persistence for local folder/toolchain settings.
  - [x] Surface AI/OCR settings with secret-safe handling for API-key source references.

- [x] Add editable Innola Integration tab. (AC: 5, 9-10, 12)
  - [x] Surface current Innola connectivity, workflow gating, and supported-transaction configuration in editable form.
  - [x] Keep certificate and attachment-upload settings grouped but compact.
  - [x] Preserve existing fallback behavior in `InnolaTransactionSettings.Load()` when values are missing or invalid.

- [x] Refactor Preflight Rules into a dedicated tab with editing affordances. (AC: 6, 9-10, 12)
  - [x] Reuse the external `PreflightRules.json` catalog as the authoritative source.
  - [x] Keep locked/core rules read-only.
  - [x] Allow configurable operational rules to expose editable enable/severity fields where the rule contract already supports it.

- [x] Add Spatial Workspace tab with GSI sync subsection. (AC: 7-8, 11-12)
  - [x] Surface review workspace mode and mode-specific explanations.
  - [x] Group Enterprise working-layer configuration in one subsection.
  - [x] Add a `GSI Sync Target` subsection for `gsi_server`, `gsi_user`, and `gsi_password` handling.
  - [x] Prefer a password source/reference pattern where possible; if direct password entry is supported for local admin convenience, keep it masked and out of routine status surfaces.

- [x] Implement settings persistence and validation flow. (AC: 9-11)
  - [x] Create or extend a settings read/write service so UI edits round-trip to JSON safely.
  - [x] Preserve unrelated settings and comments/formatting where practical, or document deterministic rewrite behavior if full preservation is not feasible.
  - [x] Add per-tab validation summaries plus field-level warnings for missing required values, unsupported combinations, and secret-source misconfiguration.

- [x] Add focused tests and regression coverage. (AC: 12)
  - [x] Test settings load into the tabbed UI/view-model layer.
  - [x] Test save behavior for normal values and secret-source values.
  - [x] Test invalid save attempts do not corrupt the settings file.
  - [x] Test existing loaders (`InnolaTransactionSettings`, `ProcessingEnvironmentSettings`, workflow settings loaders) still interpret persisted values correctly after edits.

## Dev Notes

### Why This Story Exists

- The current `ConfigurationWindow` has grown into a long read-only summary that now mixes local runtime settings, AI choices, Innola integration details, Enterprise review configuration, supported transaction gating, and preflight rule visibility.
- Recent stories already made configuration more central:
  - Story `4.5` surfaced the external preflight rule catalog.
  - Story `7.1A` surfaced review workspace mode and Enterprise targets.
  - Story `2.15` surfaced supported transaction types and compute workflow stages.
- The next practical step is to turn that summary into an administrator-usable Settings workspace with structure, editability, validation, and safe secret handling.

### Current Code Reality

The current settings experience is still primarily read-only and code-behind driven:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`

Current settings are read from more than one seam:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleSettingsLoader.cs`

This story should unify the administrator experience without breaking those existing loader contracts.

### Recommended Tab Organization

Use the following top-level tab grouping:

1. `General`
   - settings file path (read-only)
   - case-folder root
   - ArcGIS Python executable
   - adapter paths and timeout values that are operationally local
   - restart/apply-scope notes

2. `AI Toolset`
   - OCR engine
   - OpenAI enabled
   - OpenAI model
   - API key source / environment variable name
   - any future AI provider toggles

3. `Innola Integration`
   - server URL
   - live/mock mode
   - process step
   - supported transaction types
   - compute workflow stages
   - certificate controls
   - attachment/upload contract options

4. `Preflight Rules`
   - rule catalog path
   - rule rows with category, enabled, severity, locked state, description
   - warnings for invalid or fallback rule catalog state

5. `Spatial Workspace`
   - review workspace mode
   - output template project / template GDB references
   - Enterprise working review settings
   - `GSI Sync Target` subsection:
     - GSI server
     - GSI user
     - GSI password handling

### GSI Secret Handling Recommendation

The user asked for `GSI server, user and pass` in the Spatial Workspace tab. For implementation:

- show editable `gsi_server_url`
- show editable `gsi_username`
- prefer editable `gsi_password_env_var` or equivalent secret-source setting instead of plaintext storage
- if local direct password entry is supported for v1 admin convenience, keep it masked, never echo it into logs/status labels, and make the storage mode explicit

This story should at minimum make the password-handling strategy visible and administrator-controlled.

### Scope Boundaries

This story is about the **Settings UX and persistence contract**, not about:

- implementing GSI sync itself
- changing Innola workflow behavior
- changing preflight rule semantics beyond exposing supported edits
- hot-reloading every changed value into already-running workflow steps

The expected outcome is a structured, editable settings surface that remains compatible with the current JSON-backed runtime.

### UX Guidance

- Keep the ArcGIS Pro desktop posture: compact, technical, no decorative layout.
- Prefer tabs and grouped subsections over nested cards.
- Use small editable controls, restrained labels, and clear state text.
- Secret fields should be visibly masked and separated from ordinary values.
- Validation messages should appear near the relevant section and not only in one footer banner.

### Suggested Technical Direction

- Introduce a dedicated settings view model rather than continuing to expand window code-behind.
- Add a write-capable settings service that can round-trip `WorkflowSettings.json` safely.
- Keep `PreflightRules.json` separate and authoritative for rule editing.
- Reuse existing formatting/normalization helpers from `InnolaTransactionSettings` where possible.
- If some fields cannot be safely edited yet, disable them explicitly rather than silently ignoring user edits.

### Testing Guidance

Prefer focused tests around:

- tab/view-model population from current settings files
- save round-trip for editable values
- masked secret-field presentation
- invalid field validation and last-known-good preservation
- compatibility with current runtime loaders after saving edited settings

### References

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/implementation-artifacts/4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md`
- `_bmad-output/implementation-artifacts/7-1a-expose-review-workspace-mode-and-enterprise-targets-in-configuration-panel.md`
- `_bmad-output/implementation-artifacts/2-15-gate-supported-transaction-types-before-workflow-load.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleSettingsLoader.cs`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Completion Notes

- Refactored the read-only Configuration dialog into an editable tabbed `Settings` workspace with `General`, `AI Toolset`, `Innola Integration`, `Preflight Rules`, and `Spatial Workspace` tabs.
- Added a dedicated `SettingsWorkspaceService` and `SettingsWorkspaceDocument` seam so the UI can safely load, validate, and persist `WorkflowSettings.json` plus the authoritative external `PreflightRules.json` catalog.
- Added editable GSI sync target controls with masked direct-password entry and environment-variable password mode handling.
- Preserved unrelated JSON settings during save by rewriting only known keys in the existing document tree; preflight rules remain a separate deterministic rewrite of the external rule catalog.
- Added focused tests for settings tab population, save round-trip, rule editing, loader compatibility, and invalid save validation.
- Focused settings tests pass; one unrelated existing integration test currently still fails in resume-package restore (`InnolaTransactionLoadServiceTests.ResumePackageRestoresSavedWorkflowState`) and should be handled separately from this settings refactor.

### File List

- `_bmad-output/implementation-artifacts/7-5-refactor-configuration-into-editable-settings-workspace-with-functional-tabs.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
