---
baseline_commit: handoff-2026-06-12
---

# Story 4.2B: Add Embedded Source Viewer For PDF/TIFF/Image Verification

Status: review

## Story

As a cadastral technical staff user,
I want to view the active source document inside the Parcel Workflow review workspace,
so that I can verify extracted points against PDF, TIFF, and image evidence without constantly leaving ArcGIS Pro or switching to external applications.

## Acceptance Criteria

1. Given Extraction Review is active and a point-bearing source file exists, when the review workspace loads, then the pane shows an embedded source viewer region for supported verification formats instead of only `Open source` / `Reveal` actions.
2. Given the active review source is a PDF, TIFF, TIF, PNG, JPG, or JPEG file, when the viewer loads, then the user can see the source content directly inside the add-in workspace.
3. Given a supported source file is displayed, when the user needs to inspect point evidence, then the viewer provides the minimum practical controls needed for verification inside ArcGIS Pro such as fit/refresh and clear indication of the currently loaded file.
4. Given the user selects or changes the review source document, when the workspace refreshes, then the embedded viewer updates to the currently resolved source file without losing the loaded review rows.
5. Given the viewer cannot render a source file because the format is unsupported, the file is missing, or rendering fails, when the workspace shows that source, then the pane presents a clear fallback state and retains `Open source` / `Reveal` actions so the user is not blocked.
6. Given the viewer is present, when the user edits or reviews extracted rows, then the compact point review table remains visible and usable in the same workspace rather than forcing a separate modal workflow.
7. Given the viewer is introduced, when existing workflow actions run, then current behavior for review save, approval, validation launch, and output gating remains unchanged.
8. Given this story is complete, then the embedded viewer supports verification use only; it does not introduce OCR, in-view annotations, page markup, geometry editing, or changes to processing adapters.
9. Given this story is complete, then focused tests cover supported-format source resolution, fallback rendering behavior, active-source refresh behavior, and no regression to the current review workflow state/gating.

## Tasks / Subtasks

- [x] Add an embedded source viewer surface to the Extraction Review workspace. (AC: 1-6, 8-9)
  - [x] Refactor the current source document panel in `ParcelWorkflowDockpane.xaml` to host a real viewer region instead of text-only guidance plus external-launch buttons.
  - [x] Keep the compact review table visible alongside the viewer in the same workspace.
  - [x] Preserve current workspace density for ArcGIS Pro dockpane constraints.
- [x] Implement supported-format rendering for verification sources. (AC: 2-5, 8-9)
  - [x] Support PDF rendering for the resolved review source.
  - [x] Support TIFF/TIF rendering for scanned raster sources.
  - [x] Support PNG/JPG/JPEG rendering for image-based sources.
  - [x] Show clear file name, source role, and load state in the viewer header.
- [x] Provide practical verification controls without overbuilding the surface. (AC: 3, 6, 8)
  - [x] Add lightweight controls such as fit/reload or equivalent minimal viewer actions.
  - [x] Keep `Open source` and `Reveal` as fallback/secondary actions.
  - [x] Do not introduce annotation, mark-up, or OCR controls in this story.
- [x] Handle viewer refresh and failure cases cleanly. (AC: 4-5, 9)
  - [x] Refresh the viewer when the selected or resolved review source changes.
  - [x] Show a deterministic fallback state when the file is missing, unsupported, or cannot be rendered.
  - [x] Ensure the fallback state does not wipe the loaded review dataset.
- [x] Keep workflow behavior unchanged outside the viewer. (AC: 6-9)
  - [x] Preserve extraction review save/approve semantics from Story 2.14A.
  - [x] Preserve validation gating from Story 4.1.
  - [x] Do not change `WorkflowSession` stage authority beyond what is needed to expose current source-view state.
- [x] Add focused tests and validation. (AC: 9)
  - [x] Add or extend tests for review source resolution and viewer-state projection.
  - [x] Verify supported vs unsupported source behavior.
  - [x] Verify review data remains loaded when viewer state changes.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.

## Dev Notes

### Why This Story Exists

- The current review workspace is source-first in concept, but still depends on launching the associated desktop application for actual inspection.
- The user explicitly asked for the ability to verify points against the source document inside the add-in.
- The review task is tightly coupled to visual evidence, so switching in and out of ArcGIS Pro adds friction at the exact moment the user needs precision.

### Current Implementation Reality

Today the review workspace in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml` shows:

- source document title and path,
- source guidance text,
- `Open source` and `Reveal` buttons,
- but no embedded rendering surface.

The view model in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs` already resolves:

- the preferred review source,
- the file type,
- source title/path/guidance,
- and the open/reveal actions.

That means the source-selection logic largely exists already; this story is mainly about upgrading presentation and in-pane usability.

### Scope Intent

This story should add an **embedded verification viewer** to the Extraction Review workspace.

It should:

- render supported source evidence in-pane,
- keep point editing and evidence side by side,
- preserve fallback launch actions,
- and improve review flow without changing workflow semantics.

It should **not**:

- add OCR or AI extraction behavior,
- alter review approval rules,
- create geometry outputs,
- introduce in-view editing/annotation,
- or replace the existing processing pipeline.

### Suggested Technical Direction

Choose the simplest robust WPF-compatible viewer approach that fits ArcGIS Pro hosting constraints.

Good implementation direction:

1. keep source resolution in the current view model,
2. project a viewer model/state from the resolved file,
3. render images/TIFF directly in WPF where practical,
4. use a safe PDF host/viewing approach appropriate for ArcGIS Pro/WPF,
5. fall back gracefully to external open/reveal actions when in-pane rendering is not available.

Do not introduce a brittle dependency unless it is clearly stable in the ArcGIS Pro 3.6/3.7 environment.

### UX Expectations

The viewer should feel like a verification aid, not a document-management subsystem.

Minimum expectations:

- clear file identity,
- visible load status,
- enough scaling/fit behavior to inspect evidence,
- no modal interruption,
- stable side-by-side pairing with the compact point table.

The point table remains the editing surface. The embedded viewer is there to support judgment and correction.

### Code Areas That Matter

Read and preserve behavior in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`

### Dependency Notes

- Story 2.14A already established the source-first review workspace and compact point table.
- Story 4.2A can improve the shell/layout hierarchy around stages, but 4.2B should still be implementable against the current review workspace if needed.
- Validation and Outputs stories should not assume an external viewer once this is complete.

### Testing Guidance

Prefer testing:

- resolved source -> viewer state mapping,
- supported extension detection,
- fallback state rendering decisions,
- and non-regression of loaded review data / command enablement.

Avoid overpromising UI automation that the ArcGIS host does not make easy in the current test harness.

### Environment Caveat

PDF hosting inside WPF/ArcGIS Pro may be the riskiest part of this story. If a full embedded PDF surface proves unstable, the implementation should still preserve the story’s intent with the best reliable in-pane rendering strategy available and a strong fallback path. Note that tradeoff explicitly during implementation if it becomes necessary.

### References

- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-review-before-output.html`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`
- `_bmad-output/implementation-artifacts/4-2a-redesign-parcel-workflow-into-stage-focused-workspace.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Completion Notes List

- Created the companion story for adding an embedded source viewer to the extraction review workspace.
- Kept the scope tightly centered on verification UX and fallback behavior, not processing logic.
- Called out PDF hosting risk explicitly so implementation can choose a stable ArcGIS Pro-compatible approach.
- Added an active review-source selector plus an embedded verification surface that can render PDF, TIFF/TIF, PNG, JPG, and JPEG evidence directly inside the Extraction Review workspace.
- Preserved the side-by-side compact point table and kept `Open source` / `Reveal` available as fallback actions while adding `Reload` and `Fit/Actual size` controls.
- Added focused tests for source selection refresh behavior, supported-format viewer projection, and deterministic fallback states for missing, unsupported, or render-failed sources.
- Verified the story with `dotnet build`, `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`, and `powershell -ExecutionPolicy Bypass -File tools\package_addin.ps1`.

### File List

- `_bmad-output/implementation-artifacts/4-2b-add-embedded-source-viewer-for-pdf-tiff-image-verification.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ReviewSourceSelectionResolver.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ReviewSourceViewerState.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ReviewSourceViewerStateProjector.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ReviewSourceSelectionResolverTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ReviewSourceViewerStateProjectorTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for embedded source viewer support in the extraction review workspace. | Codex |
| 2026-06-12 | 0.2 | Implemented embedded viewer rendering, source selection/refresh behavior, fallback states, focused tests, and packaged add-in deployment. | Codex |
