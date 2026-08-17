---
baseline_commit: 453eac5
created: 2026-08-16
source_request: "Mary/Winston/Sally review for a new ArcGIS Pro parcel search tab using Legal, Fiscal/Cadastral, and Survey cadaster layers"
---

# Story 8.7: Add Parcel Search Dockpane Tab

Status: done

## Story

As a cadastral examiner,
I want a dedicated ArcGIS Pro search tab for Legal, Cadastral, and Survey parcel sources,
so that I can find parcels by volume/folio, owner name, PE/LandVal number, wildcard patterns, and parish, then see the matching parcels selected and symbolized in a reusable local working layer.

## Business Context

Examiners need a fast, transaction-independent parcel search surface inside ArcGIS Pro. The current map settings already prepare the cadaster reference layers, and the compare workflow already has enterprise cadaster layer mappings. This story turns those configured sources into a reusable search workflow that does not require a loaded transaction, while still respecting the existing local `ParcelWorkflowCases` storage convention.

The search uses the last three configured map reference layers:

- `Legal_Cadastre`
- `Fiscal_Cadastre`, shown to users as `Cadastral`
- `Survey_Cadastre`

Within those map services, search targets are configurable by source and sublayer:

- `Legal_Cadastre` > `Legal_Parcel`
- `Fiscal_Cadastre` > `Parcels`
- `Survey_Cadastre` > `COGO_Fabric`

The parish selector list is sourced from `Fiscal_Cadastre` > `Parishes`, field `Parish_nam`, unless settings override the parish list source.

Search results must be materialized into a per-user local file geodatabase so they can be selected, styled, cleared, and reused without creating a new transaction Case Folder.

## Acceptance Criteria

1. Given the add-in is loaded in ArcGIS Pro, when the user opens the parcel workflow/search surface, then a dedicated `Search` tab or dockpane section is available without requiring a transaction to be loaded.
2. Given Settings > Map Layers contains `Legal_Cadastre`, `Fiscal_Cadastre`, and `Survey_Cadastre`, when the Search tab loads, then the layer selector exposes user-facing options `Legal`, `Cadastral`, `Survey`, and `All`.
3. Given the user selects `Cadastral`, when search executes, then the implementation searches the configured `Fiscal_Cadastre` source.
4. Given `case_folder_output_root` is configured to the local `ParcelWorkflowCases` folder, when the first search runs for a user, then the add-in creates or reuses `ParcelWorkflowCases\GDB_[username]_working.gdb`.
5. Given the working GDB exists, when search results are returned, then the add-in creates or updates one reusable result feature class named `Parcel_Search_Results` and displays it under one result group named `Parcel Search Results`.
6. Given a previous search result group exists, when a new search runs, then the visible `Parcel Search Results` group is rebuilt from the reusable result feature class rather than adding timestamped duplicate map layers or duplicate result datasets.
7. Given search results are written, then each result row includes at minimum `SourceLayer`, `SourceDisplayName`, `SearchRunId`, `SearchTimestamp`, source object/global ID where available, parish where available, and the configured identifier fields returned by the source.
8. Given the user enters criteria in multiple fields, when search runs, then the criteria are combined using `AND`.
9. Given no criteria are entered and the user clicks Search, then no search is executed and the UI shows a non-blocking prompt to enter at least one criterion or filter.
10. Given the user enters a name such as `*smith*`, `SMITH`, or `smith`, when search runs, then name matching is case-insensitive and uses `*` as the multi-character wildcard.
11. Given the user enters number-like text such as `12??344?99`, `?23????`, or `3*`, when search runs, then `?` is treated as a single-character wildcard and `*` is treated as a multi-character wildcard.
12. Given PE number and LandVal number are stored as text fields, when wildcard criteria are used, then the query treats them as string/text attributes rather than numeric comparisons.
13. Given the user selects `All` parishes, when search runs, then no parish filter is applied.
14. Given the user selects one or more specific parishes, when search runs, then matching rows must satisfy the selected parish filter using configured parish fields.
15. Given the user selects `All` layers, when search runs, then the add-in runs one query per enabled configured source: Legal, Fiscal/Cadastral, and Survey.
16. Given some selected sources do not have a configured field for a requested criterion, when search runs, then that source is skipped for that criterion or excluded with a clear non-secret warning rather than failing the whole search.
17. Given results are returned from multiple sources, when the result layer is displayed, then one `Parcel_Search_Results` feature class is shown as multiple filtered child layers under the `Parcel Search Results` group: `Legal`, `Cadastral`, `Survey`, and `Other`, each filtered by `source_display_name` and symbolized independently so users can toggle sources on/off and inspect overlaps without multiplying data storage.
18. Given results are returned, when the map updates, then matching source parcels are selected where possible and the map zooms to the result extent.
19. Given no records match, when search completes, then the UI shows zero results, clears the active `Parcel Search Results` features, and does not remove configured cadaster reference layers.
20. Given the user clicks `Clear Search`, then map selection and result features are cleared while the reusable result group/layers, working GDB, configured sources, and form layout remain available for the next search.
21. Given service authentication, timeout, schema, or field-mapping failures occur, when search completes, then the UI reports source-specific diagnostics without exposing tokens, passwords, raw authorization payloads, or secret-bearing URLs.
22. Given configured source result limits are reached, when search completes, then the UI reports that results were limited and still displays the returned subset.
23. Given Settings is opened, when an administrator reviews map/search configuration, then the settings surface exposes or references the field mappings needed for Legal, Cadastral/Fiscal, and Survey search: service/layer URL, sublayer/table name, display name, lot number, parcel ID, PID, volume, folio, combined volume/folio title reference, DP number, PE number, R number, LandVal number, parish, object ID, global ID, and enabled state.
24. Given `compare_enterprise_cadaster` already contains query layer URLs and field mappings, when this story is implemented, then the new search configuration reuses or extends that settings shape instead of duplicating unrelated settings.
25. Given the parish selector loads, when the Fiscal Cadastre `Parishes` layer is configured, then the add-in extracts distinct parish names from configured field `Parish_nam` and includes `All` as the default option.
26. Given Legal `vol_folio` or Fiscal `title_reference` stores volume/folio in combined text form such as `1234/344` or `123/23`, when the user enters Volume and/or Folio, then the query planner searches the configured combined reference field using the configured separator/pattern rules as text.
27. Given an administrator opens Settings > Parcel Search, when they review parcel search configuration, then Legal, Cadastral/Fiscal, Survey, and parish lookup URLs, sublayers, display names, enabled states, search fields, and display fields are summarized in a compact grid, with raw `compare_enterprise_cadaster` JSON available under Advanced JSON.
28. Given automated tests run, then wildcard translation, case-insensitive name matching, AND criteria construction, per-layer query planning, working GDB path resolution, clear-search behavior, result metadata, parish-list loading, combined volume/folio fields, and settings parsing are covered with focused tests.

## UX Notes

Use a compact ArcGIS Pro-adjacent tab layout:

```text
Search

Sources
[x] Legal   [x] Cadastral   [x] Survey

Parish
[ All v ] or multi-select parish control

Search Criteria
Volume       [____________]
Folio        [____________]
Name         [*smith*_____]
PE Number    [12??344?99__]
LandVal No.  [?23????_____]

* = any characters. ? = one character for number-like text.

[ Search ] [ Clear Search ]

Results
23 parcels found
Layer: Parcel Search Results
Last updated: 2026-08-16 14:30

Legal [swatch]  Cadastral [swatch]  Survey [swatch]

[ Zoom to Results ]
```

UX guardrails:

- Do not use a marketing-style page or oversized hero layout.
- Keep controls dense and operational.
- Keep visible text direct and technical.
- Use a disabled Search button or inline validation when all criteria are empty.
- Keep `Clear Search` scoped to selection and active result features, not settings or source layers.
- Show `Cadastral` in the UI, but map it internally to `Fiscal_Cadastre`.

## Tasks / Subtasks

- [x] Add Search entrypoint and UI surface. (AC: 1-3, 8-15, 20)
  - [x] Add a Search tab/section to the ArcGIS Pro dockpane experience or a dedicated search dockpane registered in `Config.daml`.
  - [x] Add source scope controls for any combination of `Legal`, `Cadastral`, and `Survey`; all checked is equivalent to `All`.
  - [x] Add parish filter with `All` plus one/many parish selection.
  - [x] Add fields for Volume, Folio, Name, PE Number, and LandVal Number.
  - [x] Add Search, Clear Search, result count, last-updated text, and Zoom to Results controls.

- [x] Extend settings for parcel search mappings. (AC: 2-4, 16, 23-28)
  - [x] Reuse or extend `compare_enterprise_cadaster` in `Settings/WorkflowSettings.json`.
  - [x] Confirm Legal maps to `Legal_Cadastre`, Cadastral maps to `Fiscal_Cadastre`, and Survey maps to `Survey_Cadastre`.
  - [x] Ensure each source can configure map service URL, feature service/query URL when different, sublayer/table name, source name, display name, lot number, parcel ID, PID, volume, folio, combined volume/folio reference, DP number, PE number, R number, LandVal number, parish, object ID, and global ID.
  - [x] Add a PE number field mapping if it is not already represented by an existing configured identifier.
  - [x] Surface missing/unsupported field mappings in Settings validation without breaking unrelated map-layer configuration.
  - [x] Add configurable parish-list source settings for Fiscal_Cadastre > Parishes > `Parish_nam`.
  - [x] Expose parcel-search layer URLs, sublayers, display names, enabled flags, and field mappings in a dedicated Settings > Parcel Search tab.
  - [x] Keep Settings > Map Layers focused on basemap/reference layer planning, with Compare Neighbor Search controls separated from Parcel Search configuration.

- [x] Implement search query planning. (AC: 8-17, 21-22, 25-28)
  - [x] Add a query planner that builds one query per selected enabled source.
  - [x] Combine criteria with `AND`.
  - [x] Translate text wildcard `*` to the source SQL multi-character wildcard.
  - [x] Translate number-like text wildcards `?` and `*` to source SQL single-character and multi-character wildcards.
  - [x] Normalize name matching to case-insensitive behavior.
  - [x] Treat PE and LandVal as text fields.
  - [x] Respect configured result limit/page size and report limit truncation.
  - [x] Redact secret-bearing diagnostics.
  - [x] Query configured combined volume/folio fields such as Legal `vol_folio` and Fiscal `title_reference`.
  - [x] Return configurable source-specific identifiers including `lot_number`, `dp_number`, `r_number`, `Lv_number`, and `PE_number` in result out-fields.

- [x] Implement local working GDB and reusable result layer. (AC: 4-7, 17-20)
  - [x] Resolve `ParcelWorkflowCases` from `case_folder_output_root`.
  - [x] Resolve user name from the active Innola user when available, falling back to Windows identity if no Innola session is active.
  - [x] Create or reuse `ParcelWorkflowCases\GDB_[username]_working.gdb`.
  - [x] Create or clear/update a reusable result feature class backing `Parcel Search Results`.
  - [x] Add result metadata fields including `SourceLayer`, `SourceDisplayName`, `SearchRunId`, and `SearchTimestamp`.
  - [x] Preserve a single visible `Parcel Search Results` map group with filtered source child layers and update its contents for each search.
  - [x] Apply source-based child layer definition queries and symbology.
  - [x] Replace the deferred map integration placeholder with live FeatureServer query execution and FileGDB feature writes so searches produce rows in `Parcel Search Results`.

- [x] Integrate with ArcGIS Pro map selection and navigation. (AC: 18-20)
  - [x] Select matching source features where source layers are available in the active map.
  - [x] Add/update the result layer in the active configured working map.
  - [x] Zoom to the result extent after successful searches.
  - [x] Clear active map selection and result features when the user clicks Clear Search.

- [x] Add tests and smoke validation. (AC: 1-27)
  - [x] Add unit tests for settings parsing and mapping Cadastral to Fiscal.
  - [x] Add unit tests for wildcard translation and case-insensitive name query construction.
  - [x] Add unit tests for empty-criteria behavior and AND criteria construction.
  - [x] Add unit tests for working GDB path resolution.
  - [x] Add tests for clear-search behavior.
  - [x] Add map-integration seams or mocks for result layer update, selection, symbology, and zoom behavior.
  - [x] Add tests for configured sublayer names and source-specific fields.
  - [x] Add tests for parish-list source loading from Fiscal_Cadastre > Parishes > `Parish_nam`.
  - [x] Add tests for combined volume/folio/title-reference fields.
  - [x] Add tests for live search orchestration from selected source requests into result materialization.

### Review Findings

- [x] [Review][Patch] Do not report all failed source queries as a successful empty search [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:798]
- [x] [Review][Patch] Apply real source-based unique-value symbology for `Parcel Search Results` instead of one blue simple renderer [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:1468]
- [x] [Review][Patch] Select result/source parcels where possible and clear active map selection during Clear Search [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:865]
- [x] [Review][Patch] Stamp source object/global ID metadata fields from returned parcel attributes where configured [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:1302]
- [x] [Review][Patch] Load Parish combo options from configured `parish_source` instead of hard-coded parish names [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpaneViewModel.cs`:51]
- [x] [Review][Patch] Preserve all matching parish geometries for parish spatial filtering instead of using only the first returned feature [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:956]
- [x] [Review][Patch] Expand `Saint`/`St.` aliases for source-field parish fallback queries too [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:491]
- [x] [Review][Patch] Prevent missing label fields after `JSONToFeatures` from failing otherwise valid result materialization [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:1341]
- [x] [Review][Patch] Escape literal `%` and `_` in user-entered LIKE patterns so only `*` and `?` act as user wildcards [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`:163]
- [x] [Review][Patch] Validate parcel-search field-name syntax in Settings before saving [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`:853]
- [x] [Review][Patch] Guard Clear Search and Zoom to Results command failures with user-facing status messages [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpaneViewModel.cs`:247]
- [x] [Review][Patch] Make the parcel-search settings summary tolerate wrong JSON value types without crashing the Settings window [`src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`:379]

## Developer Notes

### Current Code Context

Relevant settings and UI files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

Relevant existing map/query seams:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/WorkingMapSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/WorkingMapPreloadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`

Current configured sources in `WorkflowSettings.json`:

- `working_map.reference_layers` contains `Legal_Cadastre`, `Fiscal_Cadastre`, and `Survey_Cadastre` as MapServer reference layers.
- `compare_enterprise_cadaster.legal.layer_url` should point to, or be resolvable to, `Legal_Cadastre` > `Legal_Parcel`.
- `compare_enterprise_cadaster.fiscal.layer_url` should point to, or be resolvable to, `Fiscal_Cadastre` > `Parcels` and should back the user-facing `Cadastral` option.
- `compare_enterprise_cadaster.survey.layer_url` should point to, or be resolvable to, `Survey_Cadastre` > `COGO_Fabric`.
- Parish options should be loaded from `Fiscal_Cadastre` > `Parishes`, configured parish name field `Parish_nam`.

The MapServer rows are appropriate for visible reference layers. The actual search should prefer the configured FeatureServer layer URLs and field mappings because those are better suited for queries and feature copying. Selected source layers do not need to be loaded in the active map for FeatureServer querying and result feature creation; they are needed only when the implementation also selects/highlights matching source parcels in the active map.

### Architectural Guardrails

- Keep ArcGIS Pro SDK operations inside `QueuedTask.Run` where required.
- Keep the UI/ViewModel separate from ArcGIS map API calls; introduce a parcel search service/map integration seam rather than embedding map manipulation directly into button handlers.
- Reuse existing settings loaders and document classes instead of parsing raw JSON ad hoc in the UI.
- Keep transaction Case Folders separate from this per-user search GDB. The search GDB belongs under the same `ParcelWorkflowCases` root but is not transaction-scoped.
- Do not create timestamped map layers. Use one visible `Parcel Search Results` group with filtered `Legal`, `Cadastral`, `Survey`, and `Other` child layers backed by the same reusable feature class; store timestamps in attributes/status.
- Avoid deleting the working GDB or configured reference layers during Clear Search.
- If a field is not configured for a source, fail that source gracefully with a warning and continue other selected sources when possible.

### Query Semantics

Search criteria:

- Volume
- Folio
- Name
- PE Number
- LandVal Number
- Parish
- Source layer scope

Source-specific query fields clarified on 2026-08-16:

- Legal `Legal_Parcel`: `lot_number`, `vol_folio` formatted like `1234/344`, `dp_number`, `pe_number`, `r_number`, `parish`.
- Fiscal `Parcels`: `Lv_number`, `LT_Volume`, `LT_Folio`, `Title_Reference` formatted like `123/23`, `dp_number`.
- Survey `COGO_Fabric`: `PE_number`.
- Fiscal parish list `Parishes`: `Parish_nam`.
- All fields above are text fields and must be configurable in Settings.

Settings location:

- `Settings/WorkflowSettings.json` > `compare_enterprise_cadaster.legal`
- `Settings/WorkflowSettings.json` > `compare_enterprise_cadaster.fiscal`
- `Settings/WorkflowSettings.json` > `compare_enterprise_cadaster.survey`
- `Settings/WorkflowSettings.json` > `compare_enterprise_cadaster.parish_source`
- ArcGIS Pro UI: Settings > Map Layers > Compare and Parcel Search > Parcel search layer and field mappings.

Rules:

- Multiple criteria use `AND`.
- Empty criteria are ignored.
- If all criteria are empty, no query runs.
- Name matching is case-insensitive.
- Text/name supports `*`.
- Number-like text supports `?` and `*`.
- PE and LandVal are text fields.
- Parish can be `All` or one/many values.
- Combined volume/folio fields must be configurable independently from separate volume and folio fields. If both Volume and Folio are entered, query the combined field using `{volume}/{folio}` where configured; if one part is entered, use wildcard matching against the combined field.
- Field matching must respect configured field names without assuming consistent casing, e.g. `Lv_number`, `LT_Volume`, `LT_Folio`, `PE_number`, and `pe_number`.

Implementation should centralize wildcard translation and SQL escaping so tests can verify it independent of ArcGIS Pro runtime.

### Local Working GDB Contract

Path:

```text
{case_folder_output_root}\GDB_[username]_working.gdb
```

Visible result layer:

```text
Parcel Search Results
```

Recommended result metadata fields:

- `source_layer`
- `source_display_name`
- `search_run_id`
- `search_timestamp`
- `source_object_id`
- `source_global_id`
- `parcel_id`
- `pid`
- `volume`
- `folio`
- `name`
- `pe_number`
- `landval_number`
- `parish`

Use existing project JSON style (`lowercase_snake_case`) for persisted settings and any audit/status artifacts. ArcGIS feature class field names can follow ArcGIS constraints but should stay recognizable.

### ArcGIS Pro SDK Notes

Official Esri references checked while writing this story:

- ArcGIS Pro SDK `QueryFilter` represents table queries and exposes `WhereClause`, `SubFields`, `RowCount`, and related query properties.
- ArcGIS Pro SDK feature/table `Select` operations and file geodatabase access must be run on the Main CIM Thread through `QueuedTask.Run`.
- Esri documents `FileGeodatabaseConnectionPath` for opening file geodatabases and notes that the path must end with `.gdb`.
- Esri documents the Create Feature Class geoprocessing tool as creating empty feature classes in an existing geodatabase or folder.

Practical implementation options:

- Use ArcGIS Pro SDK APIs for map/layer selection and styling.
- Use a focused geoprocessing/tool seam when creating or clearing feature classes is simpler and more reliable than fully hand-building schemas in C#.
- Preserve responsiveness: never run long service queries or feature-copy loops on the WPF UI thread.

### Testing Guidance

Run the existing solution checks after implementation:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build
```

Add focused tests for:

- `Cadastral` UI source resolves to Fiscal settings.
- `Legal`, `Fiscal/Cadastral`, and `Survey` each produce one query plan when `All` is selected.
- Empty search criteria blocks execution.
- `*smith*` matches owner/name case-insensitively.
- `12??344?99`, `?23????`, and `3*` translate as expected.
- Parish filters combine with other criteria using `AND`.
- Missing field mapping produces source warning, not an unhandled exception.
- Clear Search clears selection/result features but keeps the result layer contract.
- Result metadata includes source and search run fields.

## References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/7-5-refactor-configuration-into-editable-settings-workspace-with-functional-tabs.md`
- `_bmad-output/implementation-artifacts/8-4-add-legal-and-fiscal-cadaster-query-services-for-compare.md`
- `_bmad-output/implementation-artifacts/5-26-configure-default-working-map-and-reference-layers.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/ArcGisCompareMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`

## Open Questions

1. Result features should keep only the current active search rows or retain historical rows hidden by `SearchRunId`. Current acceptance criteria specify clearing/updating the active result features.

## Completion Status

Implementation complete. Search UI, clarified settings extensions, query planner, working-GDB path contract, result-layer metadata contract, live FeatureServer querying, FileGDB result materialization, map result-layer refresh, source-based symbology, result/source selection, clear selection behavior, zoom behavior, source sublayers, source-specific field mappings, combined volume/folio fields, configured parish list source loading, parish spatial filtering, popup configuration, and per-parcel labels are implemented. Code-review findings were applied and packaged in add-in version `1.1.162`.

## Dev Agent Record

### Debug Log

- Added failing parcel search tests first for layer-scope planning, wildcard SQL construction, missing field mappings, working GDB path resolution, result layer metadata, and clear-search semantics.
- Initial targeted test build was blocked by stale/missing WPF `.baml` intermediate files under `obj`; full solution build regenerated them.
- Full solution build passed after resolving a new view-model compile issue.
- Full executable harness ran through all new parcel-search tests successfully, then failed later in existing `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates` with expected `warning`, got `blocked`.
- Patched clarified source settings for `Legal_Parcel`, `Parcels`, `COGO_Fabric`, and `Parishes`.
- Default `obj\Debug\net8.0-windows` became OS-locked during validation; even elevated cleanup/ACL repair could not remove or grant access to the generated folder. Add-in project was rebuilt successfully using an external intermediate path.
- Replaced the deferred parcel-search map integration service with a live service that queries configured FeatureServer layers, materializes returned Esri JSON feature sets through ArcGIS geoprocessing, writes a reusable FileGDB result feature class, and reloads/zooms the map result layer.

### Completion Notes

- Implemented a transaction-independent Search dockpane registered in `Config.daml`.
- Added `ParcelSearchQueryPlanner` with Cadastral-to-Fiscal mapping, AND criteria, case-insensitive name search, `*`/`?` wildcard translation, source diagnostics, and result-limit/page-size propagation.
- Extended `compare_enterprise_cadaster` source settings with optional `pe_number_field` and exposed the full mapping JSON in Settings > Map Layers.
- Extended `compare_enterprise_cadaster` source settings with optional `sublayer_name`, `display_name`, `lot_number_field`, `combined_volume_folio_field`, `combined_volume_folio_separator`, `dp_number_field`, `r_number_field`, and a configurable `parish_source`.
- Exposed the parcel search layer URLs, sublayers, display names, enabled flags, and field mappings in Settings > Map Layers > Compare and Parcel Search.
- Updated default settings for Legal `Legal_Parcel`, Fiscal `Parcels`, Survey `COGO_Fabric`, and Fiscal parish source `Parishes.Parish_nam`.
- Updated planner support for combined volume/folio fields such as Legal `vol_folio` and Fiscal `title_reference`; source-specific identifiers are included in result out-fields.
- Added a per-user working GDB path resolver and result layer metadata contract for `Parcel Search Results`.
- Added an `IParcelSearchMapIntegrationService` implementation for live ArcGIS result feature materialization, map layer refresh, result selection, and best-effort source selection.
- Runtime status: live FeatureServer querying, FileGDB result feature creation, source selection, parish spatial filtering, popup configuration, and per-parcel labels are implemented.
- Improved Settings > Map Layers > Compare and Parcel Search with a parsed summary of configured Legal, Cadastral, Survey, and Parish List sources above the editable JSON.
- Updated the Search dockpane source scope control from a single layer selector to Legal/Cadastral/Survey checkboxes so users can search all, one, or two configured sources.
- Updated Parish from free text to a combo box with `All` plus the Jamaica parish options.
- Clear Search is disabled until there is an active result set to clear, and becomes disabled again after clear, blocked search, or failed search.
- Planner now treats an explicit empty source selection as blocked instead of falling back to all sources.
- Implemented live FeatureServer query orchestration for selected parcel search sources.
- Implemented FileGDB creation/reuse for `GDB_[username]_working.gdb`.
- Implemented GP-based JSON-to-feature conversion, metadata stamping, and merge/copy into `Parcel_Search_Results`.
- Implemented result-layer rebuild in the active map and automatic zoom after successful searches.
- Clear/no-result paths now clear result rows where a reusable result feature class exists instead of deleting the working GDB or configured source layers.
- Corrected Fiscal Cadastre defaults to use live layer field names `LT_Volume`, `LT_Folio`, and `Title_Reference`; planner now prefers separate Fiscal volume/folio fields when both are configured and both criteria are entered.
- Wired specific Parish selections through the configured `parish_source` layer by querying the parish geometry and passing it as an `esriSpatialRelIntersects` spatial filter to each selected parcel source.
- Parish-only searches now execute against selected parcel sources with `WHERE 1=1` plus the parish spatial filter instead of being rejected for missing attribute criteria.
- Search execution now stops before planning/querying when no active ArcGIS Pro map is available, with a user-facing status message.
- Parish geometry lookup now expands `Saint ...` values to `St ...`, `St. ...`, and `St....` aliases so UI names like `Saint Thomas` match service values such as `ST.THOMAS`.
- Parcel source queries with a parish geometry now use POST form parameters to avoid losing large polygon filters in long GET URLs.
- If a requested parish geometry cannot be resolved, Search now clears/keeps the reusable result layer empty and stops instead of running unfiltered parcel-source queries.
- Diagnostics now record each source `WHERE` clause before the FeatureServer request and include returned ArcGIS error codes/details when available, so Legal failures show the query that the service rejected.
- `Parcel Search Results` now applies a curated popup/field display profile when the result layer is rebuilt: source and parcel identifiers are renamed for readability, useful result fields are ordered first, and technical fields such as object IDs, shape fields, `search_run_id`, and edit audit fields are hidden.
- Popup field display now uses the ArcGIS Pro `FeatureLayer.GetFieldDescriptions()` / `SetFieldDescriptions()` API instead of CIM `FeatureTable.FieldDescriptions`, because default CIM field descriptions can be unavailable for newly created result layers.
- The popup/field display list is configurable in `compare_enterprise_cadaster.popup_fields`; defaults include PID, strata extension, LandVal number, title reference, volume/folio, LT multiple, R/DP/lot numbers, street/scheme address, location, district, and parish.
- `popup_fields` is now authoritative for popup visibility: configured visible fields are shown first with configured aliases, configured hidden fields remain hidden, and unconfigured fields are hidden.
- Search dockpane UX now groups Sources + Parish into a single scope panel, groups criteria into its own panel, adds R Number and DP Number criteria, and keeps the visible message area to one concise status line. Query diagnostics and working GDB paths are written to a local parcel-search log instead of displayed in the pane.
- Result parcels now need labels inside each returned parcel based on the actual returned parcel attribute values for the non-parish criteria used in the search. Parish remains a spatial/filter constraint only and is excluded from labels. For example, a LandVal search labels each parcel with its own `Lv_number`; a volume/folio search labels each parcel with its own `LT_Volume`/`LT_Folio`, `vol_folio`, or `Title_Reference` depending on source mapping.
- `Parcel Search Results` now stamps a configurable `search_label` value per materialized parcel from the actual returned attribute values for each active non-parish criterion, then enables labels on the result layer so each parcel displays its own search value in the map.
- Runtime query regression review found that live Cadastral FeatureServer queries were returning far enough to reach `JSONToFeatures`, but result materialization failed because the service `GlobalID` field was included. Result queries now request configured `outFields` instead of `*`, exclude/sanitize `GlobalID`, and show the relevant `WHERE`/`outFields` diagnostics in the dockpane and local log.
- Legal and number-like parcel search fields now use plain `field LIKE 'pattern'` instead of `UPPER(field) LIKE ...`; name search remains case-insensitive.
- Follow-up live review found the configured `outFields` list can still break Legal/Cadastral FeatureServer queries when settings include optional fields missing from the specific sublayer. Parcel search now sends `outFields=*` for service compatibility and relies on the local `GlobalID` sanitizer before `JSONToFeatures`.
- Removed the duplicate in-pane `Search` heading; the ArcGIS dockpane title remains the only top title.
- Legal LandVal searches now use the live Legal_Parcel `Lv_NUMBER` field when the existing settings value is blank or a legacy `lv_number` casing. Result materialization also stamps a normalized `landval_number` text field from the configured source field, and popup config uses that stable result field for `LandVal No.`.
- `Parcel Search Results` now uses one reusable `Parcel_Search_Results` feature class with multiple filtered child layers under one result group: `Legal`, `Cadastral`, `Survey`, and `Other`. Each child layer filters on `source_display_name`, giving source visibility toggles for overlap inspection without multiplying the stored result data.
- LandVal-only result labels are now de-duplicated before query planning and again before ArcGIS calculates `search_label`, so one selected source/child layer renders one `LandVal No.` label line per parcel instead of repeating the same label text.
- Result child layer colors are retained, with polygon fills set to 70% transparency and outlines reduced to a very thin line for cleaner overlap inspection.

### Verification

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` - Passed; existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 8 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "compare enterprise cadaster settings"` - Passed 1 test.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` - Failed in existing survey-plan boundary solver test after all parcel-search tests passed.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\obj\addin\` - Passed after the clarified settings/planner patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /m:1 /p:UseSharedCompilation=false` - Blocked by access denied writing generated add-in files under `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\obj\Debug\net8.0-windows`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false` - Blocked by the same locked add-in `obj` folder before the fresh harness could build.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed with elevated SDK read access; normal sandbox hit `C:\Users\js91482\AppData\Local\Microsoft SDKs` access denied.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-tests-obj-...\ -v:minimal` - Blocked by existing `WindowsBase` reference conflict in `JamaicaReviewWorkspaceXamlTests.cs` before the focused parcel-search harness could run.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after live FeatureServer/GDB materialization implementation.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 15 focused parcel-search tests after refreshing the test output add-in DLL. Rebuilding the full harness remains blocked by existing `WindowsBase` conflict in unrelated `JamaicaReviewWorkspaceXamlTests.cs`.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.140`. Existing warning remains for locked default add-in `obj`, but package build uses fresh `.artifacts\msbuild-obj` intermediate folders.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after Fiscal field-name/planner correction.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\ -v:minimal` - Blocked by existing unrelated `WindowsBase` conflict in `JamaicaReviewWorkspaceXamlTests.cs`.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.142` with Fiscal query fix.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after parish spatial filter and active-map validation patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\ -v:quiet` - Still blocked by existing unrelated `WindowsBase` conflict in `JamaicaReviewWorkspaceXamlTests.cs` after add-in build passed.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.146` with parish spatial filter and active-map validation.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after alias/POST/no-unfiltered-fallback parish fix.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\ -v:quiet` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 20 focused parcel-search tests.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.148` with the corrected parish filter behavior.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after adding per-source failed-query diagnostics.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.150` with improved FeatureServer query diagnostics.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after result-layer popup field profile customization.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.151` with curated `Parcel Search Results` popup fields.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after switching popup customization to `GetFieldDescriptions`/`SetFieldDescriptions`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\ -v:quiet` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 20 focused parcel-search tests.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.153` with configurable popup fields.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed after changing result display to a grouped set of filtered source child layers.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release --no-build -- "parcel search"` - Passed 31 focused parcel-search tests.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.176` with grouped `Parcel Search Results` source child layers. Packaging still reported the existing non-fatal `RegisterAddIn.exe` PATH warning after producing the add-in file.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed after LandVal result-label de-duplication.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release --no-build -- "parcel search"` - Passed 32 focused parcel-search tests including the LandVal-only duplicate-label regression.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.178` with LandVal result-label de-duplication. Packaging still reported the existing non-fatal `RegisterAddIn.exe` PATH warning after producing the add-in file.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed after result child layer transparency/outline styling changes.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release --no-build -- "parcel search"` - Passed 32 focused parcel-search tests.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.179` with 70% transparent result fills and very thin outlines. Packaging still reported the existing non-fatal `RegisterAddIn.exe` PATH warning after producing the add-in file.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after making `popup_fields` authoritative for field visibility.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.155` with unconfigured popup fields hidden.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after Search UX grouping, R/DP criteria, and parcel-search log changes.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\ -v:quiet` - Blocked by existing unrelated `WindowsBase` conflict in `JamaicaReviewWorkspaceXamlTests.cs`.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.157` with Search UX grouping, R/DP criteria, and log-only diagnostics.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after per-parcel result-label implementation.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-tests-obj-...` - Blocked by existing unrelated `WindowsBase` conflict in `JamaicaReviewWorkspaceXamlTests.cs` before the registered parcel-search tests could run.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.160` with per-parcel result labels. Existing warning remains for locked default add-in `obj`; package build used fresh `.artifacts\msbuild-obj` intermediate folders.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=$env:TEMP\pe-jamaica-addin-obj-...\ -v:minimal` - Passed after code-review fixes.
- Shared-temp add-in/test harness build with `BuildProjectReferences=false` - Passed; existing unrelated nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 26 focused parcel-search tests after review fixes.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` - Ran through all parcel-search tests successfully, then failed in existing unrelated `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates`.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.162` with code-review fixes. Existing warning remains for locked default add-in `obj`; package build used fresh `.artifacts\msbuild-obj` intermediate folders.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release --no-build -- "parcel search"` - Passed 30 focused parcel-search tests, including the regression check that FeatureServer queries use configured `outFields` and exclude `GlobalID`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed after the query/materialization regression patch.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.168` with configured-outFields/GlobalID materialization fix and visible query diagnostics. Existing warning remains for locked default add-in `obj`; package build used fresh `.artifacts\msbuild-obj` intermediate folders.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release --no-build -- "parcel search"` - Passed 30 focused parcel-search tests after restoring wildcard FeatureServer out-fields for service compatibility.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed after the duplicate Search heading removal and `outFields=*` patch.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.171` with wildcard FeatureServer out-fields and local `GlobalID` sanitization. Existing warning remains for locked default add-in `obj`; package build used fresh `.artifacts\msbuild-obj` intermediate folders.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed after Legal LandVal mapping and normalized result LandVal field fix.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release /m:1 /p:UseSharedCompilation=false` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release --no-build -- "parcel search"` - Passed 31 focused parcel-search tests, including Legal blank LandVal mapping migration to `Lv_NUMBER`.
- `tools\package_addin.ps1 -Configuration Release` - Passed; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.174` with Legal `Lv_NUMBER` LandVal mapping and normalized `landval_number` popup field. Existing warning remains for locked default add-in `obj`; package build used fresh `.artifacts\msbuild-obj` intermediate folders.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\` - Passed with existing unrelated nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 34 focused parcel-search tests, including field-map-safe result label resolution and label diagnostic formatting.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` - Ran through parcel-search tests successfully, then failed in existing unrelated `SurveyPlanBoundarySolverTests.RebuildKeepsConflictingPrintedReferenceCoordinates`.
- `tools\package_addin.ps1 -Configuration Release` - Passed after sandbox escalation for local Microsoft SDK metadata access; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.182`. Existing non-fatal warnings remain for locked default add-in `obj` cleanup and `RegisterAddIn.exe` PATH registration.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=D:\Code\BMad-Method\dev\pe-jamaica\.tmp\parcel-search-tests-obj\` - Passed after moving parcel-search settings into a dedicated tab; existing unrelated nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "settings workspace"` - Passed 8 focused settings workspace tests, including the new Parcel Search tab position and source JSON save round-trip.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- "parcel search"` - Passed 34 focused parcel-search tests after the settings-tab UX change.
- `tools\package_addin.ps1 -Configuration Release` - Passed after sandbox escalation for local Microsoft SDK metadata access; produced `ParcelWorkflowAddIn.esriAddInX` version `1.1.184` with the dedicated Settings > Parcel Search tab. Existing non-fatal warnings remain for locked default add-in `obj` cleanup and `RegisterAddIn.exe` PATH registration.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpane.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearchDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowParcelSearchDockpaneButton.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareEnterpriseCadasterEvidenceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelSearch/ParcelSearchServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`

### Change Log

- 2026-08-16: Added parcel search dockpane slice, search settings extensions, query planner, test coverage, and story progress notes.
- 2026-08-16: Updated story with clarified Legal/Fiscal/Survey sublayers, configurable source field mappings, Fiscal parish-list source, and combined volume/folio query behavior.
- 2026-08-16: Patched code for clarified sublayer/source field settings, Fiscal parish source settings, combined volume/folio planning, and focused tests.
- 2026-08-16: Added multi-source checkbox scope, parish combo box, disabled Clear Search state, and explicit empty-source blocking.
- 2026-08-16: Clarified parcel-search layer and field mapping settings location and made the Settings UI label explicit.
- 2026-08-16: Added settings summary UX for search source mappings and documented that GDB result population requires the next live ArcGIS integration slice.
- 2026-08-16: Implemented live parcel-search FeatureServer querying, FileGDB result materialization, result layer update, zoom, focused orchestration test, and Release package `1.1.140`.
- 2026-08-16: Fixed Fiscal volume/folio search to use `LT_Volume` and `LT_Folio` instead of combined `Title_Reference` when both fields are available.
- 2026-08-16: Packaged Fiscal query fix as add-in version `1.1.142`.
- 2026-08-16: Changed live FeatureServer parcel-search requests to use `outFields=*` to avoid source failures from optional configured fields that are not present in a service layer; packaged as add-in version `1.1.144`.
- 2026-08-16: Wired Parish combo to configured `parish_source` geometry as a spatial filter, allowed parish-only spatial searches, added active-map validation before Search, and packaged as add-in version `1.1.146`.
- 2026-08-16: Fixed parish filtering to match `Saint`/`St` service-name variants, POST large geometry filters, and block unfiltered source queries when parish geometry cannot be applied; packaged as add-in version `1.1.148`.
- 2026-08-16: Added failed source-query diagnostics and packaged as add-in version `1.1.150` to expose the exact Legal query rejected by the FeatureServer.
- 2026-08-16: Added curated `Parcel Search Results` popup/field display aliases and technical-field hiding; packaged as add-in version `1.1.151`.
- 2026-08-16: Replaced unavailable CIM field-description path with ArcGIS Pro field-description APIs, added configurable `popup_fields`, and packaged as add-in version `1.1.153`.
- 2026-08-16: Made `popup_fields` authoritative so unconfigured result fields no longer appear in the popup; packaged as add-in version `1.1.155`.
- 2026-08-16: Packaged Search UX grouping, R/DP criteria, and log-only query/GDB diagnostics as add-in version `1.1.157`.
- 2026-08-16: Added requirement to label result parcels from returned parcel attribute values for all active non-parish search criteria.
- 2026-08-16: Updated Parcel Search Results to display one result feature class as multiple filtered source child layers under one result group for per-source visibility control without duplicate storage.
- 2026-08-16: Packaged grouped Parcel Search Results source child layers as add-in version `1.1.176`.
- 2026-08-16: De-duplicated active result label fields so LandVal-only searches render one `LandVal No.` label line per parcel.
- 2026-08-16: Packaged LandVal duplicate-label fix as add-in version `1.1.178`.
- 2026-08-16: Adjusted Parcel Search Results child layer symbology to 70% transparent fills with very thin outlines.
- 2026-08-16: Packaged Parcel Search Results transparency/outline styling as add-in version `1.1.179`.
- 2026-08-16: Made Parcel Search Results display fields and labels source-safe by resolving configured field names to actual returned field names, stamping normalized display fields per source, and writing source/configured/actual/sample label diagnostics to the search log.
- 2026-08-16: Packaged source-safe Parcel Search Results label/display-field diagnostics as add-in version `1.1.182`.
- 2026-08-16: Updated Search UX grouping, added R Number/DP Number criteria, and moved query/GDB diagnostics out of the pane into the parcel-search log.
- 2026-08-16: Implemented per-parcel result labels from returned attribute values for active non-parish search criteria and packaged as add-in version `1.1.160`.
- 2026-08-16: Applied code-review fixes for failed-query handling, source symbology, source/result selection, metadata stamping, configured parish loading, multi-geometry parish filters, label robustness, LIKE literal escaping, settings validation, command error handling, and settings-summary robustness; packaged as add-in version `1.1.162`.
- 2026-08-16: Deep-reviewed live query failures from `parcel_search.log`, fixed `JSONToFeatures` failure on service `GlobalID`, restored visible WHERE/outFields diagnostics, removed `UPPER(...)` from number-like fields, and packaged as add-in version `1.1.168`.
- 2026-08-16: Reviewed live 400 failures caused by configured optional `outFields`, restored service-compatible `outFields=*` with local `GlobalID` sanitization, removed duplicate Search title, and packaged as add-in version `1.1.171`.
- 2026-08-16: Fixed Legal LandVal source mapping to `Lv_NUMBER`, normalized result popup LandVal values into `landval_number`, and packaged as add-in version `1.1.174`.
- 2026-08-16: Moved parcel-search configuration into a dedicated Settings > Parcel Search tab, kept Settings > Map Layers focused on reference/basemap layers, separated Compare Neighbor Search controls, hid raw JSON under Advanced JSON, added settings workspace test coverage, and packaged as add-in version `1.1.184`.
