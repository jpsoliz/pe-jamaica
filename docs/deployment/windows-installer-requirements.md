# Parcel Workflow Windows Installer Requirements

Owner lens: Mary / Business Analyst
Date: 2026-07-25

## Goal

Create a repeatable Windows installer for the Parcel Workflow ArcGIS Pro add-in and target-machine tools so a new workstation can be prepared without manual folder copying, unsigned-script workarounds, or ambiguous Python configuration.

## Business Outcomes

- A target workstation can be installed from a single installer entry point.
- The installed add-in uses the correct target-machine folder paths.
- The Python environment is compatible with the installed ArcGIS Pro version.
- Failed installs produce clear logs and actionable messages.
- Upgrades preserve user case folders, logs, and machine-specific settings unless explicitly replaced.

## Scope

The installer must install or configure:

- `ParcelWorkflowAddIn.esriAddInX`.
- `ProcessingTools`.
- `Contracts`.
- Configuration required by the add-in.
- Target scripts needed for support and diagnostics.
- `C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases`.
- `C:\Sidwell\ParcelWorkflow\logs`.
- ArcGIS Pro 3.7 compatible cloned Python environment named `arcgispro-survey-ai`.

The default install root shown to users and written to logs is:

```text
C:\Sidwell\ParcelWorkflow
```

The installer UI must show the default install root and allow an operator to review it before installation. The first production UI should also show the detected ArcGIS Pro root, the target Python environment path, and the persistent diagnostic log folder before running post-install configuration.

## Python Environment Requirements

The installer must not copy an existing ArcGIS Pro 3.6 cloned environment to an ArcGIS Pro 3.7 machine.

The installer must not modify the target machine's default `arcgispro-py3` environment.

The installer must create or reuse a clone from the target machine's own ArcGIS Pro default environment:

```powershell
conda create --clone arcgispro-py3 --name arcgispro-survey-ai --pinned
conda activate arcgispro-survey-ai
conda install --file requirements-conda.txt -c esri -c conda-forge -c defaults
pip install -r requirements-pip.txt
```

The requirements source files are:

- `docs/deployment/arcgispro37/requirements-conda.txt`
- `docs/deployment/arcgispro37/requirements-pip.txt`
- `docs/deployment/arcgispro37/arcgispro-survey-ai-component-inventory.csv`

The installer must verify:

```powershell
python -c "import arcpy; print('arcpy OK')"
python -c "import openai; import flask; import pdfplumber; import pypdfium2; print('AI Survey required imports OK')"
python -c "import importlib.metadata as m; packages=['openai','openai-clip','open-clip-torch','Flask','pdfplumber','pypdfium2']; print('package_versions:' + ';'.join(f'{p}={m.version(p)}' for p in packages))"
```

The `arcgispro-survey-ai` clone must live under the ArcGIS Pro Python environments folder, beside `arcgispro-py3`. If the environment already exists, the installer must verify required dependencies and imports before reusing it. If it does not exist, the installer must clone it from the target machine's `arcgispro-py3`.

The pip requirements must include the OpenAI and CLIP packages required by the inventory:

- `openai`
- `openai-clip`
- `open-clip-torch`

`clip` and `open_clip` import checks are diagnostic only because ArcGIS Pro/PyTorch/OpenMP DLL conflicts can cause `libiomp5md.dll` duplicate-runtime failures in elevated installer processes even when the packages are installed.

## OpenAI API Key Requirements

The installer must not embed or deploy a real OpenAI API key in the MSI, EXE, add-in package, source settings file, logs, or repository files.

The add-in setting `openai_api_key_environment_variable` names the environment variable to read. The default is:

```text
OPENAI_API_KEY
```

For the current installer, the bootstrapper can receive the `OpenAiApiKey` variable and set the machine `OPENAI_API_KEY` environment variable during installation. The stock WiX UI does not show custom text boxes; production should replace it with a custom Burn Bootstrapper Application that asks for the key in a masked field. If a key is supplied, it must be stored as the configured machine/user environment variable or an approved Windows secret, and the secret value must never be written to support logs, add-in settings, or repository files.

## Installer Package Requirements

The first production installer should be a Burn EXE plus MSI, not MSI-only. The EXE is the operator entry point and coordinates prerequisites, ArcGIS Pro detection, Python environment setup, and logging. The MSI remains the deterministic payload installer for files, folders, upgrade, and uninstall.

The installer model is mixed:

- Per-machine for the shared install root under `C:\Sidwell\ParcelWorkflow`.
- Per-user for ArcGIS Pro add-in registration or launch because ArcGIS Pro add-ins are installed into the user's profile.

Code signing is strongly recommended before distribution outside the development/test team. Unsigned MSI/EXE packages can work for internal testing, but Windows SmartScreen, antivirus policy, or enterprise endpoint controls may warn or block. The release process should support unsigned developer builds and signed production builds.

## Acceptance Criteria

1. Given ArcGIS Pro 3.7 is installed and licensed, when the installer runs, then it detects the ArcGIS Pro installation and default Python/conda location.
2. Given ArcGIS Pro 3.7 is missing, when the installer runs, then installation blocks with a clear message before copying or configuring partial assets.
3. Given the default `arcgispro-py3` environment exists, when Python setup runs, then the installer creates or reuses a separate `arcgispro-survey-ai` clone and never installs packages into `arcgispro-py3`.
4. Given `arcgispro-survey-ai` already exists under the ArcGIS Pro envs folder, when the installer runs, then it validates required dependencies/imports and reuses it only when verification passes; otherwise it repairs it according to an explicit reinstall/repair option.
5. Given requirements files are present, when Python setup runs, then conda packages install before pip packages only when conda package entries are configured.
6. Given Python setup finishes, when verification runs, then `openai`, `flask`, `pdfplumber`, and `pypdfium2` imports are checked as required; `openai-clip` and `open-clip-torch` package versions are checked; `arcpy`, `clip`, and `open_clip` imports are logged as diagnostics.
7. Given verification fails, when the installer exits, then it shows the failing import or command and leaves a log file.
8. Given installation succeeds, when the add-in is configured, then its embedded settings point to the installed `ProcessingTools`, `Contracts`, `ParcelWorkflowCases`, and cloned Python `python.exe`.
9. Given installation succeeds, when ArcGIS Pro opens, then the add-in is registered and available.
10. Given an upgrade runs, when existing case folders and logs exist, then they are preserved.
11. Given uninstall runs, when user data exists, then add-in binaries/tools can be removed while case folders and logs are preserved unless the user chooses a full cleanup option.
12. Given support needs diagnostics, when installation completes or fails, then logs identify version, install root, ArcGIS Pro path, Python path, and package versions without secrets.
13. Given a production installer is prepared for distribution, when release packaging runs, then the build can produce a signed MSI/EXE if a code-signing certificate is configured, while still allowing unsigned developer/test builds.
14. Given OpenAI extraction is enabled, when installation/configuration completes, then the add-in configuration references only the OpenAI API key environment variable name and never stores the API key value.
15. Given the installer is run interactively, when the options/configuration page is shown, then the operator can review the install root, detected ArcGIS Pro path, target `arcgispro-survey-ai` environment path, and diagnostic log folder before install.
16. Given the operator enters an OpenAI API key during installation, when the installer stores configuration, then the UI masks the key and logs only whether a key was provided, never the key value.
