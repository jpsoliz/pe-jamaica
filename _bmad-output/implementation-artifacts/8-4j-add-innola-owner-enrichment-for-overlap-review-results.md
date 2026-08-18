---
baseline_commit: handoff-2026-08-17
---

# Story 8.4J: Add Innola Owner Enrichment For Overlap Review Results

Status: drafted

## Story

As a cadastral examiner reviewing overlaps with existing cadastral layers,  
I want the overlap results enriched with owner and substantial property details from Innola/LTF using captured identifiers,  
so that the review evidence explains not only what overlaps spatially, but also which record and owner the overlap affects.

## Scope

This story assumes:
- Story 8.4H provides the overlap engine and saved overlap artifact
- Story 8.4I provides the review/report surface

This story adds the identifier-routing and owner/property enrichment stage.

## Acceptance Criteria

1. For each overlap evidence row, the add-in reads available identifiers from the overlapped feature, including where present:
   - `PID`
   - `vol_folio`
   - `landval_no`
   - `r_number`
   - `pe_number`
   - `pd_number`
2. The add-in applies a defined identifier priority order rather than querying all identifiers blindly.
3. Default priority order is:
   - `PID`
   - `vol_folio`
   - `landval_no`
   - `r_number`
   - `pe_number`
   - `pd_number`
4. The enrichment implementation reuses existing Compare/Innola query services rather than creating a separate owner-query stack.
5. If an identifier succeeds, the overlap row is enriched with owner name(s) and any configured substantial record details returned by Innola/LTF.
6. If no usable identifier exists, the overlap row is marked `identifier unavailable` and the review remains valid.
7. If an identifier exists but no owner/property match is returned, the overlap row is marked `no owner match found`.
8. If the Innola/LTF query fails, the overlap row is marked with a non-secret retryable diagnostic.
9. The enriched results are persisted into the saved overlap review artifact and displayed in the overlap review surface.
10. The report model can include enriched ownership/property results when present and can still render cleanly when enrichment is missing or failed.
11. Automated tests cover identifier priority, successful enrichment, missing identifiers, no-match handling, and sanitized failure diagnostics.

## Technical Notes

- Keep spatial analysis and enrichment as separate internal stages.
- Enrichment should run only after overlap detection has produced saved rows.
- Reuse Compare query adapters wherever possible.

## Files Likely To Change

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareCadasterQueryServices.cs`
- overlap review orchestration files under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- tests under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-17 | 0.1 | Split Innola/LTF owner enrichment out of Story 8.4G as the third implementation step. | Mary / Winston / Amelia / Codex |
