---
story_key: 13-6-register-reusable-toolbox-capabilities
track: toolbox
story_type: code
created: 2026-10-11
depends_on: [13-3-add-toolbox-case-context-and-workspace-selection, 13-4-add-single-and-multi-parcel-toolbox-model, 13-5-add-versioned-toolbox-workspace-persistence]
execution_gate: explicit-runtime-authorization-and-accepted-affected-workspace-contracts
implementation_authorized: false
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.6 - Register Reusable Toolbox Capabilities

Status: backlog
Specification: preparation and static review complete under the continuing documentation-only/no-coding instruction. Child specifications and review record describe future implementation, not deployed capabilities. Existing tracker key retained; no child keys.

## Story

As a Toolbox developer, I want a static registry of capabilities with explicit scopes, effects and prerequisites, so that presets can compose existing services without granting hidden authority or accepting stale outputs.

## Ordered Implementation Stories

| Story | Owns | Runtime dependency |
|---|---|---|
| [13.6.1 Capability Definitions And Registry](13-6-1-define-capability-contracts-and-static-registry.md) | Versioned descriptors, source/owner bindings and coverage | Accepted local context/model, reviewed descriptor contract and explicit code authorization |
| [13.6.2 Eligibility, Scope And Staleness](13-6-2-define-capability-eligibility-scope-and-staleness.md) | Pure reasoned eligibility and exact input/scope binding | 13.6.1 accepted, trusted applicable policy and explicit authorization |
| [13.6.3 Headless Runner And Reviewed Adapter Bindings](13-6-3-define-headless-runner-and-reviewed-adapter-bindings.md) | Invocation/result lifecycle and explicitly reviewed local adapter seams | 13.6.1/2 accepted, affected persistence/host contracts accepted and explicit authorization |

Documentation can precede unimplemented 13.5. Pure descriptor/eligibility code, if later authorized, may use accepted in-memory 13.3/13.4/A fixtures; actual paths/writes/host reads require the relevant accepted C/D/E/B storage adapter. Prototype receipts never count as accepted host revision evidence. 13.5 remains backlog; this decomposition does not claim that persistence gates have passed.

## Boundary And Contract Responsibilities

Use a compile-time/static registry and explicit injected adapter bindings; no assembly/plugin discovery, script eval, arbitrary commands, new GIS/domain engine or settings/DAML registration. Preserve existing extraction/solver/query/validation/rendering services and current operational call sites. 13.7/13.8/13.10 own preset/UI/workflow composition; 13.9 owns real handoff/remote effects. 13.6 describes remote operations as disabled/deferred and cannot execute provider/server requests, uploads, Enterprise writes or Innola transitions.

Proposed descriptor contract `toolbox_capability_definition_v1` contains immutable tool ID/version, group/operation, adapter owner/binding status, scope kinds, consumed/produced versioned contracts, input owner/revision requirements, typed parameters/output obligations, prerequisite capabilities/artifacts/checks, effects/write scope, invalidated dependency classes, policy/preset restrictions, cancellation/progress and recovery/overwrite classification. Definition is capability metadata, not invocation authorization.

Invocation `toolbox_capability_invocation_v1` binds caller-supplied run/operation identity, full case/current/source identity, exact descriptor/adapter versions, explicit scope IDs, revision namespace/token, input fingerprints, policy pin, immutable parameters and declared expected outputs. Source bytes/references and physical paths resolve only through an accepted owner seam. Fresh authority is separate from saved audit; context strings or stored pins cannot authorize paths or writes.

Result `toolbox_capability_result_v1` separates succeeded/blocked/failed/canceled/ambiguous from output currentness, verified commit evidence and domain approval. Carries input binding, sanitized reason/diagnostics, owned output/artifact references, actual affected IDs/dependency impact, metrics/findings and commit/publication status where applicable. Exit code zero or adapter return without required outputs is failure. Late/stale output never becomes current merely by returning succeeded.

Registry coverage traces all T01-T18 process groups to descriptors or explicit deferred/unsupported records. These are groups, not indivisible commands: split T03 crop/attach, T12 save/accept, T14 local decision/RT writeback, T15 local save/suspend upload and T17 render/snapshot/attach by effect/owner. T16/T18 and remote portions stay nonrunnable until 13.9. Missing bindings, D04/D12 detailed mappings and unproved host/store capability are explicit eligibility reasons, not defaults. Known C15 classification remains accepted and is not reopened.

## Acceptance Criteria

1. Every T01-T18 group is accounted for with versioned immutable descriptors or explicit deferred evidence; ambiguous/mixed-effect actions are split. Reject duplicate/aliased IDs, conflicting versions, missing contracts, invalid effect/output declarations and cyclic required capability dependencies.
2. Eligibility uses exact case/source/revision/scope/policy and fresh permitted local effects. Unknown facts/bindings/scopes stay unresolved/blocked, never N/A or allowed. Compare geometry/source PE mutation and all remote effects remain denied. Existing Compute-only geometry access is not silently widened to PLA/RT.
3. Input selection validates domain IDs and scope inventory. Parcel selection may restrict local work but cannot hide affected included/excluded/retired/shared-edge consumers or satisfy case-level acceptance. Reuse accepted dependency/policy services; missing coverage prevents clean currentness/approval claims.
4. Invocation freezes input/output contracts and rechecks eligibility at execution and publication. Changed source/model/geometry/tabular/inclusion/policy/authority stales only actual consumers under accepted dependency rules; completed history/findings remain intact. Actual task observation is not remote authority.
5. Runner executes only reviewed injected local adapters through accepted owner seams, never direct legacy orchestration that saves/uploads/transitions implicitly. Typed lifecycle covers cancellation/failure/ambiguous mutation without automatic resend. No mutation retries or durable run receipts are inferred from an in-memory run ID.
6. Result verification requires declared outputs, identity/contract/expected-parent bindings and actual affected scope. Outputs are staged proposals until accepted owner publication; read-only outputs do not claim commits. Existing A rejects pending proposal capture, so unsupported candidate persistence remains explicit and A format is unchanged.
7. Runtime validation preserves accepted local/model/policy/A behavior and baseline 158/applicable predecessor groups, with separate pure/build/focused/full and conditional host evidence. Known full-harness207-stop/default-PointIds limits remain. Scoped 13.6 acceptance cannot close later preset/handoff/host/policy gates.

## Tasks / Subtasks

- [ ] Confirm runtime authorization and accepted affected contracts before coding. (AC1-7)
- [ ] Implement/review 13.6.1 descriptor registry and source/owner coverage. (AC1)
- [ ] Implement/review 13.6.2 reasoned eligibility/scope/fingerprint admission. (AC2-4)
- [ ] Implement/review 13.6.3 safe runner/local binding contract and result validation. (AC4-6)
- [ ] Report actual test/evidence lanes and obtain scoped 13.6 acceptance. (AC7)

## Developer Context And File Map

Future NEW classes under `Workflow/Toolbox/Capabilities/`: descriptor/registry, eligibility and runner/adapter interfaces. Child stories allocate exact ownership. Future NEW tests under `ParcelWorkflowAddIn.Tests/Workflow/`; UPDATE Program.cs only for named registrations after full read, preserving case-insensitive substring-OR, fail-fast and no-argument full run.

REUSE ToolboxCaseContext/WorkspaceSelection, accepted policy loader/resolver, canonical/model/reconciliation/dependency APIs and storage contracts without expanding locked A DTOs. Context ValidateRoot is lexical; physical storage access requires accepted contained adapter. GeometryAccess currently permits only Compute with explicit flag, and RemoteActionAccess always denies. Policy resolver supports Compute/Compare only. Preserve these limits; later PLA/RT permission/profile changes belong to separately reviewed stories.

Explicit local adapter allowlist is an implementation deliverable: inspect every target method/call path and its side effects, name exact owner/read/write/version/output and dependencies before enabling. Source presence in capability inventory is not acceptance. No operational service, output/package/source/settings/DAML or remote dispatch changes by default; any integration amendment must be enumerated, read and reviewed under authorized scope.

## Test Requirements And Completion Evidence

Use existing net8.0-windows/x64 harness. Future build uses parent 13.5 command with separate obj-toolbox-capabilities/ and existing focused filters plus explicit capability groups. Record actual counts rather than reuse old results. Scope matrix covers case/document/parcel/boundary/participant/identifier pair, zero/many parcels, shared edges and read-only sources. No fixed three-parcel assumption. Completion requires admitted descriptor manifest/coverage, actual eligibility/run tests, reviewed safe bindings, remaining deferred list and scoped acceptance. No runtime tests run by preparation.

## References

- [Capability inventory](../../planning-artifacts/toolbox/toolbox-capability-inventory.md), [process T01-T18](../../planning-artifacts/toolbox/toolbox-process-definition.md), [tool invocation outline](../../planning-artifacts/toolbox/toolbox-contract-catalog.md#tool-definition-and-invocation-v1).
- [Architecture](../../planning-artifacts/toolbox/toolbox-architecture.md), [decision gates D04/D12](../../planning-artifacts/toolbox/toolbox-decision-register.md), [13.5 storage gates](13-5-add-versioned-toolbox-workspace-persistence.md), [A evidence](13-5a-implementation-evidence.md), [project rules](../../project-context.md).
- [Preparation review](13-6-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Prepared 2026-10-11 under no-coding instruction. All runtime tasks unchecked; implementation_authorized false. No descriptor code/catalog activation, runner, adapter execution, source reads/writes, GIS test, build, commit or deployment. Fill execution model/logs/results/files only when authorized.
