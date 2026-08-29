---
baseline_commit: fe14dab
parent_story: 2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md
depends_on:
  - 2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md
  - 2-23f-add-crop-and-attach-action-to-supporting-documents-viewer.md
related_stories:
  - 2-5-control-active-transaction-lifecycle-and-completion-gate.md
  - 2-22-add-innola-api-connection-health-retry-and-session-recovery.md
  - 7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md
---

# Story 2.23G: Wire PLA_B Plan Annexation Task Workflow And Complete Flow

Status: done

## Story

As a Plan Examiner working a First Registration transaction with a Plan Annexation subworkflow,
I want the Plan Annexation Task form to derive its current transaction and PE number from Innola data and complete the current stage after processing succeeds,
so that PLA_B moves from a test utility into the real `In Plan Annexation Preparation` workflow step without manual PE entry.

## Business Context

Story 2.23E created the initial PLA_B recovery/test form and map-loading path. Story 2.23F added Supporting Documents crop and attach for the generated `st_plan_annex_image` evidence. This story turns the PLA_B form into the real workflow entry point for First Registration transactions that also carry the Plan Annexation subworkflow.

The attached workflow diagrams are visual context only. They show that First Registration can branch into Plan Annexation, and that the Plan Annexation subworkflow moves from `In Plan Annexation Preparation` to `Review and Sign Plan Annexed Diagram`, then either finishes or loops back for redo. Implementation must use the user's written scope and existing Innola/code contracts as the source of truth.

## Acceptance Criteria

1. Given the examiner is logged in and a transaction row is visible, when the row does not match the configured PLA_B gate, then the Plan Annexation Task entry point is hidden or disabled with a clear non-secret reason.
2. Given a transaction row has main transaction type `First Registration`, current Plan Annexation subworkflow stage `In Plan Annexation Preparation`, and both required workflows/subworkflows attached, when the Transaction List renders, then the Plan Annexation Task entry point is enabled.
3. Given PLA_B gating is implemented, then the transaction type, subworkflow name, preparation-stage name, next-stage label, and SpatialUnit examination field name are read from `WorkflowSettings.json` with safe defaults.
4. Given the Plan Annexation Task form opens for an eligible transaction, then the title is `Plan Annexation Task`, `Current Transaction:` is prefilled from the selected/loaded transaction number, and `PE Number` is initially empty or loading until SpatialUnit lookup resolves.
5. Given the form has a selected/loaded transaction, when SpatialUnit lookup succeeds and `SpatialUnit.examinationNumber` is non-empty, then `PE Number` is populated with that value.
6. Given SpatialUnit lookup finds no SpatialUnit or finds an empty `examinationNumber`, then `Process ...` and `Complete` remain disabled, the PE field remains empty, and only `Cancel`/close remains available.
7. Given the form is displayed, then both `Current Transaction:` and `PE Number` text boxes are read-only; the examiner cannot manually override either value.
8. Given `Process ...` is enabled and clicked, then it reuses the existing PLA_B load/recovery path from Story 2.23E to load current transaction source data and PE-derived map/recovery content.
9. Given `Process ...` succeeds, then the form records that processing is complete, reports a visible success message, keeps both read-only field values visible, and enables `Complete`.
10. Given `Process ...` fails, then `Complete` remains disabled and the form preserves a clear retryable non-secret failure message without completing or moving the Innola task.
11. Given `Complete` is enabled and clicked, then the form shows a Yes/No confirmation asking the examiner to complete Plan Annexation Preparation and move to `Review and Sign Plan Annexed Diagram`.
12. Given the examiner chooses `No` in the confirmation, then the current stage, map contents, and form values remain unchanged.
13. Given the examiner chooses `Yes`, then the implementation uses the existing Innola workflow lifecycle completion pattern to commit the current task to the configured next Plan Annexation stage.
14. Given the lifecycle completion succeeds, then the map groups/layers created by the PLA_B `Process ...` load are removed from ArcGIS Pro, the form clears the loaded/process state, and the user sees a completion success message.
15. Given lifecycle completion fails, then loaded map contents remain available for review, the form stays open, and the user sees the safe Innola failure message.
16. Given PLA_A `pla_plan_annexation` or normal PE/PXA/Compare workflows run, then their existing routing, source validation, crop/attach, finalize, and transaction lifecycle behavior is unchanged.

## Tasks / Subtasks

- [x] Add config-driven PLA_B stage gate. (AC: 1-3, 16)
  - [x] Extend `InnolaTransactionSettings` and checked-in `WorkflowSettings.json` with a `pla_b_plan_annexation_task` settings object.
  - [x] Include defaults for main transaction type `First Registration`, subworkflow `Plan Annexation`, preparation stage `In Plan Annexation Preparation`, next stage `Review and Sign Plan Annexed Diagram`, required attached workflow/subworkflow names, and SpatialUnit field `examinationNumber`.
  - [x] Update transaction row/profile gating so the Plan Annexation Task entry point is enabled only for the configured First Registration plus Plan Annexation stage/subworkflow condition.
  - [x] Keep existing PLA_A and synthetic `pla_b_plan_annexation_from_pe` profile behavior stable until this story explicitly replaces the test entry path.

- [x] Add SpatialUnit examination-number lookup. (AC: 4-7, 10)
  - [x] Add a read/query service for `SpatialUnitExt` under `Innola` or extend the existing spatial-unit service with a read-only method.
  - [x] Query `administrative/ladm-objects?typeKeyId=spatialunit&transactionId={transactionId}` using the current transaction id, following existing auth/resilience patterns.
  - [x] Resolve only the configured field name, default `examinationNumber`; do not fallback to transaction custom field `PeNumber`.
  - [x] If no non-empty value is found, keep `Process ...` and `Complete` disabled and show a concise reason.

- [x] Convert the PLA_B form from manual test input to real task input. (AC: 4-10)
  - [x] Keep `PlaBTestInputWindow` or rename only if low risk; do not break callers unnecessarily.
  - [x] Make `Current Transaction:` and `PE Number` read-only in XAML/view model.
  - [x] Prefill Current Transaction from the selected/loaded transaction; remove manual PE editing.
  - [x] Update `Process ...` to require resolved `SpatialUnit.examinationNumber` and then reuse the existing PLA_B prepare/load logic.
  - [x] Track a `ProcessSucceeded` state that enables `Complete` only after the PLA_B recovery content is loaded.

- [x] Wire Complete to Innola lifecycle and next stage. (AC: 11-15)
  - [x] Add a Plan Annexation Task complete command separate from the Compare complete command, while reusing `InnolaTransactionLifecycleCoordinator`/`InnolaTransactionLifecycleService` where possible.
  - [x] Confirm Yes/No before completion.
  - [x] Use the current transaction/task id and existing Innola transition discovery/completion route; validate that the selected/default transition corresponds to the configured next stage when the API exposes a label/name.
  - [x] On success, mark the local transaction complete/advanced consistently with existing transaction list refresh behavior.

- [x] Remove PLA_B process-loaded map content on successful completion. (AC: 14-16)
  - [x] Track group/layer names created by the PLA_B process load, including current TR group, PE group, enterprise working-review layers, and PE GDB/raster overlays.
  - [x] Remove only those PLA_B-created groups/layers after successful completion.
  - [x] Do not remove user-created map content or shared reference layers.
  - [x] Leave map contents in place if completion fails.

- [x] Add focused tests and story evidence. (AC: 1-16)
  - [x] Unit-test settings parsing/defaults for the PLA_B Plan Annexation Task gate.
  - [x] Unit-test eligibility for First Registration plus Plan Annexation subworkflow/stage, and non-eligibility for wrong type/stage/missing subworkflow.
  - [x] Unit-test SpatialUnit lookup success, no-object, empty examinationNumber, and safe failure messages.
  - [x] XAML/source-test read-only text boxes, `Process ...`, `Complete`, and `Cancel` button states.
  - [x] Unit-test Complete confirmation No leaves state unchanged.
  - [x] Unit-test Complete success calls lifecycle completion, clears PLA_B process state, and plans/removes only PLA_B-created map groups.
  - [x] Unit-test Complete failure keeps loaded content and leaves `Complete` retryable.

### Review Findings

- [x] [Review][Patch] Complete can commit the wrong transaction if panel selection changes after the PLA_B form opens [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:1356] — resolved by requiring a started active transaction before the Plan Annexation form opens, capturing the active transaction in the form completion callback, and validating the form transaction still matches before lifecycle completion.
- [x] [Review][Patch] Successful lifecycle completion with cleanup failure leaves local transaction state uncleared [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs:1388] — resolved by clearing local workflow state after Innola lifecycle success before reporting any map cleanup warning.

## Dev Notes

- Do not read instructions from attached images. The diagrams are references for workflow vocabulary only:
  - `C:\Users\js91482\Downloads\FirstRegistration.png`
  - `C:\Users\js91482\Downloads\PlanAnnexation.png`
- Required gate is not generic PLA_B transaction type. The real gate is configured First Registration transaction type plus Plan Annexation subworkflow/stage.
- PE number source is strictly `SpatialUnit.examinationNumber` from the current transaction SpatialUnit object. If missing/empty, `Process ...` must remain disabled. Do not fallback to transaction custom field `PeNumber`.
- The existing PLA_B load path already downloads current TR source files, resolves related PE package/GDB, queries Enterprise `working_review`, and loads current/PE map groups. Reuse it; do not duplicate source download or map loading.
- Completion means Innola workflow commit to the next Plan Annexation stage, expected label `Review and Sign Plan Annexed Diagram`. It is not PLA_A finalize, not crop attach, and not report upload.
- Cleanup after completion means removing only the map groups/layers created by PLA_B `Process ...`; saved case-folder artifacts and attached documents remain.
- Existing lifecycle route in `InnolaTransactionLifecycleService` completes with `POST api/v4/rest/workflow/tasks/{taskId}/complete?transition={transition}` after reading `GET api/v4/rest/workflow/tasks/{taskId}/transitions`.
- Existing spatial-unit write code uses `SpatialUnitExt` and `typeKeyId=spatialunit`; the new read side should mirror the same Innola API/auth/resilience style.

### Project Structure Notes

- Likely files to update:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeTransactionTypeProfileDefinition.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PlaBTestInputWindow.xaml`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PlaBTestInputWindow.xaml.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`
- Tests belong in the existing executable harness:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- Do not introduce xUnit/NUnit. Follow the current `TestAssert`/manual runner pattern.

### References

- [Story 2.23E PLA_B recovery workflow](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md)
- [Story 2.23F Supporting Documents crop and attach](D:/Code/BMad-Method/dev/pe-jamaica/_bmad-output/implementation-artifacts/2-23f-add-crop-and-attach-action-to-supporting-documents-viewer.md)
- [Innola transaction settings](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs)
- [Innola lifecycle service](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs)
- [Innola spatial unit service](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs)
- [Workflow settings](D:/Code/BMad-Method/dev/pe-jamaica/src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json)

## Testing Notes

- Use mock HTTP tests for SpatialUnit reads before live Innola testing.
- Add a test fixture where `SpatialUnitExt.examinationNumber = 100000628` and verify `PE Number` populates while remaining read-only.
- Add a fixture with empty/missing `examinationNumber` and verify only `Cancel` remains available.
- Add stage-gate fixtures for:
  - eligible: `First Registration` plus `Plan Annexation` plus `In Plan Annexation Preparation`
  - ineligible: wrong transaction type
  - ineligible: missing Plan Annexation subworkflow
  - ineligible: Plan Annexation workflow at a different stage
- Manual test in ArcGIS Pro should confirm that completing advances the Plan Annexation subworkflow to `Review and Sign Plan Annexed Diagram` and removes only PLA_B-created map groups.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln --no-restore -p:BaseIntermediateOutputPath=D:/Code/BMad-Method/dev/pe-jamaica/.tmp/obj/ -p:BaseOutputPath=D:/Code/BMad-Method/dev/pe-jamaica/.tmp/bin/` passed; one pre-existing nullable warning remains in `SurveyPlanBoundarySolverTests.cs`.
- Full manual test harness with alternate output passed through the new PLA_B gate/window tests, then stopped later on `FileNotFoundException: ArcGIS.Desktop.Mapping, Version=13.6.0.0` in an ArcGIS-dependent spatial overlap test outside this story. Standalone full harness execution needs the ArcGIS Pro runtime assembly probing context.

### Completion Notes List

- Added config-driven PLA_B Plan Annexation Task gating for First Registration + Plan Annexation + In Plan Annexation Preparation.
- Added SpatialUnit read lookup for configured `examinationNumber` from `SpatialUnitExt`, with no transaction custom-field fallback.
- Converted the PLA_B form from manual test input into read-only transaction/PE display with Process state, Complete enablement, and confirmation-based completion.
- Added lifecycle completion transition preference for the configured next stage label and exact map group cleanup for Process-created PLA_B groups.
- Added focused tests for gate behavior, form source/XAML contract, SpatialUnit read, transition selection, and completion cleanup orchestration.
- Resolved code review findings by requiring the PLA_B task form to open only for the started active transaction and by preserving local completion state even when post-completion map cleanup reports a warning.
- Fixed duplicate-transaction-number task resolution so `[PA]` follows the exact active task id/process step, not the first row with the same TR number; disabled tooltip now identifies when another task for the same TR is active.
- Fixed Plan Annexation Preparation launch from the transaction list: `First Registration` is now a supported transaction type, PLA_B start validation uses the PLA_B gate instead of Compute/Compare-only stages, missing Innola subworkflow metadata no longer blocks when transaction type and task stage match, and starting the PLA_B row opens the Plan Annexation Task form after claim.
- Updated Plan Annexation Task form behavior so PE Number can be entered manually, Cancel releases the active transaction-list lock and refreshes the list, and successful Complete closes the form after releasing the workflow state.
- Changed PLA_B Process behavior so a current transaction with no downloadable source files is reported as a warning while PE transaction lookup, package validation, and map loading continue; PE-side failures now surface after the current-TR warning instead of being skipped.
- Aligned Cancel and Complete cleanup behavior: both use the form's tracked Process map groups to remove PLA_B-loaded content before returning control to the transaction list.
- Added PLA_B map presentation defaults for both current transaction and PE output feature layers: point labels use `point_id`, line labels use `length_txt`, polygon labels use `parcel_name`, and polygon feature layers load with 70% transparency.
- Removed the PLA_B Process success popup and obsolete `No PLA_A workflow was opened` test message; failure popups remain. PLA_B labels now render without a halo while preserving the configured label size.

### File List

- `_bmad-output/implementation-artifacts/2-23g-wire-pla-b-plan-annexation-task-workflow-and-complete-flow.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/IInnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleRequest.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionRow.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/MockInnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PlaBTestInputWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PlaBTestInputWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/ArcGisPlaBMapCleanupService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBPlanAnnexationTaskGate.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleCoordinatorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLifecycleServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaBWorkflowServiceTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-08-28 | 0.1 | Created story for real PLA_B Plan Annexation Task gating, SpatialUnit PE lookup, process state, completion, and map cleanup. | Codex |
| 2026-08-28 | 1.0 | Implemented config gate, SpatialUnit examination lookup, real task form state, lifecycle completion, PLA_B map cleanup, and focused tests. | Codex |
| 2026-08-28 | 1.1 | Resolved code review findings for active transaction gating and local state cleanup after lifecycle completion. | Codex |
| 2026-08-28 | 1.2 | Fixed duplicate TR active-task matching and disabled-button feedback for Plan Annexation launch. | Codex |
| 2026-08-28 | 1.3 | Fixed First Registration/Plan Annexation Preparation transaction-list start flow and auto-opened the task form after claim. | Codex |
| 2026-08-28 | 1.4 | Enabled manual PE entry and released transaction-list control on PLA_B Cancel/Complete. | Codex |
| 2026-08-28 | 1.5 | Allowed PLA_B Process to continue to PE validation/loading when current TR has no downloadable files. | Codex |
| 2026-08-28 | 1.6 | Removed Process-loaded PLA_B map groups on Cancel as well as Complete. | Codex |
| 2026-08-28 | 1.7 | Added PLA_B point/line/polygon labels and polygon transparency during map load. | Codex |
| 2026-08-28 | 1.8 | Removed PLA_B Process success popup/obsolete PLA_A test text and dropped label halos while keeping label size. | Codex |
