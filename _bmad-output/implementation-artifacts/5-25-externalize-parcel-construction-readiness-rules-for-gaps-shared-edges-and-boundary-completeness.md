---
baseline_commit: handoff-2026-06-24
---

# Story 5.25: Externalize Parcel Construction Readiness Rules For Gaps, Shared Edges, And Boundary Completeness

Status: draft

## Story

As a cadastral examiner validating extracted and reviewed parcel geometry before spatial unit creation,  
I want parcel construction readiness checks for missing edges, shared-boundary conflicts, orphan lines, and incomplete parcel rings to be driven by the same external rule framework used elsewhere in the workflow,  
so that geometry readiness can be enforced, tuned, enabled, disabled, or downgraded by policy without hardcoding review behavior.

## Acceptance Criteria

1. Given parcel points and provisional parcel lines are available after `Validate Points`, when parcel construction readiness validation runs, then each parcel group is evaluated for boundary completeness, shared-edge consistency, and line/point support using externalized rules rather than hardcoded logic.
2. Given a parcel has a missing boundary edge, open ring, or insufficient boundary sequence to build safely, when readiness results are shown, then the parcel is flagged with a parcel-scoped blocker or warning that clearly names the readiness issue.
3. Given a line appears to be shared between adjacent parcels, when validation runs, then the workflow can detect whether the shared boundary was preserved correctly, duplicated incorrectly, or omitted from one parcel’s construction path.
4. Given line-only parcel fragments, orphan lines, or parcel segments with no supporting point sequence exist, when readiness checks run, then those conditions are surfaced as explicit findings rather than only appearing as broken preview geometry.
5. Given readiness rules are externalized, when a rule is disabled in configuration, then the UI shows that rule as skipped/disabled rather than silently omitting the check.
6. Given different transaction types, parcel types, or source modes may tolerate different construction behaviors, when the rule catalog is loaded, then rule scope can vary by transaction/workflow context, parcel type, source family, and severity.
7. Given `Validate Points` is the main correction stage, when readiness issues exist, then the Points Validation Tool can identify the affected parcel and keep `Validation Complete` unavailable only for blocking readiness failures.
8. Given `Final Review` summarizes the spatial state before approval, when the stage opens, then compact readiness diagnostics show pass/warn/block counts for gap, shared-edge, and boundary completeness checks.
9. Given a reviewer needs to understand what is broken, when the parcel preview is shown, then the current parcel can highlight suspect edges, orphan segments, or missing-boundary conditions in a parcel-scoped review message and preview overlay.
10. Given this story is complete, when Create Spatial Units is invoked, then the workflow uses the same saved readiness truth across validation, preview diagnostics, and downstream spatial output messaging.
11. Given the companion settings story is not yet implemented, when this story ships first, then the workflow still loads a stable default readiness rule set from the external catalog and applies it consistently without requiring the new Settings UI.

## Tasks / Subtasks

- [ ] Add a new parcel-construction-readiness rule family to the external rule catalog. (AC: 1, 5, 6)
  - [ ] Define rules for:
    - `boundary_completeness`
    - `shared_edge_consistency`
    - `orphan_line_detection`
    - `minimum_segment_count`
    - `line_without_point_support`
  - [ ] Support `enabled`, `severity`, `scope`, and optional thresholds/flags per rule.
  - [ ] Keep the rule storage aligned with the current configurable validation-rule approach.
  - [ ] Ensure this story can ship with catalog-backed defaults even before the settings surface from `5.25A` is available.

- [ ] Add parcel readiness analysis to the validation path. (AC: 1-4, 7, 10)
  - [ ] Group lines and points by parcel group / traverse.
  - [ ] Detect open rings, missing closing edges, and incomplete boundary chains.
  - [ ] Detect duplicate/shared-edge conflicts and missing reciprocal shared boundaries where applicable.
  - [ ] Detect lines that cannot be assigned to a valid parcel point sequence.

- [ ] Extend validation contracts for readiness findings. (AC: 2-5, 8-10)
  - [ ] Add parcel-scoped readiness result objects to the saved validation contract.
  - [ ] Persist:
    - rule id
    - rule category
    - severity
    - parcel id / parcel group id
    - affected segment or line ids where available
    - reviewer-facing message
    - disabled/skipped state
  - [ ] Keep saved results deterministic across reopen/resume.

- [ ] Surface readiness findings in the Points Validation Tool. (AC: 2, 4, 5, 7, 9)
  - [ ] Show parcel-scoped messages for missing edges, open rings, orphan lines, and shared-edge conflicts.
  - [ ] Keep `Validation Complete` disabled only when blocking readiness findings remain.
  - [ ] Distinguish closure findings from construction-readiness findings so the reviewer can tell what kind of problem they are addressing.

- [ ] Surface compact readiness diagnostics in Final Review. (AC: 5, 8, 10)
  - [ ] Add readiness summary text alongside the existing closure/COGO diagnostics.
  - [ ] Show pass/warn/block counts and whether any rules were skipped because disabled.
  - [ ] Keep the summary small enough for the dockpane.

- [ ] Enhance parcel preview messaging for gap diagnosis. (AC: 2, 3, 4, 9)
  - [ ] Highlight suspect segments or missing-boundary context in the active parcel preview.
  - [ ] Show which parcel is affected and what class of readiness issue is present.
  - [ ] Avoid overloading the preview with full topology editing logic; keep it diagnostic-first.

- [ ] Add focused verification coverage. (AC: 1-10)
  - [ ] Parcel with complete closed boundary passes.
  - [ ] Parcel with missing segment blocks.
  - [ ] Shared-edge conflict surfaces as warning or blocker per configured rule.
  - [ ] Orphan line surfaces explicitly.
  - [ ] Disabled rule is shown as skipped, not omitted.
  - [ ] Final Review shows the same saved readiness truth as Validate Points.

## Dev Notes

### Why This Story Exists

- Current point and closure validation can still leave parcels visually broken during construction because shared boundaries and boundary ownership are separate concerns from raw point quality.
- In many Jamaica compute cases, a single extracted line can logically belong to two adjacent parcels. If the review contract only builds one parcel’s edge sequence from that line, the neighboring parcel can appear to have a gap even when the source data is mostly correct.
- Reviewers need a formal way to see parcel construction readiness problems before Create Spatial Units proceeds.

### Scope Boundary

This story should add:

- externalized parcel construction readiness rules
- parcel-scoped diagnostics for missing edges and shared-edge conflicts
- blocker/warning/skipped reporting in Validate Points and Final Review
- diagnostic preview hints for the active parcel
- catalog-backed default readiness rules that are immediately usable by the workflow

This story should not add:

- full topology editing tools
- authoritative cadastre commit logic
- automatic correction of shared boundaries
- parcel fabric enterprise synchronization
- new settings workspace editing controls for readiness rules

### Recommended Rule Categories

Suggested rule ids / categories:

- `boundary_completeness`
- `shared_edge_consistency`
- `orphan_line_detection`
- `line_without_point_support`
- `minimum_segment_count_by_parcel_type`

Suggested rule dimensions:

- transaction type / workflow family
- parcel type
- source type / extraction mode
- severity (`blocker`, `warning`, `info`)
- enabled / disabled
- optional thresholds such as minimum segment count or whether shared-edge duplication is permitted

### Recommended Contract Additions

At minimum, parcel-scoped readiness findings should carry:

- `parcel_id`
- `parcel_group_id`
- `readiness_rule_id`
- `readiness_rule_name`
- `readiness_rule_category`
- `readiness_status`
- `readiness_severity`
- `readiness_message`
- `affected_line_ids`
- `affected_segment_ids`
- `boundary_gap_count`
- `shared_edge_conflict_count`
- `orphan_line_count`
- `rule_disabled`
- `rule_skip_reason`

Suggested status values:

- `passed`
- `warning`
- `blocked`
- `skipped`
- `not_applicable`

### UI / Reviewer Guidance

In `Validate Points`, the reviewer should be able to tell:

1. which parcel is affected
2. whether the problem is closure vs construction readiness
3. whether the issue is a missing edge, shared-edge conflict, or orphan line
4. whether the finding is blocking or only advisory

In `Final Review`, keep it compact, for example:

- `Construction readiness: 8 parcels checked; 6 passed, 1 warning, 1 blocked; 1 rule skipped (disabled).`

### Likely Implementation Areas

- `src/ProcessingTools/rules/rules.yaml`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointsValidationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointsValidationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ProcessingTools/tests/test_validation_adapter.py`

### Alignment Notes

- This story should sit on top of Story `5.23` rather than replace it.
- This story should be implemented before `5.25A`.
- Closure tolerance and parcel construction readiness are related but distinct:
  - closure asks whether a parcel closes within tolerance
  - construction readiness asks whether the parcel has the full and consistent boundary definition required to build spatial units safely
- Disabled rules should be visible in the UI as skipped so operators can tell the check exists but is intentionally not active.
- The settings/admin surface for editing these rules belongs to `5.25A`; this story should only require a stable persisted catalog/default contract.

### Recommended Delivery Order

1. Implement rule catalog defaults and validation engine behavior in this story.
2. Surface parcel-scoped readiness findings in `Validate Points` and `Final Review`.
3. Verify Create Spatial Units consumes the saved readiness truth.
4. Implement `5.25A` afterward to expose admin editing and severity toggles in Settings.

## References

- `_bmad-output/implementation-artifacts/5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md`
- `_bmad-output/implementation-artifacts/5-24-add-whole-review-parcel-context-and-active-parcel-diagnostics-to-points-validation-preview.md`
- `_bmad-output/implementation-artifacts/5-21-add-optional-cogo-attributes-and-labels-to-non-fabric-spatial-output-layers.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-27 | 0.1 | Drafted externalized parcel construction readiness rules story for gaps, shared edges, orphan lines, and boundary completeness diagnostics. | Codex |
