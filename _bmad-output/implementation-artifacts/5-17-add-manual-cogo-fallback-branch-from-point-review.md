---
baseline_commit: handoff-2026-06-17
---

# Story 5.17: Add Manual Mode Fallback Branch From Point Review

Status: review

## Story

As a cadastral examiner reviewing extracted points from transaction PDFs,  
I want a clear Manual Mode fallback when automated point review is not good enough,  
so that I can add, remove, and edit point review rows before Create Spatial Units instead of being blocked by weak extraction results.

## Acceptance Criteria

1. Given extraction has produced a review artifact for the active transaction, when the examiner reaches `Point Review`, then the workflow offers a primary path to review the extracted data in the Jamaica COGO Tool.
2. Given extraction exists but the examiner decides the automated result is not good enough, when the examiner chooses the manual fallback option, then the workflow enables Manual Mode in Points Validation Tool rather than forcing continued reliance on the extracted rows.
3. Given the source of data for this workflow remains the transaction PDF attachments, when the manual fallback is chosen, then the workflow keeps the transaction source context available and does not require a separate non-transaction file-pick flow.
4. Given the manual fallback path is chosen, when the workflow updates state, then the shell clearly records that Manual Mode is active and explains that the user can add, remove, and edit points before save/approval.
5. Given the examiner remains on the extracted-point path, when Jamaica COGO review is approved, then the workflow continues toward `Create Spatial Outputs` and later `Map Review`.
6. Given the manual fallback path is chosen, when the user returns to the main shell, then the workflow does not misleadingly present the extracted review as fully approved.
7. Given the Jamaica COGO Tool is only valid when extracted review artifacts exist, when extraction produced no usable review artifact, then the shell does not offer the Jamaica COGO Tool as if it were available.
8. Given this story is complete, then the compute workflow supports both:
   - extracted-point review through Points Validation Tool, and
   - a deliberate Manual Mode branch for insufficient automated results.

## Tasks / Subtasks

- [x] Define the manual fallback decision point in the compute workflow. (AC: 1-4, 6-8)
  - [x] Identify where the Point Review step should expose the manual fallback action.
  - [x] Make the branch explicit rather than hidden behind generic wording.
  - [x] Record the selected branch in workflow state/audit.

- [x] Keep Jamaica COGO Tool gated to extracted-review cases. (AC: 1, 5, 7)
  - [x] Show the Jamaica COGO Tool path only when the review artifact exists.
  - [x] Prevent launch messaging that implies the tool is available without extracted review data.

- [x] Add the manual COGO branch behavior. (AC: 2-4, 6, 8)
  - [x] Add a user-facing action named `Manual Mode`.
  - [x] Update shell messaging to explain the manual path.
  - [x] Ensure the workflow can proceed without falsely marking extracted review approved.

- [x] Add verification coverage and operator guidance. (AC: 8)
  - [x] Verify the extracted-review path still works end to end.
  - [x] Verify the manual fallback branch changes state and guidance correctly.
  - [x] Verify the shell distinguishes the two paths clearly.

## Dev Notes

### Why This Story Exists

- The source attachments for this workflow are still transaction PDFs, but automated extraction quality will not always be good enough for an examiner to trust.
- The product needs a supported escape hatch into manual COGO work without pretending the automated review succeeded.

### Workflow Direction

- `Point Review` is the decision stage.
- `Points Validation Tool` is the extracted-review and manual point-review workspace.
- `Manual Mode` is the examiner-controlled fallback when extracted data is partial, empty, or insufficient.
- Later `Final Review` remains the in-map editing/spatial correction stage after Create Spatial Units.

### Scope Boundaries

- This story does not redesign the Jamaica COGO Tool internals.
- This story does not replace transaction-driven PDF sourcing with a separate local file chooser.
- This story does not implement final authoritative sync behavior.
- 2026-07-22 update: Manual Mode no longer routes immediately to the configured spatial/map-editing path. It keeps the case in editable point review, can create a blank review artifact, and requires save/approval before Create Spatial Units.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`

## References

- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-11-design-jamaica-cogo-style-review-workspace.md`
- `_bmad-output/implementation-artifacts/5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`
- `_bmad-output/planning-artifacts/research/cogo-reader-automation-boundary-evaluation-2026-06-16.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added explicit manual fallback workflow state `review_manual_pending` and mapped it through display, contract, reopen, and workspace-planning layers.
- Renamed the shell-level manual action to `Manual Mode` with guidance text so examiners can branch away from weak extraction results without falsely approving extracted review.
- Tightened Jamaica COGO Tool availability so the tool is only offered when extracted review artifacts exist for the active case.
- Preserved manual-fallback intent across extraction-review saves and reopen flows.
- Manual Mode now keeps Points Validation Tool editable for add/remove/edit point workflows and supports blank review artifact creation when extraction has no rows.
- Added automated coverage for workflow state mapping, workspace routing, reopen support, and manual-fallback session behavior.
- Verified the add-in project build and the full custom test runner pass cleanly.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowStateExtensionsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for manual COGO fallback branching from Point Review while keeping Jamaica COGO Tool as the extracted-review workspace. | Codex |
| 2026-06-17 | 1.0 | Implemented manual COGO fallback branching, gated Jamaica COGO Tool launch on extracted-review artifacts, and added regression coverage for state/reopen behavior. | Codex |
| 2026-07-22 | 1.1 | Updated the fallback to Manual Mode in Points Validation Tool, including blank/partial review editing and save-before-approval semantics. | Codex |
