<#
.SYNOPSIS
    Tests the family's profiles in Windows Sandbox (profiles-test.ps1): StartQueue moves the profiles of 1.5 to
    %APPDATA%\EsteemWorks\profiles.json, follows a profile another app chose by hand, and takes over a profile another
    app offers.
.DESCRIPTION
    Uses the family's sandbox runner (EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1, next to this repository):
    the newest setup in out\setup and tests\fixtures go to C:\sq in a fresh sandbox without network. The window opens
    inside the sandbox only; nothing comes to the front on this PC.
.EXAMPLE
    .\tests\sandbox\run-profiles-test.ps1 -WorkFolder $env:TEMP\sq-profiles
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
& $runner -App StartQueue -Short sq -Setup $setup.FullName -TestScript (Join-Path $PSScriptRoot 'profiles-test.ps1') `
    -WorkFolder $WorkFolder -Extra @{ fx = (Join-Path $root 'tests\fixtures') } -TimeoutMinutes $TimeoutMinutes
