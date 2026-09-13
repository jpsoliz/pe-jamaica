---
baseline_commit: current-wipRC9
---

# Story 12.1: Current Testing Review UX And Extraction Polish

Status: done

## Story

As a cadastral examiner testing the current Parcel Workflow build,
I want the transaction list, extraction metadata, review grids, findings tab, and early-check execution to be cleaner and faster,
so that current PE/PXA testing can continue without waiting for the larger Plan Examination refactor.

## Scope Intent

This is a current-implementation stabilization story for `wipRC9`. It must not be merged into or depend on the PE/PXA refactor story `11-2`.

## Acceptance Criteria

1. Given the Transaction List opens after login, when transactions are refreshed, then the default filter is `My tasks` / assigned-to-me and rows are limited to transactions assigned to the logged-in user.
2. Given PDF extraction runs, when the source text contains GPS instrument or GPS serial number text, then extraction attempts to populate `gps_instrument_number` and `gps_serial_number` in `survey_metadata`.
3. Given the Points Validation Tool shows the Memorandum tab, when memorandum groups are rendered, then the visible summary text no longer shows unnecessary `Memorandum Detection` wording.
4. Given the Memorandum tab contains rows with `Needs Review` or `Failed` disposition, when the examiner chooses a bulk disposition option, then all unresolved memorandum rows can be moved to one selected disposition in a single action.
5. Given the Validation Findings tab is shown, when findings are displayed, then the UI shows only:
   - Points within Parish
   - Parcel Boundary within Parish
   - Embedded Compute Sheet
   - Plan and Compute Sheet Match
   - Printed Text Height
6. Given Validation Findings are filtered in the UI, when reports, JSON artifacts, and backend validation summaries are written, then non-displayed validation findings remain preserved in the underlying data.
7. Given Validation Findings rows are displayed, when the grid renders, then row height is reduced from the current oversized layout while remaining readable.
8. Given the current `Owners / Neighbors` tab is rendered, when the patch is applied, then the tab is renamed `Participants` and uses one participant grid instead of two grids.
9. Given the Participants grid is displayed, when owner, representative, or neighbor rows exist, then they remain visible in the single grid with columns:
   - Name
   - Role
   - Lot Number
   - Address
   - LandVal No.
   - Exam No
   - Volume
   - Folio
10. Given participant data is saved, when Innola finalize later reads reviewed neighbors, then rows whose role is `Neighbor` remain persisted in the existing adjacent-owner/neighbor model so finalize behavior is not broken.
11. Given the Boundary Segments tab is shown, when the grid renders, then the `Adjacent Owner` and `Status` columns are hidden/removed from the UI while the underlying segment data remains preserved.
12. Given a case is loaded in Parcel Workflow, when the examiner runs the early compute checks, then Structure Check, Georeference Check, and Dimension Check can be run from one combined action that refreshes stage status after each step and stops if a blocker occurs.
13. Given the combined early-check action is running, when work is in progress, then the dockpane communicates that processing is running through the existing busy/status mechanism or equivalent visible status text.
14. Given extraction review actions are shown, when both normal extraction and forced re-extraction are available, then duplicate/unclear extraction labels are reduced to one clear forced action, preferably `Rerun Extraction`, without changing the existing open-existing-review behavior.

## Tasks / Subtasks

- [x] Adjust Transaction List default filter. (AC: 1)
  - [x] Change initial filter from `All tasks` to `My tasks` or equivalent assigned-to-me label.
  - [x] Preserve existing `My tasks` matching behavior for exact username, display-name forms, and logged-in user context.
  - [x] Update affected tests in `TransactionPanelStateTests`.

- [x] Improve GPS extraction coverage. (AC: 2)
  - [x] Confirm scanned/vision route continues emitting `gps_instrument_number` and `gps_serial_number`.
  - [x] Add embedded-text route extraction patterns for GPS instrument and GPS serial number when visible in PDF text.
  - [x] Add/update Python tests for GPS text extraction.

- [x] Polish Memorandum tab. (AC: 3-4)
  - [x] Remove visible `Memorandum Detection` summary wording from the tab surface.
  - [x] Add bulk unresolved-disposition control for memorandum rules.
  - [x] Apply the selected disposition only to unresolved rows (`Needs Review` / `Failed`) unless the user explicitly edits individual rows.
  - [x] Preserve per-row manual editing and save behavior.

- [x] Filter and compact Validation Findings UI. (AC: 5-7)
  - [x] Filter the UI collection/projection to only the five requested friendly rule names/rule IDs.
  - [x] Keep underlying validation summary/disposition artifacts unfiltered.
  - [x] Reduce the grid row height and text box minimum height.
  - [x] Update validation-finding UI tests to assert hidden-only behavior.

- [x] Consolidate Participants tab. (AC: 8-10)
  - [x] Rename `Owners / Neighbors` tab to `Participants`.
  - [x] Replace the two-grid layout with one grid.
  - [x] Merge owner/party/representative rows and neighbor rows into one visible participant projection.
  - [x] Ensure `Neighbor` role rows still save back into the adjacent-owner/neighbor model used by Innola finalize.
  - [x] Add/update XAML and ViewModel tests for tab label, columns, and persistence mapping.

- [x] Simplify Boundary Segments grid. (AC: 11)
  - [x] Remove or hide `Adjacent Owner` and `Status` columns from the visible grid.
  - [x] Preserve segment properties and JSON round-trip behavior.

- [x] Combine early compute checks and clarify extraction actions. (AC: 12-14)
  - [x] Add a combined early-check command/action in the dockpane that runs Structure Check, Georeference Check, and Dimension Check sequentially.
  - [x] Refresh workflow properties after each stage so completion badges update during the run.
  - [x] Stop on blockers and preserve existing blocker messages.
  - [x] Keep the workflow stopped before extraction review.
  - [x] Rename or reduce the forced extraction action to a single clear `Rerun Extraction` action.
  - [x] Confirm whether the normal action opens existing review data and only runs extraction when no artifact exists.

- [x] Verify current build. (AC: 1-14)
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`.
  - [x] Run targeted Python tests for extraction route changes.
  - [x] Document any ArcGIS Pro manual smoke checks needed after rebuild.

### Review Findings

- [x] [Review][Patch] Transaction panel registered test still expects all rows after the new `My tasks` default filter. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs:527]
- [x] [Review][Patch] `My tasks` matching no longer covers display-name-only assignment, which contradicts the story guardrail to preserve display-name forms. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs:608]
- [x] [Review][Patch] Combined `Run Checks` buttons are enabled only by `workflowSession.CanRunStructureCheck`, so a case already past Structure Check can be blocked from running remaining Georeference/Dimension checks. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs:541]
- [x] [Review][Patch] Changing a named-party participant row to `Neighbor` leaves it backed by the named-party model, while Innola neighbor finalize reads only adjacent-owner rows. [src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs:456]
- [x] [Review][Patch] Embedded-text GPS instrument extraction can over-capture the following serial label when instrument and serial text are coalesced onto one PDF line. [src/ProcessingTools/adapters/pdf_text_structured_extraction.py:79]

## Dev Notes

### Current Code Evidence

- `TransactionPanelState` currently initializes `selectedFilter = "All tasks"` while `Filters` includes `All tasks`, `My tasks`, and `Group tasks`.
- Existing `My tasks` behavior already filters through `MatchesCurrentUser(row.AssignedUser)`.
- Scanned/vision extraction already emits `gps_instrument_number` and `gps_serial_number` in `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`.
- C# persistence/reporting already knows those GPS metadata keys in `ExtractionReviewPersistenceService` and `ComputeExaminationReportService`.
- Embedded text extraction should be checked and extended in `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`.
- Memorandum rule options currently live in `ExtractionReviewMemorandumRuleResultViewModel`.
- The current Memorandum tab renders group `Summary`; the unnecessary visible text likely comes from `PxaMemorandumSummary` / memorandum group summary projection.
- Validation Findings baseline/friendly rules already exist in `ValidationFindingDispositionProjector` and `ValidationFindingDispositionRow`.
- The current Validation Findings grid row height is `96` in `JamaicaReviewWorkspaceWindow.xaml`.
- The current `Owners / Neighbors` tab has two grids: `VisibleNamedParties` and `VisibleAdjacentOwners`.
- Innola Plan Check finalize reads neighbors from reviewed adjacent owners where `Role == "Neighbor"`. Do not break this persistence path.
- Boundary Segments grid currently displays `Adjacent Owner` and `Status`; remove them only from the UI.
- `WorkflowSession.RunManifestPreflightAsync` already runs Structure, Georeference, and Dimension in sequence and stops on blockers, but `ParcelWorkflowDockpaneViewModel` currently exposes separate commands in the dockpane.

### Implementation Guardrails

- Keep this patch small and current-branch friendly. Do not start the PE/PXA unification refactor here.
- Do not change final Innola writeback semantics.
- Do not change Enterprise working layer publish semantics.
- Do not remove stored data fields just because columns are hidden.
- Keep the Validation Findings filter UI-only.
- Preserve case-folder JSON compatibility for existing test cases.
- Use existing WPF and ViewModel patterns; avoid creating a new UI framework or new transaction model.

## Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationFindingDisposition.cs`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/adapters/survey_plan_ocr_vision_extraction.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ValidationFindingDispositionTests.cs`
- `src/ProcessingTools/tests/test_survey_plan_ocr_vision_extraction.py`

## References

- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/4-8-split-structure-check-and-dimension-check-into-separate-actions-and-result-summaries.md`
- `_bmad-output/implementation-artifacts/4-9-add-georeference-check-stage-and-reportable-stage-findings-model.md`
- `_bmad-output/implementation-artifacts/4-11-add-pxa-memorandum-detection-and-review-rules.md`
- `_bmad-output/implementation-artifacts/4-12-improve-pe-pxa-memorandum-extraction-semantic-review-rules.md`
- `_bmad-output/implementation-artifacts/5-25d-apply-points-validation-tool-icon-and-action-deduplication-polish.md`
- `_bmad-output/implementation-artifacts/7-11-write-innola-plan-check-list-on-compute-finalize.md`
- `_bmad-output/implementation-artifacts/11-2-unified-pdf-extraction-contract-for-plan-examination.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-09-12 | 0.1 | Created current-testing stabilization story for review UX, GPS extraction, default transaction filter, combined early checks, and extraction action cleanup. | Codex |
| 2026-09-12 | 1.0 | Implemented current-testing UX/extraction polish and marked story ready for review. | Amelia |

## Dev Agent Record

### Completion Notes

- Changed Transaction List default filter to `My tasks` while preserving existing assigned-user matching.
- Added deterministic embedded-text GPS metadata extraction for `gps_instrument_number` and `gps_serial_number`; scanned/vision GPS support remains covered by existing tests.
- Removed the noisy memorandum detection summary from the tab-level text and added a bulk `Apply to Needs Review` disposition action.
- Filtered Validation Findings in the Points Validation Tool UI to the five requested examiner-facing rules only; backend validation summary/disposition data remains unfiltered.
- Renamed `Owners / Neighbors` to `Participants`, replaced the two-grid layout with one merged participant grid, and kept neighbor rows backed by the existing adjacent-owner model used by Innola finalize.
- Removed `Adjacent Owner` and `Status` from Boundary Segments UI columns without deleting model fields.
- Changed early compute stage buttons to `Run Checks`, executing Structure, Georeference, and Dimension sequentially with refreshes and blocker stops; renamed forced extraction action to `Rerun Extraction`.

### Verification

- PASS: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false`
- PASS: `python -m unittest tests/test_pdf_text_structured_extraction.py` from `src\ProcessingTools`
- PASS: `python -m unittest tests/test_survey_plan_ocr_vision_extraction.py` from `src\ProcessingTools`
- PARTIAL: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build` passed story-related tests, including transaction default filter, memorandum/XAML, combined checks, validation findings, participants grid, and report tests, then stopped later on `System.IO.FileNotFoundException: ArcGIS.Desktop.Mapping, Version=13.6.0.0` in `SpatialOverlapReviewPersistenceServiceTests.OverlapReviewServiceBlocksWhenNoTargetsAreConfigured`. This appears to be a local ArcGIS SDK runtime assembly resolution issue outside this patch.
- Manual ArcGIS Pro smoke still needed after rebuild/install: open Transaction List, confirm default `My tasks`; load a PE/PXA case; click `Run Checks`; open Points Validation Tool; confirm Memorandum bulk apply, Participants tab, compact Validation Findings, Boundary Segment columns, and `Rerun Extraction`.

### File List

- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/TransactionPanelStateTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ValidationFindingDispositionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewMetadataViewModels.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Validation/ValidationFindingDisposition.cs`
- `src/ProcessingTools/adapters/pdf_text_structured_extraction.py`
- `src/ProcessingTools/tests/test_pdf_text_structured_extraction.py`
