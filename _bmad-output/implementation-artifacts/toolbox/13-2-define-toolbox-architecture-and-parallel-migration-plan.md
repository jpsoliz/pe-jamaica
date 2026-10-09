---
story_key: 13-2-define-toolbox-architecture-and-parallel-migration-plan
track: toolbox
story_type: documentation
owner: Winston - Architecture
created: 2026-10-08
depends_on: [13-1-define-toolbox-process-and-task-matrix]
execution_gate: accepted-process-definition
baseline_branch: wipRC9
baseline_commit: 5908f30617cdc49e09873635a30343da8aff9ad6
tracker: _bmad-output/implementation-artifacts/toolbox/sprint-status.yaml
---

# 13.2 - Define Toolbox Architecture And Parallel Migration Plan

Status: done

## Story

As a solution architect,
I want a contract-based Toolbox architecture and an isolated migration plan grounded in the accepted process,
so that existing tools can operate over a consistent single/multi-parcel case workspace without disrupting the operational add-in.

## Dependency And Scope

The specification is prepared now, but execution follows product acceptance of Story 13.1's process definition. Existing 13.1 outputs are not assumed to exist. If process decisions remain unresolved, identify affected architecture work and keep final readiness open.

Execute with Winston as documentation/design work. Produce architecture decisions, contract/schema outlines, deployment/isolation design, acceptance strategy and implementation slicing. Do not create a worktree, change code, migrate cases, install/register add-ins, publish geometry, mutate Innola or modify the operational tracker as part of this story.

## Acceptance Criteria

1. Given accepted 13.1 artifacts, when architecture is written, then every requirement maps to a component/contract/acceptance scenario, and any conflict with current architecture or Story 11.1/11.2 is explicitly documented as preserved behavior, compatibility adaptation or a proposed new decision.
2. Given the selected working preset and actual Innola task differ, when context and authorization are specified, then a versioned context contract represents both identities, target task instance/transition, current/source TR, operator, case path, parcel selection and input revision. Local capabilities do not acquire server authority through workspace selection; revalidation precedes each server mutation.
3. Given observed Innola workflow evidence, when task mapping is designed, then stable definition identifiers, transient task instance IDs and display aliases are distinguished. Exact fields/endpoints are evidence-backed or labeled unknown with an owner; unknown/ambiguous mappings fail closed for server actions. No live mutation is needed for design acceptance.
4. Given FGDB geometry and current JSON handoffs, when ownership is decided, then the design selects one authoritative writer per data category, specifies target geometry/table relationships and compatibility projections, and stages migration from existing extraction/approval/output formats without two editable truths. Include concrete conceptual schemas, keys and versioning rather than only a component diagram.
5. Given multi-parcel Cad Maps, when schema/revision rules are defined, then stable parcel/feature/evidence IDs, document-wide metadata, many-parcel parties/neighbors, shared-edge dependencies, candidate split/merge lineage and all-included acceptance are supported. Geometry updates cannot flatten groups or silently exclude failed parcels.
6. Given tool reuse, when the tool contract is defined, then it covers tool/version, selected scope, permissions, input revision, prerequisites, outputs, persisted findings, invalidation dependencies, metrics, progress/cancellation and retry/overwrite semantics. Identify real service owners and the minimum wrappers needed; do not invent a plugin framework without evidence of need.
7. Given approved/manual geometry edits and reruns, when consistency is designed, then saving, approval, solver regeneration, JSON exports, reports, Compare and Enterprise publishing consume identified current revisions. Specify correction provenance and how stale reviewed-row/GeoJSON fallbacks are rejected after FGDB edits.
8. Given Enterprise projection, when publication is specified, then exact FGDB revision, transaction/parcel/feature keys, target capability checks, upsert/replacement semantics, stale write conflicts, partial layer publication and idempotent receipts are defined. Geometry-only Enterprise recovery cannot masquerade as full case recovery or final Legal/Fiscal promotion.
9. Given package handoff, when snapshot/restore is designed, then save/quiesce, supported FGDB snapshot, integrity/schema/revision validation, evidence inclusion, package version, upload receipts and restore precedence are defined. Retries resume failed steps without duplicate uploads/transitions; excluding lock files alone is not snapshot consistency.
10. Given Compare, PLA and RT constraints, when consumers are mapped, then Compare reads accepted geometry without editing; Compute corrections invalidate dependent comparison; PLA_B/RT read linked PE geometry without changing its identity; task-specific generated documents, current-Plan writes and RT Save/Save & Close semantics remain distinct.
11. Given current operational usage, when parallel development is planned, then it specifies baseline selection, separate worktree/branch, transfer of untracked design artifacts, separate case/config/service targets, add-in identity/registration or isolated test environment, deployment acceptance and rollback. Branch isolation alone is not claimed to isolate data or the installed add-in. Actual creation/deployment belongs to later implementation scope.
12. Given the proposed architecture, when implementation readiness is assessed, then both single-parcel and multi-parcel vertical slices are planned, along with restart, manual edits, failed extraction, shared-edge changes, stale approvals, unknown task, conflicting remote revision and partial handoff failure coverage. Split oversized backlog rows and reconcile Epic 11 overlap before declaring code stories ready.
13. Given critical decisions need product/technical judgment, when the story is submitted, then an ADR/decision register identifies alternatives, recommendation, evidence, trade-offs, acceptance and pending owners. Architecture completion is not recorded while high-impact unresolved decisions are hidden; downstream affected code stories remain backlog.

## Tasks / Subtasks

- [x] Load accepted 13.1 outputs and map requirements/current architecture differences. (AC: 1, 13)
- [x] Inspect source ownership and observed read-only workflow/API evidence. (AC: 2, 3, 6, 10)
- [x] Specify context/task selection and authorization contracts. (AC: 2, 3)
- [x] Specify FGDB/table, parcel/shared-edge and migration/compatibility contracts. (AC: 4, 5)
- [x] Define reusable tool contracts, revision ownership and invalidation model. (AC: 6, 7)
- [x] Define Enterprise projection, conflicts and recovery model. (AC: 8)
- [x] Define snapshot/package, receipts, restore and retry model. (AC: 9)
- [x] Map Compare/PLA/RT integration and preserve their write boundaries. (AC: 10)
- [x] Define parallel environment/deployment/rollback strategy without executing it. (AC: 11)
- [x] Define fixtures, acceptance boundaries and revised implementation slicing. (AC: 12)
- [x] Submit architecture decisions for review. (AC: 13)
- [x] Record product/architecture acceptance before closing. (AC: 13)

## Dev Notes

Inspect current code rather than rely on old architecture starter paths. Preserve C#/WPF ArcGIS Pro orchestration, SDK-safe GIS operations and Python/ArcPy domain processing. Use current project/toolchain configuration as the compatibility baseline; no runtime/library upgrade is implied. Check official technical documentation when choosing concrete copy, edit, API or deployment mechanisms and record the relevant version/source.

Inspect, but do not modify, these existing boundaries:

- `CaseFolders/CaseFolderLayout.cs`, `CaseResumePackageService.cs`: current layout, ZIP inclusion and restore behavior.
- `Workflow/Review/ExtractionReviewDocument.cs`, `ExtractionReviewPersistenceService.cs`, `SurveyPlanBoundarySolver.cs`: current evidence, approval and deterministic geometry inputs.
- `Workflow/Output/OutputAdapterExecutionService.cs`, `JsonEnterpriseWorkingLayerPublishService.cs`, working-state restore services: output schema and payload/fallback paths.
- `Compare/CompareWorkingGeometryService.cs`, compare decision/report persistence: Enterprise loading and read-only evidence.
- `Workflow/Pla/` and `Workflow/RtExamination/InnolaRtExaminationService.cs`: linked-PE reference and current-task outputs/writeback.
- `Innola/InnolaTransactionLifecycleCoordinator.cs`, transaction/task models and settings: actual authorization/transition/package boundaries.
- `src/ProcessingTools/adapters/{pdf_text_structured_extraction,survey_plan_ocr_vision_extraction,validation_adapter,output_adapter}.py`: reusable routes and downstream contracts.

For every future UPDATE target, record current behavior, intended adaptation and preserved contracts. Current JSON approval remains authoritative until a deliberate tested migration. Shared-edge modeling needs an explicit choice; do not assume current independent rings provide topology-safe propagation.

### Expected Deliverables

- `_bmad-output/planning-artifacts/toolbox/toolbox-architecture.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-contract-catalog.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-parallel-migration-plan.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-acceptance-strategy.md`
- Updates to the Toolbox decision register and delivery plan; refine/split downstream code story scopes after acceptance.

These output files do not exist merely because the story is prepared. Read all existing UPDATE documents in full before modifying them during execution. Do not revise operational architecture, context or Epic 11 decisions silently.

### Verification And Definition Of Done

Trace ACs to document sections and 13.1 scenarios; validate diagrams, relative links, contract examples, schema keys and deployment boundaries. Mark observed behavior separately from proposals. Verify that every handoff action has explicit failure/retry semantics and that both single/multi-parcel compatibility is addressed. No runtime build/live API mutation is required for this documentation story. Technical feasibility not yet proven must remain a named risk/spike, not a claimed test pass.

Move to review when artifacts are complete. Done requires recorded architecture/product acceptance; creating the story or drawing a diagram is insufficient.

### References

- [Story 13.1](13-1-define-toolbox-process-and-task-matrix.md)
- [Epic 13](../../planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md)
- [Current proposal](../../planning-artifacts/toolbox-case-workspace-and-task-matrix-2026-10-08.md)
- [Baseline architecture](../../planning-artifacts/architecture.md)
- [Project context](../../project-context.md)
- [Story 11.1](../11-1-inventory-and-decision-lock-for-plan-examination-unification.md)
- [Story 11.2](../11-2-unified-pdf-extraction-contract-for-plan-examination.md)
- [Pipeline inventory](../../planning-artifacts/plan-examination-general/plan-examination-pipeline-inventory.md)
- [Esri FGDB handling guidance](https://doc.esri.com/en/arcgis-pro/latest/help/data/geodatabases/manage-file-gdb/file-geodatabases-and-windows-explorer.html)

## Dev Agent Record

### Agent Model Used

Codex, Winston architecture perspective. BMAD create-story prepared the specification; this execution produced the documentation and source-based architecture analysis.

### Debug Log References

Preparation used the confirmed process proposal, current baseline constraints and previous source inspection. No independent architecture execution, deployment experiment or runtime validation claimed.

Execution inspected SelectedInnolaTransaction, transaction list mapper/routes, lifecycle transition selection, package restore/inclusion, FeatureService publication, project framework and DAML identity. Consulted official Esri Copy/geodatabase and service/layer applyEdits documentation. Validation covers document links/tables, JSON example syntax, contract version consistency and AC traceability. No source changes, build/runtime tests, sandbox service mutations or deployment performed.

### Completion Notes List

- Architecture story specification prepared on 2026-10-08 with a hard execution dependency on accepted 13.1 process outputs.
- JotaPe accepted 13.1 on 2026-10-08; its execution gate is satisfied.
- Produced architecture, contract catalog, parallel migration/isolation plan and acceptance strategy. Defined 10 proposed ADRs, target schema/context/tool/revision/package/receipt contracts and seven required feasibility checks.
- Recorded migration constraints: task-bound legacy package restore, destructive case replacement, independent delete/add layer publication, fallback task identifiers/transitions and differing transaction scope mappings.
- Preserved baseline and Epic 11 boundaries; refined proposed implementation slices without adding unaccepted code substories or changing operational statuses.
- Submitted for review with named domain/service/deployment gates retained; no feasibility pass was claimed.
- On 2026-10-08 JotaPe accepted the architecture ("architecture ok, so story 13.3"). Story 13.2 is done. This accepts the design direction, not unverified service capabilities or outstanding domain policies. Story 13.3 may be prepared as the local context-only slice; affected later implementation remains gated.

### File List

- `_bmad-output/implementation-artifacts/toolbox/13-2-define-toolbox-architecture-and-parallel-migration-plan.md`
- `_bmad-output/implementation-artifacts/toolbox/13-1-define-toolbox-process-and-task-matrix.md`
- `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml`
- `_bmad-output/planning-artifacts/toolbox/toolbox-process-definition.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-architecture.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-contract-catalog.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-parallel-migration-plan.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-acceptance-strategy.md`
- `_bmad-output/planning-artifacts/toolbox/toolbox-decision-register.md`
- `_bmad-output/planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md`
