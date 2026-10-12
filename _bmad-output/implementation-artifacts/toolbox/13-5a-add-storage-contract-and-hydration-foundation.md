---
story_key: 13-5a-add-storage-contract-and-hydration-foundation
track: toolbox
story_type: code
created: 2026-10-11
parent_story: 13-5-add-versioned-toolbox-workspace-persistence
depends_on: [13-4-add-single-and-multi-parcel-toolbox-model]
execution_scope: isolated-headless-bundle-codec-only
baseline_branch: refactor/toolbox-case-workspace
baseline_commit: 0a6188f467da81ccdbee639cd0c8954b1e727e85
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.5A - Storage Contract And Hydration Foundation

Status: done
Prepared: 2026-10-11 at JotaPe's request; no code implemented by preparation.
Tracking: detailed executable child tracked through parent 13.5; no additional sprint key. Parent remains backlog for its larger gated persistence scope.

## Story

As a developer of the transaction case workspace,
I want a versioned, validated byte-bundle representation of the accepted in-memory A/B state,
so that later persistence can restore identities and review history without recreating events or making stale approvals current.

## Execution Boundary

This slice is a pure capture -> encode -> decode -> hydrate pipeline. It has no filesystem, ArcGIS, HTTP or operational call sites. Work only in `temp/toolbox-worktree` on `refactor/toolbox-case-workspace`; preserve checkpoint plus uncommitted accepted R1-R8 corrections. It is ready for isolated headless implementation, not a published production storage contract or an authorization to start the full parent.

The contract below defines the prototype representation and validation rules. D01/D11 production schema/ownership review, D06 writer policy and F01/F02/F03/F07 are not closed by it. They do not block this no-writer foundation. Before implementation, verify its protected-boundary checklist; do not silently expand scope when a fixture cannot be represented.

Included: explicit DTOs; bounded strict JSON codec; canonical/policy payload inventory; normalized archive references; narrow validated domain restoration; focused malformed-input and round-trip tests; field mapping documentation.

Excluded: CaseStore, file save/load, paths/leases/manifest pointers, commit journal, durable receipts, legacy import/normalization, FGDB tables/geometry adapters, snapshots/packages, projections, authority cutover, UI, presets, source dispatch, policy changes and remote actions. Pending reconciliation proposals/removal audit are not captured in v1; capture rejects an explicitly supplied pending proposal rather than dropping it. Do not infer pending proposals from the accepted snapshot. Preserve only audit actually retained by the delivered model; B does not retain boundary-removal audit separately.

## Acceptance Criteria

1. Given a supported A model/register or B reconciliation snapshot plus current check reviews and available policy assets, when captured and encoded, then one `toolbox_workspace_storage_v1` prototype bundle contains the complete declared field map below. B's separately supplied model/register must match its snapshot; inconsistent inputs fail. A is supported with no reconciliation wrapper and no invented revision, provided historical evidence closure is available. A state whose source replacement removed referenced historical evidence is explicitly unsupported in v1, not silently stripped or repaired.
2. Given a bundle, when decoded with an externally supplied expected case identity, then the codec validates schema, mode, versions, sizes, duplicate fields/IDs, enums, references, archive DAG and checksums before returning a fully owned hydrated result. Any structural failure returns typed diagnostics and no usable partial domain object; no new-import fallback, silent defaults for required fields or newest-artifact heuristic is used.
3. Given current and archived model state, when hydrated, then the full case tuple, source snapshot, complete case-insensitive identity map, ordered memberships, inclusion/history, linked sources and nullable relations are restored without entity allocation, event creation, clock reads or permission grants. Derived parcels/rows/segments/memberships are reconstructed from canonical source plus the existing map, not persisted as competing owners.
4. Given cumulative findings, human reviews, saved decisions and live check reviews, when restored, then all original fields, timestamps/offsets, fingerprints, reasons and array order survive. Do not call RecordFinding, RecordHumanReview, RecordDecision, Start or Accept to hydrate. Historical evidence/parents may resolve in same-case archives; they need not exist in today's active model. Stale proof stays stale.
5. Given B state, when restored, then rerun candidates, policy/source tokens, boundary/adjacency/dependency bindings, lineage, ordered applicability, barriers, input bindings, accepted operations and accepted dispositions survive. An accepted operation remains an in-memory replay record, not a durable disk receipt. Historical base fingerprints/operation digests are preserved, not recomputed as if their proposal or historical register were available.
6. Given repeated archive ancestry, when encoded/decoded, then each unique model state is represented once and ordered archive references remain intact. Missing, foreign-case, conflicting or cyclic references fail; valid repeated references do not grow exponentially. Snapshot IDs are content-addressed representation IDs, not parcel IDs or committed disk revisions.
7. Given canonical unknown/provider fields and exact policy bytes, when round-tripped, then canonical payload values and supplied snapshot bytes remain owned and unchanged; policy bytes remain byte-identical. Transport SHA-256 and bundle IDs are separate from existing semantic fingerprints and arbitrary source/geometry tokens. Embedded pins/checksums are not a trust root or evidence of authenticity. Policy activation requires an externally trusted matching pin and the existing loader; absent assets/trust are reported as unavailable, not silently approved or fetched.
8. Given structurally valid blocked/pending state, incomplete dependency coverage, unresolved applicability, empty geometry or boundary conflicts, when decoded, then those states remain representable and inspectable. Structural corruption is rejected; domain readiness failure is retained. Forwarded-with-recorded-defects never becomes valid-geometry approval.
9. Given original/hydrated models and identical external policy, scopes, live context, authority, geometry token/verification and evaluation time, when evaluated, then inclusion, canonical, evidence, parcel, dependency and B snapshot fingerprints and readiness results are equivalent. A hash compatibility remains intact. Changed-source, changed-tabular, changed-inclusion and changed-policy fixtures still invalidate the same consumers after hydration. Equality of hashes alone is not losslessness: assert mapped state and raw payloads separately.
10. Given a decoded bundle under a different operator/task or without local permissions, when a caller evaluates it, then only that caller's fresh context and trusted inputs can authorize local work. The bundle cannot construct a context, restore edit flags/credentials, enable remote actions or confer current approval from saved decisions. Historical actors are data, not active authority.
11. Given input at/over documented limits or adversarial graphs, when parsed, then bounded traversal and typed limit diagnostics prevent recursion overflow and uncontrolled expansion. No truncation, topology limit of three parcels or dropping unknown canonical values is permitted. Validate dictionary duplicates with domain comparers before construction.
12. Given the implementation, when verified, then new named headless groups exercise A/B and the negative matrix below, all 135 predecessor focused tests remain passing, build results are reported separately and full-harness fail-fast limitations remain explicit. No FGDB recovery, file durability or full parent completion is claimed.

## Tasks / Subtasks

- [x] Add explicit versioned code DTOs and field-map tests/manifest following this story's contract; record each required/nullable field and its sole source, without a second prose schema. (AC1,3-7)
- [x] Implement capture and deterministic encoding, canonical/policy payload checksums and normalized iterative archive traversal. Reject contradictory inputs and unsupported pending proposal capture. (AC1,6-7,11)
- [x] Implement bounded duplicate-aware parsing, typed diagnostics, reference closure and structural validators before any domain result is returned. (AC2,5-8,11)
- [x] Add narrow validated model/register restoration factories; reconstruct projections from canonical/map while preserving historical evidence and original proof bindings. Keep all existing action APIs strict. (AC3-5,8,10)
- [x] Implement decoding/hydration and optional externally trusted policy resolution without context construction or file/network access. (AC2-8,10)
- [x] Add state/raw-byte round-trip, fingerprint/readiness equivalence and post-hydration invalidation fixtures, plus all malformed/limit/archive tests below. (AC1-11)
- [x] Register tests; run build, focused groups and full harness; independently review restoration and protected boundaries, document actual counts and limits. (AC12)

## Prototype Storage Contract

This section is the contract source for this slice. Do not maintain a different schema in a second document. Implementation may add a machine-readable fixture/field manifest tested against it. Incompatible changes require a different version and story update; no permissive schema migration in v1.

### Envelope And API

Proposed API responsibilities (names may follow local conventions): Capture accepts a typed input containing model, register, optional B snapshot, current check reviews, supplied policy bytes and nullable PendingProposal (`ToolboxReconciliationProposal`). A non-null PendingProposal returns unsupported_capture with no bundle; proposal persistence is not implemented. Encode returns owned UTF-8 bytes; Decode accepts bytes, required expected case identity and optional externally trusted policy pins. Its result contains restored model/register, optional reconciliation snapshot, current check reviews, owned payload assets and typed diagnostics. It does not expose a case context or a committed flag. Validation helpers are pure and available to both capture and decode.

| Required top-level field | Type / ownership |
|---|---|
| `contract` | Exact string `toolbox_workspace_storage_v1` |
| `schema_version` | Integer `1` |
| `storage_mode` | Exact string `prototype_bundle`; rejects active/committed claims |
| `case` | Object: `environment`, `case_id`, `transaction_id`, `transaction_number`; full exact tuple matching delivered `ToolboxCaseIdentity` |
| `fingerprint_contracts` | Sorted string array containing the fixed supported marker set listed below; not a hash algorithm upgrade or a policy publication |
| `root_model_id` | Reference to an entry in `models` |
| `models` | Array of unique model-state records with `snapshot_id`; references resolve within this bundle only |
| `examination` | Register object: nullable `case`, arrays `findings`, `human_reviews`, `decisions`; null case valid only for the empty unbound register |
| `current_check_reviews` | Ordered array, explicitly empty allowed; carries reviews not yet captured by a saved decision |
| `reconciliation` | Null for A; B object with fields mapped below, referencing root model and top-level examination without duplicating them |
| `payloads` | Array of unique `{payload_id, kind, utf8_base64, sha256}`; kind is `canonical` or `policy`; sha256 is lowercase 64 hex over decoded bytes |
| `policy_assets` | Array of `{pin, payload_id}`; pin is `{policy_id, policy_version, sha256}`. Empty allowed; absence does not imply a verified policy |

Each model entry contains `snapshot_id`, `case`, `source_payload_id`, `identity_map`, `inclusions`, `inclusion_history`, `linked_sources`, and nullable `relations`. Source bytes are UTF-8 of the supplied canonical `JsonSnapshot`, not an extraction rerun or regenerated geometry. Candidates in reruns use `candidate_payload_id`. Keep exact payload bytes alongside the defensive `CanonicalReviewContract.Read` projection; do not replace captured bytes with the reader's reserialized output.

Fixed marker set: `toolbox_canonical_inputs_v2`, `toolbox_canonical_dependencies_v2`, `toolbox_dependencies_v2`, `toolbox_evidence_inputs_v2`, `toolbox_evidence_dependencies_v1`, `toolbox_finding_dependencies_v1`, `toolbox_geometry_dependencies_v1`, `toolbox_human_source_v1`, `toolbox_parcel_decision_dependencies_v1`, `toolbox_readiness_inputs_v2`, `toolbox_readiness_dependencies_v1`, `toolbox_reconciliation_base_v2`. All bundles declare codec support for this set, including A where B markers are unused. Existing hashes without explicit markers and opaque stored historical proof strings are preserved; their absence of a version tag is not permission to rebind them. Dependency inventory Version and reconciliation policy are separate retained fields. Proposal hashing is outside this slice.

Records use explicit lower `snake_case` fields for each source property in the mapping below. Define enums as exact lower `snake_case` names of delivered members, never numeric values. Unknown wrapper/record fields and duplicate JSON properties fail in v1; canonical payload unknown fields are preserved. Policy payload parsing remains the existing loader's contract. Dictionary entries are objects with original key spelling; case-insensitive domain dictionaries must reject aliases such as `row:x`/`ROW:X`, retain spelling, and reconstruct the existing comparer.

All required properties must be present. Required arrays/dictionaries permit explicit empty where domain permits; missing/null is not empty. Nullable source properties remain null rather than empty strings. Preserve default-vs-empty optional collections: in particular optional default `PointIds` uses explicit null, empty uses `[]`; nullable `GeometryFingerprints` uses null vs `{}`. Other default arrays are not automatically normalized; declare their representability explicitly in field-map tests and reject unsupported default inputs. DateTimeOffset values use round-trip `O` text with original offset/precision. Do not trim or case-normalize historical values on load.

### Field-To-Source Map

Source property names below name the complete retained fields; each maps to its snake_case field. No new semantic interpretation is introduced.

| Source owner | Retained properties / transformation |
|---|---|
| `ToolboxParcelModel` | Case, Source -> source_payload_id, IdentityMap (Case, Ids), Inclusions, InclusionHistory, LinkedSources, Relations. Parcels/Rows/Segments/PointMemberships/SegmentMemberships derive from source + map |
| `ToolboxInclusionRecord` | ParcelId, State, Actor, At, Reason, EvidenceId, PolicyVersion |
| `ToolboxSourceReference` | SourceId, ServerEnvironment, TransactionId, TransactionNumber, nullable RevisionId; read-only behavior is derived |
| `ToolboxModelRelations` | Policy, SourceHashes, Boundaries, Dependencies, Archives -> ordered archive_model_ids, Lineage, Applicability, ReadinessBarriers, HumanInputBindings, FindingInputBindings, nullable Adjacency |
| `ToolboxReconciliationSnapshot` | Revision, Model -> root_model_id, Register -> top-level examination, Reruns, Lineage, Operations, AcceptedDispositions. Fingerprint is derived, not an authorization flag |
| `ToolboxRerunBatch` | Case, BatchId, RunId, ToolVersion, ProviderVersion, SourceHashes, BaseRevision, BaseFingerprint, CandidateDigest, ReconciliationPolicy, ReviewPolicy, Status, Candidate -> candidate_payload_id |
| `ToolboxAcceptedOperation` | OperationId, Digest, ResultRevision; in-memory replay only |
| `ToolboxAcceptedDispositions` | OperationId, Digest, ParentRevision, ResultRevision, AcceptedAt, Dispositions |
| `ToolboxCandidateDisposition` | Kind, nullable OldId/IncomingKey/OutputKey, Choice, Actor, Reason, EvidenceId, ExplicitIdentity |
| `ToolboxAppliedLineage` / `ToolboxParcelLineage` | Proposal, ChildIds; proposal retains OperationId, Case, Kind, ParentRevision, ResultRevision, ParentIds, ChildKeys, Actor, Reason, EvidenceId, ChildInclusions |
| `ToolboxHistoryApplicability` | RecordId, OriginalRevision, TargetParcelKeys, Disposition, Actor, Reason, EvidenceId; accumulated order and latest-wins behavior retained |
| `ToolboxPhysicalBoundary` / member | Id, Case, Revision, GeometryDigest, GeometrySemantics, SemanticsVerified, EvidenceIds, Members, Consistency, Actor, Reason, PolicyVersion, PointIds; member ParcelId, SegmentId, Sequence, Direction, Role |
| `ToolboxAdjacencyReview` | Case, ParcelKeys, Complete, EvidenceId, Actor, Reason, PolicyVersion |
| `ToolboxDependencyInventory` | Case, Version, Complete, Nodes, Edges; node Id, Kind, ReferenceId, nullable CheckBinding/DecisionParcelId; edge ProducerId, ConsumerId |
| `ToolboxDependencyCheckBinding` | Stage, ItemId, CheckId, ScopeKind, ScopeId, Revision, InputFingerprint, GeometryCheck |
| `ToolboxFinding` / event | Id, Origin, ParcelIds, DocumentId, nullable Page, Zone, ExtractionCorrected, GeometryDefect, History, RuleId, Code, Severity; event Stage, Actor, At, Status, Reason, EvidenceId, InputFingerprint, RuleVersion, PolicyVersion, DependencyFingerprint |
| `ToolboxHumanReview` | EvidenceId, RawValue, ReviewedValue, CaptureMethod, Presence, Accuracy, Stage, Actor, At, Reason, InputRevision, SourceFingerprint |
| `ToolboxCheckReview` | ResolutionFingerprint, Outcome, Reason, EvidenceIds, Actor, At, nullable FindingId, GeometryCheck, EvidenceFingerprint, nullable GeometryFingerprints, CanonicalInputFingerprint, DependencyFingerprint |
| `ToolboxReadinessSnapshot` | Case, Stage, Status, GeometryApproved, Fingerprint, Actor, At, Detected, Included, Excluded, Unresolved, CurrentAccepted, Reasons, Findings, Reviews, ParcelDispositions; derived Route/RemoteActionAllowed omitted |
| `ToolboxParcelDisposition` | Case, Stage, Policy, ParcelId, Fingerprint, Ready, Actor, At, Reasons; IsCurrent is behavior, not data |
| `ToolboxPolicyPin` | PolicyId, PolicyVersion, Sha256; policy objects derive only from supplied bytes plus external trusted pin |

Saved decision snapshots contain their own historical findings/reviews/parcel dispositions. Do not replace these with today's top-level register inventories. Reconciliation lineage and relation lineage are separately retained because both contribute to delivered behavior; validate their exact ordered correspondence in the accepted root snapshot without silently merging arrays. Archives keep their own earlier lineage prefixes.

### Archive IDs And Encoding

Traverse the archive graph iteratively with identity-based visitation for input objects, then topologically encode children before parents. Compute each `snapshot_id` as `model-` plus SHA-256 of its deterministic DTO record excluding snapshot_id, including ordered archive IDs. Same-content states deduplicate; different content must never reuse an ID. IDs have no disk-commit meaning. Payload IDs similarly use kind plus content checksum. Compare full encoded content on an ID collision and reject disagreement.

For deterministic DTO bytes, sort object keys ordinally, preserve all array order, use invariant JSON scalar encoding and consistent escaping; content hashing uses that same implementation. Decode recomputes model IDs in DAG order and payload IDs from verified bytes; an ID/content mismatch fails. Encode(Decode(bytes)) must preserve each payload and retained value and emit deterministic normalized wrapper bytes; arbitrary input wrapper whitespace need not survive. Neither this transport representation nor its checksum replaces `ToolboxSemanticHash` or any existing domain fingerprint version.

### Structural Validation And Trust

Validate entire reference closure first, then construct model states in DAG order, then register/reconciliation wrapper. Require exact case equality for current/archive models, maps, dependencies, lineage and saved decisions. Linked source references retain their own originating environment/TR: do not reject them merely because they reference another case.

Each identity map must exactly cover the canonical parcel/row/segment keys with unique nonblank trimmed domain IDs. Every model restoration factory independently requires canonical TransactionNumber.Trim() to match Case.TransactionNumber case-insensitively, as Adapt does; agreeing wrapper/map cases cannot excuse a different canonical TR. Current inclusion inventory exactly covers active parcel IDs; history may name retired IDs known in archives. Preserve many-parcel links and original source keys. Restore findings/corrections/decisions against the appropriate same-case historical inventory, not exclusively active inventory. Do not treat currently unmatched old proof fingerprints, NotApplicable targets or incomplete coverage as corrupt data.

A has no model archives when Relations is null. If ReAdapt removed an evidence/document/entity referenced by retained inclusion/finding/human/decision/live-review records, capture returns unsupported_capture and decode returns missing_reference; neither returns a partial model. The codec cannot recover absent historical source bytes or synthesize a B archive. Add a negative A source-replacement fixture to make this limitation explicit. A stale fingerprint whose actual evidence remains available is supported; unavailable historical evidence is a different condition. Relaxing this boundary or accepting externally supplied historical A models requires a later version/refinement.

Use the canonical reader and boundary structural invariants only after work-budget admission, retaining reported readiness conflicts. For dependencies, use indexed storage-local structural validation equivalent to the existing case/reference/kind/binding/DAG checks; do not call its fingerprint-producing ActualInput traversal during hydration. Keep action validators and fingerprint algorithms unchanged. Validate defined enums, check-binding shape/stage/nonblank identifiers and true one-to-one source-hash coverage. Do not demand every historical batch match today's revision/success status.

Accepted root relations.Lineage and snapshot.Lineage must be equal ordered sequences. Each applied lineage pairs ordered ChildKeys and ChildIds with equal lengths and unique keys/IDs, resolving the pair in current or same-case archived maps. ParentIds resolve in prior history; parent/result revisions must differ, not masquerade as disk pointers. Operations and AcceptedDispositions pair one-to-one by OperationId with matching Digest/ResultRevision and retained order; audit ParentRevision differs from ResultRevision and AcceptedAt is retained. References to older source/output keys are validated against historical inventories, not remapped to today's keys. Audit fields remain historical: the full sealed proposal is unavailable, so do not claim to recompute its digest or certify authenticity.

Policy assets must match their declared byte checksum; a trusted requested pin mismatch fails policy resolution. No asset means policy unavailable, not corrupt model state. Externally requested trusted policy loading uses `ToolboxReviewPolicyLoader.Load(bytes, trustedPin)`; a file-provided pin cannot authorize itself. Geometry SemanticsVerified and saved Ready flags are retained history, not proof that a newly loaded real FGDB was verified. Fresh readiness/authority stays outside this codec.

Typed diagnostics carry code, record path and a sanitized reason, without source values or credentials. Minimum codes: unsupported_contract, unsupported_version, unsupported_mode, unsupported_capture, malformed_json, duplicate_field, invalid_value, duplicate_identity, foreign_case, missing_reference, cyclic_archive, checksum_mismatch, inconsistent_state, limit_exceeded, policy_unavailable and policy_untrusted. Structural failure returns no hydrated aggregate; policy unavailability can accompany a structurally restored inspectable aggregate but cannot yield an activated policy.

### Parsing Limits

Prototype defaults, enforced before expensive expansion: 64 MiB encoded bundle; 32 MiB aggregate decoded payload bytes; 8 MiB per canonical payload; 256 KiB per policy payload (existing loader); JSON depth 64 for wrapper/canonical, policy depth remains loader-owned; 512 unique models; 100,000 aggregate collection entries and 100,000 aggregate dependency edges across unique models; 4,096 characters for identity/version/reference tokens and 1 MiB UTF-8 for individual free-text values. Count all nested decision/history records, dictionary entries and archive references, not only top-level arrays. Base64 byte budgets are checked before allocating decoded buffers; reject invalid UTF-8 rather than replacing bytes with characters. Over-limit inputs fail, not truncate; these are prototype resource bounds, not cadastral cardinality rules.

Budgets also include canonical unknown nested properties/array elements/keys/strings. A payload may be stored once, but charge its parsing/projection cost for every distinct model/candidate that materializes it. Admit construction only after a checked conservative work estimate: sum `8 * N * N` per canonical projection, where N counts all canonical JSON property/array entries, plus indexed closure operations/graph references. Default total budget is 100,000,000 estimated work units. Also cap aggregate canonical projection bytes at 256 MiB across all model/candidate materializations. Arithmetic overflow is limit_exceeded. This preflight bounds the existing reader's repeated searches before invoking it; it is a conservative admission rule, not a timing SLA. Indexed closure validation accounts for dictionary/graph operations and does not invoke semantic fingerprint computation. Add adversarial many-entity/many-trace/dense-graph and shared-payload fixtures that fail preflight before expensive constructors execute.

Keep one immutable limits object; tests may inject smaller limits to exercise exact/over boundaries cheaply. Runtime production limit policy is deferred. Archive depth uses iterative DAG validation, not the JSON nesting budget. Count shared states once but every actual projection; bound canonical equality/collision comparisons within admitted byte/work budgets. Existing fingerprint algorithms remain unchanged and are called only in explicit parity/evaluation tests after hydration, not implicitly for structural admission.

## Code Boundaries And Preservation

Paths relative to `src/ParcelWorkflowAddIn/` in the isolated worktree.

| Planned file | Change and preservation |
|---|---|
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspaceStorageDocument.cs` | DTOs, limits, capture input/hydrated result and typed diagnostics; no context/store authority |
| NEW `ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspaceStorageCodec.cs` | Pure byte capture/encode/decode, reference normalization and validation. Split a helper only when complexity warrants it |
| UPDATE `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelModel.cs` | Private constructor currently projects source/map and Adapt initializes included/history. Add narrow validated restoration for supplied inclusions/history/linked sources/relations; preserve strict Adapt/ReAdapt and allocation rules |
| UPDATE `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxExamination.cs` | Private populated register; Record APIs may rebind proofs. Add narrow validated historical restoration without action replay; preserve all recording/readiness/hash rules |
| REUSE `ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxReconciliation.cs`, `ToolboxParcelLineage.cs`, `ToolboxBoundaryDependencies.cs` in that same Model directory | Use public record contracts and structural validators unchanged; no new proposal acceptance or algorithm changes |
| REUSE `ParcelWorkflowAddIn/Workflow/Review/Canonical/CanonicalReviewContract.cs`, `ParcelWorkflowAddIn/Workflow/Toolbox/Policy/ToolboxReviewPolicyLoader.cs` | Defensive canonical parser and externally pinned policy loader; no alternate extraction or policy contract |
| NEW `ParcelWorkflowAddIn.Tests/Workflow/ToolboxWorkspaceStorageTests.cs` | Headless fixtures named `toolbox storage ...`; reuse existing samples/patterns without altering their expectations |
| UPDATE `ParcelWorkflowAddIn.Tests/Program.cs` | Append explicit named registrations only; preserve substring-filter OR semantics and fail-fast runner |

Model/register factories must protect invariants even if called independently of the codec. They accept fully owned validated source/map/history, not raw permission booleans. Prefer an internal factory exposed to the existing friend test assembly; avoid widening mutation/constructor access simply to satisfy a serializer. Reread each UPDATE source and relevant tests fully before editing. No edits to context, policy evaluator, legacy persistence/approval, layout/package, output adapter, UI/DAML, settings or services are expected.

Use existing .NET 8/x64, System.Text.Json and immutable collection patterns. No new dependency, SDK upgrade or deployment required. Official [immutable serialization documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/immutability) explains constructor binding; explicit DTO restoration still owns validation here. Set a deliberate depth bound rather than relying on the [MaxDepth default](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocumentoptions.maxdepth?view=net-8.0). These references were checked 2026-10-11; do not use APIs introduced after the installed target.

## Test Matrix And Exit Evidence

| Named group / fixture | Required assertion |
|---|---|
| A lossless single/multi/empty | 1, 3 and variable-count parcels; empty geometry; complete map/memberships/inclusion/manual/unknown provider state; null relations and empty unbound register |
| B split -> merge -> regenerated keys | Shared historical models deduplicate; retired/excluded parents and archived correction applicability remain known; original arrays/audit/times preserved |
| Current vs historical proofs | Live reviews not yet in decisions, old decision-embedded findings/reviews, Pending LTD, MissingFromSource, source defects and stale approvals are retained |
| Fingerprint/readiness parity | Compare mapped state and raw payloads first, then evaluate with identical external inputs/time; A compatibility and B hashes/readiness match |
| Post-hydration invalidation | Supporting source hash/evidence link/tabular/inclusion/policy changes invalidate the same consumers; geometry-only canonical behavior preserved |
| Malformed closure | Missing/duplicate maps, ID aliases, foreign cases, canonical TR mismatch despite agreeing wrappers, unknown enums/kinds, invalid binding, duplicate JSON properties, missing payload/archive, cycles, lineage/audit mismatch; no partial aggregate |
| Unsupported capture | Explicit PendingProposal; A ReAdapt with removed historical evidence; typed failure without lost history or synthesized archives |
| Valid blocked state | Incomplete dependency coverage, unverified boundary semantics, unresolved applicability and absent trusted policy load successfully for inspection, without becoming Ready |
| Bytes/trust | Canonical unknown nesting, labels and manual strings unchanged; exact policy bytes survive; tampered payload checksum rejected; embedded self-approved pin never activates |
| Resource boundaries | Exact/over byte, depth, collection, string, model, edge, projection-byte and work budgets; adversarial canonical/dependency inventories rejected before expensive projection; repeated ancestry/deep chain remain bounded |
| No capability restoration | Fresh denied/Compare context stays denied/read-only; saved actor/Ready/SemanticsVerified/operation record cannot grant edit/remote/commit authority |

After implementation, from isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-storage/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

Record actual new/existing counts, no-warning build result, independent code review and full-harness failure location. Predecessor evidence is 135 focused passes; full harness passed 207 before known Compare XAML failure, with historical manifest-access issue also recorded. Those are not this slice's results. Hosted GIS, file save/reopen/crash recovery and authority-switch proof belong to subsequent work. A completion does not complete parent 13.5.

## References

- [Parent 13.5](13-5-add-versioned-toolbox-workspace-persistence.md): larger persistence requirements and retained gates.
- [Accepted B fixes](13-4b-review-fixes-2026-10-11.md): R1-R8 and verification limits.
- [Ownership/identity mapping](../../planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md): accepted local reuse; later current acceptance supersedes its historical B-pending notes.
- [Architecture](../../planning-artifacts/toolbox/toolbox-architecture.md), [migration](../../planning-artifacts/toolbox/toolbox-parallel-migration-plan.md), [decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md), [project rules](../../project-context.md), [deferred work](deferred-work.md).

## Dev Agent Record

### Agent Model Used

Codex; BMAD isolated story implementation with independent source analysis and iterative code review.

### Debug Log References

Implemented against checkpoint `0a6188f` plus accepted uncommitted R1-R8 fixes. [Implementation/verification evidence](13-5a-implementation-evidence.md) records the red checks, final commands, review corrections and limits. Final build: zero warnings/errors. Focused: 158 passed (135 preserved plus 23 new storage groups). Full harness: 207 passed then known Compare XAML assertion at line 83; later tests did not execute. No full-suite green claimed.

### Completion Notes List

- Refined the requested foundation into an executable no-writer byte-codec slice; prototype contract is defined here, not asserted as accepted production schema.
- Read model/register/canonical sources and independently analyzed reconciliation/lineage/dependency/policy contracts. Protected operational ownership and all parent feasibility gates.
- Independent preparation review identified four gaps; work admission, canonical TR validation, unavailable A-history rejection and typed pending-proposal rejection are corrected. Follow-up found no remaining specification blocker. Runtime enforcement/performance are still implementation tests, not proven results.
- Implemented explicit no-writer DTOs, deterministic byte encoding, content-addressed archive references, validated historical hydration and externally trusted policy loading. All seven tasks completed; JotaPe explicitly accepted this isolated no-writer scope on 2026-10-11. Story done; parent disk/FGDB scope and all documented limits remain open.
- Locked 32 record field manifests, their type/nullability signature and 14 enum wire/numeric sets. No automatic domain-shape expansion into v1.
- Independent implementation findings and follow-ups corrected with targeted regressions; final narrow review found no remaining findings. See evidence for admission/accounting, historical reference and audit fixes.
- Prototype comparer profile: identity maps/inclusions/source hashes use OrdinalIgnoreCase; child inclusions, geometry proof maps and input-binding dictionaries use ordinal/default string equality. Unsupported capture comparers fail instead of silently changing semantics.
- Historical CorrectionId checking hashes immutable recorded review data only, to validate existing applicability references; wrapper-byte work is precharged. Consumer/readiness fingerprints are not refreshed during hydration.
- Default PointIds remains default, distinct from empty. The pre-existing domain hash cannot serialize that default state; both original and hydrated hash calls fail identically. It is inspectable, not newly certified for readiness; no hash-contract change made.
- No new tracker key, source normalization, disk store, FGDB, authority switch, deployment, commit/merge or remote operation added. Parent persistence gates and operational 11.2 remain open.

### File List

- NEW `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspaceStorageDocument.cs`
- NEW `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxWorkspaceStorageCodec.cs`
- NEW `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxStorageJson.cs`
- NEW `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Persistence/ToolboxStorageValidation.cs`
- UPDATE `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxParcelModel.cs` (Restore only)
- UPDATE `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/ToolboxExamination.cs` (Restore only)
- NEW `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxWorkspaceStorageTests.cs`
- NEW `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxReconciliationStorageTests.cs`
- UPDATE `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs` (23 appended registrations)
- This story and NEW `13-5a-implementation-evidence.md` in this directory.
- UPDATE Toolbox tracker comments, parent Dev Agent Record, Epic 13 current summary, decision/migration follow-up, project-context and deferred work; main-checkout `docs/project-management/CURRENT_SPRINT.md` navigation only.

## Change Log

- 2026-10-11: JotaPe explicitly accepted 13.5A's isolated storage-contract/byte-codec/hydration foundation. Marked done without claiming full-suite green, default-array domain-hash remediation, durable storage, FGDB proof, production schema/cutover or operational 11.2 completion.
- 2026-10-11: implemented and verified the isolated storage/hydration foundation; 23 new groups, 158 focused passes; independent review corrections complete. Submitted for scoped acceptance, with known full-suite and default-array domain-hash limits retained.
