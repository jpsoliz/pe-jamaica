---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/addendum.md
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/review-rubric.md
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/reconcile-technical-research.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-workflow.html
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-failed-extraction-manual-process.html
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-review-before-output.html
  - _bmad-output/planning-artifacts/research/technical-arcgis-pro-addin-parcel-workflow-research-2026-06-08.md
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-06-08'
project_name: 'Sid-jamaica'
user_name: 'JotaPe'
date: '2026-06-08'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Step 1 - Initialization

Architecture workflow initialized from the finalized PRD, UX design package, mockups, PRD review/reconciliation artifacts, and technical research report.

The architecture will focus on implementing an ArcGIS Pro add-in first, with a C#/.NET dock pane orchestration layer, Python/ArcPy processing boundaries, local transaction Case Folder state, file geodatabase outputs, review-before-output enforcement, Manual Process routing, and future CADINDEX Sync Facade readiness.

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

The v1 product contains 23 functional requirements across seven capability groups:

- Guided Submission Intake: create/reopen transaction Case Folders, detect or assign source input profile, copy source files, and preserve workflow state.
- Preflight and Dependency Checks: verify required inputs, file readability, DWG inspectability, ArcGIS/Python environment, coordinate/profile settings, and write access before extraction.
- Extraction and Human Review: run optional AI/OCR/local extraction, produce review data, allow point/data correction, preserve original-vs-edited values, and require review approval before validation/output.
- Validation and Rule Results: run cadastral validation rules, group findings by severity, calculate available counts/score inputs, and route insufficient cases to Manual Process.
- Parcel Output Creation and Map Integration: generate file geodatabase outputs, GeoJSON, reports/logs, annotations where available, and add outputs to ArcGIS Pro maps.
- Enterprise Evolution Readiness: record metadata for future ArcGIS Enterprise Web Tool, Notebook, geoprocessing service, and CADINDEX sync evolution without making those live v1 runtime requirements.
- Security, Audit, and Governance: preserve audit artifacts, redact logs/reports, support optional local-only processing, and document plaintext credential risk for v1.

Architecturally, these requirements imply a step-based workflow engine, stable local artifacts, explicit processing contracts, and strict gating between extraction review, validation, and output creation.

**Non-Functional Requirements:**

The architecture must support reliability, recoverability, auditability, responsiveness, maintainability, and usability for a technical desktop workflow. Long-running processing cannot freeze ArcGIS Pro. Completed artifacts must survive later step failures. Case state must be reopenable. Logs and reports must avoid leaking secrets. Processing boundaries must remain modular enough for later Enterprise migration.

**Scale & Complexity:**

- Primary domain: ArcGIS Pro desktop add-in with geoprocessing automation.
- Complexity level: high internal technical workflow.
- Estimated architectural components: C# add-in shell, WPF/MVVM dock pane, workflow state manager, case folder/artifact manager, Python toolbox/script-tool layer, processing adapter layer, preflight module, extraction module, review data editor, validation/rules module, output generation module, ArcGIS map integration adapter, report/log writer, settings/profile manager, Sync Readiness indicator, CADINDEX Sync Facade, and fixture/test harness.

### Technical Constraints & Dependencies

- Target ArcGIS Pro version: 3.6/3.7.
- Future ArcGIS Enterprise target: 11.5.
- Primary runtime surface: ArcGIS Pro add-in.
- Add-in implementation: ArcGIS Pro SDK for .NET, C#, DAML, WPF/MVVM.
- Processing implementation: Python/ArcPy toolbox or script tools wrapping existing scripts.
- Existing local script logic remains valuable and should not be rewritten wholesale into C#.
- C# and Python must communicate through versioned, file-based contracts rather than ad hoc script arguments or duplicated business logic.
- Inputs include PDF, DWG, TXT, CSV, TIF, PNG, and JPG.
- Outputs include result GDB, GeoJSON, HTML/PDF/JSON reports, process logs, output summaries, and map-added layers.
- Case Folder must copy sources into a transaction folder and preserve all run artifacts.
- The Case Folder is the system of record; hidden state in the ArcGIS Pro project must not be required for recovery or audit.
- v1 does not perform live CADINDEX update or authoritative Enterprise writeback.
- v1 may retain plaintext credential configuration as a documented constraint, but logs/reports must redact secrets.

### Cross-Cutting Concerns Identified

- Explicit workflow state machine: detected profile, intake, preflight passed/blocked, extraction running, extraction failed, review pending, review approved, validation passed/blocked, output created, manual process routed, sync-ready facade.
- Command gating by workflow state so validation/output generation cannot run before review approval.
- Review-before-output enforcement using approved immutable review snapshots.
- Approved review marker must approve a specific review data hash/version; validation must reject stale approval if review data changes.
- C# / Python contract schemas: `manifest.json`, `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, log structure, exit codes, and error categories.
- Processing adapter layer around existing Python scripts to normalize paths, config, logging, outputs, and failures.
- Artifact lifecycle and idempotency: stable run IDs, artifact naming, overwrite/resume rules, provenance, and rerun behavior.
- ArcGIS Pro threading constraints: WPF UI thread for dock pane, `QueuedTask` for GIS operations, async geoprocessing/process execution for long-running tasks, progress reporting, and safe cancellation.
- Detected input profile should be system-owned metadata in production; Case 1-4 remain fixture/test profiles, not user-facing production choices.
- Preflight is verification only; it must not create editable geometry or imply extraction has happened.
- Extraction Review is the authoritative editing surface for extracted and manually added points/lines before approval.
- Zero usable extraction is a first-class branch with preserved context, disabled automated outputs, and explicit Manual Process decision.
- Validation severity gates must remain deterministic and separate from the deferred 0-10 scoring formula.
- AI/OCR/local/manual extraction profiles should sit behind a provider interface; manual/local workflow remains the dependable baseline.
- Acceptance fixtures should be executable contracts with fixture manifests, expected artifacts, expected severity outcomes, recoverability checks, and audit/log assertions.
- CADINDEX Sync Facade should be a test seam with a fake implementation that verifies sync-ready payloads without calling real CADINDEX.
- Sync Readiness must be framed as a local indicator/facade, not authoritative Enterprise sync status.

## Starter Template Evaluation

### Primary Technology Domain

The primary technology domain is a desktop GIS extension: an ArcGIS Pro add-in with a Python/ArcPy geoprocessing core.

Generic desktop starters such as Electron or Tauri are not appropriate because the product must run inside ArcGIS Pro, interact with ArcGIS Pro maps/layers/projects, use ArcGIS Pro SDK threading patterns, and call ArcPy/geoprocessing workflows.

### Starter Options Considered

**Option 1: ArcGIS Pro Module Add-in template**

This is the correct foundation for the C# add-in shell. It provides the DAML/module structure, Visual Studio integration, add-in packaging, and compatibility with ArcGIS Pro SDK patterns.

**Option 2: ArcGIS Pro Dockpane item template**

This should be added inside the Module Add-in project to create the primary dock pane workflow surface. It aligns with the UX design package.

**Option 3: ArcGIS Pro Configuration template**

Rejected for v1. A full managed configuration would customize the broader ArcGIS Pro application experience. The PRD calls for an add-in workflow, not a branded/custom ArcGIS Pro shell.

**Option 4: Generic desktop starter, such as Electron or Tauri**

Rejected. These do not run inside ArcGIS Pro and would create unnecessary integration complexity.

**Option 5: Python-only toolbox**

Rejected as the full product foundation, but retained as the processing core. Python-only tooling would not provide the guided dock pane UX, workflow state, ArcGIS Pro map integration, or review-first user experience.

### Selected Starter: ArcGIS Pro Module Add-in + Dockpane Item Template + Python Toolbox Scaffold

**Rationale for Selection:**

This foundation matches the product's core architecture: ArcGIS Pro add-in first, C#/.NET UI/orchestration, WPF/MVVM dock pane, and Python/ArcPy processing contracts. It uses Esri-supported extension points instead of inventing a separate desktop shell.

**Initialization Approach:**

1. Create an ArcGIS Pro Module Add-in project in Visual Studio.
2. Add an ArcGIS Pro Dockpane item for the Parcel Workflow UI.
3. Add C# projects/folders for workflow state, case folder management, processing contracts, and ArcGIS Pro map integration.
4. Add a Python toolbox or script-tool package for preflight, extraction, validation, output generation, and reporting.
5. Add a shared schema/contracts folder for JSON artifacts.
6. Add test fixture folders for Case 1 through Case 4.

**Architectural Decisions Provided by Starter:**

**Language & Runtime:**

- C#/.NET for ArcGIS Pro add-in shell.
- WPF/MVVM for dock pane UI.
- DAML for ArcGIS Pro add-in commands and UI registration.
- Python/ArcPy for geoprocessing and existing script integration.

**Build Tooling:**

- Visual Studio add-in project and Esri add-in packaging.
- `.esriAddInX` output for add-in distribution.
- Python toolbox/script-tool packaging for processing commands.

**Testing Foundation:**

- Unit-testable C# workflow state and contract serialization logic.
- Python-level tests for processing adapters and schemas.
- Fixture-driven acceptance tests for Case 1 through Case 4.
- Manual ArcGIS Pro smoke tests for add-in loading, dock pane behavior, map integration, and output layer creation.

**Code Organization:**

- Add-in shell and dock pane UI remain separate from processing logic.
- C# owns user workflow, state machine, command gating, Case Folder orchestration, and ArcGIS Pro integration.
- Python owns extraction, validation, geoprocessing, output generation, and report generation.
- Shared JSON contracts define C# / Python boundaries.

**Development Experience:**

- Developers can debug the add-in from Visual Studio launching ArcGIS Pro.
- Python tools remain independently runnable for diagnostics.
- Case Folder artifacts make failed runs inspectable without depending on hidden ArcGIS Pro state.

**Version Note:**

Because the PRD targets ArcGIS Pro 3.6/3.7, architecture should explicitly choose a supported SDK/toolchain lane during implementation planning:

- ArcGIS Pro 3.6 lane: ArcGIS Pro SDK 3.6, Visual Studio 2022, .NET 8 runtime.
- ArcGIS Pro 3.7 lane: ArcGIS Pro SDK 3.7, Visual Studio 2026, .NET 10 runtime.

The implementation team should avoid mixing these lanes casually.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**

- Use ArcGIS Pro SDK add-in architecture, not a standalone desktop/web application.
- Use C#/.NET WPF/MVVM for dock pane UI, workflow orchestration, command gating, Case Folder orchestration, and ArcGIS Pro map integration.
- Use Python/ArcPy toolbox or script-tool wrappers for preflight, extraction, validation, output generation, and reporting.
- Use the transaction Case Folder as the system of record.
- Define versioned JSON contracts between C# and Python before implementation stories begin.
- Enforce review-before-output through an explicit workflow state machine.
- Keep CADINDEX sync facade-only in v1.

**Important Decisions (Shape Architecture):**

- Detect production input profile from source files and metadata; do not expose Case 1-4 as production user choices.
- Treat preflight as verification only, not extraction.
- Treat Extraction Review as the authoritative editing surface for extracted/manually added points and lines.
- Separate validation severity gates from the deferred 0-10 scoring formula.
- Use provider-style boundaries for AI/OCR/local/manual extraction modes.
- Use adapter wrappers around existing Python scripts rather than direct C# calls into legacy scripts.
- Use fixture manifests for Case 1 through Case 4 acceptance tests.

**Deferred Decisions (Post-v1 or Later Architecture Detail):**

- Live CADINDEX update/writeback implementation.
- ArcGIS Enterprise Web Tool / Notebook execution as primary runtime.
- Final production credential vault/service beyond v1 plaintext constraint.
- Exact 0-10 scoring formula.
- Exact fixture filenames and baseline output counts.

### Data Architecture

**Decision: Case Folder as system of record**

Each transaction has a Case Folder named by transaction ID, using the current v1 pattern `TR-SMD-0000001`. The Case Folder contains copied source files, manifest, intermediate summaries, review data, approval marker, validation results, output package, reports, and logs.

**Minimum folder structure:**

```text
TR-SMD-0000001/
  manifest.json
  source/
  working/
    preflight_summary.json
    extraction_review_data.json
    approved_review.json
    validation_summary.json
  output/
    result.gdb/
    extracted_geometry.geojson
    output_summary.json
    reports/
      report.html
      report.pdf
      report.json
    logs/
      process.log
```

**Decision: Versioned JSON contracts**

Use explicit JSON artifacts as the contract between the C# add-in and Python processing layer:

- `manifest.json`
- `preflight_summary.json`
- `extraction_review_data.json`
- `approved_review.json`
- `validation_summary.json`
- `output_summary.json`
- fixture manifest for Case 1-4 test cases

Each JSON artifact includes:

## Distributed Multi-User Working State Strategy

### Decision Summary

For multi-user production deployment, the solution will adopt a **hybrid working-state architecture**:

- **Local Case Folder remains the processing system of record** for source files, intermediate artifacts, logs, resume payloads, extraction review JSON, validation summaries, temporary geodatabases, and Python execution products.
- **ArcGIS Enterprise working layers become the shared spatial review workspace** for in-progress points, lines, polygons, and review-state metadata that multiple users or supervisors may need to inspect in a distributed environment.
- **Authoritative sync/publish remains a separate promotion step** and does not occur directly from raw extraction outputs.

This decision preserves the strengths of the existing local-first architecture while introducing a collaborative spatial layer that can scale across users, machines, and sessions.

### Why This Decision Was Made

The current local `.gdb` and local Parcel Fabric approach is effective for:

- isolated transaction processing
- deterministic Python/ArcPy execution
- suspend/resume packaging
- artifact-rich debugging and audit review

However, a local-only review workspace creates long-term problems in a distributed operational setting:

- no shared visibility of in-progress spatial edits
- difficult supervisor/reviewer inspection
- harder machine-to-machine resume workflows
- no central transactional spatial state for collaboration
- weak alignment with future Enterprise sync/promotion

The hybrid model keeps processing noise local while centralizing only the reviewable spatial state that benefits from shared access.

### Alternatives Considered

#### Alternative A: Local-only working state until final sync

**Pros**

- simplest implementation
- lowest concurrency risk
- easiest debugging and recovery
- no service-side edit contention

**Cons**

- weak collaboration model
- poor shared visibility
- difficult cross-machine recovery
- no centralized review workspace

**Decision**

Rejected as the long-term target. It remains acceptable for prototype and early pilot operation.

#### Alternative B: Enterprise working feature layers plus local processing workspace

**Pros**

- strong distributed-user support
- shared review and supervision surface
- clear separation between processing artifacts and collaborative spatial state
- easier audit dashboards and operational reporting
- compatible with future authoritative sync

**Cons**

- requires transaction ownership and edit-lock conventions
- requires working-layer schema and lifecycle management
- introduces Enterprise configuration and permissions work

**Decision**

Selected as the preferred production path.

#### Alternative C: Enterprise Parcel Fabric as the primary working workspace

**Pros**

- best cadastral editing semantics
- richer parcel-editing tools
- stronger long-term alignment with authoritative parcel operations

**Cons**

- materially higher implementation and operational complexity
- heavier service, permission, and workflow setup
- extraction outputs are not always mature enough to enter Parcel Fabric immediately
- more fragile as the first collaborative working-state model

**Decision**

Deferred as an advanced mode/pilot, not the default shared working-state architecture.

### Architectural Model

#### Review Workspace Mode Configuration

The solution should expose a configuration-controlled **review workspace mode** so deployments can choose the appropriate spatial review surface without changing the core workflow logic.

The initial supported modes are:

1. **`normal`**  
   Standard local transaction `.gdb` review workspace using ordinary feature classes for points, lines, polygons, and related outputs.

2. **`parcel_fabric_local`**  
   Local transaction `.gdb` review workspace using a local Parcel Fabric for advanced parcel-editing and cadastral review tools inside ArcGIS Pro.

3. **`enterprise_working_layers`**  
   Shared ArcGIS Enterprise working review workspace using configured working feature layers for collaborative, distributed review.

These three modes share the same upstream intake, preflight, extraction, review, validation, and audit patterns, but differ in where the spatial review state is materialized.

This configuration should be explicit in application settings and visible in the Configuration panel so administrators and support staff can confirm which review strategy is active for a deployment.

#### Local Case Workspace

The local Case Folder continues to hold:

- copied source attachments
- extracted review JSON and approved review JSON
- OCR/AI/intermediate artifacts
- processing logs
- validation summaries
- generated output package artifacts
- temporary `.gdb` or local Parcel Fabric products used for execution
- resume package ZIP content

These artifacts remain local because they are noisy, execution-specific, and not appropriate as shared Enterprise editing state.

#### Enterprise Working Review Workspace

ArcGIS Enterprise working layers will hold only the spatial and operational state needed for collaborative review:

- working parcel points
- working parcel lines
- working parcel polygons
- optional review issue/annotation layers
- optional case index / case extent layer

Each working feature should carry transaction and lifecycle metadata such as:

- `transaction_id`
- `transaction_number`
- `case_id`
- `workflow_name`
- `workflow_stage`
- `assigned_user`
- `status`
- `is_active`
- `last_saved_utc`
- `review_state`
- `source_mode`
- `edit_generation` or equivalent optimistic-concurrency token

This workspace is the shared review surface, not the full artifact store.

#### Authoritative Promotion Layer

Final authoritative sync is a separate step that:

- validates readiness
- checks downstream requirements
- promotes reviewed/approved geometry into the target authoritative store
- records audit metadata for the promotion event

This avoids coupling raw extraction products directly to the authoritative cadastral environment.

### Working Rules for Multi-User Safety

The distributed design depends on explicit lifecycle rules:

1. One active owner per transaction at a time.
2. Working geometry is always keyed by transaction/case identity.
3. Suspend persists local artifacts and refreshes Enterprise working-state metadata.
4. Reopen restores the latest local artifacts and, where configured, rehydrates the map from Enterprise working layers.
5. Final approval/promotion closes or archives the working record and prevents accidental parallel continuation.

### Parcel Fabric Positioning

Parcel Fabric remains valuable, but it should be introduced carefully.

Recommended sequencing:

- **Default local path:** `normal`
- **Advanced local path:** `parcel_fabric_local`
- **Distributed path:** `enterprise_working_layers`
- **Future advanced distributed path:** optional Enterprise Parcel Fabric pilot for selected transaction types or advanced cadastral editing scenarios.

This preserves simplicity for the core workflow while leaving room for a stronger cadastral editing experience where the operational value justifies the complexity.

### Consequences for the Current Solution

This decision implies the next architecture and implementation work should add:

- Enterprise working-layer schema and configuration
- publishing of approved review geometry into working layers
- reopen/resume behavior that can restore working-state context from Enterprise
- promotion workflow from working review state to sync-ready authoritative outputs
- optional future Enterprise Parcel Fabric mode as a distinct pilot path

The current local `.gdb` / local Parcel Fabric outputs remain useful as execution artifacts and fallback review products, but they are no longer the only long-term review workspace for production multi-user deployment.

- `schema_version`
- `transaction_id`
- `run_id`
- `created_at`
- `created_by` when available
- `source_manifest_hash` or equivalent provenance reference
- step-specific payload
- warnings/errors where applicable

**Decision: Approved review snapshot**

The approval marker must approve a specific extraction review data version/hash. If review data changes after approval, validation must reject the stale approval and require re-approval.

**Decision: Artifact lifecycle and idempotency**

Each processing run gets a stable `run_id`. Reruns must preserve previous logs and make overwrite/resume behavior explicit. Completed step outputs survive later step failures.

### Authentication & Security

**Decision: v1 local credential constraint**

v1 may retain plaintext credential configuration as documented in the PRD, but architecture must isolate that profile and prevent secrets from being written to manifests, logs, reports, or standard status views unless explicitly required by local dev configuration.

**Decision: No broad role-management system in v1**

The architecture assumes a trained cadastral technical operator in ArcGIS Pro. Operator identity should be captured from Windows or ArcGIS Pro user context when available.

**Decision: Security hardening path**

Future production hardening should replace plaintext credentials with Windows Credential Manager / DPAPI for desktop credentials or an organization-approved managed secret service if Enterprise-hosted processing is introduced.

### API & Communication Patterns

**Decision: Local file-based contract plus ArcGIS geoprocessing invocation**

The add-in communicates with the processing layer using manifest/summary JSON files and ArcGIS geoprocessing execution, not REST-first APIs.

**Decision: C# owns orchestration, Python owns processing**

C# owns:

- dock pane state
- workflow state machine
- command enable/disable rules
- file selection and source copy
- Case Folder orchestration
- review approval command
- ArcGIS Pro map/layer integration
- user-facing progress/status

Python owns:

- preflight probes that require ArcPy or processing dependencies
- extraction and OCR/AI/local/manual provider execution
- cadastral validation/rules
- GDB/GeoJSON/report/log generation
- DWG import/inspection processing where ArcPy is required

**Decision: Python adapter layer**

Existing Python scripts must be wrapped behind stable adapter/tool entrypoints. C# must not call many legacy scripts directly.

**Decision: Error model**

Each processing step returns structured status:

- `passed`
- `warning`
- `blocked`
- `failed`
- `manual_process_recommended`

Errors include category, severity, message, evidence, source artifact, and recommended correction.

### Frontend / Add-in Architecture

**Decision: ArcGIS Pro dock pane with WPF/MVVM**

The UX is implemented as an ArcGIS Pro dock pane. The pane uses MVVM to separate UI controls from workflow state, processing commands, and ArcGIS integration.

**Decision: Explicit workflow state machine**

The workflow state machine includes at minimum:

- `NoCaseOpen`
- `IntakeIncomplete`
- `IntakeReady`
- `PreflightRunning`
- `PreflightBlocked`
- `PreflightPassed`
- `ExtractionRunning`
- `ExtractionFailedZeroUsable`
- `ReviewPending`
- `ReviewApproved`
- `ValidationRunning`
- `ValidationBlocked`
- `ValidationPassed`
- `OutputRunning`
- `OutputCreated`
- `ManualProcessRouted`
- `SyncReadinessAvailable`

Command availability is derived from state. Output generation cannot run unless review is approved and validation state permits output.

**Decision: Production input profile detection**

Case 1-4 are testing fixture labels, not production user choices. Production UI shows Detected Input Profile based on selected files and metadata. If confidence is low, the user can correct source selection/classification without choosing an internal fixture case label.

**Decision: Source file viewing**

Source files copied into the Case Folder can be opened through:

- default Windows viewer for PDF, JPG/PNG/TIF, TXT/CSV
- ArcGIS Pro map/layer add for GIS-readable sources such as DWG, TIF/GeoTIFF, GDB outputs, and generated layers
- future custom preview only if later usability testing proves it necessary

### Infrastructure & Deployment

**Decision: Local-first deployment**

v1 deploys as an ArcGIS Pro add-in plus packaged Python toolbox/script-tool assets. Local/shared-drive Case Folders are the operational storage model.

**Decision: No live Enterprise runtime dependency**

ArcGIS Enterprise 11.5 remains a future data/service target. v1 does not require Web Tools, Notebooks, or live CADINDEX writeback to complete local workflow.

**Decision: CADINDEX Sync Facade as test seam**

Define a local sync readiness interface/facade that emits or records the result GDB package intended for future CADINDEX sync. v1 implementation uses a fake/no-op sync adapter and never calls live CADINDEX.

**Decision: ArcGIS Pro SDK lane must be chosen before implementation**

Implementation planning must choose one compatible lane:

- ArcGIS Pro 3.6 lane: ArcGIS Pro SDK 3.6, Visual Studio 2022, .NET 8 runtime.
- ArcGIS Pro 3.7 lane: ArcGIS Pro SDK 3.7, Visual Studio 2026, .NET 10 runtime.

Avoid mixing SDK/runtime/tooling lanes.

### Decision Impact Analysis

**Implementation Sequence:**

1. Establish repository structure and ArcGIS Pro add-in/toolbox scaffold.
2. Define JSON schemas/contracts and fixture manifest shape.
3. Build Case Folder/artifact manager.
4. Build workflow state machine and command gating.
5. Wrap Python scripts behind adapter/tool entrypoints.
6. Implement preflight contract and UI.
7. Implement extraction review contract and editing surface.
8. Implement approval snapshot/hash behavior.
9. Implement validation contract and severity gates.
10. Implement output generation contract and map integration.
11. Implement Sync Facade no-op/fake adapter.
12. Add fixture-driven acceptance tests and ArcGIS Pro smoke tests.

**Cross-Component Dependencies:**

- Workflow state machine depends on Case Folder artifact status.
- Validation depends on approved review snapshot.
- Output generation depends on validation gate and approved review snapshot.
- Sync Readiness depends on output summary and result GDB path.
- Test harness depends on fixture manifests and deterministic JSON contracts.
- Future Enterprise migration depends on stable processing boundaries and output summaries.

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Critical Conflict Points Identified:**

The highest-risk implementation conflicts are:

- Different agents placing workflow state in different places.
- Different JSON field naming across C# and Python.
- Direct calls to legacy scripts instead of stable adapters.
- Output generation accidentally bypassing review approval.
- Preflight doing extraction-like work.
- Validation gates being mixed with the deferred 0-10 score.
- Different artifact naming or overwrite behavior.
- Different error severity/status models.
- Hidden ArcGIS Pro project state becoming required for recovery.
- CADINDEX facade implemented as real sync too early.

### Naming Patterns

**Artifact Naming Conventions:**

Use lowercase snake_case for all JSON artifact filenames and fields.

Required artifact filenames:

- `manifest.json`
- `preflight_summary.json`
- `extraction_review_data.json`
- `approved_review.json`
- `validation_summary.json`
- `output_summary.json`
- `process.log`
- `extracted_geometry.geojson`

Use `result.gdb` for the primary output geodatabase unless a collision requires suffixing with `run_id`.

**JSON Field Naming:**

Use lowercase snake_case in JSON exchanged between C# and Python.

Examples:

```json
{
  "schema_version": "1.0",
  "transaction_id": "TR-SMD-0000001",
  "run_id": "20260608T143012Z",
  "created_at": "2026-06-08T14:30:12Z",
  "source_manifest_hash": "sha256:..."
}
```

Do not use camelCase in JSON contracts.

**C# Naming Conventions:**

Use standard C#/.NET naming:

- Classes, records, enums: `PascalCase`
- Methods and properties: `PascalCase`
- Local variables and private fields: standard .NET conventions
- Interfaces: `IProcessingAdapter`, `ICaseFolderStore`, `ISyncFacade`
- ViewModels: suffix with `ViewModel`, for example `ExtractionReviewViewModel`

**Python Naming Conventions:**

Use standard Python naming:

- Modules/files: `snake_case.py`
- Functions and variables: `snake_case`
- Classes: `PascalCase`
- Tool adapter entrypoints: `run_preflight`, `run_extraction`, `run_validation`, `run_output_generation`

### Structure Patterns

**Project Organization:**

The repository should separate add-in UI/orchestration, Python processing, shared contracts, fixtures, and docs.

Recommended top-level layout:

```text
/src/
  ProAddIn/
  ProcessingTools/
  Contracts/
  Tests/
/fixtures/
  case_1/
  case_2/
  case_3/
  case_4/
/docs/
  architecture.md
```

**C# Add-in Organization:**

```text
ProAddIn/
  Module/
  DockPanes/
  ViewModels/
  Workflow/
  CaseFolders/
  Contracts/
  Processing/
  ArcGIS/
  Sync/
  Settings/
```

Rules:

- `ViewModels/` contains UI state and command bindings only.
- `Workflow/` contains state machine and transition rules.
- `CaseFolders/` owns source copy, artifact paths, hashes, and reopen/resume.
- `Processing/` owns calls to Python tool adapters.
- `ArcGIS/` owns map/layer/project integration.
- `Sync/` owns facade/no-op CADINDEX readiness behavior.

**Python Processing Organization:**

```text
ProcessingTools/
  parcel_workflow.pyt
  adapters/
    preflight_adapter.py
    extraction_adapter.py
    validation_adapter.py
    output_adapter.py
  legacy/
  rules/
  reporting/
  utils/
```

Rules:

- New tool entrypoints live in adapters.
- Existing scripts go under `legacy/` or equivalent wrapper boundary.
- C# never calls legacy scripts directly.
- Python outputs must conform to `Contracts/`.

**Test Structure:**

```text
Tests/
  csharp/
  python/
  fixtures/
```

Rules:

- C# tests cover state machine, contract serialization, command gating, Case Folder path behavior.
- Python tests cover adapters, summaries, output artifacts, and fixture expectations.
- Fixture tests must not depend on live CADINDEX.

### Format Patterns

**Standard JSON Envelope:**

All step summary artifacts use this shape:

```json
{
  "schema_version": "1.0",
  "transaction_id": "TR-SMD-0000001",
  "run_id": "20260608T143012Z",
  "step": "preflight",
  "status": "passed",
  "created_at": "2026-06-08T14:30:12Z",
  "created_by": "domain\\user",
  "source_manifest_hash": "sha256:...",
  "messages": [],
  "payload": {}
}
```

**Status Values:**

Allowed status values:

- `not_started`
- `running`
- `passed`
- `warning`
- `blocked`
- `failed`
- `manual_process_recommended`
- `manual_process_routed`

**Severity Values:**

Allowed severity values:

- `info`
- `warning`
- `error`
- `critical`

Severity gates are deterministic and separate from the 0-10 score.

**Error Message Shape:**

```json
{
  "code": "DWG_UNREADABLE",
  "severity": "critical",
  "category": "input",
  "message": "DWG reference could not be inspected.",
  "evidence": {
    "path": "source/reference.dwg"
  },
  "recommended_action": "Replace the DWG file or continue through Manual Process."
}
```

**Date/Time Format:**

Use ISO 8601 UTC strings for all machine-readable timestamps.

### Communication Patterns

**C# to Python Processing Command:**

C# invokes a named Python toolbox/script-tool adapter with:

- `manifest_path`
- `case_folder_path`
- `run_id`
- `step_name`
- `profile_name`
- optional `settings_path`

Python writes output summary artifacts to the Case Folder. C# reads artifacts after process completion.

**Progress Reporting:**

Use coarse-grained progress states:

- `initializing`
- `checking_inputs`
- `processing`
- `writing_outputs`
- `complete`
- `failed`

If detailed progress is unavailable from Python/ArcPy, show current step and last written artifact rather than fake percentages.

**ArcGIS Pro Threading Pattern:**

- WPF UI updates occur on the UI thread.
- GIS operations requiring ArcGIS Pro object model access use `QueuedTask`.
- Long-running geoprocessing/Python execution must not block the UI thread.
- Map/layer add operations are isolated in the ArcGIS integration adapter.

**Sync Facade Pattern:**

The Sync Facade exposes local readiness only:

```text
ISyncFacade
  EvaluateReadiness(output_summary)
  WriteSyncReadiness(summary)
```

v1 implementation is no-op/fake for CADINDEX network calls.

### Process Patterns

**Workflow State Machine Rules:**

- Every command checks workflow state before execution.
- State is derived from Case Folder artifacts plus in-memory current operation.
- Reopening a case reconstructs state from artifacts.
- Output creation requires `ReviewApproved` and validation state that permits output.
- Manual Process routing records user decision and disables automated output completion unless the user returns to review/correction path.

**Preflight Rules:**

- Preflight verifies readiness only.
- Preflight may inspect files, environment, dependencies, DWG readability, and write access.
- Preflight must not create editable review geometry or final output artifacts.

**Extraction Review Rules:**

- Extraction produces draft review data.
- Users can edit/add/correct points and lines only through the review surface.
- Approval creates `approved_review.json` tied to a review data hash/version.
- Any edit after approval invalidates approval.

**Validation Rules:**

- Validation requires current approved review data.
- Validation gates use severity, not the deferred 0-10 score.
- Critical/error findings block automated output completion unless the workflow routes to Manual Process.

**Output Generation Rules:**

- Output generation reads approved review data and validation summary.
- Output generation writes result GDB, GeoJSON, reports, logs, and output summary.
- Output generation must be idempotent by `run_id` and explicit overwrite/resume policy.

**Logging Rules:**

- Logs include step, timestamp, status, run ID, transaction ID, artifact path, error category, and message.
- Logs must redact API keys and secrets.
- Logs must not be the only source of structured state; summaries are the machine-readable source.

### Enforcement Guidelines

**All AI Agents MUST:**

- Preserve C# / Python boundary responsibilities.
- Use Case Folder artifacts as the source of recovery and audit truth.
- Use snake_case JSON fields and approved artifact filenames.
- Enforce review-before-output through state transitions.
- Keep validation severity gates separate from score.
- Route all legacy Python script use through adapters.
- Avoid live CADINDEX calls in v1.
- Avoid storing required state only in ArcGIS Pro project/session memory.

**Pattern Enforcement:**

- New stories must identify which artifact contracts they read/write.
- Any schema change must update the relevant contract docs/tests.
- Any new processing step must emit a summary JSON artifact.
- Any new UI command must declare allowed workflow states.
- Any output-generation story must prove it rejects stale or missing approval.

### Pattern Examples

**Good Examples:**

- `ExtractionReviewViewModel` calls `WorkflowStateMachine.CanApproveReview`.
- `PythonProcessingAdapter.RunExtraction(manifest_path, run_id)` writes `extraction_review_data.json`.
- `approved_review.json` includes `review_data_hash`.
- `ValidationAdapter` rejects approval when `review_data_hash` does not match current review data.
- `SyncFacadeNoOp` writes local readiness metadata without contacting CADINDEX.

**Anti-Patterns:**

- Calling `CreateParcelFromFile.py` directly from a WPF button.
- Writing `reviewApproved = true` only in ViewModel memory.
- Generating `result.gdb` before approved review data exists.
- Using score `6/10` as the reason to pass validation.
- Adding a real CADINDEX update button in v1.
- Letting preflight generate review geometry.
- Logging raw API keys or secrets.

## Project Structure & Boundaries

### Complete Project Directory Structure

```text
pe-jamaica/
├── README.md
├── .gitignore
├── docs/
│   ├── architecture.md
│   ├── setup-development.md
│   ├── contract-schemas.md
│   └── fixture-guide.md
├── src/
│   ├── ProAddIn/
│   │   ├── ParcelWorkflowAddIn.sln
│   │   ├── ParcelWorkflowAddIn/
│   │   │   ├── Config.daml
│   │   │   ├── Module/
│   │   │   │   └── ParcelWorkflowModule.cs
│   │   │   ├── DockPanes/
│   │   │   │   ├── ParcelWorkflowDockPane.xaml
│   │   │   │   └── ParcelWorkflowDockPane.xaml.cs
│   │   │   ├── ViewModels/
│   │   │   │   ├── ParcelWorkflowViewModel.cs
│   │   │   │   ├── IntakeViewModel.cs
│   │   │   │   ├── PreflightViewModel.cs
│   │   │   │   ├── ExtractionReviewViewModel.cs
│   │   │   │   ├── ValidationViewModel.cs
│   │   │   │   ├── OutputsViewModel.cs
│   │   │   │   └── SyncReadinessViewModel.cs
│   │   │   ├── Workflow/
│   │   │   │   ├── WorkflowState.cs
│   │   │   │   ├── WorkflowStateMachine.cs
│   │   │   │   ├── WorkflowCommandGate.cs
│   │   │   │   └── WorkflowStateRehydrator.cs
│   │   │   ├── CaseFolders/
│   │   │   │   ├── ICaseFolderStore.cs
│   │   │   │   ├── CaseFolderStore.cs
│   │   │   │   ├── CaseFolderLayout.cs
│   │   │   │   ├── ArtifactHasher.cs
│   │   │   │   └── SourceFileCopier.cs
│   │   │   ├── Contracts/
│   │   │   │   ├── ManifestContract.cs
│   │   │   │   ├── PreflightSummaryContract.cs
│   │   │   │   ├── ExtractionReviewDataContract.cs
│   │   │   │   ├── ApprovedReviewContract.cs
│   │   │   │   ├── ValidationSummaryContract.cs
│   │   │   │   ├── OutputSummaryContract.cs
│   │   │   │   └── ContractSerializer.cs
│   │   │   ├── Processing/
│   │   │   │   ├── IProcessingAdapter.cs
│   │   │   │   ├── PythonToolboxProcessingAdapter.cs
│   │   │   │   ├── ProcessingCommand.cs
│   │   │   │   ├── ProcessingResult.cs
│   │   │   │   └── ProcessingProgress.cs
│   │   │   ├── ArcGIS/
│   │   │   │   ├── IMapLayerService.cs
│   │   │   │   ├── MapLayerService.cs
│   │   │   │   ├── SourceViewerService.cs
│   │   │   │   └── QueuedTaskRunner.cs
│   │   │   ├── Sync/
│   │   │   │   ├── ISyncFacade.cs
│   │   │   │   ├── SyncReadinessSummary.cs
│   │   │   │   └── NoOpCadindexSyncFacade.cs
│   │   │   ├── Settings/
│   │   │   │   ├── WorkflowSettings.cs
│   │   │   │   ├── CredentialProfileSettings.cs
│   │   │   │   └── ProcessingProfileSettings.cs
│   │   │   └── Resources/
│   │   │       └── Styles.xaml
│   │   └── ParcelWorkflowAddIn.Tests/
│   │       ├── Workflow/
│   │       ├── CaseFolders/
│   │       ├── Contracts/
│   │       └── Sync/
│   ├── ProcessingTools/
│   │   ├── parcel_workflow.pyt
│   │   ├── adapters/
│   │   │   ├── preflight_adapter.py
│   │   │   ├── extraction_adapter.py
│   │   │   ├── validation_adapter.py
│   │   │   └── output_adapter.py
│   │   ├── contracts/
│   │   │   ├── schema_loader.py
│   │   │   └── contract_writer.py
│   │   ├── legacy/
│   │   │   ├── CreateParcelFromFile.py
│   │   │   ├── CreateAnnotationForLayer.py
│   │   │   ├── rules_engine.py
│   │   │   └── cadastral_submission_runner.py
│   │   ├── rules/
│   │   │   └── rules.yaml
│   │   ├── reporting/
│   │   │   ├── html_report.py
│   │   │   ├── pdf_report.py
│   │   │   └── json_report.py
│   │   ├── providers/
│   │   │   ├── extraction_provider.py
│   │   │   ├── local_extraction_provider.py
│   │   │   ├── ocr_extraction_provider.py
│   │   │   └── ai_extraction_provider.py
│   │   ├── utils/
│   │   │   ├── logging_redaction.py
│   │   │   ├── path_utils.py
│   │   │   └── hashes.py
│   │   └── tests/
│   │       ├── test_preflight_adapter.py
│   │       ├── test_validation_adapter.py
│   │       ├── test_contract_writer.py
│   │       └── test_output_adapter.py
│   └── Contracts/
│       ├── schemas/
│       │   ├── manifest.schema.json
│       │   ├── preflight_summary.schema.json
│       │   ├── extraction_review_data.schema.json
│       │   ├── approved_review.schema.json
│       │   ├── validation_summary.schema.json
│       │   ├── output_summary.schema.json
│       │   └── fixture_manifest.schema.json
│       └── examples/
│           ├── manifest.example.json
│           ├── preflight_summary.example.json
│           ├── extraction_review_data.example.json
│           ├── approved_review.example.json
│           ├── validation_summary.example.json
│           └── output_summary.example.json
├── fixtures/
│   ├── case_1/
│   │   ├── fixture_manifest.json
│   │   ├── source/
│   │   └── expected/
│   ├── case_2/
│   │   ├── fixture_manifest.json
│   │   ├── source/
│   │   └── expected/
│   ├── case_3/
│   │   ├── fixture_manifest.json
│   │   ├── source/
│   │   └── expected/
│   └── case_4/
│       ├── fixture_manifest.json
│       ├── source/
│       └── expected/
└── tools/
    ├── validate_contracts.ps1
    ├── run_python_tests.ps1
    └── package_addin.ps1
```

### Architectural Boundaries

**Add-in UI Boundary**

The WPF dock pane owns presentation, user commands, workflow navigation, state display, review editing UI, and status/progress display. It does not own extraction, validation, GDB creation, or report generation logic.

**Workflow Boundary**

`Workflow/` owns all state transitions and command gating. No ViewModel should independently decide whether validation or output generation is allowed; it should ask `WorkflowCommandGate`.

**Case Folder Boundary**

`CaseFolders/` owns transaction folder layout, source copying, artifact path resolution, artifact hash calculation, and reopen/resume behavior. No other component should hardcode artifact paths.

**Contract Boundary**

`Contracts/` owns serialization/deserialization and schema alignment. Any file exchanged between C# and Python must have a schema and example.

**Processing Boundary**

`Processing/` in C# invokes Python toolbox/script-tool adapters. It does not call legacy scripts directly.

`ProcessingTools/adapters/` in Python normalizes calls to existing script logic and writes contract-compliant artifacts.

**ArcGIS Integration Boundary**

`ArcGIS/` owns ArcGIS Pro SDK-specific map/layer/project interactions and `QueuedTask` usage. ViewModels should not directly manipulate ArcGIS map/layer APIs.

**Sync Boundary**

`Sync/` owns Sync Readiness and CADINDEX facade behavior. v1 uses `NoOpCadindexSyncFacade`; no implementation calls live CADINDEX.

### Requirements to Structure Mapping

**Guided Submission Intake**

- C#: `ViewModels/IntakeViewModel.cs`
- C#: `CaseFolders/CaseFolderStore.cs`
- C#: `CaseFolders/SourceFileCopier.cs`
- Contracts: `manifest.schema.json`
- UX: source file view/open actions through `ArcGIS/SourceViewerService.cs`

**Preflight and Dependency Checks**

- C#: `ViewModels/PreflightViewModel.cs`
- C#: `Processing/PythonToolboxProcessingAdapter.cs`
- Python: `adapters/preflight_adapter.py`
- Contracts: `preflight_summary.schema.json`

**Extraction and Human Review**

- C#: `ViewModels/ExtractionReviewViewModel.cs`
- C#: `Workflow/WorkflowCommandGate.cs`
- Python: `adapters/extraction_adapter.py`
- Python: `providers/*_extraction_provider.py`
- Contracts: `extraction_review_data.schema.json`, `approved_review.schema.json`

**Validation and Rule Results**

- C#: `ViewModels/ValidationViewModel.cs`
- Python: `adapters/validation_adapter.py`
- Python: `legacy/rules_engine.py`
- Python: `rules/rules.yaml`
- Contracts: `validation_summary.schema.json`

**Parcel Output Creation and Map Integration**

- C#: `ViewModels/OutputsViewModel.cs`
- C#: `ArcGIS/MapLayerService.cs`
- Python: `adapters/output_adapter.py`
- Python: `reporting/*`
- Contracts: `output_summary.schema.json`

**Enterprise Evolution Readiness**

- C#: `ViewModels/SyncReadinessViewModel.cs`
- C#: `Sync/ISyncFacade.cs`
- C#: `Sync/NoOpCadindexSyncFacade.cs`
- Contracts: output summary sync readiness section

**Security, Audit, and Governance**

- C#: `Settings/CredentialProfileSettings.cs`
- Python: `utils/logging_redaction.py`
- Contracts: all artifact schemas include audit metadata
- Tests: redaction and artifact provenance checks

### Integration Points

**Internal Communication**

- ViewModels call workflow services and processing adapters.
- Workflow state is derived from Case Folder artifacts plus current in-memory operation.
- C# processing adapter invokes Python toolbox/script-tool entrypoints with paths and run metadata.
- Python writes contract artifacts; C# reads and updates UI state.

**External Integrations**

- ArcGIS Pro SDK: dock pane, DAML, project/map/layer operations.
- ArcPy/Python environment: geoprocessing, DWG inspection/import, GDB output, report generation.
- Windows default file viewer: source file open/view actions for non-GIS files.
- Future ArcGIS Enterprise/CADINDEX: facade only in v1.

**Data Flow**

1. User selects transaction/source files.
2. C# copies files into Case Folder and writes `manifest.json`.
3. C# invokes Python preflight adapter.
4. Python writes `preflight_summary.json`.
5. C# invokes Python extraction adapter.
6. Python writes `extraction_review_data.json`.
7. User edits/reviews in dock pane.
8. C# writes `approved_review.json` with review data hash.
9. C# invokes Python validation adapter.
10. Python writes `validation_summary.json`.
11. C# invokes Python output adapter when gates allow.
12. Python writes result GDB, GeoJSON, reports, logs, and `output_summary.json`.
13. C# adds outputs to ArcGIS Pro map and evaluates local Sync Readiness.

### File Organization Patterns

**Configuration Files**

- Add-in configuration lives in `Config.daml` and `Settings/`.
- Processing profile configuration lives in `ProcessingProfileSettings.cs` and Python adapter config.
- Rules live in `ProcessingTools/rules/rules.yaml`.
- JSON schemas live in `src/Contracts/schemas/`.

**Source Organization**

- C# code organized by responsibility, not by UI step only.
- Python code organized by adapter/tool boundary.
- Legacy scripts isolated under `ProcessingTools/legacy/`.

**Test Organization**

- C# tests mirror C# component directories.
- Python tests sit under `ProcessingTools/tests/`.
- Fixture tests use `fixtures/case_1` through `fixtures/case_4`.

**Asset Organization**

- UX design artifacts remain under `_bmad-output/planning-artifacts/ux-designs/...`.
- Runtime visual assets for the add-in, if any, live under `ProAddIn/ParcelWorkflowAddIn/Resources/`.
- Source documents for transactions are not repo assets; they live in Case Folders or fixtures.

### Development Workflow Integration

**Development Structure**

- Visual Studio is used for C# add-in development/debugging.
- Python adapters remain runnable independently for diagnostics.
- Contract schemas are shared validation points between C# and Python.

**Build Process Structure**

- Add-in build produces `.esriAddInX`.
- Python toolbox/script-tool package is copied or referenced as part of add-in deployment.
- Contract schemas/examples are packaged for validation and developer reference.

**Deployment Structure**

- v1 deployment includes:
  - ArcGIS Pro add-in package
  - Python toolbox/script-tool package
  - rules/config assets
  - schema/examples documentation
  - fixture/test package for validation environments

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:**

The architecture decisions are compatible and mutually reinforcing. ArcGIS Pro SDK add-in architecture, WPF/MVVM dock pane UI, Python/ArcPy processing adapters, file-based contracts, and Case Folder state all support the PRD's add-in-first and local-output-first direction.

The architecture avoids the main contradiction risks:

- It does not mix generic desktop/web app architecture with ArcGIS Pro add-in requirements.
- It does not treat ArcGIS Enterprise/CADINDEX as a live v1 runtime dependency.
- It does not let AI/OCR output bypass human review.
- It does not let output generation bypass approved review data.
- It keeps validation severity gates separate from the deferred 0-10 score.

**Pattern Consistency:**

Implementation patterns support the core decisions. Naming conventions are aligned across C#, Python, and JSON contracts. The processing adapter pattern prevents direct UI-to-legacy-script coupling. The state machine pattern supports review-before-output, zero-result failure handling, validation gates, and Manual Process routing.

**Structure Alignment:**

The proposed project structure supports the architecture. C# add-in code, Python processing tools, JSON contracts, tests, fixtures, and docs have clear ownership. Boundaries are explicit enough for future implementation stories.

### Requirements Coverage Validation ✅

**Feature Coverage:**

All PRD feature areas have architectural support:

- Guided Submission Intake → `ViewModels/IntakeViewModel.cs`, `CaseFolders/`, `manifest.json`
- Preflight and Dependency Checks → `PreflightViewModel`, `preflight_adapter.py`, `preflight_summary.json`
- Extraction and Human Review → `ExtractionReviewViewModel`, extraction providers, review/approval contracts
- Validation and Rule Results → `ValidationViewModel`, `validation_adapter.py`, rules engine, validation summary
- Parcel Output Creation and Map Integration → output adapter, report writers, `MapLayerService`, output summary
- Enterprise Evolution Readiness → Sync Readiness view model and no-op CADINDEX Sync Facade
- Security, Audit, and Governance → credential profile settings, redaction utilities, audit metadata in contracts

**Functional Requirements Coverage:**

All 23 functional requirements are architecturally supported by the state machine, Case Folder model, processing adapters, contract schemas, output package, and ArcGIS integration boundaries.

**Non-Functional Requirements Coverage:**

- Reliability/recoverability: supported by Case Folder as system of record, run IDs, artifact lifecycle, and rehydration.
- Auditability: supported by versioned artifacts, hashes, approval marker, reports, logs, and source copies.
- Responsiveness: supported by WPF UI separation, `QueuedTask` isolation, async processing, and progress states.
- Maintainability: supported by C# / Python boundary, adapter wrappers, and contract schemas.
- Security: supported by log/report redaction and explicit v1 credential constraint.
- Usability: supported by UX-aligned dock pane structure and workflow command gating.

### Implementation Readiness Validation ✅

**Decision Completeness:**

Critical decisions are documented clearly enough for implementation stories. The architecture states what is C#, what is Python, what is file-based contract, what is local-only, and what is deferred.

**Structure Completeness:**

The project tree is specific and maps requirements to directories/files. It defines where ViewModels, workflow state, Case Folder logic, contracts, processing adapters, ArcGIS integration, sync facade, fixtures, and tests live.

**Pattern Completeness:**

Patterns cover naming, JSON format, status/severity values, error shape, progress reporting, threading, state machine, preflight/extraction/validation/output rules, logging, and anti-patterns.

### Gap Analysis Results

**Critical Gaps:**

None identified.

**Important Gaps:**

- Exact 0-10 solution score formula remains deferred to architecture/test planning after fixture data exists.
- Exact Case 1-4 fixture filenames and baseline counts remain deferred to test planning.
- Final production credential vault/service remains deferred beyond v1 plaintext constraint.

**Minor Gaps / Future Enhancements:**

- Exact JSON schema properties beyond the shared envelope still need detailed schema authoring.
- Exact ArcGIS Pro SDK lane should be chosen before implementation setup: 3.6 or 3.7.
- Exact packaging mechanics for bundling Python toolbox assets with the add-in should be detailed during implementation planning.
- CADINDEX facade payload fields should be refined when Enterprise sync design begins.

### Validation Issues Addressed

- Party Mode review identified the need for stronger state machine, contract, adapter, approval snapshot, fixture, and sync facade guidance. These were accepted and incorporated before validation.
- Production input profile detection was clarified so Case 1-4 remain testing fixtures, not production user choices.
- Preflight was clarified as verification only, not extraction.
- Validation severity gates were separated from the deferred 0-10 score.

### Architecture Completeness Checklist

**Requirements Analysis**

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**

- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed

**Implementation Patterns**

- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**

- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High for v1 local add-in architecture; medium for extraction reliability until Case 1-4 fixtures are assembled and run.

**Key Strengths:**

- Strong separation between ArcGIS Pro UI/orchestration and Python/ArcPy processing.
- Case Folder as recoverable/auditable system of record.
- Explicit review-before-output state machine.
- Clear contract-first C# / Python boundary.
- CADINDEX future path preserved without over-scoping v1.
- Testability improved through fixture manifests and no-op sync facade.

**Areas for Future Enhancement:**

- Live CADINDEX sync implementation.
- Enterprise Web Tool / Notebook-backed processing.
- Production credential management beyond plaintext v1 profile.
- More advanced document quality scoring after fixture calibration.
- Potential custom source preview if default viewer/map-add behavior proves insufficient.

### Implementation Handoff

**AI Agent Guidelines:**

- Follow all architectural decisions exactly as documented.
- Use implementation patterns consistently across all components.
- Respect project structure and boundaries.
- Refer to this document for all architectural questions.
- Do not bypass review approval, validation gates, or Case Folder artifact contracts.

**First Implementation Priority:**

Create the repository scaffold using the ArcGIS Pro Module Add-in + Dockpane item template, Python toolbox scaffold, shared JSON schema folder, and Case 1-4 fixture folder structure.
