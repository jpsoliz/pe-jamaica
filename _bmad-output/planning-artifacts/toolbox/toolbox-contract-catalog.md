# Toolbox Contract Catalog

Story: 13.2
Date: 2026-10-08
Status: architecture direction accepted 2026-10-08; contract outlines require implementation validation; no runtime schema migration performed

All IDs/examples are local illustrative values unless marked as existing source fields. This catalog defines target obligations, not fabricated Innola fields. Schema versions are proposed; implementation must publish validated schemas and compatibility tests.

## Context v1

Required semantic fields: context schema version; server environment ID; current TransactionId and original/normalized TR; case ID and root; source references; examiner-selected preset; observed actual task instance/name/process/assignment; resolution status/provenance; optional verified task-definition identifiers; operator; selected parcel IDs; current committed revision; accepted revision; mapping/policy versions.

```json
{
  "schema_version": "toolbox_context_v1",
  "case_id": "example-case-001",
  "server_environment": "example-test",
  "transaction_id": "example-tr-id",
  "transaction_number": "TR100001",
  "selected_preset": "compute",
  "actual_task": {
    "task_id": null,
    "task_name": "Compare",
    "resolution": "unverified",
    "definition_id": null,
    "observed_at_utc": "2026-10-08T12:00:00Z",
    "id_provenance": "missing"
  },
  "selected_parcel_ids": ["example-parcel-a", "example-parcel-b"],
  "current_revision_id": "example-r4",
  "accepted_revision_id": "example-r3",
  "policy_version": "example-policy-v1"
}
```

Null/unknown task metadata permits authorized local work only. Server mutation contracts require freshly verified real instance/assignment/target. Tokens/passwords are session-only, never context artifacts. Root/path fields are validated resolved references, not blindly accepted tool arguments.

## FGDB Schema Outline v1

One active FGDB per current case plus read-only accepted snapshots/references. Logical keys are stable string/GUID values; ObjectID is storage identity only. FGDB relationship classes or explicit link-table joins may implement relationships; constraints must be validated by CaseStore because uniqueness/cross-table referential rules are not assumed automatic.

The current-case Toolbox FGDB path is `working/TR-<transaction-number>.gdb` (C09), for example `working/TR-999999999.gdb`. Preserve legacy numeric-only naming and original server identities. Filename naming does not imply authority to reuse/migrate an existing case. See the architecture's confirmed naming section for validation and isolation rules.

| Table/class | Key and main fields | Relationships/invariant |
|---|---|---|
| CaseInfo | case_id, server_environment, transaction_id, transaction_number, schema_version, commit_id, revision_id | One current case owner |
| Parcels | parcel_id, case_id, label, inclusion_status/reason, geometry_state, evidence_revision | All included membership explicit |
| DraftPoints/DraftBoundaries/DraftParcels | feature_id, candidate_batch_id, parcel_id, geometry/CRS, source_ref | Provisional, never exported as accepted by accident |
| Points | point_id, coordinates/geometry/CRS, provenance, reviewed_status | Membership via ParcelPoints; point order preserved |
| ParcelPoints | parcel_id, point_id, sequence, role | Stable ordered membership; support shared vertices explicitly |
| Boundaries | boundary_id, geometry/CRS, reviewed bearing/distance/units, source_ref, status | Shared physical edge identity after review |
| BoundaryMembership | boundary_id, parcel_id, direction, sequence | Derive parcel ring orientation; all affected neighbors known |
| AcceptedParcelGeometry | feature_id, parcel_id, geometry/CRS, geometry_revision, construction_recipe | Materialized accepted ring; reconciled to reviewed boundaries/manual provenance |
| Sources | document_id, case/source TR, relative_path, hash, role, selected page/extent | Original evidence immutable; repeated name not sole identity |
| EvidenceValues | evidence_id, document_id, field_key, raw_value, normalized_value, reviewed_value, units, status, source_page/zone, reviewer | Missing/not-applicable/negative/uncertain distinct |
| EvidenceParcelLinks | evidence_id, parcel_id, link_role | Document-wide facts need no parcel filter |
| Parties / PartyRoles | party_id; party_role_id, party_id, role, source_evidence | Owners, occupiers, representatives distinct |
| PartyParcelLinks / Neighbors | role_id, parcel_id; neighbor_id, subject_parcel_id, adjacent_ref, evidence_id | No unlabeled owner duplication across parcels |
| PlanMetadata / SurveyEquipment | fact_id, evidence_id, field_key, reviewed value | Conceptual projections over evidence; choose one owning representation, not two editable copies |
| Findings | finding_id, rule_id/version, scope_ref, input_fingerprint, outcome/effect, disposition | Current only if fingerprint and policy match |
| Approvals | approval_id, parcel_id or case scope, fingerprint, policy_version, actor/time | Case approval includes included-set fingerprint |
| RevisionHistory / Lineage | revision_id, parent_id, operation_id, digest; old/new entity IDs and split/merge reason | Preserve grouping and edit ancestry |
| ToolRuns | run_id, tool/version, input_revision, state, artifact refs, metrics | Runs don't imply approval |
| References | reference_id, source server/TR/revision, copied snapshot path, selected parcels, read_only | Current case cannot modify source PE |

Parish, equipment, north-arrow and parties are fields/evidence, not universal required columns/blocks for every preset. Required field policy is D04. Curves/Z/M support, CRS transform and precision rules require explicit versioned geometry policy; unsupported geometry is rejected, not flattened without evidence.

## Revision And Candidate Contracts

Revision v1: revision ID, monotonic local sequence, parent, expected prior accepted/current state, operator/time, affected IDs, included set, geometry/evidence/policy hashes, commit journal state and snapshot references.

CandidateBatch v1: batch/run ID, tool/provider version, input source hashes/route, suggested parcel memberships/lineage, raw values, normalized suggestions, confidence/finding evidence and expected base revision. No candidate overwrites accepted evidence without an accepted CaseStore command.

Commit command: operation ID, expected current revision, selected preset, authorized write scope, changed values/geometries, provenance and reason. Validation rejects stale parent, edits to read-only linked references, incompatible preset and incomplete shared-edge impact sets.

Approval v1: approver/time, included parcel IDs, accepted parcel fingerprints, evidence/rule/policy versions, dispositions and derived case status. Current aggregate is all included accepted. Report-only failures differ from blockers; exceptions require actual authorized policy, not string parsing of status messages.

### Accepted-With-Exceptions Extension (C10)

C11 qualification: separate stage examination disposition (forwarded-with-recorded-defects) from geometry validation/approval. The authorized current examiner records source-plan defects and uses the normal next stage; extraction mismatches are corrected first, not waived. Earlier accepted-with-exceptions terminology must not expose invalid geometry as accepted to output/projection consumers.

Finding records additionally retain stable finding ID, origin (extraction/source-plan/mixed/unknown), detecting stage/operator/time, document/page/zone, affected parcels, source versus extracted/reviewed evidence, input revision, status and explanation. Mixed findings distinguish the corrected extraction component from the remaining source defect. Stage review records track evaluated/not-applicable/not-evaluable checks and reasons; missing review is not a pass.

The case finding register is cumulative across stages: append discoveries, relate duplicates without silently overwriting them, and retain resolution/invalidation history. Stage handoff carries the full register, not only current-stage errors; final rejection consumes the cumulative review. Changed input invalidates dependent dispositions while retaining old findings. Real persistence/report/package/transition implementation remains 13.5/13.9.

Acceptance distinguishes clean acceptance from accepted-with-exceptions. A versioned exception references the unresolved finding/rule, affected case/parcel scope and current input fingerprint/revision, plus examiner identity, verified authority/policy, justification, time and policy-required evidence. Keep the original failed/unknown finding and exception approval as separate records; never rewrite outcome to passed.

For exceptions outside the confirmed C11 source-defect route, aggregate acceptance includes valid exceptions for all included parcels and applicable case findings. Expired/revoked authority, changed inputs or policy stale the exception and aggregate. D12 supplies any additional eligible findings/roles; it does not add supervisor approval to C11. Missing authority cannot be replaced by a checkbox or justification alone. Identity/integrity/source-write protections and downstream required artifacts remain non-waivable.

13.4A models/evaluates exceptions in memory; 13.5 persists them; 13.9 reports/packages them and revalidates actual handoff authority. This extension does not authorize remote operations from the local context.

### Human-Reviewed Values And Partial Comparison (C12-C14)

Value review separates source_presence (present/missing_from_source/uncertain/not_reviewed), accuracy (accurate/inaccurate/uncertain/not_reviewed/not_applicable), raw/extracted value, reviewed value, capture method (extracted/manual/corrected), source reference/page/zone, reviewer/time and input revision. An absent value has no artificial accurate pass; manual correction never erases raw extraction. Source absence needs human confirmation/evidence; provider confidence is not human validation. These semantic fields adapt the canonical 11.2 trace/review fields, not a competing extraction schema.

Comparison items reference reviewed value and fingerprint, LTD source/record/evidence and retrieval status, comparison result, reviewer/time, pending reason and dependencies. Distinguish source-plan absence from unavailable LTD data. Partial comparisons preserve completed items; aggregate Compare is pending_with_reason while required comparisons remain unavailable/incomplete. No final approval/rejection or server completion is inferred from partial available-data review.

Compare may update current-case tabular review values locally, without geometry edits or implicit Innola/system writeback. Invalidate only consumers of changed values plus affected aggregate decisions. Geometry-dependent changes retain the Compute-only boundary.

## Tool Definition And Invocation v1

ToolDefinition: tool ID/version/group, scope levels, permitted local write roles/presets, artifact/source prerequisites, consumed/produced contract versions, invalidated dependencies, cancellation/progress behavior, remote action classification, recovery/overwrite policy and adapter owner.

Invocation: run ID, context reference, exact input revision/fingerprints, selected document/parcel IDs, parameters and expected outputs. Executor resolves paths through CaseStore, not independent transaction discovery.

Result: succeeded/blocked/failed/canceled/ambiguous, operator-safe reason, technical sanitized diagnostic reference, output revision/artifact IDs, affected entities, metrics/findings, stale/current state and next eligible capabilities. Process exit success without required outputs is failed. Timeout after server mutation is ambiguous, not automatically retryable.

Adapters reuse current services: extraction orchestrator/Python routes; manual points/segments/solver; rules; output/map services; Compare query/report; PLA selection/crop; RT load/writeback. Static descriptors compose them; no plugin discovery engine proposed.

## Task Mapping And Remote Mutation v1

Mapping: mapping version, server environment, verified definition identifiers when available, explicitly evidenced task/process aliases, normalized official task, supported selected presets/operations, required evidence and allowed transition targets. Active instance ID is runtime, not static config.

Mutation command: operation ID, snapshot revision/hash, current server TR/instance, refreshed operator/assignment/session, target policy and freshly returned transition identity. Read-only official-task evidence is retained as redacted provenance, not a cached permission grant.

Observed routes/aliases in current source are documented in [architecture](toolbox-architecture.md#actual-innola-evidence-and-mapping). Exact stable definition IDs and server-side idempotency support are not established. Existing taskId=transactionId fallback and default transition selection cannot authorize Toolbox operations.

## Projection v1

Envelope: case/current/source identities, accepted revision ID/parent/digest, geometry schema/CRS, selected included parcels, feature stable keys, points/boundaries/parcels payload, actor/time, expected prior published revision and operation ID.

Receipt: target metadata/capability version, exact published revision, per-layer feature result/count checks, read-back marker and integrity evidence, server edit result IDs/time when supported, outcome and reconciliation state.

Local feature_id plus case/revision maps to server ObjectID/GlobalID through a persisted table; repeat publication preserves identity or explicitly replaces scoped identities under tested rollback. Never compare ObjectID values across exported GDBs. Independent services cannot be assumed one atomic transaction.

## Case Package v2

```json
{
  "schema_version": "toolbox_case_package_v2",
  "case_id": "example-case-001",
  "server_environment": "example-test",
  "transaction_id": "example-tr-id",
  "transaction_number": "TR100001",
  "revision_id": "example-r4",
  "parent_revision_id": "example-r3",
  "creating_task_id": "example-creating-task-instance",
  "selected_preset": "compute",
  "package_kind": "handoff",
  "included_parcel_ids": ["example-parcel-a", "example-parcel-b"],
  "complete_case": true,
  "files": [
    {
      "path": "working/TR-100001.gdb/example-file",
      "sha256": "example-placeholder-not-a-real-checksum",
      "bytes": 0
    }
  ]
}
```

Example checksum is intentionally illustrative; real schema requires valid checksums/lengths and every required snapshot file. Both source and package-file identities are checked. required source/evidence inventory, accepted snapshots, schema/tool/policy versions and reference manifests accompany the manifest.

creating_task_id preserves history; current task authorization is reacquired after restore. complete_case cannot be set merely because polygons exist. Draft suspend packages can restore unfinished work but cannot claim accepted handoff. Defaults/retention/limits remain D10 until reviewed.

Compatibility reader:
- Legacy v1 loader retains its current TR/task checks; imported evidence is labeled legacy, not newly accepted.
- v2 reader checks same server/TR identity, verified revision/lineage, bounded safe archive paths, supported schema and required FGDB/tables; creation task mismatch does not imply different transaction.
- New reader does not silently overwrite existing root. Stage/readability/identity validation precedes activation with recoverable backup.
- Old operational build is not claimed to read v2 packages. Rollback uses retained baseline cases/export adapters, not an unsupported downgrade of new schema.

## Handoff Plan And Ledger v1

Plan: operation ID, exact snapshot/content hash, selected/actual task identities, mandatory ordered actions, target policy/version and prerequisite receipts.

Ledger step: pending/running/confirmed/failed/ambiguous/reconciled, action type, input hash, target identity, request time, sanitized result, confirmed remote object/attachment identity and read-back evidence. Freeze payload; ledger updates do not alter payload hash.

Same operation with confirmed receipt is not resent. Missing response requires querying remote truth; if server cannot locate/deduplicate upload, ask for explicit recovery disposition and block silent duplicate upload. Never claim the local operation UUID makes the server idempotent.

Step examples: publish accepted revision, upload ZIP, upload report/annex image, current-Plan neighbor/checklist/SpatialUnit writes per authorized task adapter, transition. Preserve existing RT Save and Save & Close effects; remote Plan writes are not generalized into geometry editing.

## Contract Verification Obligations

Publish machine-checkable schemas and round-trip fixtures during implementation. Validate unknown-field compatibility, canonical hashing, safe naming, entity relationships, inclusion/approval fingerprints, CRS, units, geometry policies and source references. Keep explicit required/optional/null semantics. Version changes requiring transformation use a migration adapter and remain blocked if unsupported.

Catalog outlines form the accepted architecture direction, not deployed or machine-validated schemas. No contract files, FGDB tables or service fields were created in Story 13.2.
