# Seal keyboard info. Identifies which chip drives a per-key RGB keyboard, so support for it can be written.
# Run from Terminal (Admin) or a normal Terminal:
# What it does to the machine: nothing. It reads the device list Windows already has; no device is opened or
# sent anything. Writes seal-keyboard.txt to the Desktop.
$ErrorActionPreference = 'Continue'
$out = Join-Path ([Environment]::GetFolderPath('Desktop')) 'seal-keyboard.txt'
Set-Content $out "seal keyboard info $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Encoding utf8
function Scrub([string]$s) { $s -replace [regex]::Escape($env:USERNAME), '<user>' -replace [regex]::Escape($env:COMPUTERNAME), '<pc>' }
function W([string]$s) { $s = Scrub $s; Write-Host $s; Add-Content $out $s -Encoding utf8 }

W ("Board:  " + (Get-CimInstance Win32_BaseBoard).Product + "   Model: " + (Get-CimInstance Win32_ComputerSystem).Model + "   BIOS: " + (Get-CimInstance Win32_BIOS).SMBIOSBIOSVersion)

# Every HID collection, grouped by the USB device it belongs to. The usage page names what each one is for:
# 0001 keyboard/mouse, 000C media keys, 0059 lighting, FFxx the maker's own (where per-key colour usually lives).
W ""; W "===== HID collections by device ====="
$hid = Get-PnpDevice -PresentOnly -Class HIDClass -ErrorAction SilentlyContinue
$groups = $hid | Group-Object { if ($_.InstanceId -match 'VID_([0-9A-F]{4})&PID_([0-9A-F]{4})') { "VID_$($Matches[1])&PID_$($Matches[2])" } elseif ($_.InstanceId -match 'VHF') { 'HP virtual (VHF)' } else { 'other' } }
foreach ($g in ($groups | Sort-Object Name)) {
    if ($g.Name -eq 'other') { continue }
    $bus = ''
    try {
        $parent = (Get-PnpDeviceProperty -InstanceId $g.Group[0].InstanceId -KeyName DEVPKEY_Device_Parent -ErrorAction Stop).Data
        $bus = (Get-PnpDeviceProperty -InstanceId $parent -KeyName DEVPKEY_Device_BusReportedDeviceDesc -ErrorAction SilentlyContinue).Data
    } catch { }
    W ""; W ("  " + $g.Name + $(if ($bus) { "   ($bus)" } else { "" }))
    foreach ($d in ($g.Group | Sort-Object InstanceId)) {
        $mi = if ($d.InstanceId -match '&MI_([0-9A-F]{2})') { "MI_" + $Matches[1] } else { "     " }
        $col = if ($d.InstanceId -match '&COL([0-9A-F]{2})') { "COL" + $Matches[1] } else { "     " }
        $ids = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName DEVPKEY_Device_HardwareIds -ErrorAction SilentlyContinue).Data
        $up = ($ids | Where-Object { $_ -match '^HID_DEVICE_UP:' } | Select-Object -First 1) -replace 'HID_DEVICE_', ''
        W ("    $mi $col  $up  " + $d.FriendlyName)
    }
}

W ""; W "===== Keyboards Windows sees ====="
Get-PnpDevice -PresentOnly -Class Keyboard -ErrorAction SilentlyContinue | ForEach-Object {
    $id = if ($_.InstanceId -match 'VID_[0-9A-F]{4}&PID_[0-9A-F]{4}(&MI_[0-9A-F]{2})?') { $Matches[0] } else { ($_.InstanceId -split '\\')[0..1] -join '\' }
    W ("  $id  " + $_.FriendlyName)
}

W ""; W "===== HP lighting software ====="
Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'OMEN|Omen|HPOmen|LightStudio|HyperX' -or $_.DisplayName -match 'OMEN|Light Studio|HyperX' } | ForEach-Object { W ("  service " + $_.Name + " (" + $_.Status + ")") }
Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'OMEN|HyperX|Lighting' } | ForEach-Object { W ("  device  " + $_.FriendlyName) }
if (Get-AppxPackage -Name 'AD2F1837.OMENCommandCenter*' -ErrorAction SilentlyContinue) { W "  OMEN Gaming Hub installed" } else { W "  OMEN Gaming Hub not installed" }

Write-Host ""
Write-Host "Done. Drag seal-keyboard.txt from your Desktop into the GitHub issue." -ForegroundColor Green
Start-Process explorer.exe "/select,`"$out`""
