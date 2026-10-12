# 13.5C.1-C.3 Story Preparation Review

Date: 2026-10-11
Scope: specification preparation and static review only, under JotaPe's explicit no-coding instruction.
Result: three specifications complete with scope, dependencies, acceptance criteria, implementation tasks, file ownership, tests and completion evidence. No unresolved specification finding remains from this review. Implementation authorization, concrete Windows mechanism proof and scoped code acceptance remain outstanding. All three code stories and parent remain backlog; no new tracker keys.

## Prepared Stories And Handoffs

| Story | Contract responsibility | Runtime dependency |
|---|---|---|
| [C.1 Disk Contract And Recovery Logic](13-5c-1-define-disk-contract-and-recovery-logic.md) | Strict metadata, canonical hashes, budgets and pure observed-state decisions | Accepted A plus explicit authorization |
| [C.2 Windows Containment, Locking And Replacement Adapter](13-5c-2-define-windows-containment-locking-and-replacement-adapter.md) | Verified native primitives/session, existing lock and NTFS/process/race proof | C.1 accepted plus explicit authorization |
| [C.3 Revision Save, Open, Retry, Recover And Abandon](13-5c-3-define-revision-save-open-retry-recover-and-abandon.md) | Semantic initialization and operation orchestration, hydration/publication/fault matrix | C.1/C.2 accepted plus explicit authorization |

The [corrected parent C story](13-5c-add-isolated-prototype-disk-revisions.md) remains the schema/policy source. Each child references it rather than defining a competing disk layout or v1 format. C.1 owns shared records/planner, C.2 owns physical operations, C.3 owns their semantic composition. Specifications can be prepared together; runtime order is C.1 -> C.2 -> C.3, within the unchanged C -> D/E -> B order.

## Review Coverage

| Parent review finding | Specification coverage |
|---|---|
| R1 initialization | C.1 AC3/5; C.2 AC5 exclusive primitives; C.3 AC1 semantic creation and residue |
| R2 observational read consistency | C.2 AC4 existing exclusive lock; C.3 AC1/4 detached Open |
| R3 safe handle/replacement mechanism | C.2 operation table and AC1-3/6-10; C.3 dependency forbids unsupported writer integration |
| R4 seal/resume | C.1 AC5; C.2 AC6; C.3 AC3/6 |
| R5 journal transitions/replacement failures | C.1 AC4; C.2 AC7/9; C.3 AC3/6/7 initial-write and publication failures |
| R6 historical commit evidence | C.1 AC6; C.3 AC5 ancestry/payload validation and explicit tail recovery |
| R7 hashes/assets | C.1 AC1/2; C.3 AC2/5 exact frozen requests and retry |
| R8 bounded traversal/debris | C.1 AC9; C.2 AC8; C.3 AC9 |
| R9 policy versus transport | C.1 AC8; C.3 AC2/9 conservative inspection/writer distinction |
| R10 relocation | C.2 AC1/8 containment/observation; C.3 AC10 source-locked fixture, preserved pending state and clone limits |

Reviewed against the create-story context checklist, Epic 13 delivery plan, accepted architecture, current decision gates, A implementation evidence, project rules and existing codec/type-manifest/harness patterns. Specifications preserve net8.0-windows/x64, no new test framework, unchanged A schema/bytes/hydration, existing operational boundaries and explicit worker-mode isolation.

## Review Finding And Resolution

A separate read-only reviewer checked the parent and all three specifications for contradictions, handoff gaps, weaker guarantees, authorization/completion claims and R1-R10 coverage. One substantive finding was returned: C.3 AC7 originally promised no operation before intent publication, even though cancellation during the initial journal write could already leave partial disk evidence.

Corrected C.3 AC7 to restrict the no-operation guarantee to cancellation before the first operation-specific disk mutation. After mutation begins, retain evidence and classify pending/quarantined/corrupt through C.1. Added cancellation during initial journal write/flush to required fixtures. This preserves R5 and the parent's no-cleanup/no-false-rollback rules. The other two specifications had no findings reported by that reviewer. The correction was checked against the parent publication/recovery rules; no code acceptance or runtime proof is implied.

## Preparation Validation And Remaining Work

Documentation checks cover relative-link targets, required story sections, unchecked implementation tasks, dependency order, explicit implementation_authorized false, unchanged tracker keys/statuses, review traceability and whitespace. Parent story, persistence parent, sprint comments, Epic 13 plan and project continuation notes are synchronized.

No C# implementation, native experiment, test harness change, filesystem fixture, build/runtime test, commit or deployment was performed by preparation. A's 158 focused passes and full-harness stop after 207 at known Compare XAML are historical baseline evidence, not new runs. Default boundary PointIds remains inspectable without domain-hash normalization. The Windows operation table must still choose and prove exact safe mechanisms during authorized C.2 implementation; API documentation alone is not containment proof.

The next coding candidate is C.1 after explicit runtime authorization. C.2 cannot be accepted using only unsupported diagnostics, and C.3 cannot skip predecessor capability proof. Parent C and 13.5 completion require actual implementation evidence and scoped acceptance; production D01/D06/D11, FGDB/deployment F01/F02/F03/F07 and operational 11.2 remain open.
