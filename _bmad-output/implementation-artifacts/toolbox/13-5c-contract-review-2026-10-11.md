# 13.5C Prototype Disk Contract Review

Date: 2026-10-11
Scope: static review of the proposed 13.5C contract against accepted 13.5A, project rules, and Microsoft Windows API documentation.
Original result: changes required before implementation; four P1 and six P2 findings. Update 2026-10-11: JotaPe requested the corrections, and all ten are now incorporated in the 13.5C contract. This resolves the documentation gaps; adapter feasibility and runtime correctness remain to be demonstrated. Story and parent remain backlog; no runtime code or tracker status changed. The request to correct the contract is not recorded as runtime execution authorization.

Evidence line references refer to the original reviewed 122-line [13.5C story](13-5c-add-isolated-prototype-disk-revisions.md), before the corrections below were applied; they are historical, not current line locations. The finding descriptions and proposed corrections are retained as review history. The corrected story is the contract source.

## Correction Disposition

| Finding | Contract disposition | Remaining implementation evidence |
|---|---|---|
| R1 | Applied: explicit Create/Open and incomplete initialization handling | Termination and competing-initializer tests |
| R2 | Applied: serialized observational Open with existing lock | Paused-writer/reader process tests |
| R3 | Applied: adapter operation-table capability gate and handoff protection requirements | Concrete Windows mechanism, NTFS sharing and race proof; no capability assumed |
| R4 | Applied: complete staged-directory seal and conservative resume rules | Directory rename/partial-location termination tests |
| R5 | Applied: journal transition table, sibling publication and state invariants | Replacement-error and torn-journal tests |
| R6 | Applied: current-ancestry historical publication proof and explicit tail recovery | Retry/recovery after successor commits |
| R7 | Applied: explicit record/hash inputs, asset order/null rules and checked sequences | Independent golden bytes/digests and changed-field tests |
| R8 | Applied: staging/debris/entry/depth and cumulative traversal budgets | Streaming limits and unexpected-layout tests |
| R9 | Applied: transport integrity separate from policy inspection/writability | Policy warning and declared-asset corruption tests |
| R10 | Applied: complete source-locked relocation fixture, destination lock preparation and clone limits | Consistent/pending/mixed-copy tests |

## Findings And Proposed Corrections

### R1 - P1: Initialization has no interruption or concurrency protocol

Evidence: story lines 32, 47 and 69 require an exclusively created directory, valid owner and writer lock, but define no order or recovery evidence for creating them. Death after directory/lock creation but before owner publication leaves a nonempty ownerless store that must be rejected. Concurrent initializers have no specified winner or typed result.

Proposed correction: separate Create from Open. Create exclusively claims a new case directory, creates and holds the writer lock, and publishes a flushed, read-back owner before admitting saves. Existing directories are never adopted. Incomplete initialization returns a typed initialization diagnostic and preserves all evidence; it cannot be recovered as a writable store without a separate initialization protocol. The caller may explicitly choose a different new directory. Open never creates owner, lock or directories. Specify owner contract/schema, case, lowercase GUID store_id and exact prototype mode; reject unknown fields.

Acceptance: terminate after every initialization boundary; two processes attempt creation of the same directory; verify one owner and no adoption, truncation or cleanup of the losing/incomplete store.

### R2 - P1: Read-only Open lacks a consistent observation protocol

Evidence: lines 69 and 80 lock publication/recovery, but reads never mutate and have no locking or consistency rule. A reader can observe a new pointer with an old journal or a receipt while the writer is finalizing, and classify an in-flight operation as corruption.

Proposed correction: Open acquires the existing exclusive writer lock without creating or modifying it, for the entire bounded observation and owned-byte read. Contention returns busy; it never attempts recovery implicitly. Return a detached verified snapshot and release all handles after bytes and diagnostics are frozen. Missing lock in an initialized store is corruption, not a reason to create a replacement lock. This intentionally serializes prototype reads and writes.

Acceptance: pause a child writer before/after pointer and receipt publication while another process opens; Open returns busy during the write and a consistent pending/verified observation afterward.

### R3 - P1: Handle pinning and file replacement need a compatible concrete protocol

Evidence: lines 67 and 77 require lifetime handle inspection and sibling replacement, but omit desired access/share modes and the lifetime of replaceable leaf handles. A delete-denying handle on current.json or a journal conflicts with ReplaceFileW's DELETE access; releasing the guard without another demonstrated protection opens a replacement race. Ancestor pinning alone does not specify how competing leaf replacement is excluded.

Proposed correction: require a reviewed adapter operation table covering immutable reads, create-new writes, directory sealing, pointer replacement and journal replacement. For each operation, state exact handle rights, sharing, file/volume identity checks and protection during any handle handoff. Protect mutable leaves using a demonstrated handle-based replacement mechanism or scoped directory access control in the disposable root. If the adapter cannot prevent or reject a competing mutation before protected/outside-root bytes are changed, return unsafe_path/unsupported and do not enable saves. Do not claim that the candidate flags alone establish safety.

Acceptance: actual NTFS tests for replacement with the selected sharing modes, existing competing handles, leaf substitution and ancestor rename/reparse attempts. Include attempts timed at the adapter's handoff boundaries.

Microsoft documents DELETE access for replacement and sharing conflicts in [ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew) and [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew). The incompatibility is conditional on the chosen leaf sharing modes; the existing story does not select those modes.

### R4 - P1: Interrupted seal cannot be deterministically resumed

Evidence: lines 76 and 80 prohibit overwriting revisions and allow resume of a pinned intent, but do not define seal as a directory rename or specify how partial destination files are handled. A crash halfway through copying staged files into revisions/<id> leaves create-new collisions and no valid complete revision.

Proposed correction: construct and verify the complete revision directory in staging, including revision.json and exact inventory, then seal by a demonstrated same-volume directory rename to a create-new destination. Recovery verifies actual locations and complete contents rather than trusting journal state. A complete matching destination can be reused for the identical operation; partial, conflicting, duplicated or ambiguous destinations are quarantined, never filled in or overwritten. A prepared operation with partial staging may be explicitly abandoned under the existing rules; automatic resume need not repair arbitrary partial files. Define which complete staging states can resume without modification.

Acceptance: terminate immediately before/after seal and before journal update; test partial staging, conflicting destination and duplicate staging/destination evidence. Preserve the original operation/revision IDs.

### R5 - P2: Journal updates and replacement failures have no transition table

Evidence: lines 54 and 76-82 name states and require atomic abandonment, but do not apply a publication protocol to other journal transitions or define terminal invariants. Rewriting a journal in place can destroy its previous pointer evidence. ReplaceFileW also documents failure outcomes that change file names, so an exception cannot imply an unchanged store.

Proposed correction: create the initial journal exclusively. Publish every later journal state using flushed, create-new sibling metadata and the reviewed replacement adapter, preserving immutable intent and previous pointer. Define permitted transitions prepared -> sealed -> published -> committed and prepared/sealed -> abandoned; infer interrupted forward progress only from verified disk evidence. Committed and abandoned are terminal. Keep replacement temporaries under deterministic operation-derived names, enumerate them within bounds, and quarantine ambiguous replacement results. Specify invariants for each reopened state and allow only explicit recovery mutations.

Acceptance: inject partial writes and all documented replacement failure classes at each journal transition, including abandonment; prove no false committed/abandoned result. Source: [ReplaceFileW error outcomes](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).

### R6 - P2: Historical publication proof is undefined after a successor commit

Evidence: line 71 promises retries after later saves, but line 80 reconstructs a missing receipt only when current points directly to that operation. A missing receipt for an older committed revision is not resolved by that rule; a sealed orphan must also never be mistaken for a historically committed revision.

Proposed correction: a historical operation is proven published only when its exact revision id/digest/sequence lies on the verified parent chain of the current published revision and its matching intent validates. A valid receipt must also agree with this chain and intent. Verify that operation's exact bundle/assets before returning or reconstructing its receipt, within the bounded traversal budget. Sealed revisions outside the chain are not committed. Reconstruct a missing historical tail only through explicit Recover; ordinary Open is observational. An identical Save retry may return an existing verified receipt, or report recovery_required if the tail is missing.

Acceptance: commit R1 then R2, remove R1's receipt or leave an earlier journal state, and retry R1; explicit recovery recreates the same receipt without moving current. Repeat with an abandoned/off-chain R1 and require rejection.

### R7 - P2: Hash inputs and asset identity rules are not fully specified

Evidence: lines 51-57 compare expected-parent sequence but list only id/digest in the immutable parent, omit the complete expected_parent shape and owner schema, and delegate canonicalization without fixing scalar/array rules. Assets are content-addressed but duplicate hashes, ordering and optional null versus empty representation are unspecified.

Proposed correction: define expected_parent as null or {revision_id, revision_digest, sequence}, and bind all three in request hashing. Bind contract/schema, store_id, full case, operation/revision IDs, actor, exact round-trip prepared_at, parent and byte inventory. Specify ordinal object-field ordering, invariant UTF-8 JSON encoding, exact string preservation and checked integer sequence arithmetic; exclude each digest's own field. Preserve caller asset order; reject duplicate digests rather than silently deduplicating. Represent null asset inventory distinctly from an explicit empty array in request/revision metadata. Pin independent golden bytes/digests and changed-field tests before filesystem integration.

Acceptance: every intent-field change conflicts; null/empty and asset order differ; duplicate asset hashes fail; sequence overflow returns a typed error before I/O; golden fixtures identify exact v1 bytes.

### R8 - P2: Storage traversal limits omit staging and replacement debris

Evidence: line 61 caps revisions/journals/receipts and metadata traversal, but not staging entries, replacement siblings, unexpected files, directory depth or JSON depth. Retained abandoned data and interrupted attempts can grow outside the stated budgets before validation finishes.

Proposed correction: bound the entire directory observation, including owner/lock/current, staging, all metadata temporary files, abandoned artifacts and unexpected entries. Specify JSON depth 64, directory depth 8 beneath the store root, at most 10,000 staging operation directories, and a checked total entry budget of 3,000,000; allow test injection downward only. Count entries while streaming enumeration, stop at the limit without allocating the whole tree, and never follow reparse points. Apply the 64 MiB metadata traversal budget across all metadata examined for a request. Define recognized temporary paths; unknown entries return an explicit layout diagnostic without deletion. These caps may cause a conservative limit diagnostic before per-category maxima are reached.

Acceptance: adversarial deep/large staging, unknown files, malformed/deep JSON, orphan temporaries and retained abandoned operations hit typed limits with bounded work and no cleanup.

### R9 - P2: Policy/source asset failure conflates transport integrity and domain availability

Evidence: line 93 groups absent policy/source assets with corruption, while lines 26 and 88 preserve A's inspection behavior. Accepted codec Decode succeeds with policy_unavailable/policy_untrusted warnings; opaque C assets have no declared source-role bindings before D supplies provenance. Treating all warnings as corruption changes A semantics; treating all warnings as writable success contradicts AC6.

Proposed correction: keep hydration success and warnings unchanged. Expose transport verification separately from writable status. Missing/mismatched bytes declared in C's inventory are corruption. Missing external trusted policy or policy_unavailable yields inspectable transport-verified data with warnings and a read-only policy_inspection_required result under C's conservative writer gate; it does not invalidate the stored bytes or prove domain authority. Explicitly exclude resolving external source availability from C: only declared opaque assets and A's existing structural references are checked. No remote fetch or new source-role interpretation.

Acceptance: distinguish corrupt declared asset, intact bundle with no policy asset, supplied but untrusted policy, and an opaque asset without source provenance. Assert no altered A codec, permissions or domain readiness.

### R10 - P2: Relocation does not define a consistent copy boundary

Evidence: line 94 promises relocation of a copied store, but does not define how the copy excludes concurrent writes or which retained records must be copied. Copying a live directory can produce individually valid files from different moments and omit evidence needed for ancestry/recovery.

Proposed correction: C does not author a copy/export API. Its relocation fixture copies a quiescent complete prototype tree while the original writer lock is held, excluding only the open lock file; create a new empty lock in the new exclusively claimed isolated destination as an explicit relocation preparation step. Retain owner/store/case identity, all journal/receipt/revision/staging evidence and recognized recovery metadata unchanged. Release the original lock only after the source read is complete; validate the new root externally and apply normal bounded Open/recovery rules. A live or incomplete third-party copy receives normal pending/quarantine/corrupt diagnostics, never automatic repair or newest-file selection. Independent clones have local locking only and no cross-root single-writer guarantee.

Acceptance: verify full idle relocation, a preserved pending operation, intentionally mixed/incomplete copies, new root containment and simultaneous independent clones without claiming coordinated writers.

## Implementation Authorization Proposal

R1-R10 document corrections have been requested and applied. Runtime implementation authorization remains outstanding. The next implementation scope is only the corrected isolated 13.5C prototype slice in this worktree. Preserve accepted A bytes/DTOs/hydration, operational stores, UI/settings/DAML, source/remote operations and domain hashes. First pin schema/protocol fixtures, then demonstrate the Windows adapter; a failed containment capability blocks writes rather than weakening the contract. Implement revision save/open/retry/recover/abandon and named pure/NTFS/child-process tests. Report build, focused predecessor preservation and full-harness results separately. Obtain scoped acceptance after implementation review. Keep order C -> D/E -> B; D/E/B require their own applicable gates. No commit, deployment, parent completion or production ownership approval is included.

## Verification And Limits

Reviewed the complete 13.5C story, accepted 13.5A contract and codec, relevant decision-register entries and current project-context continuation rules. Checked official CreateFileW and ReplaceFileW documentation. These are contract findings, not demonstrated runtime failures. Corrections were checked for internal consistency, relative-link targets and whitespace. No build or runtime tests were run because no runtime implementation was changed. Windows containment, process interruption and copy behavior remain to be proved during implementation. This review is not an independent implementation acceptance.
