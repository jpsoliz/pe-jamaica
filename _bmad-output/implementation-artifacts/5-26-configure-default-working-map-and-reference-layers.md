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

## Performance Amendment: Reuse First, Parish First, Warm Later

Field testing on TR 100000627 and related transaction-load flows showed that selecting a transaction can feel slow because ArcGIS Pro initializes imagery, cadastre, survey, and other reference services during the same user-visible load path. The existing story already owns working-map reuse, duplicate prevention, and transaction parish zoom. This amendment tightens that behavior into a performance requirement:

- If the configured working map and required reference layers are already present, transaction load should not rebuild or re-add the map context.
- Transaction parish should be used as the first spatial context whenever available, so the user lands in the relevant parish instead of waiting on broad Jamaica/full-extent drawing.
- Heavy reference layer creation should be limited to missing layers only.
- Optional preload/warm-up may happen outside transaction selection, but it must never become a hard dependency for opening a transaction.

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
23. Given the configured working map is already open or available in the project and all required reference layers are already present, when a transaction is selected, then map preparation skips full reference-layer creation and only activates/reuses the map, verifies JAD2001, applies safe display settings if needed, and zooms to the transaction context.
24. Given the selected transaction includes a parish, when map preparation runs, then the map zooms to the matching parish extent before adding any missing heavy reference layers where ArcGIS Pro SDK ordering allows it.
25. Given only optional reference layers such as imagery alternatives, Fiscal Cadastre, Survey Cadastre, hillshade, or other context layers are missing, when transaction load runs, then the transaction can continue while those layers are added lazily, skipped, or reported as non-blocking warnings according to configuration.
26. Given a background working-map preload is enabled after login/startup, when preload succeeds, then later transaction selection reuses the warmed map/layers instead of paying the full setup cost in the transaction selection path.
27. Given a background working-map preload fails, times out, or is cancelled, when the user selects a transaction, then normal transaction load still prepares or reuses the map through the existing foreground path and reports only actionable required-layer blockers.

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

- [x] Optimize transaction-load map preparation. (AC: 23-25)
  - [x] Add an explicit prepared-map check that verifies configured map name, JAD2001 spatial reference, required reference layers, and configured layer URL/name identity.
  - [x] If the map is prepared, skip full reference-layer creation and perform only activation, JAD2001 verification, safe property refresh, and transaction-context zoom.
  - [x] Reorder preparation so transaction parish/default extent zoom happens before missing heavy reference layer creation where ArcGIS Pro SDK behavior permits.
  - [x] Add only missing required layers in the foreground transaction-load path.
  - [x] Treat missing optional layers as warnings or lazy/background candidates, not transaction blockers.
  - [x] Add timing/status diagnostics that make clear whether the map was reused, partially prepared, or fully prepared.

- [x] Add optional working-map preload/warm-up. (AC: 26-27)
  - [x] Start preload after Innola login or app startup only when configured/enabled.
  - [x] Preload should prepare the configured shared working map and required reference layers without selecting or claiming a transaction.
  - [x] Preload must be cancellable and must not block transaction list refresh or transaction selection.
  - [x] If preload fails, store/report a non-blocking warning and allow foreground transaction load to prepare the map normally.
  - [x] Add tests for preload success, preload failure fallback, and no duplicate foreground work after a successful preload.

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
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- Existing transaction cleanup behavior in `ParcelWorkflowDockpaneViewModel` and map review cleanup services.

Map/layer operations should stay behind ArcGIS integration services and use ArcGIS Pro SDK threading (`QueuedTask`) rules. ViewModels should request map preparation; they should not create maps/layers directly.

### Performance Implementation Guidance

Prefer a conservative two-phase implementation:

1. Foreground quick win: keep the existing transaction load integration, but make `ArcGisWorkingMapPreparationService` reuse-first. Add an internal readiness check before `AddReferenceLayersAsync`; if all required layers are present, skip layer creation and zoom directly to the parish/default extent.
2. Background warm-up: add a separate optional preload entry point after login/startup. It may call the same map preparation service with no transaction detail or with a safe default extent, but must not claim a task, open a case folder, or make transaction selection depend on preload completion.

Do not remove the existing foreground map preparation path. Preload is an optimization only; transaction selection must remain correct when preload never ran.

### UX Expectations

- The user should not be asked to choose a project before loading a transaction.
- Missing required map setup should explain which layer/basemap failed.
- Optional hidden layers should appear in Contents with clear names.
- The Contents pane should stay clean: shared reference layers stay, transaction-specific groups are cleaned.

## Testing Requirements

Minimum automated verification:

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build /p:Platform=x64`
- Focused working-map tests should cover prepared-map detection, missing-required-layer behavior, missing-optional-layer warning behavior, parish-before-heavy-layer ordering where testable, and preload failure fallback.

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
- 2026-08-09: Added performance amendment for reuse-first transaction loading, parish-first zoom, missing-layer-only foreground preparation, and optional background working-map preload.
- 2026-08-09: Implemented the performance amendment with prepared-map evaluation, parish-first zoom before foreground missing-layer creation, foreground required/visible layer selection, and non-blocking post-login preload.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\x64\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.exe`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release` passed with 0 warnings and 0 errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "working map"` passed 16 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release` ran through the working-map tests and failed at the pre-existing `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates` assertion: expected `warning`, got `blocked`.

### Completion Notes

- Added a `working_map` settings model and seeded default Jamaica working map configuration with Esri World Imagery, Open Basemap Streets, World Topographic, World Hillshade, Legal Cadastre, Fiscal Cadastre, and Survey Cadastre references.
- Added ArcGIS Pro working map preparation behind a service boundary; transaction loading now prepares or reuses the configured map, applies basemap/reference layers, avoids duplicates, and reports required-layer blockers or optional-layer warnings.
- Captured parish metadata from Innola detail payloads and added parish/default extent planning. Transaction parish values are matched to configured or built-in parish extents; missing parish falls back to the Jamaica default extent.
- The active default basemap is not duplicated as an operational layer, but alternate configured basemap-role layers such as Open Basemap Streets are now added to the map as reference options.
- Working maps are explicitly reset to JAD 2001 Jamaica Grid / EPSG:3448 on create and reuse so Web Mercator basemaps cannot control the workflow map coordinate system.
- Existing cleanup remains transaction-specific: shared reference/base layers are not removed by transaction close/suspend/finalize cleanup.
- Added prepared-map evaluation based on configured required reference layers and existing layer name/URI snapshots; already-prepared maps now skip full reference-layer creation and report reuse.
- Moved transaction parish/default zoom before foreground missing-layer creation so ArcGIS starts in the relevant parish context before missing heavy services are added.
- Limited foreground transaction-load preparation to required and visible missing reference layers; hidden optional layers no longer slow transaction selection.
- Added `preload_after_login` and a non-blocking `WorkingMapPreloadService` kicked after login before auto-refresh; preload failure is captured as status and foreground transaction load remains authoritative.
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
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/WorkingMapPreloadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkingMapPreparationPlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
