---
story_key: 13-8-3-define-compare-decisions-and-compute-correction-loop
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-8-compose-read-only-compare-and-correction-flow
depends_on: [13-8-1-define-revision-bound-geometry-and-comparison-evidence, 13-8-2-define-compare-workspace-tabular-correction-and-partial-review, 13-7-compose-compute-toolbox-preset]
execution_gate: explicit-runtime-authorization-and-accepted-decision-policy-owner-and-compute-loop
implementation_authorized: false
---

# 13.8.3 - Compare Decisions And Compute Correction Loop

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Existing 13.8 parent tracks this child, no new key.

## Story

As an examiner, I want an explicit complete Compare decision or a controlled geometry-correction return, so that every decision is bound to current full-case evidence and prior review stays auditable.

## Scope And Dependencies

Requires accepted .1/.2/13.7/affected owner and trusted detailed decision/effect/coverage policy plus explicit runtime/host authorization. Owns local completeness evaluator/owner-persisted final decision and correction request/Compute-return/recomparison coordination. Reuse .2 interface and Compute command/readiness seams without changing server workflow. No task transition, attachment, Enterprise publication or package; 13.9 consumes a named verified result later.

## Acceptance Criteria

1. Given requested final decision, validate exact saved case/owner/current revision/policy, all detected dispositions, all included parcel/document/participant/pair coverage, human comparison and cumulative findings. Required unavailable/incomplete evidence yields pending-with-reason, not final approved/rejected. Technical blocked/saved_progress/legacy approved flag/readiness marker is not complete examination.
2. Given sufficiently completed review, explicit examiner Approved/Rejected choice is separately represented with scoped evidence/reasons and full cumulative finding history. Rejection can cite genuine source defects without falsely requiring geometry approval; approval cannot certify invalid geometry or unresolved policy requirements. Domain geometry validity and downstream geometry-acceptance requirements remain separate from examination outcome and server completion.
3. Given decision persistence, exact logical outcome/coverage/evidence/fingerprints/audit has an admitted owning representation. If existing A/examination/storage shapes cannot faithfully encode rejected/pending comparison state, require reviewed extension/sidecar under one owner before enabling Save; do not remap legacy blocked to rejected or silently extend locked enums/DTOs. Save/readiness/navigation and later remote handoff remain distinct.
4. Given geometry defect needing correction, preserve all partial Compare data and record exact base/case/source/finding/affected scopes. Require explicit owner-backed save/dirty-state disposition before switching; canceled or ambiguous save prevents silent navigation/loss. Select Compute locally; selection alone grants no edit permission and actual Innola task stays unchanged. Fresh current geometry authority is required for actual correction.
5. Given accepted Compute correction, 13.7 creates a new saved revision through its owner/reconciliation. On Compare reentry validate new revision and impact union; stale geometry-dependent comparisons/checks/decisions and affected aggregates need fresh review/disposition. Reuse unrelated completed evidence only with exact unchanged dependencies; preserve old decisions/query snapshots as history. No automatic geometry correction, stale parent rebase or approval refresh by updating hashes.
6. Given tabular-only issue, stay in Compare under .2 whitelist; do not force geometry return or duplicate raw data. Changed source/policy/included set/authority invalidates final decision via accepted dependency rules; linked PE edits always denied. A selected subset cannot hide blocked included/shared-edge consumers.
7. Given completion, actual owner/UI/host correction-loop and pure completeness/decision fixtures cover outage pending, rejected source defect, invalid-geometry approval denial, explicit save/switch/cancel, revoked geometry authority, shared/excluded/retired impacts and stale query/decision after new revision. Preserve predecessor lanes/limits; obtain .3 then scoped13.8 acceptance, no remote or production claim.

## Tasks / Subtasks

- [ ] Confirm authorization, .1/.2/Compute/owner acceptance and exact decision/coverage storage policy review. (AC1-7)
- [ ] Implement all-included current completeness and explicit final decision representation. (AC1-3)
- [ ] Implement saved correction request/dirty-state navigation and fresh Compute permission handoff. (AC4)
- [ ] Coordinate new-revision Compare reentry and accepted dependency invalidation/history. (AC5-6)
- [ ] Run pure/owner/UI/host loop evidence and obtain .3/13.8 scoped acceptance. (AC7)

## Developer Context And File Map

Future NEW Toolbox/Compare decision evaluator/correction coordinator and tests. UPDATE .2 command interface wiring and narrow reviewed Compute reentry seam only after exact source/update review. Existing CompareReviewDecision.cs is legacy evidence: approved/blocked/returned_to_compute/saved_progress are not the full Toolbox final/pending model, and readiness uses ordinary JSON artifacts. Do not call legacy Approve/Finalize/CompleteTask handlers or output/package services. Accepted model/dependency algorithms remain sole owner; no new matching/topology engine or A hash/enum change by default.

## Test Requirements And Completion Evidence

Parent lanes plus real accepted-owner save and WPF/host return/recompare smoke. Fixtures test two accepted plus one blocked selected-out parcel, source-defect inspection/rejection, service outage no automatic rejection, no-record explicit human policy disposition, legacy blocked mapping denial, missing durable outcome mapping, canceled dirty save, stale correction base, revoked authority, unrelated evidence reuse and failed/ambiguous owner receipt. No runtime tests or server effects executed by preparation.

## References

- [Parent](13-8-compose-read-only-compare-and-correction-flow.md), [.1](13-8-1-define-revision-bound-geometry-and-comparison-evidence.md), [.2](13-8-2-define-compare-workspace-tabular-correction-and-partial-review.md), [Compute](13-7-compose-compute-toolbox-preset.md), [13.5 owner](13-5-add-versioned-toolbox-workspace-persistence.md).
- [Checklist/C11-C15](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md), [review record](13-8-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization false, all tasks unchecked. No decision/correction code, save/recompare experiment, geometry edit, UI/host tests, task mutation, installation, commit or deployment. Populate actual implementation/review evidence later.
