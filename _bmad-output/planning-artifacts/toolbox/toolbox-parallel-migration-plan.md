# Toolbox Parallel Development And Migration Plan

Story: 13.2
Date: 2026-10-08
Status: architecture direction accepted 2026-10-08; execution/deployment gates retained

## Operational Baseline And Isolation

Observed branch wipRC9; baseline HEAD 5908f30617cdc49e09873635a30343da8aff9ad6. Confirm baseline again before implementation because current development continues. Observed project/DAML version 1.2.3, net8.0-windows, x64, desktopVersion 3.6. These are inspected source settings, not installation/runtime certification.

Operational add-in ID is {7C3FB44F-F7D4-41AB-B51D-92EFCDB2E4AF}; modules/buttons/dockpanes use ParcelWorkflow-prefixed IDs. A different assembly or Git branch alone does not establish side-by-side registration safety.

Recommend isolated test machine/environment initially. A separately named user profile is acceptable only after verifying registration scope and settings/case isolation; simultaneous same-process loading needs a separate feasibility decision. No operational registration is replaced by a Toolbox build.

## Separate Worktree Procedure For Later Implementation

1. Record baseline branch/commit, clean/dirty state and protected operational package identity. Capture authorized design artifacts explicitly; untracked files are not inherited by a worktree.
2. Create dedicated branch such as design/toolbox or refactor/toolbox-case-workspace in an approved writable location. Do not switch/reset the user's operational checkout.
3. Configure separate output, case root, maps/project, test credentials/settings and mock/dry-run service adapters.
4. Build without running a packaging script that auto-registers into the operational user's add-in path.
5. Install only in the isolated test environment after baseline/experimental build identity and rollback are verified.
6. Port shared fixes explicitly with review; do not merge until all applicable acceptance gates pass.

Exact worktree path is intentionally not created here. It must remain in an authorized workspace or receive appropriate filesystem authorization if elsewhere.

Story 13.3 execution record: JotaPe authorized isolated implementation on 2026-10-08. Created `temp/toolbox-worktree` on `refactor/toolbox-case-workspace` at baseline 5908f30. A clean detached `temp/tb-base` checkout supports regression comparison. These local-only builds/tests do not establish F07 installation safety; no experimental installation, data migration or live service operation occurred. See the story for validation limits.

## Protected Data And Service Boundaries

- Baseline ParcelWorkflowCases is read-only to experimental tooling. Create sanitized copies of documents and packages under a separate case root.
- Experimental config loads no operational FeatureServer URLs or mutation credentials by default.
- Use mock/dry-run Innola writes until task mapping and ownership policies are verified. Later live sandbox operations require explicit authorized targets and ownership; no ad hoc production TR tests.
- Enterprise tests use isolated services/tables, not mixed revision rows on baseline working layers. Existing readers may ignore future revision fields.
- Never copy access tokens/passwords into repo fixtures/packages/logs. Maintain existing certificate/token acquisition boundaries.
- Do not alter current source/package data in place to fit v2 schema. Keep migration outputs and evidence separately identifiable.

## Migration Phases And Ownership

| Phase | Authoritative data | Work | Exit evidence |
|---|---|---|---|
| M0 Isolation/baseline | Existing operational case/JSON unchanged | Pin baseline and copied fixtures, mock adapters, no auto-registration | Baseline still usable; targets/case roots distinct |
| M1 Context/preset seam | Existing review/approval JSON | Add independent workspace selection and actual-task observation | S05/S12 permission tests; no server writes |
| M2 Canonical contract/parcel model | Existing JSON owner | Reuse Story 11.2 model; map stable IDs/memberships, lineage and adjacency prototype | Single/multi-parcel round-trip, manual overrides preserved |
| M3 FGDB authority cutover in test cases | One CaseStore writer over experimental FGDB | Import legacy reviewed evidence, establish journal/revisions, emit compatible projections | Restart/interruption/reconciliation tests; no dual writers |
| M4 Reusable tool adapters | Experimental CaseStore | Wrap current domain tools; candidates and findings use revision contract | Tool result/negative-path tests; no runtime rewrite |
| M5 Compute and Compare slices | Accepted experimental snapshots | Prove one parcel and Cad Map including correction/recheck | S01-S05/S10 pass; read-only Compare |
| M6 Remote handoff | Accepted same-revision snapshots | Service capability/concurrency proof, package v2, verified receipts/transition | S09/S11 and unknown/conflict tests pass on sandbox |
| M7 PLA/RT adapters | Source references immutable; current-task evidence editable | Compose existing semantics with shared persistence/handoff | S06-S08 pass; no originating PE writes |
| M8 Controlled adoption | Explicitly migrated cases/new deployment | Product approval, rollout and restore contingency | Baseline compatibility and rollback evidence |

M3 cannot run until D01/D07 design and commit feasibility are accepted. M6 cannot run until mapping/authority/publish/retention policies and service capabilities are verified. These are entry conditions, not optional hardening.

## Proposed Refined Story Slicing

Current 2026-10-11 decomposition supersedes the historical 13.5A grouping below: A is accepted no-writer codec/hydration; [C](../../implementation-artifacts/toolbox/13-5c-add-isolated-prototype-disk-revisions.md) is the isolated ordinary-file prototype store; [D](../../implementation-artifacts/toolbox/13-5d-add-read-only-copied-case-import.md) is plan-bound copied import; [E](../../implementation-artifacts/toolbox/13-5e-define-fgdb-schema-and-projection-mapping.md) is exact schema/ownership mapping then gated hosted materialization; [B](../../implementation-artifacts/toolbox/13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md) retains final recoverable FGDB commits, edit/snapshot proofs and isolated authority activation. Order A -> C -> D/E -> B, with E catalog review possible alongside C. All remaining children backlog; no child tracker keys or implementation authorization. C prototype receipts do not move M3 authority. D01/D06/D11 production policy and F01/F02/F03/F07 remain gates for affected scope.

13.3 context/preset is the first small code slice after architecture acceptance and isolation. It must not move storage authority or perform server writes.

Keep 13.4 as canonical parcel identity/dependency work and 13.5 as persistence ownership, but split before development:
- 13.4A: Epic 11-compatible model and stable parcel memberships.
- 13.4B: candidate identity lineage/shared-edge dependency model and fixtures.
- 13.5A: copied-case import plus target schema/projection read/write.
- 13.5B: recoverable commits, accepted snapshots and restore conflict behavior.
- 13.9A: snapshot/package/restore v2 and task-independent case identity.
- 13.9B: capability-gated Enterprise projection with exact revision and concurrency proof.
- 13.9C: remote receipt reconciliation and verified lifecycle handoff.
- 13.10A: PLA_A preset over extraction/review/evidence tools.
- 13.10B: PLA_B source recovery/crop/attachment preset.
- 13.10C: RT read-only PE and current-Plan Save/Save & Close preset.

These are proposed slices, not yet new accepted story IDs/files/tracker entries. Keep original backlog rows until architecture/product review accepts the decomposition; do not silently create a second overlapping sprint.

2026-10-08 preparation clarification: JotaPe subsequently requested [13.4A](../../implementation-artifacts/toolbox/13-4a-add-stable-parcel-memberships-and-acceptance.md) as a separate detailed story. A covers local identities/memberships/inclusion/acceptance; B retains rerun reconciliation, shared-boundary dependencies and lineage. A is prepared but backlog, tracked through parent 13.4 with no new sprint entry. This supersedes the no-file statement for A only; D11/11.2 and local identity-policy gates remain open, and B/other slices remain proposed.

2026-10-08 isolated follow-up: A's in-memory model and mapped canonical adaptation are implemented/tested and remain in review. JotaPe requested [13.4B preparation](../../implementation-artifacts/toolbox/13-4b-add-rerun-reconciliation-shared-boundaries-and-lineage.md); B now has a detailed specification, backlog pending A signoff, proposed D07 rule approval and execution authorization. It defines non-destructive reconciliation, shared dependencies and split/merge lineage, not FGDB commits. This supersedes earlier no-file/preparation-only descriptions for A/B; other slices remain proposed. Parent 13.4 remains in-progress with no new child tracker key.

Use the existing 11.2 contract as dependency or deliberate cross-track adaptation. Its current contract-only scope is preserved; ownership/UI/API changes stay in Toolbox stories. No legacy backlog row is marked delivered/superseded by architectural resemblance.

2026-10-08 approval/review follow-up: D07 P01-P05 approved as `toolbox_d07_reconciliation_v1`. A signoff was attempted but remains withheld for three additional final-review gaps (see A evidence); B execution is still unstarted and requires completed A signoff plus explicit authorization. Real GIS/FGDB/production integration gates remain open.

2026-10-08 current scoped acceptance: A's authorized corrections plus evidence-alias follow-up are fixed and final patch review is clear; clean build and 105 focused passes. A and its isolated canonical amendment are accepted, superseding the withheld-signoff note above. Full-suite baseline failures remain documented; B awaits explicit implementation authorization, with no deployment/FGDB/production authority cutover or original 11.2 closure.

## Future UPDATE Target Mapping

2026-10-11 refinement current state: remaining disk/import/schema/host scopes now have bounded C/D/E/B story files and unchecked tasks. This supersedes historical unrefined descriptions below, not named ownership/host gates. Immediate review is C's isolated physical path/writer/conflict contract; no runtime UPDATE performed by this refinement. E/B must enumerate and read any later operational activation adapter targets before changing them; no broad writer/reader migration authorized.

2026-10-11 latest A acceptance: JotaPe accepted the isolated no-writer codec/hydration foundation, marking 13.5A done. Supersedes A review-pending descriptions below; no disk/import/schema/FGDB work or authority activation implied. Parent remains gated. Next refine remaining M3 foundation, preserving accepted source/history semantics and the documented full-suite/default-array limits.

2026-10-11 A implementation current state: no-writer codec/hydration is implemented and in review with [158 focused passes and explicit limits](../../implementation-artifacts/toolbox/13-5a-implementation-evidence.md). Narrow restoration methods added without changing action/hash algorithms. The default-PointIds domain hash limitation is preserved, not normalized. Disk/import/schema and hosted M3 work remain unrefined/gated; no active writer, source migration, FGDB, registration or remote change. This supersedes the no-runtime/readiness description in the preparation note below.

2026-10-11 refined A boundary: [13.5A foundation](../../implementation-artifacts/toolbox/13-5a-add-storage-contract-and-hydration-foundation.md) is ready-for-dev for a no-filesystem byte bundle, bounded validation and lossless historical hydration. The earlier proposed A grouping of copied-case import, disk revisions and FGDB schema adapters is not this executable slice; retain those as separately gated M3 work to refine after A review. No runtime code or child tracker key added. Parent 13.5 remains backlog; production ownership and hosted feasibility remain open.

2026-10-11 current M2/M3 update: JotaPe accepted B's isolated in-memory scope after review corrections, closing A+B and parent 13.4 for M2. [13.5 persistence](../../implementation-artifacts/toolbox/13-5-add-versioned-toolbox-workspace-persistence.md) is now prepared/backlog with ordered proposed A/B work: refine A's versioned contract/hydration foundation first, then isolated save/reopen/schema adapters; B proves FGDB recovery, edit tracking and snapshots before isolated authority activation. No new child tracker keys or runtime persistence are added. Detailed schema/ownership and F01/F02 remain gates; original 11.2 and production writers stay unchanged. This supersedes earlier B-pending descriptions above, not their retained GIS/release limitations.

| Existing boundary | Current behavior | Intended adapter/change | Preserve |
|---|---|---|---|
| WorkflowWorkspacePlanner / session/UI | Current transaction-stage routing | Separate selected preset from observed task | Login/ownership gates and baseline launch |
| Review document/persistence | JSON review and hash approvals | Compatibility projection after explicit FGDB cutover | Single/multi-parcel metadata and existing consumers |
| Manual point/segment services and solver | Parcel-scoped rows and reconstruction | Candidates/deltas through CaseStore | Source evidence, manual edits and solver diagnostics |
| Output execution/adapter | Approved-review-driven local output | Named accepted snapshot input/export | Existing schemas/CRS/units until migration |
| CompareWorkingGeometryService | Enterprise scoped loading | Accepted local/package reader plus verified recovery | Read-only geometry and evidence separation |
| Enterprise publish/restore | Per-layer scoped replacement/fallbacks | Exact revision adapter, capability/concurrency checks | Baseline path until gated replacement |
| CaseResumePackageService | Conditional full output; strict task identity; root replacement | New v2 reader/snapshot/inclusion and activation adapter | Wrong-TR/path rejection; v1 compatibility behavior |
| PLA/RT services | Task-specific outputs, source PE reads and writes | Shared context/ledger wrappers | Attachment types, Plan child templates and branch semantics |

Before any UPDATE implementation, read the entire target and relevant tests; this design does not authorize broad refactors of all listed files at once.

## Rollback And Release

Retain baseline add-in package, configuration and original cases; record installation path/identity before experiment. An isolated test failure cannot change the operational registration/Enterprise targets.

Do not offer baseline build a v2-only case as a claimed rollback path. Return to untouched baseline data or a verified downgrade/export adapter with explicit data-loss limitations. Retain new case packages for later recovery rather than delete them.

Operational adoption requires explicit release approval and an observed installation/restore smoke on both case types. Feature flags alone are insufficient if migration changes storage or registered IDs. Monitor failed/ambiguous receipts and revision conflicts during pilot; rollback stops new writes before restoring prior reader/build. No automated authoritative cadastre promotion is included.
