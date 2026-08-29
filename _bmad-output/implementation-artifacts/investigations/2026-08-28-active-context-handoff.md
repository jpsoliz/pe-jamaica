# Active Context Handoff: 2026-08-28

## Current Build Context

- Add-in package version `1.1.293` was generated and registered.
- Latest patch captures source document `Property` text as `survey_metadata.property_name`, carries it through review persistence, output summary, polygon feature rows, GeoJSON, and maps it to `SpatialUnitExt.propertyName`.
- Existing TR `100000851` artifacts still need extraction/review/output rerun before the property value will appear in case JSON; old artifacts did not contain `property_name`.
- TR `100000839` Spatial Unit area-unit issue was patched to use confirmed Innola dictionary code `area_unit_type_sqm`.
- Story `2-23f` crop PNG attachment patch allows replacing existing generated `st_plan_annex_image` transaction source registration.

## Verification Snapshot

- Python extraction/output chain: `python -m unittest src.ProcessingTools.tests.test_survey_plan_ocr_vision_extraction src.ProcessingTools.tests.test_output_adapter` with `PYTHONPATH=src/ProcessingTools` passed, 29 tests.
- Spatial Unit focused save test: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:BaseIntermediateOutputPath=.artifacts\obj\ -p:BaseOutputPath=.artifacts\bin\ "innola spatial unit service creates defaults then saves"` passed.
- Release build: passed; one existing nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.
- Package: `tools/package_addin.ps1 -Configuration Release` passed and registered `ParcelWorkflowAddIn.esriAddInX`.

## Operational Test Steps For TR 100000851

1. Restart ArcGIS Pro so add-in `1.1.293` loads.
2. Re-run extraction for TR `100000851`.
3. Confirm `working/survey_plan_extraction_summary.json` includes `survey_metadata.property_name`.
4. Open review and confirm `Property name` is visible/correct.
5. Run Create Spatial Units / Finalize.
6. Confirm `output/output_summary.json`, `output/extracted_geometry.geojson`, and `working/spatial_unit_api_payload.json` include `propertyName`.

## Sprint Tracker Snapshot

- Stories: backlog `20`, ready-for-dev `10`, in-progress `8`, review `49`, done `24`.
- Epics: backlog `2`, in-progress `7`, done `1`.
- Retrospectives: optional `9`, done `1`.
- Recommended tracker action: code review/close review stories before opening more development, with priority on currently touched stories `2-23f`, `4-11`, and `7-9`.
