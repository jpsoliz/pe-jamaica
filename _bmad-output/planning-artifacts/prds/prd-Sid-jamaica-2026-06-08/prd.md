---
title: "ArcGIS Pro Parcel Workflow Add-in PRD"
status: final
created: 2026-06-08
updated: 2026-06-08
---

# PRD: ArcGIS Pro Parcel Workflow Add-in

*Working title: ArcGIS Pro Parcel Workflow Add-in. Confirm final product name.*

## 0. Document Purpose

This PRD defines v1 requirements for an ArcGIS Pro add-in that helps NLA Cadastral Office technical staff and cadastral officials process parcel computation inputs, reference plans/maps, DWG files, point files, validation checks, and parcel output creation through a guided workflow. It is written for product stakeholders, GIS technical leadership, UX/design, architecture, and implementation planning. Requirements are grouped by capability, functional requirements use stable IDs, assumptions are tagged inline, and deeper architecture considerations are captured in `addendum.md`.

Primary input: `_bmad-output/planning-artifacts/research/technical-arcgis-pro-addin-parcel-workflow-research-2026-06-08.md`.

## 1. Vision

NLA cadastral staff need a reliable way to turn mixed survey and plan submission materials into reviewable, validated, GIS-ready parcel outputs without manually coordinating scripts, configuration files, CAD layers, extracted tables, and ArcGIS project outputs. The v1 product should make that workflow feel like a coherent ArcGIS Pro experience: choose the submission sources, run preflight, extract and review evidence, validate the submission, create parcel outputs, and preserve a traceable case record.

The v1 product is **ArcGIS Pro add-in first**. ArcGIS Pro remains the primary operator surface because cadastral officials need map context, DWG/CAD reference layers, coordinate systems, file geodatabases, parcel/COGO review, and final GIS output inspection. The product should also keep an Enterprise evolution path open: data may later be published or synced to ArcGIS Enterprise, and selected processing steps may later move to Web Tools, geoprocessing services, or Notebook-backed services.

Success means the mentioned processes are integrated into one guided operational flow: source intake, extraction, review, validation, parcel/output creation, reporting, and future-ready publish/sync handoff. The goal is not merely to wrap scripts with buttons; it is to create a trusted cadastral workflow where human review and audit evidence are explicit.

## 2. Target User

### 2.1 Jobs To Be Done

- As a cadastral technical staff member, I need to ingest computation sheets, plans/maps, point files, and DWG references so that I can begin a parcel review without hand-editing configuration files.
- As a cadastral official, I need to review extracted parcel evidence before GIS outputs are created so that automated extraction errors do not become official map data.
- As a cadastral technical staff member, I need validation results tied to source evidence so that I can identify missing data, geometry issues, coordinate problems, or rule failures.
- As a GIS operator, I need output layers, reports, logs, and case files organized by submission so that I can inspect results, recover from failures, and support future audits.
- As a technical lead, I need a v1 architecture that starts locally in ArcGIS Pro but can evolve toward ArcGIS Enterprise services and publish/sync workflows.

### 2.2 Non-Users (v1)

- Field surveyors submitting source files directly through a public portal.
- Non-GIS clerical staff performing independent review outside ArcGIS Pro.
- Public users or applicants tracking submission status.
- Enterprise administrators managing server infrastructure.
- Fully automated cadastral approval workflows without human review.

### 2.3 Key User Journeys

- **UJ-1. Nadia creates a new cadastral submission case from mixed source files.**
  - **Persona + context:** Nadia is an NLA cadastral technical staff member reviewing a submitted plan package.
  - **Entry state:** Nadia is working in ArcGIS Pro with the add-in installed and has access to the submitted PDF/TIF/PNG/JPG/DWG/TXT/CSV files.
  - **Path:** She opens the dock pane, starts a new submission, selects the source type, drops in the computation/points file, plan/map PDF, and optional DWG reference, then chooses the coordinate system/profile.
  - **Climax:** The add-in creates a case folder and shows preflight status for all required inputs.
  - **Resolution:** Nadia can continue to extraction only after blocking input issues are resolved.
  - **Edge case:** If the DWG is unreadable or the points file is missing required fields, the add-in blocks the run and shows what must be corrected.

- **UJ-2. Nadia reviews extracted parcels and approves evidence before output creation.**
  - **Persona + context:** Nadia needs to trust the extracted parcel data before it becomes GIS output.
  - **Entry state:** Preflight has passed and extraction has produced review data.
  - **Path:** Nadia opens the extraction review step, sees parcels, points, segments, bearings/distances, metadata, confidence flags, and missing fields. She edits or rejects incorrect extraction rows.
  - **Climax:** Nadia approves the reviewed extraction set.
  - **Resolution:** The add-in records approval and enables validation and output creation.
  - **Edge case:** If confidence is low or required fields are missing, the add-in marks the submission as needing manual review before proceeding.

- **UJ-3. Nadia validates a submission and creates local GIS outputs.**
  - **Persona + context:** Nadia needs GIS-ready parcel outputs and a defensible validation trail.
  - **Entry state:** Extraction review has been approved.
  - **Path:** Nadia runs validation, reviews rule results by severity, addresses blockers if needed, then runs output creation.
  - **Climax:** The add-in creates a file geodatabase, output layers, annotations/labels, reports, logs, and adds relevant layers to ArcGIS Pro.
  - **Resolution:** Nadia has a local case package ready for technical review and later publish/sync.
  - **Edge case:** If validation finds a critical geometry or coordinate error, automated output completion is disabled and Nadia must explicitly route the case to Manual Process through manual ArcGIS Pro/COGO handling.

- **UJ-4. A technical lead evaluates Enterprise evolution candidates.**
  - **Persona + context:** A GIS technical lead wants to decide what can later run in ArcGIS Enterprise.
  - **Entry state:** v1 local workflow is producing case-folder outputs.
  - **Path:** The lead reviews logs, processing times, dependency diagnostics, and output summaries to identify candidates for Web Tools, geoprocessing services, Notebook-backed jobs, or publish/sync automation.
  - **Climax:** The team can make evidence-based decisions about what moves server-side.
  - **Resolution:** Enterprise enhancements are planned without destabilizing the v1 add-in flow.

## 3. Glossary

- **Add-in** — The ArcGIS Pro custom extension providing the user interface and workflow orchestration for v1.
- **ArcGIS Enterprise** — The organization server/portal environment that may host data, services, Web Tools, geoprocessing services, or Notebook-backed processing in later phases.
- **Approved Review Data** — Extraction Review Data that a cadastral user has reviewed and approved for validation and output creation.
- **Case Folder** — A local or shared-drive folder for one transaction containing source copies, manifest, review data, validation results, logs, reports, and output summaries.
- **Computation File** — A PDF, TIF, PNG, or JPG containing parcel computation information such as coordinates, bearings, distances, segments, or tabular survey evidence.
- **DWG Reference** — A CAD drawing used as spatial/reference context, including annotation, point, polyline, or polygon information where available.
- **Extraction Review Data** — Structured data produced from source inputs, including parcels, points, segments, metadata, confidence, missing fields, and source evidence.
- **Manifest** — A machine-readable case configuration file generated by the add-in, replacing user-facing INI editing.
- **Manual Process** — A user-driven cadastral workflow, primarily COGO/manual ArcGIS Pro work, used when automated extraction or validation is not sufficiently reliable.
- **Plan/Map Reference** — A PDF, TIF, PNG, or JPG map/plan used as visual or spatial reference for the submission.
- **Preflight** — The validation step that checks inputs, file availability, readable formats, coordinate profile, dependencies, write access, and required case metadata before extraction.
- **Publish/Sync** — A future workflow that moves or synchronizes approved local outputs to configured ArcGIS Enterprise layers.
- **Sync Facade** — A v1 placeholder interface or metadata boundary that records what would be needed for future ArcGIS Enterprise sync/update without performing live sync.
- **Test Case** — A representative transaction fixture used to prove v1 works. v1 requires at least Case 1 through Case 4.
- **Transaction** — The organizing unit for a cadastral submission. v1 uses the current transaction format `TR-SMD-0000001` until a final transaction-number standard is defined.
- **Validation Summary** — Structured rule results with severity, status, evidence, and counts.
- **v1** — The first operational product version. It is broader than a throwaway MVP but still intentionally limits Enterprise automation to future-ready hooks and selected exploration.

## 4. Features

### 4.1 Guided Submission Intake

**Description:** The Add-in provides a dock-pane workflow for creating a Case Folder and selecting source files. Users should not manually edit INI files or run scripts. The intake step supports the two known source scenarios and preserves enough metadata for downstream processing. Realizes UJ-1.

**Functional Requirements:**

#### FR-1: Create New Submission Case

Cadastral technical staff can create a new Case Folder from the Add-in by entering or confirming a submission identifier, source scenario, coordinate/profile settings, and output location. Realizes UJ-1.

**Consequences (testable):**
- A new transaction folder is created with a generated Manifest.
- The transaction folder name supports the v1 format `TR-SMD-0000001`.
- The Manifest records transaction identifier, source scenario, selected files, coordinate/profile settings, created timestamp, and operator identity when available. [ASSUMPTION: operator identity can be derived from the Windows or ArcGIS Pro user context.]
- The user can reopen the Case Folder from the Add-in.

#### FR-2: Select Source Scenario A

Cadastral technical staff can create a submission using Source Scenario A: a Computation File in PDF/TIF/PNG/JPG format containing parcel coordinates or computation evidence, plus a Plan/Map Reference in PDF/TIF/PNG/JPG format. Realizes UJ-1.

**Consequences (testable):**
- The Add-in accepts PDF, TIF, TIFF, PNG, JPG, and JPEG extensions for the Computation File.
- The Add-in requires a Plan/Map Reference before preflight can pass.
- The Manifest records the selected files and source scenario.

#### FR-3: Select Source Scenario B

Cadastral technical staff can create a submission using Source Scenario B: a points/computation file in PDF/TXT/CSV format, a DWG Reference, and a Plan/Map Reference in PDF/TIF/PNG/JPG format. Realizes UJ-1.

**Consequences (testable):**
- The Add-in accepts PDF, TXT, and CSV extensions for the points/computation source.
- The Add-in requires a DWG Reference and Plan/Map Reference before preflight can pass.
- The Manifest records selected files and source scenario.

#### FR-4: Preserve Case State

The Add-in stores submission state in the Case Folder so that work can resume after ArcGIS Pro closes or a processing step fails. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- The Case Folder includes Manifest, step status, output paths, and last successful artifact references.
- The Case Folder includes copied source files in a `source` folder and generated artifacts in an `output` folder.
- The output folder includes, at minimum, a JSON extraction/process report, GeoJSON extracted geometry, result file geodatabase, and process log when the run reaches the relevant processing steps.
- The Case Folder can be stored locally for v1 and should remain compatible with later shared-drive storage.
- A reopened case shows the latest completed step.
- Failed steps preserve diagnostic output instead of discarding prior successful outputs.

### 4.2 Preflight and Dependency Checks

**Description:** Preflight confirms the submission is ready for extraction and processing. It catches missing files, unreadable DWG references, unsupported extensions, missing write access, dependency issues, and coordinate/profile gaps before long-running processing starts. Realizes UJ-1.

**Functional Requirements:**

#### FR-5: Validate Required Inputs

The Add-in can run Preflight against the Manifest and show blocking and non-blocking issues. Realizes UJ-1.

**Consequences (testable):**
- Missing required files produce blocking errors.
- Unsupported extensions produce blocking errors.
- Empty or inaccessible files produce blocking errors.
- Non-blocking warnings are visually distinct from blocking errors.

#### FR-6: Validate GIS and Processing Environment

The Add-in can check that the required ArcGIS Pro, ArcPy, Python package, workspace, and write-access conditions are available before processing. Realizes UJ-1.

**Consequences (testable):**
- Missing required dependencies are reported before extraction.
- Missing write access to the Case Folder or output workspace blocks processing.
- Target ArcGIS Pro version, ArcGIS Enterprise version, and Python environment are recorded in the diagnostic output.

#### FR-7: Validate DWG Reference Readability

For submissions with a DWG Reference, Preflight can confirm that ArcGIS Pro/ArcPy can inspect required CAD sublayers where available. Realizes UJ-1.

**Consequences (testable):**
- Unreadable DWG files produce blocking errors.
- Missing expected sublayers produce warnings or errors according to the selected profile. [ASSUMPTION: exact required DWG sublayers will be profile-configurable.]
- Preflight records available CAD sublayers and counts when available.

### 4.3 Extraction and Human Review

**Description:** The product extracts parcel evidence from source files, then presents reviewable data before validation and output creation. Automated extraction, including OCR or AI-assisted extraction, must be treated as proposed evidence requiring user review. Realizes UJ-2.

**Functional Requirements:**

#### FR-8: Run Extraction with Optional AI/OCR

Cadastral technical staff can run extraction from the Add-in after Preflight passes. Realizes UJ-2.

**Consequences (testable):**
- Extraction produces Extraction Review Data in the Case Folder.
- Extraction records source files, extraction method, run timestamp, confidence where available, and warnings.
- Extraction does not create final parcel outputs until review is approved.
- AI-assisted extraction and OCR are optional profiles that can be enabled or disabled during the workflow.
- The user can proceed with local/manual extraction or Manual Process when AI/OCR is disabled or unsuitable.
- If extraction produces zero usable extracted records, the extraction step is marked failed.
- If extraction produces one or more usable extracted records, the user can continue to review and manual adjustment even when the extraction is incomplete.
- The review step must allow the user to add or correct points/data when automated extraction is incomplete.

#### FR-9: Review Extracted Parcels, Points, and Segments

Cadastral technical staff can review extracted parcels, points, segments, metadata, missing fields, and confidence indicators. Realizes UJ-2.

**Consequences (testable):**
- The review view displays extracted rows grouped by parcel or source section where applicable.
- Missing required fields are visually flagged.
- Low-confidence extraction results are visually flagged.
- The user can identify which source file/page/row produced a reviewed item when source evidence exists.

#### FR-10: Edit or Mark Extraction Issues

Cadastral technical staff can correct extracted values or mark extraction rows as unresolved before approval. Realizes UJ-2.

**Consequences (testable):**
- Edited values are saved to Extraction Review Data.
- Original extracted values remain available for audit comparison. [ASSUMPTION: v1 preserves original and edited values for fields edited by users.]
- Unresolved required values prevent approval.

#### FR-11: Approve Review Data

Cadastral technical staff can approve Extraction Review Data when required values are present and unresolved blockers are cleared. Realizes UJ-2.

**Consequences (testable):**
- Approval writes an Approved Review Data marker or status to the Case Folder.
- Approval records timestamp and operator identity when available.
- Validation and output creation remain disabled until review approval exists.

### 4.4 Validation and Rule Results

**Description:** The product validates Approved Review Data and source context before output creation. Validation should communicate severity, evidence, and required remediation. Realizes UJ-3.

**Functional Requirements:**

#### FR-12: Run Cadastral Validation Rules

Cadastral technical staff can run validation against Approved Review Data, source inputs, DWG-derived context, and configured rules. Realizes UJ-3.

**Consequences (testable):**
- Validation produces a Validation Summary in the Case Folder.
- Validation Summary includes rule ID, title, severity, status, and evidence.
- Validation records the rules profile/version used.

#### FR-13: Display Validation Results by Severity

The Add-in displays validation results grouped by severity and status so users can focus on blockers first. Realizes UJ-3.

**Consequences (testable):**
- Critical and high-severity failures are surfaced before lower-severity findings.
- Users can open evidence/details for each finding.
- Counts by severity/status are visible.

#### FR-14: Route Critical Failures to Manual Process

The product recommends or routes a case to Manual Process when automated validation falls below the configured success threshold or when the user decides the automated result is insufficient. Realizes UJ-3.

**Consequences (testable):**
- If extraction or validation produces zero usable results, the case is marked failed and the Add-in recommends Manual Process.
- If extraction produces one or more usable results but remains incomplete, the case can continue to review/manual adjustment.
- The review/manual adjustment path must allow the user to add or correct points/data before deciding how to continue.
- The 20% threshold remains a configurable warning/manual-review signal when an expected total count is known.
- Critical status disables automated output completion.
- The user must make an explicit decision to continue through Manual Process.
- The Add-in explains that Manual Process should continue through manual ArcGIS Pro/COGO handling.
- The Validation Summary records extracted counts, known expected counts where available, success percentage when computable, recommendation, and user decision.

### 4.5 Parcel Output Creation and ArcGIS Pro Map Integration

**Description:** After review approval and validation, the product creates local GIS outputs, reports, annotations/labels where applicable, and adds relevant layers to ArcGIS Pro for inspection. Realizes UJ-3.

**Functional Requirements:**

#### FR-15: Create Local File Geodatabase Outputs

Cadastral technical staff can create local file geodatabase outputs from Approved Review Data and source context. Realizes UJ-3.

**Consequences (testable):**
- Output creation writes feature classes to the configured output geodatabase.
- Output creation includes extracted point feature classes, line feature classes, and annotation feature classes where possible.
- Output creation also writes a GeoJSON file containing extracted points and lines where the extracted geometry is available.
- DWG-derived layers should preserve the current script-supported imports where available: `dwg_annotation`, `dwg_point`, `dwg_polyline`, `dwg_multipoint`, and `dwg_parcels`.
- Generated annotation outputs should preserve `parcel_segment_annotation` where available.
- Output Summary records generated feature class paths and counts.
- Output creation logs warnings and errors to the Case Folder.

#### FR-16: Add Outputs to ArcGIS Pro Map

The Add-in can add generated output layers to the active ArcGIS Pro project or map. Realizes UJ-3.

**Consequences (testable):**
- Generated layers are added to the map when the user chooses to add them.
- Existing output layers from the same Case Folder can be replaced or preserved according to user choice. [ASSUMPTION: default behavior is replace prior layers from the same run.]
- The user can navigate from the Add-in to generated outputs and reports.

#### FR-17: Generate Reports and Logs

The product generates human-readable reports and diagnostic logs for each case. Realizes UJ-3.

**Consequences (testable):**
- The Case Folder includes report output and processing logs.
- The report references validation counts, output summaries, number of detected/extracted points, number of detected/extracted lines, annotation output where possible, and overall solution success percentage.
- Reports are generated in HTML, PDF, and JSON formats.
- The JSON report covers the extraction process, OCR process when used, and geodatabase generation status.
- Logs must not include raw secrets or API keys.

#### FR-18: Support Publish/Sync-Ready Output State

The product prepares local outputs and summaries so that a future publish/sync workflow to ArcGIS Enterprise can be added without redesigning case state. v1 treats CADINDEX sync/update as a Sync Facade. Realizes UJ-4.

**Consequences (testable):**
- Output Summary records enough metadata for future publish/sync, including output paths, feature classes, coordinate system/profile, and run status.
- Publish/sync metadata can reference the CADINDEX layer in ArcGIS Enterprise.
- v1 records sync facade metadata but does not perform live CADINDEX updates.
- Sync Facade metadata records that the generated result geodatabase and its contents are the future sync package for final CADINDEX layers.
- v1 does not require automatic publish/sync to ArcGIS Enterprise.
- Enterprise publish/sync is tracked as a future enhancement, not a blocking v1 requirement.

### 4.6 Enterprise Evolution Readiness

**Description:** v1 is Add-in first, but it must preserve a credible path toward ArcGIS Enterprise data/services. The product should identify processing and publishing boundaries without committing to full server-side execution in v1. Realizes UJ-4.

**Functional Requirements:**

#### FR-19: Record Enterprise Candidate Metadata

The system records processing duration, dependency information, output sizes/counts, failure types, and Sync Facade metadata so the team can identify future candidates for ArcGIS Enterprise Web Tools, geoprocessing services, Notebook-backed jobs, or CADINDEX publish/sync. Realizes UJ-4.

**Consequences (testable):**
- Output Summary includes processing durations by step.
- Diagnostics include local dependency/environment information.
- Failures are categorized by input, extraction, validation, output, or environment.
- Output Summary records that CADINDEX sync/update is facade-only in v1.

#### FR-20: Keep Processing Boundaries Modular

The v1 processing pipeline is organized into discrete tool boundaries for preflight, extraction, validation, and output creation so selected steps can later move server-side. Realizes UJ-4.

**Consequences (testable):**
- Each processing step can be invoked independently through a stable contract.
- Each processing step writes a structured summary artifact.
- PRD/architecture handoff explicitly evaluates Enterprise service candidates.

### 4.7 Security, Audit, and Governance

**Description:** The product handles cadastral source files, extracted evidence, generated GIS data, and possibly AI/OCR services. v1 may retain simple plaintext local credential configuration as a controlled implementation constraint, but it must preserve audit evidence and avoid leaking sensitive data through logs or reports. Production hardening must replace plaintext secrets before broader operational deployment. Realizes UJ-2, UJ-3.

**Functional Requirements:**

#### FR-21: Manage v1 Credential Risk

The product may keep plaintext local credential configuration for v1, but credential handling must be isolated, documented, and ready to replace with encrypted/managed storage in a later production hardening step. Realizes UJ-2, UJ-3.

**Consequences (testable):**
- Generated Manifest files should not contain raw API keys unless explicitly required by the v1 local configuration profile.
- Logs do not contain raw API keys.
- Reports do not contain raw API keys.
- Plaintext credential use is recorded as a v1 risk/constraint, not a target production pattern.
- Future production deployment should replace plaintext API key storage with Windows Credential Manager / DPAPI-backed storage for desktop credentials where possible, or an organization-approved managed credential service when server-side processing is introduced.

#### FR-22: Preserve Audit Trail

The product records run IDs, timestamps, operator identity when available, source file references, extraction method, review approval, validation rules version, output paths, and report paths. Realizes UJ-3.

**Consequences (testable):**
- Each Case Folder contains enough metadata to reconstruct what happened in the run.
- Review approval and validation results are traceable.
- Output Summary links outputs to source Manifest and Approved Review Data.

#### FR-23: Support Local-Only Processing Mode

The product supports modes where AI-assisted extraction and OCR are disabled for sensitive submissions, unavailable network conditions, or operator preference. Realizes UJ-2.

**Consequences (testable):**
- Users can run a local-only profile when configured.
- The Manifest records whether external extraction services were enabled.
- If local-only mode cannot extract required evidence, the product requires manual review/correction.
- AI and OCR can be disabled independently where technically feasible.

## 5. Non-Goals (Explicit)

- v1 will not be an ArcGIS Enterprise-centered platform.
- v1 will not require automatic publishing/syncing to ArcGIS Enterprise.
- v1 will not provide a public applicant portal.
- v1 will not provide a separate web review interface for non-GIS staff.
- v1 will not fully automate cadastral approval without human review.
- v1 will not rewrite all existing Python processing logic into C#.
- v1 will not require users to manually edit INI files.
- v1 will not define final enterprise infrastructure topology; that belongs in architecture.

## 6. v1 Scope

### 6.1 In Scope

- ArcGIS Pro dock-pane Add-in as primary user surface.
- New submission Case Folder creation.
- At least four acceptance Test Cases:
  - Case 1: bad-quality scanned computation PDF plus bad-quality scanned parcel map PDF; expected limited results.
  - Case 2: good-quality scanned computation PDF plus good-quality scanned parcel map PDF.
  - Case 3: bad-quality scanned computation PDF plus TXT/CSV points file, scanned parcel map PDF, and DWG file; expected limited results.
  - Case 4: good-quality scanned computation PDF plus TXT/CSV points file, scanned parcel map PDF, and DWG file.
- Each acceptance Test Case expects, when the relevant run steps complete, a JSON report covering extraction/OCR/geodatabase generation, a GeoJSON file containing extracted points and lines, a result file geodatabase containing captured points and lines, and a process log.
- Source Scenario A intake.
- Source Scenario B intake.
- Transaction-based Case Folder organization using the current v1 format `TR-SMD-0000001`.
- Preflight validation.
- Extraction and review workflow.
- User approval of Extraction Review Data.
- Cadastral validation and rule result display.
- Local file geodatabase output creation.
- ArcGIS Pro map/layer integration.
- Reports, logs, GeoJSON, and Output Summary.
- Secret redaction for logs and reports.
- Future-ready boundaries for ArcGIS Enterprise publish/sync and processing evolution.

### 6.2 Out of Scope for v1

- Automatic publish/sync to ArcGIS Enterprise. Deferred to v2+ because v1 starts local/case-folder first, while preserving CADINDEX publish/sync metadata.
- Live CADINDEX update execution. Deferred to v2+; v1 keeps a Sync Facade.
- ArcGIS Enterprise Web Tool or Notebook-backed execution as the primary runtime. Deferred to v2+ exploration, though v1 must preserve modular boundaries.
- Public web portal for source submission.
- Non-GIS browser review workflow.
- Full enterprise parcel fabric editing/publishing workflow. [ASSUMPTION: v1 output is local/case-folder/GDB first, not direct authoritative parcel fabric writeback.]
- Full role-based workflow management beyond the cadastral technical operator.

## 7. Cross-Cutting Non-Functional Requirements

- **NFR-1 Reliability:** The workflow must preserve completed step outputs when later steps fail.
- **NFR-2 Recoverability:** Users must be able to reopen a Case Folder and resume from the latest successful step.
- **NFR-3 Security:** v1 may retain plaintext local credential configuration, but secrets must not be written into logs or reports; production deployment should replace plaintext configuration with encrypted or managed credential storage.
- **NFR-4 Auditability:** Every case must retain source references, processing summaries, review approval status, validation results, and output references.
- **NFR-5 Responsiveness:** Long-running processing must show progress and must not freeze ArcGIS Pro UI. [ASSUMPTION: progress/cancellation requirements will be refined in UX and architecture.]
- **NFR-6 Maintainability:** Processing boundaries must support future migration of selected steps to ArcGIS Enterprise services.
- **NFR-7 Usability:** A trained cadastral technical staff member should complete a standard submission without editing scripts or config files.

## 8. Integration and Dependencies

- ArcGIS Pro Add-in runtime and target SDK version for ArcGIS Pro 3.6/3.7.
- ArcGIS Enterprise 11.5 as the future publish/sync and service evolution target.
- ArcGIS Enterprise CADINDEX layer as the future sync target.
- Existing Python processing scripts in `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\Scripts`.
- ArcPy and ArcGIS Pro Python environment.
- PDF/DWG/TXT/CSV/TIF/PNG/JPG source files.
- File geodatabase outputs.
- Optional AI/OCR dependencies and credential profile.
- Future ArcGIS Enterprise publish/sync path to the CADINDEX layer.
- Future Enterprise Web Tool, geoprocessing service, or Notebook-backed processing options.

## 9. Data Governance and Audit Requirements

- Source files must be copied into the Case Folder using a structured folder layout.
- The minimum Case Folder layout is one folder per Transaction, with `source` and `output` child folders.
- Case artifacts must preserve original extraction values and edited review values when users correct data.
- Logs must redact secrets.
- Reports must distinguish extraction evidence, user-approved review data, validation findings, and generated GIS outputs.
- Future Enterprise sync must not be assumed to be authoritative until governance and approval rules are defined.

## 10. Risk and Mitigations

| Risk | Impact | Mitigation |
|---|---:|---|
| Extraction errors from varied source documents | High | Require human review, confidence flags, original-vs-edited values, regression fixtures |
| Plaintext API keys or sensitive data in config/logs | High | Plaintext may remain for v1 only as a controlled constraint; logs/reports must redact secrets; production hardening should move credentials to Windows Credential Manager / DPAPI or an organization-approved managed credential service |
| ArcGIS Pro or Python dependency mismatch | Medium | Preflight diagnostics, target version matrix, environment checks |
| Long-running processing blocks ArcGIS Pro | Medium | Async geoprocessing execution, progress display, step-based workflow |
| Automated process performs poorly | High | If extraction/validation produces zero usable results, mark failed; if nonzero but incomplete, route to review/manual adjustment; require explicit user decision for Manual Process |
| Enterprise scope expands v1 too far | Medium | Add-in first, local outputs first, Enterprise candidates recorded for v2+ |
| Case outputs are not publish/sync-ready | Medium | Structured Output Summary and modular tool boundaries |

## 11. Success Metrics

**Primary**

- **SM-1:** Integrated v1 workflow completion: a trained cadastral technical staff member can complete source intake, preflight, extraction review, validation, local output creation, and report generation without manually editing scripts or INI files. Validates FR-1 through FR-18.
- **SM-2:** Review-before-output enforcement: 100% of output creation runs require Approved Review Data. Validates FR-11, FR-15.
- **SM-3:** Case recoverability: at least 90% of failed test runs can be reopened with prior successful artifacts preserved. Validates FR-4, NFR-1, NFR-2.

**Secondary**

- **SM-4:** Preflight usefulness: preflight catches missing required files, unsupported extensions, missing write access, and unreadable DWG cases in fixture tests. Validates FR-5, FR-6, FR-7.
- **SM-5:** Audit completeness: every completed case includes Manifest, Approved Review Data, Validation Summary, Output Summary, HTML/PDF/JSON reports, GeoJSON, result GDB, and logs. Validates FR-17, FR-22.
- **SM-6:** Enterprise readiness: v1 outputs include enough metadata to plan publish/sync to CADINDEX and service-candidate evaluation. Validates FR-18, FR-19, FR-20.
- **SM-7:** Manual routing accuracy: cases with zero usable extracted/validated results are marked failed, recommend Manual Process, and require a user decision; nonzero incomplete extraction can continue to review/manual adjustment. Validates FR-14.
- **SM-8:** Acceptance fixture coverage: Case 1 through Case 4 exist as representative Test Cases with source files, expected outputs, and pass/fail criteria. Validates FR-1 through FR-18.
- **SM-9:** Scoring clarity: the product reports an overall solution score from 0 to 10, where 0 means failure/no usable result and 10 means successful result. Validates FR-8, FR-12, FR-17.

**Counter-metrics (do not optimize)**

- **SM-C1:** Do not optimize for fully automated parcel creation at the expense of review trust. Counterbalances SM-1.
- **SM-C2:** Do not optimize for Enterprise service migration before the local Add-in workflow is reliable. Counterbalances SM-6.
- **SM-C3:** Do not reduce logs or audit evidence merely to simplify implementation. Counterbalances SM-5.

## 12. Deferred Open Items

These items are not blockers for PRD finalization. They should be resolved during UX, architecture, and test planning.

1. **Solution score formula**
   - **Owner:** Architecture/test planning.
   - **Revisit when:** Case 1 through Case 4 fixture folders exist and the team can compare extraction, validation, review, and output results against actual source behavior.
   - **Decision needed:** Exact formula that converts extraction, validation, review, and output results into the 0-10 solution score.
2. **Fixture filenames and baseline counts**
   - **Owner:** Test planning/product owner.
   - **Revisit when:** Representative source files are selected for Case 1, Case 2, Case 3, and Case 4.
   - **Decision needed:** Exact sample source filenames and baseline output counts for each fixture folder.

## 13. Assumptions Index

- §4.1 FR-1: operator identity can be derived from the Windows or ArcGIS Pro user context.
- §4.2 FR-7: exact required DWG sublayers will be profile-configurable.
- §4.3 FR-10: v1 preserves original and edited values for fields edited by users.
- §4.5 FR-16: default behavior is replace prior layers from the same run.
- §6.2: v1 output is local/case-folder/GDB first, not direct authoritative parcel fabric writeback.
- §7 NFR-5: progress/cancellation requirements will be refined in UX and architecture.
