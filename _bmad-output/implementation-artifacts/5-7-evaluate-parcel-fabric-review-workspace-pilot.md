---
baseline_commit: handoff-2026-06-14
---

# Story 5.7: Evaluate Parcel Fabric Review Workspace Pilot For Cadastral Examiners

Status: ready-for-review

## Story

As a cadastral examination product and delivery team for NLA Jamaica,  
I want to evaluate a Parcel Fabric-backed review workspace as a pilot option for cadastral examiners,  
so that we can determine whether parcel-aware topology and editing tools materially improve examiner productivity over the regular transaction `.gdb` review workspace without prematurely forcing Parcel Fabric as the default model.

## Acceptance Criteria

1. Given the current workflow already produces a transaction-local `.gdb`, when the pilot story is executed, then the team documents a side-by-side comparison between regular feature-class review and a Parcel Fabric-backed review workspace for plan examination use.
2. Given local configuration controls output behavior, when the Configuration panel is opened, then it shows a new review/output workspace setting with user-facing values equivalent to `Normal` and `Parcel Fabric`, where `Normal` maps to the already implemented standard transaction `.gdb` output path.
3. Given the new output workspace setting is read from local configuration, when output generation runs, then the workflow can deterministically choose between:
   - standard feature-class output in the transaction `.gdb`, or
   - Parcel Fabric pilot output in the transaction `.gdb` review workspace.
4. Given Parcel Fabric mode is selected for the pilot, when output generation succeeds, then the transaction-local `.gdb` contains the pilot parcel review content needed for examiner review, including parcel points, parcel lines, and parcel polygons in a Parcel Fabric-aware review context.
5. Given Parcel Fabric pilot output is created, when the add-in loads the spatial review context into ArcGIS Pro, then it adds the relevant review layers to the active map, enables the expected ArcGIS Pro parcel editing/tooling workflow, and zooms to the created parcel context for review.
6. Given cadastral examiners may need topology-aware editing and manual COGO correction, when the pilot is assessed, then the evaluation explicitly considers snapping, COGO entry, visibility of parcel structure, topology help, and usability for poor-quality source plans.
7. Given lineage/history is out of scope for the current examination-stage app, when the pilot recommendation is made, then the evaluation distinguishes between Parcel Fabric as an editing accelerator versus Parcel Fabric as the system-of-record model.
8. Given this app is part of a larger cadastral ecosystem, when the pilot is evaluated, then the outcome explains how approved local geometry would later sync into enterprise regardless of whether the review workspace is plain `.gdb` or Parcel Fabric-backed.
9. Given a pilot implementation is feasible, when the story is developed, then it produces either:
   - a constrained prototype/spike showing how transaction outputs are loaded into a Parcel Fabric review context, or
   - a documented reason why a paper architecture evaluation is the safer first step.
10. Given the pilot is complete, when the recommendation is delivered, then it clearly states one of:
   - keep regular `.gdb` as the default review workspace,
   - adopt Parcel Fabric as an optional review mode,
   - or promote Parcel Fabric to the default review workspace for examination transactions.
11. Given this story is complete, then the result includes concrete next-step implications for stories, configuration, licensing/permissions assumptions, and sync mapping expectations.

## Tasks / Subtasks

- [x] Define the configurable review/output workspace mode. (AC: 2-3, 11)
  - [x] Add a local configuration setting with clear user-facing values for standard output mode and Parcel Fabric pilot mode.
  - [x] Define the internal setting names and safe default behavior so existing environments continue using the current standard transaction `.gdb` flow.
  - [x] Extend the Configuration panel summary to show the selected review/output workspace mode.

- [x] Capture the evaluation criteria. (AC: 1, 4-8, 10-11)
  - [x] Define the examiner tasks to compare: visual review, point/line correction, manual COGO entry, topology checking, parcel completion, and handoff to sync.
  - [x] Define comparison dimensions: implementation effort, examiner productivity, topology support, ArcGIS Pro tool leverage, operational complexity, and sync impact.

- [x] Assess current regular `.gdb` review path. (AC: 1-3, 8)
  - [x] Document strengths of plain transaction feature classes for the current workflow.
  - [x] Document limitations for cadastral review, especially around topology and parcel-oriented editing assistance.

- [x] Assess Parcel Fabric review workspace feasibility. (AC: 1, 3-9)
  - [x] Review ArcGIS Pro Parcel Fabric requirements relevant to a local or pilot review workspace.
  - [x] Identify required setup decisions: parcel types, records, topology/build behavior, local vs enterprise assumptions, and editing APIs/tooling.
  - [x] Define the minimum pilot dataset and structure to place parcel points, lines, and polygons into a Parcel Fabric-aware review context inside the transaction `.gdb`.
  - [x] Define how the add-in should load the pilot review layers into ArcGIS Pro and zoom to the work area once generation completes.
  - [x] If practical, implement a constrained spike/prototype path; otherwise document why a pure evaluation is preferred now.

- [x] Produce a recommendation and decision note. (AC: 7-11)
  - [x] State whether Parcel Fabric should remain future-facing, become optional, or become default for review.
  - [x] State impact on the existing output contract and enterprise sync path.
  - [x] State the next implementation stories implied by the recommendation.

## Dev Notes

### Why This Story Exists

- The workflow now reaches a point where human spatial review and manual COGO correction may be required after extraction and validation.
- Parcel Fabric may provide a materially better examiner editing experience, but it also brings heavier ArcGIS-specific structure and operational complexity.
- The team needs an informed decision, not a guess.

### Architectural Position

Current recommendation entering this story:

- keep the app’s stable output contract in a regular transaction `.gdb`
- evaluate Parcel Fabric as a review-workspace enhancement or pilot
- do not make Parcel Fabric the default system model until examiner benefit and sync impact are clearer
- use a configuration-controlled output/review workspace mode so the pilot can be enabled without breaking the current `Normal` path

This story exists to validate or overturn that recommendation with evidence.

### Output Mode Guidance

Recommended user-facing setting label:

- `Review Workspace`

Recommended initial values:

- `Normal` = current implemented transaction `.gdb` output and review flow
- `Parcel Fabric` = pilot review workspace path using Parcel Fabric-aware parcel review content

If preferred later, the user-facing label can be renamed to something calmer like:

- `Output Workspace`
- `Review Output Mode`
- `Spatial Review Mode`

For now, the key point is that `Normal` remains the safe default and the Parcel Fabric path is explicitly opt-in.

### Pilot Expected Behavior

If the Parcel Fabric pilot path is implemented in code, the expected end-to-end behavior should be:

1. output generation completes successfully for the transaction
2. the transaction-local `.gdb` is prepared for Parcel Fabric-based review
3. parcel points, lines, and polygons are represented in the Parcel Fabric-aware review context
4. the add-in loads the relevant review layers into the active ArcGIS Pro map
5. ArcGIS Pro zooms to the parcel context
6. the examiner uses native parcel editing, snapping, and COGO-capable tools for review

This story does not require enterprise Parcel Fabric adoption. It evaluates whether a local review-oriented Parcel Fabric workspace improves the examiner experience enough to justify the added complexity.

### Scope Boundaries

- This story may be a spike/evaluation rather than a production feature-complete implementation.
- It should not silently refactor the whole workflow into Parcel Fabric without an explicit recommendation outcome.
- It should stay anchored to NLA Jamaica examination use cases rather than generic cadastral theory.

### References

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/research/technical-arcgis-pro-addin-parcel-workflow-research-2026-06-08.md`
- `_bmad-output/planning-artifacts/research/parcel-fabric-review-workspace-pilot-2026-06-14.md`
- `_bmad-output/implementation-artifacts/4-4-generate-transaction-output-gdb-from-approved-review-data.md`
- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Parcel-Fabric.html`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `python -m unittest discover -s src\ProcessingTools\tests -p test_output_adapter.py`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /nodeReuse:false`
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes

- Added a local `review_workspace_mode` configuration with `Normal` as the safe default and `Parcel Fabric` as an opt-in pilot mode.
- Extended the Configuration window to show the active review workspace mode and any fallback warning when the setting is missing or invalid.
- Updated output execution so the Python output adapter receives the selected review workspace mode.
- Extended output generation to preserve the current standard feature-class outputs while optionally creating a `parcel_fabric_review` pilot dataset inside the transaction `.gdb`.
- Updated output summary metadata and map-loading behavior so Parcel Fabric pilot review layers are loaded and zoomed when that mode is selected.
- Added a written evaluation and recommendation note that keeps `Normal` as the default and recommends `Parcel Fabric` as an optional pilot review mode.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/InnolaTransactionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Execution/WorkflowExecutionSettings.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputAdapterExecutionService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/OutputSummaryDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ConfigurationWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`
- `src/ProcessingTools/adapters/output_adapter.py`
- `src/ProcessingTools/tests/test_output_adapter.py`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Innola/InnolaTransactionSettingsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `_bmad-output/planning-artifacts/research/parcel-fabric-review-workspace-pilot-2026-06-14.md`

### Change Log

- 2026-06-14: Implemented configurable review workspace mode, added Parcel Fabric pilot output dataset generation, updated map-loading/output summaries, and documented the pilot recommendation.
