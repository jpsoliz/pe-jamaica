---
baseline_commit: handoff-2026-06-17
---

# Story 4.6: Add Extraction Result Decision Gate For Rerun Vs Manual Review

Status: review

## Story

As a cadastral examiner reviewing extracted point results from transaction PDFs,  
I want the add-in to evaluate whether extraction produced usable results and then guide me to rerun extraction or continue with manual review,  
so that weak automated output does not get confused with file-readiness errors and I can choose the best next step with confidence.

## Acceptance Criteria

1. Given `Files Checks` has already passed, when extraction runs from `Point Review`, then the add-in evaluates the extraction result separately from file-readiness status.
2. Given extraction produces no matches, zero usable rows, unusable grouping, invalid coordinate conditions above threshold, or other configured critical extraction-quality failures, when the result is returned, then the add-in shows a clear result-decision prompt instead of presenting that outcome as a Files Checks failure.
3. Given the result-decision prompt is shown, when the examiner reviews the choices, then the available actions are:
   - `Re-process extraction`
   - `Manual Mode`
   - `Open Jamaica COGO Tool` only when usable extracted review artifacts exist
4. Given `Re-process extraction` is chosen, when the add-in runs extraction again, then the rerun is recorded as a new extraction attempt with timestamp, method, and attempt count in the audit/log trail.
5. Given AI-assisted extraction is enabled, when the examiner reruns extraction, then the system does not promise a better result and the operator guidance explains that rerun results may differ but may still remain insufficient.
6. Given the rerun still produces no usable results or remains below the configured quality threshold, when the add-in returns control to the examiner, then the prompt strongly recommends the manual path rather than trapping the user in repeated weak reruns.
7. Given extraction produces usable review rows, when the decision gate resolves successfully, then `Open Jamaica COGO Tool` becomes the primary next action for point review.
8. Given the examiner chooses Manual Mode, when the decision is confirmed, then the workflow records that extracted review was not approved and opens the Points Validation Tool in an editable manual state without treating the automated result as accepted.
9. Given this story is complete, then the add-in distinguishes:
   - file-readiness failures in `Files Checks`
   - extraction-quality failures in `Point Review`
   - and user routing decisions between rerun, tool-based review, and manual review.

## Tasks / Subtasks

- [x] Define extraction-result quality gates. (AC: 1-2, 5-6, 9)
  - [x] Identify the result conditions that count as unusable extraction output.
  - [x] Separate extraction-quality outcomes from Files Checks outcomes in workflow state and messaging.
  - [x] Define how configurable thresholds or flags are represented.

- [x] Add the decision gate UX in Point Review. (AC: 2-3, 7-8)
  - [x] Show operator-facing guidance when extraction is weak or empty.
  - [x] Present rerun/manual/tool actions with clear labels and no misleading approval language.
  - [x] Keep Jamaica COGO Tool gated to cases with usable review artifacts.

- [x] Track rerun behavior and audit. (AC: 4-6)
  - [x] Record extraction attempt count, timestamps, and route decisions.
  - [x] Preserve the latest active extraction result while keeping audit visibility into prior failed or weak attempts.
  - [x] Escalate guidance toward manual review after repeated weak reruns.

- [x] Add verification coverage. (AC: 9)
  - [x] Cover zero-row extraction.
  - [x] Cover low-quality extraction with artifacts present.
  - [x] Cover rerun audit/state changes.
  - [x] Cover manual-branch selection from the decision gate.

## Dev Notes

### Why This Story Exists

- The current product direction is that `Files Checks` verifies readiness, but extraction quality must be judged after `Point Review` extraction attempt.
- The examiner needs a clean decision point when automated extraction is weak, empty, or not trustworthy.

### Workflow Direction

- `Files Checks` answers: "Can the case be processed?"
- `Point Review` answers: "Did extraction produce something usable?"
- This story adds the decision bridge between weak extraction and the next valid operator action.

### Scope Boundaries

- This story does not implement the downstream manual editing surface itself.
- 2026-07-22 update: the user-facing manual option is now `Manual Mode`. It keeps the examiner in Points Validation Tool for point add/edit/remove and can start from partial extracted rows or a blank editable review artifact after weak/empty extraction.
- This story does not redesign Jamaica COGO Tool internals.
- This story does not finalize GDB/map-edit output generation; it only decides the route into the next review path.

### Suggested Files To Review

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs`

## References

- `_bmad-output/implementation-artifacts/5-16-align-compute-workflow-stage-copy-and-jamaica-cogo-handoff.md`
- `_bmad-output/implementation-artifacts/5-17-add-manual-cogo-fallback-branch-from-point-review.md`
- `_bmad-output/implementation-artifacts/2-16b-add-embedded-pdf-text-first-structured-computation-extraction.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj -m:1 /nodeReuse:false`
- `dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj -m:1 /nodeReuse:false`
- `dotnet run --no-build --project src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj`

### Completion Notes List

- Added a dedicated extraction decision-gate service that evaluates weak vs usable extracted review output and persists attempt state in `working/extraction_decision_gate.json`.
- Point Review now distinguishes weak extraction routing from Files Checks failures, supports rerun guidance, and escalates toward Manual Mode after repeated weak attempts.
- The Point Review card now shows a decision banner and only exposes Jamaica COGO Tool as the next action when extracted review artifacts are usable.
- Manual Mode selection now records the route decision into the extraction decision-gate state and lifecycle audit trail.
- Manual Mode can create a blank editable `extraction_review_data.json` when extraction produced no rows, allowing the examiner to manually add points before save/approval.
- Added workflow-session test coverage for weak extraction routing, rerun attempt tracking, and manual-branch selection.

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionDecisionGateService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-17 | 0.1 | Initial story for a post-extraction decision gate that separates extraction-quality routing from Files Checks and offers rerun vs manual-review choices. | Codex |
| 2026-06-17 | 1.0 | Implemented extraction decision-gate evaluation, rerun tracking, Point Review routing guidance, and workflow-session tests. | Codex |
| 2026-07-22 | 1.1 | Renamed the manual action to Manual Mode and clarified that it opens editable Points Validation Tool review, including blank review creation for weak/empty extraction. | Codex |
