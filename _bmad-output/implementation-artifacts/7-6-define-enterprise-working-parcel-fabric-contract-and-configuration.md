---
baseline_commit: handoff-2026-06-23
---

# Story 7.6: Define Enterprise Working Parcel Fabric Contract And Configuration

Status: review

## Story

As a solution architect and deployment administrator,  
I want the add-in to define an explicit Enterprise Parcel Fabric working-workspace contract,  
so that validated compute cases can move from local review artifacts into a shared parcel-editing surface without mixing authoritative promotion concerns into the current local workflow.

## Acceptance Criteria

1. Given the workflow already supports local `normal`, local Parcel Fabric, and Enterprise working-layer review modes, when the Enterprise Parcel Fabric path is defined, then the contract clearly distinguishes a shared working Parcel Fabric from both local transaction `.gdb` review and final authoritative promotion.
2. Given Enterprise Parcel Fabric requires more than simple point/line/polygon feature writes, when the contract is documented, then it defines the required service references, fabric dataset/layer target, parcel type target, record strategy, scoping fields, and build/load expectations needed for the compute workflow.
3. Given local transaction artifacts remain the v1 system of record, when Enterprise Parcel Fabric mode is configured, then the contract preserves transaction-local artifacts as the processing source of truth and uses the working Parcel Fabric only as a collaborative spatial review surface.
4. Given deployments need an explicit configuration model, when the settings contract is defined, then it exposes an Enterprise Parcel Fabric workspace mode, required service/configuration keys, validation rules, and operator-facing readiness messages without breaking existing local-only modes.
5. Given the team already has Enterprise working-layer stories, when this story is complete, then the Parcel Fabric branch is documented as a separate mode that complements rather than replaces Stories 7.1-7.4.
6. Given final authoritative promotion remains a later controlled step, when this story is complete, then it clearly states what belongs to working-fabric publication versus what still belongs to final sync/promotion stories.

## Tasks / Subtasks

- [x] Define the Enterprise Parcel Fabric working-mode contract. (AC: 1-3, 6)
  - [x] Describe where this mode sits relative to `normal`, `parcel_fabric_local`, and `enterprise_working_layers`.
  - [x] Define the boundary between local case artifacts, shared working fabric state, and final authoritative promotion.
  - [x] Document which workflow stage first owns the working fabric handoff (`Create Spatial Units`) and which stages remain downstream (`Final Review`, `Finalize`).

- [x] Define the required Enterprise Parcel Fabric targets and identifiers. (AC: 2, 4)
  - [x] Standardize the service-root, fabric-layer, parcel-type, record, and optional topology/utility references needed by the add-in.
  - [x] Define how transaction identity, transaction number, record name, parcel group, and run/review metadata map into the working fabric.
  - [x] Document the minimum editable layers/services that must exist before this mode is considered ready.

- [x] Define the configuration contract and readiness validation. (AC: 3-5)
  - [x] Add the recommended settings keys for Enterprise Parcel Fabric mode and validation behavior.
  - [x] Define readiness warnings vs blockers for missing service URLs, parcel type names, record settings, or scoping fields.
  - [x] Keep current local and Enterprise working-layer modes intact and clearly separated.

- [x] Define the publication/build/load lifecycle. (AC: 2-3, 6)
  - [x] Document the expected order: validated review data -> fabric record context -> geometry import/copy -> parcel build -> map load.
  - [x] Clarify how map-review labels/overlays should coexist with the Parcel Fabric layer for examiner usability.
  - [x] Define what audit/log artifacts must be written locally for later diagnosis.

## Dev Notes

### Why This Story Exists

- Story 5.8 implemented a **true local Parcel Fabric** path inside the transaction `.gdb`.
- Stories 7.1 and 7.2 implemented **Enterprise working-layer** thinking for shared points/lines/polygons, but not an Enterprise Parcel Fabric branch.
- The current workflow now needs a fourth review/output strategy: a **shared working Parcel Fabric** that examiners can use after `Validate Points`, while still preserving the local case folder as the resilient processing record.

This story is intentionally a **contract story first**. It defines the deployment and workflow agreement before we write the publishing code.

### Recommended Mode Name

Add a new explicit mode value:

- `enterprise_parcel_fabric`

This keeps the existing modes readable and avoids overloading `enterprise_working_layers` with Parcel Fabric behavior that it was never designed to imply.

Recommended supported values after this story:

- `normal`
- `parcel_fabric_local`
- `enterprise_working_layers`
- `enterprise_parcel_fabric`

### Recommended Settings Contract

Extend `WorkflowSettings.json` with a dedicated block:

```json
{
  "review_workspace_mode": "normal",
  "enterprise_parcel_fabric_review": {
    "enabled": false,
    "service_root": "",
    "fabric_layer_url": "",
    "parcel_layer_url": "",
    "records_layer_url": "",
    "parcel_type_name": "compute_review",
    "record_name_pattern": "sidwell-record-{transaction_number}",
    "transaction_scope_field": "transaction_number",
    "transaction_id_field": "transaction_id",
    "review_state_field": "review_state",
    "publish_timing": "on_outputs",
    "build_behavior": "build_after_copy",
    "load_overlays": true,
    "overlay_source": "local_case_outputs",
    "allow_replace_transaction_scope": true,
    "require_active_map": true
  }
}
```

Recommended allowed values:

- `publish_timing`
  - `on_outputs` - recommended default
  - `on_final_review` - more conservative alternative

- `build_behavior`
  - `build_after_copy` - recommended default
  - `copy_only`

- `overlay_source`
  - `local_case_outputs` - recommended default
  - `none`

### Required Working Parcel Fabric Roles

This mode should assume these roles exist conceptually, even if some are resolved through one Parcel Fabric service root:

| Role | Purpose |
|---|---|
| `fabric_layer` | Base Parcel Fabric layer/dataset used by ArcGIS Pro |
| `parcel_records` | Working record target for transaction-scoped review publication |
| `parcel_type` | Parcel type used for compute-review parcel creation |
| `parcel_lines` | Fabric-target line import/copy destination |
| `parcel_points` | Fabric-target point support when needed by the chosen workflow |
| `parcel_polygons` | Built parcel result visible to the examiner |
| `overlay_points` | Local labeled point review overlay kept for usability |
| `overlay_lines` | Local labeled line review overlay kept for usability |
| `overlay_polygons` | Local labeled polygon overlay kept for usability |

Important: the **overlay layers** are still useful even in Enterprise Parcel Fabric mode, because native fabric child layers may not preserve reviewer-friendly labels the same way the local output overlays do.

### Recommended Identity / Scoping Contract

Keep these identities distinct:

- `transaction_number`
  - human-scoped working-record and replacement key
- `transaction_id`
  - stable Innola/system identifier
- `record_name`
  - recommended: `sidwell-record-{transaction_number}`
- `parcel_group_id`
  - stable parcel/traverse grouping from validated review data
- `review_state`
  - `validated`, `published_to_working_fabric`, `map_review_pending`, `final_review_approved`

### Recommended Lifecycle

The intended runtime flow:

1. `Attachments`
2. `Data Extraction`
3. `Validate Points`
4. `Create Spatial Units`
   - local validated review snapshot remains authoritative input
   - add-in publishes/copies into working Parcel Fabric
   - add-in builds or refreshes parcel geometry
   - add-in loads working Fabric + optional local overlays into ArcGIS Pro
5. `Final Review`
   - examiner uses ArcGIS Pro map tools, parcel tools, and COGO-aware editing
6. `Finalize`
   - later stories decide how authoritative promotion/Innola completion occur

### Readiness Rules

When `review_workspace_mode = enterprise_parcel_fabric`, hard blockers should include:

- `enterprise_parcel_fabric_review.enabled != true`
- missing `fabric_layer_url`
- missing `records_layer_url`
- missing `parcel_type_name`
- missing `transaction_scope_field`
- missing required record naming or record target settings

Warnings, not blockers:

- `load_overlays = false`
- optional parcel/records URLs derivable from `service_root`
- optional overlay source not configured

### Out of Scope

This story does **not**:

- publish geometry yet
- create/fix enterprise services automatically
- perform authoritative promotion
- replace existing local case-folder recoverability
- remove or downgrade `normal` / `parcel_fabric_local` / `enterprise_working_layers`

### Suggested Areas

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ProcessingTools/adapters/output_adapter.py`

### References

- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/implementation-artifacts/7-1-define-enterprise-working-review-layer-schema-and-configuration.md`
- `_bmad-output/implementation-artifacts/7-2-publish-approved-review-geometry-to-enterprise-working-layers.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `https://developers.arcgis.com/rest/services-reference/enterprise/parcel-fabric-service/`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-23 | 0.1 | Drafted the Enterprise working Parcel Fabric contract and configuration story aligned to the current compute workflow and existing Epic 7 work. | Codex |
| 2026-06-23 | 1.0 | Implemented the Enterprise Parcel Fabric settings contract, configuration UI, validation rules, default settings block, and automated coverage. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /nodeReuse:false /p:UseSharedCompilation=false`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added a fourth review workspace mode, `enterprise_parcel_fabric`, with explicit parsing, formatting, warning, and default-contract support.
- Extended the Settings workspace to edit Enterprise Parcel Fabric targets, publish timing, build behavior, overlay behavior, and scope fields.
- Tightened Enterprise Parcel Fabric readiness so `records_layer_url` is treated as part of the required working-fabric contract, not just optional metadata.
- Surfaced Enterprise working-layer and Enterprise Parcel Fabric readiness warnings in the top-level Settings summary so operators see incomplete deployment state immediately.
- Added validation and round-trip persistence coverage for the new mode, plus execution-time timeout normalization and document-type output profile support.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/DocumentTypeCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowExecutionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
