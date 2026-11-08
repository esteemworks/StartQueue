<#
.SYNOPSIS
    Tests in Windows Sandbox that StartQueue starts at sign-in in exactly one way (autostart-test.ps1).
.DESCRIPTION
    Through the family's DevKit (EsteemWorks.Kit\tools\sandbox, next to this repository): the newest setup from
    .\build.ps1 -Setup is copied to the work folder first, since another build may run in the same tree meanwhile, and goes to
    C:\sq in a fresh sandbox without network. Never brings a window to the front.
.EXAMPLE
    .\tests\sandbox\run-autostart-test.ps1 -WorkFolder $env:TEMP\sq-autostart
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    [int]$TimeoutMinutes = 15,
    [int]$WaitMinutes = 60,
    [switch]$Keep
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$setup = Get-ChildItem (Join-Path $root 'out\setup') -Filter 'StartQueue-Setup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $setup) { throw 'Build the setup first: .\build.ps1 -Setup' }
New-Item -ItemType Directory -Force $WorkFolder | Out-Null
$copy = Join-Path $WorkFolder $setup.Name
Copy-Item $setup.FullName $copy -Force
$runner = Join-Path $root '..\EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1'
& $runner -App StartQueue -Short sq -Setup $copy -TestScript (Join-Path $PSScriptRoot 'autostart-test.ps1') `
    -WorkFolder (Join-Path $WorkFolder 'run') -TimeoutMinutes $TimeoutMinutes -WaitMinutes $WaitMinutes -Keep:$Keep
