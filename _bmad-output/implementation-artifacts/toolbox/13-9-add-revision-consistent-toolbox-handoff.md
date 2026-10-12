---
story_key: 13-9-add-revision-consistent-toolbox-handoff
track: toolbox
story_type: code
created: 2026-10-11
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-6-register-reusable-toolbox-capabilities, 13-7-compose-compute-toolbox-preset, 13-8-compose-read-only-compare-and-correction-flow]
execution_gate: explicit-runtime-sandbox-authorization-and-reviewed-handoff-policy-service-proofs
implementation_authorized: false
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.9 - Add Revision-Consistent Toolbox Handoff

Status: backlog
Specification: preparation and static review complete under continuing documentation-only/no-coding instruction. Existing 13.9 key retained; no child keys, runtime/service authorization or handoff acceptance implied.

## Story

As an examiner, I want snapshots, packages, projections, attachments and actual task effects bound to one frozen case revision, so that failures and retries cannot mix evidence or falsely complete a transaction.

## Ordered Implementation Stories

Retain the architecture's proposed A/B/C identities rather than inventing overlapping slices.

| Story | Owns | Runtime dependency |
|---|---|---|
| [13.9A Snapshot, Package And Restore V2](13-9a-define-revision-snapshot-package-and-restore-v2.md) | Same-revision local bundle, safe archive and fresh-root restore | Accepted host snapshot/owner contracts, D10 inclusion/limits review and explicit authorization |
| [13.9B Enterprise Projection And Verified Receipts](13-9b-define-enterprise-projection-and-verified-receipts.md) | Exact approved-geometry projection, concurrency/F05 and durable remote receipt | A accepted, reviewed D05/D06 target policy, actual capabilities and explicit sandbox mutation scope |
| [13.9C Handoff Ledger, Attachments And Task Transition](13-9c-define-handoff-ledger-attachments-and-task-transition.md) | Frozen ordered plan, durable ledger, uploads/reconciliation and actual lifecycle | A accepted, B when required, reviewed D02/task policy and actual F04/F06 plus explicit sandbox scope |

A -> B -> C is the default delivery order; a reviewed task policy may omit optional projection, never a policy-required receipt. B owns its projection operation/receipt durability through the accepted local owner and can be accepted before C's orchestration; C consumes that receipt without owning B's protocol. A local package is not upload/transition proof. F07 precedes installation; mocks/dry-run are default until named service/host gates and target authorization are evidenced.

Actual F04/F05/F06 capability proof may itself require isolated service effects; these run only in a separately authorized bounded proof runner on named test targets. Normal mutation adapters remain disabled until their proofs pass. Story preparation does not authorize proof-runner or normal-adapter effects.

## Contract And Authority Boundaries

Freeze versioned handoff intent: full case/environment/current/source identity, operation ID, exact owner/schema/profile/revision/parent/content digests, included/disposition inventory, selected preset/actual-task mapping version, package/report/source assets with exact bytes, required ordered actions and approved target policy. Mutable ledger progress and fresh credentials/authority stay outside the immutable intent digest; original actor/task observations remain audit, not authorization. Changed payload/target/semantic plan requires a new operation and explicit review, never reuse an old receipt for new bytes.

Separate save, snapshot, package build, projection, attachment and actual transition outcomes. Every action records pinned intent/target/request identity before effect, then verified result/object IDs and read-back evidence. No atomicity claim across FGDB, files, FeatureServer, attachments and Innola. Local UUID/HTTP success/API flag/marker alone is not remote deduplication or complete revision proof.

New runs require current policy-valid full-scope examination and applicable geometry proof. C11 forwarded-with-recorded-defects can carry findings/evidence to the normal next stage without claiming valid geometry; final rejected decision can carry rejection evidence without pretending accepted geometry. Required incomplete Compare remains pending and cannot trigger final completion. D05/D10/D12/task policy decides artifacts/actions/outcome branch; do not universally publish geometry or treat every handoff as Approved. Any geometry publication requires independently current accepted geometry from every included parcel, never selected subset/source PE.

Snapshot/package v2 preserves full source/manual/reviewed data, all findings/history, policies, named geometry/datasets and task-specific evidence. creating_task_id is provenance, not case identity or saved authority. Restore reacquires fresh context/task/policy, validates lineage/conflicts and never overwrites active/operational roots automatically. Legacy v1 remains unchanged; its imported flags are observations, not new acceptance.

Server effects require separately verified fresh session/operator/assignment/current actual instance/mapping/allowed target immediately before each action. Missing/fallback taskId, transaction-name aliases, preset selection or first/default transition is insufficient. Keep local Toolbox RemoteActionAccess denial; introduce a separately reviewed handoff authorization seam, not a global context enable switch. Future read/write calls target explicitly authorized isolated environments only; documentation preparation authorizes none.

Restart from running/missing-response state is ambiguous until read-back reconciles actual remote truth. Confirmed identical steps are never resent. Retry only when capability evidence proves safe idempotency/absence or a reviewed explicit recovery disposition applies; unknown object/transition outcome blocks further dependent effects. Compensation cannot delete or overwrite unrelated/newer data; retain partial confirmed steps and honest failed/ambiguous plan status.

## Acceptance Criteria

1. Exact revision/evidence/package/report identities remain frozen across all actions; changed intent conflicts and cross-case/selected-subset/stale geometry cannot satisfy full required scope.
2. A uses supported quiesced snapshot/read-back/F03 and safe bounded v2 archive/restore, preserving original roots, lineage and fresh authority. Partial/pending/source-defect packages are honestly classified, never accepted merely because polygons exist.
3. B publishes only applicable current accepted geometry under actual enforced writer/expected-prior target protection and F05 proof. Remote feature identities/inventory/read-back and receipt agree on complete revision; no JSON reconstruction or marker-only success.
4. C records durable pending/running/confirmed/failed/ambiguous/reconciled action evidence, pins all requests before effects, checks fresh task authority and required predecessor receipts, and restores exact progress without duplicate side effects.
5. Upload/report/task response loss and cancellation produce verified or ambiguous outcomes, with F06 capability-based reconciliation; no automatic timeout resend or inferred absent object. Final transition only occurs after all required evidence/action receipts, never local save or partial progress.
6. Policies discriminate suspend/draft, Compute forwarding, final approved/rejected examination and later PLA/RT branches. Pending comparison cannot become final completion, source defects cannot become valid-geometry projection, current/source identities stay separate.
7. Runtime acceptance records actual host/package/restore/F04/F05/F06 and conditional F07/sandbox evidence separately from pure/mocks. Preserve baseline158/applicable predecessors and known207-stop/default-PointIds. No production deployment, cadastral promotion or operational11.2 closure inferred.

## Tasks / Subtasks

- [ ] Confirm explicit runtime/host/sandbox scopes and affected owner/policy/task/service proofs. (AC1-7)
- [ ] Implement/review A snapshot/package/restore and exact artifact manifest. (AC1-2)
- [ ] Implement/review B protected projection/receipt with F05. (AC3)
- [ ] Implement/review C ledger/attachments/task reconciliation with F04/F06. (AC4-6)
- [ ] Record actual lanes/limitations and obtain scoped13.9 acceptance. (AC7)

## Developer Context And File Map

Future NEW Toolbox/Handoff and Persistence/Package/remote adapter seams, tests and separate host/sandbox runners. Reuse 13.5B supported snapshot, owner locks/receipts; 13.7/13.8 decision/coverage; existing publishing/upload/lifecycle mechanics only after full call-path/capability review. CaseResumePackageService v1 filters output/lock files and replaces roots; do not reuse that behavior as v2 completeness/restore. Existing publish rebuilds JSON point/line/polygon payloads and per-layer effects, not exact snapshot authority. Existing lifecycle/upload success may not identify remote objects; F04/F05/F06 proof is mandatory.

Exact UPDATE files/test targets/endpoints/schema/limits/credentials and invocation commands must be enumerated/read/reviewed before authorized implementation; no baseline output/package/source/settings/DAML/lifecycle modifications by default. Keep SDK3.6/net8.0-windows/x64 and existing harness; named Program.cs additions preserve substring-OR/fail-fast. Use parent capability build/focused/full lane with obj-toolbox-handoff/ and explicit new groups because full harness may stop before appended tests.

## Test Requirements And Completion Evidence

Cover same revision through all artifacts, old/current/source identity, later same-case task restore, malicious/corrupt archive, stale local/remote parent, two operators, partial layer success, confirmed attachment plus lost response, transition timeout/restart, revoked assignment, pending Compare, source-defect forwarding/rejection, geometry-publication denial, confirmed steps retained after later failure, no duplicate retry or rollback over newer data. No runtime/sandbox/host calls or tests executed by preparation.

## References

- [Architecture handoff](../../planning-artifacts/toolbox/toolbox-architecture.md), [contract ledger/package/projection](../../planning-artifacts/toolbox/toolbox-contract-catalog.md), [A/B/C proposed slicing](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md), [F03-F07](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [D02/D05/D06/D10 policies](../../planning-artifacts/toolbox/toolbox-decision-register.md).
- [13.5](13-5-add-versioned-toolbox-workspace-persistence.md), [13.6](13-6-register-reusable-toolbox-capabilities.md), [Compute](13-7-compose-compute-toolbox-preset.md), [Compare](13-8-compose-read-only-compare-and-correction-flow.md), [project rules](../../project-context.md), [review record](13-9-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/sandbox authorization false; tasks unchecked. No snapshot/package/restore/projection/upload/task code or effects, live proof, build/tests, installation, commit or deployment. Record real evidence only after authorized execution.
