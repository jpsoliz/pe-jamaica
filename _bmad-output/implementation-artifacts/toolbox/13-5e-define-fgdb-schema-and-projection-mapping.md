---
story_key: 13-5e-define-fgdb-schema-and-projection-mapping
track: toolbox
story_type: contract-and-code
created: 2026-10-11
parent_story: 13-5-add-versioned-toolbox-workspace-persistence
depends_on: [13-5a-add-storage-contract-and-hydration-foundation]
execution_gate: reviewed-exact-schema-and-geometry-profile-before-hosted-adapter
implementation_authorized: false
baseline_branch: refactor/toolbox-case-workspace
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5E - FGDB Schema And Projection Mapping

Status: backlog
Refinement: contract-first candidate. Exact schema review precedes adapter implementation; no production schema approval inferred. No child tracker key.

## Ordered Implementation Stories

Prepared under JotaPe's documentation-only/no-coding instruction; exact catalog/profile authoring is itself a future E.1 deliverable, not fabricated by story preparation.

| Story | Owns | Execution dependency |
|---|---|---|
| [E.1 Exact FGDB Catalog And Geometry Profile](13-5e-1-define-exact-fgdb-catalog-and-geometry-profile.md) | Exact catalog/ownership/profile documentation | Separately authorized catalog execution and named review; no GIS writes |
| [E.2 Pure Projection And Read-Back Verification](13-5e-2-define-pure-projection-and-readback-verification.md) | Headless row/link/payload plans and reconstruction | E.1 accepted plus explicit code authorization |
| [E.3 Isolated Hosted Schema Adapter](13-5e-3-define-isolated-hosted-schema-adapter.md) | Disposable FGDB schema/geometry round-trip evidence | E.1/E.2 accepted, schema/profile/host authorization and applicable C/D/F07 gates |

Order E.1 -> E.2 -> E.3. Full E acceptance precedes B host commit work; no new tracker keys. [Review/coverage record](13-5deb-story-preparation-review-2026-10-11.md).

## Story

As a Toolbox architect/developer, I want a machine-checkable mapping from accepted A/B state to stable-keyed FGDB tables and immutable evidence payloads, so that GIS materialization can be proven lossless without a second editable master.

## Execution Order And Gates

First deliver the exact schema/mapping catalog and a pure projection/read-back planner. These can be reviewed alongside C/D without any hosted writer. Next, implement only that reviewed schema in a disposable isolated FGDB. D01/D11 ownership/schema approval, explicit geometry profile and authorized host test environment are entry gates for the hosted adapter. F07 required before experimental installation. C acceptance precedes persistence integration; D acceptance precedes any legacy import materialization. Synthetic A/B fixtures can test schema without waiting for D.

No authority switch in E. No operational output adapter, legacy reader/writer dispatch, map/UI editing integration, remote service, solver/topology repair, snapshot/package or baseline GDB modification. Retain ArcGIS Pro SDK 3.6/net8.0-windows/x64 and existing Python/ArcPy boundaries; use SDK-safe thread adapters rather than domain-object default serialization or a new GIS library.

## Required Schema Deliverable

Add one machine-readable `toolbox_fgdb_schema_v1` catalog plus readable ownership matrix. Every persisted property in A's actual field manifests, including B nested records, must be assigned exactly one owning representation: typed table field/link row, immutable canonical/policy payload reference, or explicitly derived projection. No "serialize the rest" catch-all without exact record/field ownership. Store model/archive snapshot identity separately from durable revision/commit/sequence and domain identity; never use ObjectID as a stable key.

Explicitly separate metadata namespaces: A owns model/archive/domain identity; C's store/revision/request/receipt is immutable import/prototype provenance; the host envelope owns host store/commit/parent/sequence/schema/profile authority. E.1 defines/reviews exact preliminary host-context fields for E.2/E.3 synthetic disposable fixtures, without a committed-host claim or dependency on B.1 execution. After E acceptance, B.1 freezes its host protocol against those ownership fields; incompatible refinements require E catalog/adapter re-review before affected B integration, not silently changing A/C formats.

Each dataset definition specifies name, role (`draft`, `accepted`, `evidence`, `audit`, `projection`), geometry type/profile or nonspatial, schema version, fields with storage type/length/nullability/default/domain/index, exact primary/foreign logical keys, relationship cardinality, ordering/uniqueness/comparer rules, source property/JSON pointer, conversion/rejection rules and read-back assertion. Cross-table referential integrity is validated by the adapter; do not assume an FGDB enforces arbitrary application constraints.

| Required group | Catalog mapping obligation |
|---|---|
| CaseInfo / revisions / commits | Full case tuple, schema/owner mode, commit/revision/parent/sequence and exact payload inventory; metadata alone never proves geometry agreement |
| Model snapshots / identities | Root/archive IDs, canonical payload refs, complete kind-prefixed identity map; ordered archive links and history, retained retired/excluded parents |
| Parcels / memberships | Inclusion and append-only inclusion history; independent logical row/segment IDs, ordered parcel-point/segment membership, direction/role; no deduplication of physical vs evidence points |
| Boundaries / relations | Physical boundary IDs, source hashes, coverage/version, adjacency, dependency nodes/edges and bindings, barriers, nullable geometry tokens; explicit PointIds null/default vs empty |
| Evidence / sources | Raw/manual/reviewed/normalized values, units/presence/accuracy/source/page/zone, source identity/relative asset refs; payload owns provider unknown fields |
| Parties / links / plan metadata | Many-parcel party/evidence relationships, global facts versus parcel facts; metadata/equipment summaries are projections of their declared owner, not independent editable duplicates |
| Examination | Findings/events, original scopes, human reviews, check reviews, decisions, hashes/actors/offset times; stale/pending and forwarded-defect status preserved, never recertified on load |
| Reconciliation / lineage | Rerun candidate refs, revision/policy tokens, ordered operations/dispositions/choices, immutable lineage, ordered applicability and all original audit provenance |
| Policies / references | Exact pinned byte inventory, original linked PE identity/revision and read-only scope; stored pins/published flags not trusted policy or writable-source authority |
| Geometry datasets | Separate draft/invalid and accepted points/lines/polygons; accepted materialization requires current domain approval and geometry proof, never stage-forwarding alone |

### Proposed Encoding Rules To Review

Do not cast opaque Toolbox IDs to GUIDs or truncate them to a convenient field length. Use a deterministic surrogate key table, e.g. lowercase SHA-256 of length-delimited case/kind/full original ID with original ID owned by the immutable payload/map. Detect collisions by full-value comparison and reject; never overwrite a colliding row. Surrogate text length 64 is proposed, not a replacement domain identity/comparer. Preserve original spelling and domain-specific case sensitivity from A.

Large values (A accepts long text, token IDs and unknown JSON) remain in immutable payload assets with verified reference, record pointer and kind. Proposed bounded table display text can be nullable derived output, but truncation must be labeled projection-only and cannot replace owned values. Required identifiers/relations never truncate. Catalog must state supported maximums compatible with A or return explicit unsupported-size diagnostics.

Preserve DateTimeOffset exact round-trip text as the owning audit value; optional query-time UTC/date fields are derived. Don't lose offset/fractional precision through an ordinary Date field. Null, absent, empty, zero, unavailable and default-array states are distinct; if table storage cannot express them, assign explicit semantic-state columns plus payload reference. Arrays use ordinal link rows; do not reconstruct order from ObjectID/insertion order. All logical duplicate/comparer checks precede inserts.

ArcGIS Pro 3.6 BigInteger writable values are limited to the documented safe integer range, not every signed Int64. Preserve out-of-range owning values through an exact reviewed text/payload representation or typed unsupported rejection; numeric projections must never silently round sequence or identity metadata.

Geometry profile must be explicitly supplied/reviewed: spatial reference including local/unreferenced cases, units, precision/resolution/tolerance, ring/direction rules, multipart/holes, empty/invalid handling and curves/Z/M policy. No universal CRS or arbitrary snapping tolerance. Unsupported semantics fail, not flatten/drop. Geometry token strings from headless tests are not coordinate truth. Default PointIds remains an inspection-only domain limitation until a separate reviewed hash change; schema mapping may not normalize it to pass approval.

Canonical/policy payload files are immutable same-commit-owned evidence, not independently editable JSON. A remains `prototype_bundle`; a separate FGDB catalog contract records host commit metadata. Compatibility JSON/GeoJSON/CSV is disposable, read-only, case/schema/revision/commit-stamped output from a named verified owner. Don't regenerate accepted geometry from old reviewed JSON when a dataset is missing. If an existing consumer can't read revision-qualified projections safely, leave it disconnected until B's explicit ownership adapter.

## Acceptance Criteria

1. Given actual A v1 field/type/nullability and enum manifests, automated coverage assigns every property exactly once, rejects unmapped/dual-owned fields and pins catalog version; unknown canonical values remain intact.
2. Given A/B current/archive/shared-edge/split-merge fixtures, pure row planning and reconstruction preserve full IDs/order/comparers/raw bytes/audit and equivalent stale/readiness results where domain hashing is supported.
3. Given oversized IDs/text, collision, unsupported enum/version, null/default distinctions and offset timestamps, conversion is lossless or explicit unsupported; no silent casts/truncation/defaulting.
4. Given reviewed geometry profile and isolated host authorization, schema creation/write/read-back in a new disposable FGDB verifies exact fields/datasets/key closure/counts and geometry semantic digests under that profile. OID/lock/file-byte equality is not approval proof.
5. Given draft/invalid or forwarded-defect state, accepted datasets remain unavailable unless independently authorized/currently approved; linked source PE materialization is immutable and excluded from current writable geometry.
6. Given missing/mutated payload/projection/schema or unknown geometry semantics, read-back refuses a clean writable/accepted result, never repairs from another owner's JSON or newest GDB.
7. Given completion, exact catalog is reviewed by Product/Winston/GIS owner, headless and hosted evidence reported separately, all 158 predecessor and applicable C/D groups preserved. E does not establish B commit recovery/activation or F01/F02/F03.

## Tasks / Subtasks

- [ ] Produce exact machine catalog and readable ownership/conversion/cardinality matrix from actual accepted manifests; identify unsupported geometry/domain values explicitly. (AC1,3,5)
- [ ] Obtain D01/D11 catalog and geometry-profile review; record decision/version and host isolation prerequisites before enabling GIS writes. (AC4,7)
- [ ] Add pure stable-key projection planner and read-back verifier with strict version/field coverage tests. (AC1-3,5-6)
- [ ] Implement isolated SDK/ArcPy schema adapter on disposable targets only; integrate immutable payload refs without altering A envelope or operational writer. (AC4-6)
- [ ] Add synthetic state tests and real schema/geometry round trips, tampered/missing/size fixtures; run existing build/focused/full lanes, independently review ownership and document gaps. (AC1-7)

## Dev Notes / File Map

NEW planning catalog under `_bmad-output/planning-artifacts/toolbox/` (`toolbox-fgdb-schema-v1.json` plus mapping Markdown), only once exact fields are reviewed. NEW pure mapper under `Workflow/Toolbox/Persistence/Schema/`; NEW hosted adapter under `Workflow/Toolbox/Persistence/ArcGis/`; NEW schema projection tests under `ParcelWorkflowAddIn.Tests/Workflow/`. UPDATE only `Program.cs` focused registrations after full read. No baseline FGDB/output/approval reader modifications in E. Explicitly distinguish schema planning fixtures from production-ready schemas in every catalog header/evidence record.

REUSE A actual `ToolboxWorkspaceStorageDocument.cs` DTOs/manifests, codec and normalized archive references; domain source owners in `Workflow/Toolbox/Model/`; canonical review reader. Never maintain a divergent second DTO serializer. Hosted paths use confirmed `working/TR-<validated-number>.gdb`, leading zeros retained, legacy numeric GDBs untouched. B must add authoritative writer/commit routing before any integration.

## References

- [A contract](13-5a-add-storage-contract-and-hydration-foundation.md), [parent AC8/12/14/17](13-5-add-versioned-toolbox-workspace-persistence.md), [contract catalog outline](../../planning-artifacts/toolbox/toolbox-contract-catalog.md), [ownership mapping](../../planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md), [architecture](../../planning-artifacts/toolbox/toolbox-architecture.md), [project rules](../../project-context.md).
- Checked 2026-10-11: [ArcGIS Pro 3.6 field data types](https://pro.arcgis.com/en/pro-app/3.6/help/data/geodatabases/overview/arcgis-field-data-types.htm). Field behavior/compatibility must be checked against the installed 3.6 lane, not inferred from an unversioned latest page. This reference does not approve a target schema.

## Dev Agent Record

Prepared/refined into E.1-E.3 on 2026-10-11 under explicit no-coding instruction; not implemented. Exact catalog and hosted geometry choices remain deliverables/review gates, not fabricated accepted decisions. No GIS tests, catalog acceptance or authority switch performed by preparation.
