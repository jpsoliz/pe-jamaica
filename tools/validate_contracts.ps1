param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$requiredPaths = @(
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Module1.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpane.xaml.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ShowParcelWorkflowDockpaneButton.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/RelayCommand.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderCreationResult.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderReopenResult.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderLayout.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/CaseFolderStore.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/AvailableArtifact.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/RecoverabilityIssue.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/ISourceFileLauncher.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyBatchResult.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyResult.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileCopyService.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileAction.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionAuditDocument.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionAuditService.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionResult.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/SourceFileActionService.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/CaseFolders/WindowsSourceFileLauncher.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestDocument.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Contracts/ManifestSerializer.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/DetectedSourceInputProfile.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfile.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceInputProfileDetector.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Intake/SourceRole.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowSession.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowState.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/WorkflowStateExtensions.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/CaseFolderStoreTests.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/SourceFileActionServiceTests.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/CaseFolders/SourceFileCopyServiceTests.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Intake/SourceInputProfileDetectorTests.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/WorkflowSessionTests.cs',
    'src/ProcessingTools/parcel_workflow.pyt',
    'src/Contracts/schemas/manifest.schema.json',
    'src/Contracts/schemas/preflight_summary.schema.json',
    'src/Contracts/schemas/extraction_review_data.schema.json',
    'src/Contracts/schemas/approved_review.schema.json',
    'src/Contracts/schemas/validation_summary.schema.json',
    'src/Contracts/schemas/output_summary.schema.json',
    'src/Contracts/schemas/fixture_manifest.schema.json',
    'src/Contracts/examples/manifest.example.json',
    'src/Contracts/examples/preflight_summary.example.json',
    'src/Contracts/examples/extraction_review_data.example.json',
    'src/Contracts/examples/approved_review.example.json',
    'src/Contracts/examples/validation_summary.example.json',
    'src/Contracts/examples/output_summary.example.json',
    'fixtures/case_1/fixture_manifest.json',
    'fixtures/case_2/fixture_manifest.json',
    'fixtures/case_3/fixture_manifest.json',
    'fixtures/case_4/fixture_manifest.json',
    'docs/toolchain.md',
    'tools/run_python_tests.ps1',
    'tools/package_addin.ps1'
)

$missing = @()
foreach ($path in $requiredPaths) {
    $fullPath = Join-Path $Root $path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        $missing += $path
    }
}

if ($missing.Count -gt 0) {
    throw "Missing scaffold paths:`n$($missing -join "`n")"
}

$arcgisFramework = 'C:\Program Files\ArcGIS\Pro\bin\ArcGIS.Desktop.Framework.dll'
$arcgisCore = 'C:\Program Files\ArcGIS\Pro\bin\ArcGIS.Core.dll'
if (-not (Test-Path -LiteralPath $arcgisFramework)) {
    throw "ArcGIS Pro Framework assembly not found: $arcgisFramework"
}
if (-not (Test-Path -LiteralPath $arcgisCore)) {
    throw "ArcGIS Core assembly not found: $arcgisCore"
}

$projectFile = Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj'
$projectContent = Get-Content -LiteralPath $projectFile -Raw
if ($projectContent -notmatch 'ArcGIS\.Desktop\.Framework' -or $projectContent -notmatch 'ArcGIS\.Core') {
    throw "C# scaffold must reference ArcGIS Pro SDK runtime assemblies."
}
if ($projectContent -notmatch 'Esri\.ProApp\.SDK\.Desktop\.targets') {
    throw "C# scaffold must import ArcGIS Pro SDK desktop packaging targets."
}
if ($projectContent -notmatch '<Content Include="Config\.daml"') {
    throw "Config.daml must be included as package content so .esriAddInX packaging can find it."
}
if ($projectContent -notmatch '<TargetFramework>net8\.0-windows</TargetFramework>') {
    throw "Add-in project must target net8.0-windows for the ArcGIS Pro 3.6 compatibility lane."
}

$testProjectContent = Get-Content -LiteralPath (Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/ParcelWorkflowAddIn.Tests.csproj') -Raw
if ($testProjectContent -notmatch '<TargetFramework>net8\.0-windows</TargetFramework>') {
    throw "Add-in test project must target net8.0-windows for the ArcGIS Pro 3.6 compatibility lane."
}

$workflowSettings = Get-Content -LiteralPath (Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json') -Raw | ConvertFrom-Json
if ($workflowSettings.arcgis_pro_sdk_lane -ne '3.6' -or $workflowSettings.target_framework -ne 'net8.0-windows') {
    throw "Workflow settings must declare the ArcGIS Pro 3.6 / net8.0-windows compatibility lane."
}

$arcgisPackagingTargets = 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'
if (-not (Test-Path -LiteralPath $arcgisPackagingTargets)) {
    throw "ArcGIS Pro SDK packaging targets not found: $arcgisPackagingTargets"
}

$moduleContent = Get-Content -LiteralPath (Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Module1.cs') -Raw
if ($moduleContent -notmatch ':\s*Module') {
    throw "Module1 must derive from ArcGIS.Desktop.Framework.Contracts.Module."
}

$dockPaneContent = Get-Content -LiteralPath (Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowDockpaneViewModel.cs') -Raw
if ($dockPaneContent -notmatch ':\s*DockPane') {
    throw "Dock pane view model must derive from ArcGIS.Desktop.Framework.Contracts.DockPane."
}

$damlPath = Join-Path $Root 'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Config.daml'
$daml = [xml](Get-Content -LiteralPath $damlPath -Raw)
if ($daml.ArcGIS.AddInInfo.desktopVersion -ne '3.6') {
    throw "Config.daml must declare desktopVersion 3.6 for the compatibility lane."
}
if ($daml.GetElementsByTagName('group') | Where-Object { $_.caption -eq 'Sidwell Co' }) {
    # Group is present.
}
else {
    throw "Config.daml must define a visible Add-In ribbon group captioned 'Sidwell Co'."
}
if (-not ($daml.GetElementsByTagName('button') | Where-Object { $_.id -eq 'ParcelWorkflow_ShowDockpaneButton' -and $_.className -eq 'ShowParcelWorkflowDockpaneButton' })) {
    throw "Config.daml must define a Parcel Workflow button that opens the dock pane."
}
if (-not ($daml.GetElementsByTagName('dockPane') | Where-Object { $_.id -eq 'ParcelWorkflow_Dockpane' -and $_.GetElementsByTagName('content').className -eq 'ParcelWorkflowDockpane' })) {
    throw "Config.daml must map the Parcel Workflow dock pane to its WPF content with a nested content element."
}
$imageNodes = $daml.GetElementsByTagName('Image')
foreach ($imageNode in $imageNodes) {
    $imagePath = Join-Path (Split-Path -Parent $damlPath) $imageNode.InnerText
    if (-not (Test-Path -LiteralPath $imagePath)) {
        throw "DAML image reference does not exist: $($imageNode.InnerText)"
    }
}

$exampleFiles = Get-ChildItem -LiteralPath (Join-Path $Root 'src/Contracts/examples') -Filter '*.json' -File
foreach ($file in $exampleFiles) {
    $json = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    foreach ($property in $json.PSObject.Properties.Name) {
        if ($property -cnotmatch '^[a-z][a-z0-9_]*$') {
            throw "Invalid JSON field '$property' in $($file.FullName); fields must be lowercase snake_case."
        }
    }
}

$manifestSchemaContent = Get-Content -LiteralPath (Join-Path $Root 'src/Contracts/schemas/manifest.schema.json') -Raw
foreach ($requiredSourceField in @('original_path', 'copied_path', 'file_type', 'file_size', 'copied_at', 'source_role')) {
    if ($manifestSchemaContent -notmatch $requiredSourceField) {
        throw "Manifest schema must define source file field: $requiredSourceField"
    }
}

foreach ($requiredProfileField in @('detected_profile', 'profile_code', 'display_label', 'status', 'detected_at', 'missing_roles', 'issues')) {
    if ($manifestSchemaContent -notmatch $requiredProfileField) {
        throw "Manifest schema must define detected profile field: $requiredProfileField"
    }
}

$preflightSchemaContent = Get-Content -LiteralPath (Join-Path $Root 'src/Contracts/schemas/preflight_summary.schema.json') -Raw
foreach ($requiredPreflightField in @('blockers', 'warnings', 'passed_checks', 'check_id', 'category', 'severity', 'affected_path', 'source_role', 'correction')) {
    if ($preflightSchemaContent -notmatch $requiredPreflightField) {
        throw "Preflight summary schema must define field: $requiredPreflightField"
    }
}

foreach ($requiredPreflightFile in @(
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/ManifestPreflightService.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightCheck.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummaryDocument.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Preflight/PreflightSummarySerializer.cs',
    'src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Preflight/ManifestPreflightServiceTests.cs'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $requiredPreflightFile))) {
        throw "Missing Story 2.1 preflight file: $requiredPreflightFile"
    }
}

$pythonExe = 'C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe'
if (-not (Test-Path -LiteralPath $pythonExe)) {
    throw "Configured ArcGIS Python executable not found: $pythonExe"
}

& $pythonExe -c "import encodings"
if ($LASTEXITCODE -ne 0) {
    throw "Configured ArcGIS Python executable failed to import encodings."
}

Write-Host 'Scaffold validation passed.'
