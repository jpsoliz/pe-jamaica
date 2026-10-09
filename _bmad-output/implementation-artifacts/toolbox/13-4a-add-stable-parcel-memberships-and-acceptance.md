---
story_key: 13-4a-add-stable-parcel-memberships-and-acceptance
parent_story: 13-4-add-single-and-multi-parcel-toolbox-model
track: toolbox
story_type: code
created: 2026-10-08
depends_on: [13-3-add-toolbox-case-context-and-workspace-selection, 11-2-unified-pdf-extraction-contract-for-plan-examination]
execution_gate: canonical-contract-reuse-and-local-identity-policy
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
tracking_parent_key: 13-4-add-single-and-multi-parcel-toolbox-model
---

# 13.4A - Add Stable Parcel Memberships And Acceptance

Status: backlog
Preparation: requested by JotaPe; detailed child specification prepared, not implemented.

## Story

As an examiner,
I want single-parcel plans and multi-parcel Cad Maps represented by stable parcel identities, explicit memberships and individual acceptance,
so that selecting a parcel never hides incomplete work and all included parcels meet the same local acceptance requirements.

## Scope And Readiness

First execution slice of parent 13.4, migration M2. Deliver an in-memory domain model, a read-only adapter over the canonical review contract, membership validation, scope resolution, inclusion dispositions and local acceptance evaluation. Existing JSON remains authoritative. This is not a visible Toolbox screen or a working FGDB.

JotaPe's request prepares A as a separate story. B remains the proposed next slice; neither policy acceptance nor implementation authorization is inferred from preparation. Track A through the existing parent entry for now; no new sprint key or operational status is added automatically.

| Entry Gate | Evidence Required | Current State |
|---|---|---|
| 13.3 foundation | Preserve isolated context code and rerun focused tests | Local slice done; uncommitted work must remain intact |
| 11.2 / D11 | Accepted ownership map and verified canonical group/source-trace contract in isolated code, or explicit mapped adaptation approval | Not delivered; 11.2 tasks remain unchecked |
| D07 local identity subset | Accept mapping from canonical case/group/row/segment keys to stable domain IDs, ambiguity rejection and carried-forward mappings | Proposed rules below, not accepted GIS policy |
| Local acceptance policy | Explicit requirement/effect/version/disposition inputs, with missing inputs blocking | Synthetic policy fixtures allowed; production D04/D12 not resolved |

Shared-boundary reconciliation and split/merge policy are B gates, not required to design A's records. A must nevertheless report unknown shared-consistency evidence as a blocker when it is required, never assume independent rings are consistent. Parent 13.4 cannot close when only A is complete.

Implement only after entry gates and execution authorization, in `temp/toolbox-worktree` on `refactor/toolbox-case-workspace`. No operational case reads, mutation credentials, installation, service calls or branch switch.

### Exclusions

13.4B owns candidate rerun matching, shared physical boundary IDs/memberships/conflict resolution, shared-edge impact calculation and split/merge lineage. A does not rematch reruns, automatically merge equal labels/coordinates, snap geometry or replace accepted/manual evidence.

13.5 owns identity-map persistence, FGDB schema/save/reopen, recoverable revisions and authority cutover. Future naming remains `TR-<transaction-number>.gdb`; legacy numeric FGDBs are untouched.

No solver/OCR rewrite, new rules engine, UI/DAML/routing, geometry edit command, tool registry, output generation, package, Enterprise publication, Innola writeback/transition or deployment.

## Acceptance Criteria

1. Given accepted entry gates, when implementation starts, then changes/build outputs stay in the isolated worktree, 13.3 work is preserved and the reuse map distinguishes canonical 11.2 fields from A's identity/membership/inclusion/acceptance overlay. No operational code/tracker/data, configuration or registration is changed.
2. Given a canonical review document with one parcel or at least three parcels from a Cad Map, when adapted, then every group, point row and segment is retained, including groups with zero usable geometry. Preserve source keys, order, raw/original and reviewed/manual evidence without mutating the document. Missing/ambiguous canonical structure yields diagnostics rather than an improvised replacement contract.
3. Given case-scoped identity mappings, when adapting or reordering/renaming a document, then known canonical group/row/segment keys retain their allocated domain IDs through an explicitly supplied map. Allocate IDs once for previously unmapped, unambiguous keys through an injectable generator. Reject blank/case-insensitive duplicate keys, inconsistent/reused mappings or cross-case maps without partial results. Display labels, sequence, ObjectID, TR type and coordinates do not determine identity. This is same-contract re-adaptation, not rerun reconciliation or persisted reopen.
4. Given point and segment memberships, when validated, then all referenced IDs belong to the same case and resolve to known entities; sequence/role/direction preserve canonical reviewer order. Reject orphan, duplicate membership/sequence keys, invalid direction, ambiguous ordering and foreign references. Explicit repeated closing vertices can reference the same point at different valid positions. Identical point labels across parcels remain distinct unless an explicit supported identity link exists; A does not infer shared physical vertices or boundaries.
5. Given global facts and evidence/party links, when projected, then case/document facts remain globally accessible and one evidence/party item can link to multiple parcels without duplicated mutable values. Validate link IDs, scope and provenance. Preserve raw, normalized and reviewed distinctions and unknown canonical metadata; missing/not-applicable/negative/uncertain states do not collapse to a boolean pass. A does not add a competing source-trace serializer.
6. Given selected IDs from 13.3, when scope is resolved, then all selected IDs must resolve in the current model; multiple IDs and an empty case/document selection are supported. Unknown/foreign IDs return a failure without silently selecting the first parcel. Selection never alters inclusion or acceptance. Linked PE references retain their own case/source/revision identity and read-only scope, rather than being relabeled as current-case parcels.
7. Given detected candidates, when inclusion is evaluated or changed, then each has explicit included, excluded or unresolved state; initial adaptation never silently excludes one. An exclusion retains the parcel and requires actor, reason, resolvable supporting evidence and policy-valid disposition. Unknown exclusion policy is blocked, not guessed. Re-inclusion does not revive prior acceptance; any included-set change invalidates the aggregate decision. Exclusion retains geometry/evidence for B's later dependency analysis.
8. Given parcel requirements and versioned current evidence/check inputs, when acceptance is requested, then an immutable record binds full case/parcel identity, semantic input fingerprint, rule/policy versions, actor/time and dispositions. Required missing/unknown/stale results, blocking effects, unresolved identity and required unverified shared-consistency evidence block acceptance with scoped reasons. Report-only failures remain visible but do not block solely because outcome is failed. Required review/disposition effects demand explicitly authorized policy evidence; legacy approval flags/hashes or successful extraction/solver runs do not imply Toolbox acceptance.
9. Given a proposed/current acceptance record, when evaluated against current inputs, then changed geometry/evidence, memberships, consumed facts, rule/policy versions or re-inclusion make it stale. Label-only changes do not invalidate unless a requirement consumes the label. Preserve previous records as history. The case aggregate binds the sorted included-ID set and current parcel fingerprints plus required case results; a changed included set or required case input invalidates it. Missing fingerprint inputs cannot match by empty/null defaults.
10. Given aggregate local readiness, when evaluated, then it requires a nonempty included set, valid disposition of every detected candidate, current acceptance of every included parcel and current required case results. A three-parcel case with two accepted and one blocked/unresolved cannot pass even when only the two accepted parcels are selected. Return detected/included/excluded/unresolved/current-accepted/stale counts and reasons derived from the model. Success is local domain readiness only: remote action access remains denied; A does not certify shared topology, completed handoff or RT/PLA_B reference completeness.
11. Given read-only adaptation, validation or eligibility evaluation in any preset, when called, then source geometry/manual overrides and actual Innola task remain unchanged. Compare and linked-source geometry are read-only; A provides no geometry-write operation. Any later consumer querying local geometry eligibility must use 13.3 GeometryAccess (Compute plus explicit local permission); acceptance evaluation itself never grants edit/server authority.
12. Given completed implementation, when verified, then the existing executable C# harness covers AC2-11, canonical compatibility and unchanged 13.3/legacy behaviors. Record build, focused and full-suite results separately, distinguishing known baseline failures. No FGDB round-trip, hosted screen, real-TR workflow or B consistency pass is claimed.

### Confirmed Examination Disposition: C10/C11

JotaPe clarified that the same authorized examiner records underlying source-plan defects and forwards through the normal next stage so later examiners can add findings for a comprehensive rejection. Extraction discrepancies must be corrected first. This qualifies AC8-10 for stage forwarding, not geometry-validity approval: every included parcel needs a current examination disposition; defective geometry remains unapproved for consumers requiring valid geometry. No new default supervisor approval or separate correction route is required.

13. Given a finding, when classified and dispositioned, then distinguish extraction mismatch, source-plan defect, mixed and unknown origin using source/extracted evidence. A correct source with incorrect extraction requires correction/recheck; unknown origin cannot be labeled source-defective to bypass it. For mixed errors, correct the extraction component and retain the source defect. The same authorized examiner records source defects with stable finding ID, case/parcel/document/page/zone, rule/input revision, detection stage, actor/time and nonblank explanation/evidence. Preserve failed/unknown outcomes.
14. Given recorded source defects and completed applicable examination, when local stage-forwarding readiness is evaluated, then every included parcel has an explicit current examination disposition and the intended route remains the normal next stage. Return forwarded-with-recorded-defects separately from valid-geometry acceptance; geometry consumers cannot treat it as a pass. Changed inputs/policy or invalid authorization stale dependent dispositions; retain history. Wrong-case identity, invalid references, absent authorization, source-write protections and integrity remain blockers; no artifacts, geometry or remote permission are fabricated.
15. Given multi-stage findings, when the next stage appends discoveries or records resolution, then preserve previous findings, detecting stage/actor, evidence, relationships and status history in one cumulative case register. Do not clear Compute findings on entry to Compare or replace the case register with current-stage results. Complete applicable whole-document/stage review rather than stop at the first source defect; evaluated/not-applicable/not-evaluable checks retain reasons/evidence and pending checks cannot masquerade as passed. Handoff/rejection projections expose the cumulative findings and review limitations. A implements the in-memory contract/evaluation; persistence/report/package/actual normal transition remain 13.5/13.9.
16. Given stage-forwarding tests, when run, then cover correct-source/bad-extraction blocking, recorded bad-source forwarding, mixed-error correction, unknown origin, unauthorized/blank-evidence disposition, stale input, three-parcel completeness, incomplete review, source-defect-related not-evaluable checks, retained Compute plus new Compare findings, and defect forwarding without valid-geometry approval. Preserve all non-waivable identity/integrity assertions.

### Human Validation And Compare Data Scope (C12-C14)

17. Given extracted or manually captured evidence, when reviewed, then retain raw/extracted and reviewed values, capture method, source scope, reviewer/time and input revision, with separate presence and accuracy states. A human may confirm missing-from-source with explanation/evidence; not-extracted, uncertain and not-reviewed cannot be silently converted to source absence. Missing-from-source is a valid disposition, not automatically a pass, rejection or universal blocker; applicable check policy determines its effect. Existing geometry human-review gates remain separate.
18. Given current-case tabular evidence in Compare, when the authorized examiner manually captures/corrects values, then update the local reviewed evidence with audit history without returning to Compute, modifying geometry, editing linked PE sources or implicitly writing LTD/Innola records. Changed values stale dependent comparisons/findings/decisions, not unrelated geometry checks. Use the existing canonical review fields/types where delivered; A supplies in-memory model/evaluation only, not a new UI/edit executor.
19. Given partial/unavailable LTD data, when available items are reviewed, then retain those outcomes and evidence while marking required outstanding comparisons pending with scoped reasons. Separate source absence, LTD missing record, incomplete evidence and retrieval failure. Aggregate Compare stays pending-with-recorded-reason instead of final approved/rejected; restored availability permits reevaluation without losing earlier work. Tests cover human presence/accuracy, retained manual corrections, Compare tabular versus geometry boundaries, missing-from-source and partial LTD completion. A models the state; actual Compare integration and server outcomes remain 13.8/13.9.

## Tasks / Subtasks

- [ ] Record accepted 11.2 ownership/reuse map and local identity policy; verify delivered canonical types in isolation before coding. (AC: 1-3)
- [ ] Add failing single-parcel and three-parcel adapter/identity/membership fixtures plus invalid-key/reference cases. (AC: 2-6, 12)
- [ ] Implement immutable domain records and read-only adapter with explicit carried-forward identity map. (AC: 2-5)
- [ ] Implement multi-selection resolution and global/many-parcel evidence links with identity/scope validation. (AC: 4-6)
- [ ] Implement auditable inclusion dispositions and preserve all detected candidates. (AC: 7)
- [ ] Implement fingerprint-bound parcel acceptance/history and policy-sensitive aggregate evaluation. (AC: 8-10)
- [ ] Model error origin, source-defect stage dispositions and cumulative findings/review coverage; test normal-stage readiness separately from geometry approval. (AC: 13-16)
- [ ] Model human presence/accuracy review, manual tabular provenance, Compare-local correction scope and partial-data pending outcomes; add focused tests. (AC: 17-19)
- [ ] Verify read-only boundaries, legacy compatibility and regression evidence; document interface handoff to B/13.5. (AC: 1, 11-12)

## Dev Notes

### Contract Ownership And Source Anchors

Paths relative to `src/ParcelWorkflowAddIn/`.

| Owner / File | Current Behavior | Use And Preserve |
|---|---|---|
| 11.2: `ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs` | Current rows/segments carry group/source IDs and review values; metadata/parties/memorandum global; canonical first-class group/trace contract still planned | Consume verified 11.2 types; preserve source order, raw evidence and reviewed fields. Do not duplicate canonical models |
| 11.2: `ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs` | JSON normalization/save and hash approval with unknown metadata preservation | Read through its canonical contract; leave current writer, hashes and approved_review.json unchanged |
| 13.3: `ParcelWorkflowAddIn/Workflow/Toolbox/ToolboxCaseContext.cs` (isolated only) | Immutable context, selected IDs, unverified observed task, GeometryAccess and remote deny | Consume identity/selection/access directly; a separate resolver validates parcel membership |
| `ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs` | Mutates parcel sequence during insertion; generated manual IDs and point labels | Consume results as evidence; never invoke mutating normalization during adaptation |
| `ParcelWorkflowAddIn.Tests/Program.cs` | Manual test registration and name filters | Only planned existing UPDATE: add focused test registrations, preserve all prior entries/filter behavior |

NEW production records/services belong under `ParcelWorkflowAddIn/Workflow/Toolbox/`; tests under `ParcelWorkflowAddIn.Tests/Workflow/`, for example `ToolboxParcelMembershipTests.cs` and `ToolboxParcelAcceptanceTests.cs`. Keep small services consistent with 13.3, without a generic graph/plugin framework.

No production UPDATE is assumed. If canonical model changes are required, revisit D11 before editing them. Re-read complete UPDATE files and relevant tests immediately before implementation; this preparation is not approval for broad review/persistence refactoring.

### Proposed Domain Contract

- Case identity: environment, case ID, TransactionId and original TR; not filename/display text alone.
- Identity map: case-scoped canonical group/row/segment key to domain parcel/point/segment ID, with source provenance. A carries maps in memory; B reconciles changed candidates and 13.5 saves maps.
- Parcel: stable ID, canonical group key, label, inclusion state and disposition reference.
- Point/segment: stable domain ID, source key and immutable evidence projection; geometry is referenced, not rewritten.
- Membership: stable entity references with explicit order/role/direction. Duplicate keys fail; do not collapse valid ordered ring closure.
- Evidence/party link: canonical evidence/role reference and parcel ID; global facts have no forced parcel link.
- Requirement/result: scope, identity/version, input fingerprint, applicability, effect and explicit disposition authority/evidence.
- Acceptance: case/parcel fingerprint, policy/rule versions, actor/time and historical state.
- Aggregate: included-set fingerprint, accepted parcel fingerprints, required case results, derived counts and blockers. No server authorization field that can become true in A.

Defensively copy mutable source JSON/lists or expose immutable owned projections; read-only collection wrappers alone do not make mutable elements immutable. Failed adaptation, inclusion or acceptance must leave previous state intact.

When adapting an existing accepted/model state, a missing or incomplete prior identity map is a blocker, not permission to allocate replacements. Allocate new IDs only for an explicitly new, unambiguous import without prior identity/acceptance claims. Proposed ordered membership uses positive sequence positions and an explicit validated forward/reverse direction; never infer order from list position when canonical sequence evidence is ambiguous.

Fingerprint canonicalization must be documented and tested: stable IDs, ordered membership, relevant reviewed values and provenance, declared geometry/evidence tokens including CRS/units, dependency and rule/policy versions. Exclude ObjectIDs, display timestamps and label-only fields unless consumed. Collection enumeration order cannot change a set fingerprint; geometry membership order must remain significant. Unknown geometry-policy tokens or absent required inputs block readiness; A does not invent curve/Z/M/precision rules.

### A To B And 13.5 Handoff

Exception approvals and original findings are part of A's handoff contract. B invalidation must stale affected exceptions too; 13.5 stores their audit records and 13.9 includes them in reports/package and revalidates handoff authority. No mandatory geometry result or downstream artifact is silently fabricated to satisfy an exception.

C11 supersedes generic waiver semantics: pass cumulative findings and separate stage-forwarding/geometry-validation states. 13.8 appends Compare findings without deleting earlier defects; 13.9 targets the normal next transition with fresh task authority, not an invented correction route. Snapshot/projection consumers must never select defective geometry merely because stage forwarding is ready.

B receives the identity map, ordered memberships, retained candidates/manual evidence, inclusion dispositions, acceptance input dependencies and immutable history. B adds boundary/lineage relationships and propagates shared-edge impact using old/new dependency sets. A must not delete excluded entities or assert a default shared-consistency pass.

13.5 receives model/version outlines and fixtures for later persistence/reopen. Do not implement temporary sidecar identity-map writers now: one existing JSON owner remains until explicit cutover.

### Test Matrix

| Fixture | Assertion |
|---|---|
| Single PXA-style parcel; dimension-based and coordinate-based three-parcel Cad Maps | No first-parcel truncation; manual/raw/reviewed data and global facts retained |
| Rename/reorder with supplied map; duplicate labels/point labels | IDs stable; no label/position/coordinate merge |
| Missing map on known accepted state; incompatible/cross-case map | Explicit blocker; no ID reallocation or acceptance transfer |
| Blank/duplicate canonical IDs; orphan/bad order/direction/link | Typed failure; no partial model or source mutation |
| Ordered repeated closing vertex | Supported explicit membership, no accidental duplicate removal |
| Caller mutates source JSON/lists after adaptation | Domain snapshot unchanged |
| One fact linked to two parcels; case-wide metadata | One owned value, correct links and scopes |
| Empty/multiple/foreign selections | Scope resolution only; inclusion/acceptance unchanged |
| Unresolved third candidate; invalid exclusion; empty included set; re-inclusion | Aggregate blocked; history retained, prior acceptance not revived |
| Report-only failure versus blocking/review-required effect | Correct policy gating, authorized disposition only |
| Missing/stale checks or required shared-consistency unknown | Acceptance blocked; no default pass |
| Membership/evidence/rule changes; label-only change; reordered included-ID set | Correct semantic invalidation and deterministic fingerprints |
| Compare and linked-source inspection; permitted Compute eligibility | No geometry/task mutation, remote denied |

After implementation, run from the isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner"
dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj --no-build
```

Also run canonical/legacy review-persistence and approval tests using actual harness registration names. 13.3 has a recorded clean build/22 focused passes; the full harness has a reproduced baseline Compare XAML failure. Reconfirm results, do not claim tests after a fail-fast failure passed. Manual ArcGIS/FGDB/live TR testing is not part of this pure-model slice.

### References

- [Isolated review-policy settings mapping](../../planning-artifacts/toolbox/toolbox-review-policy-settings-design.md): proposed strict policy contract and case-pinning/effect separation; 42-item JSON seed is not activatable. Pure model work must consume explicit versioned inputs, not read or save legacy settings.

- [Approved R/O/N/A stage classification v1](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md#proposed-stage-classification): JotaPe approved all 42 items and documented conditions on 2026-10-08 (C15); required review is separate from mandatory value presence or valid-geometry approval. Detailed preset/effect mappings and runtime settings integration remain pending.

- [Source-based Compute / Compare checklist matrix](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md): 42 item groups, existing fields/rules, stage coverage and reuse gaps. Classification v1 is approved; runtime policy configuration is not yet implemented.

- [Parent 13.4 and full scope](13-4-add-single-and-multi-parcel-toolbox-model.md)
- [13.3 foundation and review evidence](13-3-add-toolbox-case-context-and-workspace-selection.md)
- [11.2 canonical contract dependency](../11-2-unified-pdf-extraction-contract-for-plan-examination.md)
- [Accepted process](../../planning-artifacts/toolbox/toolbox-process-definition.md)
- [Architecture](../../planning-artifacts/toolbox/toolbox-architecture.md)
- [Contract catalog](../../planning-artifacts/toolbox/toolbox-contract-catalog.md)
- [Migration and slicing](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md)
- [Acceptance strategy](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md)
- [D07/D11 decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md)
- [Epic 13](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md)
- [Deferred baseline issues](deferred-work.md)
- [Project context](../../project-context.md)

## Dev Agent Record

### Agent Model Used

Codex; BMAD create-story preparation. Implementation not started.

### Debug Log References

Prepared from parent 13.4, accepted Toolbox process/architecture/contract/migration, 11.2 dependency, current review/manual-point model and isolated 13.3 context. Uses existing net8.0-windows/x64 and executable harness; no new library/API behavior or version upgrade.

### Completion Notes List

- JotaPe requested separate 13.4A preparation on 2026-10-08.
- Local memberships/inclusion/acceptance isolated from B reconciliation/lineage/shared-edge work.
- Readiness remains backlog pending contract reuse and local identity-policy gates; no 11.2 retirement or policy approval inferred.
- No application code, runtime tests, case data, installation or remote service changes.
- C10 user clarification adds AC13-15 for authorized justified handoff exceptions; clean versus accepted-with-exceptions preserves all-included acceptance without hiding failed findings. D12 eligibility/roles remain explicit gates.
- C11 supersedes the generic exception description: AC13-16 separate fixable extraction discrepancies from recorded source defects, retain cumulative findings through normal stages and distinguish examination forwarding from valid geometry. Documentation only; implementation still not started.
- C12-C14 add AC17-19: valid human-confirmed source absence, human value validation, tabular corrections in Compare and partial LTD review retained under pending status. No application code or runtime verification changed.
- Independent preparation validation found no concrete scope/acceptance contradictions. All story links resolve, 12 ACs are contiguous, tasks remain unchecked and five preparation documents match across checkouts. Operational source/tracker/context diff remains unchanged.

### File List

- `_bmad-output/implementation-artifacts/toolbox/13-4a-add-stable-parcel-memberships-and-acceptance.md`
- `_bmad-output/implementation-artifacts/toolbox/13-4-add-single-and-multi-parcel-toolbox-model.md` (child link/preparation note)
- `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml` (parent comment only)
- `_bmad-output/planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md` (child preparation link)
- `_bmad-output/planning-artifacts/toolbox/toolbox-parallel-migration-plan.md` (prepared A clarification)

Preparation documents mirrored into the isolated worktree. Implementation file list and verification evidence pending.
