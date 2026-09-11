@echo off
setlocal
call "%~dp0dev-refresh-and-open.bat" %*
exit /b %ERRORLEVEL%
