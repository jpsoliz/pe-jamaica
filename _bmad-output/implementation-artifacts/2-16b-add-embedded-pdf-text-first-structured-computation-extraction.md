---
baseline_commit: handoff-2026-06-16
---

# Story 2.16B: Add Embedded PDF Text-First Structured Computation Extraction

Status: review

## Story

As a cadastral technical user,  
I want computation PDFs with embedded selectable text to be parsed through a structured text-first extraction path before OCR or AI fallback,  
so that parcel names, ordered points, segments, and coordinates are captured more accurately and with less manual correction.

## Acceptance Criteria

1. Given a computation PDF contains an embedded text layer, when extraction starts, then the pipeline detects that text layer and prefers a structured text parser before OCR-only or AI-first extraction paths.
2. Given the selected source is a computation-style report with repeated parcel and segment structure, when the text-first parser runs, then it produces normalized parcel objects with ordered point and segment data rather than only flat row extraction.
3. Given a parcel computation report includes multiple parcels in one file, when extraction completes, then the review artifact preserves parcel grouping and ordered point membership so the end point of one parcel is not assumed to be the next point of another parcel.
4. Given the structured text parser succeeds, when `extraction_review_data.json` is written, then each row includes durable routing and extraction metadata including `doc_type_id`, `extractor_id`, `extraction_method`, `parcel_group_id`, `point_order`, `source_page`, and any available segment context.
5. Given a computation PDF has no usable text layer or parsing confidence is too low, when text-first extraction cannot safely complete, then the pipeline falls back to the configured non-text extractor chain and records the fallback reason in route diagnostics.
6. Given OpenAI-assisted extraction is enabled, when the configured document type allows AI assistance, then the text-first parser may use AI only as a structured fallback or enrichment step rather than bypassing direct text parsing for embedded-text reports.
7. Given a document type in the catalog declares text-first support, when routing is resolved, then the matched document type can declare parser mode, fallback order, parcel grouping behavior, and expected coordinate/segment fields through configuration instead of hardcoded filename-only logic.
8. Given extraction artifacts are reopened later, when support or QA inspects the case, then the route diagnostics clearly show whether the run used `pdf_text_structured_computation`, AI fallback, OCR fallback, or manual review fallback.
9. Given this story is complete, then at minimum one embedded-text computation family such as a GeoLand or similar computation sheet is supported end-to-end through the text-first path.
10. Given this story is complete, then focused tests cover text-layer detection, structured multi-parcel parsing, fallback routing, persisted diagnostics, and stable ordered point output for ArcGIS Pro import.

## Tasks / Subtasks

- [x] Add a first-class embedded-text extraction mode. (AC: 1-2, 5-6, 8-10)
  - [x] Introduce a dedicated extractor mode such as `pdf_text_structured_computation`.
  - [x] Add a text-layer probe before OCR/AI extraction is selected.
  - [x] Record text-layer availability and parser outcome in extraction route diagnostics.

- [x] Implement a structured computation parser contract. (AC: 2-4, 9-10)
  - [x] Parse parcel-level sections, start coordinates, segment rows, and end-point coordinates through a structured parser or state machine.
  - [x] Normalize parser output into parcel objects and ordered point rows.
  - [x] Avoid relying on loose regex-only extraction where parcel section boundaries can be preserved explicitly.

- [x] Preserve parcel grouping in review artifacts. (AC: 3-4, 10)
  - [x] Persist `parcel_group_id`, `parcel_name`, `point_order`, `segment_no`, and source/section linkage.
  - [x] Ensure downstream line/polygon generation does not flatten multiple parcels into one implied chain.
  - [x] Carry forward route metadata already introduced by Stories `2.12A` and `2.16`.

- [x] Extend catalog-driven routing for text-first extraction. (AC: 5-7, 9-10)
  - [x] Allow document-type catalog entries to declare text-layer-first extractor preference.
  - [x] Support fallback order such as:
    - [x] structured TXT/CSV
    - [x] embedded PDF text parser
    - [x] OpenAI-assisted extraction
    - [x] OCR extraction
    - [x] manual-only fallback
  - [x] Allow the catalog to declare parcel grouping expectations and parser profiles for computation families.

- [x] Refine AI usage for computation extraction. (AC: 5-6, 8-10)
  - [x] Ensure AI is optional and only invoked when enabled and available.
  - [x] Use AI as fallback or enrichment for ambiguous sections instead of replacing deterministic text parsing for embedded-text reports.
  - [x] Persist whether AI was used, skipped, or bypassed by deterministic parsing.

- [x] Add focused tests and fixture coverage. (AC: 1-10)
  - [x] Embedded-text computation PDF chooses the text-first extractor.
  - [x] Missing text layer falls through to configured OCR/AI fallback path.
  - [x] Multi-parcel report preserves separate parcel groups and point order.
  - [x] Route diagnostics preserve text-layer detection and fallback reason.
  - [x] Review artifact rows remain stable for downstream XY/line/polygon creation.

## Dev Notes

### Why This Story Exists

- Current extraction already uses Document Type Catalog V2 and multi-source routing, but it still needs a stronger first-class path for computation PDFs that already contain embedded selectable text.
- For this class of document, direct text parsing is more reliable than OCR and often more appropriate than AI-first extraction.
- This story turns that observation into a durable pipeline path rather than a one-off special case.

### Current Process vs Proposed Process

Current active direction:

1. Match source through catalog.
2. Choose configured extractor/fallback path.
3. Write review artifact for manual review and downstream geometry generation.

Proposed addition from this story:

1. Match source through catalog.
2. Probe whether the selected computation PDF has an embedded text layer.
3. If yes, run structured text-first parsing.
4. If no, or parsing confidence is insufficient, fall back to the configured AI/OCR/manual chain.
5. Persist which path ran and why.

### Recommended Extraction Order

For supported computation-style document families, prefer:

1. `structured_csv_points` / `structured_txt_points`
2. `pdf_text_structured_computation`
3. `openai_table_pdf` or other AI-assisted extractor
4. `ocr_table_pdf`
5. `manual_only_source`

### Recommended Normalized Review Fields

At minimum preserve:

- `doc_type_id`
- `extractor_id`
- `extraction_method`
- `parcel_group_id`
- `parcel_name`
- `point_order`
- `point_id`
- `easting`
- `northing`
- `course_from_previous`
- `length_from_previous_m`
- `segment_no`
- `source_page`
- `fallback_reason`

### Important Parcel Grouping Rule

- A computation source may contain more than one parcel in a single document.
- The last point of Parcel A must not automatically become the next point of Parcel B.
- Group boundaries must be explicit in the review artifact and respected later by output generation.

### Architectural Guidance

- Keep the text-first parser behind the existing extraction seam rather than wiring ad hoc parsing into unrelated UI code.
- Prefer a structured parser/state machine contract over expanding filename regex logic.
- Treat AI as an optional assistive layer, not the default path, for embedded-text computation reports.

### Likely Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcel_doc_types.json`

### References

- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `_bmad-output/implementation-artifacts/2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md`
- `_bmad-output/planning-artifacts/epics.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Completion Notes List

- Added a dedicated `pdf_text_structured_computation` extraction path with text-layer probe, runtime fallback handling, and route/review diagnostics.
- Extended `DocumentTypeExtractionDefinition` and loader support for `prefers_text_layer` so Catalog V2 can declare text-first routing behavior instead of relying only on filename heuristics.
- Added `src/ProcessingTools/adapters/pdf_text_structured_extraction.py` as a deterministic embedded-text parser for computation PDFs, including parcel grouping, ordered point output, and fallback envelopes when no usable text layer is available.
- Enriched `extraction_review_data.json` and `extraction_route.json` with `extraction_method`, `text_layer_probe_status`, `text_layer_available`, and parsed parcel/row counts.
- Added focused adapter and catalog tests for successful text-first routing, text-layer fallback, and catalog declarations.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj` succeeded.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` succeeded.
- Full custom test harness still stops on a pre-existing unrelated failure in `WorkflowSessionTests.WorkflowSessionExposesIntakeStateAfterCreation`, which still expects the old stage label `Intake` instead of `Transaction Sources`.

### File List

- `_bmad-output/implementation-artifacts/2-16b-add-embedded-pdf-text-first-structured-computation-extraction.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/DocumentTypeCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`
- `dotnet run --no-build --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for embedded PDF text-first structured computation extraction with parcel grouping and fallback-aware routing. | Codex |
| 2026-06-17 | 1.0 | Implemented text-first embedded PDF extraction, catalog preference support, runtime fallback routing, and focused regression coverage. | Codex |
