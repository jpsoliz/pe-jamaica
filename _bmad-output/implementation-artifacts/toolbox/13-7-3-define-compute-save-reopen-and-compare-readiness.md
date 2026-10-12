---
story_key: 13-7-3-define-compute-save-reopen-and-compare-readiness
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-7-compose-compute-toolbox-preset
depends_on: [13-7-1-define-compute-routes-and-prerequisite-planning, 13-7-2-define-compute-review-and-manual-correction-workspace, 13-5-add-versioned-toolbox-workspace-persistence]
execution_gate: explicit-runtime-authorization-accepted-1371-1372-owner-and-compute-disposition-policy
implementation_authorized: false
---

# 13.7.3 - Compute Save, Reopen And Compare Readiness

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing 13.7 entry; no child key.

## Story

As an examiner, I want exact local saves/reopen and a reasoned Compute examination disposition, so that Compare receives current evidence without confusing forwarding, geometry validity or server completion.

## Scope And Dependencies

Requires accepted .1/.2/13.6, affected 13.5 owner (host B for real geometry, D when importing), trusted Compute coverage/effect/deferral policy and explicit code/host authorization. Owns workspace Save/Reopen binding, storage mapping admission, local completeness evaluation and immutable Compare-input record. No Compare execution, package/upload/publish/actual task advance; those remain 13.8/13.9. [Parent](13-7-compose-compute-toolbox-preset.md) owns requirements.

## Acceptance Criteria

1. Given current reviewed workspace, map every route/source/coverage/review/finding/inclusion/disposition field to an accepted owning model/register or separately reviewed owner extension/sidecar. Unsupported pending proposals/tool runs/checklist fields block the requested durable action with explicit diagnostic; do not change locked A DTOs, silently omit state or use legacy JSON as second master. UI navigation/preferences may be transient only when explicitly labeled.
2. Given permitted Save, freeze exact full case/base/input/policy and owned proposal, publish through one accepted owner with expected-parent/fresh authority checks and verify receipt/revision. Read-only source/Compare geometry denial remains. Prototype receipt is transport-only; actual accepted-host geometry requires host inventory/profile/domain proof. Unresolved owner journal/untracked edits cannot be presented clean.
3. Given cancellation/stale parent/failure/retry, retain dirty and historical work and report actual pending/quarantined/ambiguous/committed outcome from owner. Run ID/local return or exception is not durable save proof. After publication, missing receipt/tail requires recovery rather than false no-save/committed. Identical retry uses exact owner intent; never reallocates domain IDs or replays review events.
4. Given Reopen, validate full identity/revision/inventory/coverage mapping with fresh external policy/context and unchanged A hydration. Raw/manual/reviewed values, unknown fields, archive/lineage/finding/disposition histories and stale/pending status survive; saved permission/actual task/actor grants no authority. Default PointIds remains inspectable but cannot pass affected geometry approval through normalization.
5. Given local Compare-readiness request, require nonempty included set, disposition of every detected candidate and current applicable Compute coverage/examination for every included parcel/case/document scope. Extraction mismatches need correction/recheck or reviewed manual recovery; unresolved identity/integrity/permission failures block. Genuine source-plan defects may forward with current evidence/reason under C11 without geometry-validity or final-decision claim.
6. Given tabular work left for Compare, only trusted reviewed detailed deferral mapping permits explicit scoped outstanding items; unknown required obligation never becomes N/A or ready from free text. Keep human source presence/accuracy separate, cumulative findings intact and selected scope unable to hide blocked included/shared consumers. Compare may later correct current-case tabular data without geometry change; local readiness does not promise those comparisons completed.
7. Given readiness record, bind exact verified saved case/revision/policy/included inventory/coverage/finding state and immutable source references, clearly separating geometry approval and stage forwarding status. Save/readiness/Select Compare are separate explicit actions; change inputs/inclusion/policy/authority invalidates the record under accepted dependency rules. No auto-advance actual Innola task, upload, package or final rejection.
8. Given completion, real owner-backed save/reopen and UI smoke plus pure completeness fixtures demonstrate clean and forwarded-defect cases, pending/stale/multi-parcel failure, explicit deferrals and crash/retry. Preserve baseline158/applicable accepted groups and known207-stop/default-PointIds limitations. Obtain .3 then scoped 13.7 acceptance; real Compare/handoff/production gates remain open.

## Tasks / Subtasks

- [ ] Confirm authorization, predecessor/owner acceptance and exact coverage/deferral/storage mapping review. (AC1-8)
- [ ] Admit complete workspace state mapping or record blocking owner-contract gaps. (AC1)
- [ ] Wire .2 Save/Reopen through accepted owner with truthful pending/retry handling. (AC2-4)
- [ ] Implement local all-included completeness/disposition with explicit outstanding Compare work. (AC5-6)
- [ ] Produce exact immutable Compare-input/readiness record and explicit local navigation seam. (AC7)
- [ ] Run actual owner/UI plus pure tests, review and obtain .3/parent acceptance. (AC8)

## Developer Context And File Map

Future NEW Compute/ToolboxComputePersistenceCoordinator.cs, ToolboxComputeReadiness.cs and matching Tests/Workflow/. UPDATE only .2 command wiring and narrowly reviewed accepted-owner integration as necessary; enumerate/read exact targets before coding. Store remains one writer, model/policy/reconciliation services retain evaluation semantics and hashes. Do not use WorkflowSession/legacy Finalize/approved_review.json output generation as implicit handoff or acceptance. No source/settings/DAML/remote changes by default.

## Test Requirements And Completion Evidence

Fixtures: nonempty included gate; one blocked selected-out parcel; unresolved candidate; excluded/shared neighbor invalidation; complete coverage with recorded source defect and geometry not approved; corrected mismatch versus pending correction; explicit allowed/disallowed deferral; missing storage field; identical retry after successor; wrong case/store; fresh-policy reopen; changed authority; pending owner tail; no remote call. Real host save required for geometry claims, pure fixture success is not host proof. Parent lane plus UI/storage/host evidence remains separate; no runtime test performed now.

## References

- [Parent](13-7-compose-compute-toolbox-preset.md), [.1](13-7-1-define-compute-routes-and-prerequisite-planning.md), [.2](13-7-2-define-compute-review-and-manual-correction-workspace.md), [13.5 owners](13-5-add-versioned-toolbox-workspace-persistence.md), [13.6](13-6-register-reusable-toolbox-capabilities.md).
- [Approved checklist](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [A limitations](13-5a-implementation-evidence.md), [review record](13-7-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization false; tasks unchecked. No save coordinator/readiness code, owner revision, source artifact, UI tests, server action, commit or deployment. Exact durable coverage mapping remains future reviewed evidence.
