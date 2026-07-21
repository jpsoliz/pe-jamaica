param(
    [string]$InstallRoot = 'C:\Sidwell\ParcelWorkflow',
    [string]$PythonExe = '',
    [string]$SourcePythonEnvRoot = '',
    [switch]$SkipAddInInstall
)

$ErrorActionPreference = 'Stop'

function Copy-CleanDirectory {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Source directory not found: $Source"
    }

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

function Copy-LargeDirectory {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Source directory not found: $Source"
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    & robocopy $Source $Destination /MIR /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /XD __pycache__ .pytest_cache /XF *.pyc *.pyo
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with exit code $LASTEXITCODE while copying $Source"
    }
}

function Set-JsonStringProperty {
    param(
        [Parameter(Mandatory)]$JsonObject,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Value
    )

    if ($JsonObject.PSObject.Properties.Name -contains $Name) {
        $JsonObject.$Name = $Value
    }
    else {
        $JsonObject | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Update-AddInPackageSettings {
    param(
        [Parameter(Mandatory)][string]$SourceAddInPath,
        [Parameter(Mandatory)][string]$DestinationAddInPath,
        [Parameter(Mandatory)][string]$ConfiguredPythonExe,
        [Parameter(Mandatory)][string]$ConfiguredInstallRoot
    )

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "sidwell-addin-package-$([guid]::NewGuid().ToString('N'))"
    $tempZip = "$tempRoot.zip"
    $expanded = Join-Path $tempRoot 'expanded'

    try {
        New-Item -ItemType Directory -Path $expanded -Force | Out-Null
        Copy-Item -LiteralPath $SourceAddInPath -Destination $tempZip -Force
        Expand-Archive -LiteralPath $tempZip -DestinationPath $expanded -Force

        $settingsPath = Join-Path $expanded 'Install/Settings/WorkflowSettings.json'
        if (-not (Test-Path -LiteralPath $settingsPath)) {
            throw "WorkflowSettings.json was not found inside add-in package."
        }

        $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        $toolsRoot = Join-Path $ConfiguredInstallRoot 'ProcessingTools'
        Set-JsonStringProperty -JsonObject $settings -Name 'arcgis_python_executable' -Value $ConfiguredPythonExe
        Set-JsonStringProperty -JsonObject $settings -Name 'output_adapter_script_path' -Value (Join-Path $toolsRoot 'adapters/output_adapter.py')
        Set-JsonStringProperty -JsonObject $settings -Name 'validation_adapter_script_path' -Value (Join-Path $toolsRoot 'adapters/validation_adapter.py')
        Set-JsonStringProperty -JsonObject $settings -Name 'validation_rules_path' -Value (Join-Path $toolsRoot 'rules/rules.yaml')

        if ($settings.enterprise_working_admin) {
            Set-JsonStringProperty -JsonObject $settings.enterprise_working_admin -Name 'provisioning_script_path' -Value (Join-Path $toolsRoot 'admin/provision_enterprise_working_layers.py')
        }

        $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

        if (Test-Path -LiteralPath $DestinationAddInPath) {
            Remove-Item -LiteralPath $DestinationAddInPath -Force
        }

        $configuredZip = "$DestinationAddInPath.zip"
        if (Test-Path -LiteralPath $configuredZip) {
            Remove-Item -LiteralPath $configuredZip -Force
        }

        Compress-Archive -Path (Join-Path $expanded '*') -DestinationPath $configuredZip -Force
        Move-Item -LiteralPath $configuredZip -Destination $DestinationAddInPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $tempZip) {
            Remove-Item -LiteralPath $tempZip -Force
        }
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$deploymentRoot = Split-Path -Parent $scriptRoot
$packageRoot = Join-Path $deploymentRoot 'package'
$sourceAddIn = Join-Path $packageRoot 'ParcelWorkflowAddIn.esriAddInX'
$sourceProcessingTools = Join-Path $packageRoot 'ProcessingTools'
$sourceContracts = Join-Path $packageRoot 'Contracts'
$bundledPythonEnv = Join-Path $packageRoot 'python-env'

if (-not (Test-Path -LiteralPath $packageRoot)) {
    throw "Package folder not found: $packageRoot. Run tools/stage_target_deployment.ps1 before copying this folder."
}

$resolvedInstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$targetProcessingTools = Join-Path $resolvedInstallRoot 'ProcessingTools'
$targetContracts = Join-Path $resolvedInstallRoot 'Contracts'
$targetAddInDir = Join-Path $resolvedInstallRoot 'AddIn'
$configuredAddInPath = Join-Path $targetAddInDir 'ParcelWorkflowAddIn.configured.esriAddInX'

New-Item -ItemType Directory -Path $resolvedInstallRoot -Force | Out-Null
New-Item -ItemType Directory -Path $targetAddInDir -Force | Out-Null

if (-not [string]::IsNullOrWhiteSpace($SourcePythonEnvRoot)) {
    $targetPythonEnv = Join-Path $resolvedInstallRoot 'python-env'
    Copy-LargeDirectory -Source $SourcePythonEnvRoot -Destination $targetPythonEnv
    $PythonExe = Join-Path $targetPythonEnv 'python.exe'
}
elseif ([string]::IsNullOrWhiteSpace($PythonExe) -and (Test-Path -LiteralPath (Join-Path $bundledPythonEnv 'python.exe'))) {
    $targetPythonEnv = Join-Path $resolvedInstallRoot 'python-env'
    Copy-LargeDirectory -Source $bundledPythonEnv -Destination $targetPythonEnv
    $PythonExe = Join-Path $targetPythonEnv 'python.exe'
}
elseif ([string]::IsNullOrWhiteSpace($PythonExe) -and (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot 'python-env\python.exe'))) {
    $PythonExe = Join-Path $resolvedInstallRoot 'python-env\python.exe'
}

if ([string]::IsNullOrWhiteSpace($PythonExe)) {
    throw "Python environment was not found. Manually copy arcgispro-survey-ai to '$resolvedInstallRoot\python-env', provide -PythonExe, or provide -SourcePythonEnvRoot."
}

if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}

Copy-CleanDirectory -Source $sourceProcessingTools -Destination $targetProcessingTools
Copy-CleanDirectory -Source $sourceContracts -Destination $targetContracts

if (Test-Path -LiteralPath $sourceAddIn) {
    Update-AddInPackageSettings `
        -SourceAddInPath $sourceAddIn `
        -DestinationAddInPath $configuredAddInPath `
        -ConfiguredPythonExe $PythonExe `
        -ConfiguredInstallRoot $resolvedInstallRoot

    if (-not $SkipAddInInstall) {
        Start-Process -FilePath $configuredAddInPath
    }
}
else {
    Write-Warning "Add-in package not found in deployment package: $sourceAddIn"
}

Write-Host "Installed target tools to: $resolvedInstallRoot"
Write-Host "Python executable: $PythonExe"
Write-Host "Processing tools: $targetProcessingTools"
Write-Host "Contracts: $targetContracts"
if (Test-Path -LiteralPath $configuredAddInPath) {
    Write-Host "Configured add-in package: $configuredAddInPath"
}
