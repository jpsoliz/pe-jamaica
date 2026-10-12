# 13.6 Capability Story Preparation Review

Date: 2026-10-11
Scope: prepare parent and three child specifications under continuing documentation-only/no-coding instruction.
Result: parent and three child specifications prepared and statically reviewed; no substantive specification findings reported by separate read-only review. Runtime authorization remains false; parent/children remain backlog with unchecked implementation tasks and no new tracker keys.

## Prepared Specifications

| Story | Responsibility | Handoff |
|---|---|---|
| [13.6 parent](13-6-register-reusable-toolbox-capabilities.md) | Contract/effect boundaries, T01-T18 coverage and storage/policy readiness | Requirement source, not deployed tool catalog |
| [13.6.1 registry](13-6-1-define-capability-contracts-and-static-registry.md) | Strict immutable descriptors/version/owner/effect registry | Defines metadata consumed by .2/.3, never invocation permission |
| [13.6.2 eligibility](13-6-2-define-capability-eligibility-scope-and-staleness.md) | Pure current scope/revision/policy/authority/prerequisite checks | Frozen admission for .3; owner handles publication atomicity |
| [13.6.3 runner](13-6-3-define-headless-runner-and-reviewed-adapter-bindings.md) | Reviewed local bindings, run/result/cancellation and owner-publication seam | Presets/UI in 13.7/13.8/13.10; remote/handoff in 13.9 |

## Planning Coverage Of Process Groups

This allocation is specification coverage, not admitted runnable descriptors. Exact binding/contract/version/call-path inventory is a future reviewed .1/.3 implementation deliverable. All I/O/mutation is contingent on accepted affected 13.5 owner adapters and explicit authorization.

| Group | Descriptor split / scope obligation | Deferred boundary |
|---|---|---|
| T01 open/restore | Local inspection/open distinct from restore activation | Actual persistence/package restore proof; no legacy root-replacement wrapper |
| T02 source inventory/view | Read-only viewing separate from source replacement | Source-changing ownership/adapters; no remote download in 13.6 |
| T03 page/crop | Local selection/render/crop separate from attach | Upload effects disabled |
| T04 related geometry | Explicit immutable linked identity/revision reader | Live external recovery/fetch disabled; no source PE writes |
| T05 structure check | Pure versioned evaluator with source/rule binding | Configured coverage gaps explicit |
| T06 extraction candidates | Candidate output separate from provider request/persistence | Provider calls disabled; 11.2/provider normalization and 13.7 orchestration remain |
| T07 manual/import geometry | Explicit provenance/draft proposal and scope | Actual host mutation gated; no implicit PLA/RT geometry access |
| T08 key-value review | Raw/manual/reviewed values, local tabular proposal | D04/D12 detailed binding gaps; task/system writeback disabled |
| T09 geometry review/solve | Pure solver output distinct from Compute mutation | No source/Compare geometry edits; accepted owner publishes |
| T10 dimensional checks | Nonmutating evaluation with exact geometry/units/rules | Headless token is not host coordinate proof |
| T11 geographic checks | Nonmutating check separate from placement/transform | Editing requires separate scope/owner/profile |
| T12 save/accept | Save command and human/domain acceptance are separate | Prototype receipt not accepted geometry/host revision |
| T13 compare evidence | Local immutable overlay/evaluation separate from external query | External service queries/preset composition deferred to 13.8 |
| T14 decisions | Local cumulative review decision distinct from RT server writeback | RT/remote completion in 13.10/13.9 |
| T15 local save/suspend | Local save distinct from package/upload/suspend effect | Remote suspend policy/effects deferred |
| T16 publish | Declarative remote descriptor with blocked reason | Enterprise write disabled until 13.9 proof/authority |
| T17 render/snapshot/attach | Render and verified local snapshot distinct from upload | Snapshot needs accepted host evidence; attach disabled |
| T18 advance task | Declarative remote descriptor with blocked reason | Fresh server/task mapping and transition receipts in 13.9/13.10 |

## Source-Based Guardrails

Inspected actual ToolboxCaseContext, WorkspaceSelection, PolicyResolver and dependency API/source inventory. GeometryAccess currently requires Compute plus explicit flag and always denies source references; RemoteActionAccess denies all server actions. RefreshActualTask revokes cached local geometry permission; task observation remains unverified. Policy resolver supports Compute/Compare only and unknown applicability is unresolved, not N/A.

These source facts constrain .2/.3: no PLA/RT widening, no policy reimplementation, no inferred task authority, no lexical-path physical safety claim. Preserve accepted classification C15 while leaving detailed D04/D12 mappings open. Descriptor coverage never enables a capability. Model/archive/domain fingerprints and A schema/bytes stay unchanged; A does not persist pending proposals or tool-run receipts. Runner cannot silently extend its DTOs, replay domain actions or advertise a run ID as durable mutation idempotency.

## Review Disposition

A separate read-only reviewer examined the parent and three children against actual context/policy source and A limitations. No substantive findings or corrections were reported. Review covered declared versus actual effect/scope, supported context/policy limits, pure-versus-owner dependency gates, stale result/publication authority, cancellation/ambiguous outcomes, exact enabling allowlist and specification-versus-runtime completion. Exact adapter allowlists and per-contract payload bounds remain future required implementation deliverables, not completed evidence. Static review is not runtime/host acceptance.

## Preparation Validation And Limits

Check parent/three child required sections, explicit false runtime authorization, unchecked tasks, local links, whitespace, unchanged tracker key/status and synchronized epic/context notes. No C# source/test harness modifications, source capture, adapter invocation, owner publication, GIS experiment, build/runtime tests, settings/DAML change, commit or deployment by preparation.

Historical baseline158/known full207-stop at Compare XAML/default-PointIds inspection limits remain; they are not new runs. Pure future registry/eligibility tests can precede full persistence under explicit authorization with labeled in-memory fixtures, but actual owner/host mutation cannot bypass 13.5 acceptance. Parent 13.6 acceptance will require actual admitted descriptors, reviewed local bindings and recorded enabled/deferred evidence; no full T01-T18 execution or downstream preset/handoff acceptance is inferred.
