@echo off
cd /d "%~dp0"
if not exist "Builds\Windows\CloudSprout.exe" (
  echo Windows build not found. Open the project and run build.cmd first.
  pause
  exit /b 1
)
start "" "%~dp0Builds\Windows\CloudSprout.exe"
