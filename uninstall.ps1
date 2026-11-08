<#
.SYNOPSIS
    Removes a StartQueue installed with install.ps1 and gives every taken-over entry back to the Windows autostart.
    Installs made with setup.exe are removed under Settings > Apps instead; both run the same StartQueue --uninstall.
    Your list stays in %APPDATA%\StartQueue unless -RemoveData is given.
#>
[CmdletBinding()]
param(
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\StartQueue'
$exe = Join-Path $destination 'StartQueue.exe'

if (Test-Path $exe) {
    Write-Host 'Returning entries to Windows, removing sign-in start and context menu'
    Start-Process $exe -ArgumentList '--uninstall' -Wait
    Start-Sleep -Milliseconds 500
}
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
if ($RemoveData) { Remove-Item (Join-Path $env:APPDATA 'StartQueue') -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host 'StartQueue removed.'
