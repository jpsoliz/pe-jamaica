# Investigation: PLA_B Attachment Download Failure

## Hand-off Brief

1. **What happened.** The PLA_B test form showed `Could not load attachment. Try again.` because the PLA_B current-source downloader returned failure on the first attachment download error.
2. **Where the case stands.** Root cause is confirmed in `PlaBCurrentTransactionSourceDownloadService`; the source-only replacement correctly avoids PLA_A transaction-type validation, but it was too strict for mixed Innola attachment lists.
3. **What's needed next.** Test the packaged add-in in ArcGIS Pro with current TR `100000724` and PE `100000628`; if all current TR attachments still fail, collect the new skipped-attachment diagnostic to identify the Innola route/status.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | N/A |
| Date opened | 2026-08-27 |
| Status | Concluded |
| System | Windows, ArcGIS Pro add-in, `net8.0-windows` |
| Evidence sources | Screenshot, source code, focused test run |

## Problem Statement

User reported that the PLA_B Test Input form failed with `Could not load attachment. Try again.` after entering current TR `100000724` and PE number `100000628`.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs` | Available | Current-source downloader returned failure immediately when one attachment content request failed. |
| `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs` | Available | Shared Innola service emits the generic attachment failure message on non-success status or request exceptions. |
| Live Innola attachment payload for TR `100000724` | Missing | New diagnostics will show which attachment and error code if the live route still fails for every source file. |

## Confirmed Findings

### Finding 1: PLA_B failed on the first current-TR attachment error

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`

**Detail:** The PLA_B current-source downloader called `GetAttachmentContentAsync` and returned a failed result immediately when `content.Success` was false.

### Finding 2: The popup text came from the shared Innola attachment service

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`

**Detail:** `GetAttachmentContentAsync` returns `Could not load attachment. Try again.` for failed HTTP status handling or caught request exceptions.

## Conclusion

**Confidence:** High

The regression was introduced by the PLA_B source-only loader added to avoid PLA_A validation. That direction is correct, but the implementation needed best-effort attachment handling: skip failed or non-viewable attachments, keep downloading remaining files, and fail only when no viewable source files can be placed in `[TR]/source`.

## Recommended Next Steps

### Fix direction

Implemented: PLA_B now keeps going after individual source attachment failures and preserves warnings in the result message.

### Diagnostic

If the same current TR still fails, read the updated popup: it now includes skipped attachment count and the first attachment/error code, which should identify whether all source attachments are blocked by authorization, metadata, route, or unsupported file type.

## Reproduction Plan

1. Log in to Innola from ArcGIS Pro.
2. Open Transaction List and click `[PA]`.
3. Enter current TR `100000724` and PE `100000628`.
4. Click `Open Viewer` or `Prepare`.
5. Expected after this patch: viewer opens if at least one current TR source file downloads; otherwise failure includes the first skipped attachment diagnostic.
