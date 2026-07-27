param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Path {
    param([Parameter(Mandatory)][string]$Path)

    Assert-True -Condition (Test-Path -LiteralPath $Path) -Message "Expected path was not found: $Path"
}

$tempRoot = Join-Path $Root 'tmp\installer-validation'
$stageDir = Join-Path $tempRoot 'target-computer-tools'
$mockArcGisRoot = Join-Path $tempRoot 'ArcGIS\Pro'
$mockDefaultEnv = Join-Path $mockArcGisRoot 'bin\Python\envs\arcgispro-py3'
$mockScripts = Join-Path $mockArcGisRoot 'bin\Python\Scripts'
$logRoot = Join-Path $tempRoot 'logs'

if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $mockDefaultEnv -Force | Out-Null
New-Item -ItemType Directory -Path $mockScripts -Force | Out-Null
New-Item -ItemType File -Path (Join-Path $mockDefaultEnv 'python.exe') -Force | Out-Null
New-Item -ItemType File -Path (Join-Path $mockScripts 'conda.exe') -Force | Out-Null

& (Join-Path $Root 'tools\stage_target_deployment.ps1') `
    -Root $Root `
    -StageDir $stageDir `
    -SkipAddInPackage

$packageRoot = Join-Path $stageDir 'package'
$manifestPath = Join-Path $packageRoot 'deployment_manifest.json'
$setupScriptPath = Join-Path $packageRoot 'installer\scripts\setup_arcgispro37_environment.ps1'
$setupBatPath = Join-Path $packageRoot 'installer\scripts\setup_arcgispro37_environment.bat'
$summaryScriptPath = Join-Path $packageRoot 'installer\scripts\write_installation_summary.ps1'
$condaRequirementsPath = Join-Path $packageRoot 'installer\arcgispro37\requirements-conda.txt'
$pipRequirementsPath = Join-Path $packageRoot 'installer\arcgispro37\requirements-pip.txt'

Assert-Path $manifestPath
Assert-Path $setupScriptPath
Assert-Path $setupBatPath
Assert-Path $summaryScriptPath
Assert-Path $condaRequirementsPath
Assert-Path $pipRequirementsPath

$pipRequirementNames = @(Get-Content -LiteralPath $pipRequirementsPath | ForEach-Object { ($_.Trim() -split '[=<>~! ]', 2)[0] } | Where-Object { $_ -and -not $_.StartsWith('#') })
foreach ($requiredPipPackage in @('openai', 'openai-clip', 'open-clip-torch')) {
    Assert-True `
        -Condition ($pipRequirementNames -contains $requiredPipPackage) `
        -Message "Pip requirements do not include required package: $requiredPipPackage"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-True -Condition ($manifest.installer.technology -eq 'WiX Toolset') -Message 'Manifest does not identify WiX Toolset.'
Assert-True -Condition ($manifest.installer.arcgis_pro_target_version -eq '3.7') -Message 'Manifest does not target ArcGIS Pro 3.7.'
Assert-True -Condition ($manifest.installer.python_environment_name -eq 'arcgispro-survey-ai') -Message 'Manifest does not name arcgispro-survey-ai.'

& $setupScriptPath `
    -ArcGisProRoot $mockArcGisRoot `
    -InstallRoot (Join-Path $tempRoot 'InstallRoot') `
    -ScriptRoot (Split-Path -Parent $setupScriptPath) `
    -CondaRequirements $condaRequirementsPath `
    -PipRequirements $pipRequirementsPath `
    -LogRoot $logRoot `
    -DryRun

$planPath = Join-Path $logRoot 'setup_arcgispro37_environment_plan.json'
Assert-Path $planPath
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
Assert-True -Condition ($plan.dry_run -eq $true) -Message 'Environment setup plan was not a dry run.'
Assert-True -Condition ($plan.environment_name -eq 'arcgispro-survey-ai') -Message 'Environment setup plan used the wrong environment name.'
Assert-True -Condition (-not [string]::IsNullOrWhiteSpace($plan.install_root)) -Message 'Environment setup plan did not capture install_root.'
$condaCloneCommands = @($plan.commands | Where-Object { $_.phase -eq 'conda-clone' })
Assert-True -Condition ($condaCloneCommands.Count -eq 1) -Message 'Environment setup did not plan a conda clone.'

$phaseNames = @($plan.commands | ForEach-Object { $_.phase })
$condaInstallIndex = [array]::IndexOf($phaseNames, 'conda-install-requirements')
$pipInstallIndex = [array]::IndexOf($phaseNames, 'pip-install-requirements')
Assert-True -Condition ($pipInstallIndex -ge 0) -Message 'Pip requirements install was not planned.'
if ($condaInstallIndex -ge 0) {
    Assert-True -Condition ($condaInstallIndex -lt $pipInstallIndex) -Message 'Conda requirements must be installed before pip requirements.'
}
Assert-True -Condition ($phaseNames -contains 'verify-ai-survey-package-versions') -Message 'Package version verification was not planned.'

Remove-Item -LiteralPath $planPath -Force
$setupScriptText = Get-Content -LiteralPath $setupScriptPath -Raw
$setupScriptBlock = [ScriptBlock]::Create($setupScriptText)
& $setupScriptBlock `
    -ArcGisProRoot $mockArcGisRoot `
    -InstallRoot (Join-Path $tempRoot 'ScriptBlockInstallRoot') `
    -ScriptRoot (Split-Path -Parent $setupScriptPath) `
    -CondaRequirements $condaRequirementsPath `
    -PipRequirements $pipRequirementsPath `
    -LogRoot $logRoot `
    -DryRun

Assert-Path $planPath
$scriptBlockPlan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
Assert-True -Condition ($scriptBlockPlan.dry_run -eq $true) -Message 'Environment setup scriptblock plan was not a dry run.'
Assert-True -Condition ($scriptBlockPlan.environment_name -eq 'arcgispro-survey-ai') -Message 'Environment setup scriptblock plan used the wrong environment name.'

& $summaryScriptPath `
    -InstallRoot $packageRoot `
    -LogRoot $logRoot

$summaryPath = Join-Path $logRoot 'installation_summary.json'
Assert-Path $summaryPath
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
Assert-True -Condition (-not [string]::IsNullOrWhiteSpace($summary.status)) -Message 'Installation summary did not capture status.'
Assert-True -Condition (@($summary.post_install_actions).Count -ge 1) -Message 'Installation summary did not capture post-install action status.'
Assert-Path (Join-Path $packageRoot 'logs\installation_summary.txt')

$registerScript = Get-Content -LiteralPath (Join-Path $Root 'installer\scripts\register_parcel_workflow_addin.ps1') -Raw
Assert-True -Condition $registerScript.Contains('OpenAiApiKeyEnvironmentVariable') -Message 'Add-in registration script does not configure the OpenAI key environment variable name.'
Assert-True -Condition $registerScript.Contains('install_path_summary.json') -Message 'Add-in registration script does not write install_path_summary.json.'

$environmentSetupScript = Get-Content -LiteralPath (Join-Path $Root 'installer\scripts\setup_arcgispro37_environment.ps1') -Raw
Assert-True -Condition (-not $environmentSetupScript.Contains('ArgumentList.Add')) -Message 'Environment setup script uses ProcessStartInfo.ArgumentList, which is not reliable under Windows PowerShell 5.1 MSI custom actions.'
Assert-True -Condition $environmentSetupScript.Contains('ConvertTo-ProcessArgumentString') -Message 'Environment setup script does not use MSI-safe process argument formatting.'

$packageWxs = Get-Content -LiteralPath (Join-Path $Root 'installer\Package.wxs') -Raw
Assert-True -Condition $packageWxs.Contains('RunWriteInstallationSummary') -Message 'WiX package does not run the installation summary custom action.'
Assert-True -Condition $packageWxs.Contains('Id="RunSetupArcGisPro37Environment"') -Message 'WiX package does not define the ArcGIS Pro environment setup custom action.'
Assert-True -Condition $packageWxs.Contains('Return="check"') -Message 'ArcGIS Pro environment setup custom action must fail the install when environment setup fails.'

$environmentSetupBat = Get-Content -LiteralPath (Join-Path $Root 'installer\scripts\setup_arcgispro37_environment.bat') -Raw
Assert-True -Condition $environmentSetupBat.Contains('exit /b %EXIT_CODE%') -Message 'Environment setup batch file must propagate the PowerShell setup exit code.'
Assert-True -Condition (-not $environmentSetupBat.Contains('-File "%SCRIPT_ROOT%setup_arcgispro37_environment.ps1"')) -Message 'Environment setup batch file must not execute unsigned ps1 files with -File.'
Assert-True -Condition $environmentSetupBat.Contains('[ScriptBlock]::Create') -Message 'Environment setup batch file must use a scriptblock launcher for AllSigned target machines.'
Assert-True -Condition $environmentSetupBat.Contains('-ScriptRoot $env:SCRIPT_ROOT') -Message 'Environment setup batch file must pass ScriptRoot when using a scriptblock launcher.'

$registerBat = Get-Content -LiteralPath (Join-Path $Root 'installer\scripts\register_parcel_workflow_addin.bat') -Raw
Assert-True -Condition (-not $registerBat.Contains('-File "%SCRIPT_ROOT%register_parcel_workflow_addin.ps1"')) -Message 'Add-in registration batch file must not execute unsigned ps1 files with -File.'
Assert-True -Condition $registerBat.Contains('[ScriptBlock]::Create') -Message 'Add-in registration batch file must use a scriptblock launcher for AllSigned target machines.'

$summaryBatPath = Join-Path $Root 'installer\scripts\write_installation_summary.bat'
Assert-Path $summaryBatPath
$summaryBat = Get-Content -LiteralPath $summaryBatPath -Raw
Assert-True -Condition $summaryBat.Contains('[ScriptBlock]::Create') -Message 'Installation summary batch file must use a scriptblock launcher for AllSigned target machines.'
Assert-True -Condition $packageWxs.Contains('write_installation_summary.bat') -Message 'WiX package must run the installation summary through its batch wrapper.'
Assert-True -Condition (-not $packageWxs.Contains('write_installation_summary.ps1&quot;')) -Message 'WiX package must not execute the unsigned summary ps1 directly.'

$bundleWxs = Get-Content -LiteralPath (Join-Path $Root 'installer\Bundle.wxs') -Raw
Assert-True -Condition $bundleWxs.Contains('LaunchTarget="[InstallFolder]\logs\installation_summary.txt"') -Message 'Bootstrapper does not expose the installation summary from the success page.'

$installScript = Get-Content -LiteralPath (Join-Path $Root 'deployment\target-computer-tools\scripts\install_target_tools.ps1') -Raw
Assert-True -Condition $installScript.Contains('arcgis_python_executable') -Message 'Target install script does not rewrite arcgis_python_executable.'
Assert-True -Condition $installScript.Contains('case_folder_output_root') -Message 'Target install script does not rewrite case_folder_output_root.'
Assert-True -Condition $installScript.Contains('output_adapter_script_path') -Message 'Target install script does not rewrite output_adapter_script_path.'

Write-Host 'Installer packaging validation passed.'
