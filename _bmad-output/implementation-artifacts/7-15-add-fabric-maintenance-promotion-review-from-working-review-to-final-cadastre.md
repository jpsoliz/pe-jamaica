---
story_id: "7.15"
story_key: "7-15-add-fabric-maintenance-promotion-review-from-working-review-to-final-cadastre"
title: "Add Fabric Maintenance Promotion Review From Working Review To Final Cadastre"
status: "partially-implemented"
created: "2026-08-30"
baseline_commit: "adf2032"
source_request: "Proceed writing story 7-15 for the Fabric Maintenance stage that promotes working_review spatial data into either Legal or Fiscal/Cadastral final layers after examiner review and decision."
depends_on:
  - "7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md"
  - "7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md"
  - "8-5-persist-compare-decision-and-unlock-commit-stage.md"
  - "8-6-wire-commit-stage-readiness-to-compare-approval-and-authoritative-promotion.md"
  - "8-7-add-parcel-search-dockpane-tab.md"
ux_reference:
  - "_bmad-output/ux-artifacts/fabric-maintenance-promotion-stage-mockup.html"
---

# Story 7.15: Add Fabric Maintenance Promotion Review From Working Review To Final Cadastre

## Status

partially-implemented

## Story

As a cadastral examiner working a Fabric Maintenance task,
I want to load the active transaction's working_review geometry, resolve its PE/examination number, review topology and final-layer conflicts against exactly one selected cadastre target, and record an explicit promotion decision before final write,
so that Legal or Fiscal/Cadastral data is updated only after controlled review, audit, and examiner confirmation.

## Business Context

Fabric Maintenance is the stage where reviewed spatial data leaves the Enterprise working_review layer and becomes final cadastre data. Unlike Compute Finalize stories that close working-layer review state, this story owns the examiner-facing promotion review and the guarded final-layer write path.

Innola routing for this story is:

- Subworkflow: `Parcel Fabric Maintenance`
- Stage: `In Parcel Fabric Update`

This stage must use two transaction identifiers:

1. Current transaction number: the active Innola transaction/task number that scopes the workflow session, working_review records, audit artifacts, and completion gate.
2. Parcel in Review: the PE number from `SpatialUnitExt.examinationNumber`, used to query the `working_review.transactionNumber` field and drive spatial comparison against the selected final target. If the SpatialUnit examination number is missing, the user must be able to type this value manually before loading review data.

The examiner must choose exactly one final target for a promotion run using radio-button selection:

- Legal
- Fiscal/Cadastral, displayed to users as `Cadastral` and mapped internally to `Fiscal_Cadastre`

Legal and Fiscal/Cadastral are distinct authoritative targets. A single promotion run must never write to both.

The UX approach is hybrid: the add-in orchestrates the transaction context, target selection, parcel loading, results grid, topology/attribute review checklist, decision capture, audit artifacts, and guarded write. ArcGIS Pro remains the primary map, topology, editing, attribute table, and visual inspection surface. The add-in loads both the working parcel and selected final target context into the active map with transparent symbology so the examiner can visually inspect overlap and conflicts. The add-in should show which topology and attribute rules were validated, but the examiner remains responsible for the final cadastral decision.

## Acceptance Criteria

1. Fabric Maintenance workflow routing exposes a promotion stage only for the configured `Parcel Fabric Maintenance` subworkflow and `In Parcel Fabric Update` stage, and only when the active transaction is started or otherwise eligible for examiner work.
2. Opening the stage resolves the current transaction number from the active workflow session and blocks if the add-in cannot prove which Innola transaction is active.
3. Opening the stage resolves the Parcel in Review value from the spatial unit's `SpatialUnitExt.examinationNumber` field and displays both `Current Transaction` and `Parcel in Review` in the workspace.
4. If `SpatialUnitExt.examinationNumber` is missing, blank, or ambiguous, the stage opens with `Parcel in Review` editable and displays a clear message that the PE number must be entered manually before loading review data.
5. The stage requires the examiner to select one final target with radio buttons: `Legal` or `Cadastral`. The implementation must reject null target selection and must reject any attempt to select or write to both targets in one run.
6. The workspace provides a `Load Parcel` action that requires a selected final target and a non-blank Parcel in Review value.
7. `Load Parcel` queries configured Enterprise `working_review` layers by `working_review.transactionNumber = [Parcel in Review]`, not by the active/current Innola transaction number.
8. The stage queries candidate final cadastre records only from the selected target's configured layer group and field mappings. It must not hardcode service URLs, layer names, field names, or object identifiers.
9. Candidate final-layer lookup uses the loaded working-review parcel geometry as the spatial query input against the selected final target, using the existing Compare Neighbor Search spatial relation setting: intersects parcels only or surrounding parcels.
10. For `Fiscal_Cadastre`, the canonical final target lookup is the configured `Parcel` layer using `PID` where `parcel_status = active`.
11. For `Legal_Cadastre`, the canonical final target lookup is the configured `Legal_Parcel` layer using the `PID` field.
12. If no final candidate is found, the stage reports that the promotion appears to be a new final record candidate and limits decision wording accordingly.
13. If multiple final candidates are found, the stage requires examiner selection of the intended target candidate before allowing implemented final actions.
14. `Load Parcel` loads both the working-review parcel and the selected final target parcel context into the active ArcGIS Pro map as review-only layers or selections. It must not write to final layers during review.
15. Map symbology must use transparency or equivalent visual treatment so the working-review parcel and final target candidates can be compared clearly when they overlap.
16. The workspace shows a compact results grid after `Load Parcel`, including at minimum source label, query key, feature/candidate count, spatial relation mode, and status message for the working-review query and selected final target query.
17. Automated topology review checks include, at minimum, geometry validity, spatial reference compatibility, empty geometry, self-intersection or invalid rings where supported, overlap/conflict against selected final target, boundary offset tolerance, area delta, duplicate target candidate risk, stale working-review publish state, and missing required attributes.
18. Attribute review shows configured field comparison for parcel identifier, lot number, plan or survey reference, area, parish, PE/examination number, tenure or BAUnit identifiers when available, and source transaction metadata.
19. The workspace provides focused review tool areas for `Topology Review` and `Attribute Review`, showing which rules/checks have been validated. It should not include a generic `Refresh Review` action in this patch.
20. The examiner must see these promotion decision options:
    - `replace_existing`
    - `keep_existing_discard_working`
    - `merge_update_attributes_only`
    - `send_back_for_review`
21. `replace_existing` is visible but not implemented in this story. If the user clicks it, the add-in displays a popup message with exactly `To be implemented`, does not persist it as an executable decision, and keeps final write blocked for that action.
22. `merge_update_attributes_only` is visible but not implemented in this story. If the user clicks it, the add-in displays a popup message with exactly `To be implemented`, does not persist it as an executable decision, and keeps final write blocked for that action.
23. Decision notes are required when checks produce blocking findings, when multiple target candidates were present, when the selected action is `keep_existing_discard_working`, or when the selected action is `send_back_for_review`.
24. The review stage persists a draft review artifact and a final promotion decision artifact in the transaction case folder so the workspace can resume without losing examiner work.
25. `Approve For Final Write` is enabled only after required review checks, target selection, candidate selection when needed, implemented decision selection, and decision-note requirements are satisfied.
26. Final write is a separate confirmation action after review approval. The confirmation must display current transaction number, Parcel in Review, selected final target, selected decision, working feature counts, target candidate identity, and the output audit artifact name.
27. Immediately before final write, the service revalidates the working_review record identity/hash or equivalent version metadata, selected target, selected target candidate identity, decision artifact, authentication state, and configured schema/write permissions. A user who can access the Innola transaction is considered authorized to perform the final write for this story.
28. `replace_existing` final-layer execution is out of scope for this story and must not edit final target features.
29. For `keep_existing_discard_working`, the service does not edit the final target. It records the working_review state as discarded/closed with examiner rationale and audit evidence.
30. `merge_update_attributes_only` final-layer execution is out of scope for this story and must not edit final target attributes or geometry.
31. For `send_back_for_review`, the service does not edit the final target and marks the working_review record/case as returned or needing review with examiner rationale.
32. A final promotion summary artifact is written for every terminal action. It includes current transaction number, Parcel in Review, selected target, decision, examiner, timestamps, working feature references, target feature references, topology/check results, attribute/check results, write result counts, diagnostics, and pre-write evidence references.
33. The final promotion summary is attached back to the Innola transaction as a supporting document after it is successfully written.
34. Working_review rows and case/index metadata are updated after terminal action with promotion status, selected final target, selected decision, final target references when applicable, audit artifact paths, and lifecycle timestamps.
35. Innola transaction completion is allowed only after the final promotion action has succeeded, the promotion summary artifact exists, and the promotion summary has been attached to the Innola transaction. Failed or partial writes must block completion and preserve enough diagnostics for retry or manual recovery.
36. Reopening the transaction restores the latest draft review, loaded parcel results, final decision, write status, final promotion summary state, and Innola attachment status, and prevents duplicate writes or duplicate attachment uploads unless the examiner explicitly starts a configured retry or supersede flow.
37. Automated tests cover target exclusivity, transaction/PE resolution, working_review lookup by Parcel in Review, final target spatial query planning using Compare Neighbor Search settings, map-load planning with transparency, compact results-grid population, topology/attribute rule visibility, not-implemented option popups, decision persistence, final write confirmation gating, implemented decision paths, promotion summary attachment, lifecycle completion blocking, and no-cross-target-write behavior.

## Tasks

- [x] Add Fabric Maintenance promotion stage routing and eligibility checks for `Parcel Fabric Maintenance` / `In Parcel Fabric Update`.
- [x] Resolve and display current transaction number and spatial-unit PE/examination number.
- [x] Rename the PE context label to `Parcel in Review` and keep it editable when `SpatialUnitExt.examinationNumber` is missing.
- [x] Replace final target buttons with mutually exclusive Legal/Cadastral radio buttons and remove the generic `Refresh Review` action for now.
- [x] Add `Load Parcel` action that queries `working_review.transactionNumber = [Parcel in Review]` and then queries the selected Legal/Cadastral layer by spatial relationship to the working parcel.
- [x] Load both working-review and selected final target layers/features into the active ArcGIS Pro map with transparent symbology for overlap inspection.
- [x] Populate a compact review-results grid with working parcel count, final candidate count, query keys, spatial relation mode, and query status.
- [x] Reuse Compare Neighbor Search spatial relation settings for intersects-only versus surrounding-parcel final target query behavior.
- [x] Show focused Topology Review and Attribute Review areas with validated rule/check status so the examiner can make the final decision.
- [x] Reuse and extend existing settings for Legal and Fiscal/Cadastral final cadastre targets, including field mappings, layer aliases, query keys, and write strategies.
- [x] Load transaction-scoped Enterprise working_review context and map review context for the active case.
- [x] Implement selected-target candidate lookup for Legal or Fiscal/Cadastral final layers using configured query adapters, canonical PID rules, and spatial overlap checks.
- [x] Implement topology and cadastral consistency review orchestration behind testable seams.
- [x] Add the guided review/decision UX based on the Sally mockup artifact.
- [x] Persist draft review, final promotion decision, topology/check result, and final promotion summary artifacts.
- [x] Implement final write readiness service and guarded confirmation workflow.
- [x] Implement executable decision-specific final action handlers for discard working and send back.
- [x] Add visible but non-executable `replace_existing` and `merge_update_attributes_only` options that display `To be implemented` when clicked.
- [x] Update working_review disposition/case-index metadata after terminal actions.
- [x] Integrate promotion success/failure with existing Innola transaction completion readiness.
- [x] Attach the final promotion summary artifact back to the Innola transaction as a supporting document.
- [x] Add focused tests in the existing executable test harness; do not introduce xUnit, NUnit, or a new test runner.

## Dev Notes

### Existing Patterns To Reuse

- `TransactionPanelState.cs` for stage state, active transaction gating, user-facing workflow messages, and command enablement patterns.
- `Innola/ParcelWorkflowStageRouter.cs` for task/stage routing decisions.
- `Innola/InnolaSpatialUnitService.cs` and related transaction detail models for spatial unit and `examinationNumber` resolution.
- `Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`, `JsonEnterpriseWorkingDispositionService.cs`, and `JsonEnterpriseWorkingStateRestoreService.cs` for working_review state, disposition, and restore patterns.
- Compare decision persistence and readiness patterns from story 8.5 and commit readiness patterns from story 8.6.
- Parcel Search Legal/Fiscal layer configuration and search adapter patterns from story 8.7. In user-facing labels, Fiscal should appear as `Cadastral`; internally it maps to `Fiscal_Cadastre`.
- `ArcGisSpatialOverlapReviewService` or adjacent geometry/map review services where already available for spatial review patterns.
- Portal/auth services introduced for Enterprise operations in Epic 7.
- `InnolaTransactionLifecycleCoordinator` and `DefaultTransactionCompletionReadinessService` for completion gating.

### Required Artifacts

Use existing case-folder conventions and add these artifacts, or equivalent names if the local artifact service already defines a stricter naming pattern:

- `working/fabric_maintenance_review_draft.json`
- `working/fabric_maintenance_topology_review.json`
- `working/fabric_maintenance_promotion_decision.json`
- `working/final_cadastre_promotion_summary.json`

Artifact payloads must include enough data to support resume, audit, and manual recovery. Do not rely only on UI state.

### Decision Values

Use stable serialized enum/string values:

- `replace_existing`
- `keep_existing_discard_working`
- `merge_update_attributes_only`
- `send_back_for_review`

Use stable target values:

- `legal`
- `fiscal`

The user-facing label for `fiscal` is `Cadastral`.

### Routing And Candidate Keys

- Innola subworkflow: `Parcel Fabric Maintenance`
- Innola stage: `In Parcel Fabric Update`
- UI context labels:
  - `Current Transaction`: active Innola task transaction number.
  - `Parcel in Review`: PE number from `SpatialUnitExt.examinationNumber`; editable when missing.
- Working-review lookup for this stage: `working_review.transactionNumber = [Parcel in Review]`.
- `Load Parcel` must run the working-review lookup first, then use the loaded working parcel geometry for selected final target spatial query.
- Final target selection must be radio-button based: Legal or Cadastral, exactly one.
- Final target spatial query must reuse the existing Compare Neighbor Search spatial relation configuration for intersects-only versus surrounding-parcel review context.
- Fiscal/Cadastral target: `Fiscal_Cadastre` > configured `Parcel` layer; canonical key is `PID` with `parcel_status = active`.
- Legal target: `Legal_Cadastre` > configured `Legal_Parcel` layer; canonical key is `PID`.
- If PID lookup and spatial overlap produce multiple candidates, the examiner must select the intended candidate before any implemented terminal action can proceed.
- For this story, a user who can access the transaction can perform the final write; no additional Innola role or ArcGIS Portal permission gate is required beyond existing access/authentication.

### Patch Scope: Revised Review Loading Flow

The next patch for this story must adjust the current Fabric Maintenance workspace flow:

- Preserve the existing final-write guardrails, but treat this patch as review/evidence UX first, not final authoritative write expansion.
- Replace the target action buttons with `Legal` and `Cadastral` radio buttons.
- Remove the generic `Refresh Review` button for now.
- Display `Current Transaction` as read-only.
- Display `Parcel in Review` instead of `PE Number`; populate it from `SpatialUnitExt.examinationNumber` when available.
- If `SpatialUnitExt.examinationNumber` is missing, leave `Parcel in Review` blank and editable.
- Add a `Load Parcel` action.
- On `Load Parcel`, validate that a target radio option is selected and `Parcel in Review` is not blank.
- Query working-review geometry using `working_review.transactionNumber = [Parcel in Review]`.
- Load the working-review parcel into the active ArcGIS Pro map.
- Query the selected final target layer using the working parcel geometry and the configured Compare Neighbor Search spatial relation mode.
- Load the selected final target parcels into the active ArcGIS Pro map.
- Apply transparent symbology to both contexts so overlap and surrounding relationships are easy to inspect.
- Populate a compact results grid with one row for Working Review and one row for the selected final target, showing source, query key, count, spatial relation mode, and status.
- Show validated topology rules and attribute review checks as evidence. Do not use these checks to make the decision automatically.

### Not Implemented Yet

These options must appear in the screen but must not execute final-layer edits in this story:

- `replace_existing`
- `merge_update_attributes_only`

When either option is clicked, display a popup message with exactly:

```text
To be implemented
```

Do not persist either option as an executable final decision and do not enable final write for either option.

### UX Direction

Follow `_bmad-output/ux-artifacts/fabric-maintenance-promotion-stage-mockup.html` as the stage-level design reference:

- Screen 1: `Review And Decide`
- Screen 2: `Final Layer Write`
- Required context fields: current transaction, Parcel in Review, spatial unit, selected target.
- Required context fields after patch: `Current Transaction`, `Parcel in Review`, and selected target.
- Required review areas after patch: topology review, attribute review, final-layer candidate/conflict review, decision and notes.
- Target selection must be radio buttons, not command buttons.
- The review load command should be labeled `Load Parcel`.
- The workspace must show a compact results grid for working-review and final-target query counts/status.
- ArcGIS Pro standard tools should be available through launch/select/open actions. The add-in should not attempt to duplicate the full ArcGIS Pro topology or editing experience in WPF.
- Both working-review and selected final-target layers/features should be visible in the map with transparent symbology for visual comparison.
- Final write must feel deliberately separate from review approval, with an explicit confirmation modal or equivalent high-friction confirmation.
- The final promotion summary attachment status must be visible in the completion/readiness area.

### Final Write Guardrails

- Never write to Legal and Fiscal/Cadastral in the same promotion run.
- Never execute `replace_existing` or `merge_update_attributes_only` in this story; both are visible future options only.
- Never write final cadastre data from stale working_review state without revalidation.
- Never complete the Innola transaction before a terminal promotion summary exists.
- Never complete the Innola transaction before the terminal promotion summary has also been attached to the Innola transaction.
- Never treat Enterprise working layers as final authoritative layers.
- Never hardcode target services, field names, field aliases, or layer IDs in implementation code.
- Any ArcGIS SDK map/layer work must remain behind seams and must use `QueuedTask` where ArcGIS Pro requires it.
- If rollback is not implemented, persist pre-write target feature evidence and make recovery expectations explicit in diagnostics. Do not claim automated rollback behavior unless it is actually implemented and tested.

### Recommended Topology Review Approach

Use a hybrid approach:

- The add-in runs deterministic preflight checks it can own reliably: target selection, scoped record lookup, geometry presence, spatial reference, configured field consistency, overlap/boundary/area tolerance checks where existing geometry services support them, stale state, and write readiness.
- The working-review parcel is found by `working_review.transactionNumber = [Parcel in Review]`. The active Innola transaction remains the workflow/control transaction and should not be used as the working-review parcel query key for this stage.
- The selected final target query is spatial: working-review parcel geometry against Legal or Cadastral parcels, using the Compare Neighbor Search spatial relation setting.
- ArcGIS Pro standard tools remain the visual and technical review surface for deeper topology inspection. Provide buttons/actions to select features, open attribute tables, run or open configured topology/geoprocessing tools, and refresh add-in check results after examiner review.
- The add-in records the examiner's topology decision and supporting notes as part of the promotion decision artifact.
- The add-in should present validated topology and attribute rules as evidence for the examiner; it must not automatically choose `replace_existing`, `keep_existing_discard_working`, `merge_update_attributes_only`, or `send_back_for_review`.
- For the next patch, do not expand authoritative final-layer write behavior. Keep the existing final-write guardrails intact and focus implementation on validation evidence, map context, results visibility, and examiner decision support.

## Project Structure Notes

Likely files to inspect or update:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaSpatialUnitService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingLayerPublishService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingDispositionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/JsonEnterpriseWorkingStateRestoreService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelSearch/`
- New story-scoped services under a folder such as `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/FabricMaintenance/`
- Focused tests under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/FabricMaintenance/` or the nearest existing workflow test folder.

## References

- `_bmad-output/project-context.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md`
- `_bmad-output/implementation-artifacts/7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md`
- `_bmad-output/implementation-artifacts/8-5-persist-compare-decision-and-unlock-commit-stage.md`
- `_bmad-output/implementation-artifacts/8-6-wire-commit-stage-readiness-to-compare-approval-and-authoritative-promotion.md`
- `_bmad-output/implementation-artifacts/8-7-add-parcel-search-dockpane-tab.md`
- `_bmad-output/ux-artifacts/fabric-maintenance-promotion-stage-mockup.html`

## Testing Notes

- Run `dotnet build` for the add-in solution/project after implementation.
- Run the existing executable test harness with focused Fabric Maintenance tests registered in `Program.cs`.
- Do not add xUnit, NUnit, or a separate test framework.
- If full harness execution reaches tests that require ArcGIS Pro runtime assemblies unavailable outside ArcGIS Pro, document the boundary and ensure story-specific tests have already run.

## Resolved Decisions

- Route this story from Innola subworkflow `Parcel Fabric Maintenance` and stage `In Parcel Fabric Update`.
- For `Fiscal_Cadastre`, query the configured `Parcel` layer by `PID` and require `parcel_status = active`.
- For `Legal_Cadastre`, query the configured `Legal_Parcel` layer by `PID`.
- `replace_existing` remains visible but is not implemented in this story; clicking it shows `To be implemented`.
- `merge_update_attributes_only` remains visible but is not implemented in this story; clicking it shows `To be implemented`.
- A user who can access the Innola transaction can perform final writes for this story.
- The final promotion summary must be attached back to the Innola transaction as a supporting document.
- Fabric Maintenance review loading uses Parcel in Review as the working-review query key: `working_review.transactionNumber = [Parcel in Review]`.
- The final target query is spatial from the loaded working-review parcel into the selected Legal or Cadastral layer, using the existing Compare Neighbor Search spatial relation setting.
- The next implementation patch should be review/evidence UX first. Preserve existing final-write guardrails and do not expand authoritative final-layer write behavior in that patch.

## Open Questions

- Confirm whether a future story should implement `replace_existing` as update-in-place, retire/deactivate-and-replace, or delete-and-reinsert.
- Confirm which final-layer attributes a future story should allow for `merge_update_attributes_only`.

## Dev Agent Record

### Partial Implementation Note

Story 7.15 is intentionally marked `partially-implemented` as of 2026-08-31. The implemented patch covers routing, context launch, editable Parcel in Review, target selection, review/evidence UX, map layer loading with transparency, spatial-overlap-first candidate discovery, candidate grid population, zoom-to-working-parcel, Cancel cleanup, guarded decision capture, summary artifact generation, and summary attachment flow. The story must be revisited before final closure for live ArcGIS Pro smoke validation of Legal/Cadastral candidate discovery and for any authoritative final-write expansion beyond the currently guarded/non-implemented actions.

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed 12 focused Fabric Maintenance tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed with 0 warnings and 0 errors.
- Full executable harness passed through the new Fabric Maintenance tests and broad regression coverage, then stopped later on an unrelated temp restore access error in `InnolaTransactionLoadServiceTests.ResumePackageRestoresSavedWorkflowState`: access denied to `%TEMP%\sidwell-resume-restore-...\case`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed 13 focused Fabric Maintenance tests after adding duplicate transaction-row coverage.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed with 0 warnings and 0 errors after duplicate transaction-row fix.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "transaction panel load selected transaction preserves duplicate task row" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed after the Load Transaction row-identity fix.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "transaction panel fabric maintenance start uses selected duplicate transaction task" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed after the Load Transaction row-identity fix.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed 13 focused Fabric Maintenance tests after the Load Transaction row-identity fix.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\` passed with 0 warnings and 0 errors after the Load Transaction row-identity fix.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance routing gate requires configured subworkflow and stage" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed after allowing exact-stage Fabric rows when Innola omits subworkflow metadata.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "transaction panel fabric maintenance start uses selected duplicate transaction task" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with duplicate transaction rows and no explicit subworkflow metadata on the selected Fabric row.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed 13 focused Fabric Maintenance tests after the live-list metadata fallback.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after the live-list metadata fallback.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.318`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance missing pe number keeps workspace editable" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed after adding manual PE entry support.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "transaction panel fabric maintenance start opens editable pe when missing" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed after allowing Fabric Maintenance to open when `SpatialUnitExt.examinationNumber` is missing.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed 15 focused Fabric Maintenance tests after manual PE entry support.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after manual PE entry support.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.320`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed 17 focused Fabric Maintenance and related transaction-panel tests after the review/evidence UX patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after the review/evidence UX patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed broad regression coverage until an outside-ArcGIS runtime boundary stopped at `SpatialOverlapReviewPersistenceServiceTests.OverlapReviewServiceBlocksWhenNoTargetsAreConfigured`: missing `ArcGIS.Desktop.Mapping, Version=13.6.0.0`.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.322`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed 19 focused Fabric Maintenance and related transaction-panel tests after adding Cancel cleanup, Load Parcel lockout, and selected-decision feedback.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after the Fabric Maintenance improvement patch.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.324`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed broad regression coverage until the existing outside-ArcGIS runtime boundary stopped at `SpatialOverlapReviewPersistenceServiceTests.OverlapReviewServiceBlocksWhenNoTargetsAreConfigured`: missing `ArcGIS.Desktop.Mapping, Version=13.6.0.0`.
- Inspected ArcGIS Pro dump `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_08_31_2026_00_15_54.dmp`; readable dump strings showed `ArcGisFabricMaintenanceReviewLoadService.QueryCandidateObjectIds()` / `LoadFinalTargetContext()` failing during `BasicFeatureLayer.Search` / `Table_Search` with `ArcGIS.Core.Data.Exceptions.GeodatabaseGeneralException` and a COM exception marker.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed 20 focused Fabric Maintenance and related transaction-panel tests after adding recoverable ArcGIS geodatabase/COM exception handling.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after the dump-driven crash guard patch.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.326`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.
- Repo review found the working parcel recovery by `Parcel in Review` was implemented, but final Legal/Cadastral overlap evidence was only counted and not exposed as candidate identity/relationship/overlap data; Fabric Maintenance map load also did not zoom to the recovered working parcel.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "fabric maintenance" -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed 20 focused Fabric Maintenance and related transaction-panel tests after adding final candidate detail flow and zoom-to-working-parcel behavior.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after the overlap evidence/zoom patch.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.328`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build -p:BaseIntermediateOutputPath=.artifacts\msbuild-obj\ -p:BaseOutputPath=.artifacts\msbuild-bin\ -p:UseSharedCompilation=false` passed broad regression coverage until the existing outside-ArcGIS runtime boundary stopped at `SpatialOverlapReviewPersistenceServiceTests.OverlapReviewServiceBlocksWhenNoTargetsAreConfigured`: missing `ArcGIS.Desktop.Mapping, Version=13.6.0.0`.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -c Release -- "fabric maintenance"` passed 20 focused Fabric Maintenance and related transaction-panel tests after changing final candidates to spatial-overlap-first discovery.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj -c Release` passed with 0 warnings and 0 errors after the final-candidate query fix.
- `tools/package_addin.ps1 -Configuration Release` produced `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Release\net8.0-windows\ParcelWorkflowAddIn.esriAddInX` and bumped add-in patch version to `1.1.330`; auto-register did not run because `RegisterAddIn.exe` was not on PATH.

### Completion Notes

- Added configurable Fabric Maintenance promotion settings for `Parcel Fabric Maintenance` / `In Parcel Fabric Update`, reusing Enterprise working-review and Legal/Fiscal cadastre settings.
- Added routing and transaction-panel launch support so started Fabric Maintenance rows open a dedicated workspace after resolving `SpatialUnitExt.examinationNumber`.
- Added Fabric Maintenance review planning, final-target query planning, topology/check readiness gating, decision selection, final action result handling, and completion readiness services.
- Added the guided Fabric Maintenance workspace with `Review And Decide` and `Final Layer Write` screens, Legal/Cadastral target choice, standard ArcGIS Pro review action prompts, decision notes, approval, and final confirmation.
- Kept `replace_existing` and `merge_update_attributes_only` visible but non-executable; both surface exactly `To be implemented`.
- Added draft, topology, decision, final promotion summary, and working-review disposition JSON artifact persistence.
- Added a real Innola attachment wrapper for `final_cadastre_promotion_summary.json` using source type `st_fabric_promotion_summary`.
- Fixed duplicate transaction-row handling so Fabric Maintenance launch binds to the exact selected active task id, not the first row with the same transaction number.
- Fixed Load Transaction duplicate-row handling so clearing the search text preserves the exact selected task row when multiple Innola rows share the same transaction number.
- Relaxed Fabric Maintenance list-row routing so exact `In Parcel Fabric Update` rows can start when Innola omits explicit subworkflow metadata from the task list; explicit wrong subworkflow values still block.
- Changed missing `SpatialUnitExt.examinationNumber` handling so Fabric Maintenance opens with a blank editable PE Number field and a manual-entry status instead of blocking the workspace.
- Story updated for the next patch: target selection changes to Legal/Cadastral radio buttons, `PE Number` label changes to `Parcel in Review`, `Load Parcel` drives working-review and final-layer loading, both contexts load into the map with transparency, and the workspace shows compact result counts plus topology/attribute rule evidence.
- Added Amelia implementation note: preserve existing final-write guardrails and treat the next patch as review/evidence UX first, not final authoritative write expansion.
- Implemented the review/evidence UX patch: `Parcel in Review` now drives the working_review query, target selection is radio-button based, `Load Parcel` plans and executes review loading, and the workspace shows compact working/final result rows plus topology and attribute evidence.
- Added an ArcGIS-backed Fabric Maintenance review loader that loads working-review and selected final target context into the active map with transparent review layers while keeping final-layer writes untouched.
- Preserved final-write guardrails and kept `replace_existing` / `merge_update_attributes_only` visible but non-executable.
- Added a Fabric Maintenance Cancel command that cleans the transaction-scoped review group layer from the active map and then closes the workspace.
- Disabled `Load Parcel` after a successful load to prevent duplicate layer/query execution; changing `Parcel in Review` or target selection invalidates the loaded context and re-enables the command.
- Added explicit selected-decision feedback in the Decision panel so the examiner can see the active implemented decision before approval.
- Added dump-driven crash protection for final-target spatial candidate search: recoverable ArcGIS geodatabase and COM failures now become warning/status output instead of escaping the async WPF command.
- Added final Legal/Cadastral overlap candidate evidence: object id, global id, parcel id, PID, spatial relationship, overlap area, and overlap percentage flow from the ArcGIS spatial query into a selectable `Final Candidates` grid.
- Added automatic zoom to the loaded working-review parcel layer after `Load Parcel`; status now distinguishes loaded-and-zoomed from loaded-with-zoom-warning.
- Fixed Fabric Maintenance final-candidate discovery to query Legal/Cadastral parcels by spatial overlap first (`1=1`) and keep configured PID/status SQL as review evidence, so visible overlaps are not hidden by identity/status filters.
- Fixed Cadastral candidate identity mapping to use the configured Fiscal key (`Lv_number`) instead of assuming a `PID` field exists on the Fiscal layer.
- Root-cause note from map review: the full `Fiscal_Cadastre` reference layer could show overlaps while the generated `Cadastral candidates` layer stayed empty because the candidate layer is definition-filtered to the object ids returned by the add-in query. The previous query could return zero when the hardcoded Fiscal `PID`/`parcel_status` evidence filter did not match the live layer schema/value set.

### File List

- `_bmad-output/implementation-artifacts/7-15-add-fabric-maintenance-promotion-review-from-working-review-to-final-cadastre.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/FabricMaintenancePromotionWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/FabricMaintenancePromotionWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ComputeAttachmentSourceTypeCatalog.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ParcelWorkflowStageRouter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/FabricMaintenance/FabricMaintenancePromotionServices.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/FabricMaintenance/FabricMaintenancePromotionViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/FabricMaintenance/FabricMaintenancePromotionTests.cs`

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-08-31 | 1.12 | Marked story as partially implemented pending return work for live ArcGIS Pro validation and final-write expansion decisions. | Amelia |
| 2026-08-31 | 1.11 | Fixed Fabric Maintenance final target candidate discovery to use spatial overlap first and configured Fiscal identity fields, preserving status/PID checks as evidence instead of suppressing visible overlaps. | Amelia |
| 2026-08-31 | 1.10 | Added final Legal/Cadastral overlap candidate details and zoom-to-working-parcel behavior for Fabric Maintenance Load Parcel. | Amelia |
| 2026-08-31 | 1.9 | Added dump-driven Fabric Maintenance crash guard for ArcGIS final-target spatial query failures. | Amelia |
| 2026-08-31 | 1.8 | Added Fabric Maintenance Cancel cleanup, Load Parcel duplicate-run guard, and selected-decision feedback. | Amelia |
| 2026-08-31 | 1.7 | Patched Fabric Maintenance review loading UX: Parcel in Review query key, Legal/Cadastral radio targets, Load Parcel map context with transparency, compact results grid, and topology/attribute evidence. | Amelia |
| 2026-08-31 | 1.6 | Added Amelia handoff note to keep next patch focused on validation evidence UX and preserve final-write guardrails. | Mary |
| 2026-08-31 | 1.5 | Revised requested Fabric Maintenance review flow: radio target selection, Parcel in Review query key, Load Parcel map/results behavior, transparency, and topology/attribute evidence display. | Mary |
| 2026-08-31 | 1.4 | Opened Fabric Maintenance with editable PE Number when SpatialUnit examination number is missing. | Amelia |
| 2026-08-31 | 1.3 | Allowed exact Fabric Maintenance stage rows to route when live Innola task-list payload omits subworkflow metadata; explicit wrong subworkflow still blocks. | Amelia |
| 2026-08-31 | 1.1 | Adjusted Fabric Maintenance launch for duplicate transaction rows and added regression coverage matching the PLA_B active-task pattern. | Codex |
| 2026-08-31 | 1.2 | Preserved exact Fabric Maintenance task-row selection when Load Transaction clears search text for duplicate transaction numbers. | Codex |
| 2026-08-31 | 1.0 | Implemented Fabric Maintenance promotion routing, review/decision workspace, guarded decision services, JSON artifacts, summary attachment service, and focused tests. | Codex |
| 2026-08-30 | 0.2 | Renamed to Fabric Maintenance, resolved routing/candidate-key questions, and marked TBD actions as visible but not implemented. | Codex |
| 2026-08-30 | 0.1 | Initial ready-for-dev story for Fabric Maintenance promotion review and final cadastre write. | Codex |
