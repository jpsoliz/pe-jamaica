param(
    [string]$PythonExe = 'C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}

$env:PYTHONPATH = Join-Path (Resolve-Path '.').Path 'src/ProcessingTools'
& $PythonExe -m unittest discover -s src/ProcessingTools/tests
if ($LASTEXITCODE -ne 0) {
    throw "Python tests failed."
}
