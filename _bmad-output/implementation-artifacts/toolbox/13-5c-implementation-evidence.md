# 13.5C implementation evidence

Date: 2026-10-11. Branch: `refactor/toolbox-case-workspace`. Baseline: `feebb5c`. Scope: isolated ordinary-file prototype only.

## Authorization and status

JotaPe explicitly requested completion of C.1, C.2 and C.3. This supersedes preparation-only restrictions for these three slices, not D/E/B or presets/handoff. Implementation, runtime verification and independent code review are complete for the isolated prototype. The three stories are review-ready; final JotaPe scoped acceptance remains separate and is not inferred from execution authorization.

No accepted A production file, domain action/hash, operational writer, UI, settings/DAML, FGDB, remote adapter, package or host activation was modified. No commit, deployment or production data operation was performed.

## Implemented responsibilities

- C.1: explicit disk field/type/nullability manifest, strict bounded JSON, independent golden canonical UTF-8 bytes/digests, owned preparation, immutable request/revision identity, pure transitions, initialization classification and bounded exact ancestry proof.
- C.2: rooted Windows native handles, pinned physical ancestor/exclusion identities, fixed NTFS capability, single-link/no-reparse checks, exclusive existing writer lease, create-new files/directories, streaming asset verification, same-volume absent-target seal and kernel-isolated sibling replacement. See [operation table](13-5c-windows-operation-table.md).
- C.3: detached Create/Open, exact Save/retry, explicit stage/seal/publication and historical-tail Recover, caller-audited Abandon, retained IDs/evidence, conservative fresh policy/capability gates, projected admission budgets, cancellation handling, relocation fixtures and bounded task-owned child workers.

The external policy catalog may span current/proposed revisions. After A validates transport structurally, C selects only each bundle's declared policies for A's unchanged trusted decode. Unknown/missing/revoked trust never activates a policy. Stored bytes, pins and actors grant no domain permission.

## Platform proof and limitations

Actual handle inspection found the worktree D volume is fixed exFAT. It was rejected before case creation. Authorized filesystem tests use unique disposable directories below `C:\Users\js91482\AppData\Local\Temp\2\toolbox-disk-tests`, selected explicitly through `TOOLBOX_DISK_TEST_ROOT`, on fixed NTFS.

The initially attempted deny-delete target guard plus POSIX rename failed with Windows error 32. It was replaced, not weakened: one KTM/TxF transaction enlists each rooted source/target as a verified transacted writer; kernel locks protect the closed-target and closed-handle handoffs through commit. An owner-file capability probe performs no file writes, rename or recovery before admitting writable status/mutation. Missing capability fails closed. TxF is an optional/deprecated Windows dependency; this is not a general filesystem adapter.

Actual NTFS fixtures verify seal/replacement, initial/existing targets, independent-thread leaf substitution/content/ancestor races, hard-link/reparse rejection, preopened incompatible handles, native failure outcomes, non-mutating capability inspection, independent-process lock contention and process-death release. Caller-owned native transactions are explicitly rejected without mutation or context changes; only adapter-owned replacement transactions may enlist. The final aggregate run passes all nine constituent native groups.

Only process-interruption recovery is claimed. No hardware power-loss guarantee, multi-file atomic Save, production ownership, domain approval or cross-clone writer coordination is claimed. Open never repairs owner/lock/layout or discards replacement debris. Unexpected/torn/ambiguous evidence is retained. Historical payloads are verified only when selected for inspection/retry/recovery; ancestry traversal verifies bounded metadata, not every historical payload.

## Verification

Commands use the existing net8.0-windows/x64 solution and fail-fast substring-OR harness. No test framework upgrade.

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-disk/ /p:UseSharedCompilation=false /m:1
$env:TOOLBOX_DISK_TEST_ROOT = Join-Path ([System.IO.Path]::GetTempPath()) 'toolbox-disk-tests'
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll 'toolbox disk'
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll 'toolbox' 'workspace planner' 'settings workspace' 'workflow execution settings' 'review persistence' 'review approval'
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

| Lane | Final actual evidence |
|---|---|
| Solution build | Pass, zero warnings/errors; final stable build 45969, 16.92 seconds |
| Pure disk protocol | 20 named groups pass |
| Preserved focused lane | 158 named groups pass, excluding newly added disk groups |
| Accepted A codec lane | 23 named groups pass, included in preserved 158 |
| Native adapter | Aggregate passes, including independent-thread and capability-probe deltas |
| Fine process interruption | 154 scenarios pass: 112 Save, 25 Create and 17 Abandon, including all explicit flush/target-verification barriers |
| Full harness | 207 groups pass, then pre-existing CompareWorkspaceXamlTests:83 footer-progress assertion; later groups are not implied green |
| Final combined focused lane | Pass: 197 groups = preserved 158 + new disk 39; final stable run 27879 |
| New disk lane | All 39 registered groups pass in the final combined run; includes native aggregate, B history/recovery, contention, relocation and dedicated review regressions |

Each process scenario freezes exact supplied intent/bytes, pauses one owned child at the selected native/coarse barrier, confirms serialization where applicable, terminates that child, reopens in a fresh process, and checks old/new pointer, retained bytes and explicit resolution/refusal. Workers and pipe readers have bounded waits and are joined before evidence is reused. Save assets include three actual native write blocks to expose incomplete staging, not only managed exceptions after complete writes.

## Independent review and corrections

Three independent roles reviewed the runtime diff without inherited conversation context: blind hunter, edge-case hunter and acceptance auditor. They found concrete defects; fixes include explicit empty-assets directory completeness, retained-abandonment refusal, forward-sibling abandonment refusal, short-candidate name bounds, fresh current/proposed trust consistency, original typed conflicts, projected Save/Recover/Abandon admission, strict lowered-depth creation/encoding, unique journal-to-revision ownership, and registration of every new suite. Dedicated regressions retain exact refusal/unchanged-byte assertions.

The suggestion to hash every historical payload during Open was rejected as contrary to the admitted selected-payload/bounded-ancestry contract. Selected historical retries/tail recovery still verify the complete exact inventory before returning a receipt.

All three reviewer roles closed their final static delta review with no remaining production-code blocking findings. The final guard delta rejects external ambient transactions and verifies adapter-owned enlistment. All dedicated runtime regressions pass. No independent review is treated as execution evidence, and no drafted test is treated as passed.

The abnormal OS transaction-restoration/detachment failure branch was statically reviewed, not artificially fault-injected. It confirms detachment before closure, or strongly retains one bounded per-thread handle and refuses further native I/O until safe detachment/thread exit. Normal context restoration, independent competitors and real external-ambient refusal were executed successfully. This limitation does not expand the tested Windows/NTFS capability scope.

The complete registry retains all 977 original names/order and appends 39 groups (1016 total). The full harness still stops before later groups at its unchanged Compare XAML failure; those later groups are not claimed green by the full run. The accepted default-PointIds inspection limitation is unchanged; disk B proof uses admitted non-default point sets.

## Changed runtime files

New production files under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/`:

- `ToolboxPrototypeDiskContract.cs`
- `ToolboxPrototypeRecovery.cs`
- `ToolboxContainedFileSystem.cs`
- `ToolboxPrototypeRevisionStore.cs`

New tests:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Tests/ToolboxPrototypeDiskContractTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxContainedFileSystemTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxPrototypeRevisionStoreTests.cs`

Updated runtime file: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`, with appended named groups and reserved worker dispatch only. All original group names/order, substring-OR selection and normal fail-fast behavior are preserved.

Scoped story/spec/status/context/evidence documents were updated to record authorization and actual results. Earlier unrelated story preparation changes remain intact and uncommitted.
