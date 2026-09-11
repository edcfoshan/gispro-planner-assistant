@echo off
setlocal
call "%~dp0fix-and-start-arcgispro.bat" %*
exit /b %ERRORLEVEL%
