@echo off
title Building Screen Time Tracker
cd /d "%~dp0"
echo Building Screen Time Tracker (first time takes 1-3 minutes, needs internet once)...
echo.
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "%~dp0output"
if errorlevel 1 (
  echo.
  echo ============ BUILD FAILED ============
  echo Copy ALL the red text above and send it to Claude.
  pause
  exit /b 1
)
echo.
echo DONE! ScreenTimeTracker.exe is in the "output" folder.
explorer "%~dp0output"
pause
