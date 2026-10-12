---
story_key: 13-7-compose-compute-toolbox-preset
track: toolbox
story_type: code
created: 2026-10-11
depends_on: [13-6-register-reusable-toolbox-capabilities, 13-5-add-versioned-toolbox-workspace-persistence]
execution_gate: explicit-runtime-authorization-accepted-capabilities-storage-and-reviewed-compute-policy
implementation_authorized: false
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.7 - Compose Compute Toolbox Preset

Status: backlog
Specification: preparation and static review complete under continuing documentation-only/no-coding instruction. Existing 13.7 tracker entry retained; no child keys, runtime authorization or workflow acceptance implied.

## Story

As an examiner, I want automatic, assisted and manual preparation composed over one case workspace, so that I can review every parcel and source, correct capture errors and retain survey-plan defects before local progression to Compare.

## Ordered Implementation Stories

| Story | Owns | Runtime dependency |
|---|---|---|
| [13.7.1 Compute Route And Prerequisite Planning](13-7-1-define-compute-routes-and-prerequisite-planning.md) | Versioned route/step composition and eligibility, candidate preparation | Accepted 13.6, reviewed Compute policy/bindings and explicit authorization |
| [13.7.2 Examiner Review And Manual Correction Workspace](13-7-2-define-compute-review-and-manual-correction-workspace.md) | ArcGIS-adjacent WPF review surface and model command proposals | 13.7.1 accepted, accepted model/capability seams and explicit UI/host authorization |
| [13.7.3 Saved Revision And Compare-Readiness Gate](13-7-3-define-compute-save-reopen-and-compare-readiness.md) | Owner-backed save/reopen, exact examination disposition and local Compare input | 13.7.1/2 plus affected 13.5/13.6 accepted and explicit authorization |

Preparation may precede these code dependencies. Pure composition tests can use labeled accepted in-memory fixtures after authorization; actual durable writes/host geometry require accepted C/E/B owners, D when importing legacy cases. Prototype C receipts do not establish accepted FGDB truth. 13.8 owns Compare review, 13.9 packages/publish/attach/actual task transition, 13.10 PLA/RT presets. No legacy Finalize action is composed into 13.7's local readiness gate.

## Process And Policy Boundaries

Use accepted capability descriptors/eligibility/runner from 13.6, canonical/model/reconciliation/policy services from 13.4 and the one accepted 13.5 owner. Do not duplicate extraction, solver, rule or persistence engines. Retain Compute-only explicit geometry permission, read-only linked PE, selected-preset versus actual-task distinction and fresh current authority.

Route choice is explicit automatic/assisted/manual within reviewed profile, sources and enabled capabilities. No guessed transaction-to-parcel count, first-parcel shortcut or automatic candidate approval. Provider request, candidate generation, human review, local commit, geometry acceptance and stage examination disposition have distinct outcomes. Zero/failed extraction never deletes previous manual/accepted evidence; manual recovery is a reviewed route, not a source-integrity waiver.

The approved C15 M01-M42 classification and conditions are inputs. Required examination does not mean compulsory source-value presence or universal automated pass. Retain unknown applicability/absence/accuracy/not-reviewed independently. D04/D12 detailed effect/preset/deferral bindings remain named gates; no default supervisor requirement or new block-at-first-source-defect behavior overrides C11. Technical identity/integrity/permission/ownership failures remain nonwaivable.

Every included parcel and applicable document/evidence section needs current examination coverage/disposition. Correct extraction mismatches or use a verified reviewed manual route; genuine source defects accumulate with evidence and reason. Forwarded-with-recorded-defects is not geometry validity or final Compare approval. Selected parcel filtering cannot hide unresolved candidates or shared-edge consumers. Geometry-approved consumers require independent current geometry proof/all-included approval; default-PointIds domain-hash limitation remains unresolved for affected approval.

13.6 deliberately leaves provider/server effects disabled. 13.7 may enable an existing extraction-provider binding only through a separately reviewed effect contract, explicit provider authorization, bounded source/credential/timeout/cancellation/output handling and audited scope; it must not silently relax registry denial. Provider authority is not Innola/Enterprise authority. Manual/local routes remain available when declared prerequisites permit; lack of optional provider is an explicit choice/blocked action, not claimed extraction success. No external provider call is authorized by this specification.

## Acceptance Criteria

1. Versioned Compute plan traces source/profile/policy/step/capability/input/output and route prerequisites; unsupported/unbound/stale tools are reasoned blocked, never fallback to arbitrary script or legacy workflow execution.
2. Single/multi-parcel review retains raw/candidate/manual/reviewed values, identities/memberships/history and human source-presence/accuracy evidence. Failed or empty reruns cannot erase work; changed inventories use explicit accepted reconciliation, not new-map allocation.
3. Workspace presents sources, parcel inclusion/selection, points/segments/tabular groups and cumulative scoped findings with current/stale/pending states. Commands require current allowed scope; Compare/source-reference geometry cannot be edited. UI cannot bypass capability/owner gates.
4. Structure/georeference/dimension/closure/orientation and applicable printed-value review reuse configured evaluators under trusted policy. Skipped/disabled/provider confidence is not completed examination. Changing source/tabular/geometry/inclusion/policy invalidates actual dependent consumers while preserving audit.
5. Local save/reopen binds exact owner/case/revision/evidence/coverage/disposition; pending/dirty state remains honest. New completeness/run/view fields need an explicitly reviewed representation if accepted storage cannot retain them; no silent A v1 expansion or loss of pending state.
6. Local Compare readiness requires all detected candidates dispositioned, all included current examination coverage, corrected extraction mismatches and retained source-defect/unknown evidence per trusted policy. Tabular work may be deferred only under explicit reviewed mapping, with scoped outstanding items. Readiness is not remote task transition, final Compare decision or valid-geometry approval.
7. Actual UI, pure/model, storage and conditional host tests prove both single/multi-parcel automatic/assisted/manual flows and negative scope/authority/staleness/retry cases. Preserve baseline158/applicable predecessor and known207-stop/default-PointIds limitations; scoped acceptance records enabled/deferred routes and no production/remote/11.2 closure.

## Tasks / Subtasks

- [ ] Confirm explicit code/host scope, accepted affected 13.5/13.6 and Compute policy/effect/deferral review. (AC1-7)
- [ ] Implement/review 13.7.1 route/prerequisite composition. (AC1)
- [ ] Implement/review 13.7.2 review/manual workspace and command proposals. (AC2-4)
- [ ] Implement/review 13.7.3 owner save/reopen and local readiness. (AC5-6)
- [ ] Run separate UI/model/storage/host lanes and obtain scoped 13.7 acceptance. (AC7)

## Developer Context And File Map

Future NEW composition/viewmodel/command seams under Workflow/Toolbox/Compute/. Future WPF views follow existing compact ArcGIS add-in patterns; no standalone/web replacement. Exact existing dockpane/view/registration UPDATE set must be enumerated and read fully before UI wiring; baseline registration/settings/cases are protected, F07 precedes any installation. Source selection/rendering/manual row/segment/solver/rule mechanics are reused only after side-effect and identity review.

ParcelScopedManualPointService and ManualBoundarySegmentService produce legacy canonical labels, not independently accepted Toolbox domain identity. SurveyPlanBoundarySolver.Apply mutates review documents; use owned proposals and accepted reconciliation, never directly mutate the live owner through a legacy call. CreateParcelDraftExtractionAdapter invokes existing routes; full call-path/provider/output inspection is needed before enablement. No arbitrary Python command, package/output/finalize/Innola dispatch by default.

Future Tests/Workflow/Compute tests and Program.cs named registrations preserve substring-OR/fail-fast after full source read. Use existing net8.0-windows/x64/SDK3.6 lane with parent capability test commands and separate obj-toolbox-compute/. Actual test counts and UI/host invocations must be reported, not inferred from source or old evidence.

## Test Requirements And Completion Evidence

Fixtures: 1/3/many parcels; one blocked selected-out parcel; excluded shared-edge consumer; zero extraction/manual recovery; mixed automatic/manual candidates; changed inventory/manual override; memoranda absent/negative/uncertain values; skipped versus examined; forwarded defects versus valid geometry; stale/revoked permissions; pending owner commit and reopen. UI has stable compact layout/source/evidence navigation and command disabled reasons; actual hosted smoke is distinct from viewmodel checks. No runtime/UI/host tests run by preparation.

## References

- [Approved checklist/classification](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md), [process](../../planning-artifacts/toolbox/toolbox-process-definition.md), [capability inventory](../../planning-artifacts/toolbox/toolbox-capability-inventory.md), [13.6](13-6-register-reusable-toolbox-capabilities.md).
- [13.5 owner gates](13-5-add-versioned-toolbox-workspace-persistence.md), [accepted A evidence](13-5a-implementation-evidence.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md), [legacy stage reference](../../../docs/project/COMPUTE_STAGE_STEPS_AND_RULES.md), [project rules](../../project-context.md), [preparation review](13-7-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. No coding authorized, tasks unchecked. No Compute UI, route, provider call, owner publication, runtime tests, installation, commit or deployment created. Future exact policy/adapter/UPDATE evidence remains required.
