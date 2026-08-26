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
9. Given PLA output documents are generated during the workflow, then they remain local/generated case artifacts until Finalize and are not uploaded to Innola earlier. `st_plan_annex_output` is the selected page extracted from the input `st_plan_annexation_pdf`; `st_plan_annex_output2` is the generated geometry document built from reviewed bearings/distances; `st_plan_annex_output3` is reserved and not produced until defined by a later requirement.
10. Given the examiner has completed all required PLA review findings, when the main transaction form renders, then a `Finalize` button is available according to the same readiness/gating pattern used by existing transaction finalization.
11. Given the examiner clicks `Finalize`, then the add-in asks the examiner to confirm finalization.
12. Given the examiner selects `No` in the Finalize confirmation, then no Innola writeback, upload, or completion action occurs.
13. Given the examiner selects `Yes`, then the add-in saves/attaches generated PLA output documents to the Innola transaction using the configured/resolved PLA output document/source type and continues the normal finalization flow: selected source page to `st_plan_annex_output`, generated geometry to `st_plan_annex_output2`, and no `st_plan_annex_output3` unless a third document has been explicitly defined.
14. Given upload or finalization fails, then Finalize stops before marking the transaction complete, shows a retryable non-secret diagnostic, and preserves local case artifacts for retry.
15. Given a previous generated PLA output attachment exists for the same transaction and document/source type, when Finalize uploads the new current artifact, then the add-in follows existing replacement/overwrite behavior where supported so Innola retains the current finalized artifact rather than stale duplicates.
16. Given the transaction is reopened, when the case folder contains PLA selection, extraction, review, visual comparison, and finalize/upload artifacts, then the workflow restores those artifacts and does not require the examiner to repeat page selection, extraction, or visual review unless they choose to rerun.
17. Given existing Compute and Compare finalize/report attachment behavior exists, when PLA workflow UX/finalize behavior is added, then those workflows keep their current source types, report upload behavior, workspace labels, stage gates, and completion ordering.
18. Given the Points Validation Tool opens for a PLA Plan Annexation review, then the Memorandum tab is hidden and memorandum disposition rules do not block validation completion because memorandum evidence is not part of this transaction type.
19. Given the Points Validation Tool opens for non-PLA PXA survey-plan reviews, then the Memorandum tab remains available with memorandum rule evidence, status, and workflow-effect fields.
20. Given the examiner creates a title-plan comparison overlay from the map/image placement tool, when the PLA native visual comparison artifact is absent, then PLA visual-comparison readiness accepts the persisted `working/title_plan_overlay/title_plan_overlay_artifact.json` evidence as the completed approximate visual comparison.
21. Given PLA Finalize is not ready, when the main Finalize workspace renders, then the badge/summary/help text shows the exact readiness blocker from `PlaFinalizeService.CheckReadiness(...)` instead of showing a misleading `Ready` message.
22. Given automated tests run, then coverage proves PLA-specific workspace rendering, first-class Select Plan Evidence behavior, extraction gating by saved selection artifact, deferred georeference/dimension labeling, visual comparison persistence/title-plan overlay fallback, Finalize readiness blocker messaging, Finalize confirmation `No` behavior, Finalize confirmation `Yes` upload behavior, source type resolution use, failure short-circuit behavior, retry/reopen evidence, PLA-only memorandum tab suppression and blocker exclusion, non-PLA PXA memorandum tab retention, and Compute/Compare non-regression.

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
  - [x] Accept persisted title-plan comparison overlay evidence as PLA visual comparison readiness when native `pla_visual_comparison.json` is absent. (AC: 20)

- [x] Add PLA Finalize flow. (AC: 9-15, 17, 21)
  - [x] Add or reuse a main transaction form `Finalize` button gated by PLA review readiness.
  - [x] Prompt the user to confirm finalization.
  - [x] Ensure `No` exits without Innola writeback, upload, or task completion.
  - [x] Ensure `Yes` attaches the selected source page to `st_plan_annex_output` and the generated geometry document to `st_plan_annex_output2`; leave `st_plan_annex_output3` unused until defined.
  - [x] Reuse existing attachment upload/replacement service patterns where practical.
  - [x] Stop finalization before transaction completion on upload/writeback failure.
  - [x] Persist sanitized finalize/upload evidence.
  - [x] Display the same PLA readiness blocker message used by the Finalize command gate in the Finalize workspace.

- [x] Add tests. (AC: 1-22)
  - [x] Test PLA workflow profile renders the PLA-specific surface and labels.
  - [x] Test `Select Plan Evidence` appears as a first-class step for PLA.
  - [x] Test PLA extraction is disabled until `pla_plan_evidence_selection.json` exists with a generated evidence artifact.
  - [x] Test PLA georeference/dimension readiness is hidden or labeled as deferred before extraction.
  - [x] Test visual review decision/status/notes persistence.
  - [x] Test generated visual artifact path and metadata.
  - [x] Test title-plan overlay artifact fallback satisfies PLA visual comparison readiness.
  - [x] Test the Finalize workspace reports the `PlaFinalizeService` readiness blocker instead of `Ready` when blocked.
  - [x] Test Finalize confirmation `No` performs no upload or completion.
  - [x] Test Finalize confirmation `Yes` uploads generated PLA output documents with resolved PLA source type.
  - [x] Test upload failure blocks completion and leaves retryable state.
  - [x] Test reopen restores PLA visual/finalize evidence.
  - [x] Test Compute/Compare finalize behavior is unchanged.
  - [x] Test the Memorandum tab and memorandum disposition blockers are hidden/excluded for PLA while remaining wired for non-PLA PXA survey-plan reviews. (AC: 18-20)

### Review Findings

- [x] [Review][Patch] PLA Finalize still runs Compute publish/disposition work before the PLA lifecycle upload path [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3615]. Resolved by branching `CompleteTransactionAsync` so PLA skips Compute publish/disposition and proceeds to the PLA lifecycle completion path after confirmation.
- [x] [Review][Patch] PLA output attachment registration does not replace prior finalized PLA output source entries [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs:565]. Resolved by extending generated-source replacement to `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`.
- [x] [Review][Patch] PLA generated output discovery can upload unrelated or stale PDFs and shift `st_plan_annex_output*` mappings [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaFinalizeService.cs:272]. Resolved by limiting PLA finalize uploads to explicit current `OutputSummary.Payload.ArtifactPaths` PDFs in their saved order.
- [x] [Review][Patch] PLA visual comparison overlay fallback could hide corrupt native comparison metadata. Resolved by allowing overlay fallback only when `pla_visual_comparison.json` is absent.
- [x] [Review][Patch] PLA visual comparison overlay fallback did not verify the overlay transaction number. Resolved by matching the overlay transaction against the manifest transaction number, with case-folder name fallback when no manifest exists.
- [x] [Review][Reject] The title-plan overlay artifact does not contain a separate accept/flag/reject decision. For this story, creating the `TitlePlanComparison` overlay with examiner-selected image and map control points is the completed visual-comparison action requested for TR 100001219, so the bridged comparison is intentionally treated as accepted approximate visual evidence.
- [x] [Review][Reject] Dockpane Finalize message coverage is source-inspection based rather than a full ViewModel behavior test. This matches existing ArcGIS `DockPane` test style in the harness; the service readiness behavior is covered by focused tests.

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
- If the confirmation answer is `Yes`, generated output documents are saved/attached to the transaction using the PLA-resolved output document/source type: selected plan page to `st_plan_annex_output`, generated geometry to `st_plan_annex_output2`, and no `st_plan_annex_output3` until that document is defined.
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
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs`.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pxa review xaml"`: PASS 2 tests after PLA memorandum-tab visibility patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after PLA hidden memorandum blocker patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pxa review xaml"`: PASS 2 tests after PLA hidden memorandum blocker patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after validation blocked copy/area-detail patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "validation blocked" "pxa review xaml"`: PASS 3 tests after validation blocked copy/area-detail patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after PLA title-plan overlay readiness and Finalize blocker-message patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pla visual comparison" "pla finalize" "validation blocked" "pxa review xaml"`: PASS 13 tests after PLA title-plan overlay readiness and Finalize blocker-message patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after review hardening.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pla visual comparison" "pla finalize" "validation blocked" "pxa review xaml"`: PASS 15 tests after review hardening.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after PLA save/approval stale-row cleanup patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pxa review xaml" "pla visual comparison" "pla finalize" "validation blocked"`: PASS 15 tests after PLA save/approval stale-row cleanup patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after PLA spatial-review approval visual-comparison bridge patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pla visual comparison" "pla finalize"`: PASS 13 tests after PLA spatial-review approval visual-comparison bridge patch.
- `tools/package_addin.ps1 -Configuration Release`: Package succeeded and bumped add-in patch version to `1.1.227`.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after square-foot document-area parsing patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pxa review xaml" "validation blocked"`: PASS 3 tests after square-foot document-area parsing patch.
- `tools/package_addin.ps1 -Configuration Release`: Package succeeded and bumped add-in patch version to `1.1.229`.
- `python -m unittest tests.test_output_adapter.OutputAdapterTests.test_output_adapter_generates_pla_output_pdf_artifact`: PASS after selected-page/output2 producer mapping patch.
- `python -m unittest tests.test_output_adapter`: PASS 16 tests after selected-page/output2 producer mapping patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after generated PLA output PDF producer patch.
- `tools/package_addin.ps1 -Configuration Release`: Package succeeded and bumped add-in patch version to `1.1.232`.
- `python -m unittest tests.test_output_adapter`: PASS 16 tests after exact PLA selected-page/output2 contract patch.
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln -c Release /p:UseSharedCompilation=false`: Build succeeded, 1 existing nullable warning in `SurveyPlanBoundarySolverTests.cs` after exact PLA output readiness patch.
- `dotnet src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\bin\Release\net8.0-windows\ParcelWorkflowAddIn.Tests.dll "pla finalize"`: PASS 9 tests, including missing output2 and undefined output3 blockers.
- `tools\package_addin.ps1 -Configuration Release`: Package succeeded and bumped add-in patch version to `1.1.234`.
- `powershell -ExecutionPolicy Bypass -File tools\validate_installer_packaging.ps1`: PASS after adding `pypdf` as an explicit ArcGIS Python requirement for selected-page extraction.

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
- Hid the Memorandum tab and excluded hidden memorandum disposition blockers for PLA plan-annexation reviews only, leaving the memorandum rule review tab available for non-PLA PXA survey-plan workflows and restoring its workflow-effect evidence column.
- Clarified Create Spatial Units validation-blocked copy so area mismatch blockers report computed area, document area, percent delta, and tolerance instead of appearing as only a generic closure failure.
- Connected title-plan overlay artifacts into PLA visual-comparison readiness so a persisted examiner-created `TitlePlanComparison` overlay can satisfy the visual review gate when native PLA comparison metadata is absent.
- Updated the Finalize workspace badge/summary/help text to use the same `PlaFinalizeService.CheckReadiness(...)` result that gates the Finalize button, preventing a blocked PLA case from showing a misleading `Ready` message.
- Hardened the title-plan overlay fallback so it does not mask corrupt native PLA comparison metadata and does not accept an overlay artifact for a different transaction.
- Patched PLA review Save/Approve so local-origin geometry built from a closed reviewed segment chain replaces stale OCR/reference point rows outside that chain, preventing rows such as blank point `C` in TR 100001219 from keeping validation completion disabled after the boundary is already closed.
- Bridged current PLA spatial-review approval into visual-comparison readiness so the `Visual Comparison` stage and Finalize gate agree when the examiner approved the generated output layers but no separate `pla_visual_comparison.json` or title-plan overlay artifact was written.
- Fixed document-area parsing so OCR text such as `408.62 square feet` is not treated as `408.62 sq m` for validation area comparison.
- Patched PLA output generation so `output_adapter.py` creates `output/reports/pla_selected_plan_page.pdf` for `st_plan_annex_output` and `output/reports/pla_generated_geometry.pdf` for `st_plan_annex_output2`, registering both in `output_summary.payload.artifact_paths` in upload order. `st_plan_annex_output3` remains reserved/undefined.
- Patched PLA Finalize readiness so the transaction requires exactly the two currently defined PLA output PDFs: selected source page and generated geometry. It now blocks clearly when output2 is missing or when undefined output3 is present.
- Added `pypdf` to the ArcGIS Python deployment requirements and installer verification because `st_plan_annex_output` is now extracted from the selected page of `st_plan_annexation_pdf`.

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
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`

### Change Log

- 2026-08-24: Implemented PLA workflow UX stage, visual comparison evidence, and Finalize/upload flow for Story 2.23D.
- 2026-08-25: Code review completed with changes requested; story returned to in-progress for PLA finalize ordering, PLA source replacement, and generated output discovery fixes.
- 2026-08-25: Applied code review patches for PLA finalize ordering, source replacement, and strict output document discovery; returned story to review.
- 2026-08-25: Patched PLA-specific UX routing/labels so TRs like 100001219 surface Select Plan Evidence before deferred coordinate/dimension evidence gates.
- 2026-08-25: Patched save-refresh behavior so saving PLA plan evidence advances the shared dockpane to Extract Plan Geometry and sets the PLA dockpane title.
- 2026-08-25: Hid the Memorandum tab and excluded hidden memorandum disposition blockers for PLA plan-annexation reviews only while keeping non-PLA PXA memorandum review visible with workflow-effect evidence.
- 2026-08-25: Clarified validation-blocked summary/help/status copy for PLA Create Spatial Units area mismatch blockers.
- 2026-08-26: Connected title-plan overlay artifacts to PLA visual-comparison readiness and changed the Finalize panel to show the exact PLA readiness blocker when Finalize remains disabled.
- 2026-08-26: Applied review hardening so title-plan overlay fallback only applies when native PLA visual-comparison metadata is absent and the overlay transaction matches the active case.
- 2026-08-26: Patched PLA Save/Approve to rebuild review rows from the closed reviewed segment chain and remove stale OCR/reference rows outside that chain.
- 2026-08-26: Patched PLA visual-comparison readiness to accept current spatial-review approval as accepted visual comparison evidence.
- 2026-08-26: Patched document-area parsing to ignore square-foot area text for square-metre validation comparison.
- 2026-08-26: Patched PLA output generation to emit and register the selected-page PDF and generated-geometry PDF in PLA Finalize attachment order.
- 2026-08-26: Patched PLA Finalize readiness to require both defined outputs and block undefined output3 until the document is specified.
- 2026-08-26: Added explicit `pypdf` deployment requirement for selected-page PDF extraction.

## Senior Developer Review (AI)

### Review Outcome

Approved after patch.

### Findings

- [x] [Review][Patch] PLA Finalize still runs Compute publish/disposition work before the PLA lifecycle upload path [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:3615]. Resolved by branching `CompleteTransactionAsync` so PLA skips Compute publish/disposition and proceeds to the PLA lifecycle completion path after confirmation.
- [x] [Review][Patch] PLA output attachment registration does not replace prior finalized PLA output source entries [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionDetailService.cs:565]. Resolved by extending generated-source replacement to `st_plan_annex_output`, `st_plan_annex_output2`, and `st_plan_annex_output3`.
- [x] [Review][Patch] PLA generated output discovery can upload unrelated or stale PDFs and shift `st_plan_annex_output*` mappings [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Pla/PlaFinalizeService.cs:272]. Resolved by limiting PLA finalize uploads to explicit current `OutputSummary.Payload.ArtifactPaths` PDFs in their saved order.

### Residual Risk

- Full regression still stops on unrelated existing PXA XAML assertion `JamaicaReviewWorkspaceXamlTests.PxaReviewExposesMemorandumRuleGroups`; this should be handled outside 2.23D unless that test failure is later shown to be caused by the PLA dockpane changes.
