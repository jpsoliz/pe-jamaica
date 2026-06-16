---
baseline_commit: handoff-2026-06-12
---

# Story 2.12: Execute Draft Extraction and Review Artifact Generation

Status: ready-for-dev

## Story

As a cadastral technical staff user,
I want the add-in to execute the resolved extraction plan and generate draft review data from the selected transaction files,
so that I can review and correct extracted points before parcel geometry and `.gdb` outputs are created.

## Acceptance Criteria

1. Given a transaction has been loaded, its attachments copied to the Case Folder, and preflight has passed, when the user starts extraction, then the add-in executes the resolved manifest `script_plan` through bounded adapter entrypoints instead of opening folders or expecting artifacts to already exist.
2. Given a Scenario A transaction includes computation and map documents in PDF/image form, when draft extraction runs, then the configured document extraction path produces `working/extraction_review_data.json` suitable for review, without yet generating final parcel geometry or `.gdb` outputs.
3. Given a Scenario B transaction includes TXT/CSV points, optional computation source, map source, and DWG reference, when draft extraction runs, then TXT/CSV point normalization is preferred over OCR for point rows and any DWG step remains context-only.
4. Given OpenAI-assisted extraction is enabled by configuration/profile, when the relevant document extraction step runs, then the selected provider may use OpenAI; when disabled, the workflow remains local-only and does not attempt external AI calls.
5. Given extraction from documents is incomplete, low-confidence, or ambiguous, when draft review data is written, then the artifact records unresolved or missing rows so the next review story can support manual correction and point entry before parcel build.
6. Given a single computation sheet, plan, or image may describe multiple parcels or multiple closed traverses, when draft extraction runs, then the review artifact preserves parcel grouping and/or explicit boundary-break metadata so downstream line creation does not assume that the last point of one parcel is automatically connected to the first point of the next parcel.
7. Given parcel grouping cannot be determined confidently from the source document, when draft review data is written, then the affected rows are marked for manual review instead of being silently chained into a potentially incorrect parcel sequence.
8. Given draft extraction completes, when artifacts are refreshed, then the Parcel Workflow "Open" / Extraction Review action opens the generated review artifact rather than falling back to the case folder.
9. Given extraction fails, times out, or returns malformed results, when the add-in handles the failure, then the workflow records a redacted user-facing status, preserves prior intake/preflight artifacts, and does not create final geometry, output package artifacts, CADINDEX writes, or Enterprise writes.
10. Given this story is complete, then parcel build remains out of scope: no `.gdb`, feature classes, validation summary, output summary, or completion action is created here.
11. Given the story is complete, then focused tests cover Scenario A draft extraction execution, local-only mode, provider toggle behavior, artifact generation, multiple-parcel boundary handling, failure handling, and the absence of final output artifacts.

## Tasks / Subtasks

- [ ] Add execution boundary for resolved script plans. (AC: 1, 7, 8)
  - [ ] Add execution interfaces under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/`, for example:
    - [ ] `IWorkflowScriptAdapter`
    - [ ] `IWorkflowScriptExecutor`
    - [ ] `WorkflowScriptExecutionResult`
  - [ ] Resolve adapters by stable adapter identifier from the manifest `script_plan`.
  - [ ] Keep execution bounded, cancellable where practical, and independent from final output generation.
- [ ] Add draft extraction adapter implementation. (AC: 1-7, 9)
  - [ ] Implement a production adapter that can invoke `CreateParcelFromFile.py` or a wrapper entrypoint in review-only mode.
  - [ ] Generate a per-case config/INI file in the Case Folder `working` area instead of depending on the shared legacy INI.
  - [ ] Use transaction-loaded source files from `source/` and write review artifacts into `working/`.
  - [ ] Keep OpenAI provider usage controlled by settings/profile, not hardcoded script defaults.
- [ ] Capture parcel grouping / boundary metadata in review artifacts. (AC: 5-7)
  - [ ] Extend the draft extraction contract so each extracted review row can optionally carry grouping fields such as `parcel_group_id`, `traverse_id`, `sequence_in_group`, or a boundary-break marker.
  - [ ] Preserve source ordering within each parcel/traverse group instead of flattening all extracted rows into one implied parcel sequence.
  - [ ] Mark rows as unresolved when the extractor cannot confidently determine whether the next row continues the same parcel or starts a new one.
- [ ] Add per-case config generation. (AC: 2-5, 7)
  - [ ] Build a generated config such as `working/CreateParcelFromFile_case.ini`.
  - [ ] Populate source file paths, results/log directories, transaction number, provider mode, and model settings from the add-in configuration.
  - [ ] Do not persist API keys, bearer tokens, passwords, or certificate private data in generated config or logs.
- [ ] Generate and register draft review artifacts. (AC: 2, 5-8, 10)
  - [ ] Normalize review output path to `working/extraction_review_data.json`.
  - [ ] Preserve any parcel/traverse grouping metadata needed by later manual review and output stages.
  - [ ] Register the artifact in `WorkflowSession.AvailableArtifacts`.
  - [ ] Refresh Parcel Workflow extraction-review state from artifact existence.
  - [ ] Ensure parcel-build/final-output artifacts are still absent after this story.
- [ ] Respect source-family behavior. (AC: 2-7)
  - [ ] Scenario A: document extraction path for computation/map PDF/image packages.
  - [ ] Scenario B: prefer TXT/CSV normalization for point rows where present.
  - [ ] DWG remains context-only for this story and must not trigger parcel build.
- [ ] Add status and failure handling. (AC: 8, 9)
  - [ ] Surface execution start, success, and failure in the Parcel Workflow status text.
  - [ ] Redact script stderr/stdout before exposing messages.
  - [ ] Preserve previously written `preflight_summary.json` and manifest state on extraction failure.
- [ ] Add focused tests. (AC: 1-11)
  - [ ] Script plan executes only after valid preflight-passed state.
  - [ ] Scenario A writes `working/extraction_review_data.json`.
  - [ ] Local-only mode avoids OpenAI execution path.
  - [ ] Provider-enabled mode passes provider/model configuration without leaking secrets.
  - [ ] Multi-parcel input produces explicit group/boundary metadata instead of a single flattened point chain.
  - [ ] Failure/timeout produces sanitized status and no final output artifacts.
  - [ ] Parcel Workflow extraction review action opens the generated review artifact when present.
- [ ] Validate and package. (AC: 1-11)
  - [ ] Run `tools\validate_contracts.ps1`.
  - [ ] Run `tools\run_python_tests.ps1`.
  - [ ] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [ ] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [ ] Run `tools\package_addin.ps1`.

## Dev Notes

### Why This Story Exists

- Story 2.11 resolved and persisted `script_plan`, but intentionally did not execute it.
- Current Parcel Workflow behavior can open artifacts only if they already exist.
- The real workflow requires draft extraction first, followed by review/edit and only later parcel geometry generation.

### Current State From Prior Stories

- Story 2.4 loads transaction details and attachments into the local Case Folder.
- Story 2.6 validates ArcGIS Pro and Python processing environment readiness.
- Story 2.8 validates DWG readiness without creating downstream artifacts.
- Story 2.11 resolves workflow rules and persists manifest-backed `script_plan`.
- Current preflight already blocks unsupported intake, stale plans, missing required roles, and environment failures.

### Alignment Rules

- Draft extraction is not final parcel generation.
- Review/edit is expected and mandatory for imperfect document extraction cases.
- TXT/CSV flows should remain deterministic and should not depend on OCR by default.
- OpenAI is optional and must stay settings/profile controlled.
- When a source document contains multiple parcels or multiple traverses, extraction must preserve parcel boundaries instead of implying one continuous line chain across all extracted rows.
- If parcel boundary detection is uncertain, the system should prefer flagged manual review over silent geometric assumptions.

### Recommended Execution Model

1. Read persisted `script_plan` from manifest.
2. Resolve each step to an adapter implementation.
3. Generate any per-case config required by the adapter.
4. Execute extraction/normalization steps in order.
5. Preserve parcel/traverse grouping and any boundary-break signals from extraction.
6. Merge or normalize intermediate outputs into `working/extraction_review_data.json`.
7. Refresh workflow artifacts and status.

### Integration Guidance For `CreateParcelFromFile.py`

- Prefer a wrapper or review-only invocation mode first:
  - `python CreateParcelFromFile.py --config <case-ini> --review-data --review-case <transaction_id>`
- The generated config should point to:
  - copied source files under `source/`
  - logs under `logs/`
  - results/review outputs under `working/`
- If the script produces a transaction-named review file, normalize or copy it to:
  - `working/extraction_review_data.json`
- If OpenAI-assisted parsing is used to improve line/point interpretation from low-quality PDFs or images, keep it behind settings and emit structured grouping metadata that downstream review/output stages can trust or challenge.
- Do not allow the story to proceed into final `.gdb` generation even if the legacy script can do so.

### Parcel Boundary Handling

- The extraction artifact must support more than one parcel/traverse in a single transaction source.
- Do not assume `row N` and `row N+1` belong to the same parcel boundary unless the source clearly indicates continuity.
- Preferred extraction output shape includes one or more of:
  - `parcel_group_id`
  - `traverse_id`
  - `sequence_in_group`
  - `is_boundary_break`
  - `group_confidence`
- Later output-generation stories should consume these fields when creating lines and polygons, instead of reconstructing parcel chains from flat row order alone.

### Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessRunner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Scope Boundaries

- Do not implement inline review-edit UI in this story.
- Do not generate final `.gdb`, feature classes, validation summary, output summary, or sync artifacts.
- Do not persist secrets into manifest, config, logs, or review artifacts.
- Do not introduce live network calls into automated tests.

### References

- `docs/project/PROCESSING_ALIGNMENT.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/2-11-workflow-rule-resolution-and-script-plan-manifest.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts\CreateParcelFromFile.py`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for executing draft extraction and generating review artifacts from transaction-driven script plans. | Codex |
| 2026-06-15 | 0.2 | Expanded the story to cover optional OpenAI-assisted parsing and explicit multi-parcel boundary/group metadata in draft extraction artifacts. | Codex |
| 2026-06-16 | 0.3 | Wired draft extraction to the external CreateParcel document-type catalog and required matched `doc_type_id` metadata to be persisted into extraction review artifacts and generated case config. | Codex |
