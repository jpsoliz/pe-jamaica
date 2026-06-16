---
baseline_commit: handoff-2026-06-16
---

# Story 2.16: Apply Document Type Catalog V2 To Multi-Source Extraction Pipelines

Status: review

## Story

As a cadastral technical user,  
I want the active extraction pipeline to resolve document families through Catalog V2 and route each source through the right extraction and geometry rules,  
so that computation PDFs, scanned images, TXT/CSV point files, and future mixed-source packages can all produce trustworthy review data for point, line, and polygon generation.

## Acceptance Criteria

1. Given Document Type Catalog V2 is available, when draft extraction starts for a transaction, then the add-in resolves the active source family through the V2 loader and uses its declared `extractor_id`, `geometry_mode`, `validation_profile`, and `review_mode` rather than relying on adapter-local assumptions alone.
2. Given a transaction contains more than one candidate source file, when extraction routing is resolved, then the pipeline identifies the primary extraction source and any secondary context sources based on document-type family and configured source roles.
3. Given a supported computation-sheet document family is selected, when extraction runs, then the draft extraction path writes `working/extraction_review_data.json` with matched V2 metadata including `doc_type_id`, `extractor_id`, `geometry_mode`, `validation_profile`, and `review_mode`.
4. Given a structured TXT/CSV source is present and its document type declares a structured import extractor, when extraction runs, then structured point normalization is preferred over OCR or AI-based document parsing.
5. Given a document type declares AI-assisted extraction, when OpenAI is enabled and the required API key/source is available, then the configured AI extractor may run; when AI is disabled or not available, then the pipeline falls back to the declared non-AI extractor sequence and records which path was used.
6. Given a document family supports multiple parcels or traverse groups, when extraction review data is generated, then grouping and boundary-break fields required by the declared `geometry_mode` are persisted so downstream lines and polygons are not built as one flat continuous chain.
7. Given a document family cannot be matched with sufficient confidence, when extraction is attempted, then the workflow records an explicit low-confidence or unsupported-document result and blocks risky automated geometry assumptions until manual review or configuration update occurs.
8. Given extraction completes, when artifacts are inspected or reopened later, then the review artifact and any generated case config/manifest metadata preserve the resolved V2 routing contract used for that run.
9. Given this story is complete, then the active pipeline covers at minimum:
   - GeoLand computation-sheet style PDF/image cases
   - generic/manual computation-sheet fallback cases
   - one structured TXT/CSV points case
10. Given this story is complete, then focused tests cover multi-source routing, AI/non-AI extractor selection, structured-file preference, grouping preservation, low-confidence handling, and reopen/read-back behavior.

## Tasks / Subtasks

- [x] Resolve V2 routing in the active extraction path. (AC: 1-3, 8-10)
  - [x] Load the matched Catalog V2 entry during draft extraction start.
  - [x] Use V2 routing metadata as the adapter input contract instead of only local filename heuristics.
  - [x] Carry resolved routing metadata into generated config and extraction artifacts.

- [x] Add primary/secondary source routing. (AC: 2, 4, 9)
  - [x] Determine which source acts as the primary extraction input for each document family.
  - [x] Preserve secondary sources as context inputs where required, such as map references or plan images.
  - [x] Ensure TXT/CSV structured points sources outrank OCR/AI extractors when the matched doc type declares that behavior.

- [x] Route AI and fallback extraction paths through the catalog contract. (AC: 5, 10)
  - [x] Use catalog-declared `extractor_id` and fallback extractor order.
  - [x] Record whether AI was requested, available, and actually used.
  - [x] Persist the provider or fallback path used into the review artifact or step diagnostics.

- [x] Honor V2 geometry-mode requirements in review artifact output. (AC: 3, 6, 8-10)
  - [x] Populate parcel/traverse grouping fields required by the chosen geometry mode.
  - [x] Preserve boundary-break metadata for multi-parcel sources.
  - [x] Avoid flattening all extracted rows into one implied parcel chain.

- [x] Add low-confidence and unsupported-document handling. (AC: 7, 10)
  - [x] Record low-confidence classification as a visible extraction status.
  - [x] Prevent unsafe automated geometry assumptions when no adequate match exists.
  - [x] Surface a clear operator message that the document family needs configuration review or manual handling.

- [x] Persist routing diagnostics for reopen and support. (AC: 8, 10)
  - [x] Store the V2 routing decision used for the run alongside the review artifact and/or manifest-derived execution metadata.
  - [x] Ensure reopen or resume can show which doc type and extractor were used previously.

- [x] Add focused tests. (AC: 1-10)
  - [x] GeoLand computation-sheet case selects the intended V2 doc type and extractor.
  - [x] Heathfield-style “Computer Sheet” naming can be classified through the intended GeoLand family once configured.
  - [x] TXT/CSV structured points source bypasses OCR/AI preference.
  - [x] AI enabled vs disabled produces the correct route and persisted diagnostics.
  - [x] Multi-parcel grouping survives into `extraction_review_data.json`.
  - [x] Low-confidence classification blocks unsafe automation and reports actionable status.

## Dev Notes

### Relationship To Story 2.12A

- Story `2.12A` defines the durable Catalog V2 contract.
- This story applies that contract to the real extraction pipeline.
- Treat them as companion stories:
  - `2.12A` = contract and loader
  - `2.16` = runtime adoption and routing behavior

### Why This Story Exists

- The add-in now needs to support many document families through one repeatable extraction path.
- A catalog is only useful if the runtime actually obeys it.
- The workflow must decide, per transaction:
  - which file is the primary source
  - which extractor to run
  - whether AI can be used
  - how grouped rows should later become points, lines, and polygons

### Practical Routing Goals

For each loaded transaction, the active extraction path should be able to answer:

1. Which doc type matched?
2. Which source file is primary?
3. Which extractor runs first?
4. Which fallback extractor runs next if needed?
5. Which geometry mode should shape the review artifact?
6. Which validation profile will gate the next stage?
7. Which review workspace mode should be presented?

### Recommended Persisted Routing Fields

At minimum persist:

- `doc_type_id`
- `doc_type_name`
- `doc_type_family`
- `extractor_id`
- `fallback_extractor_id` or list
- `geometry_mode`
- `validation_profile`
- `review_mode`
- `match_mode`
- `match_confidence`
- `primary_source_role`
- `primary_source_file`
- `ai_requested`
- `ai_used`
- `provider_used`
- `fallback_reason`

### Multi-Source Handling Guidance

Expected near-term source families include:

- computation PDF/image plus plan/map reference
- structured TXT/CSV point source plus optional plan/map
- future DWG reference alongside non-DWG computation sources

The pipeline should not assume all files are equal. It should explicitly route:

- one primary extraction source
- one or more secondary context sources
- optional non-extraction context sources such as DWG references

### Scope Boundaries

- This story does not redesign the review-edit UI.
- This story does not generate final parcel output packages by itself.
- This story does not replace later output-stage geometry builders; it supplies them with cleaner routing metadata and grouped review data.

### Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/*.cs`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcel_doc_types.json`

### References

- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
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

- Applied Catalog V2 routing all the way through the active extraction adapter, including primary/secondary source selection, structured TXT/CSV preference, AI availability checks, fallback extractor selection, and persisted route diagnostics.
- Added `working/extraction_route.json` plus richer `extraction_review_data.json` metadata so reopen/support flows can see the exact routing contract used for a run.
- Added grouping enrichment for grouped geometry modes so review artifacts persist `parcel_group_id`, `traverse_id`, `sequence_in_group`, and group-review flags even when raw extraction rows omit them.
- Added focused adapter tests for structured-source preference, AI fallback diagnostics, grouping persistence, and unsupported-document blocking.
- Full solution build passed. Test harness passes all new 2.16 coverage and still ends on the pre-existing unrelated failure in `SettingsWorkspaceServiceTests.SettingsWorkspaceValidationRejectsInvalidEnterpriseAndSecretConfiguration`.

### File List

- `_bmad-output/implementation-artifacts/2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalog.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-16 | 0.1 | Initial story for applying Document Type Catalog V2 through the active multi-source extraction pipeline. | Codex |
| 2026-06-16 | 1.0 | Implemented runtime V2 extraction routing, structured-source preference, AI fallback diagnostics, grouped review enrichment, and focused adapter tests. | Codex |
