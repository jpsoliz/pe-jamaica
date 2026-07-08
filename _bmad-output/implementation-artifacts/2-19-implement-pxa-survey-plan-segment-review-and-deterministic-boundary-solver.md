---
baseline_commit: handoff-2026-07-08
---

# Story 2.19: Implement PXA Survey Plan Segment Review And Deterministic Boundary Solver

Status: review

## Story

As a cadastral examiner,  
I want PXA survey-plan segment candidates to be reviewable and deterministically resolved into parcel boundary points,  
so that image-only survey plan PDFs can produce reliable single-parcel geometry after human confirmation rather than depending on imperfect OCR/vision output alone.

## Business Context

Story 2.18 adds OCR/vision extraction for scanned PXA survey plan PDFs. TR100000562 proved that the configured AI extraction path is being used and can extract useful plan metadata and some segment candidates, but it also exposed a critical workflow gap:

- OCR/vision can misread segment endpoints, bearings, and sequence.
- The extracted artifact may omit boundary points that are visible or derivable from reviewed segments.
- The Points Validation Tool is still mostly point-row centered, so examiners cannot directly correct the boundary segment chain that should drive geometry creation.
- Create Spatial Units needs reviewed, deterministic points and segments, not raw AI guesses.

For TR100000562, the OCR/vision candidate rows did not produce the expected boundary chain. The user-verified boundary sequence should be:

```text
18 -> 15: S84°56'E, 33.470 m
15 -> 30: S01°27'E, 18.343 m
30 -> 16: S01°39'W, 5.230 m
16 -> 17: N82°59'W, 41.415 m
17 -> 18: N19°09'E, 22.715 m
```

This story adds the missing human-in-the-loop segment table and deterministic solver layer. OCR/vision remains the candidate extractor; reviewed segments become the authoritative construction input.

## Acceptance Criteria

1. Given a PXA extraction artifact contains a `segments` array, when the user opens `Validate Points and Lines`, then the workspace shows a segment table in addition to the existing point table.

2. Given segment rows are displayed, then each segment row exposes editable values for sequence, from point, to point, bearing text, distance/length text, include-in-boundary flag, status/confidence, and review notes.

3. Given a user edits a segment, when the review artifact is saved, then corrected segment values are written back to `extraction_review_data.json` while preserving the original OCR/vision candidate values and source evidence.

4. Given a reviewed segment chain has enough anchored coordinates, when the deterministic boundary solver runs, then it derives missing point coordinates from the reviewed sequence, bearing, and distance values.

5. Given the solver parses bearings, then it supports quadrant bearing formats used in Jamaican survey plans, including variants such as `S84°56'E`, `S84 56 E`, `S01°27'E`, `S01 39 W`, `N82°59'W`, and `N19°09'E`.

6. Given a reviewed boundary chain is solved, then the solver validates endpoint continuity, missing point references, duplicate point references, closure distance, and whether the final segment returns to the start point within configured tolerance.

7. Given the source plan includes a captured document area, when the solver computes a polygon area, then the workflow compares computed area against document area and reports the absolute and percentage delta.

8. Given a coordinate is derived by the solver, then the resulting point row is marked with a clear provenance/status such as `derived_from_reviewed_segments`, carries source segment references, and remains reviewable before approval.

9. Given a point already has a printed coordinate from the plan, when a reviewed segment would derive a conflicting coordinate, then the solver does not silently overwrite the printed value; it emits a reportable finding requiring examiner review.

10. Given TR100000562 is corrected to the verified segment chain above, when the solver runs with the known point coordinates from the plan, then it derives the missing boundary points, validates closure, and reports area close to the document area of `854.807 sq. metres` within configured tolerance.

11. Given `Create Spatial Units` runs for a PXA case after validation, then it consumes the corrected/derived point rows and reviewed segment rows rather than unreviewed OCR/vision segment candidates.

12. Given the reviewed segments cannot produce a closed geometry, then `Validate Points and Lines` remains incomplete and downstream stages show a clear blocker explaining which segment or point prevents construction.

13. Given automated tests run, then coverage proves segment persistence, bearing parsing, deterministic point derivation, closure/area validation, and the TR100000562-style sequence behavior.

## Tasks / Subtasks

- [x] Extend the extraction review artifact model for editable segments. (AC: 1-3, 8-9)
  - [x] Add a first-class `ExtractionReviewSegment` model or equivalent typed projection.
  - [x] Load `segments` from `extraction_review_data.json` into the review document.
  - [x] Preserve raw OCR/vision segment candidates, reviewed overrides, source page/zone, confidence, status, and notes.
  - [x] Include segments in review hashing so approved review artifacts are invalidated when segment edits occur.
  - [x] Save corrected segments back to `extraction_review_data.json`.

- [x] Add segment review UI to `Validate Points and Lines`. (AC: 1-3, 12)
  - [x] Add a segment table, tab, or split panel near the existing point grid.
  - [x] Allow editing segment sequence, from point, to point, bearing, distance/length, include-in-boundary, status, and notes.
  - [x] Keep the active parcel selection stable after adding/editing points or segments.
  - [x] Surface chain/closure/area findings beside the segment and parcel preview areas.
  - [x] Keep existing PE point-review behavior unchanged.

- [x] Implement the deterministic PXA boundary solver. (AC: 4-10, 12)
  - [x] Add a service such as `SurveyPlanBoundarySolver` or `PxaSurveyPlanBoundarySolver`.
  - [x] Parse quadrant bearings into coordinate deltas using project coordinate convention: easting = X, northing = Y.
  - [x] Normalize distance/length strings with metres, commas, and OCR spacing variants.
  - [x] Walk the reviewed segment chain in sequence and derive unknown point coordinates from anchored coordinates.
  - [x] Support solving forward and backward where a later point is anchored.
  - [x] Detect conflicts between printed coordinates and derived coordinates.
  - [x] Compute closure distance and polygon area.
  - [x] Compare computed area with document area from survey metadata.

- [x] Wire solver output into review persistence and stage findings. (AC: 6-12)
  - [x] Write derived points into `rows` with explicit provenance and source segment references.
  - [x] Write solver diagnostics into `extraction_review_data.json` and/or the Dimension Check summary.
  - [x] Ensure unresolved solver findings block approval and Create Spatial Units.
  - [x] Ensure successful solver output enables the normal point/line validation completion path.

- [x] Ensure Create Spatial Units uses reviewed PXA geometry. (AC: 11-12)
  - [x] Update the PXA path so corrected/derived point rows and reviewed segment rows are the construction source.
  - [x] Avoid using raw `segments` candidates when reviewed overrides exist.
  - [x] Preserve bearing/distance fields for downstream labels and Enterprise publishing.

- [x] Add regression tests and fixtures. (AC: 1-13)
  - [x] Unit-test bearing parser variants used in Jamaican survey plans.
  - [x] Unit-test solver derivation from a TR100000562-style segment chain.
  - [x] Test document-area comparison against `854.807 sq. metres`.
  - [x] Test printed-coordinate conflict handling.
  - [x] Test segment save/load round trip in `ExtractionReviewPersistenceService`.
  - [x] Test that unclosed or incomplete PXA chains block validation completion.
  - [x] Test that existing PE point validation remains unchanged.

## Dev Notes

### Current State

The current extraction model has `RowCount`, `SegmentRowCount`, point `Rows`, and `RootMetadata`, but no first-class typed segment list in the C# review model.

Key current files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ProcessingTools/adapters/validation_adapter.py`

`survey_plan_ocr_vision_extraction.py` already writes normalized `segments` with values like `from_point`, `to_point`, `bearing_txt`, `distance_txt`, `length_txt`, confidence, source page/zone, status, and review notes. The next implementation should not ask the AI to be perfect; it should make those candidates editable and then solve the reviewed geometry deterministically.

### TR100000562 Evidence

The transaction showed that OCR/vision can produce basic values, but the segment candidates are not authoritative enough:

- Point `30` may be missing or misinterpreted.
- Non-boundary points can be included as boundary candidates.
- A bearing can be misread, for example `N19°59'W` instead of the verified `N19°09'E`.
- The correct boundary chain is known only after examiner review.

The solver should therefore treat OCR/vision segment rows as candidate evidence and reviewed segment rows as construction input.

### Boundary Solver Direction

The solver should be deterministic, testable, and independent of ArcGIS UI state. Prefer a pure C# service that accepts:

- reviewed segments
- known point coordinates
- document area, if available
- tolerance settings

and returns:

- updated/derived point candidates
- closure diagnostics
- computed area diagnostics
- conflict findings
- blocker/warning/pass status

Do not put the geometry-solving algorithm in WPF code-behind.

### Artifact Shape Recommendation

Preserve existing top-level `segments`, but add reviewed fields instead of replacing the raw values:

```json
{
  "segments": [
    {
      "segment_no": 1,
      "from_point": "18",
      "to_point": "15",
      "bearing_txt": "S84°56'E",
      "distance_txt": "33.470",
      "review_sequence": 1,
      "review_from_point": "18",
      "review_to_point": "15",
      "review_bearing_txt": "S84°56'E",
      "review_distance_txt": "33.470",
      "review_include_in_boundary": true,
      "review_status": "accepted",
      "review_notes": null,
      "source_page": 1,
      "source_zone": "parcel_sketch"
    }
  ],
  "boundary_solver": {
    "status": "warning",
    "closure_distance_m": 0.04,
    "computed_area_sq_m": 854.852,
    "document_area_sq_m": 854.807,
    "area_delta_sq_m": 0.045,
    "findings": []
  }
}
```

Exact field names can follow existing code conventions, but raw and reviewed values must both remain available for audit.

### Create Spatial Units Dependency

For PXA, `Create Spatial Units` should build from the reviewed artifact after solver approval. It should not reconstruct the boundary directly from unreviewed AI rows. This protects the downstream geometry, Enterprise publish, Innola Spatial Unit creation, and final reports.

### Testing Notes

Use synthetic fixture JSON for deterministic solver tests rather than depending on a live user case folder. The TR100000562 values can be used as the numeric fixture:

```text
18 -> 15: S84°56'E, 33.470 m
15 -> 30: S01°27'E, 18.343 m
30 -> 16: S01°39'W, 5.230 m
16 -> 17: N82°59'W, 41.415 m
17 -> 18: N19°09'E, 22.715 m
document area: 854.807 sq. metres
```

Known printed coordinates from the plan can be used as anchors:

```text
Point 15: N 670582.156, E 712897.345
Point 17: N 670563.653, E 712856.553
```

## Dependencies

- Builds on Story 2.18: PXA survey-plan OCR/vision extraction.
- Builds on Story 2.18A: transaction-type workflow profiles for PE/PXA.
- Complements Story 4.8 and 4.9: separate Dimension/Georeference findings and reportable stage model.
- Feeds Story 5.x Create Spatial Units behavior for reviewed PXA geometry.

## References

- `_bmad-output/implementation-artifacts/2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `_bmad-output/implementation-artifacts/2-18a-add-transaction-type-workflow-profiles-for-pe-and-pxa-source-requirements.md`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-08 | 0.1 | Initial story for PXA segment review, deterministic boundary solving, closure/area validation, and reviewed artifact persistence. | Codex |
| 2026-07-08 | 1.0 | Implemented editable segment artifacts, PXA boundary solver, review UI segment grid, reviewed-segment output construction, and regression coverage. | Codex |

## Dev Agent Record

### Implementation Notes

- Added typed segment support to the extraction review model and persistence layer, including raw candidate preservation, reviewed overrides, original values, segment hashing, and save/load round trip.
- Added editable segment projection and a segment table in the Points Validation Tool for sequence, from/to points, bearing, distance, boundary inclusion, status, and notes.
- Added `SurveyPlanBoundarySolver` with Jamaican quadrant bearing parsing, forward/backward coordinate derivation, printed-coordinate conflict detection, closure distance, computed area, document-area comparison, and `boundary_solver` diagnostics.
- Wired the solver into review save/approval so derived points are written back with `derived_from_reviewed_segments` provenance and solver blockers prevent completion.
- Updated the output adapter to prefer reviewed segment rows for line construction when they resolve to reviewed/derived points, preserving bearing and distance text for downstream labels and Enterprise publishing.

### Debug Log

- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "survey plan solver" "review persistence saves editable segment rows"`: passed 4 focused tests.
- `python -m unittest tests.test_output_adapter.OutputAdapterTests.test_output_adapter_prefers_reviewed_pxa_segments_for_lines` from `src/ProcessingTools`: passed.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 340 tests.
- `python -m unittest tests.test_output_adapter` from `src/ProcessingTools`: passed 13 tests.
- `python -m unittest discover -s tests` from `src/ProcessingTools`: passed 78 tests.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`: succeeded with 0 warnings and 0 errors.

### Completion Notes

- Story 2.19 is implemented and ready for review.
- Existing PE point-review behavior remains on the existing row grid path; the segment table appears only when review artifacts contain segment candidates.
- The output adapter falls back to point-order line construction when no reviewed segments can be resolved, preserving existing behavior for non-PXA cases.

## File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewSegmentViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/SurveyPlanBoundarySolverTests.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
