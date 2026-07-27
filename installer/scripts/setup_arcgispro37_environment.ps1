param(
    [Alias('A')]
    [string]$ArcGisProRoot = '',
    [Alias('I')]
    [string]$InstallRoot = '',
    [Alias('N')]
    [string]$EnvironmentName = 'arcgispro-survey-ai',
    [Alias('C')]
    [string]$CondaRequirements = '',
    [Alias('P')]
    [string]$PipRequirements = '',
    [Alias('L')]
    [string]$LogRoot = '',
    [Alias('S')]
    [string]$ScriptRoot = '',
    [switch]$Repair,
    [switch]$DryRun
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
        'Unknown setup error.'
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = 'Unknown setup error.'
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
        $fallbackLog = Join-Path $fallbackLogRoot 'setup_arcgispro37_environment_error.log'
        Add-Content -LiteralPath $fallbackLog -Value "[$(Get-Date -Format o)] ERROR $message"
        Add-Content -LiteralPath $fallbackLog -Value "RawInstallRoot=$InstallRoot"
        Add-Content -LiteralPath $fallbackLog -Value "RawLogRoot=$LogRoot"
        Add-Content -LiteralPath $fallbackLog -Value "RawScriptRoot=$ScriptRoot"
        if (-not [string]::IsNullOrWhiteSpace($stackTrace)) {
            Add-Content -LiteralPath $fallbackLog -Value $stackTrace
        }
        $logTail = @()
        if (-not [string]::IsNullOrWhiteSpace($script:CurrentSetupLogPath) -and (Test-Path -LiteralPath $script:CurrentSetupLogPath -PathType Leaf)) {
            $logTail = @(Get-Content -LiteralPath $script:CurrentSetupLogPath -Tail 80 -ErrorAction SilentlyContinue)
            if ($logTail.Count -gt 0) {
                Add-Content -LiteralPath $fallbackLog -Value 'Detailed setup log tail:'
                Add-Content -LiteralPath $fallbackLog -Value $logTail
            }
        }

        $status = [ordered]@{
            schema_version = 'parcel_workflow_arcgispro37_environment_status_v1'
            generated_at = (Get-Date).ToString('o')
            success = $false
            install_root = $InstallRoot
            arcgis_pro_root = $ArcGisProRoot
            environment_name = $EnvironmentName
            target_python_exe = ''
            conda_requirements = $CondaRequirements
            pip_requirements = $PipRequirements
            script_root = $ScriptRoot
            failed_phase = $script:CurrentSetupPhase
            error = $message
            log_path = $script:CurrentSetupLogPath
            log_tail = $logTail
        }
        $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fallbackLogRoot 'setup_arcgispro37_environment_status.json') -Encoding UTF8
    }
    catch {
        $trapWriteMessage = if ($_ -and $_.Exception) { $_.Exception.Message } else { $_.ToString() }
        [Console]::Error.WriteLine($trapWriteMessage)
    }

    [Console]::Error.WriteLine($message)
    exit 1
}

function Resolve-DefaultPath {
    param(
        [Parameter(Mandatory)][string]$Preferred,
        [Parameter(Mandatory)][string]$Fallback
    )

    if (-not [string]::IsNullOrWhiteSpace($Preferred)) {
        return Resolve-InstallerPathArgument $Preferred
    }

    return Resolve-InstallerPathArgument $Fallback
}

function Resolve-FirstExistingPath {
    param([Parameter(Mandatory)][string[]]$Candidates)

    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $fullPath = Resolve-InstallerPathArgument $candidate
        if (Test-Path -LiteralPath $fullPath) {
            return $fullPath
        }
    }

    foreach ($candidate in $Candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return Resolve-InstallerPathArgument $candidate
        }
    }

    throw 'No candidate paths were provided.'
}

function Find-ArcGisProRoot {
    param([string]$ExplicitRoot)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitRoot)) {
        $candidates += $ExplicitRoot
    }

    if ($env:ProgramFiles) {
        $candidates += (Join-Path $env:ProgramFiles 'ArcGIS\Pro')
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ($programFilesX86) {
        $candidates += (Join-Path $programFilesX86 'ArcGIS\Pro')
    }

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $root = [System.IO.Path]::GetFullPath($candidate)
        $python = Join-Path $root 'bin\Python\envs\arcgispro-py3\python.exe'
        $conda = Join-Path $root 'bin\Python\Scripts\conda.exe'
        if ((Test-Path -LiteralPath $python) -and (Test-Path -LiteralPath $conda)) {
            return $root
        }
    }

    throw "ArcGIS Pro 3.7 Python/conda tooling was not found. Install ArcGIS Pro 3.7 or pass -ArcGisProRoot."
}

function New-CommandRecord {
    param(
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [bool]$AllowFailure = $false
    )

    [ordered]@{
        phase = $Phase
        file_path = $FilePath
        arguments = $Arguments
        allow_failure = $AllowFailure
    }
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$LogPath,
        [bool]$AllowFailure = $false
    )

    Add-Content -LiteralPath $LogPath -Value "[$(Get-Date -Format o)] START $Phase"
    Add-Content -LiteralPath $LogPath -Value "$FilePath $($Arguments -join ' ')"
    $script:CurrentSetupPhase = $Phase
    $script:CurrentSetupLogPath = $LogPath

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = ConvertTo-ProcessArgumentString -Arguments $Arguments
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode

    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        Add-Content -LiteralPath $LogPath -Value $stdout.TrimEnd()
    }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Add-Content -LiteralPath $LogPath -Value "[stderr]"
        Add-Content -LiteralPath $LogPath -Value $stderr.TrimEnd()
    }

    Add-Content -LiteralPath $LogPath -Value "[$(Get-Date -Format o)] END $Phase exit_code=$exitCode"
    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            Add-Content -LiteralPath $LogPath -Value "[$(Get-Date -Format o)] WARNING $Phase failed but was configured as non-blocking."
            $script:CurrentSetupPhase = ''
            return [ordered]@{
                phase = $Phase
                exit_code = $exitCode
                ok = $false
                warning = $true
            }
        }

        throw "$Phase failed with exit code $exitCode. See log: $LogPath"
    }
    $script:CurrentSetupPhase = ''
    return [ordered]@{
        phase = $Phase
        exit_code = $exitCode
        ok = $true
        warning = $false
    }
}

function ConvertTo-ProcessArgumentString {
    param([string[]]$Arguments)

    $escapedArguments = foreach ($argument in $Arguments) {
        if ($null -eq $argument) {
            '""'
            continue
        }

        $text = [string]$argument
        if ($text.Length -eq 0) {
            '""'
            continue
        }

        if ($text -notmatch '[\s"]') {
            $text
            continue
        }

        '"' + ($text -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
    }

    return ($escapedArguments -join ' ')
}

function Test-Import {
    param(
        [Parameter(Mandatory)][string]$PythonExe,
        [Parameter(Mandatory)][string]$ImportCode,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$LogPath
    )

    Invoke-LoggedCommand `
        -Phase $Phase `
        -FilePath $PythonExe `
        -Arguments @('-c', $ImportCode) `
        -LogPath $LogPath
}

function Test-RequirementFileHasEntries {
    param([Parameter(Mandatory)][string]$Path)

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed) -and -not $trimmed.StartsWith('#')) {
            return $true
        }
    }

    return $false
}

$scriptRoot = if (-not [string]::IsNullOrWhiteSpace($ScriptRoot)) {
    Resolve-InstallerPathArgument $ScriptRoot
}
elseif ($MyInvocation -and $MyInvocation.MyCommand -and -not [string]::IsNullOrWhiteSpace($MyInvocation.MyCommand.Path)) {
    Resolve-InstallerPathArgument (Split-Path -Parent $MyInvocation.MyCommand.Path)
}
elseif (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Resolve-InstallerPathArgument $PSScriptRoot
}
else {
    throw 'Script root could not be resolved. Pass -ScriptRoot when running setup through a script block.'
}
$repoRootCandidate = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..'))
$cachedRequirementsCandidate = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot 'arcgispro37'))
$installedRequirementsCandidate = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..\arcgispro37'))

$resolvedCondaRequirements = if ([string]::IsNullOrWhiteSpace($CondaRequirements)) {
    Resolve-FirstExistingPath -Candidates @(
        (Join-Path $cachedRequirementsCandidate 'requirements-conda.txt'),
        (Join-Path $installedRequirementsCandidate 'requirements-conda.txt'),
        (Join-Path $repoRootCandidate 'docs\deployment\arcgispro37\requirements-conda.txt')
    )
}
else {
    [System.IO.Path]::GetFullPath($CondaRequirements)
}

$resolvedPipRequirements = if ([string]::IsNullOrWhiteSpace($PipRequirements)) {
    Resolve-FirstExistingPath -Candidates @(
        (Join-Path $cachedRequirementsCandidate 'requirements-pip.txt'),
        (Join-Path $installedRequirementsCandidate 'requirements-pip.txt'),
        (Join-Path $repoRootCandidate 'docs\deployment\arcgispro37\requirements-pip.txt')
    )
}
else {
    [System.IO.Path]::GetFullPath($PipRequirements)
}
$resolvedLogRoot = Resolve-DefaultPath `
    -Preferred $LogRoot `
    -Fallback (Join-Path $repoRootCandidate 'deployment\installer-logs')
$resolvedInstallRoot = if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    ''
}
else {
    Resolve-InstallerPathArgument $InstallRoot
}

if (-not (Test-Path -LiteralPath $resolvedCondaRequirements)) {
    throw "Conda requirements file not found: $resolvedCondaRequirements"
}

if (-not (Test-Path -LiteralPath $resolvedPipRequirements)) {
    throw "Pip requirements file not found: $resolvedPipRequirements"
}

New-Item -ItemType Directory -Path $resolvedLogRoot -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $resolvedLogRoot 'setup_arcgispro37_environment_error.log') -Force -ErrorAction SilentlyContinue
$logPath = Join-Path $resolvedLogRoot "setup_arcgispro37_environment_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
$planPath = Join-Path $resolvedLogRoot "setup_arcgispro37_environment_plan.json"

$resolvedArcGisRoot = Find-ArcGisProRoot -ExplicitRoot $ArcGisProRoot
$defaultPythonExe = Join-Path $resolvedArcGisRoot 'bin\Python\envs\arcgispro-py3\python.exe'
$condaExe = Join-Path $resolvedArcGisRoot 'bin\Python\Scripts\conda.exe'
$targetPythonExe = Join-Path $resolvedArcGisRoot "bin\Python\envs\$EnvironmentName\python.exe"

$commands = New-Object System.Collections.Generic.List[object]

if ($Repair -or -not (Test-Path -LiteralPath $targetPythonExe)) {
    $commands.Add((New-CommandRecord `
        -Phase 'conda-clone' `
        -FilePath $condaExe `
        -Arguments @('create', '--clone', 'arcgispro-py3', '--name', $EnvironmentName, '--pinned', '-y')))
}

if (Test-RequirementFileHasEntries -Path $resolvedCondaRequirements) {
    $commands.Add((New-CommandRecord `
        -Phase 'conda-install-requirements' `
        -FilePath $condaExe `
        -Arguments @('install', '--name', $EnvironmentName, '--file', $resolvedCondaRequirements, '-c', 'esri', '-c', 'conda-forge', '-c', 'defaults', '-y')))
}
$commands.Add((New-CommandRecord `
    -Phase 'pip-install-requirements' `
    -FilePath $targetPythonExe `
    -Arguments @('-m', 'pip', 'install', '-r', $resolvedPipRequirements)))
$commands.Add((New-CommandRecord `
    -Phase 'verify-arcpy' `
    -FilePath $targetPythonExe `
    -Arguments @('-c', "import arcpy; print('arcpy OK')") `
    -AllowFailure $true))
$commands.Add((New-CommandRecord `
    -Phase 'verify-ai-survey-imports' `
    -FilePath $targetPythonExe `
    -Arguments @('-c', "import openai; import flask; import pdfplumber; import pypdfium2; print('AI Survey required imports OK')")))
$commands.Add((New-CommandRecord `
    -Phase 'verify-clip-imports' `
    -FilePath $targetPythonExe `
    -Arguments @('-c', "import clip; import open_clip; print('CLIP imports OK')") `
    -AllowFailure $true))
$commands.Add((New-CommandRecord `
    -Phase 'verify-ai-survey-package-versions' `
    -FilePath $targetPythonExe `
    -Arguments @('-c', "import importlib.metadata as m; packages=['openai','openai-clip','open-clip-torch','Flask','pdfplumber','pypdfium2']; print('package_versions:' + ';'.join(f'{p}={m.version(p)}' for p in packages))")))

$plan = [ordered]@{
    schema_version = 'parcel_workflow_arcgispro37_environment_plan_v1'
    generated_at = (Get-Date).ToString('o')
    dry_run = [bool]$DryRun
    install_root = $resolvedInstallRoot
    arcgis_pro_root = $resolvedArcGisRoot
    default_python_exe = $defaultPythonExe
    conda_exe = $condaExe
    environment_name = $EnvironmentName
    target_python_exe = $targetPythonExe
    conda_requirements = $resolvedCondaRequirements
    pip_requirements = $resolvedPipRequirements
    log_path = $logPath
    commands = $commands
}

$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

if ($DryRun) {
    Write-Host "Dry run complete. Plan written to: $planPath"
    Write-Host "Target Python executable: $targetPythonExe"
    exit 0
}

Add-Content -LiteralPath $logPath -Value "Parcel Workflow ArcGIS Pro 3.7 environment setup"
Add-Content -LiteralPath $logPath -Value ($plan | ConvertTo-Json -Depth 8)

$commandResults = New-Object System.Collections.Generic.List[object]
foreach ($command in $commands) {
    $commandResult = Invoke-LoggedCommand `
        -Phase $command.phase `
        -FilePath $command.file_path `
        -Arguments ([string[]]$command.arguments) `
        -LogPath $logPath `
        -AllowFailure ([bool]$command.allow_failure)
    $commandResults.Add($commandResult) | Out-Null
}

$warnings = @($commandResults | Where-Object { $_.warning } | ForEach-Object {
    "$($_.phase) completed with warning exit code $($_.exit_code). Review the setup log."
})

$status = [ordered]@{
    schema_version = 'parcel_workflow_arcgispro37_environment_status_v1'
    generated_at = (Get-Date).ToString('o')
    success = $true
    install_root = $resolvedInstallRoot
    arcgis_pro_root = $resolvedArcGisRoot
    environment_name = $EnvironmentName
    target_python_exe = $targetPythonExe
    conda_requirements = $resolvedCondaRequirements
    pip_requirements = $resolvedPipRequirements
    verified_imports = @('openai', 'flask', 'pdfplumber', 'pypdfium2')
    verified_packages = @('openai', 'openai-clip', 'open-clip-torch', 'Flask', 'pdfplumber', 'pypdfium2')
    optional_imports = @('arcpy', 'clip', 'open_clip')
    warnings = $warnings
    log_path = $logPath
}
$status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedLogRoot 'setup_arcgispro37_environment_status.json') -Encoding UTF8

Write-Host "ArcGIS Pro environment setup complete."
Write-Host "Python executable: $targetPythonExe"
Write-Host "Log: $logPath"
