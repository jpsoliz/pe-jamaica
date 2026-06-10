---
baseline_commit: 5d3813e
---

# Story 2.6: Validate ArcGIS Pro and Python Processing Environment

Status: done

## Story

As a cadastral technical staff user,
I want preflight to verify the ArcGIS Pro and Python processing environment,
so that extraction does not fail later because of missing runtime dependencies.

## Acceptance Criteria

1. Given a valid loaded Case Folder is in an intake/preflight state, when the user runs preflight, then the add-in runs the existing manifest/source checks and also runs ArcGIS Pro and Python processing environment checks.
2. Given environment checks run, then the add-in verifies ArcGIS Pro compatibility, ArcPy availability, configured Python executable/environment path, required Python packages, Case Folder workspace access, source/working/output directory access, and write access.
3. Given the v1 settings include `arcgis_python_executable`, then the configured path may reference `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe` and the check must report whether that executable exists and can be invoked.
4. Given an environment dependency is missing or incompatible, then the result is reported as a blocker or warning according to configured severity rules, with a clear correction message and no secrets.
5. Given environment checks are long-running or invoke subprocesses, then the UI does not freeze ArcGIS Pro; the dock pane shows a coarse running status and updates when checks complete.
6. Given preflight completes, then all environment results are written to the existing Case Folder `working\preflight_summary.json` using lowercase snake_case fields and are visible in the dock pane blocker/warning/passed groups.
7. Given Story 2.6 is complete, then preflight still performs verification only; it must not run extraction, create editable review geometry, create result GDB/GeoJSON/output artifacts, perform DWG readiness inspection beyond generic environment/file-access checks, or call live CADINDEX/Enterprise services.

## Tasks / Subtasks

- [x] Add environment check contracts and settings. (AC: 1, 2, 3, 4, 6)
  - [x] Add add-in-owned environment check models under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/`, such as `ProcessingEnvironmentCheckResult` and `ProcessingEnvironmentCheckSeverity`.
  - [x] Extend or wrap `PreflightCheck` so checks can use categories beyond `manifest`, for example `environment`, `arcgis_pro`, `python`, `workspace`, and `write_access`.
  - [x] Use lowercase snake_case `check_id`, `category`, `severity`, `status`, `message`, `affected_path`, and `correction` values in `preflight_summary.json`.
  - [x] Read `arcgis_python_executable` from `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`.
  - [x] Add settings for minimum ArcGIS Pro lane/version, required Python packages, and severity mapping if needed; keep defaults compatible with ArcGIS Pro 3.6 and `net8.0-windows`.
- [x] Add injectable environment probe service. (AC: 1, 2, 3, 4, 5, 7)
  - [x] Add an interface such as `IProcessingEnvironmentProbe` or `IProcessingEnvironmentPreflightService`.
  - [x] Implement a production probe that can check file existence, process invocation, package import checks, workspace/read/write access, and ArcGIS Pro runtime/version when available.
  - [x] Keep ArcGIS Pro SDK calls isolated behind a provider or adapter so unit tests can run outside ArcGIS Pro.
  - [x] Keep Python/ArcPy checks behind a process runner abstraction so tests do not require the real `arcgispro-survey-ai` environment.
  - [x] Treat `arcpy` as a required dependency for extraction readiness; report missing ArcPy as a blocker unless configuration explicitly marks it warning-only.
  - [x] Do not execute extraction, import DWG content, create geodatabases, or write output artifacts during the probe.
- [x] Integrate environment checks into manifest preflight. (AC: 1, 4, 6, 7)
  - [x] Extend `ManifestPreflightService.Run` to combine existing manifest/source results with environment results.
  - [x] Preserve existing source manifest hash behavior and workflow state transitions.
  - [x] Ensure any blocker from manifest or environment checks sets `payload.status` to `blocked`.
  - [x] Ensure warnings are included in `payload.warnings` and the top-level `warnings` array.
  - [x] Ensure passed checks include successful ArcGIS/Python/workspace/write-access results.
  - [x] Ensure preflight writes only `preflight_summary.json` and workflow state changes; no extraction or downstream artifacts.
- [x] Add non-freezing preflight execution path in the dock pane. (AC: 5, 6)
  - [x] Update `WorkflowSession` and `ParcelWorkflowDockpaneViewModel` so long-running environment checks can run asynchronously or through an injected async preflight runner.
  - [x] Keep UI state consistent with `preflight_running`, `preflight_blocked`, and `preflight_passed`.
  - [x] Show coarse status such as "Preflight running: environment checks" rather than fake percentages.
  - [x] Prevent duplicate preflight runs while a run is active.
  - [x] Preserve reopen behavior: reopening a Case Folder reads environment results from `preflight_summary.json` and displays blockers/warnings/passed checks.
- [x] Add workspace and write-access checks. (AC: 2, 4, 6)
  - [x] Verify Case Folder root exists and is readable.
  - [x] Verify `source`, `working`, and `output` directories can be read or created as appropriate.
  - [x] Verify `working\preflight_summary.json` can be written without corrupting prior valid data on failure.
  - [x] Verify temporary write/delete checks stay inside the Case Folder.
  - [x] Return sanitized blocker/warning messages for unauthorized access, locked directories, missing paths, invalid paths, and write failures.
- [x] Add Python executable/package/ArcPy checks. (AC: 2, 3, 4, 5, 6)
  - [x] Verify the configured Python executable path is present and executable.
  - [x] Invoke a bounded command such as `python -c` through an injectable runner to report Python version.
  - [x] Verify required package imports, including `arcpy`, through a bounded import command or adapter probe.
  - [x] Enforce a timeout and convert timeout/failure output to sanitized preflight blockers or warnings.
  - [x] Do not record full environment variables, command lines containing secrets, raw stack traces, or raw subprocess output if it may contain secrets.
- [x] Add ArcGIS Pro compatibility checks. (AC: 2, 4, 6)
  - [x] Verify the configured/active ArcGIS Pro lane is compatible with Story 2.6 expectations: ArcGIS Pro 3.6 lane, ArcGIS Pro SDK 3.6, Visual Studio 2022, .NET 8.
  - [x] In production, use safe ArcGIS Pro SDK/environment APIs where available; in tests, use fake providers.
  - [x] Report incompatible or unknown ArcGIS Pro version as blocker or warning based on severity rules.
  - [x] Do not require live ArcGIS Enterprise/CADINDEX access for this check.
- [x] Add focused tests. (AC: 1-7)
  - [x] Test all existing manifest/source preflight tests still pass.
  - [x] Test successful environment probe contributes passed ArcGIS/Python/workspace/write-access checks and keeps preflight passed when manifest checks pass.
  - [x] Test missing configured Python executable creates a blocker with the configured path in `affected_path`.
  - [x] Test configured path `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe` is accepted as a valid setting value when fake probe reports it available.
  - [x] Test missing ArcPy/package import is reported as blocker or warning according to severity mapping.
  - [x] Test workspace/write failure is reported as a blocker and does not write outside the Case Folder.
  - [x] Test subprocess timeout is sanitized and does not leak raw output, tokens, passwords, or environment variables.
  - [x] Test async/running state prevents duplicate preflight command execution.
  - [x] Test reopening a Case Folder with environment checks reloads blockers/warnings/passed checks.
  - [x] Test preflight does not create extraction, review, validation, output, result GDB, GeoJSON, CADINDEX, or Enterprise artifacts.
- [x] Validate and package. (AC: 1-7)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.
  - [ ] Manual ArcGIS Pro 3.6 smoke test: load a mock transaction, start/claim it, run preflight, confirm environment checks appear in blockers/warnings/passed groups, and confirm ArcGIS Pro remains responsive.

### Review Findings

- [x] [Review][Patch] ArcGIS Pro 3.7 runtime is reported as incompatible even though the architecture targets 3.6/3.7 [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs:101]
- [x] [Review][Patch] Processing settings load can throw IO/security exceptions before preflight can report a blocker or warning [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs:34]

## Dev Notes

### Current State From Prior Stories

- Story 2.1 implemented manifest/source preflight checks in `ManifestPreflightService`.
- Story 2.4 made transaction-loaded Case Folders the normal start point and writes Innola transaction/attachment provenance to `manifest.json`.
- Story 2.5 added transaction lifecycle gates, `innola_lifecycle`, and `working\workflow_lifecycle_audit.json`; it is done after review fixes.
- Current `ManifestPreflightService.Run` reads `manifest.json`, validates detected profile/source files, writes `working\preflight_summary.json`, and returns blockers/warnings/passed checks.
- Current `PreflightCheck.Blocker` and `PreflightCheck.Passed` hardcode category `manifest`; Story 2.6 should generalize this without breaking old tests.
- Current `WorkflowSession.RunManifestPreflight` is synchronous and sets status to `PreflightRunning`, then `PreflightBlocked` or `PreflightPassed`.
- Current `ParcelWorkflowDockpaneViewModel.RunPreflight` calls `workflowSession.RunManifestPreflight(Environment.UserName)` synchronously.
- `Settings/WorkflowSettings.json` already includes:

```json
{
  "arcgis_pro_sdk_lane": "3.6",
  "target_framework": "net8.0-windows",
  "arcgis_python_executable": "C:\\JPFiles\\Dropbox\\Sidwell\\Development\\AI-Survey\\python-envs\\arcgispro-survey-ai\\python.exe"
}
```

### Scope Boundaries

- Do not implement Story 2.7 DWG readiness inspection here. It is acceptable to verify that files/workspace are readable, but do not use ArcPy to inspect DWG layers or content in this story.
- Do not implement extraction, validation, output generation, review geometry, result GDB, GeoJSON, reports, CADINDEX sync, or ArcGIS Enterprise writeback.
- Do not make live Innola lifecycle endpoint assumptions in this story.
- Do not require the actual `C:\JPFiles\...arcgispro-survey-ai` environment in automated tests; use fake probes/process runners.

### Recommended Design

Prefer extending preflight with a small set of injectable services:

```text
Preflight/
  IProcessingEnvironmentPreflightService.cs
  ProcessingEnvironmentPreflightService.cs
  IProcessRunner.cs
  IArcGisProEnvironmentProvider.cs
  ProcessingEnvironmentSettings.cs
```

The production service can perform real checks when available. Tests should inject fake providers so the behavior is deterministic and can run in the current test harness.

Suggested checks:

```text
arcgis_pro_version_compatible
python_executable_configured
python_executable_exists
python_executable_invokable
python_version_supported
python_package_arcpy_available
python_package_<name>_available
case_folder_readable
source_directory_readable
working_directory_writable
output_directory_writable
```

Use category values such as `environment`, `arcgis_pro`, `python`, `workspace`, or `write_access` rather than `manifest` for every new check.

### Severity Guidance

Suggested blocker checks:

- Python executable path missing when extraction requires Python.
- Python executable file does not exist.
- Python cannot be invoked within timeout.
- ArcPy import unavailable.
- Case Folder root/source/working/output access fails.
- `working\preflight_summary.json` cannot be written.
- ArcGIS Pro major/minor version is known incompatible with configured lane.

Suggested warning checks:

- ArcGIS Pro version cannot be detected outside ArcGIS Pro test runtime.
- Optional packages are missing.
- Python version is detectable but not the preferred version, if still compatible.

### Preflight Summary Contract Guidance

Current contract:

```json
{
  "schema_version": "1.0.0",
  "transaction_id": "TR100000004",
  "run_id": "preflight-...",
  "created_at": "2026-06-10T...",
  "created_by": "user",
  "source_manifest_hash": "sha256:...",
  "payload": {
    "status": "blocked",
    "blockers": [],
    "warnings": [],
    "passed_checks": []
  },
  "warnings": [],
  "errors": []
}
```

Keep this structure. Add environment results as additional `PreflightCheck` entries unless a truly new payload structure is necessary. If a payload extension is added, it must be backward-compatible and use lowercase snake_case.

### Security Requirements

- Never write passwords, tokens, signed URLs, raw request bodies, raw response bodies, full stack traces, full environment variable dumps, or raw subprocess output to `preflight_summary.json`, logs, reports, status text, or tests.
- Subprocess command failures must be converted to sanitized messages and categories such as `python_missing`, `python_timeout`, `package_missing`, `arcpy_missing`, `workspace_unreadable`, or `write_access_denied`.
- Keep all temporary write probes inside the Case Folder and clean them up best-effort.

### Testing Notes

- Keep C# tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`.
- Register new tests in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`.
- Existing test count after Story 2.5 review fixes was 101 passing tests.
- Avoid live network, live Innola, live ArcGIS Enterprise, live CADINDEX, and mandatory live ArcPy in automated tests.
- Build/package commands may require elevated execution in this environment because the .NET/ArcGIS SDK accesses `C:\Users\js91482\AppData\Local\Microsoft SDKs`.

### References

- `_bmad-output/planning-artifacts/epics.md`: Story 2.6 acceptance criteria.
- `_bmad-output/planning-artifacts/architecture.md`: Preflight verification-only rules, C# / Python boundary, processing adapter boundary, ArcGIS Pro lane guidance, and no live Enterprise/CADINDEX dependency.
- `_bmad-output/implementation-artifacts/2-1-run-manifest-preflight.md`: original manifest preflight implementation context.
- `_bmad-output/implementation-artifacts/2-5-control-active-transaction-lifecycle-and-completion-gate.md`: current Innola/lifecycle gating and latest validation state.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`: current preflight implementation to extend.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`: current check result contract to generalize.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs`: current preflight summary contract.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`: current synchronous preflight command, workflow state transitions, reopen behavior.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`: current Run Preflight command binding.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`: current ArcGIS Pro lane and Python executable setting.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `tools\validate_contracts.ps1` - passed.
- `tools\run_python_tests.ps1` - passed, 2 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 110 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed after rerunning separately from package generation.
- `tools\package_addin.ps1` - passed, produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.
- Review patch validation: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` - passed, 112 tests.
- Review patch validation: `tools\validate_contracts.ps1` - passed.
- Review patch validation: `tools\run_python_tests.ps1` - passed, 2 tests.
- Review patch validation: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` - passed.
- Review patch validation: `tools\package_addin.ps1` - passed, produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX`.

### Completion Notes List

- Added a category-aware preflight check model while keeping existing manifest helpers backward-compatible.
- Added processing environment settings, ArcGIS Pro runtime provider, process runner, and injectable environment preflight service.
- Integrated environment checks into manifest preflight through `RunAsync`, with production checks used by the workflow session and no-op injection available for manifest-only tests.
- Added async preflight execution from the dock pane/session, coarse running status, and duplicate-run prevention.
- Added workspace/read/write, ArcGIS Pro lane/framework, Python executable, ArcPy, package, timeout, and sanitized failure checks.
- Added focused tests for environment checks, summary persistence, reopen display, duplicate-run prevention, and no downstream artifact creation.
- Manual ArcGIS Pro 3.6 smoke was not run from this environment; the package is built and ready for the user-side smoke test.
- Review fixes applied: ArcGIS Pro 3.7 is accepted as compatible with the configured 3.6 lane, while 3.5 remains blocked.
- Review fixes applied: processing settings load now falls back safely on JSON, IO, unauthorized access, path, and security exceptions.

### File List

- `_bmad-output/implementation-artifacts/2-6-validate-arcgis-pro-and-python-processing-environment.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightResult.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/IProcessingEnvironmentPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/NoOpProcessingEnvironmentPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/IProcessRunner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessRunner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/IArcGisProEnvironmentProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ArcGisProEnvironmentProvider.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ProcessingEnvironmentPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-10 | 0.1 | Initial story context for ArcGIS Pro and Python processing environment validation during preflight. | Mary |
| 2026-06-10 | 1.0 | Implemented environment preflight checks, async workflow execution, and focused validation coverage. | Amelia |
| 2026-06-10 | 1.1 | Applied code review fixes for ArcGIS Pro 3.7 compatibility and resilient settings loading. | Amelia |

