---
baseline_commit: handoff-2026-06-24
---

# Story 5.25A: Expose Parcel Construction Readiness Rules In Settings Workspace

Status: review

## Story

As a solution administrator managing compute-validation policy,  
I want parcel construction readiness rules for gaps, shared edges, orphan lines, and boundary completeness to be visible and editable in the Settings workspace,  
so that the review workflow can be tuned by policy without changing code and users can clearly see which readiness checks are enabled, disabled, blocking, or advisory.

## Acceptance Criteria

1. Given parcel construction readiness rules are externalized by Story `5.25`, when the Settings workspace opens, then administrators can view those rules in a dedicated validation/readiness section instead of only editing raw JSON files.
2. Given readiness rules may be enabled or disabled by policy, when a rule is displayed in Settings, then the UI shows at minimum:
   - rule name
   - rule category
   - enabled/disabled state
   - severity
   - scope summary
3. Given some readiness rules use thresholds or options, when the Settings workspace renders them, then administrators can edit the supported values for rules such as minimum segment count, shared-edge strictness, or gap detection behavior.
4. Given different workflows or parcel types may need different behavior, when rule scope is shown, then the Settings workspace clearly indicates whether the rule is global, transaction-specific, source-specific, parcel-type-specific, or workflow-specific.
5. Given an administrator disables a readiness rule, when that configuration is saved, then downstream `Validate Points` and `Final Review` show the rule as skipped/disabled rather than silently removing it from diagnostics.
6. Given an administrator changes a rule severity from blocker to warning or vice versa, when the workflow next runs, then `Validate Points` and `Final Review` use the updated severity without requiring code changes.
7. Given default/fallback readiness rules exist, when the Settings workspace is shown, then the UI distinguishes default fallback rules from explicitly scoped rules.
8. Given the readiness-rule configuration becomes invalid, when the administrator saves settings, then the workspace blocks invalid values where practical and shows a clear validation message instead of saving a broken rule payload.
9. Given this story is complete, when support teams troubleshoot a case, then they can confirm from Settings which parcel construction readiness rules are active for the environment.
10. Given Story `5.25` already persists and applies the readiness-rule catalog, when this settings story is implemented, then it edits and validates that same rule contract rather than introducing a parallel configuration model.

## Tasks / Subtasks

- [x] Add a parcel construction readiness section to the Settings workspace. (AC: 1-4, 7, 9)
  - [x] Group it near existing validation / closure configuration rather than scattering it across unrelated tabs.
  - [x] Show readiness rules in a way that is readable inside the ArcGIS Pro settings surface.
  - [x] Distinguish default rules from scoped overrides.
  - [x] Reuse the persisted rule contract from `5.25` rather than creating a second settings-only schema.

- [x] Surface rule state and severity controls. (AC: 2, 5, 6)
  - [x] Add enabled/disabled control per readiness rule.
  - [x] Add severity control per rule where supported.
  - [x] Keep labels understandable for administrators, not only developers.

- [x] Surface supported threshold and behavior settings. (AC: 3, 4, 7)
  - [x] Add editable values for readiness-rule options such as:
    - minimum segment count
    - shared-edge strictness
    - orphan-line handling mode
    - boundary completeness behavior
  - [x] Show scope summary for transaction type, parcel type, workflow family, and source type where applicable.

- [x] Add settings validation and persistence support. (AC: 5-8)
  - [x] Validate required numeric and enumerated values before save.
  - [x] Preserve structured rule configuration when saved and reloaded.
  - [x] Avoid overwriting unrelated settings sections.

- [x] Align workflow messaging with saved rule state. (AC: 5, 6, 9)
  - [x] Ensure disabled rules are surfaced later as skipped.
  - [x] Ensure updated severity is reflected in validation outcomes.
  - [x] Keep environment-level rule truth visible to support teams.

- [x] Add focused verification coverage. (AC: 1-9)
  - [x] Rule enable/disable persists correctly.
  - [x] Severity changes persist correctly.
  - [x] Invalid rule values are blocked or clearly rejected.
  - [x] Default and scoped rules are distinguishable after reload.

## Dev Notes

### Why This Story Exists

- Story `5.25` introduces a new parcel construction readiness rule family.
- Without a settings surface, those rules would remain effectively hidden and too hard to govern in day-to-day administration.
- The workflow already uses configurable rule patterns elsewhere, so readiness rules should follow the same operational model.

### Recommended Settings Placement

Best fit:

- existing Settings workspace
- under validation / spatial review / rule configuration area

Suggested subsection title:

- `Parcel Construction Readiness`

Suggested grouped controls:

1. `Boundary completeness`
2. `Shared-edge consistency`
3. `Orphan line detection`
4. `Line/point support`
5. `Fallback / default readiness profile`

### Recommended UI Shape

Because ArcGIS Pro settings space is tight, prefer:

- compact rule rows
- enable toggle
- severity dropdown
- short scope label
- expandable detail area for thresholds/options

Do not force admins to edit long raw JSON unless they are using an advanced override field intentionally.

### Scope Boundary

This story should add:

- settings visibility/editability for readiness rules
- validation for those settings
- persistence/load behavior
- admin-safe editing of the exact readiness catalog contract already used by the workflow

This story should not add:

- the readiness rule engine itself
- parcel preview logic
- additional validation categories outside readiness rules
- a new parallel readiness-rule storage format

### Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ProcessingTools/rules/rules.yaml`

### Alignment Notes

- This story is the companion admin surface for Story `5.25`.
- This story should be implemented after `5.25`, not before it.
- Disabled readiness rules should later appear in the workflow as skipped, which means the settings model needs to preserve enabled state clearly.
- Severity and threshold values should be operationally editable without requiring code changes or manual file surgery.
- The primary architectural goal is to edit the same readiness-rule truth already consumed by `Validate Points` and `Final Review`, not to mirror or duplicate it.

### Recommended Delivery Order

1. Complete `5.25` so the workflow has a stable readiness-rule contract and default rule set.
2. Bind the Settings workspace to that same rule contract in this story.
3. Add UI validation for editable threshold/severity/toggle fields.
4. Verify disabled and severity-changed rules appear downstream exactly as the workflow now reports them.

## References

- `_bmad-output/implementation-artifacts/5-25-externalize-parcel-construction-readiness-rules-for-gaps-shared-edges-and-boundary-completeness.md`
- `_bmad-output/implementation-artifacts/5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md`
- `_bmad-output/implementation-artifacts/7-5-refactor-configuration-into-editable-settings-workspace-with-functional-tabs.md`

## Dev Agent Record

### Debug Log

- 2026-06-27: Added a dedicated parcel construction readiness editor in the Settings workspace, including default fallback controls and scoped rule controls.
- 2026-06-27: Wired settings persistence to `parcel_construction_readiness_profile_overrides` so the workspace edits the same runtime rule truth used by the workflow.
- 2026-06-27: Updated both C# and Python readiness consumers to honor saved enabled/severity/behavior overrides and surface disabled rules as skipped.
- 2026-06-27: Added focused settings tests for readiness round-trip persistence and invalid readiness validation.

### Completion Notes

- Implemented a new `Parcel construction readiness` section in the Spatial Workspace tab.
- Added editable default fallback profile controls plus compact scoped readiness rule rows.
- Saved readiness overrides back into `WorkflowSettings.json` without introducing a parallel rule schema.
- Verified both add-in and tests projects build successfully.
- Full console-style test runner still stops on a pre-existing unrelated failure in `SourceInputProfileDetectorTests.DetectsScenarioBFromPointsDwgAndPlanRoles()`.

## File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-27 | 0.1 | Drafted the settings-surface companion story for parcel construction readiness rules introduced by Story 5.25. | Codex |
| 2026-06-27 | 0.2 | Implemented Settings workspace readiness rule editing, persistence, runtime override application, and focused verification coverage. | Codex |
