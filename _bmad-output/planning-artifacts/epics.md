---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-workflow.html
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-failed-extraction-manual-process.html
  - _bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-review-before-output.html
workflowType: 'epics-and-stories'
project_name: 'Sid-jamaica'
user_name: 'JotaPe'
date: '2026-06-08'
---

# Sid-jamaica - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Sid-jamaica, decomposing the requirements from the PRD, UX Design, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Create or reopen a submission Case Folder from an Innola transaction selected in the ArcGIS Pro add-in after user login. The transaction supplies the transaction identifier, case type/profile metadata, and attachment references; the add-in stores downloaded source files and workflow artifacts in the local Case Folder.

FR2: Support Source Scenario A inputs: computation file in PDF/TIF/PNG/JPG format plus plan/map reference in PDF/TIF/PNG/JPG format.

FR3: Support Source Scenario B inputs: points/computation source in PDF/TXT/CSV format, DWG reference, and plan/map reference in PDF/TIF/PNG/JPG format.

FR4: Preserve case state in the Case Folder so work can resume after ArcGIS Pro closes or processing fails.

FR5: Run Preflight against the Manifest and show blocking and non-blocking input issues.

FR6: Validate ArcGIS Pro, ArcPy, Python package, workspace, and write-access conditions before processing.

FR7: Validate DWG reference readability and inspect available CAD sublayers where possible.

FR8: Run extraction after Preflight passes, with optional AI/OCR and local/manual extraction profiles.

FR9: Review extracted parcels, points, segments, metadata, missing fields, confidence indicators, and source evidence.

FR10: Allow users to correct extracted values or mark extraction rows as unresolved before approval.

FR11: Approve Extraction Review Data only when required values are present and blockers are cleared.

FR12: Run cadastral validation rules against Approved Review Data, source inputs, DWG-derived context, and configured rules.

FR13: Display validation results grouped by severity and status with visible counts and details.

FR14: Route or recommend cases to Manual Process when extraction/validation produces zero usable results, critical failures, or insufficient automated results.

FR15: Create local file geodatabase outputs, extracted point/line feature classes, annotation feature classes where possible, GeoJSON, and output summary.

FR16: Add generated output layers to the active ArcGIS Pro map when the user chooses to add them.

FR17: Generate HTML, PDF, JSON reports and diagnostic logs for each case without leaking secrets.

FR18: Support publish/sync-ready output state for future CADINDEX/ArcGIS Enterprise evolution while keeping v1 facade-only.

FR19: Record Enterprise candidate metadata such as processing duration, dependency information, output counts, failure types, and sync facade metadata.

FR20: Keep processing boundaries modular so preflight, extraction, validation, and output creation can later move server-side if needed.

FR21: Manage v1 credential risk by isolating plaintext local credential configuration and preparing a hardening path.

FR22: Preserve audit trail with run IDs, timestamps, operator identity when available, source file references, extraction method, review approval, validation rules version, output paths, and report paths.

FR23: Support local-only processing where AI-assisted extraction and OCR are disabled.

FR24: Authenticate to the configured Innola environment and retain passwords only in ArcGIS Pro session memory.

FR25: Display only transactions available to the logged-in user or their group for the relevant process step.

FR26: Load Innola transaction metadata and attachment metadata/files before enabling the Parcel Workflow.

FR27: Prevent loading a different transaction unless the current process is cancelled or the full Case Folder is saved.

FR28: Complete an Innola transaction only after the required downstream sync/readiness gate passes and only for the user who started or claimed it.

### NonFunctional Requirements

NFR1: Reliability - preserve completed step outputs when later steps fail.

NFR2: Recoverability - allow users to reopen a Case Folder and resume from the latest successful step.

NFR3: Security - v1 may retain plaintext local credential configuration for non-Innola local processing profiles only, but Innola passwords must remain session-only and secrets must not be written into logs, reports, manifests, or standard status views; production deployment should replace plaintext configuration with encrypted or managed credential storage.

NFR4: Auditability - every case must retain source references, processing summaries, review approval status, validation results, and output references.

NFR5: Responsiveness - long-running processing must show progress and must not freeze ArcGIS Pro UI.

NFR6: Maintainability - processing boundaries must support future migration of selected steps to ArcGIS Enterprise services.

NFR7: Usability - trained cadastral technical staff should complete a standard submission without editing scripts or config files.

### Additional Requirements

- Use ArcGIS Pro Module Add-in + Dockpane Item Template + Python toolbox scaffold as the implementation foundation.
- Use ArcGIS Pro SDK add-in architecture, not standalone desktop/web application architecture.
- Use C#/.NET WPF/MVVM for dock pane UI, workflow orchestration, command gating, Case Folder orchestration, and ArcGIS Pro map integration.
- Use Python/ArcPy toolbox or script-tool wrappers for preflight, extraction, validation, output generation, and reporting.
- Use the transaction Case Folder as the system of record.
- Define versioned JSON contracts before implementation stories begin.
- Enforce review-before-output through an explicit workflow state machine.
- Keep CADINDEX sync facade-only in v1.
- Use lowercase snake_case for all JSON contract fields and artifact filenames.
- Required artifacts include `manifest.json`, `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, `process.log`, and `extracted_geometry.geojson`.
- Approval must be tied to a specific review data hash/version; validation must reject stale approval after review data changes.
- C# owns workflow state, command enable/disable rules, source copy, review approval, ArcGIS Pro map integration, and user-facing progress/status.
- Python owns ArcPy-dependent preflight probes, extraction, OCR/AI/local/manual providers, validation/rules, GDB/GeoJSON/report/log generation, and DWG processing.
- Existing Python scripts must be wrapped through adapter/tool entrypoints; C# must not call many legacy scripts directly.
- Workflow states must include no case, intake, preflight running/blocked/passed, extraction running/failed, review pending/approved, validation running/blocked/passed, output running/created, Manual Process routed, and sync readiness.
- Production input profile detection should be system-owned metadata; Case 1-4 remain fixture/test profiles, not production user choices.
- Preflight must verify readiness only and must not create editable review geometry or final output artifacts.
- Validation severity gates must remain deterministic and separate from the deferred 0-10 score.
- Use a no-op/fake CADINDEX Sync Facade implementation for v1 tests and local readiness.
- Implement Case 1-4 as executable fixture contracts with fixture manifests, expected artifacts, expected severity outcomes, recoverability checks, and audit/log assertions.
- Target ArcGIS Pro 3.6/3.7 and choose one compatible SDK/toolchain lane before implementation setup.
- Include local ArcGIS Python environment path in setup/preflight/test stories: `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai`.
- Manage OpenAI/AI usage through profile-based settings: `ProcessingProfileSettings`, `CredentialProfileSettings`, and Python extraction providers. Current config-file approach may remain for v1, but AI must be optional and logs/reports must redact secrets.
- Use `https://eltrs.innola-solutions.com/` as the initial configured Innola environment.
- Gate add-in commands by Innola login state: Login and About remain available when logged out; Transaction Panel and Parcel Workflow require a logged-in session; Parcel Workflow also requires a loaded transaction.
- Store Innola access tokens and passwords only in memory for the current ArcGIS Pro session. Do not persist passwords or tokens to settings, logs, reports, manifests, or Case Folder artifacts.
- Treat Innola transaction metadata and attachment metadata as the normal production source of Case Folder creation and source file intake.
- Preserve the full Case Folder as the local save/resume artifact when the user pauses or does not complete an Innola transaction.
- Expose a Complete action only after required sync/readiness criteria pass; the task can be completed only by the user who started or claimed it.

### UX Design Requirements

UX-DR1: Implement ArcGIS Pro dock pane as the primary workflow container with compact desktop/WPF-style layout.

UX-DR2: Provide persistent workflow navigation for Intake, Preflight, Review, Validation, Outputs, and Sync Readiness.

UX-DR3: Show transaction header with transaction ID, current step, status, and score/status when available.

UX-DR4: Provide Intake file rows with browse/reselect/remove, required/optional state, accepted extensions, copied-to-case-folder result, and source view/open actions.

UX-DR5: In production, show detected input profile rather than exposing Case 1-4 labels as user-facing choices.

UX-DR6: Provide Preflight checklist grouped by blockers and warnings, with concrete correction guidance.

UX-DR7: Provide processing progress states showing current step, elapsed time, cancellable state where available, and last written artifact.

UX-DR8: Provide Extraction Review table with inline edit, add point/data row, mark unresolved, evidence link, confidence indicator, and original-vs-edited value comparison.

UX-DR9: Lock validation and geometry/output generation until review data is approved.

UX-DR10: Provide zero usable extraction state with failed extraction status, Manual Process recommendation, and manual correction option when profile permits.

UX-DR11: Provide Validation findings list grouped by Critical, High, Warning, Info, and Passed.

UX-DR12: Provide Manual Process decision panel that requires explicit user confirmation and records the decision.

UX-DR13: Provide Output artifact list showing GDB, feature classes, GeoJSON, reports, and logs with open/reveal/add-to-map actions where applicable.

UX-DR14: Provide Sync Facade panel showing target CADINDEX reference and result GDB package while clearly stating v1 does not perform live CADINDEX update.

UX-DR15: Support keyboard operation, visible focus rings, non-color-only severity indicators, correct tab order, and full path tooltips for truncated paths.

UX-DR16: Use direct, technical, calm microcopy such as "Preflight blocked: DWG is unreadable" and "Route to Manual Process."

UX-DR17: Apply DESIGN.md tokens for compact ArcGIS Pro-adjacent UI: Segoe UI, light neutral surfaces, restrained primary color, severity colors only for real processing/validation states, tight corners, and dense tables.

UX-DR18: Keep active ArcGIS Pro map as companion surface; add/select/navigate layers rather than embedding a map preview in the dock pane.

UX-DR19: Provide a Sidwell Co add-in shell with Login, Transaction Panel, Configuration, Parcel Workflow, and About commands.

UX-DR20: Disable Transaction Panel and Parcel Workflow when the user is not logged into Innola; disable Parcel Workflow until a transaction is loaded and validated.

UX-DR21: Provide an Innola transaction list with refresh/open controls, filter, search, sorting, and compact task rows showing transaction number, task name, responsible party/group, and received timestamp.

UX-DR22: When a user attempts to load a new transaction while another is active, require Save Case Folder, Cancel Current Process, or Stay on Current Transaction.

### FR Coverage Map

FR1: Epic 1 / Epic 2 - Create or reopen Case Folder from a selected Innola transaction.

FR2: Epic 1 - Support Scenario A source inputs.

FR3: Epic 1 - Support Scenario B source inputs.

FR4: Epic 1 - Preserve resumable case state.

FR5: Epic 2 - Run manifest preflight with blockers and warnings.

FR6: Epic 2 - Validate ArcGIS Pro, ArcPy, Python package, workspace, and write access.

FR7: Epic 2 - Validate DWG readability and inspect CAD sublayers.

FR8: Epic 3 - Run extraction with optional AI/OCR and local/manual profiles.

FR9: Epic 3 - Review extracted parcels, points, segments, metadata, confidence, and evidence.

FR10: Epic 3 - Correct extracted rows or mark them unresolved.

FR11: Epic 3 - Approve review data before validation and output generation.

FR12: Epic 4 - Run cadastral validation rules.

FR13: Epic 4 - Display validation results by severity and status.

FR14: Epic 4 - Route or recommend Manual Process.

FR15: Epic 5 - Create local GDB outputs, feature classes, GeoJSON, summaries, and annotations where possible.

FR16: Epic 5 - Add generated output layers to the active ArcGIS Pro map.

FR17: Epic 5 - Generate HTML, PDF, JSON reports and diagnostic logs without leaking secrets.

FR18: Epic 6 - Maintain publish/sync-ready output state with facade-only CADINDEX readiness.

FR19: Epic 6 - Record Enterprise candidate metadata.

FR20: Epic 2 / Epic 6 - Keep boundaries modular for future server-side migration.

FR21: Epic 2 - Manage v1 credential risk and future hardening path.

FR22: Epic 1 / Epic 5 / Epic 6 - Preserve transaction audit trail across intake, outputs, and readiness.

FR23: Epic 2 / Epic 3 - Support local-only processing with AI/OCR disabled.

FR24: Epic 2 - Authenticate to Innola with session-only credentials.

FR25: Epic 2 - Display available user/group transactions.

FR26: Epic 2 - Load transaction metadata and attachments into the Case Folder.

FR27: Epic 2 - Guard active transaction switching with save/cancel controls.

FR28: Epic 2 / Epic 6 - Complete transaction after sync/readiness gate.

## Epic List

### Epic 1: Local Case Folder & Transaction Workspace Foundation

Cadastral staff can create or reopen a local transaction workspace, copy source files into a structured Case Folder, detect the input profile from file types, and view/open source documents from the dock pane. After the Innola correction, these capabilities are the local foundation used by the normal transaction-driven workflow.

**FRs covered:** FR1, FR2, FR3, FR4, FR22

### Epic 2: Innola Transaction Entry, Preflight Readiness & Processing Setup

Cadastral staff can log into Innola, load an available transaction and its attachments into a local Case Folder, control the active transaction lifecycle, and verify that the transaction is ready before extraction.

**FRs covered:** FR5, FR6, FR7, FR20, FR21, FR23, FR24, FR25, FR26, FR27, FR28

### Epic 3: Extraction & Review Before Geometry

Cadastral staff can run extraction using optional AI/OCR/local/manual profiles, review extracted points/segments/metadata, correct or add data, mark unresolved items, and approve review data before geometry/output generation.

**FRs covered:** FR8, FR9, FR10, FR11, FR23

### Epic 4: Validation & Manual Process Decision

Cadastral staff can validate approved review data using cadastral rules, see findings by severity, and explicitly decide whether to continue correction or route the transaction to Manual Process when automated results are insufficient.

**FRs covered:** FR12, FR13, FR14

### Epic 5: Output Package, Map Integration & Reports

Cadastral staff can generate the local output package, including GDB feature classes, GeoJSON, reports, process logs, optional DWG-derived annotations, and add generated layers to the active ArcGIS Pro map.

**FRs covered:** FR15, FR16, FR17, FR22

### Epic 6: Sync Readiness, Audit Trail & v1 Acceptance Fixtures

Cadastral staff and project stakeholders can confirm the case is ready for future CADINDEX/Enterprise evolution through a no-op Sync Facade, Enterprise candidate metadata, audit records, and executable Case 1-4 acceptance fixtures.

**FRs covered:** FR18, FR19, FR20, FR22

## Epic 1: Local Case Folder & Transaction Workspace Foundation

Goal: Cadastral staff can create or reopen a transaction, copy source files into a structured Case Folder, detect the input profile from file types, and view/open source documents from the dock pane.

### Story 1.1: Set Up ArcGIS Pro Add-in and Processing Scaffold

As a development team member,
I want the ArcGIS Pro add-in, dock pane, Python toolbox scaffold, shared contract folder, and fixture folder structure created,
So that implementation stories have a consistent project foundation aligned with the approved architecture.

**Acceptance Criteria:**

**Given** the selected architecture uses the ArcGIS Pro Module Add-in template, Dockpane item template, and Python toolbox/script-tool scaffold
**When** the initial project scaffold is created
**Then** the repository contains the C# ArcGIS Pro add-in project with module and dock pane entrypoints
**And** the repository contains a Python toolbox or script-tool package location for processing adapters
**And** the repository contains shared JSON schema/contract and example artifact locations
**And** the repository contains fixture folder locations for Case 1, Case 2, Case 3, and Case 4
**And** initial configuration documents the chosen ArcGIS Pro SDK/toolchain lane before feature implementation proceeds.

### Story 1.2: Create Transaction Case Folder

As a cadastral technical staff user,
I want to create a new transaction workspace from the ArcGIS Pro dock pane,
So that all source files, state, logs, and outputs for the transaction are organized under one Case Folder.

**Acceptance Criteria:**

**Given** ArcGIS Pro is open and no active case is loaded
**When** the user enters or confirms a transaction ID and selects an output location
**Then** the add-in creates a Case Folder using the transaction ID
**And** the folder contains the required v1 subfolders for source files, working state, outputs, reports, and logs
**And** a `manifest.json` file is initialized using lowercase snake_case fields
**And** the dock pane moves the case state from `no_case` to `intake`
**And** the transaction header displays transaction ID, current step, and status.

### Story 1.3: Add and Copy Source Files to the Case Folder

As a cadastral technical staff user,
I want to add source files for the transaction and have them copied into the Case Folder,
So that the processing run is based on stable, auditable source inputs.

**Acceptance Criteria:**

**Given** a transaction Case Folder exists in `intake` state
**When** the user adds PDF, DWG, TXT, CSV, TIF, PNG, or JPG source files through the dock pane
**Then** the add-in copies the selected files into the Case Folder source area
**And** the manifest records original path, copied path, file type, file size, timestamp, and source role where known
**And** unsupported file extensions are rejected with a clear message
**And** the source file list shows required/optional status and copied-to-case-folder result
**And** no extraction, validation, or output artifact is created by this story.

### Story 1.4: Detect Source Input Profile

As a cadastral technical staff user,
I want the add-in to detect the transaction input profile from the files provided,
So that production users are not forced to choose test Case 1-4 labels manually.

**Acceptance Criteria:**

**Given** one or more source files have been copied into the Case Folder
**When** the user refreshes or confirms intake
**Then** the add-in detects whether the inputs match Scenario A, Scenario B, incomplete intake, or unsupported intake
**And** the dock pane displays a production-facing detected profile label instead of Case 1-4 fixture names
**And** missing required file roles are shown as intake issues
**And** the detected profile is written to `manifest.json`
**And** Case 1-4 labels remain reserved for fixture/test metadata only.

### Story 1.5: Reopen and Resume Existing Case Folder

As a cadastral technical staff user,
I want to reopen an existing transaction Case Folder,
So that I can resume work after ArcGIS Pro closes or a processing step fails.

**Acceptance Criteria:**

**Given** a Case Folder contains a valid `manifest.json` and workflow state artifacts
**When** the user opens that Case Folder from the dock pane
**Then** the add-in loads the transaction ID, source file list, detected profile, current state, and available artifacts
**And** the dock pane resumes at the latest successful workflow state
**And** missing or damaged required state files are reported as recoverability issues
**And** the user is not required to reselect copied source files when the Case Folder is valid.

### Story 1.6: Open or Reveal Source Files from Intake

As a cadastral technical staff user,
I want to view or reveal copied source files from the dock pane,
So that I can inspect computation files, maps, plans, and DWG references without leaving the transaction context.

**Acceptance Criteria:**

**Given** source files have been copied into the Case Folder
**When** the user selects a source file action
**Then** the add-in can open the file with the system/default viewer or reveal it in the file location
**And** compatible GIS/CAD references can be routed to ArcGIS Pro map/layer workflows where supported
**And** long paths show full-path tooltips when truncated
**And** failed open/reveal actions show a clear non-blocking message
**And** the action is recorded in the transaction audit trail when audit identity is available.

## Epic 2: Innola Transaction Entry, Preflight Readiness & Processing Setup

Goal: Cadastral staff can log into Innola, load an available transaction and its attachments into a local Case Folder, control the active transaction lifecycle, and verify that the transaction is ready before extraction.

### Story 2.1: Run Manifest Preflight

As a cadastral technical staff user,
I want to run preflight against the transaction manifest,
So that I know whether the case has the required source files before extraction begins.

**Acceptance Criteria:**

**Given** a Case Folder exists with copied source files and a detected input profile
**When** the user starts Preflight
**Then** the add-in validates required source roles, file existence, file extensions, and readable copied paths
**And** results are grouped as blockers, warnings, and passed checks
**And** the case state becomes `preflight_running` during execution
**And** the case state becomes `preflight_blocked` when blockers exist
**And** no extraction or output artifacts are created.

### Story 2.2: Add Sidwell Shell, Innola Login, and Session Gating

As a cadastral technical staff user,
I want the add-in to provide a Sidwell Co shell with Innola login and command gating,
So that only authenticated users can access assigned transactions and parcel workflow actions.

**Acceptance Criteria:**

**Given** ArcGIS Pro starts with no active Innola session
**When** the add-in ribbon and dock panes load
**Then** Login and About are enabled
**And** Transaction Panel and Parcel Workflow are disabled
**And** Configuration exposes only safe local preferences to general users
**And** no Innola password or access token is loaded from disk.

**Given** the user opens Login
**When** valid Innola credentials are submitted to the configured environment `https://eltrs.innola-solutions.com/`
**Then** the add-in stores password/token data only in memory for the current ArcGIS Pro session
**And** the logged-in user identity and group context are available to the add-in
**And** Transaction Panel becomes enabled
**And** Parcel Workflow remains disabled until a transaction is selected and loaded
**And** failed login returns a clear non-secret error.

**Given** the user logs out or the session expires
**When** the session state changes to logged out
**Then** Transaction Panel and Parcel Workflow are disabled
**And** in-memory token/password state is cleared
**And** logs, reports, manifests, and status views do not contain secrets.

### Story 2.3: Display Available Innola Transactions

As a cadastral technical staff user,
I want to see the Innola transactions available to me or my group for the current parcel workflow step,
So that I can choose the correct assigned task before creating or reopening the local Case Folder.

**Acceptance Criteria:**

**Given** the user is logged into Innola
**When** the Transaction Panel opens or refreshes
**Then** the add-in requests only transactions available to the logged-in user or their group for the configured parcel workflow step
**And** the list shows transaction number, task name, responsible party or group, received/assigned timestamp, and status where available
**And** the user can filter, search, sort, refresh, and select a transaction
**And** unavailable, completed, or wrong-step transactions are not shown
**And** API failures show a clear retryable error without exposing tokens.

### Story 2.4: Load Transaction Details and Attachments into Case Folder

As a cadastral technical staff user,
I want to load a selected Innola transaction and its attachments into the local Case Folder,
So that the parcel workflow starts from authoritative transaction metadata and stable local source files.

**Acceptance Criteria:**

**Given** the user is logged in and selects an available transaction
**When** the user loads the transaction
**Then** the add-in retrieves transaction metadata, case type/profile metadata, ownership/claim status, and attachment metadata
**And** the attachment metadata includes stable id, file name, extension or MIME type, source role/category where available, size/checksum where available, and download reference
**And** the add-in creates or reopens the Case Folder using the transaction identifier
**And** attachments are downloaded or copied into the Case Folder `source` area
**And** `manifest.json` records Innola transaction metadata, task id, user/group assignment, attachment provenance, and copied source file paths using lowercase snake_case fields
**And** source profile detection runs after attachment load
**And** Parcel Workflow is enabled only after transaction load validation passes.

### Story 2.5: Control Active Transaction Lifecycle and Completion Gate

As a cadastral technical staff user,
I want the add-in to control active transaction ownership, save/cancel behavior, and completion,
So that I cannot accidentally overwrite work or complete a transaction I did not start.

**Acceptance Criteria:**

**Given** a transaction is loaded and active
**When** the user attempts to load another transaction
**Then** the add-in requires one of: save the full Case Folder, cancel the current process, or stay on the current transaction
**And** the selected action is recorded in the local audit trail
**And** a new transaction cannot replace the active transaction without one of those actions.

**Given** the user starts or claims an Innola transaction
**When** the transaction is marked in progress
**Then** the manifest records the started/claimed user identity and timestamp where available
**And** completion is allowed only for the user who started or claimed the transaction
**And** the task remains in progress if the user saves the Case Folder without completing it.

**Given** the parcel workflow reaches the configured downstream sync/readiness gate
**When** the user clicks Complete
**Then** the add-in calls the Innola completion endpoint or service operation
**And** completion is blocked if sync/readiness criteria are not met
**And** completion success/failure is recorded in the Case Folder audit trail.

### Story 2.6: Validate ArcGIS Pro and Python Processing Environment

As a cadastral technical staff user,
I want preflight to verify the ArcGIS Pro and Python processing environment,
So that extraction does not fail later because of missing runtime dependencies.

**Acceptance Criteria:**

**Given** manifest preflight has started
**When** the environment checks run
**Then** the add-in verifies ArcGIS Pro compatibility, ArcPy availability, configured Python environment path, required packages, workspace access, and write access
**And** the configured v1 Python environment path can reference `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai`
**And** missing or incompatible dependencies are reported as blockers or warnings according to configured severity rules
**And** long-running checks show progress without freezing ArcGIS Pro
**And** results are written to `preflight_summary.json`.

### Story 2.7: Validate DWG Readiness When Present

As a cadastral technical staff user,
I want preflight to inspect DWG references when they are part of the transaction,
So that CAD-derived context and annotation expectations are known before extraction.

**Acceptance Criteria:**

**Given** the detected profile includes a DWG source file
**When** preflight reaches CAD validation
**Then** the processing adapter checks whether the DWG is readable by the available ArcGIS/ArcPy tooling
**And** available CAD sublayers are listed where possible
**And** unreadable DWG files are reported as blockers or warnings according to profile rules
**And** absent DWG files do not block Scenario A transactions
**And** the DWG readiness result is included in `preflight_summary.json`.

### Story 2.8: Configure Processing and Credential Profiles

As a cadastral technical staff user or administrator,
I want v1 processing profiles to control AI/OCR/local/manual behavior and credential usage,
So that AI remains optional and local-only processing is always possible.

**Acceptance Criteria:**

**Given** the add-in has access to local v1 configuration
**When** preflight loads processing settings
**Then** it resolves the selected processing profile, including AI enabled/disabled, OCR enabled/disabled, local/manual provider options, and local credential profile reference
**And** local-only mode is valid when AI and OCR are disabled
**And** plaintext credentials remain isolated to the configured v1 location for non-Innola local profiles only
**And** general/server-managed Innola configuration is hidden from general users
**And** secrets are never written to `preflight_summary.json`, reports, or logs
**And** missing AI credentials do not block local-only processing.

### Story 2.9: Display Preflight Results and Gate Extraction

As a cadastral technical staff user,
I want clear preflight results and correction guidance in the dock pane,
So that I know whether I can proceed to extraction or must fix intake/setup issues first.

**Acceptance Criteria:**

**Given** preflight has completed
**When** the dock pane displays results
**Then** blockers, warnings, and passed checks are grouped with visible counts
**And** each blocker includes concrete correction guidance
**And** extraction is disabled while the case state is `preflight_blocked`
**And** extraction is enabled when the case state is `preflight_passed`
**And** the transaction header shows the current state and last written artifact
**And** preflight results can be reopened after closing and reopening the Case Folder.

## Epic 3: Extraction & Review Before Geometry

Goal: Cadastral staff can run extraction using optional AI/OCR/local/manual profiles, review extracted points/segments/metadata, correct or add data, mark unresolved items, and approve review data before geometry/output generation.

### Story 3.1: Run Extraction from Passed Preflight

As a cadastral technical staff user,
I want to run extraction only after preflight passes,
So that extracted review data is generated from a verified transaction setup.

**Acceptance Criteria:**

**Given** a Case Folder is in `preflight_passed` state
**When** the user starts extraction
**Then** the add-in invokes the configured Python/ArcPy processing adapter
**And** extraction uses the resolved processing profile, including AI/OCR/local/manual settings
**And** the case state becomes `extraction_running` during processing
**And** progress shows current step, elapsed time, and last written artifact where available
**And** generated review data is written to `extraction_review_data.json`
**And** no final GDB or GeoJSON output package is generated by this story.

### Story 3.2: Handle Zero or Failed Extraction Results

As a cadastral technical staff user,
I want the add-in to clearly handle zero usable extraction results,
So that I can decide whether to manually capture/correct data instead of continuing an unreliable automated path.

**Acceptance Criteria:**

**Given** extraction has completed or failed
**When** zero usable records are produced or extraction cannot produce review data
**Then** the dock pane shows failed extraction status and a Manual Process recommendation
**And** the case state becomes `extraction_failed` or `review_pending` with zero usable records according to the result type
**And** validation and output generation remain disabled
**And** the user can choose a manual correction/add-data path when the processing profile permits
**And** the result and failure reason are recorded in the audit trail and diagnostic log.

### Story 3.3: Display Extraction Review Table

As a cadastral technical staff user,
I want to review extracted points, segments, parcels, metadata, confidence, and source evidence,
So that I can verify the extracted information before any geometry is generated.

**Acceptance Criteria:**

**Given** `extraction_review_data.json` exists with one or more usable records
**When** the Review step opens
**Then** the dock pane displays extracted records in a compact review table
**And** each row shows required fields, missing-field indicators, confidence/status where available, and source evidence reference where available
**And** rows can be filtered or grouped by issue/status where practical
**And** the active ArcGIS Pro map remains the companion surface rather than embedding a map preview in the dock pane
**And** validation and output actions remain locked until review approval.

### Story 3.4: Edit, Add, or Mark Review Records

As a cadastral technical staff user,
I want to correct extracted values, add missing point/data rows, or mark rows unresolved,
So that the approved review dataset reflects human-reviewed cadastral information.

**Acceptance Criteria:**

**Given** extraction review data is open in `review_pending` state
**When** the user edits a value, adds a row, or marks a row unresolved
**Then** the add-in records the original value and edited value where applicable
**And** required field validation runs on edited and added rows
**And** unresolved rows are visibly marked and excluded or handled according to validation rules
**And** every edit updates the review data version/hash
**And** any previous approval becomes stale after review data changes.

### Story 3.5: Approve Extraction Review Data

As a cadastral technical staff user,
I want to approve the reviewed extraction data only when blockers are cleared,
So that validation and output generation use a specific human-approved dataset.

**Acceptance Criteria:**

**Given** review data exists and required fields are complete or explicitly unresolved according to rules
**When** the user approves the review data
**Then** the add-in writes `approved_review.json` tied to the current review data hash/version
**And** the case state becomes `review_approved`
**And** validation becomes available after approval
**And** validation rejects stale approval if review data changes afterward
**And** approval records timestamp, operator identity when available, extraction method, and source review artifact references.

## Epic 4: Validation & Manual Process Decision

Goal: Cadastral staff can validate approved review data using cadastral rules, see findings by severity, and explicitly decide whether to continue correction or route the transaction to Manual Process when automated results are insufficient.

### Story 4.1: Run Validation on Approved Review Data

As a cadastral technical staff user,
I want validation to run only against approved review data,
So that cadastral rule checks are based on a known human-reviewed dataset.

**Acceptance Criteria:**

**Given** the case state is `review_approved` and `approved_review.json` matches the current review data hash/version
**When** the user starts validation
**Then** the add-in invokes the validation processing adapter using approved review data, source inputs, DWG-derived context where available, and configured validation rules
**And** the case state becomes `validation_running` during execution
**And** stale approval prevents validation and returns the user to Review
**And** validation progress shows current step, elapsed time, and last written artifact where available
**And** validation results are written to `validation_summary.json`.

### Story 4.2: Display Validation Findings by Severity

As a cadastral technical staff user,
I want validation findings grouped by severity and status,
So that I can quickly understand what must be fixed before output generation.

**Acceptance Criteria:**

**Given** `validation_summary.json` exists
**When** the Validation step opens
**Then** findings are grouped by Critical, High, Warning, Info, and Passed
**And** visible counts are shown for each group
**And** each finding includes rule identifier, message, affected record or geometry reference where available, and suggested correction where available
**And** severity is shown using both text/icon and color, not color alone
**And** validation results can be reopened after closing and reopening the Case Folder.

### Story 4.3: Gate Output Generation Based on Validation Result

As a cadastral technical staff user,
I want the add-in to clearly decide whether output generation is allowed, blocked, or requires review,
So that only acceptable transactions proceed to local output creation.

**Acceptance Criteria:**

**Given** validation has completed
**When** validation results contain critical blockers or zero usable approved records
**Then** output generation remains disabled
**And** the case state becomes `validation_blocked`
**And** the dock pane displays why output generation is blocked
**And** lower-severity warnings do not automatically block output unless configured rules require it
**And** when validation passes configured gates, the case state becomes `validation_passed` and output generation becomes available.

### Story 4.4: Route Case to Manual Process by User Decision

As a cadastral technical staff user,
I want to explicitly route a transaction to Manual Process when automated extraction or validation is insufficient,
So that manual COGO/correction work is treated as a legitimate documented outcome.

**Acceptance Criteria:**

**Given** extraction or validation recommends Manual Process, or the user chooses Manual Process from an eligible state
**When** the user confirms the Manual Process decision
**Then** the add-in records the decision, reason, timestamp, operator identity when available, and current case state
**And** the case state becomes `manual_process_routed`
**And** the dock pane shows the case as routed to Manual Process rather than failed silently
**And** automated output generation remains disabled unless the case returns to review/validation through an explicit correction path
**And** the decision is included in the audit trail and later reports.

### Story 4.5: Return from Validation to Review for Corrections

As a cadastral technical staff user,
I want to return from validation findings to the review data that needs correction,
So that I can fix issues and rerun validation without restarting the transaction.

**Acceptance Criteria:**

**Given** validation findings exist and the case is not finalized
**When** the user chooses to correct review data from a validation finding
**Then** the add-in navigates back to the relevant review record where possible
**And** any edit to review data invalidates the prior approval and validation result
**And** the case state returns to `review_pending`
**And** the user must approve the updated review data before rerunning validation
**And** previous validation artifacts remain available for audit history.

## Epic 5: Output Package, Map Integration & Reports

Goal: Cadastral staff can generate the local output package, including GDB feature classes, GeoJSON, reports, process logs, optional DWG-derived annotations, and add generated layers to the active ArcGIS Pro map.

### Story 5.1: Generate Local Output Package from Passed Validation

As a cadastral technical staff user,
I want to generate outputs only after validation passes,
So that local geodatabase and exchange files are created from approved and validated review data.

**Acceptance Criteria:**

**Given** the case state is `validation_passed`
**When** the user starts output generation
**Then** the add-in invokes the output generation processing adapter
**And** the case state becomes `output_running` during processing
**And** outputs are generated inside the transaction Case Folder output area
**And** output generation uses the approved review data and validation summary for traceability
**And** output generation progress shows current step, elapsed time, and last written artifact where available.

### Story 5.2: Create GDB Feature Classes and GeoJSON

As a cadastral technical staff user,
I want generated point and line outputs in local GDB and GeoJSON formats,
So that the transaction result can be reviewed in ArcGIS Pro and exchanged with downstream workflows.

**Acceptance Criteria:**

**Given** output generation is running with valid approved review data
**When** geometry artifacts are created
**Then** the output package includes a local `.gdb` containing extracted point and line feature classes
**And** annotation feature classes are included where DWG-derived annotation can be converted by the processing tools
**And** `extracted_geometry.geojson` contains extracted point and line data suitable for exchange/review
**And** generated artifact paths and feature counts are recorded in `output_summary.json`
**And** annotation output is marked best-effort when unavailable.

### Story 5.3: Generate Reports and Diagnostic Logs

As a cadastral technical staff user,
I want case reports and logs generated with the output package,
So that the transaction has a readable execution summary and machine-readable audit record.

**Acceptance Criteria:**

**Given** output generation has access to manifest, preflight, extraction, approval, validation, and output metadata
**When** reporting runs
**Then** the Case Folder includes HTML, PDF, and JSON reports
**And** reports include source file summary, extraction counts, validation results, output counts, processing duration, manual-process status if applicable, and output paths
**And** `process.log` captures diagnostic execution details
**And** reports and logs do not expose plaintext credentials or API keys
**And** report paths are recorded in `output_summary.json`.

### Story 5.4: Display Output Artifact List

As a cadastral technical staff user,
I want the dock pane to show generated output artifacts with available actions,
So that I can open, reveal, or add results to the map without searching the folder manually.

**Acceptance Criteria:**

**Given** output generation has completed
**When** the Outputs step opens
**Then** the dock pane lists the result GDB, feature classes, GeoJSON, HTML report, PDF report, JSON report, and process log where present
**And** each artifact shows status, path, and available actions
**And** missing optional artifacts are shown as unavailable rather than failed when marked best-effort
**And** the case state becomes `output_created` after required outputs are present
**And** output artifact status can be reopened after closing and reopening the Case Folder.

### Story 5.5: Add Generated Layers to Active ArcGIS Pro Map

As a cadastral technical staff user,
I want to add generated output layers to the active ArcGIS Pro map,
So that I can visually inspect transaction results in the normal ArcGIS Pro workspace.

**Acceptance Criteria:**

**Given** output GDB feature classes exist
**When** the user chooses Add to Map for one or more generated layers
**Then** the add-in adds the selected layers to the active ArcGIS Pro map
**And** the dock pane reports success or a clear non-blocking failure
**And** the map remains the companion surface rather than embedding a preview in the dock pane
**And** added-layer actions are recorded in the audit trail when audit identity is available
**And** the output artifacts remain unchanged by the map-add action.

## Epic 6: Sync Readiness, Audit Trail & v1 Acceptance Fixtures

Goal: Cadastral staff and project stakeholders can confirm the case is ready for future CADINDEX/Enterprise evolution through a no-op Sync Facade, Enterprise candidate metadata, audit records, and executable Case 1-4 acceptance fixtures.

### Story 6.1: Display CADINDEX Sync Readiness Facade

As a cadastral technical staff user,
I want to see the intended CADINDEX sync target and local result package readiness,
So that v1 clearly prepares for future Enterprise sync without performing live CADINDEX updates.

**Acceptance Criteria:**

**Given** output artifacts exist or a case has been routed to Manual Process
**When** the Sync Readiness step opens
**Then** the dock pane shows the intended CADINDEX target reference from configuration where available
**And** the result GDB path and output summary path are shown when available
**And** the panel clearly states that v1 does not perform live CADINDEX updates
**And** no ArcGIS Enterprise/CADINDEX write operation is executed
**And** the case state can become `sync_ready` only as a local readiness status.

### Story 6.2: Record Enterprise Candidate Metadata

As a project stakeholder,
I want each transaction to record metadata useful for future Enterprise-hosted processing,
So that v1 outputs can inform later ArcGIS Enterprise, Notebook, or GP Service evolution.

**Acceptance Criteria:**

**Given** a case has reached output creation, manual routing, or terminal failure
**When** the sync readiness metadata is generated
**Then** metadata includes run ID, transaction ID, processing duration, dependency versions where available, source profile, extraction method, output counts, failure types, manual-process status, output GDB path, and intended CADINDEX target reference where available
**And** metadata is written using lowercase snake_case fields
**And** missing optional Enterprise configuration is recorded as unavailable rather than failed
**And** metadata does not contain plaintext credentials or API keys.

### Story 6.3: Preserve End-to-End Audit Trail

As a cadastral supervisor or approver,
I want the transaction to retain a complete audit trail,
So that the Cadastral Director and technical staff can review what happened during processing.

**Acceptance Criteria:**

**Given** the user performs intake, preflight, extraction, review approval, validation, manual routing, output generation, or sync readiness actions
**When** the case artifacts are updated
**Then** the audit trail records run IDs, timestamps, operator identity when available, source file references, extraction method, review approval, validation rules version, output paths, report paths, and manual-process decision where applicable
**And** audit records survive ArcGIS Pro restart through Case Folder state
**And** reports summarize the audit trail at a readable level
**And** diagnostic logs retain enough detail to troubleshoot processing failures
**And** secrets are redacted from audit and log artifacts.

### Story 6.4: Create Case 1-4 Acceptance Fixture Contracts

As a project stakeholder,
I want representative Case 1-4 fixture contracts,
So that v1 can be tested against bad-quality and good-quality scenarios with and without TXT/CSV/DWG inputs.

**Acceptance Criteria:**

**Given** the project includes fixture folders for Case 1, Case 2, Case 3, and Case 4
**When** fixture manifests are assembled
**Then** Case 1 represents bad-quality scanned computation PDF plus bad-quality scanned parcel map PDF
**And** Case 2 represents good-quality scanned computation PDF plus good-quality scanned parcel map PDF
**And** Case 3 represents bad-quality scanned computation PDF plus TXT/CSV points, scanned parcel map PDF, and DWG
**And** Case 4 represents good-quality scanned computation PDF plus TXT/CSV points, scanned parcel map PDF, and DWG
**And** each fixture contract defines expected source roles, expected artifacts, expected severity outcome, and minimum audit/log assertions.

### Story 6.5: Validate Fixture Runs and Recovery Behavior

As a project stakeholder,
I want fixture runs to verify workflow behavior and recovery,
So that v1 acceptance proves the integrated process works across the required scenarios.

**Acceptance Criteria:**

**Given** Case 1-4 fixture contracts exist
**When** the fixture validation routine runs
**Then** each fixture verifies expected artifacts such as JSON extraction/report data, GeoJSON, GDB, and process log according to fixture expectations
**And** bad-quality cases may pass with limited extraction or Manual Process recommendation when expected
**And** good-quality cases must demonstrate usable extracted/reviewable records when source data supports it
**And** recovery checks confirm completed step outputs remain available after simulated restart/reopen
**And** fixture results are summarized for v1 acceptance review.

## Appendix: Implementation & Testability Contract

This appendix is binding for all implementation stories. It preserves the approved user-value backlog while defining the shared contracts needed to make the ArcGIS Pro add-in, Python/ArcPy processing wrappers, Case Folder artifacts, and QA fixtures consistent.

### Case Folder Contract

Each transaction Case Folder is the system of record for v1 and must contain stable, restartable artifacts. The v1 folder layout must include source files, working state, outputs, reports, logs, and fixture/test metadata when applicable. The required canonical artifacts are `manifest.json`, `preflight_summary.json`, `extraction_review_data.json`, `approved_review.json`, `validation_summary.json`, `output_summary.json`, `process.log`, and `extracted_geometry.geojson`.

All JSON artifacts must use lowercase snake_case fields, include a schema/version field, and include enough path metadata to distinguish original source path from copied Case Folder path. Implementations must define duplicate transaction behavior, missing artifact behavior, corrupt JSON behavior, and recovery behavior before a story is marked done.

### Workflow State Machine and Gate Rules

The workflow state machine must be explicit and persisted in the Case Folder. Required states include `no_case`, `intake`, `preflight_running`, `preflight_blocked`, `preflight_passed`, `extraction_running`, `extraction_failed`, `review_pending`, `review_approved`, `validation_running`, `validation_blocked`, `validation_passed`, `output_running`, `output_created`, `manual_process_routed`, and `sync_ready`.

Invalid transitions must be blocked by the add-in. Extraction requires `preflight_passed`; validation requires `review_approved`; output generation requires `validation_passed`; sync readiness in v1 is local/facade-only and must not imply external update success. Reruns must define which downstream artifacts become stale or invalidated.

### Review Approval Hash and Stale Approval Rule

`approved_review.json` must be tied to the exact `extraction_review_data.json` version/hash approved by the user. The hash basis must include geometry-affecting extracted values, edited values, unresolved flags, source evidence references, processing profile identifier, extraction method, and review data schema version.

Any edit, added row, unresolved-status change, source evidence change, extraction rerun, or relevant processing profile change must invalidate prior approval. Validation must reject stale approval and return the case to `review_pending`.

### Python/ArcPy Wrapper Contract

C# must call processing through stable adapter/tool entrypoints rather than many legacy scripts directly. Each wrapper must define input JSON path, output JSON path, expected artifacts, exit codes, error envelope, log path, timeout/cancellation behavior, and exception-to-severity mapping.

Wrapper outputs must be deterministic enough for fixture validation. Failed wrappers must return structured failure data where possible and must not leave the dock pane frozen. Partial artifacts must be either explicitly resumable or explicitly marked stale/failed.

### AI, OCR, and Local-Only Modes

AI and OCR are optional throughout v1. Local-only mode must be valid when AI and OCR are disabled. Missing AI credentials must not block local-only processing. No external AI/OCR call may be attempted when the selected processing profile disables that provider.

Plaintext credential configuration is allowed only as a v1 constraint and must remain isolated to the configured local location. Logs, reports, audit artifacts, fixture summaries, and crash/error traces must redact API keys, bearer tokens, connection strings, UNC credentials, and other sensitive-looking values.

### Manual Process Contract

Manual Process routing is a legitimate transaction outcome, not only a failure branch. Manual routing must record a reason code, human-readable explanation, timestamp, operator identity when available, source state, and downstream output restrictions.

When a case is routed to Manual Process, automated output generation remains blocked unless the user explicitly returns to review/correction and re-enters the approved validation path. Manual decision artifacts must appear in reports and audit summaries.

### CADINDEX Sync Facade Contract

v1 must not perform live CADINDEX or ArcGIS Enterprise updates. The Sync Facade must be a no-op/local readiness surface that records intended target reference, result GDB path, output summary path, readiness status, and unavailable configuration where applicable.

Acceptance tests must prove no production CADINDEX mutation path is executed in v1, no Enterprise write credential is required for v1 sync readiness, and the UI never labels facade status as a completed external sync.

### Recovery and Idempotency Expectations

Every state-changing story must define behavior after ArcGIS Pro restart, interrupted processing, deleted or corrupt artifacts, duplicate transaction IDs, partial output generation, rerun validation, and reopened cases where applicable.

Completed step artifacts must remain available for audit even when later steps fail. Rerunning a step must either create a new run record or clearly replace the current artifact while preserving enough history to explain the transition.

### Fixture Matrix and Definition of Testable Done

Case 1-4 fixtures must cover the four approved baseline scenarios and include variants for missing files, malformed PDFs/images, unreadable DWG, projection or coordinate mismatch, locked workspace/GDB, invalid geometry, duplicate parcel identifiers, partial extraction, stale approval, manual routing, wrapper failure, output regeneration, long paths, unusual filenames, path traversal attempts, and secret-redaction probes.

Golden outputs must normalize nondeterministic values such as timestamps and run IDs. For each story that touches workflow state, validation, outputs, audit, wrappers, or reports, done means the story identifies the fixture or test double used, expected artifacts, failure mode, restart expectation, and audit/log expectation.

Acceptance criteria should be testable by implementation layer where practical: UI, ViewModel, wrapper, filesystem, audit, fixture, and ArcGIS Pro map integration.
