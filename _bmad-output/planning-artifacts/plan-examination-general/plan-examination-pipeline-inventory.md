# Plan Examination Pipeline Inventory

Last updated: 2026-09-12
Owner: Winston / Architecture
Review partners: Mary / Business Analysis, Sally / UX, Amelia / Implementation
Status: Draft architecture inventory for PE/PXA unification

## Purpose

Inventory the current scripts, services, and procedures used by PE/PXA Plan Examination so the refactor can decide what to reuse, adapt, retire, or recreate.

This is required before implementation stories because the current behavior is distributed across:

- C# workflow orchestration.
- C# source/profile detection.
- Python PDF extraction scripts.
- Python validation/output scripts.
- Legacy CreateParcel fallback.
- C# review normalization and UI projection.
- ArcGIS map/output services.
- Enterprise `working_review` publish and Innola finalize/writeback.

All source documents for unified Plan Examination are expected to be PDFs for the core review flow:

- Mandatory `st_surveyplan`.
- Mandatory `st_surveysheet`.

Optional non-PDF support remains:

- `st_autocad_file`.
- `st_survey_points`.

The target architecture should extract as much evidence as possible from PDF first, then analyze/validate the extracted data after extraction.

## Architectural Position

Create a formal pipeline inventory and decision matrix before creating implementation stories.

Reason:

- PE and PXA currently share the business goal but not a clean architecture model.
- PXA contains much of the desired review behavior, but it was originally single-parcel.
- PE contains multi-parcel pressure, scanned computation-sheet pressure, and case-folder restart pain.
- The refactor should not start by rewriting scripts. It should first classify each existing piece by responsibility and decide whether it remains part of the unified Plan Examination pipeline.

## Target Pipeline Shape

The unified pipeline should be:

```text
Source intake
  -> source role/profile resolution
  -> PDF evidence extraction
  -> normalized review artifact
  -> examiner review/correction
  -> approved review artifact
  -> validation/rule analysis
  -> local/map-ready output generation
  -> final review
  -> Enterprise working_review publish + Innola finalize
```

Key principle:

Extraction and analysis must be separate. Extraction produces evidence and review candidates. Analysis validates whether that evidence is good enough to build parcel geometry and close the workflow.

## Current Script And Procedure Inventory

| Area | File / Procedure | Current Role | Outputs | Recommendation |
| --- | --- | --- | --- | --- |
| Workflow orchestration | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs` | Owns stage state, extraction start, validation, output generation, final review, and finalize gates. | Workflow state, available artifacts, status messages. | Reuse, but introduce a smaller Plan Examination orchestration boundary for macro actions. |
| Script execution | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowScriptExecutor.cs` | Runs configured script-plan steps through registered adapters. | `extraction_review_data.json` check and artifact list. | Reuse. Keep as script runner boundary. |
| Draft extraction adapter | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs` | Main C# extraction route orchestrator. Chooses structured PDF, scanned/AI route, or legacy script fallback. | `working/extraction_review_data.json`, `working/extraction_route.json`, stage summaries. | Adapt. This is currently too much of the extraction brain; split route planning from execution over time. |
| Placeholder extraction adapter | `src/ProcessingTools/adapters/extraction_adapter.py` | Placeholder only. | None. | Recreate or retire. It should not remain the named abstraction if real extraction lives elsewhere. |
| Embedded-text computation extraction | `src/ProcessingTools/adapters/pdf_text_structured_extraction.py` | Deterministic parser for PDFs with usable embedded text. Parses computation-style segment/point evidence and metadata. | Normalized review artifact / fallback envelope. | Reuse and strengthen. This is the best first route for machine-readable PDFs. |
| Scanned survey-plan OCR/vision extraction | `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py` | Renders scanned/image PDFs, calls vision provider, normalizes survey plan metadata, points, segments, memorandum, parties, volume/folio. | Normalized review artifact and survey plan extraction summary. | Adapt. Generalize from single-parcel PXA to multi-parcel Plan Examination. |
| Known scanned fixture route | `src/ProcessingTools/adapters/known_cases/100001027_document5_sha256_fb1df8d1a68294e322c7c48bc99c50fe5c09e47441717d17ea0ecdd6fca30912.json` | Known case fixture for scanned computation sheet recovery. | Fixture-backed extraction evidence. | Keep as regression fixture, not production logic. |
| Validation adapter | `src/ProcessingTools/adapters/validation_adapter.py` | Analyzes approved review data: current approval hash, rows, unresolved values, closure, construction readiness, orientation, bearing consistency, parish boundary, plan-vs-sheet consistency, printed text size. | `working/validation_summary.json`. | Reuse. This is the post-extraction analysis layer and should remain separate from extraction. |
| Output adapter | `src/ProcessingTools/adapters/output_adapter.py` | Converts approved review data into local/map-ready geometry, GDB/GeoJSON/report artifacts, optional orientation normalization, COGO attributes, parcel fabric modes. | `output/output_summary.json`, output GDB/geometry/report artifacts. | Reuse. Ensure it consumes only approved normalized review data. |
| Preflight adapter | `src/ProcessingTools/adapters/preflight_adapter.py` | Placeholder/early-stage adapter path. | Preflight summaries where configured. | Review. Decide whether C# preflight remains owner or Python adapter becomes real. |
| Source profile detector | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs` | Detects current source combination: PXA survey plan, PE scenarios, PLA, incomplete/unsupported. | `detected_profile` in manifest/session. | Adapt. Replace PE/PXA final labels with unified Plan Examination profiles while retaining routing behavior. |
| Source roles | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs` | Defines normalized roles such as computation sheet, plan map reference, survey plan PDF, DWG, coordinate source. | Manifest source roles. | Reuse. Ensure `st_surveyplan` and `st_surveysheet` map cleanly into required roles. |
| Document type catalog | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs` and `DocumentTypeCatalog.cs` | Loads document-type/extraction route catalog. | Route config used by extraction adapter. | Reuse and centralize. Move route decisions here where practical. |
| Review artifact persistence | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs` | Loads/saves review rows, metadata, segments, memorandum/parties and approved review snapshot. | `working/extraction_review_data.json`, `working/approved_review.json`. | Reuse and adapt into canonical Plan Examination review contract. |
| Review model | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs` | C# model for extraction/review artifact. | In-memory review model. | Adapt. Add/normalize parcel groups and PDF evidence source metadata explicitly. |
| Boundary solver | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs` | Builds/rebuilds derived points from reviewed boundary segments and applies geometry repair policies. | Updated review rows/solver diagnostics. | Reuse with caution. Separate PXA-only policies from shared Plan Examination behavior. |
| Parcel-scoped point service | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs` | Adds/edits/removes parcel-scoped points. | Updated review rows. | Reuse. Required for multi-parcel unified UX. |
| Manual boundary service | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ManualBoundarySegmentService.cs` | Adds/edits boundary segments. | Updated review segments. | Reuse. Required for multi-parcel unified UX. |
| Parcel review validation | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs` | Validates editable review rows before approval. | UI blockers before `approved_review.json`. | Reuse and extend for segments/bearings/distances if gaps remain. |
| Points Validation UI | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs` and `.xaml` | Detailed review workspace for documents, metadata, points, boundary segments, memorandum, parcel selector, and validation complete. | User edits, approved review handoff. | Reuse as main unified Plan Examination review workspace. Rename visible PXA language. |
| Output execution service | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs` | C# caller for `output_adapter.py`. | Output execution result and `output_summary.json`. | Reuse. |
| Output map integration | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs` | Loads local output geometry into ArcGIS Pro map and applies styling. | Map layers. | Reuse. |
| Enterprise working publish | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs` | Copies generated local review geometry to Enterprise `working_review` layers when configured. | `output/enterprise_working_publish.json`; Enterprise feature rows. | Reuse. Keep publish timing configurable but default to reviewed/finalize path for working_review. |
| Enterprise working restore | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs` | Restores case state from Enterprise working layers when local artifacts are missing/stale. | Rehydrated output state. | Reuse. Important for support/recovery. |
| Enterprise disposition | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs` | Records final review disposition against Enterprise working rows. | Disposition evidence. | Reuse. |
| Innola SpatialUnit write | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs` | Creates/updates Innola `SpatialUnitExt` records from final output/disposition evidence. | `working/spatial_unit_api_request.json`, payload/response/failure artifacts. | Reuse. Keep separate from PDF extraction and review UX. |
| Lifecycle finalize | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs` | Uploads package, saves SpatialUnit when needed, completes Innola task. | Updated manifest/lifecycle audit and completed transaction. | Reuse. Do not mix with extraction. |

## Current PDF Extraction Routes

### Route A: Embedded Text PDF

Best for:

- Machine-readable computation sheet PDFs.
- PDFs where points, bearings, distances, and tables can be parsed from text.

Current owner:

- `pdf_text_structured_extraction.py`
- Called by `CreateParcelDraftExtractionAdapter.cs`

Decision:

- Reuse.
- Improve parser coverage as needed.
- Keep deterministic and test-heavy.
- Emit clear fallback reason when text exists but does not parse.

### Route B: Scanned/Image PDF OCR + Vision

Best for:

- Scanned survey plan PDFs.
- Image-only computation sheets.
- Documents where metadata, memorandum, parties, points, bearings, and distances are visible but not embedded text.

Current owner:

- `survey_plan_ocr_vision_extraction.py`

Decision:

- Adapt into a generalized `plan_examination_pdf_vision_extraction` route.
- Do not keep it single-parcel only.
- It must support multiple parcels, parcel grouping, source page/zone evidence, and structured counts.

### Route C: Legacy CreateParcel Script Fallback

Best for:

- Existing supported legacy flows where the script still produces valid review artifacts.

Current owner:

- Configured `CreateParcelScriptPath`, invoked by `CreateParcelDraftExtractionAdapter.cs`.

Decision:

- Keep temporarily as compatibility fallback.
- Do not make it the target architecture.
- Wrap with stronger route artifact diagnostics.

### Route D: Manual Mode

Best for:

- No extraction.
- Low confidence extraction.
- Unsupported layout.
- Presentation rescue/support workflows.

Current owner:

- Points Validation Tool and manual point/boundary services.

Decision:

- Keep.
- Treat as a first-class recovery route.

## Analysis After Extraction

Post-extraction analysis should be explicit and separate from OCR/parsing.

Required analysis layers:

- Schema/contract validation.
- Point row presence and completeness.
- Boundary segment presence and completeness.
- Parcel grouping completeness.
- Bearing/distance parseability.
- Closure/misclose.
- Point uniqueness per parcel.
- Segment connectivity and ring readiness.
- Orientation.
- Bearing versus coordinate-derived azimuth consistency.
- Plan PDF versus computation sheet consistency.
- Document metadata consistency.
- Georeference/parish/JAD2001 checks.
- Source confidence and manual-review indicators.

Current owner:

- `validation_adapter.py`
- C# review validation services.
- Rule catalog/settings.

Decision:

- Reuse validation adapter as the main analysis engine after review approval.
- Add a lighter extraction-quality analysis immediately after extraction for counts, route quality, and next-action guidance.

## Reuse / Adapt / Recreate Summary

Reuse:

- `WorkflowSession.cs` stage gate concepts.
- `WorkflowScriptExecutor.cs`.
- `pdf_text_structured_extraction.py`.
- `validation_adapter.py`.
- `output_adapter.py`.
- Review persistence and approved-review snapshot.
- Parcel-scoped manual point/segment services.
- Enterprise working publish/restore/disposition.
- Innola lifecycle and SpatialUnit services.

Adapt:

- `CreateParcelDraftExtractionAdapter.cs` into cleaner route-planning plus route-execution responsibilities.
- `survey_plan_ocr_vision_extraction.py` from PXA single-parcel to Plan Examination multi-parcel PDF vision extraction.
- `SourceInputProfileDetector.cs` from PE/PXA scenario names to unified Plan Examination source profiles.
- `ExtractionReviewDocument.cs` to make parcel groups and source evidence first-class.
- Points Validation visible labels from PXA-specific to Plan Examination.

Recreate or retire:

- `extraction_adapter.py` placeholder.
- Any production dependency on known-case fixture JSON.
- Hidden PXA-only assumptions in survey-plan OCR or boundary solver behavior.

## Proposed Refactor Work Packages

### Work Package 1: Pipeline Inventory And Decision Lock

Output:

- This inventory reviewed by Mary, Sally, Winston, and Amelia.
- Final decision per script/service: reuse, adapt, retire, recreate.

### Work Package 2: Unified PDF Extraction Contract

Output:

- One normalized Plan Examination extraction contract.
- Explicit source evidence for both `st_surveyplan` and `st_surveysheet`.
- Parcel groups, points, segments, metadata, memorandum, parties, volume/folio, confidence, and source trace.

### Work Package 3: Route Planner

Output:

- Central route planner for PDF extraction:
  - embedded text first
  - scanned/vision second
  - legacy fallback where safe
  - manual mode when automation is weak

### Work Package 4: Multi-Parcel Vision Extraction

Output:

- Generalized scanned PDF route that can extract multiple parcel tables/groups from survey plans and computation sheets.
- Route evidence includes counts per parcel and failure reason.

### Work Package 5: Post-Extraction Analysis

Output:

- Extraction quality summary immediately after extraction.
- Validation summary after examiner approval.
- Clear separation between "we extracted evidence" and "the evidence is good enough to build geometry."

### Work Package 6: Review UX Binding

Output:

- Existing Points Validation workspace consumes the unified contract.
- General tabs are transaction/document-wide.
- Points and Boundary Segments are active-parcel scoped.

## Open Architecture Questions

- Should `survey_plan_ocr_vision_extraction.py` be renamed immediately, or wrapped under a new route name while keeping the file for compatibility?
- Should `pdf_text_structured_extraction.py` handle both `st_surveysheet` and embedded text inside `st_surveyplan`, or should the route planner call it per source and merge results?
- What is the canonical merge rule when both survey plan PDF and survey sheet PDF provide the same point/bearing/distance with different values?
- Should extraction-quality analysis write a new artifact, for example `working/extraction_quality_summary.json`, or be embedded in `working/extraction_route.json`?
- Which real case folders can become stable regression fixtures without sensitive data?

## Recommended Next Step

Before creating implementation stories, review this inventory and mark each row as:

- `Reuse as-is`
- `Reuse with small changes`
- `Adapt into unified Plan Examination`
- `Retire after compatibility period`
- `Recreate`

Then create the first architecture decision document for the unified PDF extraction contract and route planner.

## Locked Decision Matrix

Story: `11-1-inventory-and-decision-lock-for-plan-examination-unification`

Decision date: 2026-09-12

Decision principle:

Do not rewrite working PE/PXA behavior before the unified extraction contract and route planner exist. Preserve the current review-before-output workflow and move behavior behind clearer Plan Examination boundaries in later stories.

| Area | File / Procedure | Locked Decision | Decision Notes | Unlocks |
| --- | --- | --- | --- | --- |
| Workflow orchestration | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs` | `Reuse with small changes` | Keep stage gates, artifact tracking, validation/output/finalize order, and recovery behavior. Later stories may add macro action grouping for the simplified Plan Examination flow. | `11-5`, `11-7` |
| Script execution | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowScriptExecutor.cs` | `Reuse as-is` | Keep this as the script runner boundary. Do not move extraction parsing into the executor. | `11-3` |
| Draft extraction adapter | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs` | `Adapt into unified Plan Examination` | Keep current behavior during compatibility. Split route planning from route execution in a later story so route selection is explainable and testable. | `11-3` |
| Placeholder extraction adapter | `src/ProcessingTools/adapters/extraction_adapter.py` | `Retire after compatibility period` | Current file only raises `NotImplementedError`; it must not be treated as the production extraction route. A later story may replace it with a thin compatibility shim only if a script-plan contract still references `extraction_adapter`. | `11-3` |
| Embedded-text computation extraction | `src/ProcessingTools/adapters/pdf_text_structured_extraction.py` | `Reuse with small changes` | Keep as deterministic first route for PDFs with usable embedded text. It should own embedded-text parsing for `st_surveysheet` and may parse embedded-text `st_surveyplan` when the route planner supplies that source. It must emit clear fallback reasons when text exists but parsing is insufficient. | `11-2`, `11-3` |
| Scanned survey-plan OCR/vision extraction | `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py` | `Adapt into unified Plan Examination` | Keep current PXA-compatible behavior. Introduce a generalized Plan Examination route name in routing/config first; keep the existing file as the compatibility implementation until multi-parcel behavior is proven. | `11-2`, `11-3`, `11-4` |
| Known scanned fixture route | `src/ProcessingTools/adapters/known_cases/100001027_document5_sha256_fb1df8d1a68294e322c7c48bc99c50fe5c09e47441717d17ea0ecdd6fca30912.json` | `Retire after compatibility period` | Keep only as regression evidence. It must not remain a production extraction shortcut once the generalized scanned PDF route is in place. | `11-4`, `11-6` |
| Validation adapter | `src/ProcessingTools/adapters/validation_adapter.py` | `Reuse as-is` | Preserve as the post-review analysis engine. Extraction must produce evidence; validation determines whether approved evidence can build geometry. | `11-6`, `11-7` |
| Output adapter | `src/ProcessingTools/adapters/output_adapter.py` | `Reuse as-is` | Preserve as the approved-review-to-local-output geometry generator. It must consume approved normalized review data only. | `11-7` |
| Preflight adapter | `src/ProcessingTools/adapters/preflight_adapter.py` | `Retire after compatibility period` | Keep only where existing script plans still reference it. Do not expand it until ownership between C# rule checks and Python checks is re-decided. | Future support cleanup |
| Source profile detector | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs` | `Adapt into unified Plan Examination` | Replace visible PE/PXA scenario labels with unified Plan Examination source profiles while preserving the existing behavior behind compatibility aliases. | `11-3`, `11-5` |
| Source roles | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs` | `Reuse with small changes` | Keep normalized source roles. Ensure mandatory `st_surveyplan` and `st_surveysheet` map cleanly into source roles, with `st_autocad_file` and `st_survey_points` optional. | `11-2`, `11-3` |
| Document type catalog | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs` and `DocumentTypeCatalog.cs` | `Reuse with small changes` | Keep catalog loading and move more route declaration into catalog/config where practical. Route planner remains responsible for runtime decisions. | `11-3` |
| Review artifact persistence | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs` | `Adapt into unified Plan Examination` | Reuse persistence mechanics, but align load/save with the canonical unified review contract from `11-2`. | `11-2`, `11-5` |
| Review model | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs` | `Adapt into unified Plan Examination` | Add/normalize parcel groups, source trace, document role evidence, and review status fields as first-class contract members. | `11-2`, `11-5` |
| Boundary solver | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/SurveyPlanBoundarySolver.cs` | `Reuse with small changes` | Preserve deterministic rebuild behavior. Separate any PXA-only assumptions from shared Plan Examination logic when multi-parcel scanned extraction is implemented. | `11-4`, `11-5` |
| Parcel-scoped point service | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs` | `Reuse as-is` | Keep active-parcel point add/edit/remove behavior. | `11-5` |
| Manual boundary service | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ManualBoundarySegmentService.cs` | `Reuse as-is` | Keep active-parcel boundary add/edit/exclude behavior. | `11-5` |
| Parcel review validation | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs` | `Reuse with small changes` | Keep as pre-approval UI validation, extend only where unified segments/bearings/distances need missing blockers. | `11-5`, `11-6` |
| Points Validation UI | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs` and `.xaml` | `Adapt into unified Plan Examination` | Preserve the workspace and dense-grid interaction model. Rename visible PXA language and bind generic tabs versus active-parcel geometry tabs to the unified contract. | `11-5` |
| Output execution service | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs` | `Reuse as-is` | Keep as C# caller for `output_adapter.py`. | `11-7` |
| Output map integration | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs` | `Reuse as-is` | Keep local output loading and styling integration. | `11-7` |
| Enterprise working publish | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs` | `Reuse as-is` | Preserve configurable publish timing. Default remains publish on Finalize unless configuration explicitly says otherwise. | `11-7` |
| Enterprise working restore | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs` | `Reuse as-is` | Keep for recovery when local artifacts are missing or stale. | `11-7` |
| Enterprise disposition | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs` | `Reuse as-is` | Keep final-review disposition evidence separated from extraction. | `11-7` |
| Innola SpatialUnit write | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs` | `Reuse as-is` | Keep Innola SpatialUnit writeback separate from PDF extraction and review UX. | `11-7` |
| Lifecycle finalize | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs` | `Reuse as-is` | Preserve package upload, SpatialUnit save, transition completion, and lifecycle audit behavior. | `11-7` |

## Locked Route Decisions

### Embedded Text PDF Route

Decision: `Reuse with small changes`.

`pdf_text_structured_extraction.py` remains the deterministic first route when a PDF has usable text. The route planner in `11-3` should call it against `st_surveysheet` and may call it against embedded-text `st_surveyplan` when useful text is detected. The unified contract in `11-2` must define how extracted fields identify their source role, source file, page, zone, confidence, and parcel group.

### Scanned PDF OCR/Vision Route

Decision: `Adapt into unified Plan Examination`.

`survey_plan_ocr_vision_extraction.py` stays available for current PXA compatibility. The target route name should be generalized in routing/config, for example `plan_examination_pdf_vision_extraction`, while the existing script can remain the implementation wrapper during transition. Multi-parcel extraction belongs in `11-4`, after the unified contract and route planner exist.

### Placeholder Route

Decision: `Retire after compatibility period`.

`extraction_adapter.py` is not production behavior. Do not build `11-2` or `11-3` around this placeholder. If old script-plan entries still require the adapter id, use a thin shim or route-alias decision in `11-3`.

### Route Planning Boundary

Decision: `Adapt into unified Plan Examination`.

`CreateParcelDraftExtractionAdapter.cs` remains the compatibility orchestrator for now. In `11-3`, split its responsibilities conceptually into:

- Route planner: reads source roles, document type catalog, PDF text probe results, config flags, and fallback policy; writes `working/extraction_route.json`.
- Route executor: invokes the selected extraction script and records runtime diagnostics.
- Artifact normalizer: ensures route outputs match the `11-2` unified review contract.

No route-planning refactor is required in `11-1`.

## Locked UX And Process Decisions

- Keep two UX surfaces:
  - ArcGIS Pro dockpane for process flow.
  - Points Validation workspace for detailed source, point, boundary, and parcel review.
- Generic transaction/document data remains generic:
  - Supporting Documents.
  - General Information.
  - Owners / Occupiers / Parties.
  - Memorandum.
  - Stage Findings / Diagnostics.
  - Final Review.
- Active-parcel filtering applies only to:
  - Points.
  - Boundary Segments.
  - Parcel Preview / Map Evidence.
  - Active parcel blockers and closure/geometry summaries.
- Keep extraction and analysis separate:
  - Extraction creates review candidates and evidence.
  - Post-extraction analysis summarizes quality and blockers.
  - Validate Points and Lines lets the examiner correct and approve data.
  - Create Spatial Units validates approved review data and creates local/map output.
  - Finalize publishes/writes back only after final review.

## Fixture And Evidence Decision Table

| Transaction / Case | Current Use | Locked Decision | Notes |
| --- | --- | --- | --- |
| `100001027` | PE scanned computation/survey-sheet failure and known fixture recovery evidence. | Sanitized regression fixture candidate | Use to prove multi-parcel scanned PDF extraction and zero-result diagnostics. Remove production dependency on known-case hash shortcuts. |
| `100000896` | PXA/Plan Examination review workspace example with dense metadata and geometry rows. | Acceptance fixture candidate | Use for UX binding and parcel-scoped grids if source artifacts are complete. |
| `100000622` | PXA missing finalize / spatial-unit evidence from prior investigation. | Investigation-only evidence until sanitized | Useful for downstream finalize preservation, but not first extraction contract proof. |
| `100001005` | Existing case/restart visibility issue and extraction quality threshold example. | Support/recovery acceptance fixture candidate | Use for restart/reprocess UX and stale artifact behavior, not as primary OCR quality fixture. |
| `100000623` | PE/Plan route finalize and spatial-unit object evidence. | Investigation-only evidence until sanitized | Useful for downstream `11-7` finalization checks. |
| `100000626` | Linked transaction source context for RT/PE spatial-unit loading cases. | Investigation-only evidence until sanitized | Useful for linked-transaction spatial context, outside the immediate `11-2`/`11-3` PDF contract. |

## Story 11-2 And 11-3 Prerequisites

`11-2: Unified PDF Extraction Contract` can start after this story because the reuse/adapt decisions are now locked. It should define the canonical artifact before implementation changes are made.

Required contract content for `11-2`:

- Transaction number and workflow/profile identifiers.
- Source role inventory for `st_surveyplan`, `st_surveysheet`, optional `st_autocad_file`, and optional `st_survey_points`.
- Source trace per extracted value: source role, source file, page, zone/bbox where available, confidence, raw text, normalized value, and review status.
- Transaction-wide metadata, memorandum, parties, owners/neighbors, volume/folio, and document findings.
- Parcel groups with stable parcel ids, display names, lot names where present, and generated labels such as `parcel-001` when no name exists.
- Point rows and boundary segment rows linked to parcel group.
- Extraction quality counts for points, segments, parcels, metadata fields, warnings, blockers, and manual-review recommendation.
- Compatibility mapping from legacy PE/PXA review artifacts into the unified contract.

`11-3: Route Planner For PDF Extraction` can start after `11-2` contract draft exists. It should centralize route decisions without changing downstream output/finalize behavior.

Required route planner behavior for `11-3`:

- Attempt embedded-text extraction when the PDF text probe finds useful text.
- Attempt scanned OCR/vision when embedded text is absent or insufficient.
- Keep legacy fallback temporarily when configured and safe.
- Enter Manual Mode when automation is unsafe, unsupported, or below threshold.
- Write `working/extraction_route.json` with selected route, attempted routes, source files, fallback reason, provider, confidence, counts, and operator-facing guidance.
