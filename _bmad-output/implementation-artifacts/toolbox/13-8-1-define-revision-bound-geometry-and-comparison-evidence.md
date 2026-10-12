---
story_key: 13-8-1-define-revision-bound-geometry-and-comparison-evidence
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-8-compose-read-only-compare-and-correction-flow
depends_on: [13-7-compose-compute-toolbox-preset, 13-6-register-reusable-toolbox-capabilities, 13-5-add-versioned-toolbox-workspace-persistence]
execution_gate: explicit-runtime-authorization-and-reviewed-owner-query-bindings
implementation_authorized: false
---

# 13.8.1 - Revision-Bound Geometry And Comparison Evidence

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Existing 13.8 parent tracks this child, no new key.

## Story

As a Compare examiner, I want exact saved source/geometry and scoped system evidence, so that reference results cannot masquerade as current accepted case truth.

## Scope And Dependencies

Owns immutable input/read-only geometry load plan and evidence retrieval/binding. Requires accepted affected 13.5/13.6/13.7, trusted Compare policy and explicit code/host/read authorization. Pure fixture work uses labeled synthetic evidence; live service reads separately authorized. [Parent](13-8-compose-read-only-compare-and-correction-flow.md) owns requirements. No UI edits, save/decision, geometry mutation, uploads, reports/finalize or task lifecycle.

## Acceptance Criteria

1. Given exact 13.7 input, verify full case/owner/revision/included inventory/policy and inherited review/findings/deferrals before load. Read named verified immutable geometry/reference, never latest-file or transaction-only Enterprise substitution. If geometry lacks valid approval due to source defects, expose verified inspection data and provenance without accepted-geometry claim; geometry-required consumers remain blocked.
2. Given map read, enforce geometry/reference immutability in adapter/owner as well as UI display. Retain current versus linked PE/source identity and snapshot/profile; unexpected host edits/divergence invalidate clean use. Read-only copying/loading cannot fabricate new current geometry or context authority.
3. Given parcel/party/identifier query, bind every source/subject/query/mapping/version/retrieval result to exact consumed inputs and full inventory, including multiple volume/folio pairs. Preserve legal/LTD ownership versus fiscal/taxpayer/Enterprise context labels. Unsupported source-specific field/mode remains typed, not a guessed query or first result chosen automatically.
4. Given read adapter activation, review exact allowlisted target/modes, session boundary, paging/size/work/time/cancellation and sanitized logging under separate read permission. Do not relax 13.6 write/server denial; refresh/session auth is not task completion. Mock mode is explicit and never displayed/certified as live evidence.
5. Given no-record/mismatch/ambiguity/unavailable/unsupported/source-absence, retain distinct outcome and actual returned records/diagnostic. Explicit refresh versions evidence and preserves earlier snapshots. Late response after case/scope/query/tabular/geometry change is archived/discarded as stale, never rebound to current inputs. Local equality is an evaluation aid, not human review completion.
6. Given completed implementation, fixture tests prove all read bindings/outcomes/limits and real host read-only smoke separately. Live-read evidence is recorded only for authorized tested sources; unsupported/deferred modes remain visible. Preserve predecessor lanes/limits and obtain .1 acceptance without claiming examination or remote handoff.

## Tasks / Subtasks

- [ ] Confirm authorization, owner/Compute/registry acceptance and exact read-query policy contract. (AC1-6)
- [ ] Define immutable Compare-input/evidence manifest and owner-qualified geometry reader. (AC1-2)
- [ ] Inspect/review existing query/map/title call paths before scoped binding activation. (AC3-4)
- [ ] Implement bounded typed retrieval/refresh/stale response handling. (AC3-5)
- [ ] Run pure/host/conditional live-read lanes and obtain .1 scoped acceptance. (AC6)

## Developer Context And File Map

Future NEW Toolbox/Compare input reader and evidence coordinator; NEW Tests/Workflow/ToolboxCompareEvidenceTests.cs. Reuse Compare evidence/query interfaces, actual case/domain map and accepted storage/snapshot adapters. Existing CompareWorkingGeometryService reads transaction-scoped Enterprise layers; no direct accepted snapshot claim from it. Exact query modes/endpoints come from reviewed existing settings/services; no new universal property/area/surveyor API assumptions. Future UPDATE targets must be enumerated/read; no operational settings/output/lifecycle changes by default.

## Test Requirements And Completion Evidence

Use parent lane plus authorized read-only host/query scenarios; include wrong revision/store/case, prototype receipt, inspection-only defects, missing geometry, multiple pairs/roles, stale response, pagination exhaustion, redaction and mock/live separation. Preserve baseline158/known207-stop/default-PointIds. Actual geometry enforcement must be tested beyond disabled edit buttons. No tests/network calls performed by preparation.

## References

- [Parent](13-8-compose-read-only-compare-and-correction-flow.md), [.2 workspace](13-8-2-define-compare-workspace-tabular-correction-and-partial-review.md), [.3 correction loop](13-8-3-define-compare-decisions-and-compute-correction-loop.md).
- [Checklist](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md), [13.7](13-7-compose-compute-toolbox-preset.md), [13.6](13-6-register-reusable-toolbox-capabilities.md), [review record](13-8-story-preparation-review-2026-10-11.md), [project rules](../../project-context.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/read/host authorization false, tasks unchecked. No evidence retrieval, geometry map load, implementation/tests, commit or deployment. Exact adapter/bounds evidence remains future work.
