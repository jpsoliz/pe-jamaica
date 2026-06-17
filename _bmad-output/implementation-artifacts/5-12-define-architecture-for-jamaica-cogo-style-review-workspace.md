---
baseline_commit: handoff-2026-06-16
---

# Story 5.12: Define Architecture For Jamaica COGO-Style Review Workspace

Status: ready-for-dev

## Story

As the Sidwell Co architecture and delivery team,  
I want a clear architecture for a Jamaica-specific COGO-style review workspace,  
so that source verification, OCR/AI extraction review, parcel grouping, and Parcel Fabric handoff are implemented inside a supported, maintainable ArcGIS Pro boundary.

## Story Requirements

### User Story Statement

The built-in COGO Reader is not a safe product foundation for this workflow. The app needs its own architecture that keeps transaction-controlled source routing and extraction logic inside the add-in and custom processing stack, while handing approved results into supported ArcGIS Pro map review and Parcel Fabric workflows.

### Acceptance Criteria

1. Given the review workspace must replace any dependency on Esri's built-in COGO Reader behavior, when this architecture story is completed, then it defines a custom transaction-controlled review boundary with no product-critical dependency on unsupported internal UI automation.
2. Given the workflow starts from Innola transaction files, when the architecture is documented, then it defines the end-to-end contract from transaction metadata and source files through extraction, review workspace state, approval, spatial output creation, and Parcel Fabric/map-review handoff.
3. Given multiple document types and extraction modes are already in scope, when the architecture is documented, then it explicitly defines where document-type catalog resolution, OpenAI-assisted extraction, OCR, TXT/CSV normalization, and DWG context processing sit in the pipeline.
4. Given the review workspace must support multi-parcel documents, when the architecture is documented, then it defines the durable data contract for parcel grouping, traverse grouping, row sequencing, boundary-break metadata, and parcel-in-focus selection.
5. Given the product needs a left-source / center-results / right-parcel interpretation review surface, when the architecture is documented, then it defines the internal services, view models, contracts, and synchronization rules that should power those workspace regions.
6. Given the review workspace must preserve trust and recoverability, when the architecture is documented, then it explicitly defines which artifacts are authoritative at each stage, including `manifest.json`, `extraction_review_data.json`, saved review edits, approval artifacts, and handoff metadata.
7. Given Parcel Fabric is the intended spatial review target, when the architecture is documented, then it defines the handoff contract from approved review data into local Parcel Fabric outputs, including coordinate system handling, review-workspace spatial reference configuration, and map-review initialization metadata.
8. Given the user may still need to manually correct data before spatial review, when the architecture is documented, then it preserves manual row edits, unresolved states, save semantics, and approval semantics as the authoritative gate before map review and output generation.
9. Given the system must remain maintainable across ArcGIS Pro upgrades, when the architecture is completed, then it distinguishes:
   - supported ArcGIS Pro automation surfaces,
   - custom add-in responsibilities,
   - Python/adapter responsibilities,
   - and optional/non-authoritative assistive tooling.
10. Given this architecture is complete, then it produces clear implementation seams for later development stories and spikes, including what can be prototyped in the dev spike without prematurely committing production contracts.

## Tasks / Subtasks

- [ ] Define the supported product boundary. (AC: 1, 9)
  - [ ] State what remains inside public/supported ArcGIS Pro automation.
  - [ ] State what remains in custom add-in and Python adapter logic.
  - [ ] Explicitly exclude unsupported COGO Reader automation from the authoritative workflow.
  - [ ] Define whether any optional manual-assist launch behavior is allowed and how it is quarantined from the core workflow.

- [ ] Define the end-to-end review workspace flow. (AC: 2-3, 6, 8-10)
  - [ ] Document the flow from Innola transaction load to source copy to extraction to review approval to map-review handoff.
  - [ ] Identify the state transitions and artifacts required at each stage.
  - [ ] Define where resume/suspend state persists.
  - [ ] Define which artifact is authoritative for each state transition.

- [ ] Define the extraction and review data contracts. (AC: 3-6, 8)
  - [ ] Specify the document-type catalog interaction.
  - [ ] Specify the extraction-review data shape for parcel-grouped results.
  - [ ] Specify how manual edits, unresolved flags, and approval hashes are preserved.
  - [ ] Specify which fields belong to source metadata, extracted review rows, parcel grouping, and handoff metadata.

- [ ] Define the review-workspace component architecture. (AC: 5-6, 8, 10)
  - [ ] Identify viewer, table, parcel-preview, and status components.
  - [ ] Define service/view-model boundaries for each region.
  - [ ] Define how source selection and parcel selection synchronize.
  - [ ] Define how workspace actions such as reload, save review, approve review, and add point are routed through the session/state model.

- [ ] Define the Parcel Fabric handoff contract. (AC: 7-10)
  - [ ] Specify how approved review data becomes local Parcel Fabric review output.
  - [ ] Specify spatial reference / coordinate system settings.
  - [ ] Specify what metadata must be carried into map review and outputs.
  - [ ] Specify what the review workspace must not attempt to do once the handoff occurs.

- [ ] Produce implementation guidance. (AC: 9-10)
  - [ ] Identify the next dev spike and implementation stories.
  - [ ] Identify risks, prerequisites, and fallback paths.
  - [ ] Identify which contracts may remain provisional during the spike and which must be stable before dev begins.

## Dev Notes

### Why This Story Exists

- Story 5.10 established that built-in COGO Reader should not be the core automation surface.
- Stories 2.12A and 2.16 are already moving extraction toward a durable document-type-driven contract.
- The product now needs an architecture note that ties those threads together before deeper UI and implementation work continue.

### Architectural Direction

Recommended architecture:

1. `Innola transaction sources`
   - transaction metadata
   - copied source files
   - source document roles/types

2. `Extraction pipeline`
   - document-type catalog resolution
   - OCR/OpenAI/custom parser selection
   - TXT/CSV normalization
   - DWG context enrichment

3. `Review workspace`
   - source viewer
   - extracted-row editor
   - parcel grouping and interpretation preview
   - review approval

4. `Spatial output and map review`
   - approved review data to `.gdb` / Parcel Fabric
   - ArcGIS Pro map-review editing
   - final completion/sync after map review

### Authoritative Artifacts And State Ownership

The architecture note should explicitly define ownership like this:

- `manifest.json`
  - authoritative source-routing and case-context contract
- `extraction_review_data.json`
  - authoritative editable review workspace dataset before approval
- `approved_review.json`
  - authoritative approved handoff dataset for validation/output
- output summary and map-review metadata
  - authoritative transition contract into spatial review

The architecture should avoid mixing "current UI state" with "authoritative saved review state". The saved artifact must win.

### Key Architecture Constraint

The review workspace should be **transaction-controlled and document-aware**, while ArcGIS Pro remains the **spatial editing host**. Mixing those responsibilities too early will make the system brittle.

### Required Service Seams To Define

The architecture should name the seams clearly enough that the spike and later implementation can align to them. At minimum:

- `SourceDocumentWorkspaceService`
  - resolves the current document and rendering/fallback strategy
- `ExtractionReviewWorkspaceService`
  - loads and saves editable review workspace state
- `ParcelGroupingService`
  - resolves parcel/group membership, boundary breaks, and current parcel focus
- `ReviewApprovalService`
  - validates whether the current review state can be approved
- `ReviewWorkspaceProjectionService`
  - prepares right-panel parcel/line interpretation and bottom preview models
- `SpatialReviewHandoffService`
  - converts approved review state into Parcel Fabric / map-review-ready outputs

The final names can vary, but the architecture should define these responsibilities explicitly.

### Required Runtime Contracts To Define

The architecture note should include, at minimum:

- source-document descriptor contract
- extracted review row contract
- parcel-group contract
- review-workspace session state contract
- approval contract
- spatial handoff contract

These should be described in repo language, not left as abstract ideas.

### Coordinate System And Spatial Reference Guidance

The architecture should standardize that the review workspace does not infer spatial reference ad hoc at review time. It should define:

- where the configured review/output spatial reference is read
- how that setting is carried into local Parcel Fabric output creation
- what metadata is written so later map-review or enterprise steps know what spatial reference was used

### Explicit Non-Goals

The architecture story should also state what this workspace is not:

- not a replacement for ArcGIS Pro map editing
- not a live enterprise editing surface
- not a thin wrapper around Esri's COGO Reader
- not the final sync engine
- not a place where undocumented UI automation becomes a workflow dependency

### Likely Files / Areas To Be Affected Later

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/*`
- `src/ProcessingTools/adapters/*`
- settings and configuration surfaces around document-type catalog, AI enablement, and spatial reference

### Suggested Output

This story should produce a concise architecture note covering:

- component boundaries
- data contracts
- service seams
- state model
- Parcel Fabric handoff
- upgrade/supportability constraints
- spike-safe provisional decisions vs production-stable decisions

## References

- `_bmad-output/implementation-artifacts/2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md`
- `_bmad-output/implementation-artifacts/2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/5-10-evaluate-supported-arcgis-pro-automation-boundary-for-cogo-reader-assist-vs-custom-transaction-controlled-extraction-flow.md`
- `_bmad-output/planning-artifacts/research/cogo-reader-automation-boundary-evaluation-2026-06-16.md`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Parcel-Fabric.html`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-16 | 0.1 | Initial architecture story for the Jamaica-specific COGO-style review workspace. | Codex |
| 2026-06-16 | 0.2 | Refined the architecture story with stronger artifact ownership, service seams, runtime contracts, Parcel Fabric handoff constraints, and spike-versus-production guidance. | Codex |
