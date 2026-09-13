# Investigation: Compare Title Image Innola Session

## Hand-off Brief

1. **What happened.** User reported `Search Title Image` shows `Innola connection could not be restored. Please log in again and retry.` immediately after login while Compare case `100000896` is open.
2. **Where the case stands.** Root cause confirmed and patched; the case artifact confirms `Faith Smith` has Volume/Folio `1284/27`, so extraction is not the blocker.
3. **What's needed next.** Retest in ArcGIS Pro with add-in `1.1.549`; if Innola still returns no source, inspect `compare_title_source_trace.json` in the case `working` folder.

## Case Info

| Field | Value |
| ----- | ----- |
| Ticket | N/A |
| Date opened | 2026-09-12 |
| Status | Active |
| System | ArcGIS Pro add-in dev workspace on Windows |
| Evidence sources | Screenshot, case folder artifacts, source code |

## Problem Statement

`Search Title Image` in Compare Workspace for transaction `100000896` says the Innola connection cannot be restored even though the user logged in seconds earlier.

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| Screenshot | Available | Shows Compare Workspace, active transaction `100000896`, selected `Faith Smith 1284/27`, and login-required status. |
| Case folder | Available | `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000896` exists; participant rows include `1284/27`. |
| Source code | Available | Title-source service and session manager wiring under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn`. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Verify title source uses live session manager rather than stale/null session | High | Done | `ShellState` used forced refresh for title lookup. |
| 2 | Verify `EnsureCurrentSessionAsync` behavior immediately after login | High | Open | May require code trace and tests. |
| 3 | Verify trace artifact after next Pro test | Medium | Open | New trace writes only if ensure returns a search result or service reaches append point. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-09-12 | User tested Search Title Image for `100000896` and received login-required message | Screenshot | Confirmed |
| 2026-09-12 | Case artifact contains adjacent owner `Faith Smith` with `1284/27` | Local artifact inspection | Confirmed |
| 2026-09-12 | Title lookup session injection changed from forced refresh to active-session-first | Source patch | Confirmed |

## Confirmed Findings

### Finding 1: Participant Volume/Folio Exists

**Evidence:** Local inspection of `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000896\working\extraction_review_data.json`.

**Detail:** Adjacent owners include `Faith Smith`, role `Neighbor`, Volume `1284`, Folio `27`.

### Finding 2: Title Lookup Forced a Session Refresh Before Searching

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs:344`

**Detail:** The production Compare workspace title-source service is wired to `Session.EnsureCurrentSessionAsync(...)`. Before the fix this call used `forceRefresh: true`, which required a successful re-login before the read-only title search could run.

### Finding 3: Title Lookup Matches the CT Source API Contract

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs:106`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs:111`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs:356`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareComputedParticipantTitleService.cs:316`

**Detail:** The implementation posts `searchKind = "source"` with `documentTypes = ["st_title"]`, reads `/api/v4/rest/portal/ladm-objects/{sourceId}?typeKeyId=source`, and downloads `/api/v4/rest/source/download?noredirect=1&bodyId=...`.

## Hypothesized Paths

### Hypothesis 1: Title search is using a session ensure function that fails when a session already exists but cannot be re-authenticated.

**Status:** Confirmed

**Theory:** The service returns login-required before sending the title source search because `EnsureCurrentSessionAsync` reports failure.

**Would confirm:** Source trace shows title image command gets an `InnolaSessionEnsureResult` failure despite `CurrentSession` being logged in.

**Would refute:** Trace shows the HTTP request is sent and Innola returns 401/403.

**Resolution:** `ShellState` was patched to use `forceRefresh: false` for title-image lookup only. Write/finalize paths keep forced refresh.

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs:344` |
| Trigger | Click `Search Title Image` for a computed participant |
| Condition | Active Compare workspace has participant Volume/Folio, but the read-only title lookup forced a full Innola refresh before searching |
| Related files | `CompareComputedParticipantTitleService.cs`, `CompareWorkspaceViewModel.cs`, `ShellState.cs`, `InnolaSessionManager.cs` |

## Conclusion

**Confidence:** High

Extraction is not the blocker. The user-facing message was produced because the Compare title-image lookup forced session refresh before a read-only source search; the active login could be valid, but refresh failure stopped the search before the CT lookup was attempted. The path now uses the active session first, preserves the CT API contract, retries cookie-only on HTTP auth failure, and writes `compare_title_source_trace.json` for the next live test.
