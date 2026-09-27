$ErrorActionPreference = 'Stop'
$usbipd = 'C:\Program Files\usbipd-win\usbipd.exe'
$targetId = 'USB\VID_0BDA&PID_8179\00E04C0001'
$log = Join-Path $PSScriptRoot 'adapter-setup.log'
Start-Transcript -Path $log -Append
try {
    $state = (& $usbipd state | Out-String | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect USB devices.' }
    $devices = @($state.Devices | Where-Object InstanceId -EQ $targetId)
    if ($devices.Count -ne 1 -or -not $devices[0].BusId) {
        throw 'The expected TP-Link RTL8188EU adapter is not connected.'
    }
    $device = $devices[0]
    Write-Host ('Sharing only: ' + $device.Description + ' at bus ' + $device.BusId)
    & $usbipd bind --busid $device.BusId
    if ($LASTEXITCODE -ne 0) { throw 'USB binding failed.' }
    Write-Host 'Adapter is shared. Linux attachment is performed separately.'
} finally {
    Stop-Transcript
}
