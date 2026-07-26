param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipStage,
    [switch]$SkipBootstrapper,
    [switch]$Sign,
    [string]$SignToolPath,
    [string]$CertificatePath,
    [string]$CertificatePassword = $env:PARCEL_WORKFLOW_SIGN_CERT_PASSWORD,
    [string]$CertificateThumbprint,
    [string]$CertificateStoreName = 'My',
    [switch]$MachineCertificateStore,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$AcceptWixEula
)

$ErrorActionPreference = 'Stop'

$installerProject = Join-Path $Root 'installer\ParcelWorkflowInstaller.wixproj'
$bootstrapperProject = Join-Path $Root 'installer\ParcelWorkflowBootstrapper.wixproj'
$addinProject = Join-Path $Root 'src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\ParcelWorkflowAddIn.csproj'
$installerOutput = Join-Path $Root "installer\bin\$Configuration\ParcelWorkflowInstaller.msi"
$bootstrapperOutput = Join-Path $Root "installer\bin\$Configuration\ParcelWorkflowSetup.exe"

function Invoke-WixEulaAcceptance {
    $wix = Get-Command wix.exe -ErrorAction SilentlyContinue
    if (-not $wix) {
        throw "wix.exe was not found. Install the WiX .NET tool with: dotnet tool install --global wix --version 7.0.0"
    }

    & $wix.Source eula accept wix7
    if ($LASTEXITCODE -ne 0) {
        throw "WiX OSMF EULA acceptance failed."
    }
}

function Resolve-SignTool {
    param([string]$ConfiguredPath)

    if ($ConfiguredPath) {
        if (-not (Test-Path -LiteralPath $ConfiguredPath)) {
            throw "signtool.exe was not found at: $ConfiguredPath"
        }

        return (Resolve-Path -LiteralPath $ConfiguredPath).Path
    }

    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitsRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or Visual Studio Build Tools, or pass -SignToolPath."
}

function Invoke-CodeSign {
    param(
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Cannot sign missing file: $Path"
    }

    if (-not $CertificatePath -and -not $CertificateThumbprint) {
        throw "Signing requested, but no certificate was configured. Pass -CertificatePath or -CertificateThumbprint."
    }

    $tool = Resolve-SignTool -ConfiguredPath $SignToolPath
    $arguments = @('sign', '/fd', 'SHA256', '/td', 'SHA256', '/tr', $TimestampUrl)

    if ($CertificatePath) {
        if (-not (Test-Path -LiteralPath $CertificatePath)) {
            throw "Certificate file was not found: $CertificatePath"
        }

        $arguments += @('/f', (Resolve-Path -LiteralPath $CertificatePath).Path)
        if ($CertificatePassword) {
            $arguments += @('/p', $CertificatePassword)
        }
    }
    else {
        $arguments += @('/sha1', $CertificateThumbprint, '/s', $CertificateStoreName)
        if ($MachineCertificateStore) {
            $arguments += '/sm'
        }
    }

    $arguments += $Path
    & $tool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Code signing failed for: $Path"
    }
}

if (-not $SkipStage) {
    & (Join-Path $Root 'tools\stage_target_deployment.ps1') -Root $Root -Configuration $Configuration
}

$addinProjectXml = [xml](Get-Content -Raw -LiteralPath $addinProject)
$installerVersion = $addinProjectXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($installerVersion)) {
    throw "Could not read installer version from: $addinProject"
}

if ($AcceptWixEula) {
    Invoke-WixEulaAcceptance
}

& dotnet build $installerProject -c $Configuration -p:InstallerVersion=$installerVersion
if ($LASTEXITCODE -ne 0) {
    throw "WiX MSI build failed."
}

if (-not (Test-Path -LiteralPath $installerOutput)) {
    throw "Expected MSI was not produced: $installerOutput"
}

if ($Sign) {
    Invoke-CodeSign -Path $installerOutput
}

if (-not $SkipBootstrapper) {
    Copy-Item -LiteralPath $installerOutput -Destination (Join-Path $Root 'installer\ParcelWorkflowInstaller.msi') -Force
    try {
        & dotnet build $bootstrapperProject -c $Configuration -p:InstallerVersion=$installerVersion
        if ($LASTEXITCODE -ne 0) {
            throw "WiX bootstrapper build failed."
        }

        if (-not (Test-Path -LiteralPath $bootstrapperOutput)) {
            throw "Expected bootstrapper was not produced: $bootstrapperOutput"
        }

        if ($Sign) {
            Invoke-CodeSign -Path $bootstrapperOutput
        }
    }
    finally {
        Remove-Item -LiteralPath (Join-Path $Root 'installer\ParcelWorkflowInstaller.msi') -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "MSI: $installerOutput"
Write-Host "Installer version: $installerVersion"
if (-not $SkipBootstrapper) {
    Write-Host "Bootstrapper: $bootstrapperOutput"
}
if ($Sign) {
    Write-Host "Signing: completed"
}
