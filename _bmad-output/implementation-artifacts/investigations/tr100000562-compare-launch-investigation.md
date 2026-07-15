# Investigation: TR100000562 Compare Launch

## Hand-off Brief

1. **What happened.** Transaction `TR100000562` does not launch the Compare workspace because the available evidence still identifies it as a compute-stage transaction, not a configured Compare-stage transaction.
2. **Where the case stands.** Confirmed locally: the case manifest records `task_name = Compute Survey Plan`, while Compare launches only for task names listed in `compare_workflow_stages`.
3. **What's needed next.** Verify the live Innola transaction-list row; if its task is still `Compute Survey Plan`, advance the transaction to the Compare task in Innola rather than adding the compute task to Compare config.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000562 |
| Date opened | 2026-07-14 |
| Status | Concluded |
| System | ArcGIS Pro add-in, local repo `pe-jamaica`, case folder `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000562` |
| Evidence sources | Workflow settings, transaction panel routing code, local case manifest, enterprise working publish summary, tests |

## Problem Statement

User reports that the Compare tab/workspace is not launched for `TR100000562` and asks whether the transaction stage must be added.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| `WorkflowSettings.json` | Available | Configured Compare stages are `Compare` and `Compare Survey Plan`. |
| `TransactionPanelState.cs` | Available | Routes by exact task-name match against compute stages first, then Compare stages. |
| Local case manifest | Available | `TR100000562` has `workflow_state = spatial_review_pending` and `task_name = Compute Survey Plan`. |
| Enterprise working publish summary | Available | Geometry for `100000562` was published to working review: 1 polygon, 5 lines, 5 points. |
| Live Innola transaction-list row | Missing | Needed to prove the current server-side task after completion/advance. |

## Confirmed Findings

### Finding 1: Compare stages are configured, but only for explicit Compare task names

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json:77`

**Detail:** The current config has `compare_workflow_stages` set to `Compare` and `Compare Survey Plan`.

### Finding 2: The launcher routes by `TaskName`, not by transaction number or workflow state

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:1302`

**Detail:** `ResolveWorkflowStageRoute` trims `row.TaskName`, checks `computeWorkflowStages.Contains(...)`, then checks `compareWorkflowStages.Contains(...)`. A row whose task is `Compute Survey Plan` is routed to Compute.

### Finding 3: Compare opens only after Start/claim succeeds

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:634`

**Detail:** `StartSelectedTransactionAsync` loads the row, calls `StartOrClaimAsync`, and only then calls `OpenWorkflowWorkspace`. `LoadSelectedTransactionAsync` validates and loads but does not open Compare.

### Finding 4: The local TR100000562 manifest still records a compute task

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000562\manifest.json:33`

**Detail:** The case manifest records `task_name = Compute Survey Plan`, with `workflow_state = spatial_review_pending` at line 9.

### Finding 5: Working-review geometry exists for this transaction

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000562\output\enterprise_working_publish.json:17`

**Detail:** The published summary records points, lines, and polygon records for transaction scope `100000562`, so missing geometry is not the likely launch blocker.

## Deduced Conclusions

### Deduction 1: The Compare workspace is not launching because the row is not in a configured Compare task

**Based on:** Findings 1, 2, and 4.

**Reasoning:** The code routes using the transaction-list row task name. The recorded task for `TR100000562` is `Compute Survey Plan`. That task is configured as Compute, not Compare, so it will not launch the Compare workspace.

**Conclusion:** Do not add `Compute Survey Plan` to `compare_workflow_stages`; instead, advance/change the Innola task to `Compare` or `Compare Survey Plan`, or add the exact live task name only if that task truly represents Compare.

### Deduction 2: If the user only loads the transaction, Compare will not appear

**Based on:** Finding 3.

**Reasoning:** The Compare launcher runs from `StartSelectedTransactionAsync` after a successful ownership/start result.

**Conclusion:** For a Compare-stage row, the user must Start/claim the transaction, not only Load it.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Current live Innola transaction-list row for `TR100000562` | Confirms whether the server still exposes `TaskName = Compute Survey Plan` or has advanced to a Compare task | Refresh transaction list and inspect/display the row task name; optionally add a diagnostic status/log line for selected row task/type |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `TransactionPanelState.ResolveWorkflowStageRoute` |
| Trigger | User starts or loads a selected transaction row |
| Condition | Row `TaskName` is not in configured Compare stages, or user only loads without starting |
| Related files | `WorkflowSettings.json`, `ShellState.cs`, `TransactionPanelStateTests.cs` |

## Conclusion

**Confidence:** High

The evidence shows `TR100000562` has working-review geometry available, but the local case and route logic still identify the transaction as Compute-stage. Compare is currently enabled only for task names `Compare` and `Compare Survey Plan`, and it opens after Start/claim, not after plain Load.

## Recommended Next Steps

### Fix direction

Advance the live Innola transaction to the configured Compare task (`Compare` or `Compare Survey Plan`). If Innola uses a different exact task label for Compare, add that exact label to `compare_workflow_stages`.

### Diagnostic

Refresh the transaction list and confirm the `TaskName` shown for `TR100000562`. If the UI does not show the task visibly enough, add a temporary or permanent status/detail line showing selected row `TransactionType`, `TaskName`, and resolved route.

## Reproduction Plan

1. Configure `compare_workflow_stages` with `Compare` and `Compare Survey Plan`.
2. Select a transaction-list row whose task is `Compute Survey Plan`.
3. Start the row.
4. Expected: route resolves to Compute and opens the Parcel Workflow dockpane, not the Compare workspace.
5. Change the row task to `Compare` or `Compare Survey Plan`.
6. Start the row.
7. Expected: ownership/start succeeds and the Compare workspace launches.
