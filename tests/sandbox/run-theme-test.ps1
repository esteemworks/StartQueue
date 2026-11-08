<#
.SYNOPSIS
    Tests StartQueue's colors in Windows Sandbox (theme-test.ps1): the window follows a switch of Windows' color mode
    while it is open, and the saved choice holds from every start (STANDARD.md section 1).
.DESCRIPTION
    Uses the family's sandbox runner (EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1, next to this repository):
    the newest setup in out\setup goes to C:\sq in a fresh sandbox without network. The window opens inside the
    sandbox only; nothing comes to the front on this PC.
.EXAMPLE
    .\tests\sandbox\run-theme-test.ps1 -WorkFolder $env:TEMP\sq-theme
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    [int]$TimeoutMinutes = 15
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$setup = Get-ChildItem (Join-Path $root 'out\setup') -Filter 'StartQueue-Setup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $setup) { throw 'Build the setup first: .\build.ps1 -Setup' }
$runner = Join-Path $root '..\EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1'
& $runner -App StartQueue -Short sq -Setup $setup.FullName -TestScript (Join-Path $PSScriptRoot 'theme-test.ps1') `
    -WorkFolder $WorkFolder -TimeoutMinutes $TimeoutMinutes
