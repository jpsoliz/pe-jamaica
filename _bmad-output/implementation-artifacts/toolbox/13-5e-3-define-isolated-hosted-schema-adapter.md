---
story_key: 13-5e-3-define-isolated-hosted-schema-adapter
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5e-define-fgdb-schema-and-projection-mapping
depends_on: [13-5e-2-define-pure-projection-and-readback-verification]
execution_gate: explicit-host-authorization-reviewed-schema-profile-and-e2-acceptance
implementation_authorized: false
---

# 13.5E.3 - Isolated Hosted Schema Adapter

Status: backlog
Specification: preparation and static review complete under documentation-only instruction; runtime not started. Tracked through parent 13.5; no child tracker key.

## Story

As a Toolbox developer, I want the reviewed schema materialized and read back in disposable FGDB fixtures, so that logical and geometry fidelity are demonstrated in ArcGIS Pro.

## Scope And Dependencies

Accepted E.1/E.2, D01/D11 schema review, explicit geometry profile and authorized isolated host/root are required. F07 is required before any experimental installation. C acceptance precedes persistence integration; D acceptance precedes legacy fixture materialization. Synthetic A/B state may exercise disposable schema without D. Owns hosted schema create/write/read-back, not recoverable host commit/activation; B owns those.

## Acceptance Criteria

1. Given approved host/root and catalog/profile, create only new disposable working/TR-<validated-number>.gdb targets, preserving leading zeros and physical separation from operational/legacy numeric GDBs. Never adopt, overwrite or infer target identity from a filename. Use installed SDK 3.6/net8.0-windows/x64 and SDK-safe threading/ArcPy boundaries.
2. Given E.2 plans, actual datasets/fields/types/lengths/nullability/indexes/domains and logical keys match catalog; validate complete application constraints on read-back rather than assume FGDB enforcement. Record full host/case/schema/profile and provenance bindings without claiming a B committed state from a synthetic marker.
3. Given reviewed coordinate fixtures, prove semantic geometry/dataset round-trip under exact CRS/units/precision/rings/multipart/holes/curves/Z/M profile. Unsupported/invalid/local-unreferenced cases remain explicit; no automatic snapping/flattening or headless-token proof. OID/file-byte equality is not geometry agreement.
4. Given raw/manual/unknown payloads, history, large IDs/numbers, offsets/default/null and retired/excluded relationships, reconstruction retains declared owning values. Missing/tampered assets/schema/datasets reject a clean result; no repair from legacy JSON or authority switch. Linked PE is read-only and forwarded defects cannot populate accepted geometry by themselves.
5. Given host/schema locks, GP/save failures or cancellation, report incomplete disposable fixture honestly and preserve evidence; no atomic host commit/recovery guarantee until B. Dispose ArcGIS resources/locks through correct host boundaries; tests inspect source/operational bytes unchanged.
6. Given completion, independent Product/Winston/GIS review accepts exact schema/profile and actual hosted evidence separately from pure results. Preserve baseline/applicable C/D/E groups and full-suite/default-array limits. E does not close F01/F02/F03, ownership activation, production deployment or parent 13.5.

## Tasks / Subtasks

- [ ] Confirm explicit host authorization, E.1/E.2 acceptance, named reviews and isolation/F07 where applicable. (AC1-6)
- [ ] Implement disposable schema and read-back adapter using reviewed E.2 plans. (AC1-2)
- [ ] Add real coordinate/profile, payload/history and missing/tampered hosted fixtures. (AC3-4)
- [ ] Capture locks/failure/cancellation and resource-disposal evidence. (AC5)
- [ ] Review actual headless/host lanes and obtain E.3 then E acceptance. (AC6)

## Developer Context And File Map

Future NEW hosted adapter under Workflow/Toolbox/Persistence/ArcGis/ and separate hosted scenario/evidence runner; headless tests under existing Tests/Workflow/. Enumerate the host test invocation and exact fixture root before execution; a console mock cannot prove ArcGIS behavior. Program.cs updates limited to headless registrations unless a separately reviewed host-runner seam is needed. No legacy output/source/package/approval/settings/DAML/remote integration.

## Test Requirements And Completion Evidence

Record installed SDK/runtime, profile/catalog digest, actual fields/row counts/relationships/coordinates, semantic digests, unsupported cases and host invocation/locks. Use separate pure/build/focused/full/host lanes; retain baseline 158/known207-stop/default-PointIds limits. Exact host runner is an implementation deliverable under authorization, not an invented working command. No GIS experiments run by preparation.

## References

- [E parent](13-5e-define-fgdb-schema-and-projection-mapping.md), [E.1](13-5e-1-define-exact-fgdb-catalog-and-geometry-profile.md), [E.2](13-5e-2-define-pure-projection-and-readback-verification.md).
- [F07/host evidence strategy](../../planning-artifacts/toolbox/toolbox-acceptance-strategy.md), [project rules](../../project-context.md), [preparation review](13-5deb-story-preparation-review-2026-10-11.md).
- Esri [Pro 3.6 field types](https://pro.arcgis.com/en/pro-app/3.6/help/data/geodatabases/overview/arcgis-field-data-types.htm); reference checked 2026-10-11, not target acceptance.

## Dev Agent Record

Specification only, 2026-10-11. Runtime/host authorization not granted; all tasks unchecked. No schema catalog acceptance, FGDB fixture, hosted tests, installation, commit or deployment created. Fill actual evidence after execution.
