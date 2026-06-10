---
baseline_commit: NO_VCS
---

# Story 1.3: Add and Copy Source Files to the Case Folder

Status: done

## Story

As a cadastral technical staff user,
I want to add source files for the transaction and have them copied into the Case Folder,
so that the processing run is based on stable, auditable source inputs.

## Acceptance Criteria

1. Given a transaction Case Folder exists in `intake` state, when the user adds PDF, DWG, TXT, CSV, TIF, PNG, or JPG source files through the dock pane, then the add-in copies the selected files into the Case Folder source area.
2. The manifest records original path, copied path, file type, file size, timestamp, and source role where known.
3. Unsupported file extensions are rejected with a clear message.
4. The source file list shows required/optional status and copied-to-case-folder result.
5. No extraction, validation, or output artifact is created by this story.

## Tasks / Subtasks

- [x] Add source file domain model and copy service. (AC: 1, 2, 3)
  - [x] Create `CaseFolders/SourceFileCopyService.cs` or equivalent under the existing `CaseFolders/` boundary.
  - [x] Accept only `.pdf`, `.dwg`, `.txt`, `.csv`, `.tif`, `.tiff`, `.png`, `.jpg`, and `.jpeg`, case-insensitively.
  - [x] Copy accepted files into `CaseFolderLayout.SourceDirectory`.
  - [x] Prevent path traversal and never copy outside the active Case Folder source area.
  - [x] Define deterministic duplicate-name behavior; recommended: preserve the first copy and add a non-destructive suffix for later copies.
  - [x] Return clear per-file results for copied, rejected, duplicate-renamed, and failed files.
- [x] Extend the manifest contract for source file metadata. (AC: 2)
  - [x] Expand `ManifestSourceFile` in `Contracts/ManifestDocument.cs` with snake_case JSON fields for `original_path`, `copied_path`, `file_type`, `file_size`, `copied_at`, and `source_role`.
  - [x] Preserve the existing top-level manifest envelope and `payload.workflow_state == "intake"`.
  - [x] Add read/update behavior through `ManifestSerializer` or a small manifest store; do not rewrite manifest JSON with ad hoc string operations.
  - [x] Update `src/Contracts/schemas/manifest.schema.json` and `src/Contracts/examples/manifest.example.json` to match the new `source_files` item shape.
- [x] Add workflow/session surface for adding source files. (AC: 1, 3, 4)
  - [x] Extend `WorkflowSession` with an intake-only method that copies selected source files and updates status text.
  - [x] Reject source-file add attempts when there is no active case or the workflow state is not `Intake`.
  - [x] Keep the case state in `Intake`; do not introduce detected profile, preflight, extraction, validation, output, or sync states.
  - [x] Expose the copied source file list through a view-model-friendly immutable collection or read-only list.
- [x] Update the dock pane surface for source files. (AC: 1, 3, 4)
  - [x] Extend `ParcelWorkflowDockpaneViewModel` with a source-file add method/command and source file list properties.
  - [x] Update `ParcelWorkflowDockpane.xaml` to show compact source file rows with filename, role/requiredness label, extension, copy result, and copied path or status.
  - [x] Use direct, calm microcopy for rejected files, such as `Unsupported source file type: .docx`.
  - [x] Do not implement open/reveal source file actions; Story 1.6 owns those.
- [x] Add focused tests for source copy and manifest update. (AC: 1, 2, 3)
  - [x] Add tests under `ParcelWorkflowAddIn.Tests/CaseFolders/`.
  - [x] Verify accepted extensions are copied into `source/` and the copied files preserve byte content.
  - [x] Verify unsupported extensions are rejected and are not copied.
  - [x] Verify duplicate filenames do not overwrite existing copied files.
  - [x] Verify manifest `payload.source_files` records original path, copied path, file type, file size, copied timestamp, and nullable/known source role.
  - [x] Verify invalid or missing active case state returns a clear failure result.
- [x] Update validation tooling. (AC: 1-5)
  - [x] Extend `tools/validate_contracts.ps1` only for stable source files, schema/example consistency, or required C# files.
  - [x] Ensure `tools/validate_contracts.ps1`, `tools/run_python_tests.ps1`, `dotnet restore`, `dotnet build`, and relevant C# tests pass.

### Review Findings

- [x] [Review][Patch] Dock pane has no user-facing add-source control or bound command, so files cannot be added through the dock pane [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml:55]
- [x] [Review][Patch] Workflow source list drops rejected files, so unsupported-file messages are not visible in the dock pane list [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:65]

## Dev Notes

### Previous Story Intelligence

- Story 1.2 is done and created the canonical Case Folder foundation:
  - `CaseFolderLayout` owns `manifest.json`, `source/`, `working/`, `output/`, `output/reports/`, and `output/logs`.
  - `CaseFolderStore` creates the Case Folder and returns `CaseFolderCreationResult` instead of throwing for expected user/path failures.
  - `ManifestDocument`, `ManifestPayload`, `ManifestSourceFile`, and `ManifestSerializer` own manifest shape and JSON serialization.
  - `WorkflowSession` owns workflow state/status and currently transitions from `NoCase` to `Intake` after case creation.
  - `ParcelWorkflowDockpaneViewModel` derives from ArcGIS `DockPane` and wraps `WorkflowSession`.
- Story 1.2 review patches matter for this story:
  - Do not rely on unmerged XAML resources.
  - Keep `manifest.schema.json` strict enough to catch missing payload fields.
  - Convert expected filesystem/path failures into clear failed results.
  - User-facing status must change when validation fails.
- Current C# tests use a no-package console runner in `ParcelWorkflowAddIn.Tests`; follow this pattern unless the project intentionally introduces a test framework later.

### Current Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
  - Current state: centralizes root, manifest, source, working, output, reports, and logs paths.
  - This story should use `SourceDirectory`; avoid hardcoding `source` elsewhere.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
  - Current state: creates an initial Case Folder only.
  - Prefer a separate source-copy service over expanding creation logic into a large mixed-responsibility class.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
  - Current state: `ManifestSourceFile` is an empty record placeholder.
  - This story must make it the durable source-file metadata contract.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs`
  - Current state: writes the manifest.
  - This story likely needs read/round-trip/update support so source files append without losing envelope fields.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Current state: stores current state, transaction ID, case folder path, and status text.
  - This story should add source-copy orchestration while preserving `Intake`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: exposes transaction/output/current state/status and `CreateCase()`.
  - Add source-file list and add-source entrypoint without breaking existing case creation behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: compact transaction header.
  - Add a compact intake source-file section; no open/reveal buttons yet.
- `src/Contracts/schemas/manifest.schema.json`
  - Current state: requires top-level fields and `payload.workflow_state` / `payload.source_files`.
  - Add item-level schema for source file metadata while preserving empty `source_files: []` compatibility.
- `src/Contracts/examples/manifest.example.json`
  - Current state: initial empty intake manifest.
  - Either keep it as an empty-source example plus schema, or add a representative source-file example if validation tooling expects one.

### Architecture Requirements

- Use ArcGIS Pro SDK add-in architecture with C#/.NET WPF/MVVM for dock pane UI, workflow orchestration, command gating, and Case Folder orchestration.
- C# owns file selection and source copy; Python is not involved in this story.
- The transaction Case Folder is the system of record. Copied source files and manifest metadata must be sufficient for later reopen/resume and downstream processing.
- Use file-based JSON contracts. Manifest updates must remain compatible with Python processing adapters that will consume the Case Folder later.
- Preserve audit intent: record original source path and copied path, but do not write credentials or unrelated local environment details to the manifest.

### Source File Contract

Supported extensions for this story:

```text
.pdf, .dwg, .txt, .csv, .tif, .tiff, .png, .jpg, .jpeg
```

Recommended `payload.source_files[]` shape:

```json
{
  "original_path": "C:\\incoming\\plan.pdf",
  "copied_path": "D:\\cases\\TR-SMD-0000001\\source\\plan.pdf",
  "file_type": ".pdf",
  "file_size": 12345,
  "copied_at": "2026-06-09T00:00:00Z",
  "source_role": null
}
```

`source_role` may be `null` or a known role only when the UI/service knows it. Do not infer Scenario A/B or required roles in this story; Story 1.4 owns production input profile detection.

### UX Requirements

- The dock pane remains the primary workflow container.
- Source file rows should show required/optional status and copied-to-case-folder result, but role detection may be unknown until Story 1.4.
- Microcopy should be direct, technical, and calm:
  - Use: `Copied to Case Folder source area.`
  - Use: `Unsupported source file type: .docx.`
  - Avoid generic messages like `Something went wrong.`
- Keep the UI compact and ArcGIS Pro-adjacent. Do not create a landing page, map preview, or decorative shell.

### Testing Guidance

- Use temporary directories and temporary source files; do not copy real source files into repo fixtures for unit tests.
- Test byte-level preservation for copied files.
- Test extension handling case-insensitively, including `.tiff` and `.jpeg` aliases.
- Test duplicate-name behavior explicitly so source evidence cannot be overwritten silently.
- Test manifest round-trip after source file updates.
- Test workflow gating: adding source files without an active intake case must fail cleanly and update status.
- After implementation, run:
  - `tools/validate_contracts.ps1`
  - `tools/run_python_tests.ps1`
  - `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
  - `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Scope Boundaries

- Do not create a new Case Folder in this story; Story 1.2 owns creation.
- Do not detect Scenario A/B, incomplete intake, unsupported intake, or required role completeness; Story 1.4 owns that.
- Do not run preflight, extraction, validation, or output generation.
- Do not create `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, GDB, GeoJSON, reports, or processing logs.
- Do not implement open/reveal source file actions; Story 1.6 owns that.
- Do not call ArcPy or Python adapters.
- Do not add live CADINDEX/Enterprise operations.

### References

- [epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Epic 1 goal and Story 1.3 acceptance criteria.
- [architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): Case Folder as system of record, C# file selection/source copy ownership, JSON contract boundary, and workflow data flow.
- [prd.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md): FR2/FR3 accepted source extensions, FR4 copied source folder and resumability/audit requirements.
- [EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): intake surface, source file row behavior, and direct technical microcopy.
- [1-2-create-transaction-case-folder.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/1-2-create-transaction-case-folder.md): completed Case Folder, manifest, workflow, test, and review-fix patterns.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because `SourceFileCopyService`, `WorkflowSession.AddSourceFiles`, and source-file session properties did not exist yet.
- Green phase: C# test runner passed 12 tests after source-copy, manifest, workflow, and UI updates.
- `tools/validate_contracts.ps1` passed.
- `tools/run_python_tests.ps1` passed: 2 Python tests.
- `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.

### Completion Notes List

- Added source-file copy service and result models under `CaseFolders/`.
- Implemented supported extension validation, source-area copy, non-destructive duplicate filename suffixing, and per-file result messages.
- Expanded manifest source-file metadata contract and added manifest read/update support.
- Updated manifest schema/example to define `payload.source_files[]` metadata.
- Extended `WorkflowSession` with intake-only source file add behavior and source list exposure while preserving `Intake` state.
- Extended dock pane view model and XAML with compact source-file list display and calm status/microcopy.
- Added C# tests for accepted copies, unsupported extensions, duplicate filenames, manifest metadata, and workflow gating.
- Extended `tools/validate_contracts.ps1` for Story 1.3 source-copy files and manifest source metadata fields.

### File List

- `_bmad-output/implementation-artifacts/1-3-add-and-copy-source-files-to-the-case-folder.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Contracts/examples/manifest.example.json`
- `src/Contracts/schemas/manifest.schema.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/SourceFileCopyServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyBatchResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RelayCommand.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `tools/validate_contracts.ps1`

### Change Log

- 2026-06-09: Story 1.3 created with comprehensive context for source file add/copy implementation.
- 2026-06-09: Implemented Story 1.3 source file copy, manifest metadata, workflow/UI surface, tests, and validation updates; moved story to review.
- 2026-06-09: Applied code review patches for dock pane add-source command and rejected source-file row visibility; moved story to done.
