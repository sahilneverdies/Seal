@echo off
:: Runs fanwatch.ps1 elevated. Close Seal first, then start your game and play for 15 minutes.
setlocal
cd /d "%~dp0"
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fanwatch.ps1"
echo.
pause
