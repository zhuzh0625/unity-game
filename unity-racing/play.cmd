@echo off
cd /d "%~dp0"
if not exist "Builds\Windows\CoastRush.exe" (
  echo Windows build missing. Open this project and choose Coast Rush / Build Windows player.
  exit /b 1
)
start "Coast Rush" "Builds\Windows\CoastRush.exe"
