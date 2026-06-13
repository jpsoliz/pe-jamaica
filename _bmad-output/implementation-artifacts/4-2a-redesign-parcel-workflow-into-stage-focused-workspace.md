---
baseline_commit: handoff-2026-06-12
---

# Story 4.2A: Redesign Parcel Workflow Into Stage-Focused Workspace

Status: review

## Story

As a cadastral technical staff user,
I want the Parcel Workflow dockpane to focus on the single stage I am actively working in,
so that I can move through intake, preflight, extraction review, validation, and outputs without the pane feeling overloaded or forcing me to scan unrelated sections.

## Acceptance Criteria

1. Given a transaction case is loaded, when the Parcel Workflow pane opens, then the workflow stages are shown through a compact stage-focused navigation surface that makes the current stage, completed stages, pending stages, and blocked stages immediately clear.
2. Given the user is in one workflow stage, when the pane renders, then the current stage workspace is visually dominant and inactive downstream stages are collapsed, minimized, or otherwise de-emphasized instead of all being fully expanded at once.
3. Given a stage is completed, when the user moves forward, then the completed stage remains visible as a compact summary with clear done-state treatment and quick access to reopen its details if needed.
4. Given a stage is blocked, when the pane renders, then the blocked stage is visually distinct, shows the blocking reason at summary level, and prevents the user from proceeding to later stages until the blocker is resolved or an allowed alternate path is chosen.
5. Given Extraction Review is the main working stage for human verification, when that stage is active, then the dockpane prioritizes the source-verification workspace and point editing surface over unrelated validation/output content.
6. Given Validation becomes the active stage after approved review data exists, when that stage is active, then the pane emphasizes validation findings and actions while extraction and output sections are reduced to compact context summaries.
7. Given Outputs becomes the active stage after validation passes, when that stage is active, then the pane emphasizes output artifact generation/preview while earlier stages remain accessible as compact summaries rather than full-height cards.
8. Given the workflow state changes because of save, approve, cancel, reopen, rerun, or validation blockage, when the session refreshes, then the stage-focused layout updates deterministically without losing the current case context or enabling commands that should remain gated.
9. Given the stage-focused redesign is implemented, when existing workflow commands run, then current behavior for preflight execution, extraction review run/open, review save/approval, validation launch, and output gating remains intact.
10. Given this story is complete, then the redesign improves clarity and space usage only; it does not itself add new processing adapters, embedded document rendering, geometry generation, or sync execution.
11. Given this story is complete, then focused tests cover stage-state projection, active-stage visibility rules, blocked-stage summaries, reopen/resume layout restoration, and no regression to command gating introduced by Stories 2.14A and 4.1.

## Tasks / Subtasks

- [x] Replace the always-expanded stacked layout with a stage-focused workspace shell. (AC: 1-4, 8-11)
  - [x] Refactor the dockpane XAML so the top workflow stage strip remains compact and persistent.
  - [x] Introduce an active-stage content host or equivalent layout pattern so only one primary stage workspace is expanded at a time.
  - [x] Keep completed and blocked stages visible through condensed summaries rather than fully expanded cards.
- [x] Redesign lifecycle presentation around stage status clarity. (AC: 1-4, 8, 11)
  - [x] Preserve the existing stage sequence: Intake, Preflight, Extraction Review, Validation, Outputs, Ready to Complete.
  - [x] Make pending, done, active, and blocked states clearer through typography, color, and compact state treatment aligned with the UX design tokens.
  - [x] Surface blocker text or counts directly in the condensed stage summary so users do not have to expand every section to understand why they are stopped.
- [x] Reframe stage content so only the relevant workspace dominates. (AC: 2-7, 10)
  - [x] When Extraction Review is active, prioritize source verification and point editing.
  - [x] When Validation is active, prioritize validation summary/findings space and reduce earlier/later stages to summaries.
  - [x] When Outputs is active, prioritize output preview/artifact work and keep prior stages compact.
- [x] Preserve quick access to prior-stage context without restoring visual overload. (AC: 3, 5-7, 10)
  - [x] Allow completed stages to expose summary metadata such as counts, status, and last result.
  - [x] Add lightweight expand/reopen affordances only where useful for reference.
  - [x] Avoid restoring the current “all cards fully visible” vertical stack as the default presentation.
- [x] Keep workflow/session gating authoritative in code-behind/session logic. (AC: 4, 8, 9, 11)
  - [x] Ensure visual stage focus follows `WorkflowSession.CurrentState` and does not invent parallel UI-only state transitions.
  - [x] Preserve current command enable/disable behavior for preflight, extraction review, validation, save, approve, cancel, and complete.
  - [x] Ensure reopen/resume restores the correct active stage and compact summaries from case artifacts/state.
- [x] Prepare the workspace for later stage-specific enhancements. (AC: 5-7, 10)
  - [x] Keep the Validation stage ready for Story 4.2 grouped findings work.
  - [x] Keep the Outputs stage ready for Epic 5 output artifact work.
  - [x] Do not entangle this layout refactor with embedded file viewers or new processing logic.
- [x] Add focused tests and validation. (AC: 8-11)
  - [x] Add or extend tests for workflow-stage projection and active-stage selection logic.
  - [x] Verify blocked states still prevent downstream actions.
  - [x] Verify reopen/resume restores stage-focused layout correctly after loading an existing Case Folder.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.

## Dev Notes

### Why This Story Exists

- The current Parcel Workflow pane still behaves like a tall stack of cards, which makes the ArcGIS Pro dockpane feel cramped as the workflow grows.
- The user has confirmed that the pane needs to stay manageable inside ArcGIS Pro’s limited width while still carrying multiple downstream stages beyond validation.
- Story 2.14A improved the Extraction Review workspace, but the overall container still needs a higher-level stage-focused redesign.

### Current Implementation Reality

The current layout in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml` renders:

- a compact stage strip at the top,
- but also a long always-scrollable stack of stage cards underneath,
- with Source Intake, Validation, Preflight, Extraction Review, and Output Stage Preview all competing for space.

That means the pane already has the right pieces, but not the right hierarchy.

### Scope Intent

This story is a **layout and workflow focus refactor** for the dockpane shell.

It should:

- make the active stage visually dominant,
- keep upstream/downstream context compact,
- preserve existing session-driven gating,
- and create a better host for future Validation and Outputs work.

It should **not**:

- change validation rule logic,
- add new processing adapters,
- add embedded PDF/TIFF/image rendering,
- add output generation behavior,
- or rewrite the workflow state machine.

### UX Direction To Preserve

Use the existing design system in `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`:

- compact ArcGIS Pro-adjacent density,
- small desktop typography,
- restrained use of severity color,
- no decorative card-heavy presentation,
- clear step states and strong operational posture.

This story should align especially with:

- compact workflow navigation,
- one primary work surface at a time,
- visible state clarity for blocked vs active vs done.

### Recommended Layout Direction

Recommended implementation direction:

1. Keep the header and stage rail compact at the top.
2. Use a single active-stage workspace region beneath it.
3. Render non-active stages as short summary strips/cards.
4. Let the active stage own most of the pane height.
5. Keep the footer action bar and status strip stable.

This can be implemented with WPF visibility switching, content presenters, or a stage-to-template mapping pattern. Follow the repo’s current MVVM/WPF approach instead of introducing a new UI framework pattern.

### Code Areas That Matter

Read and preserve behavior in:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`

Preserve:

- stage sequencing,
- command enablement,
- reopen/resume behavior,
- state-driven badges/text,
- and current case-folder-backed workflow progression.

### Story Dependencies

- Story 2.14A provides the current source-first Extraction Review workspace and must not be regressed.
- Story 4.1 provides the current Validation state and gating behavior and must remain authoritative.
- Story 4.2 can build on this redesign for richer grouped validation findings after the workspace shell is improved.
- Epic 5 output stories should inherit this stage-focused shell rather than reintroducing stacked-pane sprawl.

### Testing Guidance

Favor tests around stage projection logic and state-driven visibility decisions rather than brittle view-only assertions where ArcGIS framework hosting is awkward.

At minimum verify:

- which stage is active for each `WorkflowState`,
- whether later stages stay gated when blocked,
- whether reopen/resume restores the expected active stage,
- whether active-stage selection does not break existing commands.

### Known Environment Caveat

The current validation environment may still show adapter configuration issues until local settings are corrected. That is not the purpose of this story. The UI redesign should tolerate blocked validation states and present them clearly, not attempt to solve validation adapter wiring here.

### References

- `_bmad-output/planning-artifacts/epics.md` (Epic 4, Story 4.2 baseline intent, workflow state appendix)
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-review-before-output.html`
- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`
- `_bmad-output/implementation-artifacts/4-1-run-validation-on-approved-review-data.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`
- `powershell -ExecutionPolicy Bypass -File tools\package_addin.ps1`

### Completion Notes List

- Reworked the Parcel Workflow dockpane into a stage-focused workspace with one dominant active stage and compact summaries for completed or downstream stages.
- Added deterministic active-stage planning through `WorkflowWorkspacePlanner` so the UI follows workflow state instead of inventing separate navigation state.
- Preserved existing preflight, review, validation, and output gating while reducing the vertical stacked-card layout.
- Added focused tests for intake, preflight, extraction review, validation, and output stage selection logic.
- Built the solution successfully, ran the full local .NET test executable successfully, and packaged the ArcGIS Pro add-in.

### File List

- `_bmad-output/implementation-artifacts/4-2a-redesign-parcel-workflow-into-stage-focused-workspace.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowWorkspacePlanner.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowWorkspacePlannerTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for stage-focused Parcel Workflow redesign. | Codex |
| 2026-06-12 | 0.2 | Implemented active-stage workspace layout, compact summary cards, workflow stage planner, and focused tests. | Codex |
