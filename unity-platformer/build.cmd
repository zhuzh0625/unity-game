@echo off
cd /d "%~dp0"
unity command build run --project-path "%~dp0." --target StandaloneWindows64 --outputPath Builds/Windows/CloudSprout.exe --confirm true
echo Poll the build with: unity command build status --project-path "%~dp0."
