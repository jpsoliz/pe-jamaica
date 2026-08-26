# Investigation: TR 100001219 PLA Workflow Classification

## Hand-off Brief

1. **What happened.** TR 100001219 loaded through the generic Parcel Workflow [Compute] shell, but its manifest confirms it was classified as PLA with source type `st_plan_annexation_pdf` and workflow rule `pla_plan_annexation_v1`.
2. **Where the case stands.** The transaction is blocked at preflight because Georeference Check requires coordinate context before extraction, which conflicts with the 2.23C PLA local-origin design.
3. **What's needed next.** Patch PLA preflight/georeference gating so PLA can proceed to selected-plan evidence extraction and local-origin geometry review without requiring coordinate evidence up front.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR 100001219 |
| Date opened | 2026-08-24 |
| Status | Concluded |
| System | Windows, ArcGIS Pro add-in local case folder |
| Evidence sources | Case manifest, preflight summaries, workflow settings/rules, source code |

## Problem Statement

User reports that Plan Annexed / PLA transaction TR 100001219 launched in the Parcel Workflow [Compute] flow and asks what is right or wrong for testing Stories 2.23A, 2.23B, and 2.23C.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json` | Available | Confirms PLA profile, source type, source role, and workflow rule. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\preflight_summary.json` | Available | Confirms preflight is blocked by georeference rules. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\structure_check_summary.json` | Available | Confirms structure check passes for Plan annexation PDF and `pla_plan_annexation_v1`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\georeference_check_summary.json` | Available | Confirms georeference check blockers. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json` | Available | Defines PLA transaction profile and source type. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json` | Available | Defines PLA workflow rule. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs` | Available | Shows georeference evaluation is run before extraction evidence exists. |

## Confirmed Findings

### Finding 1: TR 100001219 is classified as PLA, not generic PE

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json:22`, `manifest.json:23`, `manifest.json:35`, `manifest.json:36`

**Detail:** The manifest records `profile_code = pla_plan_annexation`, display label `PLA - plan annexation PDF`, `case_type = PLA`, and `profile_hint = PLA`.

### Finding 2: The source document is correctly classified for 2.23A

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json:17`, `manifest.json:18`, `manifest.json:50`, `manifest.json:57`

**Detail:** The copied PDF `1000-55.pdf` has workflow role `plan_annexation_pdf` and Innola source type `st_plan_annexation_pdf`.

### Finding 3: The PLA workflow rule resolved correctly

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json:81`, `manifest.json:82`, `manifest.json:93`, `manifest.json:95`, `manifest.json:100`

**Detail:** The manifest records `workflow_profile = pla_plan_annexation`, `workflow_rule_id = pla_plan_annexation_v1`, and script step `select_plan_annexation_pdf_page` with output artifact `working/pla_plan_annexation/pla_plan_evidence_selection.json`.

### Finding 4: Structure check passed for PLA

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\structure_check_summary.json`

**Detail:** Structure check status is `passed`; it records passed checks for required `Plan annexation PDF`, copied source integrity, and workflow rule `pla_plan_annexation_v1`.

### Finding 5: Preflight is blocked by georeference rules before PLA extraction can run

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\preflight_summary.json:10`, `preflight_summary.json:13`, `preflight_summary.json:17`, `preflight_summary.json:29`, `preflight_summary.json:33`

**Detail:** The combined preflight status is `blocked` with blockers `georeference_source_presence` and `georeference_spatial_validation_readiness`. The PLA selected-plan working folder is absent, so Stories 2.23B/2.23C have not run yet.

### Finding 6: Current georeference source rules do not include PLA plan-annexation PDF as acceptable pre-extraction evidence

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`

**Detail:** Georeference readiness checks look for computation sheet, coordinate text, plan/map reference, or survey-plan extraction evidence. For PLA, extraction evidence is expected to be generated after selecting the plan page, so the preflight order is too strict for PLA local-origin workflows.

## Deduced Conclusions

### Deduction 1: Parcel Workflow [Compute] shell is expected, but the preflight blocker is not acceptable for 2.23C testing

**Based on:** Findings 1, 2, 3, and 5.

**Reasoning:** The top-level shell is shared. The actual workflow identity is determined by manifest profile/rule/source role, and those are PLA. However, the combined preflight gate blocks before the PLA plan-selection/extraction step can produce the review artifacts that 2.23C requires.

**Conclusion:** TR 100001219 is a valid test transaction for 2.23A. It cannot fully test 2.23B/2.23C until PLA georeference preflight gating is relaxed or sequenced after selected-plan extraction.

## Conclusion

**Confidence:** High

The transaction setup is correct for PLA classification. What is wrong is the workflow gate: combined preflight is still enforcing generic georeference readiness before PLA selected-plan extraction, even though PLA is allowed to proceed with no coordinate evidence and later produce local-origin geometry.

## Recommended Next Steps

### Fix direction

Patch PLA preflight behavior so `pla_plan_annexation` can pass initial structure/preflight with a valid `plan_annexation_pdf`, then run selected-plan evidence extraction. Georeference absence should become reportable/no-coordinate evidence after extraction, not an up-front blocker.

### Diagnostic

After patching, rerun TR 100001219 and verify:

- manifest stays `pla_plan_annexation`
- preflight no longer blocks solely on missing coordinate/georeference context
- `working/pla_plan_annexation/pla_plan_evidence_selection.json` is created after page selection
- extraction creates `working/extraction_review_data.json` with `geometry_reference_mode = local_origin` when no coordinates are extracted

## Reproduction Plan

1. Load TR 100001219.
2. Confirm the case folder is `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219`.
3. Run preflight.
4. Observe blocked status with georeference source/readiness blockers despite correct PLA profile and source classification.

## Follow-up: 2026-08-24

### New Question

User asks whether PLA should have a dedicated UX because TR 100001219 still feels like it is running through the generic Compute workflow even though scripts/processes can be reused.

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json` | Available | Still shows `workflow_state = preflight_blocked`, `task_name = Compute Survey Plan`, and PLA profile/rule/source role. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\preflight_summary.json` | Available | Now contains no blockers, but status remains `blocked` because the combined legacy summary is not fully passed until all early check summaries exist/pass. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\dimension_check_summary.json` | Missing | No dimension summary exists yet. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\pla_plan_annexation\pla_plan_evidence_selection.json` | Missing | No selected-plan artifact exists yet, so the PLA user-facing selection/extraction path has not started. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | Active workspace labels are generic: Preflight, Georeference Check, Dimension Check, Validate Points and Lines. PLA selection is shown only when Extraction Review is active. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml` | Available | PLA selection controls exist inside the generic `Validate Points and Lines` section. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs` | Available | PLA extraction fails with “Select and save PLA plan evidence before running PLA extraction” if the selection artifact does not exist. |

### Confirmed Findings

#### Finding 7: The case is now past the original georeference blocker, but the combined UI status still reads blocked

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\preflight_summary.json:10`, `preflight_summary.json:11`, `preflight_summary.json:259`, `preflight_summary.json:275`

**Detail:** The legacy combined summary has `status = blocked` and `blockers = []`. Its PLA georeference checks are passed/deferred, not blocking. The remaining blocked state is caused by the early-check sequence not being fully complete.

#### Finding 8: PLA page selection is implemented, but it is buried inside the generic extraction workspace

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:404`, `ParcelWorkflowDockpaneViewModel.cs:407`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml:936`, `ParcelWorkflowDockpane.xaml:1006`

**Detail:** `ShowPlaPlanEvidenceSelection` is true only when `IsExtractionReviewStageActive` and the workflow profile is PLA. The visible section is still titled `Validate Points and Lines`.

#### Finding 9: PLA extraction requires page selection as a prerequisite

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs:54`, `CreateParcelDraftExtractionAdapter.cs:65`, `CreateParcelDraftExtractionAdapter.cs:71`

**Detail:** The PLA script path is `select_plan_annexation_pdf_page`, and extraction returns a failure if `pla_plan_evidence_selection.json` is missing or incomplete.

#### Finding 10: The generic early-check sequence does not match the examiner’s PLA mental model

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs:26`, `WorkflowSession.cs:397`, `WorkflowSession.cs:935`, `WorkflowSession.cs:2143`

**Detail:** The workflow requires structure, georeference, and dimension stages to pass before extraction can run. For PLA, georeference/dimension evidence is expected to be discovered after selecting the plan page and running OCR/vision/local-origin extraction.

### Deduced Conclusions

#### Deduction 2: A dedicated PLA UX is justified

**Based on:** Findings 7-10.

**Reasoning:** The code correctly detects PLA and has reusable PLA services, but the active workspace and status model still speak PE/PXA language. Because the user’s first PLA action is “select plan page/evidence,” showing Georeference Check and Dimension Check before the selected evidence exists is confusing even when technically nonblocking/deferred.

**Conclusion:** PLA should reuse the existing source classification, selected evidence service, OCR/vision extraction, review grid, local-origin solver, and finalize/upload services, but it should have a profile-specific workflow surface that exposes the PLA sequence directly.

### Recommended Fix Direction

Add a Story 2.23 UX adjustment before or as part of 2.23D:

1. When `workflow_profile = pla_plan_annexation`, show a PLA-specific workspace label and stepper.
2. Make `Select Plan Evidence` a first-class active step after Structure Check, not just a sub-panel inside `Validate Points and Lines`.
3. For PLA, hide or relabel pre-extraction Georeference/Dimension checks as deferred evidence gates, not examiner action gates.
4. Require saved `pla_plan_evidence_selection.json` before enabling `Run Extraction`.
5. Keep the shared extraction/review/solver/finalize services underneath the new surface.

### Updated Conclusion

**Confidence:** High

Yes, a new PLA-specific UX makes sense. It should not duplicate the processing engine; it should wrap the existing reusable services in the correct PLA examiner sequence: load PLA PDF, select plan evidence, extract/review local-origin geometry, compare visually, then Finalize/upload.

## Follow-up: 2026-08-26

### New Question

User reports that TR 100001219 now has a created polygon and closure passed in the Points Validation Tool, but only `Save` is enabled; `Validation Complete` is not available.

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json` | Available | Current manifest is `workflow_state = review_pending`, `workflow_profile = pla_plan_annexation`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\extraction_review_data.json` | Available | Current saved artifact is PLA local-origin review data with 5 rows, 8 segments, `boundary_solver.status = blocked`, and 13 memorandum rule results. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs` | Available | `Validation Complete` requires `HasLoadedReviewData`, not locked, no `ReviewHasBlockers`, and no manual edit mode; `Save` only requires dirty review data. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | `ReviewHasBlockers` included memorandum disposition blockers even for PLA, while Story 2.23D hides the Memorandum tab for PLA. |

### Confirmed Findings

#### Finding 11: `Save` and `Validation Complete` use different gates

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:142`, `JamaicaReviewWorkspaceViewModel.cs:150`, `JamaicaReviewWorkspaceViewModel.cs:156`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1380`

**Detail:** `Save` is enabled when loaded review data is dirty and unlocked. `Validation Complete` is enabled only when loaded review data is not locked, has no review blockers, and is not in manual edit mode.

#### Finding 12: TR 100001219 still has saved unresolved memorandum disposition rules

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\extraction_review_data.json:1145`, `extraction_review_data.json:1164`, `extraction_review_data.json:1178`, `extraction_review_data.json:1206`, `extraction_review_data.json:1220`, `extraction_review_data.json:1234`, `extraction_review_data.json:1248`, `extraction_review_data.json:1262`, `extraction_review_data.json:1304`, `extraction_review_data.json:1318`

**Detail:** The artifact includes multiple `workflow_effect = requires_disposition` memorandum rules with `needs_review` or `not_available` outcomes.

#### Finding 13: The PLA Memorandum tab is hidden, but the blocker gate still considered memorandum dispositions

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:202`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml:692`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1175`, `ParcelWorkflowDockpaneViewModel.cs:1177`

**Detail:** The UI correctly hides the Memorandum tab for PLA. Before the follow-up patch, `ReviewHasMemorandumDispositionBlockers` had no PLA exception, so hidden memorandum rules could keep `ReviewHasBlockers = true`.

### Deduced Conclusion

**Confidence:** High

The reason only `Save` was enabled is that the current review had unsaved edits, but `Validation Complete` was blocked by review blockers. The actionable defect is the hidden PLA memorandum blocker: PLA does not expose the Memorandum tab, so PLA must not require memorandum dispositions to complete validation.

### Fix Direction

Patch `ReviewHasMemorandumDispositionBlockers` to return false for `IsPlaPlanAnnexationReview`, while keeping memorandum disposition blocking intact for non-PLA PXA survey-plan reviews.

## Follow-up: 2026-08-26 #2

### New Question

User shared the Plan Annexation workflow screenshot showing `Validation blocked`, `Closure - blocker 1`, and footer text `Validation blocked: blocking findings require correction before outputs.`

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| Screenshot | Available | Shows active stage `Review Local-Origin Geometry`, Create Spatial Units status `Blocked`, and generic blocking copy. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\validation_summary.json` | Available | Validation status is `blocked`; closure distance is `0.0`, but area delta is `22.0399%` against `5.0%` tolerance. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | Summary text grouped the area mismatch under generic `Closure - blocker`, hiding the exact reason. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs` | Available | Footer status used generic copy: `blocking findings require correction before outputs`. |

### Confirmed Findings

#### Finding 14: The polygon closure distance passed; the blocker is area mismatch

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\validation_summary.json:31`, `validation_summary.json:37`, `validation_summary.json:47`, `validation_summary.json:48`, `validation_summary.json:49`, `validation_summary.json:50`

**Detail:** The closure result has `closure_distance_m = 0.0`, computed area `498.6795 sq m`, document area `408.62 sq m`, area delta `22.0399%`, and max area delta `5.0%`.

#### Finding 15: The UI copy made this look like ordinary closure failure

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1554`, `ParcelWorkflowDockpaneViewModel.cs:1561`, `ParcelWorkflowDockpaneViewModel.cs:1569`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1264`

**Detail:** The summary only showed `Closure - blocker 1` and the help/status text was generic, so it did not explain that the polygon closed but failed because generated area did not match the document area.

### Deduced Conclusion

**Confidence:** High

The next stage is blocked correctly by validation because the reviewed local-origin geometry area does not match the document area tolerance. The UX text was too generic and misleading; it needed to surface the first blocking validation finding and the area values.

### Fix Applied

Updated validation summary deserialization and UI text so blocked Create Spatial Units now reports closure/area details, including computed area, document area, delta, and tolerance. Replaced the footer copy with `Validation blocked: review blocking findings before Create Spatial Units.`

## Follow-up: 2026-08-26 #3

### New Question

User reports TR 100001219 boundary is OK, but only `Save` is enabled in the Points Validation Tool.

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json` | Available | Current run is `workflow_state = review_pending`, `workflow_profile = pla_plan_annexation`; this is a fresh review-stage run, not the earlier spatial-review-approved run. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\extraction_review_data.json` | Available | Boundary solver has `status = warning`, `closure_distance_m = 0`, `derived_point_count = 4`, `computed_area_sq_m = 498.6795`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\extraction_review_data.json` | Available | Reviewed segments are closed: `1->2`, `2->3`, `3->4`, `4->1`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\extraction_review_data.json` | Available | Review rows still include original OCR row `C` with blank easting/northing and sequence `5`, plus four derived rows `1`-`4`. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | Save/Approve previously applied PLA solver in merge mode, preserving inactive OCR/reference rows outside the reviewed segment chain. |

### Confirmed Findings

#### Finding 16: The boundary is closed; the stale OCR row is the review blocker

**Evidence:** `extraction_review_data.json` has `boundary_solver.closure_distance_m = 0`, reviewed segments `1->2`, `2->3`, `3->4`, `4->1`, and one non-derived row `C` with blank coordinates.

**Detail:** The generic review validator treats blank-coordinate rows as blockers. For PLA local-origin geometry, the reviewed boundary segment chain is the authoritative construction path after solver rebuild. Keeping row `C` in the active row list lets a stale OCR/reference row block `Validation Complete` even though the generated boundary points are complete.

### Deduced Conclusion

**Confidence:** High

The UI is correct to show `Save` while the review is dirty, but the old Save/Approve path did not clean the stale PLA OCR row. After Save, that stale row could keep approval disabled or blocked.

### Fix Applied

Patched PLA Save/Approve to run the boundary solver in explicit rebuild mode for PLA, replacing the active review row set with the closed reviewed segment chain and removing inactive OCR/reference/manual rows outside it. Non-PLA PXA keeps the existing merge behavior.

## Follow-up: 2026-08-26 #4

### New Question

User reports TR 100001219 is still blocked at Finalize. Screenshot shows Finalize panel message: `Finalize is blocked until PLA visual comparison review is accepted or flagged.`

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\manifest.json` | Available | Current workflow state is `spatial_review_approved`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\validation_summary.json` | Available | Current validation status is `passed`; closure/readiness/orientation all have zero blockers. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\spatial_review_approval.json` | Available | Spatial review was approved at `2026-08-26T02:58:24Z` and matches current output artifacts. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\pla_visual_comparison\pla_visual_comparison.json` | Missing | No native PLA visual comparison decision artifact exists. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\title_plan_overlay\title_plan_overlay_artifact.json` | Missing | No title-plan overlay fallback artifact exists in the current run. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\output\output_summary.json` | Available | Output package exists, but artifact paths list only `output\extracted_geometry.geojson`; no generated PLA PDF is present. |

### Confirmed Findings

#### Finding 17: The Finalize blocker is not validation or geometry

**Evidence:** `validation_summary.json` reports `status = passed`, closure blocker `0`, readiness blocker `0`, orientation blocker `0`; `manifest.json` reports `workflow_state = spatial_review_approved`.

**Detail:** The polygon and validation gates have passed. The blocker shown in the UI comes from `PlaFinalizeService.CheckReadiness(...)`, not from Create Spatial Units or spatial review.

#### Finding 18: The workflow stage and readiness artifact were out of sync

**Evidence:** `spatial_review_approval.json` exists and the UI marks `Visual Comparison` completed, but both `pla_visual_comparison.json` and `title_plan_overlay_artifact.json` are missing.

**Detail:** The stage model treated spatial-review approval as the completed PLA visual comparison, but the Finalize readiness service only accepted native PLA comparison metadata or title-plan overlay metadata.

### Deduced Conclusion

**Confidence:** High

Finalize is blocked because the readiness service cannot find accepted/flagged PLA visual-comparison evidence, even though spatial review approval exists. After bridging spatial-review approval into PLA visual-comparison readiness, the next likely blocker for this current case is missing generated PLA output PDF because `output_summary.json` lists only GeoJSON.

### Fix Applied

Patched `PlaVisualComparisonService.Load(...)` so, when native comparison metadata and title-plan overlay metadata are absent, it accepts a current `spatial_review_approval.json` plus matching `output_summary.json` as accepted PLA visual-comparison evidence. Added regression coverage and packaged add-in version `1.1.227`.

## Follow-up: 2026-08-26 #5

### New Question

User reports TR 100001219 must be completed and asks why Create Spatial Units is blocked by `Reviewed boundary area differs from the document area...`.

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\validation_summary.json` | Available | Validation is blocked only by area mismatch: computed area `498.6795`, document area `408.62`, delta `22.0399%`. Closure distance is `0.0`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\extraction_review_data.json` | Available | `survey_metadata.document_area.value = "408.62 square feet"` and review note says the numeric value/unit could not be parsed deterministically. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | `TryReadAreaValue(...)` stripped non-numeric characters and parsed the first number, passing `408.62` into `documentAreaSqM`. |

### Confirmed Finding

#### Finding 19: Square-foot text was treated as square metres

**Evidence:** `extraction_review_data.json` says `408.62 square feet`; `validation_summary.json` says `document_area_sq_m = 408.62`.

**Detail:** The validator compared local-origin computed area `498.68` against `408.62` as though both were square metres. The source area text is square feet, and the extractor already marked it `needs_review` because the unit/value was not deterministic.

### Deduced Conclusion

**Confidence:** High

The blocker is a unit parsing defect. The polygon closure passed. The area comparison should not use square-foot OCR text as a square-metre document area.

### Fix Applied

Patched document-area parsing so text/unit values containing square-foot units are ignored for square-metre validation comparison. Build and focused tests passed; packaged add-in version `1.1.229`.

## Follow-up: 2026-08-26 #6

### New Question

User reports TR 100001219 is at Finalize but cannot move to the next stage and asks whether all documents are ready to attach.

### Added Evidence

| Source | Status | Notes |
| --- | --- | --- |
| Screenshot | Available | Finalize panel shows `Blocked` with `Finalize is blocked until at least one generated PLA output PDF exists.` |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\validation_summary.json` | Available | Validation status is `passed`; closure/readiness/orientation have zero blockers. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\working\spatial_review_approval.json` | Available | Spatial review is approved for the current output created at `2026-08-26T03:37:41Z`. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\output\output_summary.json` | Available | Before repair, `artifact_paths` contained only `output\extracted_geometry.geojson`; no generated `.pdf` was listed. |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\output\reports` | Available | Directory existed but was empty before repair. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaFinalizeService.cs` | Available | `CheckReadiness(...)` blocks with `pla_output_documents_missing` when `ResolveGeneratedOutputDocuments(...)` finds no existing in-case `.pdf` from `output_summary.payload.artifact_paths`. |
| `src/ProcessingTools/adapters/output_adapter.py` | Available | `_build_summary(...)` only added GeoJSON and optional review dataset artifacts; it did not produce/register any generated PLA PDF. |

### Confirmed Findings

#### Finding 20: The current Finalize block is correct

**Evidence:** Validation passed and spatial review was approved, but `output_summary.payload.artifact_paths` did not contain any generated PDF.

**Detail:** The case had geometry outputs and approval evidence, but not an attachable PLA output document. The only PDF in the case was the original source attachment under `source\1000-55.pdf`, which Finalize must not upload as a generated output.

#### Finding 21: The producer side was incomplete for PLA

**Evidence:** `PlaFinalizeService` requires a current PDF listed in `output_summary.payload.artifact_paths`; `output_adapter.py` generated only GeoJSON/GDB artifacts for this PLA run.

**Detail:** Previous tests fabricated PLA output PDFs for Finalize, proving the upload path, but the real output adapter did not create those PDFs. This made every real PLA case reach Finalize with the correct blocking message and no document to attach.

### Deduced Conclusion

**Confidence:** High

TR 100001219 could not move forward because the generated PLA output PDF was missing, not because geometry, validation, or visual comparison was still blocked.

### Fix Applied

Patched `output_adapter.py` so PLA output generation creates `output/reports/pla_plan_annexation_output.pdf` and registers it in `output_summary.payload.artifact_paths`. Added a Python regression test proving a PLA run emits an attachable PDF artifact. Packaged add-in version `1.1.232`.

### Live Case Repair

Backfilled TR 100001219 without touching the locked output GDB:

- Created `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\output\reports\pla_plan_annexation_output.pdf`.
- Updated `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001219\output\output_summary.json` so `artifact_paths` now includes the generated PDF after `output\extracted_geometry.geojson`.

## Follow-up: 2026-08-26 #7

### New Question

User clarified the required PLA output document contract and asked whether it exists in the stories:

1. `st_plan_annex_output`: the page selected in the form from the input `st_plan_annexation_pdf`; only that selected page is extracted and attached.
2. `st_plan_annex_output2`: the generated geometry document built by the user/system from bearings and distances in the selected plan-annexation PDF.
3. `st_plan_annex_output3`: to be defined.

### Confirmed Findings

#### Finding 22: The previous single-PDF repair was incomplete for the clarified contract

**Evidence:** Follow-up #6 created and registered `pla_plan_annexation_output.pdf` as a generic attachable PLA PDF. The clarified contract requires two defined output PDFs in order, with different content responsibilities.

**Detail:** A single summary PDF must not be treated as `st_plan_annex_output`, because output1 is specifically the examiner-selected source page extracted from `st_plan_annexation_pdf`. The generated geometry belongs to `st_plan_annex_output2`, not output1.

#### Finding 23: The stories now explicitly define the output mapping

**Evidence:** Story 2.23 AC20, Story 2.23A business context/tasks, and Story 2.23D AC9/AC13 now state that output1 is the selected source page, output2 is generated geometry, and output3 is reserved/undefined.

### Deduced Conclusion

**Confidence:** High

TR 100001219 cannot be considered final-ready under the clarified contract until the output summary lists both current PDFs in order:

1. `output\reports\pla_selected_plan_page.pdf`
2. `output\reports\pla_generated_geometry.pdf`

The stale `pla_plan_annexation_output.pdf` evidence from Follow-up #6 is superseded by this clarification.

### Fix Applied

Patched `output_adapter.py` so PLA output generation extracts the selected page from `st_plan_annexation_pdf` into `pla_selected_plan_page.pdf`, generates `pla_generated_geometry.pdf` from the reviewed geometry data, and registers those two PDFs in `output_summary.payload.artifact_paths` in Finalize upload order. Patched `PlaFinalizeService` so Finalize requires exactly the two currently defined PLA outputs and blocks clearly if only one exists or if an undefined output3 is present.
