---
baseline_commit: NO_VCS
---

# Story 1.2: Create Transaction Case Folder

Status: done

## Story

As a cadastral technical staff user,
I want to create a new transaction workspace from the ArcGIS Pro dock pane,
so that all source files, state, logs, and outputs for the transaction are organized under one Case Folder.

## Acceptance Criteria

1. Given ArcGIS Pro is open and no active case is loaded, when the user enters or confirms a transaction ID and selects an output location, then the add-in creates a Case Folder using the transaction ID.
2. The Case Folder contains the required v1 subfolders for source files, working state, outputs, reports, and logs.
3. A `manifest.json` file is initialized using lowercase snake_case fields.
4. The dock pane moves the case state from `no_case` to `intake`.
5. The transaction header displays transaction ID, current step, and status.

## Tasks / Subtasks

- [x] Add Case Folder domain model and layout service. (AC: 1, 2)
  - [x] Create `CaseFolders/CaseFolderLayout.cs` or equivalent to centralize all canonical paths.
  - [x] Create `CaseFolders/CaseFolderStore.cs` or equivalent to create a transaction folder and required child folders.
  - [x] Required v1 layout for this story: `manifest.json`, `source/`, `working/`, `output/`, `output/reports/`, and `output/logs/`.
  - [x] Use the transaction ID as the folder name; do not copy source files in this story.
- [x] Add manifest initialization. (AC: 3)
  - [x] Add C# contract model/serializer under `Contracts/` for the initial manifest.
  - [x] Ensure serialized JSON uses lowercase snake_case property names.
  - [x] Include at minimum `schema_version`, `transaction_id`, `run_id`, `created_at`, `created_by`, `source_manifest_hash`, `payload`, `warnings`, and `errors`.
  - [x] In `payload`, include `workflow_state: "intake"` and `source_files: []`.
  - [x] Update `src/Contracts/schemas/manifest.schema.json` and `src/Contracts/examples/manifest.example.json` only if implementation needs a tighter contract; preserve compatibility with existing example shape.
- [x] Add workflow state and command-gating primitives. (AC: 4)
  - [x] Create `Workflow/WorkflowState.cs` with at least `NoCase` and `Intake`.
  - [x] Create a small state holder/service used by the dock pane view model.
  - [x] Ensure creating a case transitions from `no_case` to `intake`.
  - [x] Do not implement later states or processing gates beyond names/tests needed for this story.
- [x] Add dock pane view-model surface for case creation. (AC: 1, 4, 5)
  - [x] Extend `ParcelWorkflowDockpaneViewModel` to expose transaction ID, output location, current workflow state, status text, and a create-case command/method.
  - [x] Update `ParcelWorkflowDockpane.xaml` to display a compact transaction header with transaction ID, current step, and status.
  - [x] Keep UI functional and minimal; do not add source-file browsing, profile detection, preflight, or map preview in this story.
- [x] Add tests for Case Folder creation and manifest output. (AC: 1-4)
  - [x] Add tests under `ParcelWorkflowAddIn.Tests/CaseFolders/`.
  - [x] Verify folder layout is created under a temporary output root.
  - [x] Verify duplicate transaction behavior is explicit: either fail with a clear result or create a deterministic non-destructive alternative, but do not silently overwrite an existing Case Folder.
  - [x] Verify `manifest.json` parses and all top-level fields are lowercase snake_case.
  - [x] Verify initial manifest contains the transaction ID and `payload.workflow_state == "intake"`.
- [x] Add tests for workflow/view-model state. (AC: 4, 5)
  - [x] Add tests under `ParcelWorkflowAddIn.Tests/Workflow/`.
  - [x] Verify initial state is `no_case`.
  - [x] Verify successful case creation exposes transaction ID, step/status text, and intake state.
- [x] Update validation tooling. (AC: 1-5)
  - [x] Extend `tools/validate_contracts.ps1` only if needed to check new required source files or contract examples.
  - [x] Ensure `dotnet restore`, `dotnet build`, and relevant tests pass.

### Review Findings

- [x] [Review][Patch] Dock pane references an unmerged WPF font resource [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml:18]
- [x] [Review][Patch] Manifest schema does not enforce the required initial payload fields [src/Contracts/schemas/manifest.schema.json:14]
- [x] [Review][Patch] Invalid output root exceptions can escape case-folder creation instead of returning a failed result [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs:35]
- [x] [Review][Patch] Blank create-case input leaves the dock pane status unchanged instead of showing the validation failure [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:34]

## Dev Notes

### Previous Story Intelligence

- Story 1.1 is done; the project now targets the ArcGIS Pro 3.6 / Visual Studio 2022 / .NET 8 lane as the compatibility floor.
- The C# add-in scaffold now references local ArcGIS Pro runtime assemblies from `C:\Program Files\ArcGIS\Pro\bin`.
- `Module1` derives from `ArcGIS.Desktop.Framework.Contracts.Module`.
- `ParcelWorkflowDockpaneViewModel` derives from `ArcGIS.Desktop.Framework.Contracts.DockPane`.
- `Config.daml` maps `ParcelWorkflow_Dockpane` to `ParcelWorkflowDockpaneViewModel` and `ParcelWorkflowDockpane`.
- Code review found and fixed a prior false-positive scaffold: validation must prove ArcGIS Pro SDK-shaped entrypoints, not merely a compiling WPF project.
- Build/test commands from Story 1.1:
  - `tools/validate_contracts.ps1`
  - `tools/run_python_tests.ps1`
  - `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`

### Current Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: minimal SDK `DockPane` subclass.
  - This story changes it to expose case creation state/commands for the dock pane.
  - Preserve SDK inheritance and `DockPaneId`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: placeholder text only.
  - This story changes it to a minimal intake/header UI.
  - Preserve ArcGIS dock pane content class binding and avoid adding a map preview.
- `src/Contracts/schemas/manifest.schema.json`
  - Current state: permissive shared-envelope schema.
  - This story may tighten it if needed, but must stay compatible with lowercase snake_case and future Case Folder recovery.
- `src/Contracts/examples/manifest.example.json`
  - Current state: initial intake-style example with empty `source_files`.
  - Keep it aligned with the C# serializer output.
- `tools/validate_contracts.ps1`
  - Current state: validates scaffold paths, ArcGIS SDK-shaped entrypoints, JSON example casing, and configured Python.
  - Extend only for stable story outputs, not temp test folders.

### Architecture Requirements

- `CaseFolders/` owns transaction folder layout, source copying, artifact path resolution, artifact hash calculation, and reopen/resume behavior. No other component should hardcode artifact paths.
- `Contracts/` owns serialization/deserialization and schema alignment. Any file exchanged between C# and Python must have a schema and example.
- `Workflow/` owns state transitions and command gating. No ViewModel should independently decide later-step availability.
- The Case Folder is the system of record; hidden ArcGIS Pro project state must not be required for recovery or audit.
- C# owns workflow state, command gating, Case Folder orchestration, and user-facing status/progress.
- Python is not involved in this story.

### Case Folder Contract

Use the architecture’s v1 folder shape, scoped to this story:

```text
TR-SMD-0000001/
  manifest.json
  source/
  working/
  output/
    reports/
    logs/
```

Later stories add `working/preflight_summary.json`, `working/extraction_review_data.json`, `working/approved_review.json`, `working/validation_summary.json`, `output/result.gdb/`, `output/extracted_geometry.geojson`, `output/output_summary.json`, and reports/log contents. Do not create placeholder runtime artifacts for later workflow steps in Story 1.2 unless the tests explicitly need empty directories.

### Transaction ID And Duplicate Behavior

- v1 transaction IDs use the current pattern `TR-SMD-0000001`.
- Implement validation sufficient to reject empty, whitespace-only, and path-traversal-like IDs.
- Do not allow transaction IDs to escape the selected output root.
- Duplicate transaction behavior must be explicit and tested. Recommended for this story: fail creation if the target Case Folder already exists and contains a `manifest.json`, returning a clear error/status rather than overwriting.

### Manifest Requirements

The initial `manifest.json` must use lowercase snake_case fields. Minimum top-level envelope:

```json
{
  "schema_version": "1.0.0",
  "transaction_id": "TR-SMD-0000001",
  "run_id": "run-...",
  "created_at": "2026-06-09T00:00:00Z",
  "created_by": null,
  "source_manifest_hash": null,
  "payload": {
    "workflow_state": "intake",
    "source_files": []
  },
  "warnings": [],
  "errors": []
}
```

`created_at` and `run_id` may be generated dynamically. Tests should normalize or assert shape rather than hardcoding exact timestamps/IDs.

### UX Requirements

- The dock pane is the primary workflow container.
- For this story, show a transaction header with transaction ID, current step, and status.
- Keep the UI compact and ArcGIS Pro-adjacent. Use `Segoe UI` styling already started in `Resources/Styles.xaml` where practical.
- Do not implement source file rows, profile detection, preflight results, extraction review, validation, outputs, sync readiness, or embedded map preview in this story.

### Testing Guidance

- Prefer unit tests for Case Folder creation and manifest serialization. These are the core business logic for this story.
- Use temporary directories in tests; do not write test Case Folders into repo fixtures or planning artifacts.
- Tests must prove the manifest is recoverable JSON and uses lowercase snake_case top-level fields.
- If ArcGIS Pro SDK types make direct ViewModel construction difficult in tests, keep ArcGIS-dependent wrappers thin and test the underlying state/services separately.
- After implementation, run:
  - `tools/validate_contracts.ps1`
  - `tools/run_python_tests.ps1`
  - `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`

### Scope Boundaries

- Do not copy source files; Story 1.3 owns source file add/copy.
- Do not detect Scenario A/B; Story 1.4 owns input profile detection.
- Do not implement reopen/resume; Story 1.5 owns that.
- Do not invoke Python or ArcPy.
- Do not create preflight, extraction, validation, output, or sync artifacts.
- Do not add live CADINDEX/Enterprise operations.

### References

- [epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Story 1.2 acceptance criteria and implementation/testability appendix.
- [architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): Case Folder as system of record, workflow boundary, Case Folder boundary, contract boundary, and target project structure.
- [prd.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md): FR1, FR4, v1 transaction format, Case Folder definition, recoverability/audit requirements.
- [1-1-set-up-arcgis-pro-add-in-and-processing-scaffold.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/1-1-set-up-arcgis-pro-add-in-and-processing-scaffold.md): completed scaffold and review findings.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because `CaseFolders` and `Workflow` namespaces did not exist.
- Green phase: C# test runner passed 5 tests.
- `tools/validate_contracts.ps1` passed.
- `tools/run_python_tests.ps1` passed: 2 Python tests.
- `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.

### Completion Notes List

- Added `CaseFolderLayout`, `CaseFolderStore`, and `CaseFolderCreationResult` to create transaction Case Folders with centralized canonical paths.
- Added initial manifest contract model/serializer and tightened `manifest.schema.json` to require the full initial envelope.
- Added `WorkflowState`, state display helpers, and `WorkflowSession` to move from `no_case` to `intake` after successful case creation.
- Extended the ArcGIS dock pane view model and XAML with transaction ID, output location, current step, and status surface.
- Added a no-package C# test runner with Case Folder and workflow tests covering layout, manifest JSON/casing, duplicate handling, unsafe IDs, and state/status behavior.
- Extended `tools/validate_contracts.ps1` to check the new Story 1.2 C# source/test files.

### File List

- `_bmad-output/implementation-artifacts/1-2-create-transaction-case-folder.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Contracts/schemas/manifest.schema.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/TempDirectory.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/TestAssert.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderCreationResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `tools/validate_contracts.ps1`

### Change Log

- 2026-06-09: Implemented Story 1.2 Case Folder creation and moved story to review.
