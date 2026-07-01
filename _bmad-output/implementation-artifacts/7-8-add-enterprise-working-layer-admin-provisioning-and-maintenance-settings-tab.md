---
baseline_commit: handoff-2026-06-30
---

# Story 7.8: Add Enterprise Working Layer Admin Provisioning And Maintenance Settings Tab

Status: review

## Story

As a deployment administrator,  
I want a dedicated Settings admin tab for Enterprise working-layer provisioning, validation, and cleanup,  
so that testing and deployment teams can create, verify, and maintain the shared Enterprise review workspace without mixing admin operations into examiner transaction processing.

## Business Context

Story 7.7 now targets generic Enterprise working layers for transaction review: working points, lines, polygons/parcels, and a case index. Those layers must exist before examiners can publish validated spatial units into Enterprise.

Creating and maintaining these layers is an **admin task**. It should not happen silently during `Create Spatial Units`, `Final Review`, or `Finalize`.

During testing, admins also need a controlled cleanup path because working layers may accumulate test transaction geometry. Cleanup must be explicit, scoped, and auditable.

## Acceptance Criteria

1. Given an administrator opens Settings, when Enterprise workspace setup is needed, then Settings exposes a dedicated admin tab for Enterprise Working Layer provisioning and maintenance rather than burying admin actions inside examiner workflow controls.
2. Given the admin tab is opened, when the current settings are loaded, then it displays the configured portal/service root, workspace name, target layer/table URLs, publish behavior, transaction scope field, and admin maintenance options.
3. Given working layers do not exist yet, when an administrator runs provisioning, then the tool can create or prepare the Enterprise working workspace schema for points, lines, polygons, case index, and optional issues/review notes using an admin-approved script path.
4. Given provisioning succeeds, when Settings is refreshed, then the generated layer/table URLs are written back into `WorkflowSettings.json` or surfaced for copy/paste when automatic save is not allowed.
5. Given provisioning or validation fails, when the result is shown, then the admin receives clear non-secret diagnostics and existing local settings are not corrupted.
6. Given testing cleanup is needed, when an administrator runs maintenance cleanup, then cleanup requires an explicit scope such as transaction number, test batch id, or inactive working rows; it must not delete all working content by default.
7. Given cleanup runs, when records are removed or deactivated, then the result is logged locally with operator, scope, timestamp, affected layer roles, and affected record counts.
8. Given Enterprise credentials are required, when provisioning, validation, or cleanup runs, then credentials are collected only at run time or via configured secure environment/session mechanisms and are never written to settings, logs, diagnostics, or story artifacts.
9. Given `review_workspace_mode = enterprise_working_layers`, when Settings validates the Enterprise working configuration, then points, lines, polygons, case index, transaction scope field, edit/query capability, and required fields are all checked before transaction publish can be considered ready.
10. Given this story is complete, when an examiner processes a transaction, then normal transaction workflow can only consume configured/validated Enterprise working layers; it does not create or clean up Enterprise layers automatically.
11. Given live Enterprise provisioning is run against `https://jm-gis.innola-solutions.com/portal`, when provisioning returns success, then the resulting FeatureServer must expose real child resources for the required roles (`/0`, `/1`, `/2`, `/3`) and optional issues role (`/4`) before URLs are written back.
12. Given the portal/server rejects empty-service `addToDefinition` provisioning, when the admin script provisions working layers, then it must use a schema-backed publish/template path rather than reporting success for an empty hosted Feature Service shell.

## Tasks / Subtasks

- [x] Add a dedicated Enterprise Admin tab to Settings. (AC: 1-2)
  - [x] Add a new `SettingsWorkspaceDocument.TabNames` entry, recommended label: `Enterprise Admin`.
  - [x] Move or mirror admin-only Enterprise setup controls out of the crowded `Spatial Workspace` tab.
  - [x] Keep ordinary spatial review mode settings visible where operators expect them, but keep provisioning/cleanup controls clearly admin-scoped.

- [x] Define the admin provisioning settings contract. (AC: 2-5, 8)
  - [x] Add settings for provisioning script path, provisioning mode, schema version, target folder/item name, and optional save-back behavior.
  - [x] Preserve existing `enterprise_working_review` keys for runtime use.
  - [x] Add a new admin block only if needed, for example `enterprise_working_admin`.
  - [x] Do not store portal passwords, tokens, or raw credential material.

- [x] Add or wire an admin provisioning script. (AC: 3-5, 8)
  - [x] Use the configured ArcGIS Python executable when possible.
  - [x] Create or update an admin script, recommended path: `src/ProcessingTools/admin/provision_enterprise_working_layers.py`.
  - [x] Script should support `validate`, `provision`, and `cleanup` modes.
  - [x] Script should create/validate these target roles:
    - working points
    - working lines
    - working polygons/parcels
    - working case index
    - optional working issues/review notes
  - [x] Script should emit machine-readable JSON diagnostics.

- [x] Implement Enterprise working schema validation. (AC: 5, 9)
  - [x] Validate layer/table URLs are reachable.
  - [x] Validate query/edit capabilities required by Story 7.7.
  - [x] Validate required shared fields and role-specific fields.
  - [x] Validate geometry type for points, lines, and polygons.
  - [x] Validate case index exists and supports one row per transaction scope.
  - [x] Show warnings for optional issues layer absence, not blockers.

- [x] Implement maintenance cleanup operations. (AC: 6-8)
  - [x] Support cleanup by `transaction_number`.
  - [x] Support cleanup by inactive/test rows if the schema exposes `is_active`, `case_status`, or similar fields.
  - [x] Require explicit confirmation and non-empty cleanup scope.
  - [x] Prefer deactivation (`is_active = 0`, `case_status = cleanup_removed`) where practical; hard delete only when explicitly selected.
  - [x] Emit a local maintenance audit artifact, recommended file: `working/enterprise_working_admin_audit.json`.

- [x] Add Settings workflow integration. (AC: 1-9)
  - [x] Add buttons/commands for:
    - Validate working layers
    - Provision working layers
    - Cleanup test working rows
  - [x] Display last result summary without exposing secrets or raw service payloads.
  - [x] Allow generated URLs to populate the existing `enterprise_working_review.layers` fields.
  - [x] Keep UI disabled or warning-only when ArcGIS Python executable is missing.

- [x] Add automated coverage. (AC: 1-10)
  - [x] Test Settings document round-trip for the new admin settings.
  - [x] Test validation messages when case index is missing in `enterprise_working_layers` mode.
  - [x] Test provisioning command builds expected script arguments without logging credentials.
  - [x] Test cleanup requires explicit scope.
  - [x] Test cleanup audit output shape.
  - [x] Test ordinary transaction publish path does not trigger provisioning or cleanup.

- [x] Rework live Enterprise provisioning to create schema-backed hosted working layers. (AC: 3-5, 8-12)
  - [x] Stop treating empty hosted Feature Service creation as successful provisioning.
  - [x] Keep the current guard that treats ArcGIS `status: error`, `error`, and `success: false` responses as failures.
  - [x] Preserve the post-provision verification that re-reads the FeatureServer and requires the expected child layers/tables before writeback.
  - [x] Replace or augment the current empty-service + `addToDefinition` implementation with a schema-backed publish/template flow that creates physical layers/tables during publish.
  - [x] Candidate implementation paths to evaluate:
    - Publish a schema-only File Geodatabase or service definition/package created by an admin-controlled template.
    - Use a checked-in schema template artifact if ArcGIS Pro/ArcPy licensing cannot be assumed in the standalone script process.
    - Use ArcGIS Pro active/session publishing only if the Settings process can reliably run in that licensed context.
  - [x] Ensure generated URLs are written back only after `/0`, `/1`, `/2`, `/3`, and `/4` resolve with expected geometry/type metadata.
  - [x] If an existing target service exists but has no layers/tables, fail with a clear remediation message telling the admin to delete or replace the invalid empty service.
  - [x] Add tests for empty FeatureServer detection and no-writeback behavior.
  - [x] Add tests for schema-backed publish success payload mapping to `enterprise_working_review.layers`.

### Review Findings

- [x] [Review][Patch] Settings Enterprise Admin buttons do not execute validation/provisioning/cleanup; they only update status text with instructions, so generated URLs are not surfaced or written back and cleanup audits are not produced from the UI workflow. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs:86]
- [x] [Review][Patch] The provisioning helper remains non-mutating even with `--no-dry-run`; provision and cleanup modes report ready-style statuses while warning that live Enterprise execution is not implemented, so no layers are created and no rows are cleaned. [src/ProcessingTools/admin/provision_enterprise_working_layers.py:59]
- [x] [Live Enterprise Rework] The current live provisioning path creates `working_review` as a hosted Feature Service item, but the service remains empty (`layers: []`, `tables: []`). Settings must not treat this as provisioned. Evidence from `https://jm-gis.innola-solutions.com/server/rest/services/Hosted/working_review/FeatureServer?f=pjson`, item `1187be5fe87f42a5952992dedf5f34c2`.
- [x] [Live Enterprise Rework] The tested ArcGIS Enterprise 11.5 server accepts empty `createService`, rejects inline layer definitions during `createService`, and rejects all tested `addToDefinition` URL shapes for empty hosted Feature Services. Provisioning must move to a schema-backed publish/template method instead of endpoint guessing.
- [x] [Live Enterprise Rework] The current invalid empty `working_review` portal item must be deleted or replaced before the corrected provisioning run; otherwise validation/writeback may point at a FeatureServer with no usable child layers.

## Live Enterprise Findings From 2026-06-30

These findings update the implementation contract for the next 7.8 dev pass.

Portal/server tested:

- Portal URL: `https://jm-gis.innola-solutions.com/portal`
- Service root: `https://jm-gis.innola-solutions.com/server/rest`
- Server admin root observed: `https://jm-gis.innola-solutions.com/server/admin`
- Target service name tested: `working_review`
- Portal owner/account observed: `GIS_Test`

Observed result:

- Portal item `working_review` was created as a hosted Feature Layer / Feature Service.
- Item ID: `1187be5fe87f42a5952992dedf5f34c2`
- FeatureServer parent exists at `https://jm-gis.innola-solutions.com/server/rest/services/Hosted/working_review/FeatureServer`.
- Parent metadata reports `layers: []` and `tables: []`.
- Portal Data tab shows an error because no child layer/table resources exist.
- URLs such as `/FeatureServer/0`, `/FeatureServer/1`, and `/FeatureServer/2` are invalid until the schema is physically created.

Endpoint behavior observed:

- Empty hosted Feature Service `createService` succeeds.
- `createService` with inline `layers`/`tables` definitions fails with `Create Service error: Failed to create the service 'Hosted/<name>.FeatureServer'.`
- Public `.../FeatureServer/addToDefinition` returns `Invalid URL`.
- Public `.../FeatureServer/admin/addToDefinition` returns `Invalid URL`.
- `.../server/rest/admin/services/Hosted/<name>.FeatureServer/addToDefinition` returns `Invalid URL`.
- `.../server/admin/services/Hosted/<name>.FeatureServer/addToDefinition` returns `status: error` / `Could not find resource or operation 'addToDefinition' on the system.`
- ArcGIS Python API `FeatureLayerCollection.manager.add_to_definition(...)` against the existing item returns `403`.

Implementation consequence:

- The admin script must not rely on empty service + `addToDefinition` for this Enterprise deployment.
- The corrected provisioning path must create the schema as a real hosted layer/table publish operation, likely by publishing a schema-backed artifact such as a File Geodatabase/service definition/template package.
- The script must verify the final FeatureServer children after publish before returning `provisioned` or writing URLs back to `WorkflowSettings.json`.

## Enterprise Working Layer Schema To Provision

The admin provisioning flow should align with Story 7.7.

Required target roles:

| Role | Geometry / Type | Required |
|---|---|---|
| `points` | Point layer | Yes |
| `lines` | Polyline layer | Yes |
| `polygons` | Polygon layer | Yes |
| `case_index` | Table or simple nonspatial layer | Yes |
| `issues` | Table, point, or polygon layer | Optional |

Required shared fields across spatial working layers:

| Field | Type | Notes |
|---|---|---|
| `transaction_number` | Text(64) | Default transaction scope |
| `transaction_id` | Text(64) | System identifier |
| `task_id` | Text(64) | Innola task id |
| `workflow_stage` | Text(64) | Current workflow stage |
| `review_state` | Text(64) | Working review state |
| `case_status` | Text(64) | active/review_pending/completed/failed |
| `created_by` | Text(128) | Initial publisher |
| `created_utc` | Date | Initial publish time |
| `last_saved_by` | Text(128) | Last publisher |
| `last_saved_utc` | Date | Last publish time |
| `run_id` | Text(64) | Output run id |
| `is_active` | Short | Active row flag |
| `edit_generation` | Long | Republish generation |

Role-specific fields are inherited from Story 7.7 and should not diverge unless that story is updated.

## Recommended Settings Contract

Add a new admin-only block if implementation needs to persist admin preferences:

```json
{
  "enterprise_working_admin": {
    "provisioning_enabled": false,
    "provisioning_script_path": "D:\\Code\\BMad-Method\\dev\\pe-jamaica\\src\\ProcessingTools\\admin\\provision_enterprise_working_layers.py",
    "schema_version": "sidwell_enterprise_working_v1",
    "target_folder": "Sidwell Working Review",
    "target_service_name": "sidwell_working_review",
    "allow_settings_writeback": true,
    "cleanup_mode": "deactivate",
    "require_cleanup_scope": true,
    "last_validation_summary_path": "",
    "last_maintenance_audit_path": ""
  }
}
```

The runtime publish path should continue to read the existing `enterprise_working_review` block:

```json
{
  "review_workspace_mode": "enterprise_working_layers",
  "enterprise_working_review": {
    "enabled": true,
    "service_root": "",
    "workspace_name": "sidwell_working_review",
    "publish_behavior": "replace_transaction_scope",
    "publish_timing": "on_outputs",
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

## Credential Handling

Do not ask for portal credentials while drafting or reviewing this story.

Credentials are needed only when one of these happens:

1. The provisioning script is being run against the live portal.
2. The Settings validation button is being tested against the live portal.
3. Cleanup is being run against live working layers.

Preferred credential handling:

- ArcGIS Pro active portal/session token when available.
- Runtime prompt/session memory.
- Environment variables for automation.

Forbidden:

- Writing username/password/token to `WorkflowSettings.json`.
- Writing tokens or raw service responses to logs.
- Storing portal credentials in story files, test fixtures, or admin audit artifacts.

## Admin Cleanup Rules

Cleanup must be intentionally scoped.

Allowed cleanup scopes:

- `transaction_number = 100000416`
- specific test batch id if implemented
- inactive rows older than a specified timestamp
- rows with `case_status` in an explicit test/failed/cleanup-eligible state

Disallowed default:

- delete all rows in all working layers
- cleanup without confirmation
- cleanup without audit

Recommended safe default:

- deactivate rows instead of delete
- set `is_active = 0`
- set `case_status = cleanup_removed`
- write cleanup audit counts

## Current Code Notes

- Settings tabs currently live in `SettingsWorkspaceDocument.TabNames`; current tabs are General, AI Toolset, Innola Integration, Preflight Rules, and Spatial Workspace.
- Enterprise runtime settings already exist in `SettingsWorkspaceDocument`, `SettingsWorkspaceService`, `WorkflowSettings.json`, and the Configuration window under the Spatial Workspace Enterprise expanders.
- Current validation requires points, lines, polygons, and transaction scope field for `enterprise_working_layers`; Story 7.7 makes `case_index` required, so this story should align Settings validation with that requirement.
- `arcgis_python_executable` is already configured and should be reused for admin scripts.
- `JsonEnterpriseWorkingLayerPublishService` and `JsonEnterpriseWorkingStateRestoreService` should not be treated as live portal provisioning. This story owns admin provisioning/validation/cleanup boundaries.
- The current admin script path exists at `src/ProcessingTools/admin/provision_enterprise_working_layers.py`.
- The current script has useful validation/cleanup scaffolding, JSON diagnostics, error-response guarding, and post-provision child-layer verification; preserve those pieces.
- The current live provisioning strategy is not sufficient for the tested Enterprise server because the created service shell has no physical child layers/tables.
- The next implementation should either replace `_provision_live_layers_rest` or route it to a schema-backed publish implementation while preserving the existing CLI contract used by Settings.

## Suggested Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/`
- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ProcessingTools/tests/`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Testing Requirements

Minimum verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- Python unit tests for provisioning argument construction and JSON diagnostics in test/offline mode.

Live portal smoke testing is separate and requires admin credentials at runtime.

## References

- `_bmad-output/implementation-artifacts/7-1-define-enterprise-working-review-layer-schema-and-configuration.md`
- `_bmad-output/implementation-artifacts/7-2-publish-approved-review-geometry-to-enterprise-working-layers.md`
- `_bmad-output/implementation-artifacts/7-3-restore-transaction-working-state-from-enterprise-review-layers.md`
- `_bmad-output/implementation-artifacts/7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

## Dev Agent Record

### Completion Notes

- Story created from admin requirement that working layer creation and cleanup must be a Settings/admin task, not transaction workflow behavior.
- Credentials should be requested only during live portal implementation/smoke testing.
- Implemented a dedicated Settings `Enterprise Admin` tab with runtime target summary, provisioning settings, scoped cleanup controls, result messaging, and admin action buttons.
- Added `enterprise_working_admin` settings persistence while preserving `enterprise_working_review` as the examiner runtime publish contract.
- Added `provision_enterprise_working_layers.py` with `validate`, `provision`, and `cleanup` modes, JSON diagnostics, live metadata validation hooks, generated settings output, scoped cleanup requirements, and local cleanup audit output.
- Updated Settings validation so `enterprise_working_layers` requires the case index layer in addition to points, lines, polygons, and transaction scope field.
- Verified with `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`, `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`, and `python -m unittest discover -s tests` from `src\ProcessingTools`.
- 2026-06-30 live Enterprise testing found the first live provisioning strategy incomplete: it creates an empty hosted Feature Service shell but not the required layers/tables.
- Story reopened to `ready-for-dev` for schema-backed live provisioning rework. Existing Settings UI/admin contract remains useful and should be preserved.
- Reworked live provisioning so the admin script no longer creates empty hosted Feature Service shells via `createService` + `addToDefinition`.
- Added schema-backed template provisioning: admins can provide a File Geodatabase `.zip` or Service Definition `.sd` via `--schema-template-path`, `ARCGIS_WORKING_SCHEMA_TEMPLATE`, or the default `admin/templates/{schema_version}.zip/.sd` locations.
- Failed live provisioning now returns no generated layer URLs/settings, preventing accidental writeback of fake `/FeatureServer/0` style targets.
- Added verification that rejects empty hosted Feature Services with a clear delete/replace remediation message before any URL writeback.
- Verified with `python -m unittest tests.test_enterprise_working_admin`, `python -m unittest discover -s tests` from `src\ProcessingTools`, `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`, and `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
- Created the schema template generator and checked-in default FileGDB zip template at `src/ProcessingTools/admin/templates/sidwell_enterprise_working_v1.zip`.
- Updated provisioning URL writeback to map FeatureServer child URLs by published child name instead of assuming fixed numeric IDs, so `working_case_index` remains correct if Enterprise assigns table IDs after feature layers.
- Verified the generated FileGDB contains `working_points`, `working_lines`, `working_polygons`, `working_issues`, and `working_case_index` with the expected shared and role-specific fields.

### File List

- `_bmad-output/implementation-artifacts/7-8-add-enterprise-working-layer-admin-provisioning-and-maintenance-settings-tab.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ProcessingTools/admin/__init__.py`
- `src/ProcessingTools/admin/create_enterprise_working_schema_template.py`
- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ProcessingTools/admin/templates/sidwell_enterprise_working_v1.zip`
- `src/ProcessingTools/tests/test_enterprise_working_admin.py`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-30 | 0.1 | Created admin story for Enterprise working-layer provisioning, validation, and testing cleanup in a dedicated Settings tab. | Mary / Codex |
| 2026-06-30 | 1.0 | Implemented Settings Enterprise Admin tab, admin settings contract, provisioning/validation/cleanup script, scoped cleanup audit, and automated coverage. | Amelia / Codex |
| 2026-06-30 | 1.1 | Reopened story based on live Enterprise findings: empty FeatureService shell was created but child layers/tables were not; next dev pass must implement schema-backed publish provisioning and strict no-writeback validation. | Mary / Codex |
| 2026-06-30 | 1.2 | Implemented schema-backed template publish provisioning, empty-service rejection, failed-live no-writeback behavior, and regression tests. | Amelia / Codex |
| 2026-06-30 | 1.3 | Added reproducible FileGDB schema template generator, default zipped schema template, and name-based FeatureServer child URL mapping for published layers/tables. | Amelia / Codex |
