---
story_key: 13-8-2-define-compare-workspace-tabular-correction-and-partial-review
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-8-compose-read-only-compare-and-correction-flow
depends_on: [13-8-1-define-revision-bound-geometry-and-comparison-evidence]
execution_gate: explicit-runtime-ui-host-authorization-and-accepted-owner-field-policy
implementation_authorized: false
---

# 13.8.2 - Compare Workspace, Tabular Correction And Partial Review

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Existing 13.8 parent tracks this child, no new key.

## Story

As an examiner, I want source/system evidence review and permitted local tabular correction with partial saves, so that available work is retained while missing required comparison remains pending.

## Scope And Dependencies

Requires .1/affected owner acceptance, trusted Compare policy/field allowlist/storage mapping and explicit UI/host authorization. Owns compact WPF workspace, local value-review/correction commands, scoped pending coverage and owner Save/Reopen. Final decision/Compute-return seam is an interface for .3, not its implementation dependency. F07 precedes installation. No geometry editing, source-system writes, actual task cancel/suspend/complete or legacy Finalize/report upload.

## Acceptance Criteria

1. Given .1 input/evidence, compact source/geometry/reference/parcel/party/pair views display origin/retrieval/input revision and ready/stale/pending/ambiguous states. Current geometry/reference is read-only; selection changes navigation only, never included set or completeness. Mock/query outage/source absence remain visible and distinct.
2. Given whitelisted current-case tabular correction, require fresh explicit local tabular authority and typed owner command with expected parent/base/policy. Preserve raw/suggested/manual/reviewed values, independent human presence/accuracy and append-only audit/evidence. No coordinates/construction bearings/distances/identity/membership/topology or linked-source patches; ambiguous fields go to Compute. UI bypass/direct owner command tests enforce the same boundary.
3. Given changed permitted value, preserve geometry bytes/profile and invalidate every actual dependent check/query/comparison/decision under accepted dependency semantics, retaining unrelated reviewed results only when bindings remain current. Async query results bind old consumed fields as stale; changed current source tabular value is not written to LTD/Innola/fiscal/PE systems.
4. Given required unavailable/incomplete evidence, review/save available scoped items and outstanding reason under C14, keeping aggregate pending. No-record or mismatch may receive explicit policy-valid human disposition, but cannot auto-pass/reject. Skipped/disabled/Not available does not equal complete review; original Compute findings accumulate alongside later ones.
5. Given Save/Reopen, admit exact durable representation for query snapshots, human comparisons, pending reasons, field audit and coverage under one accepted owner. A locked DTO cannot silently gain fields, and legacy draft JSON is not a second writer. Unsupported new fields block requested durable action rather than dropping evidence. Fresh context/policy on reopen never restores saved permissions or reapproves stale findings.
6. Given failure/cancellation/stale parent/authority, preserve unsaved and completed work, use owner honest pending/quarantine/receipt outcomes and disable conflicting commands. Late progress/results cannot alter different case/selection. Source/artifact paths resolve through contained owner seam. .3 decision/return commands unavailable until wired, with reason, never simulated final decision.
7. Given completed implementation, viewmodel/owner and real WPF/map smoke cover partial save/resume, permitted versus forbidden patch, source absence/negative states, multiple participants/pairs, long text/layout, selected-out pending parcel and cumulative evidence. Enumerate/read exact view/dockpane/UPDATE targets and installation isolation before wiring. Obtain .2 acceptance, not final decision/remote acceptance.

## Tasks / Subtasks

- [ ] Confirm .1/owner acceptance, explicit UI authority and exact field/coverage/storage mappings. (AC1-7)
- [ ] Enumerate/read UI update targets and implement compact evidence/source/scope navigation. (AC1,7)
- [ ] Implement whitelisted typed local tabular/human review commands and actual dependency invalidation. (AC2-3)
- [ ] Implement pending coverage and accepted-owner Save/Reopen without silent schema expansion. (AC4-5)
- [ ] Add stale/canceled/authority and future .3 command-interface behavior. (AC6)
- [ ] Run actual UI/owner plus pure tests, review and obtain .2 acceptance. (AC7)

## Developer Context And File Map

Future NEW Toolbox/Compare workspace/viewmodel/tabular command coordinator and tests. Reuse source viewer/query/evidence presentation mechanics after full call-path read; avoid CompareWorkspaceViewModel lifecycle commands that save reports/upload/complete. Existing CompareReviewDraftPersistenceService is reference-only until a reviewed owner adaptation; file presence/saved_progress is not durable pending coverage. Native GIS/map calls stay in SDK-safe adapters, UI handlers remain thin. Exact UPDATE files enumerated during authorized wiring; no baseline settings/DAML/lifecycle/output/package changes by default.

## Test Requirements And Completion Evidence

Parent Compare lanes plus actual UI/read-only host/owner round-trip. Require geometry semantic/byte checks before/after permitted tabular patch, direct forbidden patch denial, field taxonomy boundaries, outage preserving reviewed items, policy-bound no-record disposition, unsupported saved state, wrong case/stale save and exact audit. Baseline158/applicable predecessor/known207-stop/default-PointIds preserved; no runtime/UI/host tests now.

## References

- [Parent](13-8-compose-read-only-compare-and-correction-flow.md), [.1](13-8-1-define-revision-bound-geometry-and-comparison-evidence.md), [.3](13-8-3-define-compare-decisions-and-compute-correction-loop.md), [13.5](13-5-add-versioned-toolbox-workspace-persistence.md).
- [Approved checklist](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md), [project rules](../../project-context.md), [review record](13-8-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime/UI authorization false; tasks unchecked. No view/patch/save/reopen/query/owner code/tests, registration, installation, commit or deployment. Future field/storage mapping and UI/owner evidence remain outstanding.
