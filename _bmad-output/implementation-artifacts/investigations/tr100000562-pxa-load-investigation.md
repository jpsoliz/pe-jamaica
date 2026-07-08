# TR100000562 PXA Load Investigation

## Problem Statement

Transaction `100000562` is a new `PXA` transaction type, but no case folder is created under `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases`.

## Confirmed Evidence

- No `100000562` case folder exists under the standard case root.
- No `100000562` artifacts or persisted case logs were found under the existing case-folder tree.
- Source `WorkflowSettings.json` includes `PXA` in `supported_transaction_types`, includes `st_survey_plan_pdf`, and defines profile `pxa_single_parcel_survey_plan`.
- Active ArcGIS Pro AssemblyCache `WorkflowSettings.json` also includes `PXA`, `st_survey_plan_pdf`, and `pxa_single_parcel_survey_plan`.
- Transaction loading is gated before case-folder creation by:
  - supported transaction type check in `TransactionPanelState.cs`
  - compute workflow stage check in `TransactionPanelState.cs`
  - profile/detail/attachment checks in `InnolaTransactionLoadService.cs`
- Durable `process.log` is created inside the case folder, so a pre-folder failure does not produce a case-specific process log.

## Current Conclusion

The stale-settings hypothesis is refuted: active ArcGIS Pro settings already include PXA.

The visible transaction-list row shows `TaskName = Compute Survey Plan` and transaction type label `Plan Examination by Area`.
`Compute Survey Plan` is already listed in `compute_workflow_stages`, but `Plan Examination by Area` was missing from `supported_transaction_types` and from the PXA profile aliases.

The source settings and active ArcGIS Pro cached settings were patched so `Plan Examination by Area` maps to `pxa_single_parcel_survey_plan`.

## Evidence Needed

- If possible, a debugger/diagnostic capture around `InnolaTransactionLoadService.LoadSelectedTransactionAsync` for:
  - `selected.TransactionType`
  - `selected.TaskName`
  - `detail.CaseType`
  - `detail.TaskName`
  - `detail.ProfileHint`
  - `detail.Attachments` source types and attachment ids

## Recommended Fix Direction

Retry loading TR `100000562` after ArcGIS Pro reloads settings. If it still fails before folder creation, add a pre-case-folder load diagnostic/audit path or expand the UI status message to include the rejected transaction type/task/profile evidence.

## Follow-up: 2026-07-08 - Extraction Produces No Values

### Confirmed Evidence

- The case folder now exists at `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000562`, so the original load/profile gate issue is resolved.
- `extraction_route.json` shows the transaction routed to `SINGLE_PARCEL_SURVEY_PLAN_PDF_V1` with `active_extractor_id = survey_plan_ocr_vision`, `primary_source_file = DOC_PLAN_492321.pdf`, `ai_requested = true`, `ai_available = true`, but `ai_used = false`.
- `extraction_route.json` also records `text_layer_available = false`, `text_layer_probe_status = no_embedded_text_layer_or_unreadable_image_pdf`, `parsed_parcel_count = 1`, and `parsed_row_count = 0`.
- `extraction_review_data.json` records `status = manual_review_required`, null/zero confidence survey metadata, `row_count = 0`, `segment_row_count = 0`, and empty `rows` / `segments`.
- `extraction_decision_gate.json` records `AttemptCount = 13`, `WeakAttemptCount = 13`, and the note that the configured OCR/vision extractor did not return parcel points or bearing/distance rows.
- Source trace: `CreateParcelDraftExtractionAdapter.TryExecuteSurveyPlanExternalExtractionAsync` resolves `ResolveTextStructuredExtractionScriptPath` and launches `pdf_text_structured_extraction.py` even for the route named `survey_plan_ocr_vision`.
- Source trace: `pdf_text_structured_extraction.py` only reads embedded text through PyMuPDF `page.get_text("text")` or `pypdf.extract_text()`. It does not perform OCR or vision extraction from rendered page images.

### Conclusion

The current PXA routing and source-role/profile selection are working. The no-value extraction is caused by a placeholder implementation gap: the configured extractor name says `survey_plan_ocr_vision`, but production execution still runs the text-only PDF parser. Since `DOC_PLAN_492321.pdf` is image-only, the parser has no text input and correctly emits a manual-review artifact with zero points, zero segments, and zero metadata.

### Fix Direction

Implement the real Story 2.18 scanned survey-plan extractor:

- Add a dedicated vision/OCR adapter or script for `single_parcel_survey_plan_vision_v1`.
- Render PDF pages to images and call the configured OCR/vision provider.
- Normalize returned metadata, boundary points, bearing/distance segments, parties, adjacent owners, coordinate system, parish, and document area into `extraction_review_data.json`.
- Keep the current manual-review fallback when the provider is unavailable, returns low confidence, or produces incomplete geometry.
