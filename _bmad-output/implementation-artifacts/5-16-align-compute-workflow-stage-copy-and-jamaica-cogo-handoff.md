---
baseline_commit: handoff-2026-06-17
---

# Story 5.16: Align Compute Workflow Stage Copy And Jamaica COGO Handoff

Status: review

## Story

As a cadastral examiner using `Parcel Workflow [Compute]`,  
I want the workflow stages and actions to use business-friendly names and clearly hand off extracted-point review into the Jamaica COGO Tool,  
so that I can understand where I am in the process and what to do next without confusing the broader transaction workflow with the detailed review tool.

## Acceptance Criteria

1. Given the compute workflow shell is displayed, when stage labels are shown in the dock pane and status messages, then the user-facing names align to the agreed workflow vocabulary:
   - `Attachments`
   - `Files Checks`
   - `Point Review`
   - `Quality Check`
   - `Create Spatial Outputs`
   - `Map Review`
   - `Finalize`
2. Given a stage has a detailed panel in the dock pane, when its explanatory text is shown, then the longer copy describes the purpose of the step in plain operational language rather than legacy internal wording such as `preflight` or raw extraction terminology.
3. Given the first workflow step is shown, when attachment intake is complete, then the stage title is `Attachments` and its longer description communicates that source attachments are loaded from the selected transaction.
4. Given the file-readiness step is shown, when checks are listed, then the stage title is `Files Checks` and its longer description communicates that attached files are being validated before downstream processing.
5. Given extraction has produced usable review artifacts, when the user reaches `Point Review`, then the primary review action launches the `Jamaica COGO Tool` rather than presenting the older embedded extracted-points review wording as the main reviewer surface.
6. Given `Files Checks` has passed, when the shell advances into `Point Review`, then the stage language makes clear that this step is where extraction results are generated and reviewed, and not part of file-readiness checking.
7. Given the user is in `Point Review`, when the workflow explains the next action, then the shell clearly distinguishes:
   - `Review in Jamaica COGO Tool`
   - versus later `Map Review`
   so the examiner understands that point interpretation happens first and in-map editing happens afterward.
8. Given extraction produces no usable review rows, zero matches, or other unusable result conditions, when the shell communicates next steps, then it does not present that outcome as a `Files Checks` failure and instead leaves the decision about rerun/manual review to the downstream Point Review decision gate.
9. Given review approval is completed in the Jamaica COGO Tool, when the compute workflow resumes, then the shell communicates that the next downstream action is `Create Spatial Outputs` followed by `Map Review`.
10. Given user-facing warnings, footer text, and stage status text are shown, when legacy wording still refers to `preflight`, `extraction review`, or other outdated labels, then those messages are updated to the current approved vocabulary where the meaning has changed.
11. Given this story is complete, then the workflow shell and Jamaica COGO handoff read as one coherent process rather than two loosely connected tools.

## Tasks / Subtasks

- [x] Align stage labels across the compute workflow shell. (AC: 1, 8-9)
  - [x] Update lifecycle stage labels, active-stage labels, and status chips.
  - [x] Update dock-pane section headers where the shell still shows older terminology.
  - [x] Update footer/status text and warning text that still reference superseded stage names.

- [x] Improve explanatory copy for each workflow step. (AC: 2-4, 6-7)
  - [x] Add or revise longer descriptive copy for `Attachments` and `Files Checks`.
  - [x] Clarify the handoff between `Point Review`, `Create Spatial Outputs`, and `Map Review`.
  - [x] Keep the microcopy concise and operator-focused.

- [x] Make Jamaica COGO Tool the explicit review handoff from Point Review. (AC: 5-7, 9)
  - [x] Rename or reposition actions so the shell clearly launches the Jamaica COGO Tool for extracted-point review.
  - [x] De-emphasize or remove leftover copy that implies the older raw extracted-points surface is still the primary review workspace.
  - [x] Ensure approved review state returns clearly into the main workflow shell.

- [x] Add focused tests/manual verification notes. (AC: 8-9)
  - [x] Verify stage labels appear consistently in lifecycle buttons, workspace headers, and status text.
  - [x] Verify the Point Review action naming matches the Jamaica COGO Tool launch path.
  - [x] Verify downstream guidance after review approval points to `Create Spatial Outputs` and `Map Review`.

## Dev Notes

### Why This Story Exists

- The current shell still mixes older internal names with newer process intent, which makes the workflow harder to follow for examiners.
- The Jamaica COGO Tool has now effectively replaced the older extracted-point review surface for extracted-data cases, so the dock pane should say that plainly.

### UX Direction

- The dock pane should describe the overall transaction workflow.
- The Jamaica COGO Tool should describe the detailed point-review workspace.
- The product should feel like one process with a clear handoff, not two unrelated modules.

### Scope Boundaries

- This story is about naming, explanatory copy, and handoff clarity.
- This story does not implement the extraction-result decision gate itself; that follow-on behavior belongs after Point Review extraction attempt, not inside Files Checks.
- This story does not redesign map-editing tools or parcel-fabric generation logic.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`

## References

- `_bmad-output/implementation-artifacts/5-11-design-jamaica-cogo-style-review-workspace.md`
- `_bmad-output/implementation-artifacts/5-13-build-dev-spike-for-jamaica-cogo-style-review-workspace-shell.md`
- `_bmad-output/implementation-artifacts/5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false` currently fails on an existing WPF/XAML codegen issue in `ParcelWorkflowDockpane.xaml.cs` (`InitializeComponent` / `DockpanePdfWebView` not generated in the standalone project build path).

### Completion Notes List

- Updated compute workflow shell stage names to the approved vocabulary: `Attachments`, `Files Checks`, `Point Review`, `Quality Check`, `Create Spatial Outputs`, `Map Review`, and `Finalize`.
- Reworked dock-pane explanatory copy and status text so the handoff into Jamaica COGO Tool is explicit and downstream messaging now points from `Point Review` to `Create Spatial Outputs` and then `Map Review`.
- Added a regression test for workflow-state display names and updated workflow session assertions to the new operator-facing wording.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowStateExtensionsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for workflow-stage naming alignment and explicit Jamaica COGO review handoff in Parcel Workflow [Compute]. | Codex |
| 2026-06-17 | 1.0 | Implemented compute workflow stage-copy alignment, updated Jamaica COGO handoff messaging, and added regression coverage for the approved vocabulary. | Codex |
