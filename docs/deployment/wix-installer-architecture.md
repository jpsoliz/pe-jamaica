# WiX Installer Architecture

Owner lens: Winston / System Architect
Date: 2026-07-25

## Decision

Use WiX Toolset for the Windows installer and keep the current MSBuild/PowerShell scripts as the build and staging pipeline.

Recommended implementation:

- Build payload with existing scripts.
- Package installed files with a WiX MSI.
- Add a WiX Burn bootstrapper EXE from the start for prerequisite detection, repeated upgrades, Python environment setup, and operator-friendly logging.

## Why MSI Alone Is Not Enough

The add-in payload is simple to install, but the Python environment is not. Conda clone and package installation can be slow, version-sensitive, and dependent on the target computer's ArcGIS Pro installation. That work needs explicit checks, progress, logging, and readable failure messages.

Use the MSI for deterministic file installation. Use Burn and scripts for orchestration.

Because this product will have multiple installations over time, the recommended first installer artifact is the Burn EXE plus MSI chain. MSI-only with script fallback is acceptable for early developer smoke tests, but production deployment should start with Burn so upgrades, prerequisite checks, Python setup, and failure messages are handled consistently.

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
  1. Install MSI payload to C:\Sidwell\ParcelWorkflow.
  2. Create ProgramData diagnostic log folder.
  3. Detect ArcGIS Pro 3.7.
  4. Locate ArcGIS Pro conda/propy tooling.
  5. Create or validate arcgispro-survey-ai clone under the ArcGIS Pro Python envs folder.
  6. Install requirements-conda.txt.
  7. Install requirements-pip.txt.
  8. Verify arcpy and AI Survey imports.
  9. Configure add-in package settings with target paths.
  10. Register/open the configured add-in package.
  11. Write install logs.
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

The Python environment must be created by ArcGIS Pro conda under the ArcGIS Pro Python environment location, not copied from source control. The add-in setting should point to the clone's `python.exe`.

Example:

```text
C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-survey-ai\python.exe
```

If that environment already exists, the bootstrapper should run dependency and import verification before reusing it. If it is missing, the bootstrapper should clone it from the target machine's `arcgispro-py3`.

The OpenAI API key is not part of the installer payload. The add-in configuration stores the environment variable name, `OPENAI_API_KEY`, and runtime extraction reads the key from the target machine environment. The current standard Burn UI can accept an `OpenAiApiKey` variable for test installs, while a production installer UI should request the key with a masked field. The implementation must avoid writing the secret value into support logs or packaged settings.

## Installer UI Decision

The current WiX Standard Bootstrapper Application is enough for a simple install button and logging, but it is not a good fit for custom folder review plus masked API-key input. The recommended production path is a custom Burn Bootstrapper Application that:

- Shows the default install root, detected ArcGIS Pro root, target Python environment path, and diagnostic log folder.
- Lets the operator confirm or override approved paths before MSI execution.
- Provides a masked OpenAI API key input and stores only the configured secret/environment variable value outside logs.
- Passes non-secret properties to the MSI and scripts.
- Redacts sensitive values from all Burn, MSI, and support diagnostics.

## Install Scope

Use a mixed install model:

- Per-machine MSI payload under `C:\Sidwell\ParcelWorkflow`.
- Preserved machine-level folders for `ParcelWorkflowCases` and `logs`.
- Per-user ArcGIS Pro add-in registration/launch because ArcGIS Pro installs add-ins into the active user's profile.

The bootstrapper should make the scope visible in logs because an administrator may install files while the ArcGIS Pro user still needs the add-in registered in their own profile.

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

## Code Signing

Unsigned MSI/EXE builds are acceptable for developer and internal test builds. Production or broadly distributed installers should be signed.

Reasons to sign production installers:

- Reduce Windows SmartScreen and endpoint security warnings.
- Improve IT trust and deployment auditability.
- Make repeated upgrades less fragile in managed environments.

The build should support an optional signing step rather than requiring a certificate for every local developer build.

## Decisions

- Confirm the exact ArcGIS Pro 3.7 detection mechanism: registry, installed path, or `ArcGISPro.exe` file version.
- Python clone location: under the ArcGIS Pro envs folder, beside `arcgispro-py3`.
- First production artifact: Burn EXE plus MSI chain.
- Install scope: mixed, with per-machine payload and per-user ArcGIS Pro add-in registration.
- Code signing: optional for developer/test builds, strongly recommended for production distribution.
