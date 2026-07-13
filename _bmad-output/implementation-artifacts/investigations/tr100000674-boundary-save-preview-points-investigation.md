# Investigation: TR100000674 Boundary Save Does Not Refresh Points or Preview

## Hand-off Brief

1. **What happened.** The user added a manual boundary segment in TR100000674 and reported that pressing Save did not update the parcel preview or Points tab.
2. **Where the case stands.** The saved case artifact confirms the manual segment is persisted and the solver recalculated derived rows, but the solver remains blocked because the reviewed chain reuses points 1 and 2 and leaves a 44.969 m closure gap.
3. **What's needed next.** Keep the UI refresh patch, and treat the remaining parcel geometry as a data/chain issue: the boundary segment sequence needs to form one valid closed chain before Create Spatial Units can proceed.

## Case Info

| Field | Value |
| ----- | ----- |
| Ticket | TR100000674 |
| Date opened | 2026-07-13 |
| Status | Concluded |
| System | Windows workspace, ArcGIS Pro add-in repo `pe-jamaica` |
| Evidence sources | Local case folder, source code, test run |

## Problem Statement

User-reported description: "i have added a new boundary segment. once i press save, teh parcel view was not updated, same as the points tab. Please review for Repo TR100000674".

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000674\working\extraction_review_data.json` | Available | Contains 10 rows, 11 segments, saved manual segment 44 -> 2, and boundary solver metadata. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | Solver sync and parent notification path reviewed and patched. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs` | Available | Row refresh method reviewed and patched. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs` | Available | Parcel preview rebuild path reviewed. |

## Confirmed Findings

### Finding 1: The manual segment is saved in the TR100000674 artifact

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000674\working\extraction_review_data.json`, lines around 1199-1210.

**Detail:** Segment 11 is saved as manual entry `44 -> 2`, bearing `N84 50W`, distance `38.099m`, included in boundary.

### Finding 2: The boundary solver is still blocked after the manual segment

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000674\working\extraction_review_data.json`, lines around 1260-1276.

**Detail:** Solver metadata reports `status: blocked`, `closure_distance_m: 44.968999999962`, and findings that point 1 and point 2 are referenced more than twice in the reviewed boundary chain.

### Finding 3: Points were recalculated and persisted for derived rows

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000674\working\extraction_review_data.json`, rows table inspection.

**Detail:** Rows include derived coordinates for points 2, 10, 11, 12, 44, 45, 46, and 47. Point 44 is persisted as `707328.314, 669768.767` from reviewed segment `47->44`.

### Finding 4: Parcel preview is driven from point rows, not directly from segment geometry

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:969`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:1007`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:1123`.

**Detail:** `BuildPreviewPoints()` orders review rows by segment point IDs and scales parsed easting/northing values. It does not draw the segment line itself when the solver remains blocked.

### Finding 5: Existing row refresh needed a parent `ReviewRows` notification for workspace preview redraw

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:716`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:780`.

**Detail:** The PXA workspace refreshes parcel preview and context paths when the parent raises `ReviewRows`. Row-level property changes alone are not enough for the calculated `ParcelPreviewPoints` property to be re-requested.

## Deduced Conclusions

### Deduction 1: Save is not losing the manual segment

**Based on:** Findings 1 and 2.

**Reasoning:** The saved artifact contains the added segment and updated solver metadata, so the persistence path is working.

**Conclusion:** The symptom is not a failed save; the saved state remains blocked by the reviewed boundary chain.

### Deduction 2: The UI could remain stale even after solver row changes

**Based on:** Findings 3, 4, and 5.

**Reasoning:** The solver can change existing row values without changing the row collection count. Without refreshing existing row view models and raising `ReviewRows`, the Points tab and calculated parcel preview may keep showing old values.

**Conclusion:** The row refresh and parent notification patch is needed.

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | Existing rows changed by solver were not forcing parent `ReviewRows` notification. |
| Trigger | Add/edit/save boundary segment in PXA survey plan review. |
| Condition | Solver mutates existing derived rows or sequence, but row count remains unchanged. |
| Related files | `ExtractionReviewRowViewModel.cs`, `ParcelWorkflowDockpaneViewModel.cs`, `JamaicaReviewWorkspaceViewModel.cs`. |

## Conclusion

**Confidence:** High

TR100000674 saved the manual boundary segment and recalculated derived point rows, but the reviewed boundary chain remains blocked because the added `44 -> 2` segment causes point 2 to be referenced more than twice and still leaves a 44.969 m closure gap. A UI refresh defect also existed: existing row updates did not notify the parent `ReviewRows` property, so the Points tab/parcel preview could remain stale until reload.

## Recommended Next Steps

### Fix direction

Completed in code: refresh existing `ExtractionReviewRowViewModel` values from the model, notify `ReviewRows` when solver synchronization changes rows, and include `ReviewRows` in general workflow property refresh.

### Diagnostic

For TR100000674, adjust the boundary segment sequence so the included segments form one closed chain without repeating point 1 or point 2 as internal branch points. The solver blocker message should clear before Create Spatial Units can be expected to succeed.

## Reproduction Plan

1. Load TR100000674.
2. Add or edit a boundary segment that causes the solver to update an existing derived point row.
3. Save the review.
4. Expected after patch: Points tab and parcel preview are notified immediately.
5. Expected for the current artifact: solver remains blocked until the chain no longer repeats point 1/point 2 and closure distance is within tolerance.

## Verification

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed with elevated temp access: 355 tests.

## Follow-up: 2026-07-13

### New Evidence

The user challenged the preview diagnosis with the screenshot: segment 11 is `44 -> 2`, so the preview should show the examiner-entered closing edge even if the solver still reports a blocked chain.

### Additional Findings

Confirmed: `BuildPreviewRowsInReviewedSegmentOrder()` previously de-duplicated point IDs while walking reviewed segments. For TR100000674, point `2` appears in segment 1 and again as the manual segment endpoint `44 -> 2`; the second occurrence was skipped. `ClosePreviewRingIfNeeded()` then added a phantom closure back to the first point, so the preview did not reflect the actual last segment.

### Updated Conclusion

The manual segment is saved, and the user is correct that the preview should reflect `44 -> 2`. The solver may still flag repeated point references for validation, but the parcel preview should render the reviewed segment chain as entered.

### Backlog Changes

Done: changed the PXA preview to preserve repeated segment endpoints and avoid auto-closing segment-driven previews back to the first point unless the reviewed segment chain itself returns there.

### Verification

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed with elevated temp access: 355 tests.

## Follow-up: 2026-07-13 #2

### New Evidence

The user asked why the workspace still shows "Closure Blocked. Tolerance profile: Standard closed parcel tolerance for compute review" and why `Validation Complete` is not available.

### Additional Findings

Confirmed: TR100000674 uses `geometry_mode = single_parcel_survey_plan`, `validation_profile = single_parcel_survey_plan_v1`, and `review_mode = point_line_review`.

Confirmed: The persisted boundary solver metadata is still `status = blocked`, with `closure_distance_m = 44.968999999962` and findings that point `1` and point `2` are each referenced more than twice.

Confirmed: The included segment list has point-reference counts `1 = 3` and `2 = 3`. All other boundary points are referenced twice. This happens because both the original segment `1 -> 2` and the new/manual chain ending `44 -> 2` are included.

Confirmed: `Validation Complete` is enabled only when the parent review has no blockers. The parent review treats a blocked boundary solver as a blocker before running the final approval validation.

Confirmed: The "Standard closed parcel tolerance for compute review" label is a humanized closure profile label for `closure_standard_plan_exam` / `closure_standard_compute_review`. It is the standard closed-parcel tolerance, not a separate transaction error.

### Updated Conclusion

The parcel can visually appear closed after adding `44 -> 2`, but with all listed segments set to `Use`, the app does not have one simple closed exterior ring. It has an extra included `1 -> 2` segment that makes points `1` and `2` branch/interior/shared vertices. The solver blocks because standard closed parcel review expects each boundary vertex to participate in a single ring, normally two references per point.

### Diagnostic

The likely examiner action for this case is to exclude the original `1 -> 2` segment from `Use` if it is an internal/shared line, leaving the exterior ring as:

`2 -> 9 -> 10 -> 11 -> 12 -> 1 -> 45 -> 46 -> 47 -> 44 -> 2`

After saving, the solver blocker about points `1` and `2` being referenced more than twice should clear if that is the intended outer parcel boundary.
