---
story_key: 13-7-1-define-compute-routes-and-prerequisite-planning
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-7-compose-compute-toolbox-preset
depends_on: [13-6-register-reusable-toolbox-capabilities]
execution_gate: explicit-runtime-authorization-accepted-registry-and-reviewed-compute-bindings
implementation_authorized: false
---

# 13.7.1 - Compute Routes And Prerequisite Planning

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing 13.7 entry; no child key.

## Story

As a Compute examiner, I want explicit automatic/assisted/manual route choices and step prerequisites, so that available tools prepare draft evidence without hiding unsupported or stale inputs.

## Scope And Dependencies

Owns versioned Compute composition manifest/route planner, prerequisite reasons and candidate-preparation orchestration through accepted 13.6. Runtime requires explicit authorization, accepted registry/eligibility/runner and reviewed Compute policy/source/output bindings. Pure plans can precede storage implementation with labeled fixtures; real artifact access/mutation needs affected accepted 13.5 owner. [Parent](13-7-compose-compute-toolbox-preset.md) owns boundaries. No UI, domain approval, finalization or remote transition.

## Acceptance Criteria

1. Given exact selected Compute preset/case/profile/source inventory, plan named capabilities/versions, route/step dependencies and required input/output owners under trusted policy. Do not derive parcel count from transaction type or arbitrary filenames. Unknown profile/policy/binding or unsafe source remains reasoned blocked; manual choice does not waive identity/integrity failure.
2. Given automatic/assisted/manual alternatives, examiner choice and constraints are explicit and retained. Local preprocessing/extraction/manual preparation remain draft; confidence or subprocess exit cannot approve. Optional unavailable provider blocks its action and offers eligible explicit manual/local alternatives, never silently claims automatic route succeeded.
3. Given provider binding activation, separately review registry effect contract and exact existing provider/source/credential/timeout/retry/cancellation/output call path before enablement. Future provider-specific authorization does not enable Innola/Enterprise/upload/transition. No automatic network fallback or rerun on ambiguous timeout; current 13.6 provider denial remains until that scoped contract is accepted.
4. Given failed/zero/canceled preparation, retain prior accepted/manual state, candidate/raw diagnostics and source inventory; do not delete empty-result entities. Changed candidate membership/IDs requires accepted B reconciliation and exhaustive examiner choices. Pending candidates are nonpersistent or explicitly unsupported where A/owner cannot represent them, never silently lost.
5. Given M01-M17 route/check coverage, prerequisites apply to the chosen tools: manual-only route does not universally require Python/CAD, PDF-only no CAD layers, absent memorandum not mandatory failure. Reuse declared rules; distinguish technical tool failure from source defect and unknown applicability from N/A. Detailed D04/D12 mappings remain gates, C15 classification retained.
6. Given changing input/source/policy/authority, invalidate exact plan/admissions and re-evaluate rather than rebind a running output to current state. Preserve independent manual work and prior history, use accepted scope impact services and bound loops/parameters by consumed contracts. No unbounded source discovery/script dispatch.
7. Given completed runtime code, tests prove reviewed route choices and blocked/deferred states for single/many/zero geometry candidates, failures, stale policies, source integrity, manual preservation and unsupported provider. Record actual pure/integration/provider lanes separately, preserve predecessors and obtain scoped 13.7.1 acceptance; optional provider not tested remains explicitly disabled.

## Tasks / Subtasks

- [ ] Confirm explicit authorization, accepted 13.6 and exact route/policy/binding manifest review. (AC1-7)
- [ ] Implement immutable route/step composition and eligibility reasons. (AC1-2,5)
- [ ] Inspect/review optional provider effect contract or retain disabled provider status. (AC3)
- [ ] Compose safe candidate preparation without identity/action/persistence shortcuts. (AC4,6)
- [ ] Add route/failure/stale/zero/manual fixtures and obtain scoped acceptance. (AC7)

## Developer Context And File Map

Future NEW Compute/ToolboxComputePlan.cs and route coordinator; NEW Tests/Workflow/ToolboxComputePlanTests.cs. Reuse 13.6 registry/runner, source/profile route mechanics and accepted canonical/reconciliation APIs; exact wrapper call paths must be read in full before changing/enabling. No duplicated extraction engine or legacy whole-workflow Execute/Finalize wrapper. Parameters/candidates retain exact owner versions; physical I/O not authorized by context lexical paths.

Future UPDATE only named harness registration or explicitly reviewed route seam, never baseline settings/DAML/source/output/package/remote behavior by default. Runtime UI/provider activation and host/storage tests require separate applicable permissions.

## Test Requirements And Completion Evidence

Parent Compute lanes, baseline158/applicable predecessors and known207-stop/default-PointIds preserved. Include manual-only without Python, PDF-only without DWG, unknown source profile, source/package mismatch, mixed assisted/manual work, canceled/zero rerun and optional provider timeout. Completion needs actual manifest/binding review, tests/enabled/deferred list and acceptance; no runtime tests performed now.

## References

- [Compute parent](13-7-compose-compute-toolbox-preset.md), [13.6](13-6-register-reusable-toolbox-capabilities.md), [checklist](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md).
- [13.7.2 workspace](13-7-2-define-compute-review-and-manual-correction-workspace.md), [13.7.3 save/readiness](13-7-3-define-compute-save-reopen-and-compare-readiness.md), [review record](13-7-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization false; all tasks unchecked. No route/provider/candidate implementation or execution, tests, commit or deployment. Fill actual execution evidence when authorized.
