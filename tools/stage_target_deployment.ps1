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

    $cacheDirectories = Get-ChildItem -LiteralPath $Destination -Recurse -Force -Directory |
        Where-Object { $_.Name -in @('__pycache__', '.pytest_cache') }
    foreach ($cacheDirectory in $cacheDirectories) {
        Remove-Item -LiteralPath $cacheDirectory.FullName -Recurse -Force
    }

    $compiledPythonFiles = Get-ChildItem -LiteralPath $Destination -Recurse -Force -File |
        Where-Object { $_.Extension -in @('.pyc', '.pyo') }
    foreach ($compiledPythonFile in $compiledPythonFiles) {
        Remove-Item -LiteralPath $compiledPythonFile.FullName -Force
    }
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
$installerScriptsSource = Join-Path $Root 'installer/scripts'
$arcGisPro37DocsSource = Join-Path $Root 'docs/deployment/arcgispro37'
$addinSource = Join-Path $Root "src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/bin/$Configuration/net8.0-windows/ParcelWorkflowAddIn.esriAddInX"

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null

if (-not $SkipAddInPackage) {
    Write-Host "Building fresh add-in package..."
    & (Join-Path $PSScriptRoot 'package_addin.ps1') -Root $Root -Configuration $Configuration

    if (-not (Test-Path -LiteralPath $addinSource)) {
        throw "Add-in package not found after build: $addinSource"
    }

    Copy-Item -LiteralPath $addinSource -Destination (Join-Path $packageDir 'ParcelWorkflowAddIn.esriAddInX') -Force
}

Copy-CleanDirectory -Source $processingToolsSource -Destination (Join-Path $packageDir 'ProcessingTools')
Copy-CleanDirectory -Source $contractsSource -Destination (Join-Path $packageDir 'Contracts')

$installerPackageDir = Join-Path $packageDir 'installer'
$installerScriptsDestination = Join-Path $installerPackageDir 'scripts'
$installerRequirementsDestination = Join-Path $installerPackageDir 'arcgispro37'
Copy-CleanDirectory -Source $installerScriptsSource -Destination $installerScriptsDestination
New-Item -ItemType Directory -Path $installerRequirementsDestination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $arcGisPro37DocsSource 'requirements-conda.txt') -Destination $installerRequirementsDestination -Force
Copy-Item -LiteralPath (Join-Path $arcGisPro37DocsSource 'requirements-pip.txt') -Destination $installerRequirementsDestination -Force
Copy-Item -LiteralPath (Join-Path $arcGisPro37DocsSource 'arcgispro-survey-ai-component-inventory.csv') -Destination $installerRequirementsDestination -Force

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
        'installer/scripts/setup_arcgispro37_environment.ps1',
        'installer/scripts/setup_arcgispro37_environment.bat',
        'installer/scripts/write_installation_summary.ps1',
        'installer/arcgispro37/requirements-conda.txt',
        'installer/arcgispro37/requirements-pip.txt',
        'scripts/install_target_tools.ps1',
        'scripts/install_target_tools.bat'
    )
    target_default_root = 'C:\Sidwell\ParcelWorkflow'
    installer = [ordered]@{
        technology = 'WiX Toolset'
        package_project = 'installer/ParcelWorkflowInstaller.wixproj'
        bootstrapper_project = 'installer/ParcelWorkflowBootstrapper.wixproj'
        arcgis_pro_target_version = '3.7'
        python_environment_name = 'arcgispro-survey-ai'
        conda_requirements = 'installer/arcgispro37/requirements-conda.txt'
        pip_requirements = 'installer/arcgispro37/requirements-pip.txt'
    }
    notes = @(
        'Python runtime is not bundled by default because it is large and external to the repository.',
        'For ArcGIS Pro 3.7 installs, use installer/scripts/setup_arcgispro37_environment.ps1 to clone arcgispro-py3 into the install-root envs\arcgispro-survey-ai folder and install requirements.',
        'Do not copy an ArcGIS Pro 3.6 cloned Python environment to an ArcGIS Pro 3.7 target computer.',
        'The target installer first uses the target computer ArcGIS Pro conda tooling from C:\Program Files\ArcGIS\Pro\bin\Python.',
        'The configured add-in should point to C:\Sidwell\ParcelWorkflow\envs\arcgispro-survey-ai\python.exe after setup.',
        'The target installer requires openai, flask, pdfplumber, pypdf, and pypdfium2 imports; verifies OpenAI/CLIP package versions; and logs arcpy/clip/open_clip imports as diagnostics.',
        'The WiX installer can set OPENAI_API_KEY from a supplied OpenAiApiKey variable; the package stores only the environment variable name in add-in settings.',
        'The target installer sets case_folder_output_root to C:\Sidwell\ParcelWorkflow\ParcelWorkflowCases in the configured add-in package.',
        'Run scripts/install_target_tools.ps1 or scripts/install_target_tools.bat on the target computer to copy tools and configure the add-in package paths.',
        'If PowerShell is blocked by MachinePolicy AllSigned, run scripts/install_target_tools.bat instead.'
    )
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageDir 'deployment_manifest.json') -Encoding UTF8

Write-Host "Target deployment tools staged: $stageRoot"
Write-Host "Copy this folder to the target computer: $stageRoot"
