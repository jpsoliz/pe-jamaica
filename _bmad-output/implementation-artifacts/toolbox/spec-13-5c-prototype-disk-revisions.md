---
title: Isolated prototype disk revisions
type: feature
created: 2026-10-11
status: done
baseline_commit: feebb5c
context:
  - _bmad-output/project-context.md
  - _bmad-output/implementation-artifacts/toolbox/13-5c-add-isolated-prototype-disk-revisions.md
  - _bmad-output/implementation-artifacts/toolbox/13-5c-1-define-disk-contract-and-recovery-logic.md
  - _bmad-output/implementation-artifacts/toolbox/13-5c-2-define-windows-containment-locking-and-replacement-adapter.md
  - _bmad-output/implementation-artifacts/toolbox/13-5c-3-define-revision-save-open-retry-recover-and-abandon.md
---

<frozen-after-approval reason="User explicitly requested completion of corrected C.1-C.3">

## Intent

Implement the three reviewed C stories as one isolated ordinary-file revision store. Separate pure metadata/recovery, Windows containment and store operations; complete build/tests/independent review without touching accepted A or operational writers.

## Boundaries & Constraints

Always preserve exact bytes/case/IDs/audit/pending states, enforce approved NTFS root/exclusive writer, strict bounded JSON, expected parent and immutable request identity. Verify actual contents before returning committed; explicit recovery/abandonment only, no automatic repair/deletion. User authorization covers existing dirty documentation and all three C slices.

Ask First only for changing accepted A/domain semantics or production/FGDB/source/remote work; none is part of this implementation.

Never modify legacy writers, UI/settings/DAML, output/package adapters or remote operations. No commit/deployment. No full-suite/power-loss/host authority claim without evidence.

## I/O & Edge-Case Matrix

| State | Behavior |
|---|---|
| Valid new intent/current parent | Freeze, journal, stage, seal, publish, receipt and terminal verification |
| Identical retry after successor | Original verified receipt, no pointer move or ID allocation |
| Conflict/abandoned | Typed refusal, unchanged evidence |
| Interrupted complete staging/new publication | Explicit exact resume/tail recovery |
| Partial/contradictory/missing evidence | Pending/quarantine/corrupt; no guessed latest revision |
| Unsafe root/link/race/unsupported storage | Reject before unsafe mutation |
| Missing policy trust | Transport inspection only, no normal save |

</frozen-after-approval>

## Code Map

- Persistence/ToolboxWorkspaceStorageCodec.cs: accepted A, unchanged.
- Persistence/ToolboxPrototypeDiskContract.cs and ToolboxPrototypeRecovery.cs: new pure protocol.
- Persistence/ToolboxContainedFileSystem.cs: new narrow native adapter.
- Persistence/ToolboxPrototypeRevisionStore.cs: new owner orchestration.
- Tests/Workflow new disk tests and Program.cs: named groups and bounded child-worker dispatch.

## Tasks & Acceptance

- [x] Implement strict metadata/hash/limits and pure planner with golden/negative fixtures.
- [x] Demonstrate rooted NTFS read/create/seal/replace and process locking/race rejection.
- [x] Compose exact store operations, hydration and fault/process tests.
- [x] Run build, new/preserved focused/full lanes; independently review/fix and document evidence.
- Given exact input and fresh external trust, save/reopen preserves A state without actions/authority.
- Given failures/retries/concurrency/unsafe paths, verified current evidence is retained and ambiguous state is honest.

## Spec Change Log

- The D worktree was verified exFAT and refused; explicitly approved Windows Temp NTFS roots supplied actual platform proof.
- Failed deny-delete/POSIX replacement was replaced by verified rooted per-leaf KTM/TxF isolation, with no weak fallback.
- Independent findings corrected completion, ownership, trust, admission, cancellation and retained-candidate rules without changing A or domain semantics. Final scoped acceptance remains separate from technical completion.

## Design Notes

Reviewed detailed C parent and children remain contract source. All metadata is separate from A; transport hashes do not certify geometry/acceptance. Integration uses the filesystem session API with derived paths, never arbitrary path-based fallback.

## Verification

Use solution build with obj-toolbox-disk/, UseSharedCompilation=false, m:1. Run explicit disk groups and preserved Toolbox/planner/settings/review filters, then full harness. Record actual counts and known Compare XAML fail-fast separately. Child processes use unique explicitly supplied disposable NTFS roots, bounded waits and no unrelated process termination.

Final results: build zero warnings/errors; combined focused 197 passes (158 preserved + 39 new); 154 native process-interruption scenarios pass (112 Save/25 Create/17 Abandon). Full harness: 207 passes, then unchanged Compare XAML assertion. D is exFAT and is refused; explicitly authorized unique Windows Temp fixtures on fixed NTFS were used. Three independent reviewer roles closed the final code delta; dedicated regressions passed. See [complete evidence](13-5c-implementation-evidence.md) and [native operation table](13-5c-windows-operation-table.md).

Spec implementation is done; C stories remain review-ready awaiting final JotaPe scoped acceptance. Parent 13.5 remains in-progress for D/E/B, with no added child tracker keys. No commit/deployment or production/FGDB/remote integration is part of this run.

## Suggested Review Order

**Entry and ownership**

- Compose isolated operations without granting domain or host authority.
  [ToolboxPrototypeRevisionStore.cs:58](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxPrototypeRevisionStore.cs#L58)

**Native containment**

- Validate optional replacement capability without changing store bytes.
  [ToolboxContainedFileSystem.cs:305](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxContainedFileSystem.cs#L305)

- Keep rooted kernel protection through target and handle handoffs.
  [ToolboxContainedFileSystem.cs:318](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxContainedFileSystem.cs#L318)

**Protocol and recovery**

- Pin exact disk fields separately from accepted A.
  [ToolboxPrototypeDiskContract.cs:59](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxPrototypeDiskContract.cs#L59)

- Verify bounded exact ancestry and plan only supported recovery actions.
  [ToolboxPrototypeRecovery.cs:26](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxPrototypeRecovery.cs#L26)

**Publication safeguards**

- Reserve resulting observation capacity before save or resolution mutation.
  [ToolboxPrototypeRevisionStore.cs:588](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxPrototypeRevisionStore.cs#L588)

**Tests and evidence**

- Run native block, flush, handoff and commit termination barriers.
  [ToolboxPrototypeRevisionStoreTests.cs:342](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxPrototypeRevisionStoreTests.cs#L342)

- Preserve all original harness groups while appending scoped disk registrations.
  [Program.cs:1004](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs#L1004)
