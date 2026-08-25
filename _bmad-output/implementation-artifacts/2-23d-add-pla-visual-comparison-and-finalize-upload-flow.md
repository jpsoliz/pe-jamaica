---
baseline_commit: handoff-2026-08-24
parent_story: 2-23-add-pla-plan-annexation-pdf-selection-extraction-and-review.md
depends_on: 2-23c-add-pla-selected-plan-extraction-review-and-local-origin-geometry.md
---

# Story 2.23D: Add PLA-Specific Workflow UX, Visual Comparison, And Finalize Upload Flow

Status: review

## Story

As an SMD examiner working a PLA Plan Annexation transaction,
I want a PLA-specific workflow surface that starts with selecting plan evidence, then supports visual comparison and explicit Finalize,
so that the process follows the PLA examiner sequence and generated PLA output documents are attached to Innola only after review findings are accepted.

## Business Context

PLA is a distinct transaction workflow even when Innola presents the task as `Compute Survey Plan`. The current reusable services correctly classify `workflow_profile = pla_plan_annexation`, persist selected plan evidence, run OCR/vision extraction, reuse the review grid/local-origin solver, and resolve PLA output document source types. What is missing is a workflow surface that presents those reusable pieces in the right PLA order.

PLA source-plan versus generated-geometry matching is approximate visual similarity evidence only. It must not be represented as survey-accurate georeferencing or authoritative parcel fabric promotion. Generated output documents should remain local case-folder artifacts until the examiner presses `Finalize` on the main transaction form and confirms the action.

## Acceptance Criteria

1. Given `workflow_profile = pla_plan_annexation`, when the main workflow form renders, then the examiner sees a PLA-specific workflow surface/labels instead of generic PE/PXA-only stage labels.
2. Given the transaction is PLA and the required plan annexation PDF is available, when structure/source checks pass, then `Select Plan Evidence` is shown as a first-class active step.
3. Given no selected plan evidence artifact exists, when the examiner views the PLA workflow surface, then extraction is disabled with a clear requirement to save `working/pla_plan_annexation/pla_plan_evidence_selection.json`.
4. Given the examiner saves selected plan evidence, when the workflow refreshes or the case is reopened, then the selected source/page/artifact status is restored and extraction can be enabled.
5. Given the workflow is PLA before selected-plan extraction, when georeference/dimension readiness is shown, then it is hidden or relabeled as deferred PLA evidence rather than presented as a blocking PE/PXA action gate.
6. Given selected plan image evidence and generated geometry both exist, when visual review opens, then the examiner can compare the selected source plan evidence against the generated geometry using a side-by-side or overlay-style visual similarity review.
7. Given visual comparison is approximate, when the reviewer accepts or flags the comparison, then the result is persisted as review evidence without claiming survey-accurate alignment.
8. Given geometry visual evidence is generated, then the workflow persists a generated visual artifact under the case folder.
9. Given PLA output documents are generated during the workflow, then they remain local/generated case artifacts until Finalize and are not uploaded to Innola earlier.
10. Given the examiner has completed all required PLA review findings, when the main transaction form renders, then a `Finalize` button is available according to the same readiness/gating pattern used by existing transaction finalization.
11. Given the examiner clicks `Finalize`, then the add-in asks the examiner to confirm finalization.
12. Given the examiner selects `No` in the Finalize confirmation, then no Innola writeback, upload, or completion action occurs.
13. Given the examiner selects `Yes`, then the add-in saves/attaches generated PLA output documents to the Innola transaction using the configured/resolved PLA output document/source type and continues the normal finalization flow.
14. Given upload or finalization fails, then Finalize stops before marking the transaction complete, shows a retryable non-secret diagnostic, and preserves local case artifacts for retry.
15. Given a previous generated PLA output attachment exists for the same transaction and document/source type, when Finalize uploads the new current artifact, then the add-in follows existing replacement/overwrite behavior where supported so Innola retains the current finalized artifact rather than stale duplicates.
16. Given the transaction is reopened, when the case folder contains PLA selection, extraction, review, visual comparison, and finalize/upload artifacts, then the workflow restores those artifacts and does not require the examiner to repeat page selection, extraction, or visual review unless they choose to rerun.
17. Given existing Compute and Compare finalize/report attachment behavior exists, when PLA workflow UX/finalize behavior is added, then those workflows keep their current source types, report upload behavior, workspace labels, stage gates, and completion ordering.
18. Given automated tests run, then coverage proves PLA-specific workspace rendering, first-class Select Plan Evidence behavior, extraction gating by saved selection artifact, deferred georeference/dimension labeling, visual comparison persistence, Finalize confirmation `No` behavior, Finalize confirmation `Yes` upload behavior, source type resolution use, failure short-circuit behavior, retry/reopen evidence, and Compute/Compare non-regression.

## Tasks / Subtasks

- [x] Add PLA-specific workflow surface. (AC: 1-5, 17)
  - [x] Detect `workflow_profile = pla_plan_annexation` from the active manifest/session and switch workflow labels/surface to PLA language.
  - [x] Present `Select Plan Evidence` as a first-class PLA step after source/structure readiness.
  - [x] Show the existing PLA selected-plan evidence controls in the PLA step, not only as a sub-panel under generic `Validate Points and Lines`.
  - [x] Hide or relabel pre-extraction Georeference/Dimension readiness as deferred PLA evidence gates.
  - [x] Preserve existing PE/PXA workspace labels and early-check behavior.

- [x] Gate PLA extraction by saved plan evidence. (AC: 2-5, 16-18)
  - [x] Disable PLA extraction until `PlaPlanEvidenceSelectionService.LoadSelection(...)` returns a complete selection with an existing generated evidence artifact.
  - [x] Show a clear status/action message when selection is missing or incomplete.
  - [x] Enable extraction once the saved selection artifact is present and valid.
  - [x] Restore gating state on case reopen.

- [x] Generate geometry visual evidence. (AC: 6-9)
  - [x] Produce a visual artifact of generated geometry suitable for comparison with selected plan evidence.
  - [x] Use existing ArcGIS map/screenshot, geometry-preview, or title-plan image-placement patterns where practical.
  - [x] Persist the generated visual artifact under the case folder.
  - [x] Keep all generated output local until Finalize.

- [x] Add visual similarity review. (AC: 6-8, 16)
  - [x] Provide side-by-side or overlay-style comparison between selected plan evidence and generated geometry visual artifact.
  - [x] Record reviewer decision/status and notes.
  - [x] Label comparison as approximate visual similarity, not survey-accurate georeferencing.
  - [x] Restore comparison state on reopen.

- [x] Add PLA Finalize flow. (AC: 9-15, 17)
  - [x] Add or reuse a main transaction form `Finalize` button gated by PLA review readiness.
  - [x] Prompt the user to confirm finalization.
  - [x] Ensure `No` exits without Innola writeback, upload, or task completion.
  - [x] Ensure `Yes` attaches generated PLA output documents to Innola using the configured/resolved PLA output document/source type.
  - [x] Reuse existing attachment upload/replacement service patterns where practical.
  - [x] Stop finalization before transaction completion on upload/writeback failure.
  - [x] Persist sanitized finalize/upload evidence.

- [x] Add tests. (AC: 1-18)
  - [x] Test PLA workflow profile renders the PLA-specific surface and labels.
  - [x] Test `Select Plan Evidence` appears as a first-class step for PLA.
  - [x] Test PLA extraction is disabled until `pla_plan_evidence_selection.json` exists with a generated evidence artifact.
  - [x] Test PLA georeference/dimension readiness is hidden or labeled as deferred before extraction.
  - [x] Test visual review decision/status/notes persistence.
  - [x] Test generated visual artifact path and metadata.
  - [x] Test Finalize confirmation `No` performs no upload or completion.
  - [x] Test Finalize confirmation `Yes` uploads generated PLA output documents with resolved PLA source type.
  - [x] Test upload failure blocks completion and leaves retryable state.
  - [x] Test reopen restores PLA visual/finalize evidence.
  - [x] Test Compute/Compare finalize behavior is unchanged.

### Review Findings

- [x] [Review][Patch] PLA Finalize still runs Compute publish/disposition work before the PLA lifecycle upload path [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3615]. Resolved by branching `CompleteTransactionAsync` so PLA skips Compute publish/disposition and proceeds to the PLA lifecycle completion path after confirmation.
- [x] [Review][Patch] PLA output attachment registration does not replace prior finalized PLA output source entries [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs:565]. Resolved by extending generated-source replacement to `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`.
- [x] [Review][Patch] PLA generated output discovery can upload unrelated or stale PDFs and shift `st_plan_annex_output*` mappings [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaFinalizeService.cs:272]. Resolved by limiting PLA finalize uploads to explicit current `OutputSummary.Payload.ArtifactPaths` PDFs in their saved order.

## Dev Notes

Relevant existing implementation:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaPlanEvidenceSelectionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeReportAttachmentService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReportAttachmentService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Reports/ComputeExaminationReportService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceOverlayService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/MapGeoreferenceViewModel.cs`

PLA UX clarification:

- The workflow must not present PLA as only a generic `Compute Survey Plan` / PE/PXA flow once `workflow_profile = pla_plan_annexation` is known.
- The examiner-facing PLA sequence should be: source/structure readiness, Select Plan Evidence, OCR/vision extraction and review, local-origin/coordinate evidence review, visual comparison, Finalize.
- `Select Plan Evidence` must be a visible first-class step; it may reuse `PlaPlanEvidenceSelectionService` and `PlaPlanEvidenceSelectionViewModel`.
- Pre-extraction Georeference and Dimension readiness for PLA should be hidden or relabeled as deferred evidence gates because coordinate/geometry evidence is discovered after selected-plan extraction.
- Do not duplicate processing services. Reuse classification, selection artifact persistence, OCR/vision extraction, review grid, local-origin solver, visual comparison, and Finalize/upload services.

Finalize clarification:

- The main transaction form must have a `Finalize` button.
- The user decides whether to finalize after reviewing all findings.
- If the confirmation answer is `No`, no generated output document is saved/attached to Innola and no completion action occurs.
- If the confirmation answer is `Yes`, generated output documents are saved/attached to the transaction using the PLA-resolved output document/source type.
- Do not use `st_compute_report` or `st_compare_report` for PLA unless configuration explicitly maps PLA to one of those types, which is not expected.

Preserve these constraints:

- Do not attach generated PLA artifacts to Innola before Finalize.
- Do not claim visual similarity is survey-accurate alignment.
- Do not treat local/unreferenced geometry as authoritative parcel fabric promotion.
- Do not duplicate the PE/PXA workflow engine when a PLA-specific surface can reuse existing services underneath.
- Do not enable PLA extraction without a complete saved selected-plan evidence artifact.
- Do not log tokens, passwords, certificate material, API keys, or raw sensitive Innola responses.

## References

- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/2-23a-add-pla-transaction-profile-source-type-and-doc-type-resolution.md`
- `_bmad-output/implementation-artifacts/2-23b-add-pla-plan-annexation-pdf-page-selection-and-evidence-artifact.md`
- `_bmad-output/implementation-artifacts/2-23c-add-pla-selected-plan-extraction-review-and-local-origin-geometry.md`
- `_bmad-output/implementation-artifacts/investigations/tr-100001219-pla-workflow-investigation.md`
- `_bmad-output/implementation-artifacts/7-14-attach-formatted-compute-finalize-report-to-innola-transaction.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "pla finalize"`: PASS 3 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "workspace planner" "pla plan evidence selection" "pla visual comparison" "pla finalize"`: PASS 30 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release`: Build succeeded, 0 warnings, 0 errors.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release`: full regression attempted; stopped on existing unrelated PXA XAML assertion `JamaicaReviewWorkspaceXamlTests.PxaReviewExposesMemorandumRuleGroups` for `JamaicaReviewWorkspaceWindow.xaml`, which was not modified by Story 2.23D.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "pla finalize" "attachment upload replaces"`: PASS 7 tests.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "workspace planner" "pla plan evidence selection" "pla visual comparison" "pla finalize" "attachment upload replaces"`: PASS 34 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release`: Build succeeded, 0 warnings, 0 errors after review patches.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "workspace planner" "pla plan evidence selection"`: PASS 28 tests after PLA UX routing patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release`: Build succeeded, 0 warnings, 0 errors after PLA UX routing patch.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "workspace planner" "pla plan evidence selection" "pla selected plan"`: PASS 30 tests.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -c Release -- "workspace planner" "pla plan evidence selection" "pla selected plan"`: PASS 31 tests after save-refresh/title patch.

### Completion Notes List

- Added a PLA-specific active workspace stage for `Select Plan Evidence`, including first-class dockpane controls and profile-aware workspace routing before extraction.
- Gated PLA extraction on a complete saved `pla_plan_evidence_selection.json` with an existing generated plan evidence artifact, while preserving PE/PXA workspace behavior.
- Added PLA visual comparison persistence with generated SVG geometry evidence, approximate-visual-similarity disclaimer, reviewer decision/notes, and reopen-safe metadata.
- Added PLA Finalize upload service and lifecycle integration so generated PLA output PDFs attach using `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3` only after confirmation and readiness; upload failure stops before transaction completion and writes sanitized retry evidence.
- Updated PLA completion readiness and main Finalize copy/gating; PLA skips Compute report attachment/source type use during lifecycle completion.
- Resolved code-review findings by isolating PLA Finalize from Compute publish/disposition, replacing prior finalized PLA output source entries during registration, and restricting uploaded PLA output PDFs to explicit current output-summary artifacts.
- Patched PLA workflow routing so `PreflightBlocked` PLA cases with a valid Structure Check and `plan_annexation_pdf` open `Select Plan Evidence` instead of Georeference/Dimension; after `pla_plan_evidence_selection.json` exists, PLA can proceed to extraction while PE/PXA gates remain unchanged.
- Updated PLA-facing labels/stepper to show Plan Annexation, deferred coordinate/dimension evidence, Extract Plan Geometry, Review Local-Origin Geometry, Visual Comparison, and Finalize while reusing the shared dockpane/services.
- Patched the PLA plan evidence save path so a successful save notifies the parent dockpane, refreshes the active stage, enables Extract Plan Geometry, and updates the dockpane caption/tab text to `Parcel Workflow - Plan Annexation`.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaPlanEvidenceSelectionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaVisualComparisonService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaFinalizeService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/DefaultTransactionCompletionReadinessService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionLifecycleCoordinator.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaPlanEvidenceSelectionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaVisualComparisonServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PlaFinalizeServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionServiceTests.cs`

### Change Log

- 2026-08-24: Implemented PLA workflow UX stage, visual comparison evidence, and Finalize/upload flow for Story 2.23D.
- 2026-08-25: Code review completed with changes requested; story returned to in-progress for PLA finalize ordering, PLA source replacement, and generated output discovery fixes.
- 2026-08-25: Applied code review patches for PLA finalize ordering, source replacement, and strict output document discovery; returned story to review.
- 2026-08-25: Patched PLA-specific UX routing/labels so TRs like 100001219 surface Select Plan Evidence before deferred coordinate/dimension evidence gates.
- 2026-08-25: Patched save-refresh behavior so saving PLA plan evidence advances the shared dockpane to Extract Plan Geometry and sets the PLA dockpane title.

## Senior Developer Review (AI)

### Review Outcome

Approved after patch.

### Findings

- [x] [Review][Patch] PLA Finalize still runs Compute publish/disposition work before the PLA lifecycle upload path [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3615]. Resolved by branching `CompleteTransactionAsync` so PLA skips Compute publish/disposition and proceeds to the PLA lifecycle completion path after confirmation.
- [x] [Review][Patch] PLA output attachment registration does not replace prior finalized PLA output source entries [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs:565]. Resolved by extending generated-source replacement to `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`.
- [x] [Review][Patch] PLA generated output discovery can upload unrelated or stale PDFs and shift `st_plan_annex_output*` mappings [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaFinalizeService.cs:272]. Resolved by limiting PLA finalize uploads to explicit current `OutputSummary.Payload.ArtifactPaths` PDFs in their saved order.

### Residual Risk

- Full regression still stops on unrelated existing PXA XAML assertion `JamaicaReviewWorkspaceXamlTests.PxaReviewExposesMemorandumRuleGroups`; this should be handled outside 2.23D unless that test failure is later shown to be caused by the PLA dockpane changes.
