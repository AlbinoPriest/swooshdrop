$ErrorActionPreference = 'Stop'
$env:WSL_UTF8 = '1'
$controlScript = Join-Path $PSScriptRoot 'airdrop-lab-control.sh'
$controlPath = (& wsl -d AirDropLab -u root --exec wslpath -u $controlScript).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the lab helper in WSL.' }
& wsl -d AirDropLab -u root --exec bash $controlPath stop
$usbipd = 'C:\Program Files\usbipd-win\usbipd.exe'
$state = (& $usbipd state) | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Cannot read USB state.' }
$device = @($state.Devices | Where-Object { $_.InstanceId -eq 'USB\VID_0BDA&PID_8179\00E04C0001' })
if ($device.Count -eq 1 -and $device[0].BusId -and $device[0].ClientIPAddress) {
    & $usbipd detach --busid $device[0].BusId
}
