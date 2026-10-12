---
story_key: 13-6-3-define-headless-runner-and-reviewed-adapter-bindings
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-6-register-reusable-toolbox-capabilities
depends_on: [13-6-1-define-capability-contracts-and-static-registry, 13-6-2-define-capability-eligibility-scope-and-staleness]
execution_gate: explicit-runtime-authorization-accepted-1361-1362-and-affected-owner-contracts
implementation_authorized: false
---

# 13.6.3 - Headless Runner And Reviewed Adapter Bindings

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing 13.6 entry, no child key.

## Story

As a Toolbox developer, I want explicit headless invocation/results through safe local adapter seams, so that presets can reuse domain services without hidden persistence or remote effects.

## Scope And Dependencies

Requires accepted 13.6.1/2 and explicit code authorization. Owns runner lifecycle/result validation and reviewed injected local bindings; actual I/O/mutation requires accepted applicable 13.5 owner adapters and authorization. Fake adapters can prove pure orchestration only. [Parent](13-6-register-reusable-toolbox-capabilities.md) owns contract; no dynamic dispatch, direct command/script execution, preset/UI wiring or remote/server/provider calls. Later stories enable effects only under their own gates.

## Acceptance Criteria

1. Given a proposed real binding, enumerate exact target method/call path, version, input/output owner, actual reads/writes/remote effects, cancellation and return obligations before enablement. Allowlist only fully reviewed local side-effect-compatible services; blocked/unbound descriptors return typed reason without invocation. Existing CreateParcelDraftExtractionAdapter/output/RT/PLA orchestrators cannot be wrapped blindly because they may save/upload/transition.
2. Given admitted invocation, freeze caller bytes/parameters/scope and own a supplied unique run/operation identity; resolve artifacts/paths via accepted owner seam, not context lexical path or independent transaction discovery. Recheck fresh authority/currentness before invocation. Run identity is correlation only, not durable receipt or retry-safe mutation.
3. Given read-only local adapter, validate declared output contract/type/identity/scope/findings and return owned data bound to exact input; unknown/missing/malformed outputs or exit success without required outputs fails. Eligibility/not-applicable and successful evaluation do not imply passed checks, geometry acceptance or case readiness.
4. Given permitted local mutating adapter, produce staged output/command proposal and actual impact without independent persistence. Publish through one accepted owner with expected-parent/input checks under its serialization, revalidate authority there, and validate actual affected IDs rather than trust declared impact alone. Never promote stale late output. No source PE/Compare geometry or undeclared remote effect, dual writer or hidden domain action replay.
5. Given failures/cancellation/concurrent input change, preserve current accepted state/history and report blocked/failed/canceled/ambiguous plus separate publication status. Before first effect canceled may mean no output; after owner publication use its verified receipt/recovery rules, never false nothing-saved or committed with missing tail. Exceptions/timeouts do not cause automatic resend; retry only via explicit accepted owner idempotency contract with exact intent.
6. Given pending candidates/runs, A's locked codec has no run/proposal persistence; do not add fields, manufacture durable journals or drop pending state silently. Return nonpersistent candidate output with explicit durability limitation, or block requested durable execution until a reviewed owner contract can represent it. C receipt is not accepted-host/domain approval. Remote classified operations remain disabled even if a test adapter would succeed.
7. Given adapter/progress/result data, bound parameter/output collections and bytes by descriptor/consumed contracts before materialization, sanitize diagnostics/metrics, check cancellation and release owned resources. No sensitive payload/secrets in logs. Late progress from a completed/canceled run cannot mutate the current result or UI state; no UI implementation is included.
8. Given completed runtime work, pure runner fixtures and at least one reviewed real pure local evaluator binding demonstrate reuse, output admission, authority revocation/stale rejection and cancellation/ambiguous result handling. Any I/O/mutating bindings require accepted owner integration evidence; otherwise remain deferred. Record exact enabled/deferred list and build/focused/full/conditional host lanes. Obtain 13.6.3 then scoped 13.6 acceptance without implying full T01-T18 execution or downstream workflow completion.

## Tasks / Subtasks

- [ ] Confirm authorization, 13.6.1/2 acceptance and exact enabled binding/owner scope. (AC1-8)
- [ ] Inspect/read real target call paths and record allowlist/side-effect/cancellation/version matrix. (AC1)
- [ ] Implement injected runner/admission/result verification and resource/progress lifecycle. (AC2-3,5,7)
- [ ] Add owner-proposal/publication seam and honest nonpersistent/unsupported outcomes. (AC4-6)
- [ ] Exercise pure fixtures and at least one reviewed real local evaluator; record blocked storage/remote gaps explicitly. (AC8)
- [ ] Review actual tests/bindings and obtain 13.6.3/parent scoped acceptance. (AC8)

## Developer Context And File Map

Future NEW Capabilities/ToolboxCapabilityRunner.cs, narrow adapter interface/bindings and Tests/Workflow/ToolboxCapabilityRunnerTests.cs. Registry owns definitions; eligibility owns admission; runner owns execution/result, accepted store owns publication/receipts. Reuse pure validation/solver/policy methods only after full call-path inspection; do not copy their algorithms. Exact real binding is a future implementation deliverable and cannot be represented as already reviewed by this specification.

Future UPDATE Program.cs only for named tests after complete read. Any necessary local adapter integration change must enumerate/read the exact file/side effects before coding; no default operational service/settings/DAML/source/output/package/remote changes. Persisted tool runs or new domain snapshot fields require separately reviewed contract work, not an A v1 extension within this story.

## Test Requirements And Completion Evidence

Test fake executor never invoked when blocked, wrong output identity/version, missing output, canceled before/after effects, owner conflict, ambiguous tail, stale policy/assignment, actual versus declared scope, shared-edge consumers, defensive input/output ownership, revoked authority and deferred remote descriptors. Real pure evaluator proves reuse, not host behavior. Use parent lanes, preserve baseline158/known207-stop/default-PointIds and accepted prior groups. Completion needs admitted invocation/result fixtures, actual enabled binding evidence, no hidden side effects, limitations, review and acceptance. No runtime tests executed now.

## References

- [Parent contract](13-6-register-reusable-toolbox-capabilities.md), [13.6.1](13-6-1-define-capability-contracts-and-static-registry.md), [13.6.2](13-6-2-define-capability-eligibility-scope-and-staleness.md).
- [Source inventory](../../planning-artifacts/toolbox/toolbox-capability-inventory.md), [13.5 owner gates](13-5-add-versioned-toolbox-workspace-persistence.md), [A limitations](13-5a-implementation-evidence.md), [project rules](../../project-context.md), [preparation review](13-6-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; all tasks unchecked. No runner/binding, domain invocation, source artifact, owner publication, tests, commit or deployment. Fill actual model/logs/results/files after authorized implementation.
