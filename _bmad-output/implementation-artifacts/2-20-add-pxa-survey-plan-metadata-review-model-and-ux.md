---
baseline_commit: handoff-2026-07-08
---

# Story 2.20: Add PXA Survey Plan Metadata Review Model And UX

Status: done

## Story

As a cadastral examiner,  
I want PXA survey-plan metadata, reviewed boundary segments, derived points, and parcel preview to be organized in a PXA-specific review workspace,  
so that scanned single-parcel survey plans can be reviewed from the PDF source, corrected deterministically, and carried forward with reportable metadata.

## Business Context

Story 2.18 introduced PXA survey-plan PDF extraction, and Story 2.19 added segment review plus deterministic boundary solving. The current PXA review surface now has the right ingredients, but the user workflow needs clearer hierarchy:

- The scanned survey plan PDF is the primary source.
- Printed/reference points should anchor the parcel in real coordinates.
- Reviewed boundary segments should drive derived point coordinates and parcel preview.
- General survey-plan metadata from the survey plan must be captured and reviewed, not hidden in raw OCR output.
- Owner, representative, and adjacent-neighbor information should be reviewed in a dedicated people/context tab instead of being mixed with document facts.
- PXA should not overload the PE point-review layout with unrelated metadata and segment controls.

For PXA, the examiner needs one review workspace that keeps the PDF visible while separating metadata review, segment correction, point coordinates, and parcel preview.

## UX Reference

![PXA Points Validation Metadata and Segments Wireframe](../ux-artifacts/pxa-points-validation-metadata-segments-wireframe.png)

The wireframe shows the intended PXA-only workspace pattern:

- Left: source PDF verification and embedded plan viewer.
- Center: tabbed review workspace.
- Right: active parcel interpretation, preview, and validation findings.

## Acceptance Criteria

1. Given a transaction resolves to the PXA workflow profile, when the Points Validation Tool opens, then the center review area uses PXA-specific tabs: `General Info`, `Owners / Neighbors`, `Boundary Segments`, and `Points`.

2. Given a transaction is not PXA, when the Points Validation Tool opens, then existing PE point-review behavior remains unchanged.

3. Given the `General Info` tab is selected, then the workspace displays editable/reviewable document and survey fields for:
   - coordinate system / `JAD 2001` present
   - north arrow present
   - parish
   - document area value and unit
   - document dates, including survey date and plan check date where available
   - make and number of instrument / survey instrument
   - surveyed by / surveyor
   - volume and folio values as two editable/reviewable values

3a. Given the `Owners / Neighbors` tab is selected, then the workspace displays editable/reviewable people and neighboring-parcel context for parties, owners, representatives, adjacent owners, and optional segment associations.

4. Given metadata values are extracted from OCR/vision, when the metadata form is shown, then each field preserves value, raw/source text where available, confidence/status where available, and examiner review state.

5. Given adjacent owners are reviewed in `Owners / Neighbors`, then the UI supports associating an adjacent owner with a boundary segment where possible.

6. Given the `Boundary Segments` tab is selected, then the workspace displays reviewed segment rows with editable sequence, from point, to point, bearing, distance, adjacent owner, include/use flag, status, and notes.

7. Given the user edits segment values and selects `Save Review`, then the save action persists reviewed segment values, runs the deterministic boundary solver, updates/derives point coordinates, recomputes closure/area diagnostics, refreshes parcel preview, and writes the updated artifact to `extraction_review_data.json`.

8. Given a reviewed segment chain is valid, when save/solve completes, then the parcel preview redraws from the reviewed segment chain rather than stale point order.

9. Given a reviewed segment chain is blocked, when save/solve completes, then the UI keeps validation incomplete and shows the exact blocker, including missing point references, duplicate point misuse, endpoint conflict, closure failure, or area mismatch.

9a. Given a reviewed segment chain closes within tolerance and matches document area within tolerance, when unconfirmed OCR/reference point coordinates conflict with segment-derived coordinates, then the UI treats those coordinate conflicts as resolved warnings instead of blockers and explains that reviewed segments were used as the geometry source of truth.

9b. Given the review still has an active blocker, then the UI must identify the blocking rule and the row/point/segment involved; it must not show only generic copy such as `Needs attention`, `Selected row has no active validation blocker`, or `Parcel exceeds tolerance` without the actionable reason.

10. Given derived points are created from reviewed segments, when the `Points` tab is selected, then derived rows appear with clear provenance/status and remain reviewable.

10a. Given the `Points` tab is selected, then printed/reference coordinate points are visually distinguishable from solver-derived points and unresolved candidate points.

11. Given metadata is reviewed and saved, then `extraction_review_data.json` contains a stable PXA metadata section suitable for downstream reports, Enterprise popups, and Innola spatial-unit context.

12. Given final reports are generated later, then PXA metadata and segment findings are available as reportable stage findings without reparsing the PDF.

13. Given automated tests run, then coverage proves PXA metadata persistence, segment-save solve/refresh behavior, adjacent-owner segment association, and non-PXA behavior preservation.

14. Given the `Boundary Segments` tab is selected for a PXA review, then the user can add a new boundary segment from the segment tab using the same interaction standard already available for adding point rows.

15. Given a user adds a boundary segment, then the form supports sequence, from point, to point, bearing, distance, adjacent owner, include/use flag, status, and notes, and the new segment is inserted into the reviewed segment collection before save/solve runs.

16. Given a PXA review workspace is active, then PE-only global row action icons must not appear as a floating top toolbar above the PXA tabs.

17. Given a PXA review workspace is active, then row actions are scoped to the active tab: segment actions appear in the `Boundary Segments` tab command area, point actions appear in the `Points` tab command area, and PE/non-PXA keeps its existing point-review controls unchanged.

## Tasks / Subtasks

- [x] Add PXA metadata model and persistence. (AC: 3-5, 11-12)
  - [x] Add a typed survey-plan metadata model under the extraction review document.
  - [x] Preserve raw extracted values, reviewed values, confidence/status, source page/zone, and review notes.
  - [x] Save metadata into `extraction_review_data.json` under a stable section such as `survey_metadata`.
  - [x] Support adjacent-owner records with optional segment association.

- [x] Add PXA-specific review tabs. (AC: 1-6, 10)
  - [x] Add `General Info`, `Owners / Neighbors`, `Boundary Segments`, and `Points` tabs for PXA.
  - [x] Keep document/survey facts such as Volume/Folio, document dates, instrument make/no., and surveyor in `General Info`.
  - [x] Keep parties, owners, representatives, adjacent owners, and neighbor-to-segment associations in `Owners / Neighbors`.
  - [x] Keep source PDF viewer visible beside the tabs.
  - [x] Keep parcel preview and validation findings visible in the right panel.
  - [x] Distinguish printed/reference anchor points, derived points, reviewed boundary segments, and solver conflicts in the PXA review surface.
  - [x] Ensure PE/non-PXA layout remains unchanged.

- [x] Wire segment save to solve and refresh. (AC: 7-10)
  - [x] On `Save Review`, persist segment edits before solving.
  - [x] Run the deterministic boundary solver after segment edits.
  - [x] Write derived/corrected point rows back into the artifact.
  - [x] Refresh the point table and parcel preview from the solved reviewed segment chain.
  - [x] Keep validation incomplete when solver status is blocked.

- [x] Expose metadata for downstream use. (AC: 11-12)
  - [x] Make metadata available to final report generation.
  - [x] Make reviewed PXA metadata available to Enterprise publish/popups where configured.
  - [x] Make relevant metadata available to Innola Spatial Unit creation when mapped.

- [x] Add tests. (AC: 1-13)
  - [x] Test metadata load/save round trip.
  - [x] Test adjacent owner to segment association.
  - [x] Test save action runs solver and updates derived points.
  - [x] Test parcel preview refresh uses reviewed segments.
  - [x] Test PE/non-PXA review UI remains unchanged.

- [x] Add PXA Boundary Segments row creation. (AC: 14-15)
  - [x] Add an `Add segment` command in the `Boundary Segments` tab command area.
  - [x] Reuse the existing segment edit dialog/form pattern so add and edit share field validation and review-state behavior.
  - [x] Default the new segment sequence to the next available segment order and allow the examiner to revise it.
  - [x] Persist newly added segments into `extraction_review_data.json`.
  - [x] Run the deterministic boundary solver after saving added segments and refresh points, blockers, diagnostics, and preview.
  - [x] Add coverage proving an added segment round-trips through save/reload and participates in solve/preview refresh.

- [x] Clean up PXA row-action UX and PE-only toolbar leakage. (AC: 16-17)
  - [x] Hide the current PE/global top row-action icons when the PXA tabbed review workspace is active.
  - [x] Put segment actions inside the `Boundary Segments` tab as a compact tab-local command row: add, edit, delete/exclude as supported.
  - [x] Put point actions inside the `Points` tab as a compact tab-local command row: add, edit, delete/exclude as supported.
  - [x] Keep PE/non-PXA toolbar behavior unchanged.
  - [x] Add tooltips/accessibility labels for icon-only commands and disable commands with clear status text when no row is selected.
  - [x] Add a UI regression test or view-model test proving PXA does not expose the PE global action toolbar.

### Review Findings

- [x] [Review][Patch] Plan Metadata tab omits party/representative and array volume-folio records — AC3 requires parties / owners / representatives and volume/folio values to be displayed and reviewable. The persistence layer loads `document.Parties` and `document.VolumeFolios`, but `LoadReviewDocumentIntoPane` only projects `SurveyMetadataFields` and `AdjacentOwners`, and the XAML only binds `VisibleMetadataFields` plus `VisibleAdjacentOwners`; array `volume_folio`, `parties`, and `representatives` are therefore not reviewable in the PXA tab. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1419`]
- [x] [Review][Patch] PXA detection treats any segmented review artifact as PXA — AC2 says non-PXA review behavior must remain unchanged, but `IsPxaSurveyPlanDocument` returns true whenever `document.Segments.Count > 0`. Any PE/non-PXA artifact that gains segment rows will switch to the PXA tabbed UX. Detection should be based on the PXA profile/extractor/source metadata, not segment presence alone. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:1550`]

## Dev Notes

### Current State

Story 2.19 introduced reviewed segment persistence, the segment grid, and `SurveyPlanBoundarySolver`. The next gap is UX organization and metadata persistence.

The user expectation is:

```text
Reviewed segments guide the process.
Save segment edits -> solve boundary -> update points -> refresh preview.
```

Do not require the examiner to leave and reopen the workspace to see the corrected parcel preview.

### Metadata Shape Recommendation

Use a stable review-friendly artifact shape:

```json
{
  "survey_metadata": {
    "coordinate_system": {
      "value": "JAD 2001",
      "present": true,
      "confidence": 0.9,
      "source_page": 1,
      "source_zone": "title_block",
      "review_status": "accepted",
      "review_notes": null
    },
    "north_arrow": {
      "present": true,
      "confidence": 0.8,
      "source_page": 1,
      "source_zone": "plan_body",
      "review_status": "accepted"
    },
    "parish": {
      "value": "Clarendon",
      "review_status": "accepted"
    },
    "document_area": {
      "value": 854.807,
      "unit": "sq_m",
      "raw_text": "854.807 sq. metres",
      "review_status": "accepted"
    },
    "survey_date": {
      "value": "2024-09-03",
      "raw_text": "September 03, 2024",
      "review_status": "accepted"
    },
    "survey_instrument": {
      "value": "TOPCON GM-52 #1Y013971",
      "review_status": "accepted"
    },
    "surveyed_by": {
      "value": "Michael D. Isaacs",
      "review_status": "accepted"
    },
    "volume_folio": [
      {
        "volume": "313",
        "folio": "71",
        "raw_text": "Vol.313 Fol.71",
        "review_status": "accepted"
      }
    ],
    "parties": [
      {
        "name": "Clayon Smith",
        "role": "party_at_instance",
        "review_status": "accepted"
      }
    ],
    "adjacent_owners": [
      {
        "name": "Glen Alford Battiste",
        "related_segment_from": "18",
        "related_segment_to": "15",
        "volume": "313",
        "folio": "71",
        "source_zone": "north_adjoiner",
        "review_status": "accepted"
      }
    ]
  }
}
```

Exact field names may follow code conventions, but raw/extracted and reviewed values must remain auditable.

### UX Guidance

Use tabs instead of stacking every PXA control in one panel:

```text
General Info | Owners / Neighbors | Boundary Segments | Points
```

The tab labels may show badges such as:

```text
General Info (2 missing)
Owners / Neighbors (1 needs review)
Boundary Segments (blocked)
Points (4 rows)
```

`General Info` is for document and survey facts: coordinate system, north arrow, parish, document area, document dates, survey date, instrument make/no., surveyor, and Volume/Folio. `Owners / Neighbors` is for parties, owners, representatives, adjacent owners, and optional boundary-segment associations. Do not place Volume/Folio in `Owners / Neighbors`; it belongs with `General Info` as two reviewable values.

Keep the PDF visible on the left because the examiner needs to compare field values directly against the plan. Keep the preview visible on the right because segment edits should have immediate spatial feedback.

The PXA review surface must make the construction roles clear:

- Printed/reference points are coordinate anchors.
- Reviewed boundary segments are the construction path.
- Derived points are solver outputs.
- Confirmed-anchor conflicts, closure failures, area mismatches, broken chains, and missing segment references are blockers requiring examiner correction.
- Unconfirmed OCR/reference point conflicts that are resolved by a closed, area-matched reviewed segment chain are warnings, not blockers.

The parcel preview should reflect the solved reviewed segment chain, not stale point-row order.

### Sally UX Recommendation: Tab-Scoped Commands

The three floating action icons above the PXA tabs read as PE point-review controls leaking into the PXA workflow. For a cleaner PXA experience, remove that global icon cluster while the PXA review tabs are active and make commands live where the examiner's attention already is.

Recommended command placement:

```text
Boundary Segments tab
  + Add segment | Edit segment | Exclude/Delete segment

Points tab
  + Add point | Edit point | Exclude/Delete point
```

Keep these as compact icon buttons with tooltips and accessible names, but place them in the active tab header/command strip instead of above the tab set. The visible text button `Edit segment` can be replaced by the same command strip once `Add segment` exists, so the tab has one consistent action model.

Behavior guidance:

- `Add segment` should open the same editor shell as `Edit segment`, with blank/default values and the next sequence number prefilled.
- Disable `Edit` and `Delete/Exclude` until a row is selected.
- Prefer `Exclude from boundary` over destructive delete when the segment came from OCR/source evidence; allow delete only for examiner-added unsaved rows or clearly mark deletion as removing the review row, not the source evidence.
- Keep `Save Review` as the commit point that persists changes and reruns the solver, matching the current segment-edit flow.
- Preserve PE toolbar behavior for non-PXA review so this cleanup does not disrupt the existing PE transaction workflow.

### Validation Message Wording

Replace generic `Needs attention` messaging with three distinct message groups:

```text
Blocking
Reviewed boundary segments cannot complete because {specific rule} failed.
Fix {segment/point/row id} before completing validation.

Warnings
Reviewed boundary segments close and area is within tolerance, but extracted point
coordinates for {point ids} conflicted with the segment-derived coordinates.
Those points were recalculated from reviewed segments.

Resolved by Reviewed Segments
Geometry source: reviewed boundary segments.
Point coordinates updated: {point ids}.
Original OCR/reference coordinates were preserved as source evidence.
```

When the user selects a row that has no row-level blocker but the parcel has a parcel-level blocker, show both facts together, for example:

```text
Selected row has no row-level blocker.
Parcel blocker: closure distance is {value} m, above the configured tolerance of {tolerance} m.
```

Validation Complete is enabled when the `Blocking` group is empty. Warnings and `Resolved by Reviewed Segments` messages remain visible for audit/reporting but do not prevent completion.

### Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewSegmentViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`

## Dependencies

- Builds on Story 2.18: PXA OCR/vision extraction.
- Builds on Story 2.18A: transaction-type workflow profiles.
- Builds on Story 2.19: PXA segment review and deterministic boundary solver.
- Feeds Story 7.9/final report generation by producing reviewed metadata and reportable findings.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-08 | 0.1 | Initial story for PXA metadata review UX, segment-driven save/solve/preview refresh, and metadata persistence. | Codex |
| 2026-07-08 | 1.0 | Implemented PXA metadata persistence, PXA-specific review tabs, adjacent owner segment association, and segment-chain preview refresh. | Codex |
| 2026-07-08 | 1.1 | Patched review findings for party/representative and volume-folio metadata projection plus stricter PXA routing. | Codex |
| 2026-07-08 | 1.2 | Patched UX so PXA uses only the three-tab review workspace and segment-driven preview remains PXA-scoped. | Codex |
| 2026-07-09 | 1.3 | Clarified PXA UX roles for printed/reference anchor points, reviewed boundary segments, derived points, and solver blockers. | Codex |
| 2026-07-12 | 1.4 | Revised PXA review tabs to split `General Info` from `Owners / Neighbors`, with Volume/Folio and survey facts moved to General Info. | Codex |
| 2026-07-12 | 1.5 | Implemented the four-tab PXA review UX split in WPF and added separate General Info / Owners-Neighborhood summaries. | Codex |
| 2026-07-12 | 1.6 | Patched UX wording so validation details separate blockers, warnings, and reviewed-segment resolutions, with Validation Complete gated only by active blockers. | Codex |
| 2026-07-13 | 1.7 | Added follow-up dev requirements for PXA Boundary Segments `Add segment` and Sally UX guidance to remove PE-only global toolbar icons from the PXA tabbed workspace. | Mary / Sally / Codex |
| 2026-07-13 | 1.8 | Implemented PXA tab-scoped segment/point commands, including Add segment and Exclude segment, with regression coverage. | Codex |

## Dev Agent Record

### Debug Log

- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "review persistence saves pxa metadata"`: passed 1 targeted test.
- Ran `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`: passed.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 341 tests.
- Ran `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln` after XAML label alignment: passed.
- Ran `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`: passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "review persistence saves pxa metadata" "review routing requires pxa"`: passed 2 targeted tests.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 342 tests.
- Ran `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln` after PXA tab visibility correction: passed with existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "survey plan solver" "review persistence saves pxa metadata" "review routing requires pxa"`: passed 6 targeted tests.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 342 tests.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "review persistence saves pxa metadata" "review routing requires pxa"` after General Info / Owners-Neighborhood split: passed 2 targeted tests.
- Ran `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`: first attempt hit a transient `VBCSCompiler` file lock, rerun passed with the existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 342 tests.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -- "manual boundary segment" "review persistence saves manual segment" "pxa review xaml"`: passed 3 targeted tests.
- Ran `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`: passed.
- Ran `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`: passed 355 tests.

### Completion Notes

- Added typed PXA survey metadata, party, volume/folio, and adjacent-owner review records to the extraction review document.
- Extended extraction review persistence to load, hash, save, and reload `survey_metadata`, `parties`, `adjacent_owners`, and segment `adjacent_owner` values while preserving raw/source fields.
- Added PXA-only review tabs in the Points Validation Tool, keeping the existing non-PXA stacked point-review layout in place.
- Updated review workspace preview logic so reviewed boundary segment order drives the parcel preview when a reviewed segment chain is available.
- Added persistence regression coverage for PXA metadata and adjacent-owner segment association.
- Patched review finding so the PXA Plan Metadata tab now projects and saves parties/owners, representatives, and array volume/folio records.
- Patched review finding so PXA routing is based on survey-plan source/profile metadata and no longer treats segment presence alone as PXA.
- Corrected the PXA review UX so the center workspace presents only the PXA review tabs; the legacy PE/PA point grid and legacy segment block are hidden while PXA tabs are active.
- Confirmed the save path syncs segment edits, runs the deterministic boundary solver, saves/reloads `extraction_review_data.json`, and refreshes preview bindings. Segment-order preview is now scoped to PXA review only.
- Clarified the PXA review UX requirement so printed/reference coordinate anchors, reviewed boundary segments, derived solver points, and blockers are visually distinct.
- Updated the target PXA tab model to `General Info`, `Owners / Neighbors`, `Boundary Segments`, and `Points`; Volume/Folio, document dates, instrument make/no., and surveyor belong in `General Info`, while parties/owners/representatives/adjacent owners belong in `Owners / Neighbors`.
- Implemented the WPF tab split so PXA review now presents `General Info`, `Owners / Neighbors`, `Boundary Segments`, and `Points`.
- Added separate review workspace summaries for `General Info` and `Owners / Neighbors`; Volume/Folio remains with general survey facts, while parties/representatives/adjacent owners are isolated in the people/neighborhood tab.
- Added a manual boundary segment factory and PXA `Add segment` command that reuses the segment editor, defaults to the next segment sequence, and persists added segments through `extraction_review_data.json`.
- Added PXA tab-scoped command strips: `Boundary Segments` owns add/edit/exclude segment actions, `Points` owns add/edit/remove point actions, and the PE/global point toolbar is hidden while PXA tabs are active.
- Added regression coverage for manual segment defaults, manual segment save/reload, and PXA XAML command scoping.

### File List

- `_bmad-output/implementation-artifacts/2-19-implement-pxa-survey-plan-segment-review-and-deterministic-boundary-solver.md`
- `_bmad-output/implementation-artifacts/2-20-add-pxa-survey-plan-metadata-review-model-and-ux.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/ux-artifacts/pxa-points-validation-metadata-segments-wireframe.png`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ManualBoundarySegmentServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/SegmentEditDialogViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewSegmentViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ManualBoundarySegmentService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PxaSurveyPlanReviewRouting.cs`
