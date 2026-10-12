---
story_key: 13-5c-1-define-disk-contract-and-recovery-logic
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5c-add-isolated-prototype-disk-revisions
depends_on: [13-5a-add-storage-contract-and-hydration-foundation]
execution_gate: explicit-runtime-authorization
implementation_authorized: true
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5C.1 - Disk Contract And Recovery Logic

Status: review
Specification and isolated implementation complete. JotaPe authorized C.1-C.3 execution on 2026-10-11. Final build is clean; 197 focused groups and 154 native interruption scenarios pass. Independent review is closed. See [implementation evidence](13-5c-implementation-evidence.md); final scoped acceptance remains pending. Tracked through 13.5C/13.5, without a new tracker key.

## Story

As a Toolbox developer, I want strict disk metadata and a pure recovery planner, so that persistence decisions are deterministic before filesystem operations are introduced.

## Scope And Dependencies

This is the first executable slice of corrected 13.5C. The [parent contract](13-5c-add-isolated-prototype-disk-revisions.md) is the schema/policy source; this story allocates responsibilities and acceptance evidence, not a competing contract. Preparation authorizes documentation only. Later implementation requires explicit runtime authorization and preserves D01/D06/D11 production gates.

Included: immutable disk records, strict parsing/encoding, hash/limit rules, typed observations/results, pure transition planning, initialization classification, historical publication proof and decision fixtures. Excluded: actual file I/O, platform calls, locks, directory creation, journal execution, UI, FGDB, imports, source/remote access and domain approval. C.2 consumes the records and operation requirements; C.3 executes planner actions through C.2.

## Acceptance Criteria

1. Given each owner, request, revision, pointer, journal and receipt record from the parent, parsing requires all fields and their declared types/nullability, exact contract/schema and enum values. Duplicate/unknown fields, bad identity/IDs/digests, invalid integers, aliasing and unknown versions return bounded typed diagnostics without usable partial metadata. Explicit nullable assets remain distinct from empty.
2. Given a frozen save request, independent golden UTF-8 bytes and SHA-256 fixtures pin field sorting, invariant scalar escaping, array order and exact timestamp/string preservation. Every intent field, including store/case and expected-parent sequence, affects the request digest; revision hashing includes request_digest but excludes revision_digest and mutable journal progress. Duplicate asset digests fail; checked sequence overflow fails before any action is planned.
3. Given immutable observation input containing owner/layout/lock capability outcomes, current/preserved pointers, journal/receipt/revision inventories and independently verified payload results, the planner returns storage state, transport verification, inspectable revision, writable flag, diagnostics and permitted actions separately. It does not read paths, fetch policies, call clocks, allocate IDs, mutate observations or treat a caller-supplied hash as authenticity.
4. Given every legal journal transition, the planner permits prepared -> sealed -> published -> committed or prepared/sealed -> abandoned. Terminal states never regress. Missing progress may be inferred only from verified physical evidence; a journal claim alone never proves publication. Missing/torn pointers, conflicting intents, duplicate locations or partial sealed destinations produce quarantine/corruption according to parent invariants.
5. Given complete staging only, exact pinned resume may seal; given complete matching destination only, resume may finish publication; given partial staging and a verified unchanged parent, only explicit abandonment is available. Both staging/destination present or conflicting contents cannot be automatically filled, overwritten, deleted or adopted. Incomplete initialization cannot become a writable store through recovery.
6. Given retry after successor commits, historical publication requires exact id/digest/sequence on the verified current ancestry, matching immutable intent and complete byte verification for that operation. A complete matching receipt returns the original result; missing terminal tail requires explicit recovery without moving current. Off-chain sealed/abandoned evidence never becomes a commit. Conflicting operation IDs and changed abandonment audit return conflict.
7. Given repeated explicit abandonment, only identical audit is idempotent. Current must equal the preserved verified parent and no publication evidence may exist. Retained IDs stay reserved. Unrelated unfinished intents block new mutations without changing an already verified historical receipt's identity.
8. Given policy_unavailable/policy_untrusted from accepted A, the planner retains inspection data and transport status but denies writable use with policy_inspection_required. Declared missing/modified bytes are corruption. No source-role availability, permissions or current domain approval is inferred from opaque assets or stored actors/pins.
9. Given depth/count/length/traversal boundaries, the pure validators charge the parent's limits with checked counters before materialization. Ancestry traversal is bounded, iterative and cycle-aware; default/null versus empty values are preserved. All recognized/unknown/abandoned/temporary observations count, and unexpected layout is diagnosed without deletion.
10. Given completed implementation, all transition and negative fixtures pass in the existing harness, accepted A schema/DTO/hash behavior remains unchanged and the predecessor focused lane is preserved. Record actual build/focused/full results separately; full-suite failure at the known Compare XAML assertion cannot imply success for later tests.

## Tasks / Subtasks

- [x] Confirm explicit runtime authorization and protected boundaries before implementation. (AC1-10)
- [x] Define disk-only record/type/nullability/enum field manifest and strict parser. (AC1)
- [x] Implement deterministic manifest hashing, independent golden fixtures and checked limits. (AC2,9)
- [x] Define owned observation/result/action records without filesystem or domain authority. (AC3,8)
- [x] Implement pure transition, ancestry/retry, resume and abandonment planning. (AC4-7)
- [x] Add named legal/illegal transition, corruption, collision, budget and policy fixtures. (AC1-9)
- [x] Independently review code and report actual validation under the combined C.1-C.3 execution authorization. (AC10)
- [ ] Obtain final JotaPe scoped acceptance of C.1 as part of completed C acceptance. (AC10)

## Developer Context And File Map

Work in this isolated worktree on refactor/toolbox-case-workspace; preserve the dirty documentation work and accepted baseline. Target net8.0-windows/x64 with the existing executable harness, no framework/library upgrade.

NEW proposed production files: `ToolboxPrototypeDiskContract.cs` and `ToolboxPrototypeRecovery.cs` under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/`. NEW tests: `ToolboxPrototypeDiskContractTests.cs` and `ToolboxPrototypeRecoveryTests.cs` under `ParcelWorkflowAddIn.Tests/Workflow/`. Names may follow local conventions. C.1 owns these shared definitions; C.2/C.3 reuse them rather than duplicating schemas or planners.

REUSE unchanged `ToolboxWorkspaceStorageCodec.cs`, `ToolboxWorkspaceStorageDocument.cs`, `ToolboxStorageValidation.cs`, A/B fixtures and policy loader. `ToolboxStorageJson` has a locked A type manifest; do not extend that manifest with disk records or change its pinned signature. Disk serialization needs a separately admitted manifest using the same explicit scalar conventions. Existing `ToolboxStorageResult.Success` means a value exists, not writable or authorized.

UPDATE only test registrations in `ParcelWorkflowAddIn.Tests/Program.cs` during future implementation. Its current top-level tuple registry executes Actions, prints PASS after each action, fails fast, and selects tests by case-insensitive substring OR across arguments. Read the entire current harness before editing; preserve no-argument full-run behavior and all existing names/bodies. No child-process dispatch is needed by C.1.

## Test Requirements And Completion Evidence

Use table-driven pure fixtures for every result state and journal transition; test changed fields independently of serializer-produced expectations. Cover initial/empty state, prepublication partial work, historical missing tails, abandoned retries, conflicting audits, policy inspection, foreign case/store, cycles, null/empty assets, overflow and lowered budgets. These are planned tests, not tests executed by story preparation.

Use the parent build and focused/full commands. Baseline: 158 focused passes, full harness stopped after 207 at known Compare XAML; default boundary PointIds remains inspectable without calling the failing domain hash. Completion requires implementation review, changed-file list, field/hash fixtures, actual named-test results and scoped acceptance. Documentation preparation alone does not complete this code story.

## References

- [Corrected contract and test lane](13-5c-add-isolated-prototype-disk-revisions.md), [R1-R10 review](13-5c-contract-review-2026-10-11.md).
- [Accepted A story](13-5a-add-storage-contract-and-hydration-foundation.md), [A evidence and limits](13-5a-implementation-evidence.md).
- [Architecture](../../planning-artifacts/toolbox/toolbox-architecture.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md).
- [C.2 platform adapter](13-5c-2-define-windows-containment-locking-and-replacement-adapter.md), [C.3 revision operations](13-5c-3-define-revision-save-open-retry-recover-and-abandon.md).

## Dev Agent Record

Preparation itself performed no runtime work. JotaPe later authorized completion of C.1-C.3; Codex implemented the isolated slices with disjoint implementation workers and three independent reviewer roles. The 20 pure C.1 groups, pinned shape SHA-256 and golden fixtures pass. Build: zero warnings/errors. Final combined lane: 197 passes; native matrix: 154 scenarios. Full harness: 207 passes then unchanged Compare XAML assertion. Complete commands, corrections, file list and limitations are in [runtime evidence](13-5c-implementation-evidence.md). No commit/deployment, A/domain change or production authority is implied. Final human acceptance remains outstanding.
