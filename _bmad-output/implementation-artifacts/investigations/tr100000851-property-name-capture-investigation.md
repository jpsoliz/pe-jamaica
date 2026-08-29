# Investigation: TR 100000851 Property Name Capture

## Hand-off Brief

1. **What happened.** TR 100000851 extraction/review artifacts do not contain a captured property-name value; downstream output uses generic `survey-plan-parcel`.
2. **Where the case stands.** Evidence shows the OCR/vision extraction captured parish, document area, survey date, instrument, surveyor, and file reference, but not `surveyed_property_names`, `property_name`, or `propertyName`.
3. **Patch applied.** Extraction/property propagation now captures source `Property` as `survey_metadata.property_name`, carries it through review/output polygons and output summary, then maps generated polygon `propertyName`/`property_name` into `SpatialUnitExt.propertyName`.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR 100000851 |
| Date opened | 2026-08-28 |
| Status | Patched; requires rerun on case artifacts |
| System | ArcGIS Pro add-in, PXA Compute Survey Plan |
| Evidence sources | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000851`, repo source |

## Problem Statement

User asked whether property name is captured during extraction and how to update Spatial Unit property name.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| `survey_plan_extraction_summary.json` | Available | No property-name fields; metadata has parish, document area, survey date, instrument, surveyor, file reference. |
| `extraction_review_data.json` | Available | Review rows use generic `survey-plan-parcel`; no `surveyed_property_names` hit. |
| `extracted_geometry.geojson` | Available | Feature properties use `parcel_name = survey-plan-parcel`; no `propertyName`. |
| Spatial Unit mapper source | Available | Reads parcel id/name, parish, area, and SUID from GeoJSON; does not map `propertyName`. |

## Confirmed Findings

### Finding 1: Extraction did not capture property name for TR 100000851

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000851\working\survey_plan_extraction_summary.json`

**Detail:** The summary contains `survey_metadata.parish = Clarendon`, `survey_metadata.document_area = 6422.110 Sq. Meters`, and other metadata, but no `surveyed_property_names`, `property_name`, or `propertyName`.

### Finding 2: Reviewed geometry output has only generic parcel naming

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000851\output\extracted_geometry.geojson`

**Detail:** GeoJSON feature properties contain `parcel_name = survey-plan-parcel`.

### Finding 3: Spatial Unit save does not currently populate propertyName

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`

**Detail:** `ApplyWorkingPolygonSpatialUnitFields` maps PID/lot/label, parish, area, area units, and SUID, but not `propertyName`.

## Conclusion

**Confidence:** High

TR 100000851's existing artifacts do not currently have property name captured or propagated. The code is now patched so new extraction/review/output runs can carry the value to `SpatialUnitExt.propertyName`; the existing case must be rerun or manually reviewed so the value appears in `survey_plan_extraction_summary.json` / `extraction_review_data.json`.

## Post-Patch Verification

| Check | Result |
| --- | --- |
| Python extraction/output tests | Passed: 29 tests with property-name extraction, output summary, polygon rows, and GeoJSON assertions. |
| Spatial Unit save test | Passed: focused C# test asserts `SpatialUnitExt.propertyName` in the Innola save payload. |
| Release build | Passed: one existing nullable warning remains in `SurveyPlanBoundarySolverTests.cs`. |
| Add-in package | Passed: version `1.1.293` generated and registered. |

## Recommended Next Steps

### Fix direction

Rerun extraction/review output for TR 100000851, confirm the value appears in `survey_metadata.property_name`, then rerun Create Spatial Units/Finalize so `spatial_unit_api_payload.json` shows `propertyName`.

### Diagnostic

After rerunning extraction for TR 100000851, review these artifacts in order: `survey_plan_extraction_summary.json`, `extraction_review_data.json`, `extracted_geometry.geojson`, then `spatial_unit_api_payload.json`.
