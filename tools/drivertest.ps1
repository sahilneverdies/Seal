# Why is the driver not working?
#
# Ohman's optional driver is PawnIO. When Settings says "Driver not detected", the answer is in one of a few
# places: the driver did not install, the service is not running, Windows will not load it, the CPU module
# refuses this CPU, or another program holds the embedded controller. This asks all of them in one go.
#
# Most of it is Ohman.exe --driver, the same check the Troubleshoot link in Settings runs; the rest is what
# only PowerShell can ask (Defender's own detections, Secure Boot). Read-only, except for the fan test at the
# end, which it asks you about first and which you can say no to.
# Paste drivertest.txt into a GitHub issue or the Discord: the user name and machine name are already taken out.
$ErrorActionPreference = 'Continue'
$out = Join-Path $PSScriptRoot 'drivertest.txt'
$lines = New-Object System.Collections.Generic.List[string]
function L($s) { $lines.Add($s); $s }
function Scrub($s) {
    if (-not $s) { return "" }
    $s = [string]$s
    if ($env:USERPROFILE) { $s = $s -replace [regex]::Escape($env:USERPROFILE), '<userprofile>' }
    if ($env:USERNAME)    { $s = $s -replace [regex]::Escape($env:USERNAME), '<user>' }
    if ($env:COMPUTERNAME){ $s = $s -replace [regex]::Escape($env:COMPUTERNAME), '<host>' }
    $s -replace '(?i)[A-Z]:\\Users\\[^\\/:*?<>|
]+', '<userprofile>'
}

L "Seal driver test  $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
L "----------------------------------------"

# Seal.exe sits one folder up in a release zip, or beside this file if somebody copied it here.
$exe = @((Join-Path $PSScriptRoot '..\Seal.exe'), (Join-Path $PSScriptRoot 'Seal.exe')) | Where-Object { Test-Path $_ } | Select-Object -First 1
# Both fan registers exist on every HP board anyone has looked at, both accept a write, and only one of them is
# wired to a fan. Reading cannot tell them apart, so the only way to know is to write one and watch the
# tachometer. It asks for more air than the machine is already moving, never less, and hands the fans back
# afterwards. Fifteen seconds. Answering no just leaves that section out.
$args2 = @('--driver')
Write-Host ""
Write-Host "The fan test raises your fans for about fifteen seconds to find which register controls them."
Write-Host "It never slows them down, and it hands them back afterwards. On a new board this is the single"
Write-Host "most useful thing you can send us. Skip it if the machine is busy."
$yn = Read-Host "Run the fan test as well? [y/N]"
if ($yn -match '^(y|yes)$') { $args2 += '--fantest'; L "Fan test: yes" } else { L "Fan test: skipped by the user" }

if ($exe) {
    L "Seal.exe $($args2 -join ' '):"
    L ""
    try {
        $tmp = Join-Path $env:TEMP ('seal-driver-' + [guid]::NewGuid().ToString('N') + '.txt')
        $p = Start-Process -FilePath $exe -ArgumentList $args2 -Wait -PassThru -NoNewWindow -RedirectStandardOutput $tmp
        Get-Content $tmp | ForEach-Object { L ("  " + (Scrub $_)) } | Out-Null
        Remove-Item $tmp -ErrorAction SilentlyContinue
    } catch { L ("  could not run it: " + (Scrub $_)) }
} else {
    L "Seal.exe not found next to the tools folder; the driver's own view is missing from this file."
    L "Run it yourself from an administrator prompt:  Seal.exe --driver"
}
L ""

L "Windows"
try { L ("  Secure Boot:        " + $(if (Confirm-SecureBootUEFI -ErrorAction Stop) { 'on' } else { 'off' })) } catch { L "  Secure Boot:        not UEFI or cannot ask" }
$hvci = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' -Name Enabled -ErrorAction SilentlyContinue
L ("  Memory Integrity:   " + $(if ($null -eq $hvci) { 'not configured' } elseif ($hvci.Enabled) { 'on' } else { 'off' }))
$svc = Get-Service -Name PawnIO -ErrorAction SilentlyContinue
L ("  PawnIO service:     " + $(if ($svc) { $svc.Status.ToString() + " (start " + $svc.StartType + ")" } else { 'not registered' }))
$reg = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO' -ErrorAction SilentlyContinue
L ("  PawnIO registry:    " + $(if ($reg) { $reg.DisplayVersion + "  " + (Scrub $reg.InstallLocation) } else { 'no uninstall key' }))
$sys = Join-Path $env:WINDIR 'System32\drivers\PawnIO.sys'
if (Test-Path $sys) {
    $sig = Get-AuthenticodeSignature -FilePath $sys
    L ("  PawnIO.sys:         " + (Get-Item $sys).VersionInfo.FileVersion + "  signature " + $sig.Status + "  " + $(if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { '' }))
} else { L "  PawnIO.sys:         not in System32\drivers" }
L ""

L "Defender detections that name PawnIO, Seal or Ohman (last 30 days)"
try {
    $det = Get-MpThreatDetection -ErrorAction Stop | Where-Object { $_.InitialDetectionTime -gt (Get-Date).AddDays(-30) }
    $hits = @($det | Where-Object { ($_.Resources -join ' ') -match 'PawnIO|Seal|Ohman' })
    if ($hits.Count -eq 0) { L "  none" }
    foreach ($h in $hits) {
        $t = Get-MpThreat -ThreatID $h.ThreatID -ErrorAction SilentlyContinue
        L ("  " + $h.InitialDetectionTime.ToString('yyyy-MM-dd HH:mm') + "  " + $(if ($t) { $t.ThreatName } else { $h.ThreatID }) + "  " + (Scrub ($h.Resources -join ' ')))
    }
} catch { L ("  cannot ask Defender (" + (Scrub $_) + ")") }
L ""

L "Other programs that talk to the EC or the MSRs right now"
$names = 'OmenMon','FanControl','LibreHardwareMonitor','HWiNFO64','HWiNFO32','OmenCore','OmenCommandCenterBackground','ThrottleStop','XTU','Seal','Ohman'
$seen = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.ProcessName } | Select-Object -ExpandProperty ProcessName -Unique)
L ("  " + $(if ($seen.Count) { $seen -join ', ' } else { 'none seen' }))
L ""
L "Two programs on the EC at once is fine (they share a lock); two programs writing fan levels is not."

$lines | Out-File $out -Encoding utf8
""
"Saved to $out - paste it into the issue."
