# Toolbox Process Definition

Story: 13.1
Date: 2026-10-08
Status: accepted process; architecture/domain decisions remain tracked separately
Owner: Mary - Business Analysis

## Evidence And Decision Labels

C = confirmed by the user in this conversation. E = observed source/document evidence, not runtime certification. P = proposed process behavior. U = unresolved decision.

A label on a catalog row applies to every field in that row unless a field has its own label. In the preset matrix each cell has its own label. Confirmed requirements are not reopened by this proposal. No exact Innola server stage/definition identifiers have been verified in this story.

## Confirmed Process Requirements

- C: The examiner selects Compute, Compare, PLA_A, PLA_B or RT as a working preset; the actual Innola task is tracked separately and validated before server actions/handoff.
- C: One current-TR FGDB/case supports a single parcel or multiple parcels in Cad Maps, derived from plan dimensions or coordinate documents.
- C: Compare geometry is read-only. Authorized corrections happen in Compute, followed by fresh affected checks and comparison.
- C: Every included parcel must be accepted before the initial handoff. Partial parcel delivery is outside initial scope.
- C: Save reusable tabular data with geometry, including owners, neighbors, parish, equipment and north-arrow evidence where available.
- C: Enterprise working_review receives copies/updates at process checkpoints. It does not replace the working FGDB.
- C: A case ZIP attached to the current transaction preserves the spatial and tabular work.
- C: Current development remains usable in parallel. RC refers to existing RT Examination.

## Identity And Authority

| Concept | Authority | Example | Rule |
|---|---|---|---|
| C: Current transaction | Innola identity | Current TR number | Owns case and current-task outputs |
| P: Originating transaction | Explicit source link | Related PE TR | Preserve identity; never overwrite via RT/PLA_B |
| P: Transaction type/profile | Innola type plus configured profile | PE with multi-parcel Cad Map evidence | Suggest sources/rules; not number of parcels or official task |
| P: Official task definition | Verified Innola workflow metadata | Compute task definition | Stable definition ID if API exposes one; exact fields U |
| P: Active task instance | Fresh Innola task context | Assigned instance of Compare | Instance ID is not a permanent mapping key |
| C: Selected working preset | Examiner choice | Compute while Innola says Compare | Local organization; does not move server workflow |
| P: Examination step | Application process catalog | Dimension Check | Describes examination work, not a server transition |
| P: Tool | Reusable capability identity/version | Solve reviewed traverse | Declares scope, inputs, outputs and side effects |
| P: Evidence state | Persisted data/rule result | Accepted or stale for parcel A | Tied to saved revision and dependencies |

The header must distinguish selected workspace from actual task. P: unknown/ambiguous task permits authorized local preparation but blocks server writes/handoff. P: select the server target explicitly when several task instances are available. U: exact task identifiers, mappings and Compute correction ownership (D02/D03).

## Groups And Examination Steps

| Proposed group | Purpose | Typical steps |
|---|---|---|
| P: Case & Sources | Establish identity and preserved inputs | Open/restore, source inventory, related case |
| P: Extract | Create review candidates | Text/image extraction, coordinate import, metadata capture |
| P: Check | Evaluate current evidence | Structure, dimensions, georeference, orientation, parish |
| P: Review & Edit | Resolve evidence and save trusted geometry | Review points/segments, solve, manually edit, approve |
| P: Compare & Evidence | Reconcile sources and external records | Overlay, cadaster queries, overlaps, parties/neighbors |
| P: Save & Handoff | Persist and deliver accepted work | Save, suspend, report, snapshot, publish, attach, advance |

Recommended Compute steps: supporting documents, structure, extraction/manual capture, georeference, dimensions, review points/lines, create accepted spatial units, final review, handoff. They are recommended steps, not a rigid prohibition on rerunning a tool whose inputs are available. Tool prerequisites and progression gates are evaluated separately.

## Task And Action Matrix

R = required for the task's accepted outcome; O = optional if relevant; V = read-only geometry verification/reference; X = unavailable geometry mutation in this preset; Q = configured requirement still unresolved. P: required outcomes can be achieved through an approved alternative (for example manual capture instead of automatic extraction); R does not require one particular script.

| Action ID | Compute | Compare | PLA_A | PLA_B | RT |
|---|---|---|---|---|---|
| T01 Open/restore case | P:R | P:R | P:R | P:R | P:R |
| T02 Inventory/view sources | P:R | P:R | P:R | P:R | P:R |
| T03 Select page/crop evidence | P:O | P:O | P:R | P:R | P:O |
| T04 Load related geometry | P:O | P:V/O | P:O | E:V/R | E:V/R |
| T05 Structure check | P:R | P:R if absent/stale | P:R | P:O | P:O |
| T06 Extract point/segment candidates | P:O alternative to manual | C:X; use Compute | P:O alternative to manual | E:X for PE rebuild | E:X |
| T07 Import/manual geometry capture | P:O alternative to extraction | C:X; use Compute | P:O | P:V linked input only | P:V linked input only |
| T08 Capture/review key tabular values | P:R for required fields | P:O evidence | P:R for required fields | P:O | P:R for neighbor evidence |
| T09 Review/edit/solve geometry | P:R when constructing | C:X; use Compute | P:R when constructing | E:X for linked PE | E:X |
| T10 Dimensions/closure/orientation | P:R as applicable | P:V/R as applicable | P:R as applicable | P:V/O | P:V/O |
| T11 Georeference/parish check | P:R as applicable | P:V/R as applicable | P:O; explicit unreferenced policy | P:V/O | P:V/O |
| T12 Save/accept geometry revision | P:R | C:V only | P:R | P:V linked PE | P:V linked PE |
| T13 Overlay/query/compare evidence | P:O | P:R by declared evidence | P:R visual similarity | P:R annex evidence | P:R/ O by neighbor evidence |
| T14 Record task decision | P:R | P:R | P:R | P:R | E:R selected RT result at completion |
| T15 Local save/suspend | P:O anytime | P:O anytime | P:O anytime | P:O anytime | P:O; distinguish RT server Save |
| T16 Publish working projection | U:Q D05 | P:V; no geometry overwrite | U:Q D05 | P:X linked PE overwrite | P:X linked PE overwrite |
| T17 Report/snapshot/attach | C:R package at handoff | C:R package at handoff | C:R plus annex evidence | C:R plus diagram evidence | C:R target package plus existing RT evidence |
| T18 Advance actual Innola task | P:R explicit gate | P:R explicit gate | P:R explicit gate | P:R explicit gate | E:R selected RT branch |

C:R package identifies the user's target requirement, not existing per-preset packaging parity. Adapting RT/PLA_B packaging is future work. Their existing explicit uploads/writes are preserved.

## Individual Action Contracts

All rows below are P: proposed shared-tool contracts. Existing capability evidence and gaps are in the inventory. Input/output names describe business artifacts, not newly locked schemas.

| Action | Purpose/scope | Inputs and prerequisite | Editable/read-only output and persisted evidence |
|---|---|---|---|
| T01 | Open/resume current case | Current TR; available local/package revisions | Local case context and restore receipt; sources immutable |
| T02 | Identify/view document evidence | Copied attachments and manifest | Source inventory; viewing read-only; replacement creates new source version |
| T03 | Select evidence document/page/area | Readable source; valid page/extent | Selection/crop artifact and source coordinates; original unchanged |
| T04 | Load originating/reference geometry | Explicit linked TR and accepted source revision | Read-only reference copy/link and provenance; never relabel as current-TR ownership |
| T05 | Check structural usability | Current sources; configured rules | Structure findings with source hash and rule version; geometry unchanged |
| T06 | Capture geometry candidates | Usable source; approved provider/manual alternative | New draft points/segments and traces; no silent accepted-data replacement |
| T07 | Import or manually digitize | Declared coordinate system/provenance; editable Compute/PLA_A scope | Draft local geometry/rows and edit history; reference imports separately labeled |
| T08 | Capture/review key values | Source or prior candidates; field definitions | Raw/reviewed metadata, parties, neighbors, equipment; field-level source and disposition |
| T09 | Review/correct/solve | Parcel scope, review rows/segments/anchors; edit permission | Reviewed geometry evidence and solver findings; original candidates preserved |
| T10 | Test dimensional coherence | Saved points/segments/geometry; units/tolerances | Parse, continuity, closure, area, orientation and bearing findings; no geometry mutation |
| T11 | Test geographic coherence | Saved geometry/source reference evidence | Coordinate-reference/parish/placement findings; no implicit transformation |
| T12 | Save and accept geometry | Saved candidate; current required checks; examiner approval | Accepted FGDB revision plus approval evidence; draft kept distinct |
| T13 | Gather comparison evidence | Accepted geometry for approval; identified external sources | Overlay/query/overlap/owner evidence with source/time/revision; external geometry read-only |
| T14 | Accept/dispose task findings | Current required evidence; authorized examiner | Decision and notes referencing included parcels and accepted inputs |
| T15 | Save locally or suspend | Current case; active edits saved | Saved revision; optional package/upload under explicit suspend policy; no automatic completion |
| T16 | Publish accepted projection | Exact saved revision; verified server target/authority | Enterprise receipt, scoped feature identities and revision; local master unchanged |
| T17 | Report, snapshot, attach | Saved frozen revision; required evidence; upload authority | Consistent ZIP/report, integrity manifest and confirmed upload receipts |
| T18 | Advance task | Fresh task/ownership; compatible target; required receipts and all included parcels accepted | Lifecycle receipt; application does not invent transition IDs |

| Action | Rerun/invalidation rule | Server side effects | Handoff contribution |
|---|---|---|---|
| P:T01 | Compare revisions; reject wrong identity/corrupt package | Reads/downloads only | Establish current case identity |
| P:T02 | Changed source stales dependent extraction/checks | Read attachments; no upload | Required source completeness |
| P:T03 | New crop stales dependent extraction/report | Attach is separate explicit action | Required annex/source evidence |
| P:T04 | New source revision stales dependent reference decisions | Read only | Correct originating geometry |
| P:T05 | Rerun findings; replace current result, retain history | None | Current structure result |
| P:T06 | Preserve accepted overrides/IDs; reconcile new candidates | Optional provider request; no Innola mutation | Reviewed evidence exists |
| P:T07 | Change stales dependent geometry checks/decisions | None | Documented manual/import provenance |
| P:T08 | Changed reviewed field stales only dependent rules/reports/decisions | Task-specific writeback separate | Required fields reviewed or dispositioned |
| P:T09 | Changed shared edge stales every affected parcel | None | Resolved reviewed geometry |
| P:T10 | Replace current result only for matching revision/rules | None | Applicable dimensions acceptable |
| P:T11 | Place/CRS changes stale dependent results | None | Placement acceptable or authorized unreferenced status |
| P:T12 | New acceptance supersedes prior, never clears history | None | All included parcel revisions accepted |
| P:T13 | Refresh evidence explicitly; changed geometry invalidates comparison | External reads only | Current compare/annex/neighbor evidence |
| P:T14 | Changed input stales acceptance | Local decision; RT Plan update separately authorized | Current examiner decision |
| P:T15 | Save repeatable; preserve current data on failed suspend | Upload only for explicitly chosen suspend policy | Resume without completion |
| P:T16 | Same revision retry nonduplicating; conflict/partial projection explicit | Enterprise working write only | Mandatory checkpoint receipt if D05 requires |
| P:T17 | Retry same snapshot; failed upload retains local package | Attachment upload; generated evidence separately tracked | Required attachments confirmed |
| P:T18 | Verify server state before retry after ambiguous response | Authorized Innola transition/writeback only | Confirmed next task or recoverable failure |

## Data Scope And Acceptance

P: Case/document metadata stays visible regardless of active parcel. Geometry, dimensional diagnostics and parcel-specific approval filter by stable parcel ID. Party/evidence links can relate to multiple parcels; do not copy unlabeled owners into every parcel.

P: Preserve source hash/page/zone, raw value, reviewed value, units, confidence/status, reviewer and change reason. Missing, not applicable, uncertain and explicitly negative are distinct. North-arrow observation is not authorization to rotate geometry.

P: Case progress distinguishes detected, included, excluded, unresolved and accepted parcel counts. Excluding an erroneous duplicate candidate retains identity/reason; a blocked legitimate included parcel cannot be excluded to bypass acceptance.

C: All included parcels accepted is the initial aggregate gate. P: zero included parcels blocks geometry-task handoff; reference-only RT/PLA_B requires accepted relevant source context rather than fabricating newly constructed parcels. U: precise reference-task completeness policy D08.

P: Approval is a statement about saved evidence, geometry, applicable rule versions and included parcel set. Same tool run on a changed input is not reuse of the old approval.

## Modes, Reruns And State

| Proposed mode | Operation | Shared boundary |
|---|---|---|
| P: Automated preparation | Run declared non-destructive sequence; produce drafts/checks | Stop on missing evidence/approval; no implicit server mutations |
| P: Assisted | Suggest route, review candidates, selectively accept | Examiner controls corrections and dispositions |
| P: Manual | Import/digitize in editable workspace; enter missing data | Same provenance, checks and acceptance gates |

P: Failed/zero extraction keeps documents and diagnostics and offers manual capture. P: rerun writes new candidates and requires reconciliation to accept changes; approved manual values and parcel IDs remain intact.

P: Evidence progresses from absent -> draft -> reviewed -> accepted; changes make dependent accepted/check/decision results stale. Failed tool results never overwrite prior accepted artifacts. Geometry changes invalidate dimensions/placement as applicable, approvals, compare decisions, exports, projections and packages. Metadata edits invalidate only results which consume those fields. A source change invalidates its dependent extraction and downstream accepted evidence until reviewed.

## Save, Publish, Attach And Advance

P: Treat each as a separately persisted action/result. A local save can succeed while upload fails; display both accurately. An earlier successful upload does not make an incomplete multi-layer projection complete.

P: Handoff evaluates: current TR/source identity; fresh server task and operator authority; compatible selected-task/actual-task target; all included parcel acceptance; current applicable findings/decision; mandatory metadata; same-revision reports/package; configured projection/upload/writeback receipts.

P: Saving/suspending incomplete work is allowed without claiming task completion. Suspend package policy is D10; server suspend/upload still needs authority. The ordering of task-specific remote actions must preserve existing RT/PLA behavior and be finalized in 13.2.

P: Restore compares local, attached-package and Enterprise revision evidence before choosing a current case. Corrupt/wrong-TR packages block replacement, preserving usable local work. Geometry-only Enterprise recovery is labeled partial; missing reviewed tables/approvals are not fabricated.

## Architecture Handoff And AC Coverage

### Human Value Review And Compare Completion (C12-C14)

Compute primarily constructs/reviews geometry and checks structure, dimensions and georeferencing. It also reviews the extracted printed items: participants, neighbors, owners, volume/folio, lot numbers, equipment and other defined evidence. Human review records presence and accuracy separately; extracted/provider confidence does not establish correctness. Manual capture retains original extraction and reviewer provenance. Missing-from-source is a valid evidenced finding, distinct from not-extracted, not-reviewed or uncertain.

Compare validates these values against LTD/Innola records and may capture or correct current-case tabular evidence directly, without returning to Compute. It remains geometry read-only and does not silently write corrected source/system records. Tabular edits stale consuming comparisons/findings/decisions, not unrelated geometry checks. Geometry errors requiring edits still return to Compute.

Review available LTD evidence even when some data is unavailable/incomplete. Preserve per-item outcomes and reviewed evidence, mark outstanding comparisons pending with reason, and retain cumulative defects. Pending is not final approval/rejection or a confirmed server transition. End-of-Compare decision is approved/rejected only after applicable completion requirements are met; source-plan defects remain eligible for the confirmed cumulative review route.

### Confirmed Justified Handoff Exceptions (C10)

C11 refines this rule: the same authorized examiner fixes extraction discrepancies, records evidenced defects already present in the survey plan, and forwards through the normal next stage. Compare and later stages add findings to the same cumulative case review; do not stop examination or issue a first-error-only rejection merely because a source defect was found.

Distinguish extraction error, source-plan defect, mixed error and unknown origin. Correct the extraction component of mixed errors before forwarding; preserve the source defect. Stage status forwarded-with-recorded-defects is not valid-geometry approval. Complete applicable whole-document review and record checks that cannot be evaluated because of a source defect, with reason/evidence; unperformed checks are not passed. Each included parcel needs an explicit examination disposition. Preserve open/resolved findings and history across stages so rejection can describe the full review.

C11 supersedes generic exception eligibility/supervisor assumptions below for recorded source-plan defects. It does not waive transaction authorization, identity, integrity or permit publishing invalid geometry as accepted.

JotaPe confirmed that an authorized examiner must be able to hand off with unresolved examination findings and recorded justification. Every included parcel still needs current acceptance: clean or accepted-with-exceptions. Findings remain failed/unknown as observed, visible in review, reports and package; approving an exception does not turn a failed check into a pass.

The exception binds finding/scope, current inputs/revision, rule/policy version, examiner identity and verified authority, justification and time. Policy-required evidence is retained. Input/policy/authority changes require reevaluation and fresh exception approval. D12 still defines which findings/roles qualify, including geometry-quality findings; unknown eligibility or authority blocks the exception, not unfinished local work.

Case identity, referential integrity, current authorization, source read-only boundaries and package integrity cannot be waived. Downstream actions still validate required inputs; no synthetic geometry, missing artifact or successful publish is inferred from an exception. Earlier unconditional examination-finding blockers are qualified by this confirmed policy; unresolved parcel identity/inclusion is not an examination exception.

13.2 receives this process, [inventory](toolbox-capability-inventory.md), [walkthroughs](toolbox-scenario-walkthroughs.md) and [decision register](toolbox-decision-register.md). The FGDB/JSON ownership migration, exact server mappings, snapshot implementation and deployment isolation remain architecture work.

| Story AC | Process evidence |
|---|---|
| 1 | Groups, presets and action matrix |
| 2 | Identity/authority table |
| 3 | Preset matrix and two action contract tables |
| 4-5 | Data scope/acceptance; multi-parcel scenarios S02/S03/S10 |
| 6-7 | Modes/reruns; S04 and S10 |
| 8 | Identity, handoff, Compare correction S05 |
| 9 | Save/publish/attach/advance; S09/S11/S12 |
| 10 | Capability inventory and overlap mapping |
| 11 | Scenario walkthroughs S01-S12 |
| 12 | Decision register and architecture handoff |

Review boundary: documentation and selected source inspection only; scenarios are acceptance examples, not executed tests. JotaPe accepted the 13.1 process on 2026-10-08 and authorized Story 13.2. Open domain/architecture decisions remain explicit inputs rather than implied approvals.
