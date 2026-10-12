---
story_key: 13-5e-2-define-pure-projection-and-readback-verification
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5e-define-fgdb-schema-and-projection-mapping
depends_on: [13-5e-1-define-exact-fgdb-catalog-and-geometry-profile]
execution_gate: explicit-runtime-authorization-and-e1-acceptance
implementation_authorized: false
---

# 13.5E.2 - Pure Projection And Read-Back Verification

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As a Toolbox developer, I want deterministic table/link plans and losslessness checks, so that schema conversion can be validated without writing a geodatabase.

## Scope And Dependencies

Requires accepted E.1 catalog/profile and explicit code authorization. Owns pure stable-key row planning, immutable payload references, reconstruction and logical comparison. No filesystem/GIS writes or owner activation. Host-context metadata is explicit external input with E.1 field ownership; pure fixtures never pretend it is a committed host revision. C/D acceptance is required later for affected persistence/import integration, not synthetic headless mapping.

## Acceptance Criteria

1. Given accepted A/B current/archive state plus reviewed catalog and explicit host context, plan every owned row/link/payload and preserve complete raw bytes, exact identities/order/comparers/audit/default states. Reject unknown/changed schema/version or ambiguous host/prototype/domain bindings. No new domain IDs/actions or reconstructed fresh permission.
2. Given planned/read-back records, validate every dataset/field, logical uniqueness/foreign key/cardinality/order and complete payload inventory before returning hydrated state. Compare raw values and exact audit separately from hashes. Null/absent/default/empty arrays and source/case/history remain unchanged.
3. Given long strings/IDs, surrogate collisions, unsafe numeric conversions, offset timestamps and unsupported shapes, use E.1 exact conversion or explicit typed unsupported failure; no silent casts, truncation, sorting by ObjectID or dropping unknown fields. Test Int64 values outside host-safe numeric range.
4. Given draft/invalid/forwarded-defect or linked PE input, do not plan accepted writable geometry from approval flags or selected parcels. Readiness equivalence is checked only for supported domain hashes with identical fresh external inputs; default PointIds stays inspection-only. Geometry token strings are not coordinate truth.
5. Given changed/missing payload, schema, row or relationship inventory, verifier rejects a clean result rather than repairing from legacy JSON/newest artifact. Derived compatibility output, if planned, is immutable, explicitly read-only and case/schema/revision/commit stamped; no production consumer wiring.
6. Given completed code, field coverage/golden row/negative/round-trip tests pass, all baseline 158 and applicable accepted predecessor groups remain passing, and actual pure/build/focused/full results and limits are recorded separately. Obtain scoped E.2 acceptance; headless pass is not hosted schema or geometry proof.

## Tasks / Subtasks

- [ ] Confirm code authorization and accepted exact E.1 catalog. (AC1-6)
- [ ] Implement pure row/link/payload projection and host-context binding validation. (AC1,3-4)
- [ ] Implement strict reconstruction/coverage/semantic comparison without I/O. (AC2,5)
- [ ] Add full manifest coverage, collision/numeric/default/audit and corruption fixtures. (AC1-5)
- [ ] Review and document actual lanes; obtain E.2 scoped acceptance. (AC6)

## Developer Context And File Map

Future NEW `Workflow/Toolbox/Persistence/Schema/ToolboxSchemaProjection.cs` and matching tests under Tests/Workflow/. Reuse A codec/manifests, canonical reader and reviewed E.1 catalog; no divergent serializer. Future UPDATE only Program.cs registrations after complete read, preserving substring-OR/fail-fast. No A schema/domain hash changes, operational output/approval adapters, package restore, settings/DAML or native/GIS writer.

## Test Requirements And Completion Evidence

Use E/C build/focused/full lane with separate obj-toolbox-schema/; test new schema filters explicitly because known full-harness Compare XAML stop can skip appended groups. Include shared archives, split/merge, inclusion/exclusion/retired identities, unknown provider data, long IDs, offset/fraction precision and linked PE. Completion requires actual coverage/round-trip evidence, review and acceptance; no tests executed by specification preparation.

## References

- [E parent](13-5e-define-fgdb-schema-and-projection-mapping.md), [E.1](13-5e-1-define-exact-fgdb-catalog-and-geometry-profile.md), [E.3](13-5e-3-define-isolated-hosted-schema-adapter.md).
- [A evidence](13-5a-implementation-evidence.md), [project rules](../../project-context.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).

## Dev Agent Record

Specification only, 2026-10-11. Runtime authorization not granted; tasks unchecked. No mapper, build/tests, GIS writes, commit or deployment created. Fill execution logs/results/files after authorization.
