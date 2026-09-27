param([ValidateSet('rtl8xxxu','8188eu')][string]$Driver = '8188eu')
$ErrorActionPreference = 'Stop'
$env:WSL_UTF8 = '1'
$usbipd = 'C:\Program Files\usbipd-win\usbipd.exe'
$targetId = 'USB\VID_0BDA&PID_8179\00E04C0001'
$state = (& $usbipd state) | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Cannot read USB state.' }
$devices = @($state.Devices | Where-Object { $_.InstanceId -eq $targetId })
if ($devices.Count -ne 1 -or -not $devices[0].BusId) { throw 'Expected TP-Link adapter is not plugged in.' }
$busId = $devices[0].BusId
$linuxScript = Join-Path $PSScriptRoot 'airdrop-lab.sh'
$wslPath = (& wsl -d AirDropLab -u root --exec wslpath -u $linuxScript).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the lab script in WSL.' }
$controlScript = Join-Path $PSScriptRoot 'airdrop-lab-control.sh'
$controlPath = (& wsl -d AirDropLab -u root --exec wslpath -u $controlScript).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the lab helper in WSL.' }
& wsl -d AirDropLab -u root --exec bash $controlPath available
if ($LASTEXITCODE -ne 0) { throw 'An AirDrop lab session is already running.' }
if ($devices[0].ClientIPAddress) {
    & wsl -d AirDropLab -u root --exec bash $controlPath attached
    if ($LASTEXITCODE -ne 0) { throw 'The adapter is attached elsewhere. Detach it before starting the lab.' }
} else {
    & $usbipd attach --wsl AirDropLab --busid $busId
    if ($LASTEXITCODE -ne 0) { throw 'USB attachment failed.' }
}
$labExit = 0
try {
    & wsl -d AirDropLab -u root --exec setsid --wait bash $wslPath $Driver
    $labExit = $LASTEXITCODE
    if ($labExit -notin @(0, 124, 130, 143)) { throw "Lab failed (exit $labExit). See outputs\logs." }
} finally {
    if ($labExit -ne 75) { & $usbipd detach --busid $busId }
}
