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

Status: review
Refinement: JotaPe authorized completion of C.1-C.3 after R1-R10 corrections/preparation. Isolated implementation, verification and independent code review are complete: clean build, 197 focused passes and 154 native interruption scenarios. See [runtime evidence](13-5c-implementation-evidence.md). Final JotaPe scoped acceptance remains separate. No new tracker key; tracked through parent 13.5.

## Story

As a Toolbox developer, I want immutable byte-bundle revisions with exclusive local saves and deterministic reopen, so that the accepted codec can be tested on disk without moving operational or FGDB authority.

## Boundary And Entry Gate

Implement only after review of this prototype's path, lock, recovery and conflict policies. This is not acceptance of D01/D06 production ownership. Use `temp/toolbox-worktree`, branch `refactor/toolbox-case-workspace`, disposable sanitized cases under an explicitly supplied isolated test root. Verify root separation physically, not just by string comparison. No existing production case roots, UI call sites, registration, remote operations, GDB operations or approval/export integration.

The [contract review](13-5c-contract-review-2026-10-11.md) records R1-R10 and their corrections. Authorization to correct this document does not authorize runtime implementation. The Windows adapter operation table and its actual NTFS evidence remain implementation deliverables; saves must remain unsupported until containment and replacement capabilities are demonstrated. Execution order remains C -> D/E -> B.

## Ordered Implementation Stories

JotaPe requested story preparation/review only, explicitly no coding. The three child specifications below divide this contract without replacing its schema/policies or adding tracker keys. Each remains backlog with runtime authorization not granted; specification completion is distinct from code-story acceptance.

| Story | Owns | Runtime dependency / exit |
|---|---|---|
| [13.5C.1 - Disk Contract And Recovery Logic](13-5c-1-define-disk-contract-and-recovery-logic.md) | Strict disk records, hashing, limits and pure recovery decisions | A accepted plus runtime authorization; pure protocol evidence and scoped C.1 acceptance |
| [13.5C.2 - Windows Containment, Locking And Replacement Adapter](13-5c-2-define-windows-containment-locking-and-replacement-adapter.md) | Native capability/session boundary, physical I/O, locking and replacement proof | C.1 accepted plus runtime authorization; demonstrated required NTFS/process/race capabilities and C.2 acceptance |
| [13.5C.3 - Revision Save, Open, Retry, Recover And Abandon](13-5c-3-define-revision-save-open-retry-recover-and-abandon.md) | Store orchestration, hydration, publication/tail handling and operation fault matrix | C.1/C.2 accepted plus runtime authorization; operation evidence, C.3 review and scoped C acceptance |

C.1 -> C.2 -> C.3 is the implementation order within C. Documentation for all three may be prepared together. C.3 owns semantic initialization/owner classification; C.2 supplies exclusive creation and observation primitives; C.1 supplies pure classification rules. New revision operations must not duplicate C.1's planner or bypass C.2's capability gate. Full C acceptance still precedes affected D/E integration, and B remains the final hosted FGDB slice.

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

`owner.json` binds contract, case, store_id and prototype mode. It is not a user permission grant or cryptographic trust root. Existing directories are never adopted by Create, including empty ones. Open rejects a nonempty ownerless store and never repairs initialization. Expected identity and isolated-root authorization arrive externally on every open.

| Record | Required fields |
|---|---|
| Owner | contract, schema_version, case, store_id (lowercase GUID N), storage_mode (exact `prototype_store`) |
| Save request | contract, schema_version, store_id, case, operation_id, proposed_revision_id, expected_parent (null or revision_id + revision_digest + sequence; null only for empty store), actor, prepared_at (round-trip offset timestamp), owned bundle bytes, owned optional asset bytes |
| Immutable revision | contract/schema, store_id, case, operation_id, revision_id, parent (revision_id + revision_digest + sequence, or null), sequence (1 then checked parent+1), actor, prepared_at, bundle (sha256 + byte_length), ordered nullable assets (sha256 + byte_length), request_digest, revision_digest |
| Pointer | contract/schema, store_id, case, revision_id, revision_digest, sequence; null pointer only means uninitialized when no published evidence exists |
| Journal | complete immutable revision intent, preserved previous pointer (null only for initial empty store), state (`prepared`, `sealed`, `published`, `committed`, `abandoned`), nullable abandonment audit (actor, at, reason); mutable progress/audit is not part of revision digest |
| Receipt | contract/schema, store_id, case, operation_id, request_digest, revision_id, revision_digest, sequence; can be reconstructed only from a verified published revision and matching intent |

All disk record contract/schema fields use `contract` = `toolbox_prototype_disk_v1` and integer `schema_version` = 1; case uses A's exact four named fields. Required fields, including nullable fields, must be present. A missing pointer file means uninitialized only in a valid initialized store with no publication evidence; a JSON null pointer is not a valid pointer record.

Define request/revision hash canonicalization in a pure manifest helper with independent golden UTF-8 bytes/digests before filesystem integration. Recursively sort object keys ordinally, preserve array order and exact strings, use invariant JSON scalar encoding with the same explicit escaping rules as A's canonical serializer, and emit no BOM or whitespace. Fix these rules with golden fixtures rather than runtime defaults. Preserve original round-trip timestamp offset/precision; no trimming, casing or timestamp normalization. Sequence and byte lengths are nonnegative signed 64-bit integers, with sequence starting at 1; checked overflow returns a typed diagnostic before I/O.

The request hash covers contract/schema, store_id, full case, operation_id, proposed_revision_id, the complete expected_parent including sequence, actor, prepared_at, exact bundle inventory and ordered nullable asset inventory. The revision hash covers the complete immutable revision, including request_digest, excluding only revision_digest. Journal state, abandonment audit and progress do not enter these hashes; preserved previous pointer must independently equal the revision's parent binding. Compare exact bundle/asset bytes separately. Empty vs null inventory is not interchangeable. Metadata IDs are storage IDs; no domain IDs/events/timestamps are generated during reopen.

Assets are opaque ordinary files, optional, content-addressed, owned and frozen with the revision; no GDB directories, symlinks, executable invocation or remote fetching. Their use is transport-only until D supplies provenance. Reject source secrets before fixtures are admitted; don't log raw bundle/asset content.

Preserve caller asset order and null versus explicitly empty asset inventory in both request and revision metadata. Reject duplicate asset digests, including repeated identical bytes, rather than silently deduplicating. Inventory digests/lengths derive from frozen owned bytes; paths derive from digests, never caller names. No asset role or external source-resolution semantics are introduced in C.

Default disk bounds, injectable downward for tests: metadata file 1 MiB, assets 256, each asset 64 MiB, aggregate revision bytes 512 MiB including A bundle, revisions/journals/receipts each 10,000 entries, metadata traversal 64 MiB total. A's own bundle/payload/work limits also apply. Check lengths before allocation and stream digest verification; never raise limits silently. No automatic retention/GC or full-history payload hydration; verify selected revision payload and bounded ancestry metadata. Missing/corrupt ancestry blocks a clean writable open.

Additionally bound JSON depth to 64, directory depth to 8 beneath the store root, staging operation directories to 10,000 and total observed filesystem entries to 3,000,000. Count all owner/lock/current, journal/receipt/revision/staging files, directories, abandoned evidence, recognized replacement siblings and unexpected entries. Stream enumeration and use checked counters; stop before exceeding budgets or materializing an entire tree. Never traverse a reparse point. The metadata-byte budget applies across all metadata examined in one request, including historical retry/recovery. Limits may conservatively stop traversal before per-category maxima. Unknown entries return `unexpected_layout` without deletion.

Recognized replacement siblings use fixed derived names: `current.<operation_id>.next.json` at the store root and `<operation_id>.<target_state>.next.json` beside its journal. Each name binds to a known journal intent, and each candidate must pass strict record/binding validation. Unmatched, multiple conflicting or partially written candidates remain pending/quarantined; neither overwrite nor deletion is a recovery shortcut.

### Initialization And Observation

Create and Open are distinct APIs. Create exclusively claims a new case directory under the externally approved root, establishes its contained store directories, creates and holds a new empty writer.lock, then create-new writes/flushes/read-backs the owner before admitting saves. Concurrent creation has one winner; an existing directory returns `initialization_conflict` without adoption or cleanup. Interruption before a valid owner/lock/layout is established returns `initialization_incomplete`; retain the directory/evidence. No initialization repair protocol is included in v1. The caller may explicitly select a different new directory.

Open never creates owner, lock, directories or recovery files. After containment checks, it opens the existing writer.lock with exclusive sharing for the entire bounded observation and owned-byte read; contention returns busy. A missing lock in an owned store is corrupt, never recreated by Open. Return a detached owned snapshot/diagnostics, then release handles. Open performs no disk mutation or implicit recovery. Read serialization is intentional in this prototype. Save, Recover and Abandon acquire the same lock; no nested lock acquisition. All acquired handles are non-inheritable and disposed on completion/failure.

### Containment And Writer Protocol

Local fixed NTFS test storage only for v1. Reject UNC, remote/mapped drives, reparse points and explicitly identified synchronization-managed roots; caller must attest the approved unmanaged test root. Capability checks and actual Windows filesystem tests are required, not assumptions about a drive letter. Privileged malicious filesystem manipulation is outside the guarantee; ordinary ancestor/leaf replacement races must be prevented or fail closed.

The filesystem boundary must pin and inspect the ancestor chain and actual file handles for the operation lifetime. Candidate Windows mechanism: directory handles using backup-semantics/open-reparse-point flags, final-path/volume identity checks, deny delete-sharing on pinned ancestors, and create-new leaves. Validate existing leaf handles and reject reparse/hard-linked writable files; never truncate an arbitrary existing leaf. Merely checking attributes before a later path-based write is insufficient. If a safe creation/replacement protocol cannot be demonstrated, stop with `unsafe_path` rather than fall back to lexical checks. Narrow SafeHandle/PInvoke code may live in one platform adapter; no framework upgrade.

Before enabling saves, document and test an adapter operation table for immutable read, create-new file/directory, directory seal, pointer replacement and journal replacement. Each row must specify desired access, sharing, flags, identity/volume/link checks, handle lifetime and protection during any handoff. Do not retain a delete-denying mutable-leaf handle while invoking an API that requires DELETE access. Ancestor pinning does not alone prove safe leaf replacement. Use a demonstrated handle-based replacement protocol or scoped access control in the disposable root that actually excludes competing leaf mutation; a same-user access-control assumption alone is not proof. Prevent or reject races before any protected/outside-root bytes are changed. Missing capability returns unsafe_path/unsupported, never a weaker path-based fallback. Actual NTFS tests must cover the selected sharing modes, preexisting handles, leaf substitution and ancestor replacement at handoff boundaries.

Acquire the existing `writer.lock` with OS-enforced exclusive sharing and retain the handle through publication/recovery. Lock file existence, PID or timestamp is not a lease; no TTL deletion/force-unlock. Process death releases handles, then the next owner inspects durable state. Two independent processes must contend in tests. Under lock, validate owner and observe unfinished intents/current pointer. Before admitting a new operation, require a fully resolved store and compare expected parent id/digest/sequence. Unfinished work requires explicit Recover/resume/Abandon; Save does not silently repair unrelated operations. No automatic merge, last-writer-wins or stale-parent retry with a changed parent.

First inspect operation ID across verified receipts, journals and revision metadata: identical request with complete verified terminal evidence returns its receipt even if later saves advanced the pointer; conflicting intent returns `operation_conflict`. Do this before rejecting the original expected parent on an already-published retry. Missing receipt or incomplete published journal tail returns `recovery_required`; explicit Recover completes it. Unrelated pending work still blocks mutations, but cannot change the identity of a verified historical receipt. An uncommitted identical retry resumes only its pinned original intent after explicit recovery, never reallocates IDs.

A historical published operation must have its exact revision_id/revision_digest/sequence on the verified parent chain of current, plus matching intent. Receipts must agree with that evidence. Verify that operation's exact bundle and complete asset inventory before returning/reconstructing its receipt, within request budgets; do not hydrate every historical payload. A sealed off-chain revision is not committed. Recovery of historical tails does not move current or create new operation/domain IDs.

### Publication And Recovery

1. Validate A bundle against expected case and freeze all bytes before I/O. Acquire physical containment and writer handles; verify expected parent and operation identity.
2. Create and flush prepared intent before staged bytes. Build the complete revision directory under staging/<operation_id>, including revision.json, bundle.json and assets. Write create-new staged files, flush to disk, read back exact inventory/sizes/digests and decode A again. Seal by a demonstrated same-volume directory rename to the new revisions/<revision_id> destination, then publish the sealed journal state. Never copy files incrementally into a published revision location or overwrite a revision/asset.
3. Publish `current.json` using a tested same-volume sibling-file replacement (create-new for initial pointer), retaining the previous verified pointer as recovery evidence before replacement. Flush required files; read back pointer and revision before reporting publication. File replacement is not a multi-file transaction.
4. Write/read-back receipt and finalize journal. Return `committed` only with verified evidence. Cancellation before publication leaves a pending intent and old pointer; after verified publication return the committed receipt with a late-cancellation diagnostic, not a false "nothing saved" result.

Reopen states: `empty`, `verified`, `busy`, `pending`, `recovered`, `quarantined`, `unsupported`, `corrupt`. Reads never silently mutate; explicit Recover acquires the same writer lock. If old pointer is intact and a new revision is sealed but unpublished, retain old revision read-only and require explicit resume/abandon of that exact intent. If current or its verified ancestry proves publication of the exact revision and intent, explicit Recover reconstructs missing tail/receipt idempotently. Torn/missing pointer, contradictory metadata, competing intents or missing bytes remain quarantined; never choose newest file or advance from sequence alone. Expose a previous verified revision read-only only when its preserved pointer and complete inventory validate.

Recovery evaluates disk contents rather than journal state alone. A complete matching staging directory with no destination may resume sealing; a complete matching destination with no staging may resume the journal/publication protocol. Both locations present, a partial destination or mismatched inventory is quarantined and never completed by copying/overwriting. Partial staging with intact verified parent and no publication evidence may be explicitly abandoned; it is not automatically repaired. IDs remain reserved after abandonment.

### Journal Transitions And State Invariants

Initial intent is create-new. All later journal states use flushed, create-new recognized siblings and the reviewed replacement adapter, retaining the exact immutable intent and preserved previous pointer. Never rewrite a journal in place. Normal transitions are prepared -> sealed -> published -> committed and prepared/sealed -> abandoned. Committed/abandoned are terminal. Explicit recovery may infer skipped forward transitions only from verified complete staged/sealed/published evidence, never from journal claims alone. A replacement exception may have changed names/content; inspect evidence and return pending/quarantined until resolved rather than assuming rollback. No implicit cleanup of replacement debris.

| Result state | Required evidence and permitted behavior |
|---|---|
| empty | Valid owner/lock/layout, absent current and no publication or unresolved intent; may accept an initial save if policy and capabilities permit |
| verified | Current revision/inventory/ancestry and all completed operation evidence consistent; no unresolved intents or replacement artifacts; writable only with policy/capability gates satisfied |
| busy | Exclusive lock unavailable; no mutable observation or save |
| pending | Verified preserved parent or empty state plus one unambiguous unfinished intent; read-only parent inspection, explicit resume/recover/abandon only |
| recovered | Explicit recovery completed and read-back evidence now satisfies verified invariants; annotate recovery without minting domain approval |
| quarantined | Ambiguous publication, conflicting locations/intents or replacement outcome; no writes, only independently proven previous revision inspection |
| corrupt | Invalid required ownership/schema/reference/bytes or ancestry; no normal save or automatic repair |
| unsupported | Platform, containment or storage capability not demonstrated; no writes |

Initialization diagnostics and policy inspection diagnostics are distinct from these storage states. Typed results expose storage state, transport verification, owned inspection data where safe, writable status and diagnostics separately; no single Success boolean grants authority.

Explicit Abandon runs under the same lock and requires the current pointer to equal the verified preserved parent (or verified uninitialized store), with no evidence that this operation was published. Published or ambiguous intent cannot be abandoned. Atomically replace/flush/read-back the journal with state `abandoned` and non-null caller-supplied actor/time/reason audit; retain staged/sealed bytes and IDs. Only after verified abandonment and no other unresolved intents may a fresh operation save against that unchanged parent. A failed/torn abandonment stays pending/quarantined until recovery. Retry of the original save returns `operation_abandoned`, never resurrects it; repeated identical abandon is idempotent, changed abandonment audit conflicts. No receipt pretends an abandoned operation committed and no arbitrary recursive cleanup occurs. Include abandoned operations in bounds/collision checks.

This slice proves process-interruption recovery, not hardware power-loss durability. Publish/flush failures have ambiguous outcomes until read-back/reopen resolves them. Unresolved intent or corruption blocks further save. A verified prototype reopen grants no fresh context/permissions, trusted policy, geometry proof or domain approval.

### Policy Inspection And Relocation

Keep A's successful hydration and policy_unavailable/policy_untrusted warnings unchanged. Missing/mismatched declared C inventory bytes are corruption; absent external trust or unavailable policy is not byte corruption. Such bundles remain inspectable and transport-verified, but return read-only `policy_inspection_required` under C's conservative writer gate. Every save, including new bundle validation, requires externally trusted policy resolution; Create can initialize an empty store without claiming policy/domain authority. Do not infer external source availability from opaque assets or fetch missing sources. Only C's declared inventory and A's existing structural references are validated.

C adds no copy/export API. The relocation test copies a quiescent complete store while holding the source's existing writer lock throughout the read, excluding only that open lock file. An explicit relocation preparation step claims a new isolated destination, copies owner/store/case identity and all revisions/journals/receipts/staging/recognized recovery evidence unchanged, creates a new empty destination writer.lock and validates physical containment. Open never creates that lock. Normal bounded observation/recovery rules apply at the destination; saved permissions are never restored. Mixed or incomplete third-party copies receive normal pending/quarantined/corrupt results. Independent clones have per-root locks only, with no cross-root writer coordination guarantee.

## Acceptance Criteria

1. Given valid accepted A/B fixtures, save/reopen preserves all A fields, exact bytes, comparers, IDs and stale/pending states; decode uses external case identity/trusted pins and never replays domain actions.
2. Given invalid ownership, identity, unsafe paths, reparse/race/hard-link fixtures or unsupported storage, open/save/cleanup fails without changing any protected or outside-root file.
3. Given competing initializers, two processes or stale expected parent, exactly one permitted creation/save wins; the loser gets initialization_conflict/busy/conflict and cannot alter winner bytes, pointer or receipts. Open during a paused write returns busy, then a consistent detached observation after lock release.
4. Given repeated operation ID after restart, identical intent returns the same verified receipt, including after later commits; missing historical tails require explicit Recover without moving current. Changed bytes/assets/parent sequence/actor/time/IDs return conflict without new audit/entity allocation. Golden hash fixtures cover null/empty inventory, asset order, duplicate digests and sequence overflow.
5. Given injected failures before/after every initialization, write, flush, seal, journal/pointer replacement, receipt and abandonment boundary, reopen/recovery deterministically verifies old/new state or quarantines it; incomplete initialization is never adopted. Partial staging can be explicitly abandoned only against its verified unchanged parent; partial/conflicting sealed destinations never resume by overwrite. Test actual replacement error outcomes and process termination, not only managed exceptions.
6. Given oversize/deep/duplicate/unknown/corrupt metadata, unbounded debris or modified/missing declared bytes, bounded typed diagnostics prevent clean writable open. Intact bundles with unavailable/untrusted policy retain A's inspection diagnostics and C's read-only policy gate. Opaque assets introduce no external source-role availability claims. All observed entries and metadata count toward streaming traversal budgets.
7. Given a complete lock-consistent relocation fixture, externally validated new root rebases derived paths while original identity/store/revision stays intact; preserve pending evidence and create the new lock only during explicit relocation preparation. Mixed/incomplete copies fail conservatively. Separate clones do not imply coordinated writer ownership or saved permissions.
8. Given completed implementation, report build, pure protocol tests, actual NTFS/process/race tests and full-harness results separately; preserve all 158 predecessor focused groups and A's default-PointIds limitation. No FGDB, accepted snapshot or parent completion claim.

## Tasks / Subtasks

- [x] Review scoped prototype path/writer/conflict contract and record execution authorization; preserve D01/D06 production gates. (AC2-5)
- [x] Apply R1-R10 contract corrections requested by JotaPe 2026-10-11; runtime authorization and platform evidence remain separate.
- [x] Prepare the three C.1/C.2/C.3 specifications under JotaPe's no-coding instruction; review evidence is recorded in the story-preparation review.
- [x] Add strict disk records, manifest hashing, limits and pure transition/recovery planner. (AC1,4-6)
- [x] Specify/test the adapter operation table; implement contained Windows filesystem capability/handle adapter and exclusive process writer/observer. (AC2-3,7)
- [x] Implement freeze/stage/seal/publish/read-back/receipt APIs with injectable I/O faults and explicit Recover/Abandon. (AC1,3-7)
- [x] Add initialization/observation, immutable-revision, historical-tail recovery, policy-inspection, bounded debris, consistent relocation, retry-after-successor, cancellation, fault-boundary and adversarial path tests, including actual child-process contention/termination. (AC1-7)
- [x] Register named tests, build, run focused/full lanes and document actual evidence/unsupported capabilities. (AC8)
- [ ] Obtain final JotaPe scoped C acceptance before affected D/E integration. (AC8)

## Dev Notes And File Map

NEW under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/`: `ToolboxPrototypeDiskContract.cs`, `ToolboxPrototypeRevisionStore.cs`, `ToolboxPrototypeRecovery.cs`, `ToolboxContainedFileSystem.cs` (names may follow local conventions). NEW tests under `ParcelWorkflowAddIn.Tests/Workflow/`: disk-store/protocol/path test classes. UPDATE only `ParcelWorkflowAddIn.Tests/Program.cs` test registrations and tightly scoped child-process test dispatch if required; read entire current harness first, preserve substring OR filtering, fail-fast and normal invocation behavior. No new test framework.

REUSE `ToolboxWorkspaceStorageCodec.cs`, `ToolboxWorkspaceStorageDocument.cs`, `ToolboxStorageValidation.cs` and existing A/B fixtures unchanged. DO NOT UPDATE `CaseFolderStore`, `ManifestSerializer`, output adapters, package restore, domain action/hash algorithms, settings or DAML. Legacy sibling temp replacement is not a complete recovery protocol. Preserve checkpoint `0a6188f` and all accepted uncommitted work; do not reset/commit/deploy automatically.

Testing lane: existing net8.0-windows/x64 executable harness; build with `/p:BaseIntermediateOutputPath=obj-toolbox-disk/ /p:UseSharedCompilation=false /m:1`, run focused filters `"toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"`, then full harness. Report actual new group counts. Full predecessor run stopped after 207 at known Compare XAML; default boundary PointIds are inspectable but existing domain hashing fails, never normalize in this store.

## References

- [Parent and ownership gates](13-5-add-versioned-toolbox-workspace-persistence.md), [accepted A contract](13-5a-add-storage-contract-and-hydration-foundation.md), [A evidence](13-5a-implementation-evidence.md).
- [R1-R10 contract review and correction record](13-5c-contract-review-2026-10-11.md); Microsoft [ReplaceFile sharing and error outcomes](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).
- [C.1-C.3 story preparation review](13-5c-story-preparation-review-2026-10-11.md).
- [Architecture A01-A03](../../planning-artifacts/toolbox/toolbox-architecture.md), [decisions D01/D06/D11](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md).
- Checked 2026-10-11: Microsoft [CreateFile sharing/flags](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [final handle paths](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew), [reparse operations](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations), [FileStream flush](https://learn.microsoft.com/en-us/dotnet/core/api/system.io.filestream). These inform the proposed adapter; they do not prove its race safety or power-loss behavior.

## Dev Agent Record

Prepared and later implemented 2026-10-11. JotaPe explicitly authorized C.1-C.3 completion after documentation corrections/preparation. Code, actual NTFS capability proof, exact A/B history transport, fresh trust, retry/recovery/abandonment and independent-review corrections are complete in isolation. Final build has zero warnings/errors. Final combined focused lane passes 197 groups, including 39 new disk groups; 154 native interruption scenarios pass (112 Save/25 Create/17 Abandon). Full harness still stops after 207 at unchanged Compare XAML. All original 977 registry names/order and accepted A schemas/semantics are unchanged. Full commands, files, optional/deprecated TxF dependency and limitations are recorded in [runtime evidence](13-5c-implementation-evidence.md) and the [operation table](13-5c-windows-operation-table.md). No commit, deployment or production/FGDB/UI/remote activation occurred. Final human scoped acceptance remains pending.
