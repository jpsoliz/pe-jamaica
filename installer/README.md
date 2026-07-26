# Parcel Workflow Installer

This folder contains the WiX-based installer scaffold for the Parcel Workflow target-machine deployment.

## Build Machine Prerequisites

Install the .NET SDK and WiX Toolset:

```powershell
dotnet tool install --global wix --version 7.0.0
wix --version
```

The WiX projects use SDK-style WiX authoring:

```xml
<Project Sdk="WixToolset.Sdk/7.0.0">
```

WiX Toolset v7 requires Open Source Maintenance Fee (OSMF) EULA acceptance. On a new build machine, accept the WiX v7 EULA once:

```powershell
wix eula accept wix7
```

Or let the project build wrapper do it:

```powershell
.\tools\build_installer.ps1 -Configuration Release -AcceptWixEula
```

The WiX projects also set `<AcceptEula>wix7</AcceptEula>` explicitly so CI/build logs show that the project is intended to build under the v7 EULA.

## Build Flow

Validate installer staging and the ArcGIS Pro 3.7 environment dry-run path:

```powershell
.\tools\validate_installer_packaging.ps1
```

1. Build and stage the add-in payload:

```powershell
.\tools\stage_target_deployment.ps1 -Configuration Release
```

2. Build the MSI:

```powershell
.\tools\build_installer.ps1 -Configuration Release -SkipBootstrapper
```

3. Build the bootstrapper EXE after the MSI exists:

```powershell
.\tools\build_installer.ps1 -Configuration Release
```

## Signing

Developer and test builds can remain unsigned. Production builds should be signed after the MSI is produced and again after the bootstrapper EXE is produced.

Sign with a PFX certificate:

```powershell
$env:PARCEL_WORKFLOW_SIGN_CERT_PASSWORD = '<password>'
.\tools\build_installer.ps1 `
  -Configuration Release `
  -AcceptWixEula `
  -Sign `
  -CertificatePath 'C:\Path\SidwellCodeSigningCert.pfx'
```

Sign with a certificate already installed in the Windows certificate store:

```powershell
.\tools\build_installer.ps1 `
  -Configuration Release `
  -AcceptWixEula `
  -Sign `
  -CertificateThumbprint '<thumbprint>'
```

Use `-MachineCertificateStore` when the certificate is installed under the local machine store instead of the current user store.

## Installer Boundary

The MSI installs the staged payload. The bootstrapper coordinates prerequisite checks and Python environment setup.

The installer must not copy a Python environment from another ArcGIS Pro version. It must create or validate `arcgispro-survey-ai` from the target computer's ArcGIS Pro 3.7 `arcgispro-py3` environment.

Python setup logs are written under:

```text
C:\ProgramData\Sidwell\ParcelWorkflow\logs
```

This location is intentionally outside the MSI install root so logs survive MSI rollback when Python setup fails.

The bootstrapper keeps the MSI installation even if Python environment setup fails. This lets support inspect installed files and the persistent setup logs, then repair the Python environment without losing the installed add-in payload.

## Install-Time Configuration

The default install folder is:

```text
C:\Sidwell\ParcelWorkflow
```

The current bootstrapper uses the standard WiX UI. It does not yet show custom text boxes for the install paths or API key. For test deployments, pass installer variables on the command line:

```powershell
.\ParcelWorkflowSetup.exe `
  InstallFolder="C:\Sidwell\ParcelWorkflow" `
  OpenAiApiKey="sk-..."
```

The installer writes the OpenAI key to the machine `OPENAI_API_KEY` environment variable and configures the add-in to read that environment variable. The key value is not written to the add-in settings or support logs.

For upgrades, run the same EXE. Installed payload files are replaced, `ParcelWorkflowCases` and logs are preserved, the Python environment is verified again, and the add-in package is reconfigured. If `OpenAiApiKey` is omitted during an upgrade, the existing environment variable value is left unchanged.

After install, verify:

```text
C:\ProgramData\Sidwell\ParcelWorkflow\logs\setup_arcgispro37_environment_status.json
C:\ProgramData\Sidwell\ParcelWorkflow\logs\install_path_summary.json
```

A future production UI should replace the stock WiX bootstrapper with a custom Burn Bootstrapper Application so users can review the default folder paths and paste the API key into a masked field.
