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
