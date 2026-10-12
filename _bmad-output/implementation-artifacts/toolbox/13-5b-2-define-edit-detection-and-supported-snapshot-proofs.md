---
story_key: 13-5b-2-define-edit-detection-and-supported-snapshot-proofs
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5b-add-recoverable-fgdb-commits-and-isolated-activation
depends_on: [13-5b-1-define-host-commit-and-recovery-protocol]
execution_gate: explicit-host-authorization-and-b1-acceptance
implementation_authorized: false
---

# 13.5B.2 - Edit Detection And Supported Snapshot Proofs

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As an isolated Toolbox developer, I want actual edit-divergence detection and supported complete snapshots/restores, so that stale approval or incomplete evidence cannot reach downstream consumers.

## Scope And Dependencies

Requires accepted B.1 plus C/E and explicit host/root authorization; D if importing legacy fixtures, F07 if installed. Owns F02 edit detection and full F03 quiesced supported copy/read-back/fresh-root restore, extending B.1's narrow baseline mechanism. No activation, production writer guards, packages/remote or domain hash repair. [B parent](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md) remains policy source.

## Acceptance Criteria

1. Given actual SDK, GP and direct/manual edits, compare actual geometry/table/membership/inclusion/shared-edge/policy/schema inputs against B.1 committed inventory before save/reopen/approval/snapshot consumers. Every editable path is tracked or divergence-detected; an unsupported path is blocked or prevents clean consumer access. Marker/OID/file checksum alone is insufficient.
2. Given changed shared dependencies, use accepted old/new dependency union to invalidate consuming checks/decisions for included/excluded/retired parcels; unrelated current checks survive only when bindings match. Preserve cumulative findings/manual history; no automatic snapping/repair or stale-proof refresh.
3. Given F03 snapshot request, save/quiesce every writer/tool, reject pending commit/edit disagreement and copy via supported ArcGIS operation to a new immutable target. Freeze complete external source/policy/document/raster inventory. Referenced mosaic/raster assets must be explicitly copied/verified or diagnosed unsupported; a successful Copy call is not a complete-snapshot claim.
4. Given snapshot read-back, validate exact case/schema/profile/host commit, datasets/fields/keys/order/content and complete asset inventory with semantic geometry digests. Restore through supported operations into a fresh isolated root, using fresh caller context and policy; immutable original/operational targets remain unchanged. No live-folder ZIP, lock-file skipping or package-v1 root replacement.
5. Given invalid/draft/forwarded-defect/default-PointIds state, snapshots may preserve permitted inspection evidence but accepted geometry is unavailable without current full-case domain approval and independently supported geometry proof. Linked PE is immutable. Unsupported domain hashing remains a blocker for affected approval, not a normalization opportunity.
6. Given failure/cancellation/locks during save/quiesce/copy/read-back/restore, retain uncertain evidence and return pending/quarantined/unsupported outcomes; never make partial target accepted or overwrite previous snapshot. Record actual divergent inputs and fixture limitations without raw sensitive payload logging.
7. Given completion, real F02/F03 scenario matrix and independently verified fresh-root restore pass under named GIS review, separate from pure tests. Preserve baseline/predecessor groups and known harness limits. Scoped B.2 acceptance is prerequisite for B.3; no activation/production or full parent completion inferred.

## Tasks / Subtasks

- [ ] Confirm authorization, B.1 acceptance and host/edit/copy capability matrix. (AC1-7)
- [ ] Implement actual divergence detection and accepted dependency invalidation adapter. (AC1-2)
- [ ] Implement quiesced supported snapshot and complete asset inventory/read-back. (AC3-4)
- [ ] Implement fresh-root restore with current context and conservative failures. (AC4-6)
- [ ] Prove F02/F03 real edit/copy/restore scenarios and obtain B.2 acceptance. (AC7)

## Developer Context And File Map

Future NEW edit/snapshot/restore adapters under Persistence/ArcGis/, pure tests and hosted F02/F03 scenarios. Reuse B.1 inventory/receipt/host protocol, E verified schema and accepted dependency services; any narrower integration amendment must preserve their prior evidence. No broad legacy approval/output/package adapter updates. Full source read and exact update set required for any future narrow consumer blocker. Keep SDK thread/resource boundaries and normal harness behavior.

## Test Requirements And Completion Evidence

Actual host cases include shared-edge edit, nongeometry table change, membership/inclusion changes, independent parcel, external/manual GP paths, untracked edits before snapshot, pending commit, missing external raster, copy failure/lock, schema mismatch and fresh-context restore. Record source/target content/semantic digests and incomplete capabilities. Use parent lanes plus explicit hosted runner; no F02/F03 experiments run now.

## References

- [B parent](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md), [B.1](13-5b-1-define-host-commit-and-recovery-protocol.md), [B.3](13-5b-3-define-isolated-activation-and-rollback.md), [F01-F07](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md).
- Esri [Pro 3.6 Copy behavior](https://pro.arcgis.com/en/pro-app/3.6/tool-reference/data-management/copy.htm), checked 2026-10-11; successful API copy is not project F03 evidence.
- [Project rules](../../project-context.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/host authorization not granted; tasks unchecked. No edits, snapshots, restored case, GIS tests, installation, commit or deployment performed. Record execution evidence later.
