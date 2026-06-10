---
baseline_commit: NO_VCS
---

# Story 1.5: Reopen and Resume Existing Case Folder

Status: done

## Story

As a cadastral technical staff user,
I want to reopen an existing transaction Case Folder,
so that I can resume work after ArcGIS Pro closes or a processing step fails.

## Acceptance Criteria

1. Given a Case Folder contains a valid `manifest.json` and workflow state artifacts, when the user opens that Case Folder from the dock pane, then the add-in loads the transaction ID, source file list, detected profile, current state, and available artifacts.
2. The dock pane resumes at the latest successful workflow state.
3. Missing or damaged required state files are reported as recoverability issues.
4. The user is not required to reselect copied source files when the Case Folder is valid.

## Tasks / Subtasks

- [x] Add a Case Folder reopen result and recoverability issue model. (AC: 1, 3)
  - [x] Create a small result type such as `CaseFolderReopenResult` with success flag, `CaseFolderLayout`, `ManifestDocument`, resolved workflow state, source file rows, available artifact paths, and recoverability issues.
  - [x] Create a recoverability issue model with fields or properties for code, severity/status, message, affected path, and whether reopening can continue.
  - [x] Use direct user-facing messages such as `Manifest could not be read.` and `Copied source file is missing from the Case Folder.`
- [x] Implement Case Folder reopen validation in `CaseFolders/`. (AC: 1, 3)
  - [x] Add `CaseFolderStore.ReopenCaseFolder(string caseFolderPath)` or a dedicated `CaseFolderReopenService`.
  - [x] Validate that the selected folder exists and contains a readable `manifest.json`.
  - [x] Read the manifest through `ManifestSerializer.Read`; do not parse JSON with ad hoc string handling.
  - [x] Verify required v1 Case Folder directories: `source`, `working`, `output`, `output/reports`, and `output/logs`.
  - [x] Verify each manifest `source_files[].copied_path` still exists; report missing copied files without silently dropping them from the UI list.
  - [x] Report corrupt JSON, unreadable manifest, missing manifest, missing source directory, and malformed manifest payload as recoverability issues.
  - [x] Do not copy, move, repair, or redetect files as part of reopen unless a user later invokes an explicit command.
- [x] Rehydrate workflow/session state from the Case Folder. (AC: 1, 2, 4)
  - [x] Extend `WorkflowSession` with `ReopenCaseFolder(string caseFolderPath)` or equivalent.
  - [x] Set `TransactionId`, `CaseFolderPath`, `CurrentState`, `StatusText`, `SourceFiles`, `DetectedProfileLabel`, and `IntakeIssues` from the reopen result.
  - [x] Preserve source-file rows from `manifest.payload.source_files` so the user does not need to reselect copied source files.
  - [x] Load `manifest.payload.detected_profile` when present; if it is missing, show a non-blocking intake issue and keep `Refresh Intake` available.
  - [x] Keep the current implementation's supported resume state as `Intake`; map manifest `workflow_state: "intake"` to `WorkflowState.Intake`.
  - [x] For known future workflow-state strings not implemented yet, reopen the case with a recoverability issue or clear unsupported-state status rather than crashing.
- [x] Detect available artifacts without implementing later processing steps. (AC: 1, 2)
  - [x] Add a minimal artifact discovery list for canonical Case Folder artifact paths that already exist, such as `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, `process.log`, and `extracted_geometry.geojson`.
  - [x] Treat these as available artifact references for reopen context only.
  - [x] Do not transition into preflight, extraction, review, validation, output, Manual Process, or sync states until those stories implement the state and gating behavior.
- [x] Add dock pane reopen command and recovery surface. (AC: 1, 3, 4)
  - [x] Add a `Reopen Case` command to `ParcelWorkflowDockpaneViewModel`.
  - [x] Use a folder-picker approach that fits the existing WPF/ArcGIS Pro add-in project without adding new package dependencies.
  - [x] Show the reopened transaction ID, current step, source file list, detected profile, and recoverability issues in the existing compact dock pane.
  - [x] Keep New Case and Add Source behavior intact after a reopen.
  - [x] Do not implement source open/reveal actions; Story 1.6 owns that.
- [x] Add focused tests for reopen and recoverability. (AC: 1-4)
  - [x] Add C# tests under `ParcelWorkflowAddIn.Tests/CaseFolders/` and `ParcelWorkflowAddIn.Tests/Workflow/`.
  - [x] Verify a valid Case Folder with manifest source files and detected profile reopens to `Intake`.
  - [x] Verify transaction ID, source file list, copied paths, detected profile label, and current state are loaded from disk.
  - [x] Verify missing `manifest.json` returns a recoverability issue and does not create a new case.
  - [x] Verify corrupt `manifest.json` returns a recoverability issue instead of throwing through the view model/session boundary.
  - [x] Verify missing copied source files are reported while preserving manifest source rows for user correction.
  - [x] Verify an unsupported or unknown workflow state is reported clearly.
  - [x] Verify reopening does not create `preflight_summary.json`, extraction, validation, output, report, log, GDB, GeoJSON, or sync artifacts.
- [x] Update validation tooling and story record. (AC: 1-4)
  - [x] Extend `tools/validate_contracts.ps1` only if stable new Story 1.5 files or contract checks need to be enforced.
  - [x] Run the focused C# test runner and existing contract/Python checks listed below.
  - [x] Update this story's Dev Agent Record, File List, Change Log, and final status during development.

### Review Findings

- [x] [Review][Patch] Creating a new case after reopen retained prior case source/profile/artifact state [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:60] — fixed by clearing session source rows, intake issues, available artifacts, and detected profile label on successful new case creation.

## Dev Notes

### Previous Story Intelligence

- Story 1.4 is done and added the intake profile foundation that reopen must rehydrate:
  - `ManifestPayload` now includes `workflow_state`, `source_files`, and nullable `detected_profile`.
  - `ManifestSerializer.Read` and `ManifestSerializer.Write` are the supported manifest IO boundary.
  - `DetectedSourceInputProfile` contains `profile_code`, `display_label`, `status`, `detected_at`, `missing_roles`, and `issues`.
  - `WorkflowSession.RefreshInputProfile()` persists `detected_profile` and keeps the workflow in `Intake`.
  - `ParcelWorkflowDockpaneViewModel` exposes `DetectedProfileLabel`, `IntakeIssues`, `SourceFiles`, and `RefreshInputProfileCommand`.
- The current workflow state enum only supports `NoCase` and `Intake`; Story 1.5 should not invent later step behavior ahead of Epic 2-6.
- The C# tests use the lightweight console runner in `ParcelWorkflowAddIn.Tests`; continue that pattern unless the project intentionally changes test framework later.

### Current Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
  - Current state: creates new Case Folders and initializes `manifest.json`.
  - Add reopen validation here or delegate to a small service under `CaseFolders/`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
  - Current state: centralizes Case Folder paths for manifest, source, working, output, reports, and logs.
  - Use this instead of hardcoding artifact paths throughout the app.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
  - Current state: manifest records transaction identity, source files, workflow state, and detected profile.
  - Reopen should consume this contract without requiring hidden ArcGIS Pro project state.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Current state: creates cases, copies source files, refreshes intake, and exposes session state to the dock pane.
  - Add the reopen method and rehydration behavior here.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
  - Current state: `NoCase`, `Intake`.
  - Add parsing helpers as needed, but do not add later states unless the implementation can gate them correctly.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: exposes new case, add source files, refresh intake, header/status/source/profile properties.
  - Add `Reopen Case` command and recoverability issue projection.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: compact transaction header, source file list, detected profile, and intake issue display.
  - Add a compact reopen action and recovery issue display without turning the pane into a landing page.

### Reopen Semantics

Story 1.5 should make the Case Folder the only required durable state source:

1. `manifest.json` must be present and readable.
2. `manifest.transaction_id` becomes the session transaction ID.
3. `manifest.payload.workflow_state` becomes the resume state when supported.
4. `manifest.payload.source_files` becomes the UI source-file list.
5. `manifest.payload.detected_profile` becomes the displayed production input profile when present.
6. Existing canonical artifacts are discovered and reported as available references, but later-step state transitions remain out of scope until the owning stories exist.

For this story, the latest successful implemented workflow state is `intake`. If a manifest contains future states such as `preflight_passed` or `review_pending`, the implementation should not crash and should not falsely enable future commands. It may reopen to a conservative supported state with a clear recoverability issue, or report that the case uses a workflow state not yet supported by this build.

### Recoverability Rules

- Missing `manifest.json`: fail reopen with a blocking recoverability issue.
- Corrupt or unreadable `manifest.json`: fail reopen with a blocking recoverability issue.
- Missing required Case Folder directories: report recoverability issues. If the manifest is readable, the session may still display transaction metadata while indicating the case needs repair.
- Missing copied source file referenced by manifest: report a recoverability issue and preserve the row in the source list.
- Missing `detected_profile`: reopen the case, display `Detected profile: not refreshed`, and show a non-blocking intake issue that the user can resolve with `Refresh Intake`.
- Unknown `workflow_state`: report a recoverability issue and do not enable downstream commands.
- Reopen must not rewrite the manifest unless a later explicit user action changes intake state.

### Architecture Requirements

- The Case Folder is the system of record; hidden ArcGIS Pro project state must not be required for recovery or audit.
- `CaseFolders/` owns transaction folder layout, source copying, artifact paths, hashes, and reopen/resume behavior.
- C# owns workflow state, command gating, Case Folder orchestration, and user-facing status.
- Workflow state is derived from Case Folder artifacts plus current in-memory operation.
- Every command should check allowed workflow states before execution.

### UX Requirements

- Intake has both New Case and Reopen Case entry points.
- The transaction header shows transaction ID, current step, last status, and future score/status when available.
- Reopen should use compact, direct, technical microcopy.
- Recovery issues should be visible and actionable without being framed as extraction/preflight failures.
- Do not add a map preview, decorative shell, or source-file open/reveal behavior in this story.

### Testing Guidance

- Test reopen service behavior separately from the dock pane where possible.
- Use temporary Case Folder layouts and real `manifest.json` files for filesystem behavior.
- Exercise both success and recovery paths:
  - valid manifest with copied source files and detected profile
  - missing manifest
  - malformed manifest JSON
  - missing source directory
  - manifest source file whose copied path no longer exists
  - missing detected profile
  - unknown workflow state
- After implementation, run:
  - `tools/validate_contracts.ps1`
  - `tools/run_python_tests.ps1`
  - `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
  - `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Scope Boundaries

- Do not create new Case Folders; Story 1.2 owns creation.
- Do not copy source files; Story 1.3 owns source copy.
- Do not redetect input profiles automatically during reopen; Story 1.4 owns explicit profile refresh behavior.
- Do not open or reveal source files; Story 1.6 owns those actions.
- Do not run preflight, validate DWG readability, inspect ArcPy, or check write access; Epic 2 owns those behaviors.
- Do not create or modify `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, GDB, GeoJSON, reports, processing logs, or sync artifacts.
- Do not invoke Python or ArcPy.
- Do not perform live CADINDEX or ArcGIS Enterprise operations.

### References

- [_bmad-output/planning-artifacts/epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Story 1.5 acceptance criteria and Epic 1 recoverable Case Folder goal.
- [_bmad-output/planning-artifacts/architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): Case Folder system of record, workflow-state ownership, `CaseFolders/` boundary, and recovery/audit architecture.
- [_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md): FR4 and NFR-2 recoverability requirements.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): intake New Case/Reopen Case entry point, transaction header, and calm technical microcopy.
- [_bmad-output/implementation-artifacts/1-4-detect-source-input-profile.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/1-4-detect-source-input-profile.md): completed detected-profile manifest, session, UI, and testing patterns.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Add Case Folder reopen result models for recoverability issues and discovered artifacts.
- Keep reopen validation in `CaseFolderStore` so Case Folder layout, manifest IO, source-file checks, and artifact discovery remain inside the `CaseFolders/` boundary.
- Rehydrate `WorkflowSession` from the manifest and reopen result, with `Intake` as the only supported active resume state in this build.
- Add dock pane command and compact recovery/artifact display without implementing Story 1.6 open/reveal behavior.
- Add red-first C# tests for valid reopen, missing/corrupt manifest, missing copied sources, missing detected profile, unknown workflow state, and no processing artifacts during reopen.

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because `CaseFolderStore.ReopenCaseFolder` and `WorkflowSession.ReopenCaseFolder` did not exist.
- Green phase: C# test runner passed 30 tests after reopen models, Case Folder validation, workflow rehydration, UI command, and tests were implemented.
- `tools\validate_contracts.ps1` passed.
- `tools\run_python_tests.ps1` passed: 2 Python tests.
- `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.
- Code review patch verification: C# test runner passed 31 tests after adding `WorkflowSessionClearsReopenedCaseStateWhenCreatingNewCase`.
- Code review patch verification: `tools\validate_contracts.ps1`, `tools\run_python_tests.ps1`, and `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed.

### Completion Notes List

- Added `CaseFolderReopenResult`, `RecoverabilityIssue`, and `AvailableArtifact` models.
- Implemented `CaseFolderStore.ReopenCaseFolder()` with readable manifest validation, required directory checks, copied source-file existence checks, available artifact discovery, and unsupported workflow-state reporting.
- Added `WorkflowSession.ReopenCaseFolder()` to restore transaction ID, Case Folder path, source rows, detected profile, recoverability issues, available artifacts, and intake state from disk.
- Added `Reopen Case` dock pane command using the WPF folder picker and compact artifact/recovery display.
- Preserved the Story 1.5 boundary: reopen does not copy files, auto-refresh profile detection, run preflight, invoke Python, create processing artifacts, open/reveal sources, or enable future workflow states.
- Added focused C# tests for valid and recoverability reopen paths.
- Resolved code review finding: successful new case creation now resets any source/profile/recovery/artifact state from a previously reopened case.

### File List

- `_bmad-output/implementation-artifacts/1-5-reopen-and-resume-existing-case-folder.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/AvailableArtifact.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderReopenResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/RecoverabilityIssue.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `tools/validate_contracts.ps1`

### Change Log

- 2026-06-09: Story 1.5 created with Case Folder reopen/resume, recoverability, artifact discovery, UI, and test guidance.
- 2026-06-09: Implemented Story 1.5 Case Folder reopen/resume behavior and moved story to review.
- 2026-06-09: Applied code review patch for stale reopened case state during new case creation; moved story to done.
