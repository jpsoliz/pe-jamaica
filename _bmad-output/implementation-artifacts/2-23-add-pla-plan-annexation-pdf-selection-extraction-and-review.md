---
baseline_commit: handoff-2026-08-24
---

# Story 2.23: Add PLA Plan Annexation PDF Selection, Extraction, And Visual Review

Status: superseded-by-split-stories

## Split Notice

This original jumbo story has been split into implementation-ready slices:

- `2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md`
- `2-23b-add-pla-plan-annexation-pdf-page-selection-and-evidence-artifact.md`
- `2-23c-add-pla-selected-plan-extraction-review-and-local-origin-geometry.md`
- `2-23d-add-pla-visual-comparison-and-finalize-upload-flow.md`

Do not implement this parent story directly. Use the split stories above in order.

## Story

As an SMD examiner working a PLA Plan Annexation transaction,
I want to select the plan page or plan area from the transaction PDF, extract annexation geometry from that selected plan evidence, and compare the generated geometry back to the selected source image,
so that old annexation plans can be reviewed and advanced even when they have no usable geographic reference.

## Business Context

PLA is a new Innola transaction type for Plan Annexation. The transaction has an attached plan document with title information at the beginning and a plan/map page at the end. The examiner needs to identify the plan content in the PDF, preserve that selected plan evidence as a generated transaction artifact, extract bearing/distance and optional coordinate evidence from it, validate the resulting polygon shape, and visually compare the generated geometry to the selected plan image.

Two sample PDFs were provided as evidence, not as instructions:

- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\1000-55.pdf`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\1150-100.pdf`

Observed sample characteristics from local inspection:

- Both PDFs are 2 pages.
- Both pages are US Letter sized: 612 x 792 PDF points.
- Embedded text extraction returned 0 characters for every page, so the examples are image-only PDFs.
- The likely plan/map content is therefore OCR/vision and image-rendering work, not embedded-text parsing.

## Product Decision

Implement full-page plan selection first, with the model and artifact schema ready for rectangular crop support later.

Rationale:

- Full-page selection is lower risk because existing PDF page rendering and source-document viewer patterns can be reused.
- Full-page selection is easier to test and easier for users to understand during the first PLA pilot.
- Rectangular crop is still valuable because it reduces OCR/vision noise and produces a cleaner evidence attachment, but it requires image-region selection UX, coordinate conversion between displayed page pixels and PDF/page pixels, crop persistence, and more visual QA.
- The first implementation must not block future crop support. Persist selection metadata as a region object where `selection_type = "full_page"` for MVP and `selection_type = "rectangle"` can be added later.

## Acceptance Criteria

1. Given an Innola transaction has type/code/name `PLA` or `Plan Annexation`, when the transaction is loaded, then the add-in treats it as a supported transaction type and assigns it to the PLA workflow profile.

2. Given a PLA transaction is loaded, when source attachments are copied into the case folder, then the required plan annexation document type/source type is `st_plan_annexation_pdf`.

3. Given the required `st_plan_annexation_pdf` attachment is missing, unreadable, or not a PDF, when Supporting Document Check or Structure Check runs, then the workflow reports a blocking finding and does not silently route the transaction through PE/PXA extraction.

4. Given a PLA source PDF is available, when the examiner starts the PLA plan selection step, then the workspace lists the transaction PDF attachments and lets the examiner select the source PDF and page containing the plan/map.

5. Given the PDF has multiple pages, when the examiner selects a page, then the selected page number is persisted in the case folder and is not assumed from a fixed page position.

6. Given rectangular crop support is not implemented in this first slice, when the examiner selects a plan page, then the system records the selection as `selection_type = "full_page"` and uses the full rendered page as the extraction area.

7. Given the selected page is rendered, when the selection is saved, then the system creates a generated plan evidence artifact under the transaction case folder as a PDF if practical; if PDF generation is not practical in the implementation environment, it may generate a PNG with a clear artifact type and reason.

8. Given the generated plan evidence artifact is created, when the case artifacts are listed, then the artifact is visible as an internal/generated PLA plan evidence document and is not uploaded to Innola until the final PRO stage.

9. Given extraction runs for PLA, when a selected plan page artifact exists, then OCR/vision extraction is run against the selected plan evidence, not against unrelated title-information pages.

10. Given the selected plan evidence contains bearings and distances, when extraction completes, then `extraction_review_data.json` contains reviewable boundary segment rows with sequence, from point, to point, bearing, distance, source page/region, confidence/status, and review notes.

11. Given the selected plan evidence contains a coordinate table or coordinate labels, when extraction completes, then available coordinates are captured as reviewable point rows; absence of coordinates must be recorded as explicit no-coordinate/georeference evidence rather than an extraction failure.

12. Given the extracted boundary segments are sufficient to form a closed polygon, when the user rebuilds or validates points, then the existing deterministic boundary solver creates the parcel geometry from bearings/distances.

13. Given no usable coordinate/georeference evidence exists, when geometry is created, then the system creates local unreferenced geometry using a local origin such as `(0,0)` and records that the geometry is form-valid but not geographically referenced.

14. Given usable coordinates or georeference evidence exists, when geometry is created, then the system uses the existing georeference readiness/placement path where practical and records the source evidence used.

15. Given geometry is generated from the selected plan evidence, when Validate Points and Lines runs, then the examiner can review and edit the point/segment rows using the existing review workspace patterns before approving.

16. Given the reviewed PLA geometry is approved, when validation runs, then the polygon is validated for shape, closure, parseable bearings/distances, duplicate/missing points, and geometry construction readiness.

17. Given the generated geometry is local/unreferenced, when validation reports status, then the workflow distinguishes geometry shape validity from geographic placement validity and does not block solely because there is no geographic reference.

18. Given the selected plan image and generated geometry both exist, when visual review opens, then the examiner can compare the selected source plan evidence against the generated geometry using a side-by-side or overlay-style visual similarity review.

19. Given the visual comparison is approximate, when the reviewer accepts or flags the comparison, then the result is persisted as review evidence without claiming survey-accurate alignment.

20. Given PLA output documents are generated during the workflow, when the transaction reaches the final PRO step, then generated output documents, including the selected plan evidence PDF/PNG and generated geometry visual artifact, are saved/attached according to the PRO-stage behavior, not earlier.

21. Given the transaction is reopened, when the case folder contains PLA selection, extraction, review, and visual comparison artifacts, then the workflow restores those artifacts and does not require the examiner to repeat page selection or extraction unless they choose to rerun.

22. Given existing PE, PXA, M-Geo, Compare, and title-plan image-placement workflows exist, when PLA is added, then those workflows keep their current routing, source types, stage gates, and artifacts.

## Tasks / Subtasks

- [ ] Add PLA transaction routing and source requirements. (AC: 1-3, 22)
  - [ ] Add `PLA` / `Plan Annexation` to supported transaction type handling.
  - [ ] Add a PLA workflow profile with required source type `st_plan_annexation_pdf`.
  - [ ] Keep PLA separate from PE computation-sheet and PXA survey-plan PDF profile matching.
  - [ ] Add or update structure/preflight rules for missing/unreadable PLA plan PDFs.

- [ ] Add selected plan evidence model and persistence. (AC: 4-8, 20-21)
  - [ ] Add case-folder artifact naming for selected PLA plan evidence.
  - [ ] Persist source path, attachment/source metadata, selected page number, selection type, page dimensions, generated artifact path, and timestamp.
  - [ ] Represent the MVP selection as `selection_type = "full_page"`.
  - [ ] Design the schema so rectangular crop can later add x/y/width/height without breaking existing full-page artifacts.
  - [ ] Generate a selected-plan evidence PDF when practical, with PNG fallback and explicit reason when PDF is unavailable.

- [ ] Build PLA page selection UX. (AC: 4-8, 21-22)
  - [ ] Reuse existing Supporting Documents / PDF viewer patterns for transaction attachments.
  - [ ] Let the examiner choose the source PDF and plan page.
  - [ ] Show selected source file and page in the workspace.
  - [ ] Save/reopen the selection from case artifacts.
  - [ ] Do not require rectangular crop in the MVP.

- [ ] Route PLA extraction to selected plan evidence. (AC: 9-11, 22)
  - [ ] Reuse the OCR/vision extraction pattern from PXA where applicable.
  - [ ] Ensure extraction input is the selected plan evidence artifact/page, not the whole title PDF unless no selected artifact exists.
  - [ ] Extract bearing/distance boundary segments with reviewability, confidence, source page/region, and notes.
  - [ ] Extract coordinate rows when visible.
  - [ ] Emit explicit no-coordinate/no-georeference evidence when the plan lacks usable coordinate control.

- [ ] Reuse boundary review and solver behavior for PLA. (AC: 12-17)
  - [ ] Load extracted PLA segments and optional points into the existing point/segment review workspace or a PLA-specific variant that preserves the same review contracts.
  - [ ] Rebuild geometry from reviewed bearings/distances.
  - [ ] Support local-origin geometry when no geographic control exists.
  - [ ] Validate closure, parseability, duplicate/missing labels, area/shape where available, and construction readiness.
  - [ ] Make unreferenced/local geometry a reportable condition, not an automatic blocker.

- [ ] Generate geometry visual evidence. (AC: 18-20)
  - [ ] Produce a visual artifact of the generated geometry suitable for comparison with the selected plan evidence.
  - [ ] Use existing ArcGIS map/screenshot or geometry-preview patterns where practical.
  - [ ] Persist the generated visual artifact under the case folder.
  - [ ] Defer Innola attachment/upload until PRO-stage behavior.

- [ ] Add visual similarity review. (AC: 18-19)
  - [ ] Provide side-by-side or overlay-style comparison between selected plan evidence and generated geometry visual artifact.
  - [ ] Record reviewer decision/status and notes.
  - [ ] Label comparison as approximate visual similarity, not survey-accurate georeferencing.

- [ ] Add tests and fixtures. (AC: 1-22)
  - [ ] Add fixture copies or redacted equivalents for `1000-55.pdf` and `1150-100.pdf` under the repo fixture tree.
  - [ ] Test PLA transaction profile routing and required `st_plan_annexation_pdf` source type.
  - [ ] Test image-only PDF probe behavior with zero embedded text.
  - [ ] Test full-page selected-plan artifact metadata persistence.
  - [ ] Test OCR/vision adapter routing receives the selected plan evidence artifact.
  - [ ] Test local-origin geometry creation when no coordinates are available.
  - [ ] Test validation distinguishes shape validity from geographic reference availability.
  - [ ] Test reopen restores PLA selection and generated artifacts.
  - [ ] Test PE/PXA routing is unchanged.

## Dev Notes

### Mary Requirement View

This is a new transaction workflow, but it should not become a new application. PLA should extend the existing transaction-profile, case-folder, source-document, extraction-review, validation, output, and final closeout patterns.

The user clarified:

- Innola transaction type is PLA / Plan Annexation.
- Required source type is `st_plan_annexation_pdf`.
- User should select the relevant plan evidence. Full page is acceptable for the first implementation if rectangular crop is too complex.
- Preferred generated selected-plan artifact is PDF if practical.
- Generated output documents should be saved/attached at the final PRO step, not earlier.
- If no georeference is available, geometry should still be generated in a local coordinate space from `(0,0)` or equivalent.
- Matching selected plan evidence against generated geometry is a visual overlay/similarity review only.

### Winston Architecture View

Recommended first-slice architecture:

- Add PLA as a profile-driven route, parallel to PXA, not hardcoded into PE.
- Keep source-role detection and required attachment rules in settings/catalog paths.
- Add a selected-plan artifact contract, for example:

```json
{
  "schema_version": "1.0.0",
  "transaction_number": "string",
  "source_type": "st_plan_annexation_pdf",
  "source_relative_path": "source/...",
  "selected_page_number": 2,
  "selection_type": "full_page",
  "selection_region": null,
  "page_width_points": 612,
  "page_height_points": 792,
  "generated_plan_evidence_path": "working/pla_selected_plan.pdf",
  "generated_plan_evidence_format": "pdf",
  "created_at_utc": "date-time",
  "updated_at_utc": "date-time"
}
```

- Feed the selected evidence artifact into the existing OCR/vision extraction seam, adding a PLA profile/prompt where needed.
- Reuse `extraction_review_data.json` for reviewable point and segment rows.
- Reuse the boundary solver for reviewed bearings/distances.
- Add explicit metadata for local/unreferenced geometry, for example `geometry_reference_mode = "local_origin"` or `georeference_status = "not_available"`.
- Reuse visual artifact patterns from map/georeference/title-plan image placement stories where practical.

### Amelia Implementation View

Likely source areas to inspect and preserve:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/StructureRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceOverlayService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/*`

Preserve these constraints from project context:

- Keep ArcGIS Pro SDK map/layer work behind service seams and `QueuedTask.Run`.
- Do not block the WPF UI thread with PDF rendering, OCR/vision, file IO scans, or map work.
- Python/PDF/OCR processing should stay behind adapter boundaries and write contract-compliant JSON artifacts.
- Do not bypass review-before-output workflow.
- Do not upload or attach generated artifacts to Innola before the configured final PRO step.
- Do not treat Enterprise working layers or local generated geometry as final authoritative promotion unless a later story explicitly does that.

### Relationship To Existing Stories

Reuse from Story 2.18:

- Image-only scanned PDF detection.
- OCR/vision survey-plan extraction path.
- `survey_plan_extraction_summary.json`, `extraction_review_data.json`, and `extraction_route.json` style artifacts.
- Reviewable points, segments, metadata, confidence, source page/zone, and manual-review fallback.

Reuse from Story 2.19:

- Editable boundary segment review.
- Deterministic boundary solving from reviewed bearing/distance segments.
- Closure and area validation where sufficient.

Reuse from Story 5.28:

- Source PDF/page selection ideas.
- Render selected scanned page to a stable image.
- Visual comparison/overlay language and warnings.

Important difference from Story 5.28:

- PLA does need COGO/geometry reconstruction from selected plan evidence.
- 5.28 is reference-image placement and explicitly out of scope for COGO reconstruction.

### Open Questions For Later Slices

- Should rectangular crop be added immediately after full-page MVP, or only after real PLA pilot feedback?
- Should visual similarity review be a side-by-side image comparison first, with overlay added later?
- What exact PRO-stage API/source type should be used to save generated artifacts back to Innola?
- Should generated local-origin geometry be written to the transaction GDB immediately after validation, or only after visual similarity review is accepted?

## References

- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `_bmad-output/implementation-artifacts/2-19-implement-pxa-survey-plan-segment-review-and-deterministic-boundary-solver.md`
- `_bmad-output/implementation-artifacts/5-28-add-assisted-title-plan-image-placement-for-map-comparison.md`
- Sample evidence: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\1000-55.pdf`
- Sample evidence: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\1150-100.pdf`

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

### Completion Notes List

### File List
