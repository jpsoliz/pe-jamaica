# AI Project Context

## 1) Project identity

- **Project:** Sid-jamaica
- **Goal:** ArcGIS Pro add-in for Innola transaction-driven cadastral parcel workflow
- **MVP scope:** local-first desktop workflow with review-first extraction, validation gates, output artifacts, and future CADINDEX sync readiness
- **Current architecture baseline:** ArcGIS Pro 3.6-first lane with 3.7 forward-compatibility checks
- **Primary repo paths:**
  - `src/ParcelWorkflowAddIn/` (C# add-in and MVVM workflow)
  - `src/ProcessingTools/` (Python processing tools and adapters)
  - `src/Contracts/` (JSON schemas/examples)
  - `fixtures/` (Case 1–4 acceptance contracts)
  - `_bmad-output/` (BMAD planning/implementation artifacts)

## 2) Current technical stack

- ArcGIS Pro SDK (Module Add-in + Dockpane) + WPF/MVVM
- C# / .NET (`net8.0-windows`) for orchestration and UI
- Python/ArcPy tooling in `ProcessingTools/` for extraction/validation/output adapters
- File contracts in JSON (`snake_case`) between C# and Python
- Add-in command gating/state machine model (no live output until approval and validation gates pass)

## 3) Environment constraints

- **ArcGIS Pro lane:** 3.6 compatibility target, test 3.7 smoke path
- **Python environment:** `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe`
- **Packaging:** run `tools/package_addin.ps1` (MSBuild path expected, dotnet package path differs for add-in artifacts)
- **Build helper docs:** `docs/toolchain.md`

## 4) Integration domains

- **Innola services:** authentication + transactions + details + attachments + lifecycle actions (active transaction workflow started in Story 2.8A).
- **Case folder model:** each transaction has a local folder as system-of-record for manifests, working artifacts, and outputs.
- **Sync readiness:** CADINDEX sync is visible/facade-only in v1, not live mutation.

## 5) Workflow ownership model

- **C# owns**
  - UI state, workflow transitions, command enable/disable
  - source intake orchestration and case folder lifecycle
  - review approval logic and process gating
  - ArcGIS Pro map interactions and progress/error visibility
- **Python owns**
  - preflight checks requiring ArcPy/dependency probing
  - extraction/validation/output generation
  - report/log writing through contract outputs
- **Shared boundary**
  - JSON contracts + run metadata
  - stable input/output paths and naming

## 6) Source-of-truth files

### Permanent
- `src/ParcelWorkflowAddIn/` architecture and implementation
- `src/ProcessingTools/` and `src/Contracts/`
- `docs/toolchain.md`, `docs/INDEX.md`, `docs/README.md`
- `_bmad-output/planning-artifacts/` (architecture/design/PRD/epic context)
- `docs/project/AI_PROJECT_CONTEXT.md`
- `docs/project/PROCESSING_ALIGNMENT.md`

### Operational state (changes frequently)
- `docs/project-management/CURRENT_SPRINT.md`
- `docs/project-management/DECISIONS.md`
- `docs/retrospectives/LESSONS_LEARNED.md`
- `_bmad-output/implementation-artifacts/*`

## 7) Active sprint (2026-06-11)

- Epic 2 in progress; stories 2.1 through 2-8a are done in implementation records.
- Next queued stories: `2-8-validate-dwg-readiness-when-present`, `2-9-configure-processing-and-credential-profiles`, `2-10-display-preflight-results-and-gate-extraction`.

## 8) Naming / conventions for consistency

- Docs: uppercase file names for top-level coordination files (`CURRENT_SPRINT.md`, `DECISIONS.md`).
- Code: explicit, typed service boundaries; status states are discrete and testable.
- JSON: `snake_case` fields, explicit schema versioning, explicit approval hash/version for review approval.
