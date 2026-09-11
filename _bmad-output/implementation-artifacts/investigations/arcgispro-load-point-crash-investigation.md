# Investigation: ArcGIS Pro Load Point Crash

## Hand-off Brief

1. **What happened.** Confirmed: ArcGIS Pro crashed while the add-in was in extraction/reprocess flow because a managed `System.ComponentModel.Win32Exception (5)` escaped when starting the configured Python executable.
2. **Where the case stands.** Confirmed dump evidence identifies Windows error 5, `Access is denied`, for `C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai\python.exe`; root environmental permission/policy cause remains outside the dump.
3. **What's needed next.** Check target-machine ACL/AppLocker/endpoint-security policy for that Python path and add add-in handling around process startup so this reports as an extraction failure instead of crashing Pro.

## Case Info

| Field | Value |
| - | - |
| Ticket | N/A |
| Date opened | 2026-09-10 |
| Status | Concluded on dump evidence |
| System | ArcGIS Pro process under `C:\Users\etitles.trainee8`; .NET runtime `Microsoft.NETCore.App\10.0.12`; add-in loaded from ArcGIS Pro AssemblyCache |
| Evidence sources | `C:\Users\js91482\Desktop\RC2_add-in JAMAICA\Dump\ArcGISPro_13.7.1.1904_0_09_10_2026_07_18_51.dmp`; local source tree |

## Problem Statement

User reports ArcGIS Pro crashes when trying to load points in the tool.

## Confirmed Findings

### Finding 1: The dump is a managed .NET exception

**Evidence:** Minidump exception stream shows exception code `0xe0434352`, exception thread `17124`, fault address in `C:\Windows\System32\KERNELBASE.dll`, and parameter `0xffffffff80004005`.

**Detail:** `0xe0434352` is the standard CLR managed-exception code. `ParcelWorkflowAddIn.dll` was loaded from `C:\Users\etitles.trainee8\AppData\Local\ESRI\ArcGISPro\AssemblyCache\{7C3FB44F-F7D4-41AB-B51D-92EFCDB2E4AF}\ParcelWorkflowAddIn.dll`.

### Finding 2: Windows denied process startup for the configured Python executable

**Evidence:** Dump string stream contains `Exception Info: System.ComponentModel.Win32Exception (5): An error occurred trying to start process 'C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai\python.exe' with working directory 'C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai'. Access is denied.`

**Detail:** The dump also records the focused WPF element as `System.Windows.Controls.Button: Re-process extraction`.

### Finding 3: The add-in starts Python without catching startup exceptions

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ProcessRunner.cs:46` calls `process.Start()` directly.

**Detail:** Extraction paths call `ProcessRunner.RunAsync` after only checking that the Python executable path exists, e.g. `CreateParcelDraftExtractionAdapter.cs:376` and `CreateParcelDraftExtractionAdapter.cs:392`.

## Deduced Conclusions

### Deduction 1: This is not a bad point coordinate; it is a Python launch failure

**Based on:** Findings 1-3.

**Reasoning:** The active UI action was reprocessing extraction; the stack strings name `ProcessRunner.RunAsync`, `CreateParcelDraftExtractionAdapter`, `WorkflowSession.RunDraftExtractionInternalAsync`, and `ParcelWorkflowDockpaneViewModel.RunOrOpenExtractionReviewAsync`. The exception message is from Windows process creation, before point parsing can complete.

**Conclusion:** The crash happens while the add-in tries to launch the extraction Python environment used to load/reprocess point evidence.

## Conclusion

**Confidence:** High.

The dump confirms an unhandled managed `Win32Exception (5)` caused by Windows denying startup of `C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai\python.exe`. The most likely operational cause is permission or endpoint-policy blocking on the deployed Python environment. The add-in should also catch startup exceptions in `ProcessRunner` and convert them to a failed extraction result so ArcGIS Pro does not crash.

## Recommended Next Steps

### Fix direction

1. On the target machine, verify the configured Python path exists and can be executed by the same Windows user running ArcGIS Pro.
2. Check ACLs, AppLocker/Defender/CheckPoint endpoint policy, and whether the copied env folder has executable restrictions.
3. In code, wrap `process.Start()` in `ProcessRunner.RunAsync` and return a nonzero `ProcessRunResult` with a sanitized error message when startup fails.

### Diagnostic

Run this as the target user on the target machine:

```powershell
& 'C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai\python.exe' --version
```

If that returns `Access is denied`, the environment/policy issue is confirmed outside ArcGIS Pro.

