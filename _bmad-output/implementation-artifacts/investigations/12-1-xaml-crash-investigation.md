# Investigation: Story 12-1 XAML Crash Opening Points Validation

## Hand-off Brief

1. **What happened.** Confirmed: ArcGIS Pro crashed while opening the Points Validation review window after story 12-1 changes.
2. **Where the case stands.** Confirmed: the dump contains `XamlParseException` caused by missing resource `SmallButtonStyle` during `JamaicaReviewWorkspaceWindow` construction; the local XAML now defines that resource.
3. **What's needed next.** Rebuild/repackage the add-in, restart ArcGIS Pro, and retry opening the review window.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | 12-1 blocker |
| Date opened | 2026-09-12 |
| Status | Concluded |
| System | ArcGIS Pro 3.6 lane, add-in `net8.0-windows` |
| Evidence sources | `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_09_12_2026_13_37_48.dmp`, source XAML, test harness |

## Problem Statement

User reported a blocker: the app crashed after loading the latest story 12-1 changes.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| ArcGIS Pro dump | Available | Dump path was direct under `ErrorReports`; string extraction found XAML exception data. |
| Source XAML | Available | `JamaicaReviewWorkspaceWindow.xaml` referenced `SmallButtonStyle` but did not define it before the fix. |
| Tests | Partial | String-based XAML tests now assert the resource exists; full harness still stops later outside ArcGIS Pro on missing ArcGIS runtime assembly. |

## Confirmed Findings

### Finding 1: Review window crashed during XAML loading

**Evidence:** Dump string extraction found `XamlParseException`, `JamaicaReviewWorkspaceWindow..ctor()`, `InitializeComponent()`, and `RunOrOpenExtractionReviewAsync`.

**Detail:** The crash occurred while constructing the WPF review window, before normal review workflow logic could complete.

### Finding 2: Missing XAML resource caused the crash

**Evidence:** Dump string extraction found `Cannot find resource named 'SmallButtonStyle'. Resource names are case sensitive.` and `Provide value on 'System.Windows.StaticResourceExtension' threw an exception.`

**Detail:** Story 12-1 added a button in the Memorandum tab using `Style="{StaticResource SmallButtonStyle}"`; that style existed in `ParcelWorkflowDockpane.xaml`, not in `JamaicaReviewWorkspaceWindow.xaml`.

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml` |
| Trigger | Opening Points Validation / PXA review workspace |
| Condition | WPF resolves a window-local/static resource reference to `SmallButtonStyle` and cannot find it in that window resource scope |
| Related files | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs` |

## Conclusion

**Confidence:** High

The crash root cause is a missing XAML resource reference in `JamaicaReviewWorkspaceWindow.xaml`. The fix is to define `SmallButtonStyle` in that window's resources so the Memorandum bulk-action button can load.

## Recommended Next Steps

### Fix direction

Done: added a local `SmallButtonStyle` definition in `JamaicaReviewWorkspaceWindow.xaml` and a regression assertion in `JamaicaReviewWorkspaceXamlTests`.

### Diagnostic

After installing the rebuilt add-in, restart ArcGIS Pro and open the same transaction/review window. If a new dump appears, compare the exception string; it should no longer mention `SmallButtonStyle`.

## Reproduction Plan

1. Install the story 12-1 build that references `SmallButtonStyle` without defining it in the review window.
2. Open a PXA/Points Validation review window.
3. WPF throws `XamlParseException` during `JamaicaReviewWorkspaceWindow.InitializeComponent()`.
4. Install the patched build and repeat; the review window should open.
