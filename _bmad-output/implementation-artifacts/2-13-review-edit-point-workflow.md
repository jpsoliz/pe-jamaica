---
baseline_commit: handoff-2026-06-12
---

# Story 2.13: Review/Edit Point Workflow

Status: review

## Story

As a cadastral technical staff user,
I want to review, correct, add, and mark unresolved extracted point data from the current transaction,
so that the parcel workflow uses approved human-reviewed point data before any parcel geometry or `.gdb` output is created.

## Acceptance Criteria

1. Given `working/extraction_review_data.json` exists for the active transaction, when the user opens Extraction Review, then the Parcel Workflow pane loads the review dataset into a visible editable review surface instead of only opening the raw JSON file externally.
2. Given extracted point rows are present, when the review surface is displayed, then the user can inspect core fields such as point identifier, coordinates or extracted values, source evidence, extraction status, and unresolved/missing indicators.
3. Given an extracted point row contains incorrect or incomplete values, when the user edits the row, then the change is recorded as a human override without losing the original extracted value.
4. Given extraction missed one or more points, when the user adds a new point manually, then the new row is stored in the working review dataset and is clearly marked as manually entered.
5. Given a row cannot yet be corrected confidently, when the user marks it unresolved, then the workflow records that unresolved state and prevents downstream approval/build until configured blockers are cleared.
6. Given the user makes changes in review, when the review dataset is saved, then the updated working review artifact remains in the Case Folder and survives ArcGIS Pro restart/reopen.
7. Given the review dataset contains unresolved rows or missing required values, when the user attempts approval in this story’s review stage, then approval remains blocked with clear status guidance.
8. Given reviewed rows are complete and valid for this stage, when the user approves the review dataset, then the add-in writes `working/approved_review.json` tied to the current review version/hash and moves the workflow into an approved review state.
9. Given the story is complete, then parcel geometry generation, `.gdb` creation, validation execution, and output packaging are still out of scope for this story.
10. Given the story is complete, then focused tests cover loading review data, editing rows, adding rows, unresolved-state blocking, approval hash persistence, reopen/resume behavior, and not creating final output artifacts.

## Tasks / Subtasks

- [x] Add review/edit state support. (AC: 1, 6, 8, 9)
  - [x] Extend workflow state support to include review-active/approved states as needed.
  - [x] Keep extraction result state and approved review state distinct.
  - [x] Preserve restart/reopen behavior from Case Folder artifacts.
- [x] Add review data contract and persistence helpers. (AC: 1-8, 10)
  - [x] Define C# models for `extraction_review_data.json` and `approved_review.json`.
  - [x] Support original extracted values plus edited/manual override values.
  - [x] Add version/hash generation for approval binding.
  - [x] Keep serialized fields lowercase snake_case.
- [x] Load review artifact into the Parcel Workflow pane. (AC: 1, 2, 6)
  - [x] Replace the current external-file-only review behavior with an in-pane review surface for the active case.
  - [x] Read from `working/extraction_review_data.json`.
  - [x] Show user-friendly row/status summaries derived from the review artifact.
- [x] Implement edit and manual-entry actions. (AC: 2-6)
  - [x] Support editing point fields in the review grid/form.
  - [x] Support adding a new manual point row.
  - [x] Mark manual rows distinctly from extracted rows.
  - [x] Track unresolved rows and reasons where possible.
- [x] Implement save behavior for working review data. (AC: 3-6)
  - [x] Persist edits back into the working review artifact.
  - [x] Ensure save does not destroy source evidence or original extracted values.
  - [x] Reopen the case and confirm saved review data is restored.
- [x] Implement approval gate and approved artifact output. (AC: 7, 8, 10)
  - [x] Block approval if unresolved rows or required missing values remain.
  - [x] Write `working/approved_review.json` only when current review data is approvable.
  - [x] Tie approval to review version/hash so later edits invalidate prior approval.
- [x] Update UI behavior and status messaging. (AC: 1-8)
  - [x] Reflect review stage readiness, edited state, unresolved blockers, and approval status in the dock pane.
  - [x] Keep status messages clear and technical.
  - [x] Do not expose final output/build actions yet.
- [x] Add focused tests. (AC: 1-10)
  - [x] Load review artifact into view-model state.
  - [x] Edit existing row persists.
  - [x] Add manual row persists.
  - [x] Unresolved row blocks approval.
  - [x] Approval writes `approved_review.json` with review hash/version.
  - [x] Reopen restores saved review edits and approval state.
  - [x] No `.gdb`, `output_summary.json`, or final geometry artifact is created by this story.
- [x] Validate and package. (AC: 1-10)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.

## Dev Notes

### Why This Story Exists

- Story 2.12 now generates draft review artifacts successfully from transaction-driven extraction.
- Real-world point extraction can be incomplete or partially wrong.
- The workflow must support human correction before parcel geometry generation.

### Current State From Prior Stories

- Story 2.11 resolves and persists a manifest-backed `script_plan`.
- Story 2.12 executes draft extraction and writes:
  - `working/extraction_review_data.json`
  - `working/extraction_points.json`
  - `working/plan_ocr.json`
  - generated per-case extraction config
- Current UI still relies on a simplified Extraction Review section and does not yet provide in-pane review editing.

### Recommended Review Data Behavior

The working review artifact should preserve:

- original extracted values
- source evidence references
- manual edits / override values
- unresolved flags
- row provenance (`extracted`, `manual`)
- review version/hash inputs

Avoid destructive overwrite of source-derived data.

### Approval Contract

`approved_review.json` should:

- reference the transaction id / number
- include review version/hash
- include approval timestamp and operator identity when available
- become invalid if the working review dataset changes later

### UX Guidance

The review UI should be practical, compact, and correction-oriented:

- visible row list/grid
- clear unresolved markers
- inline or side-panel editing
- add-point action
- explicit save and approve actions

This story should align to the existing review mockups rather than introducing a separate UI pattern.

### Scope Boundaries

- Do not build parcel geometry or `.gdb` outputs yet.
- Do not run validation or output generation yet.
- Do not introduce CADINDEX/Enterprise writes.
- Do not remove the Case Folder as the source of truth.

### References

- `docs/project/PROCESSING_ALIGNMENT.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-review-before-output.html`
- `_bmad-output/implementation-artifacts/2-12-execute-draft-extraction-and-review-artifact-generation.md`
- `c:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000206\working\extraction_review_data.json`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- `tools\validate_contracts.ps1`
- `tools\run_python_tests.ps1`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
- `tools\package_addin.ps1`

### Completion Notes List

- Added in-pane extraction review editing with manual row entry, unresolved flags, row coloring, and review summary/gate messaging.
- Added review persistence models/services that preserve original extracted values while saving human-reviewed values back into `working/extraction_review_data.json`.
- Added review approval output to `working/approved_review.json` with review version/hash binding; later saves invalidate stale approvals automatically.
- Extended workflow/case reopen support for `review_approved` state and kept downstream `.gdb`/output artifacts out of scope for this story.
- Verified with 153 passing .NET tests, scaffold validation, Python tests, solution build, and add-in packaging/redeployment.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ApprovedReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for in-pane review/edit workflow over extracted point data prior to parcel generation. | Codex |
| 2026-06-12 | 1.0 | Implemented in-pane review/edit workflow, review persistence/approval artifacts, focused tests, and packaged add-in. | Codex |
