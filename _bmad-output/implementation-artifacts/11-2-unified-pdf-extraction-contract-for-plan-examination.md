# Story 11.2: Unified PDF Extraction Contract For Plan Examination

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Plan Examination implementation team member,
I want one canonical PDF extraction review contract for unified `PE / Plan Examination`,
so that embedded-text extraction, scanned OCR/vision extraction, Points Validation, validation analysis, and output generation consume the same single-parcel and multi-parcel data shape.

## Business Context

Story `11-1` locked the PE/PXA refactor direction: preserve working behavior, keep extraction separate from analysis, and unify PE/PXA through a stable Plan Examination contract before changing route planning or OCR/vision logic.

The contract must support:

- Mandatory `st_surveyplan` PDF evidence.
- Mandatory `st_surveysheet` PDF evidence.
- Optional `st_autocad_file`.
- Optional `st_survey_points`.
- Current PXA single-parcel survey plan behavior.
- PE multi-parcel survey sheet / computation sheet behavior.
- Existing downstream review, validation, output, Enterprise `working_review`, and Innola finalize behavior.

This story defines and implements the contract boundary only. It must not implement the `11-3` route planner and must not rewrite extraction quality in `11-4`.

## Acceptance Criteria

1. Given a Plan Examination extraction result is produced by any supported PDF route, when it is persisted as `working/extraction_review_data.json`, then the artifact contains canonical top-level contract fields for schema version, transaction identifiers, workflow/profile identifiers, source inventory, route summary reference, extraction quality summary, review status, parcel groups, transaction-wide metadata, parties/owners, memorandum evidence, point rows, boundary segments, findings, and compatibility metadata.
2. Given both `st_surveyplan` and `st_surveysheet` are mandatory source roles for unified Plan Examination, when the contract is loaded, then each source role can be represented with source type, source role, original/copied file name, file path or case-relative path, content kind, page count when known, text-layer status when known, selected extraction route, and source-level warnings/blockers.
3. Given an extracted value appears in metadata, parties, memorandum, points, segments, findings, or parcel groups, when the contract is loaded in C#, then the value can carry source trace: source role, source file, page, zone, optional bounding box, raw text, normalized value, confidence, status, review status, and review notes.
4. Given a document contains one parcel, when the contract is loaded, then it contains a stable parcel group such as `parcel-001` and preserves current PXA-compatible rows, segments, metadata, memorandum, adjacent owners, volume/folio, and review behavior.
5. Given a document contains multiple parcels, when the contract is loaded, then each point row and boundary segment is linked to a stable parcel group, and each parcel group exposes display label, parcel name, lot number, point count, segment count, validation status placeholder, and source evidence.
6. Given legacy PE/PXA artifacts do not yet contain all new canonical fields, when `ExtractionReviewPersistenceService.Load` reads them, then it derives safe defaults without losing existing rows, segments, metadata, memorandum, parties, adjacent owners, volume/folio, or review hash behavior.
7. Given `ExtractionReviewPersistenceService.Save` writes a reviewed artifact, when the saved JSON is inspected, then existing fields needed by validation and output remain present while new canonical contract fields are preserved or written consistently.
8. Given `approved_review.json` currently stores the approved snapshot reference and hash, when the contract is extended, then approval remains compatible with validation/output services and does not require downstream services to read source PDFs.
9. Given `pdf_text_structured_extraction.py` remains the deterministic embedded-text route, when its existing output is normalized, then its rows, segments, parcel groups, volume/folio, fallback reason, source page, and source zone map into the canonical contract.
10. Given `survey_plan_ocr_vision_extraction.py` remains the PXA-compatible scanned/vision route, when its existing output is normalized, then its metadata, memorandum, parties, adjacent owners, volume/folio, points, segments, source profile, provider, and review notes map into the canonical contract without breaking current PXA behavior.
11. Given the Points Validation UX must keep transaction-wide tabs generic and geometry tabs parcel-scoped, when the C# review model is updated, then it exposes enough structured data for transaction-wide source/metadata/party/memorandum/finding views and active-parcel point/segment filtering.
12. Given route planning belongs to `11-3`, when this story is implemented, then it must not add route selection behavior beyond preserving route/source fields already emitted by current extraction paths.
13. Given the story changes review contract behavior, when implementation is complete, then focused tests cover legacy artifact loading, canonical contract round-trip save/load, single-parcel PXA compatibility, multi-parcel parcel-group compatibility, source trace preservation, and downstream approved-review compatibility.

## Tasks / Subtasks

- [ ] Define the canonical Plan Examination review contract fields in C# review model types. (AC: 1, 2, 3, 5, 11)
  - [ ] Add first-class source inventory model(s) for required and optional source roles.
  - [ ] Add first-class source trace model(s) reusable by metadata, parties, memorandum, points, segments, findings, and parcel groups.
  - [ ] Add first-class parcel group model(s) with display label, parcel name, lot number, counts, validation status placeholder, and source evidence.
  - [ ] Add extraction quality summary model(s) for counts, warnings, blockers, and manual-review recommendation.
- [ ] Update `ExtractionReviewPersistenceService.Load` to normalize legacy and current artifacts into the canonical model. (AC: 4, 5, 6, 9, 10)
  - [ ] Preserve current `rows`, `segments`, `survey_metadata`, `document_sections`, parties, adjacent owners, representatives, volume/folio, memorandum, and root metadata.
  - [ ] Derive `parcel-001` for single-parcel artifacts when no explicit parcel group collection exists.
  - [ ] Derive parcel groups from `parcel_group_id`, `parcel_name`, and segment/row membership for multi-parcel artifacts.
  - [ ] Derive source inventory from current root fields such as `primary_source_role`, `primary_source_file`, `secondary_source_files`, `source_profile`, `provider_used`, and route metadata where present.
- [ ] Update `ExtractionReviewPersistenceService.Save` to serialize the canonical fields without breaking validation/output consumers. (AC: 1, 7, 8)
  - [ ] Keep existing `rows`, `segments`, `survey_metadata`, `document_sections`, `review_summary`, `row_count`, `segment_row_count`, and `review_hash` behavior.
  - [ ] Write canonical source inventory, parcel groups, extraction quality summary, and findings using lowercase `snake_case`.
  - [ ] Preserve unknown root metadata for compatibility.
- [ ] Add focused C# tests in the existing executable test harness. (AC: 4, 5, 6, 7, 8, 13)
  - [ ] Test loading a legacy/current PXA-style single-parcel artifact derives one parcel group and preserves metadata/memorandum evidence.
  - [ ] Test loading a multi-parcel PE-style artifact derives multiple parcel groups and preserves row/segment membership.
  - [ ] Test source inventory and source trace fields round-trip through save/load.
  - [ ] Test extraction quality summary fields round-trip through save/load.
  - [ ] Test approval still writes `approved_review.json` and invalidation behavior still works after a reviewed artifact is saved.
- [ ] Update any contract documentation or planning note needed for `11-3` route planner handoff. (AC: 12)
  - [ ] Record the contract field list and compatibility rules in the story completion notes or a planning artifact.
  - [ ] State clearly that route selection remains a later story.

## Dev Notes

### Story Boundary

Implement the contract shape and compatibility normalization only.

Do not:

- Implement the `11-3` route planner.
- Rename visible PXA UI labels.
- Rewrite `pdf_text_structured_extraction.py`.
- Rewrite `survey_plan_ocr_vision_extraction.py`.
- Remove legacy fallback behavior.
- Change Enterprise `working_review` publish or Innola finalize behavior.

### Current Contract State

Current primary review artifact:

- `working/extraction_review_data.json`

Current approval artifact:

- `working/approved_review.json`

Current C# model:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`

Current persistence service:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`

The existing C# model already includes:

- Root schema/version/review fields.
- `Rows` for point review.
- `Segments` for boundary segment review.
- Survey metadata fields.
- Adjacent owners.
- Parties and representatives.
- Volume/folio.
- Memorandum parties, rules, and groups.
- Root metadata preservation through `RootMetadata`.

Known gaps for unified Plan Examination:

- No first-class source inventory collection.
- No reusable source trace object.
- No first-class parcel group collection.
- No first-class extraction quality summary object.
- No unified findings collection with transaction/document/parcel/segment scope.
- Parcel grouping exists on rows/segments but not as its own contract object.

### Required Canonical Top-Level Fields

Use lowercase `snake_case` for persisted JSON fields.

Minimum canonical top-level fields:

- `schema_version`
- `contract_name`
- `contract_version`
- `transaction_number`
- `workflow_code`
- `workflow_name`
- `source_profile`
- `extraction_source`
- `extractor_id`
- `active_extractor_id`
- `provider_used`
- `review_status`
- `review_version`
- `review_hash`
- `review_saved_at`
- `review_saved_by`
- `source_inventory`
- `extraction_route`
- `extraction_quality_summary`
- `parcel_groups`
- `survey_metadata`
- `document_sections`
- `parties`
- `representatives`
- `adjacent_owners`
- `volume_folios`
- `rows`
- `segments`
- `findings`
- `errors`
- `review_summary`
- `compatibility`

Do not remove existing fields that validation/output already use.

### Source Inventory Shape

Each source inventory item should support:

- `source_id`
- `source_type`
- `source_role`
- `required`
- `original_file_name`
- `copied_file_name`
- `case_relative_path`
- `content_kind`
- `page_count`
- `text_layer_status`
- `selected_route`
- `warnings`
- `blockers`

Source role expectations:

- `st_surveyplan` maps to survey plan PDF evidence.
- `st_surveysheet` maps to survey sheet / computation PDF evidence.
- `st_autocad_file` is optional DWG evidence.
- `st_survey_points` is optional coordinate table evidence.

### Source Trace Shape

Source trace must be reusable. It may be stored as an object on new canonical structures while existing scalar fields remain for compatibility.

Minimum fields:

- `source_role`
- `source_file`
- `source_id`
- `source_page`
- `source_zone`
- `bbox`
- `raw_text`
- `normalized_value`
- `confidence`
- `status`
- `review_status`
- `review_notes`

Keep current scalar fields such as `source_page`, `source_zone`, `source_evidence`, `confidence`, and `review_status` where existing consumers expect them.

### Parcel Group Shape

Each parcel group should support:

- `parcel_group_id`
- `display_label`
- `parcel_name`
- `lot_number`
- `source_label`
- `point_count`
- `segment_count`
- `validation_status`
- `source_trace`
- `warnings`
- `blockers`

Display label priority:

1. Parcel name from source document.
2. Lot number from source document.
3. Generated label such as `parcel-001`.

### Extraction Quality Summary Shape

Minimum fields:

- `status`
- `point_count`
- `segment_count`
- `parcel_count`
- `metadata_field_count`
- `party_count`
- `memorandum_detected`
- `warning_count`
- `blocker_count`
- `manual_review_recommended`
- `message`
- `warnings`
- `blockers`

Example message:

`8 points found, 0 segments found, parcel cannot be built yet.`

### Findings Shape

Findings are mixed-scope. Each finding should support:

- `finding_id`
- `scope`
- `parcel_group_id`
- `segment_id`
- `point_id`
- `severity`
- `code`
- `message`
- `source_trace`
- `recommended_action`

Allowed scope values:

- `transaction`
- `document`
- `parcel`
- `segment`
- `point`

### Compatibility Rules

- Legacy/current artifacts without `parcel_groups` must derive parcel groups from rows and segments.
- Single-parcel PXA artifacts should derive `parcel-001` unless an existing parcel id/name is present.
- Existing row and segment scalar fields must remain available after save.
- Existing `review_hash` semantics must remain stable enough that approval invalidation still works after edits.
- Unknown root JSON fields must be preserved through `RootMetadata` unless they conflict with canonical field names.
- `approved_review.json` remains a snapshot/reference artifact and must not require downstream validation/output to read PDFs.

### Existing Extraction Routes To Normalize

Do not rewrite these scripts in this story, but ensure their existing outputs can be loaded into the contract:

- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`

Relevant existing root fields include:

- `source_profile`
- `extraction_source`
- `extractor_id`
- `active_extractor_id`
- `provider_used`
- `primary_source_role`
- `primary_source_file`
- `secondary_source_files`
- `survey_metadata`
- `document_sections`
- `parcel_count_hint`
- `rows`
- `segments`
- `review_notes`
- `errors`
- `fallback_reason`

### Project Structure Notes

Expected files to update:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

Only add new files if the model becomes too large for `ExtractionReviewDocument.cs`; if so, place them under:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/`

Follow repo rules:

- C# properties use PascalCase.
- JSON fields use lowercase `snake_case`.
- Preserve nullable checks and case-insensitive matching.
- Use existing executable test harness, not xUnit/NUnit.
- Do not add new dependencies.

### Testing Requirements

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build
```

If ArcGIS Pro SDK references are unavailable in the current environment, document the build limitation in the Dev Agent Record and still run any feasible pure test/inspection checks.

### Previous Story Intelligence

Story `11-1` locked these decisions:

- `WorkflowSession.cs`, validation, output, Enterprise working review, and Innola finalize behavior are preserved.
- `CreateParcelDraftExtractionAdapter.cs` is adapted later by splitting route planning from route execution.
- `pdf_text_structured_extraction.py` remains the deterministic embedded-text PDF route.
- `survey_plan_ocr_vision_extraction.py` remains PXA-compatible while being adapted into generalized multi-parcel Plan Examination vision extraction.
- `extraction_adapter.py` is not production extraction behavior.
- UX remains two-surface: ArcGIS Pro dockpane process flow plus Points Validation workspace.
- Points and Boundary Segments are active-parcel scoped; general tabs remain transaction/document scoped.

### References

- [Story 11-1](./11-1-inventory-and-decision-lock-for-plan-examination-unification.md)
- [Pipeline inventory](../planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md)
- [Story slicing plan](../planning-artifacts/plan-examination-general/plan-examination-story-slicing-plan.md)
- [Process design](../planning-artifacts/plan-examination-general/plan-examination-process-design.md)
- [Unified review UX](../planning-artifacts/plan-examination-general/plan-examination-unified-review-ux.md)
- [Project context](../project-context.md)

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD
