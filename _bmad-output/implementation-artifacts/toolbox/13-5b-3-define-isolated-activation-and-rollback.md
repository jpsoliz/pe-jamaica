---
story_key: 13-5b-3-define-isolated-activation-and-rollback
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5b-add-recoverable-fgdb-commits-and-isolated-activation
depends_on: [13-5b-1-define-host-commit-and-recovery-protocol, 13-5b-2-define-edit-detection-and-supported-snapshot-proofs]
execution_gate: explicit-isolated-activation-authorization-and-all-host-proof-policy-gates
implementation_authorized: false
---

# 13.5B.3 - Isolated Activation And Rollback

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As an isolated test examiner, I want explicit single-owner activation and conservative rollback, so that verified new cases cannot be edited by independent legacy writers.

## Scope And Dependencies

Requires accepted C/E/B.1/B.2 and real F01/F02/F03 evidence, D if imported cases used, reviewed D01/D06/D11 ownership/conflict/rollback and D08 reference scope, plus explicit isolated activation authorization. F07 before installation. This slice alone owns narrow activation/legacy-write guards/consumer routing and rollback. [B parent](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md) remains authority source. No production release, remote/handoff, package-v2 or operational 11.2 closure.

## Acceptance Criteria

1. Given all named gates, enumerate exact adapter UPDATE files/call sites and read them completely before coding. Review activation flag/store/schema/commit binding, allowed current-case mutations, consumer-read provenance and deny routes. Unknown/missing guard coverage blocks activation. Specification preparation names categories, not an unverified concrete update list.
2. Given explicit isolated case activation, one CaseStore owns FGDB/evidence and only current permitted scope routes to it. Legacy independent writers reject activated targets; no dual-write editable JSON/FGDB. Read-only compatibility projections are exact owner/schema/revision/commit-qualified. Baseline operational cases/build/settings/registration remain untouched and usable.
3. Given current caller permissions/task/linked-source identity, Compute geometry and Compare current-case tabular permissions stay separate. Persisted actor/task/permission class remains audit; mutation/recovery publication requires fresh authority independent of frozen intent digest. Linked PE/source geometry cannot become current writable geometry.
4. Given pending/untracked/ambiguous activation or commit, edit/approval/export consumers fail closed rather than use a cached marker or stale projection. Accepted geometry consumers use a named verified immutable snapshot of all included parcels; forwarded defects and selected subset never substitute for acceptance.
5. Given rollback, stop writers/consumers, preserve new-only data/snapshots/audit and return to untouched baseline cases/build. Old build is never claimed able to read new schema; no downgrade without separately reviewed lossless mapping. Unknown rollback outcome blocks edits/exports and retains evidence, rather than partially enabling both owners.
6. Given installation, demonstrate F07 identity/config/root separation and rollback on explicitly authorized test target before activating; never install over operational registration merely because a worktree build exists. Where host tests do not install, record F07 as not invoked, not passed.
7. Given completion, independent GIS/ownership review accepts complete guard/route matrix, actual activation/denial/rollback smoke and predecessor lanes. B closes only after all B slices and required parent acceptance coverage; production migration/remote/11.2 remain open. No automatic commit, merge or deployment.

## Tasks / Subtasks

- [ ] Confirm explicit activation authorization, predecessor proof/acceptance and named policy/F07 gates. (AC1-7)
- [ ] Enumerate/read/review exact writer/consumer update boundaries and guard matrix. (AC1)
- [ ] Implement single-owner activation and narrow legacy-writer/consumer guards. (AC2-4)
- [ ] Implement stopped-owner rollback with retained new-only evidence and conservative uncertain outcomes. (AC5)
- [ ] Run authorized isolation/install and activation/denial/rollback smoke; record scoped B.3/B acceptance coverage. (AC6-7)

## Developer Context And File Map

Future NEW activation/guard/rollback adapters under Persistence/ArcGis/, headless guard tests and hosted smoke scenarios. Concrete operational adapter UPDATE list is deliberately unclaimed until full source/boundary review under authorization; no broad rewrite allowed. Reuse B.1 authority/receipt protocol and B.2 snapshot/edit proof. Settings/DAML/registration/source/output/package/remote changes are excluded unless explicitly required and separately reviewed within authorized isolated scope. Preserve baseline data/worktree/user changes.

## Test Requirements And Completion Evidence

Guard matrix must cover all activated writer entry points and stale consumer paths, wrong case/store, revoked authority, Compare geometry denial, source PE denial, active pending commit, projection mismatch, rollback interruption and baseline smoke. Record exact code changes and host/installation identity evidence separately from headless tests. Preserve baseline 158/applicable predecessors, known Compare XAML/full-suite limits and default-PointIds limitation. No activation/deployment smoke executed now.

## References

- [B parent](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md), [B.1](13-5b-1-define-host-commit-and-recovery-protocol.md), [B.2](13-5b-2-define-edit-detection-and-supported-snapshot-proofs.md).
- [Decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [F01-F07](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [migration/rollback](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md), [project rules](../../project-context.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/activation authorization not granted; all tasks/proof gates unchecked. No operational adapter updates, installation, activation, rollback experiment, runtime tests, commit or deployment. Fill actual update list/logs/review evidence on authorized execution.
