---
baseline_commit: handoff-2026-07-07
---

# Story 7.12: Create Innola Compute Review Web Map View For Completed Transactions

Status: in-progress

## Story

As an Innola user reviewing a completed Compute transaction,  
I want the Innola map panel to open a curated Enterprise web map focused on the transaction parcels,  
so that completed Compute review geometry is visible with the right reference layers, labels, symbology, pop-ups, filters, and default extent without exposing the internal editable `working_review` service directly.

## Business Context

The Compute workflow now publishes completed review geometry into the Enterprise `working_review` layers and creates Innola Spatial Units during Finalize. Innola's transaction page already has a generic map component, but it currently opens a broad/default Jamaica map rather than a transaction-scoped review map.

The next product need is a stable Enterprise map/view contract that Innola can consume after transaction completion. The map must show the parcels copied to `working_review` together with authoritative/context layers such as Survey Cadastre, Legal Cadastre, Fiscal Cadastre, parish boundaries, basemaps, OpenStreetMap, and Esri World Imagery. It must be styled for review: parcel labels, bearing/distance labels, useful pop-ups, sensible layer order, visibility ranges, filters, and an extent zoomed to the completed transaction parcel set.

This story should create an admin/provisioning script, preferably Python with ArcGIS Enterprise/ArcGIS REST support, that can build and maintain this map experience. It complements Stories 7.8, 7.9, 7.10, and 7.11. It must not replace the internal working-layer schema or make transaction Finalize depend on creating duplicate Portal items for every run unless transaction-specific map mode is explicitly configured.

## Product Decision

Default approach:

- Keep `working_review` as the internal writable operational FeatureServer used by ArcGIS Pro Finalize.
- Create a read-only hosted feature layer view, recommended name `working_review_innola_view`, for Innola and other external consumers.
- Create a reusable Enterprise web map, recommended name `innola_compute_review_map`, that uses the read-only view plus reference layers and basemaps.
- Let Innola pass `transaction_number` to the map component so it can filter and zoom to the selected transaction.

Supported fallback:

- If the Innola map component cannot apply a transaction filter/extent dynamically, the admin/runtime tool may generate or refresh a transaction-specific web map after Finalize and store its item id or URL in `working_case_index` and/or the local closeout artifacts.

## Acceptance Criteria

1. Given Enterprise admin settings are configured with a Portal URL, `working_review` service URLs, and reference layer sources, when the admin runs validation, then the tool verifies that the source working service, required child layers, reference layers, Portal access, and sharing target are reachable without modifying Portal content.
2. Given validation passes, when the admin runs provisioning, then the tool creates or updates a read-only hosted feature layer view for Innola consumption instead of exposing the editable `working_review` layers directly.
3. Given the read-only view exists, when the tool configures the view, then it exposes only fields needed for Innola map display, filtering, pop-ups, labels, and audit context, including `transaction_number`, `parcel_name`, `suid`, `area_sq_m` or equivalent area field, `review_decision`, `case_status`, `workflow_stage`, `bearing_txt`, `length_txt`, `distance_txt`, and core point/line identifiers where applicable.
4. Given the reusable web map is created or updated, when it is opened in Enterprise Map Viewer, then it contains the configured layers in this default order from bottom to top: selected basemap/imagery, parish boundaries, Survey Cadastre, Legal Cadastre, Fiscal Cadastre, working polygons, working lines, working points, and working issues/notes where available.
5. Given `working_polygons` rows exist for a completed transaction, when the web map opens with that transaction context, then the map filters to the selected transaction and zooms to all matching transaction polygons with a reasonable padding extent.
6. Given a transaction contains multiple parcels, when extent is calculated, then the default view includes all transaction parcels and nearby context rather than zooming to only one selected feature.
7. Given the map is opened without a transaction context, when no transaction filter is supplied, then the map uses a safe default extent such as Jamaica/parish context and does not show unrelated test rows by default if a completed/active row filter is configured.
8. Given working lines contain COGO fields, when the web map displays line labels, then line labels show `bearing_txt` plus a newline plus `length_txt`, falling back to `distance_txt` if `length_txt` is empty.
9. Given working polygons contain parcel identity and SUID fields, when polygon labels are enabled, then labels show the parcel identifier and SUID where available without overwhelming the map at small scales.
10. Given users inspect a working polygon pop-up, when the pop-up opens, then it displays transaction number, parcel/PID, SUID, lot number if available, area, review decision, case status, workflow stage, creator, updated time, and report/package reference fields where available.
11. Given users inspect working lines or points, when pop-ups open, then lines expose bearing/distance/length and source context, and points expose point id, sequence, parcel group, and source/review status where available.
12. Given reference layers are included, when map visibility ranges are applied, then national/parish layers remain visible at broad scales while cadastre, working geometry, and labels become visible at review-appropriate closer scales.
13. Given the web map or view already exists, when provisioning runs again, then the tool updates the existing items idempotently rather than creating duplicate Portal items.
14. Given reference layer URLs or item ids are missing, inaccessible, or incompatible, when validation/provisioning runs, then the tool fails with clear non-secret diagnostics and does not corrupt existing working map settings.
15. Given provisioning succeeds, when local settings writeback is enabled, then `WorkflowSettings.json` records the web map item id/URL, feature layer view item id/URL, reference layer configuration, sharing target, and transaction filter field under a dedicated Innola map view settings block.
16. Given provisioning succeeds, when diagnostics are written, then a machine-readable `enterprise_innola_map_view_provision.json` artifact records Portal item ids, service URLs, operation status, UTC timestamp, layer order, labeling status, pop-up status, filter mode, extent mode, and non-secret warnings.
17. Given a transaction Finalize completes after this story is configured, when `working_case_index` is updated, then it can store or reference the reusable map item id/URL and, in transaction-specific map mode, the transaction-specific map item id/URL.
18. Given Innola consumes the configured web map, when the map panel is opened for transaction `100000492` or another completed transaction, then the map starts at the specific parcel extent and displays the completed `working_review` geometry with labels and reference context.
19. Given Portal credentials or tokens are required, when validation/provisioning runs, then credentials are supplied only through runtime session, environment variable, or secure prompt behavior and are never written to settings, logs, diagnostics, or story artifacts.
20. Given automated tests run, then configuration validation, web map JSON generation, layer-order generation, label expression generation, pop-up template generation, transaction-filter URL/template generation, and extent calculation are covered without requiring live Portal access.

## Tasks / Subtasks

- [ ] Define the Innola map view configuration contract. (AC: 1-4, 7, 12, 15)
  - [ ] Add a dedicated settings block, recommended `enterprise_innola_map_view`.
  - [ ] Include fields for Portal URL, source working service root, target feature layer view name, target web map name, sharing target group/org, transaction scope field, completion filter, default extent behavior, basemap selection, and reference layer sources.
  - [ ] Preserve existing `enterprise_working_review` runtime settings and do not duplicate tokens or credentials.
  - [ ] Support configured reference layers for `Survey_Cadastre`, `Legal_Cadastre`, `Fiscal_Cadastre`, `parish`, OpenStreetMap, Esri World Imagery, and any required base map.

- [x] Add an admin script for map/view provisioning. (AC: 1-16, 19)
  - [x] Add a Python admin entrypoint, recommended path: `src/ProcessingTools/admin/provision_innola_compute_review_web_map.py`.
  - [x] Support `validate`, `provision`, and `export-config` or equivalent dry-run modes.
  - [x] Use the configured ArcGIS Python executable where possible.
  - [x] Prefer ArcGIS Python API where available, with REST fallback for web map JSON update if needed.
  - [x] Use `ARCGIS_PORTAL_TOKEN` or equivalent secure runtime auth mechanism without writing secrets.
  - [x] Emit machine-readable JSON diagnostics.

- [x] Create or update the read-only feature layer view. (AC: 2-3, 13-16)
  - [x] Create `working_review_innola_view` or update the configured existing view.
  - [x] Make the view query-only/read-oriented for external consumers where Enterprise supports it.
  - [ ] Apply default filters for active/completed rows if compatible with the chosen map behavior.
  - [x] Ensure expected child layers map to working points, lines, polygons, issues, and case index/table where available.
  - [x] Validate that `working_polygons` exposes SUID and area fields needed by Innola map pop-ups.

- [ ] Create or update the reusable Innola web map. (AC: 4-14, 18)
  - [ ] Build web map operational layers in the required layer order.
  - [ ] Configure symbology for working polygons, lines, points, issues, parish, and cadastre context layers.
  - [ ] Configure polygon, line, and point pop-ups.
  - [ ] Configure polygon and line labels, including COGO bearing/distance labels on `working_lines`.
  - [ ] Configure visibility ranges for broad reference layers, cadastre layers, working geometry, and labels.
  - [ ] Configure a default extent and transaction extent behavior.
  - [ ] Configure item metadata: title, summary, description, tags, thumbnail placeholder if available, and sharing settings.

- [ ] Support transaction-scoped map opening. (AC: 5-7, 17-18)
  - [ ] Define the URL or web map parameter strategy Innola will use to pass `transaction_number`.
  - [ ] Provide a reusable map URL template or app configuration value that Innola can consume.
  - [ ] Add optional transaction-specific map generation mode only if Innola cannot apply runtime filters/extent.
  - [ ] If transaction-specific mode is used, persist the item id/URL in `working_case_index`, closeout artifacts, and settings diagnostics.

- [ ] Extend Settings Enterprise Admin UX where appropriate. (AC: 1, 14-16, 19)
  - [ ] Add or expose fields for Innola map view provisioning settings.
  - [ ] Add an admin action such as `Validate Innola Map View` / `Provision Innola Map View`.
  - [ ] Display concise non-secret diagnostics and link to the generated provision artifact path.
  - [ ] Keep this admin function separate from examiner transaction processing and normal Finalize.

- [ ] Integrate with Finalize artifacts only after the map/view exists. (AC: 17-18)
  - [ ] Update Finalize/closeout output to include the reusable web map item id/URL when configured.
  - [ ] Update `working_case_index` with map reference fields only if the Enterprise schema/settings support them.
  - [ ] Do not fail transaction completion solely because the reusable map item is missing unless product marks map availability as a closeout blocker.

- [ ] Add automated coverage. (AC: 1-20)
  - [ ] Unit-test settings round-trip for `enterprise_innola_map_view`.
  - [x] Unit-test validation diagnostics for missing source working layers and missing reference layers.
  - [x] Unit-test generated web map JSON layer ordering.
  - [x] Unit-test generated label expressions for working lines and polygons.
  - [x] Unit-test generated pop-up templates for polygons, lines, and points.
  - [x] Unit-test transaction filter URL/template generation.
  - [ ] Unit-test extent calculation from a set of working polygon envelopes.
  - [ ] Unit-test idempotent item lookup/update behavior by mocking Portal search/update responses.
  - [x] Unit-test diagnostics redaction for tokens and credentials.

## Developer Notes

### Existing Enterprise Working Foundation

Relevant stories:

- `_bmad-output/implementation-artifacts/7-8-add-enterprise-working-layer-admin-provisioning-and-maintenance-settings-tab.md`
- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/7-10-add-enterprise-working-layer-default-visualization-labeling.md`
- `_bmad-output/implementation-artifacts/7-11-write-innola-plan-check-list-on-compute-finalize.md`

Relevant source files:

- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ProcessingTools/admin/create_enterprise_working_schema_template.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs`

Current configured Enterprise working layers use:

- `working_points`
- `working_lines`
- `working_polygons`
- `working_issues`
- `working_case_index`

Current `WorkflowSettings.json` already contains:

- `enterprise_working_review`
- `enterprise_working_admin`

This story should add a separate Innola-facing map/view settings block rather than overloading the existing publish/admin blocks.

### Recommended Settings Shape

Candidate settings:

```json
{
  "enterprise_innola_map_view": {
    "enabled": true,
    "portal_url": "https://jm-gis.innola-solutions.com/portal",
    "source_workspace_name": "working_review",
    "feature_layer_view_name": "working_review_innola_view",
    "web_map_name": "innola_compute_review_map",
    "transaction_scope_field": "transaction_number",
    "completion_filter": "is_active = 1 AND case_status = 'review_closed'",
    "transaction_filter_template": "transaction_number = '{transaction_number}'",
    "extent_mode": "transaction_polygons_with_padding",
    "extent_padding_percent": 15,
    "sharing_group_id": "",
    "writeback_enabled": true,
    "reference_layers": {
      "survey_cadastre": "",
      "legal_cadastre": "",
      "fiscal_cadastre": "",
      "parish": "",
      "open_street_map": "",
      "esri_world_imagery": ""
    },
    "outputs": {
      "feature_layer_view_item_id": "",
      "feature_layer_view_url": "",
      "web_map_item_id": "",
      "web_map_url": ""
    }
  }
}
```

Adjust names to match project conventions during implementation.

### Label And Pop-Up Guidance

Line label expression should match Story 7.10 behavior:

```arcade
var len = IIf(IsEmpty($feature.length_txt), $feature.distance_txt, $feature.length_txt);
When(
  IsEmpty($feature.bearing_txt) && IsEmpty(len), '',
  IsEmpty($feature.bearing_txt), len,
  IsEmpty(len), $feature.bearing_txt,
  $feature.bearing_txt + TextFormatting.NewLine + len
)
```

Polygon labels should prefer:

- `parcel_name`
- `suid`
- optionally area at close scales only

Polygon pop-ups should expose:

- transaction number
- parcel/PID
- lot number if available
- SUID
- area
- parish
- review decision
- case status
- workflow stage
- created/updated metadata
- report/package reference when present

### Guardrails

- Do not expose the editable `working_review` service directly to external users when a view can be used.
- If the target Enterprise deployment rejects child-layer creation for hosted views, the admin script may use the explicit `allow_source_service_map` fallback to create/update the web map from the source `working_review` service for controlled internal testing only. This fallback must be clearly reported in diagnostics.
- Do not write to CADMAP, CADINDEX, Survey Cadastre, Legal Cadastre, Fiscal Cadastre, parish, or basemap sources.
- Do not make web map provisioning run automatically during every transaction unless explicitly configured.
- Do not duplicate Portal items on every provision or Finalize retry.
- Do not log Portal tokens, Innola tokens, passwords, certificate material, or sensitive service response bodies.
- Do not assume the Innola map component can perform all client-side filtering until verified; keep the transaction-specific map fallback documented.

## Testing Requirements

Minimum automated verification:

- `python -m unittest tests.test_enterprise_innola_map_view` from `src/ProcessingTools`
- `python -m unittest discover -s tests` from `src/ProcessingTools`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build /p:Platform=x64`

Manual/live smoke testing:

- Use the dev Portal at `https://jm-gis.innola-solutions.com/portal`.
- Provision/validate the existing `working_review` service first.
- Provision the Innola map view against a non-production sharing group or test owner.
- If live view provisioning fails because Enterprise rejects `addToDefinition` for the hosted view service, rerun with the explicit source-service fallback for map-only validation:
  `powershell -ExecutionPolicy Bypass -File tools\provision_innola_compute_review_web_map.ps1 -Mode provision -Live -AllowSourceServiceMap -PortalToken "<portal-token>"`
- Open the created web map in Enterprise Map Viewer and verify layer order, labels, pop-ups, visibility ranges, and basemap/reference layers.
- Open a completed transaction such as `100000492` through the Innola map panel and confirm the map zooms to that transaction's copied `working_review` parcels.
- Verify unrelated active/test transactions are not shown when the transaction filter is applied.

## References

- `_bmad-output/implementation-artifacts/7-8-add-enterprise-working-layer-admin-provisioning-and-maintenance-settings-tab.md`
- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/7-10-add-enterprise-working-layer-default-visualization-labeling.md`
- `_bmad-output/implementation-artifacts/7-11-write-innola-plan-check-list-on-compute-finalize.md`
- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

- `python -m unittest src/ProcessingTools/tests/test_enterprise_innola_map_view.py`
- `python -m unittest discover -s tests` from `src/ProcessingTools`

### Completion Notes

- Added terminal-first admin script with `validate`, `export-config`, and `provision` modes.
- Dry-run/export modes generate a complete web map definition without touching Portal.
- Live provision path creates or reuses `working_review_innola_view` using Portal REST first, with ArcGIS Python API as fallback, and then creates/updates the web map item using `ARCGIS_PORTAL_TOKEN`.
- If `feature_layer_view_url` is already configured, the script reuses that view and only updates/creates the web map.
- Live testing against `jm-gis` showed this Enterprise deployment rejects `addToDefinition` for an empty hosted view FeatureServer, so the script now validates configured view children and supports an explicit `-AllowSourceServiceMap` fallback to create the web map from `working_review` for controlled internal validation.
- Patched the live fallback path so source-service fallback no longer fails later on the missing `feature_layer_view_url` guard. Verified live provisioning created `innola_compute_review_map` item `8b4c4b43ad6c4620b1ea2d59dc94f26f`.

### File List

- `src/ProcessingTools/admin/provision_innola_compute_review_web_map.py`
- `src/ProcessingTools/tests/test_enterprise_innola_map_view.py`
- `tools/provision_innola_compute_review_web_map.ps1`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-07 | 0.1 | Initial story for creating an Innola-facing Enterprise feature layer view and web map for completed Compute review transactions. | Codex |
| 2026-07-07 | 0.2 | Added terminal-first admin script and focused dry-run/export tests for Innola web map provisioning. | Codex |
| 2026-07-07 | 0.3 | Patched live provisioning to create or reuse `working_review_innola_view` automatically through ArcGIS Python API before web map creation. | Codex |
| 2026-07-07 | 0.4 | Added a single PowerShell wrapper for validate, dry-run export, and live provisioning execution. | Codex |
| 2026-07-07 | 0.5 | Reworked feature-layer-view creation to use Portal REST before ArcGIS Python API fallback for more reliable terminal execution. | Codex |
| 2026-07-07 | 0.6 | Added hosted-view child validation, early token validation, and explicit source-service fallback for Enterprise deployments that reject view `addToDefinition`. | Codex |
| 2026-07-08 | 0.7 | Fixed live source-service fallback web map provisioning and verified Portal token validity against `community/self`. | Codex |
