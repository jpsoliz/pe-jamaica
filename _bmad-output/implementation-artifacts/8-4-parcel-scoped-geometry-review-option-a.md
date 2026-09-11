---
status: review
created_at: 2026-09-10
owner: Sally / UX
target_case: "100001027"
---

# Story: Parcel-Scoped Geometry Review For Multi-Parcel PE

## Story

As a plan examiner reviewing a multi-parcel PE computation sheet,  
I want the active parcel selection to filter only parcel geometry review,  
so that I can review Lot 1, Lot 2, and Lot 3 independently while keeping document-level evidence stable.

## Business Context

Transaction `100001027` contains a scanned computation sheet with separate Lot 1, Lot 2, and Lot 3 traverse tables. The current review workspace has a parcel selector and parcel preview, but the mental model is not clean enough:

- Document tabs contain transaction/source facts that are shared by the whole document.
- Geometry tabs contain parcel-specific points, boundary segments, and preview.
- Geometry actions such as rebuild, add, remove, and edit apply only to the selected parcel geometry.

The UI must make that boundary obvious. Switching from Lot 1 to Lot 2 must change the point table, segment table, and parcel preview together. It must not change General Info, Owners / Neighbors, or other document-level tabs.

## UX Decision

Use Option A: keep the existing tabbed workspace, but divide behavior into document-scoped tabs and parcel-scoped geometry tabs.

```text
Document Review
[General Info] [Owners / Neighbors] [Certificates / Notes]

Parcel Geometry Review
Parcel: [Lot 1 ▼]
[Points] [Boundary Segments] [Parcel Preview]
```

The exact visual grouping may reuse the existing WPF layout, but behavior must follow this model.

## Acceptance Criteria

1. Given a review document has multiple `parcel_group_id` values, when the workspace opens, then the parcel selector lists each parcel group and selects the first available group by default.

2. Given `Lot 1` is selected, when the user opens the `Points` tab, then only point rows whose `parcel_group_id` resolves to `Lot 1` are visible.

3. Given `Lot 1` is selected, when the user opens the `Boundary Segments` tab, then only boundary segments whose `parcel_group_id` resolves to `Lot 1` are visible.

4. Given the user switches from `Lot 1` to `Lot 2`, then the point table, boundary segment table, selected row/segment, validation summary, and parcel preview all refresh to `Lot 2`.

5. Given the user is on `General Info`, `Owners / Neighbors`, or any other document-scoped tab, then switching parcels must not filter, hide, or mutate document-level metadata, parties, volume/folio, instrument, parish, surveyor, or document notes.

6. Given geometry commands are shown, then `Rebuild Points`, `Add Point`, `Remove Point`, `Edit Point`, `Add Segment`, `Edit Segment`, and `Exclude Segment` are visible only inside parcel-scoped geometry tabs or geometry command areas.

7. Given the user is on a document-scoped tab, then geometry commands are hidden or disabled and must not appear as generic/global document actions.

8. Given the user adds a point from a parcel-scoped tab, then the new point defaults to the currently selected parcel group.

9. Given the user adds a boundary segment from a parcel-scoped tab, then the new segment defaults to the currently selected parcel group.

10. Given the user edits or removes a point/segment, then the operation applies to the selected parcel item only and must not affect rows in another parcel group.

11. Given a selected parcel has no visible rows or segments, then the workspace shows an empty state for that geometry tab without implying that the whole document extraction is empty.

12. Given automated tests run, then coverage proves parcel selection filters both points and segments, and document-level tabs remain unfiltered.

## Implementation Notes

The likely implementation surface is already present:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`

Current behavior to preserve:

- `JamaicaReviewWorkspaceViewModel.ParcelGroups` already derives selectable parcel groups.
- `SelectedParcelGroup` already triggers projection refresh.
- `VisibleRows` already uses `BuildVisibleRowsForParcel(...)`.
- Document metadata collections such as `VisibleMetadataFields`, `VisibleAdjacentOwners`, `VisibleNamedParties`, `VisibleVolumeFolios`, and `VisibleMemorandumGroups` are document-scoped and should remain unfiltered.

Primary gap to fix:

- `RebuildVisibleSegments()` currently rebuilds from all `parent.ReviewSegments`; it must filter by `SelectedParcelGroup?.GroupId` using the same parcel-group normalization pattern as `VisibleRows`.

Expected segment filter:

```csharp
var selectedGroupId = SelectedParcelGroup?.GroupId;
var segments = string.IsNullOrWhiteSpace(selectedGroupId)
    ? parent.ReviewSegments
    : parent.ReviewSegments.Where(segment =>
        string.Equals(ResolveParcelGroupKey(segment.ParcelGroupId), selectedGroupId, StringComparison.OrdinalIgnoreCase));
```

Then preserve existing ordering:

```csharp
.OrderBy(segment => segment.Sequence ?? int.MaxValue)
.ThenBy(segment => segment.FromPoint, StringComparer.OrdinalIgnoreCase)
.ThenBy(segment => segment.ToPoint, StringComparer.OrdinalIgnoreCase)
```

Also verify whether point/segment add commands currently know the selected parcel group. If they default to blank/global, update the command path so new rows inherit the active parcel group when launched from the workspace.

## UX Copy / Empty States

Use short, concrete status text:

- For document tabs: `Document-level review`
- For geometry tabs: `Reviewing Lot 1`, `Reviewing Lot 2`, etc.
- For an empty parcel geometry tab: `No points are loaded for this parcel.` or `No boundary segments are loaded for this parcel.`

Do not use generic text like `No review rows are loaded` when the document has rows in another parcel group.

## Test Plan

Use transaction `100001027` or a fixture based on it:

- Lot 1: points and segments visible only for Lot 1.
- Lot 2: points and segments visible only for Lot 2.
- Lot 3: points and segments visible only for Lot 3.
- General Info still shows document metadata regardless of active parcel.
- Owners / Neighbors still shows document-level people/neighbor data regardless of active parcel.
- Add Point from the parcel workspace creates a row with the active `parcel_group_id`.
- Add Segment from the parcel workspace creates a segment with the active `parcel_group_id`.
- Rebuild Points uses only the active parcel's visible boundary chain.

## Non-Goals

- Do not redesign the full window chrome.
- Do not change extraction schema.
- Do not change PE/PXA source routing.
- Do not change approval or output contracts except where parcel-group defaults are required for new rows/segments.
- Do not add a new map engine or preview component.

## Developer Checklist

- [x] Filter `VisibleSegments` by `SelectedParcelGroup`.
- [x] Refresh point table, segment table, preview, and selected item on parcel change.
- [x] Keep metadata/document tabs unfiltered.
- [x] Scope geometry commands to geometry tabs/areas in XAML.
- [x] Default new point and segment parcel group to the selected parcel.
- [x] Add or update tests for parcel-scoped rows and segments.
- [x] Validate with `100001027` showing distinct Lot 1 / Lot 2 / Lot 3 geometry.

## Dev Agent Record

### Implementation Notes

- Added first-class `ParcelGroupId` / `ParcelName` support to reviewed boundary segments so extracted and manually added segments can be scoped like point rows.
- Updated `JamaicaReviewWorkspaceViewModel` so `VisibleSegments` is rebuilt from the selected parcel group, and parcel changes refresh points, segments, preview, and segment summary together.
- Kept document-scoped collections unfiltered: metadata fields, adjacent owners, named parties, volume/folio values, and memorandum groups.
- Updated manual boundary segment creation so new segments inherit the active parcel group/name and calculate the next sequence inside that parcel.
- Changed the center title from `PXA Survey Plan Review` to `Parcel Geometry Review` so the shared segmented workspace reads correctly for PE multi-parcel computation review.

### Test Log

- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "visible segments"`: PASS
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "manual boundary segment"`: PASS
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "review persistence saves editable segment rows"`: PASS
- `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj "pxa review xaml"`: PASS
- `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj -c Release`: PASS
- `tools/package_addin.ps1 -Configuration Release`: PASS, add-in version `1.1.500`
- `tools/stage_target_deployment.ps1 -Configuration Release`: PASS, add-in version `1.1.501`
- `git diff --check`: PASS with line-ending warnings only

### File List

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewDocument.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewPersistenceService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionReviewSegmentViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ManualBoundarySegmentService.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ExtractionReviewPersistenceServiceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ManualBoundarySegmentServiceTests.cs`
- `_bmad-output/implementation-artifacts/8-4-parcel-scoped-geometry-review-option-a.md`

### Change Log

- 2026-09-10: Implemented parcel-scoped boundary segment filtering, segment parcel identity persistence, active-parcel manual segment defaults, and focused regression tests.
