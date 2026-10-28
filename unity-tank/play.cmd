@echo off
cd /d "%~dp0"
if not exist "Builds\Release-1.0.3\IronBastion.exe" (
  echo Windows build not found. Open the project in Unity and choose Iron Bastion / Build Windows player.
  pause
  exit /b 1
)
start "Iron Bastion" "Builds\Release-1.0.3\IronBastion.exe"
