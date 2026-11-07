$ErrorActionPreference = 'SilentlyContinue'
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)
$MATCH = 'REDMI Buds 6 Pro'

Write-Output ''
Write-Output '=== 1) environment ==='
$node = (where.exe node 2>$null | Select-Object -First 1)
Write-Output ('node      : ' + $(if ($node) { $node } else { 'NOT FOUND - install Node.js LTS' }))
Write-Output ('HidReader : ' + (Test-Path 'HidReader.exe'))
Write-Output ('KeepAlive : ' + (Test-Path 'KeepAlive.exe'))

Write-Output ''
Write-Output '=== 2) render endpoints ==='
if (Test-Path 'KeepAlive.exe') { & '.\KeepAlive.exe' --list }

Write-Output ''
Write-Output '=== 3) buds HID node ==='
$hid = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like 'HID\*0094*' -or $_.InstanceId -like '*VID_00010094*' }
if (-not $hid) {
  Write-Output '  none - the head-tracker HID node is missing'
  Write-Output '  -> pair the buds, then check again'
} else {
  foreach ($d in $hid) {
    $svc = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_Service').Data
    $inf = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName 'DEVPKEY_Device_DriverInfPath').Data
    Write-Output ('  ' + $d.Status + '  svc=[' + $svc + ']  inf=' + $inf)
    Write-Output ('     ' + $d.InstanceId)
  }
}

Write-Output ''
Write-Output '=== 4) audio sessions on the buds endpoint ==='
if (Test-Path 'KeepAlive.exe') { & '.\KeepAlive.exe' --sessions }

Write-Output ''
Write-Output 'done. press Enter to close'
Read-Host | Out-Null