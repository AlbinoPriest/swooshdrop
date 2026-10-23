$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$arguments = @('/nologo','/target:exe',('/out:' + (Join-Path $PSScriptRoot 'dist\ReceiveUiTests.exe')),('/reference:' + (Join-Path $PSScriptRoot 'dist\SwooshDrop.exe')))
foreach ($assembly in @('System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')) { $arguments += '/reference:' + (Join-Path $framework $assembly) }
$arguments += (Join-Path $PSScriptRoot 'tests\ReceiveUiTests.cs')
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Receive UI tests did not compile.' }
& (Join-Path $PSScriptRoot 'dist\ReceiveUiTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Receive UI tests failed.' }
