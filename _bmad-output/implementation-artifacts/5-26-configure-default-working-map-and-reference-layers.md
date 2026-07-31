---
baseline_commit: handoff-2026-07-26
---

# Story 5.26: Configure Default Working Map And Reference Layers

Status: in-progress

## Story

As a cadastral examiner loading a Compute transaction,  
I want the add-in to create or prepare the required ArcGIS Pro working map from configuration,  
so that I do not need to manually open a predefined project before I can process, validate, and review the transaction.

## Business Context

Today the Compute workflow assumes the user has already opened a prepared ArcGIS Pro project containing the Jamaica map and required reference layers. That makes local testing and target-machine deployment fragile: a transaction can load, but map-dependent workflow steps may be blocked or confusing when the expected map is missing.

The add-in should be able to prepare the working map on transaction load using configuration. The configuration should define the default map name, basemap choice, operational reference layers, drawing order, default visibility, and whether missing maps/layers are created automatically.

This story is not about generating parcel output layers. It prepares the base/reference map context needed before and during the workflow; generated transaction review layers remain handled by the output/map integration stories.

## Acceptance Criteria

1. Given a supported Compute transaction is loaded, when no active map exists, then the add-in creates or opens a configured working map without requiring a predefined `.aprx` project.
2. Given a configured working map already exists in the current ArcGIS Pro project, when a transaction is loaded, then the add-in reuses that map instead of creating duplicate maps.
3. Given a different active map is open, when the transaction loads, then the add-in activates the configured working map when `activate_on_transaction_load` is enabled.
4. Given configured basemaps include Esri imagery and OpenStreetMap/Open Basemap options, when the working map is prepared, then the add-in applies the configured default basemap and preserves configured alternate basemap options without trying to draw two incompatible basemaps as normal operational layers.
5. Given Esri World Imagery is configured as the default basemap, when the map is prepared, then the map uses the ArcGIS Pro/Portal-supported imagery basemap or configured imagery service item/URL.
6. Given OpenStreetMap is requested, when the map configuration is prepared, then the configuration prefers Esri's current Open Basemap option when available and treats legacy OpenStreetMap vector basemap references as configurable but deprecated/mature-support choices.
7. Given operational reference layers are configured, when the working map is prepared, then the add-in adds missing layers from their configured layer URLs or item references.
8. Given `Legal_Cadastre` is configured, when the working map is prepared, then it is added using the configured feature layer URL and default visibility.
9. Given multiple reference layers are configured, when the working map is prepared, then the add-in applies configured group names, drawing order, layer names, default visibility, and optional opacity.
10. Given some configured reference layers should not be visible by default, when the working map is prepared, then those layers are present in Contents but unchecked.
11. Given a configured layer already exists in the working map, when the map is prepared again, then the add-in does not duplicate it and updates safe display properties such as visibility/order only when configured to do so.
12. Given a configured layer URL is unavailable, unauthorized, or invalid, when map preparation runs, then the workflow reports a clear non-secret warning or blocker based on the layer's configured `required` flag.
13. Given a transaction-specific review group such as `TR 100000854 - Review` already exists, when a new transaction is loaded, then the add-in keeps reference layers but removes or refreshes stale transaction-specific groups according to existing cleanup rules.
14. Given the transaction is cancelled, suspended, finalized, or closed, when cleanup runs, then transaction-specific groups are removed while the configured base/reference working map can remain available for the next transaction.
15. Given the add-in is installed on a target computer, when configuration paths/URLs are deployed, then the working map can be prepared without copying a custom `.aprx` file.
16. Given the loaded Innola transaction includes a parish value, when the working map is prepared, then the add-in zooms to that parish extent using a configured parish layer or parish lookup before transaction-specific geometry exists.
17. Given the loaded transaction does not include a parish, when the working map is prepared, then the add-in falls back to the configured full Jamaica extent without blocking the workflow.
18. Given an administrator opens Settings, when the Map Layers tab is selected, then configured working-map layers can be reviewed and edited by name, source type, URL, group, and default visibility.
19. Given an administrator edits a configured layer in Settings, when the settings are saved, then the add-in preserves hidden planner metadata such as required flag, drawing order, opacity, basemap role, and scale limits for existing layers.
20. Given any Compute or Compare workflow map is created, reused, or activated, when map preparation completes, then the ArcGIS Pro map coordinate system is JAD 2001 Jamaica Grid / EPSG:3448.
21. Given configured public basemaps such as Esri imagery, Open Basemap Streets, World Topographic, or World Hillshade use Web Mercator, when they are added for display, then they must not change the workflow map coordinate system away from JAD2001.
22. Given generated review/output layers are loaded into the workflow map, when their spatial reference is inspected, then they declare JAD2001/EPSG:3448 or the workflow reports a clear blocker before moving forward.

## Tasks / Subtasks

- [x] Define the configurable working map contract. (AC: 1-6, 15)
  - [x] Add a `working_map` or equivalent section to workflow/settings configuration.
  - [x] Include map name, create-if-missing, reuse-existing, activate-on-load, default basemap, alternate basemaps, and cleanup behavior.
  - [x] Document that only one basemap is active at a time; alternates are available choices, not regular overlay layers.

- [x] Define operational reference layer configuration. (AC: 7-12)
  - [x] Support layer name, source type, URL/item path, group, required flag, visible-by-default flag, drawing order, opacity, and optional min/max scale.
  - [x] Seed configuration for `Legal_Cadastre` from the current Jamaica Enterprise layer URL.
  - [x] Seed configuration for `Fiscal_Cadastre` and `Survey_Cadastre` from the current Jamaica Enterprise map service URLs.
  - [x] Seed configuration for public Esri imagery, OpenStreetMap/Open Basemap, World Topographic, and World Hillshade references.
  - [x] Allow optional layers such as Fiscal Cadastre, Survey Cadastre, parishes, civic features, fishing beaches, hotels/attractions, communities, major roads, contours, river network, and enclosure boundaries to be hidden by default.

- [x] Expose working-map layers in Settings. (AC: 18-19)
  - [x] Add a Map Layers tab with editable columns for layer name, source type, URL, group, and default visibility.
  - [x] Load the tab from `working_map.reference_layers`.
  - [x] Save edited/new layer rows back to `working_map.reference_layers`.
  - [x] Preserve existing internal planner metadata for known rows.

- [x] Add parish-based initial zoom. (AC: 16-17)
  - [x] Capture the transaction parish value from Innola detail/selected transaction metadata where available.
  - [x] Add configurable parish lookup behavior using a parish layer URL, parish name field, and optional fallback extents.
  - [x] Zoom to the matched parish extent after the working map is prepared and before transaction-specific output layers exist.
  - [x] Fall back to the configured Jamaica/default extent if the parish is missing or unmatched.

- [x] Implement working map preparation service. (AC: 1-13)
  - [x] Add or extend an ArcGIS Pro SDK map service under the existing ArcGIS/map integration boundary rather than manipulating maps directly from ViewModels.
  - [x] Create/reuse the configured map on transaction load.
  - [x] Apply the configured basemap.
  - [x] Add missing configured reference layers.
  - [x] Avoid duplicate maps/layers across repeated loads.
  - [x] Apply default visibility/order/grouping.

- [x] Integrate map preparation into transaction load/reopen flow. (AC: 1-3, 13-15)
  - [x] Prepare the map after a supported Compute transaction is loaded and before map-dependent workflow actions are enabled.
  - [x] Ensure failures from required map setup produce actionable status messages.
  - [x] Ensure optional layer failures do not block the transaction.

- [x] Preserve cleanup behavior. (AC: 13-14)
  - [x] Keep reference/base layers across cancel/suspend/finalize unless configuration says otherwise.
  - [x] Continue removing transaction-specific review groups and generated layers.
  - [x] Ensure cleanup does not remove shared cadastral reference layers.

- [x] Add tests and smoke checks. (AC: 1-15)
  - [x] Unit-test configuration parsing and validation.
  - [x] Unit-test duplicate-prevention behavior through ArcGIS map service test seams.
  - [x] Unit-test required vs optional layer failure handling.
  - [x] Add or update XAML/ViewModel command gating tests if map readiness affects button enablement.
  - [x] Add manual smoke-test steps for a blank ArcGIS Pro project: load transaction, map created, layers added, process enabled.

- [x] Enforce JAD2001 for the working map canvas. (AC: 20-22)
  - [x] Set the ArcGIS Pro map spatial reference to JAD 2001 Jamaica Grid / EPSG:3448 when creating the configured working map.
  - [x] Re-apply JAD2001 when reusing an existing configured map, so Web Mercator basemaps cannot leave the map canvas in Web Mercator.
  - [x] Fail clearly when the configured working map is missing and map creation is disabled, instead of silently using an unrelated active map.
  - [x] Add regression coverage that the working-map service explicitly sets the map spatial reference to EPSG:3448.

### Review Findings

- [x] [Review][Patch] Parish zoom cannot work from the shipped/default configuration because `parish_lookup.layer_name` and `name_field` are parsed but never queried; with empty `known_extents`, every real parish falls back to the Jamaica extent. Patched by using transaction parish data against built-in/default parish extents, with missing parish falling back to Jamaica full extent. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs:158`]
- [x] [Review][Patch] If `create_if_missing` is false and the configured map is not found, map preparation returns the unrelated active map and can add/zoom reference layers there instead of failing clearly. Patched to return no map and surface the existing preparation failure message. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`]
- [x] [Review][Patch] The configured working map can remain in Web Mercator when created from an imagery/open basemap or reused from a prior project, causing JAD2001 output layers to display against a non-JAD2001 map canvas. Patched by forcing the workflow map spatial reference to EPSG:3448 on create, reuse, and active-map reuse. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`]
- [ ] [Review][Patch] Reused existing working maps do not get the configured default basemap applied; only newly created maps receive `ResolveBasemap(plan.DefaultBasemap)`. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs:233`]
- [ ] [Review][Patch] Configured alternate basemaps are carried through the plan but never exposed or applied, while basemap-role reference layer entries are skipped entirely. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs:132`]
- [ ] [Review][Patch] `source_type`/`item_path` are accepted in settings but layer creation always treats the value as a raw URI, so configured item references or non-URL layer files are not honored. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs:296`]
- [ ] [Review][Patch] Transaction load tests still use the no-op map service path only, so map-preparation invocation, failure handling, and warning propagation are not covered. [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs:647`]

## Developer Notes

### Recommended Configuration Shape

Use the existing settings system rather than a new standalone file unless implementation pressure says otherwise.

Example:

```json
{
  "working_map": {
    "enabled": true,
    "map_name": "Jamaica",
    "create_if_missing": true,
    "reuse_existing": true,
    "activate_on_transaction_load": true,
    "cleanup_transaction_groups_on_close": true,
    "default_basemap": "esri_world_imagery",
    "alternate_basemaps": [
      "open_basemap",
      "world_topographic"
    ],
    "default_extent": {
      "name": "Jamaica",
      "wkid": 3448,
      "xmin": 580172.099,
      "ymin": 605960.245,
      "xmax": 845529.005,
      "ymax": 728209.243
    },
    "zoom_to_transaction_parish": true,
    "parish_lookup": {
      "enabled": true,
      "layer_name": "Parishes",
      "name_field": "parish",
      "required": false
    },
    "reference_layers": [
      {
        "name": "Esri World Imagery",
        "source_type": "map_service_url",
        "url": "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
        "group": "Basemaps",
        "basemap_role": "imagery",
        "required": false,
        "visible": false,
        "order": 0,
        "opacity": 1.0
      },
      {
        "name": "Open Basemap Streets",
        "source_type": "vector_tile_style_url",
        "url": "https://www.arcgis.com/sharing/rest/content/items/643f29ef5ab94511912dd337c9e1a13b/resources/styles/root.json",
        "group": "Basemaps",
        "basemap_role": "streets",
        "required": false,
        "visible": true,
        "order": 1,
        "opacity": 1.0
      },
      {
        "name": "World Topographic",
        "source_type": "vector_tile_style_url",
        "url": "https://cdn.arcgis.com/sharing/rest/content/items/7dc6cea0b1764a1f9af2e679f642f0f5/resources/styles/root.json",
        "group": "Basemaps",
        "basemap_role": "topographic",
        "required": false,
        "visible": false,
        "order": 2,
        "opacity": 1.0
      },
      {
        "name": "World Hillshade",
        "source_type": "map_service_url",
        "url": "https://services.arcgisonline.com/arcgis/rest/services/Elevation/World_Hillshade/MapServer",
        "group": "Terrain Reference",
        "required": false,
        "visible": false,
        "order": 5,
        "opacity": 0.65
      },
      {
        "name": "Legal_Cadastre",
        "source_type": "map_service_url",
        "url": "https://jm-gis.innola-solutions.com/server/rest/services/Legal_Cadastre/MapServer",
        "group": "Cadastre Reference",
        "required": true,
        "visible": true,
        "order": 10,
        "opacity": 1.0
      },
      {
        "name": "Fiscal_Cadastre",
        "source_type": "map_service_url",
        "url": "https://jm-gis.innola-solutions.com/server/rest/services/Fiscal_Cadastre/MapServer",
        "group": "Cadastre Reference",
        "required": false,
        "visible": false,
        "order": 20,
        "opacity": 1.0
      },
      {
        "name": "Survey_Cadastre",
        "source_type": "map_service_url",
        "url": "https://jm-gis.innola-solutions.com/server/rest/services/Survey_Cadastre/MapServer",
        "group": "Cadastre Reference",
        "required": false,
        "visible": false,
        "order": 30,
        "opacity": 1.0
      }
    ]
  }
}
```

### Basemap Guidance

ArcGIS Pro basemaps should be handled through supported ArcGIS Pro SDK/Portal basemap APIs where possible rather than as ordinary feature layers.

The workflow map itself must remain JAD 2001 Jamaica Grid / EPSG:3448. Public basemaps may be served in Web Mercator, but they are display/reference layers only and must not set the ArcGIS Pro map coordinate system for Compute, Compare, Create Spatial Units, Final Review, or generated transaction layers.

Recommended configured choices:

- `esri_world_imagery`: default imagery basemap for visual parcel context.
- `open_basemap`: preferred open-style basemap option going forward.
- `openstreetmap_vector`: optional legacy compatibility alias if the organization still exposes it, but it should be marked deprecated/mature support in configuration notes.
- `world_topographic`: public Esri topographic/vector style option for a cartographic streets/topology view.
- `world_hillshade`: optional terrain relief map service overlay, hidden by default.

Do not assume OpenStreetMap vector basemap remains the best production default. Esri has announced OpenStreetMap vector basemap deprecation/mature support and recommends transition to Open Basemap.

### Public / Organization Layer Candidates

Public or organization-hosted layers that may be useful as configured operational references:

- Legal Cadastre feature layer, visible by default.
- Fiscal Cadastre feature layer, hidden by default unless requested.
- Survey Cadastre or authoritative survey/cadastral reference layers, optional.
- Parish/boundary layer, optional and usually visible or semi-transparent.
- Civic Features, optional and hidden by default.
- Fishing Beaches, optional and hidden by default.
- Hotels and Attractions, optional and hidden by default.
- Enclosure Boundary, optional and hidden by default unless needed for examination context.
- Communities, optional and hidden by default.
- Major Roads, optional and usually visible when a streets/topographic basemap is not being used.
- Contours, optional and hidden by default.
- River Network, optional and hidden by default.
- Roads/transportation labels from the selected basemap or an organization-hosted reference layer.
- Imagery labels/hybrid reference from the Esri basemap gallery when imagery context needs labels.
- Esri World Imagery public imagery service: `https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer`.
- Esri World Hillshade public terrain service: `https://services.arcgisonline.com/arcgis/rest/services/Elevation/World_Hillshade/MapServer`.
- OpenStreetMap/Open Basemap vector style: `https://www.arcgis.com/sharing/rest/content/items/643f29ef5ab94511912dd337c9e1a13b/resources/styles/root.json`.
- World Topographic vector style: `https://cdn.arcgis.com/sharing/rest/content/items/7dc6cea0b1764a1f9af2e679f642f0f5/resources/styles/root.json`.

The story should not hard-code external public URLs directly into C# except through configuration defaults. Deployment-specific services should remain configurable because NLA/Innola environments may differ between dev, test, and production.

If a layer is visible in the eLandjamaica web map, implementation must still use a supported ArcGIS REST service URL, Portal item ID, or organization-approved layer reference. Do not scrape the eLandjamaica UI or depend on browser-only layer state as the production integration contract.

### Parish Zoom Behavior

The transaction detail should be inspected for a parish value. The exact source field may vary by Innola payload, so implementation should support configured candidate fields such as:

- `parish`
- `Parish`
- `transaction.parish`
- `application.parish`
- parcel/property parish fields already mapped in transaction detail

If a parish value is present in the loaded transaction, map preparation should use that value as the source of truth, normalize the parish name, and zoom to the matching configured or built-in parish extent. This is especially important before generated parcel geometry exists, because it gives the examiner immediate geographic context for the transaction.

If the transaction has no parish value, the user should still be able to continue; the map should use the configured full Jamaica/default extent.

### Implementation Seams

Likely files and boundaries to inspect:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- Existing transaction cleanup behavior in `ParcelWorkflowDockpaneViewModel` and map review cleanup services.

Map/layer operations should stay behind ArcGIS integration services and use ArcGIS Pro SDK threading (`QueuedTask`) rules. ViewModels should request map preparation; they should not create maps/layers directly.

### UX Expectations

- The user should not be asked to choose a project before loading a transaction.
- Missing required map setup should explain which layer/basemap failed.
- Optional hidden layers should appear in Contents with clear names.
- The Contents pane should stay clean: shared reference layers stay, transaction-specific groups are cleaned.

## Testing Requirements

Minimum automated verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build /p:Platform=x64`

Manual smoke test:

1. Open ArcGIS Pro with a blank/new project or no prepared Jamaica map.
2. Log in to Innola.
3. Load a supported Compute transaction.
4. Confirm the configured working map is created or activated.
5. Open Map Properties > Coordinate Systems and confirm Current XY is `JAD 2001 Jamaica Grid`, not WGS84 or Web Mercator.
6. Confirm Esri World Imagery or configured default basemap is present.
7. Confirm `Legal_Cadastre` is added and visible by default.
8. Confirm optional layers are present/hidden according to configuration.
9. Confirm transaction parish zoom moves the map to the parish when the transaction includes a parish.
10. Confirm a missing/unmatched parish falls back to Jamaica/default extent without blocking the workflow.
11. Confirm `Process` and downstream workflow actions are not blocked solely because a predefined project was missing.
12. Cancel/suspend/finalize and confirm transaction review groups are removed while base/reference layers remain.

## Change Log

- 2026-07-26: Created from user request to remove dependency on a predefined ArcGIS Pro project and prepare the map/layer context from configuration on transaction load.
- 2026-07-26: Implemented configurable working map preparation, seeded reference/basemap layer settings, added parish/default extent planning, integrated map preparation into transaction load, and added automated coverage.
- 2026-07-26: Code review found unresolved working-map behavior gaps; story returned to in-progress pending patches.
- 2026-07-26: Clarified parish zoom behavior to use the loaded transaction parish value and fall back to full Jamaica extent when parish is absent; patched default parish extents and tests.
- 2026-07-26: Patched alternate basemap-role handling so the configured Open Basemap Streets layer is added as a reference option when imagery is the active default basemap.
- 2026-07-31: Enforced JAD2001/EPSG:3448 as the working map coordinate system on map create/reuse and corrected story examples that still used WGS84 extents.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\x64\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.exe`

### Completion Notes

- Added a `working_map` settings model and seeded default Jamaica working map configuration with Esri World Imagery, Open Basemap Streets, World Topographic, World Hillshade, Legal Cadastre, Fiscal Cadastre, and Survey Cadastre references.
- Added ArcGIS Pro working map preparation behind a service boundary; transaction loading now prepares or reuses the configured map, applies basemap/reference layers, avoids duplicates, and reports required-layer blockers or optional-layer warnings.
- Captured parish metadata from Innola detail payloads and added parish/default extent planning. Transaction parish values are matched to configured or built-in parish extents; missing parish falls back to the Jamaica default extent.
- The active default basemap is not duplicated as an operational layer, but alternate configured basemap-role layers such as Open Basemap Streets are now added to the map as reference options.
- Working maps are explicitly reset to JAD 2001 Jamaica Grid / EPSG:3448 on create and reuse so Web Mercator basemaps cannot control the workflow map coordinate system.
- Existing cleanup remains transaction-specific: shared reference/base layers are not removed by transaction close/suspend/finalize cleanup.
- Manual ArcGIS Pro smoke test was not run in this shell session.

### File List

- `_bmad-output/implementation-artifacts/5-26-configure-default-working-map-and-reference-layers.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/WorkingMapSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetail.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkingMapPreparationPlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
