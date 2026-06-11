param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$log = Join-Path $env:TEMP "pe-jamaica-readiness-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

function Invoke-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    Write-Host "=> $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
    Write-Host "✅ $Name"
}

function Write-FailureHint {
    param([string]$Message)
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -LiteralPath $log -Value "[$stamp] ERROR: $Message"
}

try {
    Write-Host "ArcGIS Pro Add-In readiness checks: $root"
    Write-Host "Log: $log"

    Invoke-Step 'validate_contracts.ps1' {
        & (Join-Path $PSScriptRoot 'validate_contracts.ps1') -Root $root
    }

    Invoke-Step 'run_python_tests.ps1' {
        & (Join-Path $PSScriptRoot 'run_python_tests.ps1')
    }

    Invoke-Step 'dotnet build (no restore)' {
        dotnet build "src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln" --no-restore --nologo -c $Configuration | Tee-Object -FilePath $log -Append
    }

    Invoke-Step 'dotnet tests (console harness)' {
        dotnet run --project "src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj" --nologo | Tee-Object -FilePath $log -Append
    }

    if (-not $SkipPackage) {
        Invoke-Step 'package_addin.ps1' {
            & (Join-Path $PSScriptRoot 'package_addin.ps1') -Root $root -Configuration $Configuration | Tee-Object -FilePath $log -Append
        }
    }

    Write-Host ''
    Write-Host "All readiness checks passed."
    if (-not $SkipPackage) {
        Write-Host "Package path: src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/bin/$Configuration/net8.0-windows/ParcelWorkflowAddIn.esriAddInX"
    }
}
catch {
    Write-FailureHint $_.Exception.Message

    if ($_.Exception.Message -like '*Microsoft SDKs*denied*') {
        Write-Host ''
        Write-Host 'Hint: SDK metadata permission error detected. Run this command from an elevated PowerShell session or ensure write/read access to:'
        Write-Host 'C:\Users\js91482\AppData\Local\Microsoft SDKs'
    }

    Write-Host ''
    Write-Host "Readiness checks failed. See log: $log"
    throw
}
