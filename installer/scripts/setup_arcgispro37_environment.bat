@echo off
setlocal

set "SCRIPT_ROOT=%~dp0"
set "INSTALL_ROOT=%SCRIPT_ROOT%..\.."
set "LOG_ROOT=%ProgramData%\Sidwell\ParcelWorkflow\logs"
if not exist "%LOG_ROOT%" mkdir "%LOG_ROOT%" >nul 2>nul
if not exist "%LOG_ROOT%" set "LOG_ROOT=%TEMP%\Sidwell\ParcelWorkflow\logs"
if not exist "%LOG_ROOT%" mkdir "%LOG_ROOT%" >nul 2>nul
set "BAT_LOG=%LOG_ROOT%\setup_arcgispro37_environment_bat.log"

echo [%DATE% %TIME%] Starting ArcGIS Pro environment setup.>"%BAT_LOG%"
echo Script root: "%SCRIPT_ROOT%">>"%BAT_LOG%"
echo Install root: "%INSTALL_ROOT%">>"%BAT_LOG%"
echo Log root: "%LOG_ROOT%">>"%BAT_LOG%"
echo Arguments: %*>>"%BAT_LOG%"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$scriptPath = Join-Path $env:SCRIPT_ROOT 'setup_arcgispro37_environment.ps1'; $script = [ScriptBlock]::Create([System.IO.File]::ReadAllText($scriptPath)); & $script -ScriptRoot $env:SCRIPT_ROOT -InstallRoot $env:INSTALL_ROOT -LogRoot $env:LOG_ROOT" >>"%BAT_LOG%" 2>&1
set "EXIT_CODE=%ERRORLEVEL%"
echo [%DATE% %TIME%] Completed with exit code %EXIT_CODE%.>>"%BAT_LOG%"
if not "%EXIT_CODE%"=="0" (
  echo [%DATE% %TIME%] ERROR: Python environment setup failed. Inspect this log and rerun setup after fixing Python/ArcGIS prerequisites.>>"%BAT_LOG%"
)
exit /b %EXIT_CODE%
