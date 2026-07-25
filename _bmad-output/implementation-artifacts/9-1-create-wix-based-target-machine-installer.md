# Story 9.1: Create WiX-Based Target-Machine Installer

Status: ready-for-dev

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
4. Given Python setup is required, when the installer runs, then it creates or validates a cloned environment named `arcgispro-survey-ai` from the target machine's own `arcgispro-py3`.
5. Given Python setup runs, then it installs conda requirements before pip requirements using the repo-controlled files in `docs/deployment/arcgispro37`.
6. Given Python setup completes, then verification confirms `arcpy`, `openai`, `flask`, `pdfplumber`, and `pypdfium2` imports.
7. Given the add-in is configured, then the embedded `WorkflowSettings.json` points to the target install root, target `ProcessingTools`, target `Contracts`, target `ParcelWorkflowCases`, and the cloned environment `python.exe`.
8. Given installation succeeds, then the configured add-in is registered or launched for installation in ArcGIS Pro.
9. Given installation is upgraded, then `ParcelWorkflowCases` and logs are preserved by default.
10. Given uninstall runs, then installed binaries/tools can be removed without deleting case folders/logs unless a future explicit full-clean option is added.
11. Given installation fails, then logs identify the failing phase, command, exit code, ArcGIS Pro path, Python path, and non-secret diagnostics.
12. Given automated validation runs on the build machine, then the installer staging/build script can be tested without requiring ArcGIS Pro by using dry-run or mocked detection hooks.

## Tasks / Subtasks

- [ ] Add installer source structure. (AC: 1)
  - [ ] Create `installer/` folder.
  - [ ] Add WiX MSI project for file payload.
  - [ ] Add WiX Burn bootstrapper project or documented placeholder if Burn is deferred.
  - [ ] Add README with build prerequisites.

- [ ] Extend staging pipeline. (AC: 1, 5)
  - [ ] Update `tools/stage_target_deployment.ps1` to stage `docs/deployment/arcgispro37/requirements-conda.txt`.
  - [ ] Stage `docs/deployment/arcgispro37/requirements-pip.txt`.
  - [ ] Stage installer-support scripts under the deployment payload.
  - [ ] Ensure `deployment_manifest.json` includes the Python setup requirements and installer version.

- [ ] Add target Python environment setup script. (AC: 2-6, 11-12)
  - [ ] Add a script such as `installer/scripts/setup_arcgispro37_environment.ps1`.
  - [ ] Detect ArcGIS Pro 3.7 and default conda/propy tooling.
  - [ ] Clone `arcgispro-py3` to `arcgispro-survey-ai` when missing.
  - [ ] Reuse or repair the clone according to explicit options.
  - [ ] Install conda requirements before pip requirements.
  - [ ] Verify imports and write logs.
  - [ ] Provide dry-run/mocked path mode for tests.

- [ ] Wire add-in configuration to installer result. (AC: 7-8)
  - [ ] Reuse `Update-AddInPackageSettings` logic from `deployment/target-computer-tools/scripts/install_target_tools.ps1` or extract it into a shared support script.
  - [ ] Configure `arcgis_python_executable` to the cloned environment `python.exe`.
  - [ ] Preserve existing target path conventions under `C:\Sidwell\ParcelWorkflow`.

- [ ] Define upgrade/uninstall behavior. (AC: 9-10)
  - [ ] Mark case folders and logs as preserved data.
  - [ ] Ensure upgrade replaces add-in/tools/contracts/config templates safely.
  - [ ] Document any manual cleanup path separately.

- [ ] Add tests/validation. (AC: 1-12)
  - [ ] Add PowerShell validation for stage manifest contents.
  - [ ] Add dry-run tests for ArcGIS Pro detection and missing ArcGIS Pro failure.
  - [ ] Add dry-run tests for environment clone command composition.
  - [ ] Add dry-run tests for conda-before-pip ordering.
  - [ ] Add dry-run tests for add-in settings rewrite paths.

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

## Open Questions

- Should `arcgispro-survey-ai` live in the ArcGIS Pro envs folder or under `C:\Sidwell\ParcelWorkflow\python-env`?
- Should the first installer artifact be MSI-only with script fallback, or Burn EXE plus MSI from the start?
- Should the installer be per-user, per-machine, or mixed?
- What certificate/code-signing process will be used for MSI/EXE?

