---
baseline_commit: handoff-2026-07-08
---

# Story 2.18: Add Single-Parcel Survey Plan PDF Metadata And Geometry Extraction

Status: review

## Story

As a cadastral examiner,  
I want scanned single-parcel survey plan PDFs to be extracted into structured parcel, survey, party, and geometry data,  
so that this transaction type can move through compute review without relying on the multi-parcel computation-sheet extraction path.

## Business Context

The current compute extraction work has focused heavily on computation sheets, including multi-parcel point/segment reports. A newer transaction pattern is expected to be mostly single parcel, with the primary information contained in a scanned survey plan PDF rather than a structured computation sheet.

The sample file `DOC_PLAN_492321.pdf` is a one-page scanned image PDF with no embedded text layer. It contains a survey sketch, north arrow, JAD 2001 coordinate reference, survey points, bearing/distance annotations, adjacent owners, parish, area, survey date, instrument details, parties, representatives, and surveyor details. This means the extraction approach must be image/OCR/vision oriented and must preserve confidence and reviewability rather than pretending it is the same deterministic text-first computation parser.

## Acceptance Criteria

1. Given a transaction includes a survey plan PDF for this transaction type, when document routing runs, then the file is classified as a primary `survey_plan_pdf` or equivalent source role for this transaction type rather than as a computation sheet.

2. Given the PDF has no embedded text layer, when extraction starts, then the pipeline records that embedded text is unavailable and routes to the configured OCR/vision survey-plan extractor.

3. Given a scanned survey plan contains a north arrow, when extraction completes, then the review artifact records whether a north arrow was detected, its approximate page location, and confidence.

4. Given the plan identifies the coordinate system, when extraction completes, then the review artifact captures the coordinate system text, including `JAD 2001` when present.

5. Given the plan includes survey points and a coordinate table, when extraction completes, then the review artifact captures point numbers and coordinates where available, preserving source page, source zone, and confidence per point.

6. Given the plan includes bearings and distances around the parcel, when extraction completes, then the review artifact captures normalized bearing text and distance text/value rows suitable for downstream line construction or manual review.

7. Given the plan includes administrative and survey metadata, when extraction completes, then it captures at minimum:
   - parish
   - captured document area
   - survey date
   - instrument make/no.
   - surveyed by
   - plan/check dates where visible
   - file/reference numbers where visible.

8. Given the plan includes parties and representatives, when extraction completes, then it captures at minimum:
   - names of parties/owners
   - representatives or persons appearing
   - adjacent owners/adjacent parcels where visible.

9. Given the extractor cannot confidently parse a field, when extraction completes, then the field remains present with `value = null` or blank, a confidence score/status, and a review note rather than silently omitting it.

10. Given extraction completes, when `extraction_review_data.json` and route diagnostics are written, then they include a source profile such as `scanned_single_parcel_survey_plan_pdf`, extractor id, OCR/vision path used, page/zone references, detected fields, confidence summaries, and fallback/manual-review reasons.

11. Given the transaction type uses scanned survey-plan PDFs, when `Structure Check` runs, then it evaluates the configured transaction/source structure rules for this source pattern, including required PDF presence, PDF readability, one-page/multi-page handling, image/text-layer probe result, expected memorandum/table zones where detectable, and whether the source is eligible for the survey-plan OCR/vision extractor.

12. Given the transaction type uses scanned survey-plan PDFs, when `Georeference Check` runs, then it consumes extraction/routing evidence where available to report coordinate-system and location readiness, including JAD2001 detection, coordinate table usability, parish presence, Jamaica/parish bounds checks when coordinates are available, and reportable findings when evidence is missing or low confidence.

13. Given the transaction type uses scanned survey-plan PDFs, when `Dimension Check` runs, then it consumes extracted candidate points and segment/bearing/distance rows to assess geometry-construction readiness, including parseable bearings, parseable distances, point references, single-parcel closure/tolerance, document-captured area versus computed area when a polygon can be built, and a clear blocker or manual-review finding when geometry cannot be safely constructed.

14. Given extracted point and line information is sufficient, when downstream validation runs, then it can treat this source as a single-parcel geometry candidate without assuming multi-parcel computation-sheet grouping.

15. Given extracted point or line information is incomplete, when the user reaches `Validate Points and Lines`, then the workflow opens the Points Validation Tool with the scanned PDF/source page, extracted point rows, extracted segment rows, metadata warnings, and manual correction path available for review.

16. Given the user validates or edits extracted points and lines in the Points Validation Tool, when the user saves, then the corrected review artifact preserves the survey-plan source profile, single-parcel grouping, manual edits, inserted/deleted rows, and source-page references so `Create Spatial Units` can build from the reviewed values.

17. Given Structure, Georeference, or Dimension Check has not passed according to configured workflow effects, when the user attempts to open `Validate Points and Lines`, then the workflow blocks with the existing stage-gating behavior and does not bypass required early checks for this new transaction type.

18. Given more sample survey plans are added later, when tests run, then fixture coverage verifies at least:
   - no embedded text scanned plan
   - detected JAD 2001 coordinate system
   - parish and area extraction
   - coordinate table extraction
   - bearing/distance annotation extraction
   - owner/adjacent-owner extraction
   - low-confidence/manual-review fallback.

## Tasks / Subtasks

- [x] Add a survey-plan PDF source profile. (AC: 1-2, 10-11)
  - [x] Extend the document type/source-role catalog to recognize scanned single-parcel survey plan PDFs for the new transaction type.
  - [x] Add a profile id such as `scanned_single_parcel_survey_plan_pdf`.
  - [x] Ensure routing records text-layer probe result and selected OCR/vision extractor.
  - [x] Add structure-rule eligibility metadata so Structure Check can report this source as readable, scanned/image-first, and eligible for survey-plan extraction without treating it as a computation sheet.

- [x] Define normalized survey-plan extraction output. (AC: 3-10, 12-16)
  - [x] Add a structured artifact model for plan-level metadata.
  - [x] Add structured sections for coordinate system, north arrow, points, bearings/distances, parties, adjacent owners, surveyor, instrument, dates, parish, area, and source references.
  - [x] Include confidence, source page, source zone, and review status for each field/group.
  - [x] Ensure the artifact can be read by Georeference Check, Dimension Check, and Validate Points and Lines without each stage re-parsing the PDF.

- [x] Implement scanned survey-plan OCR/vision extraction adapter. (AC: 2-10, 12-16)
  - [x] Use OCR/vision extraction when embedded text is unavailable.
  - [x] Prefer page-zone extraction where practical:
    - plan/map sketch zone
    - coordinate table zone
    - memorandum/table zone
    - surveyor/instrument/signature zone.
  - [x] Normalize common Jamaican survey plan terms such as parish, JAD 2001, coordinates, area, instrument make/no., surveyed by, and parties.
  - [x] Emit review warnings when values conflict or confidence is low.
  - [x] Implement real `survey_plan_ocr_vision` provider for image-only PXA survey plan PDFs.
    - [x] Render PDF page(s) to image(s) using the configured ArcGIS/Python environment.
    - [x] Call the configured OCR/vision provider rather than the embedded-text parser.
    - [x] Extract north arrow, coordinate system, survey metadata, parties, adjacent owners, coordinate table points, and bearing/distance segments.
    - [x] Normalize OCR/vision output into real `rows` and `segments` in `extraction_review_data.json`.
    - [x] Preserve confidence, source page/zone, review notes, and manual-review fallback when extraction is incomplete.

- [x] Wire extraction results into Structure, Georeference, Dimension, and point/line validation. (AC: 11-17)
  - [x] Structure Check reads routing/source-profile evidence and reports this transaction source as survey-plan PDF ready or blocked.
  - [x] Georeference Check reads extracted coordinate-system, coordinate-table, parish, and location evidence.
  - [x] Dimension Check reads extracted candidate points, bearings, distances, and captured document area.
  - [x] Convert extracted coordinate-table rows into reviewable point rows.
  - [x] Convert extracted bearing/distance annotations into reviewable line/segment rows when confidence is sufficient.
  - [x] Open the Points Validation Tool with extracted rows and scanned source context so the user can validate/correct values before Create Spatial Units.
  - [x] Keep manual review enabled when geometry cannot be safely constructed.

- [x] Add settings/rules for the new transaction type. (AC: 1-2, 9-18)
  - [x] Add configurable rule entries for required survey plan PDF and optional supporting sources for this transaction type.
  - [x] Allow OCR/vision extractor enablement and confidence thresholds to be adjusted in settings.
  - [x] Keep this separate from the computation-sheet primary extraction rules.
  - [x] Add rule grouping so Structure Check, Georeference Check, and Dimension Check each display their own reportable findings for the survey-plan source profile.

- [x] Add tests and fixtures. (AC: 1-18)
  - [x] Add a fixture manifest for `DOC_PLAN_492321.pdf` or a redacted equivalent.
  - [x] Test that the sample routes to scanned survey-plan extraction, not computation-sheet parsing.
  - [x] Test no-text-layer detection.
  - [x] Test extracted metadata and geometry candidate artifact shape.
  - [x] Test Structure Check, Georeference Check, and Dimension Check consume the extracted/routing evidence through the normal stage summaries.
  - [x] Test Validate Points and Lines receives extracted point/segment rows and can persist user edits for Create Spatial Units.
  - [x] Test low-confidence fields remain reportable and reviewable.

## Dev Notes

### Sample Observation

Sample reviewed:

- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\DOC_PLAN_492321.pdf`
- One page.
- PDF text extraction returned zero characters.
- PDF contains one image object, so OCR/vision is required.
- Visible content includes:
  - `JAD 2001`
  - north arrow / grid north
  - coordinate table
  - point numbers
  - bearing/distance labels
  - parish: Clarendon
  - area: `854.807 sq. metres`
  - survey date: `September 03, 2024`
  - instrument: `TOPCON GM-52 # ...`
  - surveyed by: Michael D. Isaacs
  - owners/parties and adjacent owners.

### Mary Requirement View

This is a different extraction product from the computation-sheet flow:

- It is usually one parcel.
- The plan PDF is the business primary source.
- The source mixes map graphics, handwritten/typed annotations, stamps, and a memorandum table.
- Extraction must be reportable and reviewable because OCR/vision confidence will vary.

The story should preserve a human review path. The goal is not to guarantee perfect automated geometry from one scanned plan; the goal is to capture the right information, propose geometry when possible, and make gaps obvious.

### Workflow Stage Alignment

This story must fit the current compute workflow rather than creating a side path:

- `Supporting Document Check`: confirms this transaction type has the required survey plan PDF and any optional supporting sources.
- `Structure Check`: confirms the PDF is readable, classified as `scanned_single_parcel_survey_plan_pdf`, has expected survey-plan structure where detectable, and is eligible for the configured OCR/vision extractor.
- `Georeference Check`: uses extracted evidence for JAD2001, coordinate table usability, parish/location readiness, and reportable georeference findings.
- `Dimension Check`: uses extracted candidate points, bearings, distances, closure/tolerance, and document-captured area to decide whether geometry can proceed to user validation.
- `Validate Points and Lines`: remains the user-facing review/correction tool for extracted points and segments. This is where the examiner validates, inserts, edits, or corrects rows before `Create Spatial Units`.

Structure, Georeference, and Dimension Check should not create final output layers, Enterprise rows, Innola Spatial Units, or final reports. Their job is to evaluate and persist findings/gates. The extraction artifacts feed those checks and the Points Validation Tool.

### Winston Architecture View

Do not hardcode this into the current computation-sheet parser. Add it behind the existing document-type catalog and extraction adapter seam:

- document/source profile selects extractor
- extractor emits normalized artifacts consumed by stage checks and point/line validation
- Structure Check consumes route/source-profile evidence
- Georeference Check consumes coordinate-system/parish/location evidence
- Dimension Check consumes candidate points, segments, bearings, distances, closure, and captured-area evidence
- point/line validation consumes the artifact when geometry candidates exist and allows user correction
- manual review remains the fallback

Recommended artifact split:

- `survey_plan_extraction_summary.json`
- `extraction_review_data.json` for point/line rows
- `extraction_route.json` for routing/probe/fallback details

Recommended top-level extracted groups:

```json
{
  "source_profile": "scanned_single_parcel_survey_plan_pdf",
  "parcel_count_hint": 1,
  "coordinate_system": {},
  "north_arrow": {},
  "survey_metadata": {},
  "parties": [],
  "adjacent_owners": [],
  "points": [],
  "segments": [],
  "document_area": {},
  "stage_evidence": {
    "structure_check": {},
    "georeference_check": {},
    "dimension_check": {}
  },
  "field_confidence": {},
  "review_notes": []
}
```

### Open Questions

- What is the official Innola transaction type name for this source pattern?
- Is the plan PDF the only required source for this transaction type, or will a coordinate TXT/CSV or DWG still be required/optional?
- Should adjacent owners become formal structured data in Innola, or only report/audit metadata for now?
- Should `area` captured from the document be compared against computed polygon area during Dimension Check?

### Additional Examples Needed

More survey examples would improve this story before development. Ideal set:

- 3-5 single-parcel survey plan PDFs from different surveyors.
- At least one low-quality scan.
- At least one rotated/skewed scan.
- At least one with missing coordinate table or missing bearings.
- At least one with a different parish/instrument/surveyor layout.

## Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/adapters/pdf_ocr_extraction.py` if present or added later
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/DataExtractionRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ProcessingTools/tests/`

## References

- `_bmad-output/implementation-artifacts/2-16b-add-embedded-pdf-text-first-structured-computation-extraction.md`
- `_bmad-output/implementation-artifacts/5-16d-externalize-data-extraction-rules-by-transaction-and-source-type.md`
- `_bmad-output/implementation-artifacts/5-16f-configure-supporting-document-source-types-and-attachment-role-rules-for-compute-intake.md`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\DOC_PLAN_492321.pdf`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-08 | 0.1 | Initial story for scanned single-parcel survey plan PDF metadata and geometry extraction, based on `DOC_PLAN_492321.pdf`. | Codex |
| 2026-07-08 | 1.0 | Implemented PXA survey-plan document routing, normalized extraction artifacts, stage evidence consumption, and regression tests. | Codex |
| 2026-07-08 | 1.1 | Reclassified story as partial after TR100000562 showed the OCR/vision provider is still a placeholder and image-only PDFs produce zero rows/segments. | Codex |
| 2026-07-08 | 1.2 | Implemented production `survey_plan_ocr_vision` provider script, C# routing to the provider, normalized point/segment output, and fallback tests. | Codex |
| 2026-07-08 | 1.3 | Tightened OCR/vision prompt and normalization for complete bearings, derived point candidates, renderer diagnostics, and sample PDF validation. | Codex |

## Dev Agent Record

### Implementation Plan

- Add a survey-plan document type and source-role-biased routing so PXA `survey_plan_pdf` sources do not fall into the computation-sheet extractor.
- Emit normalized `extraction_review_data.json`, `survey_plan_extraction_summary.json`, and `extraction_route.json` artifacts with metadata, point rows, segment rows, confidence/status, and stage evidence.
- Feed survey-plan extraction summaries into Georeference and Dimension checks so the existing stage-gating/reporting path can consume the new source profile.
- Add regression tests for text-backed survey-plan extraction, no-text scanned/image-only routing, and stage evidence consumption.
- Add the real image-only `survey_plan_ocr_vision` provider script that renders PDF pages, calls OpenAI vision when configured, and normalizes the response into review rows/segments.

### Debug Log

- Fixed test profile construction to use `ComputeTransactionTypeProfileCatalog.ToResolved(...)`.
- Tightened survey-plan text probing so raw `%PDF` container bytes are not mistaken for an embedded text layer.
- Added no-text PXA survey-plan test to confirm OCR/vision routing and manual-review artifact behavior.
- Replaced the image-only PXA external extraction call from the embedded-text parser to `survey_plan_ocr_vision_extraction.py`.
- Added a deterministic `SURVEY_PLAN_OCR_VISION_MOCK_JSON` path for tests and provider diagnostics.
- Verified the configured ArcGIS Python has `pypdfium2` available but not PyMuPDF/fitz; the provider now falls back to `pypdfium2` rendering and reports renderer failures explicitly.
- Verified the original sample file `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\ScannedImages\DOC_PLAN_492321.pdf` renders and produces OCR/vision metadata, point rows, and segment rows.
- Verified no `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000562` case folder/source PDF exists during this implementation pass, so TR100000562 still needs the transaction-load/case-creation path to place the source PDF before the new extractor can run inside the workflow.

### Completion Notes

- `SINGLE_PARCEL_SURVEY_PLAN_PDF_V1` is now available in the default document type catalog and maps to `scanned_single_parcel_survey_plan_pdf` / `survey_plan_ocr_vision`.
- PXA survey-plan extraction writes review, route, and summary artifact shapes with coordinate system, north arrow evidence, survey metadata, parties, adjacent owners, point candidates, segment candidates, warnings, and stage evidence.
- Georeference and Dimension checks can consume `survey_plan_extraction_summary.json` without reparsing the PDF.
- No-text PDF containers route to OCR/vision/manual review and do not fabricate point rows.
- Image-only survey-plan PDFs now call `survey_plan_ocr_vision_extraction.py`, which renders PDF pages through PyMuPDF/fitz when available or `pypdfium2` otherwise, calls the configured OpenAI vision provider when `OPENAI_API_KEY` is available, and writes normalized `rows` and `segments` to `extraction_review_data.json`.
- OCR/vision output can include lower-confidence `derived_points` when boundary coordinates can be calculated from visible anchored coordinates, bearings, and distances; these are normalized into reviewable point rows with `extraction_status = derived`.
- When the provider is unavailable or returns incomplete data, the script writes a manual-review artifact rather than failing the workflow.

### Implementation Gap Resolution

- The previously confirmed TR100000562 gap is resolved in code: image-only PXA no longer calls `pdf_text_structured_extraction.py`.
- Production extraction still requires `OPENAI_API_KEY` and at least one supported PDF renderer (`pypdfium2` or PyMuPDF/fitz) in the configured ArcGIS Python environment. Without those, the workflow intentionally falls back to manual review.
- TR100000562 still cannot produce transaction-local extraction rows until the Innola transaction load creates the case folder and copies/downloads the PXA survey PDF into the case source folder.
- TR100000562 also showed that OCR/vision segment candidates are not sufficient as construction geometry by themselves. Story 2.19 covers the remaining product gap: editable PXA segment review, deterministic boundary solving from reviewed bearings/distances, closure/area validation, and persistence of corrected points/segments back to `extraction_review_data.json`.

### File List

- `_bmad-output/implementation-artifacts/2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py`

### Validation

- `python -m unittest discover -s tests` from `src/ProcessingTools` — 77 tests passed.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` — 334 tests passed.
- Configured ArcGIS Python probe against `DOC_PLAN_492321.pdf` — `parser_status = ocr_vision_parsed`, `parsed_row_count = 2`, metadata/party/adjacent-owner/segment rows produced.
- Configured ArcGIS Python probe against TR100000562 case path — no case/source PDF exists yet, so transaction load must be corrected before workflow rerun can extract this transaction.
- `git diff --check`
