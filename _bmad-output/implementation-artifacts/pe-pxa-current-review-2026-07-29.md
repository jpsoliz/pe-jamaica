# PE/PXA Current Review - 2026-07-29

## Summary

The current implementation supports two compute transaction lines:

- **PE**: computation-sheet driven plan examination.
- **PXA**: survey-plan PDF driven plan examination by area / single-parcel survey-plan review.

Both lines share the same compute workflow shell and stage vocabulary, but they intentionally differ in source requirements, extraction route, review workspace shape, and geometry construction policy.

## Current Behavior

PE resolves to `pe_computation_review`.

- Required sources: `computation_sheet`, `plan_map_reference`.
- Optional sources: `coordinate_text_source`, `dwg_source`.
- Primary extraction role: `computation_sheet`.
- Document profile: `computation_sheet_multi_or_single_parcel`.
- Review path: existing point-row review behavior.

PXA resolves to `pxa_single_parcel_survey_plan`.

- Required source: `survey_plan_pdf`.
- Optional sources: `coordinate_text_source`, `dwg_source`.
- Primary extraction role: `survey_plan_pdf`.
- Document profile: `scanned_single_parcel_survey_plan_pdf`.
- Supported aliases include `PXA` and `Plan Examination by Area`.
- Review path: PXA-specific General Info, Owners / Neighbors, Boundary Segments, and Points tabs.

Both transaction lines use the same high-level stage gates:

```text
Supporting Document Check
Structure Check
Georeference Check
Dimension Check
Validate Points and Lines
Create Spatial Units
Final Review
Finalize
```

## Review Findings

No PE/PXA runtime routing mismatch was found.

The review confirmed:

- PXA does not require a computation sheet.
- PE does not route into the scanned survey-plan OCR/vision extractor.
- `Plan Examination by Area` resolves through the PXA transaction profile and document profile.
- PXA detection is based on survey-plan source/profile metadata, not segment rows alone.
- PXA reviewed boundary segments drive geometry once saved/solved.
- PE/non-PXA remains on the existing point-row review/output path.
- Supporting Documents now refreshes when already open instead of auto-opening during workflow sync.

## Remaining Risks

PXA production extraction still depends on live/runtime inputs:

- The Innola transaction load must place the survey plan PDF in the case source folder.
- The configured ArcGIS Python environment must have a supported PDF renderer.
- OCR/vision extraction requires `OPENAI_API_KEY`.

If those prerequisites are missing, the expected behavior is manual-review fallback rather than silent success.

OCR/vision candidates remain starting evidence, not authoritative geometry. The examiner must review or rebuild the PXA segment chain before geometry should be trusted.

## Updates Made

Stories updated:

- `2-18A`: documented PE/PXA parity and `Plan Examination by Area` alias behavior.
- `2-18`: documented PXA extraction separation and live OCR/vision dependencies.
- `2-19`: documented PXA reviewed-segment source-of-truth behavior.
- `2-20`: documented PE/PXA UX split and tab/action-scope differences.

Code/test updates:

- Added workflow-rule regression coverage for `Plan Examination by Area` resolving to PXA.
- Updated split-stage workflow tests to seed current Structure / Georeference / Dimension gate state explicitly.
- Made extraction decision gate evaluation reuse the loaded post-extraction artifact state.
- Prevented Supporting Documents from auto-opening during workflow sync; it now refreshes only if open.
- Adjusted one output-map test to assert local review outputs remain available without depending on point-first ordering.

## Validation

Command:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

Result:

- `542 tests` passed.
- Existing warning remains: nullable value warning in `SurveyPlanBoundarySolverTests.cs`.

