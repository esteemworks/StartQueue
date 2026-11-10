<#
.SYNOPSIS
    Tests the first start of a new install in Windows Sandbox (firstrun-test.ps1): the introduction, looking around,
    switching on, a full setup through the admin helper, an update of 1.6.0 and --switch-on.
.DESCRIPTION
    Through the family's DevKit (EsteemWorks.Kit\tools\sandbox, next to this repository): the newest setup from
    .\build.ps1 -Setup is copied to the work folder first, since another build may run in the same tree meanwhile, and goes
    to C:\sq in a fresh sandbox without network, the released setup of 1.6.0 to C:\sq\old. Never brings a window to the
    front. The pictures land in <WorkFolder>\run\results.
.EXAMPLE
    .\tests\sandbox\run-firstrun-test.ps1 -WorkFolder $env:TEMP\sq-firstrun -OldSetup $env:TEMP\sq-firstrun\old
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    # A folder with the released setup to update from (StartQueue-Setup-1.6.0.exe).
    [Parameter(Mandatory)] [string]$OldSetup,
    [int]$TimeoutMinutes = 30,
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
& $runner -App StartQueue -Short sq -Setup $copy -TestScript (Join-Path $PSScriptRoot 'firstrun-test.ps1') -Extra @{ old = (Resolve-Path $OldSetup).Path } `
    -WorkFolder (Join-Path $WorkFolder 'run') -TimeoutMinutes $TimeoutMinutes -WaitMinutes $WaitMinutes -Keep:$Keep
