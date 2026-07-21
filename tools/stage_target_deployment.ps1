param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$StageDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'deployment/target-computer-tools'),
    [string]$SourcePythonEnvRoot = '',
    [switch]$IncludePythonEnv,
    [switch]$SkipAddInPackage
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

    Get-ChildItem -LiteralPath $Destination -Recurse -Force -Directory |
        Where-Object { $_.Name -in @('__pycache__', '.pytest_cache') } |
        Remove-Item -Recurse -Force

    Get-ChildItem -LiteralPath $Destination -Recurse -Force -Include '*.pyc', '*.pyo' |
        Remove-Item -Force
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

$stageRoot = [System.IO.Path]::GetFullPath($StageDir)
$packageDir = Join-Path $stageRoot 'package'
$scriptsDir = Join-Path $stageRoot 'scripts'
$processingToolsSource = Join-Path $Root 'src/ProcessingTools'
$contractsSource = Join-Path $Root 'src/Contracts'
$addinSource = Join-Path $Root "src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/bin/$Configuration/net8.0-windows/ParcelWorkflowAddIn.esriAddInX"

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null

if (-not $SkipAddInPackage) {
    if (-not (Test-Path -LiteralPath $addinSource)) {
        & (Join-Path $PSScriptRoot 'package_addin.ps1') -Root $Root -Configuration $Configuration
    }

    if (-not (Test-Path -LiteralPath $addinSource)) {
        throw "Add-in package not found after build: $addinSource"
    }

    Copy-Item -LiteralPath $addinSource -Destination (Join-Path $packageDir 'ParcelWorkflowAddIn.esriAddInX') -Force
}

Copy-CleanDirectory -Source $processingToolsSource -Destination (Join-Path $packageDir 'ProcessingTools')
Copy-CleanDirectory -Source $contractsSource -Destination (Join-Path $packageDir 'Contracts')

if ($IncludePythonEnv) {
    if ([string]::IsNullOrWhiteSpace($SourcePythonEnvRoot)) {
        $SourcePythonEnvRoot = 'C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai'
    }

    if (-not (Test-Path -LiteralPath (Join-Path $SourcePythonEnvRoot 'python.exe'))) {
        throw "Bundled Python environment must contain python.exe: $SourcePythonEnvRoot"
    }

    Write-Host "Copying Python environment. This can take several minutes: $SourcePythonEnvRoot"
    Copy-LargeDirectory -Source $SourcePythonEnvRoot -Destination (Join-Path $packageDir 'python-env')
}

$manifest = [ordered]@{
    package_name = 'Sidwell Parcel Workflow target-computer tools'
    created_at = (Get-Date).ToString('o')
    configuration = $Configuration
    includes = @(
        'ParcelWorkflowAddIn.esriAddInX',
        'ProcessingTools',
        'Contracts',
        'scripts/install_target_tools.ps1'
    )
    target_default_root = 'C:\Sidwell\ParcelWorkflow'
    notes = @(
        'Python runtime is not bundled by default because it is large and external to the repository.',
        'Manually copy arcgispro-survey-ai to C:\Sidwell\ParcelWorkflow\python-env on the target computer, or run install_target_tools.ps1 with -PythonExe.',
        'Run scripts/install_target_tools.ps1 on the target computer to copy tools and configure the add-in package paths.'
    )
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageDir 'deployment_manifest.json') -Encoding UTF8

Write-Host "Target deployment tools staged: $stageRoot"
Write-Host "Copy this folder to the target computer: $stageRoot"
