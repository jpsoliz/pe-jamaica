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
- PXA survey plans commonly provide both printed/reference coordinate points and boundary segment calls; the reference points locate the parcel in JAD 2001 space while the reviewed segments define the construction path.

For TR100000562, the OCR/vision candidate rows did not produce the expected boundary chain. The user-verified boundary sequence should be:

```text
18 -> 15: S84°56'E, 33.470 m
15 -> 30: S01°27'E, 18.343 m
30 -> 16: S01°39'W, 5.230 m
16 -> 17: N82°59'W, 41.415 m
17 -> 18: N19°09'E, 22.715 m
```

This story adds the missing human-in-the-loop segment table and deterministic solver layer. OCR/vision remains the candidate extractor; reviewed segments become the authoritative construction input, and printed/reference coordinate points become the authoritative anchors used to locate the solved boundary.

## Acceptance Criteria

1. Given a PXA extraction artifact contains a `segments` array, when the user opens `Validate Points and Lines`, then the workspace shows a segment table in addition to the existing point table.

2. Given segment rows are displayed, then each segment row exposes editable values for sequence, from point, to point, bearing text, distance/length text, include-in-boundary flag, status/confidence, and review notes.

3. Given a user edits a segment, when the review artifact is saved, then corrected segment values are written back to `extraction_review_data.json` while preserving the original OCR/vision candidate values and source evidence.

4. Given a reviewed segment chain has enough anchored coordinates, when the deterministic boundary solver runs, then it derives missing point coordinates from the reviewed sequence, bearing, and distance values.

4a. Given a PXA survey plan contains printed/reference coordinate points and reviewed boundary segments, when the parcel is solved, then the system uses printed/reference points as coordinate anchors and reviewed boundary segments as the construction path.

5. Given the solver parses bearings, then it supports quadrant bearing formats used in Jamaican survey plans, including variants such as `S84°56'E`, `S84 56 E`, `S01°27'E`, `S01 39 W`, `N82°59'W`, and `N19°09'E`.

6. Given a reviewed boundary chain is solved, then the solver validates endpoint continuity, missing point references, duplicate point references, closure distance, and whether the final segment returns to the start point within configured tolerance.

7. Given the source plan includes a captured document area, when the solver computes a polygon area, then the workflow compares computed area against document area and reports the absolute and percentage delta.

8. Given a coordinate is derived by the solver, then the resulting point row is marked with a clear provenance/status such as `derived_from_reviewed_segments`, carries source segment references, and remains reviewable before approval.

9. Given a point already has a printed/reference coordinate from the plan or OCR, when a reviewed segment would derive a conflicting coordinate, then the solver distinguishes true examiner-confirmed anchors from unconfirmed OCR/reference candidates and records the conflict with clear provenance.

9a. Given the reviewed segment chain is continuous, closes within tolerance, and matches the captured document area within tolerance, when unconfirmed OCR/reference point coordinates conflict with the segment-derived coordinates, then the reviewed segment chain becomes the geometry source of truth, the conflicting point coordinates are superseded/recalculated with warning findings, and validation is not blocked solely by those stale coordinate conflicts.

9b. Given a reviewed segment derives a coordinate that conflicts with an examiner-confirmed anchor beyond tolerance, then the solver blocks validation completion until the examiner explicitly resolves whether the anchor or the segment call is wrong.

10. Given TR100000562/TR100000568-style PXA cases are corrected to verified segment chains, when the solver runs, then it derives/recalculates boundary points from reviewed segment calls, validates closure, compares computed area to document area, preserves original OCR/reference coordinates as evidence, and reports only true closure/area/confirmed-anchor failures as blockers.

11. Given `Create Spatial Units` runs for a PXA case after validation, then it consumes the solved reviewed-segment boundary chain and its printed/derived point coordinates rather than point-row order or unreviewed OCR/vision segment candidates.

12. Given the reviewed segments cannot produce a closed geometry, then `Validate Points and Lines` remains incomplete and downstream stages show a clear blocker explaining which segment or point prevents construction.

13. Given a user corrects segment rows and saves the review, then the save action reruns the deterministic boundary solver, updates derived point coordinates, recomputes closure/area diagnostics, and refreshes the parcel preview from the reviewed segment chain.

14. Given automated tests run, then coverage proves segment persistence, bearing parsing, deterministic point derivation, closure/area validation, save-triggered solver refresh, and the TR100000562-style sequence behavior.

15. Given PXA extraction reads a survey plan with printed/reference point labels, when a boundary vertex is not visibly labeled as that same point, then extraction must not invent the next sequential label from the printed/reference labels; it must either use the visible map/course-table label or create a temporary generated placeholder in the opposite label style for examiner confirmation.

15a. Given the examiner selects `Rebuild points` for a PXA reviewed segment chain and any point label is reused before the final closing segment, then the solver preserves real printed/reviewed point labels, replaces premature reused labels with generated intermediate labels, and uses temporary generated placeholders in the opposite label style: lettered plans receive numeric generated labels and numbered plans receive alphabetic generated labels.

16. Given `Rebuild points` repairs premature label reuse, then the updated segment labels are written back as reviewed segment overrides, the segment grid refreshes immediately, derived point rows are regenerated from the repaired chain, and solver findings explain the automatic label repair.

17. Given `Rebuild points` regenerates the reviewed boundary after labels or coordinates are edited, then stale solver-derived point rows that are no longer referenced by the current reviewed segment chain are removed before resequencing so duplicate sequence values do not block validation.

18. Given `Rebuild points` is selected and an existing point coordinate conflicts with the reviewed boundary path, then the explicit rebuild replaces the conflicting coordinate from the reviewed segments and reports that the point was recalculated, while ordinary save remains conservative.

19. Given `Rebuild points` is selected and the reviewed segment chain is the active source of truth, then extracted/reference point rows that are no longer referenced by the reviewed boundary chain are removed from the parcel review rows before validation so inactive points cannot create duplicate sequence blockers.

20. Given the PXA reviewed boundary solver has a usable result (`passed` or non-blocking `warning`) from `reviewed_boundary_segments`, then Validate Points and downstream validation treat reviewed segments as the geometry/sequence source of truth and do not block solely on superseded point-row closure or duplicate/missing sequence values.

21. Given `Rebuild points` generates a temporary vertex whose coordinate matches an extracted reference point that is not currently in the segment chain, then the solver replaces the generated label with the extracted reference point label, keeps that reference row in the boundary sequence, removes the extra generated point row, and leaves a closed parcel with active point count equal to included segment count.

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
  - [x] Treat printed/reference point coordinates as authoritative anchors and reviewed segments as the construction path.
  - [x] Compute closure distance and polygon area.
  - [x] Compare computed area with document area from survey metadata.
  - [x] Instruct extraction not to invent sequential point labels from printed/reference labels unless those labels are visibly tied to the boundary vertex. (AC: 15)
  - [x] Repair premature reuse of point labels during explicit point rebuild, using generated labels in the opposite style from the reviewed chain. (AC: 15-16)
  - [x] Remove stale solver-derived point rows when explicit rebuild changes the reviewed boundary chain. (AC: 17)
  - [x] Allow explicit point rebuild to replace conflicting existing point coordinates from reviewed segments. (AC: 18)
  - [x] Remove inactive extracted/reference rows during explicit rebuild and refresh the point grid after row removal. (AC: 19)
  - [x] Align Validate Points, Save/Approve, and downstream validation so usable PXA reviewed-boundary solver output supersedes point-row closure/sequence blockers. (AC: 20)
  - [x] Merge generated rebuild vertices with matching extracted reference coordinates instead of creating an additional duplicate point row. (AC: 21)

- [x] Wire solver output into review persistence and stage findings. (AC: 6-12)
  - [x] Write derived points into `rows` with explicit provenance and source segment references.
  - [x] Write solver diagnostics into `extraction_review_data.json` and/or the Dimension Check summary.
  - [x] Ensure unresolved solver findings block approval and Create Spatial Units.
  - [x] Ensure successful solver output enables the normal point/line validation completion path.
  - [x] Ensure saving reviewed segment edits reruns the solver, updates derived point rows, and refreshes parcel preview state from the reviewed segment chain.

- [x] Ensure Create Spatial Units uses reviewed PXA geometry. (AC: 11-12)
  - [x] Update the PXA path so corrected/derived point rows and reviewed segment rows are the construction source.
  - [x] Avoid using raw `segments` candidates when reviewed overrides exist.
  - [x] Avoid using point-row order as the PXA polygon source when a solved reviewed segment chain is available.
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

The PXA review save behavior must treat reviewed segments as the active construction source. When the examiner edits segments and saves, the workflow should immediately persist the reviewed segment values, rerun the boundary solver, write any derived/corrected point rows, recompute closure/area diagnostics, and refresh the parcel preview. The user should not need to close and reopen the review workspace to see the corrected polygon preview.

### PXA Construction Policy

For PXA survey-plan geometry, the construction rule is:

```text
anchor_source = printed_reference_points
construction_source = reviewed_boundary_segments
derived_source = deterministic_boundary_solver
validation_source = document_area + closure + coordinate_system
conflict_policy = block_on_confirmed_anchor_conflict
unconfirmed_reference_conflict_policy = supersede_with_segment_derived_coordinate_when_chain_closes_and_area_matches
```

Printed/reference coordinates are candidate anchors until the examiner confirms them. Reviewed boundary segments are authoritative for traversal order, bearings, and distances once saved by the examiner. If the reviewed segment chain is continuous, closes within configured tolerance, and matches the document area within configured tolerance, then the solver may supersede unconfirmed OCR/reference point coordinates with segment-derived coordinates and record non-blocking warning findings. The original values must remain in the artifact as source evidence.

If a derived point conflicts with an examiner-confirmed anchor beyond configured tolerance, the workflow must surface a blocker and require examiner correction before Create Spatial Units.

Reference points and boundary segments must be treated as complementary evidence, not competing sources. Reference points anchor the solved parcel to real coordinates; reviewed segments construct the boundary shape between those anchors.

For cases like TR100000568, where the reviewed boundary calls form a valid closed chain and the segment-derived area is close to the document area, the solver should not block only because stale OCR point coordinates disagree with the reviewed chain. The expected behavior is to recalculate/supersede the affected point rows, show a warning that point coordinates were resolved from reviewed segments, and enable Validation Complete when no closure, area, missing-reference, or confirmed-anchor blocker remains.

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
| 2026-07-08 | 1.1 | Patched PXA source-of-truth behavior so reviewed boundary segments fill blank derived point rows and drive output polygon rings. | Codex |
| 2026-07-09 | 1.2 | Clarified PXA construction policy: printed/reference points anchor geometry and reviewed boundary segments construct the parcel. | Codex |
| 2026-07-12 | 1.3 | Patched conflict policy so closed, area-matched reviewed segment chains can supersede unconfirmed OCR/reference coordinate conflicts while confirmed-anchor conflicts still block. | Codex |
| 2026-07-23 | 1.4 | Added explicit Rebuild behavior for premature label reuse, with opposite-style generated point labels and immediate segment-grid refresh. | Codex |
| 2026-07-23 | 1.5 | Patched PXA extraction guidance so printed/reference labels are not reused for unlabeled boundary vertices unless visibly tied to that point. | Codex |

## Dev Agent Record

### Implementation Notes

- Added typed segment support to the extraction review model and persistence layer, including raw candidate preservation, reviewed overrides, original values, segment hashing, and save/load round trip.
- Added editable segment projection and a segment table in the Points Validation Tool for sequence, from/to points, bearing, distance, boundary inclusion, status, and notes.
- Added `SurveyPlanBoundarySolver` with Jamaican quadrant bearing parsing, forward/backward coordinate derivation, printed-coordinate conflict detection, closure distance, computed area, document-area comparison, and `boundary_solver` diagnostics.
- Wired the solver into review save/approval so derived points are written back with `derived_from_reviewed_segments` provenance and solver blockers prevent completion.
- Updated the output adapter to prefer reviewed segment rows for line construction when they resolve to reviewed/derived points, preserving bearing and distance text for downstream labels and Enterprise publishing.
- Patched the solver to fill existing blank point rows when their coordinates are derived from reviewed boundary segments, instead of adding only new rows or leaving blank coordinates behind.
- Patched Create Spatial Units output construction so reviewed PXA boundary segments are the source of truth for polygon rings when available; point-order polygons remain the fallback for non-PXA/legacy cases.
- Clarified the PXA construction policy in the story: printed/reference points are authoritative coordinate anchors, reviewed boundary segments are authoritative traversal calls, and conflicts between the two block validation.
- Patched explicit PXA `Rebuild points` behavior so premature reuse of the closing point label is converted to generated intermediate labels in the same point-label style, while the final closure segment remains unchanged.

### Debug Log

- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "survey plan solver" "review persistence saves editable segment rows"`: passed 4 focused tests.
- `python -m unittest tests.test_output_adapter.OutputAdapterTests.test_output_adapter_prefers_reviewed_pxa_segments_for_lines` from `src/ProcessingTools`: passed.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 340 tests.
- `python -m unittest tests.test_output_adapter` from `src/ProcessingTools`: passed 13 tests.
- `python -m unittest discover -s tests` from `src/ProcessingTools`: passed 78 tests.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`: succeeded with 0 warnings and 0 errors.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`: passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "survey plan solver solves TR100000562"`: passed 1 focused test.
- `python -m unittest tests.test_output_adapter.OutputAdapterTests.test_output_adapter_uses_reviewed_pxa_segments_for_polygon_ring tests.test_output_adapter.OutputAdapterTests.test_output_adapter_prefers_reviewed_pxa_segments_for_lines` from `src/ProcessingTools`: passed 2 focused tests.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 342 tests.
- `python -m unittest discover -s tests` from `src/ProcessingTools`: passed 79 tests.

### Completion Notes

- Story 2.19 is implemented and ready for review.
- Existing PE point-review behavior remains on the existing row grid path; the segment table appears only when review artifacts contain segment candidates.
- The output adapter falls back to point-order line construction when no reviewed segments can be resolved, preserving existing behavior for non-PXA cases.
- For PXA, reviewed boundary segments now define the authoritative geometry chain after save/solve. The solver fills missing coordinates for existing point IDs, and Create Spatial Units builds PXA polygons from the reviewed segment chain when it resolves.
- Printed/reference points and reviewed boundary segments are complementary inputs: the former locates the parcel, the latter constructs its shape.

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
