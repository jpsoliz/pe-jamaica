# Sprint Reconciliation Review

Date: 2026-10-08
Project: Sid-jamaica
Sources: `sprint-status.yaml`, numbered story files, `planning-artifacts/epics.md`, project context, and selected source-file inventory.

## Review Boundary

This is a documentation and completion-evidence audit, not a new code acceptance pass. Historical build/test notes were inspected; builds, automated tests, live Innola operations, and ArcGIS Pro smoke tests were not rerun. A checked checklist does not prove current runtime correctness. Missing story files do not prove missing functionality.

The tracker and story files have not been changed by this review. This report records proposed corrections and unresolved evidence so a cleanup can preserve completion history without inventing acceptance.

## Current Tracker

120 tracked stories: 30 done, 49 review, 9 in-progress, 11 ready-for-dev, 20 backlog, and 1 partially-implemented.

There are 133 numbered story files. Of these, 32 are absent from the tracker; 19 tracked backlog stories have no dedicated story file. The union contains 152 distinct file/tracker keys, not necessarily 152 independent delivery scopes.

| Epic | Tracked Stories | Done | Review | In Progress | Ready | Backlog | Partial |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | 6 | 6 | 0 | 0 | 0 | 0 | 0 |
| 2 | 31 | 14 | 11 | 0 | 4 | 2 | 0 |
| 3 | 5 | 0 | 0 | 0 | 0 | 5 | 0 |
| 4 | 15 | 2 | 6 | 4 | 1 | 2 | 0 |
| 5 | 22 | 0 | 11 | 3 | 3 | 5 | 0 |
| 6 | 5 | 0 | 0 | 0 | 0 | 5 | 0 |
| 7 | 15 | 1 | 10 | 1 | 1 | 1 | 1 |
| 8 | 13 | 3 | 9 | 0 | 1 | 0 | 0 |
| 9 | 2 | 1 | 0 | 1 | 0 | 0 | 0 |
| 10 | 1 | 0 | 1 | 0 | 0 | 0 | 0 |
| 11 | 2 | 0 | 1 | 0 | 1 | 0 | 0 |
| 12 | 3 | 3 | 0 | 0 | 0 | 0 | 0 |

## Closure Findings

1. **Done does not consistently mean acceptance complete.** Stories 2.6 and 2.7 have unchecked manual ArcGIS smoke tasks (lines 87 and 86 respectively). Story 2.23F is `done` in the tracker but `review` in its file. Its notes include historical live upload failure and later certificate/upload fixes, without a clear matching closure record. Reconcile the live verification result before retaining final closure.
2. **Review contains unfinished implementation.** Story 2.22 has six open tasks, including expired-session recovery and checking task state after ambiguous lifecycle failures. Story 4.10 has five open checklist entries covering rule templates, adding rules, and verification. Stories 5.16A and 5.16B each have two open product-alignment/persistence tasks. These are not merely review queues with fully finished scope.
3. **Seven story/tracker status discrepancies exist.** They are listed below. Story 5.23A is particularly inconsistent: its header says `implemented`, all 32 checklist entries remain open, and its change log establishes add-in-side wiring rather than complete end-to-end acceptance.
4. **32 story files are invisible to sprint tracking.** This includes implemented/review work as well as planned and superseded scope. Do not add superseded or coordinated parent stories as new active work without recording their relationship to delivery stories.
5. **Epic 12 is stale at the epic level.** All three tracked stories are done, but the epic remains in-progress. It is eligible for closure after confirming their acceptance; the optional retrospective is separate.
6. **Scope identity has drifted.** Story 4.4 in the tracker/epics is manual-process routing, while the existing 4.4 story file covers transaction output GDB generation. Two different files also use Story 8.4. Story 4.3 and Story 4.5 in the epic document have different titles/scope from their tracked story keys. Resolve identifiers and canonical scope before deduplication.
7. **Backlog cannot be read as functionality absent.** Extraction/review/output code exists (`CreateParcelDraftExtractionAdapter`, `ExtractionReviewPersistenceService`, `OutputAdapterExecutionService`, output summaries and map interfaces). Epic 3 and early Epic 5 backlog items need acceptance-criteria mapping against that implementation and later stories before planning new development.
8. **The status vocabulary is inconsistent.** Files use `ready-for-review`, `implemented`, `updated`, `draft`, `drafted`, `in progress`, and superseded variants. The tracker documents a local `partially-implemented` extension that the installed sprint-status skill does not recognize. Preserve this intentional partial state and reconcile workflow validation rather than converting it to done.

## Proposed Status Reconciliation

| Story | Tracker | Story File | Proposed Handling |
|---|---|---|---|
| 2.14A | ready-for-dev | review | Move tracker to review; acceptance still required. |
| 2.23F | done | review | Retain as unresolved closure conflict until review/live upload evidence is reconciled; conservatively use review. |
| 4.3 | ready-for-dev | updated | Normalize story header to ready-for-dev; notes document specification updates, not completed implementation. |
| 5.23A | ready-for-dev | implemented | Audit actual coverage and reconcile checklist; do not promote to done from the header alone. |
| 5.6 | ready-for-dev | ready-for-review | Normalize both to review. |
| 5.7 | ready-for-dev | ready-for-review | Normalize both to review. |
| 7.4 | backlog | drafted | Draft exists; confirm readiness before promoting to ready-for-dev. |

For 2.6 and 2.7, retain the distinction between implemented and acceptance complete. Close the recorded manual smoke tasks with evidence, or return the stories to review. Do not check the boxes without running the checks.

Story 7.15 must remain partial. Its partial implementation note explicitly requires live Legal/Cadastral candidate discovery validation. `replace_existing` and `merge_update_attributes_only` are deliberately non-executable and need future scope decisions; later completion patches do not establish that these actions are implemented.

## Recorded Done Without a Status/Checklist Conflict

These 27 stories agree between the tracker and their file and have no unchecked checklist items. This is document consistency, not fresh runtime certification:

[1-1](./1-1-set-up-arcgis-pro-add-in-and-processing-scaffold.md), [1-2](./1-2-create-transaction-case-folder.md), [1-3](./1-3-add-and-copy-source-files-to-the-case-folder.md), [1-4](./1-4-detect-source-input-profile.md), [1-5](./1-5-reopen-and-resume-existing-case-folder.md), [1-6](./1-6-open-or-reveal-source-files-from-intake.md), [2-1](./2-1-run-manifest-preflight.md), [2-2](./2-2-add-sidwell-shell-innola-login-and-session-gating.md), [2-4](./2-4-load-transaction-details-and-attachments-into-case-folder.md), [2-5](./2-5-control-active-transaction-lifecycle-and-completion-gate.md), [2-8a](./2-8a-wire-live-innola-api-contracts-while-preserving-mock-mode.md), [2-11](./2-11-workflow-rule-resolution-and-script-plan-manifest.md), [2-20](./2-20-add-pxa-survey-plan-metadata-review-model-and-ux.md), [2-23a](./2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md), [2-23b](./2-23b-add-pla-plan-annexation-pdf-page-selection-and-evidence-artifact.md), [2-23c](./2-23c-add-pla-selected-plan-extraction-review-and-local-origin-geometry.md), [2-23g](./2-23g-wire-pla-b-plan-annexation-task-workflow-and-complete-flow.md), [4-1](./4-1-run-validation-on-approved-review-data.md), [4-12](./4-12-improve-pe-pxa-memorandum-extraction-semantic-review-rules.md), [7-11](./7-11-write-innola-plan-check-list-on-compute-finalize.md), [8-4d](./8-4d-add-collapsible-pdf-panel-to-compare-workspace.md), [8-7](./8-7-add-parcel-search-dockpane-tab.md), [8-8](./8-8-add-rt-examination-linked-pe-review-and-writeback.md), [9-2](./9-2-add-supporting-documents-full-panel-viewer.md), [12-1](./12-1-current-testing-review-ux-and-extraction-polish.md), [12-2](./12-2-centralize-innola-session-refresh-for-workflow-finalize.md), [12-3](./12-3-add-computed-participants-and-title-image-download-to-compare.md).

Older completed stories 2.2, 2.4, and 2.5 also describe manual UI smoke as user-run despite checked packaging/smoke entries. Clarify historical acceptance evidence during release validation. Historical broader test failures are recorded in 2.23C and 2.23G; targeted success does not establish a clean full suite.

## Untracked Stories

| Story File | Recorded Status | Open Checklist Entries | Handling |
|---|---|---:|---|
| [2-12](./2-12-execute-draft-extraction-and-review-artifact-generation.md): execute draft extraction and review artifact generation | ready-for-dev | 48 | Untracked planned scope; confirm overlap and delivery |
| [2-13](./2-13-review-edit-point-workflow.md): review edit point workflow | review | 0 | Untracked review item |
| [2-15a](./2-15a-route-supported-transactions-into-compute-workflow-and-align-compute-ui-labels.md): route supported transactions into compute workflow and align compute ui labels | ready-for-review | 0 | Untracked review item |
| [2-23](./2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md): add pla plan annexation pdf selection extraction and review | superseded-by-split-stories | 48 | Superseded; retain history, exclude from active backlog |
| [4-4](./4-4-generate-transaction-output-gdb-from-approved-review-data.md): generate transaction output gdb from approved review data | review | 0 | Untracked review item |
| [5-9](./5-9-add-map-review-editing-toolbar-for-spatial-correction-workflows.md): add map review editing toolbar for spatial correction workflows | drafted | 25 | Untracked planned scope; confirm overlap and delivery |
| [5-10](./5-10-evaluate-supported-arcgis-pro-automation-boundary-for-cogo-reader-assist-vs-custom-transaction-controlled-extraction-flow.md): evaluate supported arcgis pro automation boundary for cogo reader assist vs custom transaction controlled extraction flow | ready-for-review | 0 | Untracked review item |
| [5-11](./5-11-design-jamaica-cogo-style-review-workspace.md): design jamaica cogo style review workspace | ready-for-dev | 32 | Untracked planned scope; confirm overlap and delivery |
| [5-12](./5-12-define-architecture-for-jamaica-cogo-style-review-workspace.md): define architecture for jamaica cogo style review workspace | ready-for-dev | 29 | Untracked planned scope; confirm overlap and delivery |
| [5-14](./5-14-replace-embedded-pdf-browser-with-unified-rendered-document-viewer-for-pdf-and-raster-verification.md): replace embedded pdf browser with unified rendered document viewer for pdf and raster verification | review | 0 | Untracked review item |
| [5-15](./5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md): parcel scoped manual point editing and live parcel preview controls in jamaica cogo tool | review | 0 | Untracked review item |
| [5-15a](./5-15a-add-modal-point-editor-for-parcel-scoped-point-add-edit-in-points-validation-tool.md): add modal point editor for parcel scoped point add edit in points validation tool | review | 0 | Untracked review item |
| [5-16c](./5-16c-realign-early-compute-stages-around-document-structure-and-georeference-checks.md): realign early compute stages around document structure and georeference checks | superseded | 11 | Superseded; retain history, exclude from active backlog |
| [5-16d](./5-16d-externalize-data-extraction-rules-by-transaction-and-source-type.md): externalize data extraction rules by transaction and source type | superseded | 20 | Superseded; retain history, exclude from active backlog |
| [5-16e](./5-16e-coordinate-early-compute-stage-realignment-with-externalized-document-structure-and-georeference-rule-catalogs.md): coordinate early compute stage realignment with externalized document structure and georeference rule catalogs | implemented | 0 | Untracked implementation; acceptance review needed |
| [5-16f](./5-16f-configure-supporting-document-source-types-and-attachment-role-rules-for-compute-intake.md): configure supporting document source types and attachment role rules for compute intake | implemented | 0 | Untracked implementation; acceptance review needed |
| [5-17](./5-17-add-manual-cogo-fallback-branch-from-point-review.md): add manual cogo fallback branch from point review | review | 0 | Untracked review item |
| [5-22](./5-22-group-transaction-review-layers-and-ground-cogo-diagnostics-in-final-feature-class-state.md): group transaction review layers and ground cogo diagnostics in final feature class state | review | 0 | Untracked review item |
| [5-23](./5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md): add parcel type aware closure tolerance validation to validate points and final review | review | 0 | Untracked review item |
| [5-24](./5-24-add-whole-review-parcel-context-and-active-parcel-diagnostics-to-points-validation-preview.md): add whole review parcel context and active parcel diagnostics to points validation preview | review | 0 | Untracked review item |
| [5-25](./5-25-externalize-parcel-construction-readiness-rules-for-gaps-shared-edges-and-boundary-completeness.md): externalize parcel construction readiness rules for gaps shared edges and boundary completeness | draft | 33 | Untracked planned scope; confirm overlap and delivery |
| [5-25a](./5-25a-expose-parcel-construction-readiness-rules-in-settings-workspace.md): expose parcel construction readiness rules in settings workspace | review | 0 | Untracked review item |
| [5-25d](./5-25d-apply-points-validation-tool-icon-and-action-deduplication-polish.md): apply points validation tool icon and action deduplication polish | review | 0 | Untracked review item |
| [5-25e](./5-25e-integrate-calcite-esri-svg-icons-into-points-validation-tool.md): integrate calcite esri svg icons into points validation tool | review | 0 | Untracked review item |
| [7-6](./7-6-define-enterprise-working-parcel-fabric-contract-and-configuration.md): define enterprise working parcel fabric contract and configuration | review | 0 | Untracked review item |
| [8-4](./8-4-parcel-scoped-geometry-review-option-a.md): parcel scoped geometry review option a | review | 0 | Untracked review item |
| [8-4f](./8-4f-add-parcel-selection-owner-lookup-launch-from-search-and-map-popup.md): add parcel selection owner lookup launch from search and map popup | drafted | 6 | Untracked planned scope; confirm overlap and delivery |
| [8-4g](./8-4g-add-overlap-review-and-innola-owner-enrichment-report-for-compute-and-compare.md): add overlap review and innola owner enrichment report for compute and compare | drafted | 8 | Untracked planned scope; confirm overlap and delivery |
| [8-4h](./8-4h-add-button-driven-spatial-overlap-engine-for-compute-and-compare.md): add button driven spatial overlap engine for compute and compare | review | 0 | Untracked review item |
| [8-4i](./8-4i-add-overlap-review-surface-and-evidence-report-model-for-compute-and-compare.md): add overlap review surface and evidence report model for compute and compare | review | 0 | Untracked review item |
| [8-4j](./8-4j-add-innola-owner-enrichment-for-overlap-review-results.md): add innola owner enrichment for overlap review results | drafted | 0 | Untracked planned scope; confirm overlap and delivery |
| [8-5a](./8-5a-generate-compare-review-report-artifacts.md): generate compare review report artifacts | in progress | 4 | Untracked active implementation |

Explicit supersession: 2.23 was split into delivery stories; 5.16C and 5.16D were superseded by 5.16E. Story 8.4G is a coordinated parent for 8.4H/8.4I/8.4J, not three additional independent copies of the same scope.

Story 5.25 remains draft with 33 open entries while 5.25A is in review and says it reuses the already-persisted 5.25 contract. This dependency needs code/evidence reconciliation before scheduling the parent as wholly new work.

## Cleanup Order

1. Resolve the seven discrepancies, the 2.6/2.7 manual checks, and Epic 12's closure.
2. Assign canonical IDs to conflicting 4.4 and 8.4 scopes; align epic-document titles with current stories.
3. Register active untracked delivery work, with explicit parent/superseded relationships.
4. Reconcile open implementation tasks in review stories, especially 2.22, 4.10, 5.16A/B and 5.23A.
5. Map legacy Epic 3 / Epic 5 backlog acceptance criteria to current code and later delivery stories. Classify each as covered, partially covered, superseded, or still needed.
6. Establish a current build/test baseline and complete the ArcGIS/live acceptance checks required to close remaining review work.

## Complete Inventory

Open counts include parent and child checklist entries; they are evidence indicators, not independent task counts. A zero count does not imply completion (for example 7.15 and 8.4J).

| Story / Scope Key | Tracker | Story File | Open Entries | Review Assessment |
|---|---|---|---:|---|
| [1-1](./1-1-set-up-arcgis-pro-add-in-and-processing-scaffold.md): set up arcgis pro add in and processing scaffold | done | done | 0 | Recorded done; no status/checklist conflict found |
| [1-2](./1-2-create-transaction-case-folder.md): create transaction case folder | done | done | 0 | Recorded done; no status/checklist conflict found |
| [1-3](./1-3-add-and-copy-source-files-to-the-case-folder.md): add and copy source files to the case folder | done | done | 0 | Recorded done; no status/checklist conflict found |
| [1-4](./1-4-detect-source-input-profile.md): detect source input profile | done | done | 0 | Recorded done; no status/checklist conflict found |
| [1-5](./1-5-reopen-and-resume-existing-case-folder.md): reopen and resume existing case folder | done | done | 0 | Recorded done; no status/checklist conflict found |
| [1-6](./1-6-open-or-reveal-source-files-from-intake.md): open or reveal source files from intake | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-1](./2-1-run-manifest-preflight.md): run manifest preflight | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-2](./2-2-add-sidwell-shell-innola-login-and-session-gating.md): add sidwell shell innola login and session gating | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-3](./2-3-display-available-innola-transactions.md): display available innola transactions | review | review | 0 | Awaiting acceptance/review closure |
| [2-4](./2-4-load-transaction-details-and-attachments-into-case-folder.md): load transaction details and attachments into case folder | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-5](./2-5-control-active-transaction-lifecycle-and-completion-gate.md): control active transaction lifecycle and completion gate | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-6](./2-6-validate-arcgis-pro-and-python-processing-environment.md): validate arcgis pro and python processing environment | done | done | 1 | Implementation recorded; manual smoke outstanding |
| [2-7](./2-7-enforce-transaction-execution-state-and-panel-locking.md): enforce transaction execution state and panel locking | done | done | 1 | Implementation recorded; manual smoke outstanding |
| [2-8](./2-8-validate-dwg-readiness-when-present.md): validate dwg readiness when present | ready-for-dev | ready-for-dev | 35 | Prepared scope; completion not established |
| [2-8a](./2-8a-wire-live-innola-api-contracts-while-preserving-mock-mode.md): wire live innola api contracts while preserving mock mode | done | done | 0 | Recorded done; no status/checklist conflict found |
| 2-9: configure processing and credential profiles | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 2-10: display preflight results and gate extraction | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| [2-11](./2-11-workflow-rule-resolution-and-script-plan-manifest.md): workflow rule resolution and script plan manifest | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-12](./2-12-execute-draft-extraction-and-review-artifact-generation.md): execute draft extraction and review artifact generation | not tracked | ready-for-dev | 48 | Untracked planned scope; confirm overlap and delivery |
| [2-12a](./2-12a-introduce-document-type-catalog-v2-for-extraction-routing.md): introduce document type catalog v2 for extraction routing | review | review | 0 | Awaiting acceptance/review closure |
| [2-13](./2-13-review-edit-point-workflow.md): review edit point workflow | not tracked | review | 0 | Untracked review item |
| [2-14a](./2-14a-redesign-extraction-review-workspace-around-source-document-verification.md): redesign extraction review workspace around source document verification | ready-for-dev | review | 0 | Tracker stale; story ready for review |
| [2-15](./2-15-gate-supported-transaction-types-before-workflow-load.md): gate supported transaction types before workflow load | ready-for-dev | ready-for-dev | 20 | Prepared scope; completion not established |
| [2-15a](./2-15a-route-supported-transactions-into-compute-workflow-and-align-compute-ui-labels.md): route supported transactions into compute workflow and align compute ui labels | not tracked | ready-for-review | 0 | Untracked review item |
| [2-16](./2-16-apply-document-type-catalog-v2-to-multi-source-extraction-pipelines.md): apply document type catalog v2 to multi source extraction pipelines | review | review | 0 | Awaiting acceptance/review closure |
| [2-16b](./2-16b-add-embedded-pdf-text-first-structured-computation-extraction.md): add embedded pdf text first structured computation extraction | review | review | 0 | Awaiting acceptance/review closure |
| [2-17](./2-17-stabilize-transaction-return-and-refresh-after-workflow-exit.md): stabilize transaction return and refresh after workflow exit | review | review | 0 | Awaiting acceptance/review closure |
| [2-18](./2-18-add-single-parcel-survey-plan-pdf-metadata-and-geometry-extraction.md): add single parcel survey plan pdf metadata and geometry extraction | review | review | 0 | Awaiting acceptance/review closure |
| [2-18a](./2-18a-add-transaction-type-workflow-profiles-for-pe-and-pxa-source-requirements.md): add transaction type workflow profiles for pe and pxa source requirements | review | review | 0 | Awaiting acceptance/review closure |
| [2-19](./2-19-implement-pxa-survey-plan-segment-review-and-deterministic-boundary-solver.md): implement pxa survey plan segment review and deterministic boundary solver | review | review | 0 | Awaiting acceptance/review closure |
| [2-20](./2-20-add-pxa-survey-plan-metadata-review-model-and-ux.md): add pxa survey plan metadata review model and ux | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-21](./2-21-add-innola-arcgis-pro-in-progress-workflow-stage.md): add innola arcgis pro in progress workflow stage | ready-for-dev | ready-for-dev | 36 | Prepared scope; completion not established |
| [2-22](./2-22-add-innola-api-connection-health-retry-and-session-recovery.md): add innola api connection health retry and session recovery | review | review | 6 | Review with open work; cannot close |
| [2-23](./2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md): add pla plan annexation pdf selection extraction and review | not tracked | superseded-by-split-stories | 48 | Superseded; retain history, exclude from active backlog |
| [2-23a](./2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md): add pla transaction profile source type and doc type resolution | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-23b](./2-23b-add-pla-plan-annexation-pdf-page-selection-and-evidence-artifact.md): add pla plan annexation pdf page selection and evidence artifact | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-23c](./2-23c-add-pla-selected-plan-extraction-review-and-local-origin-geometry.md): add pla selected plan extraction review and local origin geometry | done | done | 0 | Recorded done; no status/checklist conflict found |
| [2-23d](./2-23d-add-pla-visual-comparison-and-finalize-upload-flow.md): add pla visual comparison and finalize upload flow | review | review | 0 | Awaiting acceptance/review closure |
| [2-23e](./2-23e-add-pla-b-plan-annexation-from-pe-workflow-and-test-ux.md): add pla b plan annexation from pe workflow and test ux | review | review | 0 | Awaiting acceptance/review closure |
| [2-23f](./2-23f-add-crop-and-attach-action-to-supporting-documents-viewer.md): add crop and attach action to supporting documents viewer | done | review | 0 | Closure conflict; reconcile review evidence |
| [2-23g](./2-23g-wire-pla-b-plan-annexation-task-workflow-and-complete-flow.md): wire pla b plan annexation task workflow and complete flow | done | done | 0 | Recorded done; no status/checklist conflict found |
| 3-1: run extraction from passed preflight | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 3-2: handle zero or failed extraction results | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 3-3: display extraction review table | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 3-4: edit add or mark review records | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 3-5: approve extraction review data | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| [4-1](./4-1-run-validation-on-approved-review-data.md): run validation on approved review data | done | done | 0 | Recorded done; no status/checklist conflict found |
| 4-2: display validation findings by severity | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| [4-2a](./4-2a-redesign-parcel-workflow-into-stage-focused-workspace.md): redesign parcel workflow into stage focused workspace | review | review | 0 | Awaiting acceptance/review closure |
| [4-2b](./4-2b-add-embedded-source-viewer-for-pdf-tiff-image-verification.md): add embedded source viewer for pdf tiff image verification | review | review | 0 | Awaiting acceptance/review closure |
| [4-3](./4-3-save-and-resume-transaction-cases-through-innola-resume-package.md): save and resume transaction cases through innola resume package | ready-for-dev | updated | 32 | Specification updated; implementation not established by story record |
| [4-4](./4-4-generate-transaction-output-gdb-from-approved-review-data.md): generate transaction output gdb from approved review data | not tracked | review | 0 | Untracked review item |
| 4-4: route case to manual process by user decision | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| [4-5](./4-5-externalize-configurable-preflight-rules-and-expose-them-in-configuration-panel.md): externalize configurable preflight rules and expose them in configuration panel | review | review | 0 | Awaiting acceptance/review closure |
| [4-6](./4-6-add-extraction-result-decision-gate-for-rerun-vs-manual-review.md): add extraction result decision gate for rerun vs manual review | review | review | 0 | Awaiting acceptance/review closure |
| [4-7](./4-7-rename-preflight-rules-to-structure-rules-and-add-configurable-dwg-cad-layer-validation.md): rename preflight rules to structure rules and add configurable dwg cad layer validation | in-progress | in-progress | 10 | Active; open implementation/verification tasks |
| [4-8](./4-8-split-structure-check-and-dimension-check-into-separate-actions-and-result-summaries.md): split structure check and dimension check into separate actions and result summaries | in-progress | in-progress | 4 | Active; open implementation/verification tasks |
| [4-9](./4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md): add georeference check stage and reportable stage findings model | in-progress | in-progress | 6 | Active; open implementation/verification tasks |
| [4-10](./4-10-add-configurable-compute-rule-catalog-by-stage.md): add configurable compute rule catalog by stage | review | review | 5 | Review with open work; cannot close |
| [4-11](./4-11-add-pxa-memorandum-detection-and-review-rules.md): add pxa memorandum detection and review rules | review | review | 0 | Awaiting acceptance/review closure |
| [4-12](./4-12-improve-pe-pxa-memorandum-extraction-semantic-review-rules.md): improve pe pxa memorandum extraction semantic review rules | done | done | 0 | Recorded done; no status/checklist conflict found |
| [4-13](./4-13-add-parish-boundary-and-document-consistency-validation-rules.md): add parish boundary and document consistency validation rules | in-progress | in-progress | 4 | Active; open implementation/verification tasks |
| 5-1: generate local output package from passed validation | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 5-2: create gdb feature classes and geojson | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 5-3: generate reports and diagnostic logs | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 5-4: display output artifact list | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 5-5: add generated layers to active arcgis pro map | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| [5-6](./5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md): add spatial review stage for in map editing and manual cogo | ready-for-dev | ready-for-review | 0 | Tracker stale; story ready for review |
| [5-7](./5-7-evaluate-parcel-fabric-review-workspace-pilot.md): evaluate parcel fabric review workspace pilot | ready-for-dev | ready-for-review | 0 | Tracker stale; story ready for review |
| [5-8](./5-8-implement-true-local-parcel-fabric-output-mode.md): implement true local parcel fabric output mode | review | review | 0 | Awaiting acceptance/review closure |
| [5-9](./5-9-add-map-review-editing-toolbar-for-spatial-correction-workflows.md): add map review editing toolbar for spatial correction workflows | not tracked | drafted | 25 | Untracked planned scope; confirm overlap and delivery |
| [5-10](./5-10-evaluate-supported-arcgis-pro-automation-boundary-for-cogo-reader-assist-vs-custom-transaction-controlled-extraction-flow.md): evaluate supported arcgis pro automation boundary for cogo reader assist vs custom transaction controlled extraction flow | not tracked | ready-for-review | 0 | Untracked review item |
| [5-11](./5-11-design-jamaica-cogo-style-review-workspace.md): design jamaica cogo style review workspace | not tracked | ready-for-dev | 32 | Untracked planned scope; confirm overlap and delivery |
| [5-12](./5-12-define-architecture-for-jamaica-cogo-style-review-workspace.md): define architecture for jamaica cogo style review workspace | not tracked | ready-for-dev | 29 | Untracked planned scope; confirm overlap and delivery |
| [5-13](./5-13-build-dev-spike-for-jamaica-cogo-style-review-workspace-shell.md): build dev spike for jamaica cogo style review workspace shell | review | review | 0 | Awaiting acceptance/review closure |
| [5-14](./5-14-replace-embedded-pdf-browser-with-unified-rendered-document-viewer-for-pdf-and-raster-verification.md): replace embedded pdf browser with unified rendered document viewer for pdf and raster verification | not tracked | review | 0 | Untracked review item |
| [5-15](./5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md): parcel scoped manual point editing and live parcel preview controls in jamaica cogo tool | not tracked | review | 0 | Untracked review item |
| [5-15a](./5-15a-add-modal-point-editor-for-parcel-scoped-point-add-edit-in-points-validation-tool.md): add modal point editor for parcel scoped point add edit in points validation tool | not tracked | review | 0 | Untracked review item |
| [5-16](./5-16-align-compute-workflow-stage-copy-and-jamaica-cogo-handoff.md): align compute workflow stage copy and jamaica cogo handoff | review | review | 0 | Awaiting acceptance/review closure |
| [5-16a](./5-16a-realign-compute-workflow-vocabulary-around-data-extraction-and-points-validation.md): realign compute workflow vocabulary around data extraction and points validation | review | review | 2 | Review with open work; cannot close |
| [5-16b](./5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md): implement points validation tool save return flow and downstream stage handoff | review | review | 2 | Review with open work; cannot close |
| [5-16c](./5-16c-realign-early-compute-stages-around-document-structure-and-georeference-checks.md): realign early compute stages around document structure and georeference checks | not tracked | superseded | 11 | Superseded; retain history, exclude from active backlog |
| [5-16d](./5-16d-externalize-data-extraction-rules-by-transaction-and-source-type.md): externalize data extraction rules by transaction and source type | not tracked | superseded | 20 | Superseded; retain history, exclude from active backlog |
| [5-16e](./5-16e-coordinate-early-compute-stage-realignment-with-externalized-document-structure-and-georeference-rule-catalogs.md): coordinate early compute stage realignment with externalized document structure and georeference rule catalogs | not tracked | implemented | 0 | Untracked implementation; acceptance review needed |
| [5-16f](./5-16f-configure-supporting-document-source-types-and-attachment-role-rules-for-compute-intake.md): configure supporting document source types and attachment role rules for compute intake | not tracked | implemented | 0 | Untracked implementation; acceptance review needed |
| [5-17](./5-17-add-manual-cogo-fallback-branch-from-point-review.md): add manual cogo fallback branch from point review | not tracked | review | 0 | Untracked review item |
| [5-18](./5-18-route-manual-review-branch-into-configured-gdb-map-editing-path.md): route manual review branch into configured gdb map editing path | review | review | 0 | Awaiting acceptance/review closure |
| [5-19](./5-19-define-cogo-ready-non-fabric-output-layer-schema.md): define cogo ready non fabric output layer schema | review | review | 0 | Awaiting acceptance/review closure |
| [5-20](./5-20-configure-cogo-style-map-symbology-labeling-and-editing-experience-for-non-fabric-spatial-outputs.md): configure cogo style map symbology labeling and editing experience for non fabric spatial outputs | review | review | 0 | Awaiting acceptance/review closure |
| [5-21](./5-21-add-optional-cogo-attributes-and-labels-to-non-fabric-spatial-output-layers.md): add optional cogo attributes and labels to non fabric spatial output layers | review | review | 0 | Awaiting acceptance/review closure |
| [5-22](./5-22-group-transaction-review-layers-and-ground-cogo-diagnostics-in-final-feature-class-state.md): group transaction review layers and ground cogo diagnostics in final feature class state | not tracked | review | 0 | Untracked review item |
| [5-23](./5-23-add-parcel-type-aware-closure-tolerance-validation-to-validate-points-and-final-review.md): add parcel type aware closure tolerance validation to validate points and final review | not tracked | review | 0 | Untracked review item |
| [5-23a](./5-23a-add-orientation-detection-bearing-consistency-and-optional-orientation-normalization.md): add orientation detection bearing consistency and optional orientation normalization | ready-for-dev | implemented | 32 | Partial evidence; implemented header conflicts with 32 unchecked tasks |
| [5-24](./5-24-add-whole-review-parcel-context-and-active-parcel-diagnostics-to-points-validation-preview.md): add whole review parcel context and active parcel diagnostics to points validation preview | not tracked | review | 0 | Untracked review item |
| [5-25](./5-25-externalize-parcel-construction-readiness-rules-for-gaps-shared-edges-and-boundary-completeness.md): externalize parcel construction readiness rules for gaps shared edges and boundary completeness | not tracked | draft | 33 | Untracked planned scope; confirm overlap and delivery |
| [5-25a](./5-25a-expose-parcel-construction-readiness-rules-in-settings-workspace.md): expose parcel construction readiness rules in settings workspace | not tracked | review | 0 | Untracked review item |
| [5-25b](./5-25b-polish-points-validation-tool-visual-hierarchy-and-preview-readability.md): polish points validation tool visual hierarchy and preview readability | review | review | 0 | Awaiting acceptance/review closure |
| [5-25c](./5-25c-refine-points-validation-preview-modes-and-rule-status-readability.md): refine points validation preview modes and rule status readability | review | review | 0 | Awaiting acceptance/review closure |
| [5-25d](./5-25d-apply-points-validation-tool-icon-and-action-deduplication-polish.md): apply points validation tool icon and action deduplication polish | not tracked | review | 0 | Untracked review item |
| [5-25e](./5-25e-integrate-calcite-esri-svg-icons-into-points-validation-tool.md): integrate calcite esri svg icons into points validation tool | not tracked | review | 0 | Untracked review item |
| [5-26](./5-26-configure-default-working-map-and-reference-layers.md): configure default working map and reference layers | in-progress | in-progress | 4 | Active; open implementation/verification tasks |
| [5-27](./5-27-add-map-georeference-review-launcher-and-workflow.md): add map georeference review launcher and workflow | in-progress | in-progress | 6 | Active; open implementation/verification tasks |
| [5-28](./5-28-add-assisted-title-plan-image-placement-for-map-comparison.md): add assisted title plan image placement for map comparison | in-progress | in-progress | 14 | Active; open implementation/verification tasks |
| 6-1: display cadindex sync readiness facade | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 6-2: record enterprise candidate metadata | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 6-3: preserve end to end audit trail | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 6-4: create case 1 4 acceptance fixture contracts | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| 6-5: validate fixture runs and recovery behavior | backlog | no file | - | Backlog only; validate existing coverage before implementing |
| [7-1](./7-1-define-enterprise-working-review-layer-schema-and-configuration.md): define enterprise working review layer schema and configuration | review | review | 0 | Awaiting acceptance/review closure |
| [7-1a](./7-1a-expose-review-workspace-mode-and-enterprise-targets-in-configuration-panel.md): expose review workspace mode and enterprise targets in configuration panel | review | review | 0 | Awaiting acceptance/review closure |
| [7-2](./7-2-publish-approved-review-geometry-to-enterprise-working-layers.md): publish approved review geometry to enterprise working layers | review | review | 0 | Awaiting acceptance/review closure |
| [7-3](./7-3-restore-transaction-working-state-from-enterprise-review-layers.md): restore transaction working state from enterprise review layers | review | review | 0 | Awaiting acceptance/review closure |
| [7-4](./7-4-promote-working-review-geometry-to-sync-ready-authoritative-package.md): promote working review geometry to sync ready authoritative package | backlog | drafted | 9 | Draft exists; not accepted complete |
| [7-5](./7-5-refactor-configuration-into-editable-settings-workspace-with-functional-tabs.md): refactor configuration into editable settings workspace with functional tabs | review | review | 0 | Awaiting acceptance/review closure |
| [7-6](./7-6-define-enterprise-working-parcel-fabric-contract-and-configuration.md): define enterprise working parcel fabric contract and configuration | not tracked | review | 0 | Untracked review item |
| [7-7](./7-7-publish-validated-spatial-units-into-enterprise-working-parcel-fabric.md): publish validated spatial units into enterprise working parcel fabric | ready-for-dev | ready-for-dev | 11 | Prepared scope; completion not established |
| [7-8](./7-8-add-enterprise-working-layer-admin-provisioning-and-maintenance-settings-tab.md): add enterprise working layer admin provisioning and maintenance settings tab | review | review | 0 | Awaiting acceptance/review closure |
| [7-9](./7-9-record-compute-final-review-disposition-and-closeout-enterprise-working-layer.md): record compute final review disposition and closeout enterprise working layer | review | review | 0 | Awaiting acceptance/review closure |
| [7-10](./7-10-add-enterprise-working-layer-default-visualization-labeling.md): add enterprise working layer default visualization labeling | review | review | 0 | Awaiting acceptance/review closure |
| [7-11](./7-11-write-innola-plan-check-list-on-compute-finalize.md): write innola plan check list on compute finalize | done | done | 0 | Recorded done; no status/checklist conflict found |
| [7-12](./7-12-create-innola-compute-review-web-map-view-for-completed-transactions.md): create innola compute review web map view for completed transactions | in-progress | in-progress | 32 | Active; open implementation/verification tasks |
| [7-13](./7-13-add-internal-arcgis-portal-auth-provider-for-enterprise-operations.md): add internal arcgis portal auth provider for enterprise operations | review | review | 0 | Awaiting acceptance/review closure |
| [7-14](./7-14-attach-formatted-compute-finalize-report-to-innola-transaction.md): attach formatted compute finalize report to innola transaction | review | review | 0 | Awaiting acceptance/review closure |
| [7-15](./7-15-add-fabric-maintenance-promotion-review-from-working-review-to-final-cadastre.md): add fabric maintenance promotion review from working review to final cadastre | partially-implemented | partially-implemented | 0 | Intentionally partial; live validation and future-write decisions remain |
| [8-1](./8-1-add-compare-stage-gating-and-transaction-launch.md): add compare stage gating and transaction launch | review | review | 0 | Awaiting acceptance/review closure |
| [8-2](./8-2-load-compare-documents-and-transaction-scoped-working-review-geometry.md): load compare documents and transaction scoped working review geometry | review | review | 0 | Awaiting acceptance/review closure |
| [8-3](./8-3-build-compare-workspace-evidence-reconciliation-ux.md): build compare workspace evidence reconciliation ux | review | review | 0 | Awaiting acceptance/review closure |
| [8-4](./8-4-add-legal-and-fiscal-cadaster-query-services-for-compare.md): add legal and fiscal cadaster query services for compare | review | review | 0 | Awaiting acceptance/review closure |
| [8-4](./8-4-parcel-scoped-geometry-review-option-a.md): parcel scoped geometry review option a | not tracked | review | 0 | Untracked review item |
| [8-4a](./8-4a-add-manual-compare-evidence-search-and-result-curation-ui.md): add manual compare evidence search and result curation ui | review | review | 0 | Awaiting acceptance/review closure |
| [8-4b](./8-4b-add-live-innola-baunit-search-adapter-for-compare.md): add live innola baunit search adapter for compare | review | review | 0 | Awaiting acceptance/review closure |
| [8-4c](./8-4c-add-enterprise-legal-and-fiscal-neighbor-evidence-for-compare.md): add enterprise legal and fiscal neighbor evidence for compare | review | review | 0 | Awaiting acceptance/review closure |
| [8-4d](./8-4d-add-collapsible-pdf-panel-to-compare-workspace.md): add collapsible pdf panel to compare workspace | done | done | 0 | Recorded done; no status/checklist conflict found |
| [8-4e](./8-4e-add-compare-task-lifecycle-actions-and-reopen-active-workspace.md): add compare task lifecycle actions and reopen active workspace | review | review | 0 | Awaiting acceptance/review closure |
| [8-4f](./8-4f-add-parcel-selection-owner-lookup-launch-from-search-and-map-popup.md): add parcel selection owner lookup launch from search and map popup | not tracked | drafted | 6 | Untracked planned scope; confirm overlap and delivery |
| [8-4g](./8-4g-add-overlap-review-and-innola-owner-enrichment-report-for-compute-and-compare.md): add overlap review and innola owner enrichment report for compute and compare | not tracked | drafted | 8 | Untracked planned scope; confirm overlap and delivery |
| [8-4h](./8-4h-add-button-driven-spatial-overlap-engine-for-compute-and-compare.md): add button driven spatial overlap engine for compute and compare | not tracked | review | 0 | Untracked review item |
| [8-4i](./8-4i-add-overlap-review-surface-and-evidence-report-model-for-compute-and-compare.md): add overlap review surface and evidence report model for compute and compare | not tracked | review | 0 | Untracked review item |
| [8-4j](./8-4j-add-innola-owner-enrichment-for-overlap-review-results.md): add innola owner enrichment for overlap review results | not tracked | drafted | 0 | Untracked planned scope; confirm overlap and delivery |
| [8-5](./8-5-persist-compare-decision-and-unlock-commit-stage.md): persist compare decision and unlock commit stage | review | review | 0 | Awaiting acceptance/review closure |
| [8-5a](./8-5a-generate-compare-review-report-artifacts.md): generate compare review report artifacts | not tracked | in progress | 4 | Untracked active implementation |
| [8-6](./8-6-wire-commit-stage-readiness-to-compare-approval-and-authoritative-promotion.md): wire commit stage readiness to compare approval and authoritative promotion | ready-for-dev | ready-for-dev | 25 | Prepared scope; completion not established |
| [8-7](./8-7-add-parcel-search-dockpane-tab.md): add parcel search dockpane tab | done | done | 0 | Recorded done; no status/checklist conflict found |
| [8-8](./8-8-add-rt-examination-linked-pe-review-and-writeback.md): add rt examination linked pe review and writeback | done | done | 0 | Recorded done; no status/checklist conflict found |
| [9-1](./9-1-create-wix-based-target-machine-installer.md): create wix based target machine installer | in-progress | in-progress | 7 | Active; open implementation/verification tasks |
| [9-2](./9-2-add-supporting-documents-full-panel-viewer.md): add supporting documents full panel viewer | done | done | 0 | Recorded done; no status/checklist conflict found |
| [10-1](./10-1-create-user-training-and-test-guide-for-parcel-workflow-extension.md): create user training and test guide for parcel workflow extension | review | review | 0 | Awaiting acceptance/review closure |
| [11-1](./11-1-inventory-and-decision-lock-for-plan-examination-unification.md): inventory and decision lock for plan examination unification | review | review | 0 | Awaiting acceptance/review closure |
| [11-2](./11-2-unified-pdf-extraction-contract-for-plan-examination.md): unified pdf extraction contract for plan examination | ready-for-dev | ready-for-dev | 23 | Prepared scope; completion not established |
| [12-1](./12-1-current-testing-review-ux-and-extraction-polish.md): current testing review ux and extraction polish | done | done | 0 | Recorded done; no status/checklist conflict found |
| [12-2](./12-2-centralize-innola-session-refresh-for-workflow-finalize.md): centralize innola session refresh for workflow finalize | done | done | 0 | Recorded done; no status/checklist conflict found |
| [12-3](./12-3-add-computed-participants-and-title-image-download-to-compare.md): add computed participants and title image download to compare | done | done | 0 | Recorded done; no status/checklist conflict found |

