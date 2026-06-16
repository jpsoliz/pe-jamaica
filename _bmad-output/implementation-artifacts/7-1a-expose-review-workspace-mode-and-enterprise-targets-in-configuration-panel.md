---
baseline_commit: handoff-2026-06-15
---

# Story 7.1A: Expose Review Workspace Mode And Enterprise Targets In Configuration Panel

Status: review

## Story

As a solution administrator,  
I want the Configuration panel to show and validate the selected review workspace mode and any Enterprise working-layer targets,  
so that deployments can intentionally switch between local standard review, local Parcel Fabric review, and Enterprise working-layer review without editing hidden code paths.

## Acceptance Criteria

1. Given the application supports multiple review workspace modes, when the user opens Configuration, then the panel shows the active review workspace mode with the supported values `normal`, `parcel_fabric_local`, and `enterprise_working_layers`.
2. Given the Configuration panel shows review workspace modes, when the user inspects the setting, then the panel explains the purpose of each mode in concise administrator-facing language.
3. Given the review workspace mode is `enterprise_working_layers`, when the user reviews configuration details, then the panel shows the configured Enterprise targets for working points, lines, polygons, and optional issue layers.
4. Given the review workspace mode is `enterprise_working_layers`, when the user reviews configuration details, then the panel shows when Enterprise publish is expected to happen, with `on_complete` presented as the local-first default.
5. Given the review workspace mode is `enterprise_working_layers`, when required Enterprise targets are missing or invalid, then the panel shows a clear readiness warning without destroying existing local workflow settings.
6. Given the review workspace mode is `normal` or `parcel_fabric_local`, when the panel is opened, then Enterprise working-layer settings are hidden or visually de-emphasized and the panel makes clear that the workflow remains local-first.
7. Given the selected mode is incomplete or invalid, when the panel is displayed, then local modes remain available as safe fallback options.

## Tasks / Subtasks

- [x] Extend configuration view models and settings display. (AC: 1-3)
- [x] Extend configuration view models and settings display. (AC: 1-4)
  - [x] Surface the active review workspace mode in the Configuration panel.
  - [x] Show concise explanatory text for each supported mode.
  - [x] Display configured Enterprise targets when Enterprise working layers mode is selected.
  - [x] Display Enterprise publish timing so administrators can see whether shared visibility occurs on outputs or only on final completion.

- [x] Add readiness warnings and fallback messaging. (AC: 5-7)
  - [x] Detect missing required Enterprise working-layer targets.
  - [x] Present non-destructive warnings that do not block local modes.
  - [x] Make local-first fallback behavior explicit in the UI.

- [x] Keep the panel aligned with the architecture contract. (AC: 1-6)
  - [x] Reuse the same mode names defined in architecture and settings.
  - [x] Avoid introducing hidden alternate values or legacy labels in the UI.

## Dev Notes

### Why This Story Exists

- The architecture now recognizes three review workspace modes.
- Administrators need to see which mode is active without tracing JSON by hand.
- The Configuration panel should become the visible contract between deployment intent and runtime behavior.

### Scope Boundaries

- This story is about configuration visibility and validation, not about publishing or restoring Enterprise geometry.
- It may read settings and display readiness, but it does not implement Enterprise working-layer writes by itself.

### Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/`

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/7-1-define-enterprise-working-review-layer-schema-and-configuration.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.sln /nodeReuse:false`
- `dotnet run --project src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.Tests\\ParcelWorkflowAddIn.Tests.csproj --no-build`

### Completion Notes

- Extended the Configuration panel so it now shows the active review workspace mode, its explanation, and the supported configuration values.
- Grouped Enterprise working review settings into a dedicated section with mode-aware presentation.
- Surfaced Enterprise working review timing alongside behavior so administrators can see that the default local-first mode publishes only on final completion.
- De-emphasized the Enterprise section when a local-first mode is active and made the local-first fallback explicit.
- Preserved non-destructive readiness warnings for incomplete Enterprise working-layer setups and added coverage for local-vs-enterprise warning behavior.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
