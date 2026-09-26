@echo off
cd /d "%~dp0"
if not exist "%~dp0TextSnip.exe" (
  echo TextSnip.exe is missing. Extract the entire ZIP before running Start.cmd.
  pause
  exit /b 1
)
start "" "%~dp0TextSnip.exe" --settings