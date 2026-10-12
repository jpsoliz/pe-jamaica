# C.2 Windows operation table

Implementation: `ToolboxContainedFileSystem.cs`; fixture harness: `ToolboxContainedFileSystemTests.cs`. These files implement ordinary-file primitives, not record parsing, ownership adoption, schema validation, recovery, or domain authority. The store must hold one writer lease across its complete observation/mutation protocol. Sessions are synchronous; callers must finish/dispose enumerators before disposing the session and must not consume a session concurrently.

## Current evidence and acceptance boundary

- Final stable solution build 45969 passed with zero warnings/errors. Combined run 27879 passed 197 focused groups: 158 preserved and 39 new disk groups.
- The nine-group native aggregate passed on Windows 10.0.22631/x64 fixed NTFS. It covers rooted reads/streamed verification, create-new claims, existing-lock contention, directory seal, initial/existing replacement, independent-thread hard-link/reparse/ancestor/leaf races, preopened handles, failure outcomes, non-mutating capability inspection and external-ambient transaction refusal.
- The final process matrix passed 154 scenarios: 112 Save, 25 Create and 17 Abandon, including native write blocks, every flush, transacted handoff/commit and semantic target-verification barriers. Every terminated child was reopened in a fresh process; only explicit proven recovery/abandonment was permitted.
- The worktree D volume is fixed exFAT and was refused before case creation. Tests explicitly used unique disposable Windows Temp directories on C fixed NTFS through TOOLBOX_DISK_TEST_ROOT. No weaker fallback, operational root, privilege, account or service change was used.
- The original deny-delete target guard plus POSIX rename failed with Windows error 32. The first TxF standard rename with an open target failed with error 5. Native `.` root enumeration failed with error 123. Those disproven choices were replaced by the operation table below, not treated as proof.
- Replacement verifies actual adapter-owned rooted TxF writer enlistment; kernel locks protect the closed-target and closed-handle handoffs through commit. Caller-owned native transactions are refused without mutation or context changes.
- Blind, edge-case and acceptance reviewers closed the final static code delta with no remaining production-code blockers. Abnormal OS restoration/detachment failure handling was statically reviewed, not fault-injected; normal restoration and real independent/external-context fixtures passed.
- C.2 is implementation-complete and review-ready for this tested isolated capability. Final JotaPe scoped acceptance remains separate. No hardware power-loss, multi-file Save transaction, production/host/domain authority or cross-clone writer-coordination claim is made.

See [complete C runtime evidence](13-5c-implementation-evidence.md) for commands, limits, review corrections and the unchanged full-harness failure after 207 passes. All original 977 registered names/order are retained; the registry now contains 1016 groups.

## Public API

Namespace: `ParcelWorkflowAddIn.Workflow.Toolbox.Persistence`.

```csharp
ToolboxContainedFileSystem(string approvedRoot, bool unmanagedRootAttested,
    IEnumerable<string>? synchronizationRoots = null,
    IEnumerable<string>? protectedRoots = null);
ToolboxFileSession OpenCase(string relativeCasePath, bool createNew = false);
// ToolboxFileSession : IDisposable
IDisposable AcquireWriter(bool createNew = false);
bool Exists(string relative);
byte[] Read(string relative, int maxBytes);
void Verify(string relative, long expectedLength, string sha256);
void WriteNew(string relative, ReadOnlySpan<byte> bytes);
void CreateDirectory(string relative);
void SealDirectory(string sourceRelative, string targetRelative);
void ValidateReplacementCapability(string existingRelative);
void Replace(string siblingRelative, string targetRelative);
IEnumerable<ToolboxFileEntry> Enumerate();
// public sealed record ToolboxFileEntry(string RelativePath, bool IsDirectory, long Length);
// public sealed class ToolboxFileSystemException : IOException { public string Code { get; } }
```

`Verify` is the explicitly requested addition: SHA-256 and length validation with a fixed 64 KiB buffer, no asset hydration. It admits only lowercase 64-hex inventory digests and nonnegative lengths. `Read` materializes only one caller-bounded owned byte array. `WriteNew` flushes and compares exact bytes with a fixed read-back buffer. None of these primitives interprets disk records.

`ValidateReplacementCapability(existingRelative)` is the parent's requested admission probe. Under the existing writer lease and after semantic ownership validation, C.3 can probe `toolbox/prototype-store/owner.json` before claiming Writable or admitting mutations. It opens that existing single-link ordinary leaf as a rooted transacted writer, verifies the UNCOMMITTED version, closes the handle, then rolls back. It creates no store file, writes/flushes no bytes, performs no rename/recovery/commit, and preserves caller native transaction context. Missing/unsupported TxF remains inspection-only; incompatible preopened writers return busy. The probe proves required writer enlistment and lock availability, not every future destination's rename permissions. Actual replacement still verifies native results and C.3 still reads semantic publication evidence.

The optional fourth `protectedRoots` argument was added at the parent's request. Explicit synchronization/protected roots and OneDrive environment roots are checked lexically at construction and by opened ancestor identities before any case-directory claim. Each excluded root is opened/pinned without mutation; overlap means the approved root identity occurs on its ancestry or vice versa. A missing/unsafe/unverifiable exclusion root fails closed. The adapter does not infer operational roots: the caller must supply them and explicitly approve/attest the disposable test root. Excluded and approved pin chains remain held for the complete case session. Volume serial plus file index comparisons conservatively reject duplicated identities rather than treating distinct drive letters as separation.

## Common native choices

Windows x64 is required. There is no unsafe code or project setting change. Native handles use `SafeFileHandle`. `OBJECT_ATTRIBUTES` excludes `OBJ_INHERIT`; `SetHandleInformation(HANDLE_FLAG_INHERIT, 0)` additionally clears inheritance on native-created handles. The initial Win32 drive handle has null security attributes (non-inheritable).

Only a DOS absolute drive root is admitted. UNC/device/extended prefixes, slash roots, traversal and ambiguous components are rejected. `GetDriveTypeW == DRIVE_FIXED`, `QueryDosDeviceW` requires a physical HarddiskVolume mapping, and the filesystem is queried through the actual opened handle. `GetFinalPathNameByHandleW` verifies drive and approved-root final paths. Each subsequent open uses `NtCreateFile` with `OBJECT_ATTRIBUTES.RootDirectory` referencing the held immediate parent and **one** derived component. Relative names admit ASCII alphanumeric, dot, underscore and hyphen; no ADS, wildcard, device name, trailing dot/space, empty, dot or dot-dot component.

`GetFileInformationByHandle` checks type, volume serial, reparse/offline/recall attributes, and one link for ordinary files. The volume serial and 64-bit file index form the identity checked before/after seal and replacement. All ancestors remain live, without delete sharing, through the operation. Drive-to-case pins last for the session; writer parent pins last for the lease; transient descendant pins last for their operation. No raw path is reopened for production I/O.

`FILE_SYNCHRONOUS_IO_NONALERT` is `0x20`; `FILE_OPEN_REPARSE_POINT` is `0x00200000`. File opens additionally use `FILE_NON_DIRECTORY_FILE (0x40)`, directory opens `FILE_DIRECTORY_FILE (1)`. Existing opens use `FILE_OPEN (1)`; exclusive claims use `FILE_CREATE (2)`. There is no overwrite/truncate disposition. A reparse object is opened without following it, then rejected by its opened attributes.

## Operation table

| Operation | Native mechanism; desired access | Share/disposition/flags | Verification, flush, lifetime and handoff |
|---|---|---|---|
| Pin drive and approved root | First `CreateFileW`; remaining components `NtCreateFile`. Directory `SYNCHRONIZE | FILE_READ_ATTRIBUTES | FILE_TRAVERSE | FILE_LIST_DIRECTORY` (`0x001000A1`) | Share READ+WRITE, no DELETE. Initial OPEN_EXISTING with BACKUP_SEMANTICS+OPEN_REPARSE_POINT. Native existing directory opens as above | Fixed physical mapping, handle NTFS query, actual final path and opened type/volume/attributes. Retain complete chain until session disposal. No case mutation before root capability checks. |
| Claim new case/store directory | Rooted `NtCreateFile`, directory access `0x001000A1` | FILE_CREATE, directory flags, share READ+WRITE without DELETE | Existing leaf never adopted; reports initialization_conflict. OpenCase may use existing intermediate parents but exclusively claims its final case leaf. CreateDirectory exclusively claims its final leaf. No rollback/cleanup. |
| Existing writer.lock | Rooted `NtCreateFile`, file `SYNCHRONIZE | FILE_READ_ATTRIBUTES | FILE_READ_DATA` (`0x00100081`) | FILE_OPEN, share **0**, non-directory/no-follow/synchronous | Validate ordinary single-link handle and same volume. Pin toolbox/store ancestors with the lease. No creation, truncation, byte write, expiry, PID or forced deletion. Dispose releases the OS lock; session disposal also releases it. |
| New writer.lock | Rooted `NtCreateFile`, above plus FILE_WRITE_DATA (`0x2`) | FILE_CREATE, share 0 | Single-link ordinary leaf; FlushFileBuffers on the empty new file. Failure retains initialization residue and releases handles. Schema/owner classification belongs to C.3. |
| Create immutable leaf | Rooted `NtCreateFile`, `0x00100083` | FILE_CREATE; share READ only (deny write/delete), no-follow/synchronous/non-directory | Verify newly opened handle before WriteFile. Fixed 64 KiB write blocks; FlushFileBuffers; opened length; seek and compare exact bytes through the same handle. File handle and all parents retained through verification. Partial failures retain the leaf. |
| Read/Verify existing immutable leaf | Rooted `NtCreateFile`, `0x00100081` | FILE_OPEN; share READ only (deny write/delete) | Opened type/reparse/link/volume checks precede content. Preopened incompatible writers/delete users yield busy. Read checks length before allocation; Verify streams SHA-256 in 64 KiB blocks. Native ReadFile loops on partial reads; zero before expected length is corruption. Retain handle through result construction. |
| Validate optional replacement capability | CreateTransaction/Rtl thread scope; rooted existing leaf writer `0x00100083`; FSCTL_TXFS_GET_TRANSACTED_VERSION | FILE_OPEN; READ+DELETE sharing, deny WRITE; no store creation or file mutation | Check single link/type/volume and require UNCOMMITTED. Close file, rollback and restore prior native context. C.3 owns gating Writable and Save/Recover/Abandon after owner validation; no schema authority is inferred by this primitive. Probe cannot guarantee all future rename permissions. |
| Seal prepared directory | Rooted source open, directory access plus DELETE (`0x001100A1`); `NtSetInformationFile(FileRenameInformation=10)` with held destination RootDirectory and simple target name | Existing source; share READ+WRITE, deny DELETE. Rename ReplaceIfExists=false | Both parent chains retained; source handle stays open across same-volume atomic rename. Existing target fails; no copying, destination filling, or overwrite. Check same source handle identity afterward. Actual NTFS rights/share feasibility still requires execution. |
| Replace sibling metadata; target absent | `CreateTransaction`; `RtlGetCurrentTransaction`/`RtlSetCurrentTransaction`; rooted native source FILE_OPEN `0x00110083`; `FSCTL_TXFS_GET_TRANSACTED_VERSION`; native FileRenameInformation=10 | Source share READ+DELETE, deny WRITE; transacted writer required; absent destination, ReplaceIfExists=false | Same-parent distinct sibling names. Verify actual TxF writer enlistment, single link/type/volume before mutation; flush source and verify source identity after rooted rename. A competing destination appearing before absent-target rename causes conflict/failure without overwrite. Close transacted handles, retain parent pins and KTM locks, then CommitTransaction. Restore prior native thread context in finally. |
| Replace sibling metadata; target existing | Same transaction/source; target rooted FILE_OPEN writer `0x00100083`; both handles require `UNCOMMITTED` version; native `NtSetInformationFile(FileRenameInformation=10)` | Source/target share READ+DELETE, deny WRITE. ReplaceIfExists=true. No POSIX flag | Actual TxF writer locks, rather than delete-denying sharing, exclude external content and namespace mutation in the parent's passing NTFS fixture. Validate ordinary single-link same-volume handles, then close target handle before standard rename while its TxF lock remains owned by the live transaction. Parent pins and source writer remain live during rename. Close remaining transacted handles before commit while TxF locks retain protection; restore ambient context on success/error. No multi-file store-transaction claim. Any error requires bounded observation and no weaker retry. |
| Enumerate | Query pinned case handle directly; child directories opened rooted with FILE_LIST_DIRECTORY; `NtQueryDirectoryFile(FileDirectoryInformation=1)` | FILE_OPEN for children; directory share READ+WRITE, no DELETE; one 64 KiB native buffer per recursion frame; RestartScan=true on each fresh enumeration | Stream all ordinary case-relative files/directories including debris. Never reopen native `.` or dispose the borrowed case handle. Reject reparse/aliased file handles; no link traversal or cleanup. Count before returning the next entry, ceiling 3,000,000. Directory depth ceiling 10 beneath case (8 beneath toolbox/prototype-store). Strict path component ceiling 12. Held writer.lock is inspected through the owned exclusive handle rather than reopened. Dispose iterator to release owned child handles/buffers. |

## Failure and threat boundaries

Typed codes currently include unsafe_path, unsupported, busy, missing, initialization_conflict, limits_exceeded, corrupt, and io_error. Windows error numbers are retained in error messages without dumping content. Failed native rename does not promise unchanged names; C.3 must inspect actual evidence under its lease. Previous-pointer preservation and semantic read-back are C.3 responsibilities. Adapter source-handle checks do not prove the store's record bindings.

The intended guarantee is ordinary Windows path/reparse/sharing races, not privileged filesystem surgery or hardware power-loss durability. ACL ownership is not used as a same-user exclusion claim. The parent's actual NTFS runs demonstrated rooted writer enlistment, replacement success, external namespace/content exclusion before and after transacted handles close, and tested failure/process outcomes. Build 18211 also executed the stronger independent-thread/reparse/hard-link/probe fixtures. Separate fine Create/Abandon and earlier Save matrix evidence is recorded above; the final combined rerun and fresh review remain pending. Mutation integration must probe the optional kernel capability rather than infer it solely from fixed NTFS.

TxF is deprecated and may be unavailable on future Windows versions. This dependency is confined to a single metadata replacement and never wraps the multi-file store protocol. `CreateTransaction` uses null security attributes (non-inheritable), DO_NOT_PROMOTE, and a 10-second pre-prepare timeout. Transaction handles are SafeFileHandle-owned. Prior native transaction context is saved/restored synchronously without await or worker handoff. Uncommitted transactions request rollback before disposal; commit failures never imply no publication. The timeout is not a promise to abort an already prepared commit or to impose a hard disk-I/O deadline. No service, account, privilege, ACL, or production-root configuration is introduced.

Critical native-context cleanup: restoring the prior context must both report success and be confirmed by RtlGetCurrentTransaction; an observed value equal to the owned handle is never eligible for closure. A restore failure attempts RtlSetCurrentTransaction(0) and observes the TEB again. Confirmed zero permits closure with an unsupported diagnostic. A confirmed different installed handle also permits closing only this detached owned handle, with an unsupported diagnostic. If the owned handle remains installed, it is retained strongly in one ThreadStatic SafeFileHandle slot; it is not closed, marked invalid, abandoned, or silently treated as restored. The faulted-thread guard checks session creation, Native.Open, replacement-transaction construction/commit, and directory enumeration before further contained I/O. It retries detachment and refuses with unsupported while the owned handle remains installed. Once detachment is observed, the guard clears and disposes the slot. A prior detached stranded slot is disposed before retaining a newly observed installed handle, keeping ownership bounded to one slot per thread.

Ordinary contained operations require zero caller-native ambient transaction. The adapter neither joins nor wraps host transactions: nonzero context returns unsupported before any case claim or byte write, leaving the caller's context untouched. Only the adapter's explicitly flagged transactional opens within its own replacement/probe scope may enlist; the active owned SafeFileHandle is tracked per thread and checked against the observed Rtl context, then actual writer enlistment is verified with FSCTL. Disposal clears the active marker before checked context restoration/stranded retention. The external-ambient fixture restores zero and rolls back/disposes its own transaction; its dedicated thread retains ownership rather than closing a handle if fixture detachment itself fails. The parent reported this fixture passing in the latest actual NTFS aggregate.

| Guard | Demonstrated evidence | Limit / refusal behavior |
|---|---|---|
| External ambient transaction | Dedicated-thread refusal preserved caller context, exact case paths and bytes in the latest parent NTFS aggregate | Ordinary operations require zero context; this adapter does not enlist in or coordinate host transactions. |
| Owned replacement/probe context | Active per-thread handle check, FSCTL transacted-writer verification, actual target-close rename and handoff races passed | Requires optional local TxF/KTM support. Probe establishes enlistment/lock availability, not every later rename permission. No weaker path/sharing fallback. |
| Context cleanup | Normal restoration assertions passed; stranded-handle failure branch was added for review | Native restore/clear failure was not forced in runtime tests. A still-installed handle remains owned, and further contained I/O on that thread is refused until detachment. |
| Root/leaf containment | Fixed NTFS, pinned rooted opens, hard-link/reparse and independent mutation fixtures passed | Applies to explicitly approved roots and supplied exclusions; privileged filesystem surgery is outside the guarantee. Sessions/iterators are not consumed concurrently. |
| Publication and interruption | Native replacement plus separately reported process matrices demonstrate tested operation boundaries | No multi-file store transaction, hardware power-loss durability, production ownership, or domain approval claim. Final combined/matrix results and remaining edge-review outcome are pending. |

If native detachment remains broken, that thread cannot safely continue contained I/O. Ownership remains until a later successful detachment, thread termination (which discards its TEB), or process exit. There is no Env.FailFast or untracked SetHandleAsInvalid leak. Failed restore/clear is a critical platform diagnostic, not evidence that a preceding commit did not publish; C.3 must preserve its normal ambiguity/read-back handling. Failure branch safety is reviewed code logic; the actual runtime evidence covers normal context restoration, not forced failure of Windows' native setter.

Oplocks were investigated as an alternative. Namespace operations can require an oplock-break acknowledgment, including an adapter's own operation; acknowledging/releasing before rename does not establish a protected destination handoff. No unproven oplock fallback was added.

## Exact harness entry points

Internal `Native.TestBoundary` remains a null observer in production. `WriteNew` emits `write_new_opened`, `before_native_write:<relative>` after verified create-new opening, `after_native_write:<relative>:<offset>` after each successful 64 KiB-or-smaller block, `before_native_flush:<relative>`, `after_native_flush:<relative>` after successful FlushFileBuffers, and `after_native_readback:<relative>` after exact length/byte read-back. Offsets are invariant cumulative byte counts; path text is the exact relative argument supplied by the deriving caller. Empty writes emit all non-block callbacks. Replacement emits `replace_opened`, existing-target `replace_target_closed`, `replace_before_commit` after transacted handles close, and `replace_after_commit` only after successful CommitTransaction. These hooks support the parent's isolated termination worker; adding them is not evidence that its fault matrix has run.

Additional auditor-requested flush hooks: `before_native_replacement_flush` precedes the replacement sibling's native flush and `after_native_replacement_flush` follows its success, both with verified TxF handles retained. `before_native_lock_flush`/`after_native_lock_flush` bracket the newly created empty writer.lock's native flush, while its exclusive handle and ancestor pins are retained. Existing lock acquisition never flushes or emits those initialization hooks. These names contain no path suffix and are automatically included by the parent's Newton native-prefix filter. Parent reported raising matrix bounds to 130 Save / 32 Create / 24 Abandon; these are limits, not reported executed counts.

All methods are static on `ParcelWorkflowAddIn.Tests.Workflow.ToolboxContainedFileSystemTests`:

- `RunAll()` runs pure contract then actual NTFS groups.
- `PurePathAndAttestationContract()` requires no filesystem mutation.
- `RunActualNtfs()` calls nine actual groups below; unsupported roots fail explicitly as UNVERIFIED. All nine passed in the parent's latest reported Ctmp NTFS aggregate. Build 18211 remains the earlier eight-group result; the final combined/process-matrix run is separate pending evidence.
- `ImmutableWritesReadsAndStreamingVerification()`
- `ExclusiveClaimsAndLockContention()`
- `SealAndGuardedReplacement()`
- `HardLinksReparseAndPreopenedHandles()`
- `DeterministicPinnedBoundaryRaces()`
- `ReplacementFailureRetainsEvidence()`
- `ProcessContentionAndTermination()`
- `ReplacementCapabilityProbeDoesNotMutate()`
- `ExternalAmbientTransactionIsRejectedWithoutMutation()`
- `RunWorker(string[] args)` returns an exit code; parent dispatches `--toolbox-contained-worker <root> case <hold|contend>` by passing `args[1..]` **before** its normal registry. Worker validates the disposable fixture prefix and exactly one GUID-N child component, opens only an existing case/lock, and never launches the suite recursively.

Set `TOOLBOX_DISK_TEST_ROOT=C:\Users\js91482\AppData\Local\Temp\2\toolbox-disk-tests` in the parent test process. Each fixture creates one fresh GUID-N subdirectory; workers inherit the same environment and validate against the same base. Without the variable, the base remains `Path.Combine(cwd, ".artifacts", "toolbox-disk-tests")`. The environment variable supplies the externally authorized test root, not a production default; no fixture cleanup is performed.

Child holder prints `TOOLBOX_LOCK_READY`, flushes stdout and waits up to 30 seconds for stdin or termination. Contender succeeds only on busy. Parent fixture has 10-second bounded startup/exit checks and finally kills/waits only its own workers. After actual process termination it reacquires the same lock and observes unchanged empty lock bytes. The parent reported this group passing in the authorized Ctmp NTFS run.

The earlier passing fixture revision ran competing Win32 I/O with the caller's native thread transaction explicitly cleared and restored. The current revision, passed in parent build 18211, creates independent native threads and asserts zero native transaction context in each competitor. Threads are joined with a 10-second fixture deadline; failure unwinds the transaction and then joins any outstanding competitor before exiting. `DeterministicPinnedBoundaryRaces` checks `replace_opened`, `replace_target_closed`, and `replace_before_commit`, including target/source rename, target byte write, outside-sentinel hard-link overwrite substitution, new target-hard-link creation, target reparse conversion, and ancestor rename attempts. Reparse conversion is first calibrated on an unguarded disposable ordinary file using non-Microsoft GUID reparse data; unavailable fixture support is explicitly UNVERIFIED. Assertions check retained alias/outside bytes, absence of new aliases, and caller transaction-context restoration.

## Fresh independent review delta

The parent has three fresh native review roles running against the final adapter/test files; outcomes remain pending. Review scope includes the verified-writer/target-close/rename/remaining-close/commit ordering; rollback/error observation and native-context restoration; optional capability probe before C.3 Writable/Save claims; independent competitors at all three handoffs; calibrated hard-link/reparse attacks and sentinel preservation; handle cleanup when native entry points are missing; and native write/flush/read-back/after-commit termination hooks. The probe's snapshot fixture passed in the current aggregate, demonstrating no new paths or changed bytes. Newton's fine-boundary process results remain reported separately by matrix/build. No independent-review outcome or final story acceptance is claimed by this document.

## Primary API references

- [NtCreateFile](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-ntcreatefile): parent-relative names, native options/dispositions/sharing.
- [FILE_RENAME_INFORMATION](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information): rooted native rename, access/volume constraints and POSIX flags.
- [FileRenameInformationEx protocol layout](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/4217551b-d2c0-42cb-9dc1-69a716cf6d0c): replaced handles remain valid under POSIX semantics. This reference does not establish the guard combination's successful local NTFS execution.
- [TxF isolation and writer locks](https://learn.microsoft.com/en-us/windows/win32/fileio/txf-basic-concepts): external modifications are excluded by transacted writer locks in addition to ordinary sharing.
- [TxF programming considerations](https://learn.microsoft.com/en-us/windows/win32/fileio/programming-considerations-for-transacted-fileio-): namespace isolation and closing transacted handles before commit.
- [TXFS_GET_TRANSACTED_VERSION](https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-txfs_get_transacted_version): UNCOMMITTED identifies a transacted writer; NONTRANSACTED must be refused.

Checked 2026-10-11. References document mechanisms; actual runtime evidence above remains separate.
