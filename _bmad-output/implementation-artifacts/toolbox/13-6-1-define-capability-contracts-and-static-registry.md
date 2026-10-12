---
story_key: 13-6-1-define-capability-contracts-and-static-registry
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-6-register-reusable-toolbox-capabilities
depends_on: [13-3-add-toolbox-case-context-and-workspace-selection, 13-4-add-single-and-multi-parcel-toolbox-model]
execution_gate: explicit-runtime-authorization-and-reviewed-descriptor-contract
implementation_authorized: false
---

# 13.6.1 - Capability Contracts And Static Registry

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing 13.6 entry, no child key.

## Story

As a Toolbox developer, I want immutable capability definitions with explicit owners/effects, so that repeated services can be composed without treating registration as permission.

## Scope And Dependencies

Owns strict descriptor/versioned metadata and static registry/coverage; no tool execution, I/O, dynamic loading or UI. Uses accepted 13.3/13.4 local shapes. The [13.6 parent](13-6-register-reusable-toolbox-capabilities.md) is requirement source; runtime needs explicit code authorization and reviewed descriptor semantics. 13.6.2 consumes metadata; 13.6.3 binds actual reviewed adapters.

## Acceptance Criteria

1. Given descriptor records, require exact contract/version, unique stable ID/version/group/operation, adapter owner/status, supported scopes, input/output version/owner, parameters, prerequisites, effects/write scope, preset/policy restrictions, invalidation, progress/cancellation and retry/recovery policy. Missing/unknown enum/version/type/field declarations fail rather than default to read-only or enabled.
2. Given static registration, resolve an exact ID/version only; aliasing, duplicates, ambiguous version selection, conflicting output owners, undeclared effects and prerequisite cycles fail. No assembly scan, string-to-type activation, external scripts or latest-version fallback. Registrations and returned definitions are defensively owned.
3. Given T01-T18 source inventory, map each group to specific operation descriptors or explicit deferred reason with source method/owner. Split mixed local/remote or save/accept actions. T16/T18 and server/provider/upload portions remain disabled; existing service presence does not make them runnable or satisfy host/storage gates.
4. Given unresolved D04/D12 mappings or unsupported PLA/RT policy, preserve accepted C15 classification but record binding gaps. Registry definitions cannot redefine required-review as compulsory source-value presence or grant task/preset authorization. Policy resolver/local context limitations remain explicit.
5. Given bounded inputs, initial limits are 256 descriptors, 4096 prerequisite edges and 4096 UTF-16 code units per metadata text value, injectable downward; validate/count before materialization with checked arithmetic. Actual parameter/output payloads have separately reviewed per-contract bounds, not this metadata allowance. Cycles use bounded iterative detection. Unsupported larger registries return typed limits, never truncation.
6. Given completed runtime implementation, field/version/golden descriptor, duplicate/cycle/ownership/effect/deferred coverage tests pass in the existing harness with preserved baseline158/applicable predecessors. Record pure/build/focused/full lanes and obtain 13.6.1 acceptance; no runtime capability is accepted merely by registry coverage.

## Tasks / Subtasks

- [ ] Confirm explicit code authorization and reviewed descriptor/coverage semantics. (AC1-6)
- [ ] Define separate capability metadata/type/version manifest without altering A format. (AC1)
- [ ] Implement immutable static registry, strict resolution and bounded dependency validation. (AC2,5)
- [ ] Produce T01-T18 operation/effect/owner/deferred coverage matrix. (AC3-4)
- [ ] Add named admission/coverage tests and scoped acceptance evidence. (AC6)

## Developer Context And File Map

Future NEW `Capabilities/ToolboxCapabilityDefinition.cs`, `ToolboxCapabilityRegistry.cs`; NEW Tests/Workflow/ToolboxCapabilityRegistryTests.cs. Reference existing inventory services and policy/model ownership; do not copy domain algorithms or create a second source/approval writer. Future UPDATE only Program.cs registrations after full read, preserving normal filtering/fail-fast. No settings/DAML registration, source dispatcher or operational adapter modifications.

## Test Requirements And Completion Evidence

Test mixed-effect split coverage, absent binding, duplicate/case alias ID, cycle/deep graph, version mismatch, mutable caller collections, unknown enums and lowered bounds. Use parent capability lane explicitly; baseline full suite can stop before appended groups. Completion needs actual descriptor/coverage admission, code review and scoped acceptance. No code/tests performed by specification.

## References

- [Parent contract](13-6-register-reusable-toolbox-capabilities.md), [inventory](../../planning-artifacts/toolbox/toolbox-capability-inventory.md), [tool outline](../../planning-artifacts/toolbox/toolbox-contract-catalog.md#tool-definition-and-invocation-v1).
- [13.6.2 eligibility](13-6-2-define-capability-eligibility-scope-and-staleness.md), [13.6.3 runner](13-6-3-define-headless-runner-and-reviewed-adapter-bindings.md), [preparation review](13-6-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; all tasks unchecked. No registry implementation/admitted runtime descriptors, build/tests, commit or deployment. Populate execution evidence when authorized.
