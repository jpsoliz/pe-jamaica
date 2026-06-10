---
baseline_commit: NO_VCS
---

# Story 1.4: Detect Source Input Profile

Status: done

## Story

As a cadastral technical staff user,
I want the add-in to detect the transaction input profile from the files provided,
so that production users are not forced to choose test Case 1-4 labels manually.

## Acceptance Criteria

1. Given one or more source files have been copied into the Case Folder, when the user refreshes or confirms intake, then the add-in detects whether the inputs match Scenario A, Scenario B, incomplete intake, or unsupported intake.
2. The dock pane displays a production-facing detected profile label instead of Case 1-4 fixture names.
3. Missing required file roles are shown as intake issues.
4. The detected profile is written to `manifest.json`.
5. Case 1-4 labels remain reserved for fixture/test metadata only.

## Tasks / Subtasks

- [x] Add source input profile domain model and detector. (AC: 1, 2, 3, 5)
  - [x] Create `Intake/SourceInputProfile.cs` or equivalent with profile codes for `scenario_a`, `scenario_b`, `incomplete_intake`, and `unsupported_intake`.
  - [x] Create `Intake/SourceInputProfileDetector.cs` or equivalent that accepts copied `ManifestSourceFile` metadata and returns profile, display label, missing roles, and issues.
  - [x] Detect Scenario A when intake has computation evidence in PDF/TIF/TIFF/PNG/JPG/JPEG format plus a plan/map reference in PDF/TIF/TIFF/PNG/JPG/JPEG format.
  - [x] Detect Scenario B when intake has a points/computation source in PDF/TXT/CSV format, a DWG reference, and a plan/map reference in PDF/TIF/TIFF/PNG/JPG/JPEG format.
  - [x] Return `incomplete_intake` when supported files are present but required roles are missing or ambiguous.
  - [x] Return `unsupported_intake` when the copied source set cannot map to either production scenario.
  - [x] Do not expose or return Case 1, Case 2, Case 3, or Case 4 as production profile labels.
- [x] Add role classification support for copied source files. (AC: 1, 3)
  - [x] Use `source_role` when already present in `manifest.json`.
  - [x] Add deterministic role inference for common filenames and extensions where safe, e.g. DWG -> `dwg_reference`, TXT/CSV -> `points_computation`, filename hints like `plan`/`map` -> `plan_map_reference`, and `computation`/`coord`/`points` -> `computation_source`.
  - [x] Leave ambiguous PDF/image files as ambiguous instead of guessing silently.
  - [x] Surface ambiguous/missing roles as intake issues, not as preflight results.
- [x] Extend manifest payload for detected profile. (AC: 4)
  - [x] Add a `detected_profile` payload object with snake_case fields such as `profile_code`, `display_label`, `status`, `detected_at`, `missing_roles`, and `issues`.
  - [x] Preserve existing `workflow_state` and `source_files` payload fields.
  - [x] Update `ManifestDocument`, `ManifestSerializer` usage, `src/Contracts/schemas/manifest.schema.json`, and `src/Contracts/examples/manifest.example.json`.
  - [x] Do not create `preflight_summary.json` or any processing/output artifact.
- [x] Add workflow/session surface for refresh or confirm intake. (AC: 1, 2, 3, 4)
  - [x] Extend `WorkflowSession` with an intake-only method such as `RefreshInputProfile()` or `ConfirmIntake()`.
  - [x] Read source files from the current manifest/case state, run the detector, persist the detected profile, and update status text.
  - [x] Keep `CurrentState` as `Intake`; later state transitions belong to preflight stories.
  - [x] Expose detected profile label and intake issues through view-model-friendly properties.
- [x] Update the dock pane intake UI. (AC: 2, 3, 5)
  - [x] Extend `ParcelWorkflowDockpaneViewModel` with detected profile label, intake issues, and refresh/confirm intake command.
  - [x] Update `ParcelWorkflowDockpane.xaml` to display the detected production profile label and missing required roles/issues.
  - [x] Use direct microcopy, e.g. `Detected profile: Scenario B - points/computation + DWG + plan/map reference.` and `Missing: plan/map reference.`
  - [x] Do not add Case 1-4 labels, a fixture picker, preflight checklist, DWG readability checks, or extraction controls.
- [x] Add focused tests for profile detection and manifest persistence. (AC: 1-5)
  - [x] Add tests under `ParcelWorkflowAddIn.Tests/Intake/` or an equivalent test folder.
  - [x] Verify Scenario A detection from copied source metadata with computation and plan/map roles.
  - [x] Verify Scenario B detection from points/computation, DWG, and plan/map roles.
  - [x] Verify incomplete intake reports missing required roles.
  - [x] Verify unsupported intake does not claim Scenario A/B.
  - [x] Verify no production-facing result contains `Case 1`, `Case 2`, `Case 3`, or `Case 4`.
  - [x] Verify detected profile is persisted to `manifest.json` without removing existing `source_files`.
- [x] Update validation tooling. (AC: 1-5)
  - [x] Extend `tools/validate_contracts.ps1` only for stable Story 1.4 files and manifest profile contract fields.
  - [x] Ensure `tools/validate_contracts.ps1`, `tools/run_python_tests.ps1`, `dotnet restore`, `dotnet build`, and relevant C# tests pass.

### Review Findings

- [x] [Review][Patch] TXT/CSV role inference requires filename hints even though the story calls TXT/CSV deterministic points/computation inputs [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs:111]
- [x] [Review][Patch] DWG-only intake is classified as unsupported instead of incomplete with missing Scenario B roles [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs:65]

## Dev Notes

### Previous Story Intelligence

- Story 1.3 is done and created the source-file foundation this story must extend:
  - `SourceFileCopyService` copies supported files into `CaseFolderLayout.SourceDirectory`.
  - `ManifestSourceFile` records `original_path`, `copied_path`, `file_type`, `file_size`, `copied_at`, and nullable `source_role`.
  - `ManifestSerializer` can read and write manifests.
  - `WorkflowSession.SourceFiles` now includes copied and rejected file rows for the dock pane list.
  - `RelayCommand` exists for simple WPF command binding.
  - `ParcelWorkflowDockpane.xaml` has an `Add Source Files` button and source-file rows.
- Story 1.3 review patches matter for this story:
  - Every user-facing workflow path must be reachable from the dock pane, not only via service methods.
  - Rejected/issue rows must be visible where ACs require user correction.
- Current C# tests use a no-package console runner in `ParcelWorkflowAddIn.Tests`; continue this pattern unless the project intentionally adds a test framework later.

### Current Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
  - Current state: `ManifestPayload` has `workflow_state` and `source_files`.
  - This story adds `detected_profile`; keep `source_files` backward-compatible.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs`
  - Current state: read/write JSON serializer.
  - Use it for profile persistence; do not use string replacement.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
  - Current state: creates cases, adds source files, exposes status/source rows while staying in `Intake`.
  - Add profile refresh/confirm behavior here or through a small intake service called by it.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - Current state: exposes transaction header, source rows, add-source command.
  - Add detected profile label, intake issue list, and refresh/confirm command.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
  - Current state: transaction header and source file list.
  - Add compact detected profile and issue display; keep the pane utilitarian.
- `src/Contracts/schemas/manifest.schema.json`
  - Current state: enforces manifest envelope, workflow state, source files, and source metadata.
  - Add `detected_profile` schema without invalidating an initial manifest before detection if the implementation needs `detected_profile` to be nullable.
- `src/Contracts/examples/manifest.example.json`
  - Current state: includes a representative source file.
  - Update with a representative detected profile example only if validation tooling expects one.

### Profile Semantics

Production profile codes and labels should be stable and explicit:

```text
scenario_a           Scenario A - computation evidence + plan/map reference
scenario_b           Scenario B - points/computation + DWG + plan/map reference
incomplete_intake    Incomplete intake
unsupported_intake   Unsupported intake
```

Suggested roles:

```text
computation_source
points_computation
dwg_reference
plan_map_reference
ambiguous_document
```

Scenario A requires:

- `computation_source` with `.pdf`, `.tif`, `.tiff`, `.png`, `.jpg`, or `.jpeg`
- `plan_map_reference` with `.pdf`, `.tif`, `.tiff`, `.png`, `.jpg`, or `.jpeg`

Scenario B requires:

- `points_computation` with `.pdf`, `.txt`, or `.csv`
- `dwg_reference` with `.dwg`
- `plan_map_reference` with `.pdf`, `.tif`, `.tiff`, `.png`, `.jpg`, or `.jpeg`

If role inference is uncertain, report missing/ambiguous roles rather than guessing. Story 1.4 may write inferred `source_role` values back to manifest only when deterministic; otherwise preserve `null`.

### Manifest Detected Profile Contract

Recommended payload shape:

```json
{
  "workflow_state": "intake",
  "source_files": [],
  "detected_profile": {
    "profile_code": "scenario_b",
    "display_label": "Scenario B - points/computation + DWG + plan/map reference",
    "status": "matched",
    "detected_at": "2026-06-09T00:00:00Z",
    "missing_roles": [],
    "issues": []
  }
}
```

Use `status` values such as `matched`, `incomplete`, or `unsupported`. Keep fields lowercase snake_case.

### Architecture Requirements

- Use C#/.NET WPF/MVVM for dock pane state, workflow orchestration, command gating, and user-facing status.
- C# owns production input profile detection from copied source metadata.
- Python/ArcPy is not involved in this story. DWG readability belongs to preflight, not input profile detection.
- The Case Folder and manifest remain the system of record.
- Production input profile detection is system-owned metadata; Case 1-4 are fixture/test labels and must not appear in production UI labels.

### UX Requirements

- Display a production-facing profile label, not fixture labels.
- Show missing required roles as intake issues, not preflight failures.
- Microcopy should be direct, technical, and calm:
  - Use: `Detected profile: Scenario B - points/computation + DWG + plan/map reference.`
  - Use: `Missing: plan/map reference.`
  - Use: `Unsupported intake: no recognized source combination.`
  - Avoid: `Case 4 detected.`
- Keep the dock pane compact. Do not add a landing page, map preview, or decorative shell.

### Testing Guidance

- Test the detector as pure business logic with manifest source metadata.
- Test workflow persistence by reading `manifest.json` after refresh/confirm.
- Test that source files remain in the manifest after profile persistence.
- Test no production-facing profile label contains Case 1-4 strings.
- Test incomplete and unsupported intake separately.
- After implementation, run:
  - `tools/validate_contracts.ps1`
  - `tools/run_python_tests.ps1`
  - `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
  - `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
  - `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Scope Boundaries

- Do not copy source files; Story 1.3 owns source copy.
- Do not reopen/resume existing Case Folders; Story 1.5 owns that.
- Do not open/reveal source files; Story 1.6 owns that.
- Do not run preflight, validate DWG readability, inspect ArcPy, or check write access; Epic 2 owns that.
- Do not create `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, GDB, GeoJSON, reports, or processing logs.
- Do not invoke Python or ArcPy.
- Do not add live CADINDEX/Enterprise operations.
- Do not expose Case 1-4 labels outside fixture/test metadata.

### References

- [epics.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/epics.md): Story 1.4 acceptance criteria and Case 1-4 production-label constraint.
- [architecture.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/architecture.md): production input profile detection decision, C# workflow ownership, Case Folder system of record, and JSON contract boundary.
- [prd.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md): FR2/FR3 Scenario A/B source requirements and FR5 preflight boundary.
- [EXPERIENCE.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md): intake surface, technical microcopy, file row behavior, and scenario/profile UI context.
- [1-3-add-and-copy-source-files-to-the-case-folder.md](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/1-3-add-and-copy-source-files-to-the-case-folder.md): completed source copy, manifest source metadata, workflow, UI, and review-fix patterns.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` failed because the `ParcelWorkflowAddIn.Intake` namespace and profile detector APIs did not exist yet.
- Green phase: C# test runner passed 19 tests after intake profile detection, manifest persistence, workflow, and UI updates.
- `tools/validate_contracts.ps1` passed.
- `tools/run_python_tests.ps1` passed: 2 Python tests.
- `dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln` passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed with 0 warnings and 0 errors.

### Completion Notes List

- Added `Intake` domain models for source input profile codes, source roles, detected profile payload, and profile detection.
- Implemented Scenario A, Scenario B, incomplete intake, and unsupported intake detection from copied manifest source metadata.
- Added deterministic role inference for DWG, TXT/CSV, and filename-hinted PDF/image sources while preserving ambiguity when unsafe to infer.
- Extended manifest payload with nullable `detected_profile` and updated manifest schema/example contract.
- Added `WorkflowSession.RefreshInputProfile()` to detect and persist profile metadata without leaving `Intake`.
- Extended dock pane view model and XAML with detected profile label, intake issues, and a `Refresh Intake` command.
- Added C# tests for Scenario A/B, incomplete intake, unsupported intake, fixture-label avoidance, and workflow manifest persistence.
- Extended `tools/validate_contracts.ps1` for Story 1.4 intake files and detected profile schema fields.

### File List

- `_bmad-output/implementation-artifacts/1-4-detect-source-input-profile.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Contracts/examples/manifest.example.json`
- `src/Contracts/schemas/manifest.schema.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Intake/SourceInputProfileDetectorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/DetectedSourceInputProfile.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfile.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `tools/validate_contracts.ps1`

### Change Log

- 2026-06-09: Story 1.4 created with comprehensive context for source input profile detection.
- 2026-06-09: Implemented Story 1.4 source input profile detection and moved story to review.
- 2026-06-09: Applied code review patches for TXT/CSV role inference and DWG-only incomplete intake classification; moved story to done.
