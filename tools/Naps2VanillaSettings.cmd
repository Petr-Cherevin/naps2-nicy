@echo off
setlocal
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Naps2VanillaSettings.ps1"
if errorlevel 1 (
  echo PowerShell reported an error.
  pause
)
