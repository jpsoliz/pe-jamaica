---
baseline_commit: handoff-2026-06-16
---

# Story 2.12A: Introduce Document Type Catalog V2 For Extraction Routing

Status: review

## Story

As a cadastral workflow maintainer,  
I want the document-type catalog evolved into a versioned routing contract,  
so that many source document families can be identified, extracted, validated, and reviewed through configuration instead of a growing list of filename-only special cases.

## Acceptance Criteria

1. Given the add-in currently relies on a lightweight document-type catalog plus adapter-side filename matching, when Catalog V2 is introduced, then the catalog exposes a versioned schema with explicit sections for `match`, `extraction`, `schema`, `geometry`, `validation`, `review`, and `output`.
2. Given a document type is defined in Catalog V2, when the extraction path resolves it, then the match result includes stable fields such as `doc_type_id`, `name`, `family`, `extractor_id`, `geometry_mode`, `validation_profile`, and `review_mode` rather than only an inferred document label.
3. Given a source file can match more than one document type, when matching is evaluated, then the classifier uses an explicit weighted or priority-based strategy instead of first-match-wins behavior.
4. Given document-type identification is uncertain, when the classifier cannot meet the configured threshold, then the system falls back to a safe default or unknown document type and records a low-confidence result for later review instead of silently forcing a risky match.
5. Given the catalog is used for PDFs, images, TXT/CSV points, and future DWG-aware families, when new document types are added, then the schema supports both AI-assisted and non-AI extraction paths without introducing new hardcoded C# branches for each family.
6. Given a document type contains multiple parcel groups or traverses, when its geometry contract is defined, then the catalog can declare grouping, boundary-break, closing, and line/polygon construction rules needed by downstream output generation.
7. Given a document type requires manual review behavior, when the review workspace is prepared, then the catalog can declare review-mode settings such as manual point entry support, group split/merge support, and approval gating requirements.
8. Given the catalog is loaded by the add-in, when the file is missing, partially invalid, or contains unsupported V2 fields, then the loader preserves current safe fallback behavior and reports partial-invalid warnings rather than failing hard.
9. Given this story is complete, then at minimum the current GeoLand computation sheet, generic computation sheet, and one structured points source family are expressed in the V2 schema, even if some legacy V1 compatibility remains during transition.
10. Given this story is complete, then focused tests cover V2 schema load, weighted match resolution, fallback behavior, legacy compatibility, and the persistence of resolved document-type routing metadata.

## Tasks / Subtasks

- [x] Define the Catalog V2 schema contract. (AC: 1-7, 9)
  - [x] Add top-level V2 fields such as `schema_version`, `default_doc_type_id`, and optional `defaults`.
  - [x] Add per-doc-type sections for:
    - [x] `match`
    - [x] `classifier`
    - [x] `extraction`
    - [x] `schema`
    - [x] `geometry`
    - [x] `validation`
    - [x] `review`
    - [x] `output`
  - [x] Preserve compatibility with current document-type metadata where practical.

- [x] Add a typed loader for Catalog V2. (AC: 1-5, 8-10)
  - [x] Introduce a dedicated catalog model and loader under a stable seam rather than parsing ad hoc JSON at call sites.
  - [x] Support safe coexistence of legacy fields while V2 adoption is in progress.
  - [x] Emit partial-invalid warnings when entries are incomplete, unsupported, or malformed.

- [x] Replace first-match behavior with scored classification. (AC: 3-4, 10)
  - [x] Add explicit weighted matching for filename tokens, regex hits, text tokens, and/or text regex hits.
  - [x] Respect per-type priority and score threshold.
  - [x] Return a structured classification result that includes confidence or match mode.

- [x] Persist richer resolved routing metadata. (AC: 2, 4, 10)
  - [x] Ensure resolved document metadata can be carried forward into extraction artifacts and case-level generated config.
  - [x] Include fields such as:
    - [x] `doc_type_id`
    - [x] `doc_type_name`
    - [x] `doc_type_family`
    - [x] `extractor_id`
    - [x] `geometry_mode`
    - [x] `validation_profile`
    - [x] `review_mode`
    - [x] `match_mode`
    - [x] `match_confidence`

- [x] Migrate initial document families into Catalog V2. (AC: 5-7, 9)
  - [x] Express `GEOLAND_COMPUTATION_TABLE_*` in the new schema.
  - [x] Express the generic/manual computation-sheet fallback in the new schema.
  - [x] Express at least one structured points import family for TXT/CSV.
  - [x] Carry forward grouping and boundary-break expectations for multi-parcel sources.

- [x] Add focused tests. (AC: 8-10)
  - [x] V2 load succeeds for valid catalog input.
  - [x] Partial-invalid entries warn but do not break safe fallback behavior.
  - [x] Weighted match prefers the intended computation-sheet type over a weaker generic sheet hit.
  - [x] Fallback unknown/default type is used when score threshold is not met.
  - [x] Resolved routing metadata is available for downstream extraction and review stages.

## Dev Notes

### Why This Story Exists

- Story 2.12 successfully wired draft extraction to an external document-type catalog, but the current approach is still too close to a filename-token lookup table.
- The workflow now needs to support many source families:
  - scanned computation PDFs
  - traverse reports
  - TIFF/JPG/PNG plan images
  - structured TXT/CSV coordinate sources
  - future DWG-aware and manual-only source types
- Without a versioned routing schema, every new document family risks becoming a new special case in adapters or Python wrappers.

### Architectural Intent

Catalog V2 should become the durable contract that answers:

1. What kind of document is this?
2. Which extraction path should run?
3. What geometry-building rules apply?
4. What validation profile should gate progress?
5. What review workspace behavior should be enabled?

This story defines that contract. A follow-on story should apply it through the active extraction pipeline.

### Recommended V2 Shape

Use a structure similar to:

```json
{
  "schema_version": "2.0",
  "default_doc_type_id": "UNKNOWN_GENERIC_SOURCE_V1",
  "defaults": {
    "ai_assisted": false,
    "review_mode": "point_review",
    "geometry_mode": "sequential_vertices",
    "validation_profile": "generic_minimum"
  },
  "doc_types": [
    {
      "doc_type_id": "GEOLAND_COMPUTATION_TABLE_V2",
      "name": "GeoLand Computation Table",
      "priority": 200,
      "family": "computation_sheet",
      "match": { },
      "classifier": { },
      "extraction": { },
      "schema": { },
      "geometry": { },
      "validation": { },
      "review": { },
      "output": { }
    }
  ]
}
```

### Minimum New Fields Worth Standardizing

- `family`
- `priority`
- `extractor_id`
- `parser_mode`
- `ai_assisted`
- `geometry_mode`
- `validation_profile`
- `review_mode`
- `supports_multi_parcel`
- `supports_boundary_breaks`
- `approval_requires_zero_blockers`

### Suggested Initial Extractor IDs

- `openai_table_pdf`
- `ocr_table_pdf`
- `text_regex_pdf`
- `structured_csv_points`
- `structured_txt_points`
- `dwg_geometry_probe`
- `manual_only_source`

### Suggested Initial Geometry Modes

- `sequential_vertices`
- `from_to_pairs`
- `parcel_rows_with_group_breaks`
- `point_list_only`
- `dwg_entities`
- `manual_constructed`

### Scope Boundaries

- This story defines and loads the V2 contract; it does not have to fully migrate every current adapter to use all V2 fields yet.
- This story does not redesign the review workspace UX by itself.
- This story does not implement final output-generation logic changes beyond exposing geometry/output metadata for later consumers.

### Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRuleDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/DocumentTypeCatalog*.cs` (new seam recommended)
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/*.cs`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcel_doc_types.json`

### References

- `_bmad-output/implementation-artifacts/2-12-execute-draft-extraction-and-review-artifact-generation.md`
- `_bmad-output/planning-artifacts/epics.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcel_doc_types.json`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added a typed `DocumentTypeCatalog` plus `DocumentTypeCatalogLoader` seam with V2 schema support, safe defaults, and legacy V1 compatibility mapping.
- Replaced adapter-local first-match filename routing with weighted classification that preserves priority, score thresholds, and low-confidence fallback behavior.
- Enriched generated case INI and `working/extraction_review_data.json` with durable routing metadata including family, extractor, geometry mode, validation profile, review mode, and match confidence.
- Added focused tests for V2 load, weighted matching, invalid-catalog fallback, legacy compatibility, and draft extraction artifact enrichment.
- Full shared test harness still ends on an unrelated existing `SettingsWorkspaceServiceTests.SettingsWorkspaceValidationRejectsInvalidEnterpriseAndSecretConfiguration` failure outside this story's scope.

### File List

- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/DocumentTypeCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-16 | 0.1 | Initial story for introducing Document Type Catalog V2 as a durable extraction-routing contract. | Codex |
| 2026-06-16 | 0.2 | Implemented Catalog V2 loading, weighted routing, legacy compatibility, richer extraction metadata, and focused regression tests. | Codex |
