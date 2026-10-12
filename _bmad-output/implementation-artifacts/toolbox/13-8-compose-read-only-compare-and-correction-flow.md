---
story_key: 13-8-compose-read-only-compare-and-correction-flow
track: toolbox
story_type: code
created: 2026-10-11
depends_on: [13-5-add-versioned-toolbox-workspace-persistence, 13-6-register-reusable-toolbox-capabilities, 13-7-compose-compute-toolbox-preset]
execution_gate: explicit-runtime-authorization-accepted-owners-capabilities-compute-and-compare-policy
implementation_authorized: false
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.8 - Compose Read-Only Compare And Correction Flow

Status: backlog
Specification: preparation and static review complete under continuing documentation-only/no-coding instruction. Existing 13.8 key retained; no child tracker keys, runtime authorization or Compare acceptance implied.

## Story

As an examiner, I want revision-bound geometry and source/system evidence in a read-only Compare workspace, with local tabular corrections and explicit Compute return for geometry, so that partial and final examination retain full provenance and cannot approve stale inputs.

## Ordered Implementation Stories

| Story | Owns | Runtime dependency |
|---|---|---|
| [13.8.1 Revision-Bound Geometry And Comparison Evidence](13-8-1-define-revision-bound-geometry-and-comparison-evidence.md) | Local-first immutable input/reference loading and scoped evidence retrieval | Accepted affected 13.5/13.6/13.7, reviewed query bindings and explicit authorization |
| [13.8.2 Compare Workspace, Tabular Correction And Partial Review](13-8-2-define-compare-workspace-tabular-correction-and-partial-review.md) | WPF evidence/value/findings review, permitted tabular commands and partial save | .1 accepted, owner/policy mapping and explicit UI/host authorization |
| [13.8.3 Decisions And Return-To-Compute Correction Loop](13-8-3-define-compare-decisions-and-compute-correction-loop.md) | Complete local decision and revision-safe geometry correction return | .1/.2/Compute owner accepted, reviewed decision policy and explicit authorization |

Pure future tests may use labeled in-memory/owner/query fixtures; actual host geometry, durable evidence and UI require accepted affected owner contracts and host isolation. F07 precedes installation. 13.9 owns attachments/publish/package/server transitions; 13.10 owns PLA/RT-specific effects. Local Compare selection, Save, decision and Return to Compute do not change actual Innola task or confer server authority.

## Boundaries And Required Contracts

Compare never edits current/reference geometry, coordinates, bearings/distances used for construction, topology, parcel membership/identity or linked PE state. It may review/correct explicitly whitelisted current-case tabular evidence under fresh authority through the accepted owner. Field classification is a reviewed contract: geometry-driving/ambiguous changes require Compute, not a free-form tabular patch. Tabular edit may stale dependent checks/decisions/query results while leaving geometry bytes unchanged; actual dependency semantics decide impact.

Inherit the exact 13.7 saved examination input, all source/raw/manual/human review and cumulative findings, policy and explicit outstanding Compare items. Use a named verified immutable host geometry snapshot when available. Forwarded-with-recorded-defects may remain inspectable even without valid-geometry approval; label it inspection-only and never fabricate accepted geometry. Missing approved geometry blocks only consumers that require it, not lawful recording of evidence/findings/partial work. No newest-GDB or automatic Enterprise fallback.

Comparison evidence binds full case, subject parcel/participant/identifier scope, consumed geometry/tabular fingerprint, query/source/mapping version, retrieval time/response status and owned returned record IDs/content. Legal/LTD ownership evidence remains distinct from fiscal/taxpayer/Enterprise/spatial context. Multiple volume/folio pairs, parties/roles and shared source links are first-class; a first-owner summary is not complete examination. No synthetic area/property/surveyor query endpoint is invented.

No-record, mismatch, ambiguous, unavailable, unsupported and source absence are distinct. Review available evidence and preserve completed items; required unavailable/incomplete comparison stays pending with scoped reason under C14, never approved/rejected or N/A automatically. A successful query/equality is not human approval. An explicitly reviewed policy may disposition a fully understood no-record result; technical outage cannot be relabeled no-record.

13.6 leaves external effects disabled. 13.8 may enable a separately reviewed read-only query adapter with explicit retrieval authorization, allowlisted target/mode/field mapping, bounded paging/time/response, cancellation and sanitized diagnostics. Read authorization does not relax RemoteActionAccess for writes, uploads or lifecycle. Credentials/session stay external and no external request is authorized by specification preparation.

## Acceptance Criteria

1. Named case/owner/revision/policy/inclusion/geometry and source-reference inputs are verified before load; inspection-only source defects retain their label. Prototype receipt/legacy decision flag/Enterprise marker is not accepted host proof, and read-only layer presentation alone is not write enforcement.
2. Scoped evidence collection retains all relevant parcels/parties/pairs and actual legal/fiscal/source provenance. Missing/ambiguous/service-failed states remain typed and partially reviewed work is preserved; unsupported searches remain explicit.
3. WPF review permits only current-case whitelisted tabular correction and fresh independent presence/accuracy review. Original bytes/history remain; geometry-driving/linked-source edits fail at command/owner boundary even if UI controls are bypassed.
4. Changed tabular/source/geometry/inclusion/policy/authority marks exact dependent comparison/check/decision records stale while retaining history; late async responses cannot bind to new query/scope/revision. Refresh is explicit, not an overwrite of original evidence.
5. Partial Save/reopen faithfully retains required pending reasons/coverage/evidence/corrections and cumulative findings through one accepted owner. Missing durable representation blocks the requested save; A v1 is not silently expanded and legacy JSON cannot be a competing master.
6. Completed local Compare decision covers every included parcel/applicable evidence and cumulative findings. Approved/rejected examination is an explicit policy-valid choice, separate from geometry validity and server completion. Pending/blocked/saved_progress legacy states are not automatic final decisions.
7. Geometry correction return preserves exact base/evidence/affected scopes, uses explicit Compute selection and fresh geometry permission, then requires new saved revision and re-evaluation of affected comparisons before finalization. Selection alone grants no permission, stale parent fails and prior comparisons/decisions stay history.
8. Actual UI/owner/host and pure/query fixtures prove read-only enforcement, allowed tabular edits, pending data, all-included decision and corrected revision loop, with separate build/focused/full/live-read/host results. Preserve baseline158/known207-stop/default-PointIds and accepted predecessor groups. Scoped 13.8 completion does not close 13.9/remote/production/11.2 gates.

## Tasks / Subtasks

- [ ] Confirm authorization, affected predecessor acceptance and exact Compare field/query/decision/storage mappings. (AC1-8)
- [ ] Implement/review .1 revision-bound input and evidence loading. (AC1-2)
- [ ] Implement/review .2 workspace/tabular/partial-save behavior. (AC3-5)
- [ ] Implement/review .3 explicit decisions and Compute correction return. (AC6-7)
- [ ] Report actual lanes and obtain scoped 13.8 acceptance. (AC8)

## Developer Context And File Map

Future NEW services/viewmodels under Workflow/Toolbox/Compare/ and matching Tests/Workflow/. Existing Compare evidence/query/title/map/report mechanics are reuse candidates only after full call-path review. CompareWorkingGeometryService currently scopes Enterprise layers by transaction, not exact immutable Toolbox revision; reuse requires owner-qualified reader, not direct fallback. CompareReviewDecisionPersistenceService writes ordinary JSON; its legacy readiness check lacks the new complete revision/coverage semantics and must not certify Toolbox acceptance.

CompareWorkspaceViewModel couples save/report/Finalize/upload/lifecycle/cleanup; do not bind its Approve/Complete/Suspend/Cancel commands wholesale. Enumerate/read exact existing view/dockpane/adapter UPDATE set before authorized wiring. New local Toolbox commands remain independent of baseline lifecycle behavior/settings/DAML; installation needs F07. Native SDK calls stay behind safe host adapters, not WPF handlers.

Future Program.cs registrations preserve substring-OR/fail-fast after full read. Use existing net8.0-windows/x64/SDK3.6 lane and parent capability commands with separate obj-toolbox-compare/. No new test framework, GIS engine or legal-matching engine. Exact query read authorization, field allowlist, policy and storage extensions remain implementation deliverables/review gates.

## Test Requirements And Completion Evidence

Fixtures cover 1/3/many parcels, multiple owner/pair evidence, source-defect inspection with geometry not approved, legal versus fiscal labels, no-record/ambiguous/outage, partial save/resume, current-case tabular change, geometry patch attempts, source PE denial, stale response/query, shared/excluded/retired dependency, blocked selected-out parcel, decision without complete coverage, geometry return/correct/recompare and revoked permission. No runtime/query/UI/host tests executed by preparation.

## References

- [Approved checklist and C11-C15](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md), [process](../../planning-artifacts/toolbox/toolbox-process-definition.md), [capability inventory](../../planning-artifacts/toolbox/toolbox-capability-inventory.md).
- [13.7 Compute input](13-7-compose-compute-toolbox-preset.md), [13.6](13-6-register-reusable-toolbox-capabilities.md), [13.5 owners](13-5-add-versioned-toolbox-workspace-persistence.md), [A limitations](13-5a-implementation-evidence.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md), [review record](13-8-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization false; tasks unchecked. No Compare UI/code/query, geometry load/edit, owner save, decision/task mutation, runtime tests, installation, commit or deployment. Record actual implementation/review evidence when authorized.
