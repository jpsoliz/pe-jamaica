# Input Reconciliation — Technical Research

Input: `_bmad-output/planning-artifacts/research/technical-arcgis-pro-addin-parcel-workflow-research-2026-06-08.md`

## Summary

The PRD incorporates the research recommendation to make v1 an ArcGIS Pro dock-pane add-in with a Python/ArcPy processing core, local case-folder state, JSON/GDB/report outputs, and future-ready Enterprise boundaries.

## Reconciliation Findings

- **Covered:** ArcGIS Pro add-in first, Python toolbox/script-tool processing, guided intake, preflight, extraction review, validation, output creation, reports/logs, and map integration are represented in the PRD.
- **Covered:** Enterprise Web Tools, geoprocessing services, and Notebooks are preserved as future evolution candidates rather than v1 runtime commitments.
- **Covered:** Source formats from the research are reflected in the PRD: PDF, DWG, TXT, CSV, TIF, PNG, and JPG.
- **Covered:** DWG-supported outputs from the script review are represented: `dwg_annotation`, `dwg_point`, `dwg_polyline`, `dwg_multipoint`, and `dwg_parcels`.
- **Resolved during finalize:** The PRD now frames plaintext credentials as a v1 constraint/risk with log/report redaction, instead of implying production-grade secret handling is complete in v1.
- **Deferred:** Exact 0-10 scoring formula remains a fixture-calibration item.
- **Deferred:** Exact fixture filenames and baseline output counts remain test-planning items.
