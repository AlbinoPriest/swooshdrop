param([switch]$Launch, [string]$ImportFolder)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist\WinDrop.exe'
if (-not (Test-Path -LiteralPath $source)) { throw 'Build WinDrop first with Build-WinDrop.ps1.' }
$installFolder = Join-Path $env:LOCALAPPDATA 'Programs\WinDrop'
$destination = Join-Path $installFolder 'WinDrop.exe'
if (Get-Process WinDrop -ErrorAction SilentlyContinue | Where-Object Path -EQ $destination) { throw 'Quit WinDrop from its tray menu before installing an update.' }
New-Item -ItemType Directory -Force $installFolder | Out-Null
New-Item -ItemType Directory -Force (Join-Path $installFolder 'Data'),(Join-Path $installFolder 'Data\Runtime'),(Join-Path $installFolder 'Data\Previews'),(Join-Path $installFolder 'Data\Logs') | Out-Null
Copy-Item -LiteralPath $source -Destination $destination
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'WinDrop.lnk'))
$shortcut.TargetPath = $destination; $shortcut.WorkingDirectory = $installFolder; $shortcut.Description = 'Receive AirDrop on Windows'; $shortcut.IconLocation = "$destination,0"; $shortcut.Save()
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'NativeIntegration.cs')))
[NativeIntegration]::Register($destination)
if ([NativeIntegration]::IsAutostartEnabled()) { [NativeIntegration]::SetAutostart($destination,$true) }
if ($Launch) {
    if ($ImportFolder) { Start-Process -FilePath $destination -ArgumentList @('--import',('"' + $ImportFolder + '"')) }
    else { Start-Process -FilePath $destination }
}
Write-Output "Installed: $destination"
