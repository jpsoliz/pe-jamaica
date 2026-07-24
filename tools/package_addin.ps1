param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj'
$projectDir = Split-Path -Parent $projectPath
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

if (-not (Test-Path -LiteralPath $sdkTargets)) {
    throw "ArcGIS Pro SDK packaging targets not found: $sdkTargets"
}

$msbuild = $msbuildCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) {
    throw "Visual Studio MSBuild.exe was not found. ArcGIS Pro SDK packaging cannot run with dotnet build because Esri's packaging targets use CodeTaskFactory."
}

$objDir = Join-Path $projectDir 'obj'
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

Write-Host "Add-in package produced: $packagePath"
