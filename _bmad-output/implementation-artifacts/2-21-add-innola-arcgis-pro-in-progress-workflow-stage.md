---
baseline_commit: current-review-2026-07-29
---

# Story 2.21: Add Innola ArcGIS Pro In-Progress Workflow Stage

Status: ready-for-dev

## Story

As a land administration workflow manager,  
I want Innola to show when a PE or PXA transaction is actively being processed in ArcGIS Pro,  
so that office users can see that the task is with Pro and the add-in can safely own, resume, and complete the processing window.

## Business Context

The ArcGIS Pro add-in currently works against Innola task stages that are assigned to a user or group and available for parcel workflow processing. Once the examiner starts work in Pro, Innola does not have a clear intermediate state that communicates "this transaction is currently in ArcGIS Pro."

This creates ambiguity for office users: a transaction may look like it is still at the normal assignment step even though the real processing owner is the Pro add-in. The workflow needs an explicit intermediate Innola stage between the normal assignment/intake stage and the next business stage after Pro completion.

The desired state is an Innola workflow step such as:

```text
In ArcGIS Pro
Processing in ArcGIS Pro
Assigned to ArcGIS Pro
```

Exact naming should be confirmed with the Innola workflow configuration, but the product behavior is clear: once a supported PE/PXA task is started in Pro, Innola should show that the transaction is at Pro. When Pro completes successfully, the add-in should move it to the configured next Innola stage.

## Acceptance Criteria

1. Given a supported PE or PXA transaction is available at a configured pre-Pro Innola stage, when the examiner starts or claims the transaction in the ArcGIS Pro add-in, then the add-in transitions the Innola task to the configured ArcGIS Pro in-progress stage.

2. Given the task is in the ArcGIS Pro in-progress stage, when viewed from Innola, then the task clearly indicates that it is currently with ArcGIS Pro and shows normal transaction context such as transaction number, assigned user/group, owner/claim status, and current workflow step where Innola supports those fields.

3. Given a transaction is in the ArcGIS Pro in-progress stage and owned by the current Pro user, when the user refreshes or reopens the Transaction Panel, then the add-in lists or restores that transaction as resumable.

4. Given a transaction is in the ArcGIS Pro in-progress stage and owned by another user, when a different user attempts to start, resume, or complete it from Pro, then the add-in blocks the action with a clear ownership/status message.

5. Given the examiner selects Save and Close or Stop from Pro, when the resume package upload succeeds, then the task remains in the ArcGIS Pro in-progress stage and can be resumed later by the owning user.

6. Given the examiner cancels or releases the Pro processing session, when the configured cancel/release action is confirmed, then the add-in either keeps the transaction safely resumable in the Pro stage or transitions it back to the configured pre-Pro stage according to Innola workflow configuration.

7. Given Pro finalization succeeds, including required outputs, spatial unit creation, plan checklist writeback, report generation, package upload, and readiness checks, when the user completes the task in Pro, then the add-in transitions the Innola task from the ArcGIS Pro in-progress stage to the configured next Innola workflow stage.

8. Given Pro finalization fails after the transaction is already in the ArcGIS Pro in-progress stage, then the add-in preserves the task in a recoverable state, records failure evidence/audit, and does not incorrectly advance to the next Innola stage.

9. Given Innola transition metadata differs between environments, when the add-in starts or completes the task, then transition identifiers/names are resolved from configuration or Innola transition discovery rather than hardcoded to one environment-only label.

10. Given transaction filtering runs, then the Transaction Panel can distinguish:
    - available pre-Pro tasks that can be started,
    - current user's ArcGIS Pro in-progress tasks that can be resumed,
    - other users' ArcGIS Pro in-progress tasks that are visible only if allowed and not startable.

11. Given PE and PXA both use the ArcGIS Pro in-progress stage, then PE keeps its computation-sheet workflow behavior and PXA keeps its survey-plan workflow behavior after the transition.

12. Given automated tests run, then coverage proves start transition, resume from Pro stage, ownership blocking, save/close behavior, complete transition, failure containment, and PE/PXA profile preservation.

## Tasks / Subtasks

- [ ] Confirm Innola workflow stage and transition contract. (AC: 1-2, 6-9)
  - [ ] Confirm the official stage label, task code, or process step for the ArcGIS Pro in-progress stage.
  - [ ] Confirm transition names/ids for pre-Pro -> Pro, Pro -> next stage, Pro save/hold, and Pro cancel/release.
  - [ ] Confirm what Innola should display when office users open the transaction in this stage.
  - [ ] Confirm whether the stage is assigned to a user, group, service account, or "ArcGIS Pro" pseudo-owner.

- [ ] Add configuration for the Pro in-progress workflow stage. (AC: 1, 6-10)
  - [ ] Add settings for pre-Pro eligible stages, Pro in-progress stage names/codes, completion transition, and optional release/cancel transition.
  - [ ] Allow environment-specific transition ids/names to be configured without code changes.
  - [ ] Surface invalid or missing configuration clearly in Settings and transaction lifecycle diagnostics.

- [ ] Update transaction lifecycle start/claim behavior. (AC: 1-4, 8-11)
  - [ ] After successful claim/start, transition the task into the ArcGIS Pro in-progress stage.
  - [ ] Persist Pro-stage status, owner, transition evidence, and timestamp into the case manifest/audit.
  - [ ] Preserve PE/PXA transaction profile metadata after the stage transition.
  - [ ] Block ownership conflicts with clear user-facing messages.

- [ ] Update Transaction Panel filtering and resume behavior. (AC: 3-5, 10)
  - [ ] Show available pre-Pro tasks as startable.
  - [ ] Show current user's Pro in-progress tasks as resumable.
  - [ ] Prevent starting another user's Pro in-progress task.
  - [ ] Keep Save and Close in the Pro stage and upload a resume package.

- [ ] Update completion/finalization transition. (AC: 7-8)
  - [ ] Complete Pro outputs and readiness checks before transitioning out of the Pro stage.
  - [ ] Upload final package and report artifacts before workflow advancement where configured.
  - [ ] Transition to the configured next Innola stage only after all completion gates pass.
  - [ ] Keep the task recoverable in the Pro stage when completion fails.

- [ ] Add audit and user-facing status messages. (AC: 2, 4-8)
  - [ ] Record start-to-Pro, save/hold, resume, release/cancel, completion, and failure events.
  - [ ] Avoid logging tokens, passwords, certificates, or raw authorization values.
  - [ ] Make status copy clear: "Transaction is currently in ArcGIS Pro", "Resume in ArcGIS Pro", "Completed in Pro and moved to next Innola stage."

- [ ] Add automated coverage. (AC: 1-12)
  - [ ] Unit-test lifecycle transition selection and configuration fallback.
  - [ ] Test PE and PXA both preserve transaction profile/source requirements through Pro-stage transition.
  - [ ] Test current-user resume from the Pro stage.
  - [ ] Test other-user ownership block.
  - [ ] Test Save and Close leaves task in the Pro stage.
  - [ ] Test Complete advances to next stage only after readiness succeeds.
  - [ ] Test completion failure leaves task recoverable and does not advance.

## Dev Notes

### Current Relevant Implementation

Current lifecycle behavior is centered around:

- `InnolaTransactionLifecycleCoordinator`
- `IInnolaTransactionLifecycleService`
- `InnolaTransactionLifecycleService`
- `MockInnolaTransactionLifecycleService`
- `TransactionPanelState`
- `InnolaTransactionLoadService`
- `DefaultTransactionCompletionReadinessService`
- `CaseResumePackageService`

The add-in already has concepts for:

- claim/start
- save progress
- save and close with resume package
- cancel/clear active transaction
- complete after readiness gates
- ownership blocking
- resume package restore
- PE/PXA transaction profile resolution

This story should extend those concepts to include an explicit Innola workflow stage transition into and out of ArcGIS Pro.

### Suggested Settings Shape

Candidate configuration shape:

```json
"arcgis_pro_workflow_stage": {
  "enabled": true,
  "stage_code": "arcgis_pro_in_progress",
  "stage_display_name": "In ArcGIS Pro",
  "pre_pro_stage_names": [
    "Assign Computation Task",
    "Compute Survey Plan"
  ],
  "in_progress_stage_names": [
    "In ArcGIS Pro",
    "Processing in ArcGIS Pro"
  ],
  "start_transition": "send_to_arcgis_pro",
  "complete_transition": "complete_arcgis_pro_processing",
  "release_transition": "",
  "keep_in_pro_stage_on_save_close": true
}
```

Exact field names may follow existing settings conventions.

### Product Policy

Innola remains the workflow source of truth. ArcGIS Pro owns the technical processing window only while the transaction is in the Pro in-progress stage.

The Pro in-progress stage should not change PE/PXA business logic:

- PE remains computation-sheet driven.
- PXA remains survey-plan PDF driven.
- Both remain subject to the same Pro-side stage gates and completion readiness checks.

### Open Questions

- What is the official Innola stage name/code for the ArcGIS Pro in-progress state?
- Should the Pro stage be assigned to the same examiner, their group, or an ArcGIS Pro pseudo-role?
- Should cancel/release return the task to the previous Innola stage, or leave it in Pro with a recoverable resume package?
- Should Innola prevent non-Pro completion from the Pro stage, or is that controlled only by role/transition permissions?
- Should Compare transactions eventually use a similar "In ArcGIS Pro" stage, or is this story limited to PE/PXA compute workflow?

## Dependencies

- Builds on Story 2.5: active transaction lifecycle and completion gate.
- Builds on Story 2.18A: PE/PXA transaction-type workflow profiles.
- Builds on Story 4.3: save and resume through Innola resume package.
- Builds on Story 7.11: plan checklist writeback on compute finalize.

## Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-29 | 0.1 | Initial story for explicit Innola ArcGIS Pro in-progress workflow stage between assignment and post-Pro completion. | Mary / Codex |

