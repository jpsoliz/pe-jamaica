---
baseline_commit: 351ea1a1ec3f672492b093a30103e6aa5614c56d
---

# Story 4.13: Add Parish Boundary And Document Consistency Validation Rules

Status: in-progress

## Story

As a parcel workflow reviewer,
I want parish-boundary, embedded-computation-sheet, and printed-text-size checks to run as explicit validation rules,
so that PE/PXA parcel geometry and supporting document evidence can be accepted, blocked, or sent to manual review with clear, auditable reasons.

## Context

The PE/PXA extraction flow now has stronger document-type routing, PXA memorandum extraction, and semantic review rules. The next validation gap is not extraction itself, but whether the extracted parcel geometry and document evidence are internally consistent before downstream spatial-unit creation and final review.

JotaPe will load the parish boundaries into the GIS server. The application must expose that parish layer in the Settings Map Layers form and use the configured layer for validation. Public parish boundary datasets exist, but runtime validation should not depend on a public live service unless the deployment intentionally configures one.

## Acceptance Criteria

1. The workflow can validate reviewed points against the configured parish boundary for the parish extracted from the source document during Georeference Check.
2. The workflow can validate the created or reviewed parcel polygon against the configured parish boundary during Create Spatial Units and Final Review.
3. Parish validation supports point-within, polygon-within, and polygon-intersects/parish-overlap outcomes with configurable tolerance.
4. Missing parish layer, missing parish name field, missing extracted parish, unavailable geometry, unavailable spatial reference, or failed projection returns `not_available` or `needs_review`; it must not be reported as a false pass.
5. Settings > Map Layers lets an administrator add and review the parish layer URL/source type/group/visibility plus validation metadata: required, display order, opacity, parish name field, use for map zoom, and use for parish validation.
6. The configured parish layer is preserved in `WorkflowSettings.json` and does not break existing working-map reference-layer behavior.
7. Before or during PXA extraction, the workflow detects whether a source PDF contains an embedded computation sheet in addition to the plan, recording page numbers, confidence, and short evidence.
8. When an embedded computation sheet is detected, its values can be used as secondary extraction evidence for parcel values without replacing plan extraction evidence silently.
9. The workflow compares available plan-derived values and embedded-computation-sheet values for area, point identifiers, bearings, distances, easting/northing, parcel identifiers, and group identifiers where present.
10. Mismatches beyond configured tolerance are emitted as blocking or warning findings that require user disposition before downstream approval.
11. Matching plan and embedded-computation-sheet values are recorded as passed validation evidence.
12. The workflow estimates printed text height using actual PDF page dimensions where available, with A4 as the fallback page standard when metadata is absent or ambiguous.
13. The printed-text-size threshold is configurable and defaults to the assumed requirement that text smaller than 2 mm is non-compliant; if the policy is actually "must be less than 2 mm", the comparison direction must be configurable without code changes.
14. Raster-only pages, cropped scans, unknown DPI, or uncertain page scaling produce `needs_review` or `not_available` findings rather than false pass/fail results.
15. Parish-boundary, embedded-computation-sheet consistency, and printed-text-size findings appear in the stage review/findings panel with Accept, Reject, Override, and Send to Manual Review disposition actions where permitted by rule severity.
16. The Memorandum tab shows only memorandum-specific extraction/review evidence; cross-document validation findings may deep-link to memorandum, plan, or computation-sheet evidence but must also appear in the shared stage findings/disposition surface.
17. Final validation reporting includes parish-boundary, embedded-computation-sheet consistency, and printed-text-size findings with stage, severity, status, user disposition, evidence, and source document/page references.
18. Automated tests cover positive matches, mismatch findings, missing parish layer, missing parish in document, local-origin geometry, unavailable DPI/page scale, disposition persistence, and both text-size threshold directions.

## Tasks

- [x] Extend working-map settings for parish layer validation.
  - [x] Add editable Map Layers fields for required, display order, opacity, parish name field, use for zoom, and use for parish validation.
  - [x] Persist the fields to `working_map.reference_layers` without dropping existing unknown metadata.
  - [x] Keep `compare_enterprise_cadaster.parish_source` separate unless a later story explicitly maps it to the same layer.
- [ ] Add parish boundary validation service.
  - [x] Resolve the configured parish layer from settings.
  - [x] Match extracted parish text to the configured parish name field with normalized matching and clear no-match evidence.
  - [x] Project/align parish FeatureServer geometry to the review spatial reference via `outSR`; mismatched local-only boundaries return `needs_review`.
  - [x] Emit stage findings for point, polygon, and boundary-overlap results.
- [x] Add embedded computation-sheet detection for PXA documents.
  - [x] Reuse the existing structured PDF text extraction patterns where possible.
  - [x] Record page number, confidence, evidence snippets, and extracted value candidates.
  - [x] Make detection available before plan extraction decisions where technically feasible; otherwise record it in the extraction artifact before review approval.
- [x] Add plan-vs-computation-sheet comparison rules.
  - [x] Compare area, coordinates, bearings, distances, point identifiers, parcel identifiers, and group identifiers where both sides are available.
  - [x] Apply configured numeric tolerances and string normalization.
  - [x] Emit blockers/warnings for conflicts and passed evidence for matches.
- [x] Add printed-text-size validation.
  - [x] Determine page physical dimensions from PDF metadata; fall back to A4.
  - [x] Estimate printed text height from PDF text boxes where available.
  - [x] Return needs-review/not-available for raster-only or ambiguous scaling scenarios.
  - [x] Make threshold and comparison direction configurable.
- [x] Wire rule catalog, summaries, and final report output.
  - [x] Add rule IDs and defaults in the compute rule catalog/settings.
  - [x] Include findings in validation summary artifacts and final review reports.
- [ ] Add review/disposition UX for the new validation findings.
  - [x] Surface findings in the shared stage findings panel for Georeference Check, Create Spatial Units, Dimension/Validation Review, and Final Review as applicable.
  - [x] Allow Accept, Reject, Override, and Send to Manual Review actions according to severity and existing gate rules.
  - [ ] Link each finding to the best source evidence: parish boundary/map view, plan page, embedded computation-sheet page, or memorandum page.
  - [x] Persist the reviewer decision, timestamp, comment, and evidence reference in the case artifact.
- [ ] Add automated verification.
  - [x] Add unit tests for geometry/parish matching and projection cases.
  - [x] Add processing-tool tests for embedded compute sheet detection and text-size estimation.
  - [x] Add settings serialization tests for new map-layer metadata.

## Dev Notes

### Mary Review

This is a new Epic 4 validation story, not a small amendment to 4-12. Story 4-12 improved PE/PXA memorandum extraction and semantic review rules; this story adds downstream validation controls across spatial, document consistency, and document legibility checks.

Business rule summary:

- The parish named in the source document is authoritative evidence, but validation must be against a configured GIS boundary layer.
- A public parish boundary source can bootstrap setup, but production should use the parish layer JotaPe loads into the GIS server.
- Embedded computation sheets in PXA documents should be detected and used as corroborating evidence.
- Plan values and embedded computation-sheet values should agree within configured tolerances; disagreement should block or require review.
- Printed text-size policy needs product-owner confirmation. The likely cadastral rule is minimum legibility, so this story assumes text smaller than 2 mm is non-compliant while allowing the direction to be configured.
- The user should make the accept/reject/override decision from the shared stage findings surface. The Memorandum tab remains a source-specific review tab and should not become the single home for parish or plan-vs-computation validation.

### Winston Check

Recommended architecture:

- Treat parish boundaries as a configured reference layer in `working_map.reference_layers`.
- Add metadata to the existing map-layer settings model rather than creating a one-off parish settings screen.
- Run point validation after reviewed coordinates are available in Georeference Check.
- Run polygon validation after polygon creation/review in Create Spatial Units and again summarize it in Final Review.
- Keep validation findings in the existing stage finding/result artifact model used by `validation_summary.json`.
- Display the new results through the common stage finding/disposition model so they can be accepted, rejected, overridden, or routed to manual review consistently with other validation rules.
- Reuse `src/ProcessingTools/adapters/pdf_text_structured_extraction.py` for embedded computation-sheet detection instead of creating a separate PDF parser.

Candidate settings shape:

```json
{
  "name": "JM Parishes",
  "source_type": "feature_service_url",
  "url": "https://services6.arcgis.com/3R3y1KXaPJ9BFnsU/ArcGIS/rest/services/Jamaica_Parishes_SDC_Communities/FeatureServer/0",
  "group": "Administrative Boundaries",
  "visible": true,
  "required": false,
  "order": 40,
  "opacity": 0.45,
  "parish_name_field": "PARISH",
  "use_for_zoom": true,
  "use_for_validation": true
}
```

Candidate rule IDs:

- `georeference.parish_point_within_boundary`
- `spatial_units.parish_polygon_within_boundary`
- `pxa.embedded_compute_sheet_detected`
- `pxa.plan_compute_sheet_consistency`
- `document.printed_text_height`

### Amelia Validate

Implementation should preserve existing behavior when no parish layer is configured. In that case, emit a `not_available` finding and continue according to configured severity/gating.

Files likely to change:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Maps/IWorkingMapPreparationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/*`
- Stage review/disposition view models and XAML used by validation findings
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/rules/rules.yaml`

Test focus:

- Parish layer configured and point/polygon inside target parish.
- Parish mismatch or outside boundary.
- Parish layer missing.
- Extracted parish missing.
- Geometry uses local origin and cannot be projected safely.
- PXA plan plus embedded computation sheet with matching values.
- PXA plan plus embedded computation sheet with area/coordinate/bearing mismatch.
- Vector PDF text height above and below threshold.
- Raster or uncertain page scale returns review-needed.

## Public Parish Boundary Sources Checked

- NRIP Jamaica Parishes dataset: https://nrip.gov.jm/dataset/parishes
- Selected JM Parishes FeatureLayer: https://services6.arcgis.com/3R3y1KXaPJ9BFnsU/ArcGIS/rest/services/Jamaica_Parishes_SDC_Communities/FeatureServer/0 (`PARISH` display/name field)
- Esri Jamaica Boundaries item: https://www.arcgis.com/home/item.html?id=85ec74709a1f4444a56dadba68db2e7e

## Open Questions

1. Confirm the printed text-size rule direction: is the requirement minimum 2 mm text height, or maximum 2 mm text height?
2. If this public ArcGIS service is replaced by JotaPe's internal GIS server layer, confirm the replacement layer URL and parish name field.
3. Confirm numeric tolerances for area, bearings, distances, and coordinates when comparing plan values against embedded computation-sheet values.

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-09-02 | 0.1 | Initial story for parish boundary, embedded computation sheet, and printed text-size validation rules. | Mary/Winston/Amelia via Codex |
## Dev Agent Record

### Status

In progress. Settings metadata, local validation-adapter rules, embedded compute-sheet detection, rule catalog defaults, and targeted tests are implemented. Story is not marked complete because live parish-layer spatial query/projection must use JotaPe's internal GIS layer or another explicitly approved data path; the adapter currently avoids sending document-derived parish/coordinate data to the public ArcGIS service.

### Implementation Notes

- Added `JM Parishes` to working-map settings with validation, zoom, opacity, order, and `PARISH` field metadata.
- Extended settings workspace model, serialization, and XAML Map Layers grid for the new metadata.
- Added PXA embedded computation-sheet detection to structured PDF extraction artifacts.
- Added validation-summary findings for parish point/polygon validation, plan-vs-computation-sheet consistency, and printed text-size review.
- Parish validation supports configured local boundary boxes in settings and returns `not_available` or `needs_review` when boundary geometry/projection is unavailable.
- Plan-vs-computation-sheet comparison now checks point presence, coordinates, distance/length, bearings/azimuths, parcel/group IDs, from/to points, and area where both sides provide values.
- New top-level defaults in `rules.yaml` are read and merged with runtime settings; runtime settings override rule defaults.

### Files Changed

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/WorkingMapSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationFindingDisposition.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationFindingDispositionPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Settings/SettingsWorkspaceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ValidationFindingDispositionTests.cs`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/adapters/validation_adapter.py`
- `src/ProcessingTools/rules/rules.yaml`
- `src/ProcessingTools/tests/test_pdf_text_structured_extraction.py`
- `src/ProcessingTools/tests/test_validation_adapter.py`

### Verification

- `& $configured -m py_compile src/ProcessingTools/adapters/pdf_text_structured_extraction.py src/ProcessingTools/adapters/validation_adapter.py` passed.
- `& $configured -m pytest src/ProcessingTools/tests/test_pdf_text_structured_extraction.py src/ProcessingTools/tests/test_validation_adapter.py` passed: 19 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=obj-story-4-13b\` passed with existing warning `CS8629` in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- settings workspace` passed: 96 tests.
- `git diff --check` passed; only Git line-ending warnings were reported.

### Follow-up Required

- Add the internal GIS parish layer URL/field after JotaPe loads it, then implement the ArcGIS/local-layer geometry query and spatial-reference projection path.
- Decide whether printed text-size validation needs upstream extraction of PDF text-box metrics, or whether it remains a downstream consumer of `document_text_metrics` when provided.
- Remove generated `obj-story-4-13*` intermediate folders if Windows releases the locked paths; they are build scratch only.
### UX Continuation Notes - 2026-09-02

- Added actionable validation finding rows to the main workflow Create Spatial Units validation card. The Memorandum tab remains memorandum-only.
- Added Accept, Reject, Override, and Manual Review actions for each validation finding row.
- Added `validation_finding_dispositions.json` persistence for reviewer decision, timestamp, operator, comment field, and evidence reference.
- Added tests for disposition persistence, row projection, and XAML/command exposure.

### Additional Verification - 2026-09-02

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=obj-story-4-13-ux4\` passed with existing warning `CS8629` in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- validation finding` passed: 28 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -- settings workspace` passed: 96 tests.
- `& $configured -m pytest src/ProcessingTools/tests/test_pdf_text_structured_extraction.py src/ProcessingTools/tests/test_validation_adapter.py` passed: 19 tests.
- `git diff --check` passed after EOF cleanup; only Git line-ending warnings remain.

### Parish Service Continuation - 2026-09-02

- Confirmed configured `JM Parishes` reference layer uses the provided FeatureServer URL: `https://services6.arcgis.com/3R3y1KXaPJ9BFnsU/ArcGIS/rest/services/Jamaica_Parishes_SDC_Communities/FeatureServer/0`.
- Added parish FeatureServer query support for configured `feature_service_url` layers using the configured parish name field.
- Requested parish boundary geometry in the review spatial reference via ArcGIS `outSR`; local-only boundary SR mismatches still return `needs_review` instead of a false pass.
- Added polygon-ring point-in-boundary validation and partial-overlap classification for polygon checks.
- Fixed `pdf_text_structured_extraction.py` CLI to initialize `document_text_metrics` before passing it to `_parse_pages()`.

### Verification - 2026-09-02 Parish Service Continuation

- `& "C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe" -m py_compile src\ProcessingTools\adapters\pdf_text_structured_extraction.py src\ProcessingTools\adapters\validation_adapter.py` passed.
- `& "C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe" -m pytest src\ProcessingTools\tests\test_pdf_text_structured_extraction.py src\ProcessingTools\tests\test_validation_adapter.py` passed: 29 tests.
- CLI `main()` smoke with monkeypatched text-page and metrics extraction passed and wrote a review JSON artifact.
- `git diff --check` passed.

### Remaining Follow-up

- Add true source evidence navigation/deep links from validation findings to map/parish, plan, computation-sheet, or memorandum evidence.
- Run a live end-to-end parish validation against a real case once the case review data carries the expected parish and reviewed geometry SR.

### Review Fixes - 2026-09-02

- Patched adversarial review findings for normalized parish lookup by querying configured FeatureServer parish layers broadly and matching parish names locally after normalization.
- Treated ArcGIS `latestWkid` as the preferred WKID so `102095 (3448)` service metadata does not falsely require manual projection.
- Updated polygon ring handling to use even-odd containment so holes are not treated as valid parish area.
- Added segment-crossing detection so polygon/parish overlaps with all reviewed vertices outside are still classified as `needs_review`.
- Added regression coverage for each review finding.

### Verification - 2026-09-02 Review Fixes

- `& "C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe" -m py_compile src\ProcessingTools\adapters\pdf_text_structured_extraction.py src\ProcessingTools\adapters\validation_adapter.py` passed.
- `& "C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe" -m pytest src\ProcessingTools\tests\test_pdf_text_structured_extraction.py src\ProcessingTools\tests\test_validation_adapter.py` passed: 29 tests.
- `git diff --check` passed.

### Runtime Package Sync - 2026-09-02

- Synced updated `validation_adapter.py`, `pdf_text_structured_extraction.py`, `rules.yaml`, and ProcessingTools tests into `deployment/target-computer-tools/package/ProcessingTools`.
- Built `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/bin/Release/net8.0-windows/ParcelWorkflowAddIn.esriAddinX` at version `1.1.340` without running the auto-incrementing packaging script.
- Copied the fresh `1.1.340` add-in archive into `deployment/target-computer-tools/package/ParcelWorkflowAddIn.esriAddInX`.
- Verified packaged `Config.daml` reports `version="1.1.340"` and embedded `WorkflowSettings.json` contains the Jamaica parish FeatureServer URL.
- `tools/validate_installer_packaging.ps1` passed after clearing its stale temp validation directory.
- Packaged ProcessingTools targeted tests passed: 29 tests.

