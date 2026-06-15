---
baseline_commit: handoff-2026-06-14
---

# Story 2.15: Gate Supported Transaction Types Before Workflow Load

Status: ready-for-dev

## Story

As a cadastral technical staff user,  
I want the add-in to allow only supported Innola transaction types to launch the Parcel Workflow,  
so that unsupported work items are blocked before case loading and the workflow only starts for valid NLA Jamaica examination transactions.

## Acceptance Criteria

1. Given the add-in loads local configuration, when transaction-type gating settings are resolved, then the supported Innola transaction types are read from configuration rather than hardcoded only in C#.
2. Given a transaction is selected in the Transaction Panel, when the user chooses Load or Start, then the add-in verifies the transaction’s type/case type against the configured supported transaction type list before launching the Parcel Workflow.
3. Given the selected transaction type is not in the configured supported list, when the user attempts to load or start it, then the add-in blocks workflow launch, shows a clear user message that the transaction type is not valid for Parcel Workflow, and keeps the user in the Transaction Panel.
4. Given the selected transaction type is supported, when the user loads or starts it, then the current transaction load path continues unchanged.
5. Given the Configuration panel is opened, when the user reviews local settings, then the panel shows a `Supported Transaction Types` section listing the currently allowed transaction types used for workflow launch gating.
6. Given the transaction type list is missing, empty, or invalid, when configuration loads, then the add-in falls back deterministically to a safe default list and surfaces a clear warning rather than silently allowing all transaction types.
7. Given the live Innola metadata may represent transaction type under more than one field, when the add-in evaluates gating, then it uses the normalized transaction/case type field already resolved into manifest/session-facing transaction metadata where possible.
8. Given this story is complete, then focused tests cover supported-type allow, unsupported-type block, safe fallback on invalid configuration, and Configuration panel display of the transaction-type allowlist.

## Tasks / Subtasks

- [ ] Define configuration for supported transaction types. (AC: 1, 5-6)
  - [ ] Extend `WorkflowSettings.json` with a `supported_transaction_types` list using stable local configuration conventions.
  - [ ] Define deterministic safe defaults for the initial NLA Jamaica examination transaction families.
  - [ ] Add configuration loader support and warning behavior for missing/invalid lists.

- [ ] Gate workflow launch in the transaction load/start path. (AC: 2-4, 7)
  - [ ] Apply validation before `LoadSelectedTransactionAsync` and `StartSelectedTransactionAsync` transition into case loading.
  - [ ] Use normalized transaction type metadata from the selected row or resolved detail metadata, avoiding duplicated parsing logic where possible.
  - [ ] Ensure unsupported transactions do not change active workflow/session state.

- [ ] Add user-facing messaging and panel behavior. (AC: 3, 5-6)
  - [ ] Show a clear non-technical message when a transaction type is unsupported.
  - [ ] Keep the user in the Transaction Panel with selection intact so they can choose another transaction.
  - [ ] Extend the Configuration window with a read-focused `Supported Transaction Types` section.

- [ ] Preserve current supported transaction behavior. (AC: 4, 7)
  - [ ] Ensure valid examination transactions still load and start through the existing case-folder and lifecycle path.
  - [ ] Avoid regressions in login/session gating or active-transaction locking.

- [ ] Add focused tests. (AC: 8)
  - [ ] Test supported transaction type passes gating.
  - [ ] Test unsupported transaction type blocks before workflow load.
  - [ ] Test missing/invalid configured list falls back safely.
  - [ ] Test configuration view model/window renders the supported transaction types list.

## Dev Notes

### Why This Story Exists

- The current Transaction Panel can surface more Innola work items than the Parcel Workflow should actually process.
- NLA Jamaica plan examination workflows need a controlled allowlist so only the intended transaction families launch this add-in experience.
- This rule belongs to configuration because supported transaction families may evolve by environment or rollout phase.

### Architectural Direction

- Treat supported transaction types as a local operational allowlist, not a hardcoded business rule embedded deep in the load flow.
- Block early, before case folder creation or attachment download begins.
- Prefer reusing normalized transaction metadata already available from selected rows/detail mapping instead of adding a second parsing path.

### Configuration Guidance

Use a list, not a single string. The model should support multiple NLA examination transaction families from the start.

Suggested examples:

- `Plan Examination`
- `Cadastral Plan Examination`

Exact values should match authoritative Innola metadata values in the target environment.

### UX Guidance

Recommended user message:

`This transaction type is not supported by Parcel Workflow. Please return to the transaction list and select a valid examination transaction.`

Keep the behavior calm and blocking, not alarming. This is a business-rule guard, not an application failure.

### Suggested Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/*settings*`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/*`

### References

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`

