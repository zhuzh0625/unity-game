@echo off
setlocal
set "naval_builds=%~dp0Builds"
set "naval_slot=Windows"
if exist "%naval_builds%\Windows-Next\Galleon.exe" (
  powershell.exe -NoProfile -Command ^
    "$data = 'Galleon_Data\globalgamemanagers';" ^
    "$standard = Join-Path $env:naval_builds ('Windows\' + $data);" ^
    "$next = Join-Path $env:naval_builds ('Windows-Next\' + $data);" ^
    "if ([IO.File]::GetLastWriteTimeUtc($next) -gt [IO.File]::GetLastWriteTimeUtc($standard)) { exit 2 }"
  if errorlevel 2 set "naval_slot=Windows-Next"
)
set "naval_exe=%naval_builds%\%naval_slot%\Galleon.exe"
if not exist "%naval_exe%" (
  echo Build the Windows player from the Galleon menu in Unity first.
  exit /b 1
)
start "" "%naval_exe%" %* || exit /b 1
exit /b 0
