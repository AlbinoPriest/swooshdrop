param([string]$BackendDirectory = '\\wsl.localhost\AirDropLab\opt\airdrop-lab\receiver-product')
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'dist'
$build = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Force $output,$build | Out-Null
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 64,64
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([Drawing.Color]::FromArgb(238,114,93))
$pen = New-Object Drawing.Pen ([Drawing.Color]::White),5
$pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
$graphics.DrawLine($pen,32,13,32,43); $graphics.DrawLine($pen,19,32,32,45); $graphics.DrawLine($pen,32,45,45,32)
$graphics.DrawLine($pen,18,52,46,52)
$iconPng = Join-Path $build 'AppIcon.png'; $bitmap.Save($iconPng,[Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $pen.Dispose(); $bitmap.Dispose()
$imageBytes = [IO.File]::ReadAllBytes($iconPng)
$iconFile = Join-Path $build 'AppIcon.ico'
$stream = [IO.File]::Create($iconFile); $writer = New-Object IO.BinaryWriter $stream
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$imageBytes.Length); $writer.Write([uint32]22); $writer.Write($imageBytes); $writer.Dispose()
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:' + (Join-Path $output 'SwooshDrop.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),('/win32icon:' + $iconFile))
foreach ($assembly in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')) { $arguments += '/reference:' + (Join-Path $framework $assembly) }
foreach ($resource in @('airdrop-lab.sh','airdrop-lab-control.sh','render-preview.py','Show-Toast.ps1')) { $arguments += '/resource:' + (Join-Path $PSScriptRoot $resource) + ',Runtime.' + $resource }
foreach ($file in @('windrop.dll','windrop.deps.json','windrop.runtimeconfig.json','WinDrop.Protocol.dll')) {
    $path = Join-Path $BackendDirectory $file
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing backend file: $path. Build the product backend first." }
    $arguments += '/resource:' + $path + ',Runtime.' + $file
}
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'ui\MainWindow.xaml') + ',UI.MainWindow.xaml'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'ui\TransferPopup.xaml') + ',UI.TransferPopup.xaml'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'ui\TrayFlyout.xaml') + ',UI.TrayFlyout.xaml'
$arguments += '/resource:' + $iconPng + ',UI.AppIcon.png'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'LICENSE') + ',Notices.App-LICENSE.txt'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'licenses\WinDrop-LICENSE.txt') + ',Notices.Protocol-LICENSE.txt'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'adapters.json') + ',Config.adapters.json'
$arguments += (Join-Path $PSScriptRoot 'WinDropTray.cs'),(Join-Path $PSScriptRoot 'NativeIntegration.cs'),(Join-Path $PSScriptRoot 'ToastActivation.cs'),(Join-Path $PSScriptRoot 'AdapterCatalog.cs')
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'SwooshDrop compilation failed.' }
Get-Item (Join-Path $output 'SwooshDrop.exe') | Select-Object FullName,Length
