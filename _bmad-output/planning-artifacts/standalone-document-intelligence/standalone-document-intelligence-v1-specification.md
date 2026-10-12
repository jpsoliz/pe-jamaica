# Standalone Document Intelligence Platform - V1 Specification

| Attribute | Value |
|---|---|
| Document version | 0.2 - text/titling and spatial extraction scope clarified |
| Date | 2026-10-11 |
| Status | Proposed; requirements and open decisions ready for review |
| Audience | Product owners, domain specialists, architects, developers, QA, and operations |
| Perspectives | Business analysis, system architecture, and technical documentation |
| Working product name | Standalone Document Intelligence Platform |

This specification describes a proposed new product. It does not change the implementation or architectural constraints of the existing ArcGIS Pro add-in. Requirements marked MUST are proposed V1 requirements, not claims about delivered functionality. Deployment choices and numeric acceptance thresholds remain open unless explicitly stated.

## 1. Purpose and Product Scope

Provide an independent platform that turns source documents into structured, traceable, searchable data through document-specific AI extraction and human validation. Text-based titling and transaction documents and spatial/map documents are primary source families with equal standing. A document may contain text only, spatial information only, or both.

Text-document intake, extraction, review, approval, query, and export MUST work without a map, coordinates, geometry, or a linked survey plan. Spatial processing is a capability selected by the document profile, not a prerequisite for using the platform.

The platform MUST operate without Innola, ESRI software, ArcGIS licensing, or either vendor's services. ESRI and Innola may be optional integration targets or tool providers. Their identifiers, transaction states, storage formats, authentication, and SDKs MUST NOT be mandatory core dependencies.

The intended outcome is approved data that users can inspect, query, and export while retaining access to the source evidence and full revision history. Extraction does not itself establish legal ownership or authoritative cadastral status.

### 1.1 Proposed V1 Scope

- Repository, document intake, revision history, and document grouping.
- Versioned profiles for a bounded set of document types.
- AI-assisted classification and extraction with structured outputs.
- Text and tabular extraction of titling information, participants/roles, transaction references, dates, volume/folio, and property details.
- Supported spatial extraction for document profiles that contain map information.
- Human review, correction, deterministic validation, and approval.
- Structured and spatial queries over approved data.
- Batch processing, resumable jobs, exports, and administrative controls.
- A replaceable AI provider boundary and a defined path for on-premises deployment.

### 1.2 Deferred Unless Explicitly Selected

- Arbitrary document types without configured profiles.
- Autonomous legal interpretation or authoritative cadastral publication.
- General-purpose GIS editing, enterprise records-management replacement, or complex workflow design tooling.
- Training a foundation model from scratch.
- Mandatory ESRI/Innola connectors or bidirectional synchronization.
- Natural-language queries, advanced semantic search, and automatic cross-document conflict resolution.

## 2. Existing Solution and Reuse Assessment

The current project provides useful design precedents: document-specific extraction routes, JSON contracts, evidence-aware fields, explicit semantic states, review gates, and artifact-based recovery. Existing documentation distinguishes computation-sheet extraction from survey-plan OCR/vision extraction.

Reuse is subject to inspection of code dependencies, licensing, test evidence, and domain assumptions. This specification does not assert that existing modules are directly portable.

| Existing concept | Proposed standalone treatment |
|---|---|
| Extraction schemas and semantic states | Assess for reusable core contracts |
| Prompts and document-specific rules | Move into versioned profile assets after review |
| Review-before-output gates | Preserve the principle; define independent workflow states |
| Case folders and processing artifacts | Preserve recovery/evidence requirements; reconsider storage implementation |
| ArcGIS map, geometry, and geodatabase operations | Replace mandatory runtime dependencies or isolate as optional adapters |
| Innola transaction context and lifecycle | Replace with platform-owned work packages and workflow |
| Innola attachment repository | Use platform-owned repository; optional import connector later |

Reference inputs: `docs/project/AI_PROJECT_CONTEXT.md`, `docs/project/EXTRACTION_PROMPTS_AND_RULES.md`, and `_bmad-output/project-context.md`. These describe the existing product, not requirements imposed on this new platform.

## 3. Users, Roles, and Use Cases

| Role | Principal responsibilities |
|---|---|
| Repository administrator | Manage intake, metadata, access, retention, and document organization |
| Profile administrator/domain specialist | Define fields, extraction guidance, validation, and profile versions |
| Operator | Select documents, initiate processing, monitor batches, resolve intake issues |
| Reviewer | Compare results with evidence, correct values/geometry, address findings |
| Approver | Approve or reject a specific reviewed revision |
| Data consumer | Search approved records and export permitted data |
| System administrator | Manage deployment, provider credentials, workers, backup, and monitoring |

Roles may be combined for small deployments. Permissions MUST remain explicit. Separation of reviewer and approver is a configurable organizational policy.

Primary use cases are: ingest a document; process a mixed package; review extracted title information; reconstruct supported map features; resolve conflicting evidence; search approved entities; rerun extraction under a new profile; export an approved dataset; and recover an interrupted batch.

## 4. Document Types and Extraction Profiles

There is no universal extraction schema. A profile defines a document family's classification criteria, supported layouts, fields, extraction route, validation, and review policy. Jurisdiction, language, layout, and document revision may require profile variants.

| Candidate profile | Candidate outputs | Boundaries |
|---|---|---|
| Survey plan/map | Plan references, parties, dates, CRS evidence, coordinates, bearings/distances, points, lines, polygons | Distinguish printed values, derived geometry, and traced geometry |
| Title/register entry | Title/document references, volume/folio, stated owners and interests, registration dates, property descriptions, linked instruments | Works as a text-only record; retain source statements and historical context |
| Transfer/deed/transaction instrument | Participants, grantors, grantees, vendors, purchasers, transaction document numbers, typed dates, property details, consideration where stated | Preserve each party's stated role; do not infer ownership from participation |
| Other titling/supporting text document | Profile-defined fields from declarations, certificates, correspondence, or supporting instruments | Requires a dedicated supported profile; not an unrestricted legal-document extractor |
| Mixed title/map package | Classified sections and linked outputs from the profiles above | A package is a composition of profiles, not one unrestricted prompt |

These are proposed initial candidates. Concrete document samples and domain specialists must confirm the exact V1 variants and fields.

- DOC-01: Preserve source bytes, intake metadata, and a content checksum for every revision.
- DOC-02: Support manual classification and AI-proposed classification with human correction.
- DOC-03: Classify pages/regions where one file contains multiple document types; preserve page order and the original package.
- DOC-04: Route unknown, unsupported, or ambiguous content to review; do not silently apply a generic profile.
- DOC-05: Version profile schemas, prompts, rules, normalizers, and extraction configuration as a published profile release.
- DOC-06: Permit profile drafts and sample-based testing before publication. Editing a draft MUST NOT change an already published release.

### 4.1 Text and Titling Field Catalog

The following field groups form the baseline catalog for text/titling profiles. Each profile defines applicability, required status, cardinality, jurisdiction-specific labels, validation, and normalization. A field absent from the source must retain an appropriate semantic state rather than an invented value.

| Field group | Baseline fields | Modeling requirements |
|---|---|---|
| Document identity | Document type, transaction/instrument type, transaction document number, registration/instrument number, issuing authority/registry | Preserve identifier strings, leading zeros, prefixes, and source labels; distinguish document numbers from title numbers |
| Title references | Title number, volume, folio, book/page or equivalent, prior title and related instrument references | Preserve volume/folio as paired references; permit multiple references and identify their relationship to the document/property |
| Participants | Person/organization name, participant role, source role wording, identifiers and addresses where stated and permitted | Repeating collection; distinguish grantor, grantee, vendor, purchaser, owner, representative, witness, and other profile-defined roles |
| Dates | Execution, signing, transaction, lodgement, registration, issue, effective, and other labeled dates | Store date type, raw text, normalized value and precision; flag ambiguous formats instead of assigning a guessed date |
| Property details | Property name, address/location, parish/district or equivalent, parcel/lot/block/plan references, legal/property description, area and unit | Repeating property collection; retain narrative descriptions even when there is no geometry or parcel identifier |
| Interests and transaction details | Stated ownership/share, tenure, interest transferred, consideration/currency, restrictions, encumbrances, and referenced instruments where applicable | Profile-specific extraction of explicit source statements; no automatic determination of legal effect |

- TXT-01: Support born-digital text and scanned text through profile-selected text/OCR/AI routes. Preserve reading order and table/narrative structure needed to interpret fields.
- TXT-02: Represent participants and roles separately: one participant may have several roles, and one role may have several participants. Link each role to its document/transaction and relevant property or interest where stated.
- TXT-03: Preserve grantor, grantee, vendor, purchaser, and owner as distinct source-supported roles. Any jurisdiction-specific synonym mapping MUST be versioned; role alone MUST NOT establish current ownership.
- TXT-04: Support multiple transaction numbers, dates, volume/folio pairs, and properties without flattening them into one value. Retain the relationships and evidence needed to distinguish them.
- TXT-05: Permit approval of text-only documents when their profile's textual validation/review requirements pass. Geometry/CRS rules MUST be not applicable for these profiles.
- TXT-06: Preserve historical and document-stated ownership/interests separately from any asserted current state. Changes across documents require explicit reconciliation and evidence.
- TXT-07: Flag ambiguous participant-to-property relationships, conflicting identifiers, and unresolved references for review rather than silently assigning them.

## 5. Document Repository and Intake

- REP-01: Provide a platform-owned repository with stable document IDs and immutable source revisions.
- REP-02: Support individual upload and bulk import; proposed initial formats are PDF, TIFF, JPEG, and PNG. Exact size, page-count, and encoding limits require confirmation.
- REP-03: Record original filename, MIME/type verification, checksum, ingest time, uploader, source, access scope, and optional external references.
- REP-04: Detect exact duplicate content and let an authorized user link to an existing document or retain a separate record with a reason.
- REP-05: Group related documents into work packages independent of external transactions.
- REP-06: Quarantine invalid/unprocessable files with actionable diagnostics. Preserve password-protected-file handling as an explicit supported/unsupported policy.
- REP-07: Preserve originals separately from rendered pages, OCR, crops, extraction artifacts, and exports.

Retention and authorized deletion must reconcile immutable revision history with organizational deletion policies. A tombstone/audit event may outlive deleted content where policy permits; immutability is not a promise of indefinite retention.

## 6. Data Model and Evidence Traceability

Core entities are Document, DocumentRevision, WorkPackage, DocumentSection, ProfileRelease, ExtractionRun, ExtractedObservation, DataRevision, DomainEntity, GeometryRevision, ValidationFinding, ReviewDecision, Approval, ExportSnapshot, and AuditEvent.

- DAT-01: Give entities stable internal identifiers. Keep external IDs namespaced and optional.
- DAT-02: Each extracted field MUST retain raw value, normalized value, semantic state, source revision, page/region evidence, extraction run, and extraction method.
- DAT-03: Preserve available provider confidence with its provenance. Missing confidence MUST remain missing; provider scores are not calibrated accuracy guarantees.
- DAT-04: Distinguish VALUE, NONE, N_A, NOT_STATED, NOT_FOUND, ILLEGIBLE, and UNKNOWN. Profiles may add domain-specific states.
- DAT-05: Preserve repeated values, table row boundaries, party roles, units, original language, and relationships.
- DAT-06: Distinguish extracted observations from reviewer-accepted values and derived values. Corrections MUST preserve prior values, author, timestamp, reason, and evidence.
- DAT-07: Model document-to-entity and entity-to-entity links, including disputed or provisional matches. Do not silently merge parties/parcels on name similarity.
- DAT-08: Record conflicting observations and resolution decisions without discarding contradictory source evidence.
- DAT-09: Model Participant, ParticipantRole, Transaction/Instrument, TitleReference, PropertyDescription, and StatedInterest as linked domain records. Geometry is optional for a property record.
- DAT-10: Preserve typed dates and reference relationships, including current-document versus cited-document references. A shared participant/reference may link documents without collapsing their distinct statements.

Document revision, extraction-run revision, and approved-data revision are different concepts. Queries and exports must identify which they use.

## 7. AI Extraction and Model Management

Proposed pipeline: intake -> classify/segment -> preprocess -> extract -> normalize -> validate -> review -> approve -> query/export. Deterministic parsers and geometry calculations may complement AI; the overall extraction capability MUST include AI for supported profiles.

- AI-01: Use an internal provider contract that does not expose vendor-specific payloads as the canonical data model.
- AI-02: Record provider, model identifier/version when available, profile release, prompt/configuration hashes, preprocessing version, and run timestamps.
- AI-03: Validate output structure and field types before accepting an extraction result. Treat malformed or unsupported output as a failed/reviewable result.
- AI-04: Treat document contents as untrusted extraction input. Embedded instructions MUST NOT authorize tool execution, credential access, workflow changes, or external transmission.
- AI-05: Permit explicit null/uncertainty states; prohibit filling missing facts without labeled derivation and evidence.
- AI-06: Configure provider routing and data-egress policy per deployment. A local-only deployment MUST fail visibly instead of falling back to a hosted provider.
- AI-07: Support timeouts, bounded retries, cancellation, quotas, and usage/cost telemetry where available.
- AI-08: Provide an evaluation mechanism using verified reference documents and results by profile/version.

Hosted inference and local inference are architectural options. At least one deployment/provider combination must be proven for V1. Fully offline operation is an open scope decision and requires validating the complete pipeline, not just the model endpoint.

On-premises inference does not necessarily require training. Fine-tuning may be considered after baseline evaluation demonstrates a gap. Any training capability needs approved datasets, annotation practices, usage rights, evaluation, model release history, and rollback; a training interface is deferred from V1.

## 8. Spatial Extraction and Validation

- GEO-01: Support points, lines, and polygons for the explicitly selected spatial profiles.
- GEO-02: Identify geometry origin: printed coordinates, calculated bearings/distances, georeferenced tracing, or unreferenced image-space tracing.
- GEO-03: Retain CRS/datum evidence, units, axis conventions, transformations, and control points when used. Unknown CRS MUST NOT be silently assigned.
- GEO-04: Keep image-space geometry distinct from geographic geometry. Unreferenced features MUST NOT enter geographic spatial queries or geographic exports as if located.
- GEO-05: Provide source overlays and geometry review/correction with a versioned edit history.
- GEO-06: Validate geometry type, ring closure, self-intersections, repeated/invalid vertices, and profile-specific area/distance/bearing tolerances where applicable.
- GEO-07: Preserve extracted, computed, and corrected coordinates separately, including calculation assumptions and uncertainty.
- GEO-08: Gate approved spatial outputs on evidence, coordinate-reference requirements, and the selected accuracy policy.

Spatial targets must distinguish visual digitization tolerance from survey-coordinate accuracy. Georeferencing may require manually supplied control points or optional reference data. Curves, multipart geometry, holes, and multi-parcel plans require an explicit supported/unsupported decision before V1 commitment.

## 9. Rules and Profile Administration

- RUL-01: Separate extraction guidance from deterministic validation and workflow policy.
- RUL-02: Give rules stable IDs, versions, severity, applicability conditions, parameters, and understandable messages.
- RUL-03: Support required-field, type, format, range, unit, cross-field, cross-document, and spatial rules within the agreed V1 catalog.
- RUL-04: Findings MUST identify evidence, affected fields/features, and a resolution path. Overrides require permission, reason, and audit history.
- RUL-05: A rule/profile change MUST NOT silently revise historical approved data. Revalidation creates an explicit run/result.

V1 administration may use controlled configuration plus a basic management interface. An unrestricted scripting/rule-building system is not assumed.

## 10. Review, Correction, and Workflow

Proposed processing states: Queued -> Processing -> ReadyForReview, with Failed and Cancelled outcomes. Proposed data states: Draft -> InReview -> Approved or Rejected. These state families remain separate so a failed rerun does not overwrite an approved revision.

- WFL-01: Provide assignment, review queues, status filters, and finding resolution.
- WFL-02: Show source evidence alongside extracted fields and supported geometry.
- WFL-03: Require explicit review and approval for V1 approved datasets.
- WFL-04: Bind approval to a specific data revision and validation result. Any subsequent edit requires a new review/approval.
- WFL-05: Define transitions, role permissions, required checks, rejection reasons, and reopening behavior.
- WFL-06: Detect concurrent edits and stale approvals; prevent silent overwrites.
- WFL-07: Preserve work in progress so reviewers can resume after interruption.

## 11. Versioning, Audit, and Reprocessing

- VER-01: Preserve source revisions, extraction runs, data corrections, geometry edits, rule/profile releases, approvals, and export history.
- VER-02: Rerunning extraction creates a new result linked to its source/configuration. Preserve human corrections and offer explicit comparison/reconciliation.
- VER-03: Compare relevant revisions, including changed fields, geometry, validation outcomes, and provenance.
- VER-04: Record actor, action, time, target revision, and reason where required in access-controlled audit events.
- VER-05: Preserve enough run inputs/configuration to explain a result. Exact model reproducibility cannot be assumed when provider behavior is not fixed.
- VER-06: Define supersession and withdrawal of approved results without deleting their history.

## 12. Search, Query, and Data Presentation

- QRY-01: Support metadata and text search subject to access permissions.
- QRY-02: Support typed filters over approved extracted fields: dates, names/roles, references, document types, numeric values, and status.
- QRY-03: Provide document/entity views with related sources, revisions, and evidence links.
- QRY-04: Support basic bounding-area/intersection queries for approved, geographically located geometry; exact query catalog remains to be selected.
- QRY-05: Show approved data by default. Draft/rejected/superseded views require explicit selection and visible status.
- QRY-06: Apply authorization to results, counts, evidence, and downloads. Rebuildable indexes MUST NOT be the sole source of approved data.
- QRY-07: Support text/titling queries by participant name plus role, transaction document number, typed date/range, volume/folio pair, title reference, property name/location, and parcel/plan reference where captured.
- QRY-08: Support combined filters and related-record views without geometry; for example, grantee plus registration-date range, or volume/folio plus property. Name search MUST NOT imply verified identity matching.

## 13. Export and Integration Contracts

- EXP-01: Export a consistent snapshot of selected approved revisions with schema version, source references, CRS/units where applicable, and export time.
- EXP-02: Proposed baseline formats are JSON for structured data, CSV for flat tabular views, and GeoJSON for eligible geographic features. Confirm mappings and format constraints before implementation.
- EXP-03: Include explicit rules for repeated fields, relationships, null/semantic states, identifiers, and encoding. Do not silently flatten away evidence or ambiguity.
- EXP-04: Reject unsupported geometry/CRS combinations or perform an explicitly recorded transformation.
- EXP-05: Provide permission-controlled bulk export and a machine-readable contract for downstream tools. API scope and authentication are open decisions.
- EXP-06: Keep optional connectors behind adapters with separate mappings, credentials, retry policies, and integration audit events.
- EXP-07: Export text-only records independently of spatial output. Preserve participant-role, transaction, title-reference, and property relationships in JSON or related CSV tables with stable linking IDs.

Optional adapters may support ESRI, Innola, other document repositories, databases, or GIS tools. External transaction IDs remain metadata. Connector failure MUST NOT prevent independent intake, review, query, or export. Connector availability is not a V1 commitment.

## 14. Architecture, Deployment, and Operations

### 14.1 Logical Components

| Component | Responsibility |
|---|---|
| User application | Repository browsing, processing, review, search, and administration |
| Application services/API | Authorization, workflow, revision handling, query, and export |
| Document/artifact storage | Originals, derived images, extraction artifacts, and export packages |
| Structured data persistence | Documents, participants/roles, instruments, title references, properties, relationships, and version references |
| Spatial capability | Profile-selected geometry processing, spatial persistence, and geographic queries; not required for text-only workflows |
| Profile/rule registry | Immutable published releases and controlled drafts |
| Job coordinator and workers | Queues, AI/parsing/geometry processing, retries, and recovery |
| AI provider adapters | Hosted/local model access under deployment policy |
| Optional external adapters | Vendor/tool integrations isolated from the core |
| Audit and monitoring | Activity, diagnostics, health, processing usage, and operational evidence |

These are responsibility boundaries, not a requirement to deploy separate microservices. Packaging and technology selection follow deployment and scale decisions.

### 14.2 Batch Processing

- BAT-01: Identify each batch and per-document job with pinned source/profile revisions.
- BAT-02: Report per-item and aggregate progress, including partial success and actionable failures.
- BAT-03: Support bounded concurrency, cancellation, retry of failed items, and restart recovery.
- BAT-04: Prevent duplicate result publication when a job is retried or a worker restarts.
- BAT-05: Make intake and processing limits configurable; record why an item was skipped or blocked.

### 14.3 Security and Operations

- OPS-01: Enforce role/access-scope permissions and protect credentials outside documents/logs.
- OPS-02: Define encryption, transport security, audit access, and identity integration for the selected deployment.
- OPS-03: Govern retention/deletion of originals, derivatives, exports, and provider-side copies where applicable.
- OPS-04: Back up documents and structured state consistently; demonstrate restoration, including revision and approval references.
- OPS-05: Provide health monitoring, redacted diagnostics, storage/queue limits, and processing metrics.
- OPS-06: Define storage/index migrations, deployment upgrades, model/profile rollback, and interrupted-job behavior.
- OPS-07: Define data-egress and network policies for cloud, on-premises, and any offline mode.

## 15. V1 Acceptance and Delivery Plan

### 15.1 Acceptance Scenarios

| Scenario | Required evidence |
|---|---|
| Vendor-independent operation | Complete intake -> AI extraction -> review -> approval -> query -> export with no ESRI/Innola software, credentials, or endpoints |
| Type-specific extraction | Each selected profile extracts its agreed fields from a verified representative sample set |
| Text-only titling workflow | Ingest born-digital and scanned text samples; extract participants/roles, transaction numbers, typed dates, volume/folio, and property details; review, approve, query, and export without geometry or a map |
| Multiple parties and properties | Preserve several grantors/grantees/owners and their supported relationships to multiple properties; flag ambiguous links |
| Reference/date integrity | Preserve leading zeros, multiple volume/folio pairs, cited instrument references, and distinct date types; ambiguous dates remain reviewable |
| Historical ownership evidence | Keep owner statements tied to the source/date; a vendor or grantee role does not automatically become current owner |
| Mixed package | Correct section routing, preserved page evidence, and reviewable ambiguity |
| Uncertain/contradictory source | Explicit semantic states/findings; no silent fabricated resolution |
| Spatial extraction | Supported points/lines/polygons retain evidence, origin, CRS status, and validation results |
| Human correction and approval | Changed data creates a revision; stale approvals cannot approve the changed result |
| Reprocessing | Original results and corrections survive; new results can be compared |
| Batch interruption | Completed items persist; restart/retry does not duplicate published results |
| Search and export | Approved revision semantics and access controls hold across results and snapshots |
| Recovery | Restored sources, data revisions, approvals, and evidence links remain consistent |
| Local-only mode, if selected | Full selected workflow succeeds without hosted inference or undeclared outbound dependencies |

### 15.2 Measures to Finalize Before Implementation Commitment

Define targets per profile using a verified evaluation corpus: critical-field accuracy, required-field completeness, semantic-state correctness, classification performance, false entity matches, spatial accuracy by extraction method, reviewer correction effort, and rejection rate.

Define operational targets against agreed document/page sizes, batch volumes, concurrent users, hardware, and provider mode: processing latency, throughput, search response time, availability, storage growth, restore time, and maximum acceptable data loss. No numeric target is asserted without this workload context.

### 15.3 Proposed Delivery Stages

1. Confirm V1 users, deployment, document samples, field catalogs, quality targets, and supported geometry.
2. Prove a text/titling profile end to end with independent storage, an AI adapter, evidence, review, approval, participant/reference queries, and nonspatial export.
3. Add the selected additional profiles, mixed packages, spatial behavior, batch recovery, and revision comparison.
4. Verify permissions, restore, evaluation targets, and any committed local-inference mode in a pilot.
5. Consider optional ESRI/Innola adapters after core acceptance, according to actual integration demand.

### 15.4 Open Decisions and Risks

| ID | Decision or risk | Consequence |
|---|---|---|
| OD-01 | Desktop/local deployment versus self-hosted multiuser application | Determines identity, concurrency, packaging, and operations |
| OD-02 | Initial jurisdictions, languages, document variants, and samples | Determines profile scope and evaluation corpus |
| OD-03 | Required fields and critical-field definitions | Determines schemas and acceptance targets |
| OD-04 | Hosted inference, local inference, and full offline commitment | Determines hardware, data-egress policy, and provider validation |
| OD-05 | Supported geometry, georeferencing, and spatial tolerances | Prevents committing to unsupported map reconstruction |
| OD-06 | Workload, user counts, budget, and hardware | Determines performance targets and architecture sizing |
| OD-07 | Approval policy, permissions, and authoritative-data boundary | Determines workflow and publication gates |
| OD-08 | Export/API catalog and downstream consumers | Determines interoperable contracts |
| OD-09 | Retention, data sensitivity, usage rights, and training permissions | Determines storage lifecycle and AI data handling |
| OD-10 | Existing code portability and licensing | Determines reuse versus replacement effort |

## Appendix A. Document Type Profile Template

Each profile must specify:

1. Stable profile ID, name, owner, version, status, and applicable jurisdiction/language.
2. Document classification cues, supported layouts, and positive/negative samples.
3. Supported input formats, page/region selection, preprocessing, and quality limits.
4. Field catalog: name, meaning, type, cardinality, required status, units, semantic states, and examples.
5. Domain entities, roles, identifiers, and relationship rules.
6. Extraction route, AI capabilities, prompt/configuration assets, and provider constraints.
7. Evidence requirements, normalization rules, and permitted derivations.
8. Supported geometry, CRS requirements, calculation methods, and tolerances.
9. Validation rules, severities, override permissions, and review requirements.
10. Approval conditions, queryable fields, export mappings, and evaluation targets.
11. Compatibility/migration policy, publication history, and known limitations.

## Appendix B. Working Glossary

| Term | Meaning |
|---|---|
| Document profile | Type-specific contract governing extraction, validation, and review |
| Work package | Platform-owned grouping of related documents and processing work |
| Observation | A captured statement/value linked to source evidence, before reconciliation |
| Data revision | An immutable snapshot of extracted/reviewed structured data |
| Approved dataset | A specific data revision that passed the configured approval gates |
| Image-space geometry | Features located on a document image without a geographic position |
| Derived geometry | Features calculated from recorded source measurements and assumptions |
| Connector | Optional adapter to an external product or service |
| Standalone | Operationally independent of ESRI and Innola; deployment packaging is a separate decision |
| Participant role | A party's source-supported capacity in a particular document/transaction, such as grantor, grantee, vendor, or owner |
| Title reference | A source-supported title identifier or paired registry reference such as volume/folio |

## Revision History

| Version | Date | Change |
|---|---|---|
| 0.1 | 2026-10-11 | Initial independent-platform V1 specification |
| 0.2 | 2026-10-11 | Made text/titling workflows explicit; added field catalog, participant/transaction relationships, nonspatial queries/exports, and acceptance scenarios |
