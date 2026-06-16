---
baseline_commit: handoff-2026-06-15
---

# Story 7.1: Define Enterprise Working Review Layer Schema And Configuration

Status: review

## Story

As a solution administrator,  
I want the add-in to define and load configuration for Enterprise working review layers,  
so that distributed users share a consistent schema, routing model, and environment-specific connection details.

## Acceptance Criteria

1. Given the architecture introduces an Enterprise working review workspace, when the application loads configuration, then it can resolve configured working layer targets for points, lines, polygons, and optional issue layers.
2. Given Enterprise working-layer settings are loaded, when the add-in validates them, then the configuration includes transaction identity, workflow stage, assignment, status, and last-saved metadata required for distributed review.
3. Given the application supports multiple review workspace strategies, when the workflow configuration is read, then it exposes a clear review workspace mode setting with the supported values `normal`, `parcel_fabric_local`, and `enterprise_working_layers`.
4. Given both local Case Folder artifacts and Enterprise working layers are supported, when the workflow configuration is read, then local artifact storage and Enterprise working-layer publishing are clearly separated and independently toggleable.
5. Given Enterprise working-layer publishing may occur at different workflow moments, when the workflow configuration is read, then it exposes an explicit publish timing setting with `on_complete` as the safe default and `on_outputs` as an optional earlier-publish mode.
6. Given Enterprise working-layer configuration is missing or incomplete, when the user opens Configuration or attempts to use distributed review, then the add-in shows a clear non-destructive warning and keeps local-only workflow execution available.
7. Given this story is complete, when implementation planning continues, then the working-layer schema, service references, and configuration keys are documented as the authoritative contract for the next Enterprise workspace stories.

## Tasks / Subtasks

- [x] Define the authoritative Enterprise working-layer contract. (AC: 1-2, 5)
  - [x] Identify required layer roles: points, lines, polygons, and optional issue/annotation layer.
  - [x] Define the minimum shared fields for transaction identity, lifecycle state, assignment, and audit metadata.
  - [x] Document whether case-level metadata belongs on every feature, a separate index layer, or both.

- [x] Extend configuration for review workspace modes and Enterprise working review mode. (AC: 1, 3-5)
  - [x] Add a review workspace mode setting with `normal`, `parcel_fabric_local`, and `enterprise_working_layers`.
  - [x] Add settings for enable/disable, service targets, and environment-specific layer references.
  - [x] Keep the existing local-only flow as a first-class supported mode.
  - [x] Define safe defaults and validation messages for incomplete Enterprise settings.

- [x] Surface configuration and readiness clearly in the add-in. (AC: 3-5)
  - [x] Expose the review workspace mode and configured targets in the Configuration panel.
  - [x] Add lightweight readiness messaging that does not block local-only users unnecessarily.

## Dev Notes

### Architectural Direction

- Local Case Folder remains the system of record for processing artifacts.
- Enterprise working layers hold only shared reviewable spatial state and workflow metadata.
- This story defines the contract; it does not yet publish or restore geometry.
- The configuration work in this story should make the three review workspace modes explicit and operator-visible.

### Recommended Settings Contract

The recommended `WorkflowSettings.json` contract should standardize the review workspace mode and Enterprise working-layer configuration under explicit keys.

Recommended top-level keys:

```json
{
  "review_workspace_mode": "normal",
  "enterprise_working_review": {
    "enabled": false,
    "service_root": "",
    "workspace_name": "sidwell_working_review",
    "publish_behavior": "replace_transaction_scope",
    "publish_timing": "on_complete",
    "restore_behavior": "prefer_local_then_enterprise",
    "allow_cross_machine_restore": true,
    "transaction_scope_field": "transaction_number",
    "layers": {
      "points": "",
      "lines": "",
      "polygons": "",
      "issues": "",
      "case_index": ""
    }
  }
}
```

Recommended allowed values:

- `review_workspace_mode`
  - `normal`
  - `parcel_fabric_local`
  - `enterprise_working_layers`

- `enterprise_working_review.publish_behavior`
  - `replace_transaction_scope` - recommended default
  - `append_only` - not recommended for normal use

- `enterprise_working_review.publish_timing`
  - `on_complete` - recommended default
  - `on_outputs` - optional earlier shared-visibility mode

- `enterprise_working_review.restore_behavior`
  - `prefer_local_then_enterprise` - recommended default
  - `prefer_enterprise_then_local`
  - `local_only`

Rationale:

- `review_workspace_mode` keeps the active review strategy explicit.
- `enterprise_working_review.enabled` prevents accidental half-configured activation.
- `service_root` helps group service references under one environment.
- `workspace_name` is useful for diagnostics, status text, and future admin reporting.
- `publish_behavior` and `restore_behavior` make distributed lifecycle rules explicit instead of hidden in code.
- `publish_timing` keeps the visibility boundary explicit: local editing can stay private until final completion, while still allowing an earlier publish mode for deployments that want it.

### Recommended Layer Roles

The Enterprise working review workspace should define these layer roles:

#### Required

- `points`
  - Shared editable working parcel points
- `lines`
  - Shared editable working parcel lines / COGO segments
- `polygons`
  - Shared editable working parcel polygons

#### Optional but strongly recommended

- `issues`
  - Validation/review issue features, unresolved notes, or reviewer annotations
- `case_index`
  - One record per transaction/case used for resume detection, ownership, status, and lightweight lookup without scanning all geometry layers

Recommended behavior:

- Geometry layers (`points`, `lines`, `polygons`) hold the editable spatial review state.
- `case_index` holds the case-level operational state and should be the first place reopen/restore checks for distributed working-state presence.
- `issues` should remain optional because some deployments may prefer to keep issue state only in JSON artifacts at first.

### Recommended Field Schema

Use a **shared base field set** across all Enterprise working layers, then add role-specific fields.

#### Shared base fields for all working layers

| Field | Type | Notes |
|---|---|---|
| `case_id` | Text(64) | Stable case/workspace identifier if different from transaction number |
| `transaction_id` | Text(64) | Innola/internal transaction GUID or stable id |
| `transaction_number` | Text(64) | Human-visible transaction number |
| `task_id` | Text(64) | Current Innola task id when available |
| `workflow_name` | Text(64) | Example: `parcel_workflow_compute` |
| `workflow_stage` | Text(64) | Example: `review_approved`, `validation_passed`, `outputs_ready` |
| `transaction_type` | Text(128) | User-facing transaction type/stage label |
| `assigned_user` | Text(128) | Current responsible user |
| `assigned_group` | Text(128) | Current responsible group |
| `case_status` | Text(64) | Example: `active`, `suspended`, `completed`, `recoverability_warning` |
| `review_state` | Text(64) | Example: `pending`, `approved`, `rejected`, `unresolved` |
| `is_active` | Short Integer | `1` active, `0` inactive/history if history is retained |
| `edit_generation` | Long Integer | Increment on each publish/update for optimistic restore logic |
| `last_saved_utc` | Date | Last distributed save timestamp |
| `saved_by` | Text(128) | User who last published/saved distributed state |
| `run_id` | Text(64) | Processing/output run id when available |
| `source_mode` | Text(64) | Example: `pdf_extract`, `csv_points`, `dwg_reference`, `manual_edit` |

#### Points layer fields

| Field | Type | Notes |
|---|---|---|
| `point_id` | Text(64) | Primary visible point label |
| `point_role` | Text(64) | Corner, tie, reference, etc. |
| `status_txt` | Text(64) | Current point review status |
| `length_txt` | Text(64) | Preserved extracted/display length where relevant |
| `source_txt` | Text(1024) | Condensed evidence/source reference |
| `row_id` | Text(64) | Link back to review row |
| `confidence` | Double | Optional extracted confidence score |

#### Lines layer fields

| Field | Type | Notes |
|---|---|---|
| `segment_id` | Text(64) | Stable segment identifier |
| `start_pt` | Text(64) | Start point id |
| `end_pt` | Text(64) | End point id |
| `length_txt` | Text(64) | Display/source length |
| `bearing_txt` | Text(64) | Display/source bearing |
| `seg_index` | Long Integer | Ordered segment sequence |
| `source_txt` | Text(1024) | Condensed evidence/source reference |

#### Polygons layer fields

| Field | Type | Notes |
|---|---|---|
| `parcel_name` | Text(128) | Parcel/display name |
| `parcel_number` | Text(64) | Parcel number if available |
| `parcel_type` | Text(64) | Parcel classification |
| `source_txt` | Text(1024) | Condensed evidence/source reference |
| `review_note` | Text(512) | Optional short note |

#### Issues layer fields

| Field | Type | Notes |
|---|---|---|
| `issue_id` | Text(64) | Stable issue identifier |
| `severity` | Text(32) | `critical`, `warning`, `info` |
| `issue_code` | Text(64) | Validation or review code |
| `issue_text` | Text(1024) | Human-readable message |
| `resolved_flag` | Short Integer | `1` resolved, `0` unresolved |
| `resolved_by` | Text(128) | User who resolved |
| `resolved_utc` | Date | When resolved |

#### Case index layer fields

| Field | Type | Notes |
|---|---|---|
| `case_id` | Text(64) | Primary key |
| `transaction_id` | Text(64) | Stable transaction id |
| `transaction_number` | Text(64) | Human transaction label |
| `workflow_name` | Text(64) | Workflow branch name |
| `workflow_stage` | Text(64) | Current stage |
| `case_status` | Text(64) | Active/suspended/completed |
| `assigned_user` | Text(128) | Current owner |
| `assigned_group` | Text(128) | Current group |
| `last_saved_utc` | Date | Last distributed save |
| `saved_by` | Text(128) | Last saving user |
| `resume_artifact_ref` | Text(256) | Optional server/local resume reference |
| `recoverability_state` | Text(64) | `clean`, `warning`, `partial_restore` |

### Recommended Schema Rules

1. Use **one consistent transaction key** everywhere: `transaction_number` for human-scoped replacement and `transaction_id` for system-scoped identity.
2. Keep geometry features **transaction-scoped**, not shared across unrelated cases.
3. Prefer a **case index layer** for fast restore/status checks instead of querying all point/line/polygon layers.
4. Keep large review JSON, logs, OCR artifacts, and resume ZIP payloads **out of Enterprise geometry layers**.
5. Use text lengths generously for labels, but avoid storing whole JSON blobs in feature attributes.

### Recommended Validation Rules For Configuration

When `review_workspace_mode = enterprise_working_layers`, the add-in should validate:

- `enterprise_working_review.enabled = true`
- `layers.points` is present
- `layers.lines` is present
- `layers.polygons` is present
- `transaction_scope_field` is present and matches the standardized field name
- `publish_timing` is present or safely defaults to `on_complete`

Warnings, not blockers, should be used when:

- `issues` is missing
- `case_index` is missing
- `service_root` is blank but direct layer URLs are provided

Hard blockers should be used when:

- required geometry layer targets are missing
- the selected mode is `enterprise_working_layers` but Enterprise mode is disabled
- the transaction scoping field is inconsistent with the contract

### Recommended First Implementation Scope

To keep Story 7.1 practical, the initial implementation should:

1. add the settings keys
2. validate the selected mode
3. expose the mode and configured targets in Configuration
4. define the field contract in docs and code constants

It should **not** yet:

- publish geometry
- restore geometry from Enterprise
- create or migrate live feature services automatically
- implement enterprise Parcel Fabric behavior

### Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/`

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.sln /nodeReuse:false`
- `dotnet run --project src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.Tests\\ParcelWorkflowAddIn.Tests.csproj --no-build`

### Completion Notes

- Added explicit three-mode review workspace configuration support in the settings model.
- Added Enterprise working review settings parsing, defaults, layer target handling, and readiness warnings.
- Added Enterprise working review publish timing with `on_complete` as the safe default and `on_outputs` as an optional earlier-publish mode.
- Updated the Configuration window to display the active review workspace mode, mode description, Enterprise workspace settings, and configured target summaries.
- Preserved backward compatibility for legacy `parcel_fabric` values by normalizing them to `parcel_fabric_local` in settings while keeping the existing output runtime path stable.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
