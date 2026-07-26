param(
    [Alias('I')]
    [string]$InstallRoot = 'C:\Sidwell\ParcelWorkflow',
    [Alias('P')]
    [string]$PythonExe = '',
    [Alias('L')]
    [string]$LogRoot = '',
    [Alias('E')]
    [string]$OpenAiApiKeyEnvironmentVariable = 'OPENAI_API_KEY',
    [Alias('K')]
    [string]$OpenAiApiKey = '',
    [ValidateSet('User', 'Machine')]
    [Alias('T')]
    [string]$OpenAiApiKeyTarget = 'Machine'
)

$ErrorActionPreference = 'Stop'

function Resolve-InstallerPathArgument {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'A required installer path argument was empty.'
    }

    $cleanValue = $Value.Trim()
    while ($cleanValue.Length -ge 2 -and $cleanValue.StartsWith('"') -and $cleanValue.EndsWith('"')) {
        $cleanValue = $cleanValue.Substring(1, $cleanValue.Length - 2).Trim()
    }

    return [System.IO.Path]::GetFullPath($cleanValue)
}

trap {
    $errorRecord = $_
    $message = if ($errorRecord -and $errorRecord.Exception) {
        $errorRecord.Exception.Message
    }
    elseif ($errorRecord) {
        $errorRecord.ToString()
    }
    else {
        'Unknown add-in registration error.'
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = 'Unknown add-in registration error.'
    }
    $stackTrace = if ($errorRecord -and $errorRecord.ScriptStackTrace) {
        $errorRecord.ScriptStackTrace | Out-String
    }
    else {
        ''
    }
    $fallbackLogRoot = Join-Path $env:ProgramData 'Sidwell\ParcelWorkflow\logs'

    try {
        if (-not [string]::IsNullOrWhiteSpace($LogRoot)) {
            $fallbackLogRoot = Resolve-InstallerPathArgument $LogRoot
        }

        New-Item -ItemType Directory -Path $fallbackLogRoot -Force | Out-Null
        $fallbackLog = Join-Path $fallbackLogRoot 'register_parcel_workflow_addin_error.log'
        Add-Content -LiteralPath $fallbackLog -Value "[$(Get-Date -Format o)] ERROR $message"
        Add-Content -LiteralPath $fallbackLog -Value "RawInstallRoot=$InstallRoot"
        Add-Content -LiteralPath $fallbackLog -Value "RawLogRoot=$LogRoot"
        if (-not [string]::IsNullOrWhiteSpace($stackTrace)) {
            Add-Content -LiteralPath $fallbackLog -Value $stackTrace
        }
    }
    catch {
        $trapWriteMessage = if ($_ -and $_.Exception) { $_.Exception.Message } else { $_.ToString() }
        [Console]::Error.WriteLine($trapWriteMessage)
    }

    [Console]::Error.WriteLine($message)
    exit 1
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

function Resolve-ArcGisPython {
    param([string]$PreferredPython)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($PreferredPython)) {
        $candidates += $PreferredPython
    }

    if ($env:ProgramFiles) {
        $proRoot = Join-Path $env:ProgramFiles 'ArcGIS\Pro'
        $candidates += (Join-Path $proRoot 'bin\Python\envs\arcgispro-survey-ai\python.exe')
        $candidates += (Join-Path $proRoot 'bin\Python\envs\arcgispro-py3\python.exe')
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ($programFilesX86) {
        $proRoot = Join-Path $programFilesX86 'ArcGIS\Pro'
        $candidates += (Join-Path $proRoot 'bin\Python\envs\arcgispro-survey-ai\python.exe')
        $candidates += (Join-Path $proRoot 'bin\Python\envs\arcgispro-py3\python.exe')
    }

    foreach ($candidate in $candidates) {
        if ((-not [string]::IsNullOrWhiteSpace($candidate)) -and (Test-Path -LiteralPath $candidate)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    return ''
}

function Update-AddInPackageSettings {
    param(
        [Parameter(Mandatory)][string]$SourceAddInPath,
        [Parameter(Mandatory)][string]$DestinationAddInPath,
        [Parameter(Mandatory)][string]$ConfiguredPythonExe,
        [Parameter(Mandatory)][string]$ConfiguredInstallRoot,
        [Parameter(Mandatory)][string]$ConfiguredOpenAiApiKeyEnvironmentVariable
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
        Set-JsonStringProperty -JsonObject $settings -Name 'case_folder_output_root' -Value (Join-Path $ConfiguredInstallRoot 'ParcelWorkflowCases')
        Set-JsonStringProperty -JsonObject $settings -Name 'output_adapter_script_path' -Value (Join-Path $toolsRoot 'adapters/output_adapter.py')
        Set-JsonStringProperty -JsonObject $settings -Name 'validation_adapter_script_path' -Value (Join-Path $toolsRoot 'adapters/validation_adapter.py')
        Set-JsonStringProperty -JsonObject $settings -Name 'validation_rules_path' -Value (Join-Path $toolsRoot 'rules/rules.yaml')
        Set-JsonStringProperty -JsonObject $settings -Name 'openai_api_key_environment_variable' -Value $ConfiguredOpenAiApiKeyEnvironmentVariable

        if ($settings.enterprise_working_admin) {
            Set-JsonStringProperty -JsonObject $settings.enterprise_working_admin -Name 'provisioning_script_path' -Value (Join-Path $toolsRoot 'admin/provision_enterprise_working_layers.py')
        }

        $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

        $configuredZip = "$DestinationAddInPath.zip"
        Remove-Item -LiteralPath $DestinationAddInPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $configuredZip -Force -ErrorAction SilentlyContinue

        Compress-Archive -Path (Join-Path $expanded '*') -DestinationPath $configuredZip -Force
        Move-Item -LiteralPath $configuredZip -Destination $DestinationAddInPath -Force
    }
    finally {
        Remove-Item -LiteralPath $tempZip -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

$resolvedInstallRoot = Resolve-InstallerPathArgument $InstallRoot
$resolvedLogRoot = if (-not [string]::IsNullOrWhiteSpace($LogRoot)) {
    Resolve-InstallerPathArgument $LogRoot
}
else {
    Resolve-InstallerPathArgument (Join-Path $env:ProgramData 'Sidwell\ParcelWorkflow\logs')
}

New-Item -ItemType Directory -Path $resolvedLogRoot -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $resolvedLogRoot 'register_parcel_workflow_addin_error.log') -Force -ErrorAction SilentlyContinue
$logPath = Join-Path $resolvedLogRoot "register_parcel_workflow_addin_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

Add-Content -LiteralPath $logPath -Value "Parcel Workflow add-in configuration"
Add-Content -LiteralPath $logPath -Value "InstallRoot=$resolvedInstallRoot"
Add-Content -LiteralPath $logPath -Value "LogRoot=$resolvedLogRoot"
Add-Content -LiteralPath $logPath -Value "OpenAiApiKeyEnvironmentVariable=$OpenAiApiKeyEnvironmentVariable"
Add-Content -LiteralPath $logPath -Value "OpenAiApiKeyProvided=$(-not [string]::IsNullOrWhiteSpace($OpenAiApiKey))"
Add-Content -LiteralPath $logPath -Value "OpenAiApiKeyTarget=$OpenAiApiKeyTarget"

if (-not (Test-Path -LiteralPath $resolvedInstallRoot)) {
    throw "Install root not found: $resolvedInstallRoot"
}

$sourceAddIn = Join-Path $resolvedInstallRoot 'AddIn\ParcelWorkflowAddIn.esriAddInX'
$configuredAddIn = Join-Path $resolvedInstallRoot 'AddIn\ParcelWorkflowAddIn.configured.esriAddInX'
$resolvedPythonExe = Resolve-ArcGisPython -PreferredPython $PythonExe

if (-not (Test-Path -LiteralPath $sourceAddIn)) {
    throw "Source add-in package not found: $sourceAddIn"
}

if ([string]::IsNullOrWhiteSpace($resolvedPythonExe)) {
    throw "ArcGIS Pro Python was not found. Install ArcGIS Pro or pass -PythonExe."
}

if (-not [string]::IsNullOrWhiteSpace($OpenAiApiKey)) {
    $environmentTarget = [EnvironmentVariableTarget]::$OpenAiApiKeyTarget
    [Environment]::SetEnvironmentVariable($OpenAiApiKeyEnvironmentVariable, $OpenAiApiKey, $environmentTarget)
    Add-Content -LiteralPath $logPath -Value "OpenAI API key environment variable was set."
}
else {
    Add-Content -LiteralPath $logPath -Value "OpenAI API key was not provided; existing environment value was left unchanged."
}

$openAiMachineValue = [Environment]::GetEnvironmentVariable($OpenAiApiKeyEnvironmentVariable, [EnvironmentVariableTarget]::Machine)
$openAiUserValue = [Environment]::GetEnvironmentVariable($OpenAiApiKeyEnvironmentVariable, [EnvironmentVariableTarget]::User)
$openAiProcessValue = [Environment]::GetEnvironmentVariable($OpenAiApiKeyEnvironmentVariable, [EnvironmentVariableTarget]::Process)
$openAiKeyPresent = (-not [string]::IsNullOrWhiteSpace($openAiMachineValue)) -or
    (-not [string]::IsNullOrWhiteSpace($openAiUserValue)) -or
    (-not [string]::IsNullOrWhiteSpace($openAiProcessValue))
Add-Content -LiteralPath $logPath -Value "OpenAiApiKeyPresent=$openAiKeyPresent"

Update-AddInPackageSettings `
    -SourceAddInPath $sourceAddIn `
    -DestinationAddInPath $configuredAddIn `
    -ConfiguredPythonExe $resolvedPythonExe `
    -ConfiguredInstallRoot $resolvedInstallRoot `
    -ConfiguredOpenAiApiKeyEnvironmentVariable $OpenAiApiKeyEnvironmentVariable

Add-Content -LiteralPath $logPath -Value "ConfiguredAddIn=$configuredAddIn"
Add-Content -LiteralPath $logPath -Value "PythonExe=$resolvedPythonExe"

$installSummary = [ordered]@{
    schema_version = 'parcel_workflow_install_path_summary_v1'
    generated_at = (Get-Date).ToString('o')
    install_root = $resolvedInstallRoot
    log_root = $resolvedLogRoot
    addin_source = $sourceAddIn
    addin_configured = $configuredAddIn
    processing_tools_root = (Join-Path $resolvedInstallRoot 'ProcessingTools')
    cases_root = (Join-Path $resolvedInstallRoot 'ParcelWorkflowCases')
    python_executable = $resolvedPythonExe
    openai_api_key_environment_variable = $OpenAiApiKeyEnvironmentVariable
    openai_api_key_provided = (-not [string]::IsNullOrWhiteSpace($OpenAiApiKey))
    openai_api_key_present = $openAiKeyPresent
    openai_api_key_target = $OpenAiApiKeyTarget
}
$installSummary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedLogRoot 'install_path_summary.json') -Encoding UTF8

Start-Process -FilePath $configuredAddIn
Add-Content -LiteralPath $logPath -Value "Add-in registration launched."
