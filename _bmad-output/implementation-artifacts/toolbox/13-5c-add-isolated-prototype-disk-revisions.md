---
story_key: 13-5c-add-isolated-prototype-disk-revisions
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5-add-versioned-toolbox-workspace-persistence
depends_on: [13-5a-add-storage-contract-and-hydration-foundation]
execution_gate: scoped-prototype-path-and-writer-contract-review
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5C - Isolated Prototype Disk Revisions

Status: backlog
Refinement: executable candidate; proposed contract below awaits scoped review before implementation. No new tracker key; tracked through parent 13.5.

## Story

As a Toolbox developer, I want immutable byte-bundle revisions with exclusive local saves and deterministic reopen, so that the accepted codec can be tested on disk without moving operational or FGDB authority.

## Boundary And Entry Gate

Implement only after review of this prototype's path, lock, recovery and conflict policies. This is not acceptance of D01/D06 production ownership. Use `temp/toolbox-worktree`, branch `refactor/toolbox-case-workspace`, disposable sanitized cases under an explicitly supplied isolated test root. Verify root separation physically, not just by string comparison. No existing production case roots, UI call sites, registration, remote operations, GDB operations or approval/export integration.

Reuse accepted 13.5A Capture/Encode/Decode unchanged. Stored bundles still say `prototype_bundle`; an ordinary-file receipt means bytes were durably published in this prototype, not that geometry or examination is accepted. A's DTO field/type/nullability/enum lock must not change. B's in-memory Revision and archive snapshot IDs are not disk revision IDs.

## Proposed Disk Contract

New contract `toolbox_prototype_disk_v1`, schema 1, JSON snake_case. Keep it outside A's locked envelope. Strict duplicate/unknown-field rejection; no permissive migration. Paths are derived by the store, never supplied by JSON. IDs for operation/revision are lowercase GUID `N` strings supplied/allocated once at preparation and retained across retry; digests are lowercase 64-hex SHA-256. Case identity uses A's full tuple and comparers.

Layout under an exclusively created test-case directory:

```text
toolbox/prototype-store/
  owner.json
  writer.lock
  current.json
  journal/<operation_id>.json
  staging/<operation_id>/
  revisions/<revision_id>/revision.json
  revisions/<revision_id>/bundle.json
  revisions/<revision_id>/assets/<sha256>.bin
  receipts/<operation_id>.json
```

`owner.json` binds contract, case, store_id and prototype mode. It is not a user permission grant or cryptographic trust root. A preexisting nonempty directory without a valid owner is rejected, never adopted/overwritten. Expected identity and isolated-root authorization arrive externally on every open.

| Record | Required fields |
|---|---|
| Save request | case, operation_id, proposed_revision_id, expected_parent (null only for empty store), actor, prepared_at (round-trip offset timestamp), owned bundle bytes, owned optional asset bytes |
| Immutable revision | contract/schema, store_id, case, operation_id, revision_id, parent (id + digest, or null), sequence (1 then parent+1), actor, prepared_at, bundle (sha256 + byte_length), ordered assets (sha256 + byte_length), request_digest, revision_digest |
| Pointer | contract/schema, store_id, case, revision_id, revision_digest, sequence; null pointer only means uninitialized when no published evidence exists |
| Journal | complete immutable revision intent, preserved previous pointer (null only for initial empty store), state (`prepared`, `sealed`, `published`, `committed`, `abandoned`), nullable abandonment audit (actor, at, reason); mutable progress/audit is not part of revision digest |
| Receipt | store_id, case, operation_id, request_digest, revision_id, revision_digest, sequence; can be reconstructed only from a verified published revision and matching intent |

Define request/revision hash canonicalization in a pure manifest helper with pinned field/order fixtures. Include every intent field and ordered asset inventory; exclude the hash's own field and progress. Compare exact bundle/asset bytes separately. Empty vs null inventory is not interchangeable. Metadata IDs are storage IDs; no domain IDs/events/timestamps are generated during reopen.

Assets are opaque ordinary files, optional, content-addressed, owned and frozen with the revision; no GDB directories, symlinks, executable invocation or remote fetching. Their use is transport-only until D supplies provenance. Reject source secrets before fixtures are admitted; don't log raw bundle/asset content.

Default disk bounds, injectable downward for tests: metadata file 1 MiB, assets 256, each asset 64 MiB, aggregate revision bytes 512 MiB including A bundle, revisions/journals/receipts each 10,000 entries, metadata traversal 64 MiB total. A's own bundle/payload/work limits also apply. Check lengths before allocation and stream digest verification; never raise limits silently. No automatic retention/GC or full-history payload hydration; verify selected revision payload and bounded ancestry metadata. Missing/corrupt ancestry blocks a clean writable open.

### Containment And Writer Protocol

Local fixed NTFS test storage only for v1. Reject UNC, remote/mapped drives, reparse points and explicitly identified synchronization-managed roots; caller must attest the approved unmanaged test root. Capability checks and actual Windows filesystem tests are required, not assumptions about a drive letter. Privileged malicious filesystem manipulation is outside the guarantee; ordinary ancestor/leaf replacement races must be prevented or fail closed.

The filesystem boundary must pin and inspect the ancestor chain and actual file handles for the operation lifetime. Candidate Windows mechanism: directory handles using backup-semantics/open-reparse-point flags, final-path/volume identity checks, deny delete-sharing on pinned ancestors, and create-new leaves. Validate existing leaf handles and reject reparse/hard-linked writable files; never truncate an arbitrary existing leaf. Merely checking attributes before a later path-based write is insufficient. If a safe creation/replacement protocol cannot be demonstrated, stop with `unsafe_path` rather than fall back to lexical checks. Narrow SafeHandle/PInvoke code may live in one platform adapter; no framework upgrade.

Acquire `writer.lock` with OS-enforced exclusive sharing and retain the handle through publication/recovery. Lock file existence, PID or timestamp is not a lease; no TTL deletion/force-unlock. Process death releases handles, then the next owner inspects durable state. Two independent processes must contend in tests. Under lock, validate owner, reconcile unfinished intents, read current pointer, and compare expected parent id/digest/sequence. No automatic merge, last-writer-wins or stale-parent retry with a changed parent.

First inspect operation ID across verified receipts, journals and revision metadata: identical request returns its receipt even if later saves advanced the pointer; conflicting intent returns `operation_conflict`. Do this before rejecting the original expected parent on an already-published retry. An uncommitted identical retry resumes only its pinned original intent after recovery, never reallocates IDs.

### Publication And Recovery

1. Validate A bundle against expected case and freeze all bytes before I/O. Acquire physical containment and writer handles; verify expected parent and operation identity.
2. Create and flush prepared intent before staged bytes. Write create-new staged files, flush to disk, read back sizes/digests and decode A again. Seal immutable revision into its new revision location; journal becomes sealed. No overwrite of a revision or its assets.
3. Publish `current.json` using a tested same-volume sibling-file replacement (create-new for initial pointer), retaining the previous verified pointer as recovery evidence before replacement. Flush required files; read back pointer and revision before reporting publication. File replacement is not a multi-file transaction.
4. Write/read-back receipt and finalize journal. Return `committed` only with verified evidence. Cancellation before publication leaves a pending intent and old pointer; after verified publication return the committed receipt with a late-cancellation diagnostic, not a false "nothing saved" result.

Reopen states: `empty`, `verified`, `busy`, `pending`, `recovered`, `quarantined`, `unsupported`, `corrupt`. Reads never silently mutate; explicit Recover acquires the same writer lock. If old pointer is intact and a new revision is sealed but unpublished, retain old revision read-only and require explicit resume/abandon of that exact intent. If pointer names the fully verified new revision and intent, reconstruct missing tail/receipt idempotently. Torn/missing pointer, contradictory metadata, competing intents or missing bytes remain quarantined; never choose newest file or advance from sequence alone. Expose a previous verified revision read-only only when its preserved pointer and complete inventory validate.

Explicit Abandon runs under the same lock and requires the current pointer to equal the verified preserved parent (or verified uninitialized store), with no evidence that this operation was published. Published or ambiguous intent cannot be abandoned. Atomically replace/flush/read-back the journal with state `abandoned` and non-null caller-supplied actor/time/reason audit; retain staged/sealed bytes and IDs. Only after verified abandonment and no other unresolved intents may a fresh operation save against that unchanged parent. A failed/torn abandonment stays pending/quarantined until recovery. Retry of the original save returns `operation_abandoned`, never resurrects it; repeated identical abandon is idempotent, changed abandonment audit conflicts. No receipt pretends an abandoned operation committed and no arbitrary recursive cleanup occurs. Include abandoned operations in bounds/collision checks.

This slice proves process-interruption recovery, not hardware power-loss durability. Publish/flush failures have ambiguous outcomes until read-back/reopen resolves them. Unresolved intent or corruption blocks further save. A verified prototype reopen grants no fresh context/permissions, trusted policy, geometry proof or domain approval.

## Acceptance Criteria

1. Given valid accepted A/B fixtures, save/reopen preserves all A fields, exact bytes, comparers, IDs and stale/pending states; decode uses external case identity/trusted pins and never replays domain actions.
2. Given invalid ownership, identity, unsafe paths, reparse/race/hard-link fixtures or unsupported storage, open/save/cleanup fails without changing any protected or outside-root file.
3. Given two processes or stale expected parent, exactly one permitted save wins; the loser gets busy/conflict and cannot alter winner bytes, pointer or receipts.
4. Given repeated operation ID after restart, identical intent returns the same verified receipt, including after later commits; changed bytes/assets/parent/actor/time/IDs return conflict without new audit or entity allocation.
5. Given injected failures before/after every write, flush, seal, pointer replacement, receipt and abandonment boundary, reopen/recovery deterministically verifies old/new state or quarantines it; verified abandonment permits only a new operation against the unchanged parent, never resurrection or deletion of uncertain evidence.
6. Given oversize/deep/duplicate/unknown/corrupt metadata, absent policy/source assets or modified published bytes, bounded typed diagnostics prevent clean writable open. Missing trusted policy remains A's inspection diagnostic, not integrity success as authority.
7. Given relocation of a copied prototype store, externally validated new root rebases derived paths while original identity/store/revision stays intact; no saved permissions are restored.
8. Given completed implementation, report build, pure protocol tests, actual NTFS/process/race tests and full-harness results separately; preserve all 158 predecessor focused groups and A's default-PointIds limitation. No FGDB, accepted snapshot or parent completion claim.

## Tasks / Subtasks

- [ ] Review scoped prototype path/writer/conflict contract and record execution authorization; preserve D01/D06 production gates. (AC2-5)
- [ ] Add strict disk records, manifest hashing, limits and pure transition/recovery planner. (AC1,4-6)
- [ ] Implement contained Windows filesystem capability/handle adapter and exclusive process writer. (AC2-3,7)
- [ ] Implement freeze/stage/seal/publish/read-back/receipt APIs with injectable I/O faults and explicit Recover. (AC1,3-7)
- [ ] Add immutable-revision, retry-after-successor, cancellation, fault-boundary and adversarial path tests, including actual child-process contention/termination. (AC1-7)
- [ ] Register named tests, build, run focused/full lanes and document evidence/unsupported capabilities; obtain scoped acceptance. (AC8)

## Dev Notes And File Map

NEW under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/`: `ToolboxPrototypeDiskContract.cs`, `ToolboxPrototypeRevisionStore.cs`, `ToolboxPrototypeRecovery.cs`, `ToolboxContainedFileSystem.cs` (names may follow local conventions). NEW tests under `ParcelWorkflowAddIn.Tests/Workflow/`: disk-store/protocol/path test classes. UPDATE only `ParcelWorkflowAddIn.Tests/Program.cs` test registrations and tightly scoped child-process test dispatch if required; read entire current harness first, preserve substring OR filtering, fail-fast and normal invocation behavior. No new test framework.

REUSE `ToolboxWorkspaceStorageCodec.cs`, `ToolboxWorkspaceStorageDocument.cs`, `ToolboxStorageValidation.cs` and existing A/B fixtures unchanged. DO NOT UPDATE `CaseFolderStore`, `ManifestSerializer`, output adapters, package restore, domain action/hash algorithms, settings or DAML. Legacy sibling temp replacement is not a complete recovery protocol. Preserve checkpoint `0a6188f` and all accepted uncommitted work; do not reset/commit/deploy automatically.

Testing lane: existing net8.0-windows/x64 executable harness; build with `/p:BaseIntermediateOutputPath=obj-toolbox-disk/ /p:UseSharedCompilation=false /m:1`, run focused filters `"toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"`, then full harness. Report actual new group counts. Full predecessor run stopped after 207 at known Compare XAML; default boundary PointIds are inspectable but existing domain hashing fails, never normalize in this store.

## References

- [Parent and ownership gates](13-5-add-versioned-toolbox-workspace-persistence.md), [accepted A contract](13-5a-add-storage-contract-and-hydration-foundation.md), [A evidence](13-5a-implementation-evidence.md).
- [Architecture A01-A03](../../planning-artifacts/toolbox/toolbox-architecture.md), [decisions D01/D06/D11](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md).
- Checked 2026-10-11: Microsoft [CreateFile sharing/flags](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [final handle paths](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew), [reparse operations](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations), [FileStream flush](https://learn.microsoft.com/en-us/dotnet/core/api/system.io.filestream). These inform the proposed adapter; they do not prove its race safety or power-loss behavior.

## Dev Agent Record

Prepared 2026-10-11. Implementation not started; all tasks unchecked. No disk/runtime tests run by refinement. Complete evidence, changed-file list and independent review before acceptance.
