---
story_key: 13-7-2-define-compute-review-and-manual-correction-workspace
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-7-compose-compute-toolbox-preset
depends_on: [13-7-1-define-compute-routes-and-prerequisite-planning]
execution_gate: explicit-runtime-ui-host-authorization-and-1371-acceptance
implementation_authorized: false
---

# 13.7.2 - Compute Review And Manual Correction Workspace

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through existing 13.7 entry; no child key.

## Story

As an examiner, I want source-linked parcel/point/segment/tabular review and manual correction, so that capture mismatches can be corrected while original evidence and survey-plan defects remain visible.

## Scope And Dependencies

Owns compact WPF Compute workspace/viewmodels and typed model command proposals. Requires accepted 13.7.1 and relevant model/capability services plus explicit UI/host authorization. .3 later wires durable Save/Reopen/readiness; .2 tests its command seam with labeled in-memory/mock owner, without claiming persistence. Actual geometry/map edits require accepted B/E host owner. F07 precedes installation. [Parent](13-7-compose-compute-toolbox-preset.md) owns semantics; no standalone/web UI or legacy Finalize/server wiring.

## Acceptance Criteria

1. Given case/route/source, the ArcGIS-adjacent workspace provides source/page evidence, stage/tool statuses, parcel selection/inclusion inventory, points/segments and applicable printed-tabular groups with scoped findings. All detected candidates stay visible/inspectable; selected subset never changes inclusion or aggregate completeness. Empty/many parcels have explicit stable states, no first-parcel or three-parcel limit.
2. Given raw/candidate/manual/reviewed values, UI displays provenance and independent human presence/accuracy/capture status with actor/time/reason/evidence. Missing-from-source and negative attendance are valid recorded evidence; not-extracted/not-reviewed/uncertain/Skipped/Not available never auto-convert to examined or source absence. Preserve unknown/manual fields and original snapshots.
3. Given manual points/segments/solver proposals, use case-scoped stable maps and explicit reconciliation for changed inventories. Legacy manual sequence labels/solver-generated rows are canonical suggestions, not domain identity/acceptance. Never call mutating solver Apply on live shared document or normalize reviewer order silently. Parent/current/base/policy bindings and exhaustive choices protect prior manual work.
4. Given geometry/tabular/inclusion/shared-edge change, enforce fresh Compute permission/read-only source boundary and reuse actual dependency impact, including excluded/retired neighbors. Present current/stale/pending findings/checks without clearing historical review. Untracked map edits cannot bypass owner proof. Provider result success is not human approval.
5. Given applicable M18-M42 review, enumerate every included parcel and relevant document/field group, accumulate source defects and corrected extraction findings with evidence. Report-only failures stay visible without invented blockers; unknown/required unfinished review stays outstanding. Trusted detailed deferral policy, not free text or disabled checkbox, decides what can remain for Compare.
6. Given async tools/selection/context switch, command states and result binding prevent late output/progress from altering a different case/scope, keep cancellation/error evidence and preserve dirty proposals. Save/Reopen/Compare-readiness commands are exposed through .3 interface only; absent accepted persistence shows disabled with reason, never simulated saved status. Source viewing uses contained resolved owner references.
7. Given completed UI implementation, host smoke verifies readable compact pane sizing, source/grid/finding navigation, command disabled reasons and no overlap; viewmodel tests cover 1/3/many parcels, blocked selected-out scope, source absence/manual correction/shared-edge staleness and revoked permissions. Enumerate/read exact existing dockpane/registration UPDATE files before wiring; no operational installation/settings replacement. Obtain .2 scoped UI/model acceptance, not durable-save acceptance.

## Tasks / Subtasks

- [ ] Confirm explicit UI/host scope, .1 acceptance and exact pane/command integration boundaries. (AC1-7)
- [ ] Enumerate/read existing WPF/viewmodel/update targets and design compact source/review/navigation composition. (AC1,7)
- [ ] Implement owned review state and manual/solver/reconciliation command proposals. (AC2-4)
- [ ] Bind trusted checklist/scoped findings and outstanding deferrals without auto-approval. (AC5)
- [ ] Add asynchronous/cancellation/dirty-state and .3 save/readiness interface behavior. (AC6)
- [ ] Run viewmodel/actual pane smoke and obtain .2 acceptance. (AC7)

## Developer Context And File Map

Future NEW Compute/ToolboxComputeWorkspaceViewModel.cs, typed command adapter and compact WPF view following existing application conventions. Exact dockpane/view/DAML update targets are not fabricated now; enumerate/read them before authorized integration and retain isolation/F07. Commands route through .1/.6 and accepted model/owner APIs; no filesystem/network/SDK processing in WPF handlers. Future NEW Tests/Workflow/ToolboxComputeWorkspaceTests.cs plus named registrations after full Program.cs read.

Reuse original source viewer/grid/manual/solver mechanisms only after call-path review; do not reimplement GIS/COGO/extraction engines or call legacy save/approval/finalize indirectly. Full COGO editor is not claimed by manual point/segment service reuse. Durable audit/checklist mapping belongs to .3 accepted owner contract.

## Test Requirements And Completion Evidence

Parent Compute lanes plus actual authorized WPF/ArcGIS pane smoke, separately from pure tests. Source/route missing, empty data, busy/error/canceled tools, stale context, manual override, renamed/reordered parcels and cumulative finding evidence must survive navigation. Completion needs exact UI/update file list, screenshots/host invocation evidence, typed command tests/review and scoped .2 acceptance. No UI render/host tests run during preparation.

## References

- [Parent](13-7-compose-compute-toolbox-preset.md), [.1](13-7-1-define-compute-routes-and-prerequisite-planning.md), [.3](13-7-3-define-compute-save-reopen-and-compare-readiness.md), [approved checklist](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md).
- [Identity mapping](../../planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md), [F07 strategy](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [project rules](../../project-context.md), [review record](13-7-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. UI/runtime authorization false; tasks unchecked. No WPF view/code/registration, command execution, host screenshot/tests, installation, commit or deployment. Fill actual evidence after execution.
