---
story_key: 13-9a-define-revision-snapshot-package-and-restore-v2
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-9-add-revision-consistent-toolbox-handoff
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-7-compose-compute-toolbox-preset, 13-8-compose-read-only-compare-and-correction-flow]
execution_gate: explicit-runtime-host-authorization-accepted-f03-and-reviewed-package-policy
implementation_authorized: false
---

# 13.9A - Revision Snapshot, Package And Restore V2

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing13.9 entry, no child key.

## Story

As an examiner, I want a complete immutable case package from a named snapshot and safe fresh-root restore, so that resumed evidence remains consistent without copying a live geodatabase or granting old permissions.

## Scope And Dependencies

Requires accepted affected 13.5 host snapshot/owner/F03, Compute/Compare inputs when consumed, D10 package inclusion/retention/limit review and explicit runtime/host authorization. Owns v2 manifest/build/verify/staged restore, not upload or server state. Package kinds draft/suspend/examination/handoff and task requirements are reviewed, not guessed. [Parent](13-9-add-revision-consistent-toolbox-handoff.md) owns same-revision rules.

## Acceptance Criteria

1. Given exact verified owner snapshot and policy, freeze full case/schema/profile/revision/parent/digests, source/reference/geometry/table/policy/review/finding/inclusion/decision inventory and exact package/report/task-artifact bytes. Quiesce through supported snapshot owner; never ZIP live GDB or skip locks as snapshot proof. Report/evidence rendering reads only this snapshot and receives its input binding. Incomplete/pending/forwarded-defect data is labeled, not promoted to accepted geometry.
2. Given v2 manifest, contract/version/required-nullable fields and all relative path/role/length/SHA-256 inventories are strict with deterministic digest fixtures. Distinguish source-case/model IDs, host revision, package ID/digest and creating task observation. Required source/evidence/reference inventory is complete per reviewed policy; no omitted asset to fit size or conditional legacy full-output heuristic.
3. Given package build/read, reviewed explicit D10 limits bound compressed/uncompressed bytes, file count/depth/metadata/expansion work and per-asset allocation before parsing/extraction; exact values and overflow behavior must be pinned before adapter enablement. Reject traversal/absolute/device/ADS/links, duplicate/casefold-colliding paths, contradictory directories, bombs and malformed/unknown version/metadata. Structured ZIP/parser plus accepted containment primitives, no ad hoc safe-path assumption.
4. Given restore into explicitly new isolated staging root, validate all bytes/full case/environment/revision/lineage/schema/profile/assets and read-back before any activation. Later same-case actual task may differ from creating_task_id; reacquire current task/permissions/policy separately. Wrong case, newer local/remote conflict, unsupported geometry or incomplete inventory blocks automatic activation; no root overwrite or latest-file fallback.
5. Given actual FGDB restore, use supported owner/ArcGIS fresh-root mechanism, preserve original case/package and source PE. Activation is explicit only after accepted writer/ownership checks, not automatic during extraction. Legacy v1 loader/TR/task checks remain unchanged; any legacy import routes through accepted D, never relabel old flags approved. Old build is not promised to read v2.
6. Given interruption/cancellation/build/verify/restore failure, retained immutable inputs stay intact and partially staged output is not a valid package/active case. Return typed pending/unsupported/corrupt outcomes; no arbitrary cleanup of uncertain evidence or misleading rollback/downgrade. A verified package is local artifact only, not upload/server receipt.
7. Given completion, actual supported FGDB snapshot/package/fresh-root round-trip plus pure archive/inventory tests pass under scoped review. Preserve applicable predecessor lanes/limits, record D10/F03 evidence and unsupported assets, and obtain A acceptance; no remote proof or production migration inferred.

## Tasks / Subtasks

- [ ] Confirm runtime/host authorization, accepted F03/owner and exact D10 package policy/limits. (AC1-7)
- [ ] Define versioned manifest/inclusion/identity/digest fixtures and snapshot-qualified report/artifact plan. (AC1-2)
- [ ] Implement bounded safe package build/verify and contained staged restore. (AC3-4)
- [ ] Integrate supported owner fresh-root read-back/explicit activation and preserve v1. (AC5-6)
- [ ] Run pure/host round-trip/fault tests and obtain A acceptance. (AC7)

## Developer Context And File Map

Future NEW Persistence/Package/ToolboxCasePackageV2.cs and restore verifier plus tests. Reuse accepted B snapshot/restore and bounded contained filesystem; keep A codec bytes unchanged. Reference CaseResumePackageService only after complete source/test read: its task-match/root-replacement/lock filtering behavior must remain baseline-only, not leak into v2. Any compatibility/update seam enumerated/read/reviewed before coding. No upload/lifecycle/Enterprise/settings/DAML call by default.

## Test Requirements And Completion Evidence

Parent lane plus actual F03-backed snapshots. Test complete source/geometry/finding bytes, missing external raster, draft pending classification, creating-task mismatch same case, wrong full identity, active conflict, symlink/junction/ZIP duplicate/case alias/path collision/bomb, rejected outcome package, report stamp mismatch and canceled staging. Record actual validated limits and UI/host invocation separately; baseline158/known207-stop/default-PointIds retained. No tests/package created now.

## References

- [Parent](13-9-add-revision-consistent-toolbox-handoff.md), [B projection](13-9b-define-enterprise-projection-and-verified-receipts.md), [C ledger](13-9c-define-handoff-ledger-attachments-and-task-transition.md), [13.5B F03](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md).
- [Package outline](../../planning-artifacts/toolbox/toolbox-contract-catalog.md#case-package-v2), [D10 decisions](../../planning-artifacts/toolbox/toolbox-decision-register.md), [review record](13-9-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization false; tasks unchecked. No v2 implementation/snapshot/package/report/restore/activation, tests, commit or deployment. Exact policy/limits remain future reviewed gate.
