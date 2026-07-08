# Investigation: TR100000492 Complete Message

## Hand-off Brief
TR100000492 shows the UI message "Could not complete transaction. Try again." while persisted lifecycle evidence says the transaction completed successfully. The message originates from the Innola task completion path, but the current case artifacts show Enterprise publish, Spatial Unit save, Plan Check writeback, package upload, and task completion all succeeded. The remaining issue is a post-closeout state/artifact consistency problem: the manifest workflow state still reads `spatial_review_pending`, and generated closeout artifacts are missing from disk.

## Evidence

- Confirmed: `workflow_lifecycle_audit.json` ends with `transaction_complete_succeeded` and message `Completed. Final package uploaded and transaction closed.`
- Confirmed: `manifest.json` has `payload.innola_lifecycle.status = completed`, `working_package_upload_status = uploaded`, and `spatial_unit_api_status = saved`.
- Confirmed: `spatial_unit_api_response.json` reports 14 requested, defaulted, and saved Spatial Units, with returned SUID values.
- Confirmed: `plan_check_api_response.json` reports `status = saved` and 6 updated Plan Check types.
- Confirmed: `enterprise_working_publish.json` reports `status = published` with 129 points, 119 lines, 14 polygons, 1 case index, 0 issues.
- Confirmed: `output/reports` exists but is empty after closeout, despite the audit event `compute_examination_report_generated`.
- Confirmed: `working/compute_review_disposition.json` and `working/enterprise_working_disposition.json` are missing after closeout.
- Confirmed: `InnolaTransactionLifecycleService.CompleteAsync` returns the exact fallback message when transition completion fails or throws an expected adapter exception.
- Confirmed: `WorkflowSession.RemoveSpatialReviewArtifacts()` deletes closeout disposition and report artifacts when saved spatial-review evidence is considered stale.

## Conclusion

The transaction appears completed in Innola and Enterprise based on local closeout evidence. The UI message is likely stale or misleading after a successful completion, caused by state/artifact cleanup or refresh logic leaving the dockpane in a pre-finalize workflow view.

## Fix Direction

- Preserve closeout artifacts once `payload.innola_lifecycle.status = completed`.
- Do not run stale spatial-review cleanup on completed cases.
- On successful task completion, update the workflow state or load behavior so the case cannot reopen as `Final Review`.
- Improve the generic completion failure message by persisting HTTP status/body/error category when completion really fails.
