# 11.2 / 13.4A Contract Ownership And Identity Mapping

Date: 2026-10-11
Status: existing isolated mapping verified; documentation reconciliation complete
Branch: `refactor/toolbox-case-workspace`
Inspected checkpoint: `0a6188f467da81ccdbee639cd0c8954b1e727e85`

The request to resolve ownership does not require a second implementation: the isolated branch already contains the accepted [11.2 mapped amendment](../../implementation-artifacts/toolbox/11-2-toolbox-contract-amendment.md) and accepted [13.4A implementation](../../implementation-artifacts/toolbox/13-4a-implementation-evidence.md). Main-checkout preparation records predate that work. This record makes the delivered mapping explicit and verifies it against code. It does not close operational Story 11.2 or approve 13.4B.

## Ownership Resolution

| Owner | Owns | Does not own |
|---|---|---|
| Existing `ExtractionReviewPersistenceService` | Operational extraction/review JSON, raw/original/reviewed values, legacy saves, hashes and approval snapshots | Toolbox identity allocation or acceptance |
| Isolated 11.2 mapped contract: `CanonicalReviewContract` | Immutable projection of supplied JSON; canonical source/evidence keys, detected parcel candidates, rows/segments, memberships, links and extraction findings | Provider dispatch, a replacement writer, workflow approval, or automatic identity recovery |
| 13.3 `ToolboxCaseContext` | Environment/case/transaction context, selected preset/parcel IDs, linked-source identity and conservative access | Parcel inclusion, acceptance, or remote authorization |
| 13.4A `ToolboxParcelModel` and examination services | Case-scoped domain IDs, canonical-to-domain map, selection validation, inclusion history, human review, cumulative findings and local readiness | Changed-candidate matching, physical shared-boundary inference or durable storage |
| 13.4B reconciliation services | Explicit rerun reconciliation, physical boundary relationships, dependency impact and split/merge lineage | Automatic snapping, real GIS certification, production activation or durable restart |
| 13.5, future | Identity-map persistence/reopen, revision storage, FGDB ownership and authority cutover | Retroactive completion by the in-memory model |
| 13.8 / 13.9, future | Live Compare integration, packages/reports and fresh server authorization/handoff | Permission inferred from local acceptance |

The original operational 11.2 contract migration remains a separate story. The isolated amendment satisfies the explicitly mapped reuse path for 13.4A; it does not satisfy all operational normalization/save/load requirements. Existing source-requiredness and hash/approval behavior remain owned by operational services.

## Identity Mapping

| Input identity | Mapping / consumer | Rule |
|---|---|---|
| Context environment, case ID, transaction ID, normalized TR | `ToolboxCaseIdentity.From(context)` | Full tuple scopes the map; TR retains leading zeros. Operator/task/preset and filesystem name are not entity identity |
| `parcel_groups[].parcel_group_id` | `parcel:<key>` -> `ToolboxParcel.Id` | Canonical key and domain ID are distinct; display label/name/lot is not a key |
| `rows[].row_id` | `row:<key>` -> `ToolboxEntity.Id` | A review row is a source entity, not proof of a unique physical survey point |
| `segments[].segment_id` | `segment:<key>` -> `ToolboxEntity.Id` | Source segment remains independent of a later shared physical boundary |
| Row/segment `parcel_group_id` | Domain `ParcelId` through the parcel mapping | Target must be a known detected candidate; no first-parcel fallback for ambiguous data |
| Point/segment membership `entity_key` and `parcel_group_id` | Domain entity/parcel IDs plus sequence, direction and role | Membership references map keys; ordered closure may reuse an entity at a different position |
| `source_inventory[].source_id` | Canonical source/document scope ID | Preserved as a source key, not allocated through the parcel/row/segment map |
| `source_traces[].evidence_id` and `source_id` | Evidence lookup and source reference | Evidence key must resolve to a known source; aliases cannot contradict page/zone |
| `participants` / `identifier_pairs` object keys | Canonical participant / identifier-pair scope IDs | Preserved keys; optional `parcel_ids` contain canonical parcel keys mapped on consumption, not domain IDs despite the field name |
| `evidence_links` | Evidence key plus canonical parcel key and role | Preserve one item linked to multiple parcels; no mutable value duplication |
| `findings[].finding_id` / parcel keys | Canonical finding plus scoped domain links | Extraction findings remain evidence; examination history/dispositions are separate 13.4A records |
| Linked PE `ToolboxSourceReference` | Source ID, source environment/TR/transaction/revision | Retain originating identity and read-only scope; never remap as editable current-case geometry |

Canonical source keys are trimmed by the canonical reader and checked for nonblank, case-insensitively unique values within each kind. Domain map keys use a case-insensitive comparer and prefixes (`parcel:`, `row:`, `segment:`). Domain ID values are nonblank, trimmed and unique across all three kinds within the map.

The delivered `ToolboxCaseIdentity` record uses exact tuple equality for carried-forward maps. Callers must reuse the context's environment/case/transaction identity spelling and normalized TR; only the canonical TR-to-context check is case-insensitive. Case normalization changes require an explicit compatibility policy, not a newly allocated map. Canonical participant/pair spelling must also remain consistent in raw JSON consumers; case-insensitive identity checks are not a promise that every JSON property lookup normalizes spelling.

## Allocation And Reuse Rules

1. For an explicitly new import with no prior identity/acceptance claim, allocate one opaque ID per canonical parcel/row/segment key. Default implementation uses `Guid.NewGuid().ToString("N")`; tests may inject an allocator. These IDs are allocated, not derived from labels, coordinates, order, ObjectIDs, filenames or TR type.
2. For same-contract re-adaptation, carry the complete previous map. The exact key inventory must match; incomplete, foreign, surplus or duplicate-ID maps fail without returning a partial replacement. Rename/reordering alone retains mapped IDs.
3. For changed extraction inventories, use 13.4B's explicit reconciliation path. Do not rerun the new-import allocator to disguise missing identities or transfer acceptance to unmatched candidates.
4. Keep all detected candidates, including zero-geometry candidates. An ungrouped legacy single-parcel input may derive `parcel-001`; ambiguous multi-parcel input is rejected. Row and segment keys and meaningful order still must be supplied.
5. Geometry identity is not physical topology identity. Equal point labels/coordinates do not join parcels; B supplies explicit physical-boundary relationships and reviewed evidence.
6. Keep selection, inclusion, examination forwarding and valid-geometry acceptance separate. Identity allocation never grants edit or server authority.

### Worked Example

For case `(environment-A, case-A, transaction-A, 000000101)`:

| Canonical map key | Allocated domain ID (illustrative) |
|---|---|
| `parcel:group-1` | `opaque-parcel-A` |
| `row:row-1` | `opaque-row-A` |
| `segment:segment-1` | `opaque-segment-A` |

A participant with `parcel_ids: ["group-1"]` links to `opaque-parcel-A` when projected. A point membership with `entity_key: "row-1"` links to `opaque-row-A`. Changing `display_label` retains these IDs. Adding `row-2` is a changed inventory requiring reconciliation, not same-contract re-adaptation. Source/evidence IDs remain canonical keys.

## Actual Provider / Persistence Boundary

The mapped reader consumes explicitly keyed input; it is not a general legacy normalizer. Existing operational persistence can generate fallback row/segment IDs using source labels or indices. Those fallback mechanisms do not prove durable identity across a changed extraction. Upstream adapters must supply validated keys, candidate inventories, provenance and order before live Toolbox use.

`ToJson()` returns a defensive copy of the supplied snapshot, including unknown nested fields and the existing review hash. Derived typed defaults are not a new persisted canonical format. Source scalar aliases and inline legacy evidence remain preserved, but an indexed evidence-dependent check still requires resolvable `source_traces` and links. Geometry/authority/policy verification is explicit caller input and remains unproven for real production cases.

## Verification On 2026-10-11

Executed from the isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-currency/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
```

Build passed with zero warnings/errors; 125 focused tests passed. This includes canonical immutability, invalid references/order, single-parcel fallback, empty-geometry candidates, stable rename/reorder mapping, incomplete/foreign maps, duplicate allocated IDs, source identity retention, review/approval compatibility and B reconciliation regressions. No full-suite rerun, live workflow, FGDB round trip or independent B review was performed in this reconciliation task. Historical full-suite limitations remain in the implementation evidence.

Code anchors: `Workflow/Review/Canonical/CanonicalReviewContract.cs`, `Workflow/Toolbox/Model/ToolboxParcelModel.cs`, `Workflow/Toolbox/Model/ToolboxExamination.cs`, and their executable-harness tests under `ParcelWorkflowAddIn.Tests/Workflow/`.

## Resolved And Remaining

- D11 isolated ownership/reuse path and D07 local identity subset are resolved by the accepted amendment and delivered 13.4A, now freshly verified. No second approval of those existing decisions is required.
- Original operational 11.2 remains open; provider normalization and production writer cutover remain separate.
- D07 reconciliation policy was recorded as approved on the branch; 13.4B implementation is in review. Focused tests do not replace that review/acceptance.
- The actual next sprint boundary is 13.4B review, then 13.5 preparation subject to its persistence gates. Parent 13.4 remains in-progress.
