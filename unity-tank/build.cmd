@echo off
cd /d "%~dp0"
unity command script eval --project-path "%CD%" --caller plugin --skill unity-cli --timeout 600 --format json -- --code "ProjectBuilder.BuildWindows(); return 1;" --timeout 600
exit /b %ERRORLEVEL%
