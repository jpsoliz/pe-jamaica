---
baseline_commit: handoff-2026-06-16
---

# Story 5.15: Parcel-Scoped Manual Point Editing and Live Parcel Preview Controls in Jamaica COGO Tool

Status: review

## Story

As a cadastral examiner reviewing extracted computation data in the Jamaica COGO Tool,  
I want manual point editing to stay scoped to the active parcel and reflected immediately in the parcel preview,  
so that I can safely correct parcel geometry without accidentally mixing edits across parcels or approving incomplete manual changes.

## Story Requirements

### User Story Statement

The Jamaica COGO Tool now gives examiners a dedicated review surface with parcel grouping, a source viewer, and parcel preview. However, the current manual-point workflow is still only partially shaped for production use:

- `Add point` is not yet explicitly parcel-aware
- `Active parcel` can still change during manual editing
- manual rows are not yet governed by a formal edit-mode contract
- parcel preview behavior is close to live, but not yet defined as a reliable on-the-fly review signal
- error handling is still implicit rather than deliberate

This story hardens the review workspace into a parcel-safe editing experience where manual corrections belong to the currently selected parcel, edit-state rules are clear, preview updates feel immediate, and approval remains blocked until the edited parcel data is valid.

### Acceptance Criteria

1. Given the examiner is reviewing one active parcel at a time, when `Add point` is used, then the new manual row is assigned to the currently selected parcel group rather than being created as a parcel-agnostic row.
2. Given parcel sequence matters inside Jamaica parcel review, when a manual point is added, then the new row is assigned the next valid `sequence_in_group` for the active parcel unless the user later edits sequence explicitly where supported.
3. Given parcel switching during manual entry can cause accidental cross-parcel edits, when the workspace enters manual point edit mode, then the `Active parcel` selector becomes temporarily locked until the user saves or cancels the in-progress manual edit.
4. Given a selected row may be extracted or manual, when `Remove point` is used, then only manual rows can be removed from the workspace and extracted rows remain protected from destructive delete behavior.
5. Given the workspace may contain only one parcel, when only one parcel group exists, then the active parcel control is either disabled or presented read-only rather than implying a choice that does not exist.
6. Given manual edits should affect only the parcel under review, when a point is added, removed, or edited, then the center review grid, parcel summary, and right-side parcel preview all remain scoped to the active parcel and do not bleed changes into unrelated parcel groups.
7. Given the parcel preview is the examiner’s quick geometry confidence aid, when point coordinates or point membership change for the active parcel, then the parcel preview updates on-the-fly without requiring a full workflow rerun.
8. Given manual edits may be incomplete or invalid, when a manual row is missing required values or has invalid parcel/sequence/coordinate data, then the workspace shows a clear validation/error state and approval remains blocked.
9. Given the examiner may abandon a partial manual add, when a manual point is created but not completed, then the workspace provides an explicit way to cancel or discard the in-progress add without leaving orphaned parcel rows behind.
10. Given review approval must remain trustworthy, when manual edits exist, then `Approve review` stays blocked until all parcel-scoped blockers are cleared, including missing point identifiers, invalid coordinates, duplicate point IDs inside the parcel, invalid sequence values, or unassigned parcel membership.
11. Given the current Jamaica review workspace is no longer just a spike, when this story is implemented, then the window copy, control states, and messages describe parcel-scoped editing as part of the supported review workflow rather than as provisional behavior.

## Tasks / Subtasks

- [x] Make manual point creation parcel-aware. (AC: 1-2, 6)
  - [x] Update manual row creation so new rows inherit the active parcel group and traverse context.
  - [x] Assign the next valid parcel-local sequence value during add.
  - [x] Define the point-id initialization rule for newly added manual rows in a way that fits parcel-local review.

- [x] Introduce explicit manual edit mode controls. (AC: 3, 9, 11)
  - [x] Add a tracked edit-mode state for in-progress manual add/edit operations.
  - [x] Disable or lock the `Active parcel` selector during in-progress manual editing.
  - [x] Add an explicit cancel/discard path for incomplete manual adds.
  - [x] Ensure status and footer messaging describe the edit lock and recovery path clearly.

- [x] Harden remove-point behavior. (AC: 4, 6, 8)
  - [x] Keep extracted rows non-destructive/read-only for delete.
  - [x] Allow delete only for manual rows.
  - [x] Ensure row removal updates parcel counts, parcel summaries, selected-row state, and dirty-state tracking cleanly.

- [x] Make parcel preview update as a first-class live review aid. (AC: 6-7)
  - [x] Refresh the active parcel preview immediately after point add, remove, coordinate edit, or parcel-local sequence change.
  - [x] Keep preview updates scoped to the current parcel rather than rebuilding unrelated parcel groups unnecessarily.
  - [x] Define fallback behavior when preview geometry cannot be formed because the manual row is incomplete.

- [x] Add parcel-scoped validation and error control. (AC: 8-10)
  - [x] Enforce required point fields for manual rows.
  - [x] Validate numeric easting/northing values.
  - [x] Validate duplicate point IDs within the active parcel.
  - [x] Validate duplicate or invalid sequence values within the active parcel.
  - [x] Keep approval blocked while unresolved or invalid manual parcel rows remain.
  - [x] Surface actionable examiner-facing error messages rather than generic failure text.

- [x] Refine single-parcel and control-state UX. (AC: 5, 11)
  - [x] If only one parcel exists, render the parcel selector as disabled or read-only.
  - [x] Review related controls (`Add point`, `Remove point`, `Save review`, `Approve review`) so their enable/disable logic matches edit mode and review lock state.
  - [x] Ensure control-state behavior remains compact and understandable inside ArcGIS Pro.

## Dev Notes

### Why This Story Exists

- Story 5.11 defined the Jamaica COGO-style review workspace.
- Story 5.13 and 5.14 established the floating review shell, source viewing, and parcel preview scaffolding.
- The current workspace now looks and feels close to the intended review tool, but manual point correction still lacks the parcel-safe behavior expected by examiners.

This story turns manual editing from a basic table affordance into a controlled parcel-scoped workflow.

### Current Implementation Context

The current code already contains:

- parcel grouping and parcel-specific row filtering in `JamaicaReviewWorkspaceViewModel`
- row-level editing through `ExtractionReviewRowViewModel`
- manual row creation in `ParcelWorkflowDockpaneViewModel.AddManualPoint()`
- manual row removal in `ParcelWorkflowDockpaneViewModel.RemoveSelectedManualPoint()`
- parcel preview generation through `BuildPreviewPoints()` and `BuildSelectedMarker()`

Relevant files include:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`

### Known Gaps in Current Behavior

At the time this story was written:

- manual rows are created with generic identifiers and do not deliberately inherit the active parcel group
- parcel switching is not locked during manual add/edit
- there is no explicit cancel/discard path for incomplete manual adds
- preview refresh is close to live but is not yet specified as a guaranteed parcel-edit feedback loop
- approval blockers for parcel-local manual edit errors are not yet formalized

### Recommended Behavioral Model

Preferred parcel-edit workflow:

1. Examiner chooses the active parcel.
2. Examiner selects `Add point`.
3. Workspace enters manual edit mode.
4. New row is created inside the current parcel with provisional defaults.
5. Parcel selector and any conflicting controls are locked while the row is incomplete.
6. Preview updates as soon as usable coordinates exist.
7. Examiner either saves the row or cancels/discards it.
8. Approval remains blocked until all parcel-scoped manual edit blockers are cleared.

### Validation Rules to Treat as Story-Binding

This story should define explicit parcel-scoped blockers for:

- missing point identifier
- missing easting
- missing northing
- non-numeric easting
- non-numeric northing
- duplicate point identifier inside the same parcel
- invalid or duplicate parcel-local sequence
- manual row with missing parcel assignment

These conditions should block approval and generate clear examiner-facing messages.

### Preview Update Recommendation

Best direction:

- keep parcel preview live/on-the-fly
- refresh only the active parcel’s preview model when possible
- tolerate incomplete manual rows by showing a partial-warning state rather than breaking preview rendering

This keeps the preview trustworthy without making the workspace feel unstable.

### UX Guardrails

- Do not allow manual edit mode to silently switch parcels.
- Do not allow delete of extracted rows.
- Do not require a full extraction rerun for manual point corrections.
- Do not leave orphan manual rows behind when the user cancels an add.
- Do not let the active parcel control imply meaningful switching when only one parcel exists.

### Suggested Success Criteria

This story is successful if:

1. a user can add a manual point and know exactly which parcel it belongs to,
2. parcel switching cannot accidentally corrupt the current edit context,
3. preview updates feel immediate enough to support geometry checking,
4. incomplete or invalid manual edits are blocked clearly before approval,
5. the workspace behaves like a supported parcel-edit review tool rather than a provisional spike.

## References

- `_bmad-output/implementation-artifacts/5-11-design-jamaica-cogo-style-review-workspace.md`
- `_bmad-output/implementation-artifacts/5-13-build-dev-spike-for-jamaica-cogo-style-review-workspace-shell.md`
- `_bmad-output/implementation-artifacts/5-14-replace-embedded-pdf-browser-with-unified-rendered-document-viewer-for-pdf-and-raster-verification.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false`
- `dotnet run --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added a dedicated parcel-scoped manual row builder so new manual points inherit the active parcel, traverse context, and next parcel-local sequence.
- Added parcel-scoped validation so approval blocks on duplicate point ids, duplicate sequences, invalid coordinates, missing parcel assignment, unresolved rows, and in-progress manual edits.
- Locked active parcel switching while a manual point is in progress and added an explicit discard path for incomplete manual adds.
- Wired live review refresh so parcel summaries and preview bindings react to review content changes without rerunning extraction.
- Updated Jamaica review workspace controls and copy to reflect supported parcel-scoped review behavior instead of provisional spike language.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedManualPointService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ParcelScopedManualPointServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ParcelScopedReviewValidationServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/CreateParcelDraftExtractionAdapterTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for parcel-scoped manual point editing, edit-mode locking, parcel-local validation, and live parcel preview behavior in the Jamaica COGO Tool. | Codex |
| 2026-06-17 | 1.0 | Implemented parcel-scoped manual add/discard flow, parcel-local validation, parcel-lock behavior, and live review refresh wiring; updated tests and story record. | Codex |
