---
baseline_commit: handoff-2026-06-11
---

# Story 2.8: Validate DWG Readiness When Present

Status: ready-for-dev

## Story

As a cadastral technical staff user,
I want preflight to inspect DWG references when they are part of the transaction,
so that CAD-derived context and annotation expectations are known before extraction.

## Acceptance Criteria

1. Given the detected profile includes a DWG source file, when preflight reaches CAD validation, then the processing adapter checks whether the DWG is readable by the available ArcGIS/ArcPy tooling.
2. Given a readable DWG source is present, when CAD validation runs, then available CAD sublayers are listed where possible.
3. Given a DWG is unreadable, missing, inaccessible, or malformed, when CAD validation runs, then the condition is reported as a blocker or warning according to profile rules.
4. Given a Scenario A transaction has no DWG source role, when preflight runs, then absent DWG files do not block the transaction.
5. Given DWG readiness completes, when `working/preflight_summary.json` is written, then the DWG readiness result is included using lowercase snake_case fields and the existing preflight summary shape.
6. Given Story 2.8 is complete, then preflight remains verification-only: it must not run extraction, create editable review geometry, create result GDB/GeoJSON/output artifacts, perform CADINDEX/Enterprise writes, or mutate source files.

## Tasks / Subtasks

- [ ] Add a DWG readiness preflight boundary. (AC: 1, 2, 3, 5, 6)
  - [ ] Add an injectable service under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/`, for example `IDwgReadinessPreflightService`.
  - [ ] Implement a production service that can invoke bounded ArcGIS/ArcPy/CAD inspection logic without creating geometry/output artifacts.
  - [ ] Add a no-op/fake implementation for tests and non-DWG scenarios.
  - [ ] Keep the service result expressed as `PreflightCheck` collections so it merges into the existing summary contract.
- [ ] Integrate DWG readiness into `ManifestPreflightService`. (AC: 1-5)
  - [ ] Locate manifest source files with `source_role == "dwg_reference"`.
  - [ ] Run DWG readiness only when the detected profile/source roles require or include a DWG.
  - [ ] Preserve Scenario A behavior: no DWG expected, no blocker.
  - [ ] Preserve existing manifest/source/environment checks and source manifest hash behavior.
- [ ] Record CAD/DWG results in preflight summary. (AC: 2, 3, 5)
  - [ ] Use categories such as `cad` or `dwg`.
  - [ ] Suggested check IDs: `dwg_source_present`, `dwg_source_readable`, `dwg_sublayers_inspected`, `dwg_sublayer_<name>_available`.
  - [ ] Include `affected_path` and `source_role` where relevant.
  - [ ] Include user-facing correction guidance for unreadable/locked/malformed DWG.
- [ ] Add safe ArcPy/process execution for inspection. (AC: 1, 2, 3, 6)
  - [ ] Reuse `IProcessRunner` where practical instead of introducing a second subprocess abstraction.
  - [ ] Bound execution time and sanitize all failure messages.
  - [ ] Do not write raw ArcPy output, environment variables, command lines with secrets, stack traces, tokens, or credentials.
  - [ ] Do not import CAD features into a GDB during preflight.
- [ ] Add focused tests. (AC: 1-6)
  - [ ] Scenario A with no DWG still passes when other checks pass.
  - [ ] Scenario B with readable DWG adds passed DWG checks and optional sublayer metadata.
  - [ ] Scenario B with unreadable DWG produces the configured blocker/warning.
  - [ ] Missing `dwg_reference` role in Scenario B remains blocked by existing required-role behavior.
  - [ ] DWG inspection timeout/failure is sanitized and does not leak raw output.
  - [ ] DWG readiness does not create extraction/review/validation/output artifacts.
  - [ ] Reopen behavior displays persisted DWG blockers/warnings/passed checks from `preflight_summary.json`.
- [ ] Validate and package. (AC: 1-6)
  - [ ] Run `tools\validate_contracts.ps1`.
  - [ ] Run `tools\run_python_tests.ps1`.
  - [ ] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [ ] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [ ] Run `tools\package_addin.ps1`.
  - [ ] Manual ArcGIS Pro 3.6 smoke test: load a mock/live Scenario B-style transaction with DWG, run preflight, confirm DWG readiness appears and no downstream artifacts are created.

## Dev Notes

### Current State From Prior Stories

- Story 2.1 implemented manifest/source preflight checks through `ManifestPreflightService`.
- Story 2.4 made Innola-loaded Case Folders and attachments the normal production intake path.
- Story 2.6 added environment checks through `IProcessingEnvironmentPreflightService`, `ProcessingEnvironmentPreflightService`, `IProcessRunner`, and `PreflightCheck` categories beyond `manifest`.
- Story 2.8A completed live Innola auth/task/detail/attachment/lifecycle wiring while preserving mock mode. It explicitly did not implement DWG readiness.
- Current `ManifestPreflightService.RunAsync`:
  - reads `manifest.json`,
  - validates detected profile and required source roles,
  - validates copied source paths and readability,
  - calls the injected environment preflight service,
  - writes `working\preflight_summary.json`.
- Current Scenario B required roles are `points_computation`, `dwg_reference`, and `plan_map_reference`.
- Current preflight summary already supports categorized checks via `PreflightCheck.BlockerForCategory`, `WarningForCategory`, and `PassedForCategory`.

### Existing Files To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - Extend constructor/default factory to include DWG readiness service.
  - Merge DWG readiness result into blockers/warnings/passed checks before summary write.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`
  - Prefer existing category helpers before changing the contract.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs`
  - Preserve shape unless sublayer details require a backward-compatible extension.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/IProcessRunner.cs`
  - Reuse for bounded external inspection where practical.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessingEnvironmentSettings.cs`
  - Reuse configured ArcGIS Python executable if the DWG inspection runs through Python/ArcPy.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
  - Add Scenario A/B integration tests with fake DWG readiness service.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ProcessingEnvironmentPreflightServiceTests.cs`
  - Follow existing fake process runner and sanitized failure test style.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
  - Register new tests.

### Recommended Design

Use a small result/service pair matching the existing environment preflight pattern:

```text
Preflight/
  IDwgReadinessPreflightService.cs
  DwgReadinessPreflightService.cs
  NoOpDwgReadinessPreflightService.cs
  DwgReadinessPreflightResult.cs
```

Keep the result simple:

```text
DwgReadinessPreflightResult
  Blockers: IReadOnlyList<PreflightCheck>
  Warnings: IReadOnlyList<PreflightCheck>
  PassedChecks: IReadOnlyList<PreflightCheck>
```

If sublayers are discovered, record them as passed checks or as a backward-compatible `details` extension only if necessary. Prefer not changing `PreflightCheck` unless the UI needs structured sublayer lists now.

### DWG Inspection Guidance

- Production inspection can be implemented as a bounded Python/ArcPy probe using the configured ArcGIS Python executable.
- The probe may inspect whether ArcPy can describe/list CAD dataset layers or known CAD sublayers.
- The probe must not import CAD, create a file geodatabase, create feature classes, generate annotations, or alter the Case Folder except for `preflight_summary.json`.
- Treat unavailable ArcPy tooling as already covered by environment preflight. DWG readiness should report the DWG-specific consequence clearly.
- If ArcPy cannot enumerate sublayers but can confirm the file is readable, record readability passed and sublayer enumeration warning.

### Suggested Check IDs

```text
dwg_source_present
dwg_source_readable
dwg_probe_invokable
dwg_sublayers_inspected
dwg_sublayer_annotation_available
dwg_sublayer_point_available
dwg_sublayer_polyline_available
dwg_sublayer_polygon_available
dwg_sublayer_multipatch_available
dwg_sublayers_unavailable
dwg_probe_timeout
dwg_probe_failed
```

Use category `dwg` or `cad`. Use `source_role: "dwg_reference"` for source-specific checks.

### Severity Guidance

- Scenario B missing `dwg_reference`: blocker through existing manifest required-role validation.
- Scenario B copied DWG missing/unreadable: blocker unless product later configures warning-only behavior.
- DWG readable but sublayers cannot be enumerated: warning if extraction can still continue without CAD-derived context; blocker only if profile rules make sublayers mandatory.
- Scenario A absent DWG: no warning and no blocker.
- ArcPy unavailable: environment preflight blocker already covers extraction readiness; DWG readiness can add a DWG-specific blocker/warning only if a DWG file is present.

### Security And Reliability Requirements

- Never record secrets, raw subprocess output, stack traces, tokens, request bodies, or full environment dumps.
- Preserve prior valid state if DWG inspection fails; preflight should return structured blockers/warnings rather than crash the dock pane.
- Keep all temporary files, if any, inside the Case Folder `working` area and clean them up best-effort.
- Do not call live Innola, CADINDEX, ArcGIS Enterprise, or network resources from automated tests.

### Testing Notes

- Keep tests in the existing console harness style.
- Use fake DWG readiness services for manifest integration tests.
- Use fake process runner tests for production service parsing, timeout, and sanitization.
- Add at least one test proving downstream artifacts are not created.
- Expected current test baseline after Story 2.8A was 118 passing C# tests plus Python/contract validations.

### Scope Boundaries

- Do not implement extraction, review table generation, validation rules, GDB output, GeoJSON output, reports, CADINDEX sync, or ArcGIS Enterprise writes.
- Do not redesign the Parcel Workflow pane or Transaction Panel UI in this story.
- Do not add live document upload or Innola source discovery endpoints.
- Do not change ArcGIS Pro compatibility lane: keep ArcGIS Pro SDK 3.6 floor, `desktopVersion="3.6"`, `net8.0-windows`.
- Do not make automated tests require live ArcGIS Pro, live ArcPy, or live Innola network access.

### References

- `_bmad-output/planning-artifacts/epics.md`: Story 2.8 acceptance criteria and FR7.
- `_bmad-output/planning-artifacts/architecture.md`: preflight verification-only boundary, C# / Python contract boundary, and no live CADINDEX/Enterprise v1 runtime dependency.
- `_bmad-output/implementation-artifacts/2-6-validate-arcgis-pro-and-python-processing-environment.md`: current environment preflight pattern and scope boundary excluding DWG readiness.
- `_bmad-output/implementation-artifacts/2-8a-wire-live-innola-api-contracts-while-preserving-mock-mode.md`: previous story completion and explicit DWG readiness exclusion.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`: current preflight aggregation to extend.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs`: existing categorized check model.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs`: summary shape to preserve.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`: manifest preflight test pattern.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ProcessingEnvironmentPreflightServiceTests.cs`: fake process runner and sanitization test pattern.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-11 | 0.1 | Initial story context for DWG readiness preflight when a transaction includes a DWG source. | Codex |
