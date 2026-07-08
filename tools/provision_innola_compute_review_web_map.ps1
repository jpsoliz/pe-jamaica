param(
    [ValidateSet("validate", "export-config", "provision")]
    [string]$Mode = "export-config",

    [string]$ConfigPath = "src\ParcelWorkflowAddIn\ParcelWorkflowAddIn\Settings\WorkflowSettings.json",

    [string]$OutputJson = "",

    [switch]$Live,

    [switch]$AllowSourceServiceMap,

    [string]$PortalToken = "",

    [string]$PythonExe = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$resolvedConfigPath = Resolve-Path -LiteralPath $ConfigPath
$settings = Get-Content -LiteralPath $resolvedConfigPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($PythonExe)) {
    $PythonExe = [string]$settings.arcgis_python_executable
}

if ([string]::IsNullOrWhiteSpace($PythonExe)) {
    $PythonExe = "python"
}

if ($PythonExe -ne "python" -and -not (Test-Path -LiteralPath $PythonExe)) {
    throw "Configured ArcGIS Python executable was not found: $PythonExe"
}

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $outputDir = Join-Path $repoRoot "tmp"
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    $OutputJson = Join-Path $outputDir "enterprise_innola_map_view_$Mode.json"
}

if (-not [string]::IsNullOrWhiteSpace($PortalToken)) {
    $env:ARCGIS_PORTAL_TOKEN = $PortalToken
}

if ($Live -and [string]::IsNullOrWhiteSpace($env:ARCGIS_PORTAL_TOKEN)) {
    throw "Live provisioning requires ARCGIS_PORTAL_TOKEN. Pass -PortalToken or set `$env:ARCGIS_PORTAL_TOKEN first."
}

$scriptPath = Join-Path $repoRoot "src\ProcessingTools\admin\provision_innola_compute_review_web_map.py"
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Innola map provisioning script was not found: $scriptPath"
}

$arguments = @(
    $scriptPath,
    $Mode,
    "--config",
    $resolvedConfigPath,
    "--output-json",
    $OutputJson
)

if ($Live) {
    $arguments += "--no-dry-run"
}

if ($AllowSourceServiceMap) {
    $arguments += "--allow-source-service-map"
}

Write-Host "Mode: $Mode"
Write-Host "Config: $resolvedConfigPath"
Write-Host "Python: $PythonExe"
Write-Host "Output: $OutputJson"
if ($Live) {
    Write-Host "Live: true"
} else {
    Write-Host "Live: false (dry-run)"
}
if ($AllowSourceServiceMap) {
    Write-Host "Allow source service map fallback: true"
}

& $PythonExe @arguments
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "Provisioning command failed. See output JSON for diagnostics:" -ForegroundColor Red
    Write-Host $OutputJson
    exit $exitCode
}

Write-Host ""
Write-Host "Provisioning command completed." -ForegroundColor Green
Write-Host "Diagnostics: $OutputJson"
