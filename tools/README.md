# Tools - ArcGIS Add-In Workflow

## Purpose
The `tools/` scripts support validation, testing, and packaging for the Sidwell ArcGIS Pro add-in.

## One-command full-cycle readiness

From repository root:

```powershell
.\tools\run_arcgis_addin_readiness.ps1
```

This runs, in order:

1. `validate_contracts.ps1`
2. `run_python_tests.ps1`
3. `dotnet build` (no restore)
4. `dotnet run` on the test harness project
5. `package_addin.ps1` (builds `.esriAddInX`, unless skipped)

### Environment blocker: Microsoft SDKs path denied

If readiness stops on:

`Access to the path 'C:\Users\<user>\AppData\Local\Microsoft SDKs' is denied.`

run in elevated PowerShell:

```powershell
$path = "C:\Users\$env:USERNAME\AppData\Local\Microsoft SDKs"
takeown /f $path /r /d y
icacls $path /grant "$($env:USERDOMAIN)\$($env:USERNAME):(OI)(CI)F" /T
```

Then rerun:

```powershell
.\tools\run_arcgis_addin_readiness.ps1 -Configuration Debug
```

## Useful command variants

- Run with Release configuration:
  ```powershell
  .\tools\run_arcgis_addin_readiness.ps1 -Configuration Release
  ```

- Skip add-in packaging (faster iteration):
  ```powershell
  .\tools\run_arcgis_addin_readiness.ps1 -SkipPackage
  ```

- Dry-run mode (`-WhatIf`):
  ```powershell
  .\tools\run_arcgis_addin_readiness.ps1 -WhatIf
  ```
  Shows all steps without executing any command.

## Individual scripts

- `tools\validate_contracts.ps1`  
  Validates scaffold contracts, required files, ArcGIS target settings, and schema requirements.

- `tools\run_python_tests.ps1`  
  Runs Python tests using the configured ArcGIS Python environment.

- `tools\package_addin.ps1`  
  Performs add-in package build (`.esriAddInX`) via ArcGIS-compatible MSBuild target flow.

- `tools\run_arcgis_addin_readiness.ps1`  
  Orchestrates the full readiness pipeline above and writes a timestamped temp log.

## Prerequisites

- ArcGIS Pro installed and accessible (SDK assemblies/targets used by build scripts).
- ArcGIS-compatible MSBuild from Visual Studio 2022.
- Python executable path exists:
  - `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe`
  (adjust in scripts if your local path differs).
- Permissions for ArcGIS/SDK metadata locations and temp directories.

## Notes

- Dry-run mode does not alter files or call external tools.
- Full-cycle readiness produces logs under `%TEMP%`:
  `pe-jamaica-readiness-YYYYMMDD-HHMMSS.log`.
