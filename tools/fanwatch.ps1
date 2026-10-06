# Why do fan writes start failing while a game is running?
#
# Seal gives up on fan levels after eight refusals in a row and only a restart clears it, which is what an
# owner sees as "the fans stuck until I closed Seal". That is the symptom. This records the cause: it sends
# the same two commands Seal sends, once every 5 s, and writes down what the firmware answered each time.
#
# Run fanwatch.cmd, then start the game and play normally. It stops on its own after 15 minutes.
# Nothing is left behind: the last thing it does is hand the fans back to the firmware.
$ErrorActionPreference = 'Continue'
$probe = Join-Path $PSScriptRoot 'omenprobe.exe'
$out = Join-Path $PSScriptRoot 'fanwatch.txt'
$minutes = 15
$level = 40                                     # 4000 rpm: audible, safe, and well clear of the 1..17 band

function P([string]$a) { [string]::Join(' ', @(& $probe $a.Split(' '))) }
# rc=N is the firmware's own answer. When the call never reaches it, omenprobe prints "EXC <reason>" and that
# reason is the interesting one: "Access denied" and "Not supported" are different problems with different fixes.
function Rc([string]$line) { $m = [regex]::Match($line, 'rc=(\d+)'); if ($m.Success) { $m.Groups[1].Value } else { '?' } }
function Exc([string]$line) { $m = [regex]::Match($line, 'EXC\s+(.+?)\s*$'); if ($m.Success) { $m.Groups[1].Value } else { '' } }
function Fans { $l = P 'call 2D 128 00 00 00 00'; $m = [regex]::Match($l, 'data=\[([0-9A-F]{2}) ([0-9A-F]{2})'); if ($m.Success) { @(([Convert]::ToInt32($m.Groups[1].Value,16))*100, ([Convert]::ToInt32($m.Groups[2].Value,16))*100) } else { @(-1,-1) } }
function CpuTemp { try { $c = (Get-Counter '\Thermal Zone Information(*)\Temperature' -ErrorAction Stop).CounterSamples | Sort-Object -Property CookedValue -Descending | Select-Object -First 1; [math]::Round($c.CookedValue - 273.15) } catch { -1 } }
function W([string]$s) { $s; $s | Out-File $out -Append -Encoding utf8 }

"fanwatch $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Out-File $out -Encoding utf8
W "board $((Get-CimInstance Win32_BaseBoard).Product)   $((Get-CimInstance Win32_ComputerSystem).Model)"
W ""
W "Seal must be CLOSED for this: two programs writing fan levels would each see the other's failures."
Get-Process Seal, Ohman -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill(); W "stopped $($_.ProcessName)" } catch { W "could not stop $($_.ProcessName): $_" } }
W ""
W "Start the game now. Recording for $minutes minutes, one line every 5 s."
W "rc=0 is success. A non-zero rc is the firmware refusing; a blank one is the call not completing at all."
W ""
W "time      trigger  write  readback      cpu   note"

$end = (Get-Date).AddMinutes($minutes)
$fails = 0
$firstFail = $null
while ((Get-Date) -lt $end) {
    $t = P 'call 10 4 00 00 00 00'                                  # the keep-alive Seal sends before every write
    $w = P ("callpad 2E 0 128 {0:X2} {1:X2}" -f $level, $level)     # the fan level write itself
    $rcT = Rc $t
    $rcW = Rc $w
    $f = Fans
    $c = CpuTemp
    $note = Exc $w
    if ($rcW -ne '0') {
        $fails++
        if (-not $firstFail) { $firstFail = Get-Date; $note = ($note + ' <-- first failure').Trim() }
        if ($fails -eq 8) { $note = ($note + ' <-- Seal would give up here').Trim() }
    } else { $fails = 0 }
    W ("{0:HH:mm:ss}  rc={1,-5} rc={2,-5} {3,5}/{4,-5} {5,3}C  {6}" -f (Get-Date), $rcT, $rcW, $f[0], $f[1], $c, $note)
    Start-Sleep -Seconds 5
}

W ""
W "Handing the fans back."
[void](P 'call 27 0 00')                                            # max fan off, in case anything set it
W "Done after $minutes minutes. The firmware takes its own curve back within about two minutes."
W ""
W "Paste fanwatch.txt into the GitHub issue, along with what you were doing when it changed."
