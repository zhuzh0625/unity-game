@echo off
cd /d "%~dp0"
unity open "%CD%"
exit /b %ERRORLEVEL%
