---
baseline_commit: handoff-2026-07-06
---

# Story 7.10: Add Enterprise Working Layer Default Visualization Labeling

Status: review

## Story

As a cadastral examiner or supervisor reviewing Enterprise working layers outside the immediate ArcGIS Pro map session,  
I want the Enterprise `working_review` Feature Layer to carry default visualization and labeling for parcel review,  
so that bearing/distance line labels and parcel context are visible when the layer is opened from Portal or Map Viewer without requiring the add-in to load and style the layers first.

## Business Context

Stories 7.7, 7.8, and 7.9 establish the Enterprise working review workspace for temporary Compute review geometry. The runtime publish path already copies line COGO fields into `working_lines`, and the ArcGIS Pro map integration already applies labels when layers are loaded into the active Pro map.

The remaining gap is the Enterprise/Portal default visualization. A user can open the hosted `working_review` item directly in Portal/Map Viewer, but the Feature Layer service does not yet have a required default labeling configuration for `working_lines`. This follow-up adds an admin/runtime-supported default visualization contract without changing authoritative cadastral promotion behavior.

This story is intentionally small. It must not change Spatial Unit creation, Innola task closeout, Enterprise row publishing, or local GDB output generation.

## Acceptance Criteria

1. Given the Enterprise working service is provisioned or validated, when the `working_lines` child layer is available, then the admin tooling can apply or verify default line labeling based on `bearing_txt` plus a newline plus `length_txt` / `distance_txt`.
2. Given `bearing_txt` and `length_txt` are populated, when `working_lines` is opened in Portal/Map Viewer or loaded by default visualization-aware clients, then the label expression displays bearing and length on separate lines.
3. Given `length_txt` is empty but `distance_txt` is populated, when the line label is evaluated, then the label falls back to `distance_txt`.
4. Given `bearing_txt` is empty and distance/length text exists, when the line label is evaluated, then the label displays only the distance/length text.
5. Given distance/length text is empty and `bearing_txt` exists, when the line label is evaluated, then the label displays only the bearing.
6. Given all COGO label fields are empty, when the label is evaluated, then no visible label text is produced for that feature.
7. Given the Enterprise service is missing `bearing_txt`, `length_txt`, or `distance_txt`, when default visualization validation runs, then it reports a clear schema/remediation warning or blocker according to the existing 7.8 validation behavior.
8. Given the Enterprise admin script applies visualization defaults, when the operation succeeds, then diagnostics report that `working_lines` labeling was applied or already matches the expected default.
9. Given the Enterprise admin script cannot apply visualization defaults because of auth, service, or REST errors, when the operation fails, then no credentials/tokens are logged and the diagnostic explains that data publishing remains separate from visualization default setup.
10. Given the ArcGIS Pro add-in loads Enterprise working layers into the active map, when map-load labeling is applied, then existing Pro-side labels continue to work and are not broken by Portal default visualization metadata.
11. Given points and polygons are included in the working service, when default visualization is applied, then optional point-id and parcel-name labeling may be preserved or added if already supported, but the required scope of this story is the `working_lines` COGO label default.
12. Given this story is complete, then no CADMAP, CADINDEX, authoritative Parcel Fabric, or Innola Spatial Unit data is changed by the visualization update.

## Tasks / Subtasks

- [x] Define the Enterprise default visualization contract. (AC: 1-6, 11)
  - [x] Specify the expected `working_lines` label expression using `bearing_txt`, `length_txt`, and `distance_txt`.
  - [x] Prefer an Arcade expression equivalent to the existing ArcGIS Pro map-load label behavior:
    - empty bearing and empty length/distance -> empty label
    - bearing only -> bearing
    - length/distance only -> length/distance
    - bearing plus length/distance -> bearing + newline + length/distance
  - [x] Keep line label class name stable, recommended: `COGO Segment`.
  - [x] Define whether the expression uses `length_txt` first and `distance_txt` as fallback.

- [x] Extend Enterprise admin provisioning/validation to handle visualization defaults. (AC: 1, 7-9)
  - [x] Update `src/ProcessingTools/admin/provision_enterprise_working_layers.py` to support applying and validating `working_lines` labeling metadata after child layers are available.
  - [x] Preserve the current `validate`, `provision`, and `cleanup` command contract.
  - [x] Avoid reporting provisioning success if visualization validation is required and fails.
  - [x] Keep token handling consistent with existing admin script redaction behavior.
  - [x] Return machine-readable JSON diagnostics indicating whether visualization defaults were `applied`, `already_current`, `skipped`, or `failed`.

- [x] Add or update schema/template visualization metadata where practical. (AC: 1-3, 8)
  - [x] If Feature Collection publish accepts `drawingInfo.labelingInfo`, add the `working_lines` label definition to the transient Feature Collection layer definition.
  - [x] If this Enterprise deployment does not preserve label metadata during publish, apply defaults through the supported layer admin/updateDefinition path after publish.
  - [x] Do not depend on empty-service `addToDefinition`; Story 7.8 already found that path unreliable for this deployment.

- [x] Preserve ArcGIS Pro map-load labeling behavior. (AC: 10)
  - [x] Verify `IOutputMapIntegrationService` continues to apply labels when the add-in loads Enterprise layers into ArcGIS Pro.
  - [x] Do not remove or weaken existing Pro-side fallback label logic.
  - [x] If Enterprise default labeling metadata conflicts with Pro-side labels, Pro-side labels should still produce the expected in-map review experience.

- [x] Add automated coverage. (AC: 1-12)
  - [x] Add Python/admin tests for generated `working_lines` label expression payload.
  - [x] Add Python/admin tests for visualization diagnostics in validate/provision flows.
  - [x] Add tests proving auth/service errors are reported without logging tokens.
  - [x] Add tests proving missing COGO fields produce a clear schema/visualization diagnostic.
  - [x] Add C# tests only if the add-in side changes; otherwise existing output-map integration tests should remain green.

## Developer Notes

### Current Pro-Side Label Behavior

ArcGIS Pro map-load labeling already exists in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`

Important methods:

- `ShouldApplyLabels(...)`
- `ApplyLineLabels(...)`
- `ApplySingleLabelClass(...)`

The current Pro expression uses `bearing_txt` and `length_txt` first, and has fallbacks for related distance fields. This story should reuse the same operator-facing behavior for Enterprise default visualization.

### Current Enterprise Publish And Schema Behavior

Enterprise working publish already copies line COGO fields in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`

The line allowlist includes:

- `bearing_txt`
- `distance_txt`
- `length_txt`

Enterprise provisioning/validation already owns the admin schema flow in:

- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ProcessingTools/admin/create_enterprise_working_schema_template.py`

Story 7.8 now explicitly requires those line COGO fields and `working_case_index` Spatial Unit reference fields. This story builds on that schema contract; it does not replace it.

### Suggested Label Expression

Use an Arcade expression equivalent to:

```arcade
var len = IIf(IsEmpty($feature.length_txt), $feature.distance_txt, $feature.length_txt);
When(
  IsEmpty($feature.bearing_txt) && IsEmpty(len), '',
  IsEmpty($feature.bearing_txt), len,
  IsEmpty(len), $feature.bearing_txt,
  $feature.bearing_txt + TextFormatting.NewLine + len
)
```

If the Enterprise REST API requires the expression to be stored as a string in `labelExpressionInfo.expression`, keep it compact and JSON-safe.

### Scope Guardrails

- Do not change the transaction closeout sequence from Story 7.9.
- Do not change Spatial Unit API behavior.
- Do not write to CADMAP, CADINDEX, Enterprise Parcel Fabric authoritative targets, or authoritative cadastral stores.
- Do not store portal tokens, passwords, or raw credential material in settings, logs, diagnostics, or story artifacts.
- Do not make default visualization a blocker for local-only workflow modes.

## Testing Requirements

Minimum verification:

- `python -m unittest tests.test_enterprise_working_admin` from `src/ProcessingTools`
- `python -m unittest discover -s tests` from `src/ProcessingTools`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build /p:Platform=x64`

Manual/live smoke testing:

- Use a non-production/test Enterprise `working_review` service.
- Run Enterprise Admin `Validate` / `Provision` after implementation.
- Open `working_lines` in Portal/Map Viewer and confirm COGO labels display from `bearing_txt` plus `length_txt` or `distance_txt`.
- Open the same transaction in ArcGIS Pro through the add-in and confirm Pro-side labels still display correctly.

## References

- `_bmad-output/implementation-artifacts/7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md`
- `_bmad-output/implementation-artifacts/7-8-add-enterprise-working-layer-admin-provisioning-and-maintenance-settings-tab.md`
- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/5-20-configure-cogo-style-map-symbology-labeling-and-editing-experience-for-non-fabric-spatial-outputs.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `python -m unittest tests.test_enterprise_working_admin`
- `python -m unittest discover -s tests`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj /p:Platform=x64`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build /p:Platform=x64`

### Completion Notes

- Story created as a follow-up to Enterprise working-layer schema/publish work.
- Requirement is limited to Enterprise/Portal default visualization metadata, especially `working_lines` labeling.
- Existing ArcGIS Pro label behavior remains the baseline and must be preserved.
- Implemented Enterprise `working_lines` default labeling metadata with `COGO Segment` Arcade expression using `length_txt` first and `distance_txt` fallback.
- Added live admin diagnostics for visualization status and updateDefinition failure handling without token exposure.
- Preserved Pro-side map-load labels; no C# production changes were needed.

### File List

- `_bmad-output/implementation-artifacts/7-10-add-enterprise-working-layer-default-visualization-labeling.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ProcessingTools/admin/provision_enterprise_working_layers.py`
- `src/ProcessingTools/tests/test_enterprise_working_admin.py`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-06 | 0.1 | Created follow-up story for Enterprise `working_lines` default labelingInfo using bearing and distance text fields. | Mary / Codex |
| 2026-07-06 | 1.0 | Implemented Enterprise `working_lines` default labelingInfo, validation/apply diagnostics, and admin tests. | Amelia / Codex |
