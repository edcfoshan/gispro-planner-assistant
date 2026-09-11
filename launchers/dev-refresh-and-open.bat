@echo off
setlocal
chcp 65001 >nul
set "DOTNET_CLI_UI_LANGUAGE=zh-Hans"
set "DOTNET_CLI_FORCE_UTF8_ENCODING=1"
set "MSBUILDUTF8OUTPUT=1"

for %%I in ("%~dp0..") do set "ROOT_DIR=%%~fI"
set "SCRIPT_PATH=%ROOT_DIR%\scripts\dev\refresh-addin-and-open.ps1"

if not exist "%SCRIPT_PATH%" (
  echo [ERROR] Script not found: %SCRIPT_PATH%
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_PATH%" -BuildLatest -ForceCloseArcGISPro %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  powershell -NoProfile -Command "$m='['+[char]0x9519+[char]0x8BEF+'] '+[char]0x6267+[char]0x884C+[char]0x5931+[char]0x8D25+[char]0xFF0C+[char]0x9000+[char]0x51FA+[char]0x7801+': %EXIT_CODE%'; Write-Host $m"
)

echo.
powershell -NoProfile -Command "$m=[char]0x6267+[char]0x884C+[char]0x7ED3+[char]0x675F+[char]0xFF0C+[char]0x6309+[char]0x4EFB+[char]0x610F+[char]0x952E+[char]0x5173+[char]0x95ED+[char]0x7A97+[char]0x53E3+[char]0x002E+[char]0x002E+[char]0x002E; Write-Host $m"
pause >nul
exit /b %EXIT_CODE%
