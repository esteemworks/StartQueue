<#
.SYNOPSIS
    Installs or updates StartQueue from a build folder, the same way setup.exe does.
.DESCRIPTION
    For development on the build machine; other devices get the setup from build.ps1 -Setup.
    Copies the app to %LOCALAPPDATA%\Programs\StartQueue and lets StartQueue set itself up (--setup):
    start at sign-in and context menu for the current user, and for administrators also the admin features
    (elevated helper task, Windows 11 context menu), unless -UserMode is given.
#>
[CmdletBinding()]
param(
    [string]$Source = $(if (Test-Path (Join-Path $PSScriptRoot 'StartQueue.exe')) { $PSScriptRoot } else { Join-Path $PSScriptRoot 'out\app' }),
    [switch]$UserMode
)

$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\StartQueue'
$exe = Join-Path $destination 'StartQueue.exe'
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not (Test-Path (Join-Path $Source 'StartQueue.exe'))) { throw "No build found in $Source. Run build.ps1 first." }

Write-Host 'Stopping StartQueue'
if (Test-Path $exe) {
    $version = (Get-Item $exe).VersionInfo.FileVersion
    if ($version -like '1.0.*' -or $version -like '1.1.*') {
        # Versions before the setup do not know --prepare-update.
        $shellDll = Join-Path $destination 'StartQueueShell.dll'
        Get-Process StartQueue -ErrorAction SilentlyContinue | Stop-Process -Force
        Get-AppxPackage -Name 'StartQueue.ContextMenu' | Remove-AppxPackage
        Get-Process dllhost -ErrorAction SilentlyContinue | Where-Object {
            try { $_.Modules.FileName -contains $shellDll } catch { $false }
        } | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    else {
        Start-Process $exe -ArgumentList '--prepare-update' -Wait
    }
    Start-Sleep -Milliseconds 500
}

if ([IO.Path]::GetFullPath($Source).TrimEnd('\') -ne [IO.Path]::GetFullPath($destination).TrimEnd('\')) {
    Write-Host "Copying to $destination"
    New-Item -ItemType Directory -Force $destination | Out-Null
    robocopy $Source $destination /MIR /NFL /NDL /NJH /NJS /NP /XF install.ps1 uninstall.ps1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy failed (robocopy $LASTEXITCODE)" }
}

Write-Host ('Setting up{0}' -f $(if ($UserMode) { ' (user mode)' } else { '' }))
$arguments = if ($UserMode) { '--setup' } else { '--setup --admin' }
Start-Process $exe -ArgumentList $arguments -Wait
Get-Content (Join-Path $env:APPDATA 'StartQueue\StartQueue.log') -Tail 8 | Where-Object { $_ -match 'Sign-in|Context menu|Admin task|Setup done|not set up' } |
    ForEach-Object { Write-Host "  $($_ -replace '^\S+ \S+ \[[^\]]+\] ', '')" }

Write-Host 'Starting StartQueue'
if ($elevated) {
    # Explorer starts it with normal rights, even though this script runs elevated.
    Start-Process explorer.exe -ArgumentList "`"$exe`""
}
else {
    Start-Process $exe
}
Write-Host 'Installed.'
exit 0
