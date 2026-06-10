---
baseline_commit: NO_VCS
---

# Story 2.1: Run Manifest Preflight

Status: done

## Story

As a cadastral technical staff user,
I want to run preflight against the transaction manifest,
so that I know whether the case has the required source files before extraction begins.

## Acceptance Criteria

1. Given a Case Folder exists with copied source files and a detected input profile, when the user starts Preflight, then the add-in validates required source roles, file existence, file extensions, and readable copied paths.
2. Results are grouped as blockers, warnings, and passed checks.
3. The case state becomes `preflight_running` during execution.
4. The case state becomes `preflight_blocked` when blockers exist.
5. No extraction or output artifacts are created.

## Tasks / Subtasks

- [x] Define the manifest preflight contract. (AC: 1, 2)
  - [x] Add C# preflight contract models under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/` or `Preflight/` for summary, checks, severity, status, and issue metadata.
  - [x] Update `src/Contracts/schemas/preflight_summary.schema.json` with required payload fields, including grouped `blockers`, `warnings`, and `passed_checks` or an equivalent explicit grouping.
  - [x] Update `src/Contracts/examples/preflight_summary.example.json` with representative blocked and passed manifest-preflight results.
  - [x] Keep all JSON artifact fields lowercase snake_case.
- [x] Implement manifest-only preflight checks in C#. (AC: 1, 2, 5)
  - [x] Create a service such as `Preflight/ManifestPreflightService.cs` or `CaseFolders/ManifestPreflightService.cs`.
  - [x] Read the existing `manifest.json` through `ManifestSerializer.Read`; do not parse manifest JSON with string operations.
  - [x] Validate copied source file existence, readable/openable copied paths, supported extensions, and role requirements from `manifest.payload.detected_profile`.
  - [x] Treat missing copied files, unreadable copied paths, unsupported extensions, missing detected profile, incomplete intake, and unsupported intake as blockers unless the contract explicitly marks a condition warning-only.
  - [x] Treat valid required files/roles as passed checks.
  - [x] Re-check copied source paths at action time and ensure they remain under `CaseFolderLayout.SourceDirectory`.
  - [x] Do not invoke Python, ArcPy, DWG sublayer inspection, package/environment checks, extraction, validation, output generation, report generation, or live CADINDEX behavior.
- [x] Persist `working/preflight_summary.json`. (AC: 2, 4, 5)
  - [x] Write the preflight summary to `CaseFolderLayout.WorkingDirectory`.
  - [x] Include `schema_version`, `transaction_id`, `run_id`, `created_at`, `created_by`, `source_manifest_hash`, `payload`, `warnings`, and `errors`.
  - [x] Compute or reuse a deterministic manifest hash/provenance value so later stories can detect stale preflight results.
  - [x] Preserve previous Case Folder artifacts and do not create extraction, review, approval, validation, output, report, log, GDB, GeoJSON, or sync artifacts.
- [x] Extend workflow state and command gating. (AC: 3, 4)
  - [x] Extend `WorkflowState` with `PreflightRunning`, `PreflightBlocked`, and `PreflightPassed` only if command gating for those states is implemented.
  - [x] Add contract value/display helpers for new states in `WorkflowStateExtensions`.
  - [x] Add a `WorkflowSession.RunManifestPreflight(...)` method that gates execution to active `Intake` or supported preflight rerun states.
  - [x] Set state to `PreflightRunning` before checks begin and to `PreflightBlocked` when blockers exist.
  - [x] If there are no blockers, set state to `PreflightPassed` only for manifest preflight scope; environment and DWG checks in later stories may still add blockers before extraction is enabled.
  - [x] Persist the manifest workflow state when preflight state changes, or clearly document why state is derived from `preflight_summary.json` for this story.
- [x] Add dock pane preflight UI surface. (AC: 2, 3, 4)
  - [x] Add a `Run Preflight` command to `ParcelWorkflowDockpaneViewModel`.
  - [x] Expose preflight status, blocker rows, warning rows, and passed check rows from `WorkflowSession`.
  - [x] Update `ParcelWorkflowDockpane.xaml` with a compact preflight section using direct microcopy such as `Preflight blocked: missing plan/map reference.`
  - [x] Keep downstream extraction/review/validation/output controls absent or disabled; do not imply extraction can run from Story 2.1 alone.
- [x] Reopen/resume preflight results. (AC: 2, 4)
  - [x] Extend Case Folder reopen/artifact handling so an existing `working/preflight_summary.json` is discovered and can be represented in the dock pane.
  - [x] If a reopened manifest has a preflight state or preflight artifact, report it without enabling unsupported downstream workflow.
  - [x] Preserve existing Story 1.5 behavior for missing/corrupt manifests and missing copied source files.
- [x] Add focused tests and update validation tooling. (AC: 1-5)
  - [x] Add C# tests for successful manifest preflight, missing role blockers, missing copied file blockers, unsupported extension blockers, missing detected profile blockers, path containment blockers, and no extraction/output artifact creation.
  - [x] Add workflow tests for state transitions: `Intake` -> `PreflightRunning` -> `PreflightBlocked` and no-blocker result behavior.
  - [x] Add reopen tests for discovered `preflight_summary.json` without unsupported downstream commands.
  - [x] Extend `tools/validate_contracts.ps1` for stable preflight files and schema fields.
  - [x] Run `tools\validate_contracts.ps1`, `tools\run_python_tests.ps1`, `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`, `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`, and `tools\package_addin.ps1`.

### Review Findings

- [x] [Review][Patch] `source_manifest_hash` is stale immediately after every preflight run [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:243] — `RunManifestPreflight` persists `preflight_running`, then `ManifestPreflightService.Run` hashes the manifest, then the workflow persists `preflight_blocked` or `preflight_passed`. The stored `source_manifest_hash` therefore points to the transient running-state manifest, not the current manifest after the run. Later stale-preflight checks will flag every freshly generated preflight summary as stale.
- [x] [Review][Patch] Intake edits after preflight do not invalidate preflight state or results [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:115] — `AddSourceFiles` and `RefreshInputProfile` are allowed from `PreflightBlocked` and `PreflightPassed`, but they leave `CurrentState`, `manifest.payload.workflow_state`, `working/preflight_summary.json`, and exposed preflight rows intact after changing source files/profile metadata. A user can modify intake and still see stale preflight readiness.
- [x] [Review][Patch] Corrupt or unreadable active manifest can throw through Run Preflight [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:243] — `RunManifestPreflight` calls `SetWorkflowState` and `ManifestPreflightService.Run`, both of which read/hash/write manifest files without converting expected IO/JSON/path failures into blocker results. A damaged active `manifest.json` can break the dock pane command instead of producing a non-blocking preflight blocker.

## Dev Notes

### Previous Epic Intelligence

- Epic 1 is done and established the intake foundation this story must reuse:
  - `CaseFolderLayout` owns `manifest.json`, `source/`, `working/`, `output/`, `output/reports`, and `output/logs`.
  - `ManifestDocument`, `ManifestPayload`, `ManifestSourceFile`, and `ManifestSerializer` own manifest shape and IO.
  - `WorkflowSession` owns case creation, source copy, input profile refresh, reopen, source actions, status text, and command gating.
  - `SourceInputProfileDetector` writes production-facing `detected_profile` metadata to `manifest.json`.
  - `CaseFolderStore.ReopenCaseFolder(...)` discovers canonical artifacts, including `working/preflight_summary.json` when present.
  - `SourceFileActionService` already enforces copied-path existence and source-folder containment. Reuse that containment pattern for preflight checks.
- Epic 1 retrospective carry-forward:
  - Every user-facing acceptance criterion needs a dock pane path, not only a service method.
  - Case Folder paths must be treated as untrusted at action/preflight time.
  - Workflow state expansion must remain centralized in workflow/session logic, not scattered through the ViewModel.
  - `working/source_action_audit.json` is bounded intake audit; do not turn it into the full Epic 6 audit system in this story.

### Recent Packaging Correction

- The add-in now packages through Esri's desktop target when built with Visual Studio MSBuild.
- `tools\package_addin.ps1` produces and registers:
  `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`
- `dotnet build` remains useful for normal compile checks; `tools\package_addin.ps1` is the packaging/manual ArcGIS Pro smoke-test path.

### Current Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
  - Current state: `NoCase`, `Intake`.
  - This story adds preflight state names only with proper session gating and display/contract values.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
  - Current state: maps existing states to display/contract strings.
  - Add `preflight_running`, `preflight_blocked`, and `preflight_passed` mappings if states are added.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Current state: central session owner for case creation, source files, profile detection, reopen, artifacts, source actions, and status.
  - Add preflight execution and exposed result collections here; do not make the ViewModel decide preflight gates independently.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
  - Current state: owns `WorkingDirectory`, but does not expose a `PreflightSummaryPath`.
  - Add a property for `working/preflight_summary.json` if that reduces duplicated path construction.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
  - Current state: creates/reopens Case Folders and discovers canonical artifacts.
  - Extend artifact discovery or rehydration carefully without weakening existing recoverability behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
  - Current state: manifest payload has `workflow_state`, `source_files`, and nullable `detected_profile`.
  - Preflight must consume this contract and may update `workflow_state` through `ManifestSerializer`.
- `src/Contracts/schemas/preflight_summary.schema.json`
  - Current state: permissive placeholder with `payload` as an object.
  - Tighten this for Story 2.1 manifest-preflight output.
- `src/Contracts/examples/preflight_summary.example.json`
  - Current state: `status: not_run`, empty checks.
  - Update to match grouped blocker/warning/passed check contract.
- `src/ProcessingTools/adapters/preflight_adapter.py`
  - Current state: placeholder raising `NotImplementedError`.
  - Do not implement Python/ArcPy behavior in Story 2.1; Story 2.2+ own environment and DWG processing checks.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: exposes intake/reopen/source-action commands.
  - Add Run Preflight command and preflight collections, but keep it a thin wrapper over `WorkflowSession`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: compact transaction/source/profile/artifact UI.
  - Add compact preflight display; avoid creating a marketing-style page or a separate navigation shell unless needed.

### Manifest Preflight Semantics

Manifest preflight is the first Epic 2 layer. It checks only durable Case Folder and manifest readiness:

1. `manifest.json` exists and is readable through `ManifestSerializer`.
2. `manifest.payload.detected_profile` exists and is not `unsupported_intake`.
3. Required roles for the detected profile are present:
   - `scenario_a`: `computation_source` and `plan_map_reference`.
   - `scenario_b`: `points_computation`, `dwg_reference`, and `plan_map_reference`.
4. Each required source has a copied path.
5. Each copied path exists, is readable, has a supported extension, and remains under `CaseFolderLayout.SourceDirectory`.
6. The result is grouped into blockers, warnings, and passed checks.

Suggested check shape:

```json
{
  "check_id": "required_role_plan_map_reference",
  "category": "manifest",
  "severity": "blocker",
  "status": "blocked",
  "message": "Missing plan/map reference.",
  "affected_path": null,
  "source_role": "plan_map_reference",
  "correction": "Add a copied plan/map source file and refresh intake."
}
```

Use stable status/severity values such as:

```text
severity: blocker, warning, passed
status: blocked, warning, passed
```

If the implementation prefers one `checks[]` array plus grouping properties, that is acceptable only if the dock pane can explicitly present blockers, warnings, and passed checks.

### Architecture Requirements

- Preflight verifies readiness only; it must not create editable review geometry or final output artifacts.
- C# owns workflow state, command gating, Case Folder orchestration, user-facing progress/status, and manifest-level checks.
- Python/ArcPy owns later preflight probes that require runtime dependencies, DWG inspection, workspace checks, and package checks.
- C# and Python communicate through versioned file-based JSON contracts.
- The Case Folder is the system of record; hidden ArcGIS Pro project state must not be required for recovery or audit.
- Every command must check allowed workflow states before execution.
- JSON artifact filenames and fields must use lowercase snake_case.

### UX Requirements

- Add a visible `Run Preflight` path in the dock pane.
- Group results as blockers, warnings, and passed checks.
- Use direct, calm, technical microcopy:
  - `Preflight blocked: missing plan/map reference.`
  - `Preflight blocked: copied source file is missing.`
  - `Passed: required Scenario B source roles are present.`
- Do not rely on color alone for severity. Include severity/status text in each row.
- Downstream extraction should remain unavailable until the complete preflight chain supports it.

### Testing Guidance

- Continue the lightweight C# console test runner pattern in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`.
- Use temporary Case Folders and real `manifest.json` files for preflight filesystem behavior.
- Test both contract output and workflow state:
  - valid Scenario A manifest passes manifest checks
  - valid Scenario B manifest passes manifest checks
  - missing detected profile blocks
  - incomplete or unsupported intake blocks
  - missing required source role blocks
  - missing copied source file blocks
  - copied path outside `source/` blocks
  - unsupported extension in manifest blocks
  - corrupt manifest is handled without ViewModel-boundary throws
  - no extraction, review, approval, validation, output, report, log, GDB, GeoJSON, or sync artifacts are created
- After implementation, run:
  - `tools\validate_contracts.ps1`
  - `tools\run_python_tests.ps1`
  - `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
  - `tools\package_addin.ps1`

### Scope Boundaries

- Do not implement ArcGIS Pro/Python environment checks; Story 2.2 owns them.
- Do not inspect DWG readability or CAD sublayers; Story 2.3 owns that.
- Do not configure processing or credential profiles; Story 2.4 owns that.
- Do not build the full preflight checklist/gate extraction experience beyond manifest-preflight rows; Story 2.5 owns broader display/gating.
- Do not invoke Python or ArcPy.
- Do not create `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, `process.log`, `extracted_geometry.geojson`, result GDBs, reports, or sync artifacts.
- Do not perform live CADINDEX or ArcGIS Enterprise operations.

### References

- [_bmad-output/planning-artifacts/epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Epic 2 goal and Story 2.1 acceptance criteria.
- [_bmad-output/planning-artifacts/architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): Case Folder as system of record, preflight verification-only rule, workflow state machine, JSON artifact contracts, C# / Python boundary, and preflight structure mapping.
- [_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md): FR5 required-input preflight and blocker/non-blocker behavior.
- [_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): preflight grouping, blocker/warning UX, direct microcopy, and disabled downstream steps.
- [_bmad-output/implementation-artifacts/epic-1-retro-2026-06-09.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/epic-1-retro-2026-06-09.md): Epic 1 carry-forward risks and Epic 2 preparation notes.
- [_bmad-output/implementation-artifacts/1-6-open-or-reveal-source-files-from-intake.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/1-6-open-or-reveal-source-files-from-intake.md): latest source path containment, non-blocking behavior, audit, workflow, and packaging context.
- [docs/toolchain.md](D:/Code/BMad-Method/dev/pe-jamaica/docs/toolchain.md): ArcGIS Pro SDK lane and add-in packaging command.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because the `ParcelWorkflowAddIn.Preflight` namespace and manifest preflight APIs did not exist.
- Green phase: C# test runner passed 52 tests after preflight contracts, service, workflow state, UI wiring, and tests were implemented.
- `tools\validate_contracts.ps1` passed.
- `tools\run_python_tests.ps1` passed: 2 Python tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.
- `tools\package_addin.ps1` passed and produced `ParcelWorkflowAddIn.esriAddInX`.
- Review patch validation: C# test runner passed 56 tests after adding regression coverage for stable preflight hashes, intake invalidation after preflight, and corrupt manifest blocker handling.
- Review patch validation: `tools\validate_contracts.ps1`, `tools\run_python_tests.ps1`, `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`, and `tools\package_addin.ps1` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added a manifest-only preflight service that validates detected profile presence, required source roles, copied source existence/readability, supported extensions, and source folder containment.
- Added versioned preflight summary C# models plus JSON schema/example updates with explicit blocker, warning, and passed-check groups.
- Added workflow states for preflight running/blocked/passed, persisted manifest workflow state changes, and exposed preflight groups through `WorkflowSession`.
- Added a compact dock pane `Run Preflight` command and grouped result surface.
- Added reopen support for preflight summary rehydration while preserving existing recoverability behavior and avoiding downstream command enablement.
- Added C# tests for manifest preflight success, blocker scenarios, workflow transitions, reopen behavior, and no downstream artifact creation.
- Applied code review patches so preflight hashes ignore workflow state transitions, intake edits clear stale preflight artifacts/results and return the manifest to `intake`, and corrupt active manifests produce a non-crashing `manifest_readable` blocker summary.

### File List

- `_bmad-output/implementation-artifacts/2-1-run-manifest-preflight.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Contracts/examples/preflight_summary.example.json`
- `src/Contracts/schemas/preflight_summary.schema.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummarySerializer.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `tools/validate_contracts.ps1`

### Change Log

- 2026-06-09: Story 2.1 created and marked ready-for-dev.
- 2026-06-09: Implemented Story 2.1 manifest preflight contract, service, workflow/UI surface, tests, validation updates, and moved story to review.
- 2026-06-09: Applied code review patches for stable preflight hash, intake invalidation, and corrupt manifest handling; moved story to done.
