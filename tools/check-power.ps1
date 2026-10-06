# Seal power check. Reads what OMEN Gaming Hub sends for CPU and GPU power in each performance mode, from HP's
# own log files. Run from Terminal (Admin) or a normal Terminal:
# What it does to the machine: nothing. It only reads HP's logs and one HP registry key. Writes seal-power.txt
# to the Desktop.
$ErrorActionPreference = 'Continue'
$out = Join-Path ([Environment]::GetFolderPath('Desktop')) 'seal-power.txt'
function Scrub([string]$s) { $s -replace [regex]::Escape($env:USERNAME), '<user>' -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
function W([string]$s) { $s = Scrub $s; Write-Host $s; Add-Content $out $s -Encoding utf8 }
function Shared([string]$p) {
    try { $fs = [IO.File]::Open($p, 'Open', 'Read', 'ReadWrite,Delete'); $r = New-Object IO.StreamReader($fs); $t = $r.ReadToEnd(); $r.Close(); $t -split "`r?`n" } catch { @() }
}

Write-Host ""
Write-Host "Before this can read anything, OMEN Gaming Hub has to have used each mode once:" -ForegroundColor Cyan
Write-Host "  1. Quit Seal from the tray (right click the icon, Exit)."
Write-Host "  2. Open OMEN Gaming Hub, go to Performance Control."
Write-Host "  3. Click each mode it offers (Eco, Balanced, Performance, Unleashed if you have it), 30 seconds each."
Write-Host "     If there is a power slider, drag it all the way right once."
Write-Host "  4. Close OMEN Gaming Hub."
Read-Host "Press Enter when done" | Out-Null

Set-Content $out "seal power check $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Encoding utf8
W ("Board:  " + (Get-CimInstance Win32_BaseBoard).Product + "   Model: " + (Get-CimInstance Win32_ComputerSystem).Model + "   BIOS: " + (Get-CimInstance Win32_BIOS).SMBIOSBIOSVersion)
W ("CPU:    " + (Get-CimInstance Win32_Processor | Select-Object -First 1).Name)
try {
    $k = Get-ItemProperty 'HKCU:\Software\HP\OMEN Ally\Settings' -ErrorAction Stop
    if ($k.SystemDesignData) { W ("SystemDesignData: " + (($k.SystemDesignData | Select-Object -First 12 | ForEach-Object { $_.ToString('X2') }) -join ' ')) }
    foreach ($n in 'LoadedJsonSku', 'LastLoadedJsonSku') { if ($k.$n) { W "${n}: $($k.$n)" } }
} catch { W "(no OMEN Gaming Hub settings key)" }

$ogh = Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -Filter 'AD2F1837.OMENCommandCenter*' -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'LocalCache\Local\HPOMEN' } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $ogh) { W "No OMEN Gaming Hub logs on this machine."; Start-Process explorer.exe "/select,`"$out`""; return }
$logs = Get-ChildItem $ogh -Filter 'HPOMEN*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 6
# HPOMEN_ (the window) and HPOMENBG_ (the service) both log payloads; merge them by their leading timestamp, or a
# payload from one file is counted under the mode the other file last named.
$lines = foreach ($f in $logs) { Shared $f.FullName | Where-Object { $_ -match '^\d{4}-\d\d-\d\d \d\d:' } }
$lines = $lines | Sort-Object { $_.Substring(0, [Math]::Min(27, $_.Length)) }

W ""; W "===== What OGH says this laptop supports ====="
$keys = 'Algorithm goes with', 'IsPowerLimit1Support', 'IsPowerLimit2Support', 'IsTppSupport', 'IsWmiSupportTpp', 'TppMinValue', 'TppMaxValue',
        'PowerContext', 'PerformanceModeTppOffset', 'UnleashedModeTppOffset', 'GetUnleashedMode', 'IsExtremeModeSupport', 'IsUnleashedModeSupport', 'IsCtgpModeSupport'
$seen = @{}
foreach ($key in $keys) {
    $hit = $lines | Where-Object { $_ -like "*$key*" } | Select-Object -Last 1
    if ($hit -and -not $seen[$hit]) { $seen[$hit] = 1; W ("  " + $hit.Trim()) }
}

W ""; W "===== Mode changes and what followed each (last 60) ====="
$mode = '?'
$byMode = @{}
$timeline = New-Object System.Collections.Generic.List[string]
foreach ($l in $lines) {
    if ($l -match 'SetFanModeAsync.*mode = (\w+)') { $mode = $Matches[1]; $timeline.Add("mode $mode"); continue }
    if ($l -match 'PL1DefaultValue=(\d+)') { $timeline.Add("  PL1 default " + $Matches[1]); continue }
    if ($l -match 'inputData=\s*([\d]+(,\s*\d+){3})\s*,?\s*$') {
        $v = $Matches[1] -replace '\s', ''
        if ($v -eq '0,0,0,0') { continue }
        if (-not $byMode[$mode]) { $byMode[$mode] = @{} }
        $byMode[$mode][$v] = 1 + [int]$byMode[$mode][$v]
        $timeline.Add("  sent $v")
    }
}
$prev = $null; $shown = New-Object System.Collections.Generic.List[string]
foreach ($t in $timeline) { if ($t -ne $prev) { $shown.Add($t) }; $prev = $t }
$shown | Select-Object -Last 60 | ForEach-Object { W "  $_" }

W ""; W "===== Payloads per mode (count) ====="
foreach ($m in $byMode.Keys) {
    W "  mode $m"
    $byMode[$m].GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 12 | ForEach-Object { W ("    " + $_.Key.PadRight(20) + " x" + $_.Value) }
}

W ""; W "===== Seal's settings ====="
$p = Get-Process Seal, Ohman -ErrorAction SilentlyContinue | Select-Object -First 1
$state = $null
if ($p -and $p.Path) { $state = @((Join-Path (Split-Path $p.Path) 'seal.state'), (Join-Path (Split-Path $p.Path) 'ohman.state')) | Where-Object { Test-Path $_ } | Select-Object -First 1 }
if (-not $state -or -not (Test-Path $state)) { $state = Get-ChildItem "$env:USERPROFILE\Downloads", "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Documents" -Filter seal.state -Recurse -Depth 2 -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
if (-not $state -or -not (Test-Path $state)) { $state = Get-ChildItem "$env:USERPROFILE\Downloads", "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Documents" -Filter ohman.state -Recurse -Depth 2 -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
if ($state) { Get-Content $state | Where-Object { $_ -match 'Mode|Tdp|Gpu' } | ForEach-Object { W "  $_" } } else { W "  (not found)" }

Write-Host ""
Write-Host "Done. Drag seal-power.txt from your Desktop into the GitHub issue, then open Seal again." -ForegroundColor Green
Start-Process explorer.exe "/select,`"$out`""
