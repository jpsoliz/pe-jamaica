# Investigation: TR100000688 Boundary Save Does Not Refresh Points

## Hand-off Brief

1. **What happened.** The user reports that for TR100000688, saving a boundary segment does not update the Points tab.
2. **Where the case stands.** The local TR100000688 case folder is missing, but source evidence confirms the boundary solver can update the in-memory review document while existing point row view models are skipped during UI sync.
3. **What's needed next.** Patch the review row sync path so existing `ExtractionReviewRowViewModel` instances refresh from their mutated models after the boundary solver runs.

## Case Info

| Field            | Value                                                                 |
| ---------------- | --------------------------------------------------------------------- |
| Ticket           | TR100000688                                                           |
| Date opened      | 2026-07-13                                                            |
| Status           | Concluded                                                             |
| System           | Windows workspace, ArcGIS Pro add-in repo `pe-jamaica`                |
| Evidence sources | Source code, local case-folder inventory                              |

## Problem Statement

User-reported description: "review repo fpr TR100000688 as once i save the boundary, the points has not been updated".

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000688` | Missing | `Test-Path` returned `False`; parent case store lists nearby cases but not `100000688`. |
| Repo text search for `100000688` / `TR100000688` | Missing | No repository hits for the transaction ID. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` | Available | Boundary edit/save and review row sync code inspected. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs` | Available | Solver mutation behavior inspected. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs` | Available | Row view model refresh behavior inspected. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs` | Available | Workspace projection refresh behavior inspected. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Obtain TR100000688 case folder or exported review JSON | Medium | Blocked | Needed only to prove whether persisted JSON is stale too. |
| 2 | Patch existing-row refresh after solver mutation | High | Open | Source trace confirms this path can leave the current UI stale. |
| 3 | Add regression test for existing derived row refresh | High | Open | Should verify changed derived coordinates/status appear in view models after solver apply. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| Unknown | User edited/saved a boundary segment for TR100000688 and observed Points tab did not update. | User report | Hypothesized |
| 2026-07-13 | Local case folder for `100000688` was not present in the normal case store. | Shell inventory | Confirmed |
| 2026-07-13 | Source trace found boundary edit invokes the solver, but existing point view models are skipped by sync. | Source code | Confirmed |

## Confirmed Findings

### Finding 1: Boundary segment edit invokes the boundary solver

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2002`

**Detail:** `EditReviewSegment` calls `targetSegment.SyncBackToModel()` and then `ApplyBoundarySolverIfAvailable()` before marking the review dirty.

### Finding 2: Save also invokes the boundary solver before persisting

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2199`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2210`

**Detail:** Save syncs all visible point rows and segments back to the model, syncs metadata, then calls `ApplyBoundarySolverIfAvailable()` before `SaveExtractionReview`.

### Finding 3: The solver does mutate existing point rows when they are derived or missing coordinates

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs:242`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs:246`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs:250`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs:378`

**Detail:** For an existing point ID, the solver applies derived coordinates when the row is already solver-derived or lacks parseable easting/northing.

### Finding 4: Existing point row view models are skipped during solver-to-UI sync

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1580`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1586`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1591`

**Detail:** `SyncReviewRowViewModelsFromDocument()` builds a set of existing row IDs, `continue`s when a document row already has a known ID, and only adds new row view models for unknown row IDs.

### Finding 5: The row view model has no method to refresh cached display fields from its model

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs:25`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs:157`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs:181`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs:186`

**Detail:** The constructor copies model values into private display fields, and the class supports syncing UI edits back to the model, sequence refresh, and committed point edits. It does not support reloading cached fields after another service mutates the model.

### Finding 6: The workspace projection refreshes on collection-level changes, not silent model mutations

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:652`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:716`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs:806`

**Detail:** The workspace rebuilds visible point groups when the `ReviewRows` collection changes or the parent raises the `ReviewRows` property. Existing row model changes without row property notifications will not automatically rebuild or redraw the Points tab.

## Deduced Conclusions

### Deduction 1: The immediate Points tab can remain stale after boundary dialog save

**Based on:** Findings 1, 3, 4, 5, and 6.

**Reasoning:** Boundary edit runs the solver, the solver may mutate existing document rows, but the sync method ignores rows whose IDs already exist. Because the view model caches fields and does not refresh them from the model, WPF remains bound to the previous values.

**Conclusion:** The reported "points not updated" behavior is explained by a confirmed UI refresh gap for existing rows.

### Deduction 2: Bottom-level Save should reload the document after a successful persistence

**Based on:** Findings 2 and `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2212`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2218`.

**Reasoning:** Successful save assigns the returned document and calls `LoadReviewDocumentIntoPane`, which clears and recreates all row view models.

**Conclusion:** If the user still sees stale points after pressing the main Save button and save succeeds, the missing TR100000688 case artifacts are needed to check whether the solver was blocked or persistence returned stale data. The code defect confirmed here primarily explains stale points immediately after segment edit/add.

## Hypothesized Paths

### Hypothesis 1: TR100000688 persisted JSON is stale

**Status:** Open

**Theory:** The solver may be blocked or the persisted review file may not contain updated coordinates.

**Supporting indicators:** User says "once i save the boundary"; exact action may refer to the main Save button rather than the segment editor OK button.

**Would confirm:** TR100000688 `extraction_review_data.json` after save showing unchanged rows and blocked/missing `boundary_solver` metadata.

**Would refute:** TR100000688 saved JSON contains updated points while the UI remains stale.

**Resolution:** Blocked by missing local case folder.

### Hypothesis 2: The observed issue is only an in-session UI refresh gap

**Status:** Confirmed for the source-code path; transaction-specific confirmation still blocked.

**Theory:** The solver updates the loaded document, but existing row view models keep their cached values until a full reload.

**Supporting indicators:** `SyncReviewRowViewModelsFromDocument()` explicitly skips known row IDs and `ExtractionReviewRowViewModel` lacks a model refresh method.

**Would confirm:** A regression test where an existing solver-derived row changes coordinates after segment edit and the view model still displays old coordinates.

**Would refute:** Existing row view models receive property changes from another path not found in this trace.

**Resolution:** Source trace confirms the mechanism.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| TR100000688 case folder or exported review JSON | Determines whether persisted data is also stale or only the UI is stale before reload. | Provide/copy `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000688` or rerun the transaction locally. |
| Exact user action labeled "save boundary" | Distinguishes segment dialog OK from main Save button. | Repro note or screen recording. |

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | `SyncReviewRowViewModelsFromDocument` skips existing row IDs in `ParcelWorkflowDockpaneViewModel.cs:1586`. |
| Trigger | Add/edit boundary segment calls `ApplyBoundarySolverIfAvailable`. |
| Condition | Solver mutates an existing point row rather than adding a new row. |
| Related files | `SurveyPlanBoundarySolver.cs`, `ExtractionReviewRowViewModel.cs`, `JamaicaReviewWorkspaceViewModel.cs`. |

## Conclusion

**Confidence:** Medium

The root cause for points not updating immediately after boundary segment save is confirmed in source: the boundary solver can mutate existing review rows, but the UI sync path only appends new rows and never refreshes cached fields for existing row view models. Confidence is Medium rather than High only because the TR100000688 case folder is not present locally, so persisted transaction-specific JSON could not be inspected.

## Recommended Next Steps

### Fix direction

Add a refresh method to `ExtractionReviewRowViewModel` that reloads all cached display fields from `Model` and raises property changes for point identifier, easting, northing, length, status, source evidence, unresolved fields, notes, sequence, edited state, and missing-required state. In `SyncReviewRowViewModelsFromDocument()`, map existing row IDs to row view models and call that refresh method instead of skipping them.

### Diagnostic

After the patch, test a case where an existing solver-derived point row receives new coordinates from a boundary segment edit. Verify the Points tab changes without requiring a full workspace reload.

## Reproduction Plan

1. Load a PXA survey-plan review with boundary segments and existing derived/missing point rows.
2. Edit a boundary segment bearing/distance that changes a derived point coordinate.
3. Accept the segment editor.
4. Open the Points tab.
5. Expected after fix: existing affected points show the updated easting/northing/status immediately.

## Side Findings

- Confirmed: The normal local case directory contains cases such as `100000668` and `100000674`, but not `100000688`, so this investigation could not inspect transaction-specific output artifacts.
