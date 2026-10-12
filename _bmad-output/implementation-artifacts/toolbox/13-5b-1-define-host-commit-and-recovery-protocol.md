---
story_key: 13-5b-1-define-host-commit-and-recovery-protocol
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5b-add-recoverable-fgdb-commits-and-isolated-activation
depends_on: [13-5c-add-isolated-prototype-disk-revisions, 13-5e-define-fgdb-schema-and-projection-mapping]
execution_gate: explicit-host-authorization-accepted-c-e-and-reviewed-host-contract
implementation_authorized: false
---

# 13.5B.1 - Host Commit And Recovery Protocol

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As an isolated Toolbox developer, I want verifiable FGDB/file publication with honest recovery outcomes, so that interrupted host saves cannot masquerade as a clean revision.

## Scope And Dependencies

The [B parent](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md) owns host policy. Requires accepted C/E, D if legacy fixtures are used, reviewed D01/D06/D11 writer/schema/rollback contract, explicit host/root/profile and fresh local permission. F07 precedes installation. Owns host envelope/journal/receipt protocol, F01 faults and minimal verified baseline snapshot; B.2 owns complete edit/snapshot F02/F03 proof, B.3 isolated activation. No operational routing, package v2 or remote effect.

## Acceptance Criteria

1. Given E's accepted metadata ownership, freeze a separate versioned host envelope with host case/store/schema/profile/operation/parent/revision/sequence, immutable intent/audit, semantic geometry/table/payload/projection inventory and recovery references. C store/revision/request/receipt remains immutable source provenance, not host authority; A bytes stay unchanged. Any new field ownership incompatible with E requires E catalog/adapter re-review before affected host writes.
2. Given first/subsequent host commit, obtain a supported immutable baseline copy and independent read-back of the complete previous state before mutating active FGDB. For first commit this is the explicitly initialized reviewed empty-schema or supplied synthetic baseline, recorded as baseline-only evidence with no invented accepted revision/receipt. Quiesce writers and freeze all required assets. Missing/unverified baseline blocks mutation. This limited bootstrap permits F01; it does not claim full B.2 F03 coverage.
3. Given frozen intent and expected parent, quiesce SDK/GP/tool edits, acquire single contained CaseStore writer and reject untracked changes. Persist prepared intent/baseline, then fgdb_saved, catalog_written, pointer_switched and committed evidence with actual read-back. Metadata/marker/API success alone cannot prove full content. Saved-but-unpublished geometry is pending, never the old pointer's content.
4. Given complete unpublished new revision, explicit forward Recover may publish only with exact pinned intent, matching actual FGDB/catalog/assets/projections and independently current permission/expected bindings under lock. Incomplete or contradictory data is quarantined; expose only verified immutable previous baseline read-only. Restoration uses supported ArcGIS operations into fresh verified recovery targets, never piecemeal overwrite of live GDB internals.
5. Given retries after successors, verify an exact host receipt against immutable host ancestry/snapshot/catalog/intent and that operation's full inventory, not the later mutable active GDB alone. Missing historical tail requires explicit reconstruction from those proofs without moving current. Off-chain/unpublished evidence is not committed. Changed frozen intent conflicts; no domain IDs/audits or prior context are regenerated.
6. Given fresh caller permission changes, persisted actor/permission class is immutable audit included in original intent digest, not authority. Current authorization is separate nonpersisted execution input and never rewrites that digest on retry. Receipt inspection has no mutation effect; new mutation or recovery publication requires current allowed scope. Compare geometry/source PE writes remain denied regardless of saved class.
7. Given real faults/cancellation at every host save/metadata/catalog/asset/projection/pointer/receipt/journal boundary, F01 proves complete verified resume or conservative quarantine/previous immutable inspection. After first disk/host mutation, never report nothing-saved from an exception. After verified publication, committed requires verified receipt/terminal tail; missing tail returns explicit published-but-recovery-required, not false committed. C states/contracts are not renamed into host states.
8. Given completion, actual F01 matrix, baseline method/read-back, host authority/digest schema and headless/host lanes are reviewed and accepted. Preserve baseline 158/applicable predecessors and known full-suite/default-array limits. B.1 acceptance does not pass F02/F03, activate ownership or complete B/13.5.

## Tasks / Subtasks

- [ ] Confirm host authorization, accepted C/E, conditional D and named writer-policy reviews. (AC1-8)
- [ ] Freeze host namespace/envelope/authority/digest/ancestry contracts consistent with E. (AC1,5-6)
- [ ] Establish and verify the minimal supported immutable previous-state baseline before mutation. (AC2)
- [ ] Implement host save/read-back/journal/pointer/receipt and explicit recovery. (AC3-5)
- [ ] Prove F01 with actual host faults, late cancellation and missing baseline/tail. (AC7)
- [ ] Review separate lanes and obtain B.1 acceptance. (AC8)

## Developer Context And File Map

Future NEW host contract/commit/recovery adapters under Workflow/Toolbox/Persistence/ArcGis/, headless tests and hosted F01 scenarios. Reuse C containment/lock primitives, E schema/materialization and unchanged A codec. Exact SDK/GP calls and runner/thread/resource ownership must be documented and checked in the installed Pro3.6 lane; no fake geometry substitute. Initial Program.cs updates only register headless tests after full read. No activation/legacy output/package/source/settings/DAML/remote changes in B.1.

## Test Requirements And Completion Evidence

Run B/C build/focused/full plus separately authorized hosted F01 runner. Include first empty baseline, missing/corrupt snapshot, disk-full/lock/GP failure, pointer/active disagreement, complete unpublished recovery, historical receipt after successor and revoked current permission. Forced process/host boundaries must be observed, not inferred from mocked exceptions. Record exact host profile/schema, inventory and recovery outcome. No host experiments executed by preparation.

## References

- [B contract](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md), [C](13-5c-add-isolated-prototype-disk-revisions.md), [E](13-5e-define-fgdb-schema-and-projection-mapping.md).
- [B.2 proofs](13-5b-2-define-edit-detection-and-supported-snapshot-proofs.md), [B.3 activation](13-5b-3-define-isolated-activation-and-rollback.md), [F01-F07](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/host authorization not granted; all tasks unchecked. No host envelope implementation, baseline snapshot, fault experiment, installation, commit or deployment created. Fill actual evidence on execution.
