# Bounded experiment: does this firmware do anything when the performance mode is set?
#
# Several owners report that Eco / Balanced / Performance change nothing on their machine. That has two very
# different causes and they need separating: either the firmware REFUSES our command (a return code other than 0),
# or it ACCEPTS it and ignores it (rc 0, and the fans never move). This answers which, and it also finds out which
# of the three known mode byte sets this firmware will accept at all.
#
# Run it PLUGGED IN, and only when the laptop is cool and idle (CPU < 65 C). Needs administrator rights, which
# modetest.cmd handles. It never writes a fan level, so the firmware keeps driving its own curve throughout; it
# still aborts to max fan if the machine gets hot. Total runtime about 5 minutes. Results go to modetest.txt.
$ErrorActionPreference = 'Continue'
$probe = Join-Path $PSScriptRoot 'omenprobe.exe'
$out = Join-Path $PSScriptRoot 'modetest.txt'
if (-not (Test-Path $probe)) { "omenprobe.exe is not next to this script. Run it from the tools folder."; exit 1 }
Remove-Item $out -ErrorAction SilentlyContinue

function P([string]$a) { [string]::Join(' ', @(& $probe $a.Split(' '))) }
function Say([string]$s) { $s; $s | Out-File $out -Append -Encoding utf8 }
function Hex2([string]$l, [int]$i) { $m = [regex]::Match($l, 'data=\[([0-9A-F]{2}) ([0-9A-F]{2})'); if ($m.Success) { [int][Convert]::ToInt32($m.Groups[$i].Value, 16) } else { -1 } }
function Rc([string]$l) { $m = [regex]::Match($l, 'rc=(-?\d+)'); if ($m.Success) { $m.Groups[1].Value } else { '?' } }
function Fans { $l = P 'call 2D 128 00 00 00 00'; @(((Hex2 $l 1) * 100), ((Hex2 $l 2) * 100)) }
function Chassis { $l = P 'call 23 4 01 00 00 00'; $m = [regex]::Match($l, 'data=\[([0-9A-F]{2})'); if ($m.Success) { [int][Convert]::ToInt32($m.Groups[1].Value, 16) } else { -1 } }
function CpuTemp {
    try {
        $s = (Get-Counter '\Thermal Zone Information(*)\Temperature' -ErrorAction Stop).CounterSamples
        [math]::Round((($s | Measure-Object -Property CookedValue -Maximum).Maximum) - 273.15)
    } catch { -1 }
}

$script:aborted = $false
function Row([string]$phase) {
    $f = Fans; $c = Chassis; $t = CpuTemp
    Say ("{0:HH:mm:ss}  {1,-26} fans {2,5} / {3,5} rpm   chassis {4,3} C   cpu {5,3} C" -f (Get-Date), $phase, $f[0], $f[1], $c, $t)
    if ($t -ge 85 -or $c -ge 55) {
        Say 'ABORT: too hot -> max fan'
        [void](P 'call 27 0 01'); $script:aborted = $true
    }
}

Say ("modetest  " + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
Say '----------------------------------------'
Say ('board:  ' + (Get-CimInstance Win32_BaseBoard).Product)
Say ('model:  ' + (Get-CimInstance Win32_ComputerSystem).Model)
Say ('0x28 system data:  ' + (P 'call 28 128 00 00 00 00'))
Say ('0x2C fan types:    ' + (P 'call 2C 128 00 00 00 00'))
Say ''
Say 'Byte 3 of the system data is the thermal policy version, which decides which mode bytes belong to this'
Say 'machine. Every known byte is tried below regardless, because that is the thing in question.'
Say ''

# v1 uses 30/31/50, v0 uses 00/01/02, the Victus family uses 00/01/03. Try every distinct byte any of them use.
$modes = @(
    @{ b = '00'; n = 'Balanced (v0 / Victus)' },
    @{ b = '01'; n = 'Performance (v0 / Victus)' },
    @{ b = '02'; n = 'Cool (v0)' },
    @{ b = '03'; n = 'Eco (Victus)' },
    @{ b = '30'; n = 'Balanced (v1)' },
    @{ b = '31'; n = 'Performance (v1)' },
    @{ b = '50'; n = 'Cool (v1)' }
)

Row 'baseline, no mode sent'
foreach ($m in $modes) {
    if ($script:aborted) { break }
    $line = P ('call 1A 0 FF ' + $m.b + ' 00 00')
    Say ''
    Say ('--- mode 0x' + $m.b + '  ' + $m.n + '   -> rc=' + (Rc $line) + '   (rc 0 means the firmware accepted it)')
    for ($i = 0; $i -lt 4; $i++) {
        if ($script:aborted) { break }
        Start-Sleep -Seconds 10
        Row ('0x' + $m.b + ' +' + (($i + 1) * 10) + 's')
    }
}

Say ''
if ($script:aborted) {
    Say 'ABORTED: max fan is still ON. Let the machine cool, then start Seal (or OMEN Gaming Hub) to take the fans back.'
    Say ("results in " + $out)
    exit 2
}
# Leave it on a middling mode rather than whatever the last loop set.
[void](P 'call 1A 0 FF 30 00 00')
[void](P 'call 1A 0 FF 00 00 00')
Row 'end (mode set back)'
Say ''
Say 'Done. Paste modetest.txt into the GitHub issue.'
Say 'What it shows: if every rc is 0 and the fan numbers never move, the firmware is accepting the command and'
Say 'ignoring it, and there is nothing Seal can send that would help. If some bytes return a non zero rc and'
Say 'others return 0, the ones returning 0 are the set this machine actually speaks.'
