param([switch]$SkipAppBuild)
$ErrorActionPreference = 'Stop'
if (-not $SkipAppBuild) { & (Join-Path $PSScriptRoot 'Build-WinDrop.ps1') }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $PSScriptRoot 'dist\SwooshDropSetup.exe'
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+$output),('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')),('/win32icon:'+(Join-Path $PSScriptRoot 'obj\AppIcon.ico')))
foreach ($assembly in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll')) { $arguments += '/reference:'+(Join-Path $framework $assembly) }
$resources = @{
 'dist\SwooshDrop.exe' = 'Setup.SwooshDrop.exe'
 'obj\AppIcon.ico' = 'Setup.AppIcon.ico'
 'obj\runtime-payload.tar.gz' = 'Setup.runtime-payload.tar.gz'
 'obj\corresponding-source.tar.gz' = 'Setup.corresponding-source.tar.gz'
 'scripts\bootstrap-runtime.sh' = 'Setup.bootstrap-runtime.sh'
 'adapters.json' = 'Setup.adapters.json'
 'THIRD-PARTY-NOTICES.md' = 'Setup.NOTICES.txt'
}
foreach ($file in $resources.Keys) { $path = Join-Path $PSScriptRoot $file; if (-not (Test-Path -LiteralPath $path)) { throw "Missing build input: $file" }; $arguments += '/resource:'+$path+','+$resources[$file] }
$arguments += (Join-Path $PSScriptRoot 'WinDropSetup.cs'),(Join-Path $PSScriptRoot 'NativeIntegration.cs'),(Join-Path $PSScriptRoot 'AdapterCatalog.cs')
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
Get-Item $output | Select-Object FullName,Length
