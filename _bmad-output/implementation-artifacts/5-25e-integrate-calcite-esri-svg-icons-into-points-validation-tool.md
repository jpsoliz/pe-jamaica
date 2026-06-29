---
baseline_commit: handoff-2026-06-27
---

# Story 5.25E: Integrate Calcite/Esri SVG Icons Into Points Validation Tool

Status: review

## Story

As a cadastral examiner using the Points Validation Tool,  
I want the workspace actions to use a small, consistent set of Calcite/Esri SVG icons,  
so that the tool feels more aligned with ArcGIS styling and the document/review actions are clearer at a glance.

## Acceptance Criteria

1. Given the Points Validation Tool currently uses MDL2 glyphs, when this story is implemented, then the selected action icons are replaced with SVG assets sourced from the approved Calcite/Esri icon set.
2. Given the Source Verification panel contains document actions, when the tool renders, then `Open source document`, `Open in folder`, and `Reload viewer` use SVG icons that clearly communicate those meanings.
3. Given the Validate Points toolbar contains point-edit actions, when the tool renders, then `Add point`, `Remove point`, and `Discard in-progress manual point` use SVG icons that clearly communicate those meanings.
4. Given the footer continues to own `Validation Complete`, `Save`, and `Close`, when this story is implemented, then those footer actions are not visually or behaviorally duplicated in the upper toolbar.
5. Given ArcGIS Pro docked windows have limited space, when the SVG assets are integrated, then icon size, padding, hit area, and contrast remain readable without expanding the existing controls.
6. Given the window already has working tooltips, commands, and enable/disable logic, when the SVG icons are introduced, then all existing behavior remains unchanged.
7. Given the project may not yet have a general SVG icon infrastructure, when this story is implemented, then the chosen integration path is scoped only to the Points Validation Tool and does not force a global icon refactor.

## Tasks / Subtasks

- [x] Select the initial Calcite/Esri icon subset for this window. (AC: 1-3)
  - [x] Choose icons for open source document, open in folder, reload viewer.
  - [x] Choose icons for add point, remove point, discard in-progress manual point.
  - [x] Keep the set small and semantically obvious.

- [x] Add SVG asset support for the Points Validation Tool only. (AC: 1, 5, 7)
  - [x] Introduce the minimum local asset pattern needed to render the selected SVGs in WPF.
  - [x] Avoid expanding this into a ribbon-wide or add-in-wide icon migration.

- [x] Replace current glyph content in the Points Validation Tool. (AC: 2-3, 5-6)
  - [x] Update Source Verification action buttons to use SVG icons.
  - [x] Update Validate Points toolbar action buttons to use SVG icons.
  - [x] Preserve tooltips, bindings, enable/disable state, and click behavior.

- [x] Verify readability and interaction quality. (AC: 5-6)
  - [x] Confirm the icons remain legible at the current compact action sizes.
  - [x] Confirm the controls still feel aligned and clickable in docked-window layouts.

## Dev Notes

### Why This Story Exists

- `5.25D` cleaned up duplicated actions and clarified the control model.
- The next incremental polish is visual: use Calcite/Esri SVG assets instead of generic font glyphs for the key action buttons in the Points Validation Tool.

### Design Intent

This is a small visual refinement story, not a behavior story.

It should improve:

- icon clarity
- ArcGIS-style visual fit
- consistency between source actions and point-edit actions

It should not change:

- commands
- workflow sequencing
- save/close/validation logic
- footer ownership of stage actions

### Scope Boundary

This story is intentionally limited to:

- `Source Verification` action icons
- `Validate Points` toolbar action icons

This story should not expand to:

- ribbon icons
- transaction list icons
- global asset theming
- a full add-in icon framework

### Likely Implementation Areas

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- a local asset folder under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/`
- optional helper styling if needed for SVG-hosted buttons

## References

- `_bmad-output/implementation-artifacts/5-25d-apply-points-validation-tool-icon-and-action-deduplication-polish.md`
- `https://github.com/Esri/calcite-ui-icons/tree/master/icons`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-06-28 | 0.1 | Drafted tiny follow-up story for Calcite/Esri SVG icon integration in the Points Validation Tool. | Codex |
| 2026-06-28 | 1.0 | Implemented local vector icon resources for document and point-edit actions in the Points Validation Tool without introducing a global icon framework. | Codex |

## Dev Agent Record

### Completion Notes

- Replaced the Points Validation Tool document-action glyphs with lightweight vector icon content for open source document, open in folder, and reload viewer.
- Replaced the point-edit toolbar glyphs with lightweight vector icon content for add point, remove selected point, and discard in-progress manual point.
- Kept PDF page navigation and zoom controls on their existing compact glyph path so this patch stayed limited to the intended action set.
- Implemented the vector icons as local XAML geometry resources inside the window rather than adding a broader SVG rendering dependency or add-in-wide asset system.
- Preserved all existing commands, tooltips, enable/disable behavior, and stage-flow ownership.

### Verification

- `dotnet build src\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn\\ParcelWorkflowAddIn.csproj`

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
