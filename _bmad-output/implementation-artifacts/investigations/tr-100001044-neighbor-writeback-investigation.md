# TR 100001044 Neighbor Writeback Investigation

## Hand-off Brief
TR 100001044 extracted and reviewed three Neighbor rows, but Innola rejected the Plan save after those new Neighbor rows were added. The previous safety fallback retried the Plan save without neighbors, so checklist/finalize succeeded while neighbors were intentionally skipped. Root cause is most likely duplicate Innola `@id` aliases on multiple created Neighbor templates; PE did not normalize aliases before Plan save while RT already did.

## Case Info
- Transaction: 100001044
- Case folder: `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001044`
- Investigation date: 2026-09-13
- Status: Concluded

## Confirmed Evidence
- `working/extraction_review_data.json` contains three `adjacent_owners` rows with `role: Neighbor`.
- `working/plan_examination_api_request.json` reports `neighbor_rows_reviewed: 3`, `neighbor_rows_created: 3`, `neighbor_rows_skipped: 0`.
- `working/plan_examination_neighbor_writeback_warning.json` reports `Plan Examination PUT failed: BadRequest` after new Neighbor rows were added.
- `working/plan_examination_api_response.json` reports final fallback state: `neighbor_rows_created: 0`, `neighbor_rows_skipped: 3`, each row action `skipped_after_plan_save_rejection`.
- `working/workflow_lifecycle_audit.json` shows Plan Examination writeback and transaction completion succeeded after the fallback path.

## Source Trace
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:99` saves the mutated Plan with neighbors.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:101` catches `HttpRequestException` when new neighbors were created.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs:104` marks rows as `skipped_after_plan_save_rejection`.
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/RtExamination/InnolaRtExaminationService.cs:588` already normalizes Innola object aliases before RT Plan save.

## Final Conclusion
Confidence: Medium-high. The local artifacts prove the neighbors were extracted and attempted, and that the previous fallback skipped them after an Innola `BadRequest`. The most likely payload defect is duplicate temporary `@id` aliases on multiple newly created Neighbor objects; this matches the difference between PE and RT save behavior.

## Fix Direction
- Apply RT-style `@id` alias normalization to PE Plan Examination save payloads.
- Cover the case with a regression test where two created Neighbor templates both return `@id: obj:1`.

## Verification
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`
- `dotnet exec src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "innola plan check service"`
