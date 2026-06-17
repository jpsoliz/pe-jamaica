---
baseline_commit: handoff-2026-06-16
---

# Story 5.13: Build Dev Spike For Jamaica COGO-Style Review Workspace Shell

Status: review

## Story

As the Sidwell Co product delivery team,  
I want a development spike that builds the shell of the Jamaica-specific COGO-style review workspace,  
so that we can validate ArcGIS Pro hosting, workspace sizing, source-viewer integration, center-table behavior, and parcel-preview layout before full production implementation.

## Story Requirements

### User Story Statement

Before investing in the full production review workspace, the team needs a spike that proves the add-in can host a larger three-region review surface and connect it to existing transaction/review artifacts without destabilizing the current workflow.

### Acceptance Criteria

1. Given the proposed review workspace is larger and denser than the current extraction review pane, when the spike is implemented, then it creates a new experimental workspace shell inside ArcGIS Pro using the preferred host container identified by the UX/architecture work.
2. Given the workspace should not yet replace the production flow, when the spike is launched, then it is clearly marked as experimental and does not break the existing extraction review and map review workflow.
3. Given the shell needs to prove layout feasibility, when the spike opens, then it contains:
   - a source-viewer region,
   - a center extracted-results region,
   - a parcel interpretation / parcel preview region,
   - and status/action controls sufficient for usability testing.
4. Given the current system already produces `extraction_review_data.json` and source copies, when the spike is wired, then it can load an existing transaction case and bind the shell to real case artifacts without requiring final spatial outputs.
5. Given source documents may be PDF, TIFF, PNG, JPG, TXT, CSV, or other supported file types, when the spike is tested, then it demonstrates the intended primary rendering path and fallback behavior for at least one document path and one non-rendered fallback path.
6. Given the table is the examiner’s main working dataset, when the spike is tested, then it proves that the key review columns, parcel selection, and row selection can be shown in the proposed layout without unacceptable clipping or unusable density.
7. Given a parcel/line preview is part of the design direction, when the spike is built, then it includes at least a placeholder or lightweight generated preview region that can later be backed by real parcel interpretation logic.
8. Given the production architecture defines authoritative artifacts and service seams, when the spike is implemented, then it reuses those seams where practical and clearly labels any temporary/mock data or simplified adapter logic.
9. Given the workspace is intended to hand off into Parcel Fabric-backed map review later, when the spike is complete, then it clearly documents what remains mock/simulated versus what already uses live transaction data.
10. Given this is a spike, when implementation is complete, then the story records what worked, what did not fit inside ArcGIS Pro, what should remain provisional, and whether the team should proceed with full production implementation.

## Tasks / Subtasks

- [x] Create the experimental workspace shell. (AC: 1-3)
  - [x] Add an experimental view/pane/window entry point.
  - [x] Mark it clearly as experimental/spike.
  - [x] Create the three-region layout plus actions/status area.
  - [x] Confirm whether the preferred host is a large floating pane or dedicated window and implement the spike in that host.

- [x] Bind the shell to existing case artifacts. (AC: 4, 8-9)
  - [x] Load current case source files.
  - [x] Load `extraction_review_data.json` when present.
  - [x] Bind the shell to real transaction/review data where practical.
  - [x] Clearly mark any placeholder or simulated data path used by the spike.

- [x] Prove viewer and table feasibility. (AC: 5-6)
  - [x] Demonstrate the primary source-viewer path.
  - [x] Demonstrate fallback handling for non-rendered file types.
  - [x] Demonstrate key extracted-row columns and parcel switching.
  - [x] Validate that buttons, scrolling, and focus behavior remain usable at target ArcGIS Pro sizes.

- [x] Add a lightweight parcel preview region. (AC: 7-9)
  - [x] Add a placeholder or generated parcel/line preview panel.
  - [x] Bind it to currently selected parcel/group or sample review data.
  - [x] Show enough parcel context to validate parcel switching and row-to-preview synchronization.

- [x] Record spike findings. (AC: 8-10)
  - [x] Capture what fit well in ArcGIS Pro.
  - [x] Capture what needs a dedicated floating window vs dock pane.
  - [x] Capture any blockers before full implementation.
  - [x] Capture which parts are safe to promote into production implementation and which must remain provisional.

## Dev Notes

### Why This Story Exists

- The proposed review workspace is materially more ambitious than the current review pane.
- ArcGIS Pro UI constraints are real, and we should validate them before a full implementation commitment.
- A spike lets us test layout and interaction viability with real transaction artifacts.

### Spike Boundaries

This is intentionally not the final feature.

The spike should:

- prove layout and hosting
- prove data binding against existing artifacts
- prove viewer/table/preview composition
- avoid replacing the current production workflow

The spike should not:

- rewrite the full extraction review logic
- become the only supported review experience yet
- require final Parcel Fabric output to be present
- lock in final production contracts that the architecture story still leaves provisional

### Required Questions The Spike Must Answer

The spike should leave the team with confident answers to these:

1. Can ArcGIS Pro comfortably host the preferred review container size?
2. Is the source viewer usable enough inside that host, or does PDF/image behavior still force an alternate approach?
3. Can parcel switching, row review, and parcel preview coexist without the workspace feeling cramped?
4. Are the current case artifacts sufficient to drive the shell, or do we need new review-workspace projection contracts?
5. Which parts of the shell are ready to become production implementation stories immediately afterward?

### Suggested Technical Bias

Preferred spike characteristics:

- large floating review surface if the UX story recommends it
- reuse of current review artifact loading where possible
- minimal new contracts
- honest placeholder behavior where production logic is not ready yet
- deliberate notes where the spike bends the architecture for speed

### Success Criteria

The spike is successful if it answers:

1. Can ArcGIS Pro comfortably host this workspace?
2. Can the source viewer, result table, and parcel preview coexist without breaking usability?
3. Can real transaction artifacts drive the shell without an unstable pile of one-off wiring?
4. Should the team proceed to full implementation?

### Expected Deliverables

At minimum the spike should leave behind:

- an experimental launch path in the add-in
- a testable shell bound to at least one real transaction case
- a short spike findings note
- explicit go / refine / stop recommendation for the next implementation step

## References

- `_bmad-output/implementation-artifacts/2-14a-redesign-extraction-review-workspace-around-source-document-verification.md`
- `_bmad-output/implementation-artifacts/5-11-design-jamaica-cogo-style-review-workspace.md`
- `_bmad-output/implementation-artifacts/5-12-define-architecture-for-jamaica-cogo-style-review-workspace.md`
- `_bmad-output/implementation-artifacts/5-10-evaluate-supported-arcgis-pro-automation-boundary-for-cogo-reader-assist-vs-custom-transaction-controlled-extraction-flow.md`

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln`
- `_bmad-output/planning-artifacts/research/jamaica-review-workspace-spike-findings-2026-06-16.md`

### Completion Notes List

- Added an experimental `Open workspace` launch path in the Extraction Review stage without replacing the existing dock-pane workflow.
- Implemented a large floating `ProWindow` shell for the Jamaica review workspace spike.
- Reused live review artifacts from the current dock-pane/session state instead of inventing a disconnected mock-only data source.
- Reused existing embedded viewer logic for PDF/image rendering and explicit fallback messaging for unsupported source types.
- Added parcel-group projection and a lightweight generated parcel preview for parcel switching / row-to-preview synchronization.
- Build passed successfully.
- Manual ArcGIS Pro host validation is still needed for PDF behavior, scrolling, and examiner usability at runtime.

### File List

- `_bmad-output/implementation-artifacts/5-13-build-dev-spike-for-jamaica-cogo-style-review-workspace-shell.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/research/jamaica-review-workspace-spike-findings-2026-06-16.md`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewRowViewModel.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-16 | 0.1 | Initial development spike story for the Jamaica-specific COGO-style review workspace shell. | Codex |
| 2026-06-16 | 0.2 | Refined the spike story with clearer scope control, success criteria, artifact reuse expectations, and required decision outcomes before production implementation. | Codex |
| 2026-06-16 | 1.0 | Implemented the experimental floating review-workspace shell, live case-artifact binding, parcel-group projection, provisional parcel preview, and recorded spike findings. | Codex |
