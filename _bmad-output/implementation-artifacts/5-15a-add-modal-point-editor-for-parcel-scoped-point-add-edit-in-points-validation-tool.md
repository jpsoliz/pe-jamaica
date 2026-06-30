---
baseline_commit: 633d107ce7cc7fd6168d648e9ec544ac959ceb61
---

# Story 5.15A: Add Modal Point Editor For Parcel-Scoped Point Add/Edit In Points Validation Tool

Status: review

## Story

As a cadastral examiner correcting extracted parcel points in the Points Validation Tool,  
I want point add/edit actions to open a focused modal editor instead of forcing direct grid-cell typing,  
so that I can enter full point values accurately, validate them before commit, and avoid the current one-character-at-a-time editing friction.

## Acceptance Criteria

1. Given the examiner clicks `Add point`, when the action starts, then the tool opens a modal point editor for the active parcel instead of requiring inline grid editing.
2. Given the examiner selects an editable point row, when `Edit point` is invoked, then the modal opens prefilled with that row’s current values.
3. Given parcel ownership must stay stable during manual point work, when the modal is open, then the active parcel context remains locked to the parcel that owns the point being added or edited.
4. Given parcel sequence matters, when a new point is created through the modal, then the default sequence is the next valid parcel-local sequence unless the workflow explicitly allows ordered insertion.
5. Given point edits need multiple related values, when the modal is shown, then it supports editing the point identifier, easting, northing, length, extraction status, and any existing reviewer-editable fields that already flow through the review document.
6. Given required values and numeric values must be trustworthy, when the examiner saves the modal, then the tool validates required fields and numeric coordinate fields before applying the change to the review dataset.
7. Given a modal save can fail validation, when the user submits invalid data, then the modal stays open and shows clear field-level or form-level feedback without partially applying the change.
8. Given the examiner may cancel an in-progress add or edit, when the modal is closed with cancel/discard, then no partial point change remains in the dataset and no orphan manual row is left behind.
9. Given the parcel preview is a live review aid, when a point change is saved from the modal, then the active parcel grid, parcel summary, and parcel preview refresh immediately.
10. Given review approval depends on the edited data, when a point is added or updated through the modal, then the existing dirty-state, validation gating, and `Validation Complete` enable/disable behavior continue to work without regression.
11. Given extracted rows are still source-derived records, when the modal is used on an extracted row, then the tool respects the current workflow’s edit permissions and does not silently allow destructive behavior that the existing review model forbids.
12. Given the Points Validation Tool is already in live use, when this story is implemented, then the user-facing copy, button tooltips, and action labels clearly describe add/edit behavior without introducing new workflow ambiguity.

## Tasks / Subtasks

- [x] Introduce a dedicated modal point editor workflow. (AC: 1-3, 8, 12)
  - [x] Add a modal window/dialog purpose-built for point add/edit in the Points Validation Tool.
  - [x] Pass the active parcel context into the modal when creating a new point.
  - [x] Pass the selected row into the modal when editing an existing point.
  - [x] Keep parcel-switching and conflicting review actions locked while the modal is open.

- [x] Support add and edit flows through the same point editor model. (AC: 2-5, 11)
  - [x] Prepopulate defaults for add mode using the current parcel and next sequence value.
  - [x] Prepopulate current values for edit mode from the selected row.
  - [x] Define clearly which fields are editable for manual rows versus extracted rows.
  - [x] Add an explicit `Edit point` action if the current toolbar only exposes add/remove/discard.

- [x] Add validation and safe commit behavior. (AC: 6-8, 10)
  - [x] Validate required values before closing the modal with save.
  - [x] Validate numeric coordinate fields and any existing parcel-local uniqueness rules that apply at save time.
  - [x] Keep invalid edits inside the modal rather than mutating the main dataset incrementally.
  - [x] Ensure cancel/discard removes provisional add rows and leaves edit rows unchanged.

- [x] Refresh downstream review surfaces after modal save. (AC: 9-10)
  - [x] Refresh the visible row list for the active parcel.
  - [x] Refresh parcel-local counts/summaries.
  - [x] Refresh the parcel preview immediately.
  - [x] Re-run existing parcel-scoped validation/dirty-state logic after commit.

- [x] Refine Points Validation Tool UX around the modal. (AC: 1, 2, 8, 12)
  - [x] Update tooltips and labels so `Add point` and `Edit point` communicate modal behavior.
  - [x] Ensure focus returns cleanly to the relevant row after modal close where practical.
  - [x] Avoid duplicate workflow-completion actions inside the modal; keep stage progression in the main tool footer.

- [x] Verify no regression in current review flow. (AC: 9-12)
  - [x] Confirm `Save`, `Validation Complete`, and `Close` still behave correctly after point edits.
  - [x] Confirm parcel-scoped validation and preview behavior still update correctly.
  - [x] Confirm manual add/remove behavior remains consistent with Story 5.15.

## Dev Notes

### Why This Story Exists

The current Points Validation Tool is functionally close, but point editing still relies on direct DataGrid cell editing. In practice this has created two concrete examiner problems:

- typing feels constrained or awkward when changing multiple values
- point creation and correction are harder than they need to be because related values are not edited as one coherent record

This story moves point add/edit into a compact modal editor so the examiner can work on one point at a time, validate it cleanly, and return to the parcel review with less friction.

### Relationship To Existing Stories

- Story `5.15` introduced parcel-scoped manual point behavior, edit locking, preview updates, and approval blockers.
- Story `5.16B` established save/return flow and downstream handoff from Points Validation Tool into `Create Spatial Units`.
- Story `5.25C` refined parcel preview readability and rule-status display.
- This story should build on those behaviors rather than replacing them.

### Current Implementation Context

Relevant code paths already in place:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`

The current grid editing path uses `UpdateSourceTrigger=PropertyChanged`, which explains the poor editing feel for multi-character field changes. The modal should avoid mutating the live row on every keystroke and instead commit only after validation passes.

### Recommended Interaction Model

Preferred add/edit workflow:

1. Examiner selects a parcel.
2. Examiner clicks `Add point` or selects an existing row and clicks `Edit point`.
3. Modal opens with parcel context fixed.
4. Examiner edits all relevant values in one place.
5. Modal validates the full point record.
6. On save, the tool applies the committed change, refreshes preview/validation, and returns focus to the parcel review.
7. On cancel, no partial point state remains.

### Field Scope Recommendation

The modal should support the current point-editing data model first, not invent a broader cadastral editor. Minimum editable fields should align to existing review data and current user pain:

- point identifier
- easting
- northing
- length
- extraction status

Optional fields should be exposed only if they already exist in the review document and can be safely persisted without widening unrelated contracts.

### Scope Guardrails

This story should improve:

- add/edit ergonomics
- validation clarity before commit
- parcel-safe point correction flow
- preview refresh after save

This story should not change:

- attachment/source selection
- extraction logic
- closure/readiness rule definitions
- final workflow stage sequencing
- parcel-fabric or spatial-output generation logic

### Testing Focus

At minimum verify:

1. add-point modal creates a parcel-local point cleanly
2. edit-point modal updates an existing row without partial saves
3. invalid numeric values are blocked inside the modal
4. cancel leaves no orphan add row behind
5. parcel preview refreshes after save
6. `Validation Complete` gating still reflects the saved review state correctly

## References

- `_bmad-output/implementation-artifacts/5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`
- `_bmad-output/implementation-artifacts/5-16b-implement-points-validation-tool-save-return-flow-and-downstream-stage-handoff.md`
- `_bmad-output/implementation-artifacts/5-25c-refine-points-validation-preview-modes-and-rule-status-readability.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-29 | 0.1 | Drafted follow-up story for modal point add/edit flow in Points Validation Tool, aligned to parcel-scoped review and existing save/validation behavior. | Codex |
| 2026-06-29 | 1.0 | Implemented modal point add/edit flow, locked parcel switching while the dialog is open, added point-draft validation, and switched the review grid to modal-driven editing. | Codex |
| 2026-06-30 | 1.1 | Completed regression stabilization for the modal point editor story and restored the full test harness to green. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj` - passed.
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` - passed.
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj` - passed, 264 tests.

### Completion Notes List

- Added a dedicated point-edit draft model and modal dialog so point add/edit work happens off-grid and only commits after validation passes.
- Reworked `Add point` to create a provisional manual row in memory first, open the modal, and only add the row to the live review dataset when the dialog is saved.
- Added explicit `Edit point` behavior for selected rows and kept parcel switching locked while the dialog is open.
- Switched the review grid to read-only so the modal is now the primary editing path instead of `UpdateSourceTrigger=PropertyChanged` cell edits.
- Preserved downstream refresh behavior by applying committed edits through the row view model and re-running normal review dirty-state/validation refresh logic.
- Stabilized regression coverage that had drifted around canonical source roles, preflight/structure-check vocabulary, optional DWG validation, settings rule round-trip persistence, Innola task search payloads, and transaction-panel cancel-exit selection.
- The add-in and tests projects build cleanly; the console-style test runner passes all 264 tests.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointEditDialogViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointEditDialogWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/PointEditDialogWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/SettingsWorkspaceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/TransactionPanelState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/CreateParcelDraftExtractionAdapter.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/PointEditDraft.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionDetailServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Intake/SourceInputProfileDetectorTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/PreflightRuleCatalogLoaderTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/TestAssert.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ParcelScopedReviewValidationServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/PointEditDraftTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/WorkflowRules/WorkflowRuleResolverTests.cs`
