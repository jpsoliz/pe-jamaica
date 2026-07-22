@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "INSTALL_ROOT=C:\Sidwell\ParcelWorkflow"
set "PYTHON_EXE="
set "SOURCE_PYTHON_ENV_ROOT="
set "SKIP_ADDIN_INSTALL=0"
set "SCRIPT_ROOT=%~dp0"

:parse_args
if "%~1"=="" goto args_done
if /I "%~1"=="/InstallRoot" (
  set "INSTALL_ROOT=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="-InstallRoot" (
  set "INSTALL_ROOT=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="/PythonExe" (
  set "PYTHON_EXE=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="-PythonExe" (
  set "PYTHON_EXE=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="/SourcePythonEnvRoot" (
  set "SOURCE_PYTHON_ENV_ROOT=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="-SourcePythonEnvRoot" (
  set "SOURCE_PYTHON_ENV_ROOT=%~2"
  shift
  shift
  goto parse_args
)
if /I "%~1"=="/SkipAddInInstall" (
  set "SKIP_ADDIN_INSTALL=1"
  shift
  goto parse_args
)
if /I "%~1"=="-SkipAddInInstall" (
  set "SKIP_ADDIN_INSTALL=1"
  shift
  goto parse_args
)
if /I "%~1"=="/?" goto usage
if /I "%~1"=="-?" goto usage
if /I "%~1"=="--help" goto usage

echo Unknown argument: %~1
goto usage

:args_done
set "DEPLOYMENT_ROOT=%SCRIPT_ROOT%.."
set "PACKAGE_ROOT=%SCRIPT_ROOT%..\package"
set "SOURCE_ADDIN=%PACKAGE_ROOT%\ParcelWorkflowAddIn.esriAddInX"
set "SOURCE_PROCESSING_TOOLS=%PACKAGE_ROOT%\ProcessingTools"
set "SOURCE_CONTRACTS=%PACKAGE_ROOT%\Contracts"
set "BUNDLED_PYTHON_ENV=%PACKAGE_ROOT%\python-env"
set "TARGET_PROCESSING_TOOLS=%INSTALL_ROOT%\ProcessingTools"
set "TARGET_CONTRACTS=%INSTALL_ROOT%\Contracts"
set "TARGET_ADDIN_DIR=%INSTALL_ROOT%\AddIn"
set "CONFIGURED_ADDIN=%TARGET_ADDIN_DIR%\ParcelWorkflowAddIn.configured.esriAddInX"

echo Script root: "%SCRIPT_ROOT%"
echo Deployment root: "%DEPLOYMENT_ROOT%"
echo Package root: "%PACKAGE_ROOT%"
echo Install root: "%INSTALL_ROOT%"
echo.

if not exist "%PACKAGE_ROOT%\" (
  echo ERROR: Package folder not found: "%PACKAGE_ROOT%"
  echo Copy the whole target-computer-tools folder, then run:
  echo   cd C:\Sidwell\target-computer-tools
  echo   scripts\install_target_tools.bat
  exit /b 1
)

if not exist "%SOURCE_PROCESSING_TOOLS%\" (
  echo ERROR: Source directory not found: "%SOURCE_PROCESSING_TOOLS%"
  exit /b 1
)

if not exist "%SOURCE_CONTRACTS%\" (
  echo ERROR: Source directory not found: "%SOURCE_CONTRACTS%"
  exit /b 1
)

if not exist "%INSTALL_ROOT%\" (
  mkdir "%INSTALL_ROOT%" >nul 2>nul
  if errorlevel 1 (
    echo ERROR: Could not create install root: "%INSTALL_ROOT%"
    exit /b 1
  )
)

if not exist "%TARGET_ADDIN_DIR%\" (
  mkdir "%TARGET_ADDIN_DIR%" >nul 2>nul
  if errorlevel 1 (
    echo ERROR: Could not create add-in folder: "%TARGET_ADDIN_DIR%"
    exit /b 1
  )
)

if not "%SOURCE_PYTHON_ENV_ROOT%"=="" (
  if not exist "%SOURCE_PYTHON_ENV_ROOT%\python.exe" (
    echo ERROR: Source Python environment must contain python.exe: "%SOURCE_PYTHON_ENV_ROOT%"
    exit /b 1
  )
  echo Copying Python environment to "%INSTALL_ROOT%\python-env"...
  call :copy_large_directory "%SOURCE_PYTHON_ENV_ROOT%" "%INSTALL_ROOT%\python-env"
  if errorlevel 1 exit /b 1
  set "PYTHON_EXE=%INSTALL_ROOT%\python-env\python.exe"
) else (
  if "%PYTHON_EXE%"=="" (
    if exist "%BUNDLED_PYTHON_ENV%\python.exe" (
      echo Copying bundled Python environment to "%INSTALL_ROOT%\python-env"...
      call :copy_large_directory "%BUNDLED_PYTHON_ENV%" "%INSTALL_ROOT%\python-env"
      if errorlevel 1 exit /b 1
      set "PYTHON_EXE=%INSTALL_ROOT%\python-env\python.exe"
    ) else (
      if exist "%INSTALL_ROOT%\python-env\python.exe" (
        set "PYTHON_EXE=%INSTALL_ROOT%\python-env\python.exe"
      )
    )
  )
)

if "%PYTHON_EXE%"=="" (
  echo ERROR: Python environment was not found.
  echo Copy arcgispro-survey-ai to "%INSTALL_ROOT%\python-env", or run with /PythonExe "C:\Path\To\python.exe", or /SourcePythonEnvRoot "C:\Path\To\arcgispro-survey-ai".
  exit /b 1
)

if not exist "%PYTHON_EXE%" (
  set "PYTHON_LOOKUP=%TEMP%\sidwell_python_lookup_%RANDOM%%RANDOM%.txt"
  where "%PYTHON_EXE%" > "!PYTHON_LOOKUP!" 2>nul
  if errorlevel 1 (
    del "!PYTHON_LOOKUP!" >nul 2>nul
    echo ERROR: Python executable not found: "%PYTHON_EXE%"
    exit /b 1
  )
  set /p PYTHON_EXE=<"!PYTHON_LOOKUP!"
  del "!PYTHON_LOOKUP!" >nul 2>nul
)

echo Checking ArcGIS Python / ArcPy...
call :check_arcpy "%PYTHON_EXE%"
if errorlevel 1 exit /b 1

echo Copying ProcessingTools...
call :copy_clean_directory "%SOURCE_PROCESSING_TOOLS%" "%TARGET_PROCESSING_TOOLS%"
if errorlevel 1 exit /b 1

echo Copying Contracts...
call :copy_clean_directory "%SOURCE_CONTRACTS%" "%TARGET_CONTRACTS%"
if errorlevel 1 exit /b 1

if exist "%SOURCE_ADDIN%" (
  echo Configuring add-in package...
  call :configure_addin "%SOURCE_ADDIN%" "%CONFIGURED_ADDIN%" "%PYTHON_EXE%" "%INSTALL_ROOT%"
  if errorlevel 1 exit /b 1

  if "%SKIP_ADDIN_INSTALL%"=="0" (
    echo Launching ArcGIS Pro add-in installer...
    start "" "%CONFIGURED_ADDIN%"
  )
) else (
  echo WARNING: Add-in package not found in deployment package: "%SOURCE_ADDIN%"
)

echo.
echo Installed target tools to: "%INSTALL_ROOT%"
echo Python executable: "%PYTHON_EXE%"
echo Processing tools: "%TARGET_PROCESSING_TOOLS%"
echo Contracts: "%TARGET_CONTRACTS%"
if exist "%CONFIGURED_ADDIN%" echo Configured add-in package: "%CONFIGURED_ADDIN%"
exit /b 0

:copy_clean_directory
set "COPY_SOURCE=%~1"
set "COPY_DEST=%~2"
if not exist "%COPY_SOURCE%\" (
  echo ERROR: Source directory not found: "%COPY_SOURCE%"
  exit /b 1
)
set "COPY_SOURCE_COUNT=0"
for /r "%COPY_SOURCE%" %%F in (*) do set /a COPY_SOURCE_COUNT+=1
if "!COPY_SOURCE_COUNT!"=="0" (
  echo ERROR: Source directory has no files: "%COPY_SOURCE%"
  exit /b 1
)
echo Source files: !COPY_SOURCE_COUNT! from "%COPY_SOURCE%"
if exist "%COPY_DEST%\" rmdir /S /Q "%COPY_DEST%"
if exist "%COPY_DEST%\" (
  echo ERROR: Could not clear target directory: "%COPY_DEST%"
  exit /b 1
)
mkdir "%COPY_DEST%" >nul 2>nul
if errorlevel 1 (
  echo ERROR: Could not create target directory: "%COPY_DEST%"
  exit /b 1
)
robocopy "%COPY_SOURCE%" "%COPY_DEST%" /MIR /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /XD __pycache__ .pytest_cache /XF *.pyc *.pyo
set "ROBOCOPY_RESULT=%ERRORLEVEL%"
if !ROBOCOPY_RESULT! GTR 7 (
  echo ERROR: robocopy failed with exit code !ROBOCOPY_RESULT! while copying "%COPY_SOURCE%"
  exit /b 1
)
set "COPY_DEST_COUNT=0"
for /r "%COPY_DEST%" %%F in (*) do set /a COPY_DEST_COUNT+=1
echo Copied files: !COPY_DEST_COUNT! to "%COPY_DEST%"
if "!COPY_DEST_COUNT!"=="0" (
  echo ERROR: Copy completed but target directory is empty: "%COPY_DEST%"
  exit /b 1
)
exit /b 0

:copy_large_directory
set "COPY_SOURCE=%~1"
set "COPY_DEST=%~2"
if not exist "%COPY_SOURCE%\" (
  echo ERROR: Source directory not found: "%COPY_SOURCE%"
  exit /b 1
)
set "COPY_SOURCE_COUNT=0"
for /r "%COPY_SOURCE%" %%F in (*) do set /a COPY_SOURCE_COUNT+=1
if "!COPY_SOURCE_COUNT!"=="0" (
  echo ERROR: Source directory has no files: "%COPY_SOURCE%"
  exit /b 1
)
echo Source files: !COPY_SOURCE_COUNT! from "%COPY_SOURCE%"
if not exist "%COPY_DEST%\" mkdir "%COPY_DEST%" >nul 2>nul
if errorlevel 1 (
  echo ERROR: Could not create target directory: "%COPY_DEST%"
  exit /b 1
)
robocopy "%COPY_SOURCE%" "%COPY_DEST%" /MIR /MT:16 /R:1 /W:1 /NFL /NDL /NJH /NJS /XD __pycache__ .pytest_cache /XF *.pyc *.pyo
set "ROBOCOPY_RESULT=%ERRORLEVEL%"
if !ROBOCOPY_RESULT! GTR 7 (
  echo ERROR: robocopy failed with exit code !ROBOCOPY_RESULT! while copying "%COPY_SOURCE%"
  exit /b 1
)
set "COPY_DEST_COUNT=0"
for /r "%COPY_DEST%" %%F in (*) do set /a COPY_DEST_COUNT+=1
echo Copied files: !COPY_DEST_COUNT! to "%COPY_DEST%"
if "!COPY_DEST_COUNT!"=="0" (
  echo ERROR: Copy completed but target directory is empty: "%COPY_DEST%"
  exit /b 1
)
exit /b 0

:check_arcpy
set "CHECK_PYTHON_EXE=%~1"
set "CHECK_SCRIPT=%TEMP%\sidwell_check_arcpy_%RANDOM%%RANDOM%.py"
> "%CHECK_SCRIPT%" echo import sys
>> "%CHECK_SCRIPT%" echo print("python_executable:" + sys.executable)
>> "%CHECK_SCRIPT%" echo print("python_version:" + sys.version.replace("\n", " "))
>> "%CHECK_SCRIPT%" echo try:
>> "%CHECK_SCRIPT%" echo ^    import arcpy
>> "%CHECK_SCRIPT%" echo ^    print("arcpy_import:ok")
>> "%CHECK_SCRIPT%" echo ^    print("arcpy_install_info:" + str(arcpy.GetInstallInfo()))
>> "%CHECK_SCRIPT%" echo except Exception as exc:
>> "%CHECK_SCRIPT%" echo ^    message = str(exc).replace("\n", " ")
>> "%CHECK_SCRIPT%" echo ^    print("arcpy_import:error:" + type(exc).__name__ + ": " + message)
>> "%CHECK_SCRIPT%" echo ^    lowered = message.lower()
>> "%CHECK_SCRIPT%" echo ^    if "license has not been initialized" in lowered:
>> "%CHECK_SCRIPT%" echo ^        raise SystemExit(2)
>> "%CHECK_SCRIPT%" echo ^    raise SystemExit(1)
"%CHECK_PYTHON_EXE%" "%CHECK_SCRIPT%"
set "CHECK_RESULT=%ERRORLEVEL%"
del "%CHECK_SCRIPT%" >nul 2>nul
if "%CHECK_RESULT%"=="2" (
  echo WARNING: ArcPy is present, but the ArcGIS product license was not initialized in this installer session.
  echo The add-in may still work when run inside a licensed ArcGIS Pro session.
  exit /b 0
)
if not "%CHECK_RESULT%"=="0" (
  echo.
  echo ERROR: The configured Python cannot import ArcPy.
  echo Python executable: "%CHECK_PYTHON_EXE%"
  echo This usually means the python-env was copied from a different ArcGIS Pro/Python version.
  echo Use the target computer's ArcGIS Pro Python, or recreate python-env from that same ArcGIS Pro install.
  echo Example:
  echo   scripts\install_target_tools.bat /PythonExe "C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe"
  exit /b 1
)
exit /b 0

:configure_addin
set "CFG_SOURCE_ADDIN=%~1"
set "CFG_DEST_ADDIN=%~2"
set "CFG_PYTHON_EXE=%~3"
set "CFG_INSTALL_ROOT=%~4"
set "CFG_SCRIPT=%TEMP%\sidwell_configure_addin_%RANDOM%%RANDOM%.py"

> "%CFG_SCRIPT%" echo import json
>> "%CFG_SCRIPT%" echo import os
>> "%CFG_SCRIPT%" echo import shutil
>> "%CFG_SCRIPT%" echo import sys
>> "%CFG_SCRIPT%" echo import tempfile
>> "%CFG_SCRIPT%" echo import zipfile
>> "%CFG_SCRIPT%" echo source_addin, dest_addin, python_exe, install_root = sys.argv[1:5]
>> "%CFG_SCRIPT%" echo tools_root = os.path.join(install_root, "ProcessingTools")
>> "%CFG_SCRIPT%" echo temp_root = tempfile.mkdtemp(prefix="sidwell-addin-package-")
>> "%CFG_SCRIPT%" echo try:
>> "%CFG_SCRIPT%" echo ^    expanded = os.path.join(temp_root, "expanded")
>> "%CFG_SCRIPT%" echo ^    os.makedirs(expanded, exist_ok=True)
>> "%CFG_SCRIPT%" echo ^    with zipfile.ZipFile(source_addin, "r") as archive:
>> "%CFG_SCRIPT%" echo ^        archive.extractall(expanded)
>> "%CFG_SCRIPT%" echo ^    settings_path = os.path.join(expanded, "Install", "Settings", "WorkflowSettings.json")
>> "%CFG_SCRIPT%" echo ^    if not os.path.exists(settings_path):
>> "%CFG_SCRIPT%" echo ^        raise SystemExit("WorkflowSettings.json was not found inside add-in package.")
>> "%CFG_SCRIPT%" echo ^    with open(settings_path, "r", encoding="utf-8-sig") as handle:
>> "%CFG_SCRIPT%" echo ^        settings = json.load(handle)
>> "%CFG_SCRIPT%" echo ^    settings["arcgis_python_executable"] = python_exe
>> "%CFG_SCRIPT%" echo ^    settings["output_adapter_script_path"] = os.path.join(tools_root, "adapters", "output_adapter.py")
>> "%CFG_SCRIPT%" echo ^    settings["validation_adapter_script_path"] = os.path.join(tools_root, "adapters", "validation_adapter.py")
>> "%CFG_SCRIPT%" echo ^    settings["validation_rules_path"] = os.path.join(tools_root, "rules", "rules.yaml")
>> "%CFG_SCRIPT%" echo ^    admin = settings.get("enterprise_working_admin")
>> "%CFG_SCRIPT%" echo ^    if isinstance(admin, dict):
>> "%CFG_SCRIPT%" echo ^        admin["provisioning_script_path"] = os.path.join(tools_root, "admin", "provision_enterprise_working_layers.py")
>> "%CFG_SCRIPT%" echo ^    with open(settings_path, "w", encoding="utf-8") as handle:
>> "%CFG_SCRIPT%" echo ^        json.dump(settings, handle, indent=2)
>> "%CFG_SCRIPT%" echo ^    os.makedirs(os.path.dirname(dest_addin), exist_ok=True)
>> "%CFG_SCRIPT%" echo ^    if os.path.exists(dest_addin):
>> "%CFG_SCRIPT%" echo ^        os.remove(dest_addin)
>> "%CFG_SCRIPT%" echo ^    with zipfile.ZipFile(dest_addin, "w", compression=zipfile.ZIP_DEFLATED) as archive:
>> "%CFG_SCRIPT%" echo ^        for root, dirs, files in os.walk(expanded):
>> "%CFG_SCRIPT%" echo ^            dirs.sort()
>> "%CFG_SCRIPT%" echo ^            for name in sorted(files):
>> "%CFG_SCRIPT%" echo ^                path = os.path.join(root, name)
>> "%CFG_SCRIPT%" echo ^                archive.write(path, os.path.relpath(path, expanded))
>> "%CFG_SCRIPT%" echo finally:
>> "%CFG_SCRIPT%" echo ^    shutil.rmtree(temp_root, ignore_errors=True)

"%CFG_PYTHON_EXE%" "%CFG_SCRIPT%" "%CFG_SOURCE_ADDIN%" "%CFG_DEST_ADDIN%" "%CFG_PYTHON_EXE%" "%CFG_INSTALL_ROOT%"
set "CFG_RESULT=%ERRORLEVEL%"
del "%CFG_SCRIPT%" >nul 2>nul
if not "%CFG_RESULT%"=="0" (
  echo ERROR: Could not configure add-in package.
  exit /b %CFG_RESULT%
)
exit /b 0

:usage
echo Usage:
echo   install_target_tools.bat [/InstallRoot "C:\Sidwell\ParcelWorkflow"] [/PythonExe "C:\Path\python.exe"] [/SourcePythonEnvRoot "C:\Path\arcgispro-survey-ai"] [/SkipAddInInstall]
exit /b 1
