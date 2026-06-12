---
baseline_commit: 44166279ff6a34211ea0b933e0fe309306d3d58a
---

# Story 2.11: Workflow Rule Resolution and Script Plan Manifest

Status: done

## Story

As a cadastral technical staff user,
I want the add-in to resolve a workflow rule from the selected Innola transaction type and attached source files,
so that the Parcel Workflow knows which processing scripts will run, with which parameters, before extraction begins.

## Acceptance Criteria

1. Given a transaction has been selected and its metadata/attachments have been loaded into a local Case Folder, when the intake profile is refreshed, then the add-in resolves a workflow rule using transaction type, process step, detected source roles, file extensions, and configured processing mode.
2. Given a Scenario A transaction contains two PDFs, one computation/source PDF and one plan/map PDF, when rule resolution runs, then it selects a two-PDF rule that plans computation-point extraction from the computation PDF and OCR/map extraction from the plan/map PDF.
3. Given a Scenario B transaction contains points/computation, DWG, and plan/map roles, when rule resolution runs, then it selects a rule that includes the DWG-aware processing path without blocking Scenario A cases for missing DWG.
4. Given no configured rule matches the transaction metadata and copied source files, when rule resolution runs, then preflight is blocked with a clear redacted message and no extraction, validation, output, CADINDEX, or Enterprise write is attempted.
5. Given a workflow rule matches, when the Case Folder manifest is updated, then it records the resolved `workflow_profile`, `rule_id`, and a versioned `script_plan` using lowercase snake_case fields.
6. Given the script plan is created, then every planned script entry includes a stable step name, adapter/tool path or identifier, ordered input roles, output artifact paths, parameters, timeout/cancellation hints, and whether OpenAI/OCR/local-only providers are enabled.
7. Given the user views Configuration, then local AI/OCR mode and credential profile visibility remains configuration-only; no API keys, tokens, passwords, or certificate private data are written to the manifest, plan, logs, reports, or UI status.
8. Given the story is complete, then no actual extraction/review/validation/output execution is added; this story only resolves and persists the plan that later stories will execute.
9. Given rule resolution is implemented, then focused tests cover Scenario A two-PDF live-style filenames, Scenario B DWG rule selection, no-match blocking, secret redaction, manifest persistence, and mock/live parity without using live Innola network calls.

## Tasks / Subtasks

- [x] Add workflow rule contract models. (AC: 1, 5, 6)
  - [x] Add C# contracts under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/` or a dedicated `WorkflowRules/` namespace for `WorkflowRule`, `WorkflowScriptPlan`, and `WorkflowScriptStep`.
  - [x] Keep serialized fields lowercase snake_case.
  - [x] Include schema/version fields and rule version in the plan.
  - [x] Ensure paths are Case Folder-relative where possible, especially `source/...`, `working/...`, and `output/...`.
- [x] Add configurable rule registry. (AC: 1-4, 6)
  - [x] Add a default JSON rule file under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/`, for example `WorkflowRules.json`.
  - [x] Include at least Scenario A two-PDF and Scenario B DWG-aware rules.
  - [x] Match on transaction type/process step when present, but allow file-role matching to drive the current live test where metadata may be incomplete.
  - [x] Keep rule loading deterministic and testable without ArcGIS Pro.
- [x] Implement rule resolution service. (AC: 1-4)
  - [x] Add a service such as `WorkflowRuleResolver` that accepts transaction metadata, manifest source files, detected profile, and local settings.
  - [x] Prefer exact transaction type + profile matches, then profile/source-role matches.
  - [x] Return a structured no-match result with clear blocker text.
  - [x] Do not execute scripts from the resolver.
- [x] Persist the script plan in the Case Folder manifest. (AC: 5, 6, 7)
  - [x] Extend the manifest payload backward-compatibly with resolved rule/plan fields.
  - [x] Update manifest serialization tests for backward compatibility.
  - [x] Redact or omit secret-looking parameters; store only credential/profile identifiers or environment-variable names.
- [x] Integrate rule resolution into transaction load/intake refresh. (AC: 1-5)
  - [x] Run resolution after Innola attachments are copied and source roles/profile are refreshed.
  - [x] Ensure the Parcel Workflow Source Intake and Preflight surfaces can show the resolved workflow profile or no-match blocker.
  - [x] Keep existing active transaction gating and panel locking unchanged.
- [x] Add preflight gating for unresolved plans. (AC: 4, 8)
  - [x] If no plan exists or the plan is stale against the current source manifest hash, preflight should block before extraction can be enabled.
  - [x] Do not create extraction artifacts, review data, GDB, GeoJSON, reports, or output package artifacts.
- [x] Add focused tests. (AC: 1-9)
  - [x] Scenario A live-style filenames: `BELLEV029GEOLANCOMSHEET.pdf` and `BELLEV029GEOLAN20230811.pdf` resolve to the two-PDF rule.
  - [x] Scenario B source roles resolve to a DWG-aware rule.
  - [x] Unknown transaction/file combination produces a no-match blocker.
  - [x] Manifest plan persistence remains backward-compatible with old manifests.
  - [x] Plan parameters do not contain API keys, passwords, tokens, raw headers, or certificate secrets.
  - [x] Mock mode still resolves the mock two-PDF rule.
- [x] Validate and package. (AC: 1-9)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.

### Review Findings

- [x] [Review][Patch] Intake refresh does not re-resolve workflow rule for an already-loaded Innola transaction [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:169] — AC1 and the integration task require rule resolution when the intake profile is refreshed, but `RefreshInputProfile()` only updates `DetectedProfile` and writes the manifest. If an Innola Case Folder is reopened, source metadata changes, or the user refreshes intake after load, the manifest keeps the old/missing `script_plan` instead of resolving a new rule.
- [x] [Review][Patch] Secret filtering runs before settings-token substitution, so a misconfigured setting value can be persisted [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleResolver.cs:98] — AC7 forbids API keys/tokens/passwords in the manifest or script plan. `ResolveParameters()` filters the raw rule parameter value before replacing `{settings.openai_api_key_environment_variable}`; if local settings accidentally contain an actual key/token instead of an environment variable name, the substituted secret is written into `script_plan.parameters`. The same redaction guard should be applied after substitution, and top-level step fields such as `credential_profile` should be constrained to non-secret identifiers.

## Dev Notes

### Current State From Prior Stories

- Story 2.4 loads transaction details and attachments into the local Case Folder through `InnolaTransactionLoadService`.
- Story 2.6 validates ArcGIS Pro and Python readiness during preflight.
- Story 2.7 enforces active transaction state and locks the transaction list while a task is in progress.
- Story 2.8A wires live Innola API contracts while preserving mock mode.
- Recent live testing confirmed transaction `100000206` can return two source attachments from scanning application metadata.
- Recent patch maps live filenames:
  - `*COMSHEET*.pdf` to `computation_source`
  - `*GEOLAN*.pdf` to `plan_map_reference`
- Current `WorkflowSettings.json` exposes local AI/OCR configuration keys:
  - `ocr_engine`
  - `openai_enabled`
  - `openai_model`
  - `openai_api_key_environment_variable`

### Existing Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
  - Extend manifest payload backward-compatibly with resolved rule and script plan metadata.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs`
  - Preserve existing read/write behavior and old manifest compatibility.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
  - Hook rule resolution after attachments are copied and before/after profile detection is persisted.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs`
  - Keep source-role/profile detection as input to rule resolution; do not duplicate this logic inside the resolver.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
  - Add plan-present/stale-plan checks if preflight should block before extraction stories.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
  - Keep AI/OCR settings visible but do not persist actual OpenAI keys.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml(.cs)`
  - Already displays AI/OCR configuration summary; only extend if new profile ids need display.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Intake/SourceInputProfileDetectorTests.cs`
  - Existing live filename Scenario A test should remain green.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
  - Add integration-style tests around Case Folder manifest plan creation.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
  - Register new tests.

### Recommended Rule Shape

Use JSON configuration for rules so future transaction types can be added without scattering `if/else` logic through the workflow.

```json
{
  "schema_version": "1.0.0",
  "rules": [
    {
      "rule_id": "scenario_a_two_pdf_v1",
      "workflow_profile": "scenario_a_two_pdf",
      "transaction_types": ["Plan Examination", "Computation Check"],
      "process_steps": ["parcel_workflow"],
      "required_sources": [
        { "role": "computation_source", "extensions": [".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg"] },
        { "role": "plan_map_reference", "extensions": [".pdf", ".tif", ".tiff", ".png", ".jpg", ".jpeg"] }
      ],
      "script_plan": [
        {
          "step_name": "extract_points_from_computation",
          "adapter": "extraction_adapter",
          "script": "extract_points_from_computation_pdf",
          "input_roles": ["computation_source"],
          "output_artifacts": ["working/extraction_points.json"],
          "parameters": {
            "provider": "local_or_openai_ocr",
            "ocr_engine": "{settings.ocr_engine}",
            "openai_model": "{settings.openai_model}"
          },
          "timeout_seconds": 300
        },
        {
          "step_name": "ocr_plan_map_reference",
          "adapter": "extraction_adapter",
          "script": "ocr_plan_map_pdf",
          "input_roles": ["plan_map_reference"],
          "output_artifacts": ["working/plan_ocr.json"],
          "parameters": {
            "provider": "local_or_openai_ocr"
          },
          "timeout_seconds": 300
        }
      ]
    }
  ]
}
```

### Manifest Persistence Guidance

Recommended manifest payload additions:

```json
{
  "workflow_profile": "scenario_a_two_pdf",
  "workflow_rule_id": "scenario_a_two_pdf_v1",
  "workflow_rule_version": "1.0.0",
  "script_plan": {
    "schema_version": "1.0.0",
    "created_at": "2026-06-12T00:00:00Z",
    "source_manifest_hash": "sha256:...",
    "steps": []
  }
}
```

Do not store:

- OpenAI API key value
- Innola password/token
- bearer/auth headers
- certificate private data
- raw HTTP payloads
- full environment dumps

### Rule Matching Guidance

Recommended matching priority:

1. Process step + exact transaction type + detected profile/source roles.
2. Exact detected profile/source roles.
3. Required source roles/extensions only.
4. No-match blocker.

This lets the current live test work even if Innola transaction type naming changes slightly, while still allowing stricter rules later.

### Scope Boundaries

- Do not execute extraction scripts in this story.
- Do not create `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, output GDB, GeoJSON, reports, or CADINDEX sync artifacts.
- Do not redesign the Parcel Workflow or Transaction Panel UI.
- Do not add live Innola document upload.
- Do not call OpenAI; only plan/configure whether an OpenAI-enabled provider would be used later.
- Do not add live network calls to tests.

### Testing Notes

- Keep the current no-framework console test harness.
- Prefer pure unit tests for resolver matching.
- Use temp Case Folders for manifest persistence tests.
- Keep all test source files small local fixtures/temp files.
- After implementation, run the readiness script:
  `PowerShell -ExecutionPolicy Bypass -File .\tools\run_arcgis_addin_readiness.ps1 -Configuration Debug`

### References

- `_bmad-output/planning-artifacts/epics.md`: Epic 2 and Appendix C# / Python wrapper contract.
- `_bmad-output/planning-artifacts/architecture.md`: C# owns orchestration; Python owns processing; C# must call stable adapters, not many scripts directly.
- `_bmad-output/implementation-artifacts/2-4-load-transaction-details-and-attachments-into-case-folder.md`: transaction attachment load and Case Folder creation.
- `_bmad-output/implementation-artifacts/2-6-validate-arcgis-pro-and-python-processing-environment.md`: preflight/environment readiness pattern.
- `_bmad-output/implementation-artifacts/2-8a-wire-live-innola-api-contracts-while-preserving-mock-mode.md`: live Innola metadata/attachment contracts.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`: likely integration point.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`: manifest persistence contract.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`: preflight gate to extend.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `PowerShell -ExecutionPolicy Bypass -File .\tools\validate_contracts.ps1` passed.
- `PowerShell -ExecutionPolicy Bypass -File .\tools\run_python_tests.ps1` passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed: 142 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore` passed.
- `PowerShell -ExecutionPolicy Bypass -File .\tools\package_addin.ps1` passed.
- `PowerShell -ExecutionPolicy Bypass -File .\tools\run_arcgis_addin_readiness.ps1 -Configuration Debug` passed.

### Completion Notes List

- Added a versioned workflow rule registry and resolver for Scenario A two-PDF and Scenario B DWG-aware intake.
- Persisted resolved workflow profile, rule id/version, source hash, and planned script steps into the Case Folder manifest without storing secrets.
- Integrated rule resolution into Innola transaction load after attachment copy/profile detection.
- Added Innola-specific preflight blocking when no plan exists or the plan is stale against the copied source files.
- Kept story scope to planning/gating only; no extraction, review, validation, output, CADINDEX, or Enterprise write execution was added.
- Review fixes added rule re-resolution during intake refresh for loaded Innola cases and redaction after settings-token substitution.

### File List

- `_bmad-output/implementation-artifacts/2-11-workflow-rule-resolution-and-script-plan-manifest.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleRegistry.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleResolutionContext.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleResolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowRuleSettingsLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/WorkflowRules/WorkflowScriptPlan.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/TempFile.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/WorkflowRules/WorkflowRuleResolverTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for transaction-driven workflow rule resolution and persisted script plan manifest. | Mary |
| 2026-06-12 | 1.0 | Implemented workflow rule resolution, script plan manifest persistence, preflight plan gates, tests, and add-in package validation. | Codex |
| 2026-06-12 | 1.1 | Fixed code-review findings for intake refresh rule re-resolution and post-substitution secret redaction. | Codex |
