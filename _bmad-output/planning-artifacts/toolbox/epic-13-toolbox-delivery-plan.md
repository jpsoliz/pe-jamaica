# Epic 13: Transaction Case Toolbox

Status: in-progress (13.1-13.3 done for their scoped deliverables; baseline validation issues deferred; 13.4-13.10 backlog)
Created: 2026-10-08
Track: toolbox
Operational baseline inspected: wipRC9, commit 5908f30617cdc49e09873635a30343da8aff9ad6

## Objective

Organize existing capabilities into reusable tools over a versioned transaction case workspace, supporting automated preparation, assisted review and manual work for single-parcel plans and multi-parcel Cad Maps. Keep current development available while the new process is designed and validated separately.

This plan is the Toolbox epic source. It does not replace the operational epics document or add this initiative to the operational sprint tracker.

## Confirmed Requirements

- One current TR case has one spatial workspace/FGDB, with one or many identified parcels and reusable tabular evidence.
- The examiner chooses the working preset: Compute, Compare, PLA_A, PLA_B or RT. Innola task identity remains separately visible and is validated before server actions/handoff.
- Compare geometry is read-only; corrections use Compute and require fresh dependent checks and comparison.
- All included parcels must be accepted before initial multi-parcel handoff. Exclusion requires explicit evidence and reason.
- Enterprise working_review is a checkpoint projection, not final cadastral promotion or a competing editable master.
- ZIP packages preserve the FGDB, reviewed tables, approval/findings and required evidence. Save, suspend, publish, attach and advance have separate outcomes.
- Existing operational cases, configuration and add-in registration are protected during experimental development.

## Ordered Backlog

| Story | Story Key | Scope | Entry Gate |
|---|---|---|---|
| 13.1 - Define Toolbox Process And Task Matrix | 13-1-define-toolbox-process-and-task-matrix | Documentation: tasks, tools, scenarios, permissions, evidence, open decisions | Current proposal and confirmed user requirements |
| 13.2 - Define Toolbox Architecture And Parallel Migration Plan | 13-2-define-toolbox-architecture-and-parallel-migration-plan | Documentation: ownership, contracts, revisions, isolation, compatibility and architecture decisions | 13.1 accepted; unresolved architecture inputs visible |
| 13.3 - Add Toolbox Case Context And Workspace Selection | 13-3-add-toolbox-case-context-and-workspace-selection | Code: shared case context, selected workspace vs actual Innola task | 13.1/13.2 accepted; isolated development environment |
| 13.4 - Add Single- And Multi-Parcel Toolbox Model | 13-4-add-single-and-multi-parcel-toolbox-model | Code: identities, groups, shared-boundary dependencies and parcel acceptance | Architecture plus Epic 11 reuse reconciliation |
| 13.5 - Add Versioned Toolbox Workspace Persistence | 13-5-add-versioned-toolbox-workspace-persistence | Code: FGDB/table ownership, save/reopen, revisions and compatibility projections | Data contract and migration decisions accepted |
| 13.6 - Register Reusable Toolbox Capabilities | 13-6-register-reusable-toolbox-capabilities | Code: tool contracts, applicability, prerequisites and stale results | Workspace/input/output contracts stable |
| 13.7 - Compose Compute Toolbox Preset | 13-7-compose-compute-toolbox-preset | Code: automatic/assisted/manual preparation, review and geometry | Registered shared capabilities; single/multi-parcel fixtures |
| 13.8 - Compose Read-Only Compare And Correction Flow | 13-8-compose-read-only-compare-and-correction-flow | Code: accepted revision, checks, compare decision and Compute return | Compute/persistence contracts proven |
| 13.9 - Add Revision-Consistent Toolbox Handoff | 13-9-add-revision-consistent-toolbox-handoff | Code: snapshot, package, publish receipts, retries and task validation | Read/write scopes and server-side policy accepted |
| 13.10 - Compose Plan Annexation And RT Toolbox Presets | 13-10-compose-plan-annexation-and-rt-toolbox-presets | Code: shared tools with task-specific writeback and linked-PE read-only context | Foundation and handoff proven; split into smaller stories before development |

Stories 13.1-13.3 have detailed files and are done for their scoped deliverables. Story 13.3's reviewed local context foundation has a clean build and 22 focused tests; full-suite green remains a separately tracked baseline/release gate. Story 13.4 has a detailed specification with readiness gates still open. Rows 4-10 remain backlog, not ready for implementation. Refine/split them and resolve their named gates before execution.

## Tracking And Execution

Separate tracker: `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml`.
Story directory: `_bmad-output/implementation-artifacts/toolbox/`.
Design outputs: `_bmad-output/planning-artifacts/toolbox/`.

When invoking BMAD workflows, pass the exact Toolbox story and tracker paths. Default workflows otherwise resolve the operational tracker. No operational status changes are implied by Toolbox readiness/completion.

`ready-for-dev` for 13.1/13.2 means ready for documentation execution by the named owner. It does not authorize application code, deployment, API mutations or data migration. Their unchecked tasks remain unfinished work.

## Reuse And Dependency Policy

Use the Epic 11 inventory and canonical single/multi-parcel contract as inputs. Do not duplicate Story 11.2 or expand its contract-only scope implicitly. For overlapping work record reuse, adaptation, dependency or a proposed supersession; do not retire operational stories without an explicit planning decision.

Reuse extraction adapters, reviewed points/segments and solver services, output execution, package services, Compare evidence/query services, PLA evidence/crop services and RT lifecycle semantics. Presence of source code or a completed checklist does not establish current acceptance.

## Design Acceptance Gates

13.1 delivers a reviewed process definition with traceability, scenario walkthroughs and a decision log. Product decisions requiring user judgment remain open until recorded; documentation creation is not acceptance.

2026-10-08 execution update: Story 13.1 documentation is submitted for review. Deliverables: [process definition](toolbox-process-definition.md), [capability inventory](toolbox-capability-inventory.md), [scenario walkthroughs](toolbox-scenario-walkthroughs.md), and [decision register](toolbox-decision-register.md). Product acceptance is pending; Story 13.2's execution gate remains in effect.

2026-10-08 acceptance update: JotaPe accepted the 13.1 process and authorized 13.2. The process story is done and architecture documentation is in progress; unresolved domain decisions remain in the register.

2026-10-08 architecture update: 13.2 is submitted for review with [architecture](toolbox-architecture.md), [contract catalog](toolbox-contract-catalog.md), [parallel migration plan](toolbox-parallel-migration-plan.md) and [acceptance strategy](toolbox-acceptance-strategy.md). Architecture acceptance is pending. The plan proposes splitting 13.4, 13.5, 13.9 and 13.10 before development; proposed substory IDs have not been added to tracking or made ready. Current 13.3-13.10 entries remain backlog.

13.2 delivers architecture decisions, actual-source/API evidence, contract/schema outlines, isolation/rollback plan and implementation slices. Unresolved high-impact data ownership, server authorization or deployment questions block downstream affected stories.

No branch/worktree is created by story preparation. Implementation isolation will be specified in 13.2 before any operational files or services are changed.

2026-10-08 acceptance/preparation update: JotaPe accepted the 13.2 architecture direction and requested [Story 13.3](../../implementation-artifacts/toolbox/13-3-add-toolbox-case-context-and-workspace-selection.md). 13.2 is done; 13.3 is ready-for-dev with unchecked implementation tasks. Its scope separates local workspace selection from observed Innola identity, supports parcel-ID selections and denies remote actions. No code/worktree/deployment was created by preparation; unresolved domain/service feasibility gates remain.

2026-10-08 implementation update: JotaPe requested preparation and implementation in isolation. Foundation code is in `temp/toolbox-worktree` on `refactor/toolbox-case-workspace`; operational source/tracker remain unchanged. Solution build and 20 focused tests pass. Full regression execution hits existing filesystem/Compare UI failures reproduced on the untouched baseline; 13.3 remains in-progress with final validation unchecked, not review/done. No installation, merge, FGDB migration or remote action occurred.

2026-10-08 review closure: JotaPe requested review/finish and authorized all fixes. Two 13.3 bugs were fixed and regression-tested; all three review layers completed and no local finding remains. Story 13.3 is done for context/selection only. The full harness still stops at the reproduced baseline Compare UI assertion, recorded in [deferred work](../../implementation-artifacts/toolbox/deferred-work.md); no full-suite pass, live TR UI, storage migration, installation or merge is claimed. Next scope is refinement of 13.4 with Epic 11 reuse and its named domain gates.

2026-10-08 preparation update: [Story 13.4](../../implementation-artifacts/toolbox/13-4-add-single-and-multi-parcel-toolbox-model.md) now defines single/multi-parcel identity, memberships, inclusion/acceptance, candidate lineage and shared-edge invalidation. Recommended 13.4A then 13.4B slices remain proposed. Story 11.2 is not implemented; D11 ownership/reuse, D07 domain policy and decomposition acceptance must be recorded before code. No application changes or operational tracker updates occurred; 13.4 remains backlog.

## References

- [Compute / Compare checklist matrix](toolbox-compute-compare-checklist-matrix.md): source inventory and proposed stage-completeness checklist prepared at JotaPe's request; supports 13.4A and later tool/preset integration, no status or implementation approval implied.

- [13.4A - Stable Parcel Memberships And Acceptance](../../implementation-artifacts/toolbox/13-4a-add-stable-parcel-memberships-and-acceptance.md): prepared at JotaPe's request on 2026-10-08; backlog with explicit 11.2/D11 and local identity-policy gates. Tracked through parent 13.4; shared-edge/rerun/lineage scope remains proposed 13.4B. No application implementation authorized by this preparation.

- [Process proposal](../toolbox-case-workspace-and-task-matrix-2026-10-08.md)
- [Earlier toolbox draft](../toolbox-transaction-stage-architecture.md)
- [Operational architecture](../architecture.md)
- [Sprint reconciliation](../../implementation-artifacts/sprint-reconciliation-2026-10-08.md)
- [Story 13.1](../../implementation-artifacts/toolbox/13-1-define-toolbox-process-and-task-matrix.md)
- [Story 13.2](../../implementation-artifacts/toolbox/13-2-define-toolbox-architecture-and-parallel-migration-plan.md)
