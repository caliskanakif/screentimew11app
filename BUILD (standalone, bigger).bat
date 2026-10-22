@echo off
title Building Screen Time Tracker (standalone)
cd /d "%~dp0"
echo Building standalone version (runs on any Windows 10/11 PC, file is much bigger)...
echo.
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "%~dp0output-standalone"
if errorlevel 1 (
  echo.
  echo ============ BUILD FAILED ============
  echo Copy ALL the red text above and send it to Claude.
  pause
  exit /b 1
)
echo.
echo DONE! ScreenTimeTracker.exe is in the "output-standalone" folder.
explorer "%~dp0output-standalone"
pause
