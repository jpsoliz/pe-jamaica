---
baseline_commit: handoff-2026-06-24
---

# Story 5.23: Add Parcel-Type-Aware Closure Tolerance Validation To Validate Points And Final Review

Status: review

## Story

As a cadastral examiner reviewing computed parcel points before final submission,  
I want the workflow to evaluate parcel closure against parcel-type-aware tolerance rules,  
so that open traverses, excessive misclose, and parcel-specific closure exceptions are identified before Create Spatial Units is accepted as review-ready.

## Acceptance Criteria

1. Given extracted or manually corrected parcel points are available in `Validate Points`, when closure validation runs, then each parcel group is evaluated for closure using a configured tolerance rule matched by parcel type and workflow/source context.
2. Given a parcel can be closed geometrically, when closure is calculated, then the workflow records closure delta X, closure delta Y, closure distance, and misclose ratio for that parcel in a deterministic review contract.
3. Given a parcel exceeds its configured tolerance, when validation is shown in `Validate Points`, then the parcel is clearly flagged with a reviewer-facing warning or blocker and `Validation Complete` remains unavailable until blocking closure issues are resolved or explicitly waived by rule.
4. Given a parcel is within its configured tolerance, when the examiner reviews that parcel, then closure status is shown as passed and does not block completion of the `Validate Points` stage.
5. Given different parcel types have different expected closure behavior, when rules are loaded, then the workflow can distinguish at least between standard closed parcels, special/open boundary cases, and tolerance profiles defined by parcel type or transaction/source rule mapping.
6. Given `Final Review` summarizes the transaction outcome, when the stage opens, then it shows compact closure diagnostics including parcel count reviewed, pass/warn/block counts, and whether any parcel exceeded tolerance.
7. Given closure metrics are written into downstream output artifacts, when `Create Spatial Units` and `Final Review` inspect them, then the same closure truth is used across validation, output diagnostics, and reviewer messaging.
8. Given no parcel-type-specific tolerance rule is found, when closure validation runs, then the workflow applies a configured default closure rule and reports that fallback clearly in diagnostics.
9. Given this story is complete, when a reviewer asks why validation is blocked, then the workflow can point to the exact parcel and closure reason rather than only a generic geometry or duplicate-point message.

## Tasks / Subtasks

- [x] Add closure tolerance rule definitions to the externalized rule model. (AC: 1, 5, 8)
  - [x] Define rule inputs for parcel type, source type, transaction/workflow context, and default fallback behavior.
  - [x] Add closure tolerance configuration values such as max closure distance, minimum misclose ratio, and optional allow-open-boundary behavior.
  - [x] Keep closure rule definitions aligned with the current externalized rule catalog pattern introduced for early compute checks.

- [x] Add parcel-level closure computation to the validation data path. (AC: 1-4, 7-9)
  - [x] Group review points by parcel in validation processing.
  - [x] Compute parcel closure metrics from ordered point sequences:
    - [x] `closure_dx`
    - [x] `closure_dy`
    - [x] `closure_distance_m`
    - [x] `misclose_ratio`
  - [x] Evaluate those metrics against the matched parcel-type-aware tolerance rule.

- [x] Extend validation contracts and case artifacts for closure reporting. (AC: 2-4, 6-9)
  - [x] Add parcel-level closure result fields to the validation summary/review result contract.
  - [x] Persist closure status, rule id/profile, and reviewer-facing message in lowercase `snake_case`.
  - [x] Ensure reopen/resume restores closure findings without recomputation drift unless the point set changed.

- [x] Surface closure findings in `Validate Points`. (AC: 3-5, 9)
  - [x] Highlight parcels with closure warnings or blockers in the parcel navigation/review surface.
  - [x] Keep `Validation Complete` disabled while blocking closure issues remain.
  - [x] Show reviewer-friendly parcel-scoped messaging that identifies the parcel and the closure reason.

- [x] Surface compact closure diagnostics in `Final Review`. (AC: 6-8)
  - [x] Add a compact closure diagnostics line or summary card alongside current COGO diagnostics.
  - [x] Report pass/warn/block totals and whether default tolerance fallback was used.
  - [x] Keep the summary concise enough for the dockpane while still explaining blocking status.

- [x] Keep downstream outputs aligned to the same closure truth. (AC: 6-8)
  - [x] Ensure `Create Spatial Units` / output summaries can reuse closure result fields rather than recomputing a different rule set.
  - [x] Distinguish geometric polygon closure from survey tolerance closure in diagnostics and messaging.

- [x] Add focused verification coverage. (AC: 1-9)
  - [x] Parcel passes when closure is within tolerance.
  - [x] Parcel blocks when closure exceeds tolerance.
  - [x] Default tolerance fallback is applied when parcel-type-specific rule is missing.
  - [x] Open-boundary or special parcel profiles do not incorrectly block when rules allow them.
  - [x] `Final Review` shows consistent closure diagnostics from the saved validation result.

## Dev Notes

### Why This Story Exists

- The current workflow can validate points, duplicates, and structural consistency, but it does not yet perform survey-grade closure tolerance analysis.
- Existing spatial outputs may report polygon closure or produce valid geometry while still missing parcel-level closure validation against examiner expectations.
- The user explicitly needs closure review to vary by parcel type, not only by a single hardcoded tolerance.

### Scope Boundary

This story should add:

- parcel-type-aware closure tolerance rules
- parcel-level closure metrics
- `Validate Points` blocking/warning behavior for closure
- compact `Final Review` closure diagnostics

This story should not add:

- authoritative cadastre commit logic
- enterprise parcel-fabric sync rules
- full map-based closure repair tooling
- non-closure geometry quality checks unrelated to parcel tolerance

### Recommended Contract Additions

At minimum, parcel-level validation results should carry:

- `parcel_id`
- `parcel_type`
- `closure_rule_id`
- `closure_rule_name`
- `closure_rule_source`
- `closure_dx`
- `closure_dy`
- `closure_distance_m`
- `misclose_ratio`
- `closure_status`
- `closure_severity`
- `closure_message`
- `closure_rule_fallback_used`

Suggested status values:

- `passed`
- `warning`
- `blocked`
- `not_applicable`

### Rule Model Direction

Closure tolerance should follow the same externalized rule approach already used for:

- supporting document rules by transaction type
- structure rules by source type
- georeference rules by source type

Recommended closure rule dimensions:

- transaction type / workflow family
- parcel type
- source type or extraction profile
- whether open boundaries are allowed
- numeric tolerance thresholds

### UI / Reviewer Guidance

In `Validate Points`, the examiner should be able to tell:

1. which parcel failed
2. whether it is a warning or blocker
3. the measured closure amount
4. the applied tolerance profile

In `Final Review`, keep it short, for example:

- `Closure diagnostics: 8 parcels checked; 6 passed, 1 warning, 1 blocked; default tolerance used for 2 parcels.`

### Likely Implementation Areas

- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/rules/rules.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointsValidationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointsValidationWindow.xaml.cs`
- `src/ProcessingTools/tests/test_validation_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

### Alignment Notes

- This story should distinguish between:
  - simple polygon closure in output geometry
  - parcel survey closure tolerance for validation decisions
- Closure tolerance must remain configurable, not hardcoded.
- Parcel type should be resolved from the same parcel grouping / interpretation model already used in the Points Validation Tool and Create Spatial Units pipeline.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-24 | 0.1 | Drafted parcel-type-aware closure tolerance validation story for Validate Points and Final Review. | Codex |
| 2026-06-24 | 1.0 | Implemented externalized closure tolerance profiles, parcel-level closure validation, settings overrides, and closure diagnostics across Validate Points and Final Review. | Codex |
| 2026-06-24 | 1.1 | Exposed standard closure tolerance thresholds directly in the Settings workspace while keeping advanced JSON overrides for profile-level changes. | Codex |

## Dev Agent Record

### Completion Notes

- Added externalized closure tolerance defaults and profiles to the rule catalog, including standard closed, precision/small parcel, open-boundary, and tabular-coordinate variants.
- Extended the Python validation adapter to load closure profiles plus optional JSON overrides from settings, compute parcel-group closure metrics, and emit closure summary/results in the validation payload.
- Extended the C# validation summary contract and the in-review `ParcelScopedReviewValidationService` so the Points Validation Tool and dockpane share parcel-scoped closure blocker/warning/pass decisions.
- Added a settings workspace field for `closure_tolerance_profile_overrides` so tolerance profiles can be adjusted without code changes.
- Exposed the standard closed-parcel closure thresholds directly in the Settings workspace so reviewers can see and adjust blocker/warning distance and ratio values without editing raw JSON.
- Updated review and final-summary messaging so closure blockers can identify the specific parcel and the reason instead of only generic validation failures.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.csproj`
- `python -m unittest tests.test_validation_adapter` (run from `src\\ProcessingTools` with `PYTHONPATH` set to `src\\ProcessingTools`)

### File List

- `src/ProcessingTools/rules/rules.yaml`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/tests/test_validation_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
