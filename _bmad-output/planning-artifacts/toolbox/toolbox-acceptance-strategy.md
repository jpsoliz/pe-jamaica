# Toolbox Acceptance Strategy

Story: 13.2
Date: 2026-10-08
Status: architecture direction accepted 2026-10-08; feasibility/host/sandbox tests remain unrun

## Verification Layers

1. Pure contract/service tests in existing C# executable harness and Python tests: context authority, identity normalization, revisions/hash dependencies, table relationship planning, inclusion/approval and ledger state.
2. ArcGIS-hosted fixtures: FGDB schema/read/write, edit save and interrupted commit recovery, supported snapshots, maps/read-only consumers, CRS/geometry fidelity.
3. Mock/fault-injected remote tests: stale task, expired session, conflicting revisions, partial publish, unknown upload/transition outcome.
4. Authorized isolated sandbox integration: actual mapping, service capability/concurrency/rollback behavior, package attachment read-back and task transitions.
5. Product fixture walkthroughs: single parcel and multi-parcel Cad Map through Compute, reopen, Compare correction and same-revision handoff; PLA/RT branch/write boundaries.
6. Operational compatibility smoke: baseline installation/data remains usable and untouched throughout isolation; no blanket full-suite pass inferred from targeted tests.

Use existing harness/frameworks, no test framework replacement. Exact test commands/toolchain come from project context when implementation begins. No build/test or sandbox mutation is required to approve these documents.

## Required Fixture Matrix

| Scenario | Required evidence | Failure assertion |
|---|---|---|
| S01 Single parcel | Text/scanned/manual variants; source trace; accepted revision; round-trip package | Unreviewed candidates cannot become accepted output |
| S02 Cad Map | At least three parcels, one initially blocked | Extraction error blocks forwarding until corrected; documented source defect may proceed after all included parcels examined; no valid-geometry pass implied (C11) |
| S03 Shared edge | Two adjacent parcels and one independent parcel | Both affected parcels invalidated; unrelated checks reused only when dependencies match |
| S04 Zero/canceled extraction | Saved sources, run diagnostics, previous accepted snapshot | No override loss; manual route available; incomplete artifacts not success |
| S05 Compare correction | Selected Compute with actual Compare, then new accepted revision | Compare never edits; no server stage claim; old decision stale |
| S06 PLA_A | Selected page/evidence and permitted local-origin shape | Unreferenced label retained; visual match not georeference |
| S07 PLA_B | Distinct current/source TR and copied PE revision | No source PE mutation; mismatched reference blocks |
| S08 RT | Current Plan neighbors, checklist and both result branches | Save preserves checklist/no SpatialUnit write; Save & Close uses chosen valid PlanCheck/transition |
| S09 Restore | Newer local/remote, corrupt, wrong TR, later task | Valid same-case later task allowed via v2 policy; wrong case rejected; old local preserved |
| S10 Rerun/split/merge | Manual values, stable IDs and lineage | No index-based parcel reassignment or silent override loss |
| S11 Partial handoff | Layer failure, confirmed upload, ambiguous transition | No false complete revision; no duplicate replay of confirmed actions |
| S12 Unknown task/suspend | Missing/fallback taskId and unfinished parcel | Authorized local work allowed; remote mutation blocked; suspend does not mean complete |
| S13 Cumulative source-defect review | Compute source defects, corrected extraction mismatch, additional Compare findings and full-stage coverage | Normal next stage retains every finding/history; incomplete extraction correction/review blocks; final rejection is cumulative; defective geometry never advertised valid |
| S14 Human values and partial LTD comparison | Present/accurate review, human-confirmed source absence, manual Compare tabular correction and incomplete/unavailable LTD data | Raw values/history retained; absence not extraction failure; geometry unchanged; dependent results stale; reviewed available items preserved under pending reason, no final decision/transition implied |

Synthetic/sanitized fixtures first. Previously identified production cases are evidence candidates only; do not bundle personal data or perform live writes merely because their numbers appear in old story notes.

## Architecture Feasibility Spikes

| Spike | Question | Passing evidence | Blocks |
|---|---|---|---|
| F01 FGDB commit/recovery | Can CaseStore detect/recover interruptions across GDB and manifest? | Forced failure at each journal state; deterministic resume/quarantine/last snapshot recovery | 13.5 authority cutover |
| F02 External/manual edits | Can save observe all editable datasets and changed shared boundaries? | SDK edit plus external-change detection; unsupported direct writes blocked from approval | 13.5/13.7 |
| F03 Snapshot | Does supported GDB copy preserve complete tables/relationships/geometry for reopen? | Host-side save/quiesce/copy, checksum/read-back and independent restore | 13.9A |
| F04 Task mapping | Which exact definition/instance/transition IDs and ownership checks exist? | Sanitized read-only API evidence and unambiguous mapping fixtures | 13.3 remote policy/13.9C |
| F05 Projection | Can all target layers/marker commit consistently with stale-write protection? | Actual metadata, exclusive-writer/concurrency proof, rollback and read-back tests | 13.9B mandatory publication |
| F06 Upload reconciliation | Can attachment/Plan/transition results be identified after timeout? | Confirmed identities and read-back/deduplication or explicit blocked recovery | 13.9C |
| F07 Deployment isolation | Can test build run without replacing baseline registration/config/data? | Isolated environment plus installation identity/settings check and rollback | Any experimental installation |

No spike is marked passed in this architecture story. Absence of required service capability results in a blocker/alternate reviewed design, not a client-side promise of atomicity.

## Contract Example And Document Validation

Check local links/tables, JSON example syntax, schema version consistency and AC traceability. The illustrative package checksum is not a valid real schema instance; implementation fixture validators must replace it with a valid digest and complete archive inventory. Mermaid diagrams are source-reviewed; no UI/prototype rendering is claimed.

## Story 13.2 Acceptance Coverage

| AC | Deliverable / section |
|---|---|
| 1 | Architecture traceability; migration phases and Epic 11 dependency |
| 2-3 | Actual Innola evidence, context/mapping/mutation contracts |
| 4-5 | Data authority, commit protocol, FGDB schema, memberships/lineage |
| 6-7 | Component ownership, tool contracts and snapshot/export consistency |
| 8 | Enterprise projection protocol, receipts and capability spikes |
| 9 | Package v2, snapshot/restore, handoff ledger |
| 10 | Consumer changes and task-specific integration fixture cases |
| 11 | Parallel migration/isolation, protected targets and rollback |
| 12 | Fixture matrix, spikes and proposed story decomposition |
| 13 | ADR alternatives/trade-offs and decision register with readiness |

## Exit Gates

Story 13.2 moves to review once documents/source evidence/traceability are complete. Product/architecture acceptance is needed for done. Open policy decisions retain owners and block affected implementation; accepting the architecture direction does not prove service capability or deployed behavior.

13.3 may be prepared after architecture acceptance as a local context-only story in an isolated worktree; it must not require resolving all remote policies prematurely. Parcel/persistence/handoff stories need their named domain and feasibility decisions. No downstream row becomes ready solely because these documents exist.

2026-10-08: Architecture accepted; 13.3 foundation implemented in isolation. Build plus six local Toolbox and fourteen legacy planner tests pass. Full-suite execution remains incomplete due to baseline failures reproduced in a detached baseline checkout. This evidence is limited to local context/selection; no F01-F07 spike, hosted UI or service/deployment acceptance is claimed.
