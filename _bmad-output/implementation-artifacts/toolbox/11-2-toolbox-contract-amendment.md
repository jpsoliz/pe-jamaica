# 11.2 - Isolated Toolbox Contract Amendment

Status: accepted as an isolated mapped contract on 2026-10-08; original operational 11.2 remains open.
Authorization: JotaPe requested amendment, implementation/tests, then 13.4A in that order on 2026-10-08.

This is an explicit mapped adaptation of 11.2, not completion or retirement of the operational story. All code, tests and evidence stay in `temp/toolbox-worktree`. Existing extraction routes, review writer, review hashes, approved snapshots, source requirements, UI and task routing remain unchanged.

## Ownership Map

| Owner | Responsibility |
|---|---|
| Existing review persistence | Existing extraction JSON, legacy review values, original/raw data, saves and legacy approval invalidation |
| 11.2 isolated canonical contract | Immutable source inventory, source trace, parcel candidate inventory, row/segment keys, ordered memberships, extraction findings, quality and lossless JSON snapshot |
| Existing Toolbox policy loader/resolver | Pinned stage classification, scoped applicability, required/optional/N/A and effects; never infer completeness from missing data |
| 13.4A | Case-scoped domain ID map, validated memberships/links, inclusion, cumulative findings, human review, fingerprints, local examination dispositions and readiness |
| 13.4B | Changed extraction reconciliation, shared physical boundaries, topology dependencies and split/merge lineage |
| 13.5 | FGDB, identity-map persistence/reopen and authority cutover |
| 13.8 / 13.9 | Actual Compare integration, current server authority, reports/packages and normal next-stage transition |

## Contract And Local Identity Decisions

- Canonical sources describe supplied and missing sources. Document requiredness belongs to explicit stage/preset policy; legacy PE's mandatory Survey Plan/Survey Sheet rule is unchanged.
- Canonical keys are nonblank and case-insensitively unique within their entity kind. Labels, coordinates and positions are not keys. Existing explicit group/row/segment IDs are reused. An ungrouped legacy single-parcel artifact may use `parcel-001`; ambiguous multi-parcel input fails rather than guessing from names.
- Detected parcel groups are independent of geometry. Retain zero-row/zero-segment candidates and arbitrary parcel counts; three is only a fixture example.
- Memberships have explicit positive sequence and validated forward/reverse direction. An explicit closing membership may reuse a point. Geometry labels shared across groups do not establish shared physical identity.
- Canonical JSON owns original, normalized, reviewed, manual and unknown nested fields without inventing approval. Source/evidence references and many-parcel links are explicit. Missing review fields remain unknown/unreviewed.
- 13.4A allocates domain IDs only for an explicitly new import. Same-contract re-adaptation requires the complete carried-forward case-scoped map; missing/cross-case/duplicate mappings fail atomically. This is acceptance of the local identity subset only, not GIS reconciliation policy.
- Readiness requires versioned geometry-policy tokens and full scope inventory. Synthetic tokens can exercise tests but do not certify real CRS/topology.
- Extraction mismatch needs correction; mixed errors need their extraction component corrected. Authorized, evidence-backed source defects may be recorded for the normal next stage. This never approves defective geometry.
- Partial LTD comparisons stay pending with a reason. Compute findings remain in the cumulative register in Compare. Tabular changes stale only consumers of the changed evidence; no geometry-write API is provided.

## Acceptance Checks

1. Given legacy JSON, when projected/serialized, all fields and the existing review hash survive unchanged; no operational writer or hash method changes.
2. Given explicit single, variable multi-parcel or empty-geometry groups, all candidates/entities/evidence survive immutable projection.
3. Given duplicate/blank keys or invalid references/order, projection fails without mutating input or producing a partial model.
4. Given renamed/reordered same-contract data and its identity map, IDs remain stable; incomplete/cross-case maps fail.
5. Given policy resolver outputs, local readiness rejects unresolved bindings, missing/stale reviews and unauthorized dispositions; report-only findings remain visible.
6. Given source defects, readiness distinguishes justified stage forwarding from valid geometry; extraction mismatch and integrity cannot be waived.
7. Given incomplete Compare data, outcomes and finding history survive while final readiness is pending.

## Implementation Plan

1. Add `Workflow/Review/Canonical/CanonicalReviewContract.cs` and focused executable-harness tests; leave existing model/persistence unchanged.
2. Verify canonical compatibility and immutability before implementing 13.4A services under `Workflow/Toolbox/Model/`.
3. Consume existing policy resolver types and the three binding bundles in tests. Keep unmapped checklist items unresolved, not passed.
4. Record focused/build/full-suite results separately. Do not close parent 13.4 or operational 11.2.

## Delivered Contract

`CanonicalReviewContract.Read(JsonObject)` is opt-in and has no production call sites. It owns immutable JSON elements and exposes typed source inventory, evidence traces, parcel candidates, entities, ordered memberships, links and extraction findings. `ToJson()` returns a defensive copy of the supplied artifact, not a new competing persistence format. Quality, global facts and provider extensions remain losslessly owned JSON.

The optional `source_traces` index uses stable `evidence_id` and `source_id` references. Existing `source_page`/`source_zone` and fixture `page`/`zone` aliases are supported; contradictory aliases fail. Inline/scalar legacy evidence remains in the snapshot. Unkeyed or ambiguous provider output needs an explicit upstream normalization adapter before it can satisfy Toolbox readiness; this change does not silently rewrite providers or mint identity from labels.

The local identity subset and ownership map above were used under the explicit ordered implementation request and accepted with scoped 13.4A signoff. D07 P01-P05 are separately approved as toolbox_d07_reconciliation_v1; actual GIS normalization/host proof and D11 production persistence cutover remain outside this approval. 13.4A consumes the contract directly; legacy model/writer/hash/approval code is unchanged.

Verification and residual limits: [13.4A implementation evidence](13-4a-implementation-evidence.md).

Review correction: optional `participants` / `identifier_pairs` inventories must be objects when supplied. Their optional `parcel_ids` fields must be arrays of unique, nonblank string keys referring to detected parcels. Malformed fields fail atomically before model adaptation; absent links and empty arrays remain representable. Legacy model/writer/hash behavior remains unchanged. This amendment is accepted for isolated reuse with 13.4A; no production writer cutover or original 11.2 closure is implied. Final evidence: 105 focused passes and explicitly retained full-suite limitations.
