# Investigation: TR 100001040 Finalize Package Lock

## Hand-off Brief

1. **What happened.** TR `100001040` reached the final package upload phase, but Finalize stopped because the completed case package could not be built: `manifest.json` was locked by another process.
2. **Where the case stands.** Spatial Unit save, Enterprise SUID writeback, compute report attachment, and Plan Examination writeback succeeded; Innola task completion was not reached, so the transaction remains recoverable but incomplete.
3. **What's needed next.** Improve package build/read behavior and user messaging so locked files identify the locked path and advise retry/close-file action; optionally add a short retry around package file reads.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR 100001040 |
| Date opened | 2026-09-13 |
| Status | Concluded |
| System | ArcGIS Pro add-in, Compute Survey Plan Finalize |
| Evidence sources | Case folder artifacts under `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001040`; source code under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn` |

## Problem Statement

User pressed Finalize at the end of TR `100001040`. The UI showed `Could not package case state for upload.` User asked whether this was because of timeout and requested the issue/problem first before next steps.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| `working/working_package_upload_failure.json` | Available | Contains the exact exception and locked file path. |
| `working/workflow_lifecycle_audit.json` | Available | Shows the successful stages before package upload failure. |
| `manifest.json` | Available | Shows lifecycle status `error`, completion readiness `true`, Spatial Unit saved, package upload failed. |
| `InnolaTransactionLifecycleCoordinator.cs` | Available | Shows package upload is before final Innola task completion. |
| `CaseResumePackageService.cs` | Available | Shows package builder enumerates case files and includes `manifest.json`. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-09-13T18:00:29Z | Transaction claim/session refresh succeeded. | `workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-13T18:09:00Z | Completion readiness passed. | `workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-13T18:09:02Z | Spatial Unit saved. | `workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-13T18:09:03Z | Enterprise working polygon SUID values saved. | `workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-13T18:09:05Z | Compute PDF report attached to Innola transaction. | `workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-13T18:09:07Z | Innola Plan Examination details saved. | `workflow_lifecycle_audit.json` | Confirmed |
| 2026-09-13T18:09:07Z | Completed case package upload failed. | `workflow_lifecycle_audit.json`; `working_package_upload_failure.json` | Confirmed |

## Confirmed Findings

### Finding 1: The failure was a local file lock during package build

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001040\working\working_package_upload_failure.json:14-15`

**Detail:** The recorded error category is `IOException`; the recorded error message is: the process cannot access `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001040\manifest.json` because it is being used by another process.

### Finding 2: Innola session timeout is not supported by the failure evidence

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100001040\working\workflow_lifecycle_audit.json:126-130`, `:156-160`

**Detail:** The audit shows Innola connection refresh succeeded for compute report attachment and Plan Examination writeback immediately before the package failure.

### Finding 3: Final Innola task completion was not attempted

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:665-699`, `:724-733`

**Detail:** The coordinator uploads the completed case package first. If that fails, it returns failure before refreshing for `transaction lifecycle complete` and before calling `lifecycleService.CompleteAsync`.

### Finding 4: The generic UI message hides the actionable lock detail

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs:815-826`

**Detail:** Package build/read/upload `IOException`, `UnauthorizedAccessException`, and `TaskCanceledException` all return the same user-facing message: `Could not package case state for upload.`

### Finding 5: The zip builder includes `manifest.json` and uses normal file read semantics

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseResumePackageService.cs:67-79`, `:219-230`

**Detail:** During package build, the service enumerates the entire case folder and creates zip entries from included files. `manifest.json` is explicitly included, and `CreateEntryFromFile` fails if another process holds the manifest without compatible sharing.

## Deduced Conclusions

### Deduction 1: This is recoverable by retrying once the lock is gone

**Based on:** Findings 1, 3, and manifest state.

**Reasoning:** The transaction was not completed in Innola, and the package upload status was persisted as failed. The failure was before final task completion and is not a destructive completed state.

**Conclusion:** Closing whatever has `manifest.json` open, then pressing Finalize again should retry the remaining path, though the current code may repeat earlier idempotent save/writeback phases before reaching package upload.

### Deduction 2: The product message is too generic for presentation/testing

**Based on:** Finding 4.

**Reasoning:** The app captured the exact locked path in diagnostic JSON but did not surface it in the dockpane. The examiner sees a server/upload-like message even though the remedy is local: close the file/tool/process using `manifest.json`, then retry.

**Conclusion:** The user-facing message should distinguish local package build failure from Innola upload/session failure.

## Hypothesized Paths

### Hypothesis 1: Another app or viewer had `manifest.json` open

**Status:** Open

**Theory:** An editor, File Explorer preview pane, antivirus scanner, backup sync process, or another add-in operation held `manifest.json` while Finalize tried to zip it.

**Supporting indicators:** The exact IOException says `manifest.json` is being used by another process.

**Would confirm:** A handle scan during failure showing the owning process for `manifest.json`.

**Would refute:** Reproduction where no external process holds the file but package build still fails.

**Resolution:** Not yet confirmed because the owning process was not captured at the time.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Process holding `manifest.json` | Would identify whether the lock came from ArcGIS Pro, editor/Explorer, sync, antivirus, or our own code. | During the failure, run Sysinternals Handle/Process Explorer for `manifest.json`, or add lock-owner diagnostics if feasible. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `CaseResumePackageService.Build()` via `archive.CreateEntryFromFile(...)` while including `manifest.json`. |
| Trigger | User pressed Finalize; lifecycle coordinator reached completed case package upload. |
| Condition | `manifest.json` was locked by another process when the zip builder attempted to read it. |
| Related files | `InnolaTransactionLifecycleCoordinator.cs`, `CaseResumePackageService.cs`, `ManifestSerializer.cs`, `TransactionPanelState.cs` |

## Conclusion

**Confidence:** High

The observed failure is not primarily an Innola timeout. The case evidence confirms a local file lock: `manifest.json` in the case folder was being used by another process at the exact moment Finalize tried to build `sidwell-case-complete-100001040.zip`. The transaction reached several successful Innola writes before this point, but final package upload and Innola task completion did not happen.

## Recommended Next Steps

### Fix direction

Improve the completed package build/upload failure path:

- Surface local file-lock errors with an actionable message naming the locked file and advising the user to close editors/preview panes/sync tools and retry Finalize.
- Add a small retry/backoff around package build file reads for transient locks.
- Consider packaging from a stable snapshot/copy of the case folder so active UI writes or short-lived locks do not break Finalize.
- Preserve the current behavior that final Innola task completion does not run until the completed package upload succeeds.

### Diagnostic

If this repeats, capture the owning process for `manifest.json` while the error is visible. The failure artifact already gives the locked path and timestamp.

## Reproduction Plan

1. Open a transaction case folder.
2. Hold `manifest.json` open with an exclusive lock.
3. Press Finalize after readiness passes.
4. Expected current behavior: UI shows `Could not package case state for upload.` and `working_package_upload_failure.json` records an `IOException`.
5. Expected improved behavior: UI says the case package cannot be built because `manifest.json` is locked and prompts retry after closing the file/process.

