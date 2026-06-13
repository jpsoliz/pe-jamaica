---
baseline_commit: handoff-2026-06-12
---

# Story 4.4: Generate Transaction Output GDB From Approved Review Data

Status: review

## Story

As a cadastral technical staff user,  
I want the add-in to generate a transaction-local ArcGIS output geodatabase from the approved review dataset,  
so that I can inspect the resulting parcel geometry directly in ArcGIS Pro and move the case toward final completion.

## Acceptance Criteria

1. Given the active transaction is in `validation_passed` state and `approved_review.json` is current, when the user starts the Outputs stage, then the add-in generates the transaction output package from approved review data rather than re-reading the original PDF/image source files.
2. Given output generation starts from a valid approved review dataset, when execution begins, then the workflow enters an `output_running` state and the UI shows clear in-progress status without freezing ArcGIS Pro.
3. Given output generation succeeds, when the stage finishes, then a transaction-local file geodatabase is created inside the transaction folder and contains the required parcel output layers for the current use case, including points, lines, and parcel/polygon geometry where supported by the approved review data.
4. Given the current case uses approved extracted/reviewed point data, when the output stage runs, then the implementation uses the approved review artifact as the authoritative geometry input and does not rerun OCR, OpenAI extraction, or source-document point parsing.
5. Given a standard ArcGIS Pro project/template or schema scaffold is required for the `.gdb`, when outputs are created, then the generation step uses that configured standard structure instead of an ad hoc geodatabase layout.
6. Given output artifacts are created successfully, when the add-in refreshes workflow state, then it registers output artifacts such as the `.gdb`, feature-class outputs, `output_summary.json`, and any derived geometry artifacts needed for map loading and audit.
7. Given outputs are created successfully, when the user remains in ArcGIS Pro, then the add-in adds the generated output layer(s) to the active map and zooms to the created geometry so the map becomes the primary review surface for this stage.
8. Given output generation fails, when the add-in handles the failure, then the user receives deterministic status guidance, previously approved review/validation artifacts are preserved, and no stale success state is shown for Outputs or Ready to Complete.
9. Given Outputs completes successfully, when workflow gating is recalculated, then Outputs becomes complete and `Ready to Complete` becomes enabled as the next stage.
10. Given this story is complete, then focused tests cover output-stage gating, approved-review-only generation, `.gdb` creation, output artifact persistence, map-add/zoom orchestration boundaries, and the absence of extraction reruns during the output stage.

## Tasks / Subtasks

- [x] Add output-stage workflow state and gating support. (AC: 1-2, 8-10)
  - [x] Extend workflow/session logic to support `output_running` and `output_created` states cleanly.
  - [x] Ensure Outputs is only available from a valid `validation_passed` state with current approved review data.
  - [x] Keep `Ready to Complete` unavailable until Outputs succeeds.

- [x] Define the output-generation contract from approved review data. (AC: 1, 3-6, 10)
  - [x] Define the required input artifact set, centered on `working/approved_review.json`.
  - [x] Define expected output artifacts such as transaction-local `.gdb`, output summary, generated geometry files, and any intermediate output metadata.
  - [x] Keep JSON artifact fields and filenames in lowercase `snake_case`.

- [x] Implement a dedicated output-generation adapter/service. (AC: 1-6, 8, 10)
  - [x] Add a bounded output execution service under the existing workflow/processing boundary.
  - [x] Implement a dedicated Python entrypoint for output generation from approved review data, reusing pieces of `CreateParcelFromFile.py` only where appropriate.
  - [x] Ensure the new output path does not rerun extraction, OCR, or AI parsing from source documents.

- [x] Create transaction-local `.gdb` outputs using approved review data. (AC: 3-6)
  - [x] Generate the `.gdb` inside the current transaction folder.
  - [x] Create required points, lines, and parcel/polygon outputs for the active scenario where sufficient approved data exists.
  - [x] Use the configured ArcGIS Pro template/project or schema scaffold required by Sidwell’s target output structure.

- [x] Register output artifacts and refresh workflow state. (AC: 6, 8-10)
  - [x] Write and persist `working/output_summary.json` or `output/output_summary.json` according to the established contract.
  - [x] Register created `.gdb` and related output artifacts in the case artifact list.
  - [x] Restore output state correctly on reopen/resume when outputs already exist.

- [x] Add ArcGIS Pro map integration for generated outputs. (AC: 7, 10)
  - [x] Add generated layer(s) to the active map after successful output creation.
  - [x] Zoom/select to the generated geometry extent so the map becomes the primary review surface.
  - [x] Keep dockpane responsibility focused on workflow status and artifact actions, not full map replacement.

- [x] Preserve failure handling and stage boundaries. (AC: 4, 8-10)
  - [x] Preserve approved review and validation artifacts when output generation fails.
  - [x] Surface clear redacted status for script/process failures.
  - [x] Ensure failed output runs do not mark `Ready to Complete` as available.

- [x] Add focused tests and validation. (AC: 1-10)
  - [x] Test that Outputs runs only after `validation_passed`.
  - [x] Test that approved review data, not original source files, drives output generation.
  - [x] Test `.gdb` creation/artifact registration for a valid approved case.
  - [x] Test failure handling with no false success state.
  - [x] Test reopen/resume when outputs already exist.
  - [x] Test that extraction is not rerun as part of Outputs.

- [x] Validate and package. (AC: 1-10)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.

## Dev Notes

### Why This Story Exists

- Validation now gates downstream geometry creation, but the workflow still needs a real Outputs implementation.
- The user’s review/approval work should become the authoritative source for geometry creation.
- ArcGIS Pro itself should become the main inspection surface once geometry exists, with the dockpane guiding stage flow rather than trying to contain all geometry review UI.

### Current State From Prior Stories

- Story 2.12 generates draft extraction artifacts such as `working/extraction_review_data.json`.
- Story 2.13 and Story 2.14A add review/edit/approval behavior and produce `working/approved_review.json`.
- Story 4.1 adds validation against approved review data and gates output creation on `validation_passed`.
- Story 4.2A reshapes the dockpane into a stage-focused workspace, but Outputs is still mostly a placeholder.

### Output Stage Intent

This story should make Outputs the first geometry/materialization stage:

1. read approved review data  
2. generate transaction-local `.gdb` outputs  
3. register output artifacts  
4. add output layers to the active ArcGIS Pro map  
5. move workflow into `output_created`  
6. enable `Ready to Complete`

Do not treat Outputs as another extraction pass.

### Alignment Rules

- `approved_review.json` is the authoritative geometry input after review approval and validation pass.
- Original PDF/TIFF/PNG/JPG inputs are still evidence/reference, not the direct input for this stage.
- If legacy Python tooling currently combines extraction and output generation, split or wrap it so Outputs only consumes approved reviewed data.
- Keep OpenAI optional and outside this story’s primary execution path unless a downstream output helper explicitly requires it; Outputs should not depend on rerunning AI extraction.

### Recommended Technical Direction

Prefer a narrower dedicated script or adapter instead of calling the full legacy script end-to-end:

- candidate shape: `GenerateParcelOutputsFromApprovedReview.py`
- inputs:
  - `working/approved_review.json`
  - current transaction/case metadata
  - configured output/template settings
- outputs:
  - transaction-local `.gdb`
  - output feature classes
  - `output_summary.json`
  - any map-load metadata needed by C#

If code is reused from `CreateParcelFromFile.py`, reuse only the parcel/GDB creation pieces and not the document parsing/extraction pieces.

### ArcGIS Pro UX Guidance

Best UX for this stage:

- dockpane: show stage status, output summary, artifact actions, and completion gating
- ArcGIS Pro map: primary place to inspect generated points/lines/parcels
- after successful output generation:
  - add outputs to active map
  - zoom to geometry
  - let the user inspect there before final completion/sync

This keeps the limited dockpane space focused and avoids forcing heavy geometry inspection into cramped form controls.

### Suggested Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ProcessingTools/`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcelFromFile.py`

### Scope Boundaries

- Do not rerun source-document extraction here.
- Do not redesign the map-review experience into a large custom embedded editor in this story.
- Do not implement ArcGIS Enterprise sync in this story.
- Do not collapse Outputs and Ready to Complete into the same stage; Outputs creates local geometry, Ready to Complete prepares for final case completion.

### References

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/implementation-artifacts/2-12-execute-draft-extraction-and-review-artifact-generation.md`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`
- `_bmad-output/implementation-artifacts/4-1-run-validation-on-approved-review-data.md`
- `_bmad-output/implementation-artifacts/4-3-save-and-resume-transaction-cases-through-innola-resume-package.md`
- `docs/project/PROCESSING_ALIGNMENT.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcelFromFile.py`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Completion Notes List

- Added explicit `output_running` and `output_created` workflow states, dockpane gating, and Ready to Complete progression after successful output generation.
- Implemented a dedicated output execution path that consumes approved review data and writes transaction-local `output_summary.json`, `extracted_geometry.geojson`, and `.gdb` outputs without rerunning extraction.
- Added ArcGIS Pro map integration to load generated layers into the active map and zoom to the created geometry after output creation.
- Added reopen/resume support, stale-artifact cleanup rules, focused .NET tests, Python adapter tests, contract validation, and successful add-in packaging.

### File List

- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_adapter_placeholders.py`
- `src/ProcessingTools/tests/test_output_adapter.py`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for generating transaction-local output geodatabase artifacts from approved review data and loading them into ArcGIS Pro. | Codex |
| 2026-06-12 | 1.0 | Implemented output-stage generation, artifact persistence, map loading, reopen support, focused tests, and packaged the add-in. | Codex |
