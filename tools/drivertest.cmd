@echo off
:: Why is the driver not working? Asks PawnIO, the CPU registers and the EC what they say on this machine and
:: writes drivertest.txt next to this file. Read-only: nothing is installed, written or changed. Asks for admin once.
setlocal
cd /d "%~dp0"
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0drivertest.ps1"
echo.
pause
