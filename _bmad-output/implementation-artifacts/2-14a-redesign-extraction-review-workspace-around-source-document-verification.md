---
baseline_commit: handoff-2026-06-12
---

# Story 2.14A: Redesign Extraction Review Workspace Around Source Document Verification

Status: review

## Story

As a cadastral technical staff user,
I want the Parcel Workflow review experience to center on the source point document and a compact editable point table,
so that I can verify extracted points against the actual PDF/TIF/image evidence without losing time inside oversized preflight lists or secondary output placeholders.

## Acceptance Criteria

1. Given preflight has produced a long list of checks, when the user views the Preflight section, then the pane shows a compact summary by default with explicit `Show details` / `Hide details` behavior instead of always rendering the full list.
2. Given preflight results include blockers, warnings, and passed checks, when the section is collapsed, then counts remain visible and blockers stay discoverable without requiring the user to scroll through all passed items.
3. Given extraction review data exists and the transaction includes a point-bearing source document in PDF, TIF, TIFF, PNG, or JPG format, when the user opens Extraction Review, then the UI presents a source-document verification workspace rather than only a generic artifact editor.
4. Given the extraction review workspace is open, when the user reviews extracted rows, then the primary editable table is limited to the compact business columns: point, easting, northing, status, and length when applicable.
5. Given additional row metadata such as source evidence, unresolved flags, provenance, and notes still exists, when the user selects or edits a row, then those details remain accessible without overloading the main table columns.
6. Given the user needs to compare extracted points against the source file, when the source viewer is shown, then the UI keeps the source document and point table visually paired in the same review experience, whether as a split view, dedicated review window, or equivalent source-first layout.
7. Given the user saves review changes from the redesigned workspace, when save completes, then `working/extraction_review_data.json` is updated in place and continues to preserve original extracted values plus human-reviewed values.
8. Given Output Package Preview is not yet the user’s primary focus in this stage, when the redesigned pane is displayed, then output preview is visually de-emphasized or collapsed so review/edit work remains dominant.
9. Given this story is complete, then the redesign does not yet generate `.gdb` outputs, validation summaries, or final output packages; it only improves preflight presentation and review UX around source verification.
10. Given the story is complete, then focused tests cover preflight expand/collapse state, review workspace state loading, compact point-column projection, save persistence, and no regression to approval gating introduced in Story 2.13.

## Tasks / Subtasks

- [x] Compress and gate the Preflight presentation. (AC: 1, 2, 10)
  - [x] Add collapsed/expanded state for the Preflight section in the Parcel Workflow pane.
  - [x] Show summary counts for blockers, warnings, and passed checks when collapsed.
  - [x] Keep blocker visibility obvious even when details are collapsed.
- [x] Redesign Extraction Review as a source-first verification workspace. (AC: 3, 6, 8)
  - [x] Choose a practical ArcGIS Pro/WPF layout pattern such as split view, dedicated review panel, or auxiliary review window.
  - [x] Keep source document viewing and point editing in the same review experience.
  - [x] De-emphasize Output Package Preview at this stage.
- [x] Reduce the main point table to compact business columns. (AC: 4, 5, 10)
  - [x] Keep the main grid focused on point, easting, northing, status, and length when applicable.
  - [x] Move unresolved reason, provenance, review notes, evidence, and other secondary fields into a side detail panel, row expander, or equivalent compact secondary surface.
  - [x] Preserve manual point support and edited/unresolved visual cues from Story 2.13.
- [x] Add source-document verification behavior. (AC: 3, 6)
  - [x] Resolve which transaction source file should drive review for point verification.
  - [x] Prefer the computation / point-bearing source document for review when multiple transaction files exist.
  - [x] Keep fallback behavior clear when no reviewable document viewer is available for a given file type.
- [x] Preserve review save and approval semantics. (AC: 7, 9, 10)
  - [x] Ensure the redesigned workspace still saves back into `working/extraction_review_data.json`.
  - [x] Ensure Story 2.13 approval gating, unresolved blocking, and stale-approval invalidation remain unchanged.
  - [x] Do not introduce validation or output generation into this story.
- [x] Update status messaging and UX hierarchy. (AC: 1-9)
  - [x] Clarify that Preflight is a compact readiness summary.
  - [x] Clarify that Extraction Review is the main working area.
  - [x] Keep the UI aligned with the review mockup direction while adapting to ArcGIS Pro dock pane constraints.
- [x] Add focused tests. (AC: 10)
  - [x] Preflight expand/collapse state behaves deterministically in implementation; direct dockpane-only unit coverage remains a known ArcGIS harness gap.
  - [x] Review workspace loads when extraction review data exists.
  - [x] Compact point-column projection does not drop persisted review data.
  - [x] Saving from the redesigned review workspace still updates `working/extraction_review_data.json`.
  - [x] Approval gating still blocks unresolved/missing-value rows.
- [x] Validate and package. (AC: 1-10)
  - [x] Run `tools\validate_contracts.ps1`.
  - [x] Run `tools\run_python_tests.ps1`.
  - [x] Run `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`.
  - [x] Run `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`.
  - [x] Run `tools\package_addin.ps1`.

## Dev Notes

### Why This Story Exists

- Story 2.13 established working in-pane review persistence and approval behavior.
- Real review work is still too cramped because Preflight can dominate the pane and the current point table carries more columns than the reviewer needs during verification.
- The user has clarified that the core task is reviewing extracted points against the actual point-bearing source document, not primarily browsing raw artifacts.

### Current UX Direction To Preserve

- Preflight remains part of the workflow, but should feel like a compact checklist/report area.
- Extraction Review remains the dominant task surface.
- Save must continue updating `working/extraction_review_data.json`.
- Approval must still depend on the same review rules from Story 2.13.

### Primary UI Intent

The pane should visibly separate two jobs:

1. `Preflight`: read-only readiness and issue summary
2. `Extraction Review`: active human verification and correction of extracted points

The redesign should favor point verification over artifact browsing.

### Extraction Review Layout Guidance

Recommended direction:

- source document viewer on one side
- compact point table on the other side
- secondary details shown only for the selected row or via a compact detail surface

Avoid a wide all-fields table as the primary interaction pattern.

### Table Column Guidance

Main table should focus on:

- point
- easting
- northing
- status
- length when applicable

Secondary details such as source evidence, unresolved reason, notes, and provenance should remain available but not occupy primary table width.

### Source File Guidance

- Use the transaction-provided source files already copied into the Case Folder `source/` directory.
- Prefer the point-bearing computation or extracted-point document for verification.
- The review experience should support point-source documents in PDF, TIF, TIFF, PNG, and JPG forms.
- If an embedded viewer is not practical for a specific file type in this story, provide the cleanest ArcGIS Pro-compatible fallback without regressing the source-first UX intent.

### Output Preview Guidance

`Output Package Preview` is still a future-stage concern and should remain present only as a lightweight secondary section or collapsed placeholder. It must not visually compete with the review workspace in this story.

### Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

### Scope Boundaries

- Do not implement geometry generation or `.gdb` creation here.
- Do not implement validation execution or output packaging here.
- Do not change the underlying review approval contract from Story 2.13.
- Do not replace the Case Folder as the persisted source of truth.

### References

- `_bmad-output/implementation-artifacts/2-13-review-edit-point-workflow.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/mockups/dock-pane-review-before-output.html`
- `c:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000206\working\extraction_review_data.json`
- `c:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000206\working\preflight_summary.json`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`
- `powershell -ExecutionPolicy Bypass -File tools\validate_contracts.ps1`
- `powershell -ExecutionPolicy Bypass -File tools\run_python_tests.ps1`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --no-restore`
- `powershell -ExecutionPolicy Bypass -File tools\package_addin.ps1`

### Completion Notes List

- Reworked the Parcel Workflow dockpane so Preflight defaults to a compact summary with explicit detail toggles and visible blocker/warning/pass counts.
- Redesigned Extraction Review into a source-first split workspace with source document actions on one side and a compact point grid on the other.
- Tightened the review footer copy and layout so `Save review` and `Approve review` remain visible in constrained ArcGIS Pro dockpane widths.
- Stabilized the embedded source viewer pane with a fixed split-workspace viewport so PDF/image verification behaves less like a floating inline element during dockpane scrolling.
- Reduced the editable review table to business columns only: point, easting, northing, status, and length.
- Moved row-level evidence and context into a selected-row detail panel instead of keeping them in the main grid.
- Added review-source resolution that prefers the point-bearing computation document and falls back cleanly to other copied source files.
- Extended extraction review persistence to round-trip `length` values and preserve original vs reviewed values in `working/extraction_review_data.json`.
- Revalidated approval/persistence behavior with the existing workflow and persistence test suites.
- Attempted a direct dockpane unit test for expand/collapse behavior, but the plain test runner cannot load ArcGIS framework assemblies; this remains a known harness limitation rather than a compile/runtime issue in the add-in.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Properties/AssemblyInfo.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-12 | 0.1 | Initial story for redesigning extraction review around source-document verification and compact preflight presentation. | Codex |
| 2026-06-12 | 0.2 | Implemented compact preflight UX, source-first extraction review workspace, compact review grid, persistence updates, and validation/package pass. | Codex |
| 2026-06-15 | 0.3 | Polished extraction review footer messaging/button visibility and stabilized the embedded source viewer viewport after dockpane usability testing. | Codex |
