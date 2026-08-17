# Investigation: Parcel Search Query Regression

## Hand-off Brief

1. **What happened.** User reports Parcel Search queries stopped working after the recent review/fix pass; screenshot evidence shows at least one matching Fiscal `Parcels` row exists while the dockpane reports zero results or a result write failure.
2. **Where the case stands.** Two regression mechanisms were confirmed in code and patched: stale user settings could keep old field names, and map refresh/decorations could turn a written result feature class into an overall failed search.
3. **What's needed next.** Install the packaged add-in and rerun one Cadastral-only query; if it still fails, collect `ParcelWorkflowCases\logs\parcel_search.log` to verify the remaining boundary.

## Case Info

| Field            | Value |
| ---------------- | ----- |
| Ticket           | N/A |
| Date opened      | 2026-08-16 |
| Status           | Active |
| System           | Windows, ArcGIS Pro add-in, .NET 8 WPF |
| Evidence sources | User screenshot, source code, git history, local tests |

## Problem Statement

User-reported description: "Non queries are working now... prior to the last patch and code review, it was working."

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| User screenshot | Partial | Shows Fiscal `Parcels` table has a matching row for `LT_Volume=1374`, `LT_Folio=140`, `R_Number=39700`, while dockpane reports no result/write failure. |
| Source code | Available | Parcel search implementation is in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`. |
| Version control | Partial | Parcel search story work is mostly uncommitted, so exact pre-review commit diff is limited. |
| Runtime ArcGIS Pro logs | Available | `ParcelWorkflowCases\logs\parcel_search.log` confirmed FeatureServer queries were sent and Cadastral result materialization failed at `JSONToFeatures` on `GlobalID`. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | FeatureServer query URL and WHERE construction | High | Done | Planner tests confirm expected Fiscal `LT_Volume`/`LT_Folio`/`R_Number` WHERE clauses. |
| 2 | FileGDB materialization after FeatureServer results | High | Done | Patched map refresh/decorations to be non-fatal after result feature class write. |
| 3 | Settings migration/default behavior | High | Done | Patched loader migration for known old Legal/Fiscal/Survey field names. |
| 4 | UI status/log truncation | Medium | Done | Query diagnostics are shown in the dockpane while the primary status remains one line. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-08-16 | Story 8.7 code-review fixes applied and packaged through 1.1.162. | Story notes and package output | Confirmed |
| 2026-08-16 | Additional query fix packaged as 1.1.164. | Package output | Confirmed |
| 2026-08-16 | User reports no queries work after latest patch/review. | User message | Confirmed |
| 2026-08-16 | Regression fixes added for stale settings migration and non-fatal map refresh failures. | Source code and focused tests | Confirmed |
| 2026-08-16 | Runtime log showed Cadastral `LT_Volume`/`LT_Folio` queries reaching materialization, then `JSONToFeatures` failed with `ERROR 001558 ... Field. [GlobalID]`. | `parcel_search.log` | Confirmed |

## Confirmed Findings

### Finding 1: A matching source row can exist while the result layer reports no output

**Evidence:** User screenshot in chat.

**Detail:** Fiscal source table shows `LT_Volume=1374`, `LT_Folio=140`, and `R_Number=39700` on a visible row. The dockpane still reports zero parcel search results or a write failure.

### Finding 2: Active settings could preserve stale pre-story field names

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`

**Detail:** `CompareEnterpriseCadasterSourceSettings.FromJson` previously honored configured values directly. If an existing user settings file contained legacy values such as Fiscal `r_number`, `lt_volume`, `lt_folio`, or Legal `vol_fol`, packaged defaults would not replace them.

### Finding 3: Map refresh was part of the fatal materialization path

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/ParcelSearchServices.cs`

**Detail:** `ArcGisParcelSearchResultMaterializer.MaterializeAsync` wrote/merged result feature classes and then called `AddResultLayerToMapAsync` before returning success. An exception in map layer refresh, labels, popup, symbology, or selection could therefore return "Parcel Search Results could not be written" even after the GDB write path had succeeded.

## Deduced Conclusions

### Deduction 1: The failure may be after source data availability

**Based on:** Finding 1.

**Reasoning:** Source data contains a candidate row matching the visible criteria. If the planned WHERE uses those exact fields and values, then an empty result implies either the query is not sent as expected, the active settings differ from the visible layer schema, the query target URL/layer is wrong, or the returned features fail during local materialization.

**Conclusion:** Investigation must trace all three boundaries: settings -> FeatureServer request -> FileGDB/result layer.

### Deduction 2: The regression likely combines settings drift and fatal map refresh

**Based on:** Findings 2 and 3.

**Reasoning:** User-reported "none queries work" is broader than one field mismatch, but screenshots already showed matching source data and a result write failure. Stale settings can cause wrong or excluded WHERE clauses; fatal map refresh can hide successful GDB writes as failed searches.

**Conclusion:** The fix must make known legacy settings self-heal and make map decorations non-fatal after result features are written.

## Hypothesized Paths

### Hypothesis 1: Active settings still contain stale Fiscal R-number mapping

**Status:** Confirmed

**Theory:** The add-in may load an existing user settings file from AssemblyCache/AppData rather than packaged `WorkflowSettings.json`, so package defaults do not correct `fiscal.r_number_field` if the existing file already has a stale value.

**Supporting indicators:** Screenshot shows live field is `R_Number`; earlier code/defaults used lowercase `r_number` in several places.

**Would confirm:** Runtime log shows `UPPER(r_number)` or source exclusion for R Number on Cadastral/Fiscal.

**Would refute:** Runtime log shows `UPPER(R_Number)` and the FeatureServer response still fails/returns zero.

**Resolution:** Patched field-name normalization for known legacy Legal, Fiscal, and Survey parcel search fields.

### Hypothesis 2: Review-era materialization changes fail every successful query

**Status:** Confirmed

**Theory:** FeatureServer returns results, but `JSONToFeatures`, metadata stamping, popup, labels, or source selection throws before the result layer is created.

**Supporting indicators:** Previous screenshot showed "Parcel Search Results could not be written" after a row was visible in the source layer.

**Would confirm:** Runtime log shows a `result=` failure naming `conversion.JSONToFeatures`, `management.CalculateField`, `LayerFactory`, popup, label, or selection.

**Would refute:** Runtime log shows no FeatureServer features returned.

**Resolution:** Patched `MaterializeAsync` so `AddResultLayerToMapAsync` failures are logged as diagnostics but do not turn a written result feature class into a failed search.

### Finding 4: Successful FeatureServer responses could fail during JSONToFeatures because of GlobalID

**Evidence:** `ParcelWorkflowCases\logs\parcel_search.log`

**Detail:** The log showed Cadastral queries such as `LT_Volume LIKE '999' AND LT_Folio LIKE '22%'` and `Lv_number LIKE '149020%'` being attempted, followed by `conversion.JSONToFeatures failed ... ERROR 001558 ... Field. [GlobalID]`.

**Resolution:** Patched FeatureServer requests to use configured `outFields` instead of `*`, exclude GlobalID-like fields from requested/materialization fields, and sanitize any returned GlobalID metadata/attributes before `JSONToFeatures`.

### Finding 5: Legal FeatureServer rejected UPPER around number-like fields

**Evidence:** `ParcelWorkflowCases\logs\parcel_search.log`

**Detail:** Legal queries such as `UPPER(vol_folio) LIKE '999/22%'` returned FeatureServer 400 "Unable to complete operation." These fields are configured as text, but the service SQL parser rejects the function expression.

**Resolution:** Patched number-like criteria (volume, folio, PE, LandVal, DP, R) to use plain `field LIKE 'pattern'`. Name search remains case-insensitive with `UPPER(...)`.

### Hypothesis 3: Source scopes with unsupported criteria cause perceived all-search failure

**Status:** Open

**Theory:** Leaving Legal/Cadastral/Survey all checked with a criterion not supported by some layers causes enough errors/noise that the successful source is not loaded or is obscured.

**Supporting indicators:** Survey lacks volume/folio/R fields; Legal/Fiscal field names differ.

**Would confirm:** Runtime diagnostics show source exclusions or source failures, while the intended single Cadastral query succeeds.

**Would refute:** A Cadastral-only query still fails the same way.

**Resolution:** Open.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| Latest `parcel_search.log` from `ParcelWorkflowCases\logs` | Confirms actual WHERE, source failures, and GP failure tool. | Obtained and reviewed. |
| Active user settings JSON from AssemblyCache/AppData | Confirms loaded field mappings, layer URLs, parish source, popup fields. | Use Settings active file path shown in Settings window or log settings source path. |
| Raw FeatureServer JSON from failed query | Confirms whether source returned features before materialization. | Capture URL/query params from diagnostics or add targeted debug output. |

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | Open |
| Trigger | Search button -> `ParcelSearchDockpaneViewModel.RunSearchAsync` |
| Condition | Search criteria present, selected sources queried, results expected in local working GDB |
| Related files | `ParcelSearchServices.cs`, `ParcelSearchDockpaneViewModel.cs`, `InnolaTransactionSettings.cs`, `WorkflowSettings.json` |

## Conclusion

**Confidence:** High

Root cause is confirmed by runtime logs and code: Cadastral queries were reaching FeatureServer, but result materialization failed because `JSONToFeatures` rejected the service `GlobalID` field. Legal also had a service-side SQL problem caused by wrapping number-like text fields in `UPPER(...)`.

## Recommended Next Steps

### Fix direction

Install the patched add-in and rerun the failing searches. The dockpane now shows the relevant WHERE/outFields diagnostics, and the log records the same details for support review.

### Diagnostic

Review `ParcelWorkflowCases\logs\parcel_search.log` after a failed search. It should include planned WHERE clauses, result status, and tool-level GP failures where applicable.

## Reproduction Plan

In ArcGIS Pro, run Parcel Search with Cadastral selected only, Parish `All`, `Volume=1374`, `Folio=140`, `R Number=39700`. Expected result: one or more Fiscal parcels selected and loaded into `Parcel Search Results`.

## Side Findings

- None yet.
