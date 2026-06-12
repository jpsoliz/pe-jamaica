---
baseline_commit: handoff-2026-06-12
---

# Story 4.1: Run Validation on Approved Review Data

Status: review

## Story

As a cadastral technical staff user,
I want to run validation against the approved review dataset for the active transaction,
so that rule findings and validation gates are recorded before any local output package is generated.

## Acceptance Criteria

1. Given the case state is `review_approved` and `approved_review.json` matches the current review data hash/version, when the user starts validation, then the add-in invokes the validation processing adapter using approved review data, source inputs, DWG-derived context where available, and configured rules.
2. Given validation starts from a valid approved review snapshot, when execution begins, then the case state becomes `validation_running` during processing and the UI shows clear in-progress status without freezing ArcGIS Pro.
3. Given the approved review hash/version is stale because review data changed after approval, when the user attempts validation, then validation is blocked, the workflow returns to review, and the user receives clear status guidance to re-approve the review dataset.
4. Given validation completes successfully, when the adapter writes results, then `working/validation_summary.json` is created using lowercase `snake_case` contract fields and is registered as a case artifact.
5. Given validation runs, when the summary is produced, then it records rule profile/version, run metadata, status, findings, and any warnings/errors needed for later display and audit.
6. Given validation finds blocking issues, when the run completes, then the workflow moves into a blocked validation state and output generation remains unavailable.
7. Given validation finishes with no blocking issues, when the run completes, then the workflow moves into a passed validation state and the output stage becomes eligible.
8. Given this story is complete, then the validation implementation does not yet build output packages, `.gdb` geometry, or final reports; it only executes validation, persists `validation_summary.json`, and updates workflow state/gating.
9. Given this story is complete, then focused tests cover valid validation execution, stale approval rejection, blocked vs passed validation states, artifact persistence, reopen/resume behavior, and no regression to review/output gating.

## Tasks / Subtasks

- [x] Add validation workflow state and gating support. (AC: 1-3, 6-9)
  - [x] Extend workflow state support to include `validation_running`, `validation_blocked`, and `validation_passed`.
  - [x] Keep review approval, validation, and output eligibility distinct in command gating.
  - [x] Ensure validation is only available from a current `review_approved` snapshot and is disabled after stale review edits.
- [x] Add validation contract models and artifact persistence. (AC: 4, 5, 9)
  - [x] Implement C# contract models for `validation_summary.json` aligned with `src/Contracts/schemas/validation_summary.schema.json`.
  - [x] Preserve run metadata, rule profile/version, findings payload, warnings, and errors in lowercase `snake_case`.
  - [x] Register `working/validation_summary.json` as an available case artifact and support reopen/resume.
- [x] Implement stale approval verification before validation. (AC: 1, 3, 9)
  - [x] Read `approved_review.json` and compare its `review_data_hash` to the current extraction review data hash.
  - [x] Reject validation when approval is stale, set a clear status message, and return the workflow to `review_pending`.
  - [x] Remove or invalidate stale validation artifacts when review data changes after a successful validation run.
- [x] Implement validation adapter execution path. (AC: 1, 2, 4, 5, 8)
  - [x] Add a C# validation execution service/coordinator that invokes the Python validation adapter through the established processing boundary rather than direct legacy script calls.
  - [x] Define the adapter input bundle from the current Case Folder artifacts: manifest, approved review, source context, optional DWG context, and rules reference.
  - [x] Implement the Python-side `validation_adapter.py` contract so it writes a deterministic `validation_summary.json` result for this story’s scope.
- [x] Update Parcel Workflow UI/status handling for validation launch. (AC: 2, 6, 7, 8)
  - [x] Enable validation from the approved review state and reflect `validation_running`, `validation_blocked`, and `validation_passed` in the lifecycle/status area.
  - [x] Keep validation findings display lightweight in this story; Story 4.2 owns the richer grouped findings UI.
  - [x] Ensure output-stage controls remain gated until validation passes.
- [x] Preserve reopen/recovery and downstream artifact rules. (AC: 4-9)
  - [x] Reopen a case with an existing `validation_summary.json` and restore the correct validation state.
  - [x] Ensure validation does not create `output_summary.json`, `.gdb` outputs, reports, or final geometry artifacts in this story.
  - [x] Ensure later edits to review data invalidate stale validation readiness just as they invalidate approval.
- [x] Add focused tests. (AC: 1-9)
  - [x] Valid approved review runs validation and writes `validation_summary.json`.
  - [x] Stale approved review is rejected and returns to `review_pending`.
  - [x] Validation blocked results move the workflow to `validation_blocked`.
  - [x] Validation passed results move the workflow to `validation_passed`.
  - [x] Reopen restores validation artifacts/state correctly.
  - [x] No output package/report artifacts are created by this story.
- [x] Validate and package. (AC: 1-9)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.

## Dev Notes

### Why This Story Exists

- Review approval is now implemented and stable enough to serve as the handoff into validation.
- The current UI shows a Validation stage, but there is no executed validation engine or persisted validation contract yet.
- Output generation must not start until validation has produced a deterministic gate result.

### Current State From Prior Stories

- Story 2.12 writes draft extraction artifacts such as `working/extraction_review_data.json`.
- Story 2.13 introduced in-pane review editing plus `working/approved_review.json` with `review_data_hash` binding.
- Story 2.14A refined the review UX and approval flow, but intentionally left validation and outputs out of scope.
- The current dock pane lifecycle already contains placeholder Validation and Outputs steps that need real workflow state backing.

### Validation Workflow Intent

This story should make validation the first real downstream step after review approval:

1. read current Case Folder state
2. verify approval hash is still current
3. invoke Python validation adapter
4. write `working/validation_summary.json`
5. update workflow state to `validation_blocked` or `validation_passed`

Do not shortcut validation by treating review approval itself as equivalent to validation success.

### Contract and Rules Guidance

- Validation must use the existing adapter boundary in `src/ProcessingTools/adapters/validation_adapter.py`.
- C# must orchestrate state, gating, status, and artifact registration.
- Python must own validation rule execution and contract output writing.
- Validation severity gates must remain deterministic and separate from the deferred 0-10 solution score.
- `approved_review.json` is authoritative only if its `review_data_hash` still matches the current review dataset.

### Practical Scope for Story 4.1

For this story, implement a stable validation execution path and contract output, not the full rich findings UI:

- yes: adapter invocation, state transitions, artifact creation, reopen behavior
- yes: blocked/passed status logic
- no: full grouped validation explorer UI (Story 4.2)
- no: output package creation (Epic 5)
- no: manual-process decision panel (Story 4.4)

### Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/tests/test_adapter_placeholders.py`
- `src/Contracts/schemas/validation_summary.schema.json`
- `src/Contracts/examples/validation_summary.example.json`

### Architecture Compliance

- Keep the Case Folder as the v1 system of record.
- Do not let any ViewModel decide validation eligibility independently; keep gating owned by workflow/session logic.
- Do not bypass adapter boundaries by calling legacy scripts directly.
- Do not mix validation pass/fail gating with the later scoring formula.
- Do not create output artifacts in this story.

### Testing Requirements

- Add .NET tests for workflow state transitions, stale approval rejection, artifact persistence, and reopen behavior.
- Extend Python adapter tests so `validation_adapter.py` is no longer just a placeholder.
- Keep validation scaffold/contract checks passing.
- Preserve existing review approval and output gating behavior; validation should sit between them cleanly.

### Project Structure Notes

- The repo already contains contract placeholders for validation in `src/Contracts/` and adapter placeholders in `src/ProcessingTools/adapters/`.
- Validation artifacts belong under the Case Folder `working/` area in this story, matching the established canonical artifact list.
- Current sprint docs are stale relative to implemented review work; use the repo implementation history and Epic 4 requirements as the source of truth for sequencing here.

### References

- `_bmad-output/planning-artifacts/epics.md` (Story 4.1, Epic 4, Appendix: workflow state and artifact contract)
- `_bmad-output/planning-artifacts/architecture.md` (state machine, adapter boundaries, validation contract, gating rules)
- `_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md` (FR-12, FR-13, validation/output sequencing)
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md` (validation stage and finding presentation direction)
- `docs/project/PROCESSING_ALIGNMENT.md` (approved review precedes downstream processing)
- `src/Contracts/schemas/validation_summary.schema.json`
- `src/Contracts/examples/validation_summary.example.json`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `_bmad-output/implementation-artifacts/2-13-review-edit-point-workflow.md`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- `powershell -ExecutionPolicy Bypass -File tools\run_python_tests.ps1`
- `powershell -ExecutionPolicy Bypass -File tools\validate_contracts.ps1`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
- `powershell -ExecutionPolicy Bypass -File tools\package_addin.ps1`

### Completion Notes List

- Added validation workflow states, stale approval protection, validation artifact persistence, and reopen recovery support.
- Implemented `validation_adapter.py` with deterministic blocked/passed summary generation from approved review data.
- Added lightweight validation UI in the Parcel Workflow dockpane and kept outputs gated behind validation pass.
- Added focused .NET and Python tests for validation execution, stale approval rejection, blocked vs passed outcomes, and reopen behavior.

### File List

- `_bmad-output/implementation-artifacts/4-1-run-validation-on-approved-review-data.md`
- `src/Contracts/examples/validation_summary.example.json`
- `src/Contracts/schemas/validation_summary.schema.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/IValidationExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationSummaryPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/rules/rules.yaml`
- `src/ProcessingTools/tests/test_adapter_placeholders.py`
- `src/ProcessingTools/tests/test_validation_adapter.py`
