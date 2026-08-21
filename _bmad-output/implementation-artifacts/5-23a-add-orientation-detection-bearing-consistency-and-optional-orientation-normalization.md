---
baseline_commit: handoff-2026-08-19
---

# Story 5.23A: Add Orientation Detection, Bearing Consistency, And Optional Orientation Normalization

Status: implemented

## Story

As a cadastral examiner validating extracted parcel traverses before spatial unit creation,  
I want the workflow to detect parcel ring orientation from the full ordered geometry, validate each segment's bearing against its coordinates, and optionally normalize ring orientation for downstream outputs,  
so that clockwise/counterclockwise handling is deterministic, reviewer-visible, and not left to AI guesswork when source documents are imperfect.

## Acceptance Criteria

1. Given an extracted or edited parcel point sequence exists in `Validate Points`, when validation runs, then the workflow computes parcel orientation from the full closed ring geometry rather than inferring it from only the first bearing.
2. Given a parcel can be built from an ordered point ring, when orientation is computed, then the workflow records whether the parcel is `clockwise`, `counterclockwise`, or `indeterminate` in the saved validation/review contract.
3. Given a segment has both coordinates and source bearing text, when validation runs, then the workflow compares the stated bearing with the computed azimuth from `from_point -> to_point` and surfaces mismatches beyond tolerance as rule results.
4. Given a segment lacks usable bearing text, when validation runs, then the workflow does not fabricate a pass and instead records that bearing-consistency check as `not_applicable` or `skipped`.
5. Given a parcel sequence is geometrically valid but its orientation differs from the configured downstream expectation, when optional normalization is enabled, then the workflow can normalize the output ring orientation for `Create Spatial Units` without altering the saved reviewer truth about the original reviewed order.
6. Given optional normalization is disabled, when a parcel orientation differs from the preferred output orientation, then the workflow leaves the original reviewed order intact and reports that orientation state in diagnostics.
7. Given `Validate Points` shows parcel diagnostics, when a parcel is selected, then the examiner can see:
   - the detected orientation
   - whether bearing consistency passed, warned, or blocked
   - whether normalization would or did apply downstream
8. Given `Final Review` summarizes review readiness, when the stage opens, then compact diagnostics show orientation and bearing-consistency counts alongside existing closure/readiness signals.
9. Given the source document is ambiguous or the order is wrong, when bearings and point order disagree, then the workflow flags that disagreement as a reviewer-facing issue instead of silently choosing a clockwise/counterclockwise interpretation.
10. Given this story is complete, when `Create Spatial Units` or non-fabric/fabric outputs are generated, then the geometry build path uses the same validated orientation truth and optional normalization policy consistently.

## Tasks / Subtasks

- [ ] Add rule definitions to the external rule catalog. (AC: 1-4, 7-9)
  - [ ] Define an `orientation_detection` rule family with configurable preferred output orientation.
  - [ ] Define a `bearing_coordinate_consistency` rule family with tolerance settings.
  - [ ] Support enable/disable, severity, workflow/source scope, and default fallback behavior.

- [ ] Add parcel-level orientation computation to the validation path. (AC: 1-2, 7-10)
  - [ ] Compute signed area / ring orientation from the full closed ordered parcel geometry.
  - [ ] Return `clockwise`, `counterclockwise`, or `indeterminate`.
  - [ ] Reuse the same point ordering already used for closure and readiness checks.

- [ ] Add segment-level bearing consistency checks. (AC: 3-4, 7-9)
  - [ ] Compute azimuth from coordinates for each ordered segment.
  - [ ] Compare computed azimuth with stated bearing text where available.
  - [ ] Surface mismatches as pass/warn/block based on configured tolerance.
  - [ ] Preserve `not_applicable` / `skipped` outcomes where bearings are absent or unparsable.

- [ ] Extend saved validation contracts and review diagnostics. (AC: 2, 7-10)
  - [ ] Persist parcel orientation results in validation payloads.
  - [ ] Persist parcel/segment bearing-consistency findings in deterministic form.
  - [ ] Add compact summary counts for orientation/bearing checks in `Final Review`.

- [ ] Add optional downstream orientation normalization. (AC: 5-6, 10)
  - [ ] Add settings-backed policy for `normalize_orientation`.
  - [ ] Apply normalization only in output build paths, not by mutating reviewer-entered order in saved review data.
  - [ ] Support at least `disabled`, `prefer_clockwise`, and `prefer_counterclockwise`.

- [ ] Surface reviewer guidance in `Validate Points`. (AC: 7-9)
  - [ ] Show parcel orientation status in the parcel details area.
  - [ ] Show whether a parcel's order conflicts with source bearings.
  - [ ] Make blocker/warning language explicit when orientation is not trustworthy.

- [ ] Add focused verification coverage. (AC: 1-10)
  - [ ] Clockwise parcel is detected correctly.
  - [ ] Counterclockwise parcel is detected correctly.
  - [ ] Bearing consistency passes for a well-ordered traverse.
  - [ ] Bearing consistency warns/blocks when row order disagrees with the source bearing chain.
  - [ ] Normalization flips output orientation only when enabled.
  - [ ] Saved reviewer data remains unchanged when normalization is applied downstream.

## Dev Notes

### Why This Story Exists

- The workflow currently follows source traversal order and validates closure/readiness, but it does not yet treat orientation as an explicit validation concept.
- The first bearing is not reliable enough to decide clockwise/counterclockwise direction for production cadastral work because orientation is a property of the full ring, not one segment.
- Bearings are still valuable, but as consistency evidence against ordered coordinates rather than as the sole source of orientation truth.

### Key Design Decision

Use the **full ordered closed ring** to determine orientation.  
Use **segment bearing-vs-coordinate comparison** to detect ordering mistakes or source ambiguity.  
Use **optional normalization** only for downstream output geometry policy.

### Recommended Settings Surface

Add to the Settings workspace:

- `orientation_normalization_mode`
- `preferred_output_orientation`
- `bearing_consistency_tolerance_deg`
- `bearing_consistency_warning_tolerance_deg`
- `orientation_rule_overrides`

### Patch Path

Primary implementation files:

- `src/ProcessingTools/rules/rules.yaml`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointsValidationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointsValidationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ProcessingTools/tests/test_validation_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`

Implementation order:

1. Add rule definitions and settings keys.
2. Compute parcel orientation in `validation_adapter.py`.
3. Add bearing-vs-coordinate segment checks in `validation_adapter.py`.
4. Persist parcel and segment diagnostics into validation contracts.
5. Surface parcel orientation / bearing consistency in `Validate Points` and `Final Review`.
6. Apply optional normalization in `output_adapter.py` only for output geometry build paths.
7. Verify that saved review order remains authoritative while output orientation can still be normalized downstream.

### Scope Boundary

This story should add:

- explicit parcel orientation detection
- explicit bearing consistency checks
- reviewer-facing orientation diagnostics
- optional output normalization policy

This story should not add:

- automatic AI-driven reordering of parcel points
- silent correction of reviewer-entered order
- full topology repair
- parcel fabric commit behavior changes

### Alignment Notes

- This story extends `5.23` rather than replacing it.
- Orientation should complement closure and readiness, not compete with them.
- If orientation is wrong but closure still passes, the workflow should still tell the reviewer that the parcel order may not reflect the source bearing chain correctly.
- If normalization is enabled, it should be treated as an output policy, not as proof that the reviewed parcel order was correct.

## References

- `_bmad-output/implementation-artifacts/5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md`
- `_bmad-output/implementation-artifacts/5-25-externalize-parcel-construction-readiness-rules-for-gaps-shared-edges-and-boundary-completeness.md`
- `_bmad-output/implementation-artifacts/5-21-add-optional-cogo-attributes-and-labels-to-non-fabric-spatial-output-layers.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-08-19 | 0.1 | Drafted follow-up story for explicit parcel orientation detection, bearing-coordinate consistency validation, and optional output-orientation normalization. | Codex |
| 2026-08-19 | 1.0 | Implemented add-in-side orientation validation contract, settings surface, parcel diagnostics, final review summary wiring, and downstream normalization argument plumbing. | Codex |
