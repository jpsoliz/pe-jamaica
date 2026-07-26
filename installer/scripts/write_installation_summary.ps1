param(
    [Alias('I')]
    [string]$InstallRoot = 'C:\Sidwell\ParcelWorkflow',
    [Alias('L')]
    [string]$LogRoot = ''
)

$ErrorActionPreference = 'Stop'

function Resolve-InstallerPathArgument {
    param(
        [string]$Path,
        [string]$DefaultPath
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [System.IO.Path]::GetFullPath($DefaultPath)
    }

    $trimmedPath = $Path.Trim().Trim('"')
    if ($trimmedPath.EndsWith('\.')) {
        $trimmedPath = $trimmedPath.Substring(0, $trimmedPath.Length - 2)
    }

    return [System.IO.Path]::GetFullPath($trimmedPath)
}

function Get-DefaultProgramDataLogRoot {
    $programData = [Environment]::GetFolderPath('CommonApplicationData')
    if ([string]::IsNullOrWhiteSpace($programData)) {
        $programData = 'C:\ProgramData'
    }

    return (Join-Path $programData 'Sidwell\ParcelWorkflow\logs')
}

function Test-NonEmptyFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    return ((Get-Item -LiteralPath $Path).Length -gt 0)
}

function New-Check {
    param(
        [string]$Name,
        [string]$Path,
        [bool]$Ok,
        [string]$Detail = ''
    )

    return [ordered]@{
        name = $Name
        path = $Path
        ok = $Ok
        detail = $Detail
    }
}

function Get-LatestFile {
    param(
        [string]$Directory,
        [string]$Filter
    )

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $Directory -Filter $Filter -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Get-FirstMeaningfulLine {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ''
    }

    $line = Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    if ($null -eq $line) {
        return ''
    }

    return [string]$line
}

function Get-BatActionStatus {
    param(
        [string]$Name,
        [string]$Path,
        [string]$SuccessNeedle = 'Completed with exit code 0'
    )

    $exists = Test-NonEmptyFile $Path
    $content = if ($exists) { Get-Content -LiteralPath $Path -Raw -ErrorAction SilentlyContinue } else { '' }
    $ok = $exists -and $content.Contains($SuccessNeedle)
    $exitLine = ''
    if ($exists) {
        $exitLine = Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue |
            Where-Object { $_ -match 'Completed with exit code' } |
            Select-Object -Last 1
        if ($null -eq $exitLine) {
            $exitLine = Get-FirstMeaningfulLine -Path $Path
        }
    }

    return [ordered]@{
        name = $Name
        path = $Path
        ok = $ok
        status = if ($ok) { 'OK' } elseif ($exists) { 'WARNING' } else { 'NOT_RUN_OR_NO_LOG' }
        detail = if ($exists) { [string]$exitLine } else { 'Log file was not found.' }
    }
}

$resolvedInstallRoot = Resolve-InstallerPathArgument -Path $InstallRoot -DefaultPath 'C:\Sidwell\ParcelWorkflow'
$resolvedLogRoot = Resolve-InstallerPathArgument -Path $LogRoot -DefaultPath (Get-DefaultProgramDataLogRoot)
New-Item -ItemType Directory -Path $resolvedLogRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $resolvedInstallRoot 'logs') -Force -ErrorAction SilentlyContinue | Out-Null

$checks = New-Object System.Collections.Generic.List[object]
$checks.Add((New-Check -Name 'Install root' -Path $resolvedInstallRoot -Ok (Test-Path -LiteralPath $resolvedInstallRoot -PathType Container)))
$checks.Add((New-Check -Name 'Add-in package' -Path (Join-Path $resolvedInstallRoot 'AddIn\ParcelWorkflowAddIn.esriAddInX') -Ok (Test-NonEmptyFile (Join-Path $resolvedInstallRoot 'AddIn\ParcelWorkflowAddIn.esriAddInX'))))
$checks.Add((New-Check -Name 'Configured add-in package' -Path (Join-Path $resolvedInstallRoot 'AddIn\ParcelWorkflowAddIn.configured.esriAddInX') -Ok (Test-NonEmptyFile (Join-Path $resolvedInstallRoot 'AddIn\ParcelWorkflowAddIn.configured.esriAddInX'))))
$checks.Add((New-Check -Name 'Processing tools' -Path (Join-Path $resolvedInstallRoot 'ProcessingTools') -Ok (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot 'ProcessingTools') -PathType Container)))
$checks.Add((New-Check -Name 'Contracts' -Path (Join-Path $resolvedInstallRoot 'Contracts') -Ok (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot 'Contracts') -PathType Container)))
$checks.Add((New-Check -Name 'Local case folder' -Path (Join-Path $resolvedInstallRoot 'ParcelWorkflowCases') -Ok (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot 'ParcelWorkflowCases') -PathType Container)))
$checks.Add((New-Check -Name 'Local log folder' -Path (Join-Path $resolvedInstallRoot 'logs') -Ok (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot 'logs') -PathType Container)))
$checks.Add((New-Check -Name 'ProgramData log folder' -Path $resolvedLogRoot -Ok (Test-Path -LiteralPath $resolvedLogRoot -PathType Container)))
$checks.Add((New-Check -Name 'Conda requirements' -Path (Join-Path $resolvedInstallRoot 'installer\arcgispro37\requirements-conda.txt') -Ok (Test-NonEmptyFile (Join-Path $resolvedInstallRoot 'installer\arcgispro37\requirements-conda.txt'))))
$checks.Add((New-Check -Name 'Pip requirements' -Path (Join-Path $resolvedInstallRoot 'installer\arcgispro37\requirements-pip.txt') -Ok (Test-NonEmptyFile (Join-Path $resolvedInstallRoot 'installer\arcgispro37\requirements-pip.txt'))))

$setupStatusPath = Join-Path $resolvedLogRoot 'setup_arcgispro37_environment_status.json'
$setupErrorPath = Join-Path $resolvedLogRoot 'setup_arcgispro37_environment_error.log'
$setupBatLogPath = Join-Path $resolvedLogRoot 'setup_arcgispro37_environment_bat.log'
$latestSetupLog = Get-LatestFile -Directory $resolvedLogRoot -Filter 'setup_arcgispro37_environment_*.log'
$setupSuccess = $false
$setupDetail = ''

if (Test-NonEmptyFile $setupStatusPath) {
    try {
        $setupStatus = Get-Content -LiteralPath $setupStatusPath -Raw | ConvertFrom-Json
        $setupSuccess = [bool]$setupStatus.success
        if ($setupSuccess) {
            $setupDetail = "Environment '$($setupStatus.environment_name)' configured."
        } else {
            $setupDetail = 'Environment status file exists but does not report success.'
        }
    } catch {
        $setupDetail = "Environment status file could not be parsed: $($_.Exception.Message)"
    }
} elseif (Test-NonEmptyFile $setupErrorPath) {
    $setupDetail = Get-FirstMeaningfulLine -Path $setupErrorPath
} elseif (Test-NonEmptyFile $setupBatLogPath) {
    $setupDetail = 'Environment setup did not report success. Review setup_arcgispro37_environment_bat.log.'
} else {
    $setupDetail = 'Environment setup status was not found.'
}

$registerSummaryPath = Join-Path $resolvedLogRoot 'install_path_summary.json'
$registerErrorPath = Join-Path $resolvedLogRoot 'register_parcel_workflow_addin_error.log'
$registerBatLogPath = Join-Path $resolvedLogRoot 'register_parcel_workflow_addin_bat.log'
$latestRegisterLog = Get-LatestFile -Directory $resolvedLogRoot -Filter 'register_parcel_workflow_addin_*.log'
$registerSuccess = Test-NonEmptyFile $registerSummaryPath
$registerDetail = if ($registerSuccess) {
    'Add-in path summary created.'
} elseif (Test-NonEmptyFile $registerErrorPath) {
    Get-FirstMeaningfulLine -Path $registerErrorPath
} elseif (Test-NonEmptyFile $registerBatLogPath) {
    'Add-in registration did not report success. Review register_parcel_workflow_addin_bat.log.'
} else {
    'Add-in registration status was not found.'
}

$openAiValue = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'Machine')
$openAiPresent = -not [string]::IsNullOrWhiteSpace($openAiValue)
$postInstallActions = @(
    (Get-BatActionStatus -Name 'setup_arcgispro37_environment.bat' -Path $setupBatLogPath),
    (Get-BatActionStatus -Name 'register_parcel_workflow_addin.bat' -Path $registerBatLogPath),
    ([ordered]@{
        name = 'write_installation_summary.ps1'
        path = $MyInvocation.MyCommand.Path
        ok = $true
        status = 'OK'
        detail = 'Installation summary was written.'
    })
)

$requiredPayloadNames = @('Install root', 'Add-in package', 'Processing tools', 'Contracts', 'Local case folder', 'ProgramData log folder', 'Conda requirements', 'Pip requirements')
$payloadChecks = @($checks | Where-Object { $requiredPayloadNames -contains $_.name })
$payloadOk = ($payloadChecks | Where-Object { -not $_.ok }).Count -eq 0
$configurationOk = $setupSuccess -and $registerSuccess
$overallStatus = if ($payloadOk -and $configurationOk) {
    'Complete'
} elseif ($payloadOk) {
    'CompletedWithWarnings'
} else {
    'Failed'
}

$summary = [ordered]@{
    product = 'Sidwell Parcel Workflow'
    status = $overallStatus
    created_at = (Get-Date).ToString('o')
    install_root = $resolvedInstallRoot
    log_root = $resolvedLogRoot
    installed_items = $checks
    post_install_actions = $postInstallActions
    arcgis_python_environment = [ordered]@{
        ok = $setupSuccess
        status_path = $setupStatusPath
        error_path = $setupErrorPath
        batch_log_path = $setupBatLogPath
        latest_log_path = if ($latestSetupLog) { $latestSetupLog.FullName } else { '' }
        detail = $setupDetail
    }
    addin_registration = [ordered]@{
        ok = $registerSuccess
        summary_path = $registerSummaryPath
        error_path = $registerErrorPath
        batch_log_path = $registerBatLogPath
        latest_log_path = if ($latestRegisterLog) { $latestRegisterLog.FullName } else { '' }
        detail = $registerDetail
    }
    openai_api_key = [ordered]@{
        configured = $openAiPresent
        environment_variable = 'OPENAI_API_KEY'
        detail = if ($openAiPresent) { 'Machine environment variable is configured.' } else { 'Machine environment variable is not configured. Pass OpenAiApiKey during setup if AI extraction is required.' }
    }
}

$jsonPath = Join-Path $resolvedLogRoot 'installation_summary.json'
$textPath = Join-Path $resolvedLogRoot 'installation_summary.txt'
$localJsonPath = Join-Path $resolvedInstallRoot 'logs\installation_summary.json'
$localTextPath = Join-Path $resolvedInstallRoot 'logs\installation_summary.txt'
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('Sidwell Parcel Workflow installation summary')
$lines.Add("Status: $overallStatus")
$lines.Add("Created: $($summary.created_at)")
$lines.Add("Install root: $resolvedInstallRoot")
$lines.Add("Log root: $resolvedLogRoot")
$lines.Add('')
$lines.Add('Installed items:')
foreach ($check in $checks) {
    $marker = if ($check.ok) { 'OK' } else { 'MISSING' }
    $lines.Add("[$marker] $($check.name): $($check.path)")
}
$lines.Add('')
$lines.Add('Post-install actions:')
foreach ($action in $postInstallActions) {
    $marker = if ($action.ok) { 'OK' } else { $action.status }
    $lines.Add("[$marker] $($action.name): $($action.detail)")
    $lines.Add("    Log: $($action.path)")
}
$lines.Add('')
$lines.Add("ArcGIS Pro Python environment: $(if ($setupSuccess) { 'OK' } else { 'WARNING' })")
$lines.Add($setupDetail)
$lines.Add('')
$lines.Add("Add-in registration: $(if ($registerSuccess) { 'OK' } else { 'WARNING' })")
$lines.Add($registerDetail)
$lines.Add('')
$lines.Add("OpenAI API key: $(if ($openAiPresent) { 'Configured' } else { 'Not configured' })")
if (-not $openAiPresent) {
    $lines.Add('Pass OpenAiApiKey during setup if AI extraction is required.')
}
$lines.Add('')
$lines.Add('Next action:')
if ($overallStatus -eq 'Complete') {
    $lines.Add('Open ArcGIS Pro and verify the Sidwell Parcel Workflow add-in loads.')
} elseif ($payloadOk) {
    $lines.Add('Review the warning logs listed above. The main files are installed, but one or more post-install configuration steps did not finish.')
} else {
    $lines.Add('Review the MSI/Burn installer log. Required payload files are missing.')
}

$lines | Set-Content -LiteralPath $textPath -Encoding UTF8
try {
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $localJsonPath -Encoding UTF8
    $lines | Set-Content -LiteralPath $localTextPath -Encoding UTF8
} catch {
    Write-Host "WARNING: Could not write local installation summary: $($_.Exception.Message)"
}

Write-Host "Parcel Workflow installation status: $overallStatus"
Write-Host "Installation summary: $textPath"
