---
story_key: 13-4-add-single-and-multi-parcel-toolbox-model
track: toolbox
story_type: code
created: 2026-10-08
depends_on: [13-3-add-toolbox-case-context-and-workspace-selection, 11-2-unified-pdf-extraction-contract-for-plan-examination]
execution_gate: epic-11-contract-reuse-and-shared-edge-policy
baseline_branch: wipRC9
baseline_commit: 5908f30617cdc49e09873635a30343da8aff9ad6
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.4 - Add Single- And Multi-Parcel Toolbox Model

Status: backlog
Preparation: detailed specification prepared; implementation readiness gates remain open.

Child preparation: JotaPe requested [13.4A - Stable Parcel Memberships And Acceptance](13-4a-add-stable-parcel-memberships-and-acceptance.md) on 2026-10-08. A now has its own detailed file, tracked through this parent; B remains proposed. This supersedes earlier statements that both labels have no files, not the open policy/dependency gates. Parent completion still requires A and B.

## Story

As an examiner,
I want stable parcel identities, explicit geometry/evidence memberships and parcel-level acceptance in one transaction case,
so that single-parcel plans and multi-parcel Cad Maps use the same tools without losing corrections or handing off incomplete work.

## Scope And Readiness

This is migration M2: a pure domain model and compatibility adapter, not FGDB persistence or a deployed Toolbox screen. Continue only in `temp/toolbox-worktree` on `refactor/toolbox-case-workspace` after authorization to implement. Protect operational source, case folders, configuration, registration and services.

Preparation does not make these entry gates pass:

| Gate | Current evidence | Required before implementation |
|---|---|---|
| 13.3 foundation | Done for local context/selection; code uncommitted in isolated worktree | Preserve that work and rerun focused context regressions |
| D11 / Epic 11 reuse | Story 11.2 is ready-for-dev, with unchecked tasks; canonical group/source-trace types are not present in inspected code | Accept ownership/dependency mapping; deliver and verify 11.2 contract in isolation, or explicitly approve a mapped adaptation preserving every relevant 11.2 AC |
| D07 identity/shared edges | Architecture direction accepted, GIS policy still open | Approve identity reconciliation, explicit boundary membership/direction, conflict disposition and split/merge lineage policy |
| Execution slices | Architecture proposes 13.4A and 13.4B | Accept decomposition before development; these are planning labels, not new tracker keys |

Do not mark 11.2 done/superseded, invent a second canonical extraction schema, or resolve D07/D11 implicitly. Keep this parent and its tracker row at backlog until the gates are evidenced. Once accepted, create separately scoped execution stories or record an explicitly accepted sequential decomposition; do not implement both slices as an unreviewed broad change.

### Recommended Order

1. 13.4A: consume the canonical Epic 11 contract; add case-scoped stable IDs, parcel/point/segment memberships, evidence links, included-set and local acceptance evaluation.
2. 13.4B: add candidate reconciliation proposals, explicit lineage and shared-boundary dependency/conflict model; prove invalidation across affected parcels.

A does not claim shared-edge consistency before B. Parent completion requires both sets of criteria. Existing JSON remains authoritative; adapter output is an in-memory domain projection. Persistence/reopen and recoverable committed revisions belong to 13.5.

### Out Of Scope

FGDB creation or renaming, CaseStore commits, filesystem migration, solver/OCR rewrites, automatic snapping/topology repair, Parcel Fabric, new rules engine, tool execution/registry, UI/routing/DAML changes, Enterprise writes, ZIP/upload/restore, Innola lifecycle and deployment.

Future 13.5 uses `TR-<transaction-number>.gdb`, preserves leading zeros and leaves legacy numeric FGDBs untouched. The naming decision is not parcel identity or isolation.

## Acceptance Criteria

1. Given the accepted readiness gates, when development begins, then the isolated worktree retains 13.3 changes, uses synthetic fixtures and does not modify operational source/tracker/cases, install the add-in or call live services. A reuse map identifies 11.2-owned canonical fields versus Toolbox-specific memberships, inclusion, lineage and acceptance.
2. Given one parcel or at least three Cad Map parcels from plan dimensions or coordinate evidence, when the canonical review contract is adapted, then every parcel, point and segment is represented without selecting only the first parcel. Case/document facts stay global; evidence/party links can reference multiple parcels without copying facts into each. Raw candidates and reviewed/manual values remain distinct.
3. Given canonical parcel groups, when Toolbox identities are mapped, then existing nonblank group IDs are preserved as source keys within the case, Toolbox parcel IDs are stable within that case and mappings are retained explicitly. IDs cannot be derived anew from display labels, array positions, ObjectIDs, point labels or TR type. Rename/reorder preserves identity; duplicate/ambiguous IDs produce diagnostics, not silent deduplication or merging.
4. Given memberships, when validated, then point/segment/boundary references resolve to known IDs in the same case; ordered membership preserves reviewer order and boundary direction. Reject blank/duplicate keys, orphan references, invalid direction/sequence and cross-case links with typed diagnostics. Repeated point labels in different parcels are valid and do not establish a shared point. Do not rewrite source objects during validation.
5. Given selected parcel IDs in the 13.3 context, when parcel scope is resolved, then all requested IDs are validated against the model, multiple selections remain supported and unknown IDs fail explicitly. Empty selection is allowed for case/document work. Selection/filtering does not change inclusion or acceptance. Linked PE source geometry remains separate, read-only and retains source identity/revision.
6. Given included/excluded parcels, when inclusion changes, then exclusion preserves the parcel and records actor, reason and supporting evidence reference. A blocked legitimate parcel cannot be silently removed to pass the gate; only explicit, policy-valid dispositions can exclude it. Re-inclusion requires current acceptance. Changing the included set invalidates the aggregate decision; exclusion does not erase shared-edge dependencies affecting included neighbors.
7. Given applicable check requirements and current geometry/evidence fingerprints, when a parcel is accepted, then the record binds case/parcel, input fingerprint, rule/policy versions, reviewer and time. Missing required policy/check inputs, blocking outcomes, required unknown/stale results or unresolved identity/shared-edge conflicts prevent acceptance with actionable reasons. Report-only failures remain visible but do not block unless policy says so; required review/disposition effects must be satisfied by an explicitly authorized, policy-valid decision (D12 is not implicitly resolved). A caller-supplied legacy approval flag/hash alone cannot establish Toolbox acceptance; prior acceptance records remain history.
8. Given the initial geometry-task gate, when evaluated, then success requires a nonempty included set, an explicit inclusion or policy-valid exclusion disposition for every detected candidate, and current acceptance for every included parcel plus any required case-level results. Unresolved/unassigned candidates block the aggregate even when not yet included. A three-parcel case with two accepted and one blocked is blocked even when only accepted parcels are selected. Return detected/included/excluded/unresolved/accepted/stale counts and per-parcel blockers. This is local readiness only; it never grants remote handoff authority or declares RT/PLA_B completeness policy D08 resolved.
9. Given explicitly identified shared boundaries, when the model is built, then boundary IDs have parcel memberships and direction; independent source evidence is retained. Contradictory geometry/evidence yields an unresolved conflict and prevents affected acceptance. No equality-by-name/coordinate-only merge, tolerance-based snapping or automatic opposite-bearing reconciliation occurs. Geometry overlap alone is not proof of a shared boundary.
10. Given a changed shared boundary or parcel evidence, when impact is calculated, then every dependent parcel's checks/acceptance/comparison and case aggregate become stale; unrelated parcel results remain current unless they depend on a changed case-wide fact/policy. Impact uses the union of previous and proposed dependency/membership sets so removal or reassignment invalidates former as well as new dependents. It includes all shared memberships, including excluded parcels, and returns IDs/reasons for later consumers. Historical records are retained; no package/publish side effect is performed.
11. Given a rerun candidate batch and expected base revision, when reconciliation is proposed, then accepted/manual evidence is not replaced automatically. Explicit matching evidence can propose reuse of stable IDs; uncertain/new/removed identities require disposition. Split/merge records parent/child lineage and inclusion decisions, invalidates affected acceptance and cannot silently transfer approval. A stale base revision is rejected without changing current data.
12. Given any geometry mutation proposal, when domain eligibility is evaluated, then 13.3 GeometryAccess and source-read-only boundaries are enforced: Compare rejects it; Compute requires explicit local permission. Querying acceptance/impact does not mutate geometry or change actual Innola task. A returned valid proposal is not a persisted edit or server authorization.
13. Given the implementation, when verified, then the existing executable C# harness covers criteria 2-12 with single/multi-parcel fixtures, negative relationships, rerun preservation and shared-edge impact. Build/focused/full results and baseline failures are reported separately. The operational legacy review/approval/output behavior remains unchanged; no hosted UI, FGDB round-trip or live TR end-to-end pass is claimed.

## Tasks / Subtasks

### Confirmed C10 Qualification

C12-C14 further qualify the child contract (AC17-19): human-confirmed presence/accuracy with raw/manual review history; missing-from-source is valid; current-case tabular corrections happen directly in Compare; unavailable/incomplete required LTD comparisons remain pending with recorded reasons and preserved partial work. Geometry stays Compute-editable/Compare-read-only. Parent completion includes these states; detailed UI/service integration stays 13.8/13.9.

C11 subsequently narrows the model: fix extraction mismatches, record genuine survey-plan defects, and let the same authorized examiner forward to the normal next stage. Later stages accumulate further findings for comprehensive rejection. Child AC13-16 define origin classification, cumulative register and whole-document review coverage. All included parcels need examination disposition, not falsely valid geometry; forwarded-with-recorded-defects never grants geometry-output/projection approval. This supersedes broad generic-waiver wording below.

The earlier C10 proposal allowed unresolved findings through authorized, justified, revision-bound exceptions. C11 refines it into source-defect forwarding, not waiver of extraction discrepancies or valid-geometry approval. See child 13.4A AC13-16 for the current audit, classification, authority and test requirements; D12 cannot reopen the confirmed same-examiner/normal-stage choice.

Retain original failed/unknown outcomes, exception justification and history. Changed inputs/dependencies/policy or expired/revoked authority stale exceptions and aggregate. B must propagate invalidation to exception approvals; 13.9 includes them in reports/package and revalidates authority before handoff. An exception cannot fabricate missing geometry/artifacts or guarantee unsupported publication.

- [ ] Resolve and record D11 reuse/ownership, D07 policy and accepted decomposition; verify 11.2 dependency in isolated code. (AC: 1, 3, 9, 11)
- [ ] Add failing single-parcel and three-parcel model/adapter fixtures and negative identity/membership tests. (AC: 2-5, 13)
- [ ] Implement stable case-scoped identity mapping, explicit ordered memberships and global/many-parcel evidence links over the reused contract. (AC: 2-5)
- [ ] Implement explicit inclusion dispositions, immutable acceptance records and all-included local aggregate evaluation. (AC: 6-8)
- [ ] Include C10 accepted-with-exceptions and child AC13-15; retain failed findings and invalidate exception approvals with dependencies. (AC: 7-10; C10)
- [ ] Add shared-boundary/conflict/dependency fixtures; implement conservative impact calculation without geometry repair. (AC: 9-10)
- [ ] Add candidate reconciliation and split/merge lineage proposals, preserving prior/manual data and rejecting stale inputs. (AC: 3, 11)
- [ ] Enforce context permissions and read-only source scope for mutation proposals; retain remote deny. (AC: 5, 12)
- [ ] Build, run focused/full regressions and review isolated diff; record evidence and remaining gates before status changes. (AC: 1, 13)

## Dev Notes

### Inspected Source And Reuse Map

Source paths below are relative to `src/ParcelWorkflowAddIn/`.

| Boundary | Current behavior | 13.4 use / preserve |
|---|---|---|
| `ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs` | Rows have RowId, ParcelGroupId, point label and SequenceInGroup; segments have SegmentId, ParcelGroupId and effective reviewed values; global metadata/parties/memorandum and RootMetadata | Read through 11.2 canonical contract. Existing row/segment IDs and display labels are not evidence of stable shared-boundary identity. Preserve raw objects, original/reviewed distinctions and source order |
| `ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs` | Legacy JSON loading/saving, preserved metadata and hash-bound approval | 11.2 owns canonical loader changes. Do not replace the writer/hash/approved_review.json path in 13.4; no dual editable JSON/Toolbox master |
| `ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs` | Parcel-filtered point insertion, sequence normalization, unique manual RowId and point labels | Reuse produced evidence; labels/sequence-generated IDs are not persistent rerun matching. Do not call a mutating helper during read-only adaptation |
| `ParcelWorkflowAddIn/Workflow/Toolbox/ToolboxCaseContext.cs` (isolated only) | Immutable case/task context, defensive selected IDs, explicit local Compute permission, read-only sources and remote deny | Consume directly; case selection currently does not validate membership. Add resolution in a separate model service rather than expanding context into a mutable geometry store |
| `ParcelWorkflowAddIn.Tests/Program.cs` | Explicit harness registrations and name-substring filtering | Planned UPDATE: register focused tests only; preserve every existing registration/filter |

Story 11.2 owns source inventory, reusable source trace, canonical parcel groups/findings and legacy JSON compatibility. 13.4 owns the Toolbox domain overlay: identity maps, memberships, inclusion dispositions, revision-bound acceptance, explicit lineage and dependency impacts. Reuse canonical types after delivery; do not guess their final type names or duplicate them before ownership acceptance.

Planned NEW files under `ParcelWorkflowAddIn/Workflow/Toolbox/`: small parcel model records and focused adapter/validation/acceptance/impact/reconciliation services. Choose names consistent with 13.3; do not create a generic graph framework. Tests under `ParcelWorkflowAddIn.Tests/Workflow/`, for example `ToolboxParcelModelTests.cs`. No production UPDATE is assumed; any need to edit existing canonical files requires the recorded ownership decision. Read the full current UPDATE targets and relevant tests again before code edits.

### Model And Algorithm Guardrails

- Pass complete case identity and expected base revision explicitly; scope keys by case/environment, not TR display text alone.
- Defensively copy exposed collections; failed validation/proposal leaves current state unchanged.
- Preserve canonical parcel_group_id as a source key. Store an explicit mapping to Toolbox parcel_id; never overwrite legacy identifiers merely to fit a new format.
- Allocate opaque stable IDs once through an injectable ID source. Membership/provenance records carry those IDs forward; reopening persistence is 13.5.
- Required relationships: parcel-point (sequence/role), parcel-segment (sequence/direction), parcel-boundary (direction), evidence-parcel and lineage parent-child links. Reject duplicate membership keys while permitting valid explicitly ordered repeated vertices for a closed ring.
- Validation returns structured severity/code/scope/affected IDs and safe message. Counts are derived, not editable substitutes for the underlying relationships.
- Accept evaluation consumes explicit applicable requirements/current result identities. Missing policy/check inputs fail closed; do not invent mandatory metadata or waiver authority while D04/D12 remain open.
- Fingerprints bind semantic inputs and dependency scope. Document deterministic canonical field/order handling; include geometry token, evidence, units/CRS and policy/rule versions. Do not hash display timestamps or FGDB bytes. Actual geometry normalization/curve/Z/M precision policy is not invented in this pure-model story; unknown geometry semantics cannot yield acceptance.
- Domain records express stale/history; downstream 13.6/13.8/13.9 consume impact IDs. No hidden singleton/session mutation, silent ID rematching, or auto-acceptance on successful solver/extraction.
- Existing C# net8.0-windows/x64, ArcGIS SDK lane and executable harness remain. No dependency/framework upgrade, GIS SDK call or new Python extraction logic is required.

### Test Matrix And Verification

| Fixture | Expected result |
|---|---|
| Single PXA-style group, reviewed/manual evidence | One mapped parcel; raw/reviewed fields intact; acceptance requires current checks |
| Three-parcel Cad Map from dimension/coordinate evidence | All memberships retained; one blocked parcel blocks aggregate |
| Duplicate labels, reordered/renamed groups, repeated point labels | IDs preserved; no label-based merge; selection does not alter inclusion |
| Blank/case-insensitive duplicate IDs, orphan/cross-case link, bad sequence/direction | Structured failure; source/model unchanged |
| Empty/no included set, undispositioned detected candidate, excluded parcel with missing evidence, re-inclusion | Block; every candidate disposition explicit; aggregate invalidated |
| Report-only failure versus blocking/required-disposition outcome | Apply declared policy effects; no blanket failure block or invented waiver |
| One shared edge with two or more parcel memberships | Change stales every dependent parcel; unrelated parcel current |
| Excluded neighbor shares changed boundary | Included neighbor still invalidated; dependency not dropped |
| Boundary/evidence membership removed or reassigned | Union of previous/proposed dependents stale; no former dependent left current |
| Conflicting shared-edge evidence or unknown geometry policy | Affected acceptance blocked; no snapping/rewriting |
| Rerun/manual override, stale base, explicit split/merge | Proposal only; prior accepted/manual state retained; fresh approval required |
| Changed case-wide fact/rules versus parcel-only fact | Exact dependent scopes stale, aggregate reevaluated |
| Compare/source-reference versus permitted Compute | Mutation proposal denied/allowed locally as appropriate; remote always denied |

Run from the isolated worktree after implementation:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner"
dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build
```

Add legacy review/persistence/approval focused runs using actual registered names; do not assume the filters above cover them. 13.3 recorded a clean build/22 focused passes and a reproduced pre-existing full-harness Compare XAML failure; see deferred work. Reconfirm baseline and record new regressions separately. Tests after a fail-fast full-run failure are not implicitly passed. Pure fixture coverage here is not testing a real TR through the application.

### References

- [Epic 13 scope and reuse policy](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md)
- [Process: data scope, all-included acceptance and reruns](../../planning-artifacts/toolbox/toolbox-process-definition.md)
- [Architecture: parcel and evidence model](../../planning-artifacts/toolbox/toolbox-architecture.md#parcel-and-evidence-model)
- [Contract catalog](../../planning-artifacts/toolbox/toolbox-contract-catalog.md)
- [Migration M2 and proposed slices](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md)
- [Acceptance strategy: S01-S03/S10](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md)
- [Open D07/D11 and confirmed naming](../../planning-artifacts/toolbox/toolbox-decision-register.md)
- [Story 11.2 canonical contract dependency](../11-2-unified-pdf-extraction-contract-for-plan-examination.md)
- [Story 13.3 foundation and review](13-3-add-toolbox-case-context-and-workspace-selection.md)
- [Baseline limitations](deferred-work.md)
- [Project context](../../project-context.md)

## Dev Agent Record

### Agent Model Used

Codex; BMAD create-story preparation only. Implementation agent: not started.

### Debug Log References

Prepared against operational baseline 5908f30 and the isolated 13.3 foundation. Inspected accepted process/architecture/contracts/migration/acceptance, Epic 11.2 and current review model/manual point/context code. No new external API or dependency behavior is specified; no version upgrade research is needed for this pure contract preparation.

### Completion Notes List

- Detailed parent specification prepared on 2026-10-08; tasks remain unchecked.
- Independent preparation review applied three refinements: explicit disposition of every detected candidate, policy-sensitive finding effects, and old/new dependency union for removals/reassignments. Document links, contiguous AC numbering, unchecked tasks and mirrored tracker status validated; no runtime tests were run.
- Readiness intentionally remains backlog: unresolved D07/D11 and undelivered canonical contract prevent a truthful ready-for-dev designation.
- Recommended A-before-B decomposition remains proposed, with no new tracked IDs or operational story retirement.
- No application code, build, runtime test, case data, installation or service operation changed by preparation.

### File List

Preparation artifacts only:
- `_bmad-output/implementation-artifacts/toolbox/13-4-add-single-and-multi-parcel-toolbox-model.md`
- `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml` (readiness comment only)
- `_bmad-output/planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md` (preparation status and story link)

The same preparation documents are mirrored into the isolated worktree. Implementation file list and test evidence are pending.
