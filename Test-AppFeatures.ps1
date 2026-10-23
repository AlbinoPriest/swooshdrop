$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $PSScriptRoot 'obj\AppFeatureTests.exe'
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
& (Join-Path $framework 'csc.exe') /nologo /target:exe ('/out:' + $output) (Join-Path $PSScriptRoot 'AppFeatures.cs') (Join-Path $PSScriptRoot 'tests\AppFeatureTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'App feature test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'App feature tests failed.' }
