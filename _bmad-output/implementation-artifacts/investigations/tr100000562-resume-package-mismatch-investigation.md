# Investigation: TR100000562 Resume Package Mismatch

## Hand-off Brief

1. **What happened.** Loading `TR100000562` can fail with "Saved resume package does not belong to the selected transaction" when Innola exposes an old saved-state zip for the same transaction number but a different transaction/task identity.
2. **Where the case stands.** Fixed in code: resume package selection now validates every candidate before restore and skips stale/mismatched packages instead of selecting them.
3. **What's needed next.** Rebuild/reload the add-in in ArcGIS Pro and rerun `TR100000562`; if a stale resume package remains attached, it should now be ignored and the transaction should load from current source attachments.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000562 |
| Date opened | 2026-07-14 |
| Status | Concluded |
| System | ArcGIS Pro add-in, `pe-jamaica` repo |
| Evidence sources | User error message, resume restore code, transaction load code, local resume manifest |

## Problem Statement

User reports that running `TR100000562` shows: "Saved resume package does not belong to the selected transaction."

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| `CaseResumePackageService.Restore` | Available | Throws the exact user-facing message when resume manifest transaction number/task/transaction id do not match selected transaction. |
| `InnolaTransactionLoadService.ResolveLatestResumeAttachmentAsync` | Available | Previously returned a single resume attachment without validating its manifest and fell back to the first attachment when all candidates mismatched. |
| Local `sidwell_resume_manifest.json` | Available | Local manifest records transaction number `100000562`, task id `9dd26f63-7b06-11f1-aa34-e6d6366fff7e`, and transaction id `019f42e4-26e8-74c4-857f-d3319c6acb73`. |
| Live Innola selected row | Missing | Needed to confirm the current server-side transaction/task identity, but code path explains the observed error. |

## Confirmed Findings

### Finding 1: The error is raised by restore-time identity validation

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseResumePackageService.cs:139`

**Detail:** Restore rejects packages when the resume manifest transaction number, task id, or transaction id does not match the selected transaction.

### Finding 2: Resume selection allowed an unvalidated single resume attachment

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs:353` before fix.

**Detail:** A single resume attachment was selected without reading its manifest, so a stale package could reach restore and fail with the user-facing mismatch message.

### Finding 3: Resume selection could fall back to a known-mismatched package

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs:407` before fix.

**Detail:** When multiple candidates existed but none matched, the selector returned the first attachment anyway.

## Deduced Conclusions

### Deduction 1: TR100000562 is likely seeing a stale Innola resume package from an earlier stage/identity

**Based on:** Findings 1, 2, and 3.

**Reasoning:** The user-facing message only appears after a resume package is downloaded and its internal manifest fails selected-transaction validation. The selector previously let stale candidates reach restore.

**Conclusion:** The correct behavior is to skip mismatched resume packages and continue with source attachments for the selected current-stage transaction.

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `CaseResumePackageService.Restore` |
| Trigger | `InnolaTransactionLoadService` finds a resume attachment and attempts restore |
| Condition | Resume manifest belongs to same/related transaction number but different task id or transaction id |
| Related files | `InnolaTransactionLoadService.cs`, `CaseResumePackageService.cs`, `InnolaTransactionLoadServiceTests.cs` |

## Fix Applied

- Removed the single-resume shortcut so every resume attachment is manifest-validated before selection.
- Added `ResumeManifestMatchesSelectedTransaction` to check transaction number, task id, and transaction id.
- Changed the "no matching candidate" behavior from "fallback to first attachment" to "return null", allowing the load to continue as a fresh/current case when source attachments exist.
- Added regression coverage: `innola transaction load skips mismatched resume package`.

## Validation

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "innola transaction load skips mismatched resume package"`: passed.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`: passed 372 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`: passed.

## Conclusion

**Confidence:** High

The load failure was caused by selecting a stale/mismatched resume package before verifying its manifest. The code now treats stale resume packages as non-applicable and avoids blocking the selected transaction.
