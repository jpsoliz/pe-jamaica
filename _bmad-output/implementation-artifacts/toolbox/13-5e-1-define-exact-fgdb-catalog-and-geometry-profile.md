---
story_key: 13-5e-1-define-exact-fgdb-catalog-and-geometry-profile
track: toolbox
story_type: documentation
created: 2026-10-11
parent_story: 13-5e-define-fgdb-schema-and-projection-mapping
depends_on: [13-5a-add-storage-contract-and-hydration-foundation]
execution_gate: separately-authorized-catalog-authoring-and-named-schema-review
implementation_authorized: false
---

# 13.5E.1 - Exact FGDB Catalog And Geometry Profile

Status: backlog
Specification: preparation and static review complete; catalog authoring is the future story deliverable, not claimed complete. Tracked through parent 13.5; no child tracker key.

## Story

As a Toolbox architect, I want one exact ownership/schema catalog and explicit geometry profile, so that projections and hosted adapters have a reviewed contract to implement.

## Scope And Dependencies

The [E parent](13-5e-define-fgdb-schema-and-projection-mapping.md) is the requirement source. Owns future machine-readable toolbox_fgdb_schema_v1 catalog and readable mapping/profile, built from actual A manifests. May be authored/reviewed alongside C/D; no GIS code/write, inferred product acceptance or authority switch. E.2 consumes approved catalog; E.3 implements it on disposable hosted targets. Authoring the exact catalog is separate from preparing this story.

## Acceptance Criteria

1. Given actual A record/type/nullability/enum manifests and B nested owners, catalog assigns each property exactly one owning field/link/payload or explicitly derived projection, with versioned coverage and no unmapped/dual-owned fields or serialize-the-rest fallback. Canonical unknown fields stay in immutable payloads.
2. Given every dataset, declare role, exact name/schema, type/length/null/default/domain/index, logical primary/foreign keys, cardinality/order/comparer, source pointer, conversion/rejection rule and read-back assertion. Application closure checks are explicit; ObjectID is not stable identity or ordering.
3. Given source C revisions, their store/revision/request/receipt identifiers are immutable provenance only. A model/archive/domain IDs retain their owners. E.1 defines/reviews the exact preliminary host-context fields and namespace used by E.2/E.3 synthetic disposable fixtures, without claiming a host commit. Catalog cross-references all three namespaces without rewriting A or relabeling C receipts as host authority. After E acceptance, B.1 finalizes its separate host commit protocol consistently with these fields; incompatible changes require E re-review before affected B integration. E.3 does not depend on B.1 execution or acceptance.
4. Given long IDs/text, exact offsets/fractions, null/absent/empty/default arrays and full integer ranges, catalog owns originals losslessly via typed field or payload/reference/state columns. Proposed 64-hex surrogate hashes require length-delimited case/kind/full ID, full-value collision checks and domain comparer rules. ArcGIS Pro 3.6 BigInteger's safe integer range is not full signed Int64: out-of-range owners use exact reviewed text/payload or explicit unsupported, never round/cast silently.
5. Given geometry, a separately reviewed named profile declares CRS/local-unreferenced semantics, units, precision/resolution/tolerance, ring/direction/multipart/holes and curves/Z/M/empty/invalid rules. Unsupported values are diagnosed; no universal CRS/snapping or flattening. Draft/invalid and accepted datasets are separate; forwarded-defect state is not valid geometry. Linked PE is immutable; default PointIds remains an inspection limitation.
6. Given named Product/Winston/GIS reviewers, record catalog/profile version/digest and D01/D11 review disposition before downstream hosted writes. Open decisions and unsupported shapes remain visible; exact choices are not fabricated by story preparation. E.1 acceptance alone does not pass F01/F02/F03 or activate ownership.

## Tasks / Subtasks

- [ ] Obtain authorization to execute this catalog-authoring story and read actual manifests. (AC1-6)
- [ ] Produce one exact JSON catalog and readable ownership/conversion matrix. (AC1-4)
- [ ] Specify source/prototype/host metadata namespace and host-context handoff. (AC3)
- [ ] Document reviewed geometry profiles and unsupported semantic cases. (AC5)
- [ ] Validate JSON/schema coverage and obtain named catalog/profile acceptance. (AC6)

## Developer Context And File Map

Future NEW planning files: toolbox-fgdb-schema-v1.json and toolbox-fgdb-schema-mapping.md under planning-artifacts/toolbox/. Use actual accepted A manifests as source, not a second DTO format. No C# or hosted artifacts in E.1. Table lengths/precision and timestamp representations must support the installed SDK 3.6/net8 lane; any limited display field is labeled derived, never an owning truncated value.

## Test Requirements And Completion Evidence

Future catalog execution checks JSON syntax, complete record/property coverage, types/nullability/enums, references/comparers, namespace handoff and reviewed profiles. No build/host test or F01-F03 proof is required or claimed for this documentation deliverable. Completion requires the actual exact catalog, coverage report and named acceptance; this story specification is not that catalog.

## References

- [E requirement contract](13-5e-define-fgdb-schema-and-projection-mapping.md), [A evidence](13-5a-implementation-evidence.md), [decision gates](../../planning-artifacts/toolbox/toolbox-decision-register.md).
- [E.2 projections](13-5e-2-define-pure-projection-and-readback-verification.md), [E.3 hosted adapter](13-5e-3-define-isolated-hosted-schema-adapter.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).
- Esri [Pro 3.6 field types and safe integer limits](https://pro.arcgis.com/en/pro-app/3.6/help/data/geodatabases/overview/arcgis-field-data-types.htm), checked 2026-10-11; API limits are not catalog approval.

## Dev Agent Record

Specification only, 2026-10-11. No exact catalog/profile authored or accepted by preparation, no runtime authorization, code, GIS test, commit or deployment. Record future catalog evidence and reviewer decisions when separately executed.
