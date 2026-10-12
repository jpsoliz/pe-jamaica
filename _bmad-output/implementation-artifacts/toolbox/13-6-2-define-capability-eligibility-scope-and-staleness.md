---
story_key: 13-6-2-define-capability-eligibility-scope-and-staleness
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-6-register-reusable-toolbox-capabilities
depends_on: [13-6-1-define-capability-contracts-and-static-registry]
execution_gate: explicit-runtime-authorization-and-1361-acceptance
implementation_authorized: false
---

# 13.6.2 - Capability Eligibility, Scope And Staleness

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing 13.6 entry, no child key.

## Story

As a Toolbox developer, I want reasoned eligibility bound to exact current inputs, so that an available service cannot run against stale, foreign or unauthorized scope.

## Scope And Dependencies

Accepted 13.6.1 plus explicit code authorization required. Owns pure eligibility/required-input/scope validation and admission binding; no I/O, context mutation or policy publication. Reuse accepted local access, policy and dependency/readiness APIs. [Parent](13-6-register-reusable-toolbox-capabilities.md) controls requirements. Storage/host capabilities arrive as verified external owner evidence, never inferred from currentRevisionId strings or C receipts.

## Acceptance Criteria

1. Given exact descriptor/version, full case/source identity, selected preset, explicit requested scope and externally verified revision/owner/fingerprint/policy, return allowed/not-applicable/blocked/unresolved with stable typed reasons and required missing evidence. Unknown facts/scope/adapter/storage capability are not N/A. A descriptor's mere presence cannot grant permission.
2. Given fresh context/task assignment, enforce Compute-only explicit geometry permission, Compare current-case tabular-only policy and linked PE read-only access. All remote effects remain denied. Actual task observation may be unverified for authorized local work, but selected preset never certifies server ownership. Assignment refresh revokes cached geometry access exactly as current context does.
3. Given parcel/document/boundary/participant/pair scope, validate case-scoped domain IDs/inventory with declared comparer. Selection may constrain work, but expand actual dependency impact through accepted old/new graph semantics, including excluded/retired neighbors, before admitting mutations. Case-wide acceptance still covers every included parcel; absent inventory/coverage blocks readiness-sensitive consumers.
4. Given consumed artifact/check/policy versions, validate freshness for each exact declared consumer rather than whole-case blanket reuse. Source/tabular/geometry/inclusion/policy changes stale appropriate bindings while retaining old human review/findings. Original A/B hashes stay unchanged; default PointIds hashing is explicitly unsupported for affected readiness rather than normalized.
5. Given proposed run admission, freeze exact descriptor/adapter version, case/scope/input revision namespace/fingerprints/parameters/policy and fresh authority evidence. Revalidate before execution and at publication through the owning store; pure eligibility cannot prove atomicity or physical path safety. Changed current inputs or revoked authority return stale/conflict without rebinding the admission.
6. Given unresolved D04/D12 detailed mappings, apply only a trusted reviewed Compute/Compare binding using existing resolver. Accepted required-review/optional/absence semantics remain distinct from permission. PLA_A/PLA_B/RT resolver gaps return explicit unsupported, never synthesize a Compute policy or extend enum/hash contracts.
7. Given bounded evaluation, validate at most 100,000 expanded scope/prerequisite evaluations per request with checked budgets and existing policy/model/storage bounds taking precedence. Zero/many parcels remain valid domain inputs when declared; no fixed three-parcel shortcut. Overflow/incomplete graph fails explicitly without partial allowed set.
8. Given completed code, matrix tests exercise missing/foreign/case-alias/stale scopes, dependency changes, revoked assignment, unsupported presets, untrusted policy, inspectable prototype versus accepted host evidence and lowered budgets. Preserve predecessors and record actual lanes; obtain scoped 13.6.2 acceptance.

## Tasks / Subtasks

- [ ] Confirm code authorization and 13.6.1 acceptance plus trusted applicable policy fixtures. (AC1-8)
- [ ] Define typed eligibility/input/authority/owner evidence records. (AC1,5)
- [ ] Implement conservative preset/scope/prerequisite/freshness checks through existing services. (AC2-4,6-7)
- [ ] Define frozen admission and atomic-publication expectations for runner/store handoff. (AC5)
- [ ] Add scope/policy/stale/authority/budget tests and obtain scoped acceptance. (AC8)

## Developer Context And File Map

Future NEW Capabilities/ToolboxCapabilityEligibility.cs and admission types; NEW Tests/Workflow/ToolboxCapabilityEligibilityTests.cs. Reuse ToolboxCaseContext, model identity maps/readiness/dependencies and trusted policy loader/resolver. Context lexical root and bare revision IDs are not contained path/committed storage proof. Do not widen GeometryAccess or policy resolver to PLA/RT, reimplement policy predicates or change A/B hashes. Future Program.cs registrations preserve existing behavior after full read.

## Test Requirements And Completion Evidence

Use parent lanes, baseline158/applicable predecessor and known207-stop/default-PointIds limitations. Pure tests use explicitly labeled verified owner fixtures, not claims that 13.5 is implemented. Actual owner integration needs accepted affected 13.5 contract and runtime evidence. Completion requires precise admission/reason matrix, actual tests, review and acceptance; no tests executed by preparation.

## References

- [Parent](13-6-register-reusable-toolbox-capabilities.md), [13.6.1](13-6-1-define-capability-contracts-and-static-registry.md), [13.6.3](13-6-3-define-headless-runner-and-reviewed-adapter-bindings.md).
- [Policy design](../../planning-artifacts/toolbox/toolbox-review-policy-settings-design.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md), [preparation review](13-6-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; all tasks unchecked. No eligibility engine/context changes, policy activation, build/tests, commit or deployment. Populate actual execution records when authorized.
