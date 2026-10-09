---
story_key: 13-1-define-toolbox-process-and-task-matrix
track: toolbox
story_type: documentation
owner: Mary - Business Analysis
created: 2026-10-08
baseline_branch: wipRC9
baseline_commit: 5908f30617cdc49e09873635a30343da8aff9ad6
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.1 - Define Toolbox Process And Task Matrix

Status: done

## Story

As a cadastral examination product owner,
I want a reviewed process and tool matrix for an examiner-selected transaction toolbox,
so that existing capabilities can support automatic, assisted and manual work without fragmenting case data or disrupting the current application.

## Scope And Execution Boundary

This is the first documentation story in Epic 13. Execute with Mary; architecture decisions belong to 13.2. Creation of this story does not mean its deliverables have been produced or accepted.

Create a process definition, capability inventory, scenario walkthroughs and decision log under `_bmad-output/planning-artifacts/toolbox/`. The proposal dated 2026-10-08 is input, not an already-approved specification. Preserve the earlier untracked toolbox draft and operational planning artifacts.

No production code/configuration/schema changes, installation, packaging, service writes, operational case edits, branch switching or operational sprint transitions are in scope. Source inspection is read-only. No technical library upgrade is needed for this documentation work.

## Confirmed Business Rules

- One current-TR case/FGDB supports one or many parcels, including multi-parcel Cad Maps extracted from plans or coordinate documents.
- Examiner-selected workspace and actual Innola task are separate. Workspace selection does not imply a workflow transition or server-write permission.
- Compare geometry stays read-only; Compute performs authorized corrections. Dependent checks/approval/comparison become stale after relevant changes.
- All included parcels must be accepted before initial handoff. Exclusions require a recorded reason and cannot conceal failed included parcels.
- Original source evidence, raw candidates, reviewed values and manual overrides remain distinguishable.
- FGDB geometry plus reusable tabular evidence are preserved in the case/package. Enterprise working_review is a checkpoint projection.
- PLA_B and RT cannot overwrite originating PE geometry. RT means the existing RT Examination; there is no new RC process.

## Acceptance Criteria

1. Given the existing tools and proposed toolbox, when the process definition is written, then it describes six capability groups and five presets (Compute, Compare, PLA_A, PLA_B, RT) with distinct tool prerequisites and task acceptance gates, without requiring a separate implementation per transaction type.
2. Given Innola and application terminology currently overlap, when identities are documented, then transaction type/profile, official task definition, active task instance, selected workspace, examination step, tool and evidence state each have a definition, authority and example. Unknown/ambiguous server task identity blocks server writes/handoff, not authorized local preparation by default.
3. Given each preset, when its matrix is inspected, then each action specifies purpose, scope, inputs, editable/read-only outputs, prerequisites, required/optional status, persisted evidence, rerun/invalidation behavior, server side effects and handoff requirement. Every cell is labeled confirmed, proposed or unresolved.
4. Given single-parcel and multi-parcel sources, when scenarios are documented, then points/segments/geometry are parcel-scoped, generic evidence stays document/case-scoped, owners/neighbors retain explicit parcel links, and shared-boundary changes identify every affected parcel.
5. Given a Cad Map has several included parcels and one is blocked, when the handoff scenario is evaluated, then acceptance remains blocked; documented exclusions, shared edges, candidate splits/merges and rerun identity preservation are covered without silently dropping parcels.
6. Given automatic, assisted and manual modes, when the process is compared, then all modes use the same case/evidence model and acceptance rules. Low/zero extraction results permit a manual route; automatic preparation cannot implicitly approve, publish or advance.
7. Given an extraction rerun or correction, when the expected behavior is documented, then new candidates preserve accepted values/manual edits until explicitly replaced, and affected approvals/findings/reports/published copies/packages are marked stale by input dependencies.
8. Given a selected workspace differs from the actual Innola task, when correction/handoff scenarios are documented, then authorized local work and explicit server actions remain distinct, Compare never edits geometry, and the required return/reassignment authority is recorded as a decision rather than invented.
9. Given current package/Enterprise services, when save and handoff are defined, then local save, suspend, snapshot, report, publish, attach and transition have separate evidence and failure/retry outcomes. Restore compares revisions and detects stale local/remote cases; an Enterprise geometry-only recovery is not described as full case recovery.
10. Given existing Epic 11 and later delivery work, when the inventory is produced, then each capability references actual source/story evidence and is assigned reuse/adapt/dependency/proposed-supersession or missing capability. Operational completed/backlog statuses are not changed by this mapping.
11. Given representative workflows, when walkthroughs are complete, then they include single-parcel preparation, multi-parcel Cad Map with a shared edge and blocked parcel, manual/failed extraction, read-only Compare correction, PLA_A, PLA_B, RT, package reopen and partial handoff failure. Each identifies state/evidence before and after the action and expected next eligible action.
12. Given unresolved business/architecture inputs, when the story is submitted for review, then a decision register contains question, options, recommendation, evidence, owner, affected stories and blocking/nonblocking status. Confirmed user choices are not reopened as assumptions; no approval is fabricated. Story 13.2 receives explicit inputs and remaining decisions.

## Tasks / Subtasks

- [x] Inventory capabilities from existing services, scripts and stories with evidence references. (AC: 1, 10)
- [x] Define terminology and identity/authority table. (AC: 2, 8)
- [x] Create the five-preset task/action matrix with required evidence and permissions. (AC: 3, 9)
- [x] Define case/document/parcel scopes and all-included acceptance policy. (AC: 4, 5)
- [x] Define automatic, assisted and manual paths plus rerun/correction behavior. (AC: 6, 7)
- [x] Write the representative scenario walkthroughs and failure/reopen expectations. (AC: 5, 8, 9, 11)
- [x] Reconcile overlaps with Epic 11 and legacy backlog without retiring operational work. (AC: 10)
- [x] Record unresolved decisions and architecture handoff; review AC coverage and scope. (AC: 12)
- [x] Submit the process artifacts for product review. (AC: 1-12)
- [x] Record product acceptance of the deliverables before marking done. (AC: 1-12)

## Dev Notes

Use the existing task matrix as a starting draft, but verify individual tool boundaries in source. A source file does not prove a standalone reusable tool or successful current runtime acceptance. Separate current behavior, proposed change and open domain decisions.

Current constraints worth preserving: ArcGIS Pro-hosted C#/WPF orchestration, Python/ArcPy adapter boundaries, approved-review gates, stable case artifacts, secret redaction and recoverable side effects. Do not import obsolete starter folder names from the baseline architecture instead of inspecting the current source tree.

Current behavior evidence:

- CaseFolderLayout organizes manifest/source/working/output under the case root.
- CaseResumePackageService's full output inclusion depends on policy; no blanket guarantee that every save contains output FGDB exists.
- CompareWorkingGeometryService currently loads transaction-scoped Enterprise geometry; local-first FGDB Compare is proposed work.
- Enterprise publish has output-feature and reviewed-row fallback paths; the target exact-revision publication needs architecture analysis.
- Story 11.2 already plans single/multi-parcel canonical review contracts; it remains contract-only and must not be duplicated.
- RT Save differs from Save & Close; current Plan neighbors, checklist, chosen transition and read-only PE SpatialUnits must retain their boundaries.

### Deliverables And File Structure

New outputs:

- `_bmad-output/planning-artifacts/toolbox/toolbox-process-definition.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-capability-inventory.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-scenario-walkthroughs.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-decision-register.md`

Allowed updates: this story, its completion record, the Toolbox epic plan and the separate Toolbox tracker. Runtime files and operational tracker are read-only for this story.

### Verification And Definition Of Done

Review each AC against a deliverable section; validate links and table consistency; check that confirmed requirements remain intact and no unknown stage/API identifier is fabricated. Record unresolved decisions with owners and affected scope. Run no builds or live transactions for documentation-only work. Status moves to review when deliverables exist; done requires recorded product acceptance, not story-file creation.

### References

- [Epic 13](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md)
- [Current proposal](../../planning-artifacts/toolbox-case-workspace-and-task-matrix-2026-10-08.md)
- [Earlier proposal](../../planning-artifacts/toolbox-transaction-stage-architecture.md)
- [Project context](../../project-context.md)
- [Baseline architecture](../../planning-artifacts/architecture.md)
- [Epic 11 inventory](../../planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md)
- [Story 11.2](../11-2-unified-pdf-extraction-contract-for-plan-examination.md)
- Repository: `docs/project/COMPUTE_STAGE_STEPS_AND_RULES.md`; `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/{CaseFolders,Compare,Workflow/Pla,Workflow/RtExamination,Workflow/Review,Workflow/Output}`; `src/ProcessingTools/adapters`.

## Dev Agent Record

### Agent Model Used

Codex, Mary analyst perspective. Story preparation used BMAD create-story; execution was a documentation and source-evidence analysis pass.

### Debug Log References

Story preparation inspected branch/HEAD, recent release commit subjects, proposals, baseline architecture/PRD/UX, Epic 11 scope and project rules. No runtime checks performed.

Execution inspected extraction routing, review persistence, parcel-scoped editing, PLA evidence selection, Compare decision/report boundaries, package inclusion and RT Save/Complete semantics. Validated every local Markdown link and table column count across Toolbox planning documents. Reviewed AC-to-deliverable coverage. No build, runtime smoke, live API mutation or production packaging was run for this documentation-only story.

### Completion Notes List

- Story specification prepared and checked for scope, dependencies, single/multi-parcel coverage and operational isolation on 2026-10-08.
- Produced the four process deliverables on 2026-10-08: six capability groups, five presets, 18 individual action contracts, source-based reuse inventory, 12 scenario walkthroughs and 12 owned open decisions.
- Confirmed user decisions are recorded separately from observed source behavior, proposed target behavior and unresolved choices.
- Compared operational/Epic 11 scopes without retiring or changing existing stories.
- Submitted deliverables for product review; acceptance remains unrecorded. Story and separate Toolbox tracker are at review; 13.2 execution remains gated on accepted process inputs.
- Product acceptance recorded on 2026-10-08 from JotaPe's explicit instruction: "accepted 13.1 process with story 13.2". Story 13.1 is done; the execution gate for 13.2 is satisfied. Open architecture/domain decisions remain owned inputs, not implicitly resolved.

### File List

- `_bmad-output/implementation-artifacts/toolbox/13-1-define-toolbox-process-and-task-matrix.md`
- `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml`
- `_bmad-output/planning-artifacts/toolbox/toolbox-process-definition.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-capability-inventory.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-scenario-walkthroughs.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-decision-register.md`
- `_bmad-output/planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md`
