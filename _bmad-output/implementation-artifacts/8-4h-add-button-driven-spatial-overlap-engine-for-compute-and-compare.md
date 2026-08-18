---
baseline_commit: handoff-2026-08-17
---

# Story 8.4H: Add Button-Driven Spatial Overlap Engine For Compute And Compare

Status: review

## Story

As a cadastral examiner reviewing Compute or Compare geometry in ArcGIS Pro,  
I want to run a button-driven overlap check against configured map layers already loaded in the active map,  
so that I can detect overlap or no-overlap conditions with measurable spatial evidence before owner enrichment or reporting begins.

## Scope

This story is the spatial-analysis foundation for Story 8.4G. It does **not** include Innola/LTF owner enrichment yet. It establishes the overlap engine, map-state gating, and normalized evidence model that later stories will build on.

## Acceptance Criteria

1. Add a `Run Overlap Review` command in both Compute and Compare review flows.
2. The command runs only when:
   - an active map exists,
   - the review parcel/review geometry is already loaded in the map,
   - the configured overlap target layers are already present in the map.
3. If any required map dependency is missing, the command is blocked with a clear examiner-facing message naming what is missing.
4. The overlap engine checks the review geometry against configured map layers by role, including legal, fiscal, cadastral, and roads when enabled.
5. For each configured layer, the engine records either:
   - one or more overlap records, or
   - an explicit `No overlap` result.
6. For every overlap record, the engine computes:
   - overlap area
   - overlap percentage against the review parcel
   - source layer/role
   - source feature identity
7. If no overlap exists across all configured layers, the overall result is valid and reportable and is not treated as a failure.
8. The engine persists a case-scoped overlap review artifact that can be reused by reruns and later report stages.
9. The command can be rerun safely without duplicating prior overlap rows in the case artifact.
10. Automated tests cover blocked execution, overlap detection, no-overlap results, overlap-area computation, and artifact persistence.

## Technical Notes

- Operate on current map state only.
- Do not auto-load missing layers in this story.
- Do not call Innola in this story.
- Normalize overlap output into a stable model that later enrichment/report stages can consume.

## Files Likely To Change

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- tests under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-17 | 0.1 | Split spatial overlap engine out of Story 8.4G as the first implementation step. | Mary / Winston / Amelia / Codex |
| 2026-08-17 | 1.0 | Implemented compute and compare overlap-review command wiring, persisted case-scoped overlap review artifacts, enabled optional roads target parsing, and added focused persistence/XAML tests. | Codex |
