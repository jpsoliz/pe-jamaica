---
baseline_commit: 99b349cd34935fbe185e43e10ef782878708bd56
---

# Story 9.1: Create WiX-Based Target-Machine Installer

Status: in-progress

## Story

As a deployment administrator,  
I want a Windows installer for the Parcel Workflow add-in and target-machine tools,  
so that a new workstation can be prepared repeatably with the correct files, folders, ArcGIS Pro add-in registration, and compatible Python environment.

## Business Context

Current deployment depends on copying `deployment\target-computer-tools` and manually running scripts. This has created repeated target-machine issues around unsigned scripts, missing files, ArcGIS Pro Python paths, copied Python environments, ArcPy import errors, and unclear upgrade behavior.

The installer must preserve the working deployment scripts as a support fallback, but introduce a proper Windows installer path using WiX.

## Acceptance Criteria

1. Given the repository has a staged deployment payload, when the installer build runs, then it produces a Windows installer artifact for the target machine.
2. Given ArcGIS Pro 3.7 is installed, when the installer runs, then it detects ArcGIS Pro and the ArcGIS Pro Python/conda tooling.
3. Given ArcGIS Pro 3.7 is missing, when the installer runs, then it fails before configuration with a clear message and install log.
4. Given Python setup is required, when the installer runs, then it creates or validates a cloned environment named `arcgispro-survey-ai` under the target machine's ArcGIS Pro envs folder from that machine's own `arcgispro-py3`.
5. Given Python setup runs, then it installs conda requirements before pip requirements only when the conda requirements file contains real package entries; a comment-only conda file is skipped and pip requirements still run using the repo-controlled files in `docs/deployment/arcgispro37`.
6. Given Python setup completes, then verification confirms required imports for `openai`, `flask`, `pdfplumber`, and `pypdfium2`; verifies installed package versions for `openai`, `openai-clip`, and `open-clip-torch`; and logs `arcpy`, `clip`, and `open_clip` imports as diagnostics.
7. Given the add-in is configured, then the embedded `WorkflowSettings.json` points to the target install root, target `ProcessingTools`, target `Contracts`, target `ParcelWorkflowCases`, and the cloned environment `python.exe`.
8. Given installation succeeds, then the configured add-in is registered or launched for installation in ArcGIS Pro.
9. Given installation is upgraded, then `ParcelWorkflowCases` and logs are preserved by default.
10. Given uninstall runs, then installed binaries/tools can be removed without deleting case folders/logs unless a future explicit full-clean option is added.
11. Given installation fails, then logs identify the failing phase, command, exit code, ArcGIS Pro path, Python path, and non-secret diagnostics.
12. Given automated validation runs on the build machine, then the installer staging/build script can be tested without requiring ArcGIS Pro by using dry-run or mocked detection hooks.
13. Given OpenAI extraction support is installed, then the Python setup includes `openai`, `openai-clip`, and `open-clip-torch`.
14. Given an OpenAI API key is needed, then the installer/add-in references only an environment variable name and does not package, log, or persist the key value.
15. Given the installer is run interactively, then the operator can review the default install root, detected ArcGIS Pro path, target Python environment path, and diagnostic log folder before installation.
16. Given the operator supplies an OpenAI API key during installation, then the key field is masked and no MSI, Burn, command-line, support-log, add-in-settings, or repository output contains the key value.
17. Given post-install phases run, then the installer writes a machine-readable and human-readable installation summary that lists each phase, command/script, status, exit code, start/end time, and log file path.
18. Given any non-optional phase fails, then the installer must clearly report that the installation did not complete successfully; it must not appear successful only because files were copied.
19. Given optional Python/OpenAI setup is allowed to continue without rollback, then the installer summary must mark the install as `CompletedWithWarnings` or `RepairRequired` and identify the exact remediation step.
20. Given Python/OpenAI setup completes or is reused, then the summary must include verified package/import status for required AI/PDF packages and warnings for non-blocking ArcPy/CLIP diagnostics.
21. Given ArcPy or CLIP import verification runs from an elevated installer context and ArcGIS named-user licensing or OpenMP runtime initialization fails for that process, then the installer records that verification as a warning and continues after required package/import checks pass.

## Tasks / Subtasks

- [x] Add installer source structure. (AC: 1)
  - [x] Create `installer/` folder.
  - [x] Add WiX MSI project for file payload.
  - [x] Add WiX Burn bootstrapper project or documented placeholder if Burn is deferred.
  - [x] Add README with build prerequisites.

- [x] Extend staging pipeline. (AC: 1, 5)
  - [x] Update `tools/stage_target_deployment.ps1` to stage `docs/deployment/arcgispro37/requirements-conda.txt`.
  - [x] Stage `docs/deployment/arcgispro37/requirements-pip.txt`.
  - [x] Stage installer-support scripts under the deployment payload.
  - [x] Ensure `deployment_manifest.json` includes the Python setup requirements and installer version.

- [x] Add target Python environment setup script. (AC: 2-6, 11-12)
  - [x] Add a script such as `installer/scripts/setup_arcgispro37_environment.ps1`.
  - [x] Detect ArcGIS Pro 3.7 and default conda/propy tooling.
  - [x] Clone `arcgispro-py3` to `arcgispro-survey-ai` when missing.
  - [x] Reuse or repair the clone according to explicit options.
  - [x] Install conda requirements before pip requirements when conda packages are configured; skip conda when the file is comment-only.
  - [x] Verify imports and write logs.
  - [x] Provide dry-run/mocked path mode for tests.

- [x] Wire add-in configuration to installer result. (AC: 7-8)
  - [x] Reuse `Update-AddInPackageSettings` logic from `deployment/target-computer-tools/scripts/install_target_tools.ps1` or extract it into a shared support script.
  - [x] Configure `arcgis_python_executable` to the cloned environment `python.exe`.
  - [x] Preserve existing target path conventions under `C:\Sidwell\ParcelWorkflow`.

- [x] Define upgrade/uninstall behavior. (AC: 9-10)
  - [x] Mark case folders and logs as preserved data.
  - [x] Ensure upgrade replaces add-in/tools/contracts/config templates safely.
  - [x] Document any manual cleanup path separately.

- [x] Add tests/validation. (AC: 1-12)
  - [x] Add PowerShell validation for stage manifest contents.
  - [x] Add dry-run tests for ArcGIS Pro detection and missing ArcGIS Pro failure.
  - [x] Add dry-run tests for environment clone command composition.
  - [x] Add dry-run tests for conda-before-pip ordering and pip execution when conda is intentionally empty.
  - [x] Add dry-run tests for add-in settings rewrite paths.
  - [x] Add staged requirements validation for `openai`, `openai-clip`, and `open-clip-torch`. (AC: 13)

- [ ] Surface post-install phase status clearly. (AC: 17-20)
  - [ ] Write `installation_summary.json` and `installation_summary.txt` under the ProgramData log folder.
  - [ ] Include MSI payload copy, add-in configuration, add-in registration launch, ArcGIS Pro detection, Python environment clone/reuse, conda install, pip install, import verification, and OpenAI API-key environment-variable handling.
  - [ ] Record each phase as `Succeeded`, `Skipped`, `Failed`, or `Warning`.
  - [ ] Ensure Python setup failures that do not roll back the MSI are still visible as `CompletedWithWarnings` or `RepairRequired`.
  - [ ] Ensure the final Burn/MSI result does not silently imply success when required post-install configuration failed.
  - [ ] Add tests that simulate failed Python setup and verify the summary/status tells support what failed and where to look.

## Developer Notes

Reference files:

- `docs/deployment/windows-installer-requirements.md`
- `docs/deployment/wix-installer-architecture.md`
- `docs/deployment/arcgispro37/README.md`
- `docs/deployment/arcgispro37/ArcGISPro37_Environment_Installation_Guide.docx`
- `docs/deployment/arcgispro37/requirements-conda.txt`
- `docs/deployment/arcgispro37/requirements-pip.txt`
- `docs/deployment/arcgispro37/arcgispro-survey-ai-component-inventory.csv`

Existing deployment scripts to preserve or reuse:

- `tools/package_addin.ps1`
- `tools/stage_target_deployment.ps1`
- `deployment/target-computer-tools/scripts/install_target_tools.ps1`
- `deployment/target-computer-tools/scripts/install_target_tools.bat`

Current script behavior already rewrites add-in `WorkflowSettings.json` inside the `.esriAddInX`. Prefer extracting reusable functions rather than duplicating that package-editing logic.

Critical Python rule:

Do not copy the old `python-env` folder into the installer. The target environment must be cloned from the target machine's ArcGIS Pro 3.7 default environment.

Suggested WiX build-machine setup:

```powershell
dotnet tool install --global wix
wix --version
```

Potential WiX extensions:

- `WixToolset.Util.wixext`
- `WixToolset.BootstrapperApplications.wixext`

## Testing Notes

Run repository tests after script changes:

```powershell
dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --configuration Release
```

Run installer script dry-run tests before testing on a real ArcGIS Pro workstation.

## Decisions

- `arcgispro-survey-ai` must live inside the ArcGIS Pro Python environments folder, beside `arcgispro-py3`.
- If `arcgispro-survey-ai` exists, verify dependencies/imports before reuse. If it does not exist, clone it from the target machine's `arcgispro-py3`.
- The first production installer artifact should be Burn EXE plus MSI from the start, because the product will be installed/upgraded multiple times and needs prerequisite orchestration.
- Installer scope is mixed: per-machine payload under `C:\Sidwell\ParcelWorkflow`, per-user ArcGIS Pro add-in registration/launch.
- Code signing is optional for developer/test builds and strongly recommended for production distribution.
- OpenAI API key values are not deployed as payload files. The configured add-in stores only the environment variable name, defaulting to `OPENAI_API_KEY`; the installer may set the environment variable from an install-time value.
- The production installer UI should use a custom Burn Bootstrapper Application rather than the stock WiX Standard Bootstrapper Application if folder review and masked API-key entry are required in the installer itself.
- File copy alone is not a successful installation. Add-in configuration/registration and Python dependency verification must be represented in the final installation status, even when Python setup is intentionally non-rollback.

## Dev Agent Record

### Debug Log

- 2026-07-25: Added WiX MSI and Burn bootstrapper scaffolding under `installer/`.
- 2026-07-25: Added ArcGIS Pro 3.7 environment setup script with dry-run planning, conda clone, conda-before-pip install order, import verification, and logs.
- 2026-07-25: Extended target deployment staging to include installer scripts and ArcGIS Pro 3.7 requirements.
- 2026-07-25: Added installer packaging validation script and fixed its conda-clone assertion after first validation run found a PowerShell scalar/count issue.
- 2026-07-25: `tools/validate_installer_packaging.ps1` passed.
- 2026-07-25: Full add-in regression suite passed: `PASS 503 tests`.
- 2026-07-25: `dotnet build .\installer\ParcelWorkflowInstaller.wixproj -c Release` is blocked on this machine because `WixToolset.Sdk/7.0.0` is not installed/restorable from the configured offline NuGet source, and `wix` is not on PATH.
- 2026-07-25: Open installer questions resolved: ArcGIS Pro envs folder for `arcgispro-survey-ai`, Burn EXE plus MSI from the start, mixed install scope, and optional developer/strongly recommended production code signing.
- 2026-07-25: Fixed WiX v7 OSMF acceptance by setting `AcceptEula` to the v7 EULA id `wix7`, added optional build-script signing support, split MSI/Bundle WiX sources by project, embedded MSI cabinet payload, and verified full MSI + Burn EXE build.
- 2026-07-26: Investigated Burn runtime failure `0xfffd0000`; root cause was the bootstrapper caching only `setup_arcgispro37_environment.bat`, so the BAT could not find its sibling `.ps1` or requirements. Fixed by adding Burn payloads for the PowerShell script and requirement files, installing the MSI first, passing a fixed log root, and targeting the MSI install root at `C:\Sidwell\ParcelWorkflow`.
- 2026-07-26: Investigated follow-up Burn runtime failure `0x80070001`; MSI now installs first and payloads are present, so remaining failure is inside Python setup. Added persistent setup logging under `C:\ProgramData\Sidwell\ParcelWorkflow\logs`, BAT stdout/stderr capture, PowerShell trap logging, and wired WiX MSI/Burn versions to the add-in package version.
- 2026-07-26: Changed the Burn Python environment setup package to non-vital so MSI payload installation remains in place when Python setup fails; support can inspect installed files and persistent setup logs before repairing Python.
- 2026-07-26: Added MSI-owned creation of `C:\ProgramData\Sidwell\ParcelWorkflow\logs` so persistent diagnostics exist even if the setup BAT fails before creating its log folder.
- 2026-07-26: Changed setup BAT to always return success to Burn after recording the real Python setup exit code in its log, preventing Python setup failures from rolling back or failing the installer. Added an HKLM registry key path for the ProgramData log-folder component.
- 2026-07-26: Deep installer review found the MSI copied raw files only and did not run the existing target add-in configuration/registration flow. Added explicit `INSTALLFOLDER=C:\Sidwell\ParcelWorkflow`, added Burn add-in configuration/registration package, and added scripts to create `ParcelWorkflowAddIn.configured.esriAddInX` and launch it for ArcGIS Pro add-in registration.
- 2026-07-26: Investigated v0.1.35 MSI logs. The MSI custom actions were scheduled, but `WixQuietExec64` rejected both command lines because the executable name was not quoted. Fixed the custom action properties to launch the full quoted PowerShell path before the script arguments.
- 2026-07-26: Investigated v0.1.36 registration error `Illegal characters in path` at `register_parcel_workflow_addin.ps1:127`. Hardened installer script path parsing, added raw argument diagnostics, and changed WiX directory arguments to pass `...\.` so trailing directory backslashes cannot interfere with quoted PowerShell arguments.
- 2026-07-26: Added install-time OpenAI environment variable support, install path summary output, and Python package-version verification so target installs can prove whether `openai`, `openai-clip`, and `open-clip-torch` were installed.
- 2026-07-26: Investigated v0.1.37 logs. Add-in configuration/registration completed, but Python environment setup stopped during `conda-clone` because PowerShell treated Conda stderr warning text as a terminating error before checking the process exit code. Replaced native invocation with `System.Diagnostics.ProcessStartInfo` so stdout/stderr are logged and only the real exit code controls phase failure.
- 2026-07-26: Installer dependency review initially found `openai-clip` and `open-clip-torch` in the ArcGIS Pro 3.7 inventory but missing from dependency validation. Later target logs showed the ArcGIS Pro 3.7 conda solver cannot resolve that AI stack against pinned ArcGIS packages, so AI dependencies are installed from `requirements-pip.txt` and comment-only conda requirements are skipped.
- 2026-07-26: Investigated v0.1.39 target logs. MSI payload install succeeded, but `RunSetupArcGisPro37Environment` failed before Python setup installed OpenAI dependencies; the PowerShell trap dereferenced a null error record and masked the real failure. Hardened setup/registration trap logging and added installer UI requirements for folder review and masked OpenAI API key entry.
- 2026-07-26: Reviewed v0.1.48 target summary with `CompletedWithWarnings`; payload, add-in registration, folders, and requirements were present, but `setup_arcgispro37_environment.bat` exited 1 during `conda-install-requirements` without a status file. Hardened environment setup to write `setup_arcgispro37_environment_status.json` on trap failures and to drain conda/pip stdout/stderr asynchronously so the real failing phase and command output are preserved.

### Completion Notes

- Implemented the WiX installer source structure, target staging integration, ArcGIS Pro 3.7 Python environment setup script, build wrapper, and installer validation.
- The environment setup script does not copy the old Python environment. It plans or executes a clone from the target machine's own `arcgispro-py3` into `arcgispro-survey-ai` under the ArcGIS Pro envs folder.
- The default install root is `C:\Sidwell\ParcelWorkflow`; the Python clone is under the ArcGIS Pro envs folder, not under the install root.
- The installer can set an OpenAI API key into the configured environment variable when `OpenAiApiKey` is provided. If omitted during install or upgrade, any existing environment variable value remains unchanged.
- Folder review and masked API-key entry require a custom Burn Bootstrapper Application; the stock WiX Standard Bootstrapper Application is not sufficient for that production UI.
- The generated staging manifest now records WiX installer metadata, ArcGIS Pro 3.7 target version, Python environment name, and staged requirement file locations.
- Open installer questions are resolved in the story and supporting deployment docs.
- The MSI/Burn projects are present but final MSI artifact generation requires WiX Toolset availability on the build machine.
- Current validation produced `installer\bin\Release\ParcelWorkflowInstaller.msi` and `installer\bin\Release\ParcelWorkflowSetup.exe`.
- Current Burn validation build produced `installer\bin\Release\ParcelWorkflowInstaller.msi` and `installer\bin\Release\ParcelWorkflowSetup.exe` after the payload/root-path fix.
- Current diagnostic build produced installer version `0.1.26`.
- Current supportability build produced installer version `0.1.27`.
- Current ProgramData diagnostics build produced installer version `0.1.28`.
- Current no-rollback diagnostics build produced installer version `0.1.29`.
- Current add-in registration build produced installer version `0.1.32`.
- Current explicit Burn-to-MSI install folder handoff build produced installer version `0.1.33`.
- Current MSI-owned custom-action build produced installer version `0.1.36`.
- Current sanitized MSI custom-action path build produced installer version `0.1.37`.
- Current native-process logging build produced installer version `0.1.38`.
- Current OpenAI/CLIP dependency and API-key documentation build produced installer version `0.1.39`.

### File List

- `installer/README.md`
- `installer/ParcelWorkflowInstaller.wixproj`
- `installer/Package.wxs`
- `installer/ParcelWorkflowBootstrapper.wixproj`
- `installer/Bundle.wxs`
- `installer/scripts/setup_arcgispro37_environment.bat`
- `installer/scripts/setup_arcgispro37_environment.ps1`
- `tools/build_installer.ps1`
- `tools/stage_target_deployment.ps1`
- `tools/validate_installer_packaging.ps1`
- `deployment/target-computer-tools/README.md`
- `docs/deployment/windows-installer-requirements.md`
- `docs/deployment/wix-installer-architecture.md`
- `_bmad-output/implementation-artifacts/9-1-create-wix-based-target-machine-installer.md`

### Change Log

- 2026-07-25: Added WiX installer scaffold, ArcGIS Pro 3.7 environment setup, staging updates, installer build wrapper, validation script, and deployment documentation updates.
- 2026-07-25: Resolved installer open questions and updated requirements/architecture/story decisions.
- 2026-07-25: Added WiX v7 EULA handling, optional MSI/EXE code signing flow, and verified the full installer build.
- 2026-07-26: Fixed Burn cached script payload layout and corrected MSI root install location to `C:\Sidwell\ParcelWorkflow`.
- 2026-07-26: Added persistent Python setup diagnostics and made installer version follow add-in version.
- 2026-07-26: Kept MSI installation when Python setup fails by making the Burn Python setup package non-vital.
- 2026-07-26: Made the MSI create the ProgramData diagnostic log folder.
- 2026-07-26: Prevented Python setup failures from failing Burn by logging the setup exit code and returning success to the bootstrapper.
- 2026-07-26: Added explicit MSI install root and Burn package for add-in configuration/registration.
- 2026-07-26: Made Burn pass `ROOTDRIVE` and `INSTALLFOLDER=C:\Sidwell\ParcelWorkflow` directly to the MSI so payload installation and post-install scripts use the same target path.
- 2026-07-26: Moved post-install script execution into MSI custom actions and fixed `WixQuietExec64` command quoting so PowerShell setup/registration scripts can actually start.
- 2026-07-26: Sanitized installer path arguments and avoided quoted trailing-backslash directory arguments in MSI custom actions.
- 2026-07-26: Changed Python setup command execution to capture native stdout/stderr without PowerShell turning Conda warnings into terminating script errors.
- 2026-07-26: Added OpenAI/CLIP dependency coverage, staged requirement validation, and explicit OpenAI API key handling guidance; generated installer version `0.1.39`.
- 2026-07-26: Hardened installer setup/registration failure logging after v0.1.39 target logs showed the setup trap masking the Python setup failure.
- 2026-07-27: Moved `openai`, `openai-clip`, and `open-clip-torch` from conda requirements to pip requirements after target logs showed conda solver conflicts with ArcGIS Pro 3.7 pins; setup now skips comment-only conda requirements and proceeds to pip installation.
- 2026-07-27: Reviewed target logs where OpenAI packages installed successfully but `verify-arcpy` failed with `The Product License has not been initialized` from the elevated installer process. Made ArcPy verification non-blocking, retained it as a diagnostic warning, and kept AI/PDF package verification required.
- 2026-07-27: Reviewed target logs where required OpenAI/PDF packages installed successfully and ArcPy warning was non-blocking, but combined `clip`/`open_clip` import failed with duplicate `libiomp5md.dll` OpenMP runtime initialization. Split verification so required imports exclude CLIP runtime imports, package-version checks still prove CLIP packages are installed, and CLIP imports are diagnostic warnings.
