---
baseline_commit: handoff-2026-06-16
---

# Story 5.9: Add Map Review Editing Toolbar For Spatial Correction Workflows

Status: drafted

## Story

As a cadastral examiner using the Sidwell Co ArcGIS Pro add-in,  
I want a dedicated Map Review editing toolbar to appear during spatial review,  
so that I can quickly access the point, line, snapping, attribute, and correction tools most relevant to transaction review without hunting through the full ArcGIS Pro ribbon.

## Acceptance Criteria

1. Given a transaction reaches the `Map Review` stage, when the examiner enters that stage, then ArcGIS Pro exposes a dedicated review-oriented toolbar or ribbon group for the workflow rather than relying only on the default editing ribbon layout.
2. Given the review toolbar is shown, when the examiner uses it, then the available tools focus on the correction tasks needed for this workflow: point adjustment, point creation/removal, line correction, snapping, attribute editing, selection, and review navigation.
3. Given the toolbar is part of the add-in workflow, when the user is not in `Map Review`, then the toolbar is either hidden, disabled, or clearly de-emphasized so it does not confuse broader transaction stages.
4. Given the toolbar is intended to accelerate review rather than replace ArcGIS Pro editing, when the story is implemented, then the toolbar reuses native ArcGIS Pro editing commands and panes wherever possible instead of recreating geometry editing logic in custom WPF controls.
5. Given point correction is a primary examiner activity, when the toolbar is used, then it includes direct access to the tools needed to inspect, move, add, edit, and delete review points in the active review layers.
6. Given line and parcel correction may also be required, when the toolbar is used, then it includes access to the tools needed to reshape, split, merge, and otherwise correct linework and parcel geometry in the active review context.
7. Given snapping and precision editing are critical in cadastral review, when the toolbar is configured, then it includes the most relevant snapping and editing-environment commands or shortcuts needed for precise map review work.
8. Given the active review workspace may be `normal`, `parcel_fabric`, or enterprise-backed in future stories, when the toolbar is designed, then it remains compatible with all supported review workspace modes and does not hardcode one storage model.
9. Given the examiner must still understand what to do next, when the toolbar is active, then the dock pane and/or status area explains that map edits should be completed with the review toolbar before final approval.
10. Given this story is complete, then focused tests and manual verification cover toolbar visibility, stage gating, command availability, and compatibility with the current `Map Review` workflow.

## Recommended Toolbar Scope

### Design Recommendation

Implement this as a **dedicated ArcGIS Pro ribbon group or contextual add-in tab** associated with the Parcel Workflow review experience.

Recommended label:

- `Map Review Tools`

Recommended behavior:

- visible or enabled only when the active transaction is in `Map Review`
- optimized for examiner tasks, not full GIS authoring
- built primarily from native ArcGIS Pro editing commands, with only light custom workflow buttons where needed

### Recommended Tool Groups

#### 1. Review Navigation

Purpose: help the examiner move quickly around the transaction review extent and select the features to correct.

Recommended tools:

- `Explore`
- `Select`
- `Clear Selection`
- `Zoom To Review Extent`
- `Reload Review Layers`

#### 2. Point Editing

Purpose: support the common case where extracted points must be fixed manually.

Recommended tools:

- `Create Point`
- `Move`
- `Edit Vertices`
- `Delete`
- `Attributes`
- optional add-in shortcut: `Add Manual Point`

#### 3. Line And Parcel Correction

Purpose: support correction of parcel boundaries and related geometry once points are adjusted.

Recommended tools:

- `Reshape`
- `Split`
- `Merge`
- `Modify Features`
- `Attributes`
- optional COGO-capable line tools if available in the chosen workspace mode

#### 4. Precision / Snapping

Purpose: reduce review error and improve examiner efficiency.

Recommended tools:

- `Snapping On/Off`
- `Snapping Settings`
- `Edge / Vertex / End Snapping` controls as appropriate
- any appropriate edit-environment shortcut already available through ArcGIS Pro command reuse

#### 5. Review Workflow Shortcuts

Purpose: connect editing work back to the transaction workflow.

Recommended tools:

- `Back To Workflow`
- `Refresh Map Review Status`
- `Mark Map Review Complete`

These should remain lightweight workflow controls, not geometry editors.

## Tasks / Subtasks

- [ ] Define the Map Review toolbar contract and UX scope. (AC: 1-4, 8-9)
  - [ ] Confirm whether the toolbar should be implemented as a ribbon tab, ribbon group, or contextual visibility model inside the existing add-in shell.
  - [ ] Define the final user-facing label and group names.
  - [ ] Define the enable/disable or show/hide behavior tied to `Map Review`.

- [ ] Map native ArcGIS Pro editing commands to examiner tasks. (AC: 2, 4-7)
  - [ ] Identify the native ArcGIS Pro commands to reuse for review navigation.
  - [ ] Identify the native commands to reuse for point editing.
  - [ ] Identify the native commands to reuse for line/parcel correction.
  - [ ] Identify the snapping commands/settings that should be surfaced for cadastral review.

- [ ] Implement the toolbar/ribbon wiring in the add-in. (AC: 1-5, 7-9)
  - [ ] Add DAML definitions for the toolbar/tab/groups/buttons as needed.
  - [ ] Wire stage-aware visibility or enabled state to the `Map Review` workflow stage.
  - [ ] Add any small custom commands needed for workflow-specific actions such as `Zoom To Review Extent`, `Reload Review Layers`, or `Mark Map Review Complete`.

- [ ] Connect toolbar usage to review workspace context. (AC: 5-8)
  - [ ] Ensure the toolbar works against the current loaded review layers.
  - [ ] Ensure behavior remains compatible with `normal`, `parcel_fabric`, and future enterprise review workspace modes.
  - [ ] Avoid storage-model assumptions in the toolbar command layer.

- [ ] Update dock-pane guidance and labels. (AC: 9)
  - [ ] Add concise Map Review instructions that reference the new toolbar.
  - [ ] Make sure the stage messaging clearly separates map editing from final completion.

- [ ] Add focused tests and manual verification notes. (AC: 10)
  - [ ] Test toolbar availability only during `Map Review`.
  - [ ] Test that non-review stages do not expose confusing editing actions.
  - [ ] Test custom workflow shortcuts such as zoom/reload/mark-complete.
  - [ ] Record manual validation steps for ArcGIS Pro command availability.

## Dev Notes

### Why This Story Exists

- The current workflow reaches `Map Review`, but the examiner still has to find the right ArcGIS Pro tools manually.
- The add-in should reduce that friction by curating the editing tools most relevant to parcel review.
- This is especially important when extracted points are incomplete, misordered, or require manual correction before approval.

### Architectural Direction

- Reuse native ArcGIS Pro commands wherever possible.
- Keep geometry editing in the ArcGIS Pro map, not in the dock pane.
- Use the dock pane for workflow guidance, state, and approval gating.
- Keep the toolbar compatible with both regular feature-class review workspaces and Parcel Fabric-oriented review modes.

### Recommended First Implementation Cut

The safest first cut is a **minimal curated toolbar** with:

- `Explore`
- `Select`
- `Create Point`
- `Move`
- `Edit Vertices`
- `Reshape`
- `Split`
- `Delete`
- `Attributes`
- `Snapping`
- `Zoom To Review Extent`
- `Reload Review Layers`
- `Mark Map Review Complete`

This gives examiners the tools they need most often without trying to mirror the entire Edit ribbon.

### Scope Boundaries

- Do not reimplement ArcGIS Pro editing inside the add-in.
- Do not create a second geometry-editing surface in the dock pane.
- Do not assume Parcel Fabric-only tooling unless a command is explicitly safe across supported review modes.
- Keep this story focused on toolbar access and workflow acceleration, not full spatial editing automation.

### Suggested Files Likely To Extend

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Output/IOutputMapIntegrationService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/*`

### References

- `_bmad-output/implementation-artifacts/5-6-add-spatial-review-stage-for-in-map-editing-and-manual-cogo.md`
- `_bmad-output/implementation-artifacts/5-8-implement-true-local-parcel-fabric-output-mode.md`
- `_bmad-output/planning-artifacts/ux-designs/ux-Sid-jamaica-2026-06-08/DESIGN.md`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/topic10324.html`
- `https://pro.arcgis.com/en/pro-app/3.6/sdk/api-reference/conceptdocs/docs/ProConcepts-Framework.html`

## Questions / Follow-up For Implementation

1. Should the toolbar be a permanent tab, or should it appear only while `Map Review` is active?
2. Do you want Parcel Fabric-specific tools exposed only when the review workspace mode is `parcel_fabric`?
3. Should `Mark Map Review Complete` live on the toolbar, in the dock pane, or in both places for convenience?

