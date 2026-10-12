---
story_key: 13-5c-2-define-windows-containment-locking-and-replacement-adapter
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5c-add-isolated-prototype-disk-revisions
depends_on: [13-5c-1-define-disk-contract-and-recovery-logic]
execution_gate: explicit-runtime-authorization-and-c1-acceptance
implementation_authorized: true
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5C.2 - Windows Containment, Locking And Replacement Adapter

Status: review
Isolated implementation and actual NTFS capability proof complete. Final build is clean; 197 focused groups and 154 native interruption scenarios pass. Independent containment/delta review is closed. See [operation table](13-5c-windows-operation-table.md) and [runtime evidence](13-5c-implementation-evidence.md); final scoped acceptance remains pending. Tracked through C/13.5 without a new key.

## Story

As a Toolbox developer, I want a contained Windows filesystem adapter with enforceable process locking, so that revision operations cannot silently weaken path or publication guarantees.

## Scope And Dependencies

Depends on accepted [C.1 records/planner requirements](13-5c-1-define-disk-contract-and-recovery-logic.md) and explicit runtime authorization. The [corrected C contract](13-5c-add-isolated-prototype-disk-revisions.md) remains the policy source. Specification preparation can finish before predecessor code exists; runtime acceptance cannot.

Included: narrow Windows capability/session adapter, pinned root/ancestor and leaf verification, exclusive existing-lock opening, exclusive initialization primitives, bounded enumeration/read/write/flush, same-volume directory seal, metadata replacement and actual NTFS/process/race tests. Excluded: domain hydration/approval, full revision orchestration, a duplicate journal planner, copy/export API, production roots, ArcGIS/FGDB, UI/settings/DAML and remote work.

## Required Adapter Operation Table

Before enabling writer capability, deliver an operation table with exact native APIs, desired access, sharing, flags, creation disposition, non-inheritance, volume/file/link checks, flush/read-back points and handle lifetime/handoff for each row below. The mechanism is an implementation feasibility deliverable, not already proven by this story.

| Operation | Required outcome | Verification boundary |
|---|---|---|
| Claim new case/store directories | Exclusive claim; existing path never adopted | Check actual parent/created directory identities and interruption residue |
| Pin root/ancestors and inspect leaf | Physical containment, no reparse traversal or writable hard-link alias | Protect handles and ordinary replacement races throughout operation |
| Open existing writer.lock | Exclusive OS sharing, no creation/truncation/TTL | Two-process contention and process-death release |
| Create new lock/owner/staged metadata or bytes | Only initialization/prepared new paths; no arbitrary overwrite | Create-new, length/hash read-back and pinned containment |
| Read immutable revision/metadata | Owned bounded bytes from verified handles | Stable identity/content while reading; reject unsafe layout |
| Seal staged directory | Same-volume rename to absent immutable destination | Compatible directory-handle rights and no destination overwrite |
| Publish pointer/journal sibling | Reviewed replacement, recoverable actual outcomes | Mutable-leaf protection during handoff and documented partial failures |
| Enumerate layout | Streaming bounded observations without mutation | Include unexpected/debris/abandoned entries; never follow links |

## Acceptance Criteria

1. Given an explicit approved unmanaged test root, capability checks verify local fixed NTFS and actual physical containment. Reject UNC, mapped/remote/synchronization-managed roots, reparse paths and unsafe aliases. Caller attestation is retained as external context, not inferred from owner.json or a drive letter. Device/extended-path forms are rejected or explicitly normalized by a documented safe rule; metadata never supplies arbitrary paths.
2. Given actual ancestors/leaves, the adapter pins and verifies handles and volume/file identity for the operation lifetime. Reject reparse/hard-linked writable leaves and existing unsafe handles before mutation. Attribute checks followed by an unprotected path write are insufficient. Unsupported mechanisms return unsafe_path/unsupported without a fallback.
3. Given each table operation, tested rights/share/flag choices permit the intended operation without releasing containment protection. Mutable-leaf handoff excludes ordinary competing mutation before protected/outside-root bytes can change; same-user ACL assumptions alone are insufficient. Failure to demonstrate replacement protection blocks writer capability even when pure C.1 tests pass.
4. Given two independent processes, exactly one can hold the existing writer.lock; a concurrent observer/writer gets busy without changing it. Existing lock acquisition never creates/truncates the file. Process termination releases all non-inheritable handles; lock existence/PID/time never causes forced deletion. Read sessions can acquire the lock without changing disk contents.
5. Given exclusive initialization primitives, one directory claim wins and no existing directory is adopted. Failure or termination retains initialization evidence. The adapter reports concrete outcomes; C.3 owns owner/schema validation and initialization_complete/incomplete classification through C.1.
6. Given staged verified content, seal uses a proven same-volume rename to an absent destination, never incremental copying or overwriting. File/ancestor guard lifetimes remain compatible with rename. Existing/partial/conflicting destinations are reported for C.1/C.3 quarantine without mutation.
7. Given metadata replacement, the adapter reports actual errors and performs bounded observation of resulting names/identities. A failed API does not claim unchanged files. Create-new siblings, file flush/read-back and previous-pointer evidence are retained; no journal in-place writes or automatic debris cleanup. The promise is process-interruption recovery, not hardware power-loss durability.
8. Given deep/large/unknown layouts and files changing during read, bounded streaming enumeration/read/digest work obeys C's limits, counts all entries and returns typed diagnostics rather than partially trusted bytes. Freeze returned data and release handles on success, exception and cancellation; no leaked inheritable handles or unbounded waiting.
9. Given deterministic adversarial tests synchronized at adapter boundaries, actual NTFS evidence covers ancestor rename/reparse, leaf substitution/hard links, competing preopened handles, mutable-leaf handoff, partial replacement outcomes and child-process contention/death. Outside-root sentinel bytes are checked unchanged. Unsupported fixture privileges/capabilities are reported as unverified, not silently passed.
10. Given completed implementation, report pure adapter-contract tests separately from actual NTFS/race/process results and predecessor focused/full lanes. C.2 scoped acceptance requires demonstrated safe create/read/lock/seal/replace capabilities for C.3; unsupported-only behavior cannot close the full adapter story or authorize C.3 writer integration.

## Tasks / Subtasks

- [x] Confirm combined C.1-C.3 execution authorization, internally reviewed C.1 evidence and explicit disposable NTFS root. (AC1-10)
- [x] Resolve/review the operation table and handle/session interface against C.1 requirements. (AC2-3)
- [x] Implement narrow platform capability/containment/identity adapter with SafeHandle ownership. (AC1-3,8)
- [x] Implement create-new primitives, existing exclusive-lock acquisition and bounded observation. (AC4-5,8)
- [x] Demonstrate safe directory seal and kernel-isolated mutable metadata replacement on the admitted capability. (AC6-7)
- [x] Add actual NTFS/process/race fixtures and tightly scoped child-process harness dispatch. (AC9)
- [x] Independently review containment evidence and require capability proof before C.3 writable admission. (AC10)
- [ ] Obtain final JotaPe scoped C.2 acceptance as part of completed C acceptance. (AC10)

## Developer Context And File Map

NEW proposed file: `ToolboxContainedFileSystem.cs` under `Workflow/Toolbox/Persistence/`, with narrow supporting session/capability types if justified. Keep native calls in this platform boundary, using net8.0-windows/x64 and SafeHandle, with no framework upgrade. REUSE C.1 records/diagnostic types. Store APIs in C.3 consume owned adapter sessions, not raw caller paths or native handles.

NEW tests under `ParcelWorkflowAddIn.Tests/Workflow/`: `ToolboxContainedFileSystemTests.cs` plus a narrowly scoped process worker helper if needed. UPDATE only the existing Program.cs registration and explicit reserved child-process dispatch. Read the whole harness before editing. Worker mode must dispatch before normal test selection, validate scenario/root inputs, execute only a named disposable fixture, return a checked exit code and never recursively launch the normal suite. Preserve all normal substring-OR/fail-fast/no-argument behavior. Bound process waits; dispose/terminate only task-owned workers on test failure and verify no worker remains.

Do not change operational CaseFolderStore, ManifestSerializer, package/restore, settings, DAML, A schema/codec, policy/domain hashes or source adapters. Existing sibling temp replacement is not evidence for this adapter. Do not perform destructive test cleanup against computed paths; retained interrupted evidence is part of the protocol, and any fixture cleanup needs separately verified containment.

## Test Requirements And Completion Evidence

Fixtures use deterministic barriers rather than timing guesses for race windows. Record actual filesystem type, mechanism/share table, privilege limitations, file identities, outside-root sentinel hashes, child exit codes and posttermination lock release. Required negative cases include initialization collision, missing existing lock, hard-link alias, reparse ancestor/leaf, preopened sharing conflict, rename destination collision, and replacement errors. No NTFS/process tests run during specification preparation.

Use the parent build/focused/full lane, preserving 158 predecessor focused groups and default-PointIds behavior. A known full-harness stop after 207 does not execute later appended groups; run new named adapter groups explicitly. Completion needs independent containment review, concrete operation table, actual evidence and scoped acceptance. A blocked platform mechanism remains an open C.2 story.

## References

- [Corrected C contract](13-5c-add-isolated-prototype-disk-revisions.md), [C.1 protocol](13-5c-1-define-disk-contract-and-recovery-logic.md), [C.3 composition](13-5c-3-define-revision-save-open-retry-recover-and-abandon.md), [review R1-R10](13-5c-contract-review-2026-10-11.md).
- [A evidence](13-5a-implementation-evidence.md), [architecture](../../planning-artifacts/toolbox/toolbox-architecture.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md).
- Microsoft [CreateFileW rights/sharing/flags](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [ReplaceFileW access and partial failure outcomes](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew). Checked 2026-10-11; documentation establishes API behavior, not this adapter's safety.

## Dev Agent Record

Preparation itself performed no runtime work. JotaPe later authorized C.1-C.3 completion. Codex and a disjoint platform worker implemented the adapter; blind/edge/acceptance reviewers closed its final delta. Actual Windows 10/x64 fixed-NTFS proof passes all nine constituent groups, including independent-thread races, external-ambient refusal, capability inspection and process contention/death. Final build: zero warnings/errors; combined focused 197 passes; native matrix 154 scenarios. D exFAT is refused and authorized unique Ctmp NTFS fixtures supply the proof. See [operation table](13-5c-windows-operation-table.md) and [complete evidence](13-5c-implementation-evidence.md) for rights, optional TxF dependency, abnormal-cleanup static-review limit and changed files. No commit/deployment or production/host/domain authority is implied. Final human acceptance remains outstanding.
