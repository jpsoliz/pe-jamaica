---
baseline_commit: handoff-2026-06-17
---

# Story 5.16A: Realign Compute Workflow Vocabulary Around Data Extraction And Points/Lines Validation

Status: review

## Story

As a cadastral examiner using `Parcel Workflow [Compute]`,  
I want the workflow stages and tool names to match the real operational sequence,  
so that the shell describes extraction, validation, spatial creation, and final review in plain business language rather than legacy internal labels.

## Acceptance Criteria

1. Given the compute workflow shell is displayed, when stage labels, stage chips, active-stage text, section headers, status messages, and footer text are shown, then the user-facing workflow stages read:
   - `Attachments`
   - `Data Extraction`
   - `Validate Points and Lines`
   - `Create Spatial Units`
   - `Final Review`
   - `Finalize`
2. Given the current shell still contains legacy labels, when wording is updated, then the following older labels are replaced where the meaning has changed:
   - `Files Checks` -> `Data Extraction`
   - `Point Review` -> `Validate Points and Lines`
   - `Quality Check` -> `Final Review` only if that stage is truly the examiner decision stage
   - `Create Spatial Outputs` -> `Create Spatial Units`
   - `Map Review` -> merged into `Final Review` only if map-based approval is part of that step
3. Given the detailed parcel review window is displayed, when its title or workflow references appear, then the user-facing name is `Points and Lines Validation Tool` or equivalent compact wording that makes clear both parcel points and parcel lines are reviewed before spatial unit creation.
4. Given the shell describes the purpose of each step, when longer guidance text is shown, then the operational meaning is clear:
   - `Attachments` = load transaction source files from Innola
   - `Data Extraction` = derive candidate point/parcel data from source documents
   - `Validate Points and Lines` = review and correct extracted parcel points, parcel lines, and proposed parcel construction data in the dedicated validation tool
   - `Create Spatial Units` = create parcel fabric or configured spatial geometry from validated points and lines
   - `Final Review` = examiner map review that marks the case ready for `Finalize`; rejection and postponement belong to the general workflow process, not this Compute closeout path
   - `Finalize` = commit or close out the workflow result back to Innola
5. Given the shell still refers to `Jamaica COGO Tool` or `Points Validation Tool`, when the new naming is approved for this process, then user-facing references in the compute workflow path are renamed to `Points and Lines Validation Tool` or compact `Validate Points and Lines` wording unless they intentionally describe older story/history context.
6. Given warnings, banners, confirmation dialogs, and footer messages are shown, when they mention legacy stage names, then they are updated to the approved vocabulary where the process meaning has changed.
7. Given this story is complete, then the compute workflow reads as one consistent operator journey rather than mixing extraction-era labels, tool names, and downstream spatial-review terms.

## Tasks / Subtasks

- [x] Align stage vocabulary in the compute workflow shell. (AC: 1-2, 6-7)
  - [x] Update lifecycle buttons and active-stage labels.
  - [x] Update section headers, status chips, and helper text.
  - [x] Update warning banners and footer/status copy.

- [x] Rename the dedicated review tool in user-facing compute workflow copy. (AC: 3, 5)
  - [x] Change the window title and tool references to `Points Validation Tool`.
  - [x] Remove or de-emphasize `Jamaica COGO Tool` references in the production compute flow.
  - [x] Preserve code/history identifiers only where needed for technical continuity.
  - [ ] Product alignment patch: update user-facing workflow copy from point-only wording to `Validate Points and Lines` and `Points and Lines Validation Tool` where space allows.

- [x] Reframe step descriptions around the revised process. (AC: 4, 7)
  - [x] Keep descriptions short, operational, and business-friendly.
  - [x] Ensure `Create Spatial Units` clearly follows saved point validation.
  - [ ] Product alignment patch: ensure `Create Spatial Units` clearly follows saved point and line validation.
  - [x] Ensure `Final Review` is described as the examiner decision stage.

## Dev Notes

### Why This Story Exists

- The current workflow language still mixes older step names with newer product direction.
- The dedicated review tool has become a real product step, so its name should reflect function rather than prototype/history terminology.
- The examiner’s mental model should be: supporting documents -> structure/georeference/dimension checks -> points and lines validation -> spatial creation -> final review -> finalize.

### Product Alignment Update - 2026-07-03

The latest compute workflow notes in `docs/project/compute-steps.docx` expand the validation stage from point-only review to point-and-line review. Product language should move toward:

- Stage label: `Validate Points and Lines`
- Tool/window label: `Points and Lines Validation Tool`
- Meaning: human/assisted review of extracted points, extracted/proposed lines, and proposed parcel construction data before parcel polygon/spatial unit construction.

Historical code identifiers may remain point-oriented until a dev story changes them safely, but new user-facing copy should avoid implying that only points are reviewed.

### Process Recommendation

- Keep `Save` in the validation tool separate from spatial generation.
- Treat `Create Spatial Units` as the explicit downstream stage that consumes validated point and line data.
- Only collapse `Map Review` into `Final Review` if the product decision is that map-based approval and examiner disposition now live in the same step.

### Scope Boundaries

- This story is a naming, vocabulary, and process-alignment story.
- This story does not implement the Save button behavior inside the validation tool.
- This story does not itself create parcel fabric or other spatial units.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`

## References

- `_bmad-output/implementation-artifacts/5-16-align-compute-workflow-stage-copy-and-jamaica-cogo-handoff.md`
- `_bmad-output/implementation-artifacts/5-15-parcel-scoped-manual-point-editing-and-live-parcel-preview-controls-in-jamaica-cogo-tool.md`
- `_bmad-output/implementation-artifacts/5-18-route-manual-review-branch-into-configured-gdb-map-editing-path.md`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial alignment story for revised compute-stage vocabulary and renaming Jamaica COGO Tool to Points Validation Tool in the user-facing process. | Codex |
| 2026-06-17 | 1.0 | Implemented compute-workflow vocabulary realignment, renamed the review tool to Points Validation Tool, and updated related tests and helper copy. | Codex |
| 2026-07-03 | 1.1 | Patched vocabulary target to expand Validate Points into Validate Points and Lines based on compute workflow notes. | Mary / Codex |

## Dev Agent Record

### Debug Log

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false`
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false /p:UseSharedCompilation=false`
- `dotnet run --no-build --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` (fails in unrelated `InnolaTransactionLoadServiceTests.ResumePackageRestoresSavedWorkflowState`)

### Completion Notes

- Realigned the compute shell to show the operator-facing six-step journey: `Attachments`, `Data Extraction`, `Validate Points`, `Create Spatial Units`, `Final Review`, and `Finalize`.
- Updated workflow state display names, session status messages, section headers, helper copy, and extraction decision guidance to use the new vocabulary consistently.
- Renamed the detailed review experience from `Jamaica COGO Tool` to `Points Validation Tool` in the live workflow and workspace window.
- Kept the implementation scoped to user-facing language only; workflow mechanics and state enums were left intact for follow-up stories.
- Full test harness still reports an unrelated failure in `InnolaTransactionLoadServiceTests.ResumePackageRestoresSavedWorkflowState`; project builds succeeded for the add-in and tests.
- Product alignment patch added the future wording target `Validate Points and Lines`; implementation may need a follow-up dev patch because current UI/tests still use `Points Validation Tool`.
- Current target wording is `Validate Points and Lines` / `Points and Lines Validation Tool`; any remaining `Validate Points` or `Points Validation Tool` references in live UI/tests are legacy implementation debt, not the final product language.

## File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionDecisionGateService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightRuleCatalogLoader.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/PreflightRules.json`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowStateExtensionsTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`
