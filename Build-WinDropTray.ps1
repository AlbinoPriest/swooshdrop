$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'WinDropTray.cs'))) -Language CSharp -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions -OutputAssembly (Join-Path $PSScriptRoot 'WinDropTray.exe') -OutputType WindowsApplication
