# Investigation: TR100000668 Compare Launch Crash

## Hand-off Brief

1. **What happened.** User reports ArcGIS Pro 3.6.2 crashes when launching the Compare form for TR100000668; crash dump metadata is confirmed at `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_07_14_2026_20_31_20.dmp`.
2. **Where the case stands.** Active; dump string scan and source trace both point to the Compare WPF window launch/load boundary, with WebView2/CoreWebView2 and graphics components present in the dump.
3. **What's needed next.** Patch Compare launch to be defensive in the ArcGIS host: dispatch/catch launch errors, catch async window-load errors, and make PDF WebView2 initialization lazy/fallback-safe.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000668 |
| Date opened | 2026-07-15 |
| Status | Active |
| System | ArcGIS Pro 3.6.2.59530, Windows 11 Enterprise 22631, 2 monitors, Intel UHD + virtual display adapters |
| Evidence sources | User report, ESRI crash dump metadata, local source code |

## Problem Statement

User reports: "ESRi crashes when the compare form is launch" for TR100000668. The referenced dump is `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_07_14_2026_20_31_20.dmp`.

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| Crash dump metadata | Available | File exists, length 10,656,578 bytes, timestamp July 14, 2026 around 20:31. |
| Crash dump stack/content | Partial | Focused dump string scan found `CompareWorkspace`, `CompareWorkspaceWindow`, `WebView2`, `CoreWebView2`, `Microsoft.Web`, `PresentationFramework`, `WindowsBase`, `KERNELBASE`, `ucrtbase`, `D3D`, `DirectX`, and `ArcGIS.Desktop.Mapping`; it did not find `QueuedTask`. |
| User reproduction context | Partial | TR100000668, Compare form launch. |
| Source code | Available | Compare launch, workspace window, and load services are in repo. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Compare window launch/threading path | High | Confirmed risk | Compare launcher lacks the dispatcher/error containment used elsewhere. |
| 2 | WebView2 initialization and PDF viewer path | High | Confirmed risk | Compare XAML eagerly instantiates WebView2 and initializes it differently from existing stable viewers. |
| 3 | Active map layer loading during window load | Medium | Reduced | Map service catches many managed errors; dump scan did not show `QueuedTask`. |
| 4 | Dump stack analysis | Medium | Partial | String scan supports WPF/WebView2/graphics path; full WinDbg stack still unavailable. |

## Timeline of Events

| Time | Event | Source | Confidence |
| --- | --- | --- | --- |
| 2026-07-14 20:31 | ArcGIS Pro crash dump written | Dump metadata | Confirmed |
| 2026-07-15 | User reports crash on Compare launch for TR100000668 | User message | Confirmed |

## Confirmed Findings

### Finding 1: Crash dump exists for the reported time window

**Evidence:** `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_07_14_2026_20_31_20.dmp`

**Detail:** File metadata read confirmed the referenced dump exists and is 10,656,578 bytes.

### Finding 2: The dump contains Compare window, WPF, WebView2, and graphics markers

**Evidence:** Focused string scan of the `.dmp`.

**Detail:** The dump contains `CompareWorkspace`, `CompareWorkspaceWindow`, `WebView2`, `CoreWebView2`, `Microsoft.Web`, `PresentationFramework`, `WindowsBase`, `KERNELBASE`, `ucrtbase`, `D3D`, `DirectX`, and `ArcGIS.Desktop.Mapping`. It does not contain `QueuedTask`.

### Finding 3: Compare launch has no dispatcher or error containment

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Innola/ShellState.cs:98`

**Detail:** `OpenCompareWorkspace` constructs `CompareWorkspaceViewModel`, constructs `CompareWorkspaceWindow`, sets `Owner`, and calls `Show()` directly. Unlike the existing dockpane path, this launch boundary has no dispatcher handoff, no try/catch, and no user-safe failure state if window construction fails inside ArcGIS Pro.

### Finding 4: Compare window eagerly creates WebView2 during XAML initialization

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml:69`

**Detail:** The XAML declares `<wv2:WebView2 x:Name="DocumentWebView" Visibility="Collapsed" />`. That means WebView2 is constructed during `InitializeComponent()`, before the user opens a PDF and before `EnsureWebViewAsync()` can catch or fall back. The crash happening on form launch fits this timing.

### Finding 5: Compare WebView2 initialization differs from existing working viewers

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs:87`; existing patterns in `JamaicaReviewWorkspaceWindow.xaml.cs:128` and `ParcelWorkflowDockpane.xaml.cs:97`.

**Detail:** Existing viewers set `WebView2.CreationProperties` with `CoreWebView2CreationProperties.UserDataFolder` and then call `EnsureCoreWebView2Async()`. Compare creates a `CoreWebView2Environment` directly and passes it into `EnsureCoreWebView2Async(environment)`. The direct environment path is not necessarily wrong, but it is a different host-initialization path in an ArcGIS Pro WPF add-in.

### Finding 6: Async window event handlers can leak exceptions into the host process

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs:26` and `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CompareWorkspaceWindow.xaml.cs:37`.

**Detail:** `OnLoaded` and `OnViewModelPropertyChanged` are `async void` handlers and await `viewModel.LoadAsync()` / `RefreshViewerAsync()` without try/catch. Any managed exception during transaction reload, attachment viewer setup, map integration, or WebView2 navigation can escape the add-in boundary.

## Deduced Conclusions

1. The Compare stage/configuration is not the immediate issue for TR100000668. `WorkflowSettings.json` includes `compare_workflow_stages` with `Compare` and `Compare Survey Plan`.
2. The crash most likely occurs at the Compare form construction/load boundary, not during later ownership review logic.
3. The highest-risk concrete cause is WebView2 initialization inside the new Compare form, especially because the dump contains WebView2/CoreWebView2/D3D/DirectX markers and the form eagerly creates WebView2 in XAML.
4. Even if WebView2 is not the only trigger, the current Compare launcher and window async handlers do not contain failures safely inside ArcGIS Pro.

## Hypothesized Paths

### Hypothesis 1: Compare launch path triggers an unmanaged ArcGIS Pro host crash

**Status:** Open

**Theory:** The crash occurs during `CompareWorkspaceWindow` launch or first load, likely from WPF/WebView2/ArcGIS SDK host interaction rather than ordinary managed exception handling.

**Supporting indicators:** User says the crash happens when the Compare form launches; ArcGIS Pro writes a `.dmp` instead of the add-in surfacing a managed error.

**Would confirm:** Dump stack references WebView2, WPF window construction, ArcGIS Desktop framework UI, or Compare load code.

**Would refute:** Dump stack points to unrelated extension/module or crash occurs before Compare code executes.

**Resolution:** Open.

### Hypothesis 2: Eager WebView2 construction crashes ArcGIS Pro during Compare form launch

**Status:** Strong candidate

**Theory:** `CompareWorkspaceWindow.InitializeComponent()` constructs the hidden `DocumentWebView`. On this machine, the WebView2/graphics stack fails inside the ArcGIS host process before managed add-in code can report a normal error.

**Supporting indicators:** Dump contains WebView2/CoreWebView2 and D3D/DirectX markers; user reports crash at form launch; XAML instantiates WebView2 even when collapsed; machine has Intel UHD plus virtual display adapters.

**Would confirm:** Temporarily remove/lazy-create WebView2 and TR100000668 Compare opens without crashing.

**Would refute:** Crash persists before WebView2 creation when WebView2 is fully removed from the launch path.

**Resolution:** Open.

### Hypothesis 3: Unhandled async load exception terminates the host

**Status:** Plausible

**Theory:** `OnLoaded` runs `viewModel.LoadAsync()` and `RefreshViewerAsync()` without a catch. A transaction-specific load, attachment, map, or viewer exception escapes the `async void` handler and ArcGIS Pro writes a crash dump.

**Supporting indicators:** Window loaded/property changed handlers have no try/catch; TR100000668 includes attachments and Compare geometry load; dump includes WPF framework markers.

**Would confirm:** Add try/catch around these handlers and the process no longer crashes, instead showing a managed status/error.

**Would refute:** Crash occurs before `Loaded` fires.

**Resolution:** Open.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Dump stack/module exception | Would identify unmanaged crash origin | Analyze `.dmp` with WinDbg/Visual Studio or inspect ESRI companion report files. |
| Exact click sequence | Would separate transaction-load crash from form-render crash | Reproduce with logs around transaction selection and Compare launch. |
| Full native stack | Would distinguish WebView2/GPU fault from managed unhandled exception | Open `.dmp` in Visual Studio/WinDbg with native + managed stack symbols. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | Compare WPF window launch/load boundary |
| Trigger | Compare workspace launch for TR100000668 |
| Condition | New Compare form construction and first load; likely WebView2/PDF viewer initialization or unhandled async load exception |
| Related files | `ShellState.cs`, `CompareWorkspaceWindow.xaml.cs`, `CompareWorkspaceViewModel.cs`, `CompareWorkingGeometryService.cs`, `ArcGisCompareMapIntegrationService.cs` |

## Conclusion

**Confidence:** Medium

The crash is confirmed as an ArcGIS Pro process dump and the strongest code path is the new Compare window launch/load boundary. The most likely immediate trigger is eager WebView2 creation/initialization in the Compare form, amplified by missing exception containment around `Show()`, `Loaded`, and property-change refresh. This is not primarily a missing Compare-stage configuration problem.

## Recommended Next Steps

### Fix direction

1. Wrap `ShellState.OpenCompareWorkspace` in UI-dispatcher and try/catch handling so Compare launch failures surface as a status/message instead of terminating ArcGIS Pro.
2. Add try/catch around `CompareWorkspaceWindow.OnLoaded` and `OnViewModelPropertyChanged`; fail into a visible fallback state and keep the window/process alive.
3. Align Compare WebView2 setup with the existing stable viewer pattern using `CoreWebView2CreationProperties.UserDataFolder`.
4. Prefer lazy WebView2 creation or a temporary PDF fallback for Compare so the form can open even if WebView2 fails. The quickest confirmation test is to remove WebView2 from the initial XAML construction path and retry TR100000668.

### Diagnostic

After patching, rerun TR100000668 and verify whether the Compare window opens. If it opens but documents fail, the root cause is WebView2/PDF viewer initialization. If it shows a managed error before geometry/documents load, the root cause is the async load path.

## Reproduction Plan

Load TR100000668, start Compare stage, launch Compare workspace, observe whether ArcGIS Pro crashes before or after the window is visible.

## Side Findings

## Follow-up: 2026-07-15

### New Evidence

### Additional Findings

### Updated Hypotheses

### Backlog Changes

### Updated Conclusion

Focused dump scan and source trace moved confidence from Low to Medium. The fix should start at Compare launch/window robustness before deeper geometry or cadaster-query code.

## Patch Note: 2026-07-15

Applied the first crash-hardening patch:

1. `ShellState.OpenCompareWorkspace` now dispatches to the WPF UI dispatcher and catches launch failures before they can escape the ArcGIS Pro host.
2. `CompareWorkspaceWindow.OnLoaded` and document refresh property-change handling now catch failures and report them into the Compare workspace status.
3. Compare WebView2 setup now follows the existing stable `CoreWebView2CreationProperties.UserDataFolder` pattern.
4. Compare no longer creates WebView2 in XAML during `InitializeComponent`; it lazy-creates the control only when a PDF document needs the embedded viewer and falls back without closing the form if WebView2 fails.

Verification: solution build passed, and the local test harness passed 392 tests. Build still reports the pre-existing nullable warning in `SurveyPlanBoundarySolverTests.cs:82`.
