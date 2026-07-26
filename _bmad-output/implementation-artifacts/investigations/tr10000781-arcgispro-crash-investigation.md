# Investigation: TR10000781 ArcGIS Pro Crash

## Hand-off Brief

1. **What happened.** User reported that ArcGIS Pro crashed while inspecting transaction `TR10000781`; no matching local transaction artifact or ArcGISPro crash event is available on this workstation.
2. **Where the case stands.** Evidence is partial: repo source is available, Windows Application events here show earlier test-host crashes but no `ArcGISPro.exe` crash, and local case/log folders do not contain `TR10000781`.
3. **What's needed next.** Harden the newest supporting-documents UI path and collect the target machine Windows Application crash event if the crash repeats.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR10000781 |
| Date opened | 2026-07-26 |
| Status | Active |
| System | Windows / ArcGIS Pro add-in repo inspection |
| Evidence sources | Repo source, local Windows Application event log, local Sidwell case/log folder searches |

## Problem Statement

User reported: "inspect repo for TR10000781. the arcgispro crashed".

## Evidence Inventory

| Source | Status | Notes |
| --- | --- | --- |
| Repository search | Available | No matches for `TR10000781`, `10000781`, or `100000781` were found. |
| Local case/log folders | Missing | Searches under `C:\Sidwell` and `C:\ProgramData\Sidwell` found no `781` case/log artifacts. |
| Windows Application event log | Partial | No `ArcGISPro.exe` crash found on this workstation in the recent window; earlier `.NET Runtime` crashes were test-host failures. |
| Supporting Documents code path | Available | New dockpane activation and WebView2 preview path exists in the repo. |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --- | --- | --- | --- |
| 1 | Target machine Windows Application event entries for `ArcGISPro.exe` | High | Open | Needed to confirm the actual crash module and stack. |
| 2 | Case folder for `TR10000781` from the machine that crashed | High | Open | Needed to confirm whether a specific document/path triggered preview. |
| 3 | Supporting Documents dockpane error handling | High | In Progress | Most recent UI path that auto-opens when a transaction is loaded. |

## Confirmed Findings

### Finding 1: No Local Transaction Artifact

**Evidence:** `rg` for `TR10000781`, `10000781`, and `100000781` returned no repo matches; local `C:\Sidwell` / `C:\ProgramData\Sidwell` searches returned no matching artifacts.

**Detail:** The transaction data needed to reproduce the crash is not present in this workspace.

### Finding 2: Supporting Documents Dockpane Auto-Activates With Workflow Pane

**Evidence:** `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\TransactionPanelState.cs:787`

**Detail:** Loading/opening the parcel workflow pane also activates the Supporting Documents dockpane.

### Finding 3: WebView2 Initialization Is Partially Guarded

**Evidence:** `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\SupportingDocumentsDockpane.xaml.cs:102` and `src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\SupportingDocumentsDockpane.xaml.cs:109`

**Detail:** The WebView2 initialization call is inside a filtered exception handler, but the async WPF event handlers need top-level protection so no preview failure can escape the UI event boundary.

## Hypothesized Paths

### Hypothesis 1: Supporting Documents Preview Exception Escaped UI Event Handling

**Status:** Open

**Theory:** Auto-opening the new Supporting Documents tab encountered a document/WebView2/path condition that escaped the async event handler and crashed ArcGIS Pro.

**Supporting indicators:** The new dockpane is activated automatically when the transaction pane opens, and the user's crash report followed story 9-2 work.

**Would confirm:** A target-machine Application event showing `ArcGISPro.exe` / `.NET Runtime` with a WebView2, XAML, Uri, file, or SupportingDocuments stack.

**Would refute:** A crash event pointing to unrelated native ArcGIS, Python, licensing, or another add-in module.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | --- | --- |
| Actual crash event | Cannot confirm root cause | Run the Event Log command on the machine that crashed and share the ArcGISPro/.NET Runtime entries around the crash time. |
| TR10000781 case folder | Cannot reproduce data-specific failure | Copy or inspect `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases\...781...` from the crash machine. |

## Source Code Trace

| Element | Detail |
| --- | --- |
| Error origin | Open: likely new supporting-docs dockpane preview path until crash log proves otherwise. |
| Trigger | Transaction load/open activates parcel workflow dockpane and Supporting Documents dockpane. |
| Condition | Missing evidence; suspected document preview or path/render failure. |
| Related files | `SupportingDocumentsDockpane.xaml.cs`, `SupportingDocumentsDockpaneViewModel.cs`, `TransactionPanelState.cs` |

## Conclusion

**Confidence:** Low

The repository does not contain transaction-specific evidence for `TR10000781`, and this workstation has no ArcGISPro crash event for the reported crash. The most plausible source-code risk is the newly auto-activated Supporting Documents dockpane preview path, so the next engineering action is to harden that path and then collect the target-machine crash event if ArcGIS Pro still exits.

## Follow-up: 2026-07-26

### New Evidence

The user provided pasted Windows Application event text from `2026-07-26 01:12:19`, `01:11:50`, `01:11:02`, and `00:23:54`. All four `.NET Runtime` error entries identify the faulting application as `ParcelWorkflowAddIn.Tests.exe`, not `ArcGISPro.exe`.

### Additional Findings

The pasted events are local automated test failures from earlier development runs:

- `01:12:19`: test host failed loading `ArcGIS.Desktop.Framework` from `TransactionPanelState.AddDocumentsToLoadedTransaction`.
- `01:11:50`: test host failed restoring a resume package due to temp-folder access denial.
- `01:11:02`: test assertion failed around supporting-document ordering.
- `00:23:54`: test host could not locate `SupportingDocumentWorkspaceProjection.cs`.

These entries do not prove an ArcGIS Pro production crash. They are consistent with known test-run failures that were subsequently addressed and followed by a passing 509-test harness run.

### Updated Conclusion

Confidence remains **Low** for the real ArcGIS Pro crash root cause because the provided evidence is not from ArcGIS Pro. The strongest current conclusion is that the attached event log is unrelated to the user's reported `TR10000781` ArcGIS Pro crash, except that it points to nearby supporting-documents code that has now been hardened.

## Follow-up: 2026-07-26 #2

### New Evidence

The user provided another pasted Application event export. It repeats the same `ParcelWorkflowAddIn.Tests.exe` failures and adds Windows Error Reporting / Application Error entries for `svchost.exe_UserDataSvc` faulting in `ESENT.dll`, plus Windows Store update scan failures.

### Additional Findings

The additional events still do not include `ArcGISPro.exe`, `ParcelWorkflowAddIn` loaded inside ArcGIS Pro, or a WebView2/ArcGIS add-in fault. The `svchost.exe_UserDataSvc` / `ESENT.dll` crash is a Windows service crash, not the Sidwell add-in or ArcGIS Pro.

### Updated Conclusion

The latest attached log does not identify the reported ArcGIS Pro crash. The real blocker remains missing evidence from the target event where the faulting application is `ArcGISPro.exe`.

## Follow-up: 2026-07-26 #3

### New Evidence

The user provided the ESRI ArcGIS Pro dump path:

`C:\Users\js91482\AppData\Local\ESRI\ErrorReports\ArcGISPro_13.6.0.59527_0_07_26_2026_02_01_52.dmp`

The dump exists and is about 10 MB. A readable string scan found:

- `Last command`: `ParcelWorkflow_LoginButton`
- `Last active dockpane`: `ParcelWorkflow_SupportingDocumentsDockpane`
- `WPF.FocusedElementDataCtx`: `ParcelWorkflowAddIn.ParcelWorkflowDockpaneViewModel`
- WPF binding error: `A TwoWay or OneWayToSource binding cannot work on the read-only property 'SupportingDocumentTextContent' of type 'ParcelWorkflowAddIn.SupportingDocumentsDockpaneViewModel'.`

### Additional Findings

The binding error maps to `SupportingDocumentsDockpane.xaml`, where `TextBox.Text` was bound to the read-only `SupportingDocumentTextContent` property without `Mode=OneWay`. `TextBox.Text` defaults to two-way binding, so WPF rejected the read-only source property while the Supporting Documents dockpane was active.

### Resolution

Patched `SupportingDocumentsDockpane.xaml` so `SupportingDocumentTextContent` is bound with `Mode=OneWay`. The add-in Release build passed, the test project Release build passed, and the full test harness passed 509 tests.

### Updated Conclusion

Confidence is now **Medium-High** that the crash was caused by the new Supporting Documents dockpane XAML binding trying to write back to a read-only view-model property. The next verification step is to package/reinstall the add-in and reproduce the login/transaction flow that previously created the ArcGIS Pro dump.
