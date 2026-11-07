$ErrorActionPreference = 'Continue'

# All messages are ASCII on purpose: PowerShell 5.1 reads .ps1 as ANSI,
# any non-ASCII literal breaks script parsing on a GBK system.

$HWID = 'HID\VID_0094&UP:0020_U:00E1'
$INF  = Join-Path $env:windir 'INF\input.inf'
$LOGF = Join-Path $env:TEMP 'buds-hidfix.log'

function Say($s) {
  $line = (Get-Date -Format 'HH:mm:ss') + '  ' + $s
  Write-Output $line
  Add-Content -Path $LOGF -Value $line -ErrorAction SilentlyContinue
}

function Get-BudsChild {
  foreach ($d in (Get-PnpDevice)) {
    if ($d.InstanceId -like 'HID\*0094*') { return $d }
  }
  return $null
}

function Test-Broken($d) {
  if ($null -eq $d) { return $false }
  $svc = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_Service').Data
  $pc  = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_ProblemCode').Data
  if ($d.Status -ne 'OK') { return $true }
  if ($null -ne $pc -and $pc -ne 0) { return $true }
  if ($svc -eq 'SensorsHIDClassDriver') { return $true }
  return $false
}

# exit codes: 0 ok / 3 no node / 10 broken (needs admin run) / 2 not admin / 5 fix failed
$mode = '--check'
if ($args -contains '--run') { $mode = '--run' }

$child = Get-BudsChild
if ($null -eq $child) {
  if ($mode -eq '--check') { exit 3 }
  Say 'no buds HID child node'
  exit 3
}

$svc0 = (Get-PnpDeviceProperty -InstanceId $child.InstanceId -KeyName 'DEVPKEY_Device_Service').Data
$pc0  = (Get-PnpDeviceProperty -InstanceId $child.InstanceId -KeyName 'DEVPKEY_Device_ProblemCode').Data
$broken = Test-Broken $child

if ($mode -eq '--check') {
  Write-Output ('child: ' + $child.InstanceId)
  Write-Output ('  status=' + $child.Status + ' pc=' + $pc0 + ' svc=[' + $svc0 + '] broken=' + $broken)
  if ($broken) { exit 10 } else { exit 0 }
}

Say ('child: ' + $child.InstanceId + '  status=' + $child.Status + '  pc=' + $pc0 + '  svc=[' + $svc0 + ']')
if (-not $broken) { Say 'binding already good, nothing to do'; exit 0 }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Say 'need admin'; exit 2 }
if (-not (Test-Path $INF)) { Say 'input.inf not found'; exit 5 }

if (-not ('BudsHidBind' -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class BudsHidBind {
  [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  public static extern bool UpdateDriverForPlugAndPlayDevicesW(
      IntPtr hwndParent, string hardwareId, string fullInfPath,
      int installFlags, out bool rebootRequired);
}
'@
}

Say ('forcing input.inf onto ' + $HWID)
$reboot = $false
$ok = $false
try {
  $ok = [BudsHidBind]::UpdateDriverForPlugAndPlayDevicesW([IntPtr]::Zero, $HWID, $INF, (0x1 -bor 0x4), [ref]$reboot)
} catch { Say ('  call failed: ' + $_.Exception.Message) }
Say ('  result=' + $ok + ' reboot=' + $reboot + ' err=' + [Runtime.InteropServices.Marshal]::GetLastWin32Error())

Start-Sleep -Seconds 3
$sc = (pnputil /scan-devices 2>&1 | Out-String)
Say ('  scan: ' + ($sc -replace "`r`n", ' ').Trim())
Start-Sleep -Seconds 5

$after = Get-BudsChild
if ($null -eq $after) { Say 'AFTER: node missing'; exit 5 }
$svc2 = (Get-PnpDeviceProperty -InstanceId $after.InstanceId -KeyName 'DEVPKEY_Device_Service').Data
$inf2 = (Get-PnpDeviceProperty -InstanceId $after.InstanceId -KeyName 'DEVPKEY_Device_DriverInfPath').Data
$pc2  = (Get-PnpDeviceProperty -InstanceId $after.InstanceId -KeyName 'DEVPKEY_Device_ProblemCode').Data
Say ('AFTER: ' + $after.Status + '  pc=' + $pc2 + '  svc=[' + $svc2 + ']  inf=' + $inf2)

if (Test-Broken $after) { Say 'RESULT: STILL BROKEN'; exit 5 } else { Say 'RESULT: OK'; exit 0 }