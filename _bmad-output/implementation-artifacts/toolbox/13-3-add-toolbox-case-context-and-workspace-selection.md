---
story_key: 13-3-add-toolbox-case-context-and-workspace-selection
track: toolbox
story_type: code
created: 2026-10-08
depends_on: [13-1-define-toolbox-process-and-task-matrix, 13-2-define-toolbox-architecture-and-parallel-migration-plan]
execution_gate: isolated-local-context-only
baseline_branch: wipRC9
baseline_commit: 5908f30617cdc49e09873635a30343da8aff9ad6
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.3 - Add Toolbox Case Context And Workspace Selection

Status: done

## Story

As an examiner,
I want a shared transaction-case context and independently selectable working preset,
so that tools can support local preparation without confusing my workspace with the official Innola task.

## Scope And Execution Gates

First code slice of migration M1. Prepare and implement in isolation as explicitly authorized by JotaPe on 2026-10-08. Use a dedicated worktree/branch; do not switch the operational checkout. Tests use synthetic context and a separate temporary case root. Do not install/register/package the experimental add-in, read operational case contents, load operational mutation credentials or call live services.

Deliver pure context, selection service and bindable selector state for later host integration. No new ribbon/window launch or change to current transaction routing is required in this foundation story. A test exercising load/select/refresh/clear is the integration boundary; do not claim a deployed examiner UI.

Exclude geometry creation/editing, FGDB migration, persistent context files, canonical parcel schema, tool registry/execution, publication, ZIP/restore and Innola lifecycle/writeback. Those remain 13.4-13.10. D02/D03 are not resolved by a client-side permission boolean. All remote actions are denied in this slice.

## Acceptance Criteria

1. Given the operational checkout, when implementation starts, then a separate worktree/branch is pinned to the recorded baseline; source changes/build outputs remain there, synthetic cases use a separate root and no operational add-in installation/configuration/data is changed.
2. Given a selected transaction and explicit environment/operator/case/root, when context is created, then it retains original and normalized TR, TransactionId, case ID/root, source-reference slots, selected preset, observed actual task/name/process/assignment and observation time/provenance, selected parcel IDs and optional current/accepted revision references. No credentials are held. Missing identities or unsafe/out-of-test-root paths are rejected without creating folders.
3. Given any transaction type or parcel count, when the examiner selects Compute, Compare, PLA_A, PLA_B or RT, then only local preset changes. Actual task ID/name/process/assignment and case/source/revision identities remain unchanged; no claim, transition, remote lookup or legacy WorkflowState mutation occurs. Invalid enum values and selection without a loaded context fail safely.
4. Given the existing SelectedInnolaTransaction mapper cannot prove TaskId provenance, when adapted, then actual task is unverified, even when TaskId is nonempty or equals TransactionId. Unknown task metadata does not prevent local inspection/selection. Display names, transaction types, cached observations and selected presets never authorize remote effects.
5. Given Compare or a read-only source reference, when local geometry eligibility is queried, then geometry editing is denied. Compute eligibility additionally requires an explicit local-edit permission; default is denied. Selected Compute with actual Compare does not imply a backward server transition or resolved correction authority. Other presets do not acquire geometry editing implicitly in this slice.
6. Given single or multiple selected parcel IDs, when selection changes, then all IDs are retained, validated, deduplicated case-insensitively and defensively copied. No first-parcel-only behavior, acceptance inference, included-set exclusion or parcel schema duplication occurs. Empty selection is allowed for document/case-level preparation, not handoff acceptance.
7. Given context-bound selection state, when load, preset selection, task refresh or clear occurs, then consumers receive property notifications/current projections for original TR, actual task and selected workspace. Refresh is explicitly supplied evidence for the same transaction and preserves preset/parcel selections. Wrong transaction refresh fails without replacing the context. Replacing/clearing context cannot retain old parcels/permissions/revisions.
8. Given any context state, when handoff/publish/upload/task transition eligibility is queried, then it is explicitly blocked with a safe reason. No HTTP, GIS operation, persistence or session mutation dependency exists in the new selector services.
9. Given the implementation, when validated, then the existing executable test harness covers AC2-8 plus unchanged legacy stage-planner behavior. Build and focused/full-suite results are recorded separately; host/deployment smoke is not claimed. Existing suite/environment failures must be distinguished from regressions before review status.

## Tasks / Subtasks

- [x] Establish isolated worktree, baseline and test-only case-root evidence. (AC: 1)
- [x] Add failing context/selection/permission/refresh tests to the existing executable harness; confirm red phase. (AC: 2-9)
- [x] Implement immutable context and conservative selected-transaction adapter with validated root/identity and defensive collections. (AC: 2, 4, 6)
- [x] Implement independent preset selection and bindable load/refresh/clear state, preserving actual-task identity. (AC: 3, 7)
- [x] Implement explicit local geometry eligibility and deny all remote actions. (AC: 5, 8)
- [x] Build, run focused and full regression checks, inspect changes and record evidence/remaining limitations. (AC: 1, 9)

### Review Findings

- [x] [Review][Patch] Valid descendants of a drive-root trusted path were rejected by a doubled-separator prefix. Fixed prefix construction and explicit same-root rejection; drive-root/UNC-root regression coverage passes. Confirmed by Blind Hunter and Edge Case Hunter.
- [x] [Review][Patch] Same-case refresh compared untrimmed environment/TransactionId against constructor-normalized identities. Fixed using the same required-identity helper before comparison; blank/mismatched/stale refresh still fails safely. Confirmed by Blind Hunter and Edge Case Hunter.
- [x] [Review][Defer] Full harness contains pre-existing Compare XAML assertion and filesystem replacement failures; reproduced on baseline 5908f30. Keep separate from 13.3 regression results; do not alter operational UI under this story.
- [x] [Review][Dismiss] Junction containment is not claimed by this in-memory, no-write slice; reparse-point/canonical filesystem checks belong to 13.5 persistence. Acceptance Auditor found no additional local-scope violations.

## Dev Notes

Use existing C# net8.0-windows/x64 and WPF binding patterns. No library/SDK/framework upgrade or new dependency. Prefer small records/services under `Workflow/Toolbox/`. Keep server identity observations separate from local selection; do not add a second global singleton or change InnolaSessionManager.SelectedTransaction.

Context schema is an in-memory v1 contract only here; optional revision/source fields are references, not evidence acceptance. No serializer/persistence cutover. Source PE references retain their own identities and are read-only. Resolve a case root through an explicitly supplied trusted experimental root; reject traversal/outside paths and do not use the legacy path combinator on unvalidated input. This slice does not claim symlink-proof authoritative filesystem writes because it performs no writes.

### Existing Boundaries And Planned Files

| Boundary | Current behavior | Change in this story | Preserve |
|---|---|---|---|
| Innola/SelectedInnolaTransaction.cs | Immutable task/transaction display record; no provenance/definition ID | Read-only adapter into new context, do not edit record | Existing transaction/session consumers |
| Workflow/WorkflowWorkspacePlanner.cs | WorkflowState routes intake/review/validation/output, plus PLA evidence selection | No change; separate Toolbox selector | Every legacy routing result |
| CaseFolders/CaseFolderLayout.cs | Builds source/working/output under supplied root; For combines transaction string | Read layout reference only after independent validation; no change | Existing case naming/store/restore |
| Tests/Program.cs | Manually registered executable tests; optional name-substring filters | Add Toolbox test registrations only in isolated checkout | Existing registrations/filter behavior |

New production types: context/task observation, preset enum, selector state and local access result. Tests belong in `ParcelWorkflowAddIn.Tests/Workflow/ToolboxContextTests.cs`. Read full UPDATE targets before editing; Program.cs is the only planned existing UPDATE file. If host integration is added, refine scope and inspect its complete owner first instead of silently changing the large transaction/view-model routing surface.

### Test Matrix

Synthetic one/three-parcel contexts; all five presets independent of transaction type; unverified/fallback/empty task ID; Compare read-only; Compute default-denied/explicit local permission; source reference read-only; invalid preset; blank identity; rooted/traversal/outside case path; case-insensitive duplicate IDs; caller-array mutation; same-case task refresh; wrong-case refresh; replacement and clear; notification sequence; remote deny in every preset. Include direct legacy planner regressions.

Run `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` and `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build` from the isolated worktree. Use test-name filters for targeted runs. Do not invoke release packaging/deployment scripts. Full harness may depend on ArcGIS-hosted runtime; report actual results and baseline comparison, not an inferred pass.

### References

- [Accepted process](../../planning-artifacts/toolbox/toolbox-process-definition.md)
- [Architecture](../../planning-artifacts/toolbox/toolbox-architecture.md#actual-innola-evidence-and-mapping)
- [Context contract](../../planning-artifacts/toolbox/toolbox-contract-catalog.md#context-v1)
- [Migration M1 and isolation](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md)
- [Acceptance S05/S12 and F04/F07](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md)
- [Decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md)
- [Story 13.2](13-2-define-toolbox-architecture-and-parallel-migration-plan.md)
- [Epic 13](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md)
- [Project context](../../project-context.md)
- [Story 11.2 contract-only boundary](../11-2-unified-pdf-extraction-contract-for-plan-examination.md)

## Dev Agent Record

### Agent Model Used

Codex. BMAD create-story prepared the specification; dev-story executes the isolated local slice.

### Debug Log References

Preparation inspected accepted 13.1/13.2 artifacts, baseline HEAD/history, selected transaction record, case layout, stage planner and test harness. No new external API/SDK behavior is introduced; retain the inspected project toolchain.

Implementation worktree: `D:/Code/BMad-Method/dev/pe-jamaica/temp/toolbox-worktree`, branch `refactor/toolbox-case-workspace`, baseline 5908f30617cdc49e09873635a30343da8aff9ad6. Authorized Toolbox documents were copied explicitly because they were untracked in the operational checkout. The parent operational `src` and operational sprint tracker remain unchanged (`git diff --exit-code` passed).

Red phase: build failed with four expected missing Toolbox-type errors before production types existed. Green phase: normal solution build passed with 0 warnings/0 errors. Later incremental/rebuild attempts encountered missing generated WPF .baml/.g.cs files; serialized `dotnet build ... /p:UseSharedCompilation=false /m:1` succeeded with 0 warnings/0 errors. No toolchain/settings changes were made to work around this.

Final focused run: `dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll toolbox "workspace planner"` passed 20 registered tests (six Toolbox groups, fourteen unchanged legacy planner tests), including the final added stale-refresh/scope tests.

Full harness was attempted repeatedly. Default temp-root runs encountered UnauthorizedAccessException in the unchanged ManifestSerializer.File.Move replacement, including on a clean detached baseline checkout. With process-only TEMP/TMP redirected to the isolated worktree's temp directory, execution proceeded to the existing CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands assertion: "Compare should show an indeterminate footer progress indicator while Finalize is saving." A targeted run on untouched baseline 5908f30 reproduced the exact assertion. No unrelated Compare or manifest code was altered. Full-suite validation remains incomplete; the final task stays unchecked and status stays in-progress rather than asserting all tests pass.

Baseline verification checkout: `temp/tb-base` (detached HEAD), source clean. An initial longer baseline path failed Git checkout on a tracked generated-artifact filename length; the shorter checkout completed. No production package, installation, geometry/filesystem case operation or live API mutation was performed by the new services. Existing harness uses synthetic/mock tests; runtime host/deployment smoke was not run.

### Completion Notes List

- JotaPe accepted architecture and explicitly requested preparation plus implementation in isolation on 2026-10-08.
- Specification prepared with no remote effects or operational route changes; implementation tasks initially unchecked.
- Implemented immutable case/task/source context, defensive multi-parcel selection, independent preset changes and observable load/refresh/clear state only in the isolated worktree.
- Task adaptation is always unverified; no inferred authority from cached IDs/display aliases. Refresh rejects wrong identity/stale observation and clears old local-edit permission. Compute needs explicit local eligibility; Compare and source-reference geometry remain read-only; every remote action is denied.
- Initial build and 20 focused tests passed; reproduced baseline test/environment failures kept final validation open pending code review and disposition. This initial result did not authorize merge, installation or storage migration.
- Code changes are uncommitted in the dedicated worktree. No operational branch switch or merge occurred. This is bindable foundation state, not a registered/deployed Toolbox screen.
- 2026-10-08 review closure: JotaPe requested review/finish and selected "Apply all fixes and finish 13.3". Three independent review layers completed: two confirmed local bugs fixed, no additional acceptance findings; junction containment dismissed for this explicitly no-write scope and retained as a persistence obligation. Both regression tests failed before fixes and pass afterward. Acceptance Auditor rechecked the corrected code with no remaining local findings.
- Final serialized solution build passed with 0 warnings/0 errors. Focused run passed 22 registered tests: eight Toolbox groups plus fourteen unchanged legacy planner tests. Full harness was rerun with isolated TEMP/TMP and again stopped at the reproduced pre-existing Compare lifecycle-XAML assertion. This is an explicitly deferred baseline failure, not a Toolbox regression or claimed full-suite pass. Tests after that fail-fast point remain unexecuted by the full run.
- All local story ACs were reviewed and the final validation/evidence task is closed under the code-review workflow's pre-existing-issue deferral rule. Story 13.3 is done for local context/selection only. Full-suite green remains a baseline/release gate; hosted UI, live TR testing, FGDB creation/migration and remote authority remain outside scope. TR-prefixed FGDB naming remains future 13.5 work.

### File List

- `_bmad-output/implementation-artifacts/toolbox/13-3-add-toolbox-case-context-and-workspace-selection.md`
- `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml`
- `_bmad-output/planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/ToolboxCaseContext.cs` (isolated worktree only)
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/ToolboxWorkspaceSelection.cs` (isolated worktree only)
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxContextTests.cs` (isolated worktree only)
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs` (isolated worktree only)
- `_bmad-output/implementation-artifacts/toolbox/deferred-work.md`

## Senior Developer Review

Date: 2026-10-08
Outcome: approved for the local context-only Story 13.3; baseline validation limitations explicitly deferred.

Review target: four code files in the isolated worktree, including untracked new files. Diff-only Blind Hunter, source-aware Edge Case Hunter and spec-aware Acceptance Auditor ran separately. Two deduplicated P2 findings were confirmed and fixed. No review layer failed. No unresolved HIGH/MEDIUM issue remains in this story's scope. This is not release/deployment approval.

| AC | Evidence / disposition |
|---|---|
| 1 | Dedicated branch/worktree, original operational src/tracker/context diff unchanged; synthetic roots; no install or remote operations |
| 2 | Immutable identity/task/source fields; invalid/blank/outside/traversal/same-root rejected; drive/UNC child paths regression tested |
| 3 | Every preset preserves actual task/case/source/revision/parcel identity; invalid/no-case selection blocked |
| 4 | Adapter always unverified, including missing/fallback IDs; remote authorization never inferred |
| 5 | Compute explicit local eligibility only; other presets and source geometry denied |
| 6 | Multiple selected IDs retained/deduplicated/defensively copied; empty case scope allowed without acceptance claim |
| 7 | Load/select/refresh/clear notifications; normalized same-case refresh succeeds; wrong/stale/blank identity leaves current context unchanged and valid refresh clears old permission |
| 8 | All remote actions denied; no HTTP/GIS/persistence/session mutation dependency |
| 9 | Red/green review regressions; clean build; 22 focused passes; full harness attempted with reproduced baseline failure recorded in deferred-work.md; no host or blanket full-suite pass claimed |

## Change Log

- 2026-10-08: Prepared local context-only Story 13.3 following accepted architecture.
- 2026-10-08: Implemented local foundation in isolated worktree; build/20 focused checks pass, full-suite validation remains open due to reproduced baseline failures.
- 2026-10-08: Reviewed and fixed root-prefix and refresh-normalization bugs; build/22 focused tests pass. Closed local Story 13.3 with baseline failures explicitly deferred; no release/installation approval implied.
