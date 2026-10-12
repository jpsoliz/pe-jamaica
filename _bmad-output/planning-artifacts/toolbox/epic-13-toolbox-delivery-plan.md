# Epic 13: Transaction Case Toolbox

Status: in-progress (13.1-13.4 done for scoped deliverables; 13.5 prepared/backlog; 13.6-13.10 backlog; baseline validation issues deferred)
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

Stories 13.1-13.4 are done for their scoped deliverables. JotaPe accepted 13.4B's isolated in-memory scope on 2026-10-11, closing parent 13.4 for M2. Final correction build is clean and 135 focused tests pass; the full harness stops after 207 passes at the known Compare XAML assertion. No full-suite or production GIS acceptance is claimed.

[Story 13.5](../../implementation-artifacts/toolbox/13-5-add-versioned-toolbox-workspace-persistence.md) remains backlog pending production storage/schema review and feasibility gates. [13.5A](../../implementation-artifacts/toolbox/13-5a-add-storage-contract-and-hydration-foundation.md) is accepted/done for isolated no-writer byte-codec/hydration on 2026-10-11, tracked through parent without a new key. Final build clean and 158 focused passes; full207 then known Compare XAML failure. [A evidence](../../implementation-artifacts/toolbox/13-5a-implementation-evidence.md) retains limits. Remaining M3 work is refined into C/D/E/B candidates below, not implemented or authorized. 13.6-13.10 remain backlog. Historical updates below do not override this current summary.

## Tracking And Execution

2026-10-11 remaining-work refinement: execution order A -> C -> D/E -> B. [C prototype disk revisions](../../implementation-artifacts/toolbox/13-5c-add-isolated-prototype-disk-revisions.md), [D read-only copied import](../../implementation-artifacts/toolbox/13-5d-add-read-only-copied-case-import.md), [E exact schema/projection mapping](../../implementation-artifacts/toolbox/13-5e-define-fgdb-schema-and-projection-mapping.md) and [B final FGDB recovery/activation](../../implementation-artifacts/toolbox/13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md) are backlog, with contract/dependency review gates. C's R1-R10 contract corrections are applied, and C.1 protocol, C.2 Windows adapter and C.3 revision operations are specified/reviewed under JotaPe's no-coding instruction; [ordered child stories](../../implementation-artifacts/toolbox/13-5c-add-isolated-prototype-disk-revisions.md#ordered-implementation-stories). Runtime authorization, adapter proof and scoped acceptance remain outstanding; E catalog review may proceed in parallel. No new child tracker keys, product decision acceptance, runtime implementation, installation or ownership switch.

2026-10-11 latest acceptance: JotaPe accepted 13.5A for isolated no-writer contract/codec/hydration; child done, superseding review-pending descriptions above. Parent 13.5 and later disk/import/schema/FGDB gates remain open. Next refine remaining M3 foundation; no production/cutover approval or full-suite green implied. See [accepted A evidence](../../implementation-artifacts/toolbox/13-5a-implementation-evidence.md).

Separate tracker: `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml`.
2026-10-11 D/E/B preparation: [D child specifications](../../implementation-artifacts/toolbox/13-5d-add-read-only-copied-case-import.md#ordered-implementation-stories), [E child specifications](../../implementation-artifacts/toolbox/13-5e-define-fgdb-schema-and-projection-mapping.md#ordered-implementation-stories) and [B child specifications](../../implementation-artifacts/toolbox/13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md#ordered-implementation-stories) are prepared/reviewed under the continuing no-coding instruction. [Review evidence](../../implementation-artifacts/toolbox/13-5deb-story-preparation-review-2026-10-11.md). All remain backlog without new keys; exact mapper/catalog/profile, host proof and activation targets remain future deliverables and gates.
Story directory: `_bmad-output/implementation-artifacts/toolbox/`.
2026-10-11 13.6 preparation: [capability registry parent](../../implementation-artifacts/toolbox/13-6-register-reusable-toolbox-capabilities.md) now defines ordered .1 descriptor/registry, .2 eligibility/scope/staleness and .3 headless runner/reviewed local bindings. [Preparation review](../../implementation-artifacts/toolbox/13-6-story-preparation-review-2026-10-11.md) records coverage/limits. Documentation-only; existing backlog key retained, no runtime authorization, policy publication, remote effects or persistence gate closure. 13.7/13.8/13.9/13.10 remain later preset/handoff scope.
Design outputs: `_bmad-output/planning-artifacts/toolbox/`.
2026-10-11 13.7 preparation: [Compute parent and child stories](../../implementation-artifacts/toolbox/13-7-compose-compute-toolbox-preset.md) now specify .1 route/prerequisite planning, .2 examiner review/manual workspace and .3 owner-backed save/reopen/local Compare readiness. [Preparation review](../../implementation-artifacts/toolbox/13-7-story-preparation-review-2026-10-11.md) records scoped source/policy/storage handoffs. Documentation only, existing backlog key retained; no runtime/UI/provider/owner execution or automatic finalization. 13.8/13.9/13.10 remain later comparison/handoff/task-preset scopes.

2026-10-11 13.8 preparation: [Compare parent and ordered children](../../implementation-artifacts/toolbox/13-8-compose-read-only-compare-and-correction-flow.md) specify .1 revision-bound read-only geometry/evidence, .2 local tabular correction/partial-review workspace and .3 complete decisions/Compute-return loop. [Review evidence](../../implementation-artifacts/toolbox/13-8-story-preparation-review-2026-10-11.md). Documentation only; required incomplete evidence remains pending, saved source defects do not become accepted geometry, and actual lifecycle/attachments/publish remain 13.9. Existing backlog key retained; no runtime/live queries/host/save/task effects by preparation.

2026-10-11 13.9 preparation: [handoff parent and A/B/C specifications](../../implementation-artifacts/toolbox/13-9-add-revision-consistent-toolbox-handoff.md) retain architecture IDs for A same-revision snapshot/package/restore v2, B protected Enterprise projection/receipt and C durable attachments/task reconciliation. [Review evidence](../../implementation-artifacts/toolbox/13-9-story-preparation-review-2026-10-11.md). Existing backlog key/no child keys; no coding or remote effect authorized. D02/D05/D06/D10 and actual F03-F06/F07 where applicable remain gates. Source-defect/rejected handoff never advertises invalid geometry accepted; 13.10 retains PLA/RT composition.

2026-10-11 13.10 preparation: [preset parent and architecture A/B/C specifications](../../implementation-artifacts/toolbox/13-10-compose-plan-annexation-and-rt-toolbox-presets.md) define A PLA_A selected evidence/permitted unreferenced construction, B read-only originating PE/current annex evidence and C stage-driven RT current-Plan Save/Save & Close branch actions. [Review evidence](../../implementation-artifacts/toolbox/13-10-story-preparation-review-2026-10-11.md). Existing backlog key/no children tracked; documentation only, no profile widening/geometry/source/network/task effects. Actual PLA permission/profile/reference and RT Plan/template/branch/13.9 receipt proof stay open. Epic implementation is not completed by specification preparation.

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

2026-10-08 current isolated acceptance: A's authorized corrections and evidence-alias follow-up are complete, final patch review has no remaining blocker, build is clean and 105 focused tests pass. Requested 13.4A signoff and canonical amendment acceptance are complete for their in-memory scope; full-suite limits persist. D07 P01-P05 remain approved; B is prepared/backlog awaiting explicit execution authorization. Parent 13.4 stays in-progress. This supersedes earlier pending/withheld A acceptance notes below, not production or original 11.2 gates.

2026-10-08 approval/review follow-up: D07 P01-P05 are approved by JotaPe as `toolbox_d07_reconciliation_v1`; their local contract approval gate is resolved. Final A review identified three additional currency/resolution gaps, so signoff remains withheld and B stays backlog/unimplemented. This supersedes the pending-D07-approval statement below, not the recorded full-suite/production limits. No operational status or code changed.

2026-10-08 isolated current-state follow-up: 13.4A and the opt-in canonical amendment are implemented/tested but remain in-review (101 focused passes; full-suite limits retained). [13.4B](../../implementation-artifacts/toolbox/13-4b-add-rerun-reconciliation-shared-boundaries-and-lineage.md) is now prepared/backlog, not implemented or approved. A signoff, proposed D07 rules and execution authorization remain entry gates. Parent 13.4 is in-progress in the isolated tracker; historical backlog/no-file statements above are preparation history, not the current isolated delivery status. Operational trackers and code remain unchanged.

- [Compute / Compare checklist matrix](toolbox-compute-compare-checklist-matrix.md): source inventory and proposed stage-completeness checklist prepared at JotaPe's request; supports 13.4A and later tool/preset integration, no status or implementation approval implied.

- [13.4A - Stable Parcel Memberships And Acceptance](../../implementation-artifacts/toolbox/13-4a-add-stable-parcel-memberships-and-acceptance.md): prepared at JotaPe's request on 2026-10-08; backlog with explicit 11.2/D11 and local identity-policy gates. Tracked through parent 13.4; shared-edge/rerun/lineage scope remains proposed 13.4B. No application implementation authorized by this preparation.

- [Process proposal](../toolbox-case-workspace-and-task-matrix-2026-10-08.md)
- [Earlier toolbox draft](../toolbox-transaction-stage-architecture.md)
- [Operational architecture](../architecture.md)
- [Operational sprint reconciliation, main checkout](../../../../../_bmad-output/implementation-artifacts/sprint-reconciliation-2026-10-08.md)
- [Story 13.1](../../implementation-artifacts/toolbox/13-1-define-toolbox-process-and-task-matrix.md)
- [Story 13.2](../../implementation-artifacts/toolbox/13-2-define-toolbox-architecture-and-parallel-migration-plan.md)
