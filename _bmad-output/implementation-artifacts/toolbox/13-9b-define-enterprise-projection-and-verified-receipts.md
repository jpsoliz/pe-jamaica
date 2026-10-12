---
story_key: 13-9b-define-enterprise-projection-and-verified-receipts
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-9-add-revision-consistent-toolbox-handoff
depends_on: [13-9a-define-revision-snapshot-package-and-restore-v2]
execution_gate: explicit-sandbox-mutation-authorization-reviewed-d05-d06-and-actual-f05-capability-proof
implementation_authorized: false
---

# 13.9B - Enterprise Projection And Verified Receipts

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing13.9 entry, no child key.

## Story

As an authorized sandbox examiner, I want exact accepted snapshot geometry projected with enforceable concurrency and read-back, so that partial layers or stale writes cannot appear as a complete published revision.

## Scope And Dependencies

Requires A/host owner/schema/geometry acceptance, reviewed D05 checkpoint and D06 remote-conflict policy, explicit named sandbox mutation target/session and actual F05 capability evidence before enabling writes. Pure mock planning can precede live proof under explicit code authorization. Owns projection payload/target protection/independent durable operation journal/receipt; C later consumes receipts through a narrow interface without an implementation dependency. No authoritative cadastral promotion, task transition, attachment or linked PE mutation.

## Acceptance Criteria

1. Given exact frozen A snapshot, require independently current accepted geometry/all-included inventory and profile/evidence policy. Build points/lines/polygons/catalog from actual saved FGDB snapshot, not old reviewed JSON or fallback approved flags. Include full case/current/source identity, geometry schema/CRS, host revision/parent/digest, operation, stable feature keys and expected prior target revision.
2. Given target service, record actual version/capabilities/schema/layer/profile/field mapping and tested permission scope. Require enforceable remote per-case writer/reservation or conditional expected-parent protection that covers all allowed writers. Local lock/read-before-write/marker/global UUID cannot provide server exclusion; unsupported capability blocks mutation/F05 acceptance.
3. Given protected write, pin exact payload/request before effects, resolve stable local feature-to-server identity mapping and verify every expected layer/entity/count/content/metadata result and read-back before a published receipt. Same-service applyEdits/rollback support is verified, not assumed; cross-service layers never treated as one transaction. Marker-only or HTTP success cannot certify revision completeness.
4. Given timeout/partial layer/error/cancellation/process restart, retain request and known effects, mark ambiguous/partial projection and deny published-current use until read-back resolves target truth. Resume/rollback only through reviewed protected protocol preserving newer/unrelated rows; no delete-and-add replay without proof. Exact historical receipt after successors uses retained revision-qualified evidence, not current marker alone.
5. Given durable retry, same operation/intent returns original verified receipt without resend; changed bytes/target/expected prior conflicts. When response lacks object identity/content or remote dedup/read-back cannot prove outcome, return recovery_required and block next effects. Persist B journal/receipt through accepted local owner without extending A; caller identity is audit, fresh remote authority required for mutations.
6. Given completion, actual isolated-service two-writer/stale-parent/partial/failure/read-back/rollback F05 tests pass plus pure payload/identity tests. Record capability/target schema and exact enabled/deferred policies, preserve predecessors/limits and obtain B acceptance. Read-only Enterprise recovery cannot silently adopt projection as active master.

## Tasks / Subtasks

- [ ] Confirm A/host acceptance, code/remote scope, D05/D06 review and named F05 target capability plan. (AC1-6)
- [ ] Define payload/server identity/expected-parent and standalone durable journal/receipt contract. (AC1,5)
- [ ] Prove real enforceable target writer/conditional commit and actual layer capabilities before writes enabled. (AC2)
- [ ] Implement protected publish/read-back/partial reconciliation without stale rollback. (AC3-4)
- [ ] Run actual F05 matrix plus pure tests and obtain B acceptance. (AC6)

## Developer Context And File Map

Future NEW Toolbox/Handoff projection adapter/receipt and tests plus separate authorized sandbox runner. Reuse owner-qualified FGDB export and existing Enterprise transport/auth only after full call-path review. JsonEnterpriseWorkingLayerPublishService's JSON/fallback/per-layer mechanics cannot be wrapped as exact snapshot publication without reviewed adaptation. No baseline target/config/output/source changes by default; exact UPDATE files/URLs/policies read/reviewed before execution. No ledger-C prerequisite for B scoped proof; C consumes B's verified receipt contract.

## Test Requirements And Completion Evidence

Test actual two operators including outside-process writers, stale target while local snapshot current, identity map replay after successor, per-feature failure, marker succeeds but layer missing, lost response, partial rollback with newer data, unauthorized/source PE scope, no approved geometry and actual schema/profile mismatch. Mock passes are not F05. Parent build/focused/full plus actual sandbox lane separately; baseline158/known207-stop/default-PointIds retained. No mutation/proof executed now.

## References

- [Parent](13-9-add-revision-consistent-toolbox-handoff.md), [A](13-9a-define-revision-snapshot-package-and-restore-v2.md), [C](13-9c-define-handoff-ledger-attachments-and-task-transition.md), [F05](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [D05/D06](../../planning-artifacts/toolbox/toolbox-decision-register.md).
- Esri [Feature Service applyEdits](https://developers.arcgis.com/rest/services-reference/enterprise/apply-edits-feature-service/), checked 2026-10-11; documented API capabilities do not prove configured target concurrency or cross-service atomicity.
- [Review record](13-9-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/sandbox authorization false; tasks unchecked. No projection/publish/remote-lock/receipt implementation, target query/mutation, F05 tests, commit or deployment. Record actual service evidence on execution.
