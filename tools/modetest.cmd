@echo off
:: Runs modetest.ps1 elevated (see the notes at the top of modetest.ps1). Plugged in, and only when the laptop
:: is cool and idle. It never writes a fan level, so the firmware keeps driving its own curve.
setlocal
cd /d "%~dp0"
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0modetest.ps1"
echo.
pause
