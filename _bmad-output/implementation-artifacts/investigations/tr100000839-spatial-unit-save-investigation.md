# Investigation: TR 100000839 Spatial Unit Save Failure

## Hand-off Brief

1. **What happened.** TR 100000839 published one computed polygon to the Enterprise working layers, but Innola Spatial Unit save failed with HTTP 400 `Failed to read request`.
2. **Where the case stands.** Confirmed evidence shows the failed payload is the only local case that sent `area_unit_type_square_meters` in SpatialUnitExt area unit fields; older successful cases did not send those fields.
3. **What's needed next.** Ship the recovery patch that keeps numeric area values and uses the confirmed `AreaUnitType` dictionary code `area_unit_type_sqm` for square metres.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR 100000839 |
| Date opened | 2026-08-28 |
| Status | Active |
| System | ArcGIS Pro add-in against `https://eltrs-dev.innola-solutions.com` |
| Evidence sources | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000839`, repo source, local prior case artifacts |

## Problem Statement

User reported that Spatial Units could not be saved for report/transaction 100000839 and asked whether this was caused by the area type.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| TR 100000839 case folder | Available | Contains Spatial Unit request, payload, failure, disposition, manifest, and output summary artifacts. |
| Local prior case folders | Available | Multiple recent cases contain successful `spatial_unit_api_response.json`; only 100000839 contains `area_unit_type_square_meters`. |
| Innola server dictionary | Available | User-provided screenshot confirms `AreaUnitType` code `area_unit_type_sqm` has name `sq m` and description `Square Meter`. |
| Source code | Available | `InnolaSpatialUnitService` added `area_unit_type_square_meters` locally. |

## Confirmed Findings

### Finding 1: Spatial Unit save failed at Innola save endpoint

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000839\working\spatial_unit_api_failure.json`

**Detail:** Failure was `400 BadRequest` at `/api/v4/rest/administrative/ladm-objects`, with response detail `Failed to read request`.

### Finding 2: The payload sent one SpatialUnitExt row with numeric area values and square-metre unit keys

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000839\working\spatial_unit_api_payload.json`

**Detail:** Payload included `area`, `legalArea`, `surveyArea`, `gisArea` as `1645.44931030273`, and set `legalAreaUnitType`, `surveyAreaUnitType`, and `gisAreaUnitType` to `area_unit_type_square_meters`.

### Finding 3: Previous local successful Spatial Unit saves did not send the square-metre area unit key

**Evidence:** Local case folder scan under `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases`

**Detail:** Recent successful cases include `_100000755`, `100000754`, `100000628`, `100000631`, `100000630`, and others. The only payload found with `area_unit_type_square_meters` was `100000839`, and it failed.

## Hypothesized Paths

### Hypothesis 1: Innola rejected the unconfirmed SpatialUnitExt area unit dictionary key

**Status:** Confirmed enough for recovery patch

**Theory:** The dev server does not recognize `area_unit_type_square_meters` for SpatialUnitExt area unit fields, so request deserialization fails before normal validation can return a more precise field error.

**Supporting indicators:** The key exists only in the new local patch and only in the failed case. Existing Postman evidence only proves `area_unit_type_hectares` on a Plan object, while SpatialUnitExt fixture examples leave area unit fields null.

**Would confirm:** Innola dictionary lookup or server team confirms the exact key for square metres, or a save succeeds with `area_unit_type_sqm`.

**Would refute:** A payload without area unit fields still fails with the same server response.

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`, `SaveSpatialUnitsAsync` |
| Trigger | Finalize approved Compute closeout creates default SpatialUnitExt rows, populates them from working polygon attributes, then saves them to Innola |
| Condition | Payload contains unverified `area_unit_type_square_meters` values for SpatialUnitExt area unit fields |
| Related files | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`, story `7-9` |

## Conclusion

**Confidence:** High

The numeric area is not the primary suspect. The rejected value was the invalid area unit type key `area_unit_type_square_meters`; the server dictionary confirms the square-metre key should be `area_unit_type_sqm`.

## Recommended Next Steps

### Fix direction

Patch the add-in so SpatialUnitExt continues to send `area`, `legalArea`, `surveyArea`, and `gisArea`, and sets `legalAreaUnitType`, `surveyAreaUnitType`, and `gisAreaUnitType` to `area_unit_type_sqm`.

### Diagnostic

Retry Finalize for TR 100000839 after installing the patched add-in. If it still fails, inspect the new `spatial_unit_api_failure.json` and compare the payload against this case.

## Reproduction Plan

1. Load transaction 100000839 against the dev Innola server.
2. Proceed to Final Review and run Finalize.
3. Expected result after patch: Spatial Unit save succeeds or produces a different, more specific failure unrelated to `area_unit_type_square_meters`.
