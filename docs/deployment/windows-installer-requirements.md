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
python -c "import openai; import flask; import pdfplumber; import pypdfium2; print('AI Survey env OK')"
```

## Acceptance Criteria

1. Given ArcGIS Pro 3.7 is installed and licensed, when the installer runs, then it detects the ArcGIS Pro installation and default Python/conda location.
2. Given ArcGIS Pro 3.7 is missing, when the installer runs, then installation blocks with a clear message before copying or configuring partial assets.
3. Given the default `arcgispro-py3` environment exists, when Python setup runs, then the installer creates or reuses a separate `arcgispro-survey-ai` clone and never installs packages into `arcgispro-py3`.
4. Given `arcgispro-survey-ai` already exists, when the installer runs, then it either validates and reuses it or repairs it according to an explicit reinstall/repair option.
5. Given requirements files are present, when Python setup runs, then conda packages install before pip packages.
6. Given Python setup finishes, when verification runs, then `arcpy`, `openai`, `flask`, `pdfplumber`, and `pypdfium2` imports are checked and logged.
7. Given verification fails, when the installer exits, then it shows the failing import or command and leaves a log file.
8. Given installation succeeds, when the add-in is configured, then its embedded settings point to the installed `ProcessingTools`, `Contracts`, `ParcelWorkflowCases`, and cloned Python `python.exe`.
9. Given installation succeeds, when ArcGIS Pro opens, then the add-in is registered and available.
10. Given an upgrade runs, when existing case folders and logs exist, then they are preserved.
11. Given uninstall runs, when user data exists, then add-in binaries/tools can be removed while case folders and logs are preserved unless the user chooses a full cleanup option.
12. Given support needs diagnostics, when installation completes or fails, then logs identify version, install root, ArcGIS Pro path, Python path, and package versions without secrets.

