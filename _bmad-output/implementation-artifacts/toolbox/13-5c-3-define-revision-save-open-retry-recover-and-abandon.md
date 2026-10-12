---
story_key: 13-5c-3-define-revision-save-open-retry-recover-and-abandon
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5c-add-isolated-prototype-disk-revisions
depends_on: [13-5c-1-define-disk-contract-and-recovery-logic, 13-5c-2-define-windows-containment-locking-and-replacement-adapter]
execution_gate: explicit-runtime-authorization-and-c1-c2-acceptance
implementation_authorized: true
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5C.3 - Revision Save, Open, Retry, Recover And Abandon

Status: review
Isolated implementation, actual NTFS verification and independent review complete. Build: zero warnings/errors. Final focused lane: 197 passes (158 preserved + 39 new). Native process matrix: 154 scenarios. See [runtime evidence](13-5c-implementation-evidence.md); final JotaPe scoped acceptance remains pending. Tracked through C/13.5 without a new key.

## Story

As a Toolbox developer, I want explicit revision-store operations composed from the accepted protocol and filesystem adapter, so that prototype bundles survive restart without duplicating commits or inventing current approval.

## Scope And Dependencies

Runtime entry requires explicit authorization, accepted [C.1 protocol](13-5c-1-define-disk-contract-and-recovery-logic.md) and demonstrated/accepted [C.2 capabilities](13-5c-2-define-windows-containment-locking-and-replacement-adapter.md). The [corrected C contract](13-5c-add-isolated-prototype-disk-revisions.md) owns schema/policy; reuse it and its planner/adapter unchanged except narrowly reviewed integration fixes.

Included: typed headless Create/Open/Save/Recover/Abandon orchestration, exact retry semantics, caller-frozen bytes and external case/policy context, fault injection, full process-interruption matrix and consistent-relocation fixtures. Excluded: production storage ownership, FGDB, D import/source normalization, E schema materialization, B hosted activation, UI registration, settings, exports/packages, remote writes or domain actions. No automatic cleanup/merge or copy/export API.

## Operation Responsibilities

| Operation | Required behavior |
|---|---|
| Create | Claim new root through C.2, initialize lock/layout and flush/read-back strict owner; never adopt/repair an existing directory |
| Open | Acquire existing exclusive lock, observe/verify within budgets, hydrate safe selected bytes through unchanged A, return detached result; no disk writes |
| Save | Validate/freeze owned request and trusted policy; resolve exact operation identity first, then expected parent for new saves; execute prepared/stage/seal/publish/receipt protocol |
| Retry | Same Save intent/IDs; return exact verified original receipt after successors, or conflict/operation_abandoned/recovery_required; no reallocation or rebase |
| Recover | Explicit selected intent under lock; re-observe and execute only C.1 permitted actions, reconstruct verified current/historical tails without guessing publication |
| Abandon | Explicit caller audit under lock, unchanged verified parent and no publication evidence; retained bytes/IDs and idempotent exact audit |

Cancellation/identity/trust inputs are supplied afresh; saved metadata cannot construct permissions or context. Recover/Abandon execute bounded storage repairs only, never domain review/reconciliation actions. Policy restrictions retain the parent's conservative writer rules; do not silently bypass them through a recovery API.

## Acceptance Criteria

1. Given Create interrupted at each boundary or competing initializers, the winner produces a verified owned store; residue remains initialization_incomplete/conflict without adoption or deletion. Open of a valid initialized empty store creates nothing, and a missing lock is corruption. All operations revalidate external root/case/store bindings through the adapter.
2. Given supported accepted A/B bytes and assets, Save freezes all caller input before I/O and retains exact bytes, null/empty distinctions, IDs, timestamps, comparers, histories and stale/pending domain state. It decodes against expected case and trusted pins without replaying actions, refreshing proofs or evaluating the failing default-PointIds domain hash. Untrusted/unavailable policy remains inspectable but blocks writable use.
3. Given a new valid request and unchanged verified parent, Save publishes flushed/read-back prepared intent, complete create-new staging, same-volume immutable seal, verified pointer, receipt and final journal in the parent order. No return says committed until pointer/revision/receipt/terminal journal evidence verifies; immutable files are never overwritten.
4. Given simultaneous processes or a paused writer, stale parent id/digest/sequence causes conflict and concurrent Open/Save returns busy. Detached Open data remains owned after lock release. Cancellation and exceptions release every task-owned handle/worker without changing winner bytes or granting authority.
5. Given repeated exact operation after restart and successor commits, return the same verified receipt before rejecting its original parent. Changed intent or reused revision IDs conflict; off-chain or abandoned operations never return committed. Missing historical receipt/journal tail requires explicit Recover, which verifies ancestry and that operation's payloads and does not move current.
6. Given injected failures and process death before/after every journal write/flush, staged-file write/flush, seal, pointer replacement/read-back, receipt and terminal update, Open reports a consistent state and Recover follows C.1 only. Complete matching locations resume the exact intent; partial destination/duplicate location/torn pointer/competing intents remain quarantined. No newest-file selection, automatic rebase, partial-copy repair or deletion occurs.
7. Given cancellation before the first operation-specific disk mutation, no operation is created. Once mutation starts, including an initial partial journal write/flush, retain all evidence and let C.1 classify pending/quarantined/corrupt state; do not claim nothing was created or delete residue. A verified prepared intent before pointer publication leaves pending evidence and unchanged current. After verified publication, finish receipt/terminal tail when possible and return committed with late-cancellation diagnostic. If tail verification fails, report publication_pending/recovery_required with publication evidence, never false nothing-saved or false committed. Retry/recovery resolves the same IDs.
8. Given Abandon against unchanged parent with no publication evidence, publish/flush/read-back an abandoned journal with exact caller actor/time/reason. Repeat identical audit idempotently, conflict on changed audit, refuse ambiguous/published intent, reserve all IDs and retain bytes. Only a different new operation may save against the unchanged parent after all unresolved work is resolved.
9. Given modified/missing declared bytes, unknown/deep/oversize metadata, abandoned debris, ancestry corruption or unsafe platform capability, typed bounded diagnostics deny save. Open may return only independently verified inspection data; policy warnings are distinct from transport corruption. No raw bundle/assets/secrets enter diagnostics/logs.
10. Given full source-locked relocation fixture, all store/case/revision/operation evidence is preserved at a newly approved root with explicit destination-lock preparation; Open rebases derived paths. Pending evidence remains pending, incomplete/mixed copies fail conservatively, and clones claim no coordinated lock. No relocation product API is introduced.
11. Given completed implementation, preserve all predecessor groups plus accepted C.1/C.2 groups, run new operation/fault/process groups explicitly, and report build, pure protocol, actual NTFS/process/race, focused and full-harness results separately. Obtain independent review and scoped C.3/C acceptance; documentation or C.1/C.2 acceptance alone cannot close C or parent 13.5.

## Tasks / Subtasks

- [x] Confirm combined runtime authorization, internally reviewed/proven C.1/C.2 contracts and isolated root/policy fixtures. (AC1-11)
- [x] Compose typed store API/result and explicit Create/Open using capability-gated adapter sessions. (AC1-2,4,9)
- [x] Implement Save stage/seal/publication/tail protocol with injectable boundary failures. (AC2-3,6-7)
- [x] Implement exact operation retry and current-ancestry historical tail verification. (AC5)
- [x] Implement explicit Recover/resume and idempotent Abandon through C.1 actions. (AC6,8)
- [x] Add exact A/B bytes/history hydration, corruption/limits/policy, cancellation and relocation fixtures. (AC2,7,9-10)
- [x] Add native process-termination/contended-operation matrix and verify task-owned workers exit. (AC1,4,6)
- [x] Independently review implementation and publish actual build/focused/full evidence. (AC11)
- [ ] Obtain final JotaPe scoped C.3 and combined C acceptance before affected D/E integration. (AC11)

## Developer Context And File Map

NEW proposed file: `ToolboxPrototypeRevisionStore.cs` under `Workflow/Toolbox/Persistence/`. NEW tests: `ToolboxPrototypeRevisionStoreTests.cs` plus fault/relocation fixtures under `ParcelWorkflowAddIn.Tests/Workflow/`. Reuse C.1 strict records/planner and C.2 adapter/process-worker infrastructure. UPDATE these only for a demonstrated integration need; preserve their admitted contracts and acceptance evidence. UPDATE Program.cs only for named registrations or existing reserved worker scenarios; read the full harness before edits and preserve substring-OR filtering, fail-fast, normal invocation and predecessor groups.

Use accepted A Capture/Encode/Decode without extending its locked DTO/nullability/enums/hash manifest. No update to CaseFolderStore, ManifestSerializer, outputs/packages, settings/DAML, domain action/hash methods, policy activation or remote services. Work only in this isolated branch/worktree; preserve user/previous changes and do not reset/commit/deploy automatically.

## Test Requirements And Completion Evidence

Fault matrix includes every observable persistence boundary, not just one failure per phase. Use controlled child-process barriers and terminate task-owned workers at those boundaries; restart with a new process and inspect old/new pointer, immutable revision inventory, journal/receipt and unchanged sentinel bytes. Never claim a managed exception proves OS process-death or power-loss behavior.

Pin A/B round-trip fixtures for histories, lineage, policy bytes, source/manual/unknown fields, variable parcel counts and pending state. Include retry after two successors; missing historical tail; off-chain sealed revision; changed parent sequence; duplicate asset; caller mutation after Save begins; cancellation during the initial journal write/flush; late cancellation plus receipt failure; repeat/changed abandonment audit; fresh save after abandonment; unknown temp/debris; unsupported adapter; consistent/pending/mixed relocation.

Use parent build and focused/full commands with accepted C.1/C.2 groups plus new operation filters. Report baseline 158 separately from actual new counts, and preserve known full-harness stop after 207 at Compare XAML and default-PointIds inspection limitation. C completion evidence needs final reviewed code, hashes/operation table, fault/process matrix, remaining capability limits, changed-file list and explicit scoped acceptance. No actual runtime tests run by this preparation.

## References

- [Corrected C contract and test lane](13-5c-add-isolated-prototype-disk-revisions.md), [review corrections](13-5c-contract-review-2026-10-11.md), [C.1](13-5c-1-define-disk-contract-and-recovery-logic.md), [C.2](13-5c-2-define-windows-containment-locking-and-replacement-adapter.md).
- [A story](13-5a-add-storage-contract-and-hydration-foundation.md), [A evidence](13-5a-implementation-evidence.md), [13.5 parent and C -> D/E -> B order](13-5-add-versioned-toolbox-workspace-persistence.md).
- [Architecture](../../planning-artifacts/toolbox/toolbox-architecture.md), [decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md).

## Dev Agent Record

Preparation itself created no runtime work. JotaPe later explicitly authorized C.1-C.3 completion. Codex implemented the isolated store through reviewed pure decisions and verified native capabilities. Save/Open/identical historical retry/Recover/Abandon, real A/B histories, retained evidence, all writer trust gates, admission/cancellation and lock-consistent relocation pass. Final build has zero warnings/errors; combined focused 197 groups and native interruption 112 Save/25 Create/17 Abandon scenarios pass. Independent blind/edge/acceptance reviews are closed. Full harness stops at unchanged Compare XAML after 207 passes; default-PointIds limitations remain unchanged. See [complete runtime evidence and file list](13-5c-implementation-evidence.md). No commit/deployment or production/FGDB/UI/remote integration occurred. Final human acceptance remains outstanding.
