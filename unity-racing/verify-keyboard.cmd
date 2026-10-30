@echo off
setlocal
cd /d "%~dp0"
if not exist "Builds\Windows\CoastRush.exe" (
  echo Windows build missing. Run build.cmd with the project open in Unity first.
  exit /b 1
)
if not exist "tmp" mkdir "tmp"
start "Coast Rush keyboard acceptance" /wait "Builds\Windows\CoastRush.exe" -coast-keyboard-verify -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "%CD%\tmp\keyboard-player.log"
set "testResult=%ERRORLEVEL%"
if exist "doc\keyboard-verification.json" type "doc\keyboard-verification.json"
exit /b %testResult%
