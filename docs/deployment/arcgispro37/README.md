# ArcGIS Pro 3.7 Environment Installer Inputs

These files were copied from the external ArcGIS Pro 3.7 deployment package and are kept here as source material for the Parcel Workflow installer story.

## Files

- `ArcGISPro37_Environment_Installation_Guide.docx` - manual installation guide for the ArcGIS Pro 3.7 Python environment.
- `arcgispro-survey-ai-component-inventory.csv` - conda/pip package inventory for the survey AI environment.
- `build_environment_installation_guide.py` - generator used to produce the installation guide.
- `requirements-conda.txt` - conda requirements seed.
- `requirements-pip.txt` - pip requirements seed.

## Installer Direction

Use WiX Toolset for the Windows installer and keep MSBuild/PowerShell as the build and staging pipeline.

Recommended split:

- MSBuild builds `ParcelWorkflowAddIn.esriAddInX`.
- `tools/stage_target_deployment.ps1` stages add-in, config, scripts, and deployment assets.
- WiX packages the staged payload into an MSI.
- A WiX Burn bootstrapper can be added later if we need prerequisite detection, richer ArcGIS Pro checks, or chained installers.

## Target Machine Baseline

- ArcGIS Pro must be installed and licensed.
- ArcGIS Pro Python/ArcPy must import successfully from the target machine.
- The Parcel Workflow install root is `C:\Sidwell\ParcelWorkflow`.
- Existing target scripts/config should remain the source of truth for folder paths until moved into the MSI authoring.

