---
baseline_commit: handoff-2026-07-14
---

# Story 8.3: Build Compare Workspace Evidence Reconciliation UX

Status: review

## Story

As a cadastral examiner,  
I want a Compare workspace with documents on the left, read-only transaction geometry in the center, and ownership evidence plus decision controls on the right,  
so that Compare feels like evidence reconciliation rather than another COGO editing tool.

## Business Context

The Compare UX should be a dedicated examiner workspace. It must not copy the Points Validation Tool or Jamaica COGO editor mental model. The map is context and selection/query support. The work is reconciliation: survey plan evidence versus legal cadaster ownership and fiscal cadaster neighbors.

The UX mockup and EXPERIENCE spine have already been added:

- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/compare-workspace-evidence-reconciliation.html`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/EXPERIENCE.md`

## Acceptance Criteria

1. Given a Compare transaction is launched, when the workspace opens, then the primary layout uses three persistent regions: attached documents, working geometry, and ownership evidence/decision.
2. Given the workspace is loading, when documents, geometry, and evidence are still resolving, then each region can show independent loading or failure state without freezing the entire workspace.
3. Given documents are available, when the user selects a PDF/image attachment, then the document pane displays it using existing embedded viewer conventions where supported.
4. Given working geometry is available, when the center map loads, then it shows the transaction-scoped working layers and read-only status.
5. Given Compare is active, when the user looks for geometry editing actions, then COGO editing, add segment, add point, and boundary edit commands are not exposed in the Compare workspace.
6. Given legal/fiscal evidence is loaded, when the right panel renders, then survey plan interpretation, legal cadaster results, fiscal neighbor results, discrepancies, and decision notes are visually distinct.
7. Given an unresolved discrepancy exists, when the user attempts to approve Compare, then approval is disabled or guarded according to product policy and the discrepancy remains visible.
8. Given the user records notes, when they save progress, then notes are preserved locally in the Compare review artifact.
9. Given the user closes the workspace without approving, when they reopen the transaction, then saved progress, notes, and loaded evidence summaries are restored where available.
10. Given automated UI/view-model tests run, then independent load state, action availability, discrepancy blocking, and save/restore behavior are covered.

## Tasks / Subtasks

- [x] Add Compare workspace view model. (AC: 1-10)
  - [x] Create `CompareWorkspaceViewModel` or equivalent.
  - [x] Model document state, geometry state, legal evidence state, fiscal evidence state, discrepancy state, notes, and decision state.
  - [x] Keep command availability explicit: reload geometry, query parcel ID, query volume/folio, find neighbors, save progress, block compare, approve compare, return to Compute.

- [x] Add Compare workspace view/window. (AC: 1-7)
  - [x] Build the three-region layout from the mockup.
  - [x] Reuse existing viewer/document controls where feasible.
  - [x] Embed or coordinate with the active ArcGIS Pro map for the center geometry region.
  - [x] Provide clear read-only geometry microcopy.

- [x] Add discrepancy and decision UI. (AC: 6-9)
  - [x] Show survey plan interpretation separately from legal/fiscal records.
  - [x] Show open discrepancies with status and evidence source.
  - [x] Save notes before approval/block decisions.
  - [x] Ensure fiscal-neighbor matches are never presented as legal owner confirmation.

- [x] Add restore/progress behavior. (AC: 8-9)
  - [x] Persist a draft Compare review artifact in the Case Folder `working` directory.
  - [x] Restore notes and evidence summaries when the transaction is reopened.
  - [x] Avoid overwriting current evidence with stale drafts without a timestamp/version check.

- [x] Add tests. (AC: 1-10)
  - [x] View-model tests for independent loading states.
  - [x] Command availability tests.
  - [x] Discrepancy blocks/guards approval tests.
  - [x] Save and restore draft tests.

## Developer Notes

Relevant existing UI/view-model patterns:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionDocumentsWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`

Do not reuse the Points Validation Tool layout as-is. The Compare workspace needs a denser evidence panel and fewer geometry-editing affordances.

Recommended artifact names:

- `working/compare_review_draft.json`
- `working/compare_review_decision.json` introduced in Story 8.5

## UX References

- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/compare-workspace-evidence-reconciliation.html`
- EXPERIENCE component patterns:
  - Compare workspace shell
  - Compare source document pane
  - Compare map panel
  - Ownership evidence panel

## Testing Notes

Run:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj
```

## Post-Review Change Request: Compute-Style PDF Selector

Mary review note, 2026-07-15: The Compare attached-document pane should follow the same source-document selection pattern used by the Compute form. When a transaction has several attached plan PDFs, the examiner needs a compact selector rather than a document list taking vertical space from the viewer.

### Additional Acceptance Criteria

11. Given a Compare transaction has one or more PDF attachments, when the workspace opens, then the attached-document pane shows a combo box populated with all available PDF files using the same practical selection approach as the Compute form.
12. Given multiple PDF attachments are available, when the user chooses a different PDF from the combo box, then the embedded/fallback document viewer updates to the selected PDF without reloading geometry or evidence state.
13. Given exactly one PDF attachment is available, when the workspace opens, then that PDF is selected by default and displayed where supported.
14. Given no PDF attachment is available, when the workspace opens, then the document pane shows an empty/disabled PDF selector and a clear fallback message, while the rest of Compare remains usable.
15. Given non-PDF attachments are present, when the PDF selector is populated, then non-PDF files are excluded from the primary selector unless a later story explicitly adds an all-attachments mode.

### Patch Tasks

- [x] Replace the left-pane document list with a compact PDF `ComboBox` bound to the Compare document collection filtered to PDF attachments. (AC: 11, 15)
- [x] Keep `SelectedDocument` as the single source of truth for viewer state so changing the combo selection refreshes the existing PDF/image viewer projection without touching geometry/evidence load state. (AC: 12)
- [x] Default-select the first available PDF after Compare document load, matching Compute’s “ready to review” behavior. (AC: 13)
- [x] Add an empty-state/fallback message for transactions with no PDF attachments. (AC: 14)
- [x] Add or update view-model tests for multiple PDFs, single-PDF default selection, no-PDF empty state, and selection-change viewer refresh. (AC: 11-15)

## Dev Agent Record

### Implementation Plan

- Add a Compare workspace view model over the Story 8.2 document/geometry load state.
- Add a three-region WPF Compare window: attached documents, active-map geometry context, and ownership evidence/decision.
- Persist local draft notes, evidence summaries, and discrepancy status to `working/compare_review_draft.json`.
- Wire the transaction panel Compare launcher to the real workspace window.
- Cover load-state independence, command availability, discrepancy blocking, and draft save/restore in the test harness.

### Debug Log

- First build pass found WPF partial accessibility and private property setter issues; corrected the window and viewer-state wrapper.
- Second build pass found viewer path property mismatch; switched Compare viewer wrapper from `CopiedPath` to existing `FullPath`.
- Third build pass found test fixture API mismatches; aligned with `SourceFileCopyBatchResult.Results` and `WorkflowState.ToContractValue()`.
- Final `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed: 370 tests.
- Post-review PDF selector patch first failed as expected because `PdfDocuments`, `HasPdfDocuments`, and `PdfDocumentSelectorStatus` did not exist yet.
- Post-review PDF selector patch build passed with the pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs:82`.
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed: 395 tests.

### Completion Notes

- Implemented `CompareWorkspaceViewModel` with independent document, geometry, legal evidence, fiscal evidence, discrepancy, notes, and decision state.
- Added `CompareWorkspaceWindow` with persistent left documents, center read-only active-map context, and right ownership evidence/decision regions.
- Reused existing source viewer projection conventions for PDF/image document rendering through WebView2/Image fallback handling.
- Added draft persistence to `working/compare_review_draft.json` and restore guards for matching transaction, schema version, and saved timestamp.
- Connected `TransactionPanelDockpaneViewModel` to `ShellState.OpenCompareWorkspace`, replacing the placeholder Compare launch path.
- Added tests for independent load states, read-only command availability, discrepancy approval blocking, and save/restore draft behavior.
- Added a Compute-style PDF combo selector for Compare source documents. The view model now keeps all source documents but exposes a PDF-only selector collection, selects the first PDF by default, excludes non-PDF attachments from the primary selector, and keeps Compare usable when no PDF exists.
- Added regression coverage for multiple PDFs, selector-driven viewer refresh without geometry reload, and no-PDF empty state.

### File List

- `_bmad-output/implementation-artifacts/8-3-build-compare-workspace-evidence-reconciliation-ux.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareReviewDraftPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-07-14 | 1.0 | Added the Compare evidence reconciliation workspace, draft persistence, transaction-panel launch wiring, and view-model regression coverage. | Amelia / Codex |
| 2026-07-15 | 1.1 | Patched attached-document UX to use a Compute-style PDF combo selector with PDF-only filtering, default selection, empty state, and regression coverage. | Amelia / Codex |
