# Seal fan check. For boards where fan speed changes do nothing (the firmware refuses them and the driver route
# should take over). Run from Terminal (Admin):
# What it does to the machine: closes Seal, and if the optional driver is installed spins the fans up for about
# 15 seconds to see which register moves them, then hands them back and reopens Seal. Writes seal-fans.txt
# to the Desktop.
$ErrorActionPreference = 'Continue'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrator')) {
    Write-Host "Please open Terminal (Admin) and paste the line again." -ForegroundColor Yellow; return
}
$out = Join-Path ([Environment]::GetFolderPath('Desktop')) 'seal-fans.txt'
Set-Content $out "seal fan check $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Encoding utf8
function Scrub([string]$s) { $s -replace [regex]::Escape($env:USERNAME), '<user>' -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
function W([string]$s) { $s = Scrub $s; Write-Host $s; Add-Content $out $s -Encoding utf8 }
function Shared([string]$p) {
    try { $fs = [IO.File]::Open($p, 'Open', 'Read', 'ReadWrite,Delete'); $r = New-Object IO.StreamReader($fs); $t = $r.ReadToEnd(); $r.Close(); $t -split "`r?`n" } catch { @() }
}
function FindSeal {
    $p = Get-Process Seal, Ohman -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and $p.Path) { return $p.Path }
    foreach ($d in @("$env:USERPROFILE\Downloads", "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Documents", "$env:LOCALAPPDATA\Seal", "$env:LOCALAPPDATA\Ohman")) {
        $f = Get-ChildItem $d -Filter Seal.exe -Recurse -Depth 2 -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $f) { $f = Get-ChildItem $d -Filter Ohman.exe -Recurse -Depth 2 -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
        if ($f) { return $f.FullName }
    }
    $null
}

W ("Board:  " + (Get-CimInstance Win32_BaseBoard).Product + "   Model: " + (Get-CimInstance Win32_ComputerSystem).Model + "   BIOS: " + (Get-CimInstance Win32_BIOS).SMBIOSBIOSVersion)
$exe = FindSeal
if (-not $exe) { W "Seal.exe not found. Open Seal once, then run this again."; return }
$dir = Split-Path $exe
W ("Seal:  " + (Get-Item $exe).VersionInfo.FileVersion)
$pair = Join-Path $dir 'ecpair.txt'; if (Test-Path $pair) { W ("ecpair: " + (Get-Content $pair -Raw).Trim()) }

W ""; W "===== Seal's own decisions (last 80) ====="
$logFile = if (Test-Path (Join-Path $dir 'seal.log')) { Join-Path $dir 'seal.log' } else { Join-Path $dir 'ohman.log' }
Shared $logFile | Where-Object { $_ -match '---- Seal|---- Ohman|driver|fan route|EC|FAIL|giving up|THERMAL GUARD|max fan' } | Select-Object -Last 80 | ForEach-Object { W "  $_" }

W ""; W "===== OMEN Gaming Hub ====="
W ("running: " + ((Get-Process | Where-Object { $_.Name -match 'OmenCommandCenter|HP\.Omen|OMEN' } | ForEach-Object { $_.Name } | Sort-Object -Unique) -join ', '))
$ogh = Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -Filter 'AD2F1837.OMENCommandCenter*' -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'LocalCache\Local\HPOMEN' } | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($ogh) {
    Get-ChildItem $ogh -Filter 'HPOMENBG_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object {
        W "  -- $($_.Name)"
        Shared $_.FullName | Where-Object { $_ -match '(?i)fan|inputData|ThruDriver|SetFanMode|MaxFan' } | Select-Object -Last 120 | ForEach-Object { W "  $_" }
    }
} else { W "  (no OMEN Gaming Hub logs on this machine)" }

W ""; W "===== Closing Seal ====="
if (Get-Process Seal, Ohman -ErrorAction SilentlyContinue) {
    Start-Process $exe -ArgumentList '--exit' -NoNewWindow
    for ($i = 0; $i -lt 15 -and (Get-Process Seal, Ohman -ErrorAction SilentlyContinue); $i++) { Start-Sleep 1 }
}
$running = [bool](Get-Process Seal, Ohman -ErrorAction SilentlyContinue)
W ("closed: " + (-not $running))

W ""; W "===== Seal support report ====="
Start-Process $exe -ArgumentList '--support' -NoNewWindow -Wait
$rep = Join-Path $dir 'support-info.txt'
if (Test-Path $rep) { Get-Content $rep | ForEach-Object { W $_ } } else { W "  (no report written)" }

W ""; W "===== Driver fan test ====="
$zone = $null
try { $zone = (Get-CimInstance -Namespace root\wmi MSAcpi_ThermalZoneTemperature -ErrorAction Stop | ForEach-Object { $_.CurrentTemperature / 10 - 273.15 } | Measure-Object -Maximum).Maximum } catch { }
if ($running) { W "  skipped: Seal did not close" }
elseif ($zone -ge 85) { W ("  skipped: the machine is at " + [math]::Round($zone) + " C, run this again when it is idle") }
else {
    Start-Process $exe -ArgumentList '--driver', '--fantest' -NoNewWindow -Wait
    $drv = Join-Path $dir 'driver-check.txt'
    if (Test-Path $drv) { Get-Content $drv | ForEach-Object { W $_ } } else { W "  (no driver report written)" }
}

Start-Process $exe
Write-Host ""
Write-Host "Done. Seal is open again. Drag seal-fans.txt from your Desktop into the GitHub issue." -ForegroundColor Green
Start-Process explorer.exe "/select,`"$out`""
