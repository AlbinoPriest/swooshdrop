$ErrorActionPreference = 'Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$test=Join-Path $PSScriptRoot 'obj\SetupTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe ('/out:'+$test) ('/reference:'+(Join-Path $PSScriptRoot 'dist\WinDropSetup.exe')) (Join-Path $PSScriptRoot 'tests\SetupTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup test compilation failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\WinDropSetup.exe') -Destination (Join-Path $PSScriptRoot 'obj\WinDropSetup.exe') -Force
& $test
if ($LASTEXITCODE -ne 0) { throw 'Setup tests failed' }
