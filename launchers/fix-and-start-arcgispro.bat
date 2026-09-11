@echo off
setlocal
chcp 65001 >nul

for %%I in ("%~dp0..") do set "ROOT_DIR=%%~fI"
set "SCRIPT_PATH=%ROOT_DIR%\scripts\ops\fix-arcgispro.ps1"

if not exist "%SCRIPT_PATH%" (
  echo [ERROR] Script not found: %SCRIPT_PATH%
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_PATH%" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  echo [ERROR] Fix flow failed with exit code: %EXIT_CODE%
)

echo.
echo Completed. Press any key to close...
pause >nul
exit /b %EXIT_CODE%
