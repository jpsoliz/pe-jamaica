---
baseline_commit: NO_VCS
---

# Story 1.1: Set Up ArcGIS Pro Add-in and Processing Scaffold

Status: done

## Story

As a development team member,
I want the ArcGIS Pro add-in, dock pane, Python toolbox scaffold, shared contract folder, and fixture folder structure created,
so that implementation stories have a consistent project foundation aligned with the approved architecture.

## Acceptance Criteria

1. Given the selected architecture uses the ArcGIS Pro Module Add-in template, Dockpane item template, and Python toolbox/script-tool scaffold, when the initial project scaffold is created, then the repository contains the C# ArcGIS Pro add-in project with module and dock pane entrypoints.
2. The repository contains a Python toolbox or script-tool package location for processing adapters.
3. The repository contains shared JSON schema/contract and example artifact locations.
4. The repository contains fixture folder locations for Case 1, Case 2, Case 3, and Case 4.
5. Initial configuration documents the chosen ArcGIS Pro SDK/toolchain lane before feature implementation proceeds.

## Tasks / Subtasks

- [x] Create the ArcGIS Pro add-in solution scaffold. (AC: 1, 5)
  - [x] Create a Visual Studio ArcGIS Pro Module Add-in project under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/`.
  - [x] Add the ArcGIS Pro Dockpane item/template for the Parcel Workflow UI.
  - [x] Ensure the scaffold includes module and dock pane entrypoints, DAML registration, and add-in packaging output.
  - [x] Add a solution file under `src/ParcelWorkflowAddIn/`.
- [x] Document and lock the SDK/toolchain lane. (AC: 5)
  - [x] Add initial configuration or docs that states the selected lane: ArcGIS Pro 3.6 SDK + Visual Studio 2022 + .NET 8, or ArcGIS Pro 3.7 SDK + Visual Studio 2026 + .NET 10.
  - [x] Do not mix SDK/runtime/tooling lanes.
  - [x] Record the configured ArcGIS Python executable path: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe`.
- [x] Create C# architectural folders for future stories. (AC: 1)
  - [x] Add folders for `Workflow`, `CaseFolders`, `Contracts`, `Processing`, `ArcGIS`, `Sync`, `Settings`, and `Resources`.
  - [x] Add a test project/folder structure for workflow state, case folder behavior, contracts, and sync facade tests.
  - [x] Keep this story to scaffold and placeholders only; do not implement intake, extraction, validation, output generation, or live sync behavior.
- [x] Create the Python processing scaffold. (AC: 2)
  - [x] Add `src/ProcessingTools/parcel_workflow.pyt` or equivalent script-tool package entrypoint.
  - [x] Add `adapters/`, `contracts/`, `legacy/`, `rules/`, `reporting/`, `providers/`, `utils/`, and `tests/` folders.
  - [x] Add adapter placeholder files for preflight, extraction, validation, and output generation.
  - [x] Add a README or config note explaining that C# must call stable adapter/tool entrypoints, not legacy scripts directly.
- [x] Create shared contracts and examples. (AC: 3)
  - [x] Add `src/Contracts/schemas/` with placeholder schema files for `manifest`, `preflight_summary`, `extraction_review_data`, `approved_review`, `validation_summary`, `output_summary`, and `fixture_manifest`.
  - [x] Add `src/Contracts/examples/` with matching example JSON files using lowercase snake_case fields.
  - [x] Include a minimal shared envelope in examples: `schema_version`, `transaction_id`, `run_id`, `created_at`, `created_by`, provenance/hash field, payload, and warnings/errors where applicable.
- [x] Create fixture and tooling folders. (AC: 4)
  - [x] Add `fixtures/case_1` through `fixtures/case_4`, each with `fixture_manifest.json`, `source/`, and `expected/`.
  - [x] Add `tools/validate_contracts.ps1`, `tools/run_python_tests.ps1`, and `tools/package_addin.ps1` placeholders or working stubs.
- [x] Validate scaffold shape. (AC: 1-5)
  - [x] Verify the repository contains the required C#, Python, contracts, examples, fixture, and tool folders.
  - [x] Verify JSON examples parse and use lowercase snake_case keys.
  - [x] Verify the configured ArcGIS Python executable runs and imports `encodings`.
  - [x] Document any local machine prerequisites that prevent compiling/running the ArcGIS Pro add-in in this workspace.

### Review Findings

- [x] [Review][Patch] Replace generic WPF scaffold with ArcGIS Pro SDK add-in entrypoints [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj:1] — AC1 requires a C# ArcGIS Pro add-in project with module and dock pane entrypoints, but the project is a plain `Microsoft.NET.Sdk` WPF project with no ArcGIS Pro SDK reference/import, `Module1` is a plain class instead of an ArcGIS Pro SDK `Module`, and the dock pane is only a `UserControl` without the SDK dock pane/view-model pattern. This may compile locally but will not behave as an ArcGIS Pro Module Add-in scaffold.
- [x] [Review][Patch] Add or remove the DAML image reference so scaffold validation matches package contents [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml:10] — `Config.daml` references `Images\AddInDesktop32.png`, but the scaffold does not include that asset and `tools/validate_contracts.ps1` does not check it. A later package/load step can fail or produce a broken add-in asset reference.

## Dev Notes

### Current Repository State

- This workspace currently contains planning and BMad artifacts only. No existing `.sln`, `.csproj`, `.cs`, `.xaml`, `.pyt`, production `.py`, or contract schema files were found outside BMad support scripts.
- This story is therefore a new scaffold story. There are no existing implementation files to preserve, but do not modify unrelated BMad skill or planning artifact files except sprint tracking.
- This folder is not currently detected as a Git repository by `git status`; avoid workflow assumptions that require git metadata.

### Architecture Requirements

- Use ArcGIS Pro SDK add-in architecture, not a standalone desktop, web, Electron, Tauri, or Python-only product foundation.
- Selected starter is ArcGIS Pro Module Add-in + Dockpane Item Template + Python Toolbox Scaffold.
- C# owns user workflow, workflow state, command gating, Case Folder orchestration, ArcGIS Pro integration, status, and progress display.
- Python owns ArcPy-dependent preflight, extraction, validation/rules, GDB/GeoJSON/report/log generation, DWG processing, and existing script integration.
- C# and Python must communicate through versioned, file-based JSON contracts rather than ad hoc script arguments or duplicated business logic.
- The Case Folder is the future system of record; hidden ArcGIS Pro project state must not be required for recovery or audit.
- CADINDEX/ArcGIS Enterprise sync remains facade-only in v1. Do not create any live Enterprise/CADINDEX write path in this scaffold.

### Required Project Structure

Use this target structure from the architecture as the scaffold guide:

```text
src/
  ParcelWorkflowAddIn/
    ParcelWorkflowAddIn.sln
    ParcelWorkflowAddIn/
      Workflow/
      CaseFolders/
      Contracts/
      Processing/
      ArcGIS/
      Sync/
      Settings/
      Resources/
    ParcelWorkflowAddIn.Tests/
      Workflow/
      CaseFolders/
      Contracts/
      Sync/
  ProcessingTools/
    parcel_workflow.pyt
    adapters/
    contracts/
    legacy/
    rules/
    reporting/
    providers/
    utils/
    tests/
  Contracts/
    schemas/
    examples/
fixtures/
  case_1/
  case_2/
  case_3/
  case_4/
tools/
```

Create placeholder files only where they help preserve empty folders or document intended ownership. Avoid building feature logic before the related implementation story.

### Contract Requirements

- Required contract artifact names: `manifest.json`, `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, `process.log`, and `extracted_geometry.geojson`.
- Schema files should live in `src/Contracts/schemas/`.
- Example files should live in `src/Contracts/examples/`.
- JSON field names and artifact filenames must be lowercase snake_case.
- Each JSON artifact should include a version/provenance envelope sufficient for later Case Folder recovery, audit, and stale-approval checks.

### SDK / Runtime Lane

- Architecture requires one compatible lane before implementation setup proceeds:
  - ArcGIS Pro 3.6 lane: ArcGIS Pro SDK 3.6, Visual Studio 2022, .NET 8.
  - ArcGIS Pro 3.7 lane: ArcGIS Pro SDK 3.7, Visual Studio 2026, .NET 10.
- Latest Esri guidance confirms ArcGIS Pro 3.6 SDK requires Visual Studio 2022 v17.13 or higher and .NET 8 runtime support.
- Esri's 2026 Pro SDK guidance says ArcGIS Pro 3.7 moves to .NET 10 and requires Visual Studio 2026 version 18.3.2 or later for Pro SDK development.
- If the local machine only has one ArcGIS Pro version installed, choose the matching lane and document it. If both are available, prefer the lane installed for the target user environment and record the reason.
- Do not target .NET 9 for the Pro 3.6 lane; Esri notes later .NET versions do not satisfy the Pro 3.6 .NET Desktop Runtime 8 requirement.

### Python Environment

- Use the project ArcGIS Python environment path from planning artifacts:
  `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe`
- Verified during planning: that executable exists and can import `encodings`; it reported Python `3.13.7`.
- The default `python` command in this workspace still fails with `ModuleNotFoundError: No module named 'encodings'`. Do not rely on bare `python` for project scripts until that shell-level issue is fixed.
- Story 2.2 will implement deeper ArcGIS/ArcPy/package preflight. Story 1.1 only needs to document the configured executable and add test/tool placeholders that can use it.

### UX Alignment

- The primary UI surface is an ArcGIS Pro dock pane using WPF/MVVM.
- The dock pane should be compact and ArcGIS Pro-adjacent, with workflow navigation for Intake, Preflight, Review, Validation, Outputs, and Sync Readiness in later stories.
- Active ArcGIS Pro map remains the companion surface. Do not embed a map preview in the dock pane scaffold.
- Use `Resources/Styles.xaml` as the future location for UI tokens/styles.

### Testing Guidance

- Add structure for C# unit tests around workflow state and contract serialization, but do not require complete test coverage before feature classes exist.
- Add Python test structure for processing adapters and contract writing.
- Add tool placeholders for contract validation, Python tests, and add-in packaging.
- Done for this story means scaffold presence, lane documentation, JSON example parseability, Python executable sanity check, and clear local prerequisite notes.

### References

- [epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Story 1.1 acceptance criteria and implementation/testability appendix.
- [architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): selected starter, SDK lane, data architecture, contracts, and target project structure.
- [implementation-readiness-report-2026-06-08.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-08.md): READY status and warning about default Python.
- Esri ArcGIS Pro SDK 3.6 API Reference: https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/conceptdocs/docs/Home.html
- Esri ArcGIS Pro 3.6 system requirements: https://pro.arcgis.com/en/pro-app/latest/get-started/arcgis-pro-system-requirements.htm
- Esri ArcGIS Pro SDK 2026 Q2 update: https://www.esri.com/arcgis-blog/products/arcgis-pro-net/developers/whats-new-in-the-arcgis-pro-sdk-2026-q2

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `tools/validate_contracts.ps1` red phase failed before scaffold creation with missing scaffold paths.
- `tools/validate_contracts.ps1` passed after scaffold creation.
- `tools/run_python_tests.ps1` passed: 2 Python unit tests.
- `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` originally passed after selecting the ArcGIS Pro 3.7 / .NET 10 lane.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.
- Code review patch run: `tools/validate_contracts.ps1`, `tools/run_python_tests.ps1`, `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`, and `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` all passed after SDK-shaped scaffold fixes.

### Completion Notes List

- Created the ArcGIS Pro add-in solution scaffold with module, dock pane XAML/code-behind, DAML registration, settings, resources, and test project structure.
- 2026-06-10 compatibility update: project retargeted to the ArcGIS Pro 3.6 / Visual Studio 2022 / .NET 8 lane as the compatibility floor for Pro 3.6 and forward smoke-testing in Pro 3.7.
- Created Python toolbox and adapter placeholders with tests that explicitly assert later-story behavior is not implemented yet.
- Added shared JSON schemas/examples, Case 1-4 fixture folders/manifests, validation/test/package scripts, and `.gitignore` for generated outputs.
- Resolved code review findings by referencing local ArcGIS Pro runtime assemblies, making `Module1` derive from `ArcGIS.Desktop.Framework.Contracts.Module`, adding a `DockPane` view model, wiring DAML to the view/view-model pair, removing the missing DAML image reference, and validating these conditions in `tools/validate_contracts.ps1`.

### File List

- `.gitignore`
- `docs/toolchain.md`
- `fixtures/case_1/expected/.gitkeep`
- `fixtures/case_1/fixture_manifest.json`
- `fixtures/case_1/source/.gitkeep`
- `fixtures/case_2/expected/.gitkeep`
- `fixtures/case_2/fixture_manifest.json`
- `fixtures/case_2/source/.gitkeep`
- `fixtures/case_3/expected/.gitkeep`
- `fixtures/case_3/fixture_manifest.json`
- `fixtures/case_3/source/.gitkeep`
- `fixtures/case_4/expected/.gitkeep`
- `fixtures/case_4/fixture_manifest.json`
- `fixtures/case_4/source/.gitkeep`
- `src/Contracts/examples/approved_review.example.json`
- `src/Contracts/examples/extraction_review_data.example.json`
- `src/Contracts/examples/manifest.example.json`
- `src/Contracts/examples/output_summary.example.json`
- `src/Contracts/examples/preflight_summary.example.json`
- `src/Contracts/examples/validation_summary.example.json`
- `src/Contracts/schemas/approved_review.schema.json`
- `src/Contracts/schemas/extraction_review_data.schema.json`
- `src/Contracts/schemas/fixture_manifest.schema.json`
- `src/Contracts/schemas/manifest.schema.json`
- `src/Contracts/schemas/output_summary.schema.json`
- `src/Contracts/schemas/preflight_summary.schema.json`
- `src/Contracts/schemas/validation_summary.schema.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Contracts/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Sync/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ArcGIS/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Module1.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Processing/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Resources/Styles.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Sync/.gitkeep`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/.gitkeep`
- `src/ProcessingTools/README.md`
- `src/ProcessingTools/adapters/__init__.py`
- `src/ProcessingTools/adapters/extraction_adapter.py`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/adapters/preflight_adapter.py`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/contracts/__init__.py`
- `src/ProcessingTools/contracts/contract_writer.py`
- `src/ProcessingTools/contracts/schema_loader.py`
- `src/ProcessingTools/legacy/.gitkeep`
- `src/ProcessingTools/parcel_workflow.pyt`
- `src/ProcessingTools/providers/ai_extraction_provider.py`
- `src/ProcessingTools/providers/extraction_provider.py`
- `src/ProcessingTools/providers/local_extraction_provider.py`
- `src/ProcessingTools/providers/ocr_extraction_provider.py`
- `src/ProcessingTools/reporting/html_report.py`
- `src/ProcessingTools/reporting/json_report.py`
- `src/ProcessingTools/reporting/pdf_report.py`
- `src/ProcessingTools/rules/rules.yaml`
- `src/ProcessingTools/tests/__init__.py`
- `src/ProcessingTools/tests/test_adapter_placeholders.py`
- `src/ProcessingTools/tests/test_contract_writer.py`
- `src/ProcessingTools/utils/hashes.py`
- `src/ProcessingTools/utils/logging_redaction.py`
- `src/ProcessingTools/utils/path_utils.py`
- `tools/package_addin.ps1`
- `tools/run_python_tests.ps1`
- `tools/validate_contracts.ps1`
- `_bmad-output/implementation-artifacts/1-1-set-up-arcgis-pro-add-in-and-processing-scaffold.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### Change Log

- 2026-06-09: Implemented Story 1.1 scaffold and moved story to review.
- 2026-06-09: Resolved code review findings and moved story to done.
