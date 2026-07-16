---
baseline_commit: handoff-2026-07-14
---

# Story 8.4D: Add Collapsible PDF Panel To Compare Workspace

Status: done

## Story

As a cadastral examiner in Compare,  
I want to collapse and restore the attached-document PDF panel,  
so that I can keep the ArcGIS Pro map visible while still using Compare queries, evidence results, and decision controls.

## Business Context

Compare is an evidence reconciliation workspace. The real geometry review happens in the active ArcGIS Pro map, while the Compare window provides document review, Legal/Fiscal evidence search, evidence curation, and decision capture.

Story 8.4C added Enterprise Legal/Fiscal neighbor evidence and moved geometry into the active map instead of an embedded form map. After that, the Compare window was reduced to a compact sidecar layout, but field testing showed that the PDF and evidence panels can still obscure too much of the map on common Pro monitor layouts.

The next UX improvement is a low-risk collapse/restore control for the PDF panel. This keeps both workflows available without forcing the examiner into a tab switch that hides evidence context.

## UX Decision

Sally recommendation:

- Implement a collapsible PDF panel first, not full `PDF | Evidence` tabs.
- Default state remains PDF visible because document review is core to Compare.
- Add an explicit `Hide PDF` / `Show PDF` toggle near the Attached Documents header.
- When hidden, the evidence side uses the available Compare window width.
- Keep the active ArcGIS map visible behind the floating Compare sidecar.
- Preserve existing PDF selection, embedded WebView2 fallback behavior, and evidence search state.

Tabs can be considered later for very small screens, but collapsible PDF is the simpler and safer next step.

## Acceptance Criteria

1. Given the Compare workspace is opened, when it first loads, then the attached-document PDF panel is visible by default.
2. Given the PDF panel is visible, when the examiner clicks `Hide PDF`, then the document panel collapses, the column splitter/gap is hidden, and the evidence panel expands into the available Compare window width.
3. Given the PDF panel is hidden, when the examiner clicks `Show PDF`, then the document panel is restored with its previous width and current selected PDF/document state.
4. Given the PDF panel is hidden, when the examiner runs PID, Volume/Folio, Land Val No., Name + Parish, or Legal/Fiscal spatial evidence refresh, then those workflows continue without needing the PDF panel visible.
5. Given the PDF panel is hidden, when the selected PDF changes due to data reload or user action after restoring, then the existing embedded PDF viewer/image/fallback behavior still works.
6. Given WebView2 is unavailable or PDF rendering fails, when the PDF panel is restored, then the existing fallback message is shown and the Compare workspace remains usable.
7. Given the Compare window is resized near its minimum width, when the PDF panel is hidden, then search fields, buttons, evidence lists, decision notes, and bottom actions remain readable and do not overlap.
8. Given the Compare window is closed and reopened, then the default can return to PDF visible unless a local non-persistent in-session preference is already available; do not add persisted settings for this story.
9. Given automated tests run, then they verify the new collapse command/property defaults, state toggling, and XAML binding presence without requiring ArcGIS Pro map rendering.

## Tasks / Subtasks

- [x] Add PDF panel visibility state to the Compare workspace. (AC: 1, 2, 3, 8)
  - [x] Add `IsPdfPanelVisible` or equivalent to `CompareWorkspaceViewModel` or window code-behind.
  - [x] Add a toggle command/property for `Hide PDF` / `Show PDF`.
  - [x] Keep the default state visible on new window creation.
  - [x] Do not persist the preference to global settings in this story.

- [x] Update `CompareWorkspaceWindow.xaml` layout. (AC: 2, 3, 7)
  - [x] Name the PDF column, spacer column, and evidence column if needed.
  - [x] Bind PDF panel visibility to the new state.
  - [x] Collapse the spacer/gap when PDF is hidden.
  - [x] Let the evidence panel occupy the available width when PDF is hidden.
  - [x] Place the toggle near the `Attached Documents` heading and provide a visible `Show PDF` control when hidden.

- [x] Preserve document viewer behavior. (AC: 3, 5, 6)
  - [x] Do not recreate WebView2 unnecessarily when the panel is toggled.
  - [x] Preserve selected PDF document and `ViewerNavigationKey`.
  - [x] Ensure hidden-panel state does not suppress later viewer refresh after restore.
  - [x] Preserve existing fallback path for unavailable WebView2 or image/PDF display failure.

- [x] Preserve Compare evidence workflows. (AC: 4, 7)
  - [x] Confirm manual query controls remain visible and usable with PDF hidden.
  - [x] Confirm `Show active map`, `Refresh`, and `Refresh Legal/Fiscal spatial evidence` remain accessible.
  - [x] Confirm evidence lists and decision controls still scroll/use available space correctly.

- [x] Add tests. (AC: 9)
  - [x] Add ViewModel tests for default visible state and toggle behavior if state lives in the ViewModel.
  - [x] Add XAML smoke/binding test to verify the toggle control and named layout elements exist.
  - [x] Add regression coverage that hiding the PDF panel does not change selected document state.

## Developer Notes

Relevant existing files:

- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareEnterpriseCadasterEvidenceTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTestHelpers.cs`

Current layout facts:

- `CompareWorkspaceWindow.xaml` is currently a two-column sidecar:
  - PDF panel column: `390`
  - spacer column: `8`
  - evidence panel: `*`
- Current window size is `Width="1040"`, `Height="760"`, `MinWidth="880"`, `MinHeight="640"`.
- The PDF viewer host is `DocumentWebViewHost`; image fallback is `ImageScrollViewer`; fallback text is `DocumentFallbackText`.
- The window code-behind lazy-creates WebView2 and already handles fallback safely.

Implementation guidance:

- Prefer a simple ViewModel property if tests can cover it cleanly:
  - `IsPdfPanelVisible`
  - `PdfPanelToggleText`
  - `TogglePdfPanelCommand`
- If changing `GridLength` from binding is awkward, code-behind may adjust named column widths on property change. Keep it small and deterministic.
- Do not introduce a new settings file value unless explicitly requested later.
- Do not add tabs yet. This story is only collapsible PDF panel behavior.

## UX Notes

Sally guidance:

- The map is the primary spatial canvas; Compare should not cover it more than necessary.
- The PDF is important, but it is supporting evidence, not the whole workspace.
- Use an explicit text toggle rather than a small icon-only affordance for now because this is a specialist workflow and discoverability matters.
- Keep the control label direct: `Hide PDF` when visible, `Show PDF` when hidden.
- Avoid modal behavior. The examiner should be able to hide/show without losing search results, selected evidence, notes, or PDF selection.

## Testing Notes

Run after implementation:

```powershell
dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"
```

Manual validation target:

- Open Compare for `TR100000668`.
- Confirm the PDF panel is visible by default.
- Click `Hide PDF`.
- Confirm the Compare window becomes mostly evidence controls and the ArcGIS Pro map behind it is easier to see.
- Run a Volume/Folio or PID search with the PDF hidden.
- Click `Show PDF`.
- Confirm the same PDF selection/viewer returns and evidence results/notes are preserved.
- Resize near minimum width and confirm no controls overlap.

## Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-07-15 | 1.0 | Initial story for collapsible PDF panel in Compare workspace. | Sally / Mary |
| 2026-07-15 | 1.1 | Implemented collapsible PDF state, window column collapse behavior, and tests. | Amelia |
| 2026-07-15 | 1.2 | Added compact window resize on PDF hide and expanded width restore on PDF show. | Amelia |

## Dev Agent Record

### Implementation Plan

1. Add red tests for PDF panel default visibility, toggle behavior, selected-document preservation, and XAML layout hooks.
2. Add ViewModel state/command for `IsPdfPanelVisible`, `IsPdfPanelHidden`, `PdfPanelToggleText`, and `TogglePdfPanelCommand`.
3. Name Compare window PDF/spacer layout elements and add explicit `Hide PDF` / `Show PDF` controls.
4. Keep layout mutation in code-behind by changing column widths and panel visibility only; do not recreate the document viewer.
5. Run focused tests, Compare regression tests, build, and full test harness.

### Debug Log

- Red phase: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pdf panel"` failed because `CompareWorkspaceViewModel` did not yet expose PDF panel state or toggle command.
- Focused PDF panel tests passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pdf panel"` passed 2 tests.
- Compare regression tests passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"` passed 69 tests.
- Build passed: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- Full test harness passed: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj` passed 428 tests.
- Compact resize follow-up: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "pdf panel"` passed 2 tests.
- Compact resize follow-up: `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj -- "compare"` passed 69 tests.
- Compact resize follow-up: `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after rerun; first parallel attempt hit a transient Windows file lock while tests were compiling.

### Completion Notes

- Added non-persistent PDF panel visibility state to the Compare ViewModel.
- Added `Hide PDF` / `Show PDF` toggle text and command.
- Added named PDF panel and spacer columns in the Compare window.
- Added code-behind layout adjustment that collapses/restores the PDF panel without clearing or recreating WebView2.
- Added compact Compare window sizing when the PDF is hidden and previous expanded width restore when the PDF is shown.
- Preserved selected PDF document and viewer navigation state across toggles.
- Added focused ViewModel and XAML tests for the collapsible PDF and compact resize behavior.

### File List

- `_bmad-output/implementation-artifacts/8-4d-add-collapsible-pdf-panel-to-compare-workspace.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Compare/CompareWorkspaceViewModel.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceViewModelTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Compare/CompareWorkspaceXamlTests.cs`
- `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
