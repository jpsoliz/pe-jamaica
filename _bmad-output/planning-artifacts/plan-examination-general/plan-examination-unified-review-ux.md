# Plan Examination Unified Review UX

Last updated: 2026-09-12
Owner: Sally / UX
Review partners: Mary / Business Analysis, Winston / Architecture, Amelia / Implementation
Status: Draft UX specification for PE/PXA unification

## Purpose

Define the target examiner experience for the unified `PE / Plan Examination` review workspace.

The intent is to preserve the strongest current PXA behavior while expanding it from a single-parcel review model into a multi-parcel Plan Examination model. PXA should not remain a separate user-facing workflow once the unified flow is proven, but its current survey-plan review, memorandum review, metadata review, and geometry correction patterns should be carried forward.

This UX spec supports:

- `_bmad-output/planning-artifacts/plan-examination-general/plan-examination-process-design.md`
- `_bmad-output/planning-artifacts/plan-examination-general/pe-pxa-general-plan-examination-refactor-analysis.md`
- `_bmad-output/implementation-artifacts/8-4-parcel-scoped-geometry-review-option-a.md`

Visual review artifact:

- `_bmad-output/planning-artifacts/plan-examination-general/mockups/plan-examination-unified-review-wireframe.html`
- `_bmad-output/planning-artifacts/plan-examination-general/mockups/plan-examination-process-flow-dockpane.html`
- `_bmad-output/planning-artifacts/plan-examination-general/mockups/plan-examination-points-validation-workspace.html`

## Foundation

Form factor: ArcGIS Pro WPF add-in workspace with the ArcGIS Pro map as the companion spatial surface.

Primary user: plan examiner reviewing survey evidence, correcting extracted parcel geometry, approving generated outputs, and finalizing the transaction.

Core UX principle:

The examiner is reviewing one transaction, but may be correcting many parcels inside that transaction. The interface must keep transaction-wide evidence stable while the active parcel changes.

## Current Behavior To Preserve

Preserve these PXA strengths inside unified Plan Examination:

- Survey-plan-centered review workspace.
- Dense tabs and grids suitable for technical review.
- Document metadata review.
- Owners / neighbors / parties review where available.
- Memorandum evidence and rule visibility.
- Boundary segment review with from point, to point, bearing, distance, sequence, and generation participation.
- Points review with printed/reference, derived, and manually edited rows.
- Rebuild points from reviewed boundary segments.
- Save reviewed data separately from completing validation.
- Require explicit validation approval before downstream spatial output creation.

Preserve these newer PE multi-parcel improvements:

- Parcel selector.
- Parcel-scoped point filtering.
- Parcel-scoped boundary segment filtering.
- Parcel-scoped add/edit/delete/rebuild behavior.
- Empty states that distinguish no extraction from filtered-out-by-parcel.

## Information Architecture

The workspace should read as two stacked review lanes.

### Transaction Review Lane

This lane is document-wide and does not change meaning when the active parcel changes.

Tabs:

- Supporting Documents
- General Information
- Owners / Occupiers / Parties
- Memorandum
- Stage Findings / Diagnostics
- Final Review

Behavior:

- Shows source evidence and transaction-wide metadata.
- Shows document-level extraction findings.
- Shows memorandum and party information once per transaction.
- Shows stage findings with clear scope: transaction-wide, document-wide, or active parcel.
- Does not expose point or boundary editing commands.
- Does not filter document metadata when the active parcel changes.

### Parcel Geometry Lane

This lane is controlled by the active parcel selector.

Controls:

- Parcel selector.
- Parcel status summary.
- Geometry action toolbar.

Tabs:

- Points
- Boundary Segments
- Parcel Preview / Map Evidence

Behavior:

- The parcel selector filters only Points, Boundary Segments, and Parcel Preview.
- Add Point, Edit Point, Remove Point, Add Segment, Edit Segment, Exclude Segment, and Rebuild Points apply only to the active parcel.
- Switching parcels refreshes the point grid, segment grid, selected row, selected segment, parcel preview, and parcel validation summary together.
- Switching parcels must not mutate transaction-wide tabs.
- Shows only a compact active-parcel validation summary beside the geometry grids; full findings live in the transaction-wide Findings tab.

## Proposed Layout

Use the existing PXA workspace structure, but rename and regroup it around Plan Examination.

```text
Plan Examination Review
Transaction 100001027
Profile: Survey plan + computation sheet

[Process summary / stage findings strip]

Transaction Review
[Supporting Documents] [General Information] [Parties] [Memorandum] [Diagnostics]

Parcel Geometry Review
Parcel: [Lot 1 - 12 points / 12 segments - Ready ▼]
[Points] [Boundary Segments] [Parcel Preview]

[Save] [Validation Complete]
```

The visual grouping can be implemented with existing WPF tabs, expanders, and grids. The important contract is not a new visual style; it is the separation of scope.

## Dense Data Layout Requirement

The Points Validation workspace must be data-first. Real extracted review artifacts can contain many document metadata rows, volume/folio rows, owners/neighbors rows, point rows, and boundary segment rows.

UX requirements:

- Use resizable panes between Source Evidence and Review Tables.
- Let the source document pane collapse or shrink when the examiner is working in large grids.
- Keep grid headers sticky while rows scroll.
- Scroll inside each grid instead of growing the whole window beyond the screen.
- Keep the active parcel selector visible above parcel-scoped geometry tabs.
- Keep active scope visible in grid headers, for example `Showing Lot 1 points`.
- Allow the preview/map evidence panel to be narrower or collapsible when the point or segment grid needs space.
- Never place all extracted rows in cards; use compact grids because the examiner must scan and compare many values.
- Prefer transaction-wide grids for General Info, Parties, Memorandum, and Diagnostics, and parcel-scoped grids for Points and Boundary Segments.
- Do not keep a large Findings side panel permanently open. Findings should be a transaction-wide tab, with only active-parcel warnings/blockers summarized near the parcel selector or preview.

## Findings Placement

Findings are mixed-scope:

- General findings apply to the transaction or document.
- Parcel findings apply to a specific parcel, point, segment, closure, bearing, or distance.

UX decision:

- Put the full findings list in a `Findings` tab inside the Transaction Review lane.
- Each finding row must show scope, such as `Transaction`, `Document`, `Parcel: Lot 1`, or `Segment: Lot 1 / S4`.
- Show only the active parcel's current blocker/warning summary in the Parcel Geometry lane.
- If the active parcel has a blocker, show it near the parcel selector and allow the row/segment grid to highlight the affected item.
- Keep the default workspace focused on Points and Boundary Segments because most examiner time is spent correcting parcel geometry.

## Parcel Selector

The parcel selector is required even when there is only one parcel.

Display priority:

1. Parcel name from source document.
2. Lot number from source document.
3. Generated label such as `parcel-001`.

Recommended row format:

```text
Lot 1 - 12 points / 12 segments - Ready
Lot 2 - 8 points / 7 segments - Needs review
parcel-003 - 8 points / 0 segments - Missing segments
```

Selector behavior:

- Defaults to the first parcel requiring review.
- If all parcels are ready, defaults to the first parcel by source/order.
- Shows counts per parcel.
- Shows validation state per parcel.
- Does not hide other parcels; it changes the active context.

## Data Scope Rules

Transaction-wide data:

- Source document inventory.
- Source document viewer.
- General metadata.
- Parties, owners, occupiers, and neighbors.
- Memorandum evidence.
- Stage summaries.
- Extraction route and diagnostics.
- Final review and closeout evidence.

Parcel-scoped data:

- Point rows.
- Boundary segment rows.
- Closure/misclose evidence for a parcel.
- Bearing and distance chain readiness.
- Parcel preview.
- Geometry commands.
- Active parcel validation summary.

Mixed-scope data:

- Stage Findings should be visible in one place, but each finding must carry a scope label:
  - `Transaction`
  - `Document`
  - `Parcel: Lot 1`
  - `Parcel: parcel-003`

## State Patterns

### No Extraction

Use when no usable geometry was extracted from any source.

Message:

`0 points and 0 segments found. Rerun extraction or use Manual Mode.`

### Partial Extraction

Use when some geometry evidence exists but a parcel cannot be built.

Message:

`8 points found, 0 segments found, parcel cannot be built yet.`

### Filtered By Parcel

Use when the active parcel has no rows, but other parcels do.

Message:

`No boundary segments are loaded for Lot 2. 24 segments exist in other parcels.`

### Ready For Validation

Use when active parcel geometry has enough reviewed data.

Message:

`Lot 1 has 12 points and 12 segments. Closure is within tolerance.`

### Validation Blocked

Use when approval cannot proceed.

Message:

`Lot 1 cannot be approved: segment 4 has no distance and point P7 is duplicated.`

### Validation Complete

Use when all required parcel and transaction checks pass or are dispositioned.

Message:

`Validated points are approved. Continue in Create Spatial Units.`

## Interaction Rules

- `Save` persists reviewed edits but does not approve validation.
- `Validation Complete` approves the corrected review data and locks the review unless the user explicitly reopens/reprocesses.
- `Create Spatial Units` remains outside the geometry review workspace and is triggered after validation passes.
- `Create Spatial Units` creates the local/map-ready geometry package.
- `Final Review` is where generated geometry is visually approved in ArcGIS Pro.
- `Finalize` publishes/copies approved geometry into Enterprise `working_review`, records disposition, saves SpatialUnit evidence, uploads the working package, and closes the Innola task.

## Command Placement

Geometry commands belong only in the Parcel Geometry lane:

- Rebuild Points
- Add Point
- Edit Point
- Remove Point
- Add Segment
- Edit Segment
- Exclude Segment

Transaction commands belong outside the geometry tabs:

- Rerun Extraction
- Clear Validation
- Recreate Spatial Units
- Retry Finalize
- Restart Transaction

Support/reprocess commands should remain visible, but grouped away from normal editing commands so the examiner does not confuse data correction with workflow restart.

## Key Flows

### Flow 1: Single-Parcel Survey Plan

1. Examiner opens a Plan Examination transaction.
2. `Process Plan` extracts survey-plan evidence and document metadata.
3. Workspace opens with one parcel selected, for example `Lot 1`.
4. Examiner reviews General Information, Parties, and Memorandum without changing parcel context.
5. Examiner reviews Points and Boundary Segments for `Lot 1`.
6. Examiner saves corrections.
7. Examiner selects `Validation Complete`.
8. Main workflow enables `Create Spatial Units`.

Climax: the old PXA path still feels intact, but no longer carries a separate PXA identity in the UI.

### Flow 2: Multi-Parcel Computation Sheet

1. Examiner opens a PE transaction with multiple parcel tables.
2. `Process Plan` finds multiple parcel groups.
3. Parcel selector shows `Lot 1`, `Lot 2`, and `Lot 3` with counts and statuses.
4. Examiner corrects `Lot 1` in Points and Boundary Segments.
5. Examiner switches to `Lot 2`; the geometry grids and preview update.
6. General Information and Memorandum remain unchanged.
7. Examiner resolves all parcel blockers.
8. `Validation Complete` approves the whole transaction review.

Climax: the examiner never loses the transaction context while moving parcel by parcel.

### Flow 3: Partial Extraction Recovery

1. Extraction returns points but no segments for one parcel.
2. Parcel selector shows the affected parcel as `Missing segments`.
3. Boundary Segments tab shows an empty-state message scoped to the active parcel.
4. Examiner chooses to rerun extraction or manually add segments.
5. Added segments default to the active parcel.
6. Rebuild Points uses only the active parcel boundary chain.

Climax: the UI explains the failure as a recoverable parcel-specific condition, not as a dead-end workflow failure.

## Accessibility Floor

- Keyboard users must be able to move through parcel selector, tabs, grids, and commands without losing focus context.
- Active parcel label must be visible near every geometry grid.
- Empty states must include the active parcel name.
- Validation blockers must be readable as text and not depend only on color.
- Long parcel labels must truncate safely with tooltip/detail access.
- Buttons that can delete/regenerate data must state what will be affected before execution.

## Implementation Notes For Amelia

- Start from the current PXA/Points Validation Tool surface instead of introducing a new workspace.
- Rename visible workspace language toward `Plan Examination Review` and `Parcel Geometry Review`.
- Keep existing document-level collections unfiltered.
- Treat parcel filtering as a projection over the review artifact, not as destructive data mutation.
- Ensure new points and segments inherit the active parcel group.
- Preserve the stage contract: approved review first, local outputs second, final review third, Enterprise publish/finalize last.

## Open Questions

- Should the parcel selector live above both lanes or only above the Parcel Geometry lane? Sally recommends only above the Parcel Geometry lane so document tabs do not appear parcel-filtered.
- Should Stage Findings be a transaction-wide tab only, or also show a small active-parcel subset beside the geometry tabs?
- Should `Validation Complete` require every parcel to be visited at least once, or only require all validation blockers to be resolved?
- Should the first selected parcel be the first parcel in source order or the first parcel with blockers? Sally recommends first parcel with blockers.

## Acceptance Criteria Seeds

- Given a unified Plan Examination review has one parcel, when the workspace opens, then the parcel selector is still visible but reads as a simple active context instead of a separate workflow type.
- Given a unified Plan Examination review has multiple parcels, when the examiner changes the active parcel, then only Points, Boundary Segments, Parcel Preview, and active parcel validation summary change.
- Given the examiner is reviewing General Information, Parties, Memorandum, or Diagnostics, when the active parcel changes, then those transaction-wide tabs retain their content.
- Given the examiner adds a point or segment, when `Lot 2` is active, then the new geometry row belongs to `Lot 2`.
- Given extraction produced rows for other parcels but not the active parcel, when the active geometry tab opens, then the empty state says the active parcel is empty and shows counts for other parcels.
- Given validation is approved, when the examiner returns to the Points Validation Tool, then review is locked unless an explicit reprocess/reopen action is selected.
