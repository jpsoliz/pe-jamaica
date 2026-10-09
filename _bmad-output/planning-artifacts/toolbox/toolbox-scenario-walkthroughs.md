# Toolbox Scenario Walkthroughs

Story: 13.1
Date: 2026-10-08
Status: proposed acceptance walkthroughs, not executed tests

C = confirmed product boundary; P = expected proposed behavior; U = decision still open. All fictional parcel IDs/revisions below are examples, not production Innola identifiers. Each table is P except an explicitly labeled C/U rule.

## S01 Single-Parcel Plan: Compute

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Current TR and survey PDF; no geometry | Open case, select Compute, inventory sources | Case context; source references/hash | Structure check |
| Readable source, correct selected page | Extract text/vision or choose manual capture | Draft parcel A, points/segments, key metadata and source traces | Review candidate values |
| Draft A with unresolved bearings | Correct/solve and rerun applicable dimensions/georeference checks | Saved reviewed A with current findings | Approve if required findings satisfied |
| Current reviewed A | Explicit acceptance | Accepted case revision r1 and parcel approval | Final review/report/package, configured publish |
| All required same-revision receipts and task authority | Explicit handoff | Verified lifecycle result | Open next actual task |

Missing equipment/north-arrow information remains visible; whether either blocks completion is U:D04. Automatic extraction does not approve its own output.

## S02 Multi-Parcel Cad Map: Blocked Parcel

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Multi-parcel source/coordinate document | Identify groups and import/extract | A/B/C candidates with stable memberships; document metadata global | Review each parcel |
| A/B acceptable; C has unparseable distance | Accept A/B, evaluate case | Accepted A/B; blocked C; included=3, accepted=2 | Correct C; save/suspend allowed |
| C remains included/blocked | Attempt handoff | C: handoff blocked; explicit parcel finding | Compute correction |
| C corrected and all required checks current | Accept C | Included=3, accepted=3; current case acceptance | Package all included parcels and tables |

Do not export only the first group or silently omit C. Partial parcel delivery is outside initial scope.

## S03 Shared Edge And Exclusion

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Accepted A/B share edge E; C independent | Edit E in Compute | New draft revision; A/B approvals and dependent checks stale; C unaffected unless other dependencies change | Review both affected parcels and shared-edge consistency |
| Corrected A/B with coherent adjacency | Revalidate/accept | New accepted A/B revisions and case decision | Fresh report/comparison/projection |
| Duplicate candidate D detected | Request exclusion with reason/source | D retained as excluded historical candidate; included set changes and aggregate acceptance recomputed | Review inclusion decision |
| Legitimate included C blocked | Try exclusion solely to pass | Rejected as acceptance bypass; original blocker remains | Correct or obtain a legitimate documented scope decision |

Exact shared-edge data representation is U:D07. Expected impact propagation is a process requirement, not a claim that current code implements it.

## S04 Failed Extraction And Manual Mode

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Readable plan; text parse yields zero usable records | Attempt extraction | Failed/zero result with source and diagnostics preserved; no accepted geometry overwritten | Vision route if allowed or manual capture |
| Automation insufficient | Select Manual; enter points/segments/key fields or digitize | Draft data with manual provenance and unresolved status | Same applicable checks/review |
| Manual data coherent | Review/accept | Accepted revision without requiring automated extraction success | Normal handoff gates |

Canceled extraction retains previous accepted evidence and records incomplete run; a process exit without required artifacts is not success.

## S05 Compare Correction With Different Selected Task

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Innola task Compare; accepted r3 | Select Compare and load r3 | C: geometry read-only; sources and queries usable | Recheck dimensions/placement and compare evidence |
| Evidence shows boundary error | Record correction request; select Compute | Innola still Compare; selected workspace Compute; reason retained | Authorized local correction; ownership U:D03 |
| Local r4 geometry changed | Save/revalidate/accept | Old compare decision stale; r3 retained as history | Reopen Compare against accepted r4 |
| New evidence accepts r4 | Record fresh compare decision | Current decision/report bound to r4 | Handoff if actual task and authority compatible |
| Selected preset not compatible with requested server target | Try upload/publish/transition | Block that server action; no invented backward transition | Compatible target or explicit supported return/reassignment |

Selecting Compute must not silently complete, claim or move an Innola task.

## S06 PLA_A: Selected Evidence And Unreferenced Geometry

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Current annex TR; multi-page plan | Select page/area | Saved evidence artifact/source coordinates | Extract/manual review |
| Reviewed traverse, no geographic anchor | Solve using allowed local-origin policy | Explicitly unreferenced draft geometry and findings | Dimension review; visual similarity |
| Accepted permitted geometry/evidence | Generate annex documents; review | Accepted case and annex outputs | Authorized attach/handoff |
| Upload failure | Retry failed output | Local evidence retained; successful uploads recorded separately | Resume remaining action; advance only after gate |

Visual match is not survey-accurate georeferencing. Local-origin permission is profile policy, not a generic PE exemption.

## S07 PLA_B: Related PE Recovery

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Current PLA_B TR and originating PE reference | Resolve/load approved PE geometry and output | Read-only source geometry retaining PE identity/revision; current case distinct | Source/diagram crop |
| Supporting plan available | Crop, save generated diagram | Current-TR evidence artifact; original document unchanged | Confirm explicit attachment |
| Diagram and related case evidence valid | Attach/complete via actual task gate | Current-TR upload/transition receipts; no PE overwrite | Next actual task |
| PE source revision unavailable/mismatched | Attempt recovery | Explicit blocker; no fabricated new PE geometry | Resolve reference/source |

Copy versus linked reference packaging is U:D08/D10; either preserves source authority.

## S08 RT: Save Versus Save & Close

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Current RT task; parent documents/PE geometry | Load evidence | Read-only PE SpatialUnits; current RT neighbor review | Edit neighbor evidence |
| Current RT neighbors reviewed | Explicit RT Save | E/P: current Plan neighbor write; checklist preserved; no SpatialUnit write; task not completed | Continue review |
| Ready RT review and selected result | Save & Close | Required neighbor save, valid current PlanCheck, selected branch transition and cleanup receipts | Next actual task |
| Plan save succeeds, transition fails | Reopen/retry | Partial server success visible; local trace retained | Verify actual task state before repeating transition |

Preserve existing branch outcomes: proceed to Review Completed RT Examination or prepare Plan Pre-Check Log Sheet. No server definition IDs are invented. Target toolbox package policy is not assumed already present in RT.

## S09 Package Restore And Newer Remote Revision

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Local r2; attached package indicates r3 | Open/resume | Version conflict/selection visible; compare verified identity/revision | Restore verified newer case or explicit recovery decision |
| Matching valid package | Restore | FGDB, tabular evidence, included parcels, findings/approvals and traces reconstructed | Resume tools appropriate to saved state |
| Corrupt/wrong-TR package | Try restore | Replacement blocked; usable local case retained | Select valid source/recover |
| Only Enterprise polygons available | Recover geometry | Labeled partial case; no fabricated metadata/approval | Reconstruct missing evidence before acceptance |

Exact revision/conflict resolution mechanism is U:D01/D06. An older local case must not silently publish over a newer accepted remote case.

## S10 Rerun With Manual Overrides And Parcel Split/Merge

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Accepted A/B with corrected owner/bearing | Rerun extraction | New candidates and source trace; accepted overrides retained | Compare/reconcile candidate changes |
| Candidate has changed grouping A -> A1/A2 | Accept proposed split only after review | Explicit lineage and memberships; affected inclusion/approvals stale | Validate all newly included groups |
| Candidate proposes merge A/B | Review membership/identity | Proposed merge recorded; no automatic reassignment | Confirm legitimate grouping or reject |
| Revised owner accepted, geometry unchanged | Save reviewed field | Dependent party/report/compare evidence stale; geometry checks reused only if unaffected | Refresh affected results |

Stable IDs, lineage and shared-edge implementation remain D07 architecture work.

## S11 Partial Handoff Failure

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Accepted r5 frozen | Create report/snapshot/package | Same-revision artifacts and integrity evidence | Required projection/upload |
| Points publish succeeds; lines/polygons fail | Publish checkpoint | Partial projection, not a successful full revision | Retry/repair failed projection steps |
| ZIP upload succeeds; report upload fails | Attach outputs | Confirmed ZIP receipt; report failure retained | Retry report for same snapshot |
| All mandatory receipts current; transition response times out | Advance | Ambiguous completion, not reported as success | Read current task state |
| Server already advanced | Verify/recover receipt | Completion recorded without duplicate transition | Open actual next task |

Cross-service atomicity is not assumed. Whether Enterprise projection is mandatory is U:D05.

## S12 Unknown Task And Incomplete Suspend

| Before/evidence | Action | After/evidence | Next eligible action |
|---|---|---|---|
| Known TR; unknown/ambiguous Innola task | Select Compute | Authorized local preparation; official task unresolved | Inspect/source/review locally |
| Parcel B incomplete | Local Save | Saved draft/revision; no completion claim | Continue or explicitly suspend |
| Suspend chosen | Package/upload per allowed policy | Local success separate from upload status; incomplete evidence retained | Resolve/retry upload or reopen |
| Task identity still unresolved | Request server write/handoff | Blocked despite selected preset | Resolve actual target/ownership |

Suspend inclusion/upload policy is U:D10; local save must not depend on pretending the task is complete.

## Review Outcome

Walkthroughs cover normal and negative paths for all five presets, all three modes, one/many parcels, candidate reconciliation, stale evidence, restart and partial side effects. They are process expectations pending review, not assertions that current implementation passes. See [process](toolbox-process-definition.md) and [decisions](toolbox-decision-register.md).

