# Investigation: TR 100000755 ArcGISPro Crash Dump

## Hand-off Brief

1. **What happened.** ArcGIS Pro generated an ESRI error-report dump at `2026-08-28 00:09:53` while Parcel Workflow was working on TR `100000755`; the dump is present, and its minidump exception stream shows an unhandled CLR exception.
2. **Where the case stands.** The exact user-provided path had one extra folder level and did not exist, but the real dump was located directly under `C:\Users\js91482\AppData\Local\ESRI\ErrorReports`; the dump points to WPF layout/DataGrid activity, while case artifacts show a weak extraction decision gate immediately before the crash.
3. **What's needed next.** Decode the managed stack with WinDbg/SOS or add a focused guard/logging patch around extraction-review grid refresh, because the current evidence narrows the surface but does not yet prove the exact WPF binding/value that threw.

## Case Info

| Field | Value |
| ----- | ----- |
| Ticket | N/A |
| Date opened | 2026-08-28 |
| Status | Active |
| System | ArcGIS Pro 3.6 / assembly lane 13.6.0.59527, ParcelWorkflowAddIn running from ArcGIS assembly cache |
| Evidence sources | ESRI dump metadata and minidump stream parse, Parcel Workflow case artifacts for TR `100000755`, Windows Application event log, source code trace |

## Problem Statement

User reported this crash dump path and asked to review it:

`C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro\_13.6.0.59527\_0_08_28_2026_00_09_53.dmp`

Initial claim: an ArcGIS Pro error occurred during the current workflow and the dump should explain why.

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------ | ----- |
| User-provided dump path | Missing | Elevated `Get-Item` confirmed the exact path does not exist. |
| Real ESRI dump | Available | Located at `C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_08_28_2026_00_09_53.dmp`, length `13,276,463`, last write `2026-08-28 00:09:53`. |
| Debugger tooling | Missing | `dumpchk`, `cdb`, `windbg`, `windbgx`, `procdump`, `strings`, and `llvm-strings` were not available in PATH. |
| Windows Application event log | Partial | No matching ArcGISPro/ParcelWorkflow crash entry appeared between `2026-08-28 00:00` and `00:20`. |
| WER archive/queue | Partial | WER archive contains older ArcGISPro reports from June/July 2026; no matching `2026-08-28 00:09:53` WER archive/queue record surfaced. |
| TR `100000755` case artifacts | Available | `workflow_lifecycle_audit.json`, `extraction_decision_gate.json`, `survey_plan_extraction_summary.json`, `extraction_review_data.json`, and `preflight_summary.json` were present in the case folder. |
| Source code | Available | Extraction decision gate and workflow session code paths were traced. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Decode managed stack from dump with WinDbg + SOS | High | Open | Needed to prove exact exception type/message and managed frame. |
| 2 | Audit extraction-review WPF/DataGrid bindings and layout constraints | High | Open | Dump strings point at WPF grid measurement and DataGrid cells. |
| 3 | Add safe exception logging around review refresh after `RunDraftExtractionAsync` | High | Open | Would capture exception message before ArcGIS host terminates. |
| 4 | Reproduce with TR `100000755` weak extraction artifact loaded | Medium | Open | Use current case folder artifacts; watch for crash when rendering review UI after weak gate. |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-08-28 00:06:48 | ParcelWorkflowAddIn DLL/PDB copied into ArcGIS assembly cache. | `C:\Users\js91482\AppData\Local\ESRI\ArcGISPro\AssemblyCache\...\ParcelWorkflowAddIn.dll` listing | Confirmed |
| 2026-08-28 00:08:08 local / 04:08:08Z | Transaction claim started for TR `100000755`, status succeeded. | `workflow_lifecycle_audit.json` event timestamp | Confirmed |
| 2026-08-28 00:08:51 local / 04:08:51Z | Georeference check passed with coordinate system `JAD 2001`, parish `SAINT ANN`, and 4 reviewed point rows. | `georeference_check_summary.json` payload timestamp | Confirmed |
| 2026-08-28 00:08:58 local / 04:08:58Z | Preflight passed, including survey plan PDF, workspace, Python, ArcPy, georeference readiness, and dimension readiness. | `preflight_summary.json` created_at | Confirmed |
| 2026-08-28 00:09:25 local / 04:09:25Z | Point review extraction attempt recorded as `weak`: `Too many rows have invalid coordinates (2 of 4).` | `workflow_lifecycle_audit.json` event timestamp | Confirmed |
| 2026-08-28 00:09:53 local | ESRI dump written. | Dump file `LastWriteTime` | Confirmed |

## Confirmed Findings

### Finding 1: The exact reported dump path is wrong, but the dump exists

**Evidence:** Elevated file check returned not found for `...\ErrorReports\ArcGISPro\_13.6.0.59527\_0_08_28_2026_00_09_53.dmp`; recursive ESRI ErrorReports listing found `...\ErrorReports\ArcGISPro_13.6.0.59527_0_08_28_2026_00_09_53.dmp`.

**Detail:** The dump is a file directly in `ErrorReports`, not under an `ArcGISPro` subfolder.

### Finding 2: The dump contains an unhandled CLR exception

**Evidence:** Minidump exception stream parse reported `Exception thread=35476 code=0xE0434352 flags=0x00000081 address=0x7FFE0FF6044C params=5 firstParam=0xFFFFFFFF80131501`.

**Detail:** `0xE0434352` is the standard CLR exception code. The first exception parameter `0x80131501` is a .NET `System.ArgumentException` family HRESULT.

### Finding 3: The fault address is in KERNELBASE, with ParcelWorkflowAddIn loaded

**Evidence:** Module-list parse mapped exception address `0x7FFE0FF6044C` to `C:\Windows\System32\KERNELBASE.dll`; the same dump includes `ParcelWorkflowAddIn.dll` loaded from `C:\Users\js91482\AppData\Local\ESRI\ArcGISPro\AssemblyCache\{7C3FB44F-F7D4-41AB-B51D-92EFCDB2E4AF}\ParcelWorkflowAddIn.dll`.

**Detail:** This is expected for a managed exception raised through Windows/CLR; it does not by itself identify the application frame.

### Finding 4: Dump strings point at WPF grid/DataGrid layout

**Evidence:** Dump string scan found `System.ArgumentException` adjacent to `System.Windows.Controls.Grid.MeasureCellsGroup()`, `System.Windows.Threading.Dispatcher.LegacyInvokeImpl()`, `System.Windows.Interop.HwndMouseInputProvider.ReportInput()`, and `System.Windows.ContextLayoutManager.UpdateLayout()`. Another nearby region included `System.Windows.Controls.DataGrid`, `DataGridTemplateColumn`, and `System.Windows.Controls.DataGridCell: Party / Owner`.

**Detail:** This indicates the crash was in a WPF layout/input/render pass, not in the Python process or Innola HTTP upload path.

### Finding 5: TR `100000755` extraction data was weak because 2 of 4 point rows lacked coordinates

**Evidence:** `extraction_decision_gate.json` recorded `LastQualityStatus: weak` and note `Too many rows have invalid coordinates (2 of 4).`; source code emits that exact message in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionDecisionGateService.cs:140`.

**Detail:** `extraction_review_data.json` rows `152` and `32` had numeric JAD coordinates, while derived rows `IP1` and `IP2` had blank easting/northing and review notes saying they are generated temporary point labels.

### Finding 6: The old coordinate-system bug is not present in this run

**Evidence:** `survey_plan_extraction_summary.json` shows `coordinate_system.value = JAD 2001`; `preflight_summary.json` georeference readiness passed with `JAD 2001`, parish `SAINT ANN`, and 4 reviewed point rows.

**Detail:** The earlier problem where `coordinate_system` could be filled with survey method text is not the active failure here.

## Deduced Conclusions

### Deduction 1: This crash is not the Innola/certificate/doc_type attachment failure

**Based on:** Findings 2, 4, and 5.

**Reasoning:** The case timeline shows the app was in extraction review immediately before the dump; there are no current-case report/attachment/writeback events before the crash; dump strings point to WPF layout/DataGrid.

**Conclusion:** The active failure surface is the add-in UI/review data rendering path after weak extraction, not the server attachment route.

### Deduction 2: The most likely trigger is rendering or refreshing review UI with weak/partial extraction data

**Based on:** Findings 4 and 5 plus source code at `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1891`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1906`, and `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1917`.

**Reasoning:** `WorkflowSession` loads the extraction review document, refreshes the decision gate twice, saves gate state, records lifecycle audit, and sets workflow state to `ReviewPending`. The dump is written 28 seconds after the weak extraction attempt record, and dump strings show WPF layout/DataGrid components.

**Conclusion:** A WPF binding/layout path likely receives a value shape it does not handle when rendering the weak review result or related review summaries.

## Hypothesized Paths

### Hypothesis 1: A WPF Grid/DataGrid layout value throws `ArgumentException`

**Status:** Open

**Theory:** One of the review grids renders partial extraction data or memorandum/party-owner rows with a malformed sizing/alignment/binding value, causing `System.Windows.Controls.Grid.MeasureCellsGroup()` to throw during layout.

**Supporting indicators:** Dump strings include `System.ArgumentException`, `Grid.MeasureCellsGroup()`, `DataGridTemplateColumn`, `DataGridCell: Party / Owner`, and dispatcher/layout frames.

**Would confirm:** WinDbg/SOS managed stack showing the exact WPF binding or add-in view model/property path.

**Would refute:** Managed stack points to a non-UI service path or unrelated ArcGIS component.

**Resolution:** Open.

### Hypothesis 2: Weak extraction rows with blank coordinates expose an unhandled review UI edge case

**Status:** Open

**Theory:** Rows `IP1` and `IP2` have blank easting/northing and derived labels; when the review workspace is refreshed, the UI tries to format, group, validate, or display these rows in a way that WPF rejects.

**Supporting indicators:** The decision gate records exactly `2 of 4` invalid coordinate rows immediately before the dump; user screenshot from the same area showed Georeference Check active and Validate Points and Lines not loaded.

**Would confirm:** Repro by loading the current TR `100000755` artifacts and seeing the same crash or a captured exception during row grid refresh.

**Would refute:** Repro with the same weak artifact renders safely, or dump stack points at a different tab/control.

**Resolution:** Open.

### Hypothesis 3: The crash is unrelated to ParcelWorkflowAddIn and only coincidental

**Status:** Open

**Theory:** ArcGIS Pro crashed in its own WPF/DataGrid component while the add-in was loaded and TR `100000755` strings were present.

**Supporting indicators:** The exception address maps to `KERNELBASE.dll`, and without a managed stack the application frame is not proven.

**Would confirm:** Managed stack contains no ParcelWorkflowAddIn frames and points to an ArcGIS built-in window/control.

**Would refute:** Managed stack includes ParcelWorkflowAddIn view/model frames or a Parcel Workflow XAML binding expression.

**Resolution:** Open.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| Managed stack trace from dump | Blocks exact root-cause confirmation. | Analyze dump with WinDbg Preview/SOS: load dump, run `!analyze -v`, `.loadby sos coreclr`, `!pe`, `!clrstack`. |
| Add-in exception log at the moment of UI refresh | Would identify message/property/control without external debugger. | Add try/catch logging around extraction review refresh and UI state transitions, or hook dispatcher unhandled exception logging. |
| Deterministic repro | Needed before a targeted fix can be verified. | Reopen TR `100000755` current case folder and trigger the same post-extraction review UI rendering path. |

## Source Code Trace

| Element | Detail |
| ------- | ------ |
| Error origin | Unknown managed WPF exception; dump points to WPF layout/DataGrid, exact managed frame unavailable without SOS. |
| Trigger | Post-extraction review refresh after draft extraction for TR `100000755`. |
| Condition | Extraction gate sees 4 rows total, only 2 with valid coordinates, and routes to manual decision/review pending. |
| Related files | `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1891`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1906`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs:1917`, `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ExtractionDecisionGateService.cs:140` |

## Conclusion

**Confidence:** Medium

The dump is real and was found at the corrected ESRI ErrorReports path. It contains an unhandled CLR exception with a .NET `ArgumentException` family HRESULT, and dump strings strongly point to WPF `Grid`/`DataGrid` layout activity while ParcelWorkflowAddIn and TR `100000755` data were loaded. The current transaction artifacts show the immediate workflow state was weak extraction review (`2 of 4` rows with invalid coordinates), not Innola attachment or the earlier coordinate-system extraction bug.

## Recommended Next Steps

### Fix direction

Add defensive diagnostics and guards in the extraction-review UI refresh path before changing business logic. The best first patch is to capture dispatcher/UI exceptions and persist the active workspace, stage, selected tab/control, row counts, coordinate validity counts, and current row/finding identifiers, then harden any grid column sizing or value conversion that depends on nullable/blank review data.

### Diagnostic

1. Decode the dump with WinDbg/SOS if available.
2. Reopen TR `100000755` using the current case folder artifacts and try to render the review workspace without re-running extraction.
3. If it reproduces, capture the control/tab active at crash time and compare against dump hints: `Party / Owner`, review grids, and extraction row grids.

## Reproduction Plan

1. Use case folder `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000755`.
2. Confirm `working\extraction_review_data.json` has rows `152`, `32`, `IP1`, `IP2`, with blank coordinates on `IP1` and `IP2`.
3. Open Parcel Workflow for TR `100000755`.
4. Restore or navigate to the review-pending/georeference/validate-points path that renders extraction review data.
5. Expected if reproduced: ArcGIS Pro throws/terminates during WPF layout/render after review data is loaded.
6. Expected after diagnostic hardening: no host crash; a safe add-in error/log identifies the offending control/value if rendering still fails.

## Side Findings

- The ESRI ErrorReports directory can store dumps directly as `ArcGISPro_13.6.0.59527_...dmp`; the path shown/typed with an `ArcGISPro` subfolder was not valid on disk.
- The older `_100000755` case folder contains a successful complete/attach/writeback run and is not the current crash timeline.

## Follow-up: 2026-08-28

### New Evidence

- Diagnostic-only patch added after the dump review:
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ReviewWorkspaceDiagnostics.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceWindow.xaml`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/JamaicaReviewWorkspaceViewModel.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/JamaicaReviewWorkspaceXamlTests.cs`
  - `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Program.cs`
- Release build passed after the patch. Debug build could not be used because the existing `obj\Debug\net8.0-windows` path denied writes.
- Focused tests passed: `pxa review xaml uses tab scoped commands`, `pxa review xaml exposes memorandum rule groups`, and `points validation diagnostics capture wpf context`.

### Additional Findings

- The patch writes `review_workspace_diagnostics.jsonl` to LocalAppData and to the active case folder `working` directory when a case is loaded.
- The log captures active stage, workflow state, selected tab, last selected grid context, viewer state, row counts, invalid-coordinate row count, selected row, segment counts, metadata counts, party/owner counts, and memorandum rule context.
- The review window now listens for WPF binding traces while open and records dispatcher unhandled exceptions. WPF `ArgumentException` / `InvalidOperationException` crashes from `Grid`, `DataGrid`, `ContextLayoutManager`, or `System.Windows.Data` are logged and handled by closing the Points Validation Tool window instead of allowing ArcGIS Pro to crash.

### Updated Hypotheses

- Hypothesis 1 remains Open. The next repro should produce structured diagnostics before the host crashes or instead close only the review window.
- Hypothesis 2 remains Open. The log now records `invalid_coordinate_row_count` and selected row details to confirm or refute whether the weak `IP1`/`IP2` rows are involved.

### Backlog Changes

- Done: Add defensive UI exception logging/guards around the extraction-review workspace refresh and WPF grid rendering path.
- Open: Reproduce TR `100000755` and inspect `review_workspace_diagnostics.jsonl`.
- Open: If diagnostics identify a specific control/value, patch the rendering or binding path directly.

### Updated Conclusion

The repository now has a diagnostics-only guard for the suspected WPF review rendering crash. The next run should produce actionable JSONL context in the active case folder even if the exact managed stack is still unavailable from WinDbg/SOS.
