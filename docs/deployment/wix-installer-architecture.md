# WiX Installer Architecture

Owner lens: Winston / System Architect
Date: 2026-07-25

## Decision

Use WiX Toolset for the Windows installer and keep the current MSBuild/PowerShell scripts as the build and staging pipeline.

Recommended implementation:

- Build payload with existing scripts.
- Package installed files with a WiX MSI.
- Add a WiX Burn bootstrapper EXE for prerequisite detection and Python environment setup.

## Why MSI Alone Is Not Enough

The add-in payload is simple to install, but the Python environment is not. Conda clone and package installation can be slow, version-sensitive, and dependent on the target computer's ArcGIS Pro installation. That work needs explicit checks, progress, logging, and readable failure messages.

Use the MSI for deterministic file installation. Use Burn and scripts for orchestration.

## Build-Time Flow

```text
tools/package_addin.ps1
  -> builds ParcelWorkflowAddIn.esriAddInX

tools/stage_target_deployment.ps1
  -> stages package folder
  -> includes add-in, ProcessingTools, Contracts, scripts, requirements

installer/ParcelWorkflowInstaller.wixproj
  -> builds MSI from staged payload

installer/ParcelWorkflowBootstrapper.wixproj
  -> builds EXE bootstrapper that chains MSI and Python setup checks
```

## Target-Time Flow

```text
ParcelWorkflowSetup.exe
  1. Detect ArcGIS Pro 3.7.
  2. Locate ArcGIS Pro conda/propy tooling.
  3. Create or validate arcgispro-survey-ai clone.
  4. Install requirements-conda.txt.
  5. Install requirements-pip.txt.
  6. Verify arcpy and AI Survey imports.
  7. Install MSI payload to C:\Sidwell\ParcelWorkflow.
  8. Configure add-in package settings with target paths.
  9. Register/open the configured add-in package.
  10. Write install logs.
```

## Installed Layout

```text
C:\Sidwell\ParcelWorkflow\
  AddIn\
    ParcelWorkflowAddIn.configured.esriAddInX
  Contracts\
  ProcessingTools\
  ParcelWorkflowCases\
  logs\
  scripts\
  installer\
    requirements-conda.txt
    requirements-pip.txt
```

The Python environment should be created by ArcGIS Pro conda under the ArcGIS Pro Python environment location, not copied from source control. The add-in setting should point to the clone's `python.exe`.

## WiX Environment Needed On Build Machine

Install the WiX .NET tool:

```powershell
dotnet tool install --global wix
wix --version
```

Install WiX extensions as needed during project setup. Expected candidates:

- `WixToolset.Util.wixext` for utility/custom-action support.
- `WixToolset.BootstrapperApplications.wixext` for Burn bootstrapper UI.

## Implementation Boundaries

- Do not bundle or copy the old ArcGIS Pro 3.6 `python-env`.
- Do not install Python packages into `arcgispro-py3`.
- Do not hide Python environment errors inside a generic MSI failure.
- Do not delete `ParcelWorkflowCases` or `logs` during upgrade/uninstall by default.
- Keep current `install_target_tools.ps1` behavior available as a support fallback until the MSI/Burn path is proven.

## Open Technical Questions

- Confirm the exact ArcGIS Pro 3.7 detection mechanism: registry, installed path, or `ArcGISPro.exe` file version.
- Confirm whether the Python clone should live under the ArcGIS Pro envs folder or `C:\Sidwell\ParcelWorkflow\python-env`.
- Confirm if the installer must run per-user or per-machine. ArcGIS Pro add-ins are typically per-user, while `C:\Sidwell` may require elevated write access.
- Confirm code-signing requirements for MSI/EXE in the target environment.

