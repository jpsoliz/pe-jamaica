---
baseline_commit: NO_VCS
---

# Story 1.6: Open or Reveal Source Files from Intake

Status: done

## Story

As a cadastral technical staff user,
I want to view or reveal copied source files from the dock pane,
so that I can inspect computation files, maps, plans, and DWG references without leaving the transaction context.

## Acceptance Criteria

1. Given source files have been copied into the Case Folder, when the user selects a source file action, then the add-in can open the file with the system/default viewer or reveal it in the file location.
2. Compatible GIS/CAD references can be routed to ArcGIS Pro map/layer workflows where supported.
3. Long paths show full-path tooltips when truncated.
4. Failed open/reveal actions show a clear non-blocking message.
5. The action is recorded in the transaction audit trail when audit identity is available.

## Tasks / Subtasks

- [x] Add source-file action domain models and launcher boundary. (AC: 1, 2, 4)
  - [x] Create a small model such as `SourceFileActionResult` with `success`, `action`, `path`, `status`, and `message`.
  - [x] Create action names such as `open`, `reveal`, and `add_to_map` or `route_to_map`.
  - [x] Add an injectable launcher abstraction, e.g. `ISourceFileLauncher`, so tests do not start real external processes.
  - [x] Implement a Windows launcher using `System.Diagnostics.ProcessStartInfo`.
  - [x] For default open, use `UseShellExecute = true` so Windows uses the registered default application for documents.
  - [x] For reveal, use an Explorer selection command only after validating the copied path exists.
  - [x] Capture expected exceptions and return non-blocking failure results; do not throw through the ViewModel boundary for user action failures.
- [x] Implement source-file action service under the correct boundary. (AC: 1, 2, 4)
  - [x] Add `CaseFolders/SourceFileActionService.cs` or an equivalent service that accepts the current `CaseFolderLayout`, source row metadata, and requested action.
  - [x] Allow actions only for copied source rows with a non-empty `CopiedPath`.
  - [x] Re-check that the copied path exists before open/reveal/map routing.
  - [x] Re-check that the copied path remains inside `CaseFolderLayout.SourceDirectory` to avoid opening arbitrary paths from stale or tampered state.
  - [x] Return `Source file is missing from the Case Folder.` or similarly calm text when a file is absent.
  - [x] Do not modify source files, recopy files, run profile detection, run preflight, or invoke Python.
- [x] Add GIS/CAD routing behavior without overbuilding map integration. (AC: 2)
  - [x] Treat `.dwg`, `.tif`, and `.tiff` as GIS/CAD-route candidates for this story.
  - [x] If an ArcGIS map/layer routing adapter is not yet implemented, return a clear non-blocking status such as `Map routing is not available in this build. Open or reveal the source file instead.`
  - [x] Do not embed a map preview in the dock pane.
  - [x] Do not implement output-layer add-to-map behavior; Epic 5 owns generated output map integration.
  - [x] Keep the service shape ready for a future ArcGIS adapter while preserving testability without ArcGIS runtime execution.
- [x] Add bounded intake action audit recording. (AC: 5)
  - [x] Add a lightweight audit writer, e.g. `CaseFolders/SourceFileActionAuditService.cs`, that appends action events to a Case Folder artifact such as `working/source_action_audit.json`.
  - [x] Use lowercase snake_case JSON fields.
  - [x] Include `schema_version`, `transaction_id`, `recorded_at`, `operator_id`, `action`, `source_file_name`, `copied_path`, `status`, and `message`.
  - [x] Record audit events only when an operator identity is available, e.g. `Environment.UserName` from the dock pane/session path.
  - [x] Record both success and failure action attempts when identity is available.
  - [x] Do not write secrets, credentials, or external service details.
  - [x] Do not write or append `process.log`; process logging belongs to later processing/reporting stories.
- [x] Extend workflow/session behavior for source-file actions. (AC: 1, 2, 4, 5)
  - [x] Add methods such as `OpenSourceFile(SourceFileCopyResult row, string? operatorId)`, `RevealSourceFile(...)`, and `RouteSourceFileToMap(...)`, or one generic method with a strongly typed action enum.
  - [x] Gate source-file actions to an active case with `CurrentState == WorkflowState.Intake` for this story.
  - [x] Keep action failures non-blocking by updating `StatusText` and/or intake issue text without changing workflow state.
  - [x] Preserve source rows and detected profile after actions.
  - [x] Ensure reopened cases from Story 1.5 can use the same actions for manifest-restored source rows.
- [x] Extend dock pane row UI with source actions and full-path tooltips. (AC: 1, 3, 4)
  - [x] Add row-level `Open` and `Reveal` commands for copied source files in `ParcelWorkflowDockpaneViewModel`.
  - [x] Add a map-route action only where the service says the row is GIS/CAD-route eligible, or expose it disabled/unavailable with clear status.
  - [x] Update `ParcelWorkflowDockpane.xaml` so truncated file names, messages, and copied paths expose full path tooltips.
  - [x] Keep controls keyboard reachable and preserve visible focus behavior through standard WPF buttons.
  - [x] Keep the pane compact; do not create a landing page or map preview.
- [x] Add focused tests for action services, workflow, and ViewModel-safe behavior. (AC: 1-5)
  - [x] Add C# tests under `ParcelWorkflowAddIn.Tests/CaseFolders/` for open/reveal service behavior using a fake launcher.
  - [x] Verify open uses the copied Case Folder path and succeeds when the file exists.
  - [x] Verify reveal uses the parent folder / selection target and succeeds when the file exists.
  - [x] Verify missing copied files return a non-blocking failure result.
  - [x] Verify tampered copied paths outside the Case Folder source directory are rejected.
  - [x] Verify `.dwg`, `.tif`, and `.tiff` are map-route candidates and unsupported map routing is reported clearly when no adapter exists.
  - [x] Verify source actions work after `WorkflowSession.ReopenCaseFolder(...)`.
  - [x] Verify successful and failed actions write `working/source_action_audit.json` only when operator identity is available.
  - [x] Verify no preflight, extraction, validation, output, report, GDB, GeoJSON, or `process.log` artifact is created.
- [x] Update validation tooling and story record. (AC: 1-5)
  - [x] Extend `tools/validate_contracts.ps1` only for stable Story 1.6 source-action files and audit artifact contract expectations.
  - [x] Run the focused C# test runner and existing contract/Python checks listed below.
  - [x] Update this story's Dev Agent Record, File List, Change Log, and final status during development.

### Review Findings

- [x] [Review][Patch] Corrupt or unwritable source action audit file could throw after open/reveal and break non-blocking action behavior [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionAuditService.cs:37] — fixed by making bounded audit append best-effort for expected IO/JSON/path failures while preserving the source action result.

## Dev Notes

### Previous Story Intelligence

- Story 1.5 is done and created the reopen/resume foundation this story must preserve:
  - `CaseFolderStore.ReopenCaseFolder(...)` rehydrates manifest source rows into `SourceFileCopyResult` rows.
  - `WorkflowSession.ReopenCaseFolder(...)` restores `TransactionId`, `CaseFolderPath`, `CurrentState`, `SourceFiles`, `DetectedProfileLabel`, `IntakeIssues`, and `AvailableArtifacts`.
  - Reopen review found and fixed stale state when creating a new case after reopening; do not reintroduce state leakage.
  - Story 1.5 intentionally did not implement source open/reveal behavior; this story owns it.
- Existing tests use the lightweight no-package console runner in `ParcelWorkflowAddIn.Tests`; continue that pattern.
- The workspace is not a git repository, so story frontmatter remains `baseline_commit: NO_VCS`.

### Current Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyResult.cs`
  - Current state: row model with `OriginalPath`, nullable `CopiedPath`, `FileName`, `FileType`, nullable `FileSize`, nullable `SourceRole`, `Status`, `Message`, and `Copied`.
  - This story should either extend this row carefully or add a separate action/result model; preserve existing copy/reopen tests.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
  - Current state: central path owner for `manifest.json`, `source`, `working`, `output`, `output/reports`, and `output/logs`.
  - Use `SourceDirectory` for path containment checks and `WorkingDirectory` for the bounded source-action audit artifact.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Current state: creates cases, copies source files, refreshes input profile, reopens cases, and exposes source rows/status to the dock pane.
  - Add source action methods here so ViewModel commands do not independently decide workflow permission or filesystem behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: exposes source rows, Add Source Files, Refresh Intake, and Reopen Case commands.
  - Add row action commands or command helpers while keeping ViewModel thin.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: displays source row file name, file type, message, and role.
  - Add compact row action buttons and full-path tooltips for truncated content.
- `tools/validate_contracts.ps1`
  - Current state: validates stable scaffold files, source/profile/reopen contract files, JSON examples, ArcGIS assembly presence, and configured ArcGIS Python executable.
  - Add Story 1.6 file checks only after the implementation creates stable source-action files.

### Source Action Semantics

Use the copied Case Folder file as the source of truth:

1. Only act on `SourceFileCopyResult.Copied == true`.
2. `CopiedPath` must be present.
3. The copied file must exist at action time.
4. The copied path must resolve under `CaseFolderLayout.SourceDirectory`.
5. Open uses the Windows registered default viewer.
6. Reveal opens Windows Explorer at the copied file location, ideally selecting the file.
7. GIS/CAD map routing is a candidate path for `.dwg`, `.tif`, and `.tiff`; if no ArcGIS adapter exists yet, return an unavailable status instead of pretending success.

Recommended statuses:

```text
opened
revealed
map_route_unavailable
missing
blocked
failed
```

### Audit Artifact Guidance

The story needs enough audit behavior to satisfy AC 5 without implementing the full Epic 6 audit system. Use a bounded intake artifact such as:

```text
working/source_action_audit.json
```

Suggested payload shape:

```json
{
  "schema_version": "1.0.0",
  "transaction_id": "TR-SMD-0000001",
  "events": [
    {
      "recorded_at": "2026-06-09T00:00:00Z",
      "operator_id": "js91482",
      "action": "open",
      "source_file_name": "plan.pdf",
      "copied_path": "D:\\cases\\TR-SMD-0000001\\source\\plan.pdf",
      "status": "opened",
      "message": "Opened source file."
    }
  ]
}
```

Keep field names lowercase snake_case. Append events by reading and writing the JSON artifact through a serializer model, not string concatenation. This artifact is intake audit state, not a processing log.

### Architecture Requirements

- Use C#/.NET WPF/MVVM for dock pane UI, workflow orchestration, command gating, Case Folder orchestration, and ArcGIS Pro integration.
- `CaseFolders/` owns artifact paths, source copy, and source-file filesystem behavior.
- `Workflow/` owns state checks and command gating.
- The active ArcGIS Pro map is a companion surface; do not embed a map preview in the dock pane.
- Every new UI command must declare allowed workflow states.
- Source file open/view actions for non-GIS files use the Windows default file viewer.
- ArcGIS Pro map/layer add is allowed only where supported and must remain separable behind an adapter or service boundary.
- The Case Folder remains the system of record; actions must not depend on hidden ArcGIS Pro project state.

### UX Requirements

- Source file rows should provide row actions without making users search the folder manually.
- Use compact WPF-style controls with predictable tab order.
- Tooltips should expose full paths for truncated file/path text.
- Failed actions should show a clear non-blocking message.
- Use direct, calm microcopy:
  - `Opened source file.`
  - `Revealed source file in folder.`
  - `Source file is missing from the Case Folder.`
  - `Map routing is not available in this build.`
- Do not add explanatory in-app text about how the feature works.

### Latest Technical Notes

- Microsoft Learn documents that `ProcessStartInfo.UseShellExecute = true` lets the operating system shell open registered document types with their default associated application. Use that pattern for source file open actions rather than hardcoding PDF/image/CAD viewers. Reference: <https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-diagnostics-processstartinfo-useshellexecute>
- Microsoft Learn also shows `Process.Start(ProcessStartInfo)` supports starting document/application resources via `ProcessStartInfo`; keep action launching behind an injectable boundary so unit tests never start real processes. Reference: <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-10.0>

### Testing Guidance

- Use fake launchers/adapters for open, reveal, and map route tests.
- Use real temporary Case Folder layouts for path containment and missing-file behavior.
- Add tests in the current console runner and register them in `ParcelWorkflowAddIn.Tests/Program.cs`.
- Include both service-level tests and workflow-level tests.
- Include a reopened-case test to prove Story 1.5 rows can be acted on without reselecting files.
- After implementation, run:
  - `tools/validate_contracts.ps1`
  - `tools/run_python_tests.ps1`
  - `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
  - `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Scope Boundaries

- Do not create or reopen Case Folders; Stories 1.2 and 1.5 own those behaviors.
- Do not copy source files; Story 1.3 owns source copy.
- Do not detect or refresh source input profile; Story 1.4 owns profile detection.
- Do not run preflight, DWG readability checks, ArcPy validation, extraction, review, validation, output generation, or sync readiness.
- Do not create `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, GDB, GeoJSON, reports, or `process.log`.
- Do not invoke Python or ArcPy.
- Do not perform live CADINDEX or ArcGIS Enterprise operations.
- Do not implement full Epic 6 audit trail; only record bounded source action events needed for this story.

### References

- [_bmad-output/planning-artifacts/epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Story 1.6 acceptance criteria and Epic 1 source-view/open goal.
- [_bmad-output/planning-artifacts/architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): source file view/open actions, Windows default viewer boundary, ArcGIS map companion surface, command gating, and Case Folder ownership.
- [_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md): source file intake, auditability, and ArcGIS Pro add-in-first requirements.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): source row actions, full-path tooltips, keyboard operation, evidence/open behavior, and map as companion surface.
- [_bmad-output/implementation-artifacts/1-5-reopen-and-resume-existing-case-folder.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/1-5-reopen-and-resume-existing-case-folder.md): completed reopen/session patterns, review finding, tests, and source-row rehydration behavior.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Add source-file action primitives under `CaseFolders/` so open/reveal/map-route logic stays close to source path ownership.
- Use an injectable `ISourceFileLauncher` for tests and a Windows implementation based on `ProcessStartInfo`.
- Gate source actions through `WorkflowSession` so the ViewModel does not own workflow permission or path validation.
- Add a bounded `working/source_action_audit.json` artifact for source action attempts when operator identity is available.
- Add compact source row buttons and full-path tooltips in the dock pane.

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because `ISourceFileLauncher`, `SourceFileActionService`, action models, audit service, and workflow source-action APIs did not exist.
- Green phase: C# test runner passed 40 tests after source action services, workflow wiring, UI commands, audit writer, and tests were implemented.
- `tools\validate_contracts.ps1` passed.
- `tools\run_python_tests.ps1` passed: 2 Python tests.
- `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.
- Code review patch verification: C# test runner passed 41 tests after adding `WorkflowSessionSourceActionSucceedsWhenExistingAuditIsCorrupt`.
- Code review patch verification: `tools\validate_contracts.ps1`, `tools\run_python_tests.ps1`, and `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed.

### Completion Notes List

- Added source action models, `ISourceFileLauncher`, and `WindowsSourceFileLauncher` for default viewer and Explorer reveal behavior.
- Added `SourceFileActionService` with copied-row checks, source folder containment validation, missing-file handling, and map-route unavailable behavior for `.dwg`, `.tif`, and `.tiff`.
- Added `SourceFileActionAuditService` and JSON audit document models for bounded intake action events in `working/source_action_audit.json`.
- Added `WorkflowSession.ExecuteSourceFileAction(...)` to gate actions to active `Intake` cases, update non-blocking status, and record audit events.
- Extended dock pane ViewModel and XAML with row-level `Open`, `Reveal`, and `Map` commands plus full-path tooltips.
- Added service and workflow tests for open, reveal, missing files, tampered paths, map-route candidates, audit identity behavior, reopened cases, and no processing artifacts.
- Resolved code review finding: source action audit append is now best-effort so corrupt or unwritable audit state cannot turn an open/reveal action into a UI-breaking exception.

### File List

- `_bmad-output/implementation-artifacts/1-6-open-or-reveal-source-files-from-intake.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/ISourceFileLauncher.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileAction.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionAuditDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionAuditService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/WindowsSourceFileLauncher.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RelayCommand.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/SourceFileActionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `tools/validate_contracts.ps1`

### Change Log

- 2026-06-09: Story 1.6 created with source open/reveal/map-route, tooltip, non-blocking failure, and bounded audit guidance.
- 2026-06-09: Implemented Story 1.6 source open/reveal/map-route actions and moved story to review.
- 2026-06-09: Applied code review patch for best-effort source action audit append; moved story to done.
