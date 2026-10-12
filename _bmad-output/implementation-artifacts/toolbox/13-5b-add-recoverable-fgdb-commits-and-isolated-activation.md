---
story_key: 13-5b-add-recoverable-fgdb-commits-and-isolated-activation
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5-add-versioned-toolbox-workspace-persistence
depends_on: [13-5c-add-isolated-prototype-disk-revisions, 13-5e-define-fgdb-schema-and-projection-mapping]
execution_gate: accepted-schema-writer-policy-and-real-fgdb-recovery-edit-snapshot-proof
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5B - Recoverable FGDB Commits And Isolated Activation

Status: backlog
Refinement: retains the previously reserved B identity; final M3 slice, not the next implementation. Tracked through parent, no child key.

## Story

As an examiner in an isolated test environment, I want geometry, evidence catalog and projections published as one verifiable revision with recoverable interruptions, so that save/reopen cannot mix revisions or grant authority to stale evidence.

## Entry Gates And Order

Execution order is A -> C -> D/E -> B; letters are stable story identifiers, not execution order. A accepted. C must be accepted for physical containment/ordinary-file recovery; E exact schema/geometry profile and hosted adapter accepted. D required before any legacy fixture import; synthetic known A/B state can be used for the initial FGDB spike. Review D01/D06/D11 detailed ownership/conflict/rollback policy before this writer. D08 reference scope remains explicit, not inferred.

First perform isolated feasibility proofs F01 (commit failures), F02 (tracked/untracked edits) and F03 (quiesced supported snapshot/read-back/restore). Only after these pass and ownership policy is accepted may the isolated activation adapter be enabled. F07 before any experimental installation; a worktree/build is not installation isolation. Record authorized host/case root, no operational installation/service targets. Remote handoff/package v2 stays 13.9.

## Commit Protocol To Prove

Reuse C's contained filesystem/lock primitives and immutable evidence mechanics, not its prototype contract as an FGDB commit claim. Define a separately versioned host commit envelope with case/store/schema/geometry profile, operation/parent/revision/sequence, actor/fresh local permission class, affected entities/dependencies, semantic input/output digests, exact canonical/source/policy asset inventory, dataset/table manifest, projection inventory and recovery snapshot references. Keep A bytes unchanged inside their owning evidence catalog.

Compute geometry mutations require fresh local geometry permission; Compare current-case tabular changes only. No persisted permission/task/actor grants authority. Linked PE/current RT identities stay separate. Quiesce map edits/tool runs, acquire single CaseStore writer, resolve pending commits and compare expected parent under lock. Reject untracked active changes before mutation. Durable identical retries return verified receipts before stale-parent checks; changed intents conflict.

| State / boundary | Required evidence / behavior |
|---|---|
| prepared | Flushed exact intent plus verified prior immutable recovery snapshot; no new mutation until a complete prior state can be recovered |
| fgdb_saved | SDK/GP operation saved; FGDB metadata binds commit/case/schema/revision and semantic digest. Saved-but-unpublished geometry is explicitly pending, never the old pointer's content |
| catalog_written | Frozen evidence, exact schema/dataset coverage and revision-stamped projections written/read back; FGDB and catalog agree on full identity and logical content |
| pointer_switched | Publish pointer only after all required verification; read back pointer/GDB/catalog agreement before consumer access |
| committed | Verified durable operation receipt and final journal; no duplicate domain audits/IDs on restart |

Inject failure before/after every actual host save, metadata change, catalog/asset/projection flush, pointer replacement and receipt write. There is no atomic transaction across FGDB and files. Resume only if durable evidence proves the complete new revision; otherwise quarantine mutable active data and expose the previous verified immutable snapshot read-only. Snapshot restoration must use supported ArcGIS operations and preserve uncertain evidence; never overwrite active GDB internals piecemeal. A marker alone, successful API return or cached revision does not prove completion.

Prototype C states and host states are different protocols; share primitives but do not rename `sealed` into `fgdb_saved` or mutate A/C contract versions. Bound journal/catalog/inventory and recovery work; cancellation after verified publication returns the receipt plus diagnostic, not a false no-save result. Access denied/disk full/schema lock/GP failure remain explicit pending or quarantined outcomes until read-back resolves them.

### Edit And Snapshot Proof

F02 must cover SDK, GP and direct/manual map edits affecting geometry, table evidence, parcel membership, inclusions and shared edges. Reopen/save/approval/snapshot compare actual semantic inputs with committed evidence, including schema/profile; invalidate actual consuming parcels/checks/decisions. If supported tracking cannot cover an edit path, block it or detect divergence before consumers run. No approval based only on ObjectID, file checksums or unchanged metadata.

F03: save/quiesce all writers/runs, reject unresolved commit or edit disagreement, use supported ArcGIS geodatabase copy to a new immutable snapshot, freeze external documents/rasters/evidence, and read back exact dataset/table/asset coverage, identity/revision and semantic geometry/evidence. Explicitly inventory referenced mosaic/raster assets; supported Copy may not copy external images automatically. Prove restore into a fresh isolated root and a distinct current caller context. No raw live-folder ZIP/copy, lock-file skipping or package-v1 root replacement. Unsupported referenced assets block complete-snapshot claims.

Draft/invalid datasets remain separate. Forwarding with source defects is not valid geometry; approval-dependent consumers read a named immutable accepted snapshot and all included parcels, never selected scope alone. The default-PointIds domain hash limitation needs a separately reviewed domain resolution before affected geometry can be approved; don't normalize it in persistence.

### Isolated Ownership Activation

Only after proofs/reviews: explicit case-level activation records new owner/schema/revision, protects active FGDB and immutable evidence, and routes allowed local mutations through one CaseStore. Compatibility projections are read-only and revision-qualified. Reject legacy independent writer calls to activated targets; don't dual-write JSON and FGDB. Existing baseline cases/writers stay unchanged. The exact narrow operational adapter UPDATE set must be enumerated and read in full during activation refinement; no broad legacy call-site rewrite authorized by this story.

Rollback stops writers/consumers, retains new-only data and evidence, and returns to untouched baseline cases/build. Never claim the old build reads the new schema; no downgrade without separately verified mapping. Unknown activation/rollback outcomes block editing and export. Isolated activation acceptance is not production release approval.

## Acceptance Criteria

1. Given reviewed schema/profile, permissions and parent, isolated save/reopen binds FGDB metadata, actual content, frozen catalog, projections, pointer and receipt to exactly one revision; preserving full accepted A/B history.
2. Given competing/stale writers, read-only Compare/source PE or untracked edits, mutation fails closed with no hidden authorization/merge; dirty/partial active data is honestly marked pending/quarantined.
3. Given real FGDB failures at every state, F01 evidence proves verified resume or previous immutable read-only recovery; no saved-but-unpublished geometry appears as clean old revision.
4. Given actual SDK/GP/manual edits, F02 blocks stale approval/export and identifies every dependent included/excluded/retired consumer via accepted dependency rules; no marker-only success.
5. Given snapshot/restore, F03 proves quiescence, complete frozen asset/table/dataset coverage, supported ArcGIS copy and read-back; pending commits and untracked/unsupported assets reject snapshot.
6. Given restart/retry after successors, same operation returns exact verified receipt without duplicate events; changed intent conflicts. Cancellation/flush failures report verified or ambiguous outcomes accurately.
7. Given explicit isolated activation after all gates, one writer owns state, legacy consumers cannot independently save, projections are read-only, rollback retains baseline untouched and new schema evidence recoverable.
8. Given completed verification, report real F01/F02/F03 evidence and F07 if installation separately from headless tests; preserve predecessor groups and full-suite/default-array limits. No production/remote/11.2 closure inferred.

## Tasks / Subtasks

- [ ] Review detailed host commit/writer/activation/rollback contract, accepted C/E, D if import used, geometry profile and test host isolation; enumerate exact adapter UPDATE targets. (AC1-2,7-8)
- [ ] Implement bounded host journal/catalog, verified FGDB commit/save/read-back adapter and durable receipt/recovery routing using C primitives. (AC1-3,6)
- [ ] Prove F01 with real host faults at all boundaries, including prior snapshot unavailable and active/pointer disagreement. (AC3,8)
- [ ] Prove F02 with actual SDK/GP/manual table and geometry edits, shared edge impacts and approval/export blockers. (AC2,4,8)
- [ ] Prove F03 supported snapshot plus fresh-root restore, referenced assets, pending rejection and source immutability. (AC5,8)
- [ ] After proofs, implement narrowly reviewed isolated activation/legacy-write guard and rollback; never install on operational target. (AC7)
- [ ] Run scoped/headless/full/host lanes, independent GIS/ownership review, record limitations and request parent acceptance only when all parent ACs covered. (AC8)

## Dev Notes / File Map

NEW host persistence/recovery/snapshot/activation adapters under `Workflow/Toolbox/Persistence/ArcGis/`; NEW headless protocol tests and separate hosted evidence runner/scenarios. REUSE C contained filesystem/locks, E schema adapter and A codec. No generic fake geometry pass can substitute for hosted F01-F03. UPDATE `Program.cs` only for headless registrations initially; broader activation files remain gated until full source/read-boundary review. No source adapters, output_adapter.py, package-v1 restore, settings, registration or remote dispatch changes by default.

## References

- [Parent AC1-18 and commands](13-5-add-versioned-toolbox-workspace-persistence.md), [C](13-5c-add-isolated-prototype-disk-revisions.md), [D](13-5d-add-read-only-copied-case-import.md), [E](13-5e-define-fgdb-schema-and-projection-mapping.md).
- [F01-F07 acceptance strategy](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [architecture authority protocol](../../planning-artifacts/toolbox/toolbox-architecture.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md).
- Checked 2026-10-11: Esri [geodatabase handling](https://doc.esri.com/en/arcgis-pro/latest/help/data/geodatabases/manage-file-gdb/file-geodatabases-and-windows-explorer.html), [Pro 3.6 Copy tool](https://pro.arcgis.com/en/pro-app/3.6/tool-reference/data-management/copy.htm). These are API constraints, not successful project snapshot evidence.

## Dev Agent Record

Prepared 2026-10-11; not implemented; no hosted tests, deployment or authority switch performed. All tasks and named proof gates remain open.
