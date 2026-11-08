<#
.SYNOPSIS
    Tests StartQueue's own update in Windows Sandbox (update-test.ps1): the older setup finds a newer one in a local
    feed, installs it quietly and comes back to the tray with --updated.
.DESCRIPTION
    Uses the family's sandbox runner (EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1, next to this repository):
    the newest setup in out\setup is the one installed first, -NewerSetup the one it updates to (build it with a
    higher <Version> in the project). Never brings a window to the front.
.EXAMPLE
    .\tests\sandbox\run-update-test.ps1 -WorkFolder $env:TEMP\sq-update -NewerSetup $env:TEMP\StartQueue-Setup-1.5.3.exe
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    [Parameter(Mandatory)] [string]$NewerSetup,
    [int]$TimeoutMinutes = 20
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$setup = Get-ChildItem (Join-Path $root 'out\setup') -Filter 'StartQueue-Setup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $setup) { throw 'Build the setup first: .\build.ps1 -Setup' }
$upd = Join-Path ([IO.Path]::GetTempPath()) "startqueue-upd-$PID"
New-Item -ItemType Directory -Force $upd | Out-Null
Copy-Item $NewerSetup $upd
$runner = Join-Path $root '..\EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1'
& $runner -App StartQueue -Short sq -Setup $setup.FullName -TestScript (Join-Path $PSScriptRoot 'update-test.ps1') `
    -WorkFolder $WorkFolder -Extra @{ upd = $upd } -TimeoutMinutes $TimeoutMinutes
Remove-Item $upd -Recurse -Force
