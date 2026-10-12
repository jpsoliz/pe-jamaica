---
story_key: 13-5d-2-define-sealed-import-preparation
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5d-add-read-only-copied-case-import
depends_on: [13-5d-1-define-source-freeze-mapping-and-import-plan]
execution_gate: explicit-runtime-authorization-and-d1-acceptance
implementation_authorized: false
---

# 13.5D.2 - Sealed Import Preparation

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As a migration-fixture developer, I want an immutable sealed preparation containing the exact future save request, so that restart never replans or reallocates imported identities.

## Scope And Dependencies

Requires accepted D.1 and C plus explicit runtime authorization. Owns preparation records, isolated durable files/seal and verified resume. D.1 owns mapping; D.3 creates/executes target. The [D parent](13-5d-add-read-only-copied-case-import.md) is the contract source. No target store creation/publication, source reread, mapping changes, FGDB or external authority.

## Acceptance Criteria

1. Given externally accepted plan digest and owned D.1 bytes, PrepareImport allocates/pins once target store_id, operation_id, proposed_revision_id, null expected_parent, actor, exact prepared_at, full target case, bundle, provenance/source assets and C request_digest. All domain IDs already belong to the plan. C Create must accept the preselected store_id later; if its accepted API cannot bind that ID, record an integration blocker rather than change the frozen request at Execute.
2. Given an explicitly authorized preparation root, verify separation from source/new target and exclusively claim import-preparations/<operation_id>. Use C's safe create-new/lock/flush/read-back primitives with a separate strict preparation owner; never adopt an existing unowned directory. An existing operation with conflicting intent fails; retain incomplete initialization/evidence.
3. Given preparation metadata, exact fields/version/nullability, inventory, hashes and request bindings are strict and bounded. Write pinned intent before payload writes, freeze/read-back all bytes and A decode, then publish a create-new completion seal binding immutable preparation digest and C request_digest. A seal proves byte completeness only, never accepted geometry/authority. Mutable acceptance attestations are supplied again externally, not persisted as grants.
4. Given restart, Resume accepts only complete matching sealed metadata/payloads and reconstructs byte-identical C request without ID/time allocation, source discovery or recapture. Source loss after seal is allowed. An incomplete preparation cannot execute; an explicit recovery may seal only fully retained matching bytes under the original pinned intent, otherwise retain/abandon it and require a new operation ID. Never repair by replacing a partial payload or regenerating a map.
5. Given cancellation or failure before first operation-specific write, create nothing; afterward retain and classify evidence honestly. Fault tests terminate before/after owner/intent/payload flush/read-back/seal; contradictory/multiple seals and altered bytes fail. Exclusive preparation lock survives only process lifetime, with no TTL/force unlock.
6. Given limits and retained abandoned/preparation debris, charge C count/entry/depth/metadata and aggregate asset bounds plus D plan limits. Byte inventory includes bundle, unique original-source assets, plan/provenance and any sidecars before sealing or target work. Retain separate artifact path/role references when identical bytes share one verified C asset; never duplicate digest entries in C's inventory. Required sidecars consume the same 256-asset/512-MiB budget, so 256 selected unique source files plus sidecars are explicitly too large. No GC or loss of source evidence to fit limits. Secrets are rejected before fixtures and raw bytes never logged.
7. Given completed runtime work, independent tests demonstrate deterministic sealed restart, source loss, incomplete/corrupt preparation, changed actor/time/store IDs, two processes and unchanged sources/target absence. Record actual lanes and obtain D.2 acceptance; sealed preparation alone is not an import receipt.

## Tasks / Subtasks

- [ ] Confirm authorization, D.1/C acceptance and future Create store_id binding. (AC1-7)
- [ ] Define strict preparation/owner/seal records and manifest digest fixtures. (AC1,3,6)
- [ ] Implement isolated exclusive preparation and complete payload/seal publication. (AC2-3)
- [ ] Implement verified Resume and conservative incomplete/abandoned outcomes. (AC4-5)
- [ ] Add fault/process/limits/source-loss tests and scoped acceptance evidence. (AC5-7)

## Developer Context And File Map

NEW proposed `Import/ToolboxImportPreparation.cs` with disk records separated from A and C locked formats; NEW `Workflow/ToolboxImportPreparationTests.cs`. Reuse D.1 plan bytes/maps and C contained sessions; no new legacy writer or planner. Future Program.cs updates limited to registrations/accepted reserved child-worker scenarios after full read. Do not alter A DTO/schema, source/root repair, package restore or operational call sites.

## Test Requirements And Completion Evidence

Use parent D commands and existing harness; explicitly run preparation groups because full-suite fail-fast may skip appended groups. Preserve 158 baseline plus accepted C/D.1 groups and default-PointIds inspection. Completion needs pinned record/hash fixtures, actual disk/process interruption evidence, target-absence and source-byte assertions, review and scoped acceptance. Preparation of this story executes no filesystem experiments.

## References

- [D contract](13-5d-add-read-only-copied-case-import.md), [D.1](13-5d-1-define-source-freeze-mapping-and-import-plan.md), [D.3](13-5d-3-define-import-execution-and-retry.md), [C](13-5c-add-isolated-prototype-disk-revisions.md).
- [A evidence](13-5a-implementation-evidence.md), [project rules](../../project-context.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; tasks unchecked. No preparation bytes/owner/seal, source reads, runtime tests, commit or deployment created. Fill execution evidence when authorized.
