# Toolbox Decision Register

Story: 13.1
Date: 2026-10-08
Status: 13.1 process and 13.2 architecture direction accepted; implementation/domain gates retained
Owners: Product/JotaPe and Mary (business); Winston (architecture); Innola/GIS administration where named

## Confirmed Decisions

| ID | Decision | Evidence | Consequence |
|---|---|---|---|
| C01 | Examiner chooses working task; actual Innola task validated before handoff | Explicit user reply, 2026-10-08 | Separate selected workspace and actual task identities |
| C02 | Compare geometry read-only; corrections use Compute | Explicit user reply, 2026-10-08 | Fresh affected checks/approval/comparison after correction |
| C03 | Compute supports multi-parcel Cad Maps as well as single parcels | User requirement, 2026-10-08 | First-class parcel groups; no first-parcel-only output |
| C04 | Initially all included parcels accepted before handoff | Explicit user reply, 2026-10-08; clarified by C10/C11 | Every included parcel requires a current examination disposition; stage forwarding with recorded source defects is distinct from valid-geometry approval |
| C05 | Current-TR FGDB with reusable tabular evidence; attached case ZIP | User initial toolbox requirements | Spatial/tabular persistence and package parity are target requirements |
| C06 | Enterprise working geometry copied/updated at checkpoints | User initial toolbox requirements | Projection, not competing editable master; checkpoint timing pending |
| C07 | Preserve current development for parallel use | User requirement, 2026-10-08 | Isolate experimental code, data and installation |
| C08 | RC means existing RT Examination | Explicit user reply, 2026-10-08 | Preserve RT's established behavior |
| C09 | New Toolbox FGDB names use TR-<transaction-number>.gdb, e.g. TR-999999999.gdb; legacy numeric-only names stay unchanged | Explicit user naming instruction, 2026-10-08 | Apply in future Toolbox persistence/package paths; no existing FGDB rename or server-identity change; retain separate roots and ownership checks |
| C10 | An authorized examiner can hand off a case with unresolved examination findings and recorded justification | Explicit user confirmation, 2026-10-08 | Add accepted-with-exceptions; retain failed/unknown findings, identity, justification and revision-bound approval. Do not relabel findings passed or infer authority from free text |
| C11 | Same authorized examiner corrects extraction errors, records underlying survey-plan defects and forwards through the normal next stage; later findings accumulate for comprehensive rejection | Explicit user clarification, 2026-10-08 | Use forwarded-with-recorded-defects, not valid-geometry approval. No default supervisor/correction-route requirement; no stop at first source defect |

Confirmed here means the stated user choice is recorded. It does not imply acceptance of every proposed process or architecture detail.

### Additional Confirmed Review Decisions

| ID | Decision | Consequence |
|---|---|---|
| C12 | Missing from source is a valid recorded state; extracted values and geometry require human validation, including whether a value is present and accurate; manual capture is supported | Separate presence, accuracy, capture method and reviewer evidence. Do not equate source absence with extraction failure or auto-validate extracted values |
| C13 | Tabular information is primarily consumed in Compare; missing/corrected tabular data is cleared there without returning to Compute | Compare permits current-case tabular review/manual correction, not geometry editing or source/system writeback. Stale only dependent comparisons/findings; geometry corrections stay in Compute |
| C14 | Unavailable/incomplete LTD data leaves Compare pending with a recorded reason, while available data is reviewed and retained | Distinguish partial review from final approval/rejection; resume outstanding comparisons without discarding completed work. No implied Innola transition for pending |
| C15 | JotaPe approved Compute/Compare classification v1 for all 42 matrix items and documented applicability conditions on 2026-10-08 | Classification approval is resolved; implement versioned Toolbox policy separately from existing settings. Required review does not mean mandatory value presence or automatic blocking. Detailed preset/effect/authorization mappings remain D04/D12 |

These user decisions were confirmed on 2026-10-08. Final approval/rejection is at the end of a sufficiently completed Compare review. Detailed required-item/check catalogs remain D04/D12; the choices above are not reopened by those catalogs.

## Open Decision Log

Every recommendation below is proposed. Blocking means blocks the named downstream scope, not that all documentation work must stop.

| ID / question | Options | Recommendation | Evidence | Owner | Affected stories | Blocking status |
|---|---|---|---|---|---|---|
| D01: Which store owns reviewed tabular data during FGDB migration, and where is the FGDB? | Keep JSON temporarily; target FGDB tables with projections; another store | Stage ownership migration with one writer, explicit revision; preserve existing JSON loaders | Current review persistence/approval and case layout; proposed central FGDB | Winston with Product | 13.2/13.4/13.5/13.9; 11.2 overlap | Blocks persistence/migration code |
| D02: Which actual Innola identifiers map to tasks/transitions? | Verified stable definition IDs; configured aliases; manual target selection if ambiguous | Inspect read-only evidence; version mapping; never infer authority from names or parcel count | Current task/lifecycle services; exact definition metadata not verified | Winston + Innola administrator | 13.2/13.3/13.9/13.10 | Blocks server mapping/writeback; local design continues |
| D03: Who may correct in Compute while actual task is Compare? | Same authorized examiner; reassignment; explicit workflow return | Allow local correction only with explicit permissions/audit; define required reassignment/return before server writes | C01/C02; current lifecycle ownership gates | Product + Innola administrator; Mary captures rule | 13.2/13.3/13.8/13.9 | Blocks final correction/server-authorization contract |
| D04: Which metadata is mandatory by preset? | Core fields only; fuller task catalog; all extracted fields | Minimal required catalog by task; preserve optional equipment/north-arrow/party evidence without making absence automatically block | User requested reusable key values; metadata models/routes already exist | Product/Mary; domain examiners | 13.2/13.6/13.7/13.10 | Blocks task field gates; inventory can proceed |
| D05: When is Enterprise publish required, and are draft copies visible? | Accepted Compute handoff; explicit optional checkpoints; draft projection too | Initially accepted Compute handoff as candidate mandatory checkpoint; no draft overwrite of accepted geometry | User checkpoint request; current publish configuration/fallbacks | Product + GIS administration + Winston | 13.2/13.7/13.9 | Blocks checkpoint acceptance/side-effect plan |
| D06: How are newer remote/local revisions and two operators reconciled? | Exclusive writer with revision check; conflict review; automatic merge | Explicit ownership plus revision check; block stale remote writes; no automatic geometry merge | Current local/package and Enterprise recovery paths; proposed master FGDB | Winston + Product | 13.2/13.5/13.9 | Blocks authoritative write/conflict contract |
| D07: How do parcel identities/shared edges survive reruns, splits and merges? | Independent rings with adjacency links; normalized shared edges; topology/fabric representation | Stable IDs/lineage and dependency links first; evaluate storage without prematurely requiring Parcel Fabric | Manual parcel-scoped services and solver; 11.2 grouping plan; C03/C04 | Winston + GIS domain lead | 13.2/13.4/13.5/13.7 | Blocks multi-parcel consistency implementation |
| D08: How do reference-only PLA_B/RT satisfy completeness? | Read-only copied source FGDB; linked source plus bundled reference; source package restore | Preserve source TR/revision and all relevant accepted parcels; decide copy/link and validation scope explicitly | PLA_B related PE and RT read-only SpatialUnits; C04 | Product + Winston | 13.2/13.5/13.10 | Blocks reference-task schema/acceptance; no PE overwrite |
| D09: How can experimental add-in run beside baseline? | Separate test machine/profile; separate verified add-in identity; controlled replacement in test only | Isolated test environment initially; verify SDK/registration constraints before installation choice | Current wipRC9 usage; add-in packaging/registration shared beyond Git branches | Winston + deployment owner | 13.2 and first code/deployment story | Blocks experimental deployment, not documentation |
| D10: Which files/revisions are required in suspend and handoff packages? | Full case always; compact resume and full handoff; linked sources with integrity references | Complete resumable evidence, exact FGDB/tables revision; explicit source-size/retention rules and task documents | CaseResumePackageService conditional full-output inclusion; user ZIP requirement | Product + Winston | 13.2/13.5/13.9/13.10 | Blocks package schema/inclusion/restore parity |
| D11: How should existing Epic 11/backlog work be assigned? | Keep dependency; adapt scope with decision; replace after explicit acceptance mapping | Reuse 11.2 contract foundation; map each legacy AC; no automatic retirement | Pipeline inventory and sprint reconciliation | Mary + PM + Winston | 13.2 and implementation slicing | Blocks duplicative code story readiness |
| D12: Which proposed examination checks are mandatory versus dispositionable? | Fixed blockers; profile-specific effects; explicit exceptions | Reuse configured workflow_effect and task policies; authorized exceptions must be explicit and reported | Current stage/rule design; differing georeference permissions in PLA | Product/domain lead + Winston | 13.2/13.6/13.7/13.8 | Blocks final gate configuration |

## Architecture Input Package

2026-10-08 local implementation follow-up: JotaPe requested contract review and isolated loader/resolver tests. [Review and local engine record](../../implementation-artifacts/toolbox/toolbox-policy-contract-review-and-implementation.md) resolves executable-contract ambiguity with a strict isolated_test schema, trusted identity/hash pin, bounded applicability predicates and case-scoped immutable outputs. No production policy publication, baseline settings integration or final decision/remote authority is implemented; D04/D12 detailed domain bindings remain open.

2026-10-08 settings follow-up: at JotaPe's request, prepared an [isolated Toolbox review-policy settings design](toolbox-review-policy-settings-design.md) and a 42-item non-activatable JSON seed. Technical contract/predicate/effect bindings and publication authority remain proposed; C15 classification is unchanged. No active settings, source code, sprint status or deployment changes.

2026-10-08 D04/D12 refinement: JotaPe approved the [Compute/Compare classification v1](toolbox-compute-compare-checklist-matrix.md#proposed-stage-classification), covering all 42 items and documented applicability conditions (C15). Required examination is not compulsory source-value presence or a universal pass requirement. Classification approval is resolved; D04/D12 remain open only for detailed preset/effect/authorization mapping and versioned policy integration. No runtime settings changed.

13.2 inputs: [process definition](toolbox-process-definition.md), [inventory](toolbox-capability-inventory.md), [walkthroughs](toolbox-scenario-walkthroughs.md), this register and the [delivery plan](epic-13-toolbox-delivery-plan.md).

Architecture can analyze alternatives while decisions are open, but its execution gate remains accepted 13.1 process. Do not mark an affected downstream code story ready by assuming acceptance or inventing task metadata. Process review may accept the model while retaining clearly owned architecture questions.

## Story 13.2 Architecture Responses

The following responses form the architecture direction accepted on 2026-10-08. Their remaining gates are not supplied domain policies or successful feasibility tests. See [architecture](toolbox-architecture.md), [contracts](toolbox-contract-catalog.md), [parallel migration](toolbox-parallel-migration-plan.md) and [acceptance strategy](toolbox-acceptance-strategy.md).

| Decision | Proposed response and evidence gained | Remaining gate |
|---|---|---|
| D01 | Active FGDB under working with one CaseStore writer; C09 confirms TR-prefixed Toolbox filename. Migrate JSON ownership by phase and emit stamped compatibility projections. A01/A02 define tables and journal boundaries. | Filename confirmed; detailed schema/migration and F01/F02 recoverable commit/edit proof still required before cutover |
| D02 | Current source exposes task instance/name/process/assignment, but not verified definition IDs. List/start/transitions/complete routes and mapper aliases documented; fallback task ID cannot authorize mutation. | Read-only sanitized server evidence and mapping F04; no invented definition IDs |
| D03 | Selected Compute permits authorized local correction without changing actual task. Remote actions use fresh ownership/explicit target and preserve Compare read-only. | Product/Innola authority policy still required |
| D04 | Evidence tables distinguish raw/reviewed values, units, source and missing/negative/uncertain states; optional facts preserved. | Product/domain owner chooses mandatory field catalog by preset |
| D05 | Prefer accepted-revision checkpoint, no draft overwrite; exact same-service publication under verified exclusive-writer and rollback capability. Current service does separate delete/add per layer. | Mandatory checkpoint policy plus F05 actual target capability/concurrency proof |
| D06 | Local journal plus immutable accepted snapshots; remote expected prior revision and enforceable writer control. Read-before-write alone is not conflict protection. | Accept conflict policy and verify protected writer/lease mechanism |
| D07 | Stable IDs, shared boundary memberships/direction and split/merge lineage; rings are validated materialized geometry. | GIS domain review of shared-edge policy; F02/manual edit impact tests |
| D08 | Recommend copied immutable accepted source PE reference; current-task evidence editable, source geometry read-only; identity preserved. | Accept copy/retention and relevant-parcel completeness policy |
| D09 | Prefer isolated test machine/environment, separate worktree/cases/targets. Current DAML identity and registration remain operational. | F07 before installation; exact test environment owner/path to be chosen |
| D10 | Package v2 separates creating task from case identity, includes same-revision FGDB/tables/evidence and checksums; retain v1 checks/compatibility path. Current restore rejects task mismatch and replaces existing root. | Accept inclusion/retention/limits; F03/F06 prove snapshots/read-back/restore |
| D11 | Reuse 11.2 canonical contract and stage FGDB adaptation separately; proposed decomposition in migration plan. | Accept cross-track ownership and refined slices before code-story creation |
| D12 | Retain workflow_effect/policy versions with dependency-bound findings; automatic routes cannot infer approval or exception authority. | Product/domain severity, applicability and disposition rules still required |

Architecture direction was accepted on 2026-10-08. The remaining gates above still require domain policy, verified service evidence or implementation proof; architecture acceptance does not silently resolve them. Story 13.3 is limited to local context/selection, with no remote authorization or geometry changes. Other affected code remains backlog.

## C10 Exception Policy Clarification

The ability to forward recorded source-plan defects is confirmed by C10/C11, not an optional future feature or a generic waiver of extraction errors. The same authorized examiner records and forwards these defects through the normal next stage without a new default supervisor approval. D12 still defines the detailed classification/check catalog, completeness evidence and technical guardrails; it does not reopen this confirmed route/actor choice.

Extraction mismatch against a correct source must be corrected/rechecked before stage forwarding. For mixed errors, correct the extraction discrepancy first and retain the independently evidenced source defect. Record source-plan defects with stable finding ID, origin/type, case/parcel/document/page/zone, detection stage, raw/source versus reviewed evidence, rule/input revision, examiner/time, explanation and forwarding disposition. Unknown origin requires examination rather than being relabeled a source defect.

Use forwarded-with-recorded-defects for stage disposition, separately from geometry validation/approval. Earlier accepted-with-exceptions wording is an examination disposition only, not evidence that geometry is valid. Complete the applicable whole-document/stage review; record evaluated, not-applicable and source-defect-related not-evaluable checks with reasons. Preserve findings across stages, append later discoveries and retain resolution/history; rejection is based on the cumulative review, not just the first stage's error.

Exceptions cannot bypass wrong-case identity, invalid entity references, unknown authorization, read-only source protections, stale approval or package integrity. They do not fabricate missing geometry/artifacts, guarantee publication of unsupported geometry or remove an action's required inputs. Future 13.9 revalidates authority and carries findings/justifications into reports/package and handoff receipts.

## Review Record

- 2026-10-08: Mary produced the 13.1 documentation deliverables and submitted them for product review.
- Confirmed user choices C01-C08 are preserved; proposed matrix assignments and recommendations remain reviewable.
- Story 13.1 status: review. Product acceptance of these deliverables has not yet been recorded.
- 2026-10-08: JotaPe explicitly accepted the 13.1 process and authorized Story 13.2. Story 13.1 is done; architecture work may proceed. This does not resolve the individual open domain/API/deployment questions automatically.
- No independent runtime acceptance, architecture completion, live API mutations or production deployment occurred.
- 2026-10-08: Winston completed the 13.2 architecture proposal, contract outlines, isolation/migration plan and acceptance strategy. Submitted for review; architecture acceptance and feasibility proofs remain pending.
- 2026-10-08: JotaPe accepted the architecture and requested Story 13.3. Story 13.2 is done; prepare the isolated local context-only story. Outstanding policy and F01-F07 feasibility gates are not marked resolved/passed by this acceptance.
