param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj'
$projectDir = Split-Path -Parent $projectPath
$configDamlPath = Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml'
$outputDir = Join-Path $Root "src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/bin/$Configuration/net8.0-windows"
$packagePath = Join-Path $outputDir 'ParcelWorkflowAddIn.esriAddInX'
$sdkTargets = 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'
$msbuildCandidates = @(
    'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe',
    'C:\Program Files\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\amd64\MSBuild.exe'
)

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Add-in project not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $configDamlPath)) {
    throw "Add-in manifest not found: $configDamlPath"
}

if (-not (Test-Path -LiteralPath $sdkTargets)) {
    throw "ArcGIS Pro SDK packaging targets not found: $sdkTargets"
}

$msbuild = $msbuildCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) {
    throw "Visual Studio MSBuild.exe was not found. ArcGIS Pro SDK packaging cannot run with dotnet build because Esri's packaging targets use CodeTaskFactory."
}

function Get-NextPatchVersion {
    param([string]$ProjectText)

    $match = [regex]::Match($ProjectText, '<Version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)</Version>')
    if (-not $match.Success) {
        throw "Could not find a numeric <Version>major.minor.patch</Version> in $projectPath"
    }

    $major = [int]$match.Groups['major'].Value
    $minor = [int]$match.Groups['minor'].Value
    $patch = [int]$match.Groups['patch'].Value + 1
    return "$major.$minor.$patch"
}

function Update-RequiredVersionText {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Replacement,
        [string]$Description
    )

    $updated = [regex]::Replace($Text, $Pattern, $Replacement, 1)
    if ($updated -eq $Text) {
        throw "Could not update $Description."
    }

    return $updated
}

$originalProjectText = Get-Content -LiteralPath $projectPath -Raw
$originalConfigDamlText = Get-Content -LiteralPath $configDamlPath -Raw
$nextVersion = Get-NextPatchVersion -ProjectText $originalProjectText
$nextAssemblyVersion = "$nextVersion.0"

$updatedProjectText = $originalProjectText
$updatedProjectText = Update-RequiredVersionText $updatedProjectText '<Version>\d+\.\d+\.\d+</Version>' "<Version>$nextVersion</Version>" 'project Version'
$updatedProjectText = Update-RequiredVersionText $updatedProjectText '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>' "<AssemblyVersion>$nextAssemblyVersion</AssemblyVersion>" 'project AssemblyVersion'
$updatedProjectText = Update-RequiredVersionText $updatedProjectText '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>' "<FileVersion>$nextAssemblyVersion</FileVersion>" 'project FileVersion'
$updatedProjectText = Update-RequiredVersionText $updatedProjectText '<InformationalVersion>\d+\.\d+\.\d+</InformationalVersion>' "<InformationalVersion>$nextVersion</InformationalVersion>" 'project InformationalVersion'

$updatedConfigDamlText = Update-RequiredVersionText `
    $originalConfigDamlText `
    '(<AddInInfo\b[^>]*\bversion=")\d+\.\d+\.\d+(")' `
    "`${1}$nextVersion`${2}" `
    'Config.daml AddInInfo version'

Set-Content -LiteralPath $projectPath -Value $updatedProjectText -NoNewline
Set-Content -LiteralPath $configDamlPath -Value $updatedConfigDamlText -NoNewline
Write-Host "Bumped add-in patch version to $nextVersion."

$objDir = Join-Path $projectDir 'obj'
try {
    if (Test-Path -LiteralPath $objDir) {
        $resolvedProjectDir = (Resolve-Path -LiteralPath $projectDir).Path
        $resolvedObjDir = (Resolve-Path -LiteralPath $objDir).Path
        if (-not $resolvedObjDir.StartsWith($resolvedProjectDir, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean intermediate path outside project: $resolvedObjDir"
        }

        try {
            Remove-Item -LiteralPath $resolvedObjDir -Recurse -Force
        }
        catch {
            Write-Warning "Could not fully clean generated obj folder '$resolvedObjDir'. Continuing because stale obj files are excluded from the package build. Details: $($_.Exception.Message)"
        }
    }

    & $msbuild $projectPath /restore /t:Rebuild /p:Configuration=$Configuration /p:UseSharedCompilation=false /nr:false /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Add-in package build failed."
    }

    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected add-in package was not produced: $packagePath"
    }
}
catch {
    Set-Content -LiteralPath $projectPath -Value $originalProjectText -NoNewline
    Set-Content -LiteralPath $configDamlPath -Value $originalConfigDamlText -NoNewline
    Write-Warning "Package build failed; restored add-in version files to their previous values."
    throw
}

Write-Host "Add-in package produced: $packagePath"
