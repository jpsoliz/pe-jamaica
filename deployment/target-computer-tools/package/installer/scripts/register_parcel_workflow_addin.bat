@echo off
setlocal

set "SCRIPT_ROOT=%~dp0"
set "LOG_ROOT=%ProgramData%\Sidwell\ParcelWorkflow\logs"
if not exist "%LOG_ROOT%" mkdir "%LOG_ROOT%" >nul 2>nul
if not exist "%LOG_ROOT%" set "LOG_ROOT=%TEMP%\Sidwell\ParcelWorkflow\logs"
if not exist "%LOG_ROOT%" mkdir "%LOG_ROOT%" >nul 2>nul
set "BAT_LOG=%LOG_ROOT%\register_parcel_workflow_addin_bat.log"

echo [%DATE% %TIME%] Starting Parcel Workflow add-in configuration.>"%BAT_LOG%"
echo Script root: "%SCRIPT_ROOT%">>"%BAT_LOG%"
echo Arguments: %*>>"%BAT_LOG%"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%register_parcel_workflow_addin.ps1" %* >>"%BAT_LOG%" 2>&1
set "EXIT_CODE=%ERRORLEVEL%"
echo [%DATE% %TIME%] Completed with exit code %EXIT_CODE%.>>"%BAT_LOG%"
if not "%EXIT_CODE%"=="0" (
  echo [%DATE% %TIME%] WARNING: Add-in configuration failed. Inspect this log and rerun this script manually after fixing prerequisites.>>"%BAT_LOG%"
)
exit /b 0
