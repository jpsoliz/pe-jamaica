---
story_key: 13-4b-add-rerun-reconciliation-shared-boundaries-and-lineage
parent_story: 13-4-add-single-and-multi-parcel-toolbox-model
track: toolbox
story_type: code
created: 2026-10-08
depends_on: [13-4a-add-stable-parcel-memberships-and-acceptance]
execution_gate: accepted-13-4a-and-d07-reconciliation-policy
baseline_branch: wipRC9
baseline_commit: 5908f30617cdc49e09873635a30343da8aff9ad6
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
tracking_parent_key: 13-4-add-single-and-multi-parcel-toolbox-model
---

# 13.4B - Add Rerun Reconciliation, Shared Boundaries And Lineage

Status: review

Current implementation (2026-10-09): isolated in-memory slice implemented; clean build and 125 focused passes. Preparation/readiness text below is historical and superseded by this status and the Dev Agent Record. Full-suite, GIS/FGDB and production acceptance are not claimed.
Preparation: detailed isolated specification prepared; implementation not started.
Readiness: D07 P01-P05 approved as `toolbox_d07_reconciliation_v1` and 13.4A scoped signoff complete on 2026-10-08. Design/dependency gates resolved for this in-memory slice; awaiting explicit B implementation authorization. No B code, persistence or deployment started.

## Story

As an examiner,
I want to reconcile rerun candidates, review shared boundaries and record parcel split/merge ancestry,
so that single-parcel and variable-size Cad Map cases retain manual corrections and cumulative findings while affected checks become stale reliably.

## Scope And Entry Gates

Second slice of parent 13.4, migration M2. Implement pure, immutable proposal/validation/impact services over the isolated canonical contract and 13.4A model. A validated, explicitly accepted proposal may produce a new in-memory snapshot; it is not an FGDB commit or server transition. Existing extraction JSON remains the persistence owner.

Work only in `temp/toolbox-worktree`, branch `refactor/toolbox-case-workspace`. Do not mirror implementation into the operational checkout. Track through the existing parent key; do not add a second sprint entry automatically.

| Gate | Evidence / action required |
|---|---|
| 13.4A signoff | Complete for isolated in-memory contract; three additional corrections plus evidence-alias follow-up verified, clean build/105 focused passes; full-suite limits retained |
| Canonical ownership | Reuse the opt-in 11.2 amendment, not a second extraction schema; production cutover and original 11.2 remain open |
| D07 reconciliation policy | Approved P01-P05, `toolbox_d07_reconciliation_v1`, product approver JotaPe, 2026-10-08; actual GIS geometry-policy/host verification remains separate |
| Execution authorization | Explicit authorization to implement B in isolation; this request prepares documentation only |
| Production geometry policy | Real CRS/units/precision/curve/Z/M and provider normalization remain separate integration gates; synthetic proof is not GIS certification |

### Approved D07 Rules v1

JotaPe approved P01-P05 on 2026-10-08, version `toolbox_d07_reconciliation_v1` (decision C16). Approval covers the isolated in-memory contract, not automatic GIS detection/repair, real geometry tolerances or production certification.

| ID | Approved rule | Safety boundary |
|---|---|---|
| P01 Identity | Propose one-to-one reuse only for carried-forward case/kind/source keys with unchanged source identity, or an explicit evidence-backed examiner mapping | Keys alone do not authorize overwriting values; changed source hash requires renewed review. Names, array positions, ObjectIDs, coordinates or confidence alone never establish identity |
| P02 Manual evidence | Preserve current manual/reviewed values by default; present new extraction as a separate suggestion. Replacement requires field/entity-level examiner choice, evidence and reason | Conflicts remain unresolved, never last-write-wins. Removed/unmatched manual entities remain retained for disposition |
| P03 Shared boundaries | Model explicitly reviewed physical boundary IDs and directed parcel memberships, retaining independent segment evidence | No automatic snapping, reverse-bearing repair, coordinate-only deduplication or assumed topology consistency |
| P04 Split/merge | Split 1-to-many and merge many-to-1 allocate new result parcel IDs, retire parent IDs into history and require explicit lineage and inclusion dispositions | No parent ID reused as a child, no inherited approvals. Many-to-many reconfiguration needs separate review, not a guessed split or merge |
| P05 Impact | Use the union of old/new dependency graphs and follow consuming checks/decisions; retain records with explicit stale reasons | Excluded and retired neighbors remain dependencies. Unknown dependency coverage blocks affected acceptance; unrelated results stay current only when their declared inputs still match |

No proximity algorithm or tolerance threshold is selected here. An automatic matching/detection adapter requires a separately accepted geometry policy and an established GIS library/adapter; do not hand-roll a topology engine in this story.

### Out Of Scope

FGDB/schema creation, save/reopen/recovery, durable journals, JSON writer/hash changes, extraction/solver rewrites, real geometry repair/detection, Parcel Fabric, UI/DAML/routes/settings publication, tool execution, output/package creation, Enterprise/Innola calls, installation and operational story retirement. 13.5 owns durable revisions and retained-history storage; 13.8/13.9 own actual Compare/handoff integration.

## Acceptance Criteria

1. Given the entry gates, development remains isolated and uses synthetic fixtures. Existing 13.3 context, canonical ownership, policy loader/resolver, bindings and four 13.4A regression fixes are preserved; no operational source/configuration/data or remote behavior changes.
2. Given a rerun batch, its envelope binds complete case identity, batch/run ID, tool/provider version, source IDs/hashes, expected base revision AND logical base fingerprint, candidate digest and policy versions. Reject wrong-case, stale-base, malformed/partial/canceled/failed batches before producing an applicable delta. Preserve current model, raw candidates, manual work and findings on failure; empty successful extraction is evidence for review, not automatic deletion.
3. Given renamed/reordered unchanged-key candidates, propose stable parcel/row/segment ID reuse through the carried map. Given regenerated keys or changed source identity, require explicit provenance-backed mapping. Return matched/new/missing/ambiguous/conflicting dispositions for every old and incoming candidate. Duplicate or many-to-one matches block unless represented by an explicit approved lineage operation; no index/name/coordinate-only rematching.
4. Given a match, preserve original extraction, reviewed/manual value, capture method, source trace and human history separately from the new suggestion. An authorized examiner may retain current values or explicitly accept replacement with reason/evidence. Unresolved value conflicts block application; retained manual evidence is not automatically revalidated against a changed source. Missing candidates do not erase current entities.
5. Given a fully dispositioned proposal, local application revalidates case, current base revision/digest, proposal digest, policy, authority and geometry eligibility. It produces a new immutable snapshot and complete carried-forward map atomically in memory, or no new snapshot. New identities start with explicit unresolved inclusion until disposition; selection cannot hide them. Keep A's ordinary same-key `ReAdapt` guard intact; use a separate explicit reconciliation path for changed inventories.
6. Given a physical boundary, retain a stable case-scoped boundary ID, original segment/evidence links, geometry/evidence revision and memberships containing parcel ID, sequence, direction and role. Validate nonblank unique IDs, known same-case entities, valid order/direction and permitted membership cardinality. Distinguish source segment identity from physical boundary identity; multiple parcel segments can reference one reviewed boundary without erasing either source.
7. Given contradictory shared geometry/evidence, unsupported geometry semantics or incomplete adjacency evidence, return typed scoped conflicts and block affected acceptance. An examiner records a resolution with supporting evidence or keeps it unresolved; source-defect forwarding cannot waive referential identity or unknown consistency. Single-parcel or nonadjacent cases require explicit applicable/N/A evidence under the resolver, not an invented shared-edge pass. More than two memberships require an explicit supported role/policy or remain conflicted.
8. Given changed, removed or reassigned boundaries, points, memberships or links, impact uses old/new dependency union and follows dependent parcel rings, checks, dispositions, Compare decisions and case aggregates. Include excluded/retired neighbors. Return affected entity/check/decision/artifact IDs with dependency paths and reasons; missing dependency registration is explicit unknown coverage, never no impact. Do not execute exports, projection or packaging.
9. Given a parcel-only change, stale all checks consuming that input and affected parcel/case decisions; retain unrelated results with identical dependencies. A participant/title edit in Compare stales consuming tabular comparisons, not geometry-only checks. A case-wide fact or policy/verification revocation invalidates all consumers. Shared-boundary dependency fingerprints must participate in readiness, not only an informational impact report. Preserved old checks cannot accidentally approve changed geometry even when caller revision/input strings are reused.
10. Given an explicit split or merge, validate operation ID, parent/child identities, case, revision ancestry, source/evidence, actor/reason, complete membership mapping and inclusion decisions. Reject cycles, duplicate parents/children, self-links, foreign/missing references, accidental active-parent reuse and unsupported many-to-many operations. Allocate fresh result parcel IDs; unchanged entities may retain identity only through explicit valid mappings. Retire parents without deleting them and invalidate affected checks/decisions; children require fresh examination, never transferred approval.
11. Given split/merge or removal, preserve every manual correction and original finding ID, detecting stage/actor/evidence/classification and append-only history on the historical parent. Add auditable applicability links to result parcels, not reassignment of original scope. Each correction/finding needs explicit retained/applicable/not-applicable/unresolved mapping; unresolved applicability blocks relevant readiness. Do not copy an owner/manual value to every child, assume it remains valid after merge, or silently resolve a finding because its parcel disappeared.
12. Given finding resolution or source-defect forwarding, preserve A's current applicable passing recheck requirement, authority/policy/input binding and separate forwarding/valid-geometry states. Changed dependencies stale prior resolution/forwarding proof while retaining events; rerun itself never resolves or reopens a finding by rewriting history. Compute and Compare findings remain cumulative; corrected tabular information may be rechecked in Compare. Required incomplete LTD comparisons remain pending with reason and available outcomes preserved.
13. Given geometry-changing matching/boundary/lineage proposals, enforce Compute plus explicit local geometry permission and deny Compare/linked-source mutation. Read-only inspection and impact planning remain available. Compare tabular correction remains permitted by A; it is not a geometry reconciliation command. Local success never grants Innola authority or changes the actual task.
14. Given repeated proposal acceptance in the same in-memory history, the same operation ID and digest returns the prior result/no-op without allocating IDs or duplicating lineage/findings; the same ID with different payload blocks. Concurrent/stale proposals reject without changing the winner. Durable retry/restart guarantees are deferred to 13.5, not claimed by this test.
15. Given all included parcels, new/unresolved candidates, shared conflicts, lineage applicability or stale required checks prevent aggregate readiness. Explicit exclusions retain history and dependency impact; no selected subset bypasses the initial all-included gate. Every included parcel requires current examination disposition, while source-defect forwarding remains distinct from geometry approval.
16. Given verification, the executable harness covers the matrix below and all A regression groups, existing policy/binding/context tests and legacy persistence/approval. Report build/focused/full-suite results separately with exact commands/counts and baseline failures. Do not claim FGDB/ArcGIS/live-TR or full-suite acceptance from pure fixtures.

## Tasks / Subtasks

- [x] Record A signoff, approved P01-P05/version and isolated execution authorization. (AC1)
- [x] Add failing matching/provenance/conflict and stale-base fixtures; include variable parcel counts and manual recovery. (AC2-5)
- [x] Implement immutable batch/proposal/disposition/result contracts and explicit changed-inventory mapping path. (AC2-5, 14)
- [x] Implement physical boundary memberships/conflicts and authoritative dependency inventory; validate old/new graph references. (AC6-8)
- [x] Implement deterministic impact traversal and bind boundary/dependency digests into existing scoped readiness checks/decisions. (AC8-9, 15)
- [x] Implement split/merge proposals, historical parent registry and correction/finding applicability links without weakening original scope/history guards. (AC10-12)
- [x] Enforce Compute geometry eligibility, linked-source protection and same-operation replay rules. (AC13-14)
- [x] Run focused/legacy/full verification, inspect isolation diff and record evidence plus remaining host/production gates. (AC1, 16)

## Dev Notes

### Reuse And UPDATE Map

Paths below are relative to `src/ParcelWorkflowAddIn/`. Re-read every full UPDATE file and tests before implementation; preparation inspected relevant contracts and harness registration/filtering, not every unrelated legacy test body.

| File / boundary | Current behavior | B change / preserve |
|---|---|---|
| `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelModel.cs` | Immutable source projection; complete same-key identity map; ordered memberships; inclusions; parcel fingerprints. Boundary scope currently resolves source segment IDs | Narrow UPDATE for explicit reconciliation construction and reviewed physical-boundary scope/dependency projection. Preserve default Adapt/ReAdapt rejection, stable mappings, selection and case/read-only guards |
| `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxExamination.cs` | Canonical/evidence/geometry fingerprints, complete scope inventory, current rechecks, cumulative register. `RecordFinding` forbids changing original parcel scope and requires currently known references | Narrow UPDATE for readiness consuming B dependency state and archived-parent applicability. Retain historical model/source registry to resolve retired references; no dropping findings or relaxing foreign-reference validation. Version changed fingerprint contracts explicitly |
| `ParcelWorkflowAddIn/Workflow/Review/Canonical/CanonicalReviewContract.cs` | Defensive, lossless opt-in canonical snapshot; strict optional fact link validation | REUSE, not planned UPDATE. Provider normalization and persisted lineage schema are not silently added here; explain any necessary ownership amendment before editing |
| `ParcelWorkflowAddIn/Workflow/Toolbox/Policy/*` | Strict isolated-test policy pin, bounded applicability, complete scoped resolution | REUSE. Boundary scope migration needs an explicit segment-to-physical-boundary bridge, retaining old source scopes; do not silently reinterpret existing scope IDs or activate settings |
| `ParcelWorkflowAddIn.Tests/Program.cs` | Explicit registrations; OR substring filters; fail-fast execution | UPDATE registrations only, keep all existing tests and filter behavior |
| `ParcelWorkflowAddIn.Tests/Workflow/ToolboxParcelModelTests.cs` | A semantic invalidation, source defects, human review, variable bindings and four regression groups | REUSE unchanged tests; add B-specific class rather than rewriting assertions to fit weaker behavior |
| `ParcelWorkflowAddIn.Tests/Fixtures/ToolboxBindings/*` | Single parcel, three-parcel example and manual recovery; unresolved adapters and incomplete bundles | REUSE as seeds. Add B synthetic relationship/proposal fixtures without turning unmapped checks into passes |

Suggested NEW files under `Workflow/Toolbox/Model/`: `ToolboxReconciliation.cs`, `ToolboxBoundaryDependencies.cs`, `ToolboxParcelLineage.cs`; focused `Workflow/ToolboxReconciliationTests.cs` in the existing test project. Prefer small records/services over a generic graph/rules framework. Names are implementation suggestions, not delivered APIs.

### Proposed In-Memory Contracts

- Candidate envelope: case, batch/run/tool/provider identity, source hashes, expected base revision/digest, input/candidate digest, policy version and owned canonical snapshot.
- Match/disposition: old stable entity/kind, incoming source key, match evidence, state, retained/replacement field decisions, reviewer/reason; exhaustive inventories, not selected parcel lists.
- Reconciliation proposal/result: operation/digest, expected parent, explicit mappings/new IDs, immutable historical/current snapshots, accepted choices and impact. Allocate once on accepted result through the existing injectable ID pattern; proposing is non-mutating.
- Boundary/membership: physical boundary ID/revision, original segment/evidence references, parcel/sequence/direction/role and reviewed consistency state; geometry semantics supplied by a verified versioned adapter/policy.
- Dependency edge: case, producer identity/input kind, consumer kind/ID, consumed fingerprint/version. Require complete authoritative inventories for old/new snapshots; unknown coverage blocks acceptance. Use directed consuming edges, not indiscriminate adjacency flooding.
- Lineage: operation/type, parent/result parcel IDs and revision links, evidence/reviewer/reason, retired-parent state and explicit inclusion; lineage is acyclic. Historical parents are not current included candidates.
- Applicability link: finding/correction identity, original snapshot/scope, result scope, disposition/evidence/reviewer and consumed revision. This companion relation must not rewrite original finding `ParcelIds` or insert an invented finding-event status.
- Impact: deterministically sorted changed/affected IDs, stale check/decision/artifact references, old/new dependency paths and unknown/conflict reasons. Consuming artifacts without registered IDs are unknown future integration, not falsely enumerated.

Hash ordered memberships and consumed geometry/evidence/policy semantics; sort unordered ID sets consistently with the existing semantic hash. Labels, ObjectIDs and timestamps alone are not changes. A whole-case base digest protects application from concurrent changes; it must not blanket-stale every unrelated check solely because a case revision advanced. Maintain scope-specific dependency versions and revalidate unchanged results explicitly.

Independent rings remain evidence/materialized geometry, not proof of topology. Conflict review does not mutate either ring. Any subsequent geometry correction is a separate Compute proposal affecting every registered boundary consumer. Unknown geometry policy never yields approval.

### Lessons From 13.4A

Keep canonical-input fingerprints even when caller tokens are unchanged; keep geometry-policy verification in parcel currency; resolution needs current linked passing rechecks rather than status text; malformed participant/title links reject before scope traversal. Unknown provider extensions remain preserved but unbound. New shared-dependency consumers must not bypass these four fixes.

Recorded A evidence at signoff: clean build, zero warnings/errors and 105 focused tests outside the sandbox. Full retries stop at the intermittent `ManifestSerializer.Write` access failure or known Compare XAML progress-indicator assertion, both documented. B preparation does not claim a passing full suite. See A evidence and deferred work; preserve canonical/evidence/readiness v2 hashing and consistent evidence identity matching.

Current isolated code is uncommitted; last commits are operational release/patch history (HEAD `5908f30`), not B delivery. No new dependency, GIS API or toolchain upgrade is specified; no external latest-version research is needed for this pure local contract. Keep net8.0-windows/x64 and existing ArcGIS SDK lane.

### Regression Matrix

| Scenario | Expected evidence |
|---|---|
| Single parcel; Cad Maps with 2, 3, 5, 10, 50, 100 parcels | Complete dispositions; counts are fixtures, no product cap or performance certification |
| Rename/reorder; regenerated IDs; duplicate names; same coordinates | Stable proven matches; ambiguous identity blocked, no implicit merge |
| Matched key but changed source hash/page | Values preserved as prior review, dependent proof stale; explicit new-source review |
| Manual COGO recovery followed by successful/empty/failed/canceled rerun | Original/manual values and findings retained; no overwrite/deletion; manual route still available |
| Stale base; wrong case; duplicate operation; changed replay payload | Atomic rejection or same-result replay; no duplicate IDs/events |
| Shared edge A-B, independent C | A/B consuming checks stale, C current if independent; case aggregate stale |
| Removed/reassigned boundary A-B to B-C; excluded A | Union A/B/C invalidated; exclusion cannot hide former dependency |
| Shared vertex/geometry policy/source-evidence change | Every declared consumer stale, unknown coverage blocked |
| Conflicting directions/CRS/units, orphan/cross-case memberships, >2 unsupported members | Typed conflict; no repair, guessed consistency or false acceptance |
| Explicit no-shared-boundary evidence; missing adjacency inventory | Policy-specific N/A supported; missing knowledge stays unresolved |
| One parent split into several children; several parents merged | New parcel IDs; retained parents/manual/evidence/findings, no approval inheritance |
| Cycle/self-link/many-to-many; missing correction/finding applicability | Block; nothing erased or silently duplicated among children |
| Resolved/forwarded geometry finding then boundary change | History retained, prior proof stale; fresh recheck/disposition required |
| Compare owner/title correction and LTD outage | Only consuming tabular results stale; geometry unchanged; reviewed available data retained under pending reason |
| Compute permission vs Compare/linked-source proposal | Explicit local geometry authorization required; read-only inspection allowed, all remote denied |
| Third/new candidate unresolved; selected accepted subset | All-included aggregate blocked; lineage retirement never substitutes for justified exclusion |

### Verification After Implementation

Run from the isolated worktree; use a fresh intermediate path to avoid previously observed stale WPF artifacts:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-13-4b/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

Record actual registered test count and failure locations; fail-fast means later tests did not execute. Reconfirm operational `git diff --stat`, isolated whitespace check and absence of deployment/service effects. Host edit detection, FGDB round-trip and durable recovery are later acceptance layers, not B fixture claims.

### References

- [Parent 13.4](13-4-add-single-and-multi-parcel-toolbox-model.md)
- [13.4A contract](13-4a-add-stable-parcel-memberships-and-acceptance.md) and [evidence/four fixes](13-4a-implementation-evidence.md)
- [11.2 ownership amendment](11-2-toolbox-contract-amendment.md)
- [Confirmed process C11-C14](../../planning-artifacts/toolbox/toolbox-process-definition.md)
- [Architecture A04 and parcel/evidence model](../../planning-artifacts/toolbox/toolbox-architecture.md)
- [Candidate, lineage and human-review contract outlines](../../planning-artifacts/toolbox/toolbox-contract-catalog.md)
- [D07 and confirmed decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md)
- [Migration M2 and A/B slicing](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md)
- [S03/S04/S10/S13/S14 acceptance scenarios](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md)
- [Epic 13](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md)
- [Deferred baseline limitations](deferred-work.md)
- [Project rules](../../project-context.md)

## Dev Agent Record

### Agent Model Used

Codex; isolated dev-story implementation. Review is not product/production signoff.

### Debug Log References

2026-10-09: final build zero warnings/errors; 125 focused passes (105 preserved plus 20 B groups). Final full retry passed 146 tests, then stopped at `ManifestSerializer.Write:28`, reached from `CompareWorkspaceViewModelTests.SaveAndRestoreDraftPreservesNotesAndEvidence:94`; later tests did not execute. Earlier retry stopped at the same access failure from `CaseFolderStoreTests.ReopenCaseFolderReportsUnknownWorkflowState:216`. Existing Compare XAML limitation remains documented, not exercised to completion. See [implementation evidence](13-4b-implementation-evidence.md).

Red evidence: explicit reconciliation API test failed before implementation; contradictory-success/failed-candidate regression failed before its guard. Other negative/regression groups now pass. Intermediate corrections addressed hash serialization, boundary consumer coverage, source-bound human validation, actual finding dependencies, explicit adjacency/removal and manual-entity applicability. Failed sandbox harness processes closed before rebuilding; intermediate file-lock build failure was environmental. No claim every group individually ran red.

Local self-review only, not independent multi-agent/GIS certification. Canonical owner, policy loader/resolver, original binding bundles and A test bodies reused unchanged. No new package, solver/extraction rewrite, persistence owner or remote executor.

2026-10-08: JotaPe explicitly authorized isolated implementation. A signoff and D07 `toolbox_d07_reconciliation_v1` confirmed; operational checkout remains protected. Execution supersedes the documentation-only preparation notes above.

Prepared 2026-10-08 against isolated A contracts/evidence, parent story, accepted process/architecture, D07, migration/ownership and fixture strategy. No application edits or runtime tests performed. No formal multi-agent code-review completion is claimed.

### Completion Notes List

- Current delivery supersedes historical preparation notes: exhaustive rerun dispositions; retained manual values/raw suggestions; explicit changed-inventory path; physical boundaries and point/segment dependencies; old/new union impact; fresh split/merge IDs with retained parents and append-only histories/applicability; Compute permission and in-memory replay.
- Actual inputs bind readiness, human source validation and finding dispositions; refreshed caller/check strings cannot renew an old proof. Source-defect forwarding remains normal-next-stage and separate from valid geometry. A's ordinary guards and v2 fingerprint projections preserved.
- Parent 13.4 remains in-progress pending B acceptance; no child sprint key. Verification task completion means commands executed and baseline limits recorded, not a full-suite pass. No deployment, FGDB, case package/export, projection or Innola transition.

- Prepared at JotaPe's explicit request; P01-P05 approved as `toolbox_d07_reconciliation_v1` and A scoped signoff subsequently completed, 2026-10-08. Awaiting explicit B execution authorization; implementation has not started.
- Status intentionally backlog: named signoff/domain/execution gates override a generic ready-for-dev template. No new child sprint key or operational tracker changes.
- Scope includes exhaustive rerun dispositions, manual preservation, physical-boundary/source-segment distinction, old/new dependency union and immutable lineage/applicability history.
- 13.4A and its isolated amendment are accepted after authorized corrections and final patch review; B preparation itself did not supply that acceptance. Parent 13.4/original 11.2 remain open.
- Preparation validation: 16 contiguous criteria, eight unchecked tasks and 13 valid local reference links; isolated tracked whitespace check passed and operational tracked diff is empty. No build or runtime tests were run for this documentation-only change.

### File List

Current B delivery (repository-relative; historical preparation list below retained):

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxReconciliation.cs` (new).
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxBoundaryDependencies.cs` (new).
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelLineage.cs` (new).
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelModel.cs` (update).
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxExamination.cs` (update).
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxReconciliationTests.cs` (new).
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs` (registrations only).
- `_bmad-output/implementation-artifacts/toolbox/13-4b-add-rerun-reconciliation-shared-boundaries-and-lineage.md` (status/tasks/record).
- `_bmad-output/implementation-artifacts/toolbox/13-4b-implementation-evidence.md` (new).
- `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml` (parent comments/date).
- `_bmad-output/planning-artifacts/toolbox/toolbox-decision-register.md` (current D07 follow-up).

- This story file.
- Parent 13.4: child preparation link only.
- Epic 13 and parallel migration: current isolated preparation follow-up/link only.
- Decision register: proposed D07 package link, not a confirmed decision.

## Change Log

- 2026-10-09: approved D07 in-memory slice implemented; 20 regression groups added, A behavior and full-suite baseline limits retained; status review pending acceptance.
