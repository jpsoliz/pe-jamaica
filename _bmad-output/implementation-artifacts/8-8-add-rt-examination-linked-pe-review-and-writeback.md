---
baseline_commit: working-tree-2026-09-04
source_artifacts:
  - _bmad-output/planning-artifacts/rt-examination/rt-examination-classes-and-descriptions.docx
  - _bmad-output/planning-artifacts/rt-examination/RtExamination_WF_innola.png
  - _bmad-output/planning-artifacts/rt-examination/SWF-RtExamination-v2.png
  - _bmad-output/planning-artifacts/rt-examination/FirstRegistration.png
related_stories:
  - 7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md
  - 7-11-write-innola-plan-check-list-on-compute-finalize.md
  - 2-23g-wire-pla-b-plan-annexation-task-workflow-and-complete-flow.md
  - 8-1-add-compare-stage-gating-and-transaction-launch.md
  - 8-4e-add-compare-task-lifecycle-actions-and-reopen-active-workspace.md
---

# Story 8.8: Add RT Examination Linked PE Review And Writeback

Status: review

## Story

As an SMD Plan Examiner working an Innola task at `In RT Examination`,
I want the ArcGIS Pro add-in to load the current RT/FRT/NewCT transaction, resolve the originating PE transaction from the current Plan, load PE-derived sources/spatial units/working-review geometry, let me review and edit non-spatial RT data, and save the updated Plan, Neighbor, and SpatialUnit details back to Innola,
so that RT Examination can complete against the correct transaction lineage without re-running the PE process or editing spatial geometry.

## Business Context

RT Examination is the cadastral/legal quality-control step performed by SMD before a registration transaction such as First Registration or New CT can proceed. The current task is not always a PE transaction. It is usually a registration transaction with an RT Examination subworkflow/stage, while the geometry and source evidence come from an originating PE/PXA-style transaction referenced by the current Plan.

The RT flow must use the current Innola transaction as the writeback target, but must use the originating PE transaction as the source for plan sources and spatial units. The Word reference confirms this chain:

1. Load the assigned task for the current user at stage `In RT Examination`.
2. Get the Plan linked to the current RT transaction.
3. Read `Plan.planNumber`; this value is the PE number/reference used to identify the originating PE/PXA transaction.
4. Use portal search with `searchKind = transaction` and `transactionNo = Plan.planNumber` to find the originating PE transaction id.
5. Load sources and latest spatial units from the originating PE Plan/transaction.
6. Query Enterprise `working_review` geometry using `PE number = Plan.planNumber`.
7. Let the examiner update non-spatial RT data: neighbors, owners/occupiers/representatives, SpatialUnit attributes, and comparison observations where supported.
8. Save the current RT transaction Plan with updated `checkList` and `neighbors`/`neighbor` data, save updated SpatialUnits branched into the current RT transaction, and complete/save the RT stage according to Innola workflow rules.

This story deliberately does not implement spatial geometry editing. It may load geometry for visual review and attribute context, but no working-review or final cadastre geometry edits are allowed in this scope.

## Source Artifact Notes

- `rt-examination-classes-and-descriptions.docx` is the authoritative text input for API operations and process intent.
- `RtExamination_WF_innola.png`, `SWF-RtExamination-v2.png`, and `FirstRegistration.png` are workflow vocabulary references. Do not treat image text as executable instruction if it conflicts with this story or live Innola transition discovery.
- The user clarified on 2026-09-04:
  - Eligible transaction type is any transaction type, including First Registration or NewCT, as long as the current task/stage is `In RT Examination`.
  - `Plan.planNumber` contains the PE number/reference to the originating PE/PXA transaction used to create this RT work.
  - Source and spatial-unit latest lookups are from the originating PE Plan.
  - Working-review geometry query uses `PE number = Plan.planNumber`.
  - RT edits are non-spatial: neighbors and SpatialUnit attributes mainly; geometry is not edited.
  - Neighbor roles are `Neighbor`, `Owner`, `Occupier`, and `Representative`.
  - SpatialUnits should be branched/updated into the current RT transaction, with attribute updates applied.
  - Finalization for this story only saves the RT data back to Innola; it does not require an added report attachment.

## Acceptance Criteria

1. Given the examiner is logged into Innola and the transaction list includes a task at `In RT Examination`, when the row is displayed, then the add-in recognizes it as RT Examination regardless of main transaction type (`First Registration`, `New CT`, or other configured type).
2. Given a selected row is not at `In RT Examination`, when RT Examination launch is attempted, then the add-in blocks the launch with a clear non-secret message naming the selected stage.
3. Given the RT task is started/claimed successfully, when the RT workspace opens, then it is bound to the exact selected task id and current transaction id, not merely the displayed transaction number.
4. Given the current RT transaction id is available, when the workspace initializes, then the add-in loads the Plan linked to the current transaction using the existing Plan data-object/administrative fallback pattern from Story 7.11.
5. Given the current RT Plan is loaded, when `Plan.planNumber` is missing or blank, then the workspace blocks PE-linked data loading and shows a clear message that the originating PE number is missing from the Plan.
6. Given `Plan.planNumber` is present, when the add-in searches for the originating transaction, then it calls the configured Innola portal search endpoint with `searchKind = transaction` and `transactionNo = Plan.planNumber` using the active session and client certificate behavior.
7. Given portal search returns exactly one eligible originating PE/PXA transaction, when linked data loading continues, then that transaction id is retained as `originating_pe_transaction_id` and its transaction number is retained as `originating_pe_number` in local RT artifacts.
8. Given portal search returns no match, multiple ambiguous matches, malformed data, unauthorized, or a non-success response, then the workspace stops before any writeback and presents a retryable non-secret diagnostic. Multiple matches must not be guessed.
9. Given the originating PE transaction is resolved, when sources are loaded, then the add-in calls `GET /api/v4/rest/plan/sources/latest` with `planTransactionId` from the originating PE Plan and `transactionId` from the current RT transaction where the API requires current transaction context.
10. Given the originating PE transaction is resolved, when spatial units are loaded, then the add-in calls `GET /api/v4/rest/plan/spatialunits/latest` with `planNumbers = [Plan.planNumber]` and preserves returned `SpatialUnit.uid`, `id`, `link`, and unknown fields.
11. Given the RT workspace loads map context, when it queries Enterprise `working_review`, then the query uses `PE number = Plan.planNumber` and loads the matching geometry into ArcGIS Pro for visual review only.
12. Given the working-review query finds no geometry or fails because of schema/auth/service/network issues, then the workspace keeps non-spatial RT review available only if product-safe, records a warning/error artifact, and must not claim spatial verification passed.
13. Given RT linked data is loaded, when the examiner reviews the data, then the workspace exposes editable non-spatial fields for Neighbor, Owner, Occupier, Representative rows and SpatialUnit attributes available from the loaded Plan/SpatialUnit objects.
14. Given neighbor/party rows are edited, when values are saved locally, then role values are constrained to `Neighbor`, `Owner`, `Occupier`, and `Representative`, and all editable text values preserve original and reviewed values for audit.
15. Given SpatialUnit attributes are edited, when values are saved locally, then geometry fields and coordinate arrays are not editable or mutated; only approved attribute fields are changed.
16. Given the current RT Plan contains existing checkList rows, when RT save runs, then the add-in updates only recognized RT/Plan check rows required for this flow and preserves ids, uid, version, registered surveyor, link, unknown fields, and unrecognized checks.
17. Given the current RT Plan contains neighbor/party rows, when RT save runs, then the add-in updates/appends reviewed `Neighbor`, `Owner`, `Occupier`, and `Representative` data into the current Plan using the confirmed Innola Plan property for this environment. Until live confirmation proves otherwise, use the existing Story 7.11 `neighbors` implementation pattern and make the property name isolated behind the RT Plan writeback service.
18. Given reviewed neighbor/party rows are saved repeatedly, when RT save is retried, then duplicate Plan rows are avoided using a deterministic key over role, name, address, volume, folio, lot, land valuation number, and examination number where available.
19. Given originating PE SpatialUnits were loaded, when RT save branches them into the current transaction, then the add-in calls `POST /api/v4/rest/administrative/ladm-objects/new-version-by-uid?typeKeyId=spatialunit&uids=[SpatialUnit.uid]&transactionId={current_rt_transaction_id}` or a documented equivalent behind a service seam.
20. Given SpatialUnit versions are branched or already exist for the current RT transaction, when RT save persists attribute updates, then the add-in calls `POST /api/v4/rest/administrative/ladm-objects?typeKeyId=spatialunit&transactionId={current_rt_transaction_id}` with full SpatialUnit objects and preserves API-generated identity/link fields.
21. Given a SpatialUnit lacks `uid`, when branching is required, then the save is blocked for that SpatialUnit with a clear diagnostic instead of creating an unrelated SpatialUnit from scratch.
22. Given Plan writeback or SpatialUnit save fails, then later completion/transition steps do not run, local artifacts remain available for retry, and the user sees which RT writeback step failed.
23. Given all RT data saves succeed, when the examiner chooses the final save/complete action, then the add-in shows a Yes/No confirmation before committing the task.
24. Given the examiner cancels the confirmation, then no Innola save/complete call runs, the RT workspace remains open, and loaded map layers remain available.
25. Given the examiner confirms and Innola save/complete succeeds, then the add-in shows a success message, removes only RT-loaded transaction map groups/layers, clears the RT workspace state, refreshes the transaction list, and does not delete case-folder artifacts.
26. Given Innola completion requires a transition, then the implementation uses existing transition discovery and selects the transition that advances out of `In RT Examination`; if the exact next-stage label is not available from configuration, the service must use the available transition metadata and record the selected key/label in the local artifact.
27. Given any RT HTTP diagnostic or local artifact is written, then no access token, password, cookie, raw certificate material, or unbounded sensitive service response is logged.
28. Given the case is reopened after partial RT work, then the workspace can recover current Plan reference, originating PE reference, loaded sources/spatial units metadata, reviewed edits, save status, and last failure/success state from case-folder artifacts.
29. Given automated tests run, then coverage proves stage routing, exact selected-task binding, Plan load fallback, Plan.planNumber PE resolution, portal search no/multiple match failures, latest sources/spatial units request construction, working_review query key, editable non-spatial field persistence, geometry non-mutation, Plan neighbor/check preservation, SpatialUnit branch/save, failure short-circuit, confirmation cancel, success cleanup, and secret redaction.

## Tasks / Subtasks

- [x] Add RT Examination routing and settings. (AC: 1-3, 26)
  - [x] Add `RtExaminationSettings` under Innola settings with safe defaults: enabled, stage name `In RT Examination`, optional subworkflow name `RT Examination`, optional next-stage/transition preference, and working-review PE field mapping.
  - [x] Extend `WorkflowSettings.json` with RT settings without changing existing Compute, Compare, PLA_B, or Fabric behavior.
  - [x] Extend `ParcelWorkflowStageRoute` and transaction panel route logic with `RtExamination`.
  - [x] Allow any main transaction type when task/stage is `In RT Examination`; do not require First Registration or NewCT in code.
  - [x] Keep selected-row/task-id binding exact when multiple rows share the same transaction number.

- [x] Add current RT Plan and originating PE resolution services. (AC: 4-10, 27)
  - [x] Reuse or extract Story 7.11 Plan GET fallback behavior rather than duplicating raw HTTP logic.
  - [x] Preserve fetched Plan body shape (`Plan` or `Plan[]`) and save using the same route family that supplied the Plan.
  - [x] Add an Innola portal transaction search adapter for `POST /api/v4/rest/portal/searches` with `searchKind = transaction` and `transactionNo = Plan.planNumber`.
  - [x] Block on zero/multiple/malformed PE matches; never guess the originating transaction.
  - [x] Add adapters for `GET /api/v4/rest/plan/sources/latest` and `GET /api/v4/rest/plan/spatialunits/latest` using the originating PE Plan/transaction data.

- [x] Add RT workspace and local artifact model. (AC: 11-15, 28)
  - [x] Create RT-specific workspace files under `Workflow/RtExamination` plus WPF window/dockpane surface consistent with existing dense operational UI.
  - [x] Persist `working/rt_examination_context.json` with current RT transaction id/number/task id, current Plan id/uid/trId/trNo/planNumber, originating PE transaction id/number, source/spatial-unit counts, working-review query key, timestamps, and warnings.
  - [x] Persist `working/rt_examination_review.json` with original/reviewed neighbor/party rows, SpatialUnit attribute edits, comparison observations, and editor/timestamp metadata.
  - [x] Load Enterprise `working_review` geometry by `Plan.planNumber` for visual review only, behind an ArcGIS-safe service using `QueuedTask.Run` where required.
  - [x] Track RT-created map groups/layers for cleanup; do not remove shared basemaps/reference layers or user-created content.

- [x] Implement non-spatial RT edit rules. (AC: 13-18)
  - [x] Support roles `Neighbor`, `Owner`, `Occupier`, `Representative` in a constrained role combo.
  - [x] Expose editable fields already known from Story 7.11 Neighbor contract: name, address, volume, folio, lot, landValNumber, examNumber; add other safe fields only when they exist in the fetched Plan/SpatialUnit objects.
  - [x] Expose editable SpatialUnit attributes only from approved non-geometry fields; explicitly exclude geometry, points, bfsMinus/bfsPlus, bfMinus/bfPlus, and coordinate arrays.
  - [x] Preserve unknown Plan, Neighbor, and SpatialUnit fields.
  - [x] Make retries idempotent and avoid duplicate party rows.

- [x] Implement RT save/writeback orchestration. (AC: 16-27)
  - [x] Add `IInnolaRtExaminationService` or equivalent façade that orchestrates Plan writeback, SpatialUnit branch/save, and lifecycle completion.
  - [x] Save Plan `checkList` and neighbor/party data to the current RT transaction, not the originating PE transaction.
  - [x] Branch originating PE SpatialUnits into the current RT transaction by `uid`, then save updated SpatialUnit attributes to the current RT transaction.
  - [x] Do not create or update spatial geometry.
  - [x] Stop immediately on the first writeback failure and record `working/rt_examination_api_failure.json`.
  - [x] On success, record `working/rt_examination_api_request.json`, `working/rt_examination_api_response.json`, and lifecycle audit entries.
  - [x] Show final confirmation before save/complete; after success, show completion message, cleanup RT-loaded layers, close workspace, and refresh transaction list.
  - [x] Do not attach a new RT report in this story.

- [x] Add focused tests. (AC: 1-29)
  - [x] Settings parser/default tests for RT stage and transition settings.
  - [x] Transaction panel routing tests for First Registration, NewCT, and arbitrary transaction type at `In RT Examination`.
  - [x] Exact selected task id tests for duplicate transaction-number rows.
  - [x] Mock HTTP tests for current Plan lookup, Plan.planNumber extraction, portal search PE resolution, no-match/multi-match failures, `plan/sources/latest`, and `plan/spatialunits/latest`.
  - [x] Service tests for Plan body-shape preservation, neighbor/party duplicate prevention, allowed role values, and SpatialUnit attribute-only mutation.
  - [x] SpatialUnit branch/save tests using `new-version-by-uid` and `ladm-objects` request construction.
  - [x] ViewModel tests for editable non-spatial values, confirmation cancel, success message, cleanup, close, and retry-safe failure behavior.
  - [x] Secret-redaction tests for all failure artifacts/diagnostics.

## Developer Context

### Reuse First

- Reuse Story 7.11 `InnolaPlanCheckService` behavior for Plan route compatibility, full JSON preservation, Plan `checkList`, and `neighbors` writeback. Do not fork a second incompatible Plan serializer.
- Reuse Story 7.9 `InnolaSpatialUnitService` patterns for create/default, branch/save, full `SpatialUnitExt` JSON preservation, local request/response/failure artifacts, auth, client certificate, and failure short-circuit behavior.
- Reuse `InnolaTransactionLifecycleService` transition discovery/completion through `InnolaTransactionLifecycleRequest.DesiredTransitionName`; do not POST workflow completion directly from a ViewModel.
- Reuse `TransactionPanelState` and `ParcelWorkflowStageRouter` stage-routing patterns from Compare/PLA_B/Fabric instead of creating a separate transaction list.
- Reuse ArcGIS map/layer service seams and `QueuedTask.Run` patterns. ViewModels must not manipulate ArcGIS map state directly.

### UX Mockup

RT Examination should open as a dedicated ArcGIS Pro `ProWindow`, closer to `CompareWorkspaceWindow` than to the smaller Fabric Maintenance confirmation window. The examiner needs one work surface for linked PE context, non-spatial edits, save, completion, and cleanup.

```text
RT Examination

Transaction No: 100000xxx        Stage: In RT Examination        Status: Ready / Dirty / Saving
Current Type: First Registration  PE Plan No: 100000yyy           Originating PE: Resolved / Not resolved

[Load Linked PE Data] [Save] [Complete RT Examination] [Suspend] [Cancel]

Tabs:
  Context | Neighbors / Parties | Spatial Units | Plan Check | Sources / Map Evidence
```

The first screen should land on `Context` after launch. If linked PE data has not loaded yet, show the blocking reason and keep `Save` / `Complete RT Examination` disabled. Once loaded, the status row should show current RT Plan, originating PE transaction, latest source count, latest spatial-unit count, and working-review geometry load status.

`Neighbors / Parties` should be the primary editing tab:

```text
Role            Name        Address      Volume   Folio   Lot   LandVal No.   Exam No.
Neighbor        ...
Owner           ...
Occupier        ...
Representative  ...
```

Use a constrained combo for `Role` with `Neighbor`, `Owner`, `Occupier`, and `Representative`. All listed fields are editable. Preserve unknown fields from the fetched Plan object and avoid adding duplicate rows on retry/reload.

`Spatial Units` should expose attribute-only edits in a grid. Geometry fields, coordinate arrays, point references, and boundary fields must not appear as editable controls. If a field is present in live `SpatialUnitExt` and is not geometry-related, render it as an editable text/number field with the original value visible.

`Plan Check` should show supported checklist rows and observations with compact editable controls. It should not create or attach a report in this story.

`Sources / Map Evidence` should show the linked PE sources, working-review query key (`Plan.planNumber`), loaded map group/layer names, and warnings. This is review evidence only; no embedded map preview is needed because ArcGIS Pro's active map is the companion surface.

Completion behavior:

- `Save` writes Plan neighbors/check values and SpatialUnit attribute changes to the current RT transaction, then keeps the workspace open.
- `Complete RT Examination` shows a Yes/No confirmation before save/complete.
- On success, show a completion message, remove only RT-loaded map layers/groups, clear RT workspace state, refresh the transaction list, and close the window.
- On failure, keep the workspace open, preserve edits, write safe diagnostics, and leave layers loaded for retry.
### API Contract From RT Reference

```http
GET /api/v4/rest/workflow/my-tasks
POST /api/v4/rest/application/my-tasks/search
GET /api/v4/rest/workflow/tasks/{taskId}
GET /api/v4/rest/data/objects?typeKeyId=plan&transactionId={currentRtTransactionId}
POST /api/v4/rest/portal/searches
GET /api/v4/rest/plan/sources/latest?planTransactionId={originatingPePlanTrId}&transactionId={currentRtTransactionId}
GET /api/v4/rest/plan/spatialunits/latest?planNumbers=[{planNumber}]
PUT /api/v4/rest/data/objects?typeKeyId=plan&transactionId={currentRtTransactionId}
POST /api/v4/rest/administrative/ladm-objects/new-version-by-uid?typeKeyId=spatialunit&uids=[...uid...]&transactionId={currentRtTransactionId}
POST /api/v4/rest/administrative/ladm-objects?typeKeyId=spatialunit&transactionId={currentRtTransactionId}
GET /api/v4/rest/workflow/tasks/{taskId}/transitions
POST /api/v4/rest/validation/tasks/{taskId}/transition-check
POST /api/v4/rest/workflow/tasks/{taskId}/complete?transition={key}
```

The current RT transaction is the writeback target. The originating PE transaction/Plan is the read/reference source for latest sources and spatial units.

### Data Boundaries

- Editable: non-spatial neighbor/party data, SpatialUnit attributes, comparison observations, and supported Plan check values.
- Not editable: spatial geometry, coordinate arrays, ArcGIS final cadastre geometry, Enterprise authoritative layers, CADMAP, CADINDEX, and Parcel Fabric authoritative targets.
- `Plan.planNumber` is the PE number and the key for `working_review` geometry lookup.
- `Plan.trId` used for `plan/sources/latest` must come from the originating PE Plan once that Plan is resolved.
- If the live API reveals the current RT Plan already carries enough originating PE metadata to avoid an additional PE Plan lookup, keep the adapter flexible but preserve the audit fields that show which source produced the result.

### Suggested Files To Inspect Before Dev

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaPlanCheckService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/FabricMaintenance/FabricMaintenancePromotionServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaBWorkflowServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaPlanCheckServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaSpatialUnitServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Implementation Guardrails

- Keep all HTTP calls behind Innola services with `InnolaHttp.BuildUri`, `InnolaHttp.ApplyAuthHeaders`, `InnolaApiResilience`, and configured certificate behavior.
- Preserve all unknown JSON fields in Plan, Neighbor, and SpatialUnit objects.
- Write local artifacts before/after remote writes so failures are diagnosable and retryable.
- Do not mark local transaction complete until all configured RT save steps succeed.
- Do not upload or attach an RT report in this story.
- If the exact next RT transition label is not known from settings or diagrams, rely on transition discovery and record the selected transition; do not hardcode a guessed stage label.
- Use `StringComparer.OrdinalIgnoreCase` for stage, role, source type, and transaction type matching.
- Keep WPF operational and dense. Avoid marketing/hero layout. Use grids, tabs, combo boxes, and clear status text consistent with existing Compare/Fabric surfaces.
- Add tests in the existing executable harness. Do not introduce xUnit/NUnit.

## Testing Notes

Run focused tests first:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"
```

Then run related regression slices:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "innola plan check" "spatial unit" "transaction panel"
```

Package only after focused and related tests pass:

```powershell
tools/package_addin.ps1 -Configuration Release
```

## Open Questions

- Exact next-stage label after `In RT Examination` should be validated from the added `FirstRegistration.png` and, more importantly, from live `GET /workflow/tasks/{taskId}/transitions`. This is not blocking if the implementation records transition metadata and avoids hardcoding a guessed label.
- Confirm whether the live Plan property is spelled `neighbors`, `neighbor`, or `neighbours` for RT. Story 7.11 uses `neighbors`; this story requires the property name to be isolated so a live correction is low risk.
- Confirm which SpatialUnit attributes RT examiners must edit beyond known fields such as lot, landValNumber, examNumber/examinationNumber, address, legal/survey/gis area, parish/cad district, land use, and description.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Story Creation Notes

- Created from user-provided RT Examination Word/API reference and workflow images in `_bmad-output/planning-artifacts/rt-examination`.
- Mary/Winston review conclusion: implement as one end-to-end story but keep routing, linked PE lookup, editable review state, Plan writeback, SpatialUnit branch/save, and lifecycle completion behind separate services.
- Story is assigned to Epic 8 because RT Examination is comparison/reconciliation work over linked PE data and should reuse Compare transaction routing/lifecycle patterns.

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination" "transaction panel"` - RT and transaction-panel tests progressed through the new RT APP-profile regression; run later stopped on an existing toolbar command-state assertion unrelated to the RT load patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests, including RT selected stage with live detail case/profile `APP`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors.
- `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000854` inspection - fresh case manifest exists but has no `innola_transaction`, no `workflow_profile`, no detected profile, no source files, and no working RT artifacts. This confirms the no-form symptom happens before RT workspace context is successfully persisted.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests after TR `100000854` no-form diagnostics patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors after TR `100000854` no-form diagnostics patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment upload"` - PASS 9 tests after shared attachment filename normalization.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.386` after attachment filename normalization.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "attachment"` - PASS 21 tests, including download `documentName` leaf-name normalization and upload leaf-name normalization.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "transaction load"` - PASS 21 tests, including absolute/path-shaped attachment metadata copied inside the Case Folder by leaf name and traversal still blocked.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -p:GenerateAssemblyInfo=false -p:GenerateTargetFrameworkAttribute=false -p:UseSharedCompilation=false -- "rt examination"` - PASS 7 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj /p:UseSharedCompilation=false` - Build succeeded, 0 warnings/errors.
- `tools/package_addin.ps1 -Configuration Release` - Add-in package produced and registered as version `1.1.388` after TR `100000854` attachment download/local-copy filename normalization.

### Completion Notes

- Added RT Examination stage routing that is stage-driven and not limited by main transaction type.
- Added RT Examination ProWindow with Context, Neighbors / Parties, Spatial Units, Plan Check, and Sources / Map Evidence tabs.
- Added Innola RT service for current Plan lookup with fallback, `Plan.planNumber` PE resolution, source/spatial-unit latest reads, neighbor/party writeback, SpatialUnit branch/save, lifecycle completion, safe diagnostics, and artifact persistence.
- Wired `working_review` geometry loading through the existing ArcGIS Compare map integration seam using `rt_examination.working_review_pe_number_field = Plan.planNumber`, with loaded map group cleanup after successful completion.
- Added focused tests for RT settings, stage routing, allowed roles, geometry edit exclusion, deterministic party dedupe, XAML surface, and transaction-panel RT launch for First Registration-style rows.
- Patched transaction loading for case `100000854` shape: when the selected row is exactly `In RT Examination`, the loader no longer rejects live detail metadata reporting `APP`; it creates an RT review profile and permits an attachment-free current RT case so linked PE data can load from the RT workspace.
- Preserved exact selected task id/transaction number/process-step matching so the duplicate `100000854 - Assign Legal Officer` row is not treated as RT.
- Patched the RT workspace launcher to avoid silent returns: if selected transaction state is missing, transaction numbers do not match, or the loaded case folder is absent, the add-in now shows a specific `RT Examination - {transactionNumber}` warning instead of doing nothing.
- Added guarded RT load/save exception handling so linked PE/API/file failures surface in the RT window status text instead of closing or hiding the reason.
- Patched shared Innola attachment upload to strip local folder paths from multipart file names. This fixes the RT/Fabric-style failure `attachment file name must not contain a path` while preserving downloads into the current/main transaction case folder.
- Patched shared Innola attachment download and RT/current transaction case-folder copy to strip absolute/path-shaped metadata to the leaf filename. This addresses TR `100000854` no-form progress where the case manifest remained at intake because source attachment download/copy failed before RT workspace creation.
- Preserved path traversal blocking for unsafe relative attachment names such as `..\escape.pdf`.
### File List

- `_bmad-output/implementation-artifacts/8-8-add-rt-examination-linked-pe-review-and-writeback.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLoadService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionLoadServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RtExaminationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RtExaminationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/RtExamination/RtExaminationModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/RtExamination/InnolaRtExaminationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/RtExaminationTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-09-04 | 1.1 | Implemented RT Examination routing, workspace, Innola linked-PE load/writeback, working_review map load/cleanup seam, and focused regression tests. | Amelia / Codex |
| 2026-09-04 | 1.2 | Fixed path-shaped Innola attachment metadata handling for TR `100000854` download and current case-folder copy; preserved traversal blocking. | Amelia / Codex |
| 2026-09-04 | 1.0 | Created RT Examination story covering stage routing, linked PE load/review, non-spatial edits, Plan/neighbors/SpatialUnit save, lifecycle completion, and cleanup. | Mary / Winston / Codex |