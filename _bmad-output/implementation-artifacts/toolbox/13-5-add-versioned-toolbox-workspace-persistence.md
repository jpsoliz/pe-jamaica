---
story_key: 13-5-add-versioned-toolbox-workspace-persistence
track: toolbox
story_type: code
created: 2026-10-11
depends_on: [13-4-add-single-and-multi-parcel-toolbox-model]
execution_gate: reviewed-persistence-contract-and-isolated-recovery-proof
baseline_branch: refactor/toolbox-case-workspace
baseline_commit: 0a6188f467da81ccdbee639cd0c8954b1e727e85
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5 - Add Versioned Toolbox Workspace Persistence

Status: backlog
Preparation: detailed parent specification; A implemented and scoped-accepted, remaining children refined but not implemented.
Predecessor: 13.4A and 13.4B accepted for their isolated in-memory scope; parent 13.4 done for M2 on 2026-10-11.

The parent remains backlog because detailed production storage/schema ownership and FGDB commit/edit feasibility are not yet accepted or proven. [13.5A's no-writer foundation](13-5a-add-storage-contract-and-hydration-foundation.md) is accepted/done. C/D/E/B are refined backlog candidates with explicit review/dependency gates, not execution authorization. No separate child tracker keys are created.

## Story

As an examiner,
I want case geometry, reviewed evidence, identities, findings and examination history saved as verifiable revisions,
so that I can reopen or recover a case without losing corrections, trusting stale approvals or mixing data from different revisions.

## Scope And Ordered Slices

This is migration M3 over the accepted A/B model. Implement only in `temp/toolbox-worktree` on `refactor/toolbox-case-workspace`, with sanitized copied cases below an explicit isolated root. Operational cases, writers, registration, settings and services remain protected.

| Slice | Deliverable | Entry / exit |
|---|---|---|
| [13.5A](13-5a-add-storage-contract-and-hydration-foundation.md), done | Prototype byte codec and validated lossless A/B hydration | Scoped acceptance 2026-10-11; no storage ownership/cutover claim |
| [13.5C](13-5c-add-isolated-prototype-disk-revisions.md), backlog | Contained ordinary-file revision store, exclusive local writer, expected-parent/retry receipts and process-interruption recovery | Scoped prototype path/writer contract review before implementation; no FGDB or approved-revision authority |
| [13.5D](13-5d-add-read-only-copied-case-import.md), backlog | Frozen read-only copied-case import plan, explicit normalization/identity/provenance and C target commit | C accepted and mapper/identity review; no legacy approval promotion or GDB adoption |
| [13.5E](13-5e-define-fgdb-schema-and-projection-mapping.md), backlog | Exact machine-checkable ownership/schema catalog, pure projection planner, then isolated hosted adapter | Contract first; D01/D11 and geometry-profile review before host writes; C/D before affected integration |
| [13.5B](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md), backlog | Real FGDB commits, edit detection, supported snapshots/restore and explicit isolated authority activation | C/E accepted; D when importing; F01/F02/F03 proof and writer/cutover review before activation; F07 before installation |

Next review C's scoped contract, then authorize its implementation separately. Execution order is A -> C -> D/E -> B; E's catalog can be refined alongside C/D, but hosted/integration gates remain. Letters retain stable IDs, not execution order: B remains the previously reserved final FGDB slice. A acceptance does not authorize these children or complete the parent.

### Readiness Gates

| Gate | Current evidence | Required for affected code |
|---|---|---|
| Accepted M2 | Explicit scoped acceptance on 2026-10-11; clean build, 135 focused passes; full suite stops after 207 passes at known Compare XAML assertion | Preserve accepted corrections and local permission boundaries |
| D01/D11 storage ownership | A01/A02 direction accepted; canonical writer unchanged; mapped contract accepted | Review versioned DTO/table mapping, lossless raw payload references, projection ownership and explicit activation/rollback rules |
| D06 local writer | Single CaseStore writer is architectural direction, not a delivered lease | Define exclusive local writer acquisition, expected-parent comparison and stale-writer/recovery protocol |
| F01 recoverable commit | Not proven | Inject failure at every journal state with real isolated FGDB; demonstrate deterministic completion/quarantine/recovery |
| F02 manual/external edits | Not proven | Prove SDK/GP edits are observed; direct/untracked edits block current approval/snapshot use |
| F03 consistent snapshots | Not proven | Quiesced supported copy, full frozen evidence inventory and read-back; unresolved commits block snapshots |
| Geometry and deployment | In-memory policy tokens are synthetic; no production installation approved | Explicit supported CRS/units/geometry policy and isolated host target for GIS tests; F07 proof before any experimental installation, no new registration/install by default |

Pure DTO design and preparation do not require closing remote D02/D05/D10 gates. Conversely, M2 acceptance does not supply real geometry policy, filesystem safety or persistence feasibility.

### Out Of Scope

Tool registry/preset UI (13.6-13.8), extraction/solver rewrites, automatic topology repair, authoritative cadastral promotion, Enterprise/Innola publication or task changes, ZIP/package v2 upload/restore (13.9), production migration/deployment and retirement of operational 11.2. No arbitrary repository/document platform work from the separate standalone specification is included.

## Acceptance Criteria

1. Given a new or copied test case, when the store is opened, then it requires a full environment/case/transaction identity, supported schema and an isolated root; rejects operational roots, unsafe/colliding names, traversal and reparse-point escapes; and performs no remote operation. New Toolbox GDBs use `working/TR-<validated-number>.gdb`, preserving leading zeros. Legacy numeric GDBs are never renamed or adopted as writable masters implicitly.
2. Given an accepted A/B snapshot, when converted to the storage contract and hydrated, then case identity, canonical unknown/raw fields, source inventory/traces, stable maps, ordered memberships, inclusion/history, dependency bindings/coverage, boundaries/adjacency, policy pins, reruns, lineage, applicability, findings/human review/decisions and accepted disposition/operation audits survive. No new entity IDs, times, review events or authorization are minted on load.
3. Given archived models with repeated ancestry, when persisted, then unique historical records use stable revision/snapshot references rather than recursively duplicating the object graph. Hydration rejects missing, cyclic, foreign-case or inconsistent history. Do not equate every archived model with a separately committed disk revision: pre-commit model changes may need their own immutable snapshot IDs.
4. Given input and hydrated models under the same policy/geometry inputs, when fingerprints and readiness are compared, then semantic hashes and current/stale/pending states remain equivalent, including invalidated historical approvals, recorded source defects and unavailable LTD comparisons. Reopen must not refresh old proofs by replaying review methods against new inputs.
5. Given copied legacy JSON or geometry, when imported, then an explicit read-only normalization/import plan records original paths/hashes and source identity; preserves raw/manual values and unknown data; diagnoses unkeyed/ambiguous entities; and creates a new isolated target without modifying its source. Legacy approval flags are historical evidence, not Toolbox acceptance. Do not allocate replacement IDs to repair a known accepted case with a missing map.
6. Given two save attempts, when the writer acquires the case lock, then it reads current committed revision/digest under that exclusive lock, rejects a stale expected parent and prevents a second writer. Lease recovery cannot rely solely on timestamps or PID reuse. Concurrent, locked, canceled and ambiguous outcomes retain the winning state and actionable diagnostics.
7. Given a save, when the commit begins, then a durable prepared journal records operation ID, parent/current and proposed revision/sequence, caller/permission class, affected IDs, input/output logical digests and staged artifact inventory. Geometry changes require authorized local Compute; Compare saves current-case tabular changes only. Linked PE snapshots remain immutable/read-only.
8. Given a real FGDB write, when SDK/GP edits are saved, then GDB metadata, staged canonical/evidence catalog, revision record and generated compatibility projections bind the same case, commit ID, schema and revision. Advance the committed pointer only after required read-back and integrity checks. No atomic transaction across FGDB and ordinary files is claimed.
9. Given failure/cancellation at prepared, fgdb_saved, catalog_written, pointer_switched or journal-finalization boundaries, when reopened, then the store reports pending/recovered/quarantined state and deterministically completes a fully verified commit or exposes the last verified revision read-only. A saved-but-unpublished GDB cannot masquerade as the old pointer's geometry. Prior accepted snapshots remain available; uncertainty never becomes clean success.
10. Given an operation already recorded durably, when retried after restart, then identical operation ID/digest returns its verified receipt without reallocating IDs or duplicating audit/lineage. A changed payload, conflicting parent or incomplete commit requires reconciliation. Durable retry is a new guarantee here, not inherited from B's in-memory replay test.
11. Given an accepted snapshot request, when writers/runs and map edits are quiesced and no unresolved commit or pointer/GDB/catalog disagreement exists, then a supported ArcGIS copy produces a separately validated FGDB and frozen source/evidence references with exact revision/table/dataset coverage. Otherwise reject the snapshot request. Do not ZIP/copy a changing GDB, skip lock files as a consistency strategy or manually move its internal files. Referenced external rasters/documents need their own inventory and frozen copies or explicit unsupported status. F03-equivalent isolated proof is required, independently of package v2 delivery.
12. Given an accepted revision with recorded source defects, when stored/read, then stage forwarding remains separate from valid-geometry approval. Draft/invalid geometry is retained in explicit draft datasets; consumers requiring approved geometry cannot select it merely because a stage can forward. Every included parcel and exclusion/history remains visible regardless of selected UI scope.
13. Given geometry or evidence modified outside the recorded store operation, when save/reopen/snapshot is requested, then logical comparison or verified edit tracking detects it and blocks dependent approvals/exports until an audited revision and fresh examination are recorded. ObjectIDs, file checksums or cached revision strings alone are not semantic edit proof.
14. Given an isolated ownership activation, when it switches from copied legacy evidence to Toolbox CaseStore, then one writer owns reviewed state, projections are revision-stamped/read-only to legacy consumers, and an explicit adapter prevents independent legacy saves into the Toolbox target. Pending commits block activation. Rollback retains untouched baseline cases and never claims the old build can read a new-only schema.
15. Given missing/corrupt files, unsupported versions, invalid enum/kind references, mismatched digests/policy bytes, stale pointers or absent required artifacts, when loaded, then typed recoverability findings describe what is usable read-only and what blocks editing/approval. Do not fall back to whichever file/GDB is newest or recreate geometry from older review JSON silently.
16. Given a case reopened under a different user/task observation or relocated isolated root, when activated, then safe relative paths rebase under the validated root, source case/revision identity stays intact, and current session/permissions are reacquired. Persisted actor/history is retained, but saved local edit flags, credentials and server authority are never restored as permission grants.
17. Given the target schema and compatibility exports, when read back, then stable keys, many-parcel relations, null/semantic states, units and original/reviewed values round-trip; unsupported geometry semantics and field-size limits fail explicitly rather than truncate. Structured tables and immutable canonical payload references have one declared owning representation per value.
18. Given completed implementation, when verified, then headless DTO/filesystem/fault-injection tests and real ArcGIS/FGDB tests are reported separately. Test 1, 3 and variable-count parcels, empty geometry, shared edges, retired/excluded parents and recorded defects. Preserve all 135 focused baseline groups, report full-harness fail-fast limits, and demonstrate save/reopen plus recovery before claiming the parent complete.

## Tasks / Subtasks

- [x] Refine A's isolated prototype storage DTO/hydration foundation with field-to-source mappings and executable bounds; accepted/done for no-writer scope. (AC2-4,15-17 foundation only)
- [x] Refine remaining C disk, D copied import, E schema and B hosted recovery/activation candidates with explicit contracts/tests and review gates; no execution approval inferred. (AC1-18 planning only)
- [ ] Review/accept remaining production storage schema/ownership, path and writer contract before disk/import/FGDB work. (AC1-5,6,15-17)
- [x] Implement versioned DTO codec and explicit validated model/register restoration factories; preserve canonical snapshots and exact pinned policy bytes. (AC2-4,15; isolated no-writer 13.5A accepted/done, not production storage)
- [x] Add headless single/multi-parcel/archive round-trip and malformed-history fixtures before adding FGDB writes. (AC2-5,18; no legacy import or FGDB proof implied)
- [ ] Implement contained revision paths, exclusive writer/expected-parent guard, staged ordinary-file writes and deterministic prototype recovery. (AC1,6-10,15-16)
- [ ] Implement read-only copied-case import and reviewed target FGDB schema/projection adapter under a distinct test owner, including separate draft/approved datasets and source-defect dispositions. (AC5,12,14,17)
- [ ] Implement B's real FGDB commit metadata, catalog/projection staging, verified pointer publication, durable receipts and restart recovery behind the isolated store. (AC7-10,15)
- [ ] Prove F01 with real FGDB failure injection and read-back at every journal state; record unsupported storage/lock behaviour. (AC7-10,18)
- [ ] Prove F02 external-edit detection and F03 supported, quiesced ArcGIS snapshots with frozen evidence inventory, pending-commit rejection and no source mutation; satisfy F07 before any experimental installation. (AC11-13,18)
- [ ] Gate isolated authority activation and compatibility consumer access on all prerequisites; retain rollback evidence. (AC14-17)
- [ ] Run focused/legacy/full checks and isolated hosted save/reopen/recovery scenarios; document results and deviations without overstating completion. (AC18)

## Dev Notes

### Storage Contract And Ownership

Publish a versioned storage envelope, not direct default serialization of domain objects. Proposed contract identifier: `toolbox_workspace_storage_v1`; exact published fields and limits are A's first deliverable.

| Persisted group | Required contents / rule |
|---|---|
| Envelope | Contract/schema/fingerprint versions, full case identity, storage-owner mode, current/accepted revision pointers, operation/parent, local sequence, actor/time and integrity inventory |
| Canonical snapshots | Original `ToJson()` payloads for current, rerun and historical model inputs; unknown nested provider fields remain owned copies |
| Identity and parcel state | Complete kind-prefixed map, current inclusion plus audit history, immutable ordered memberships; no ID allocation during hydration |
| Examination | Original findings/events, human reviews, check/decision proofs and pending reasons; preserve old timestamps/bindings, do not re-record against current inputs |
| Reconciliation | Source-hash tokens, review/reconciliation policy versions, boundaries/adjacency, graph nodes/edges, GeometryCheck and DecisionParcelId bindings, lineage, applicability, barriers and input bindings |
| Accepted operations | Operation/digest/revision receipts and accepted disposition records including choices/actor/reason/evidence/UTC acceptance time |
| History references | Unique historical snapshot IDs and case/revision ancestry; shared archives referenced once, with no cycles/foreign identities |
| Artifacts and sources | Safe relative paths, sizes and real transport checksums; frozen evidence inventory and exact GDB dataset/table manifest; source logical tokens remain separate from file SHA-256 |
| Policy assets | Exact bytes and external trusted pin reference; stored approval/published flags do not establish authority |

Target FGDB tables/classes are the [catalog outline](../../planning-artifacts/toolbox/toolbox-contract-catalog.md#fgdb-schema-outline-v1). E must produce and obtain review of exact field/cardinality/length/nullability/relationship mappings before hosted writing. A's published codec is locked and does not define active-store metadata. Include delivered B concepts: identity mappings, dependency bindings, inclusion history, finding/human events, archived applicability and accepted-disposition audit. Map logical point/segment IDs to storage keys without using ObjectIDs as identity. Materialized geometry and logical row evidence are different inputs; do not manufacture one from the other when its required source is absent.

Immutable canonical/provider payload files may accompany relational FGDB records for lossless evidence. They must be content/revision-addressed and owned by the same commit; they are not a competing editable JSON master. Compatibility JSON/GeoJSON/CSV is disposable, stamped output from the committed owner. Existing operational extraction/review JSON remains owned by its current writer until an explicitly gated isolated switch.

### Proposed Layout And Recovery States

Under the validated copied-case root, propose `toolbox/manifest.json`, `toolbox/journal/<operation-id>.json`, `toolbox/revisions/<revision-id>/`, `toolbox/staging/<operation-id>/`, and `toolbox/quarantine/`. The active GDB remains the confirmed `working/TR-<validated-number>.gdb`; accepted snapshot GDBs live in immutable revision locations. Path segments are validated IDs, not filenames supplied by arbitrary provider JSON. These are proposed Toolbox-owned paths, never replacements for baseline `manifest.json`.

This is B's proposed active-owner layout, not C's executable prototype namespace. C uses `toolbox/prototype-store/` and a separate `toolbox_prototype_disk_v1` envelope around unchanged A bytes. Its committed byte receipt is not FGDB or geometry acceptance. E/B must publish separate reviewed host metadata contracts, not silently add fields/modes to A v1.

Use prepared -> fgdb_saved -> catalog_written -> pointer_switched -> committed, plus explicit failed/pending/quarantined/recovered outcomes. The active pointer and GDB commit metadata must agree with catalog contents before ordinary editing/approval. Reconstruct a missing committed-journal tail only from verifiable durable evidence; a pointer alone is insufficient. Preserve the prior accepted pointer/snapshot and recovery record before mutating active data.

Local exclusive-writer protection is not a distributed lock. Start with a verified local filesystem; UNC/network writes remain unsupported until their lock/rename/flush capabilities are proven. Do not claim power-loss durability from ordinary `WriteAllText` plus rename; define/test flush and replacement behaviour for the selected storage. File-level replacement is not a multi-file transaction.

Containment must be stronger than `Path.GetFullPath` plus string prefix: account for junctions/reparse points, parent replacement, case-insensitive collisions and safe cleanup of only owned staging artifacts. Failure to prove containment blocks writes, not just approval. Existing 13.3 lexical validation is context validation only.

### Code Map And Planned File Boundaries

Paths below are relative to `src/ParcelWorkflowAddIn/`.

| Boundary | Current behaviour | Planned change / preservation |
|---|---|---|
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspaceStorageDocument.cs` | No storage DTO exists | Versioned explicit envelope/reference records; preserve source and audit information |
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspaceStorageCodec.cs` | No durable hydration path exists | Strict bounded DTO parsing/encoding and validated domain reconstruction |
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxCaseStore.cs`, `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxCommitJournal.cs` | A/B are in-memory only | Isolated save/load, one writer, expected-parent checks, journal/recovery; no remote executor |
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspacePathPolicy.cs` | Existing guards are lexical | Contained path/owner validation and staged cleanup; naming service respects original TR |
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxGeodatabaseStore.cs` or scoped adapter | Legacy output adapter regenerates GDB from approved JSON | Explicit target-schema and save/read-back adapter; GIS calls behind existing SDK-safe/GP boundaries |
| UPDATE `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelModel.cs` | Private constructor; Adapt initializes inclusion/history | Add a narrow validated restoration factory if needed; ordinary Adapt/ReAdapt/identity guards stay strict |
| UPDATE `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxExamination.cs` | Private populated register; recording rebases some inputs | Add validated historical hydration, not replay of public record methods; readiness and source-defect rules preserved |
| REUSE `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxReconciliation.cs`, `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelLineage.cs`, `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxBoundaryDependencies.cs` | Complete accepted A/B contracts and hashes | Persist/restore exactly; amend ownership before any semantic change |
| REUSE `ParcelWorkflowAddIn/Workflow/Review/Canonical/CanonicalReviewContract.cs`, `ParcelWorkflowAddIn/Workflow/Toolbox/Policy/ToolboxReviewPolicyLoader.cs` | Defensive canonical reader and exact-byte policy pin | Canonical validation and trusted policy loading, with no second extraction schema |
| REUSE `ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs`, `ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`, `ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs` | Legacy paths/manifests and single-file temp/move write | Keep default behaviour; use separate Toolbox store, not broad legacy replacement |
| REFERENCE ONLY `ParcelWorkflowAddIn/CaseFolders/CaseResumePackageService.cs` | Live-file ZIP, lock exclusions, task-bound restore and root deletion | Do not reuse as consistent snapshot/rollback implementation; package v2 belongs to 13.9 |
| REFERENCE ONLY `ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs` / repository-relative `src/ProcessingTools/adapters/output_adapter.py` | Approved-review output writer can recreate a legacy GDB | Protect old path/writer; no dispatch/routing modification in the foundation slice |
| UPDATE `ParcelWorkflowAddIn.Tests/Program.cs` | Explicit harness registrations | Register focused persistence tests without replacing existing groups |
| NEW `ParcelWorkflowAddIn.Tests/Workflow/ToolboxWorkspacePersistenceTests.cs`, `ToolboxCommitRecoveryTests.cs` | No durable Toolbox tests | Round-trip, conflict, corruption, cancellation, recovery and safe-path coverage |

The private domain constructors require explicit restoration design. Blind JSON deserialization of getter-only collections can silently lose state. Replaying `RecordFinding`/`RecordHumanReview` can bind old evidence to new inputs; hydration must preserve history without laundering currency. Recursive serialization of archive objects duplicates ancestry; normalize references without discarding each snapshot's own relations.

Preparation read the current model/register/canonical/lease-relevant sources and complete legacy layout/store/serializer. No existing runtime file is edited by this preparation. Before implementation, reread every UPDATE file and its tests in full; new file names are scoped suggestions, not delivered APIs.

### Testing And Previous-Story Lessons

Minimum fixtures: single parcel; 3-parcel and variable-count Cad Maps; empty geometry; same label/different IDs; shared evidence and directed edges; excluded/retired parent; split -> merge and regenerated keys; case-wide/narrow/NotApplicable findings; pending LTD review; manual correction and accepted audit replay.

Fault matrix: stale parent, two writers, lease/process restart, access denied/disk full, cancellation at each journal boundary, missing/duplicate/corrupt revision/catalog/policy/source records, invalid enum/kind, foreign archives, cycles, changed transport bytes, source logical-token mismatch, missing accepted snapshot, direct untracked edits and pointer/GDB disagreement. Path fixtures include sibling-prefix collisions, traversal, junctions, UNC capability rejection and relocation without restoring edit permission.

Preserve all R1-R8 regressions: actual supporting source hashes, complete evidence/check/decision consumers, latest applicability and original audit, inclusion impact, metadata-version impact and bounded dependency validation. Semantically unchanged DTO round-trips must preserve hashes; binary file checksums and altered JSON formatting do not establish semantic equivalence. Exact policy bytes must survive unchanged because the loader's external hash pin is byte-based.

Use .NET 8/x64, the existing executable C# harness, current ArcGIS SDK lane and existing Python/ArcPy environment. No framework/GIS-library upgrade is part of this story. Pure tests can use a fake geometry store; hosted FGDB assertions must use the actual configured environment and cannot be replaced by fake passes. JSON contract fields follow existing `snake_case` convention.

After implementation, from the isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-persistence/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

Record actual counts and failure locations; the current 135 focused passes are predecessor evidence, not persistence test results. Full-suite baseline Compare XAML and intermittent manifest-access issues remain documented. SDK-hosted F01/F02/F03 evidence, F07 before any experimental installation, new geometry-store tests, isolated case save/reopen and crash recovery are separate exit criteria.

### Technical References Checked During Preparation

Esri recommends ArcGIS-managed geodatabase operations and warns about corruption from copying a GDB while processes access it: [file geodatabases and File Explorer](https://doc.esri.com/en/arcgis-pro/latest/help/data/geodatabases/manage-file-gdb/file-geodatabases-and-windows-explorer.html). Use a supported copy after quiescing, not arbitrary internal-file copying.

The [Copy tool](https://doc.esri.com/en/arcgis-pro/latest/tool-reference/data-management/copy.html) supports geodatabase copying and associated data, but externally referenced mosaic images are not automatically copied. Snapshot inventory must cover those referenced assets separately. These references were checked on 2026-10-11; they do not prove F01/F02 for this project or authorize a SDK version upgrade.

### Project References

- [Accepted 13.4 parent](13-4-add-single-and-multi-parcel-toolbox-model.md) and [B correction/acceptance evidence](13-4b-review-fixes-2026-10-11.md).
- [11.2 mapped ownership](11-2-toolbox-contract-amendment.md) and [verified identity mapping](../../planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md).
- [Architecture A01-A03, data authority and commit protocol](../../planning-artifacts/toolbox/toolbox-architecture.md).
- [Contract catalog](../../planning-artifacts/toolbox/toolbox-contract-catalog.md).
- [Migration M3 and proposed A/B slices](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md).
- [F01/F02 and acceptance scenarios](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md).
- [Decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md) and [Epic 13 plan](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md).
- [Project rules](../../project-context.md) and [baseline limitations](deferred-work.md).

## Dev Agent Record

### Agent Model Used

Codex; BMAD story preparation with independent source analysis. No implementation started.

### Debug Log References

Prepared against isolated checkpoint `0a6188f` plus accepted uncommitted R1-R8 corrections. Source inspection and official documentation checks only; no persistence/GIS runtime tests run for preparation.

### Completion Notes List

2026-10-11 remaining-work refinement: C/D/E/B story files now define bounded candidate contracts and test tasks. All four remain backlog until their named review/dependency gates; no implementation, schema approval or ownership switch performed. Next scoped C path/writer-contract review, then separately authorized implementation. Parent remains backlog; no extra tracker keys.

Refinement validation: independent source analysis exposed legacy transaction_id/copy-path hazards, now explicit in D. Independent story validation found missing durable abandonment and restart-safe import preparation; both contracts/tasks corrected. Relative document links/new-story whitespace and worktree/main `git diff --check` pass (existing LF/CRLF notices only). No runtime/build/GIS tests run for this documentation-only refinement.

2026-10-11 scoped A acceptance: JotaPe explicitly accepted [13.5A](13-5a-add-storage-contract-and-hydration-foundation.md); child done for no-writer contract/codec/hydration only. This supersedes A's review-pending descriptions below. Parent remains backlog for gated disk/import/schema/FGDB work; full-suite and default-array domain-hash limits retained. Next refine remaining M3 foundation and its ownership/path/writer gates; no implementation authorization for those tasks is inferred.

2026-10-11 current implementation follow-up: [13.5A](13-5a-add-storage-contract-and-hydration-foundation.md) is implemented in isolation and in review, not accepted/done. Final build zero warnings/errors; 158 focused passes, including 23 new groups. Full harness 207 passes then known Compare XAML assertion. [Evidence](13-5a-implementation-evidence.md) documents review corrections and the retained default-PointIds domain-hash limit. This supersedes historical no-runtime/preparation-only descriptions for A below. Parent remains backlog for its larger gated disk/FGDB scope; remaining tasks and production ownership/feasibility gates are not completed.

- User explicitly accepted B's isolated in-memory scope and requested 13.5 preparation on 2026-10-11; parent 13.4 closed for M2 only.
- Prepared the full persistence parent with ordered proposed A/B slices, complete A/B-state hydration and concrete recovery/ownership guards.
- Parent remains backlog pending detailed storage/schema review and named feasibility gates; no approval of authority cutover, execution or production migration is inferred.
- Independent preparation validation found four specification issues; corrected complete code paths, F03/F07 gate attribution, pending-commit snapshot rejection and explicit real-commit task coverage.
- No separate child tracker keys, package v2 behaviour, runtime code, files/data migration, FGDB creation or remote action added.

### File List

- This story.
- Scoped acceptance/status updates for B, parent 13.4, Toolbox tracker, Epic 13 plan, decision register and current-sprint summary.
- Persistence contract/model source files listed above are planned, not created by preparation.
