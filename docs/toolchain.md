# Toolchain

## Selected ArcGIS Pro SDK Lane

The project now selects the ArcGIS Pro 3.6 lane as the compatibility floor:

- ArcGIS Pro SDK: 3.6
- IDE: Visual Studio 2022 v17.13 or later
- Target framework: `net8.0-windows`
- .NET runtime: .NET 8 Desktop Runtime

Build and smoke-test against ArcGIS Pro 3.6 first. ArcGIS Pro 3.7 compatibility should be verified as a forward-compatibility smoke test, while avoiding SDK APIs introduced only after 3.6.

## Python Processing Environment

Configured ArcGIS Python executable:

```text
C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe
```

The default shell `python` command is not used by project scripts because it has failed in this workspace with `ModuleNotFoundError: No module named 'encodings'`.

## Local Prerequisites

Building and launching the add-in requires ArcGIS Pro, the matching ArcGIS Pro SDK Visual Studio extension, Visual Studio, and the .NET Desktop Runtime for the selected lane. This repository scaffold can be validated without ArcGIS Pro by running `tools/validate_contracts.ps1`.

If `dotnet restore` reports missing `Microsoft.NETCore.App.Ref`, `Microsoft.WindowsDesktop.App.Ref`, or `Microsoft.NETCore.App.Host.win-x64` for `net8.0-windows`, install the .NET 8 SDK/targeting packs or restore once with NuGet.org enabled:

```powershell
dotnet restore src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln --source https://api.nuget.org/v3/index.json --source "C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\"
```

## Add-in Packaging

Use the packaging helper to build and register the ArcGIS Pro add-in package:

```powershell
tools\package_addin.ps1
```

The script uses Visual Studio MSBuild because Esri's desktop packaging target uses MSBuild task factories that are not supported by `dotnet build`. A successful run produces:

```text
src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\bin\Debug\net8.0-windows\ParcelWorkflowAddIn.esriAddInX
```

The Esri packaging target also invokes `RegisterAddIn.exe` for the generated package when ArcGIS Pro is installed.

## Known Windows SDK metadata access issue

If build or package fails with:

`Access to the path 'C:\Users\<user>\AppData\Local\Microsoft SDKs' is denied.`

run from an elevated PowerShell:

```powershell
$path = "C:\Users\$env:USERNAME\AppData\Local\Microsoft SDKs"
takeown /f $path /r /d y
icacls $path /grant "$($env:USERDOMAIN)\$($env:USERNAME):(OI)(CI)F" /T
```

Then retry the readiness/package command.
