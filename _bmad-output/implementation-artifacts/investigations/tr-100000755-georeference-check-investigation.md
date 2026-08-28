# Investigation: TR 100000755 Georeference Check Blocker

## Hand-off Brief

1. **What happened.** TR `100000755` blocked at Georeference Check with `georeference_spatial_validation_readiness`, even though the survey plan visibly contains JAD2001 coordinate evidence.
2. **Where the case stands.** The case artifacts confirm the extractor recorded parish `SAINT ANN` and five point rows, but recorded `coordinate_system = Theodolite Survey (Compass Standard)` instead of the visible `JAD 2001` label above the coordinate table.
3. **What's needed next.** Patch the OCR/vision extraction prompt or normalization so survey method cannot be used as coordinate system, and so visible `JAD 2001`/`J.A.D. 2001` near coordinate tables is captured as coordinate-system evidence.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | N/A |
| Date opened | 2026-08-28 |
| Status | Active |
| System | ArcGIS Pro add-in, PXA/Compute workflow, local case folder |
| Evidence sources | User screenshot, TR `100000755` case artifacts, rendered source PDF page, source code |

## Problem Statement

User reports that TR `100000755` blocks on Georeference Check even though the survey looks clear.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| User screenshot | Available | Shows `Georeference Spatial Validation Readiness` failed with one blocker. |
| Case manifest | Available | `manifest.json` records transaction `100000755`, task `Assign Computation Task`, case type/profile `PXA`, and source `PLAN_DOC_486024.pdf` as `survey_plan_pdf`. |
| Georeference summary | Available | `georeference_check_summary.json` records the blocker and its evidence payload. |
| Extraction summary | Available | `survey_plan_extraction_summary.json` records extracted coordinate system, parish, point count, and stage evidence. |
| Extraction review data | Available | `extraction_review_data.json` records two numeric coordinate rows and three generated/review-required intermediate rows. |
| Rendered PDF page | Available | The page visibly shows `JAD 2001` above the coordinate table. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Compare georeference summary against extraction summary | High | Done | Blocker evidence contains coordinate system, parish, and point count. |
| 2 | Trace readiness rule condition | High | Done | Rule passes only when coordinate system normalizes to JAD2001 and point count is greater than zero. |
| 3 | Inspect source PDF visual evidence | High | Done | `JAD 2001` is visible above the coordinate table. |
| 4 | Patch extraction prompt/normalizer and add fixture test | High | Open | Needed to prevent survey method from filling coordinate-system field. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-08-28T03:13:00Z | Source PDF `PLAN_DOC_486024.pdf` copied into case folder. | `manifest.json` | Confirmed |
| 2026-08-28T03:13:36Z | Structure Check passed. | `structure_check_summary.json` | Confirmed |
| 2026-08-28T03:15:22Z | Georeference Check blocked. | `georeference_check_summary.json` | Confirmed |

## Confirmed Findings

### Finding 1: The blocker is `georeference_spatial_validation_readiness`

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000755\working\georeference_check_summary.json`

**Detail:** The blocker message is `Survey plan extraction summary exists, but coordinate system, parish, and coordinate table evidence are still low-confidence or missing.`

### Finding 2: The blocker evidence is not empty

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000755\working\georeference_check_summary.json`

**Detail:** The evidence payload includes `coordinate_system = Theodolite Survey (Compass Standard)`, `parish = SAINT ANN`, and `coordinate_table_point_count = 5`.

### Finding 3: Only two point rows have numeric coordinates

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000755\working\extraction_review_data.json`

**Detail:** Rows `152` and `32` have numeric easting/northing; generated rows `1`, `2`, and `3` have null coordinates.

### Finding 4: The PDF visibly contains JAD2001 coordinate evidence

**Evidence:** Rendered `PLAN_DOC_486024.pdf` page 1.

**Detail:** The coordinate table in the lower-left has a visible handwritten `JAD 2001` label above it.

### Finding 5: The code only accepts coordinate-system values that normalize to JAD2001

**Evidence:** `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Preflight\ManifestPreflightService.cs`

**Detail:** `SurveyPlanExtractionEvidence.HasGeoreferenceEvidence` requires `IsJad2001CoordinateSystem(CoordinateSystem) && PointCount > 0`; `IsJad2001CoordinateSystem` strips non-alphanumeric characters and checks for `JAD2001`.

### Finding 6: The extractor prompt asks for both coordinate system and survey method but does not explicitly prevent mixing them

**Evidence:** `src\ProcessingTools\adapters\survey_plan_ocr_vision_extraction.py`

**Detail:** The prompt asks for top-level `coordinate_system` and `survey_metadata.survey_method`; the normalization then accepts `raw["coordinate_system"]` as-is. In this case, the model placed `Theodolite Survey (Compass Standard)` into `coordinate_system`.

## Deduced Conclusions

### Deduction 1: The survey is not the root problem

**Based on:** Findings 2, 3, and 4

**Reasoning:** The source page visibly contains `JAD 2001`, parish, and coordinate table rows. The case artifacts also contain parish and point evidence.

**Conclusion:** The blocker is caused by extraction/normalization misclassification, not by an unreadable survey.

### Deduction 2: The immediate gate failure is the coordinate-system value

**Based on:** Findings 2 and 5

**Reasoning:** The gate only needs a JAD2001-recognized coordinate-system value and a point count greater than zero. Point count is `5`, but the coordinate-system value normalizes to `THEODOLITESURVEYCOMPASSSTANDARD`, not `JAD2001`.

**Conclusion:** If coordinate_system were captured as `JAD 2001`, this specific readiness gate would pass.

## Hypothesized Paths

### Hypothesis 1: OCR/vision confused survey method with coordinate system

**Status:** Confirmed

**Theory:** The visible vertical label `Theodolite Survey (Compass Standard)` was returned in `coordinate_system`, while the visible `JAD 2001` coordinate-table label was missed.

**Supporting indicators:** The extraction summary has the same string in top-level `coordinate_system` and `survey_metadata.survey_method`.

**Would confirm:** Rendered source page shows `JAD 2001` and separate `Theodolite Survey (Compass Standard)` text.

**Would refute:** Source page has no JAD2001 label.

**Resolution:** Confirmed by local page rendering.

### Hypothesis 2: The readiness check should count numeric coordinate rows, not all point rows

**Status:** Open

**Theory:** The gate evidence says `coordinate_table_point_count = 5`, but only two rows have numeric coordinates. The current condition uses row count, not numeric coordinate count.

**Supporting indicators:** Three rows are generated/review-required with null coordinates.

**Would confirm:** Product decision requires at least two concrete coordinate anchors for georeference readiness.

**Would refute:** Product decision says any reviewed/derived point rows are sufficient after JAD2001 is confirmed.

**Resolution:** Keep as a follow-up; it did not cause this specific blocker because the current gate failed on coordinate-system value.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Raw OpenAI vision response before normalization | Would show exactly whether the model returned the wrong field or whether normalization moved it. | Persist/debug the raw provider response for a controlled rerun, with secrets redacted. |
| Product rule for minimum numeric coordinate anchors | Determines whether `PointCount > 0` should become numeric-coordinate-count based. | Confirm with reviewer/SME. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `ManifestPreflightService.RunGeoreferenceCheck` via `georeference_spatial_validation_readiness` |
| Trigger | User runs Georeference Check after survey-plan OCR/vision extraction exists |
| Condition | `SurveyPlanExtractionEvidence.HasGeoreferenceEvidence` is false because coordinate system is not recognized as JAD2001 |
| Related files | `survey_plan_ocr_vision_extraction.py`, `ManifestPreflightService.cs`, `georeference_check_summary.json`, `survey_plan_extraction_summary.json`, `extraction_review_data.json` |

## Conclusion

**Confidence:** High

The survey page is clear and contains visible `JAD 2001` coordinate evidence. The blocker occurs because OCR/vision extraction populated the coordinate-system field with the survey method, `Theodolite Survey (Compass Standard)`, while the Georeference Check only accepts coordinate systems recognized as JAD2001.

## Recommended Next Steps

### Fix direction

Patch `survey_plan_ocr_vision_extraction.py` so the prompt and normalization keep `coordinate_system` separate from `survey_method`. If the model returns a survey-method phrase as coordinate system, normalize it to missing unless a JAD2001/Jamaica Grid cue is found elsewhere; also add explicit prompt language to inspect labels above/near coordinate tables for `JAD 2001`.

### Diagnostic

Rerun extraction for TR `100000755` after the patch and verify `survey_plan_extraction_summary.json` records `coordinate_system.value = JAD 2001` or `JAD 2001 Jamaica Grid`.

## Reproduction Plan

1. Open/reopen TR `100000755`.
2. Run Georeference Check using the existing artifacts.
3. Observe blocker `georeference_spatial_validation_readiness`.
4. Inspect `survey_plan_extraction_summary.json`; `coordinate_system.value` is `Theodolite Survey (Compass Standard)`.
5. Render page 1 of `PLAN_DOC_486024.pdf`; observe visible `JAD 2001` above the coordinate table.

## Side Findings

- The blocker message is too generic for this case. It says coordinate system, parish, and coordinate table evidence are missing/low-confidence, but the actual failed subcondition is coordinate-system recognition.

## Patch Applied

- Updated `survey_plan_ocr_vision_extraction.py` prompt to look near/above coordinate tables for `JAD 2001`, `J.A.D. 2001`, `Jamaica Datum 2001`, and `Jamaica Grid`.
- Added coordinate-system normalization so survey-method text such as `Theodolite Survey (Compass Standard)` is not accepted as coordinate-system evidence.
- Updated `ManifestPreflightService` to report the specific blocker message: `coordinate system was extracted as survey method, not JAD2001`.
- Added Python and C# regression tests for the exact misclassification path.

## Patch Verification

- `python -m unittest tests.test_survey_plan_ocr_vision_extraction` from `src/ProcessingTools`: 13 passed.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.tmp/obj/ /p:BaseOutputPath=.tmp/bin/`: passed with one pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/.tmp/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll georeference`: 10 passed.
