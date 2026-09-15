# Investigation: TR 100001017 ArcGIS Pro Save/Rebuild Crash

## Hand-off Brief

1. **What happened.** ArcGIS Pro crashed while reviewing TR `100001017` because a WPF command path threw an unhandled `System.ArgumentException`: `An item with the same key has already been added. Key: 66`.
2. **Where the case stands.** The root cause is confirmed: `extraction_review_data.json` has no explicit `row_id` values, the loader falls back to `point_identifier`, and this two-parcel review repeats point identifiers `66` through `71` across groups.
3. **What's needed next.** Fix review row identity to be unique per row, ideally by generating stable row ids from parcel/traverse/sequence when source JSON omits `row_id`, and harden the resync dictionary against duplicate ids.

## Case Info

| Field | Value |
| ----- | ----- |
| Ticket | TR 100001017 |
| Date opened | 2026-09-15 |
| Status | Concluded |
| System | ArcGIS Pro `13.6.0.59527`, add-in loaded from ESRI ArcGISPro AssemblyCache |
| Evidence sources | ESRI dump, TR case folder, review workspace diagnostics, source code |

## Problem Statement

User asked why the error happens in TR `100001017` and provided dump path `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro\_13.6.0.59527\_0\_09\_15\_2026\_10\_09\_39.dmp`.

The exact nested path did not exist from this shell. The actual ESRI dump was found at `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_09_15_2026_10_09_39.dmp`.

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| ESRI dump | Available | Actual flat dump file exists at `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_09_15_2026_10_09_39.dmp`, length `9,751,279`, last write `2026-09-15 10:09:39`. |
| Repeated ESRI dumps | Available | Same morning dumps found at `10:02:45`, `10:06:39`, and `10:09:39`. |
| Review diagnostics | Available | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001017\working\review_workspace_diagnostics.jsonl` captured multiple `dispatcher_unhandled_exception` entries. |
| Review data | Available | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001017\working\extraction_review_data.json` contains 12 rows, no persisted `row_id`, and duplicate point identifiers across two parcel groups. |
| Source code | Available | Relevant paths are in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn`. |
| Windows Application event log | Partial | No matching Application event entries surfaced in the queried window. The add-in diagnostics are more specific. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-09-15 10:01:37 -05:00 | Review workspace opened for transaction `100001017`, stage `ExtractionReview`, transaction type `Compute Survey Plan`. | `review_workspace_diagnostics.jsonl` | Confirmed |
| 2026-09-15 10:02:12 -05:00 | First unhandled `ArgumentException` occurred on Rebuild Boundary Points. | `review_workspace_diagnostics.jsonl:15` | Confirmed |
| 2026-09-15 10:06:13 -05:00 | Same exception repeated during Rebuild Boundary Points. | `review_workspace_diagnostics.jsonl:32-34` | Confirmed |
| 2026-09-15 10:08:44 -05:00 | Same exception repeated during Save after switching source to `Document_2.pdf`. | `review_workspace_diagnostics.jsonl:40` | Confirmed |
| 2026-09-15 10:09:39 | ESRI dump written. | `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_09_15_2026_10_09_39.dmp` | Confirmed |

## Confirmed Findings

### Finding 1: The crash is a managed duplicate-key exception

**Evidence:** `review_workspace_diagnostics.jsonl:40`; dump string extraction from `ArcGISPro_13.6.0.59527_0_09_15_2026_10_09_39.dmp`.

**Detail:** The exception is `System.ArgumentException`, HRESULT `0x80070057`, message `An item with the same key has already been added. Key: 66`.

### Finding 2: The failing source line builds a dictionary from review UI rows by `RowId`

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2296`.

**Detail:** `SyncReviewRowViewModelsFromDocument()` runs `ReviewRows.Where(row => !string.IsNullOrWhiteSpace(row.RowId)).ToDictionary(row => row.RowId, StringComparer.Ordinal);`. `ToDictionary` throws if any two `ReviewRows` share the same `RowId`.

### Finding 3: The exception is triggered by Rebuild Boundary Points and Save

**Evidence:** `review_workspace_diagnostics.jsonl:15`, `review_workspace_diagnostics.jsonl:40`; `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2213`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3229`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3343`.

**Detail:** Both user flows call `ApplyBoundarySolverIfAvailable()`, which applies the boundary solver and then calls `SyncReviewRowViewModelsFromDocument()`.

### Finding 4: TR 100001017's persisted review rows omit `row_id`

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001017\working\extraction_review_data.json`.

**Detail:** Representative rows include `parcel_group_id`, `point_identifier`, `point_id`, coordinates, sequence, and review fields, but no `row_id`.

### Finding 5: The loader falls back to point identifier when `row_id` is missing

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs:342`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs:354`.

**Detail:** `MapRow()` sets `RowId = ReadFirstString(rowObject, "row_id", "review_row_id") ?? pointIdentifier ?? $"row-{index:000}"`.

### Finding 6: TR 100001017 repeats point identifiers across parcel groups

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001017\working\extraction_review_data.json`.

**Detail:** `point_identifier` values `66`, `67`, `68`, `69`, `70`, and `71` each appear twice. One group is `Lot 1`; the other is `parcel-001`. Because `row_id` is absent, both point `66` rows become `RowId = "66"`.

## Deduced Conclusions

### Deduction 1: The crash is not caused by ArcGIS native geometry, WebView, or PDF rendering

**Based on:** Findings 1, 2, and 3.

**Reasoning:** The captured exception stack is fully managed WPF/add-in code and terminates at `ToDictionary(row => row.RowId)`. The active source/viewer context is incidental to the save/rebuild command path.

**Conclusion:** The failure is an add-in review-row identity bug.

### Deduction 2: The transaction data is valid enough to expose an identity assumption bug

**Based on:** Findings 4, 5, and 6.

**Reasoning:** Multi-parcel reviews can legitimately reuse point labels per parcel/traverse. The loader treats point labels as globally unique row ids when `row_id` is absent.

**Conclusion:** The defect is the global row-id fallback, not merely the presence of repeated point numbers in TR `100001017`.

## Hypothesized Paths

### Hypothesis 1: Boundary-solver resync crashes whenever a multi-parcel review lacks explicit row ids and repeats point labels

**Status:** Confirmed

**Theory:** Rebuild/Save invokes boundary solving, then resync creates `knownRowsById` from duplicate fallback row ids and throws.

**Supporting indicators:** Duplicate `point_identifier = 66` exists across parcel groups; stack points to `SyncReviewRowViewModelsFromDocument()` line `2296`.

**Would confirm:** A test fixture with two rows lacking `row_id` and sharing `point_identifier` reproduces `ArgumentException` on rebuild/save.

**Would refute:** A fixture with the same duplicate point labels but stable unique `row_id` values does not crash.

**Resolution:** Confirmed by diagnostics, dump strings, persisted review data, and source-code path.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| Automated repro test | Would make the fix regression-safe | Add a unit/harness test around review loading plus `SyncReviewRowViewModelsFromDocument()` or the public rebuild/save path. |
| Exact OCR/extraction adapter row-id behavior | Would show whether row ids should be emitted earlier | Inspect adapter output for `working/extraction_points.json` or adapter code path. Not required for root-cause diagnosis. |

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:2296` |
| Trigger | User clicks Rebuild Boundary Points or Save in `JamaicaReviewWorkspaceWindow` |
| Condition | Existing `ReviewRows` contains duplicate `RowId = "66"` because `row_id` is absent and `point_identifier` repeats across parcel groups |
| Related files | `ExtractionReviewPersistenceService.cs`, `ExtractionReviewRowViewModel.cs`, `SurveyPlanBoundarySolver.cs`, `JamaicaReviewWorkspaceWindow.xaml.cs` |

## Conclusion

**Confidence:** High

TR `100001017` crashes because the review data has two parcels with reused point numbers and no explicit row ids. The loader turns `point_identifier` into `RowId`, so both parcel groups produce `RowId = "66"` and other duplicate ids. When Rebuild Boundary Points or Save runs, the add-in calls `SyncReviewRowViewModelsFromDocument()`, builds a dictionary keyed by `RowId`, and `ToDictionary` throws an unhandled duplicate-key exception.

## Recommended Next Steps

### Fix direction

Generate stable, unique row ids when loading review rows that lack `row_id`. A safe fallback should include row context, for example parcel group/traverse/sequence/point identifier, instead of just `pointIdentifier`.

Also harden `SyncReviewRowViewModelsFromDocument()` so duplicate row ids cannot crash ArcGIS Pro. It should group or select deterministically and surface a validation diagnostic if duplicate persisted ids are genuinely present.

### Diagnostic

Add a focused test fixture with two rows:

- `parcel_group_id = Lot 1`, `point_identifier = 66`, no `row_id`
- `parcel_group_id = parcel-001`, `point_identifier = 66`, no `row_id`

The test should prove load/rebuild/save does not throw and produces distinct row identities.

## Reproduction Plan

1. Open TR `100001017` case folder.
2. Open Points Validation Tool.
3. Click Rebuild Boundary Points, or change review state and click Save.
4. Current behavior: ArcGIS Pro/add-in records `System.ArgumentException: An item with the same key has already been added. Key: 66`.
5. Fixed behavior: no crash; both parcel rows remain represented with unique row ids and the user sees any boundary-solver findings normally.

## Side Findings

- The user-provided dump path used a nested `ArcGISPro\_13.6...` shape, but the actual ESRI dump is stored as a flat filename under `C:\Users\js91482\AppData\Local\ESRI\ErrorReports`.
- The transaction manifest classifies TR `100001017` as `Compute Survey Plan`, case type/profile hint `PE`, with workflow profile `scenario_a_two_pdf`.
