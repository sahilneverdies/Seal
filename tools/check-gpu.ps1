# Seal GPU check. For "the NVIDIA GPU never goes to sleep". Measures how often it is awake with Seal running
# and with Seal closed. Run from Terminal (Admin), unplugged if you can, and leave the laptop alone while it runs:
# What it does to the machine: nothing. It watches for 2 minutes, asks you to quit Seal, watches 2 more minutes.
# Writes seal-gpu.txt to the Desktop.
$ErrorActionPreference = 'Continue'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrator')) {
    Write-Host "Please open Terminal (Admin) and paste the line again." -ForegroundColor Yellow; return
}
$out = Join-Path ([Environment]::GetFolderPath('Desktop')) 'seal-gpu.txt'
Set-Content $out "seal gpu check $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Encoding utf8
function Scrub([string]$s) { $s -replace [regex]::Escape($env:USERNAME), '<user>' -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
function W([string]$s) { $s = Scrub $s; Write-Host $s; Add-Content $out $s -Encoding utf8 }

$gpu = Get-PnpDevice -PresentOnly -Class Display | Where-Object { $_.InstanceId -like 'PCI\VEN_10DE*' } | Select-Object -First 1
W ("Board:  " + (Get-CimInstance Win32_BaseBoard).Product + "   Model: " + (Get-CimInstance Win32_ComputerSystem).Model)
if (-not $gpu) { W "No NVIDIA GPU found."; return }
W ("GPU:    " + $gpu.FriendlyName + "   driver " + (Get-PnpDeviceProperty -InstanceId $gpu.InstanceId -KeyName DEVPKEY_Device_DriverVersion).Data)
$power = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue | Select-Object -First 1
W ("Power:  " + $(if ($power -and $power.BatteryStatus -eq 1) { "battery" } else { "plugged in" }))
$p = Get-Process Seal, Ohman -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p -and $p.Path) {
    $st = @((Join-Path (Split-Path $p.Path) 'seal.state'), (Join-Path (Split-Path $p.Path) 'ohman.state')) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($st) { Get-Content $st | Where-Object { $_ -match '^(PollMs|RefreshHz|LowHzOnBattery|ModeIndex)=' } | ForEach-Object { W "Seal:  $_" } }
}

function Awake { $d = (Get-PnpDeviceProperty -InstanceId $gpu.InstanceId -KeyName DEVPKEY_Device_PowerData).Data; [BitConverter]::ToInt32($d, 4) -eq 1 }
function Watch([string]$label) {
    Register-CimIndicationEvent -Query "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='nvidia-smi.exe'" -SourceIdentifier "sealsmi" -ErrorAction SilentlyContinue
    $awake = 0; $drain = @()
    for ($i = 0; $i -lt 120; $i++) {
        if (Awake) { $awake++ }
        if ($i % 10 -eq 0) { try { $r = (Get-CimInstance -Namespace root\wmi BatteryStatus -ErrorAction Stop | Select-Object -First 1).DischargeRate; if ($r -gt 0) { $drain += $r } } catch { } }
        Write-Progress -Activity "Watching the GPU ($label)" -SecondsRemaining (120 - $i) -PercentComplete ($i / 1.2)
        Start-Sleep 1
    }
    Write-Progress -Activity "Watching the GPU" -Completed
    $smi = @(Get-Event -SourceIdentifier "sealsmi" -ErrorAction SilentlyContinue).Count
    Get-Event -SourceIdentifier "sealsmi" -ErrorAction SilentlyContinue | Remove-Event
    Unregister-Event -SourceIdentifier "sealsmi" -ErrorAction SilentlyContinue
    W ""; W "===== $label ====="
    W ("  GPU awake " + [math]::Round($awake / 1.2) + "% of 2 minutes")
    W ("  nvidia-smi started " + $smi + " times")
    if ($drain.Count) { W ("  battery drain about " + [math]::Round((($drain | Measure-Object -Average).Average) / 1000, 1) + " W") }
    W "  programs holding GPU memory:"
    try {
        (Get-Counter '\GPU Process Memory(*)\Dedicated Usage' -ErrorAction Stop).CounterSamples | Where-Object { $_.CookedValue -gt 1MB } | ForEach-Object {
            $procId = if ($_.InstanceName -match 'pid_(\d+)') { [int]$Matches[1] } else { 0 }
            $name = (Get-Process -Id $procId -ErrorAction SilentlyContinue).ProcessName
            $luid = if ($_.InstanceName -match 'luid_(0x[0-9a-f]+_0x[0-9a-f]+)') { $Matches[1] } else { '' }
            [pscustomobject]@{ n = $name; mb = [math]::Round($_.CookedValue / 1MB); luid = $luid }
        } | Sort-Object mb -Descending | Select-Object -First 10 | ForEach-Object { W ("    " + $_.n + "  " + $_.mb + " MB  (adapter " + $_.luid + ")") }
    } catch { W "    (not available)" }
}

Write-Host "Leave the laptop alone for the next 2 minutes." -ForegroundColor Cyan
Watch "with Seal running"
[console]::beep(880, 300)
Write-Host ""
Write-Host "Now quit Seal: right click its tray icon, Exit. This continues by itself once it has closed." -ForegroundColor Cyan
for ($i = 0; $i -lt 180 -and (Get-Process Seal, Ohman -ErrorAction SilentlyContinue); $i++) { Start-Sleep 1 }
if (Get-Process Seal, Ohman -ErrorAction SilentlyContinue) { W ""; W "Seal was not closed, so there is nothing to compare." }
else { Watch "with Seal closed" }

Write-Host ""
Write-Host "Done. You can open Seal again. Drag seal-gpu.txt from your Desktop into the GitHub issue." -ForegroundColor Green
Start-Process explorer.exe "/select,`"$out`""
